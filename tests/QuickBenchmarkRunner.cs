using System;
using ModernWrathCombo.Tests.Performance;

namespace ModernWrathCombo.Tests;

/// <summary>
/// Simple benchmark runner for testing button mashing scenarios.
/// Provides quick validation without full BenchmarkDotNet overhead.
/// </summary>
public class QuickBenchmarkRunner
{
    public static void Main(string[] args)
    {
        Console.WriteLine("🚀 ModernWrathCombo Quick Button Mashing Test");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        var benchmark = new ButtonMashingBenchmarks();
        
        try
        {
            benchmark.Setup();
            Console.WriteLine("✓ Setup completed");
            Console.WriteLine();

            Console.WriteLine("Running quick validation tests...");
            Console.WriteLine();

            // Run a subset of tests for quick validation
            Console.WriteLine("1. Testing Normal Button Mashing (100ms intervals):");
            benchmark.ButtonMashing_Normal_100ms();
            Console.WriteLine();

            Console.WriteLine("2. Testing Heavy Button Mashing (50ms intervals):");
            benchmark.ButtonMashing_Heavy_50ms();
            Console.WriteLine();

            Console.WriteLine("3. Testing Extreme Button Mashing (10ms intervals):");
            benchmark.ButtonMashing_Extreme_10ms();
            Console.WriteLine();

            Console.WriteLine("4. Testing Lockout Expiry Behavior:");
            benchmark.LockoutExpiry_Test();
            Console.WriteLine();

            Console.WriteLine("✓ Quick benchmark completed!");
            Console.WriteLine();
            Console.WriteLine("📊 Analysis:");
            Console.WriteLine("• Check console output for DoT cast counts");
            Console.WriteLine("• Ideal result: 1 DoT cast per test (no double casting)");
            Console.WriteLine("• If seeing multiple DoT casts, increase lockout window");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Benchmark failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
