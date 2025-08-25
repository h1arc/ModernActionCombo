using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// 2-way set-associative action cache with small, fixed capacity and per-frame scope.
/// Entries are valid only for the current GameStateCache.FrameStamp to guarantee coherence
/// with the global state snapshot. A small TTL is retained for eviction heuristics.
/// - 32 sets x 2 ways = 64 total slots
/// - O(1) probe: compute set index from hash and check up to 2 ways
/// - MRU swap on second-way hit
/// - Evicts expired first, otherwise oldest in the set
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct ActionCache
{
    private const int Sets = 32;
    private const int Ways = 2;
    private const int Capacity = Sets * Ways; // 64
    // TTL removed: cache validity is scoped strictly to GameStateCache.FrameStamp

    private fixed uint _keys[Capacity];
    private fixed uint _values[Capacity];
    private fixed long _timestamps[Capacity];
    private fixed uint _frameStamps[Capacity];
    private fixed byte _used[Capacity];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSetIndex(uint key)
    {
        // Simple avalanche hash and mask to [0, Sets)
        uint h = key ^ (key >> 16);
        return (int)(h & (Sets - 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public bool TryGetCached(uint actionId, out uint resolvedId)
    {
        long now = Environment.TickCount64;

        fixed (uint* keys = _keys)
        fixed (uint* values = _values)
        fixed (long* timestamps = _timestamps)
        fixed (byte* used = _used)
        {
            int set = GetSetIndex(actionId);
            int baseIdx = set * Ways;
            // way 0
            int i0 = baseIdx;
            if (used[i0] != 0 && keys[i0] == actionId)
            {
                // Frame-scoped validation: must match current GameState frame
                if (_frameStamps[i0] == GameStateCache.FrameStamp)
                {
                    resolvedId = values[i0];
                    return true;
                }
                // cross-frame -> evict
                used[i0] = 0;
                resolvedId = 0;
                return false;
            }

            // way 1
            int i1 = baseIdx + 1;
            if (used[i1] != 0 && keys[i1] == actionId)
            {
                if (_frameStamps[i1] == GameStateCache.FrameStamp)
                {
                    resolvedId = values[i1];
                    // MRU swap with way 0
                    uint k = keys[i1]; keys[i1] = keys[i0]; keys[i0] = k;
                    uint v = values[i1]; values[i1] = values[i0]; values[i0] = v;
                    long t = timestamps[i1]; timestamps[i1] = timestamps[i0]; timestamps[i0] = t;
                    uint fs = _frameStamps[i1]; _frameStamps[i1] = _frameStamps[i0]; _frameStamps[i0] = fs;
                    byte u = used[i1]; used[i1] = used[i0]; used[i0] = u;
                    return true;
                }
                // cross-frame -> evict
                used[i1] = 0;
                resolvedId = 0;
                return false;
            }
        }

        resolvedId = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void Cache(uint actionId, uint resolvedId)
    {
        long now = Environment.TickCount64;

        fixed (uint* keys = _keys)
        fixed (uint* values = _values)
        fixed (long* timestamps = _timestamps)
        fixed (byte* used = _used)
        {
            int set = GetSetIndex(actionId);
            int baseIdx = set * Ways;
            int i0 = baseIdx;
            int i1 = baseIdx + 1;

            // Overwrite in-place if key already present (rare if TryGet not used)
            if (used[i0] != 0 && keys[i0] == actionId)
            {
                values[i0] = resolvedId;
                timestamps[i0] = now;
                _frameStamps[i0] = GameStateCache.FrameStamp;
                return;
            }
            if (used[i1] != 0 && keys[i1] == actionId)
            {
                values[i1] = resolvedId;
                timestamps[i1] = now;
                _frameStamps[i1] = GameStateCache.FrameStamp;
                // Keep MRU in way 0
                uint k = keys[i1]; keys[i1] = keys[i0]; keys[i0] = k;
                uint v = values[i1]; values[i1] = values[i0]; values[i0] = v;
                long t = timestamps[i1]; timestamps[i1] = timestamps[i0]; timestamps[i0] = t;
                uint fs = _frameStamps[i1]; _frameStamps[i1] = _frameStamps[i0]; _frameStamps[i0] = fs;
                byte u = used[i1]; used[i1] = used[i0]; used[i0] = u;
                return;
            }

            // Prefer free slot
            if (used[i0] == 0)
            {
                keys[i0] = actionId;
                values[i0] = resolvedId;
                timestamps[i0] = now;
                _frameStamps[i0] = GameStateCache.FrameStamp;
                used[i0] = 1;
                return;
            }
            if (used[i1] == 0)
            {
                keys[i1] = actionId;
                values[i1] = resolvedId;
                timestamps[i1] = now;
                _frameStamps[i1] = GameStateCache.FrameStamp;
                used[i1] = 1;
                // keep MRU in way 0 -> swap
                uint k = keys[i1]; keys[i1] = keys[i0]; keys[i0] = k;
                uint v = values[i1]; values[i1] = values[i0]; values[i0] = v;
                long t = timestamps[i1]; timestamps[i1] = timestamps[i0]; timestamps[i0] = t;
                uint fs = _frameStamps[i1]; _frameStamps[i1] = _frameStamps[i0]; _frameStamps[i0] = fs;
                byte u = used[i1]; used[i1] = used[i0]; used[i0] = u;
                return;
            }

            // Both used: choose oldest by timestamp (informational), frame mismatch already handled above
            int victim = (timestamps[i0] <= timestamps[i1] ? i0 : i1);

            keys[victim] = actionId;
            values[victim] = resolvedId;
            timestamps[victim] = now;
            _frameStamps[victim] = GameStateCache.FrameStamp;
            used[victim] = 1;

            // Keep MRU in way 0
            if (victim == i1)
            {
                uint k = keys[i1]; keys[i1] = keys[i0]; keys[i0] = k;
                uint v = values[i1]; values[i1] = values[i0]; values[i0] = v;
                long t = timestamps[i1]; timestamps[i1] = timestamps[i0]; timestamps[i0] = t;
                uint fs = _frameStamps[i1]; _frameStamps[i1] = _frameStamps[i0]; _frameStamps[i0] = fs;
                byte u = used[i1]; used[i1] = used[i0]; used[i0] = u;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear()
    {
        fixed (byte* used = _used)
        {
            for (int i = 0; i < Capacity; i++) used[i] = 0;
        }
    }
}
