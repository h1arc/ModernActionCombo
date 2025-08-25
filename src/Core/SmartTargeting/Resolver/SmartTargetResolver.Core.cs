using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// Unified smart targeting engine combining rule management and target resolution.
/// Fresh implementation: Hard Target > Ally (lowest HP%) > Self
/// </summary>
public static unsafe partial class SmartTargetResolver
{
    #region Rule Management
    private static FrozenDictionary<uint, SmartTargetRule> _rules = FrozenDictionary<uint, SmartTargetRule>.Empty;

    /// <summary>
    /// Initialize smart targeting rules. Call once during job provider setup.
    /// </summary>
    public static void Initialize(ReadOnlySpan<SmartTargetRule> rules)
    {
        if (rules.IsEmpty)
        {
            _rules = FrozenDictionary<uint, SmartTargetRule>.Empty;
            return;
        }

        var dict = new Dictionary<uint, SmartTargetRule>(rules.Length * 2);
        foreach (var rule in rules)
        {
            dict[rule.ActionId] = rule;
            if (rule.SecondaryActionId != 0)
            {
                dict[rule.SecondaryActionId] = rule;
            }
        }
        _rules = dict.ToFrozenDictionary();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSmartTargetAction(uint actionId) => _rules.ContainsKey(actionId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetRule(uint actionId, out SmartTargetRule rule) => _rules.TryGetValue(actionId, out rule);
    #endregion

    #region Core Targeting Logic
    /// <summary>
    /// Get optimal target for an action. Priority: Hard Target > Ally (lowest HP%) > Self
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetOptimalTarget(uint actionId)
    {
    var currentJobId = GameStateCache.JobId;
    if (!ConfigurationManager.IsSmartTargetingEnabled(currentJobId)) return 0;
    if (!_rules.TryGetValue(actionId, out var rule)) return 0;

        return rule.Mode switch
        {
            TargetingMode.SmartAbility => GetSmartTarget(),
            TargetingMode.GroundTarget => GetGroundTarget(),
            TargetingMode.GroundTargetSpecial => GetGroundTargetSpecial(rule),
            TargetingMode.Cleanse => GetCleanseTarget(),
            _ => GetSmartTarget()
        };
    }

    /// <summary>
    /// Core smart targeting: Hard Target > Ally (lowest HP%) > Self
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetSmartTarget(float hpThreshold = 0.99f)
    {
        if (!SmartTargetingCache.IsReady) return 0;

        // 1. Hard target wins (user manually targeted someone)
        var hardTarget = GetValidHardTarget();
        if (hardTarget != 0) return hardTarget;

        // 2. Ally with missing HP (lowest HP% first, excluding self)
        var allyTarget = GetBestAlly(hpThreshold);
        if (allyTarget != 0) return allyTarget;

        // 3. Self (fallback)
        var selfTarget = GetValidSelfTarget(hpThreshold);
        if (selfTarget != 0) return selfTarget;

        // 4. Final fallback to self regardless of HP
        return SmartTargetingCache.GetSelfId();
    }
    #endregion

    #region Cleanse Targeting
    /// <summary>
    /// Find the best ally with a cleansable debuff. Priority: Hard target if cleansable > lowest HP% ally with cleansable > self if cleansable.
    /// Companion may override based on per-job settings when significantly lower HP and cleansable.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetCleanseTarget()
    {
        if (!SmartTargetingCache.IsReady) return 0;

        // 1) Hard target if it has a cleansable debuff
        var hard = GetValidHardTarget();
        if (hard != 0 && GameStateCache.HasCleansableDebuff(hard))
            return hard;

        // 2) Best party member with cleansable debuff (lowest HP% first)
        SmartTargetingCache.SortByHpPercentage();
        var partyCount = SmartTargetingCache.PartyCount;
        var selfId = SmartTargetingCache.GetSelfId();
        uint bestPartyId = 0;
        float bestPartyHp = 1.0f;
        for (int i = 0; i < partyCount; i++)
        {
            var memberId = SmartTargetingCache.GetMemberIdByIndex(i);
            if (memberId == 0) continue;
            if (!SmartTargetingCache.IsValidTarget(memberId)) continue;
            if (!GameStateCache.HasCleansableDebuff(memberId)) continue;

            bestPartyId = memberId;
            bestPartyHp = SmartTargetingCache.GetMemberHpPercent(memberId);
            break;
        }

        // 3) Optional companion override per-job if configured and companion has cleansable debuff
        if (bestPartyId != 0)
        {
            var compId = GetBestCompanionForCleanse(out var compHp);
            if (compId != 0 && GameStateCache.IsCompanionOverrideEnabledForJob(GameStateCache.JobId))
            {
                var delta = GameStateCache.GetCompanionOverrideDeltaForJob(GameStateCache.JobId);
                if (compHp + delta < bestPartyHp)
                    return compId;
            }
            return bestPartyId;
        }

        // 4) Companion alone if allowed and cleansable
        {
            var compId = GetBestCompanionForCleanse(out _);
            if (compId != 0) return compId;
        }

        // 5) Self if cleansable
        if (selfId != 0 && SmartTargetingCache.IsValidTarget(selfId) && GameStateCache.HasCleansableDebuff(selfId))
            return selfId;

        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBestCompanionForCleanse(out float compHp)
    {
        compHp = 1.0f;
        if (!SmartTargetingCache.HasValidCompanion()) return 0;
        var compId = SmartTargetingCache.GetCompanionId();
        if (compId == 0) return 0;
        if (!GameStateCache.HasCleansableDebuff(compId)) return 0;
        compHp = SmartTargetingCache.GetCompanionHpPercent();
        return compId;
    }
    #endregion

    #region Target Resolution Helpers
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetValidHardTarget()
    {
        if (!SmartTargetingCache.HasValidHardTarget()) return 0;
        
        var hardTargetId = SmartTargetingCache.GetHardTargetId();
        return SmartTargetingCache.IsValidTarget(hardTargetId) ? hardTargetId : 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBestAlly(float hpThreshold)
    {
        var partyCount = SmartTargetingCache.PartyCount;
        if (partyCount <= 1) return GetBestCompanion(hpThreshold); // Only self in party, check companion

        // Sort party by HP% and find best ally (not self)
        SmartTargetingCache.SortByHpPercentage();
        var selfId = SmartTargetingCache.GetSelfId();
        uint bestPartyId = 0;
        float bestPartyHp = 1.0f;
        for (int i = 0; i < partyCount; i++)
        {
            var memberId = SmartTargetingCache.GetMemberIdByIndex(i);
            if (memberId == selfId) continue; // Skip self - we want ally priority

            if (SmartTargetingCache.IsValidTarget(memberId) && 
                SmartTargetingCache.NeedsHealing(memberId, hpThreshold))
            {
                bestPartyId = memberId;
                bestPartyHp = SmartTargetingCache.GetMemberHpPercent(memberId);
                break;
            }
        }

        // Consider companion override if configured
        if (bestPartyId != 0)
        {
            var compId = GetBestCompanion(hpThreshold, out var compHp);
            if (compId != 0 && GameStateCache.IsCompanionOverrideEnabledForJob(GameStateCache.JobId))
            {
                var delta = GameStateCache.GetCompanionOverrideDeltaForJob(GameStateCache.JobId);
                if (compHp + delta < bestPartyHp)
                    return compId;
            }
            return bestPartyId;
        }

        // No party ally found, check companion
        return GetBestCompanion(hpThreshold);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBestCompanion(float hpThreshold)
    {
        if (!SmartTargetingCache.HasValidCompanion()) return 0;
        var companionHp = SmartTargetingCache.GetCompanionHpPercent();
        if (companionHp < hpThreshold)
            return SmartTargetingCache.GetCompanionId();
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBestCompanion(float hpThreshold, out float compHp)
    {
        compHp = 1.0f;
        if (!SmartTargetingCache.HasValidCompanion()) return 0;
        compHp = SmartTargetingCache.GetCompanionHpPercent();
        if (compHp < hpThreshold)
            return SmartTargetingCache.GetCompanionId();
        return 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetValidSelfTarget(float hpThreshold)
    {
        var selfId = SmartTargetingCache.GetSelfId();
        if (selfId == 0) return 0;

        if (SmartTargetingCache.IsValidTarget(selfId) && 
            SmartTargetingCache.NeedsHealing(selfId, hpThreshold))
        {
            return selfId;
        }
        return 0;
    }
    #endregion

    #region Ground Targeting
    /// <summary>
    /// Get ground target based on priority. Priority: Target location > Tank location > Self location
    /// For ground targeting, we return the ID of the entity whose location should be used
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetGroundTarget()
    {
        try
        {
            if (GameStateCache.HasTarget)
            {
                var targetId = GameStateCache.TargetId;
                if (GameStateCache.TryGetKnownObject(targetId, out var targetObj) && targetObj != null)
                {
                    return targetId;
                }
            }
        }
        catch
        {
            // ignore and use fallback
        }

        return GetGroundTargetFallback();
    }

    /// <summary>
    /// Get special ground target (like Liturgy) based on current target. Priority: Current target location > Tank location > Self location  
    /// For ground targeting, return the ID of where we want to place the effect
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetGroundTargetSpecial(SmartTargetRule rule)
    {
        try
        {
            if (GameStateCache.HasTarget)
            {
                var targetId = GameStateCache.TargetId;
                if (GameStateCache.TryGetKnownObject(targetId, out var targetObj) && targetObj != null)
                {
                    return targetId;
                }
            }
        }
        catch
        {
            // continue to fallback
        }

        return GetGroundTargetFallback();
    }

    /// <summary>
    /// Fallback targeting for ground abilities: Tank location > Self location
    /// Returns the ID of the player/tank whose location should be used for ground placement
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetGroundTargetFallback()
    {
        if (!SmartTargetingCache.IsReady) 
        {
            // Return player ID for self-placement when cache not ready
            return SmartTargetingCache.GetSelfId();
        }

        // Find tank in party - place ground effect at tank's location
        var tankTarget = GetTankTarget();
    if (tankTarget != 0) return tankTarget;

        // Final fallback: self location (return player ID for ground placement at player location)
    return SmartTargetingCache.GetSelfId();
    }

    /// <summary>
    /// Get tank member from party
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetTankTarget()
    {
        var partyCount = SmartTargetingCache.PartyCount;
        if (partyCount <= 1) return 0; // Only self in party

        for (int i = 0; i < partyCount; i++)
        {
            var memberId = SmartTargetingCache.GetMemberIdByIndex(i);
            if (SmartTargetingCache.IsTank(memberId) && 
                SmartTargetingCache.IsValidTarget(memberId))
            {
                return memberId;
            }
        }

        return 0;
    }
    #endregion

    #region Action Resolution
    /// <summary>
    /// Get resolved action ID for abilities that change based on player state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetResolvedActionId(uint actionId)
    {
        var currentJobId = GameStateCache.JobId;
        if (!ConfigurationManager.IsSmartTargetingEnabled(currentJobId))
            return actionId;

        if (!_rules.TryGetValue(actionId, out var rule))
            return actionId;

        // Handle special cases like Liturgy of the Bell
        if (rule.Mode == TargetingMode.GroundTargetSpecial && 
            rule.SecondaryActionId != 0 && 
            rule.RequiredBuffId != 0 &&
            GameStateCache.HasPlayerBuff(rule.RequiredBuffId))
        {
            return rule.SecondaryActionId;
        }

        return actionId;
    }
    #endregion

    #region Testing Support
    public static void ClearForTesting() => _rules = FrozenDictionary<uint, SmartTargetRule>.Empty;
    #endregion
}