using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// Targeting modes for different ability types
/// </summary>
public enum TargetingMode : byte
{
    /// <summary>Smart heal targeting - use SmartTargetingCache</summary>
    SmartAbility = 0,
    /// <summary>Ground target placement (Asylum)</summary>
    GroundTarget = 1,
    /// <summary>Ground target with secondary action (Liturgy of the Bell + activation)</summary>
    GroundTargetSpecial = 2
}

/// <summary>
/// Ultra-minimal smart targeting for abilities that benefit from automatic target selection.
/// Since SmartTargetingCache already handles all the logic and thresholds, we just need:
/// 1. Which actions need smart targeting
/// 2. What type of targeting they use
/// 3. For special cases like Liturgy, the secondary action and buff to check
/// 
/// Performance: <1ns per lookup using .NET 9 FrozenSet
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct SmartTargetRule
{
    public readonly uint ActionId;
    public readonly TargetingMode Mode;
    public readonly uint SecondaryActionId; // For abilities with secondary actions (like Liturgy)
    public readonly uint RequiredBuffId;    // Buff required for secondary action
    public readonly string? DisplayName;    // Optional display name for UI (ignored by targeting logic)

    public SmartTargetRule(uint actionId, TargetingMode mode = TargetingMode.SmartAbility)
    {
        ActionId = actionId;
        Mode = mode;
        SecondaryActionId = 0;
        RequiredBuffId = 0;
        DisplayName = null;
    }
    
    public SmartTargetRule(uint actionId, TargetingMode mode, string displayName)
    {
        ActionId = actionId;
        Mode = mode;
        SecondaryActionId = 0;
        RequiredBuffId = 0;
        DisplayName = displayName;
    }
    
    public SmartTargetRule(uint actionId, uint secondaryActionId, uint requiredBuffId, TargetingMode mode)
    {
        ActionId = actionId;
        Mode = mode;
        SecondaryActionId = secondaryActionId;
        RequiredBuffId = requiredBuffId;
        DisplayName = null;
    }
    
    public SmartTargetRule(uint actionId, uint secondaryActionId, uint requiredBuffId, TargetingMode mode, string displayName)
    {
        ActionId = actionId;
        Mode = mode;
        SecondaryActionId = secondaryActionId;
        RequiredBuffId = requiredBuffId;
        DisplayName = displayName;
    }
}

/// <summary>
/// Minimal smart targeting resolver.
/// SmartTargetingCache does all the heavy lifting - we just route actions to it.
/// Future: Can be extended for ground targeting, utility abilities, etc.
/// </summary>
public static class SmartTargetResolver
{
    private static FrozenSet<uint> _smartTargetActions = FrozenSet<uint>.Empty;
    private static readonly Dictionary<uint, SmartTargetRule> _targetingRules = new();
    
    /// <summary>
    /// Initialize smart targeting rules. Call once during job provider setup.
    /// </summary>
    public static void Initialize(ReadOnlySpan<SmartTargetRule> rules)
    {
        if (rules.IsEmpty)
        {
            _smartTargetActions = FrozenSet<uint>.Empty;
            _targetingRules.Clear();
            return;
        }
        
        var builder = new HashSet<uint>(rules.Length * 2); // Account for secondary actions
        _targetingRules.Clear();
        
        foreach (var rule in rules)
        {
            builder.Add(rule.ActionId);
            _targetingRules[rule.ActionId] = rule;
            
            // Also register secondary action if it exists
            if (rule.SecondaryActionId != 0)
            {
                builder.Add(rule.SecondaryActionId);
                _targetingRules[rule.SecondaryActionId] = rule;
            }
        }
        
        _smartTargetActions = builder.ToFrozenSet();
    }
    
    /// <summary>
    /// Get smart target for an ability. Returns 0 if no smart targeting needed.
    /// Handles different targeting modes and special cases like Liturgy's secondary action.
    /// For GroundTargetSpecial, also handles action replacement when buff is active.
    /// Respects global smart targeting configuration for the current job.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetOptimalTarget(uint actionId)
    {
        // Check if smart targeting is globally enabled for the current job
        var currentJobId = GameStateCache.JobId;
        if (!ConfigurationManager.IsSmartTargetingEnabled(currentJobId))
        {
            ModernActionCombo.PluginLog?.Debug($"🎯 Smart targeting disabled for job {currentJobId}");
            return 0; // Smart targeting disabled, no target suggestion
        }

        if (!_smartTargetActions.Contains(actionId))
        {
            ModernActionCombo.PluginLog?.Debug($"🎯 Action {actionId} not in smart target actions");
            return 0;
        }
            
        if (!_targetingRules.TryGetValue(actionId, out var rule))
        {
            ModernActionCombo.PluginLog?.Debug($"🎯 No targeting rule for action {actionId}, using default smart target");
            return SmartTargetingCache.GetSmartTarget();
        }
            
        var result = rule.Mode switch
        {
            TargetingMode.SmartAbility => SmartTargetingCache.GetSmartTarget(),
            TargetingMode.GroundTarget => GetGroundTarget(),
            TargetingMode.GroundTargetSpecial => GetGroundTargetSpecial(rule),
            _ => SmartTargetingCache.GetSmartTarget()
        };
        
        ModernActionCombo.PluginLog?.Debug($"🎯 Smart targeting for action {actionId}: Target={result:X}, Mode={rule.Mode}");
        return result;
    }
    
    /// <summary>
    /// Get the resolved action ID for abilities that change based on player state.
    /// For GroundTargetSpecial abilities like Liturgy, returns the secondary action when buff is active.
    /// Respects global smart targeting configuration for the current job.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetResolvedActionId(uint actionId)
    {
        // Check if smart targeting is globally enabled for the current job
        var currentJobId = GameStateCache.JobId;
        if (!ConfigurationManager.IsSmartTargetingEnabled(currentJobId))
            return actionId; // Smart targeting disabled, return original action
        
        if (!_smartTargetActions.Contains(actionId))
            return actionId;
            
        if (!_targetingRules.TryGetValue(actionId, out var rule))
            return actionId;
            
        // For GroundTargetSpecial, check if we should use the secondary action
        if (rule.Mode == TargetingMode.GroundTargetSpecial && 
            rule.SecondaryActionId != 0 && 
            rule.RequiredBuffId != 0)
        {
            var hasRequiredBuff = GameStateCache.HasPlayerBuff(rule.RequiredBuffId);
            if (hasRequiredBuff)
            {
                return rule.SecondaryActionId; // Return the burst action when buff is active
            }
        }
        
        return actionId; // Return original action
    }
    
    /// <summary>
    /// Basic ground targeting for simple ground abilities like Asylum.
    /// Priority: Current target (if enemy) > Tank > Self
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetGroundTarget()
    {
        // Priority 1: Current target if it's an enemy (place ground effect on enemy location)
        var currentTarget = GameStateCache.TargetId;
        if (currentTarget != 0 && GameStateCache.HasTarget)
        {
            // If it's not in our party cache, assume it's an enemy - place ground effect at enemy location
            if (!SmartTargetingCache.IsValidTarget(currentTarget))
            {
                return currentTarget;
            }
        }
        
        // Priority 2: Find tank in party to place ground effect near tank
        for (byte i = 0; i < SmartTargetingCache.MemberCount; i++)
        {
            var memberId = SmartTargetingCache.GetMemberIdByIndex(i);
            if (memberId != 0 && SmartTargetingCache.IsTank(memberId) && 
                SmartTargetingCache.IsValidTarget(memberId))
            {
                return memberId;
            }
        }
        
        // Priority 3: Self as fallback (place ground effect at own location)
        return GetSelfTarget();
    }
    
    /// <summary>
    /// Gets the player's own ID for self-targeting.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetSelfTarget()
    {
        for (byte i = 0; i < SmartTargetingCache.MemberCount; i++)
        {
            var memberId = SmartTargetingCache.GetMemberIdByIndex(i);
            if (memberId != 0 && SmartTargetingCache.IsSelf(i))
            {
                return memberId;
            }
        }
        
        // Absolute fallback: any valid party member
        for (byte i = 0; i < SmartTargetingCache.MemberCount; i++)
        {
            var memberId = SmartTargetingCache.GetMemberIdByIndex(i);
            if (memberId != 0 && SmartTargetingCache.IsValidTarget(memberId))
            {
                return memberId;
            }
        }
        
        return 0; // No valid target found
    }
    
    /// <summary>
    /// Special targeting for abilities with secondary actions like Liturgy of the Bell.
    /// Handles both initial placement and secondary activation based on buff state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetGroundTargetSpecial(SmartTargetRule rule)
    {
        // Check if this is the secondary action and we have the required buff
        if (rule.SecondaryActionId != 0 && rule.RequiredBuffId != 0)
        {
            var hasRequiredBuff = GameStateCache.HasPlayerBuff(rule.RequiredBuffId);
            
            // If we have the buff and this could be the secondary action, use smart targeting
            // for the activation (target party members who need healing)
            if (hasRequiredBuff)
            {
                return SmartTargetingCache.GetSmartTarget();
            }
        }
        
        // Otherwise, use standard ground targeting for initial placement
        return GetGroundTarget();
    }
    
    /// <summary>
    /// Check if action supports smart targeting.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSmartTargetAction(uint actionId) => _smartTargetActions.Contains(actionId);
    
    /// <summary>
    /// Clear for testing.
    /// </summary>
    public static void ClearForTesting() 
    {
        _smartTargetActions = FrozenSet<uint>.Empty;
        _targetingRules.Clear();
    }
}
