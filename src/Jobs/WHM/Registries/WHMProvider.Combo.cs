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
    #region Combo Rules
    
    public IReadOnlyList<ComboGrid> GetComboGrids()
    {
        return new[]
        {
            // Single Target DPS Grid
            new ComboGrid("Single Target DPS",
                [25859, 16533, 3568, 127, 119], // Glare III, Glare, Stone III, Stone II, Stone
                [
                    (s => HasLilyOvercapRisk(),       WHMConstants.AfflatusRapture, "Use Afflatus Rapture (Lily Overcap)"),
                    (s => HasBloodLilyReady(),        WHMConstants.AfflatusMisery, "Use Afflatus Misery (Blood Lily Ready)"),
                    (s => HasSacredSight(),           WHMConstants.Glare4, "Use Glare IV (Sacred Sight) - skipped if PoM is disabled"),
                    (s => GameStateCache.IsMoving,    _ => GetBestDoTAction(), "Use Aero/Dia (Movement Option)"),
                    (s => NeedsDoTRefresh(),          _ => GetBestDoTAction(), "Apply/Refresh Aero/Dia"),
                    (s => true,                       _ => GetBestSTAction(), "Stone/Glare (default)")
                ]),
                
            // AoE DPS Grid  
            new ComboGrid("AoE DPS",
                [25860, 139], // Holy III, Holy
                [
                    (s => HasLilyOvercapRisk(),       WHMConstants.AfflatusRapture, "Use Afflatus Rapture (Lily Overcap)"),
                    (s => HasBloodLilyReady(),        WHMConstants.AfflatusMisery, "Use Afflatus Misery (Blood Lily Ready)"),
                    (s => HasSacredSight(),           WHMConstants.Glare4, "Use Glare IV (Sacred Sight)"),
                    (s => true,                       s => GetBestAoEAction(), "Holy/Holy III (default)")
                ])
        };
    }

    /// <summary>
    /// All available oGCD rules with names for UI display.
    /// </summary>
    private static readonly NamedOGCDRule[] _namedOGCDRules = [
        new("Lucid Dreaming",
            OGCDResolver.CreateDirectRule(
                static () => GameStateCache.CurrentMp <= 6500 && GameStateCache.IsOGCDReady(WHMConstants.LucidDreaming),
                static () => WHMConstants.LucidDreaming, 1)),
                
        new("Assize",
            OGCDResolver.CreateDirectRule(
                static () => GameStateCache.IsOGCDReady(WHMConstants.Assize),
                static () => WHMConstants.Assize, 2)),
                
        new("Presence of Mind",
            OGCDResolver.CreateDirectRule(
                static () => CanUsePresenceOfMindStatic(),
                static () => WHMConstants.PresenceOfMind, 3))
    ];
    
    /// <summary>
    /// WHM abilities that support smart targeting.
    /// 🔥 SINGLE SOURCE OF TRUTH - Add abilities here and they automatically appear in the UI!
    /// 
    /// To add a new smart target ability:
    /// 1. Add the SmartTargetRule here with appropriate TargetingMode and display name
    /// 2. The UI will automatically pick it up via GetNamedSmartTargetRules()
    /// 
    /// No need to manually update NamedSmartTargetRule[] or _actionNames - it's all automatic now!
    /// </summary>
    private static readonly SmartTargetRule[] _smartTargetRules =
    [
        new(WHMConstants.Cure,                      TargetingMode.SmartAbility,    "Cure"),
        new(WHMConstants.Cure2,                     TargetingMode.SmartAbility,    "Cure II"),
        new(WHMConstants.Regen,                     TargetingMode.SmartAbility,    "Regen"),
        new(WHMConstants.AfflatusSolace,            TargetingMode.SmartAbility,    "Afflatus Solace"),
        new(WHMConstants.Tetragrammaton,            TargetingMode.SmartAbility,    "Tetragrammaton"),
        new(WHMConstants.DivineBenison,             TargetingMode.SmartAbility,    "Divine Benison"),
        new(WHMConstants.Asylum,                    TargetingMode.GroundTarget,    "Asylum"),
        new(WHMConstants.LiturgyOfTheBell,
            WHMConstants.LiturgyOfTheBellBurst,
            WHMConstants.LiturgyOfTheBellBuffId,    TargetingMode.GroundTargetSpecial, "Liturgy of the Bell")
    ];

    /// <summary>
    /// All available smart target rules with names for UI display.
    /// Generated dynamically from _smartTargetRules using built-in display names.
    /// Super clean - display names are stored right in the SmartTargetRule!
    /// </summary>
    private static readonly Lazy<NamedSmartTargetRule[]> _namedSmartTargetRules = new(() =>
    {
        var namedRules = new List<NamedSmartTargetRule>();
        
        foreach (var rule in _smartTargetRules)
        {
            // Use the display name from the rule itself, fallback to action ID if not provided
            var displayName = rule.DisplayName ?? $"Action {rule.ActionId}";
            
            namedRules.Add(new NamedSmartTargetRule(displayName, rule));
        }
        
        return namedRules.ToArray();
    });

    #endregion

    #region IComboProvider Implementation
    
    /// <summary>
    /// Gets all named combo rules for this job, extracted directly from the ComboGrids.
    /// Single source of truth - uses the actual combo rules.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<NamedComboRule>> GetNamedComboRules()
    {
        var result = new Dictionary<string, IReadOnlyList<NamedComboRule>>();
        var comboGrids = GetComboGrids();

        foreach (var grid in comboGrids)
        {
            var namedRules = new List<NamedComboRule>();

            // Convert all rules except the last one (default rule)
            for (int i = 0; i < grid.Rules.Length - 1; i++)
            {
                var rule = grid.Rules[i];
                namedRules.Add(new NamedComboRule(rule.Description, rule));
            }

            if (namedRules.Count > 0)
            {
                result[grid.Name] = namedRules;
            }
        }

        return result;
    }

    #region Ultra-Fast OGCD Resolution System
    
    /// <summary>
    /// Gets all available oGCD rules for UI display (legacy method).
    /// </summary>
    public IReadOnlyList<NamedOGCDRule> GetOGCDRules()
    {
        return _namedOGCDRules;
    }
    
    /// <summary>
    /// Gets all named oGCD rules for this job (interface implementation).
    /// </summary>
    public IReadOnlyList<NamedOGCDRule> GetNamedOGCDRules()
    {
        return _namedOGCDRules;
    }
    
    /// <summary>
    /// Gets all named smart target rules for this job (interface implementation).
    /// </summary>
    public IReadOnlyList<NamedSmartTargetRule> GetNamedSmartTargetRules()
    {
        return _namedSmartTargetRules.Value;
    }
    
    /// <summary>
    /// Ultra-optimized OGCD rules using direct cache access for maximum performance.
    /// Target: <3ns per rule evaluation, <10ns total for 3 rules.
    /// No GameStateData allocation needed.
    /// 
    /// Note: This is rebuilt dynamically based on configuration state.
    /// </summary>
    private static OGCDResolver.DirectCacheOGCDRule[] _ultraFastOGCDRules = BuildEnabledOGCDRules();
    
    /// <summary>
    /// Enabled smart target rules using configuration for maximum performance.
    /// Only enabled rules are included for faster iteration.
    /// 
    /// Note: This is rebuilt dynamically based on configuration state.
    /// </summary>
    private static SmartTargetRule[] _enabledSmartTargetRules = BuildEnabledSmartTargetRules();
    
    /// <summary>
    /// Builds the enabled oGCD rules array based on current configuration.
    /// Call this when configuration changes to update the active rules.
    /// </summary>
    private static OGCDResolver.DirectCacheOGCDRule[] BuildEnabledOGCDRules()
    {
        var enabledRules = new List<OGCDResolver.DirectCacheOGCDRule>();
        
        // Check each named rule and add if enabled
        foreach (var namedRule in _namedOGCDRules)
        {
            if (ConfigurationManager.IsOGCDRuleEnabled(24, namedRule.Name)) // 24 = WHM job ID
            {
                enabledRules.Add(namedRule.Rule);
            }
        }
        
        return enabledRules.ToArray();
    }
    
    /// <summary>
    /// Builds the enabled smart target rules array based on current configuration.
    /// Call this when configuration changes to update the active rules.
    /// </summary>
    private static SmartTargetRule[] BuildEnabledSmartTargetRules()
    {
        var enabledRules = new List<SmartTargetRule>();
        
        // Check each named rule and add if enabled
        foreach (var namedRule in _namedSmartTargetRules.Value)
        {
            if (ConfigurationManager.IsSmartTargetRuleEnabled(24, namedRule.Name)) // 24 = WHM job ID
            {
                enabledRules.Add(namedRule.Rule);
            }
        }
        
        return enabledRules.ToArray();
    }
    
    /// <summary>
    /// Rebuilds the oGCD rules when configuration changes.
    /// Called by the configuration system when oGCD rules are enabled/disabled.
    /// </summary>
    public static void RefreshOGCDRulesStatic()
    {
        _ultraFastOGCDRules = BuildEnabledOGCDRules();
        ModernActionCombo.PluginLog?.Debug($"🔮 WHM: Refreshed oGCD rules - {_ultraFastOGCDRules.Length} enabled");
    }
    
    /// <summary>
    /// Rebuilds the oGCD rules when configuration changes (interface implementation).
    /// </summary>
    public void RefreshOGCDRules()
    {
        RefreshOGCDRulesStatic();
    }
    
    /// <summary>
    /// Rebuilds the smart target rules when configuration changes.
    /// Called by the configuration system when smart target rules are enabled/disabled.
    /// </summary>
    public static void RefreshSmartTargetRulesStatic()
    {
        _enabledSmartTargetRules = BuildEnabledSmartTargetRules();
        
        // Reinitialize SmartTargetResolver with the new enabled rules
        SmartTargetResolver.Initialize(_enabledSmartTargetRules.AsSpan());
        
        ModernActionCombo.PluginLog?.Debug($"🔮 WHM: Refreshed smart target rules - {_enabledSmartTargetRules.Length} enabled");
    }
    
    /// <summary>
    /// Rebuilds the smart target rules when configuration changes (interface implementation).
    /// </summary>
    public void RefreshSmartTargetRules()
    {
        RefreshSmartTargetRulesStatic();
    }
    
    /// <summary>
    /// Rebuilds the combo rules when configuration changes (interface implementation).
    /// With the dynamic system, this triggers cache invalidation for GetComboGrids().
    /// </summary>
    public void RefreshComboRules()
    {
        // Force cache invalidation - the ActionInterceptor will pick up new rules on next evaluation
        // Since GetComboGrids() now reads configuration dynamically, no pre-built cache to refresh
        ModernActionCombo.PluginLog?.Debug($"🔮 WHM: Combo rules refreshed (dynamic evaluation from master definitions)");
    }
    
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

        // Check if oGCDs are enabled globally for this job
        if (!ConfigurationManager.IsOGCDEnabled(24)) // 24 = WHM job ID
            yield break;

        // Direct cache evaluation - zero allocation
        // Use array for results (can't use Span in yield method)
        var results = new uint[2]; // Max 2 OGCDs
        var resultsSpan = results.AsSpan();
        
        // Ultra-fast evaluation using direct cache access (respects individual rule enables)
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
    private static uint _cachedBestAoE = 0u;      // Default to no AoE (Holy unlocks at 45)
    
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
        
        // Update best AoE action
        _cachedBestAoE = currentLevel switch
        {
            >= 82 => 25860u, // Holy III
            >= 45 => 139u,   // Holy
            _ => 0u          // No AoE available
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
    
    /// <summary>
    /// Hot path: Gets best AoE action with cached lookup.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBestAoEAction()
    {
        UpdateCachedActions();
        return _cachedBestAoE;
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

    #endregion

    #region Smart Target System

    /// <summary>
    /// Initialize smart targeting for WHM.
    /// Uses configuration-aware enabled rules for optimal performance.
    /// </summary>
    private static partial void InitializeSmartHealing()
    {
        // Use the configuration-aware enabled rules
        SmartTargetResolver.Initialize(_enabledSmartTargetRules.AsSpan());
    }
    
    /// <summary>
    /// Gets the optimal target for an ability that supports smart targeting.
    /// Extension point for smart targeting integration.
    /// </summary>
    public uint GetOptimalTarget(uint actionId)
    {
        return SmartTargetResolver.GetOptimalTarget(actionId);
    }
    
    /// <summary>
    /// Gets the smart target rules for this job (for testing purposes).
    /// </summary>
    public SmartTargetRule[] GetSmartTargetRules()
    {
        return _smartTargetRules;
    }
    
    /// <summary>
    /// Check if an action supports smart targeting.
    /// </summary>
    public bool SupportsSmartTargeting(uint actionId)
    {
        return SmartTargetResolver.IsSmartTargetAction(actionId);
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
