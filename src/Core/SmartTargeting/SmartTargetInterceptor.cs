using System;
using System.Runtime.CompilerServices;
using Dalamud.Hooking;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Lightweight smart targeting system that handles soft targeting and action resolution for specific abilities.
/// 
/// Handles execution-time targeting by intercepting UseAction calls and applying smart targeting.
/// Also handles action resolution (e.g., Liturgy → LiturgyBurst) at execution time to complement 
/// ActionInterceptor's icon replacement functionality.
///
/// Only intercepts abilities listed in SmartTargetRules - everything else passes through untouched.
/// </summary>
public sealed unsafe class SmartTargetInterceptor : IDisposable
{
    // Hook for UseAction to intercept targeting at action execution time
    private Hook<UseActionDelegate>? _useActionHook;

    // Delegate for UseAction hook
    private delegate bool UseActionDelegate(IntPtr actionManager, uint actionType, uint actionId, ulong targetId, uint param, uint useType, int pvp, IntPtr a8);

    public SmartTargetInterceptor()
    {
        InitializeHook();
    }

    private void InitializeHook()
    {
        try
        {
            // Hook UseAction to intercept action execution and modify targeting
            var useActionAddress = (nint)ActionManager.Addresses.UseAction.Value;

            if (useActionAddress == IntPtr.Zero) return;

            _useActionHook = ModernActionCombo.GameInteropProvider.HookFromAddress<UseActionDelegate>(
                useActionAddress, UseActionDetour);

            if (_useActionHook != null) _useActionHook.Enable();
        }
        catch
        {
            // swallow to avoid spam during init
        }
    }

    /// <summary>
    /// Intercepts UseAction calls to apply smart targeting and action resolution for registered abilities.
    /// Uses soft targeting - temporarily changes target, executes action, then restores original target.
    /// Also resolves action IDs (e.g., Liturgy → LiturgyBurst) for execution-time replacement.
    /// </summary>
    private bool UseActionDetour(IntPtr actionManager, uint actionType, uint actionId, ulong targetId, uint param, uint useType, int pvp, IntPtr a8)
    {
        // Resolve action ID first (e.g., Liturgy → burst)
        var resolvedActionId = SmartTargetResolver.GetResolvedActionId(actionId);

        // Determine optimal target via resolver
        var optimalTargetId = SmartTargetResolver.GetOptimalTarget(resolvedActionId);
        if (optimalTargetId == 0)
            return _useActionHook!.Original(actionManager, actionType, resolvedActionId, targetId, param, useType, pvp, a8);

        // Find game object for location if needed
        var optimalTarget = FindGameObjectById(optimalTargetId);
        if (optimalTarget == null)
            return _useActionHook!.Original(actionManager, actionType, resolvedActionId, targetId, param, useType, pvp, a8);

        // Execute with either UseAction or UseActionLocation handled inside
        return ExecuteWithSoftTarget(actionManager, actionType, resolvedActionId, optimalTarget, param, useType, pvp, a8);
    }

    /// <summary>
    /// Executes an action with smart targeting - handles both regular and ground-target abilities.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ExecuteWithSoftTarget(IntPtr actionManager, uint actionType, uint actionId, IGameObject newTarget, uint param, uint useType, int pvp, IntPtr a8)
    {
        // Decide based on rules if this action should be ground-placed
        if (TryExecuteGroundTarget(actionManager, actionType, actionId, newTarget, param))
            return true;

        // Otherwise, regular UseAction with selected target
        return _useActionHook!.Original(actionManager, actionType, actionId, newTarget.GameObjectId, param, useType, pvp, a8);
    }

    /// <summary>
    /// Executes a ground-target action at the specified target's location.
    /// Uses UseActionLocation for proper ground targeting.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryExecuteGroundTarget(IntPtr actionManager, uint actionType, uint actionId, IGameObject target, uint param)
    {
        // Determine if the rule requires ground placement
        if (!SmartTargetResolver.TryGetRule(actionId, out var rule)) return false;
        if (rule.Mode != TargetingMode.GroundTarget && rule.Mode != TargetingMode.GroundTargetSpecial) return false;
        // For GroundTargetSpecial (e.g., Liturgy), only the primary ActionId should be placed on the ground.
        if (rule.Mode == TargetingMode.GroundTargetSpecial && actionId != rule.ActionId) return false;

        var targetPosition = target.Position;
        unsafe
        {
            var actionManagerPtr = (FFXIVClientStructs.FFXIV.Client.Game.ActionManager*)actionManager;
            if (actionManagerPtr == null) return false;
            return actionManagerPtr->UseActionLocation(
                (FFXIVClientStructs.FFXIV.Client.Game.ActionType)actionType,
                actionId,
                target.GameObjectId,
                &targetPosition,
                param
            );
        }
    }

    // Ground-target decision is driven by SmartTargetResolver rules; no local table needed.

    /// <summary>
    /// Cleanup method - not needed for soft targeting approach.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RestoreOriginalTarget() { }

    /// <summary>
    /// Finds a game object by its ID from the object table.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IGameObject? FindGameObjectById(uint gameObjectId)
    {
        // Fast path: use per-frame known object map if available
        if (GameStateCache.TryGetKnownObject(gameObjectId, out var known) && known != null)
            return known;

        // Check local player first (most common case for self-targeting)
        var localPlayer = ModernActionCombo.ClientState.LocalPlayer;
        if (localPlayer?.GameObjectId == gameObjectId)
        {
            return localPlayer;
        }

        // Search object table
        foreach (var obj in ModernActionCombo.ObjectTable)
        {
            if (obj?.GameObjectId == gameObjectId)
            {
                return obj;
            }
        }

        return null;
    }

    public void Dispose()
    {
        try
        {
            _useActionHook?.Disable();
            _useActionHook?.Dispose();
        }
        catch
        {
            // ignore
        }
    }
}
