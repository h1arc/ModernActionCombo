using System;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// Randomized testing framework for SmartTargeting with configurable simulation counts.
    /// Example: dotnet test -- --simulations 1000
    /// </summary>
    public class SmartTargetingRandomizedTests : IDisposable
    {
        private static readonly Random _random = new();
        
        // Party member ID ranges for random generation
        private const uint MIN_MEMBER_ID = 1000;
        private const uint MAX_MEMBER_ID = 9999;
        
        public SmartTargetingRandomizedTests()
        {
            // Ensure clean state for each test
            SmartTargetingCache.ClearForTesting();
        }
        
        public void Dispose()
        {
            // Clear all cached state to ensure test isolation
            SmartTargetingCache.ClearForTesting();
        }
        
        // Status flag constants
        private const uint ALIVE = 1u << 0;
        private const uint IN_RANGE = 1u << 1;
        private const uint IN_LOS = 1u << 2;
        private const uint TARGETABLE = 1u << 3;
        private const uint SELF = 1u << 4;
        private const uint HARD_TARGET = 1u << 5;
        private const uint TANK = 1u << 6;
        private const uint HEALER = 1u << 7;
        private const uint MELEE = 1u << 8;
        private const uint ALLY = 1u << 10;        // Ally flag for healing targets
        
        private const uint VALID_TARGET = ALIVE | IN_RANGE | IN_LOS | TARGETABLE | ALLY;
        
        /// <summary>
        /// Run comprehensive randomized simulations of party targeting scenarios.
        /// Tests various edge cases, member compositions, and HP distributions.
        /// </summary>
        public static void RunSmartTargetingSimulations(int simulationCount = 1000)
        {
            Console.WriteLine($"Starting {simulationCount} SmartTargeting simulations...");
            
            int passed = 0;
            int failed = 0;
            
            for (int i = 0; i < simulationCount; i++)
            {
                try
                {
                    RunSingleSimulation(i);
                    passed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($"Simulation {i} failed: {ex.Message}");
                }
                
                // Progress report every 100 simulations
                if ((i + 1) % 100 == 0)
                {
                    Console.WriteLine($"Progress: {i + 1}/{simulationCount} ({passed} passed, {failed} failed)");
                }
            }
            
            Console.WriteLine($"Simulations complete: {passed} passed, {failed} failed");
            Console.WriteLine($"Success rate: {(double)passed / simulationCount * 100:F2}%");
            
            if (failed > 0)
            {
                throw new Exception($"Randomized testing failed {failed}/{simulationCount} simulations");
            }
        }
        
        private static void RunSingleSimulation(int simulationId)
        {
            // Clear state for clean simulation
            SmartTargetingCache.ClearForTesting();
            
            // Generate random party composition (1-8 members)
            int memberCount = _random.Next(1, 9);
            var scenario = GenerateRandomPartyScenario(memberCount, simulationId);
            
            // Update party data
            SmartTargetingCache.UpdatePartyData(
                scenario.MemberIds, 
                scenario.HpPercentages, 
                scenario.StatusFlags, 
                (byte)memberCount);
            
            // Test basic functionality
            ValidatePartyDataStorage(scenario);
            ValidateSmartTargeting(scenario);
            ValidateHardTargeting(scenario);
            ValidateEdgeCases(scenario);
        }
        
        private static PartyScenario GenerateRandomPartyScenario(int memberCount, int seed)
        {
            var scenario = new PartyScenario();
            scenario.MemberIds = new uint[memberCount];
            scenario.HpPercentages = new float[memberCount];
            scenario.StatusFlags = new uint[memberCount];
            
            // Generate unique member IDs
            var usedIds = new HashSet<uint>();
            for (int i = 0; i < memberCount; i++)
            {
                uint memberId;
                do
                {
                    memberId = (uint)_random.Next((int)MIN_MEMBER_ID, (int)MAX_MEMBER_ID);
                } while (usedIds.Contains(memberId));
                
                usedIds.Add(memberId);
                scenario.MemberIds[i] = memberId;
                
                // Random HP percentage (10% to 100%)
                scenario.HpPercentages[i] = (float)(_random.NextDouble() * 0.9 + 0.1);
                
                // Random status flags
                scenario.StatusFlags[i] = GenerateRandomStatusFlags(i == 0); // First member is self
            }
            
            return scenario;
        }
        
        private static uint GenerateRandomStatusFlags(bool isSelf)
        {
            uint flags = VALID_TARGET; // Always start with valid target
            
            if (isSelf)
            {
                flags |= SELF;
            }
            
            // 20% chance to be dead (remove alive flag)
            if (_random.NextDouble() < 0.2)
            {
                flags &= ~ALIVE;
            }
            
            // 10% chance to be out of range
            if (_random.NextDouble() < 0.1)
            {
                flags &= ~IN_RANGE;
            }
            
            // 5% chance to be out of LoS
            if (_random.NextDouble() < 0.05)
            {
                flags &= ~IN_LOS;
            }
            
            // Random job assignments
            double jobRoll = _random.NextDouble();
            if (jobRoll < 0.25) flags |= TANK;
            else if (jobRoll < 0.5) flags |= HEALER;
            else flags |= MELEE;
            
            return flags;
        }
        
        private static void ValidatePartyDataStorage(PartyScenario scenario)
        {
            // Validate party count
            if (SmartTargetingCache.PartyCount != scenario.MemberIds.Length)
            {
                throw new Exception($"Party count mismatch: expected {scenario.MemberIds.Length}, got {SmartTargetingCache.PartyCount}");
            }
            
            // Validate member data storage
            for (int i = 0; i < scenario.MemberIds.Length; i++)
            {
                uint expectedId = scenario.MemberIds[i];
                float expectedHp = scenario.HpPercentages[i];
                uint expectedFlags = scenario.StatusFlags[i];
                
                uint actualId = SmartTargetingCache.GetMemberIdByIndex(i);
                float actualHp = SmartTargetingCache.GetMemberHpPercent(expectedId);
                uint actualFlags = SmartTargetingCache.GetMemberStatusFlags(expectedId);
                
                if (actualId != expectedId)
                {
                    throw new Exception($"Member ID mismatch at index {i}: expected {expectedId}, got {actualId}");
                }
                
                if (Math.Abs(actualHp - expectedHp) > 0.001f)
                {
                    throw new Exception($"HP percentage mismatch for member {expectedId}: expected {expectedHp}, got {actualHp}");
                }
                
                if (actualFlags != expectedFlags)
                {
                    throw new Exception($"Status flags mismatch for member {expectedId}: expected {expectedFlags}, got {actualFlags}");
                }
            }
        }
        
        private static void ValidateSmartTargeting(PartyScenario scenario)
        {
            // Test smart targeting with various thresholds
            float[] thresholds = { 0.5f, 0.8f, 0.95f, 1.0f };
            
            foreach (float threshold in thresholds)
            {
                uint target = SmartTargetingCache.GetSmartTarget(threshold);
                
                // Target should either be 0 (no valid targets) or a valid party member
                if (target != 0)
                {
                    bool isValidMember = false;
                    for (int i = 0; i < scenario.MemberIds.Length; i++)
                    {
                        if (scenario.MemberIds[i] == target)
                        {
                            isValidMember = true;
                            
                            // If target is found, validate it meets our criteria:
                            // 1. Should be below threshold OR be self (fallback)
                            // 2. If it's a party member (not self), should be valid target
                            float targetHp = scenario.HpPercentages[i];
                            uint targetFlags = scenario.StatusFlags[i];
                            bool isSelf = (targetFlags & SELF) != 0;
                            bool isValidTarget = (targetFlags & VALID_TARGET) == VALID_TARGET;
                            
                            // If it's not self, it should be a valid target below threshold
                            if (!isSelf && (!isValidTarget || targetHp >= threshold))
                            {
                                // This might be okay if all party members are invalid/above threshold
                                // In that case, system should fallback to self
                                bool allPartyMembersInvalid = true;
                                for (int j = 0; j < scenario.MemberIds.Length; j++)
                                {
                                    bool memberIsSelf = (scenario.StatusFlags[j] & SELF) != 0;
                                    if (!memberIsSelf)
                                    {
                                        bool memberValid = (scenario.StatusFlags[j] & VALID_TARGET) == VALID_TARGET;
                                        bool memberBelowThreshold = scenario.HpPercentages[j] < threshold;
                                        if (memberValid && memberBelowThreshold)
                                        {
                                            allPartyMembersInvalid = false;
                                            break;
                                        }
                                    }
                                }
                                
                                if (!allPartyMembersInvalid && !isSelf)
                                {
                                    throw new Exception($"SmartTarget returned invalid choice {target} with HP {targetHp:F2} for threshold {threshold}. Should target valid party member below threshold.");
                                }
                            }
                            break;
                        }
                    }
                    
                    if (!isValidMember)
                    {
                        throw new Exception($"SmartTarget returned invalid member ID {target} for threshold {threshold}");
                    }
                }
            }
        }
        
        private static void ValidateHardTargeting(PartyScenario scenario)
        {
            // Test hard target override - should ALWAYS work regardless of target status
            if (scenario.MemberIds.Length > 1)
            {
                uint hardTargetId = scenario.MemberIds[1]; // Pick second member as hard target
                
                // Create new scenario with hard target flag set
                var hardTargetScenario = new PartyScenario();
                hardTargetScenario.MemberIds = new uint[scenario.MemberIds.Length];
                hardTargetScenario.HpPercentages = new float[scenario.HpPercentages.Length];
                hardTargetScenario.StatusFlags = new uint[scenario.StatusFlags.Length];
                
                Array.Copy(scenario.MemberIds, hardTargetScenario.MemberIds, scenario.MemberIds.Length);
                Array.Copy(scenario.HpPercentages, hardTargetScenario.HpPercentages, scenario.HpPercentages.Length);
                Array.Copy(scenario.StatusFlags, hardTargetScenario.StatusFlags, scenario.StatusFlags.Length);
                
                // Set hard target flag on second member
                hardTargetScenario.StatusFlags[1] |= (1u << 5); // HardTargetFlag
                
                SmartTargetingCache.UpdatePartyData(
                    hardTargetScenario.MemberIds, 
                    hardTargetScenario.HpPercentages, 
                    hardTargetScenario.StatusFlags, 
                    (byte)hardTargetScenario.MemberIds.Length);
                
                uint result = SmartTargetingCache.GetSmartTarget(0.5f);
                
                // Hard target should ALWAYS be returned (even if dead/invalid) - no validation
                if (result != hardTargetId)
                {
                    throw new Exception($"Hard target failed: expected {hardTargetId}, got {result}. Hard targets should bypass all validation.");
                }
                
                // Test with various thresholds - hard target should always win
                float[] testThresholds = { 0.1f, 0.5f, 0.9f, 1.0f };
                foreach (float threshold in testThresholds)
                {
                    uint thresholdResult = SmartTargetingCache.GetSmartTarget(threshold);
                    if (thresholdResult != hardTargetId)
                    {
                        throw new Exception($"Hard target failed with threshold {threshold}: expected {hardTargetId}, got {thresholdResult}");
                    }
                }
                
                // Hard target is now flag-based - no explicit clearing needed
                // It's managed through flag manipulation in the scenario
            }
        }
        
        private static void ValidateEdgeCases(PartyScenario scenario)
        {
            // Test with invalid member IDs
            uint invalidId = MAX_MEMBER_ID + 1000;
            float invalidHp = SmartTargetingCache.GetMemberHpPercent(invalidId);
            if (invalidHp != 0.0f)
            {
                throw new Exception($"Invalid member ID returned non-zero HP: {invalidHp}");
            }
            
            uint invalidFlags = SmartTargetingCache.GetMemberStatusFlags(invalidId);
            if (invalidFlags != 0)
            {
                throw new Exception($"Invalid member ID returned non-zero flags: {invalidFlags}");
            }
            
            // Test edge case: lowest HP member is invalid (out of LoS/range/dead)
            // System should find next lowest valid member, not just return 0
            if (scenario.MemberIds.Length >= 3)
            {
                // Create a scenario where lowest HP member is dead (0% HP)
                var testScenario = new PartyScenario();
                testScenario.MemberIds = new uint[3] { scenario.MemberIds[0], scenario.MemberIds[1], scenario.MemberIds[2] };
                testScenario.HpPercentages = new float[3] { 1.0f, 0.0f, 0.6f }; // Member 1 dead (0%), member 2 injured (60%)
                testScenario.StatusFlags = new uint[3] 
                { 
                    VALID_TARGET | SELF,           // Self - valid, full HP
                    TANK,                          // Member 1 - dead (0% HP), should be ignored
                    VALID_TARGET | HEALER          // Member 2 - valid, needs healing (60% HP)
                };
                
                SmartTargetingCache.ClearForTesting();
                SmartTargetingCache.UpdatePartyData(
                    testScenario.MemberIds, 
                    testScenario.HpPercentages, 
                    testScenario.StatusFlags, 
                    3);
                
                uint result = SmartTargetingCache.GetSmartTarget(0.95f);
                
                // Should target member 2 (valid with 60% HP), not member 1 (dead with 0% HP)
                if (result != testScenario.MemberIds[2])
                {
                    throw new Exception($"Dead member edge case failed: expected member 2 ({testScenario.MemberIds[2]}) with 60% HP, got {result}. Should ignore dead members (0% HP).");
                }
                
                // Test another case: member with missing LoS
                testScenario.HpPercentages = new float[3] { 1.0f, 0.3f, 0.7f }; // Member 1 critical, member 2 moderate
                testScenario.StatusFlags = new uint[3] 
                { 
                    VALID_TARGET | SELF,                    // Self - valid, full HP  
                    ALIVE | IN_RANGE | TARGETABLE | TANK,  // Member 1 - lowest HP but missing IN_LOS
                    VALID_TARGET | HEALER                   // Member 2 - valid, moderate damage
                };
                
                SmartTargetingCache.UpdatePartyData(
                    testScenario.MemberIds, 
                    testScenario.HpPercentages, 
                    testScenario.StatusFlags, 
                    3);
                
                result = SmartTargetingCache.GetSmartTarget(0.95f);
                
                // Should target member 2 (valid with 70% HP), not member 1 (no LoS with 30% HP)
                if (result != testScenario.MemberIds[2])
                {
                    throw new Exception($"Line of Sight edge case failed: expected member 2 ({testScenario.MemberIds[2]}) with LoS, got {result}. Should ignore members without LoS.");
                }
            }
        }
        
        private class PartyScenario
        {
            public uint[] MemberIds { get; set; } = Array.Empty<uint>();
            public float[] HpPercentages { get; set; } = Array.Empty<float>();
            public uint[] StatusFlags { get; set; } = Array.Empty<uint>();
        }
    }
}
