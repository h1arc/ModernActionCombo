using System;
using Xunit;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// Focused stress testing for ActionResolver without cache interference.
    /// Set ACTIONRES_FOCUSED_BENCH_COUNT environment variable to control test intensity.
    /// 
    /// Examples:
    /// - CI/CD: ACTIONRES_FOCUSED_BENCH_COUNT=100 (fast validation)
    /// - Development: ACTIONRES_FOCUSED_BENCH_COUNT=1000 (moderate testing)
    /// - Performance validation: ACTIONRES_FOCUSED_BENCH_COUNT=10000 (thorough testing)
    /// - Stress testing: ACTIONRES_FOCUSED_BENCH_COUNT=100000 (extreme validation)
    /// </summary>
    public class ActionResolverFocusedStressTests
    {
        private const int DEFAULT_SIMULATION_COUNT = 100;
        private const string ENV_VAR_NAME = "ACTIONRES_FOCUSED_BENCH_COUNT";

        [Fact]
        public void ActionResolver_FocusedStressTest_ShouldHandlePureResolverWorkloads()
        {
            var simulationCountStr = Environment.GetEnvironmentVariable(ENV_VAR_NAME);
            var simulationCount = DEFAULT_SIMULATION_COUNT;

            if (!string.IsNullOrEmpty(simulationCountStr) && int.TryParse(simulationCountStr, out var envCount))
            {
                simulationCount = envCount;
            }

            Console.WriteLine($"Running ActionResolver focused stress test with {simulationCount} simulations (set {ENV_VAR_NAME} to override)");

            // Execute the focused randomized testing without cache interference
            ActionResolverFocusedRandomizedTests.RunActionResolverFocusedSimulations(simulationCount);
        }
    }
}
