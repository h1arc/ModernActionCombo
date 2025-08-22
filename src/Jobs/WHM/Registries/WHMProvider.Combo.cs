using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Jobs.WHM.Data;

namespace ModernActionCombo.Jobs.WHM;

/// <summary>
/// WHM Provider - Combo Processing Implementation
/// Separated into its own file for maintainability.
/// </summary>
public partial class WHMProvider // : IComboProvider is already declared in main file
{
    #region IComboProvider Implementation
    
    public IReadOnlyList<ComboGrid> GetComboGrids()
    {
        return new[]
        {
            // Single Target DPS Grid - with lily overcap protection, Presence of Mind and Glare IV support
            new ComboGrid("Single Target DPS",
                [25859, 16533, 3568, 127, 119], // Glare III, Glare, Stone III, Stone II, Stone
                [
                    (s => HasLilyOvercapRisk(),       WHMConstants.AfflatusRapture, "Lily Overcap Protection - Use Afflatus Rapture"),
                    (s => HasBloodLilyReady(),        WHMConstants.AfflatusMisery, "Use Afflatus Misery (Blood Lily Ready)"),
                    (s => HasSacredSight(),           WHMConstants.Glare4, "Use Glare IV with Sacred Sight"),
                    (s => GameStateCache.IsMoving,    _ => GetBestDoTAction(), "Apply/Refresh DoT (while moving)"),
                    (s => NeedsDoTRefresh(),          _ => GetBestDoTAction(), "Apply/Refresh DoT (stationary)"),
                    (s => true,                       _ => GetBestSTAction(), "Default Glare")
                ])
        };
    }

    #region Ultra-Fast OGCD Resolution System
    
    /// <summary>
    /// Ultra-optimized OGCD rules using direct cache access for maximum performance.
    /// Target: <3ns per rule evaluation, <10ns total for 3 rules.
    /// No GameStateData allocation needed.
    /// </summary>
    private static readonly OGCDResolver.DirectCacheOGCDRule[] _ultraFastOGCDRules = [
        // Priority 1 (Critical): Resource management
        OGCDResolver.CreateDirectRule(
            static () => GameStateCache.CurrentMp <= 6500 && GameStateCache.IsOGCDReady(WHMConstants.LucidDreaming),
            static () => WHMConstants.LucidDreaming, 1),
            
        // Priority 2 (High): Damage + utility
        OGCDResolver.CreateDirectRule(
            static () => GameStateCache.IsOGCDReady(WHMConstants.Assize),
            static () => WHMConstants.Assize, 2),
            
        // Priority 3 (Medium): DPS optimization
        OGCDResolver.CreateDirectRule(
            static () => CanUsePresenceOfMindStatic(),
            static () => WHMConstants.PresenceOfMind, 3)
    ];
    
    /// <summary>
    /// Static version for use in static delegates
    /// </summary>
    private static bool CanUsePresenceOfMindStatic()
    {
        return !GameStateCache.HasPlayerBuff(WHMConstants.PresenceOfMindBuffId)
               && GameStateCache.IsOGCDReady(WHMConstants.PresenceOfMind);
    }

    /// <summary>
    /// Ultra-fast OGCD suggestion using direct cache access.
    /// Zero-allocation evaluation with <10ns total time for 3 rules.
    /// No snapshot needed - 30ms cache refresh is sufficient for OGCD timing.
    /// </summary>
    public IEnumerable<uint> GetSuggestedOGCDs()
    {
        // Fast early exits using centralized combo eligibility
        if (!GameStateCache.CanProcessCombos)
            yield break;
            
        if (!GameStateCache.CanWeave())
            yield break;

        // Direct cache evaluation - zero allocation
        // Use array for results (can't use Span in yield method)
        var results = new uint[2]; // Max 2 OGCDs
        var resultsSpan = results.AsSpan();
        
        // Ultra-fast evaluation using direct cache access
        int count = OGCDResolver.EvaluateOGCDsDirect(_ultraFastOGCDRules.AsSpan(), resultsSpan);
        
        // Return results
        for (int i = 0; i < count; i++)
        {
            yield return results[i];
        }
    }
    #endregion
    
    #region Combo Logic Helpers

    // Cached actions for hot path performance - updated only when level changes
    private static uint _cachedLevel = 0;
    private static uint _cachedBestSt = 119u;  // Default to Stone
    private static uint _cachedBestDot = 0u;      // Default to no DoT
    private static uint _cachedDotDebuffId = 0u;  // Corresponding debuff ID
    
    /// <summary>
    /// Updates cached actions when level changes. Call this sparingly.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateCachedActions()
    {
        var currentLevel = GameStateCache.Level;
        if (currentLevel == _cachedLevel) return; // No change, hot path exit
        
        _cachedLevel = currentLevel;
        
        // Update best Glare action
        _cachedBestSt = currentLevel switch
        {
            >= 72 => 25859u, // Glare III
            >= 64 => 16533u, // Glare
            >= 18 => 3568u,  // Stone III
            >= 2 => 127u,    // Stone II
            _ => 119u        // Stone
        };
        
        // Update best DoT action and corresponding debuff ID
        (_cachedBestDot, _cachedDotDebuffId) = currentLevel switch
        {
            >= 72 => (16532u, 1871u), // Dia + Dia debuff
            >= 46 => (132u, 144u),    // Aero II + Aero II debuff
            >= 4 => (121u, 143u),     // Aero + Aero debuff
            _ => (0u, 0u)             // No DoT available
        };
    }
    
    /// <summary>
    /// Hot path: Gets best Glare action with cached lookup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBestSTAction()
    {
        UpdateCachedActions();
        return _cachedBestSt;
    }
    
    /// <summary>
    /// Hot path: Gets best DoT action with cached lookup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBestDoTAction()
    {
        UpdateCachedActions();
        return _cachedBestDot;
    }
    
    private static bool HasLilyOvercapRisk() => WHMJobGauge.HasOvercapRisk;
    
    private static bool NeedsDoTRefresh()
    {
        UpdateCachedActions();
        if (_cachedBestDot == 0) 
        {
            return false; // No DoT available at this level
        }
        
        var timeRemaining = GameStateCache.GetTargetDebuffTimeRemaining(_cachedDotDebuffId);
        var needsRefresh = timeRemaining == GameStateCache.UNINITIALIZED_SENTINEL || timeRemaining <= 3.0f;
        
        
        return needsRefresh;
    }
    
    private static bool HasBloodLilyReady() => WHMJobGauge.BloodLilyReady;
    
    private static bool HasSacredSight()
    {
        return GameStateCache.HasPlayerBuff(WHMConstants.SacredSightBuffId);
    }

    private static bool CanUsePresenceOfMind()
    {
        return !GameStateCache.HasPlayerBuff(WHMConstants.PresenceOfMindBuffId)
               && GameStateCache.IsActionReady(WHMConstants.PresenceOfMind);
    }

    #endregion
    
    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles level changes by invalidating the cached actions.
    /// This ensures we use the correct abilities when leveling up.
    /// </summary>
    public void OnLevelChanged(uint newLevel)
    {
        // Force cache invalidation by resetting the cached level
        _cachedLevel = 0;
        ModernActionCombo.PluginLog?.Debug($"🔮 WHM: Cache invalidated for level change to {newLevel}");
    }
    
    /// <summary>
    /// Handles duty state changes. WHM rotation doesn't significantly change between duties,
    /// but we could add duty-specific logic here if needed.
    /// </summary>
    public void OnDutyStateChanged(bool inDuty, uint? dutyId)
    {
        // WHM doesn't need special duty handling currently, but we could add:
        // - Different opener priorities in raids vs dungeons
        // - Lily management based on fight length expectations
        ModernActionCombo.PluginLog?.Debug($"🔮 WHM: Duty state changed - in duty: {inDuty}, duty ID: {dutyId}");
    }
    
    /// <summary>
    /// Handles combat state changes. WHM doesn't need special pre-combat setup,
    /// but we could add combat preparation logic here if needed.
    /// </summary>
    public void OnCombatStateChanged(bool inCombat)
    {
        // WHM doesn't need special combat handling currently, but we could add:
        // - Pre-combat lily management
        // - Post-combat cooldown reset tracking
        ModernActionCombo.PluginLog?.Debug($"🔮 WHM: Combat state changed - in combat: {inCombat}");
    }
    
    #endregion
    
    #region Partial Method Implementation
    
    private partial string GetComboInfo()
    {
        var grids = GetComboGrids();
        return $"Combos: {grids.Count} grids";
    }
    
    #endregion
}
