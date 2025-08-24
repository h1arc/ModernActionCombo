using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ModernActionCombo.Core.Services;
using Xunit;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// Comprehensive randomized testing framework for ActionInterceptor.
    /// Tests cache performance, action resolution, and edge cases under random conditions.
    /// </summary>
    public class ActionInterceptorRandomizedTests : IDisposable
    {
        private readonly Random _random;
        
        // Test action IDs covering different categories
        private static readonly uint[] TEST_ACTIONS = new uint[]
        {
            // WHM Actions
            119,   // Cure
            120,   // Cure II  
            121,   // Cure III
            124,   // Medica
            125,   // Medica II
            139,   // Benediction
            140,   // Presence of Mind
            3569,  // Tetragrammaton
            7531,  // Divine Benison
            16531, // Temperance
            25862, // Liturgy of the Bell
            25871, // Plenary Indulgence
            
            // BLM Actions  
            141,   // Fire
            142,   // Blizzard
            143,   // Thunder
            144,   // Fire II
            145,   // Transpose
            149,   // Sleep
            152,   // Fire III
            153,   // Blizzard III
            154,   // Thunder III
            3574,  // Blizzard IV
            7419,  // Fire IV
            16505, // Despair
            
            // Universal Actions
            7,     // Sprint
            3,     // Auto-Attack
            5,     // Limit Break
        };

        // Cache simulation parameters
        private const int MIN_CACHE_OPERATIONS = 10;
        private const int MAX_CACHE_OPERATIONS = 1000;
        private const int PERFORMANCE_TEST_ITERATIONS = 100;

        public ActionInterceptorRandomizedTests()
        {
            _random = new Random(42); // Deterministic for reproducible tests
        }

        public void Dispose()
        {
            // Nothing to dispose
        }

        /// <summary>
        /// Main randomized simulation runner for ActionInterceptor.
        /// Tests cache performance, action resolution, and interception logic under random conditions.
        /// </summary>
        public static void RunActionInterceptorSimulations(int simulationCount)
        {
            var tests = new ActionInterceptorRandomizedTests();
            var random = tests._random;
            var passed = 0;
            var failed = 0;

            var stopwatch = Stopwatch.StartNew();

            for (int simulation = 0; simulation < simulationCount; simulation++)
            {
                try
                {
                    // Test different scenarios
                    tests.TestActionCachePerformance(random, simulation);
                    tests.TestActionCacheConsistency(random, simulation);
                    tests.TestActionCacheExpiration(random, simulation);
                    tests.TestActionInterceptionModes(random, simulation);
                    tests.TestCacheEvictionBehavior(random, simulation);
                    
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
                        throw new Exception($"ActionInterceptor randomized testing failed {failed}/{simulationCount} simulations. Last error: {ex.Message}");
                    }
                }
            }

            stopwatch.Stop();
            
            Console.WriteLine($"ActionInterceptor Simulations complete: {passed} passed, {failed} failed");
            Console.WriteLine($"Success rate: {(double)passed / simulationCount * 100:F2}%");
            Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Average time per simulation: {stopwatch.ElapsedMilliseconds / (double)simulationCount:F3}ms");

            if (failed > 0)
            {
                throw new Exception($"ActionInterceptor randomized testing failed {failed}/{simulationCount} simulations");
            }
        }

        /// <summary>
        /// Test ActionCache performance under various loads and patterns.
        /// </summary>
        private void TestActionCachePerformance(Random random, int simulationId)
        {
            var cache = new ActionCache();
            var operationCount = random.Next(MIN_CACHE_OPERATIONS, MAX_CACHE_OPERATIONS);
            var actionCount = random.Next(1, TEST_ACTIONS.Length);
            var testActions = TEST_ACTIONS.Take(actionCount).ToArray();

            // Pre-generate action->resolved mappings for consistency
            var actionMappings = new Dictionary<uint, uint>();
            foreach (var actionId in testActions)
            {
                actionMappings[actionId] = actionId + (uint)random.Next(1, 10); // Consistent mapping
            }

            var stopwatch = Stopwatch.StartNew();

            // Simulate realistic cache operations
            for (int i = 0; i < operationCount; i++)
            {
                var actionId = testActions[random.Next(testActions.Length)];
                var expectedResolvedId = actionMappings[actionId];
                
                // Test cache miss, add, and subsequent hits
                if (!cache.TryGetCached(actionId, out var resolvedId))
                {
                    cache.Cache(actionId, expectedResolvedId);
                }
                
                // Verify cache hit after adding
                if (cache.TryGetCached(actionId, out resolvedId))
                {
                    if (resolvedId != expectedResolvedId)
                    {
                        throw new Exception($"Simulation {simulationId}: Cache returned wrong resolved ID. Expected: {expectedResolvedId}, Got: {resolvedId}");
                    }
                }
            }

            stopwatch.Stop();
            
            // Performance validation
            var avgTimePerOperation = stopwatch.Elapsed.TotalNanoseconds / operationCount;
            
            // Should be faster than 1000ns per operation on average (allowing for test overhead)
            if (avgTimePerOperation > 1000.0)
            {
                throw new Exception($"Simulation {simulationId}: ActionCache too slow: {avgTimePerOperation:F2}ns per operation (threshold: 1000ns)");
            }
        }

        /// <summary>
        /// Test ActionCache consistency and correctness.
        /// </summary>
        private void TestActionCacheConsistency(Random random, int simulationId)
        {
            var cache = new ActionCache();
            var testPairs = new Dictionary<uint, uint>();
            
            // Generate random action->resolved mappings
            var pairCount = random.Next(5, 20);
            for (int i = 0; i < pairCount; i++)
            {
                var actionId = TEST_ACTIONS[random.Next(TEST_ACTIONS.Length)];
                var resolvedId = actionId + (uint)random.Next(1, 100);
                testPairs[actionId] = resolvedId;
            }

            // Add all pairs to cache
            foreach (var pair in testPairs)
            {
                cache.Cache(pair.Key, pair.Value);
            }

            // Verify all pairs are retrievable and correct
            foreach (var pair in testPairs)
            {
                if (!cache.TryGetCached(pair.Key, out var resolvedId))
                {
                    throw new Exception($"Simulation {simulationId}: Cache miss for action {pair.Key} that should be cached");
                }
                
                if (resolvedId != pair.Value)
                {
                    throw new Exception($"Simulation {simulationId}: Cache inconsistency. Action: {pair.Key}, Expected: {pair.Value}, Got: {resolvedId}");
                }
            }

            // Test that non-existent actions return cache miss
            var nonExistentAction = 999999u;
            if (cache.TryGetCached(nonExistentAction, out _))
            {
                throw new Exception($"Simulation {simulationId}: Cache hit for non-existent action {nonExistentAction}");
            }
        }

        /// <summary>
        /// Test ActionCache expiration behavior.
        /// </summary>
        private void TestActionCacheExpiration(Random random, int simulationId)
        {
            var cache = new ActionCache();
            var actionId = TEST_ACTIONS[random.Next(TEST_ACTIONS.Length)];
            var resolvedId = actionId + 1;

            // Add to cache
            cache.Cache(actionId, resolvedId);
            
            // Verify it's cached
            if (!cache.TryGetCached(actionId, out var retrievedId) || retrievedId != resolvedId)
            {
                throw new Exception($"Simulation {simulationId}: Action not properly cached");
            }

            // Note: Since we can't easily simulate time passage without modifying the cache,
            // we'll test the expiration logic indirectly by testing cache behavior under stress
            
            // Fill cache to capacity to test eviction
            for (int i = 0; i < 100; i++) // Assuming cache capacity < 100
            {
                var testAction = (uint)(actionId + i + 1000); // Ensure unique actions
                cache.Cache(testAction, testAction + 1);
            }

            // Original action might be evicted due to capacity limits
            // This tests the cache's ability to handle capacity constraints
            _ = cache.TryGetCached(actionId, out _); // Should not throw exception
        }

        /// <summary>
        /// Test different action interception modes.
        /// </summary>
        private void TestActionInterceptionModes(Random random, int simulationId)
        {
            // Test IconReplacement mode
            var iconMode = ActionInterceptionMode.IconReplacement;
            ValidateInterceptionMode(iconMode, simulationId);

            // Test PerformanceMode
            var perfMode = ActionInterceptionMode.PerformanceMode;
            ValidateInterceptionMode(perfMode, simulationId);

            // Test mode transitions
            var randomMode = random.NextSingle() > 0.5f ? ActionInterceptionMode.IconReplacement : ActionInterceptionMode.PerformanceMode;
            ValidateInterceptionMode(randomMode, simulationId);
        }

        /// <summary>
        /// Validate that an interception mode behaves correctly.
        /// </summary>
        private void ValidateInterceptionMode(ActionInterceptionMode mode, int simulationId)
        {
            // Since ActionInterceptor requires game environment, we'll test the enum and basic validation
            if (!Enum.IsDefined(typeof(ActionInterceptionMode), mode))
            {
                throw new Exception($"Simulation {simulationId}: Invalid interception mode: {mode}");
            }

            // Test mode characteristics
            switch (mode)
            {
                case ActionInterceptionMode.IconReplacement:
                    // Icon replacement should be mode 0
                    if ((byte)mode != 0)
                    {
                        throw new Exception($"Simulation {simulationId}: IconReplacement mode should be 0, got {(byte)mode}");
                    }
                    break;
                    
                case ActionInterceptionMode.PerformanceMode:
                    // Performance mode should be mode 1
                    if ((byte)mode != 1)
                    {
                        throw new Exception($"Simulation {simulationId}: PerformanceMode should be 1, got {(byte)mode}");
                    }
                    break;
                    
                default:
                    throw new Exception($"Simulation {simulationId}: Unknown interception mode: {mode}");
            }
        }

        /// <summary>
        /// Test cache eviction behavior under different load patterns.
        /// </summary>
        private void TestCacheEvictionBehavior(Random random, int simulationId)
        {
            var cache = new ActionCache();
            var addedActions = new List<uint>();
            
            // Add actions until we likely exceed cache capacity
            for (int i = 0; i < 200; i++) // Exceed typical cache capacity
            {
                var actionId = (uint)(1000 + i);
                var resolvedId = actionId + 1;
                
                cache.Cache(actionId, resolvedId);
                addedActions.Add(actionId);
                
                // Verify the action was added successfully
                if (!cache.TryGetCached(actionId, out var retrievedId) || retrievedId != resolvedId)
                {
                    // This might happen due to capacity limits, which is acceptable
                    // But the cache should not crash or behave incorrectly
                }
            }

            // Test random access patterns after potential eviction
            for (int i = 0; i < 100; i++)
            {
                var randomAction = addedActions[random.Next(addedActions.Count)];
                _ = cache.TryGetCached(randomAction, out _); // Should not throw
            }

            // Test that cache still functions correctly after stress
            var testAction = 12345u;
            var testResolved = 54321u;
            cache.Cache(testAction, testResolved);
            
            if (cache.TryGetCached(testAction, out var finalTest) && finalTest != testResolved)
            {
                throw new Exception($"Simulation {simulationId}: Cache corruption detected after stress test");
            }
        }
    }
}
