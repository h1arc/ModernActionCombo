using System;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ModernActionCombo.Core.Data;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Action interception modes based on WrathCombo's two strategies.
/// </summary>
public enum ActionInterceptionMode : byte
{
    /// <summary>Icon replacement mode - changes the action icon but keeps the original action ID.</summary>
    IconReplacement = 0,
    /// <summary>Performance mode - directly replaces the action at execution time.</summary>
    PerformanceMode = 1
}

/// <summary>
/// High-performance action cache using .NET 9 value types and aggressive optimizations.
/// Designed for <50ns lookup times with zero allocations in hot paths.
/// Uses unsafe fixed arrays for maximum performance.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ActionCache
{
    private const int MaxCacheSize = 64; // Smaller for better cache locality
    private const long CacheTimeoutTicks = 1_000_000; // 100ms in ticks
    
    // Fixed arrays for ultra-fast access - no heap allocations
    private fixed uint _actionIds[MaxCacheSize];
    private fixed uint _resolvedIds[MaxCacheSize];
    private fixed long _timestamps[MaxCacheSize];
    private int _count;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetCached(uint actionId, out uint resolvedId)
    {
        var now = DateTime.UtcNow.Ticks;
        
        fixed (uint* actionIds = _actionIds)
        fixed (uint* resolvedIds = _resolvedIds)
        fixed (long* timestamps = _timestamps)
        {
            for (int i = 0; i < _count; i++)
            {
                if (actionIds[i] == actionId)
                {
                    // Check if cache entry is still valid
                    if (now - timestamps[i] < CacheTimeoutTicks)
                    {
                        resolvedId = resolvedIds[i];
                        return true;
                    }
                    
                    // Cache expired - remove entry
                    RemoveAtUnsafe(i, actionIds, resolvedIds, timestamps);
                    break;
                }
            }
        }
        
        resolvedId = 0;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Cache(uint actionId, uint resolvedId)
    {
        var now = DateTime.UtcNow.Ticks;
        
        fixed (uint* actionIds = _actionIds)
        fixed (uint* resolvedIds = _resolvedIds)
        fixed (long* timestamps = _timestamps)
        {
            // If cache is full, remove oldest entry
            if (_count >= MaxCacheSize)
            {
                RemoveOldestUnsafe(actionIds, resolvedIds, timestamps);
            }
            
            // Add new entry
            actionIds[_count] = actionId;
            resolvedIds[_count] = resolvedId;
            timestamps[_count] = now;
            _count++;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveAtUnsafe(int index, uint* actionIds, uint* resolvedIds, long* timestamps)
    {
        if (index >= _count) return;
        
        // Shift remaining elements using unsafe pointer arithmetic
        for (int i = index; i < _count - 1; i++)
        {
            actionIds[i] = actionIds[i + 1];
            resolvedIds[i] = resolvedIds[i + 1];
            timestamps[i] = timestamps[i + 1];
        }
        _count--;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveOldestUnsafe(uint* actionIds, uint* resolvedIds, long* timestamps)
    {
        if (_count == 0) return;
        
        int oldestIndex = 0;
        var oldestTime = timestamps[0];
        
        for (int i = 1; i < _count; i++)
        {
            if (timestamps[i] < oldestTime)
            {
                oldestTime = timestamps[i];
                oldestIndex = i;
            }
        }
        
        RemoveAtUnsafe(oldestIndex, actionIds, resolvedIds, timestamps);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _count = 0;
        // No need to clear the memory as we're just resetting the count
    }
}

/// <summary>
/// Simplified, focused ActionInterceptor based on WrathCombo's proven GetAdjustedActionId pattern.
/// Only cares about returning the correct actionId - nothing else.
/// Uses .NET 9 high-performance ActionCache for <50ns cache lookups.
/// Leverages GameStateCache for 30ms cached state data instead of live snapshots.
/// </summary>
public sealed unsafe class ActionInterceptor : IDisposable
{
    private readonly GameState _gameState;
    private readonly IGameInteropProvider _gameInteropProvider;
    
    // Current interception mode
    public static ActionInterceptionMode Mode { get; set; } = ActionInterceptionMode.IconReplacement;
    
    // Hooks for different modes
    private Hook<GetAdjustedActionDelegate>? _getAdjustedActionHook;  // Icon Replacement
    private Hook<UseActionDelegate>? _useActionHook;                  // Performance Mode
    
    // High-performance .NET 9 cache using unsafe fixed arrays with config versioning
    private ConfigAwareActionCache _cache = new();
    
    // Critical for the hook - copied exactly from WrathCombo
    private IntPtr _actionManager = IntPtr.Zero;
    
    // Delegates
    private delegate uint GetAdjustedActionDelegate(IntPtr actionManager, uint actionId);
    private delegate bool UseActionDelegate(IntPtr actionManager, uint actionType, uint actionId, ulong targetId, uint param, uint useType, int pvp, IntPtr a8);

    public ActionInterceptor(GameState gameState, IGameInteropProvider gameInteropProvider)
    {
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        _gameInteropProvider = gameInteropProvider ?? throw new ArgumentNullException(nameof(gameInteropProvider));
        
        InitializeHook();
    }

    private void InitializeHook()
    {
        try
        {
            InitializeIconReplacementHook();
            
            // Performance Mode hook will be initialized when switching modes
            ModernActionCombo.PluginLog?.Info($"✅ ActionInterceptor initialized in {Mode} mode");
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"ActionInterceptor initialization failed: {ex}");
        }
    }
    
    private void InitializeIconReplacementHook()
    {
        // Use WrathCombo's exact approach - hook from Dalamud's ActionManager address
        var actionManagerAddress = (nint)ActionManager.Addresses.GetAdjustedActionId.Value;
        
        if (actionManagerAddress == IntPtr.Zero)
        {
            ModernActionCombo.PluginLog?.Error("❌ ActionManager.Addresses.GetAdjustedActionId is null");
            return;
        }

        _getAdjustedActionHook = _gameInteropProvider.HookFromAddress<GetAdjustedActionDelegate>(
            actionManagerAddress, GetAdjustedActionDetour);
            
        if (_getAdjustedActionHook != null)
        {
            _getAdjustedActionHook.Enable();
            ModernActionCombo.PluginLog?.Debug($"✅ Icon Replacement hook enabled: 0x{actionManagerAddress:X}");
        }
        else
        {
            ModernActionCombo.PluginLog?.Error("❌ Failed to create GetAdjustedAction hook");
        }
    }
    
    private void InitializePerformanceModeHook()
    {
        // Hook UseAction for direct input
        var useActionAddress = (nint)ActionManager.Addresses.UseAction.Value;
        
        if (useActionAddress == IntPtr.Zero)
        {
            ModernActionCombo.PluginLog?.Error("❌ ActionManager.Addresses.UseAction is null");
            return;
        }

        _useActionHook = _gameInteropProvider.HookFromAddress<UseActionDelegate>(
            useActionAddress, UseActionDetour);
            
        if (_useActionHook != null)
        {
            _useActionHook.Enable();
            ModernActionCombo.PluginLog?.Debug($"✅ Performance Mode hook enabled: 0x{useActionAddress:X}");
        }
        else
        {
            ModernActionCombo.PluginLog?.Error("❌ Failed to create UseAction hook");
        }
    }

    /// <summary>
    /// The core detour method for icon replacement - copied from WrathCombo pattern but simplified.
    /// Only job: return the correct action ID.
    /// Uses GameStateCache directly for maximum performance.
    /// </summary>
    private uint GetAdjustedActionDetour(IntPtr actionManager, uint actionId)
    {
        try
        {
            // Safety checks
            if (_getAdjustedActionHook == null || actionId == 0 || actionId > 100000)
            {
                return _getAdjustedActionHook?.Original(actionManager, actionId) ?? actionId;
            }

            // Only active in Icon Replacement mode
            if (Mode != ActionInterceptionMode.IconReplacement)
            {
                return _getAdjustedActionHook.Original(actionManager, actionId);
            }

            // Check if this action might resolve to OGCDs (dynamic conditions)
            var hasOGCDSupport = JobProviderRegistry.HasOGCDSupport();
            
            // Check cache first for performance (but skip for OGCD-enabled actions)
            if (!hasOGCDSupport && _cache.TryGetCached(actionId, out uint cachedResult))
            {
                return cachedResult;
            }
            
            // Get the resolved action from our config-aware combo system using cached game state
            var gameStateData = GetGameStateFromCache();
            var resolvedActionId = JobProviderRegistry.ResolveAction(actionId, gameStateData);

            // Cache the result (but skip caching for OGCD-enabled actions due to dynamic conditions)
            if (!hasOGCDSupport)
            {
                _cache.Cache(actionId, resolvedActionId);
            }

            // Return the resolved action (or original if no change)
            return resolvedActionId;
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"Error in GetAdjustedActionDetour: {ex}");
            return _getAdjustedActionHook?.Original(actionManager, actionId) ?? actionId;
        }
    }

    /// <summary>
    /// Performance Mode detour for UseAction - directly executes resolved actions.
    /// This is the "direct input" mode that bypasses icon replacement entirely.
    /// Ultra-fast path for maximum performance.
    /// </summary>
    private bool UseActionDetour(IntPtr actionManager, uint actionType, uint actionId, ulong targetId, uint param, uint useType, int pvp, IntPtr a8)
    {
        try
        {
            // Only process normal actions (actionType 1) in Performance Mode
            if (actionType != 1 || Mode != ActionInterceptionMode.PerformanceMode)
            {
                return _useActionHook!.Original(actionManager, actionType, actionId, targetId, param, useType, pvp, a8);
            }

            // Safety checks
            if (_useActionHook == null || actionId == 0 || actionId > 100000)
            {
                return _useActionHook?.Original(actionManager, actionType, actionId, targetId, param, useType, pvp, a8) ?? false;
            }

            // Check if this action might resolve to OGCDs (dynamic conditions)
            var hasOGCDSupport = JobProviderRegistry.HasOGCDSupport();
            
            // Get resolved action - skip cache for OGCD actions due to dynamic conditions
            uint resolvedActionId;
            if (!hasOGCDSupport && _cache.TryGetCached(actionId, out uint cachedResult))
            {
                resolvedActionId = cachedResult;
            }
            else
            {
                // Get the resolved action from our config-aware combo system using cached game state
                var gameStateData = GetGameStateFromCache();
                resolvedActionId = JobProviderRegistry.ResolveAction(actionId, gameStateData);

                // Cache the result (but skip caching for OGCD-enabled actions due to dynamic conditions)
                if (!hasOGCDSupport)
                {
                    _cache.Cache(actionId, resolvedActionId);
                }
            }

            // Execute the resolved action directly
            return _useActionHook.Original(actionManager, actionType, resolvedActionId, targetId, param, useType, pvp, a8);
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"Error in UseActionDetour: {ex}");
            return _useActionHook?.Original(actionManager, actionType, actionId, targetId, param, useType, pvp, a8) ?? false;
        }
    }

    /// <summary>
    /// Creates GameStateData directly from GameStateCache for maximum performance.
    /// This avoids the overhead of GameState.CreateSnapshot() and leverages the 30ms cache.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GameStateData GetGameStateFromCache()
    {
        // Get state directly from the high-performance GameStateCache
        return new GameStateData(
            GameStateCache.JobId,
            GameStateCache.Level,
            GameStateCache.InCombat,
            GameStateCache.TargetId,
            GameStateCache.GcdRemaining
        );
    }

    public void ClearCache()
    {
        _cache.Clear();
        ModernActionCombo.PluginLog?.Debug("Config-aware action cache cleared");
    }

    /// <summary>
    /// Gets the current action interception mode.
    /// </summary>
    public ActionInterceptionMode GetCurrentMode() => Mode;

    /// <summary>
    /// Checks if Performance Mode is available and functioning.
    /// </summary>
    public bool IsPerformanceModeAvailable()
    {
        return ActionManager.Addresses.UseAction.Value != IntPtr.Zero;
    }

    /// <summary>
    /// Switches between action interception modes with proper hook management.
    /// Icon Replacement: Changes hotbar icons via GetAdjustedActionId hook
    /// Performance Mode: Direct action execution via UseAction hook for maximum performance
    /// </summary>
    public void SwitchMode(ActionInterceptionMode newMode)
    {
        if (Mode == newMode)
            return;

        var oldMode = Mode;
        
        // Disable current mode hooks
        DisableCurrentModeHooks();
        
        // Switch mode
        Mode = newMode;
        
        // Clear cache when switching modes
        ClearCache();
        
        // Enable new mode hooks
        switch (newMode)
        {
            case ActionInterceptionMode.IconReplacement:
                InitializeIconReplacementHook();
                break;
                
            case ActionInterceptionMode.PerformanceMode:
                InitializePerformanceModeHook();
                break;
                
            default:
                ModernActionCombo.PluginLog?.Warning($"⚠️ Unknown mode {newMode} - falling back to Icon Replacement");
                Mode = ActionInterceptionMode.IconReplacement;
                InitializeIconReplacementHook();
                break;
        }
        
        ModernActionCombo.PluginLog?.Info($"🔄 Action interception mode: {oldMode} → {Mode}");
    }
    
    private void DisableCurrentModeHooks()
    {
        try
        {
            _getAdjustedActionHook?.Disable();
            _useActionHook?.Disable();
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"Error disabling current mode hooks: {ex}");
        }
    }

    public void Dispose()
    {
        try
        {
            DisableCurrentModeHooks();
            _getAdjustedActionHook?.Dispose();
            _useActionHook?.Dispose();
            ClearCache();
            ModernActionCombo.PluginLog?.Info("ActionInterceptor disposed");
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"Error disposing ActionInterceptor: {ex}");
        }
    }
}
