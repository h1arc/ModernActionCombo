using System;
using System.Collections.Generic;
using System.Diagnostics;
using ModernActionCombo.Core.Data;
using Xunit;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// Focused randomized testing for ActionResolver without cache interference.
    /// Tests pure action resolution logic, upgrade chains, and performance.
    /// </summary>
    public class ActionResolverFocusedRandomizedTests
    {
        private readonly Random _random;
        
        // Known action upgrade chains from ActionResolver
        private static readonly uint[] STONE_CHAIN = ActionResolver.StoneGlareChain; // [119, 127, 3568, 16533, 25859]
        private static readonly uint[] AERO_CHAIN = ActionResolver.AeroDiaChain;     // [121, 132, 16532]
        private static readonly uint[] HOLY_CHAIN = ActionResolver.HolyChain;       // [139, 25860]
        
        // All known base actions for testing
        private static readonly uint[] ALL_BASE_ACTIONS = [119, 121, 139]; // Stone, Aero, Holy base actions
        
        // Level ranges for comprehensive testing
        private const uint MIN_LEVEL = 1;
        private const uint MAX_LEVEL = 90;
        private const int PERFORMANCE_TEST_ITERATIONS = 1000;

        public ActionResolverFocusedRandomizedTests()
        {
            _random = new Random(42); // Deterministic for reproducible tests
        }

        /// <summary>
        /// Main randomized simulation runner for ActionResolver (without cache interference).
        /// Tests pure action resolution logic and performance under random conditions.
        /// </summary>
        public static void RunActionResolverFocusedSimulations(int simulationCount)
        {
            var tests = new ActionResolverFocusedRandomizedTests();
            var random = tests._random;
            var passed = 0;
            var failed = 0;

            var stopwatch = Stopwatch.StartNew();

            for (int simulation = 0; simulation < simulationCount; simulation++)
            {
                try
                {
                    // Test different scenarios focusing purely on ActionResolver
                    tests.TestUpgradeChainLogic(random, simulation);
                    tests.TestLevelBoundaryBehavior(random, simulation);
                    tests.TestInvalidInputHandling(random, simulation);
                    tests.TestPureResolverPerformance(random, simulation);
                    tests.TestUpgradeProgressionConsistency(random, simulation);
                    tests.TestKnownActionChains(random, simulation);
                    
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
                        throw new Exception($"ActionResolver focused testing failed {failed}/{simulationCount} simulations. Last error: {ex.Message}");
                    }
                }
            }

            stopwatch.Stop();
            
            Console.WriteLine($"ActionResolver Focused Simulations complete: {passed} passed, {failed} failed");
            Console.WriteLine($"Success rate: {(double)passed / simulationCount * 100:F2}%");
            Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Average time per simulation: {stopwatch.ElapsedMilliseconds / (double)simulationCount:F3}ms");

            if (failed > 0)
            {
                throw new Exception($"ActionResolver focused testing failed {failed}/{simulationCount} simulations");
            }
        }

        /// <summary>
        /// Test upgrade chain logic and correctness without cache interference.
        /// </summary>
        private void TestUpgradeChainLogic(Random random, int simulationId)
        {
            var baseActionId = ALL_BASE_ACTIONS[random.Next(ALL_BASE_ACTIONS.Length)];
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
                
                // For known upgrade chains, validate the resolved action is in the correct chain
                if (IsInKnownChain(baseActionId, resolvedId) == false)
                {
                    throw new Exception($"Simulation {simulationId}: Resolved action {resolvedId} not in expected chain for base action {baseActionId}");
                }
                
                // Validate that upgrades don't go backwards (higher level should never give lower tier action)
                if (lastResolvedId != 0 && level > lastLevel)
                {
                    if (!IsUpgradeValidProgression(lastResolvedId, resolvedId, baseActionId))
                    {
                        throw new Exception($"Simulation {simulationId}: Invalid upgrade progression. Level {lastLevel}->Action {lastResolvedId}, Level {level}->Action {resolvedId}");
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
            var baseActionId = ALL_BASE_ACTIONS[random.Next(ALL_BASE_ACTIONS.Length)];
            
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
            
            // Test critical upgrade levels (where action changes)
            if (baseActionId == 119) // Stone chain
            {
                // Test key upgrade levels for Stone chain
                var stoneLevel1 = ActionResolver.ResolveToLevel(119, 1);    // Should be Stone (119)
                var stoneLevel18 = ActionResolver.ResolveToLevel(119, 18);  // Should be Stone II (127)
                var stoneLevel54 = ActionResolver.ResolveToLevel(119, 54);  // Should be Stone III (3568)
                var stoneLevel72 = ActionResolver.ResolveToLevel(119, 72);  // Should be Glare (16533)
                var stoneLevel82 = ActionResolver.ResolveToLevel(119, 82);  // Should be Glare III (25859)
                
                if (stoneLevel1 != 119) throw new Exception($"Simulation {simulationId}: Stone level 1 should resolve to 119, got {stoneLevel1}");
                if (stoneLevel18 != 127) throw new Exception($"Simulation {simulationId}: Stone level 18 should resolve to 127, got {stoneLevel18}");
                if (stoneLevel54 != 3568) throw new Exception($"Simulation {simulationId}: Stone level 54 should resolve to 3568, got {stoneLevel54}");
                if (stoneLevel72 != 16533) throw new Exception($"Simulation {simulationId}: Stone level 72 should resolve to 16533, got {stoneLevel72}");
                if (stoneLevel82 != 25859) throw new Exception($"Simulation {simulationId}: Stone level 82 should resolve to 25859, got {stoneLevel82}");
            }
        }

        /// <summary>
        /// Test handling of invalid inputs and edge cases.
        /// </summary>
        private void TestInvalidInputHandling(Random random, int simulationId)
        {
            var validBaseActionId = ALL_BASE_ACTIONS[random.Next(ALL_BASE_ACTIONS.Length)];
            var invalidActionId = (uint)random.Next(900000, 999999); // Very high action ID unlikely to exist
            var validLevel = (uint)random.Next((int)MIN_LEVEL, (int)MAX_LEVEL + 1);
            
            // Test invalid action ID - should not crash, should return original ID
            try
            {
                var result = ActionResolver.ResolveToLevel(invalidActionId, validLevel);
                // For unknown actions, ActionResolver should return the original action ID
                if (result != invalidActionId)
                {
                    throw new Exception($"Simulation {simulationId}: Expected unknown action {invalidActionId} to return itself, got {result}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Simulation {simulationId}: ActionResolver crashed on invalid action {invalidActionId}: {ex.Message}");
            }
            
            // Test level 0 - should not crash
            try
            {
                var result = ActionResolver.ResolveToLevel(validBaseActionId, 0);
                // Should handle gracefully, likely returning base action
                if (result == 0)
                {
                    throw new Exception($"Simulation {simulationId}: ActionResolver returned 0 for level 0");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Simulation {simulationId}: ActionResolver crashed on level 0: {ex.Message}");
            }
            
            // Test very high level - should not crash
            try
            {
                var result = ActionResolver.ResolveToLevel(validBaseActionId, 999);
                // Should handle gracefully, returning highest tier action
                if (result == 0)
                {
                    throw new Exception($"Simulation {simulationId}: ActionResolver returned 0 for high level");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Simulation {simulationId}: ActionResolver crashed on high level: {ex.Message}");
            }
        }

        /// <summary>
        /// Test pure resolver performance without cache interference.
        /// </summary>
        private void TestPureResolverPerformance(Random random, int simulationId)
        {
            var actionIds = new List<uint>();
            var levels = new List<uint>();
            
            // Generate random test data
            for (int i = 0; i < PERFORMANCE_TEST_ITERATIONS; i++)
            {
                actionIds.Add(ALL_BASE_ACTIONS[random.Next(ALL_BASE_ACTIONS.Length)]);
                levels.Add((uint)random.Next((int)MIN_LEVEL, (int)MAX_LEVEL + 1));
            }

            var stopwatch = Stopwatch.StartNew();

            // Test pure resolution performance
            for (int i = 0; i < PERFORMANCE_TEST_ITERATIONS; i++)
            {
                _ = ActionResolver.ResolveToLevel(actionIds[i], levels[i]);
            }

            stopwatch.Stop();
            
            var avgTimePerResolution = stopwatch.Elapsed.TotalNanoseconds / PERFORMANCE_TEST_ITERATIONS;

            // Performance threshold: Should be extremely fast (sub-500ns) since it's just dictionary lookup + array scan
            if (avgTimePerResolution > 500.0)
            {
                throw new Exception($"Simulation {simulationId}: ActionResolver too slow: {avgTimePerResolution:F2}ns per resolution (threshold: 500ns)");
            }

            // Test memory allocation (should be zero - pure static operations)
            var initialMemory = GC.GetTotalMemory(false);
            
            for (int i = 0; i < PERFORMANCE_TEST_ITERATIONS; i++)
            {
                _ = ActionResolver.ResolveToLevel(actionIds[i], levels[i]);
            }

            var finalMemory = GC.GetTotalMemory(false);
            var allocatedMemory = finalMemory - initialMemory;

            // Should have zero allocations (pure static lookups)
            if (allocatedMemory > 10240) // 10KB tolerance for test framework + GC overhead
            {
                throw new Exception($"Simulation {simulationId}: Unexpected memory allocation detected: {allocatedMemory} bytes");
            }
        }

        /// <summary>
        /// Test upgrade progression consistency across level ranges.
        /// </summary>
        private void TestUpgradeProgressionConsistency(Random random, int simulationId)
        {
            var baseActionId = ALL_BASE_ACTIONS[random.Next(ALL_BASE_ACTIONS.Length)];
            
            // Test consistent progression across full level range
            uint lastActionId = 0;
            uint lastLevel = 0;
            
            for (uint level = MIN_LEVEL; level <= MAX_LEVEL; level += 5) // Test every 5 levels
            {
                var currentActionId = ActionResolver.ResolveToLevel(baseActionId, level);
                
                if (currentActionId == 0)
                {
                    throw new Exception($"Simulation {simulationId}: ActionResolver returned 0 for base action {baseActionId} at level {level}");
                }
                
                // Verify progression never goes backwards
                if (lastActionId != 0 && level > lastLevel)
                {
                    if (!IsUpgradeValidProgression(lastActionId, currentActionId, baseActionId))
                    {
                        throw new Exception($"Simulation {simulationId}: Invalid progression from level {lastLevel} (action {lastActionId}) to level {level} (action {currentActionId})");
                    }
                }
                
                lastActionId = currentActionId;
                lastLevel = level;
            }
        }

        /// <summary>
        /// Test known action chains for correctness.
        /// </summary>
        private void TestKnownActionChains(Random random, int simulationId)
        {
            // Test Stone/Glare chain specifically
            foreach (var actionInChain in STONE_CHAIN)
            {
                var resolved = ActionResolver.ResolveToLevel(actionInChain, 90); // Max level
                if (!STONE_CHAIN.Contains(resolved))
                {
                    throw new Exception($"Simulation {simulationId}: Stone chain action {actionInChain} resolved to {resolved} which is not in Stone chain");
                }
            }
            
            // Test Aero/Dia chain specifically
            foreach (var actionInChain in AERO_CHAIN)
            {
                var resolved = ActionResolver.ResolveToLevel(actionInChain, 90); // Max level
                if (!AERO_CHAIN.Contains(resolved))
                {
                    throw new Exception($"Simulation {simulationId}: Aero chain action {actionInChain} resolved to {resolved} which is not in Aero chain");
                }
            }
            
            // Test Holy chain specifically
            foreach (var actionInChain in HOLY_CHAIN)
            {
                var resolved = ActionResolver.ResolveToLevel(actionInChain, 90); // Max level
                if (!HOLY_CHAIN.Contains(resolved))
                {
                    throw new Exception($"Simulation {simulationId}: Holy chain action {actionInChain} resolved to {resolved} which is not in Holy chain");
                }
            }
        }

        /// <summary>
        /// Helper: Check if a resolved action is in the correct known chain.
        /// </summary>
        private bool IsInKnownChain(uint baseActionId, uint resolvedActionId)
        {
            return baseActionId switch
            {
                119 => STONE_CHAIN.Contains(resolvedActionId), // Stone base
                121 => AERO_CHAIN.Contains(resolvedActionId),  // Aero base
                139 => HOLY_CHAIN.Contains(resolvedActionId),  // Holy base
                _ => true // Unknown base action, assume valid
            };
        }

        /// <summary>
        /// Helper: Check if upgrade progression is valid (no downgrades).
        /// </summary>
        private bool IsUpgradeValidProgression(uint fromActionId, uint toActionId, uint baseActionId)
        {
            // If actions are the same, that's fine (no upgrade at this level)
            if (fromActionId == toActionId) return true;
            
            // For known chains, check that we're progressing forward in the chain
            return baseActionId switch
            {
                119 => GetChainIndex(STONE_CHAIN, fromActionId) <= GetChainIndex(STONE_CHAIN, toActionId),
                121 => GetChainIndex(AERO_CHAIN, fromActionId) <= GetChainIndex(AERO_CHAIN, toActionId),
                139 => GetChainIndex(HOLY_CHAIN, fromActionId) <= GetChainIndex(HOLY_CHAIN, toActionId),
                _ => true // Unknown chain, assume valid
            };
        }

        /// <summary>
        /// Helper: Get the index of an action in a chain (-1 if not found).
        /// </summary>
        private int GetChainIndex(uint[] chain, uint actionId)
        {
            for (int i = 0; i < chain.Length; i++)
            {
                if (chain[i] == actionId) return i;
            }
            return -1;
        }
    }
}
