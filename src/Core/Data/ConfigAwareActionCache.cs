using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// Ultra-fast configuration-aware action cache that tracks config versions.
/// Eliminates the need to clear cache on config changes by versioning cache entries.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ConfigAwareActionCache
{
    private const int MaxCacheSize = 128;
    private const long CacheTimeoutTicks = 2_000_000; // 200ms in ticks
    
    // Cache entries with config version tracking
    private fixed uint _actionIds[MaxCacheSize];
    private fixed uint _resolvedIds[MaxCacheSize];
    private fixed long _timestamps[MaxCacheSize];
    private fixed uint _configVersions[MaxCacheSize]; // NEW: Track config version per entry
    private int _count;
    
    // Global config version - incremented when any job config changes
    private static uint _globalConfigVersion = 1;
    
    /// <summary>
    /// Increments the global config version, effectively invalidating all cached entries.
    /// This is called when any configuration changes, but doesn't require clearing the cache.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IncrementConfigVersion()
    {
        _globalConfigVersion++;
        // If we overflow, reset to 1 (0 is reserved for uninitialized)
        if (_globalConfigVersion == 0) _globalConfigVersion = 1;
    }
    
    /// <summary>
    /// Gets the current global config version.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetConfigVersion() => _globalConfigVersion;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetCached(uint actionId, out uint resolvedId)
    {
        var now = DateTime.UtcNow.Ticks;
        var currentConfigVersion = _globalConfigVersion;
        
        fixed (uint* actionIds = _actionIds)
        fixed (uint* resolvedIds = _resolvedIds)
        fixed (long* timestamps = _timestamps)
        fixed (uint* configVersions = _configVersions)
        {
            for (int i = 0; i < _count; i++)
            {
                if (actionIds[i] == actionId)
                {
                    // Check if cache entry is still valid (time AND config version)
                    if (now - timestamps[i] < CacheTimeoutTicks && 
                        configVersions[i] == currentConfigVersion)
                    {
                        resolvedId = resolvedIds[i];
                        return true;
                    }
                    
                    // Cache expired or config changed - remove entry
                    RemoveAtUnsafe(i, actionIds, resolvedIds, timestamps, configVersions);
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
        var currentConfigVersion = _globalConfigVersion;
        
        fixed (uint* actionIds = _actionIds)
        fixed (uint* resolvedIds = _resolvedIds)
        fixed (long* timestamps = _timestamps)
        fixed (uint* configVersions = _configVersions)
        {
            // If cache is full, remove oldest entry
            if (_count >= MaxCacheSize)
            {
                RemoveOldestUnsafe(actionIds, resolvedIds, timestamps, configVersions);
            }
            
            // Add new entry with current config version
            actionIds[_count] = actionId;
            resolvedIds[_count] = resolvedId;
            timestamps[_count] = now;
            configVersions[_count] = currentConfigVersion; // NEW: Store config version
            _count++;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveAtUnsafe(int index, uint* actionIds, uint* resolvedIds, long* timestamps, uint* configVersions)
    {
        if (index >= _count) return;
        
        // Shift remaining elements
        for (int i = index; i < _count - 1; i++)
        {
            actionIds[i] = actionIds[i + 1];
            resolvedIds[i] = resolvedIds[i + 1];
            timestamps[i] = timestamps[i + 1];
            configVersions[i] = configVersions[i + 1]; // NEW: Shift config versions too
        }
        _count--;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RemoveOldestUnsafe(uint* actionIds, uint* resolvedIds, long* timestamps, uint* configVersions)
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
        
        RemoveAtUnsafe(oldestIndex, actionIds, resolvedIds, timestamps, configVersions);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _count = 0;
    }
}
