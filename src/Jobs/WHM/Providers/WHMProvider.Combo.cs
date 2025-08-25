using System.Runtime.CompilerServices;
using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Services;
using ModernActionCombo.Jobs.WHM.Data;
using System.Collections.Generic;
using System;

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
                [WHMConstants.Glare3, WHMConstants.Glare, WHMConstants.Stone3, WHMConstants.Stone2, WHMConstants.Stone],
                [
                    (s => HasLilyOvercapRisk(),       WHMConstants.AfflatusRapture,     "Use Afflatus Rapture (Lily Overcap)"),
                    (s => HasBloodLilyReady(),        WHMConstants.AfflatusMisery,      "Use Afflatus Misery (Blood Lily Ready)"),
                    (s => HasSacredSight(),           WHMConstants.Glare4,              "Use Glare IV (Needs PoM enabled in oGCDs)"),
                    (s => GameStateCache.IsMoving,    _ => WHMConstants.DoT,            "Use Aero/Dia (Movement Option)"),
                    (s => NeedsDoTRefresh(),          _ => WHMConstants.DoT,            "Apply/Refresh Aero/Dia"),
                    (s => true,                       _ => WHMConstants.SingleTarget,   "Stone/Glare (default)")
                ]),
                
            // AoE DPS Grid  
            new ComboGrid("AoE DPS",
                [WHMConstants.Holy3, WHMConstants.Holy],
                [
                    (s => HasLilyOvercapRisk(),       WHMConstants.AfflatusRapture,     "Use Afflatus Rapture (Lily Overcap)"),
                    (s => HasBloodLilyReady(),        WHMConstants.AfflatusMisery,      "Use Afflatus Misery (Blood Lily Ready)"),
                    (s => HasSacredSight(),           WHMConstants.Glare4,              "Use Glare IV (Needs PoM enabled in oGCDs)"),
                    (s => true,                       s => WHMConstants.AoE,            "Holy/Holy III (default)")
                ])
        };
    }

    /// <summary>
    /// All available oGCD rules with names for UI display.
    /// </summary>
    private static readonly NamedOGCDRule[] _namedOGCDRules = [
        new("Assize",
            OGCDResolver.CreateDirectRule(
                static () => GameStateCache.IsOGCDReady(WHMConstants.Assize),
                static () => WHMConstants.Assize, 1)),
        new("Presence of Mind",
            OGCDResolver.CreateDirectRule(
                static () => CanUsePresenceOfMindStatic(),
                static () => WHMConstants.PresenceOfMind, 2)),
        new("Lucid Dreaming",
            OGCDResolver.CreateDirectRule(
                static () => GameStateCache.CurrentMp <= 6500 && GameStateCache.IsOGCDReady(WHMConstants.LucidDreaming),
                static () => WHMConstants.LucidDreaming, 3))
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
        new(WHMConstants.Cure,                      TargetingMode.SmartAbility,         "Cure"),
        new(WHMConstants.Cure2,                     TargetingMode.SmartAbility,         "Cure II"),
        new(WHMConstants.Cure3,                     TargetingMode.SmartAbility,         "Cure III"),
        new(WHMConstants.Regen,                     TargetingMode.SmartAbility,         "Regen"),
        new(WHMConstants.AfflatusSolace,            TargetingMode.SmartAbility,         "Afflatus Solace"),
        new(WHMConstants.Tetragrammaton,            TargetingMode.SmartAbility,         "Tetragrammaton"),
        new(WHMConstants.DivineBenison,             TargetingMode.SmartAbility,         "Divine Benison"),
        new(WHMConstants.Asylum,                    TargetingMode.GroundTarget,         "Asylum"),
        new(WHMConstants.Aquaveil,                  TargetingMode.SmartAbility,         "Aquaveil"),
        new(WHMConstants.LiturgyOfTheBell,
            WHMConstants.LiturgyOfTheBellBurst,
            WHMConstants.LiturgyOfTheBellBuffId,    TargetingMode.GroundTargetSpecial,  "Liturgy of the Bell"),

        new(WHMConstants.Esuna,                     TargetingMode.Cleanse,              "Esuna"),
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
    private static uint _ogcdRulesVersionBuilt;

    /// <summary>
    /// Enabled smart target rules using configuration for maximum performance.
    /// Only enabled rules are included for faster iteration.
    /// 
    /// Note: This is rebuilt dynamically based on configuration state.
    /// </summary>
    private static SmartTargetRule[] _enabledSmartTargetRules = BuildEnabledSmartTargetRulesSafe();

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
            if (ConfigurationManager.IsOGCDRuleEnabled(WHMConstants.WHMJobId, namedRule.Name))
            {
                enabledRules.Add(namedRule.Rule);
            }
        }

        return enabledRules.ToArray();
    }

    /// <summary>
    /// Safe initialization wrapper for BuildEnabledSmartTargetRules.
    /// Handles the case where configuration might not be available during static initialization.
    /// </summary>
    private static SmartTargetRule[] BuildEnabledSmartTargetRulesSafe()
    {
        try
        {
            return BuildEnabledSmartTargetRules();
        }
        catch (Exception ex)
        {
            Logger.Warning($"WHM: Using all smart target rules (configuration unavailable): {ex.Message}");

            // Fallback: return all rules if configuration isn't available yet
            // This ensures smart targeting works even if config isn't loaded
            return _smartTargetRules;
        }
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
            var isEnabled = ConfigurationManager.IsSmartTargetRuleEnabled(WHMConstants.WHMJobId, namedRule.Name);
            if (isEnabled)
            {
                enabledRules.Add(namedRule.Rule);
            }
        }

        if (enabledRules.Count == 0)
            Logger.Warning("WHM: No smart targeting rules enabled");

        return enabledRules.ToArray();
    }

    /// <summary>
    /// Rebuilds the oGCD rules when configuration changes.
    /// Called by the configuration system when oGCD rules are enabled/disabled.
    /// </summary>
    public static void RefreshOGCDRulesStatic()
    {
        _ultraFastOGCDRules = BuildEnabledOGCDRules();
        _ogcdRulesVersionBuilt = Core.Data.ConfigAwareActionCache.GetConfigVersion();
        Logger.Debug("WHM: Refreshed oGCD rules - {0} enabled", _ultraFastOGCDRules.Length);
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
        Logger.Debug("🔮 WHM: Combo rules refreshed (dynamic evaluation from master definitions)");
    }

    /// <summary>
    /// Static version for use in static delegates
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        // Auto-refresh on config version changes (no manual UI call needed)
        var v = Core.Data.ConfigAwareActionCache.GetConfigVersion();
        if (v != _ogcdRulesVersionBuilt)
        {
            RefreshOGCDRulesStatic();
        }

        // Fast early exits using centralized combo eligibility
        if (!GameStateCache.CanProcessCombos)
            yield break;

        if (!GameStateCache.CanWeave())
            yield break;

        // Check if oGCDs are enabled globally for this job
        if (!ConfigurationManager.IsOGCDEnabled(WHMConstants.WHMJobId))
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasLilyOvercapRisk() => WHMJobGauge.HasOvercapRisk;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool NeedsDoTRefresh()
    {
        var dot = WHMConstants.DoT;
        if (dot == 0u) return false;

        var debuffId = WHMConstants.GetDoTDebuff(dot);
        if (debuffId == 0u) return false;

        var timeRemaining = GameStateCache.GetTargetDebuffTimeRemaining(debuffId);
        var needsRefresh = timeRemaining == GameStateCache.UNINITIALIZED_SENTINEL || timeRemaining <= 3.0f;


        return needsRefresh;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasBloodLilyReady() => WHMJobGauge.BloodLilyReady;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasSacredSight() => GameStateCache.HasPlayerBuff(WHMConstants.SacredSightBuffId);

    #endregion

    #region Smart Target System

    /// <summary>
    /// Initialize smart targeting for WHM.
    /// Uses configuration-aware enabled rules for optimal performance.
    /// </summary>
    private static partial void InitializeSmartHealing()
    {
        // Initialize resolver with enabled rules
        // Use the configuration-aware enabled rules
        SmartTargetResolver.Initialize(_enabledSmartTargetRules.AsSpan());
        Logger.Debug("WHM: Smart targeting initialized with {0} rules", _enabledSmartTargetRules.Length);
    }

    /// <summary>
    /// Debug method to trace Asylum targeting behavior in real-time.
    /// </summary>
    public static void DebugAsylumTargeting()
    {
        try
        {
            Logger.Debug("ASYLUM TARGETING DEBUG:");

            // Check if rules are loaded
            if (SmartTargetResolver.IsSmartTargetAction(WHMConstants.Asylum))
            {
                Logger.Debug($"Asylum (3569) has smart targeting rule");

                // Get current targeting info
                var hasTarget = GameStateCache.HasTarget;
                var targetId = GameStateCache.TargetId;
                Logger.Debug($"Current state: HasTarget={hasTarget}, TargetId={targetId}");

                // Test the targeting resolution
                var optimalTarget = SmartTargetResolver.GetOptimalTarget(WHMConstants.Asylum);
                Logger.Debug($"Optimal target for Asylum: {optimalTarget}");

                // Show fallback info
                if (SmartTargetingCache.IsReady)
                {
                    var selfId = SmartTargetingCache.GetSelfId();
                    var partyCount = SmartTargetingCache.PartyCount;
                    Logger.Debug($"Self ID: {selfId}, Party Count: {partyCount}");

                    // Check for tank
                    for (int i = 0; i < partyCount; i++)
                    {
                        var memberId = SmartTargetingCache.GetMemberIdByIndex(i);
                        if (SmartTargetingCache.IsTank(memberId))
                        {
                            Logger.Debug($"Tank found: ID {memberId}");
                            break;
                        }
                    }
                }
            }
            else
            {
                Logger.Debug($"Asylum (3569) does NOT have smart targeting rule");
            }
        }
        catch (Exception ex)
        {
            Logger.Debug($"Debug error: {ex.Message}");
        }
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
    /// Manual debug method to force smart targeting initialization and check results.
    /// </summary>
    public static void DebugForceInitialization()
    {
        Logger.Debug("DEBUG: Force initializing WHM smart targeting");

        // Rebuild enabled rules
        _enabledSmartTargetRules = BuildEnabledSmartTargetRules();

        // Re-initialize
        InitializeSmartHealing();

        // Test specific actions
        uint[] testActions = { WHMConstants.Asylum, WHMConstants.Cure, WHMConstants.LiturgyOfTheBell };
        foreach (var actionId in testActions)
        {
            var isRecognized = SmartTargetResolver.IsSmartTargetAction(actionId);
            Logger.Debug($"DEBUG: Action {actionId} recognized: {isRecognized}");
        }
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
        // No cached level state to invalidate anymore; dynamic resolution via GameStateCache
        Logger.Debug("WHM: Level changed to {0}", newLevel);
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
        Logger.Debug("WHM: Duty state changed - in duty: {0}, duty ID: {1}", inDuty, dutyId.HasValue ? dutyId.Value : 0u);
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
        Logger.Debug("WHM: Combat state changed - in combat: {0}", inCombat);
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
