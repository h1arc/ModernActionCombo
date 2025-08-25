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

    // Cache entries with config version tracking
    private fixed uint _actionIds[MaxCacheSize];
    private fixed uint _resolvedIds[MaxCacheSize];
    private fixed uint _configVersions[MaxCacheSize];
    private int _count;
    private int _evictCursor; // simple round-robin evict index when full

    // Global config version - incremented when any job config changes (atomic)
    private static int _globalConfigVersion = 1;

    /// <summary>
    /// Increments the global config version, effectively invalidating all cached entries.
    /// This is called when any configuration changes, but doesn't require clearing the cache.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IncrementConfigVersion()
    {
        var v = Interlocked.Increment(ref _globalConfigVersion);
        // Keep non-zero (0 reserved) if wrapped
        if (v == 0)
        {
            // Rare wrap-around path: set to 1
            Interlocked.Exchange(ref _globalConfigVersion, 1);
        }
    }

    /// <summary>
    /// Gets the current global config version.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetConfigVersion() => unchecked((uint)Volatile.Read(ref _globalConfigVersion));

    /// <summary>
    /// Explicitly invalidates all cache entries across all instances by advancing the global version.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InvalidateAll() => IncrementConfigVersion();

    /// <summary>
    /// Clears this instance's storage without touching the global version.
    /// Useful when the owning scope wants per-instance reset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        _count = 0;
        _evictCursor = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetCached(uint actionId, out uint resolvedId)
    {
        var currentConfigVersion = Volatile.Read(ref _globalConfigVersion);
        fixed (uint* actionIds = _actionIds)
        fixed (uint* resolvedIds = _resolvedIds)
        fixed (uint* configVersions = _configVersions)
        {
            var span = new ReadOnlySpan<uint>(actionIds, _count);
            int idx = span.IndexOf(actionId);
            if (idx >= 0)
            {
                if (configVersions[idx] == (uint)currentConfigVersion)
                {
                    resolvedId = resolvedIds[idx];
                    return true;
                }
                // Config changed for this entry: remove O(1)
                RemoveAtUnsafe(idx, actionIds, resolvedIds, configVersions);
            }
        }
        resolvedId = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Cache(uint actionId, uint resolvedId)
    {
        var currentConfigVersion = Volatile.Read(ref _globalConfigVersion);
        fixed (uint* actionIds = _actionIds)
        fixed (uint* resolvedIds = _resolvedIds)
        fixed (uint* configVersions = _configVersions)
        {
            // Overwrite if already present
            var span = new ReadOnlySpan<uint>(actionIds, _count);
            int idx = span.IndexOf(actionId);
            if (idx >= 0)
            {
                resolvedIds[idx] = resolvedId;
                configVersions[idx] = (uint)currentConfigVersion;
                return;
            }

            if (_count < MaxCacheSize)
            {
                int i = _count++;
                actionIds[i] = actionId;
                resolvedIds[i] = resolvedId;
                configVersions[i] = (uint)currentConfigVersion;
            }
            else
            {
                // Simple round-robin eviction when full
                int i = _evictCursor;
                if (++_evictCursor >= MaxCacheSize) _evictCursor = 0;
                actionIds[i] = actionId;
                resolvedIds[i] = resolvedId;
                configVersions[i] = (uint)currentConfigVersion;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void RemoveAtUnsafe(int index, uint* actionIds, uint* resolvedIds, uint* configVersions)
    {
        if ((uint)index >= (uint)_count) return;
        int last = _count - 1;
        if (index != last)
        {
            actionIds[index] = actionIds[last];
            resolvedIds[index] = resolvedIds[last];
            configVersions[index] = configVersions[last];
        }
        _count = last;
        if (_evictCursor > _count) _evictCursor = 0; // keep cursor in range
    }

    // Removed oldest-by-time eviction; explicit invalidation via version or Clear()
}
