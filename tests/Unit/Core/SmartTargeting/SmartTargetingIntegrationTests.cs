using System;
using Xunit;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// Integration tests for SmartTargeting with GameStateCache stub.
    /// Uses GameStateCacheStub to avoid Dalamud dependencies in tests.
    /// Validates the facade methods and cache integration patterns.
    /// </summary>
    public class SmartTargetingIntegrationTests : IDisposable
    {
        private const uint SELF_ID = 1000;
        private const uint TANK_ID = 2000;
        private const uint HEALER_ID = 3000;
        private const uint DPS_ID = 4000;
        
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
        
        public void Dispose()
        {
            ClearPartyData();
        }
        
        private void ClearPartyData()
        {
            // Clear all cached state to ensure test isolation
            SmartTargetingCache.ClearForTesting();
        }
        
        #region GameStateCache Integration Tests
        
        [Fact]
        public void GameStateCache_GetSmartTarget_ShouldCallSmartTargetingCache()
        {
            // Arrange
            SetupBasicParty();
            
            // Act
            uint directResult = SmartTargetingCache.GetSmartTarget(0.95f);
            uint cacheResult = GameStateCacheStub.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(directResult, cacheResult);
        }
        
        [Fact]
        public void GameStateCache_SetSmartTargetHardTarget_ShouldUpdateHardTarget()
        {
            // Arrange
            SetupBasicParty();
            
            // Act
            GameStateCacheStub.SetSmartTargetHardTarget(TANK_ID);
            uint result = GameStateCacheStub.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(TANK_ID, result); // Should return hard target
        }
        
        [Fact]
        public void GameStateCache_IsValidSmartTarget_ShouldValidateCorrectly()
        {
            // Arrange
            SetupBasicParty();
            
            // Act & Assert
            Assert.True(GameStateCacheStub.IsValidSmartTarget(TANK_ID));
            Assert.True(GameStateCacheStub.IsValidSmartTarget(HEALER_ID));
            Assert.False(GameStateCacheStub.IsValidSmartTarget(999999)); // Invalid ID
        }
        
        #endregion
        
        #region Cache Consistency Tests
        
        [Fact]
        public void CacheConsistency_MultipleCallsSameFrame_ShouldReturnSameResult()
        {
            // Arrange
            SetupBasicParty();
            
            // Act - Multiple calls in quick succession
            uint result1 = SmartTargetingCache.GetSmartTarget(0.95f);
            uint result2 = SmartTargetingCache.GetSmartTarget(0.95f);
            uint result3 = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(result1, result2);
            Assert.Equal(result2, result3);
        }
        
        [Fact]
        public void CacheUpdate_NewPartyData_ShouldReflectChanges()
        {
            // Arrange
            SetupBasicParty();
            uint initialResult = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Act - Update party data with different HP values
            Span<uint> newMemberIds = stackalloc uint[3] { SELF_ID, TANK_ID, HEALER_ID };
            Span<float> newHpPercentages = stackalloc float[3] { 1.0f, 0.3f, 0.9f }; // Tank now lower
            Span<uint> newStatusFlags = stackalloc uint[3] 
            { 
                VALID_TARGET | SELF,
                VALID_TARGET | TANK,
                VALID_TARGET | HEALER
            };
            
            SmartTargetingCache.UpdatePartyData(newMemberIds, newHpPercentages, newStatusFlags, 3);
            uint updatedResult = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(TANK_ID, updatedResult); // Should now target tank with 30% HP
            Assert.NotEqual(initialResult, updatedResult); // Results should be different
        }
        
        #endregion
        
        #region Threshold Behavior Tests
        
        [Fact]
        public void ThresholdBehavior_DifferentThresholds_ShouldAffectTargeting()
        {
            // Arrange - Party with mixed HP levels
            Span<uint> memberIds = stackalloc uint[4] { SELF_ID, TANK_ID, HEALER_ID, DPS_ID };
            Span<float> hpPercentages = stackalloc float[4] { 1.0f, 0.92f, 0.88f, 0.85f }; // All relatively high HP
            Span<uint> statusFlags = stackalloc uint[4] 
            { 
                VALID_TARGET | SELF,
                VALID_TARGET | TANK,
                VALID_TARGET | HEALER,
                VALID_TARGET | MELEE
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 4);
            
            // Act
            uint resultLowThreshold = SmartTargetingCache.GetSmartTarget(0.8f);   // Low threshold
            uint resultHighThreshold = SmartTargetingCache.GetSmartTarget(0.95f); // High threshold
            
            // Assert
            Assert.Equal(SELF_ID, resultLowThreshold);  // With low threshold, everyone is "healthy"
            Assert.Equal(DPS_ID, resultHighThreshold); // With high threshold, target lowest HP
        }
        
        [Fact]
        public void ThresholdBehavior_NeedsHealing_ShouldRespectThreshold()
        {
            // Arrange
            SetupBasicParty();
            
            // Act & Assert - Test different thresholds
            Assert.True(SmartTargetingCache.NeedsHealing(HEALER_ID, 0.8f));  // 60% < 80%
            Assert.False(SmartTargetingCache.NeedsHealing(HEALER_ID, 0.5f)); // 60% > 50%
            Assert.True(SmartTargetingCache.NeedsHealing(TANK_ID, 0.9f));    // 80% < 90%
            Assert.False(SmartTargetingCache.NeedsHealing(TANK_ID, 0.7f));   // 80% > 70%
        }
        
        #endregion
        
        #region Hard Target Override Tests
        
        [Fact]
        public void HardTargetOverride_ValidTarget_ShouldOverrideLowestHp()
        {
            // Arrange - Setup party with tank as hard target
            Span<uint> memberIds = stackalloc uint[3] { SELF_ID, TANK_ID, HEALER_ID };
            Span<float> hpPercentages = stackalloc float[3] { 1.0f, 0.8f, 0.6f };
            Span<uint> statusFlags = stackalloc uint[3] 
            { 
                VALID_TARGET | SELF,
                VALID_TARGET | TANK | (1u << 5), // Tank with HARD_TARGET flag
                VALID_TARGET | HEALER
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 3);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(TANK_ID, result); // Should return tank despite healer being lower (60%)
        }
        
        [Fact]
        public void HardTargetOverride_InvalidTarget_ShouldFallbackToLowestHp()
        {
            // Arrange - Setup party with no hard target (simulating invalid target scenario)
            Span<uint> memberIds = stackalloc uint[3] { SELF_ID, TANK_ID, HEALER_ID };
            Span<float> hpPercentages = stackalloc float[3] { 1.0f, 0.8f, 0.6f };
            Span<uint> statusFlags = stackalloc uint[3] 
            { 
                VALID_TARGET | SELF,
                VALID_TARGET | TANK, // Tank without hard target flag
                VALID_TARGET | HEALER
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 3);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(HEALER_ID, result); // Should fallback to healer with lowest HP (60%)
        }
        
        [Fact]
        public void HardTargetOverride_TargetDead_ShouldRespectManualTarget()
        {
            // Arrange - Set up party where hard target is dead
            Span<uint> memberIds = stackalloc uint[3] { SELF_ID, TANK_ID, HEALER_ID };
            Span<float> hpPercentages = stackalloc float[3] { 1.0f, 0.0f, 0.6f }; // Tank dead
            Span<uint> statusFlags = stackalloc uint[3] 
            { 
                VALID_TARGET | SELF,
                TANK | (1u << 5) | ALLY, // Dead tank with HARD_TARGET flag and ALLY flag - missing ALIVE flag
                VALID_TARGET | HEALER
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 3);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(TANK_ID, result); // Should respect manual targeting even if dead
        }
        
        [Fact]
        public void HardTargetOverride_EnemyTarget_ShouldIgnoreAndFallback()
        {
            // Arrange - Set up party where hard target is an enemy (no ALLY flag)
            Span<uint> memberIds = stackalloc uint[3] { SELF_ID, TANK_ID, HEALER_ID };
            Span<float> hpPercentages = stackalloc float[3] { 1.0f, 0.3f, 0.6f }; // Tank low HP but enemy
            Span<uint> statusFlags = stackalloc uint[3] 
            { 
                VALID_TARGET | SELF,
                ALIVE | IN_RANGE | IN_LOS | TARGETABLE | TANK | (1u << 5), // Enemy tank with HARD_TARGET flag but NO ALLY flag
                VALID_TARGET | HEALER
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 3);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(HEALER_ID, result); // Should ignore enemy hard target and fallback to healer
        }

        #endregion
        
        #region Data Validation Tests
        
        [Fact]
        public void DataValidation_InvalidHpValues_ShouldHandleGracefully()
        {
            // Arrange - Test with edge case HP values
            Span<uint> memberIds = stackalloc uint[4] { SELF_ID, TANK_ID, HEALER_ID, DPS_ID };
            Span<float> hpPercentages = stackalloc float[4] { -0.1f, 1.1f, float.NaN, 0.5f }; // Invalid values
            Span<uint> statusFlags = stackalloc uint[4] 
            { 
                VALID_TARGET | SELF,
                VALID_TARGET | TANK,
                VALID_TARGET | HEALER,
                VALID_TARGET | MELEE
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 4);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert - Should not crash and should return a valid target
            Assert.True(result == SELF_ID || result == TANK_ID || result == HEALER_ID || result == DPS_ID);
        }
        
        [Fact]
        public void DataValidation_ZeroMemberCount_ShouldReturnZero()
        {
            // Arrange
            ClearPartyData();
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            
            // Assert
            Assert.Equal(0u, result);
        }
        
        [Fact]
        public void DataValidation_MemberCountBounds_ShouldHandleCorrectly()
        {
            // Arrange - Test with maximum party size
            Span<uint> memberIds = stackalloc uint[8] 
            { 
                1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000
            };
            Span<float> hpPercentages = stackalloc float[8] 
            { 
                1.0f, 0.9f, 0.8f, 0.7f, 0.6f, 0.5f, 0.4f, 0.3f
            };
            Span<uint> statusFlags = stackalloc uint[8];
            for (int i = 0; i < 8; i++)
            {
                statusFlags[i] = VALID_TARGET;
            }
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 8);
            
            // Act
            uint result = SmartTargetingCache.GetSmartTarget(0.95f);
            byte partyCount = SmartTargetingCache.PartyCount;
            
            // Assert
            Assert.Equal(8, partyCount);
            Assert.Equal(8000u, result); // Should target member with lowest HP (30%)
        }
        
        #endregion
        
        #region Helper Methods
        
        private void SetupBasicParty()
        {
            Span<uint> memberIds = stackalloc uint[3] { SELF_ID, TANK_ID, HEALER_ID };
            Span<float> hpPercentages = stackalloc float[3] { 1.0f, 0.8f, 0.6f }; // Healer lowest
            Span<uint> statusFlags = stackalloc uint[3] 
            { 
                VALID_TARGET | SELF,
                VALID_TARGET | TANK,
                VALID_TARGET | HEALER
            };
            
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 3);
        }
        
        #endregion
    }
}
