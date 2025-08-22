using System;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using ModernActionCombo.Core.Data;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Action interception modes based on WrathCombo's two strategies.
/// </summary>
public enum ActionInterceptionMode : byte
{
    /// <summary>Icon replacement mode - changes the action icon but keeps the original action ID.</summary>
    IconReplacement = 0,
    /// <summary>Performance mode - directly replaces the action at execution time.</summary>
    PerformanceMode = 1
}

/// <summary>
/// High-performance action cache using .NET 9 value types and aggressive optimizations.
/// Designed for <50ns lookup times with zero allocations in hot paths.
/// Uses unsafe fixed arrays for maximum performance.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ActionCache
{
    private const int MaxCacheSize = 64; // Smaller for better cache locality
    private const long CacheTimeoutTicks = 1_000_000; // 100ms in ticks
    
    // Fixed arrays for ultra-fast access - no heap allocations
    private fixed uint _actionIds[MaxCacheSize];
    private fixed uint _resolvedIds[MaxCacheSize];
    private fixed long _timestamps[MaxCacheSize];
    private int _count;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetCached(uint actionId, out uint resolvedId)
    {
        var now = DateTime.UtcNow.Ticks;
        
        fixed (uint* actionIds = _actionIds)
        fixed (uint* resolvedIds = _resolvedIds)
        fixed (long* timestamps = _timestamps)
        {
            for (int i = 0; i < _count; i++)
            {
                if (actionIds[i] == actionId)
                {
                    // Check if cache entry is still valid
                    if (now - timestamps[i] < CacheTimeoutTicks)
                    {
                        resolvedId = resolvedIds[i];
                        return true;
                    }
                    
                    // Cache expired - remove entry
                    RemoveAtUnsafe(i, actionIds, resolvedIds, timestamps);
                    break;
                }
            }
        }
        
        resolvedId = 0;
        return false;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Cache(uint actionId, uint resolvedId)
    {
        var now = DateTime.UtcNow.Ticks;
        
        fixed (uint* actionIds = _actionIds)
        fixed (uint* resolvedIds = _resolvedIds)
        fixed (long* timestamps = _timestamps)
        {
            // If cache is full, remove oldest entry
            if (_count >= MaxCacheSize)
            {
                RemoveOldestUnsafe(actionIds, resolvedIds, timestamps);
            }
            
            // Add new entry
            actionIds[_count] = actionId;
            resolvedIds[_count] = resolvedId;
            timestamps[_count] = now;
            _count++;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveAtUnsafe(int index, uint* actionIds, uint* resolvedIds, long* timestamps)
    {
        if (index >= _count) return;
        
        // Shift remaining elements using unsafe pointer arithmetic
        for (int i = index; i < _count - 1; i++)
        {
            actionIds[i] = actionIds[i + 1];
            resolvedIds[i] = resolvedIds[i + 1];
            timestamps[i] = timestamps[i + 1];
        }
        _count--;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveOldestUnsafe(uint* actionIds, uint* resolvedIds, long* timestamps)
    {
        if (_count == 0) return;
        
        int oldestIndex = 0;
        var oldestTime = timestamps[0];
        
        for (int i = 1; i < _count; i++)
        {
            if (timestamps[i] < oldestTime)
            {
                oldestTime = timestamps[i];
                oldestIndex = i;
            }
        }
        
        RemoveAtUnsafe(oldestIndex, actionIds, resolvedIds, timestamps);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _count = 0;
        // No need to clear the memory as we're just resetting the count
    }
}

/// <summary>
/// Simplified, focused ActionInterceptor based on WrathCombo's proven GetAdjustedActionId pattern.
/// Only cares about returning the correct actionId - nothing else.
/// Uses .NET 9 high-performance ActionCache for <50ns cache lookups.
/// Leverages GameStateCache for 30ms cached state data instead of live snapshots.
/// </summary>
public sealed unsafe class ActionInterceptor : IDisposable
{
    private readonly GameState _gameState;
    private readonly IGameInteropProvider _gameInteropProvider;
    
    // Current interception mode
    public static ActionInterceptionMode Mode { get; set; } = ActionInterceptionMode.IconReplacement;
    
    // Single hook for icon replacement (WrathCombo's proven approach)
    private Hook<GetAdjustedActionDelegate>? _getAdjustedActionHook;
    
    // High-performance .NET 9 cache using unsafe fixed arrays
    private ActionCache _cache = new();
    
    // Critical for the hook - copied exactly from WrathCombo
    private IntPtr _actionManager = IntPtr.Zero;
    
    // Delegate matching WrathCombo exactly
    private delegate uint GetAdjustedActionDelegate(IntPtr actionManager, uint actionId);

    public ActionInterceptor(GameState gameState, IGameInteropProvider gameInteropProvider)
    {
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        _gameInteropProvider = gameInteropProvider ?? throw new ArgumentNullException(nameof(gameInteropProvider));
        
        InitializeHook();
    }

    private void InitializeHook()
    {
        try
        {
            // Use WrathCombo's exact approach - hook from Dalamud's ActionManager address
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
                ModernActionCombo.PluginLog?.Info($"✅ ActionInterceptor initialized with Dalamud address: 0x{actionManagerAddress:X}");
            }
            else
            {
                ModernActionCombo.PluginLog?.Error("❌ Failed to create hook from Dalamud address");
            }
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"ActionInterceptor initialization failed: {ex}");
        }
    }

    /// <summary>
    /// The core detour method - copied from WrathCombo pattern but simplified.
    /// Only job: return the correct action ID.
    /// Uses GameStateCache directly for maximum performance.
    /// </summary>
    private uint GetAdjustedActionDetour(IntPtr actionManager, uint actionId)
    {
        try
        {
            // Safety checks
            if (_getAdjustedActionHook == null || actionId == 0 || actionId > 100000)
            {
                return _getAdjustedActionHook?.Original(actionManager, actionId) ?? actionId;
            }

            // Debug logging to see what actions are being intercepted
            // Note: This runs 60+ times per second - only log errors in production
            
            // Check if this action might resolve to OGCDs (dynamic conditions)
            var hasOGCDSupport = JobProviderRegistry.HasOGCDSupport();
            
            // Check cache first for performance (but skip for OGCD-enabled actions)
            if (!hasOGCDSupport && _cache.TryGetCached(actionId, out uint cachedResult))
            {
                return cachedResult;
            }
            
            // Get the resolved action from our combo system using cached game state
            var gameStateData = GetGameStateFromCache();
            var resolvedActionId = JobProviderRegistry.ResolveAction(actionId, gameStateData);

            // Cache the result (but skip caching for OGCD-enabled actions due to dynamic conditions)
            if (!hasOGCDSupport)
            {
                _cache.Cache(actionId, resolvedActionId);
            }

            // Return the resolved action (or original if no change)
            return resolvedActionId;
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"Error in GetAdjustedActionDetour: {ex}");
            return _getAdjustedActionHook?.Original(actionManager, actionId) ?? actionId;
        }
    }

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
        ModernActionCombo.PluginLog?.Debug("Action cache cleared");
    }

    /// <summary>
    /// Switches between action interception modes.
    /// Note: Currently only IconReplacement is implemented.
    /// </summary>
    public void SwitchMode(ActionInterceptionMode newMode)
    {
        if (Mode == newMode)
            return;

        var oldMode = Mode;
        Mode = newMode;
        
        // Clear cache when switching modes
        ClearCache();
        
        // For now, we only have IconReplacement implemented
        // PerformanceMode would require additional UseAction hooks
        if (newMode == ActionInterceptionMode.PerformanceMode)
        {
            ModernActionCombo.PluginLog?.Warning("⚠️ Performance Mode not yet implemented - staying in Icon Replacement mode");
            Mode = ActionInterceptionMode.IconReplacement;
            return;
        }
        
        ModernActionCombo.PluginLog?.Info($"🔄 Action interception mode: {oldMode} → {Mode}");
    }

    public void Dispose()
    {
        try
        {
            _getAdjustedActionHook?.Disable();
            _getAdjustedActionHook?.Dispose();
            ClearCache();
            ModernActionCombo.PluginLog?.Info("ActionInterceptor disposed");
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"Error disposing ActionInterceptor: {ex}");
        }
    }
}
