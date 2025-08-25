using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// Ultra-fast SmartTargeting cache core: constants, state, initialization, freshness, and dispose.
/// </summary>
public static unsafe partial class SmartTargetingCache
{

    #region Constants
    private const int MaxPartySize = 8;
    // Legacy constant retained only for historical reference (no runtime usage)
    private const long DefaultCacheRefreshThreshold = 30;
    public const float UNINITIALIZED_MEMBER = -999.0f;
    #endregion

    #region Configurable Settings
    // Primary refresh knob removed; SmartTargeting is fully frame-based
    public static void SetPrimaryCacheRefreshRate(int refreshMs) { }
    public static long GetPrimaryCacheRefreshRate() => 0;
    // Secondary (companion) refresh removed; always frame-based
    #endregion

    #region SIMD-Optimized Party Data
    internal static readonly uint* _memberIds;
    internal static readonly float* _hpPercentages;
    internal static readonly uint* _statusFlags;
    internal static readonly byte* _sortedIndices;
    // SIMD no longer used in current streamlined scalar implementation, keep field removed.

    internal static byte _selfIndex = 255; // 255 = not found

    internal static byte _memberCount;
    internal static long _lastSortTicks;
    internal static long _lastUpdateTicks;
    internal static uint _lastUpdateFrameStamp;
    internal static bool _isInitialized;
    internal static bool _partyChangedThisFrame;

    internal static uint _currentHardTargetId = 0;
    internal static bool _isHardTargetValid = false;

    // Implementation selection removed; scalar-only
    #endregion

    #region Status Flags
    internal const uint AliveFlag = 1u << 0;
    internal const uint InRangeFlag = 1u << 1;
    internal const uint InLosFlag = 1u << 2;
    internal const uint TargetableFlag = 1u << 3;
    internal const uint SelfFlag = 1u << 4;
    internal const uint HardTargetFlag = 1u << 5;
    internal const uint TankFlag = 1u << 6;
    internal const uint HealerFlag = 1u << 7;
    internal const uint MeleeFlag = 1u << 8;
    internal const uint RangedFlag = 1u << 9;
    internal const uint AllyFlag = 1u << 10;

    internal const uint ValidTarget = AllyFlag;
    // Valid ability target requires alive, in range, line of sight, and targetable
    internal const uint ValidAbilityTarget = AliveFlag | InRangeFlag | InLosFlag | TargetableFlag;
    internal const uint RoleMask = TankFlag | HealerFlag | MeleeFlag | RangedFlag;
    #endregion

    #region Static Ctor
    static SmartTargetingCache()
    {
        _memberIds = (uint*)NativeMemory.AlignedAlloc(MaxPartySize * sizeof(uint), 32);
        _hpPercentages = (float*)NativeMemory.AlignedAlloc(MaxPartySize * sizeof(float), 32);
        _statusFlags = (uint*)NativeMemory.AlignedAlloc(MaxPartySize * sizeof(uint), 32);
        _sortedIndices = (byte*)NativeMemory.AlignedAlloc(MaxPartySize * sizeof(byte), 32);

        for (int i = 0; i < MaxPartySize; i++)
        {
            _memberIds[i] = 0;
            _hpPercentages[i] = UNINITIALIZED_MEMBER;
            _statusFlags[i] = 0;
            _sortedIndices[i] = (byte)i;
        }

        _selfIndex = 255;
        _memberCount = 0;
        _lastSortTicks = 0;
        _lastUpdateTicks = 0;
        _isInitialized = false;
    }
    #endregion

    #region Testing Utilities
    public static void ClearForTesting()
    {
        for (int i = 0; i < MaxPartySize; i++)
        {
            _memberIds[i] = 0;
            _hpPercentages[i] = UNINITIALIZED_MEMBER;
            _statusFlags[i] = 0;
            _sortedIndices[i] = (byte)i;
        }
        _selfIndex = 255;
        _memberCount = 0;
        _lastSortTicks = 0;
        _lastUpdateTicks = 0;
        _isInitialized = false;
    }
    #endregion

    #region Core Access & Utility
    public static bool IsReady => _isInitialized && _memberCount > 0;
    public static byte MemberCount => _memberCount;
    public static bool IsFresh => _lastUpdateFrameStamp == GameStateCache.FrameStamp;

    public static string GetDebugInfo()
    {
        if (!IsReady) return "SmartTargeting: Not Ready";
        return IsFresh ? "SmartTargeting: Ready (Fresh)" : "SmartTargeting: Ready (Stale)";
    }

    public static void Dispose()
    {
        if (_memberIds != null) NativeMemory.AlignedFree(_memberIds);
        if (_hpPercentages != null) NativeMemory.AlignedFree(_hpPercentages);
        if (_statusFlags != null) NativeMemory.AlignedFree(_statusFlags);
        if (_sortedIndices != null) NativeMemory.AlignedFree(_sortedIndices);
    }
    #endregion

    // Implementation selection removed

    #region Hard Target Management
    /// <summary>
    /// Update hard target information
    /// </summary>
    public static void UpdateHardTarget(uint targetId, bool isValid)
    {
        if (_currentHardTargetId == targetId && _isHardTargetValid == isValid)
            return;
        _currentHardTargetId = targetId;
        _isHardTargetValid = isValid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetHardTargetId() => _isHardTargetValid ? _currentHardTargetId : 0u;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasValidHardTarget() => _isHardTargetValid && _currentHardTargetId != 0;
    #endregion

    #region Targeting and Validation Methods
    /// <summary>
    /// Core smart targeting: Hard Target > Ally (lowest HP%) > Self
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetSmartTarget(float hpThreshold = 0.99f)
    {
        if (!IsReady) return 0;

        // Use SmartTargetResolver for the actual logic
        return SmartTargetResolver.GetSmartTarget(hpThreshold);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidTarget(uint memberId)
    {
        if (!IsReady) return false;
        var idx = IndexOfMemberId(memberId);
        if (idx < 0) return false;
        
        var flags = _statusFlags[idx];
        var requiredFlags = ValidAbilityTarget | ValidTarget;
        return (flags & requiredFlags) == requiredFlags;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool NeedsHealing(uint memberId, float threshold = 1.0f)
    {
        if (!IsReady) return false;
        var idx = IndexOfMemberId(memberId);
        return idx >= 0 && _hpPercentages[idx] < threshold;
    }
    #endregion
}
