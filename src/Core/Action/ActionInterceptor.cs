using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Action interception for ComboGrid actions
/// </summary>
public enum ActionInterceptionMode : byte
{
    /// <summary>Standard mode - changes the action icon but keeps the original action ID.</summary>
    Standard = 0,
    /// <summary>Direct Input mode - directly replaces the action at execution time.</summary>
    DirectInput = 1
}

/// <summary>
/// High-performance action cache using .NET 9 value types and aggressive optimizations.
/// Designed for <50ns lookup times with zero allocations in hot paths.
/// Uses unsafe fixed arrays for maximum performance.
/// </summary>
// ActionCache moved to ActionCache.cs for clarity.

/// <summary>
/// Simplified, focused ActionInterceptor.
/// Only cares about returning the correct actionId - nothing else.
/// Uses .NET 9 high-performance ActionCache for <50ns cache lookups.
/// Leverages GameStateCache for 30ms cached state data instead of live snapshots.
/// </summary>
public sealed unsafe partial class ActionInterceptor : IDisposable
{
    private readonly IGameInteropProvider _gameInteropProvider;
    
    // Current interception mode
    public static ActionInterceptionMode Mode { get; set; } = ActionInterceptionMode.Standard;
    
    // Hooks for different modes
    private Hook<GetAdjustedActionDelegate>? _getAdjustedActionHook;  // Standard
    private Hook<UseActionDelegate>? _useActionHook;                  // Direct Input
    
    // Hot-path 2-way set-associative cache for resolved action IDs (100ms TTL)
    private ActionCache _cache = new();

    // Per-frame memoization for OGCD-enabled providers to avoid repeated work within a frame
    private uint _memoFrameStamp;
    private uint _memoActionId;
    private uint _memoResolvedId;
    private bool _memoValid;
    
    // Delegates
    private delegate uint GetAdjustedActionDelegate(IntPtr actionManager, uint actionId);
    private delegate bool UseActionDelegate(IntPtr actionManager, uint actionType, uint actionId, ulong targetId, uint param, uint useType, int pvp, IntPtr a8);

    public ActionInterceptor(IGameInteropProvider gameInteropProvider)
    {
        _gameInteropProvider = gameInteropProvider ?? throw new ArgumentNullException(nameof(gameInteropProvider));
        
        InitializeHook();
    }

    private void InitializeHook()
    {
        try
        {
            InitializeStandardHook();
            
            // Direct Input hook will be initialized when switching modes
            Logger.Info($"✅ ActionInterceptor initialized in {Mode} mode");
        }
        catch (Exception ex)
        {
            Logger.Error($"ActionInterceptor initialization failed: {ex}");
        }
    }
    
    private void InitializeStandardHook()
    {
        // hook from Dalamud's ActionManager address
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
    Logger.Debug($"✅ Standard hook enabled: 0x{actionManagerAddress:X}");
        }
        else
        {
    Logger.Error("❌ Failed to create GetAdjustedAction hook");
        }
    }
    
    private void InitializeDirectInputHook()
    {
        // Hook UseAction for direct input
        var useActionAddress = (nint)ActionManager.Addresses.UseAction.Value;
        
        if (useActionAddress == IntPtr.Zero)
        {
            Logger.Error("❌ ActionManager.Addresses.UseAction is null");
            return;
        }

        _useActionHook = _gameInteropProvider.HookFromAddress<UseActionDelegate>(
            useActionAddress, UseActionDetour);
            
        if (_useActionHook != null)
        {
            _useActionHook.Enable();
            Logger.Debug($"✅ Direct Input hook enabled: 0x{useActionAddress:X}");
        }
        else
        {
            Logger.Error("❌ Failed to create UseAction hook");
        }
    }

    /// <summary>
    /// The core detour method for Standard mode
    /// Only job: return the correct action ID.
    /// Uses GameStateCache directly for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private uint GetAdjustedActionDetour(IntPtr actionManager, uint actionId)
    {
        try
        {
            var hook = _getAdjustedActionHook;
            if (hook is null)
                return actionId;

            if (Mode != ActionInterceptionMode.Standard)
                return hook.Original(actionManager, actionId);

            // If severely throttled and not in combat, avoid extra logic and return original mapping
            if (Core.Runtime.PerformanceController.IsDegraded && !GameStateCache.InCombat)
                return hook.Original(actionManager, actionId);

            if (!IsLikelyValidAction(actionId))
                return hook.Original(actionManager, actionId);

            // SmartTarget-specific handling is performed exclusively by SmartTargetInterceptor.
            // ActionInterceptor remains agnostic and only resolves combo/OGCD actions.

            var hasOgcd = JobProviderRegistry.HasOGCDSupport();
            if (!hasOgcd && _cache.TryGetCached(actionId, out var cached))
                return cached;

            // Per-frame memoization when OGCD support is enabled (dynamic conditions),
            // to avoid repeated evaluations within the same frame for identical inputs
            if (hasOgcd)
            {
                var frame = GameStateCache.FrameStamp;
                if (_memoValid && _memoFrameStamp == frame && _memoActionId == actionId)
                    return _memoResolvedId;

                var memoState = GetGameStateFromCache();
                var resolvedNow = JobProviderRegistry.ResolveAction(actionId, memoState);
                _memoFrameStamp = frame;
                _memoActionId = actionId;
                _memoResolvedId = resolvedNow;
                _memoValid = true;
                return resolvedNow;
            }

            var state = GetGameStateFromCache();
            var resolved = JobProviderRegistry.ResolveAction(actionId, state);

            if (!hasOgcd)
                _cache.Cache(actionId, resolved);

            return resolved;
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in GetAdjustedActionDetour: {ex}");
            return _getAdjustedActionHook?.Original(actionManager, actionId) ?? actionId;
        }
    }

    /// <summary>
    /// Direct Input detour for UseAction - directly executes resolved actions.
    /// This bypasses icon replacement entirely.
    /// Ultra-fast path for maximum performance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool UseActionDetour(IntPtr actionManager, uint actionType, uint actionId, ulong targetId, uint param, uint useType, int pvp, IntPtr a8)
    {
        try
        {
            var hook = _useActionHook;
            if (hook is null)
                return false;

            // Only process normal actions (actionType 1) in Direct Input mode
            if (actionType != 1 || Mode != ActionInterceptionMode.DirectInput)
                return hook.Original(actionManager, actionType, actionId, targetId, param, useType, pvp, a8);

            // If severely throttled and not in combat, bypass resolve and execute original
            if (Core.Runtime.PerformanceController.IsDegraded && !GameStateCache.InCombat)
                return hook.Original(actionManager, actionType, actionId, targetId, param, useType, pvp, a8);

            if (!IsLikelyValidAction(actionId))
                return hook.Original(actionManager, actionType, actionId, targetId, param, useType, pvp, a8);

            var hasOgcd = JobProviderRegistry.HasOGCDSupport();
            uint resolved;
            if (!hasOgcd && _cache.TryGetCached(actionId, out var cached))
            {
                resolved = cached;
            }
            else
            {
                if (hasOgcd)
                {
                    var frame = GameStateCache.FrameStamp;
                    if (_memoValid && _memoFrameStamp == frame && _memoActionId == actionId)
                    {
                        resolved = _memoResolvedId;
                    }
                    else
                    {
                        var memoState = GetGameStateFromCache();
                        resolved = JobProviderRegistry.ResolveAction(actionId, memoState);
                        _memoFrameStamp = frame;
                        _memoActionId = actionId;
                        _memoResolvedId = resolved;
                        _memoValid = true;
                    }
                }
                else
                {
                    var state = GetGameStateFromCache();
                    resolved = JobProviderRegistry.ResolveAction(actionId, state);
                }
                if (!hasOgcd)
                    _cache.Cache(actionId, resolved);
            }

            return hook.Original(actionManager, actionType, resolved, targetId, param, useType, pvp, a8);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in UseActionDetour: {ex}");
            return _useActionHook?.Original(actionManager, actionType, actionId, targetId, param, useType, pvp, a8) ?? false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLikelyValidAction(uint actionId) => actionId != 0 && actionId <= 100_000;

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
    Logger.Debug("Action cache cleared");
    }

    /// <summary>
    /// Gets the current action interception mode.
    /// </summary>
    public ActionInterceptionMode GetCurrentMode() => Mode;

    /// <summary>
    /// Checks if Direct Input mode is available and functioning.
    /// </summary>
    public bool IsDirectInputAvailable()
    {
        return ActionManager.Addresses.UseAction.Value != IntPtr.Zero;
    }

    /// <summary>
    /// Switches between action interception modes with proper hook management.
    /// Standard: Changes hotbar icons via GetAdjustedActionId hook
    /// Direct Input: Direct action execution via UseAction hook for maximum performance
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
            case ActionInterceptionMode.Standard:
                InitializeStandardHook();
                break;
                
            case ActionInterceptionMode.DirectInput:
                InitializeDirectInputHook();
                break;
                
            default:
                Logger.Warning($"⚠️ Unknown mode {newMode} - falling back to Standard");
                Mode = ActionInterceptionMode.Standard;
                InitializeStandardHook();
                break;
        }
        
    Logger.Info($"🔄 Action interception mode: {oldMode} → {Mode}");
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
            Logger.Error($"Error disabling current mode hooks: {ex}");
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
            Logger.Info("ActionInterceptor disposed");
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"Error disposing ActionInterceptor: {ex}");
        }
    }
}
