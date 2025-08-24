using System;
using System.Diagnostics;
using ModernActionCombo.Tests.Benchmarks;

namespace ModernActionCombo.Tests;

/// <summary>
/// Simple benchmark runner to compare party targeting approaches.
/// </summary>
public class PartyTargetingComparison
{
    public static void RunComparison()
    {
        Console.WriteLine("=== Party Targeting Performance Comparison ===\n");
        
        var benchmark = new PartyTargetingBenchmarks();
        benchmark.Setup();
        
        const int iterations = 1_000_000;
        
        // Warm up
        Console.WriteLine("Warming up...");
        for (int i = 0; i < 10_000; i++)
        {
            benchmark.SimpleCache_GetBestTarget();
            benchmark.HotCache_GetBestTarget();
            benchmark.HotPaths_GetBestTarget();
        }
        
        // Test 1: GetBestTarget performance
        Console.WriteLine($"\n1. GetBestTarget Performance ({iterations:N0} iterations):");
        
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            benchmark.SimpleCache_GetBestTarget();
        }
        sw.Stop();
        var simpleCacheTime = sw.Elapsed;
        Console.WriteLine($"   Simple Cache: {simpleCacheTime.TotalMilliseconds:F2}ms ({(simpleCacheTime.TotalNanoseconds / iterations):F1}ns per call)");
        
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            benchmark.HotCache_GetBestTarget();
        }
        sw.Stop();
        var hotCacheTime = sw.Elapsed;
        Console.WriteLine($"   Hot Cache:    {hotCacheTime.TotalMilliseconds:F2}ms ({(hotCacheTime.TotalNanoseconds / iterations):F1}ns per call)");
        
        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            benchmark.HotPaths_GetBestTarget();
        }
        sw.Stop();
        var hotPathsTime = sw.Elapsed;
        Console.WriteLine($"   Hot Paths:    {hotPathsTime.TotalMilliseconds:F2}ms ({(hotPathsTime.TotalNanoseconds / iterations):F1}ns per call)");
        
        // Test 2: Sort operation performance
        Console.WriteLine($"\n2. Sort Operation Performance (100,000 iterations):");
        const int sortIterations = 100_000;
        
        sw.Restart();
        for (int i = 0; i < sortIterations; i++)
        {
            benchmark.SimpleCache_SortOperation();
        }
        sw.Stop();
        var simpleSortTime = sw.Elapsed;
        Console.WriteLine($"   Simple Cache: {simpleSortTime.TotalMilliseconds:F2}ms ({(simpleSortTime.TotalNanoseconds / sortIterations):F1}ns per call)");
        
        sw.Restart();
        for (int i = 0; i < sortIterations; i++)
        {
            benchmark.HotCache_SortOperation();
        }
        sw.Stop();
        var hotSortTime = sw.Elapsed;
        Console.WriteLine($"   Hot Cache:    {hotSortTime.TotalMilliseconds:F2}ms ({(hotSortTime.TotalNanoseconds / sortIterations):F1}ns per call)");
        
        sw.Restart();
        for (int i = 0; i < sortIterations; i++)
        {
            benchmark.HotPaths_SortOperation();
        }
        sw.Stop();
        var hotPathsSortTime = sw.Elapsed;
        Console.WriteLine($"   Hot Paths:    {hotPathsSortTime.TotalMilliseconds:F2}ms ({(hotPathsSortTime.TotalNanoseconds / sortIterations):F1}ns per call)");
        
        // Test 3: Memory access patterns
        Console.WriteLine($"\n3. Memory Access Patterns (1,000,000 accesses):");
        
        sw.Restart();
        benchmark.SimpleCache_MemoryAccess();
        sw.Stop();
        var simpleMemoryTime = sw.Elapsed;
        Console.WriteLine($"   Simple Cache: {simpleMemoryTime.TotalMilliseconds:F2}ms");
        
        sw.Restart();
        benchmark.HotPaths_MemoryAccess();
        sw.Stop();
        var hotPathsMemoryTime = sw.Elapsed;
        Console.WriteLine($"   Hot Paths:    {hotPathsMemoryTime.TotalMilliseconds:F2}ms");
        
        // Test 4: Emergency targeting
        Console.WriteLine($"\n4. Emergency Targeting (500,000 iterations):");
        const int emergencyIterations = 500_000;
        
        sw.Restart();
        for (int i = 0; i < emergencyIterations; i++)
        {
            benchmark.SimpleCache_EmergencyTarget();
        }
        sw.Stop();
        var simpleEmergencyTime = sw.Elapsed;
        Console.WriteLine($"   Simple Cache: {simpleEmergencyTime.TotalMilliseconds:F2}ms ({(simpleEmergencyTime.TotalNanoseconds / emergencyIterations):F1}ns per call)");
        
        sw.Restart();
        for (int i = 0; i < emergencyIterations; i++)
        {
            benchmark.HotPaths_EmergencyTarget();
        }
        sw.Stop();
        var hotPathsEmergencyTime = sw.Elapsed;
        Console.WriteLine($"   Hot Paths:    {hotPathsEmergencyTime.TotalMilliseconds:F2}ms ({(hotPathsEmergencyTime.TotalNanoseconds / emergencyIterations):F1}ns per call)");
        
        // Summary
        Console.WriteLine("\n=== SUMMARY ===");
        Console.WriteLine("\nSimple Cache Advantages:");
        Console.WriteLine("- Simpler code structure (easier to maintain)");
        Console.WriteLine("- Compact memory layout (better cache utilization)");
        Console.WriteLine("- SIMD-friendly data organization");
        Console.WriteLine($"- GetBestTarget: {(simpleCacheTime.TotalNanoseconds / iterations):F1}ns per call");
        
        Console.WriteLine("\nHot Cache Advantages:");
        Console.WriteLine("- More sophisticated sorting with multiple criteria");
        Console.WriteLine("- Better for complex priority systems");
        Console.WriteLine($"- GetBestTarget: {(hotCacheTime.TotalNanoseconds / iterations):F1}ns per call");
        
        Console.WriteLine("\nHot Paths Advantages:");
        Console.WriteLine("- Cache-line optimized (64-byte aligned)");
        Console.WriteLine("- No false sharing between members");
        Console.WriteLine("- Predictable memory access patterns");
        Console.WriteLine($"- GetBestTarget: {(hotPathsTime.TotalNanoseconds / iterations):F1}ns per call");
        
        var winner = simpleCacheTime < hotCacheTime && simpleCacheTime < hotPathsTime ? "Simple Cache" :
                    hotCacheTime < hotPathsTime ? "Hot Cache" : "Hot Paths";
        
        Console.WriteLine($"\n🏆 Overall Winner: {winner}");
        
        benchmark.Cleanup();
    }
}
