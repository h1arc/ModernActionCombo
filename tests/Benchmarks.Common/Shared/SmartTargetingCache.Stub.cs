using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ModernActionCombo.Core.Data
{
    // Shared benchmark stub: dependency-free SmartTargeting minimal API with SIMD + scalar fallback
    public static class SmartTargetingCache
    {
        public const int MaxPartySize = 128; // allow larger N for synthetic scaling
        public const float UNINITIALIZED_MEMBER = -999.0f;
    public static bool DisableSimd = true; // default to scalar like production

        // Public flags used by benchmarks
        public const uint AliveFlag = 1u << 0;
        public const uint InRangeFlag = 1u << 1;
        public const uint InLosFlag = 1u << 2;
        public const uint TargetableFlag = 1u << 3;
        public const uint SelfFlag = 1u << 4;
        public const uint AllyFlag = 1u << 10;
        public const uint ValidTarget = AllyFlag;
        public const uint ValidAbilityTarget = AliveFlag | InRangeFlag | InLosFlag | TargetableFlag;

        private static uint[] _memberIds = new uint[MaxPartySize];
        private static float[] _hp = new float[MaxPartySize];
        private static uint[] _flags = new uint[MaxPartySize];
        private static int _count;

        public static void UpdatePartyData(Span<uint> memberIds, Span<float> hpPercentages, Span<uint> statusFlags, byte memberCount)
        {
            int n = Math.Min(memberCount, (byte)MaxPartySize);
            _count = n;
            memberIds.Slice(0, n).CopyTo(_memberIds);
            hpPercentages.Slice(0, n).CopyTo(_hp);
            statusFlags.Slice(0, n).CopyTo(_flags);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetMemberHpPercent(uint memberId)
        {
            int idx = IndexOfMemberId(memberId);
            return idx >= 0 ? _hp[idx] : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int IndexOfMemberId(uint memberId)
        {
            int n = _count;
            if (n <= 0) return -1;
            if (!DisableSimd && Vector.IsHardwareAccelerated && n >= Vector<uint>.Count)
            {
                var ids = _memberIds;
                int width = Vector<uint>.Count;
                var target = new Vector<uint>(memberId);
                int i = 0;
                for (; i <= n - width; i += width)
                {
                    var vec = new Vector<uint>(ids, i);
                    var eq = Vector.Equals(vec, target);
                    for (int lane = 0; lane < width; lane++)
                    {
                        if (eq[lane] != 0u) return i + lane;
                    }
                }
                for (; i < n; i++) if (ids[i] == memberId) return i;
                return -1;
            }
            for (int i = 0; i < n; i++) if (_memberIds[i] == memberId) return i;
            return -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetLowestHpTarget()
        {
            int n = _count;
            if (n == 0) return 0;

            uint bestId = 0;
            float bestHp = 1.0f;

            if (!DisableSimd && Vector.IsHardwareAccelerated && n >= Vector<float>.Count)
            {
                int width = Vector<float>.Count;
                int i = 0;
                for (; i <= n - width; i += width)
                {
                    var hpVec = new Vector<float>(_hp, i);
                    var fVec = new Vector<uint>(_flags, i);
                    var va = new Vector<uint>(ValidAbilityTarget);
                    var vt = new Vector<uint>(ValidTarget);
                    var vaOk = Vector.Equals(fVec & va, va);
                    var vtMasked = fVec & vt;
                    for (int lane = 0; lane < width; lane++)
                    {
                        if (vaOk[lane] != 0u && vtMasked[lane] != 0u)
                        {
                            float hp = hpVec[lane];
                            if (hp > 0.0f && hp < bestHp)
                            {
                                bestHp = hp;
                                bestId = _memberIds[i + lane];
                            }
                        }
                    }
                }
                for (; i < n; i++)
                {
                    if ((_flags[i] & ValidAbilityTarget) == ValidAbilityTarget && (_flags[i] & ValidTarget) != 0)
                    {
                        float hp = _hp[i];
                        if (hp > 0.0f && hp < bestHp) { bestHp = hp; bestId = _memberIds[i]; }
                    }
                }
                return bestId;
            }

            for (int i = 0; i < n; i++)
            {
                if ((_flags[i] & ValidAbilityTarget) == ValidAbilityTarget && (_flags[i] & ValidTarget) != 0)
                {
                    float hp = _hp[i];
                    if (hp > 0.0f && hp < bestHp) { bestHp = hp; bestId = _memberIds[i]; }
                }
            }
            return bestId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetSmartTarget(float hpThreshold = 1.0f)
        {
            // Simplified: choose lowest HP under threshold using same validity rule
            int n = _count;
            if (n == 0) return 0;
            uint bestId = 0;
            float bestHp = 1.0f;
            if (!DisableSimd && Vector.IsHardwareAccelerated && n >= Vector<float>.Count)
            {
                int width = Vector<float>.Count; int i = 0;
                for (; i <= n - width; i += width)
                {
                    var hpVec = new Vector<float>(_hp, i);
                    var fVec = new Vector<uint>(_flags, i);
                    var va = new Vector<uint>(ValidAbilityTarget);
                    var vt = new Vector<uint>(ValidTarget);
                    var vaOk = Vector.Equals(fVec & va, va);
                    var vtMasked = fVec & vt;
                    for (int lane = 0; lane < width; lane++)
                    {
                        if (vaOk[lane] != 0u && vtMasked[lane] != 0u)
                        {
                            float hp = hpVec[lane];
                            if (hp > 0.0f && hp < bestHp && hp < hpThreshold)
                            { bestHp = hp; bestId = _memberIds[i + lane]; }
                        }
                    }
                }
                for (; i < n; i++)
                {
                    if ((_flags[i] & ValidAbilityTarget) == ValidAbilityTarget && (_flags[i] & ValidTarget) != 0)
                    {
                        float hp = _hp[i];
                        if (hp > 0.0f && hp < bestHp && hp < hpThreshold) { bestHp = hp; bestId = _memberIds[i]; }
                    }
                }
                return bestId;
            }
            for (int i = 0; i < n; i++)
            {
                if ((_flags[i] & ValidAbilityTarget) == ValidAbilityTarget && (_flags[i] & ValidTarget) != 0)
                {
                    float hp = _hp[i];
                    if (hp > 0.0f && hp < bestHp && hp < hpThreshold) { bestHp = hp; bestId = _memberIds[i]; }
                }
            }
            return bestId;
        }
    }
}
