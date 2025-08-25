using System;
using System.Runtime.CompilerServices;

namespace ModernActionCombo.Core.Data;

public static unsafe partial class SmartTargetingCache
{
    private static uint _lastSortFrameStamp;
    // Delegate index search to helpers (SIMD or Scalar)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOfMemberId(uint memberId)
    {
        int count = _memberCount;
        for (int i = 0; i < count; i++)
            if (_memberIds[i] == memberId) return i;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetMemberHpPercent(int index)
    {
        if (index < 0 || index >= _memberCount) return 0.0f;
        return _hpPercentages[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetMemberHpPercent(uint memberId)
    {
        var idx = IndexOfMemberId(memberId);
        return idx >= 0 ? _hpPercentages[idx] : 0.0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetMemberIdByIndex(int index)
    {
        if (index < 0 || index >= _memberCount) return 0;
        return _memberIds[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetMemberStatusFlags(uint memberId)
    {
        var idx = IndexOfMemberId(memberId);
        return idx >= 0 ? _statusFlags[idx] : 0u;
    }

    public static byte PartyCount => _memberCount;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetLowestHpTarget()
    {
        if (_memberCount == 0) return 0;
        int count = _memberCount;

        uint bestMember = 0;
        float bestHp = 1.0f;
        for (int i = 0; i < count; i++)
        {
            if ((_statusFlags[i] & ValidAbilityTarget) == ValidAbilityTarget && (_statusFlags[i] & ValidTarget) != 0)
            {
                float hp = _hpPercentages[i];
                if (hp > 0.0f && hp < bestHp)
                {
                    bestHp = hp;
                    bestMember = _memberIds[i];
                }
            }
        }
        return bestMember;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidHealTarget(int index)
    {
        if (index < 0 || index >= MaxPartySize) return false;
        return (_statusFlags[index] & ValidAbilityTarget) == ValidAbilityTarget &&
               (_statusFlags[index] & ValidTarget) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSelf(int index)
    {
        if (index < 0 || index >= MaxPartySize) return false;
        return (_statusFlags[index] & SelfFlag) != 0;
    }

    /// <summary>
    /// Gets the self member's ID. Returns 0 if self not found in party.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetSelfId()
    {
        return _selfIndex < _memberCount ? _memberIds[_selfIndex] : 0;
    }

    /// <summary>
    /// Check if a member ID is a tank
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTank(uint memberId)
    {
        var idx = IndexOfMemberId(memberId);
        return idx >= 0 && (_statusFlags[idx] & TankFlag) != 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SortByHpPercentage()
    {
        // Sort at most once per GameState frame to unify cadence with the global cache
        var frame = GameStateCache.FrameStamp;
        if (frame == _lastSortFrameStamp) return;

        // Note: We avoid mutating HP based on alive mask in-place to keep state simple and deterministic.

        for (byte i = 1; i < _memberCount; i++)
        {
            var currentIdx = _sortedIndices[i];
            byte j = i;
            while (j > 0 && ShouldSwapMembers(_sortedIndices[j - 1], currentIdx))
            {
                _sortedIndices[j] = _sortedIndices[j - 1];
                j--;
            }
            _sortedIndices[j] = currentIdx;
        }

    _lastSortFrameStamp = frame;
    _lastSortTicks = Environment.TickCount64;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldSwapMembers(byte aIdx, byte bIdx)
    {
        var aAlive = (_statusFlags[aIdx] & AliveFlag) != 0;
        var bAlive = (_statusFlags[bIdx] & AliveFlag) != 0;
        if (aAlive != bAlive) return bAlive;
        if (!aAlive && !bAlive) return false;

        var aHp = _hpPercentages[aIdx];
        var bHp = _hpPercentages[bIdx];
        return bHp < aHp;
    }
}
