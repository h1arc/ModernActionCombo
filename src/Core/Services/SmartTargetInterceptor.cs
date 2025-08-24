using System;
using System.Runtime.CompilerServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
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
/// Inspired by WrathCombo's ActionRetargeting system but optimized for our use case.
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
            
            if (useActionAddress == IntPtr.Zero)
            {
                ModernActionCombo.PluginLog?.Error("❌ ActionManager.Addresses.UseAction is null");
                return;
            }

            _useActionHook = ModernActionCombo.GameInteropProvider.HookFromAddress<UseActionDelegate>(
                useActionAddress, UseActionDetour);
                
            if (_useActionHook != null)
            {
                _useActionHook.Enable();
                ModernActionCombo.PluginLog?.Info($"✅ SmartTargetInterceptor initialized with UseAction address: 0x{useActionAddress:X}");
            }
            else
            {
                ModernActionCombo.PluginLog?.Error("❌ Failed to create UseAction hook");
            }
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"SmartTargetInterceptor initialization failed: {ex}");
        }
    }
    
    /// <summary>
    /// Intercepts UseAction calls to apply smart targeting and action resolution for registered abilities.
    /// Uses soft targeting - temporarily changes target, executes action, then restores original target.
    /// Also resolves action IDs (e.g., Liturgy → LiturgyBurst) for execution-time replacement.
    /// </summary>
    private bool UseActionDetour(IntPtr actionManager, uint actionType, uint actionId, ulong targetId, uint param, uint useType, int pvp, IntPtr a8)
    {
        try
        {
            // Only process normal actions (actionType 1)
            if (actionType != 1)
            {
                return _useActionHook!.Original(actionManager, actionType, actionId, targetId, param, useType, pvp, a8);
            }
            
            // First resolve the action ID (e.g., Liturgy → LiturgyBurst when buff is active)
            var resolvedActionId = SmartTargetResolver.GetResolvedActionId(actionId);
            
            // Check if this action supports smart targeting (use resolved action ID)
            var optimalTargetId = SmartTargetResolver.GetOptimalTarget(resolvedActionId);
            
            if (optimalTargetId == 0)
            {
                // No smart targeting needed, pass through with resolved action
                return _useActionHook!.Original(actionManager, actionType, resolvedActionId, targetId, param, useType, pvp, a8);
            }
            
            // Find the optimal target game object
            var optimalTarget = FindGameObjectById(optimalTargetId);
            if (optimalTarget == null)
            {
                // Can't find the target, use resolved action
                return _useActionHook!.Original(actionManager, actionType, resolvedActionId, targetId, param, useType, pvp, a8);
            }
            
            // Apply soft targeting with resolved action
            return ExecuteWithSoftTarget(actionManager, actionType, resolvedActionId, optimalTarget, param, useType, pvp, a8);
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"Error in SmartTargetInterceptor.UseActionDetour: {ex}");
            return _useActionHook?.Original(actionManager, actionType, actionId, targetId, param, useType, pvp, a8) ?? false;
        }
    }
    
    /// <summary>
    /// Executes an action with smart targeting - handles both regular and ground-target abilities.
    /// Based on WrathCombo's ActionRetargeting approach.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ExecuteWithSoftTarget(IntPtr actionManager, uint actionType, uint actionId, IGameObject newTarget, uint param, uint useType, int pvp, IntPtr a8)
    {
        // Check if this is a ground-target special ability
        bool isGroundTargetSpecial = IsGroundTargetSpecial(actionId);
        bool result;
        
        if (isGroundTargetSpecial)
        {
            // For ground-target abilities, we need to use UseActionLocation instead of UseAction
            // Place the ground effect at the target's position
            result = ExecuteGroundTargetAction(actionManager, actionType, actionId, newTarget, param, useType, pvp, a8);
        }
        else
        {
            // Regular ability - simply call the original UseAction with the smart target's ID
            // This is "soft targeting" - no need to change the player's actual target
            result = _useActionHook!.Original(actionManager, actionType, actionId, newTarget.GameObjectId, param, useType, pvp, a8);
        }
        
        // Log successful retargeting (only for debugging when needed)
        if (result)
        {
            var targetType = isGroundTargetSpecial ? "ground-targeted" : "smart targeted";
            ModernActionCombo.PluginLog?.Verbose($"🎯 {targetType} {actionId} to {newTarget.Name}");
        }
        
        return result;
    }
    
    /// <summary>
    /// Executes a ground-target action at the specified target's location.
    /// Uses UseActionLocation for proper ground targeting.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ExecuteGroundTargetAction(IntPtr actionManager, uint actionType, uint actionId, IGameObject target, uint param, uint useType, int pvp, IntPtr a8)
    {
        try
        {
            // Get the target's position for ground targeting
            var targetPosition = target.Position;
            
            // For ground-target abilities, we use UseActionLocation with the target's position
            // This places the ground effect at the target's location rather than retargeting the ability
            unsafe
            {
                var actionManagerPtr = (FFXIVClientStructs.FFXIV.Client.Game.ActionManager*)actionManager;
                if (actionManagerPtr != null)
                {
                    // Use UseActionLocation to place the ground effect at the target's position
                    return actionManagerPtr->UseActionLocation(
                        (FFXIVClientStructs.FFXIV.Client.Game.ActionType)actionType,
                        actionId,
                        target.GameObjectId,
                        &targetPosition,
                        param
                    );
                }
            }
            
            // Fallback to original UseAction if UseActionLocation fails
            return _useActionHook!.Original(actionManager, actionType, actionId, target.GameObjectId, param, useType, pvp, a8);
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"Error executing ground-target action {actionId}: {ex}");
            // Fallback to original UseAction
            return _useActionHook!.Original(actionManager, actionType, actionId, target.GameObjectId, param, useType, pvp, a8);
        }
    }
    
    /// <summary>
    /// Checks if an action is a ground-target ability that needs location-based targeting.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsGroundTargetSpecial(uint actionId)
    {
        // WHM ground-target abilities that need UseActionLocation
        return actionId switch
        {
            3569 => true,   // Asylum (always ground target)
            25862 => true,  // Liturgy of the Bell (initial placement - ground target)
            28509 => false, // Liturgy of the Bell burst (secondary action - uses normal smart targeting)
            _ => false
        };
    }
    
    /// <summary>
    /// Cleanup method - not needed for soft targeting approach.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RestoreOriginalTarget()
    {
        // Not needed for soft targeting - we're not changing the player's actual target
    }
    
    /// <summary>
    /// Finds a game object by its ID from the object table.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IGameObject? FindGameObjectById(uint gameObjectId)
    {
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
            ModernActionCombo.PluginLog?.Info("SmartTargetInterceptor disposed");
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"Error disposing SmartTargetInterceptor: {ex}");
        }
    }
}
