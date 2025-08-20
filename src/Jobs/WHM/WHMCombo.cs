using ModernWrathCombo.Core.Abstracts;
using ModernWrathCombo.Core.Data;
using ModernWrathCombo.Core.Services;
using System.Runtime.CompilerServices;
using System;

namespace ModernWrathCombo.Jobs.WHM;

/// <summary>
/// White Mage combo definitions and rotation logic.
/// This is where the actual combat logic lives.
/// Uses the action resolver to handle all single-target DPS actions.
/// Implements ultra-minimal DoT decision lockout to prevent double-casting.
/// </summary>
public sealed class WHMCombo : CustomCombo
{
    // Ultra-minimal state: when did we last decide to cast DoT?
    private static DateTime _lastDoTDecision = DateTime.MinValue;
    private static readonly TimeSpan _dotDecisionLockout = TimeSpan.FromSeconds(2); // 2s natural animation lockout
    // Intercept Glare3 (level 72+) - the highest level single-target action
    // This could be made dynamic using WHMConstants.GetCurrentSingleTargetAction(playerLevel)
    public override uint InterceptedAction => 25859; // Glare3
    
    public override bool ShouldActivate(GameStateData gameState) 
    {
        var isWHM = WHMConstants.IsJob(gameState.JobId);
        Logger.Debug($"[WHMCombo] ShouldActivate check: JobId={gameState.JobId}, IsWHM={isWHM}");
        return isWHM;
    }
    
    public override uint Invoke(uint originalAction, GameStateData gameState)
    {
        Logger.Debug($"[WHMCombo] Invoke called: OriginalAction={originalAction}, Level={gameState.Level}, InCombat={gameState.InCombat}, GCD={gameState.GlobalCooldownRemaining:F2}s");
        
        // Demonstrate the cached action resolver - should be very fast on subsequent calls
        var resolvedAction = WHMConstants.ResolveAction(originalAction);
        Logger.Debug($"[WHMCombo] Action resolved: {originalAction} -> {resolvedAction} at level {gameState.Level}");
        
        // Route to appropriate rotation based on context, passing the resolved action
        var result = GetOptimalAction(originalAction, resolvedAction, gameState);
        
        return result;
    }

    #region Rotation Logic
    /// <summary>
    /// Main rotation dispatcher - decides what action to use.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetOptimalAction(uint originalAction, uint resolvedAction, GameStateData gameState)
    {
        // If not in combat or no target, use the original action (don't interfere)
        if (!gameState.InCombat || !gameState.IsValidTarget())
        {
            Logger.Debug($"[WHMCombo] Not in combat or no valid target, using original action: InCombat={gameState.InCombat}, HasTarget={gameState.IsValidTarget()}");
            return originalAction;
        }

        Logger.Debug($"[WHMCombo] Using BasicSingleTargetRotation with resolved action: {resolvedAction}");
        
        // Phase 1: Basic rotation with DoT priority
        return BasicSingleTargetRotation(resolvedAction, gameState);
        
        // TODO: Add AoE rotation, healing priority, etc.
    }

    /// <summary>
    /// Basic single-target DPS rotation:
    /// 1. Apply/refresh Dia if missing or under 5s
    /// 2. Use resolved Glare3 as filler
    /// 3. Future: Blood Lily, Presence of Mind, etc.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint BasicSingleTargetRotation(uint resolvedAction, GameStateData gameState)
    {
        Logger.Debug($"[WHMCombo] BasicSingleTargetRotation: Level={gameState.Level}, ResolvedAction={resolvedAction}, GCD={gameState.GlobalCooldownRemaining:F2}s");
        
        // PRIORITY PHASE: Always check high-priority actions first
        
        // Priority 1: DoT maintenance (highest priority - always check)
        if (ShouldApplyDoT(gameState))
        {
            var dotAction = WHMConstants.DoT;
            Logger.Debug($"[WHMCombo] Should apply DoT: {dotAction}");
            if (dotAction != 0) 
            {
                return dotAction;
            }
        }
        
        // Priority 2: Blood Lily if ready (TODO: needs gauge system)
        // if (ShouldUseMisery(gameState))
        // {
        //     return WHMConstants.AfflatusMisery;
        // }
        
        // FALLBACK PHASE: Use resolved filler action
        Logger.Debug($"[WHMCombo] Using filler action: {resolvedAction}");
        return resolvedAction;
    }
    /// <summary>
    /// Check if we should apply/refresh DoT.
    /// Returns true if target has no DoT or DoT is expiring soon (< 5s).
    /// Uses ultra-minimal decision lockout to prevent double-casting during animation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldApplyDoT(GameStateData gameState)
    {
        var dotAction = WHMConstants.DoT;
        if (dotAction == 0) return false; // No DoT available at this level
        
        // Ultra-minimal lockout: if we decided DoT recently, don't decide again
        var timeSinceLastDecision = DateTime.UtcNow - _lastDoTDecision;
        if (timeSinceLastDecision < _dotDecisionLockout)
        {
            Logger.Debug($"[WHMCombo] DoT decision locked out: {timeSinceLastDecision.TotalMilliseconds:F0}ms < {_dotDecisionLockout.TotalMilliseconds:F0}ms");
            return false;
        }
        
        // Get the corresponding debuff ID for our DoT
        var debuffId = WHMConstants.GetDoTDebuff(dotAction);
        if (debuffId == 0) return false; // Unknown DoT action
        
        // Check if DoT is missing or expiring soon (< 5 seconds)
        var timeRemaining = GameStateCache.GetTargetDebuffTimeRemaining(debuffId);
        var shouldApply = timeRemaining <= 5.0f; // Apply if missing (0.0f) or expiring soon
        
        if (shouldApply)
        {
            // Record the decision time to prevent immediate re-decisions
            _lastDoTDecision = DateTime.UtcNow;
            Logger.Debug($"[WHMCombo] DoT {dotAction} needs refresh: {timeRemaining:F1}s remaining (locked out for {_dotDecisionLockout.TotalMilliseconds:F0}ms)");
        }
        
        return shouldApply;
    }
    #endregion

    #region Future Rotation Additions
    /*
    /// <summary>
    /// Check if we should use Blood Lily (Afflatus Misery).
    /// </summary>
    private static bool ShouldUseMisery(GameStateData gameState)
    {
        // TODO: Check if we have 3 lily stacks
        // return gameState.GetJobGauge().BloodLily >= 3;
        return false;
    }

    /// <summary>
    /// Check if we should use Presence of Mind.
    /// </summary>
    private static bool ShouldUsePresenceOfMind(GameStateData gameState)
    {
        // TODO: Check if PoM is off cooldown and we can weave
        // return gameState.GetActionCooldown(WHMConstants.PresenceOfMind) <= 0 && 
        //        gameState.CanWeave();
        return false;
    }

    /// <summary>
    /// AoE rotation for multiple targets.
    /// </summary>
    private static uint AoERotation(GameStateData gameState)
    {
        // TODO: Holy spam, DoT on 3+ targets, etc.
        return WHMConstants.AoE;
    }
    */
    #endregion
}
