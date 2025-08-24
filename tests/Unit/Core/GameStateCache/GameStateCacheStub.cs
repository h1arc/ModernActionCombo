using System;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// Test stub for GameStateCache that doesn't depend on Dalamud.
    /// Provides the same API as GameStateCache but delegates to SmartTargetingCache directly.
    /// </summary>
    public static class GameStateCacheStub
    {
        /// <summary>
        /// Gets smart target using SmartTargetingCache directly.
        /// This stub bypasses Dalamud dependencies for testing.
        /// </summary>
        public static uint GetSmartTarget(float hpThreshold = 1.0f)
        {
            return SmartTargetingCache.GetSmartTarget(hpThreshold);
        }

        /// <summary>
        /// Sets hard target using SmartTargetingCache directly.
        /// This stub bypasses Dalamud dependencies for testing.
        /// </summary>
        public static void SetSmartTargetHardTarget(uint memberId)
    {
        // No-op: Hard targets are now detected automatically from status flags
        // The game will set the HardTargetFlag in the status when calling UpdateSmartTargetData
    }

        /// <summary>
        /// Validates if a member ID is a valid smart target.
        /// This stub uses SmartTargetingCache validation directly.
        /// </summary>
        public static bool IsValidSmartTarget(uint memberId)
        {
            return SmartTargetingCache.IsValidTarget(memberId);
        }
    }
}
