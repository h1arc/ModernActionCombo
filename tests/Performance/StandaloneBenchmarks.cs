using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Engines;
using ModernWrathCombo.Core.Data;
using ModernWrathCombo.Jobs.WHM;

namespace ModernWrathCombo.Tests.Performance;

/// <summary>
/// Standalone performance benchmarks that don't depend on Dalamud.
/// These validate our <50ns action resolution target.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class StandaloneBenchmarks
{
    private ActionResolver _resolver = null!;
    private TestActionHandler _testHandler = null!;
    private StatusEffect[] _targetEffects = null!;
    private StatusEffect[] _playerEffects = null!;
    private ActionState[] _actionStates = null!;

    [GlobalSetup]
    public void Setup()
    {
        _resolver = new ActionResolver();
        _testHandler = new TestActionHandler(54321);
        
        // Register test handler
        _resolver.RegisterHandler(12345, _testHandler);
        
        // Set up realistic game state data
        _targetEffects = new[]
        {
            new StatusEffect(1871, 20.0f), // Dia DoT
        };
        
        _playerEffects = new[]
        {
            new StatusEffect(3709, 10.0f), // Sacred Sight
        };
        
        _actionStates = new[]
        {
            new ActionState(136, 60.0f), // PoM on cooldown
        };
    }

    [Benchmark]
    public uint ResolveUnhandledAction_FastPath()
    {
        // Test the ultra-fast path: action with no handler
        return _resolver.Resolve(99999);
    }

    [Benchmark]
    public uint ResolveHandledAction_LegacyInterface()
    {
        // Test legacy interface resolution
        return _resolver.Resolve(12345);
    }

    [Benchmark]
    public uint ResolveHandledAction_WithGameState()
    {
        // Test full game state resolution (our target <50ns)
        var gameState = new GameState(24, 90, true, 1001, 0.0f);
        return _resolver.Resolve(12345, gameState, 
            _targetEffects.AsSpan(), _playerEffects.AsSpan(), _actionStates.AsSpan());
    }

    [Benchmark]
    public uint ResolveHandledAction_WithGameState_ComplexScenario()
    {
        // Test more complex scenario with larger spans
        var gameState = new GameState(24, 90, true, 1001, 0.3f);
        
        var targetEffects = new StatusEffect[]
        {
            new StatusEffect(1871, 3.0f),   // Dia expiring
            new StatusEffect(2222, 15.0f),  // Some other debuff
            new StatusEffect(3333, 30.0f),  // Another debuff
        };
        
        var playerEffects = new StatusEffect[]
        {
            new StatusEffect(3709, 10.0f),  // Sacred Sight
            new StatusEffect(157, 8.0f),    // PoM buff
            new StatusEffect(4444, 20.0f),  // Some other buff
        };
        
        var actionStates = new ActionState[]
        {
            new ActionState(136, 0.0f),     // PoM ready
            new ActionState(5555, 45.0f),   // Some ability on cooldown
            new ActionState(6666, 120.0f),  // Long cooldown ability
        };
        
        return _resolver.Resolve(12345, gameState, 
            targetEffects.AsSpan(), playerEffects.AsSpan(), actionStates.AsSpan());
    }

    [Benchmark]
    public void ResolveBatch_10Actions()
    {
        // Test batch resolution performance
        var gameState = new GameState(24, 90, true, 1001, 0.0f);
        Span<uint> input = stackalloc uint[10];
        Span<uint> output = stackalloc uint[10];
        
        // Mix of handled and unhandled actions
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = (i % 3 == 0) ? 12345u : (uint)(50000 + i);
        }
        
        _resolver.ResolveBatch(input, output, gameState, 
            _targetEffects.AsSpan(), _playerEffects.AsSpan(), _actionStates.AsSpan());
    }

    [Benchmark]
    public void ResolveBatch_100Actions()
    {
        // Test larger batch resolution
        var gameState = new GameState(24, 90, true, 1001, 0.0f);
        Span<uint> input = stackalloc uint[100];
        Span<uint> output = stackalloc uint[100];
        
        // Mix of handled and unhandled actions
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = (i % 5 == 0) ? 12345u : (uint)(50000 + i);
        }
        
        _resolver.ResolveBatch(input, output, gameState, 
            _targetEffects.AsSpan(), _playerEffects.AsSpan(), _actionStates.AsSpan());
    }
}

/// <summary>
/// Quick benchmarks for fast validation (5-10 seconds).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.ColdStart, iterationCount: 3, warmupCount: 1, invocationCount: 100000)]
public class QuickBenchmarks
{
    private ActionResolver _resolver = null!;
    private TestActionHandler _testHandler = null!;
    private StatusEffect[] _targetEffects = null!;
    private StatusEffect[] _playerEffects = null!;
    private ActionState[] _actionStates = null!;

    [GlobalSetup]
    public void Setup()
    {
        _resolver = new ActionResolver();
        _testHandler = new TestActionHandler(54321);
        
        // Register test handler
        _resolver.RegisterHandler(12345, _testHandler);
        
        // Set up realistic game state data
        _targetEffects = new[]
        {
            new StatusEffect(1871, 20.0f), // Dia DoT
        };
        
        _playerEffects = new[]
        {
            new StatusEffect(3709, 10.0f), // Sacred Sight
        };
        
        _actionStates = new[]
        {
            new ActionState(136, 60.0f), // PoM on cooldown
        };
    }

    [Benchmark]
    public uint QuickResolve_FastPath()
    {
        return _resolver.Resolve(99999);
    }

    [Benchmark]
    public uint QuickResolve_WithGameState()
    {
        var gameState = new GameState(24, 90, true, 1001, 0.0f);
        return _resolver.Resolve(12345, gameState, 
            _targetEffects.AsSpan(), _playerEffects.AsSpan(), _actionStates.AsSpan());
    }

    [Benchmark]
    public void QuickBatch_10Actions()
    {
        var gameState = new GameState(24, 90, true, 1001, 0.0f);
        Span<uint> input = stackalloc uint[10];
        Span<uint> output = stackalloc uint[10];
        
        for (int i = 0; i < input.Length; i++)
        {
            input[i] = (i % 3 == 0) ? 12345u : (uint)(50000 + i);
        }
        
        _resolver.ResolveBatch(input, output, gameState, 
            _targetEffects.AsSpan(), _playerEffects.AsSpan(), _actionStates.AsSpan());
    }
}

/// <summary>
/// Direct WHM combo benchmarks without resolver overhead.
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class WHMComboBenchmarks
{
    private WHMBasicCombo _combo = null!;
    private StatusEffect[] _targetEffects = null!;
    private StatusEffect[] _playerEffects = null!;
    private ActionState[] _actionStates = null!;

    [GlobalSetup]
    public void Setup()
    {
        _combo = new WHMBasicCombo();
        
        _targetEffects = new[]
        {
            new StatusEffect(WHMConstants.DiaDebuffId, 20.0f),
        };
        
        _playerEffects = new[]
        {
            new StatusEffect(WHMConstants.SacredSightBuffId, 10.0f),
        };
        
        _actionStates = new[]
        {
            new ActionState(WHMConstants.PresenceOfMind, 60.0f),
        };
    }

    [Benchmark]
    public uint WHMCombo_Glare4_SacredSight()
    {
        // Test the Glare4 path (Sacred Sight active)
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.0f);
        return _combo.Execute(WHMConstants.Glare3, gameState, 
            _targetEffects.AsSpan(), _playerEffects.AsSpan(), _actionStates.AsSpan());
    }

    [Benchmark]
    public uint WHMCombo_DiaRefresh()
    {
        // Test the Dia refresh path (highest priority)
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.0f);
        var targetEffectsExpiring = new[]
        {
            new StatusEffect(WHMConstants.DiaDebuffId, 3.0f), // Expiring soon
        };
        
        return _combo.Execute(WHMConstants.Glare3, gameState, 
            targetEffectsExpiring.AsSpan(), _playerEffects.AsSpan(), _actionStates.AsSpan());
    }

    [Benchmark]
    public uint WHMCombo_PresenceOfMind()
    {
        // Test the PoM weaving path
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.3f); // Can weave
        var actionStatesReady = new[]
        {
            new ActionState(WHMConstants.PresenceOfMind, 0.0f), // PoM ready
        };
        
        return _combo.Execute(WHMConstants.Glare3, gameState, 
            _targetEffects.AsSpan(), _playerEffects.AsSpan(), actionStatesReady.AsSpan());
    }

    [Benchmark]
    public uint WHMCombo_Glare3_Filler()
    {
        // Test the default Glare3 path (lowest priority)
        var gameState = new GameState(WHMConstants.WHMJobId, 90, true, 1001, 0.0f);
        var playerEffectsEmpty = Array.Empty<StatusEffect>(); // No Sacred Sight
        
        return _combo.Execute(WHMConstants.Glare3, gameState, 
            _targetEffects.AsSpan(), playerEffectsEmpty.AsSpan(), _actionStates.AsSpan());
    }
}

/// <summary>
/// Simple test action handler for benchmarking.
/// </summary>
internal class TestActionHandler : IActionHandler
{
    private readonly uint _returnValue;

    public TestActionHandler(uint returnValue)
    {
        _returnValue = returnValue;
    }

    public uint Execute(uint originalActionId) => _returnValue;

    public uint Execute(uint originalActionId, GameStateData gameState, ReadOnlySpan<StatusEffect> targetEffects, 
                      ReadOnlySpan<StatusEffect> playerEffects, ReadOnlySpan<ActionState> actionStates) => _returnValue;
}

/// <summary>
/// Benchmark runner program.
/// </summary>
public class BenchmarkProgram
{
    public static void Main(string[] args)
    {
        Console.WriteLine("🚀 ModernWrathCombo Performance Benchmarks");
        Console.WriteLine("Target: <50ns for action resolution");
        Console.WriteLine();
        
        var mode = args.Length > 0 ? args[0].ToLower() : "all";
        
        switch (mode)
        {
            case "quick":
                Console.WriteLine("Running quick benchmarks (5-10 seconds)...");
                var quickSummary = BenchmarkRunner.Run<QuickBenchmarks>();
                PrintPerformanceScore(quickSummary);
                break;
                
            case "standalone":
            case "standalonebenchmarks":
                Console.WriteLine("Running standalone benchmarks...");
                var standaloneSummary = BenchmarkRunner.Run<StandaloneBenchmarks>();
                PrintPerformanceScore(standaloneSummary);
                break;
                
            case "whm":
            case "whmcombobenchmarks":
                Console.WriteLine("Running WHM combo benchmarks...");
                var whmSummary = BenchmarkRunner.Run<WHMComboBenchmarks>();
                PrintPerformanceScore(whmSummary);
                break;
                
            default:
                Console.WriteLine("Running all benchmarks...");
                var summary1 = BenchmarkRunner.Run<StandaloneBenchmarks>();
                var summary2 = BenchmarkRunner.Run<WHMComboBenchmarks>();
                PrintPerformanceScore(summary1, summary2);
                break;
        }
        
        Console.WriteLine();
        Console.WriteLine("✅ Benchmarks complete!");
    }
    
    private static void PrintPerformanceScore(params BenchmarkDotNet.Reports.Summary[] summaries)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine("🎯 PERFORMANCE SCORE REPORT");
        Console.WriteLine("═══════════════════════════════════════");
        
        var allResults = summaries.SelectMany(s => s.Reports).ToList();
        if (!allResults.Any())
        {
            Console.WriteLine("❌ No benchmark results available");
            return;
        }
        
        // Calculate scores based on our targets
        var scores = new List<(string name, double time, int score)>();
        double totalScore = 0;
        int benchmarkCount = 0;
        
        foreach (var report in allResults)
        {
            var meanNs = report.ResultStatistics?.Mean ?? double.MaxValue;
            var benchmarkName = report.BenchmarkCase.DisplayInfo.Replace("ModernWrathCombo.Tests.Performance.", "");
            
            // Calculate score (0-100) based on performance targets
            int score = CalculatePerformanceScore(benchmarkName, meanNs);
            scores.Add((benchmarkName, meanNs, score));
            totalScore += score;
            benchmarkCount++;
            
            // Color-coded output
            var scoreColor = score >= 90 ? "🟢" : score >= 70 ? "🟡" : "🔴";
            Console.WriteLine($"{scoreColor} {benchmarkName,-40} | {meanNs,8:F2} ns | Score: {score,3}/100");
        }
        
        var overallScore = benchmarkCount > 0 ? (int)(totalScore / benchmarkCount) : 0;
        var overallColor = overallScore >= 90 ? "🟢" : overallScore >= 70 ? "🟡" : "🔴";
        
        Console.WriteLine("═══════════════════════════════════════");
        Console.WriteLine($"{overallColor} OVERALL PERFORMANCE SCORE: {overallScore}/100");
        Console.WriteLine();
        
        // Performance analysis
        if (overallScore >= 95)
            Console.WriteLine("🏆 EXCEPTIONAL! Ultra-high performance exceeding all targets!");
        else if (overallScore >= 85)
            Console.WriteLine("⭐ EXCELLENT! Performance well above target thresholds!");
        else if (overallScore >= 70)
            Console.WriteLine("✅ GOOD! Meeting performance requirements!");
        else if (overallScore >= 50)
            Console.WriteLine("⚠️  ACCEPTABLE! Some optimizations may be needed!");
        else
            Console.WriteLine("❌ NEEDS IMPROVEMENT! Performance below acceptable thresholds!");
            
        Console.WriteLine($"Target: All core operations <50ns | Best case: <5ns");
        Console.WriteLine("═══════════════════════════════════════");
    }
    
    private static int CalculatePerformanceScore(string benchmarkName, double meanNs)
    {
        // Ultra-performance scoring system (0-100)
        // Based on FFXIV action resolution requirements
        
        if (benchmarkName.Contains("FastPath") || benchmarkName.Contains("QuickResolve_FastPath"))
        {
            // Fast path should be sub-nanosecond to few nanoseconds
            if (meanNs <= 1.0) return 100;      // Perfect
            if (meanNs <= 2.0) return 95;       // Excellent
            if (meanNs <= 5.0) return 90;       // Very good
            if (meanNs <= 10.0) return 80;      // Good
            if (meanNs <= 20.0) return 70;      // Acceptable
            if (meanNs <= 50.0) return 60;      // Meeting target
            return Math.Max(0, 50 - (int)((meanNs - 50) / 10)); // Below target
        }
        
        if (benchmarkName.Contains("WithGameState") || benchmarkName.Contains("QuickResolve_WithGameState"))
        {
            // Core target: <50ns for full game state resolution
            if (meanNs <= 5.0) return 100;      // Exceptional
            if (meanNs <= 10.0) return 95;      // Excellent  
            if (meanNs <= 20.0) return 90;      // Very good
            if (meanNs <= 35.0) return 85;      // Good
            if (meanNs <= 50.0) return 80;      // Meeting target
            if (meanNs <= 75.0) return 70;      // Close to target
            if (meanNs <= 100.0) return 60;     // Acceptable
            return Math.Max(0, 50 - (int)((meanNs - 100) / 20)); // Below acceptable
        }
        
        if (benchmarkName.Contains("ComplexScenario"))
        {
            // Complex scenarios can be a bit slower
            if (meanNs <= 10.0) return 100;     // Perfect
            if (meanNs <= 20.0) return 95;      // Excellent
            if (meanNs <= 40.0) return 90;      // Very good
            if (meanNs <= 60.0) return 80;      // Good
            if (meanNs <= 100.0) return 70;     // Acceptable
            return Math.Max(0, 60 - (int)((meanNs - 100) / 50)); // Below acceptable
        }
        
        if (benchmarkName.Contains("Batch"))
        {
            // Batch operations - measure per-action efficiency
            var actionsCount = benchmarkName.Contains("100") ? 100 : 10;
            var perActionNs = meanNs / actionsCount;
            
            if (perActionNs <= 2.0) return 100;     // Perfect batch efficiency
            if (perActionNs <= 5.0) return 95;      // Excellent
            if (perActionNs <= 10.0) return 90;     // Very good
            if (perActionNs <= 20.0) return 85;     // Good
            if (perActionNs <= 50.0) return 80;     // Meeting target
            return Math.Max(0, 70 - (int)((perActionNs - 50) / 10)); // Below target
        }
        
        if (benchmarkName.Contains("WHMCombo"))
        {
            // WHM combo performance - should be very fast
            if (meanNs <= 2.0) return 100;      // Perfect
            if (meanNs <= 5.0) return 95;       // Excellent
            if (meanNs <= 10.0) return 90;      // Very good
            if (meanNs <= 25.0) return 85;      // Good
            if (meanNs <= 50.0) return 80;      // Acceptable
            return Math.Max(0, 70 - (int)((meanNs - 50) / 20)); // Below acceptable
        }
        
        // Default scoring for any other benchmarks
        if (meanNs <= 50.0) return 80;          // Meeting general target
        return Math.Max(0, 70 - (int)((meanNs - 50) / 25)); // Below target
    }
}
