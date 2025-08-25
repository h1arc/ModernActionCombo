using System;
using System.Runtime.CompilerServices;

namespace ModernActionCombo.Core.Data;

public static unsafe partial class SmartTargetingCache
{
    // No-op debug trace API retained for compatibility; logging removed
    public static void SetDebugTraceEnabled(bool enabled) { }
    public static bool IsDebugTraceEnabled => false;

    public static void UpdatePartyData(
        Span<uint> memberIds,
        Span<float> hpPercentages,
        Span<uint> statusFlags,
        byte memberCount)
    {
        // Fast no-op path: if party composition and state are identical, just bump freshness and return
        // This avoids unnecessary memory writes and re-sorts when nothing changed.
        const float Epsilon = 1e-4f;
        if (memberCount == _memberCount && _memberCount > 0 && memberCount <= MaxPartySize)
        {
            bool identical = true;
            for (int i = 0; i < memberCount; i++)
            {
                if (_memberIds[i] != memberIds[i]) { identical = false; break; }
                // Compare HP with small tolerance to ignore jitter
                float delta = _hpPercentages[i] - hpPercentages[i];
                if (delta < 0) delta = -delta;
                if (delta > Epsilon) { identical = false; break; }
                if (_statusFlags[i] != statusFlags[i]) { identical = false; break; }
            }

            if (identical)
            {
                _lastUpdateTicks = Environment.TickCount64;
                _lastUpdateFrameStamp = GameStateCache.FrameStamp;
                _partyChangedThisFrame = false;
                // Keep _isInitialized as-is
                return;
            }
        }

        if (memberCount == 0)
        {
            _memberCount = 0;
            _selfIndex = 255;
            _lastUpdateTicks = Environment.TickCount64;
            _lastSortTicks = 0;
            _isInitialized = false;
            return;
        }
        if (memberCount > MaxPartySize) memberCount = MaxPartySize;

        _memberCount = memberCount;
        _selfIndex = 255;
        for (int i = 0; i < memberCount; i++) _sortedIndices[i] = (byte)i;
        for (int i = 0; i < memberCount; i++)
        {
            _memberIds[i] = memberIds[i];
            _hpPercentages[i] = hpPercentages[i];
            _statusFlags[i] = statusFlags[i];
            if ((statusFlags[i] & SelfFlag) != 0) _selfIndex = (byte)i;
        }
        for (int i = memberCount; i < MaxPartySize; i++)
        {
            _memberIds[i] = 0;
            _hpPercentages[i] = UNINITIALIZED_MEMBER;
            _statusFlags[i] = 0;
        }
        _lastUpdateTicks = Environment.TickCount64;
        _lastUpdateFrameStamp = GameStateCache.FrameStamp;
        _lastSortTicks = 0;
        _isInitialized = true;
    _partyChangedThisFrame = true;
    }

    // Companion system state and updates
    private static uint _cachedCompanionId = 0;
    private static float _cachedCompanionHp = 1.0f;
    private static bool _cachedCompanionValid = false;
    private static uint _lastCompanionFrameStamp = 0;
    private const uint CompanionGraceFrames = 3; // allow a short grace window to use last-known companion
    private static readonly object _companionScanLock = new object();
    private static bool _companionSystemEnabled = false;
    private static bool _inDutyFlag = false;

    public static void UpdateCompanionSystemState(bool enabled, bool inDuty)
    {
        lock (_companionScanLock)
        {
            _companionSystemEnabled = enabled;
            _inDutyFlag = inDuty;
            if (!enabled || inDuty)
            {
                _cachedCompanionId = 0;
                _cachedCompanionHp = 1.0f;
                _cachedCompanionValid = false;
                _lastCompanionFrameStamp = 0;
            }
        }
    }

    public static void UpdateCompanionData(uint companionId, float hpPercent, bool isValid)
    {
        lock (_companionScanLock)
        {
            if (!_companionSystemEnabled || _inDutyFlag)
            {
                _cachedCompanionId = 0;
                _cachedCompanionHp = 1.0f;
                _cachedCompanionValid = false;
                _lastCompanionFrameStamp = 0;
                return;
            }
            // Coalesce identical updates within a frame
            if (_cachedCompanionId == companionId && _cachedCompanionValid == isValid && Math.Abs(_cachedCompanionHp - hpPercent) < 1e-4f && _lastCompanionFrameStamp == GameStateCache.FrameStamp)
            {
                return;
            }
            _cachedCompanionId = companionId;
            _cachedCompanionHp = hpPercent;
            _cachedCompanionValid = isValid;
            _lastCompanionFrameStamp = GameStateCache.FrameStamp;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBestCompanionTarget(float hpThreshold)
    {
        return GetBestCompanionTarget(hpThreshold, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetBestCompanionTarget(float hpThreshold, out float compHp)
    {
        lock (_companionScanLock)
        {
            compHp = 1.0f;
            if (!_companionSystemEnabled || _inDutyFlag) return 0;
            // Accept within grace window to account for throttled scans
            var frame = GameStateCache.FrameStamp;
            var freshEnough = _lastCompanionFrameStamp != 0 && (frame == _lastCompanionFrameStamp || (frame > _lastCompanionFrameStamp && (frame - _lastCompanionFrameStamp) <= CompanionGraceFrames));
            if (freshEnough && _cachedCompanionValid && _cachedCompanionId != 0 && _cachedCompanionHp > 0.0f && _cachedCompanionHp < hpThreshold)
            {
                compHp = _cachedCompanionHp;
                return _cachedCompanionId;
            }
            return 0;
        }
    }

    public static bool HasValidCompanion()
    {
        lock (_companionScanLock)
        {
            var frame = GameStateCache.FrameStamp;
            return _cachedCompanionValid && _cachedCompanionId != 0 && (frame == _lastCompanionFrameStamp || (frame > _lastCompanionFrameStamp && (frame - _lastCompanionFrameStamp) <= CompanionGraceFrames));
        }
    }

    public static float GetCompanionHpPercent()
    {
        lock (_companionScanLock) { return _cachedCompanionHp; }
    }

    public static uint GetCompanionId()
    {
        lock (_companionScanLock) { return _cachedCompanionId; }
    }
}
