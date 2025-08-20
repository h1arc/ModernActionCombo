#!/bin/bash

# Minimal DoT Lockout Test
# Tests the basic lockout logic without full framework dependencies

echo "🚀 Minimal DoT Lockout Test"
echo "==========================="
echo ""

echo "Testing ultra-minimal DoT decision lockout logic..."
echo "This simulates button mashing scenarios with 2-second lockout."
echo ""

# Create a minimal C# test file
cat > /tmp/minimal_test.cs << 'EOF'
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Time-scaled DoT lockout simulation.
/// Simulates realistic rotation timing at accelerated speeds.
/// </summary>
public class MinimalDoTLockoutTest
{
    // Time scaling factor (how much to speed up the simulation)
    private static readonly int TIME_SCALE = 100; // 100x faster
    
    // Real-world timing (in milliseconds)
    private static readonly int REAL_GLARE_GCD = 2500;           // 2.5s
    private static readonly int REAL_DIA_CAST_TIME = 2000;       // 2.0s (cast + animation)
    private static readonly int REAL_DOT_LOCKOUT = 2000;        // 2.0s
    private static readonly int REAL_DOT_DURATION = 30000;      // 30s
    
    // Scaled timing (for fast simulation)
    private static readonly int SCALED_GLARE_GCD = REAL_GLARE_GCD / TIME_SCALE;        // 25ms
    private static readonly int SCALED_DIA_CAST_TIME = REAL_DIA_CAST_TIME / TIME_SCALE; // 20ms  
    private static readonly int SCALED_DOT_LOCKOUT = REAL_DOT_LOCKOUT / TIME_SCALE;    // 20ms
    private static readonly int SCALED_DOT_DURATION = REAL_DOT_DURATION / TIME_SCALE;  // 300ms
    
    // Simulation state
    private static DateTime _lastDoTDecision = DateTime.MinValue;
    private static TimeSpan _dotDecisionLockout = TimeSpan.FromMilliseconds(SCALED_DOT_LOCKOUT);
    private static DateTime _simulationStart;
    private static float _dotAppliedAt = -1000f; // When DoT was last applied (simulation seconds)
    
    public static void Main()
    {
        Console.WriteLine("🚀 Time-Scaled DoT Lockout Simulation");
        Console.WriteLine("=====================================");
        Console.WriteLine($"Time scale: {TIME_SCALE}x faster");
        Console.WriteLine($"Real timings → Scaled timings:");
        Console.WriteLine($"  • Glare GCD: {REAL_GLARE_GCD}ms → {SCALED_GLARE_GCD}ms");
        Console.WriteLine($"  • Dia cast: {REAL_DIA_CAST_TIME}ms → {SCALED_DIA_CAST_TIME}ms");  
        Console.WriteLine($"  • DoT lockout: {REAL_DOT_LOCKOUT}ms → {SCALED_DOT_LOCKOUT}ms");
        Console.WriteLine($"  • DoT duration: {REAL_DOT_DURATION}ms → {SCALED_DOT_DURATION}ms");
        Console.WriteLine();
        
        // Run time-scaled simulations
        RunTimeScaledSimulation("Realistic Rotation (GCD-paced)", 100, SCALED_GLARE_GCD);
        RunTimeScaledSimulation("Button Mashing (50ms)", 200, 5); // 50ms → 0.5ms scaled
        RunTimeScaledSimulation("Heavy Mashing (10ms)", 500, 1);  // 10ms → 0.1ms scaled  
        RunTimeScaledSimulation("Extreme Mashing (1ms)", 1000, 0); // No delay
        
        Console.WriteLine();
        Console.WriteLine("🎯 Time-Scaled Simulation Summary:");
        Console.WriteLine("==================================");
        Console.WriteLine("These simulations maintain proper timing relationships");
        Console.WriteLine("while running at accelerated speed for fast validation.");
        Console.WriteLine();
        Console.WriteLine("Expected behavior:");
        Console.WriteLine("• Realistic rotation: ~3-4 DoTs per 30s cycle");
        Console.WriteLine("• Button mashing: DoT lockout prevents double casting");
        Console.WriteLine("• DoT refreshes only when duration < 5s equivalent");
    }
    
    private static void RunTimeScaledSimulation(string testName, int actionCount, int actionDelayMs)
    {
        Console.WriteLine($"{testName} ({actionCount} actions, {actionDelayMs}ms intervals):");
        
        // Reset simulation state
        _lastDoTDecision = DateTime.MinValue;
        _simulationStart = DateTime.UtcNow;
        _dotAppliedAt = -1000f; // Start with expired DoT
        
        var results = new List<ActionResult>();
        
        for (int i = 0; i < actionCount; i++)
        {
            var actionTime = DateTime.UtcNow;
            var simulationSeconds = GetSimulationSeconds(actionTime);
            
            // Update DoT status based on time
            UpdateDoTStatus(simulationSeconds);
            
            bool shouldCastDoT = ShouldApplyDoT();
            uint actionUsed = shouldCastDoT ? 16532u : 25859u; // Dia : Glare3
            
            results.Add(new ActionResult
            {
                ActionNumber = i + 1,
                Timestamp = actionTime,
                SimulationSeconds = simulationSeconds,
                ActionId = actionUsed,
                WasDoT = shouldCastDoT,
                DoTTimeRemaining = GetDoTTimeRemaining(simulationSeconds)
            });
            
            // If DoT was cast, record when it was applied
            if (shouldCastDoT)
            {
                _dotAppliedAt = simulationSeconds;
            }
            
            // Add scaled delay for next action
            if (actionDelayMs > 0 && i < actionCount - 1)
            {
                Thread.Sleep(actionDelayMs);
            }
        }
        
        AnalyzeResults(results, testName);
        Console.WriteLine();
    }
    
    private static float GetSimulationSeconds(DateTime actionTime)
    {
        var elapsed = (actionTime - _simulationStart).TotalMilliseconds;
        return (float)(elapsed * TIME_SCALE / 1000.0); // Convert to simulation seconds
    }
    
    private static void UpdateDoTStatus(float simulationSeconds)
    {
        // DoT expires after REAL_DOT_DURATION simulation seconds
        var dotDurationSeconds = REAL_DOT_DURATION / 1000f;
        // Current implementation doesn't need explicit update
    }
    
    private static float GetDoTTimeRemaining(float simulationSeconds)
    {
        if (_dotAppliedAt < 0) return 0f; // Never applied
        
        var dotDurationSeconds = REAL_DOT_DURATION / 1000f; // 30s in simulation time
        var elapsedSinceApplication = simulationSeconds - _dotAppliedAt;
        return Math.Max(0f, dotDurationSeconds - elapsedSinceApplication);
    }
    
    private static bool ShouldApplyDoT()
    {
        // Ultra-minimal lockout check (scaled timing)
        var timeSinceLastDecision = DateTime.UtcNow - _lastDoTDecision;
        if (timeSinceLastDecision < _dotDecisionLockout)
        {
            return false; // Locked out
        }
        
        // Check if DoT needs refresh (when < 5s remaining in simulation time)
        var simulationSeconds = GetSimulationSeconds(DateTime.UtcNow);
        var timeRemaining = GetDoTTimeRemaining(simulationSeconds);
        var shouldApply = timeRemaining <= 5.0f; // 5s threshold
        
        if (shouldApply)
        {
            // Record decision time
            _lastDoTDecision = DateTime.UtcNow;
        }
        
        return shouldApply;
    }
    
    private static void AnalyzeResults(List<ActionResult> results, string testName)
    {
        var totalActions = results.Count;
        var dotCasts = results.Count(r => r.WasDoT);
        var glareCasts = results.Count(r => !r.WasDoT);
        
        if (results.Count == 0) return;
        
        var totalSimTime = results.Last().SimulationSeconds - results.First().SimulationSeconds;
        var realTimeMs = (results.Last().Timestamp - results.First().Timestamp).TotalMilliseconds;
        var actionsPerSecond = totalActions / (realTimeMs / 1000.0);
        
        Console.WriteLine($"  📊 Results:");
        Console.WriteLine($"     • Total actions: {totalActions}");
        Console.WriteLine($"     • DoT casts: {dotCasts}");  
        Console.WriteLine($"     • Glare casts: {glareCasts}");
        Console.WriteLine($"     • Simulation time: {totalSimTime:F1}s (scaled)");
        Console.WriteLine($"     • Real time: {realTimeMs:F0}ms");
        Console.WriteLine($"     • Actions/sec: {actionsPerSecond:F1}");
        
        // Expected DoTs in a realistic scenario
        var expectedDoTsIn30s = Math.Max(1, (int)(totalSimTime / 25.0)); // DoT every ~25s in good rotation
        var dotEfficiency = dotCasts <= expectedDoTsIn30s * 2 ? "✅ Good" : "❌ Too many";
        Console.WriteLine($"     • DoT efficiency: {dotEfficiency}");
        
        // Check DoT intervals
        var dotActions = results.Where(r => r.WasDoT).ToList();
        if (dotActions.Count > 1)
        {
            var intervals = new List<float>();
            for (int i = 1; i < dotActions.Count; i++)
            {
                var interval = dotActions[i].SimulationSeconds - dotActions[i-1].SimulationSeconds;
                intervals.Add(interval);
            }
            
            var avgInterval = intervals.Average();
            var minInterval = intervals.Min();
            
            Console.WriteLine($"     • DoT intervals: avg {avgInterval:F1}s, min {minInterval:F1}s (simulation time)");
            
            // In real time, minimum should be >= 2s
            if (minInterval < 1.9f)
            {
                Console.WriteLine($"     ❌ Lockout failure: DoT interval {minInterval:F1}s < 2.0s");
            }
            else
            {
                Console.WriteLine($"     ✅ Lockout working: No DoTs closer than 2s");
            }
        }
        
        // Show sample of DoT timing
        if (dotActions.Count > 0)
        {
            Console.WriteLine($"     • Sample DoT timings (simulation seconds):");
            foreach (var dot in dotActions.Take(5))
            {
                Console.WriteLine($"       Action #{dot.ActionNumber}: {dot.SimulationSeconds:F1}s (DoT remaining: {dot.DoTTimeRemaining:F1}s)");
            }
            if (dotActions.Count > 5)
            {
                Console.WriteLine($"       ... and {dotActions.Count - 5} more");
            }
        }
    }
    
    private struct ActionResult
    {
        public int ActionNumber;
        public DateTime Timestamp;
        public float SimulationSeconds;
        public uint ActionId;
        public bool WasDoT;
        public float DoTTimeRemaining;
    }
}
EOF

# Compile and run the test
echo "Compiling minimal test..."
if csc /tmp/minimal_test.cs -out:/tmp/minimal_test.exe 2>/dev/null; then
    echo "✓ Compilation successful"
    echo ""
    mono /tmp/minimal_test.exe
elif dotnet --version > /dev/null 2>&1; then
    echo "Using dotnet instead of mono..."
    echo ""
    cd /tmp
    dotnet new console -n MinimalTest -f net9.0 > /dev/null 2>&1
    cp minimal_test.cs MinimalTest/Program.cs
    cd MinimalTest
    dotnet run
else
    echo "❌ No C# compiler found"
    exit 1
fi

# Cleanup
rm -f /tmp/minimal_test.cs /tmp/minimal_test.exe
rm -rf /tmp/MinimalTest
