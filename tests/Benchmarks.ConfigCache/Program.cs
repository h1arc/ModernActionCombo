using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using System.Diagnostics;
using ModernActionCombo.Core.Data;

namespace Benchmarks.ConfigCache
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            // Quick mode runs benchmarks in a few seconds
            bool quick = Array.Exists(args, a => a.Equals("--quick", StringComparison.OrdinalIgnoreCase));
            IConfig config = quick ? new MacFriendlyQuickConfig() : new MacFriendlyConfig();
            BenchmarkRunner.Run(new[] { typeof(ConfigCacheBench) }, config);
            return 0;
        }
    }

    // Custom config to avoid high-priority warnings on macOS and keep runs fast
    public sealed class MacFriendlyConfig : ManualConfig
    {
        public MacFriendlyConfig()
        {
            AddJob(Job.ShortRun.WithId("ShortRun"));
            AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default);
            AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance);
        }
    }

    public sealed class MacFriendlyQuickConfig : ManualConfig
    {
        public MacFriendlyQuickConfig()
        {
            AddJob(Job.Dry.WithId("Quick"));
            AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default);
            AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance);
        }
    }

    [MemoryDiagnoser]
    public class ConfigCacheBench
    {
        private ConfigAwareActionCache _cache = new();
        private uint[] _keys = Array.Empty<uint>();
        private uint[] _values = Array.Empty<uint>();

        [GlobalSetup]
        public void Setup()
        {
            _cache = new ConfigAwareActionCache();
            _keys = new uint[256];
            _values = new uint[256];
            var rnd = new Random(42);
            for (int i = 0; i < _keys.Length; i++)
            {
                _keys[i] = (uint)rnd.Next(1, 20000);
                _values[i] = (uint)rnd.Next(1, 20000);
            }
            // Seed some entries
            for (int i = 0; i < 128; i++)
            {
                _cache.Cache(_keys[i], _values[i]);
            }
        }

        [Benchmark]
        public void Cache_MissAndInsert()
        {
            uint k = _keys[200];
            uint v = _values[200];
            _cache.Cache(k, v);
        }

        [Benchmark]
        public uint TryGet_Hit()
        {
            _cache.TryGetCached(_keys[10], out var v);
            return v;
        }

        [Benchmark]
        public bool TryGet_Miss()
        {
            return _cache.TryGetCached(0xFFFFFFF0u, out _);
        }

        [Benchmark]
        public void Overwrite_Existing()
        {
            _cache.Cache(_keys[10], _values[150]);
        }

        [Benchmark]
        public void Invalidate_VersionBump()
        {
            ConfigAwareActionCache.InvalidateAll();
        }

        [Benchmark]
        public void Clear_Instance()
        {
            _cache.Clear();
        }
    }
}
