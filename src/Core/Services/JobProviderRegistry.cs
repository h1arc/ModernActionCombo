using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ModernActionCombo.Core.Attributes;
using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Unified registry for all job providers.
/// Replaces JobComboRegistry, JobGaugeRegistry, and JobTrackingDataRegistry.
/// Automatically discovers and manages IJobProvider implementations with composition interfaces.
/// </summary>
public static class JobProviderRegistry

{
    private static readonly Dictionary<uint, IJobProvider> _providers = new();
    private static bool _initialized = false;
    private static IJobProvider? _activeProvider;
    
    /// <summary>
    /// Initialize the registry by scanning for [JobCombo] attributed classes.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var providerTypes = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<JobComboAttribute>() != null)
                .Where(t => typeof(IJobProvider).IsAssignableFrom(t))
                .Where(t => !t.IsAbstract && !t.IsInterface);
            
            foreach (var type in providerTypes)
            {
                var attribute = type.GetCustomAttribute<JobComboAttribute>()!;
                var provider = (IJobProvider)Activator.CreateInstance(type)!;
                
                // Initialize tracking for this job
                provider.InitializeTracking();
                
                _providers[attribute.JobId] = provider;
                
                // Log capabilities
                var capabilities = new List<string>();
                if (provider.HasComboSupport()) capabilities.Add("Combo");
                if (provider.HasGaugeSupport()) capabilities.Add("Gauge");
                if (provider.HasTrackingSupport()) capabilities.Add("Tracking");
                
                ModernActionCombo.PluginLog?.Info($"✅ Registered provider for Job {attribute.JobId} ({attribute.JobName ?? "Unknown"}) - Capabilities: {string.Join(", ", capabilities)}");
            }
            
            _initialized = true;
            ModernActionCombo.PluginLog?.Info($"🚀 JobProviderRegistry initialized with {_providers.Count} providers");
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"❌ Failed to initialize JobProviderRegistry: {ex}");
        }
    }
    
    #region Job Management
    /// <summary>
    /// Called when the player's job changes to activate the appropriate provider.
    /// </summary>
    public static void OnJobChanged(uint jobId)
    {
        if (_providers.TryGetValue(jobId, out var provider))
        {
            _activeProvider = provider;
            
            // Invalidate fast resolver cache on job change
            _fastResolver = null;
            _cachedJobId = 0;
            _cachedConfigVersion = 0;
            
            ModernActionCombo.PluginLog?.Debug($"🔄 Activated provider for job {jobId}");
        }
        else
        {
            _activeProvider = null;
            _fastResolver = null;
            _cachedJobId = 0;
            _cachedConfigVersion = 0;
            ModernActionCombo.PluginLog?.Debug($"❓ No provider found for job {jobId}");
        }
    }
    
    /// <summary>
    /// Called when the player's level changes to update ability availability.
    /// This can invalidate cached action resolutions.
    /// </summary>
    public static void OnLevelChanged(uint newLevel)
    {
        // Invalidate fast resolver cache on level change
        _fastResolver = null;
        _cachedJobId = 0;
        _cachedConfigVersion = 0;
        
        // Notify all providers about level change for cache invalidation
        foreach (var provider in _providers.Values)
        {
            try
            {
                // If provider has a level change handler, call it
                if (provider is ILevelChangeHandler levelHandler)
                {
                    levelHandler.OnLevelChanged(newLevel);
                }
            }
            catch (Exception ex)
            {
                ModernActionCombo.PluginLog?.Warning($"Provider failed to handle level change: {ex.Message}");
            }
        }
        
        ModernActionCombo.PluginLog?.Debug($"📈 Level changed to {newLevel} - fast resolver and providers notified");
    }
    
    /// <summary>
    /// Called when duty state changes (entering/leaving duties).
    /// This can affect rotation priorities and available actions.
    /// </summary>
    public static void OnDutyStateChanged(bool inDuty, uint? dutyId = null)
    {
        // Notify all providers about duty state change
        foreach (var provider in _providers.Values)
        {
            try
            {
                // If provider has a duty state handler, call it
                if (provider is IDutyStateHandler dutyHandler)
                {
                    dutyHandler.OnDutyStateChanged(inDuty, dutyId);
                }
            }
            catch (Exception ex)
            {
                ModernActionCombo.PluginLog?.Warning($"Provider failed to handle duty state change: {ex.Message}");
            }
        }
        
        var stateText = inDuty ? $"entered duty {dutyId}" : "left duty";
        ModernActionCombo.PluginLog?.Debug($"🏰 Duty state changed: {stateText} - providers notified");
    }
    
    /// <summary>
    /// Called when combat state changes (entering/leaving combat).
    /// This can affect action priorities and rotations.
    /// </summary>
    public static void OnCombatStateChanged(bool inCombat)
    {
        // Notify all providers about combat state change
        foreach (var provider in _providers.Values)
        {
            try
            {
                // If provider has a combat state handler, call it
                if (provider is ICombatStateHandler combatHandler)
                {
                    combatHandler.OnCombatStateChanged(inCombat);
                }
            }
            catch (Exception ex)
            {
                ModernActionCombo.PluginLog?.Warning($"Provider failed to handle combat state change: {ex.Message}");
            }
        }
        
        var stateText = inCombat ? "entered combat" : "left combat";
        ModernActionCombo.PluginLog?.Debug($"⚔️ Combat state changed: {stateText} - providers notified");
    }
    
    /// <summary>
    /// Get the currently active job provider.
    /// </summary>
    public static IJobProvider? GetActiveProvider() => _activeProvider;
    
    /// <summary>
    /// Get provider for a specific job.
    /// </summary>
    public static IJobProvider? GetProvider(uint jobId)
    {
        return _providers.TryGetValue(jobId, out var provider) ? provider : null;
    }
    
    /// <summary>
    /// Check if a job has a registered provider.
    /// </summary>
    public static bool HasProvider(uint jobId) => _providers.ContainsKey(jobId);
    
    /// <summary>
    /// Get all registered job IDs.
    /// </summary>
    public static uint[] GetRegisteredJobIds() => _providers.Keys.ToArray();
    #endregion
    
    #region Combo Processing (from JobComboRegistry)
    
    // Ultra-fast single-dispatch cache for the hot path
    private static Func<uint, GameStateData, uint>? _fastResolver;
    private static uint _cachedJobId = 0;
    private static uint _cachedConfigVersion = 0;
    
    /// <summary>
    /// Resolve an action using the active job's combo logic.
    /// Ultra-optimized single-dispatch for <20ns resolution with config-aware caching.
    /// </summary>
    public static uint ResolveAction(uint actionId, GameStateData gameState)
    {
        // Global check: If combo processing conditions aren't met, return original action immediately
        if (!GameStateCache.CanProcessCombos)
            return actionId;
            
        var currentConfigVersion = ConfigAwareActionCache.GetConfigVersion();
        
        // Ultra-fast path: use cached resolver if job and config haven't changed
        if (_fastResolver != null && 
            gameState.JobId == _cachedJobId && 
            currentConfigVersion == _cachedConfigVersion)
        {
            return _fastResolver(actionId, gameState);
        }
        
        // Job or config changed - rebuild the fast resolver
        return RebuildFastResolver(actionId, gameState, currentConfigVersion);
    }
    
    /// <summary>
    /// Rebuilds the ultra-fast resolver when job or configuration changes.
    /// This is the cold path - only called on job/config change.
    /// </summary>
    private static uint RebuildFastResolver(uint actionId, GameStateData gameState, uint configVersion)
    {
        _cachedJobId = gameState.JobId;
        _cachedConfigVersion = configVersion;
        _fastResolver = null;
        
        if (_activeProvider?.AsComboProvider() is not IComboProvider comboProvider) 
        {
            _fastResolver = static (id, state) => id; // No-op resolver
            return actionId;
        }
        
        try
        {
            var grids = comboProvider.GetComboGrids();
            var hasOGCDSupport = _activeProvider is IOGCDProvider ogcdProvider;
            
            if (grids.Count == 1 && hasOGCDSupport)
            {
                // Ultra-fast path for single grid + OGCD jobs (like WHM)
                var grid = grids[0];
                _fastResolver = (id, state) => 
                {
                    // First check for smart target action replacement (like Liturgy → LiturgyBurst)
                    var smartResolved = SmartTargetResolver.GetResolvedActionId(id);
                    if (smartResolved != id) return smartResolved;
                    
                    if (!grid.HandlesAction(id)) return id;
                    
                    var resolvedGCD = grid.Evaluate(id, state);
                    
                    // If GCD changed, return it immediately (higher priority than OGCDs)
                    if (resolvedGCD != id) return resolvedGCD;
                    
                    // If can weave, get first OGCD directly (no LINQ allocation)
                    // BUT: Re-evaluate OGCD conditions dynamically - don't cache the result!
                    if (GameStateCache.CanWeave())
                    {
                        foreach (var ogcd in ((IOGCDProvider)_activeProvider!).GetSuggestedOGCDs())
                        {
                            return ogcd; // Return first valid OGCD (conditions already checked in GetSuggestedOGCDs)
                        }
                    }
                    
                    return resolvedGCD; // No OGCDs available or can't weave
                };
            }
            else if (grids.Count == 1)
            {
                // Fast path for single grid, no OGCD
                var grid = grids[0];
                _fastResolver = (id, state) => 
                {
                    // First check for smart target action replacement (like Liturgy → LiturgyBurst)
                    var smartResolved = SmartTargetResolver.GetResolvedActionId(id);
                    if (smartResolved != id) return smartResolved;
                    
                    return grid.HandlesAction(id) ? grid.Evaluate(id, state) : id;
                };
            }
            else if (hasOGCDSupport)
            {
                // Multi-grid with OGCD support
                _fastResolver = (id, state) => 
                {
                    // First check for smart target action replacement (like Liturgy → LiturgyBurst)
                    var smartResolved = SmartTargetResolver.GetResolvedActionId(id);
                    if (smartResolved != id) return smartResolved;
                    
                    foreach (var grid in grids)
                    {
                        if (grid.HandlesAction(id))
                        {
                            var resolvedGCD = grid.Evaluate(id, state);
                            
                            // If GCD changed, return it immediately (higher priority)
                            if (resolvedGCD != id) return resolvedGCD;
                            
                            // If can weave, dynamically check OGCD conditions
                            if (GameStateCache.CanWeave())
                            {
                                foreach (var ogcd in ((IOGCDProvider)_activeProvider!).GetSuggestedOGCDs())
                                {
                                    return ogcd; // Return first valid OGCD
                                }
                            }
                            return resolvedGCD; // No OGCDs available or can't weave
                        }
                    }
                    return id;
                };
            }
            else
            {
                // Multi-grid, no OGCD
                _fastResolver = (id, state) => 
                {
                    // First check for smart target action replacement (like Liturgy → LiturgyBurst)
                    var smartResolved = SmartTargetResolver.GetResolvedActionId(id);
                    if (smartResolved != id) return smartResolved;
                    
                    foreach (var grid in grids)
                    {
                        if (grid.HandlesAction(id))
                        {
                            return grid.Evaluate(id, state);
                        }
                    }
                    return id;
                };
            }
            
            // Execute the newly built resolver
            return _fastResolver(actionId, gameState);
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"❌ Error building fast resolver: {ex}");
            _fastResolver = static (id, state) => id; // Fallback to no-op
            return actionId;
        }
    }
    
    /// <summary>
    /// Check if the current job supports combo processing.
    /// </summary>
    public static bool CurrentJobSupportsComboProcessing() => _activeProvider != null;
    
    /// <summary>
    /// Check if the current job supports OGCD resolution.
    /// </summary>
    public static bool HasOGCDSupport() => _activeProvider is IOGCDProvider;
    
    /// <summary>
    /// Get job display info from the active provider.
    /// </summary>
    public static string GetJobDisplayInfo() => _activeProvider?.GetJobDisplayInfo() ?? "No job active";
    
    /// <summary>
    /// Get job name for display.
    /// </summary>
    public static string GetJobName(uint jobId)
    {
        if (_providers.TryGetValue(jobId, out var provider))
        {
            var type = provider.GetType();
            var attribute = type.GetCustomAttribute<JobComboAttribute>();
            return attribute?.JobName ?? $"Job {jobId}";
        }
        return $"Unknown ({jobId})";
    }
    #endregion
    
    #region Gauge Management (from JobGaugeRegistry)
    /// <summary>
    /// Update gauge for the currently active job.
    /// </summary>
    public static void UpdateActiveJobGauge()
    {
        if (_activeProvider?.AsGaugeProvider() is IGaugeProvider gaugeProvider)
        {
            try
            {
                gaugeProvider.UpdateGauge();
            }
            catch (Exception ex)
            {
                ModernActionCombo.PluginLog?.Error($"❌ Failed to update gauge for active job: {ex}");
            }
        }
    }
    
    /// <summary>
    /// Update gauge for a specific job (only if it's currently active).
    /// </summary>
    public static void UpdateGauge(uint jobId)
    {
        if (_activeProvider != null && _providers.TryGetValue(jobId, out var provider) && provider == _activeProvider)
        {
            if (provider.AsGaugeProvider() is IGaugeProvider gaugeProvider)
            {
                try
                {
                    gaugeProvider.UpdateGauge();
                }
                catch (Exception ex)
                {
                    ModernActionCombo.PluginLog?.Error($"❌ Failed to update gauge for job {jobId}: {ex}");
                }
            }
        }
    }
    
    /// <summary>
    /// Get gauge debug info for all registered providers.
    /// </summary>
    public static string GetGaugeDebugInfo()
    {
        if (!_initialized || _providers.Count == 0)
            return "No providers registered";
            
        var info = new List<string>();
        foreach (var kvp in _providers)
        {
            try
            {
                if (kvp.Value.AsGaugeProvider() is IGaugeProvider gaugeProvider)
                {
                    info.Add($"Job {kvp.Key}: {gaugeProvider.GetGaugeDebugInfo()}");
                }
                else
                {
                    info.Add($"Job {kvp.Key}: No gauge support");
                }
            }
            catch (Exception ex)
            {
                info.Add($"Job {kvp.Key}: Error - {ex.Message}");
            }
        }
        
        return string.Join("\n", info);
    }
    #endregion
    
    #region Tracking Data (from JobTrackingDataRegistry)
    /// <summary>
    /// Get all debuffs that should be tracked across all jobs.
    /// </summary>
    public static uint[] GetAllDebuffsToTrack()
    {
        var allDebuffs = new HashSet<uint>();
        foreach (var provider in _providers.Values)
        {
            try
            {
                if (provider.AsTrackingProvider() is ITrackingProvider trackingProvider)
                {
                    foreach (var debuffId in trackingProvider.GetDebuffsToTrack())
                    {
                        allDebuffs.Add(debuffId);
                    }
                }
            }
            catch (Exception ex)
            {
                ModernActionCombo.PluginLog?.Error($"❌ Error getting debuffs from provider: {ex}");
            }
        }
        return allDebuffs.ToArray();
    }
    
    /// <summary>
    /// Get all buffs that should be tracked across all jobs.
    /// </summary>
    public static uint[] GetAllBuffsToTrack()
    {
        var allBuffs = new HashSet<uint>();
        foreach (var provider in _providers.Values)
        {
            try
            {
                if (provider.AsTrackingProvider() is ITrackingProvider trackingProvider)
                {
                    foreach (var buffId in trackingProvider.GetBuffsToTrack())
                    {
                        allBuffs.Add(buffId);
                    }
                }
            }
            catch (Exception ex)
            {
                ModernActionCombo.PluginLog?.Error($"❌ Error getting buffs from provider: {ex}");
            }
        }
        return allBuffs.ToArray();
    }
    
    /// <summary>
    /// Get all cooldowns that should be tracked across all jobs.
    /// </summary>
    public static uint[] GetAllCooldownsToTrack()
    {
        var allCooldowns = new HashSet<uint>();
        foreach (var provider in _providers.Values)
        {
            try
            {
                if (provider.AsTrackingProvider() is ITrackingProvider trackingProvider)
                {
                    foreach (var actionId in trackingProvider.GetCooldownsToTrack())
                    {
                        allCooldowns.Add(actionId);
                    }
                }
            }
            catch (Exception ex)
            {
                ModernActionCombo.PluginLog?.Error($"❌ Error getting cooldowns from provider: {ex}");
            }
        }
        return allCooldowns.ToArray();
    }
    #endregion
    
    #region Utility
    /// <summary>
    /// Suggest oGCDs for the currently active job, if supported.
    /// </summary>
    public static IEnumerable<uint> GetSuggestedOGCDs()
    {
        if (_activeProvider is IOGCDProvider ogcdProvider)
            return ogcdProvider.GetSuggestedOGCDs();
        return System.Array.Empty<uint>();
    }
    /// <summary>
    /// Get debug info for all providers.
    /// </summary>
    public static string GetAllDebugInfo()
    {
        if (!_initialized || _providers.Count == 0)
            return "No providers registered";
            
        var info = new List<string>
        {
            $"=== Job Provider Registry ({_providers.Count} providers) ===",
            $"Active Provider: {(_activeProvider != null ? "Yes" : "No")}"
        };
        
        foreach (var kvp in _providers)
        {
            try
            {
                var type = kvp.Value.GetType();
                var attribute = type.GetCustomAttribute<JobComboAttribute>();
                info.Add($"Job {kvp.Key} ({attribute?.JobName ?? "Unknown"}): {kvp.Value.GetJobDisplayInfo()}");
            }
            catch (Exception ex)
            {
                info.Add($"Job {kvp.Key}: Error - {ex.Message}");
            }
        }
        
        return string.Join("\n", info);
    }
    #endregion
}
