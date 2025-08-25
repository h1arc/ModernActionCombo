using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ModernActionCombo.Core.Data
{
    // Shared benchmark stub: minimal, dependency-free implementation that mirrors the API shape
    public static partial class GameStateCache
    {
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct CoreState
        {
            public uint JobId;
            public uint Level;
            public uint TargetId;
            public uint ZoneId;
            public uint Flags;
            public uint Gauge1;
            public uint Gauge2;
            public uint Timestamp;
        }

        [Flags]
        private enum StateFlags : uint
        {
            None = 0,
            InCombat = 1u << 0,
            HasTarget = 1u << 1,
            InDuty = 1u << 2,
            CanAct = 1u << 3,
            IsMoving = 1u << 4,
        }

        public const float UNINITIALIZED_SENTINEL = -999.0f;
        private const long UNINITIALIZED_TICKS = long.MinValue;

        private static CoreState _core;
        private static float _gcdRemaining;
        private static uint _currentMp;
        private static uint _maxMp;
        private static long _lastUpdateTicks;
        private static bool _isInitialized;

        private static readonly Dictionary<uint, long> _playerBuffsExpiry = new();
        private static readonly Dictionary<uint, long> _targetDebuffsExpiry = new();
        private static readonly Dictionary<uint, long> _actionCooldownsExpiry = new();

        private const int JobIdIndex = 0;
        private const int LevelIndex = 1;
        private const int TargetIdIndex = 2;
        private const int ZoneIdIndex = 3;
        private const int FlagsIndex = 4;
        private const int GaugeData1Index = 5;
        private const int GaugeData2Index = 6;
        private const int TimestampIndex = 7;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref uint Lane(int index)
        {
            switch (index)
            {
                case 0: return ref _core.JobId;
                case 1: return ref _core.Level;
                case 2: return ref _core.TargetId;
                case 3: return ref _core.ZoneId;
                case 4: return ref _core.Flags;
                case 5: return ref _core.Gauge1;
                case 6: return ref _core.Gauge2;
                case 7: return ref _core.Timestamp;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        static GameStateCache()
        {
            _core = default;
            _lastUpdateTicks = Environment.TickCount64;
        }

        public static uint JobId => Lane(JobIdIndex);
        public static uint Level => Lane(LevelIndex);
        public static uint TargetId => Lane(TargetIdIndex);
        public static uint ZoneId => Lane(ZoneIdIndex);
        public static float GcdRemaining => _gcdRemaining;
        public static bool InCombat => ((StateFlags)Lane(FlagsIndex) & StateFlags.InCombat) != 0;
        public static bool HasTarget => ((StateFlags)Lane(FlagsIndex) & StateFlags.HasTarget) != 0;
        public static bool InDuty => ((StateFlags)Lane(FlagsIndex) & StateFlags.InDuty) != 0;
        public static bool CanUseAbilities => ((StateFlags)Lane(FlagsIndex) & StateFlags.CanAct) != 0;
        public static bool IsMoving => ((StateFlags)Lane(FlagsIndex) & StateFlags.IsMoving) != 0;
        public static bool CanProcessCombos => InCombat && CanUseAbilities;
        public static uint CurrentMp => _currentMp;
        public static uint MaxMp => _maxMp;
        public static float MpPercentage => _maxMp > 0 ? (float)_currentMp / _maxMp : 0f;
        public static bool IsMpLow(float threshold = 0.3f) => MpPercentage < threshold;
        public static bool HasMpFor(uint cost) => _currentMp >= cost;
        public static bool IsInitialized => _isInitialized;
        public static uint GetGaugeData1() => Lane(GaugeData1Index);
        public static uint GetGaugeData2() => Lane(GaugeData2Index);
        public static bool CanWeave(int ogcdCount = 1)
        {
            if (_gcdRemaining <= 0) return true;
            var timeNeeded = 0.8f * ogcdCount;
            return _gcdRemaining >= timeNeeded;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void UpdateCoreState(
            uint jobId,
            uint level,
            uint targetId,
            uint zoneId,
            bool inCombat,
            bool hasTarget,
            bool inDuty,
            bool canAct,
            bool isMoving,
            uint gaugeData1 = 0,
            uint gaugeData2 = 0)
        {
            StateFlags flags = StateFlags.None;
            if (inCombat) flags |= StateFlags.InCombat;
            if (hasTarget) flags |= StateFlags.HasTarget;
            if (inDuty) flags |= StateFlags.InDuty;
            if (canAct) flags |= StateFlags.CanAct;
            if (isMoving) flags |= StateFlags.IsMoving;

            Lane(JobIdIndex) = jobId;
            Lane(LevelIndex) = level;
            Lane(TargetIdIndex) = targetId;
            Lane(ZoneIdIndex) = zoneId;
            Lane(FlagsIndex) = (uint)flags;
            Lane(GaugeData1Index) = gaugeData1;
            Lane(GaugeData2Index) = gaugeData2;
            Lane(TimestampIndex) = (uint)Environment.TickCount;

            _lastUpdateTicks = Environment.TickCount64;
            _isInitialized = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void UpdateScalarState(float gcdRemaining, uint currentMp, uint maxMp)
        {
            _gcdRemaining = gcdRemaining;
            _currentMp = currentMp;
            _maxMp = maxMp;
        }

        public static long TimeSinceLastUpdate => Environment.TickCount64 - _lastUpdateTicks;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStale(long thresholdMs = 100) => TimeSinceLastUpdate > thresholdMs;

    // Note: Snapshot API intentionally omitted in shared stub to avoid type conflicts

        public static float GetTargetDebuffTimeRemaining(uint debuffId)
        {
            if (_targetDebuffsExpiry.TryGetValue(debuffId, out var expiry))
            {
                if (expiry == UNINITIALIZED_TICKS) return UNINITIALIZED_SENTINEL;
                var now = Environment.TickCount64;
                var remaining = (expiry - now) / 1000.0f;
                return remaining > 0 ? remaining : 0.0f;
            }
            return 0.0f;
        }

        public static bool TargetHasDebuff(uint debuffId)
        {
            var timeRemaining = GetTargetDebuffTimeRemaining(debuffId);
            return timeRemaining > 0;
        }

        public static bool IsDebuffTrackingInitialized(uint debuffId) => _targetDebuffsExpiry.ContainsKey(debuffId);

        public static bool HasPlayerBuff(uint buffId)
            => GetPlayerBuffTimeRemaining(buffId) > 0;

        public static float GetPlayerBuffTimeRemaining(uint buffId)
        {
            if (_playerBuffsExpiry.TryGetValue(buffId, out var expiry))
            {
                if (expiry == UNINITIALIZED_TICKS) return UNINITIALIZED_SENTINEL;
                var now = Environment.TickCount64;
                var remaining = (expiry - now) / 1000.0f;
                return remaining > 0 ? remaining : 0.0f;
            }
            return 0.0f;
        }

        public static bool IsBuffTrackingInitialized(uint buffId) => _playerBuffsExpiry.ContainsKey(buffId);

        public static bool IsActionReady(uint actionId)
        {
            var cooldown = GetActionCooldown(actionId);
            if (cooldown == UNINITIALIZED_SENTINEL)
                return false;
            return cooldown <= 0;
        }

        public static float GetActionCooldown(uint actionId)
        {
            if (_actionCooldownsExpiry.TryGetValue(actionId, out var expiry))
            {
                if (expiry == UNINITIALIZED_TICKS) return UNINITIALIZED_SENTINEL;
                var now = Environment.TickCount64;
                var remaining = (expiry - now) / 1000.0f;
                return remaining > 0 ? remaining : 0.0f;
            }
            return 0.0f;
        }

        public static bool IsCooldownTrackingInitialized(uint actionId) => _actionCooldownsExpiry.ContainsKey(actionId);

        public static bool IsOGCDReady(uint actionId)
        {
            if (!CanProcessCombos) return false;
            var cooldown = GetActionCooldown(actionId);
            if (cooldown == UNINITIALIZED_SENTINEL) return false;
            return cooldown <= 0;
        }

        public static void UpdatePlayerBuffs(Dictionary<uint, float> buffs)
        {
            var now = Environment.TickCount64;
            foreach (var kv in buffs)
            {
                if (!_playerBuffsExpiry.ContainsKey(kv.Key)) _playerBuffsExpiry[kv.Key] = UNINITIALIZED_TICKS;
            }
            var keysToUpdate = new List<uint>(_playerBuffsExpiry.Keys);
            foreach (var buffId in keysToUpdate)
            {
                if (buffs.TryGetValue(buffId, out var remainingSec))
                {
                    _playerBuffsExpiry[buffId] = remainingSec == UNINITIALIZED_SENTINEL ? UNINITIALIZED_TICKS : now + (long)(remainingSec * 1000.0f);
                }
                else if (_playerBuffsExpiry[buffId] != UNINITIALIZED_TICKS)
                {
                    _playerBuffsExpiry[buffId] = now;
                }
            }
        }

        public static void UpdateTargetDebuffs(Dictionary<uint, float> debuffs)
        {
            var now = Environment.TickCount64;
            foreach (var kv in debuffs)
            {
                if (!_targetDebuffsExpiry.ContainsKey(kv.Key)) _targetDebuffsExpiry[kv.Key] = UNINITIALIZED_TICKS;
            }
            var keysToUpdate = new List<uint>(_targetDebuffsExpiry.Keys);
            foreach (var debuffId in keysToUpdate)
            {
                if (debuffs.TryGetValue(debuffId, out var remainingSec))
                {
                    _targetDebuffsExpiry[debuffId] = remainingSec == UNINITIALIZED_SENTINEL ? UNINITIALIZED_TICKS : now + (long)(remainingSec * 1000.0f);
                }
                else if (_targetDebuffsExpiry[debuffId] != UNINITIALIZED_TICKS)
                {
                    _targetDebuffsExpiry[debuffId] = now;
                }
            }
        }

        public static void UpdateActionCooldowns(Dictionary<uint, float> cooldowns)
        {
            var now = Environment.TickCount64;
            foreach (var kv in cooldowns)
            {
                if (!_actionCooldownsExpiry.ContainsKey(kv.Key)) _actionCooldownsExpiry[kv.Key] = UNINITIALIZED_TICKS;
            }
            var keysToUpdate = new List<uint>(_actionCooldownsExpiry.Keys);
            foreach (var actionId in keysToUpdate)
            {
                if (cooldowns.TryGetValue(actionId, out var remainingSec))
                {
                    _actionCooldownsExpiry[actionId] = remainingSec == UNINITIALIZED_SENTINEL ? UNINITIALIZED_TICKS : now + (long)(remainingSec * 1000.0f);
                }
                else if (_actionCooldownsExpiry[actionId] != UNINITIALIZED_TICKS)
                {
                    _actionCooldownsExpiry[actionId] = now;
                }
            }
        }

        public static void Dispose() { }

        public static void ResetForTesting()
        {
            _core = default;
            _gcdRemaining = 0.0f;
            _currentMp = 0;
            _maxMp = 0;
            _lastUpdateTicks = 0;
            _isInitialized = false;
            _playerBuffsExpiry.Clear();
            _targetDebuffsExpiry.Clear();
            _actionCooldownsExpiry.Clear();
        }
    }

    // Intentionally no GameStateData type here; use the production type in projects that reference it.
}
