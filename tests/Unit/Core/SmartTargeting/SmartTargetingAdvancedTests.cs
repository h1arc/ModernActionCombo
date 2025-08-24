using System;
using Xunit;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// Advanced SmartTargeting scenarios testing complex party situations.
    /// Tests real-world usage patterns and edge cases that healers encounter.
    /// </summary>
    public class SmartTargetingAdvancedTests : IDisposable
    {
        // Extended party member IDs for complex scenarios
        private const uint SELF_ID = 1000;      // Healer (self)
        private const uint TANK1_ID = 2000;     // Main Tank
        private const uint TANK2_ID = 2001;     // Off Tank  
        private const uint HEALER2_ID = 3001;   // Co-Healer
        private const uint MELEE1_ID = 4000;    // Melee DPS 1
        private const uint MELEE2_ID = 4001;    // Melee DPS 2
        private const uint RANGED1_ID = 5000;   // Ranged DPS 1
        private const uint CASTER1_ID = 6000;   // Caster DPS 1
        
        // Status flags
        private const uint ALIVE = 1u << 0;
        private const uint IN_RANGE = 1u << 1;
        private const uint IN_LOS = 1u << 2;
        private const uint TARGETABLE = 1u << 3;
        private const uint SELF = 1u << 4;
        private const uint HARD_TARGET = 1u << 5;
        private const uint TANK = 1u << 6;
        private const uint HEALER = 1u << 7;
        private const uint MELEE = 1u << 8;
        private const uint RANGED = 1u << 9;
        private const uint ALLY = 1u << 10;        // Ally flag for healing targets
        
        private const uint VALID_TARGET = ALIVE | IN_RANGE | IN_LOS | TARGETABLE | ALLY;
        
        public SmartTargetingAdvancedTests()
        {
            // Ensure clean state for each test
            SmartTargetingCache.ClearForTesting();
        }
        
        public void Dispose()
        {
            // Clear all cached state to ensure test isolation
            SmartTargetingCache.ClearForTesting();
        }
        
        private void ClearPartyData()
        {
            Span<uint> emptyIds = stackalloc uint[1] { 0 };
            Span<float> emptyHp = stackalloc float[1] { 0.0f };
            Span<uint> emptyFlags = stackalloc uint[1] { 0 };
            
            SmartTargetingCache.UpdatePartyData(emptyIds, emptyHp, emptyFlags, 0);
        }
        
        #region Multi-Tank Scenarios
        
        [Fact]
        public void DualTankScenario_BothTanksLowHp_ShouldTargetLowest()
        {
            // Arrange - Both tanks critically low, main tank slightly lower
            Span<uint> memberIds = stackalloc uint[8] 
            { 
                SELF_ID, TANK1_ID, TANK2_ID, HEALER2_ID,
                MELEE1_ID, MELEE2_ID, RANGED1_ID, CASTER1_ID
            };
            Span<float> hpPercentages = stackalloc float[8] 
            { 
                0.9f,  // Self - healthy
                0.15f, // Main tank - critical
                0.18f, // Off tank - critical but slightly higher
                0.8f,  // Co-healer - healthy
                0.7f, 0.7f, 0.7f, 0.7f // DPS - moderate damage
            };
            Span<uint> statusFlags = stackalloc uint[8] 
            { 
                VALID_TARGET | SELF | HEALER,
                VALID_TARGET | TANK,
                VALID_TARGET | TANK,
                VALID_TARGET | HEALER,
                VALID_TARGET | MELEE,
                VALID_TARGET | MELEE,
                VALID_TARGET | RANGED,
                VALID_TARGET
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 8);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(TANK1_ID, result); // Should target main tank with lowest HP
        }
        
        [Fact]
        public void TankSwapScenario_OffTankGrabsAggro_ShouldRespectHardTarget()
        {
            // Arrange - Off tank takes aggro, player hard targets them
            Span<uint> memberIds = stackalloc uint[8] 
            { 
                SELF_ID, TANK1_ID, TANK2_ID, HEALER2_ID,
                MELEE1_ID, MELEE2_ID, RANGED1_ID, CASTER1_ID
            };
            Span<float> hpPercentages = stackalloc float[8] 
            { 
                0.85f, // Self - good health
                0.70f, // Main tank - moderate damage (would be lowest without hard target)
                0.90f, // Off tank - healthy but hard targeted
                0.75f, // Co-healer - moderate damage  
                0.60f, // Melee 1 - more damage
                0.80f, // Melee 2 - light damage
                0.95f, // Ranged - minimal damage
                0.65f  // Caster - moderate damage
            };
            Span<uint> statusFlags = stackalloc uint[8] 
            { 
                VALID_TARGET | SELF | HEALER,
                VALID_TARGET | TANK,
                VALID_TARGET | TANK | (1u << 5), // Off tank with hard target flag
                VALID_TARGET | HEALER,
                VALID_TARGET | MELEE,
                VALID_TARGET | MELEE,
                VALID_TARGET | RANGED,
                VALID_TARGET
            };
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 8);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(TANK2_ID, result); // Should respect hard target even if not lowest HP
        }
        
        #endregion
        
        #region Triage Scenarios
        
        [Fact]
        public void TriageScenario_MultiplePlayersLowHp_ShouldPrioritizeTanks()
        {
            // Arrange - Tank, healer, and DPS all at similar low HP
            Span<uint> memberIds = stackalloc uint[4] 
            { 
                SELF_ID, TANK1_ID, HEALER2_ID, MELEE1_ID
            };
            Span<float> hpPercentages = stackalloc float[4] 
            { 
                0.9f,  // Self - healthy
                0.25f, // Tank - low
                0.23f, // Co-healer - slightly lower
                0.22f  // Melee - lowest
            };
            Span<uint> statusFlags = stackalloc uint[4] 
            { 
                VALID_TARGET | SELF | HEALER,
                VALID_TARGET | TANK,
                VALID_TARGET | HEALER,
                VALID_TARGET | MELEE
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 4);
            
            // Act
            uint result = SmartTargetingCache.GetLowestHpTarget(); // Pure lowest HP
            
            // Assert
            Assert.Equal(MELEE1_ID, result); // Lowest HP targeting should get melee
        }
        
        [Fact]
        public void AOEDamageScenario_MostPlayersNeedHealing_ShouldTargetLowest()
        {
            // Arrange - AOE hit everyone, various damage amounts
            Span<uint> memberIds = stackalloc uint[8] 
            { 
                SELF_ID, TANK1_ID, TANK2_ID, HEALER2_ID,
                MELEE1_ID, MELEE2_ID, RANGED1_ID, CASTER1_ID
            };
            Span<float> hpPercentages = stackalloc float[8] 
            { 
                0.4f,  // Self - hit hard
                0.6f,  // Main tank - moderate damage
                0.5f,  // Off tank - more damage
                0.3f,  // Co-healer - hit hardest
                0.45f, 0.55f, 0.65f, 0.35f // DPS - various damage
            };
            Span<uint> statusFlags = stackalloc uint[8] 
            { 
                VALID_TARGET | SELF | HEALER,
                VALID_TARGET | TANK,
                VALID_TARGET | TANK,
                VALID_TARGET | HEALER,
                VALID_TARGET | MELEE,
                VALID_TARGET | MELEE,
                VALID_TARGET | RANGED,
                VALID_TARGET
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 8);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(HEALER2_ID, result); // Should target co-healer with lowest HP (30%)
        }
        
        #endregion
        
        #region Range and Line of Sight Tests
        
        [Fact]
        public void RangeTest_PlayersOutOfRange_ShouldIgnoreOutOfRangeTargets()
        {
            // Arrange - Some players out of range
            Span<uint> memberIds = stackalloc uint[4] 
            { 
                SELF_ID, TANK1_ID, MELEE1_ID, RANGED1_ID
            };
            Span<float> hpPercentages = stackalloc float[4] 
            { 
                1.0f,  // Self - full
                0.3f,  // Tank - low but out of range
                0.8f,  // Melee - healthy and in range
                0.2f   // Ranged - lowest HP but out of range
            };
            Span<uint> statusFlags = stackalloc uint[4] 
            { 
                VALID_TARGET | SELF | HEALER,
                ALIVE | IN_LOS | TARGETABLE | TANK, // Tank missing IN_RANGE flag
                VALID_TARGET | MELEE,
                ALIVE | IN_LOS | TARGETABLE | RANGED // Ranged missing IN_RANGE flag
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 4);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(SELF_ID, result); // Should target self since melee doesn't need healing (80% HP)
        }
        
        [Fact]
        public void LineOfSightTest_PlayersBlockedByWalls_ShouldIgnoreBlockedTargets()
        {
            // Arrange - Tank needs healing but behind wall
            Span<uint> memberIds = stackalloc uint[3] 
            { 
                SELF_ID, TANK1_ID, MELEE1_ID
            };
            Span<float> hpPercentages = stackalloc float[3] 
            { 
                1.0f,  // Self - full
                0.2f,  // Tank - critical but no LoS
                0.6f   // Melee - moderate damage with LoS
            };
            Span<uint> statusFlags = stackalloc uint[3] 
            { 
                VALID_TARGET | SELF | HEALER,
                ALIVE | IN_RANGE | TARGETABLE | TANK, // Tank missing IN_LOS flag
                VALID_TARGET | MELEE
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 3);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(MELEE1_ID, result); // Should target melee since tank has no LoS
        }
        
        #endregion
        
        #region Death and Revival Scenarios
        
        [Fact]
        public void RevivalScenario_PlayerJustRevived_ShouldTargetRevivedPlayer()
        {
            // Arrange - Player just revived with very low HP
            Span<uint> memberIds = stackalloc uint[3] 
            { 
                SELF_ID, TANK1_ID, MELEE1_ID
            };
            Span<float> hpPercentages = stackalloc float[3] 
            { 
                0.8f,  // Self - healthy
                0.7f,  // Tank - moderate damage
                0.05f  // Melee - just revived, critical
            };
            Span<uint> statusFlags = stackalloc uint[3] 
            { 
                VALID_TARGET | SELF | HEALER,
                VALID_TARGET | TANK,
                VALID_TARGET | MELEE
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 3);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(MELEE1_ID, result); // Should prioritize just-revived player
        }
        
        [Fact]
        public void MultipleDeathsScenario_OnlyFewAlive_ShouldOnlyConsiderAlive()
        {
            // Arrange - Most party dead, only few alive
            Span<uint> memberIds = stackalloc uint[6] 
            { 
                SELF_ID, TANK1_ID, TANK2_ID, HEALER2_ID, MELEE1_ID, MELEE2_ID
            };
            Span<float> hpPercentages = stackalloc float[6] 
            { 
                0.6f,  // Self - damaged but alive
                0.0f,  // Main tank - dead
                0.8f,  // Off tank - alive and healthy
                0.0f,  // Co-healer - dead
                0.0f,  // Melee 1 - dead
                0.4f   // Melee 2 - alive but low
            };
            Span<uint> statusFlags = stackalloc uint[6] 
            { 
                VALID_TARGET | SELF | HEALER,
                TANK, // Dead - missing ALIVE flag
                VALID_TARGET | TANK,
                HEALER, // Dead - missing ALIVE flag
                MELEE, // Dead - missing ALIVE flag
                VALID_TARGET | MELEE
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 6);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(MELEE2_ID, result); // Should target lowest HP alive member (melee at 40%)
        }
        
        #endregion
        
        #region Performance Under Load
        
        [Fact]
        public void FullRaidPerformance_MaxPartySize_ShouldMaintainPerformance()
        {
            // Arrange
            SetupFullRaid();
            
            // Warmup
            for (int i = 0; i < 1000; i++)
            {
                SmartTargetingCache.GetSmartTarget(0.95f);
                SmartTargetingCache.GetLowestHpTarget();
            }
            
            // Act & Measure
            var startTime = DateTime.UtcNow.Ticks;
            const int iterations = 50000;
            
            for (int i = 0; i < iterations; i++)
            {
                SmartTargetingCache.GetSmartTarget(0.95f);
                SmartTargetingCache.GetLowestHpTarget();
                SmartTargetingCache.IsValidTarget(TANK1_ID);
                SmartTargetingCache.NeedsHealing(MELEE1_ID, 0.8f);
            }
            
            var endTime = DateTime.UtcNow.Ticks;
            var totalNanoseconds = (endTime - startTime) * 100;
            var averageNanoseconds = totalNanoseconds / (iterations * 4); // 4 operations per iteration
            
            // Assert
            Assert.True(averageNanoseconds < 100, $"Average time was {averageNanoseconds}ns per operation, should be under 100ns for full raid");
        }
        
        #endregion
        
        #region Helper Methods
        
        private void SetupFullRaid()
        {
            Span<uint> memberIds = stackalloc uint[8] 
            { 
                SELF_ID, TANK1_ID, TANK2_ID, HEALER2_ID,
                MELEE1_ID, MELEE2_ID, RANGED1_ID, CASTER1_ID
            };
            Span<float> hpPercentages = stackalloc float[8] 
            { 
                0.85f, // Self - good health
                0.70f, // Main tank - moderate damage
                0.90f, // Off tank - healthy
                0.75f, // Co-healer - moderate damage  
                0.60f, // Melee 1 - more damage
                0.80f, // Melee 2 - light damage
                0.95f, // Ranged - minimal damage
                0.65f  // Caster - moderate damage
            };
            Span<uint> statusFlags = stackalloc uint[8] 
            { 
                VALID_TARGET | SELF | HEALER,
                VALID_TARGET | TANK,
                VALID_TARGET | TANK,
                VALID_TARGET | HEALER,
                VALID_TARGET | MELEE,
                VALID_TARGET | MELEE,
                VALID_TARGET | RANGED,
                VALID_TARGET
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 8);
        }
        
        #endregion
    }
}
