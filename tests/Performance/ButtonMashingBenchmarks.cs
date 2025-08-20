using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using ModernWrathCombo.Core.Data;
using ModernWrathCombo.Jobs.WHM;
using System;
using System.Threading;

namespace ModernWrathCombo.Tests.Performance;

/// <summary>
/// Benchmarks for button mashing scenarios to test DoT decision lockout performance.
/// Tests various mashing frequencies from impossible (1ms) to relaxed (2.5s GCD).
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
public class ButtonMashingBenchmarks
{
    private WHMCombo _combo = null!;
    private GameStateData _gameState;
    private Random _random = new();

    [GlobalSetup]
    public void Setup()
    {
        // Initialize systems
        GameStateCache.Initialize();
        Logger.Initialize(new TestPluginLog());
        
        // Create combo instance
        _combo = new WHMCombo();
        
        // Create test game state (WHM level 100, in combat, with target)
        _gameState = new GameStateData(
            jobId: 24,      // WHM
            level: 100,     // Max level
            inCombat: true,
            currentTarget: 12345, // Mock target
            globalCooldownRemaining: 0f
        );
        
        // Set up target with expiring DoT (triggers DoT decision logic)
        GameStateCache.SetTargetDebuffTimeRemaining(143, 3.0f); // Dia debuff, 3s remaining
    }

    /// <summary>
    /// Impossible button mashing: 1ms intervals (1000 presses/second).
    /// This tests the absolute worst-case scenario.
    /// </summary>
    [Benchmark]
    public void ButtonMashing_Impossible_1ms()
    {
        var results = new uint[1000];
        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < 1000; i++)
        {
            results[i] = _combo.Invoke(25859, _gameState); // Glare3
            // Simulate 1ms delay
            Thread.SpinWait(1000); // ~1ms on modern CPUs
        }
        
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var doTCasts = CountDoTCasts(results);
        
        // Log results for analysis
        Console.WriteLine($"Impossible (1ms): {doTCasts} DoT casts in {elapsedMs:F2}ms");
    }

    /// <summary>
    /// Extreme button mashing: 10ms intervals (100 presses/second).
    /// This tests cheating/macro scenarios.
    /// </summary>
    [Benchmark]
    public void ButtonMashing_Extreme_10ms()
    {
        var results = new uint[100];
        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < 100; i++)
        {
            results[i] = _combo.Invoke(25859, _gameState);
            Thread.Sleep(10); // 10ms delay
        }
        
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var doTCasts = CountDoTCasts(results);
        
        Console.WriteLine($"Extreme (10ms): {doTCasts} DoT casts in {elapsedMs:F2}ms");
    }

    /// <summary>
    /// Heavy button mashing: 50ms intervals (20 presses/second).
    /// This tests very aggressive but humanly possible mashing.
    /// </summary>
    [Benchmark]
    public void ButtonMashing_Heavy_50ms()
    {
        var results = new uint[40];
        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < 40; i++)
        {
            results[i] = _combo.Invoke(25859, _gameState);
            Thread.Sleep(50); // 50ms delay
        }
        
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var doTCasts = CountDoTCasts(results);
        
        Console.WriteLine($"Heavy (50ms): {doTCasts} DoT casts in {elapsedMs:F2}ms");
    }

    /// <summary>
    /// Normal button mashing: 100ms intervals (10 presses/second).
    /// This tests typical aggressive player behavior.
    /// </summary>
    [Benchmark]
    public void ButtonMashing_Normal_100ms()
    {
        var results = new uint[20];
        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < 20; i++)
        {
            results[i] = _combo.Invoke(25859, _gameState);
            Thread.Sleep(100); // 100ms delay
        }
        
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var doTCasts = CountDoTCasts(results);
        
        Console.WriteLine($"Normal (100ms): {doTCasts} DoT casts in {elapsedMs:F2}ms");
    }

    /// <summary>
    /// Relaxed button mashing: 250ms intervals (4 presses/second).
    /// This tests moderate player behavior.
    /// </summary>
    [Benchmark]
    public void ButtonMashing_Relaxed_250ms()
    {
        var results = new uint[8];
        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < 8; i++)
        {
            results[i] = _combo.Invoke(25859, _gameState);
            Thread.Sleep(250); // 250ms delay
        }
        
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var doTCasts = CountDoTCasts(results);
        
        Console.WriteLine($"Relaxed (250ms): {doTCasts} DoT casts in {elapsedMs:F2}ms");
    }

    /// <summary>
    /// GCD-aligned pressing: 2.5s intervals (normal GCD timing).
    /// This tests ideal player behavior with proper GCD timing.
    /// </summary>
    [Benchmark]
    public void ButtonMashing_GCD_2500ms()
    {
        var results = new uint[2]; // Only 2 presses in 5s window
        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < 2; i++)
        {
            results[i] = _combo.Invoke(25859, _gameState);
            if (i < 1) Thread.Sleep(2500); // 2.5s GCD delay
        }
        
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var doTCasts = CountDoTCasts(results);
        
        Console.WriteLine($"GCD-aligned (2.5s): {doTCasts} DoT casts in {elapsedMs:F2}ms");
    }

    /// <summary>
    /// Random intervals: simulates realistic player behavior with varying timing.
    /// </summary>
    [Benchmark]
    public void ButtonMashing_Random_Realistic()
    {
        var results = new uint[30];
        var startTime = DateTime.UtcNow;
        
        for (int i = 0; i < 30; i++)
        {
            results[i] = _combo.Invoke(25859, _gameState);
            
            // Random delay between 50ms and 500ms (realistic human variation)
            var delay = _random.Next(50, 500);
            Thread.Sleep(delay);
        }
        
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var doTCasts = CountDoTCasts(results);
        
        Console.WriteLine($"Random Realistic: {doTCasts} DoT casts in {elapsedMs:F2}ms");
    }

    /// <summary>
    /// Tests lockout expiry and recovery behavior.
    /// </summary>
    [Benchmark]
    public void LockoutExpiry_Test()
    {
        var results = new uint[4];
        var startTime = DateTime.UtcNow;
        
        // First call should trigger DoT
        results[0] = _combo.Invoke(25859, _gameState);
        
        // Second call 1s later should be locked out
        Thread.Sleep(1000);
        results[1] = _combo.Invoke(25859, _gameState);
        
        // Third call 2.5s later should be unlocked (total 3.5s)
        Thread.Sleep(1500);
        results[2] = _combo.Invoke(25859, _gameState);
        
        // Fourth call immediately should be locked again
        results[3] = _combo.Invoke(25859, _gameState);
        
        var elapsedMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
        var doTCasts = CountDoTCasts(results);
        
        Console.WriteLine($"Lockout Expiry: {doTCasts} DoT casts in {elapsedMs:F2}ms");
    }

    private static int CountDoTCasts(uint[] results)
    {
        int count = 0;
        foreach (var result in results)
        {
            if (result == 16532) count++; // Dia action ID
        }
        return count;
    }
}

/// <summary>
/// Test plugin log implementation for benchmarks.
/// </summary>
public class TestPluginLog : Dalamud.Plugin.Services.IPluginLog
{
    public void Verbose(string messageTemplate, params object[] values) { }
    public void Verbose(Exception? exception, string messageTemplate, params object[] values) { }
    public void Debug(string messageTemplate, params object[] values) { }
    public void Debug(Exception? exception, string messageTemplate, params object[] values) { }
    public void Information(string messageTemplate, params object[] values) { }
    public void Information(Exception? exception, string messageTemplate, params object[] values) { }
    public void Warning(string messageTemplate, params object[] values) { }
    public void Warning(Exception? exception, string messageTemplate, params object[] values) { }
    public void Error(string messageTemplate, params object[] values) { }
    public void Error(Exception? exception, string messageTemplate, params object[] values) { }
    public void Fatal(string messageTemplate, params object[] values) { }
    public void Fatal(Exception? exception, string messageTemplate, params object[] values) { }
}
