using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using ModernActionCombo.Core.Data;

namespace Benchmarks.Micro
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Support a quick mode that runs in a few seconds
            bool quick = Array.Exists(args, a => a.Equals("--quick", StringComparison.OrdinalIgnoreCase));
            IConfig config = quick
                ? ManualConfig.CreateEmpty()
                    .AddJob(Job.Dry.WithId("Quick"))
                    .AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default)
                    .AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance)
                : DefaultConfig.Instance;

            BenchmarkRunner.Run(new[] { typeof(CoreGetters), typeof(CoreUpdates), typeof(StatusLookups), typeof(SmartTargetingBenchmarks) }, config);
            return 0;
        }
    }

    [MemoryDiagnoser]
    public class CoreGetters
    {
        public CoreGetters()
        {
            GameStateCache.UpdateCoreState(25, 100, 1234, 5678, inCombat: true, hasTarget: true, inDuty: false, canAct: true, isMoving: false);
            GameStateCache.UpdateScalarState(1.2f, 8000, 10000);
        }

        [Benchmark]
        public uint ReadJob() => GameStateCache.JobId;

        [Benchmark]
        public bool ReadInCombat() => GameStateCache.InCombat;

        [Benchmark]
        public uint ReadTarget() => GameStateCache.TargetId;

    [Benchmark]
    public uint ReadZone() => GameStateCache.ZoneId;
    }

    [MemoryDiagnoser]
    public class CoreUpdates
    {
        private uint _job = 25;
        private uint _tick;

        [Benchmark]
        public void UpdateCore()
        {
            _tick++;
            GameStateCache.UpdateCoreState(_job, 100, _tick, 9, inCombat: (_tick & 1) == 0, hasTarget: true, inDuty: false, canAct: true, isMoving: false, gaugeData1: _tick, gaugeData2: _tick);
        }

        [Benchmark]
        public void UpdateScalar()
        {
            var t = (float)(_tick % 250) / 100f;
            GameStateCache.UpdateScalarState(t, 9000, 10000);
        }
    }

    [MemoryDiagnoser]
    public class StatusLookups
    {
        private readonly uint[] _ids = new uint[] { 1, 2, 3, 4, 5 };

        public StatusLookups()
        {
            // Initialize common tracking once via static ctor
            // Simulate some updates
            var buffs = new System.Collections.Generic.Dictionary<uint, float>();
            foreach (var id in _ids) buffs[id] = 10f;
            GameStateCache.UpdatePlayerBuffs(buffs);

            var debuffs = new System.Collections.Generic.Dictionary<uint, float>();
            foreach (var id in _ids) debuffs[id] = 12f;
            GameStateCache.UpdateTargetDebuffs(debuffs);

            var cds = new System.Collections.Generic.Dictionary<uint, float>();
            foreach (var id in _ids) cds[id] = 5f;
            GameStateCache.UpdateActionCooldowns(cds);
        }

        [Benchmark]
        public bool HasBuff() => GameStateCache.HasPlayerBuff(_ids[0]);

        [Benchmark]
        public float DebuffRemaining() => GameStateCache.GetTargetDebuffTimeRemaining(_ids[1]);

        [Benchmark]
        public bool ActionReady() => GameStateCache.IsActionReady(_ids[2]);
    }
}

// Note: OGCDResolver benchmarks are omitted in the Micro project because it uses stubs without GameStateData.
