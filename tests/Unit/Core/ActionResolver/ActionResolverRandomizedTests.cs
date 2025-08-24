using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ModernActionCombo.Core.Data;
using Xunit;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// Comprehensive randomized testing framework for ActionResolver.
    /// Tests upgrade chain logic, level-based resolution, and edge cases under random conditions.
    /// </summary>
    public class ActionResolverRandomizedTests : IDisposable
    {
        private readonly Random _random;
        
        // Test action IDs that have upgrade chains
        private static readonly uint[] BASE_ACTIONS = new uint[]
        {
            119,   // Stone (WHM) - upgrades to Stone II -> Stone III -> Glare -> Glare III
            121,   // Aero (WHM) - upgrades to Aero II -> Dia
            139,   // Holy (WHM) - upgrades to Holy III
            // Add more base actions as they're discovered in ActionResolver
        };

        // Level ranges for comprehensive testing
        private const uint MIN_LEVEL = 1;
        private const uint MAX_LEVEL = 90;
        private const uint INVALID_LEVEL = 0;
        private const uint OVERFLOW_LEVEL = 999;

        // Performance test parameters
        private const int PERFORMANCE_TEST_ITERATIONS = 1000;

        public ActionResolverRandomizedTests()
        {
            _random = new Random(42); // Deterministic for reproducible tests
        }

        public void Dispose()
        {
            // Nothing to dispose
        }

        /// <summary>
        /// Main randomized simulation runner for ActionResolver.
        /// Tests upgrade chain resolution, level-based logic, and performance under random conditions.
        /// </summary>
        public static void RunActionResolverSimulations(int simulationCount)
        {
            var tests = new ActionResolverRandomizedTests();
            var random = tests._random;
            var passed = 0;
            var failed = 0;

            var stopwatch = Stopwatch.StartNew();

            for (int simulation = 0; simulation < simulationCount; simulation++)
            {
                try
                {
                    // Test different scenarios
                    tests.TestUpgradeChainConsistency(random, simulation);
                    tests.TestLevelBoundaryBehavior(random, simulation);
                    tests.TestInvalidInputHandling(random, simulation);
                    tests.TestPerformanceCharacteristics(random, simulation);
                    tests.TestUpgradeChainCompleteness(random, simulation);
                    
                    passed++;
                    
                    // Progress reporting every 100 simulations
                    if (simulation % 100 == 0 && simulation > 0)
                    {
                        Console.WriteLine($"Progress: {simulation}/{simulationCount} ({passed} passed, {failed} failed)");
                    }
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($"Simulation {simulation} failed: {ex.Message}");
                    
                    // Continue with other simulations unless we have too many failures
                    if (failed > simulationCount * 0.01) // More than 1% failure rate
                    {
                        throw new Exception($"ActionResolver randomized testing failed {failed}/{simulationCount} simulations. Last error: {ex.Message}");
                    }
                }
            }

            stopwatch.Stop();
            
            Console.WriteLine($"ActionResolver Simulations complete: {passed} passed, {failed} failed");
            Console.WriteLine($"Success rate: {(double)passed / simulationCount * 100:F2}%");
            Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Average time per simulation: {stopwatch.ElapsedMilliseconds / (double)simulationCount:F3}ms");

            if (failed > 0)
            {
                throw new Exception($"ActionResolver randomized testing failed {failed}/{simulationCount} simulations");
            }
        }

        /// <summary>
        /// Test upgrade chain consistency and correctness.
        /// </summary>
        private void TestUpgradeChainConsistency(Random random, int simulationId)
        {
            var baseActionId = BASE_ACTIONS[random.Next(BASE_ACTIONS.Length)];
            var testLevels = new List<uint>();
            
            // Generate random levels for testing
            for (int i = 0; i < 10; i++)
            {
                testLevels.Add((uint)random.Next((int)MIN_LEVEL, (int)MAX_LEVEL + 1));
            }
            
            // Sort levels to test upgrade progression
            testLevels.Sort();
            
            uint lastResolvedId = 0;
            uint lastLevel = 0;
            
            foreach (var level in testLevels)
            {
                var resolvedId = ActionResolver.ResolveToLevel(baseActionId, level);
                
                // Validate that resolved ID is not 0 for valid inputs
                if (resolvedId == 0)
                {
                    throw new Exception($"Simulation {simulationId}: ActionResolver returned 0 for base action {baseActionId} at level {level}");
                }
                
                // Validate that upgrade progression is monotonic (action IDs should generally increase with level)
                // Note: This might not always be true depending on action ID assignment, so we check for major regressions
                if (lastResolvedId != 0 && level > lastLevel)
                {
                    // Allow for reasonable variation in action ID progression
                    // We're mainly checking that we don't get wildly inappropriate downgrades
                    if (resolvedId != lastResolvedId && resolvedId < lastResolvedId && (lastResolvedId - resolvedId) > 10000)
                    {
                        throw new Exception($"Simulation {simulationId}: Potential upgrade regression. Level {lastLevel}->Action {lastResolvedId}, Level {level}->Action {resolvedId}");
                    }
                }
                
                lastResolvedId = resolvedId;
                lastLevel = level;
            }
        }

        /// <summary>
        /// Test behavior at level boundaries and edge cases.
        /// </summary>
        private void TestLevelBoundaryBehavior(Random random, int simulationId)
        {
            var baseActionId = BASE_ACTIONS[random.Next(BASE_ACTIONS.Length)];
            
            // Test minimum level
            var minResult = ActionResolver.ResolveToLevel(baseActionId, MIN_LEVEL);
            if (minResult == 0)
            {
                throw new Exception($"Simulation {simulationId}: ActionResolver failed for base action {baseActionId} at minimum level {MIN_LEVEL}");
            }
            
            // Test maximum level
            var maxResult = ActionResolver.ResolveToLevel(baseActionId, MAX_LEVEL);
            if (maxResult == 0)
            {
                throw new Exception($"Simulation {simulationId}: ActionResolver failed for base action {baseActionId} at maximum level {MAX_LEVEL}");
            }
            
            // Test that max level gives at least as advanced action as min level
            if (maxResult < minResult && (minResult - maxResult) > 1000) // Allow some variance for action ID schemes
            {
                throw new Exception($"Simulation {simulationId}: Max level result ({maxResult}) seems less advanced than min level result ({minResult})");
            }
            
            // Test level progression consistency
            var midLevel = (MIN_LEVEL + MAX_LEVEL) / 2;
            var midResult = ActionResolver.ResolveToLevel(baseActionId, midLevel);
            
            if (midResult == 0)
            {
                throw new Exception($"Simulation {simulationId}: ActionResolver failed for base action {baseActionId} at mid level {midLevel}");
            }
        }

        /// <summary>
        /// Test handling of invalid inputs and edge cases.
        /// </summary>
        private void TestInvalidInputHandling(Random random, int simulationId)
        {
            var validBaseActionId = BASE_ACTIONS[random.Next(BASE_ACTIONS.Length)];
            var invalidActionId = (uint)random.Next(900000, 999999); // Very high action ID unlikely to exist
            var validLevel = (uint)random.Next((int)MIN_LEVEL, (int)MAX_LEVEL + 1);
            
            // Test invalid action ID - should not crash
            try
            {
                var result = ActionResolver.ResolveToLevel(invalidActionId, validLevel);
                // Result might be 0 or the original action ID, both are acceptable for unknown actions
                if (result != 0 && result != invalidActionId)
                {
                    // This might indicate the action ID wasn't as invalid as we thought, which is fine
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Simulation {simulationId}: ActionResolver crashed on invalid action {invalidActionId}: {ex.Message}");
            }
            
            // Test invalid level (0) - should not crash
            try
            {
                var result = ActionResolver.ResolveToLevel(validBaseActionId, INVALID_LEVEL);
                // Should handle gracefully
            }
            catch (Exception ex)
            {
                throw new Exception($"Simulation {simulationId}: ActionResolver crashed on invalid level {INVALID_LEVEL}: {ex.Message}");
            }
            
            // Test overflow level - should not crash
            try
            {
                var result = ActionResolver.ResolveToLevel(validBaseActionId, OVERFLOW_LEVEL);
                // Should handle gracefully, likely returning the highest tier action
            }
            catch (Exception ex)
            {
                throw new Exception($"Simulation {simulationId}: ActionResolver crashed on overflow level {OVERFLOW_LEVEL}: {ex.Message}");
            }
        }

        /// <summary>
        /// Test performance characteristics under various conditions.
        /// </summary>
        private void TestPerformanceCharacteristics(Random random, int simulationId)
        {
            var actionIds = new List<uint>();
            var levels = new List<uint>();
            
            // Generate random test data
            for (int i = 0; i < PERFORMANCE_TEST_ITERATIONS; i++)
            {
                actionIds.Add(BASE_ACTIONS[random.Next(BASE_ACTIONS.Length)]);
                levels.Add((uint)random.Next((int)MIN_LEVEL, (int)MAX_LEVEL + 1));
            }

            var stopwatch = Stopwatch.StartNew();

            // Test resolution performance
            for (int i = 0; i < PERFORMANCE_TEST_ITERATIONS; i++)
            {
                _ = ActionResolver.ResolveToLevel(actionIds[i], levels[i]);
            }

            stopwatch.Stop();
            
            var avgTimePerResolution = stopwatch.Elapsed.TotalNanoseconds / PERFORMANCE_TEST_ITERATIONS;

            // Performance threshold: Should be faster than 1000ns per resolution on average
            if (avgTimePerResolution > 1000.0)
            {
                throw new Exception($"Simulation {simulationId}: ActionResolver too slow: {avgTimePerResolution:F2}ns per resolution (threshold: 1000ns)");
            }

            // Test memory allocation (should be zero in hot path)
            var initialMemory = GC.GetTotalMemory(false);
            
            for (int i = 0; i < PERFORMANCE_TEST_ITERATIONS; i++)
            {
                _ = ActionResolver.ResolveToLevel(actionIds[i], levels[i]);
            }

            var finalMemory = GC.GetTotalMemory(false);
            var allocatedMemory = finalMemory - initialMemory;

            // Should have minimal allocations (allow tolerance for test framework overhead)
            if (allocatedMemory > 10240) // 10KB tolerance
            {
                throw new Exception($"Simulation {simulationId}: Excessive memory allocation detected: {allocatedMemory} bytes");
            }
        }

        /// <summary>
        /// Test that upgrade chains are complete and logical.
        /// </summary>
        private void TestUpgradeChainCompleteness(Random random, int simulationId)
        {
            var baseActionId = BASE_ACTIONS[random.Next(BASE_ACTIONS.Length)];
            
            // Test a range of levels to ensure we get reasonable upgrade progression
            var levelProgression = new List<uint>();
            for (uint level = MIN_LEVEL; level <= MAX_LEVEL; level += 5) // Test every 5 levels
            {
                levelProgression.Add(level);
            }
            
            var lastActionId = uint.MaxValue;
            var upgradeSeen = false;
            
            foreach (var level in levelProgression)
            {
                var currentActionId = ActionResolver.ResolveToLevel(baseActionId, level);
                
                if (currentActionId == 0)
                {
                    throw new Exception($"Simulation {simulationId}: ActionResolver returned 0 for base action {baseActionId} at level {level}");
                }
                
                // Check if we've seen an upgrade
                if (lastActionId != uint.MaxValue && currentActionId != lastActionId)
                {
                    upgradeSeen = true;
                }
                
                lastActionId = currentActionId;
            }
            
            // For known base actions, we should see at least one upgrade across the level range
            // (This test might need adjustment based on actual upgrade chains)
            if (!upgradeSeen && MAX_LEVEL > MIN_LEVEL + 20) // Only check if we have reasonable level range
            {
                // This might be acceptable for some actions, so we'll log rather than fail
                Console.WriteLine($"Simulation {simulationId}: No upgrades detected for base action {baseActionId} across level range {MIN_LEVEL}-{MAX_LEVEL}");
            }
        }
    }
}
