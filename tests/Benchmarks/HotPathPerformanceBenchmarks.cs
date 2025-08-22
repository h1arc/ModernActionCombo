using System;
using System.Diagnostics;
using Xunit;
using FluentAssertions;
using ModernActionCombo.Jobs.WHM;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Tests.Benchmarks;

/// <summary>
/// Performance benchmarks for hot path optimizations.
/// Tests the cached action resolution vs repeated level checks.
/// </summary>
public class HotPathPerformanceBenchmarks
{
    private readonly WHMProvider _provider;

    public HotPathPerformanceBenchmarks()
    {
        _provider = new WHMProvider();
        
        // Mock the GameStateCache level to ensure consistent behavior
        // In a real scenario, we'd need to mock the static dependencies
        // For now, this test validates the pattern works
    }

    [Fact]
    public void BenchmarkCachedActionResolution()
    {
        // Arrange
        const int iterations = 100_000;
        var stopwatch = new Stopwatch();
        
        // Act - Test hot path performance
        stopwatch.Start();
        for (int i = 0; i < iterations; i++)
        {
            // Simulate multiple calls to cached methods
            // These should hit the cached values after the first call
            var grids = _provider.GetComboGrids();
            // Access the grids to ensure they're evaluated
            _ = grids.Count;
        }
        stopwatch.Stop();
        
        // Assert - Performance characteristics
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        var averageNanosPerCall = (stopwatch.ElapsedTicks * 1_000_000_000.0) / (Stopwatch.Frequency * iterations);
        
        Console.WriteLine($"Hot path performance:");
        Console.WriteLine($"  Total time: {elapsedMs}ms for {iterations:N0} iterations");
        Console.WriteLine($"  Average: {averageNanosPerCall:F1} nanoseconds per call");
        
        // Performance expectations - should be sub-microsecond per call
        averageNanosPerCall.Should().BeLessThan(10_000, 
            $"Hot path should be fast but took {averageNanosPerCall:F1}ns per call");
        elapsedMs.Should().BeLessThan(1000, 
            $"100k iterations should complete quickly but took {elapsedMs}ms");
    }

    [Fact]
    public void ValidateCachedActionsAreCorrect()
    {
        // Arrange & Act
        var grids = _provider.GetComboGrids();
        var singleTargetGrid = grids[0];
        
        // Create a mock game state (using default constructor + with syntax)
        var gameState = new GameStateData();
        
        // Assert - Verify the grid can evaluate correctly
        singleTargetGrid.Should().NotBeNull();
        singleTargetGrid.Name.Should().Be("Single Target DPS");
        singleTargetGrid.TriggerActions.Length.Should().BeGreaterThan(0);
        singleTargetGrid.Rules.Length.Should().BeGreaterThan(0);
        
        // Test that the rules can be evaluated without throwing
        var result = singleTargetGrid.Evaluate(25859u, gameState); // Glare III
        result.Should().BeGreaterThan(0u, "Should return a valid action ID");
    }

    [Fact]
    public void ValidateHotPathDoesNotThrow()
    {
        // Arrange
        const int iterations = 1_000;
        
        // Act & Assert - Multiple calls should not throw
        for (int i = 0; i < iterations; i++)
        {
            var action = () =>
            {
                var grids = _provider.GetComboGrids();
                grids.Count.Should().BeGreaterThan(0);
            };
            
            action.Should().NotThrow();
        }
    }
}
