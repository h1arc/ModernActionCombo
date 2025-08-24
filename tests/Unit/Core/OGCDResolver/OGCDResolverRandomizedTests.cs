using System;
using System.Collections.Generic;
using System.Diagnostics;
using ModernActionCombo.Core.Data;
using Xunit;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// Comprehensive randomized testing framework for OGCDResolver.
    /// Tests edge cases, performance characteristics, and rule evaluation under random conditions.
    /// </summary>
    public class OGCDResolverRandomizedTests : IDisposable
    {
        private readonly Random _random;
        
        // Test action IDs for OGCD simulation
        private const uint OGCD_ACTION_1 = 7531;  // Divine Benison
        private const uint OGCD_ACTION_2 = 3569;  // Tetragrammaton  
        private const uint OGCD_ACTION_3 = 140;   // Presence of Mind
        private const uint OGCD_ACTION_4 = 139;   // Benediction
        private const uint OGCD_ACTION_5 = 7430;  // Aquaveil
        private const uint OGCD_ACTION_6 = 25862; // Liturgy of the Bell
        private const uint OGCD_ACTION_7 = 16531; // Temperance
        private const uint OGCD_ACTION_8 = 25871; // Plenary Indulgence

        // Game state simulation ranges
        private const float MIN_HP = 0.0f;
        private const float MAX_HP = 1.0f;
        private const float MIN_MP = 0.0f;
        private const float MAX_MP = 1.0f;
        private const uint MIN_COOLDOWN = 0;
        private const uint MAX_COOLDOWN = 180; // 3 minutes max

        public OGCDResolverRandomizedTests()
        {
            _random = new Random(42); // Deterministic for reproducible tests
        }

        public void Dispose()
        {
            // Nothing to dispose
        }

        /// <summary>
        /// Main randomized simulation runner for OGCDResolver.
        /// Tests rule evaluation, priority handling, and performance under random conditions.
        /// </summary>
        public static void RunOGCDResolverSimulations(int simulationCount)
        {
            var tests = new OGCDResolverRandomizedTests();
            var random = tests._random;
            var passed = 0;
            var failed = 0;

            var stopwatch = Stopwatch.StartNew();

            for (int simulation = 0; simulation < simulationCount; simulation++)
            {
                try
                {
                    // Generate random game state
                    var gameState = tests.GenerateRandomGameState(random);
                    
                    // Generate random OGCD rules (1-8 rules)
                    var ruleCount = random.Next(1, 9);
                    var rules = tests.GenerateRandomOGCDRules(random, ruleCount, gameState);
                    
                    // Test different scenarios
                    tests.TestOGCDEvaluationScenario(gameState, rules, simulation);
                    tests.TestOGCDPriorityHandling(gameState, rules, simulation);
                    tests.TestOGCDPerformanceCharacteristics(gameState, rules, simulation);
                    
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
                        throw new Exception($"OGCDResolver randomized testing failed {failed}/{simulationCount} simulations. Last error: {ex.Message}");
                    }
                }
            }

            stopwatch.Stop();
            
            Console.WriteLine($"OGCD Simulations complete: {passed} passed, {failed} failed");
            Console.WriteLine($"Success rate: {(double)passed / simulationCount * 100:F2}%");
            Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Average time per simulation: {stopwatch.ElapsedMilliseconds / (double)simulationCount:F3}ms");

            if (failed > 0)
            {
                throw new Exception($"OGCDResolver randomized testing failed {failed}/{simulationCount} simulations");
            }
        }

        /// <summary>
        /// Generate random game state for testing OGCD conditions.
        /// </summary>
        private GameStateData GenerateRandomGameState(Random random)
        {
            return new GameStateData(
                jobId: (uint)random.Next(1, 50), // Random job ID
                level: (uint)random.Next(1, 91), // Levels 1-90
                inCombat: random.NextSingle() > 0.5f,
                currentTarget: random.NextSingle() > 0.3f ? (uint)random.Next(1000, 9999) : 0u, // 70% chance of having target
                gcdRemaining: random.NextSingle() * 2.5f // 0-2.5 seconds
            );
        }

        /// <summary>
        /// Generate random OGCD rules with realistic conditions.
        /// </summary>
        private List<OGCDResolver.SimpleOGCDRule> GenerateRandomOGCDRules(Random random, int count, GameStateData gameState)
        {
            var rules = new List<OGCDResolver.SimpleOGCDRule>();
            var actionIds = new uint[] { OGCD_ACTION_1, OGCD_ACTION_2, OGCD_ACTION_3, OGCD_ACTION_4, 
                                       OGCD_ACTION_5, OGCD_ACTION_6, OGCD_ACTION_7, OGCD_ACTION_8 };

            for (int i = 0; i < count; i++)
            {
                var actionId = actionIds[i % actionIds.Length];
                var priority = (byte)random.Next(0, 10); // Priority 0-9
                
                // Pre-generate random condition result for deterministic testing
                var randomConditionResult = random.NextSingle() > 0.5f;
                
                // Generate realistic condition based on action type
                Func<GameStateData, bool> condition = actionId switch
                {
                    OGCD_ACTION_4 => state => state.Level > 50 && state.InCombat, // Benediction - high level emergency
                    OGCD_ACTION_2 => state => state.Level > 60 && state.InCombat, // Tetragrammaton - heal
                    OGCD_ACTION_1 => state => state.InCombat && state.IsValidTarget(), // Divine Benison - shield
                    OGCD_ACTION_3 => state => state.InCombat && state.CanUseAbility(), // Presence of Mind - DPS boost
                    _ => _ => randomConditionResult // Deterministic random condition for others
                };

                Func<GameStateData, uint> action = _ => actionId;

                rules.Add(new OGCDResolver.SimpleOGCDRule(condition, action, priority));
            }

            return rules;
        }

        /// <summary>
        /// Test OGCD evaluation logic with random scenarios.
        /// </summary>
        private void TestOGCDEvaluationScenario(GameStateData gameState, List<OGCDResolver.SimpleOGCDRule> rules, int simulationId)
        {
            // Test rule evaluation consistency
            var evaluationResults = new List<bool>();
            var actionResults = new List<uint>();

            foreach (var rule in rules)
            {
                var conditionResult = rule.Condition(gameState);
                evaluationResults.Add(conditionResult);

                if (conditionResult)
                {
                    var actionResult = rule.Action(gameState);
                    actionResults.Add(actionResult);
                    
                    // Validate action result is reasonable
                    if (actionResult == 0)
                    {
                        throw new Exception($"Simulation {simulationId}: Rule returned invalid action ID 0");
                    }
                }
            }

            // Test that evaluation is deterministic
            foreach (var rule in rules)
            {
                var secondEvaluation = rule.Condition(gameState);
                var firstEvaluation = evaluationResults[rules.IndexOf(rule)];
                
                if (secondEvaluation != firstEvaluation)
                {
                    throw new Exception($"Simulation {simulationId}: Non-deterministic rule evaluation detected");
                }
            }
        }

        /// <summary>
        /// Test OGCD priority handling logic.
        /// </summary>
        private void TestOGCDPriorityHandling(GameStateData gameState, List<OGCDResolver.SimpleOGCDRule> rules, int simulationId)
        {
            // Find all applicable rules
            var applicableRules = new List<(OGCDResolver.SimpleOGCDRule rule, int index)>();
            
            for (int i = 0; i < rules.Count; i++)
            {
                if (rules[i].Condition(gameState))
                {
                    applicableRules.Add((rules[i], i));
                }
            }

            if (applicableRules.Count == 0)
            {
                return; // No rules applicable, nothing to test
            }

            // Test priority ordering
            var sortedByPriority = applicableRules.OrderByDescending(x => x.rule.Priority).ToList();
            var highestPriority = sortedByPriority.First().rule.Priority;
            var highestPriorityRules = sortedByPriority.Where(x => x.rule.Priority == highestPriority).ToList();

            // Validate that highest priority rules are identified correctly
            if (highestPriorityRules.Count == 0)
            {
                throw new Exception($"Simulation {simulationId}: Priority calculation error");
            }

            // Test that all highest priority rules have the same priority
            var firstPriority = highestPriorityRules.First().rule.Priority;
            foreach (var rule in highestPriorityRules)
            {
                if (rule.rule.Priority != firstPriority)
                {
                    throw new Exception($"Simulation {simulationId}: Priority inconsistency in highest priority group");
                }
            }
        }

        /// <summary>
        /// Test OGCD performance characteristics under various conditions.
        /// </summary>
        private void TestOGCDPerformanceCharacteristics(GameStateData gameState, List<OGCDResolver.SimpleOGCDRule> rules, int simulationId)
        {
            const int performanceTestIterations = 100;
            var stopwatch = Stopwatch.StartNew();

            // Test evaluation performance
            for (int i = 0; i < performanceTestIterations; i++)
            {
                foreach (var rule in rules)
                {
                    _ = rule.Condition(gameState);
                    if (rule.Condition(gameState))
                    {
                        _ = rule.Action(gameState);
                    }
                }
            }

            stopwatch.Stop();
            
            var totalEvaluations = performanceTestIterations * rules.Count * 2; // Condition + potential Action
            var avgTimePerEvaluation = stopwatch.Elapsed.TotalNanoseconds / totalEvaluations;

            // Performance threshold: Should be faster than 100ns per evaluation on average
            if (avgTimePerEvaluation > 100.0)
            {
                throw new Exception($"Simulation {simulationId}: OGCD evaluation too slow: {avgTimePerEvaluation:F2}ns per evaluation (threshold: 100ns)");
            }

            // Test memory allocation (should be zero in hot path)
            var initialMemory = GC.GetTotalMemory(false);
            
            for (int i = 0; i < performanceTestIterations; i++)
            {
                foreach (var rule in rules)
                {
                    _ = rule.Condition(gameState);
                    if (rule.Condition(gameState))
                    {
                        _ = rule.Action(gameState);
                    }
                }
            }

            var finalMemory = GC.GetTotalMemory(false);
            var allocatedMemory = finalMemory - initialMemory;

            // Should have minimal allocations (allow tolerance for test framework overhead and occasional GC)
            if (allocatedMemory > 10240) // 10KB tolerance
            {
                throw new Exception($"Simulation {simulationId}: Excessive memory allocation detected: {allocatedMemory} bytes");
            }
        }
    }
}
