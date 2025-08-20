using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using ModernWrathCombo.Core.Abstracts;
using ModernWrathCombo.Core.Data;
using ModernWrathCombo.Core.Services;

namespace ModernWrathCombo.Core.Services;

/// <summary>
/// Handles action interception and replacement using Dalamud hooks.
/// This class hooks both GetAdjustedActionId (for icon replacement) and UseAction (for execution).
/// Focus is on UseAction for actual combo execution.
/// </summary>
public sealed class ActionInterceptor : IDisposable
{
    private readonly List<CustomCombo> _combos;
    private readonly GameState _gameState;
    private readonly Hook<GetActionDelegate>? _getActionHook;  // Icon replacement (stub)
    private readonly Hook<UseActionDelegate>? _useActionHook; // Actual execution
    private bool _disposed = false;

    // Delegate for the game's GetAdjustedActionId function (icon replacement)
    private unsafe delegate uint GetActionDelegate(IntPtr actionManager, uint actionId);
    
    // Delegate for the game's UseAction function (actual execution)  
    private unsafe delegate bool UseActionDelegate(IntPtr actionManager, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted);

    /// <summary>
    /// Creates a new action interceptor and hooks into the game's action system.
    /// </summary>
    public unsafe ActionInterceptor(GameState gameState, IGameInteropProvider interopProvider)
    {
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        
        // Discover and register combos
        _combos = DiscoverCombos();
        ModernWrathCombo.PluginLog.Information($"[ActionInterceptor] Discovered {_combos.Count} combos:");
        foreach (var combo in _combos)
        {
            ModernWrathCombo.PluginLog.Information($"  - {combo.GetType().Name}: InterceptedAction={combo.InterceptedAction}");
        }
        
        // Hook the game's icon replacement function (stub for now)
        try
        {
            var getActionAddress = ActionManager.MemberFunctionPointers.GetAdjustedActionId;
            _getActionHook = interopProvider.HookFromAddress<GetActionDelegate>(
                getActionAddress, GetAdjustedActionDetour);
            _getActionHook?.Enable();
            
            ModernWrathCombo.PluginLog.Information("✓ Icon replacement hook enabled (stub)");
        }
        catch (Exception ex)
        {
            ModernWrathCombo.PluginLog.Error(ex, "Failed to hook icon replacement");
        }
        
        // Hook the game's action execution function (main logic)
        try
        {
            var useActionAddress = ActionManager.MemberFunctionPointers.UseAction;
            _useActionHook = interopProvider.HookFromAddress<UseActionDelegate>(
                useActionAddress, UseActionDetour);
            _useActionHook?.Enable();
            
            ModernWrathCombo.PluginLog.Information($"✓ Action execution hook enabled with {_combos.Count} combos");
        }
        catch (Exception ex)
        {
            ModernWrathCombo.PluginLog.Error(ex, "Failed to hook action execution");
        }
    }

    /// <summary>
    /// Discovers all CustomCombo implementations in the assembly.
    /// </summary>
    private static List<CustomCombo> DiscoverCombos()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(CustomCombo)))
            .Select(t => Activator.CreateInstance(t))
            .Cast<CustomCombo>()
            .OrderBy(c => c.InterceptedAction) // Order for consistent processing
            .ToList();
    }

    /// <summary>
    /// The hook detour for icon replacement - currently a stub.
    /// This controls what icons/tooltips show on hotbars but doesn't affect actual execution.
    /// Called constantly for UI updates, so no logging to avoid spam.
    /// </summary>
    private unsafe uint GetAdjustedActionDetour(IntPtr actionManager, uint actionId)
    {
        try
        {
            // TODO: Add icon replacement logic here when needed
            // This would change what icon shows on hotbars for combo actions
            
            // Return original action (no icon replacement yet)
            return _getActionHook!.Original(actionManager, actionId);
        }
        catch (Exception ex)
        {
            ModernWrathCombo.PluginLog.Error(ex, $"Error in icon replacement for action {actionId}");
            return _getActionHook?.Original(actionManager, actionId) ?? actionId;
        }
    }

    /// <summary>
    /// The main hook detour for action execution - this is where combo logic happens.
    /// This is called when the player actually executes an action.
    /// </summary>
    private unsafe bool UseActionDetour(IntPtr actionManager, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        try
        {
            // Only process actual actions/abilities, not other types
            if (actionType != ActionType.Action)
            {
                return _useActionHook!.Original(actionManager, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
            }

            // Log action execution for debugging
            Logger.Debug($"[ActionInterceptor] UseAction called: ActionId={actionId}, ActionType={actionType}");
            
            // Use cached game state instead of live API calls (ultra-fast!)
            var gameStateSnapshot = GameStateCache.CreateSnapshot();
            
            Logger.Debug($"[ActionInterceptor] Game state: JobId={gameStateSnapshot.JobId}, Level={gameStateSnapshot.Level}, InCombat={gameStateSnapshot.InCombat}");
            
            // Try each combo to see if any wants to handle this action
            foreach (var combo in _combos)
            {
                Logger.Debug($"[ActionInterceptor] Checking combo {combo.GetType().Name}: InterceptedAction={combo.InterceptedAction}, ActionId={actionId}");
                
                if (combo.TryInvoke(actionId, gameStateSnapshot, out var replacementAction))
                {
                    // Combo handled the action - execute replacement (or original if no change)
                    var finalAction = replacementAction;
                    
                    if (actionId != replacementAction)
                    {
                        Logger.Information($"[ActionInterceptor] Action replaced: {actionId} → {replacementAction} by {combo.GetType().Name}");
                    }
                    else
                    {
                        Logger.Debug($"[ActionInterceptor] Action processed but not replaced: {actionId}");
                    }
                    
                    // Record the executed action for preemptive cache updates
                    GameStateCache.RecordActionExecution(finalAction);
                    
                    // Execute the final action (replacement or original)
                    return _useActionHook!.Original(actionManager, actionType, finalAction, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
                }
                else
                {
                    Logger.Debug($"[ActionInterceptor] Combo {combo.GetType().Name} did not handle action {actionId}");
                }
            }
            
            // No combo handled this action, execute original
            return _useActionHook!.Original(actionManager, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
        }
        catch (Exception ex)
        {
            ModernWrathCombo.PluginLog.Error(ex, $"Error in action execution for action {actionId}");
            // Fallback to original action if anything goes wrong
            return _useActionHook?.Original(actionManager, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted) ?? false;
        }
    }

    /// <summary>
    /// Gets the original action ID (before any modifications).
    /// </summary>
    public unsafe uint GetOriginalAction(uint actionId)
    {
        return _getActionHook?.Original(IntPtr.Zero, actionId) ?? actionId;
    }

    /// <summary>
    /// Registers a new combo for action interception.
    /// </summary>
    public void RegisterCombo(CustomCombo combo)
    {
        if (!_combos.Contains(combo))
        {
            _combos.Add(combo);
            ModernWrathCombo.PluginLog.Information($"✓ Registered combo for action {combo.InterceptedAction}");
        }
    }

    /// <summary>
    /// Unregisters a combo from action interception.
    /// </summary>
    public void UnregisterCombo(CustomCombo combo)
    {
        if (_combos.Remove(combo))
        {
            ModernWrathCombo.PluginLog.Information($"✓ Unregistered combo for action {combo.InterceptedAction}");
        }
    }

    /// <summary>
    /// Gets the number of registered combos.
    /// </summary>
    public int ComboCount => _combos.Count;

    public void Dispose()
    {
        if (_disposed) return;
        
        _getActionHook?.Disable();
        _getActionHook?.Dispose();
        _useActionHook?.Disable();
        _useActionHook?.Dispose();
        _combos.Clear();
        
        _disposed = true;
        ModernWrathCombo.PluginLog.Information("Action interceptor disposed");
    }
}
