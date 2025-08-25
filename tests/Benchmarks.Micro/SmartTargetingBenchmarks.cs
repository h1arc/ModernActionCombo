using System;
using BenchmarkDotNet.Attributes;
using ModernActionCombo.Core.Data;

namespace Benchmarks.Micro
{
    [MemoryDiagnoser]
    public class SmartTargetingBenchmarks
    {
        [Params(4, 8, 16, 24, 48, 96)]
        public int PartySize;
    // SIMD path removed in production; keep benchmarks scalar-only to mirror runtime

        private uint[] _ids = Array.Empty<uint>();
        private float[] _hp = Array.Empty<float>();
        private uint[] _flags = Array.Empty<uint>();

        [GlobalSetup]
        public void Setup()
        {
            // Force scalar path in stub to match production implementation
            SmartTargetingCache.DisableSimd = true;
            _ids = new uint[PartySize];
            _hp = new float[PartySize];
            _flags = new uint[PartySize];

            var rand = new Random(42);
            int selfIndex = 0;
            for (int i = 0; i < PartySize; i++)
            {
                _ids[i] = (uint)(1000 + i);
                _hp[i] = (float)rand.NextDouble();
                _flags[i] = SmartTargetingCache.ValidTarget | SmartTargetingCache.ValidAbilityTarget | SmartTargetingCache.AllyFlag;
            }
            // Mark index 0 as self
            _flags[selfIndex] |= SmartTargetingCache.SelfFlag;

            // Seed cache
            SmartTargetingCache.UpdatePartyData(_ids, _hp, _flags, (byte)Math.Min(PartySize, SmartTargetingCache.MaxPartySize));
        }

        [Benchmark]
        public uint GetLowestHpTarget() => SmartTargetingCache.GetLowestHpTarget();

        [Benchmark]
        public uint GetSmartTarget() => SmartTargetingCache.GetSmartTarget(0.95f);

        [Benchmark]
        public float GetMemberHpById() => SmartTargetingCache.GetMemberHpPercent(_ids[PartySize/2]);
    }
}
