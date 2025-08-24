using Xunit;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// XUnit wrapper for randomized SmartTargeting tests.
    /// </summary>
    public class SmartTargetingStressTests : IDisposable
    {
        public SmartTargetingStressTests()
        {
            // Ensure clean state for each test
            SmartTargetingCache.ClearForTesting();
        }
        
        public void Dispose()
        {
            // Clear all cached state to ensure test isolation
            SmartTargetingCache.ClearForTesting();
        }
        
        [Fact]
        public void SmartTargeting_RandomizedSimulations_ShouldPassAllScenarios()
        {
            // Run 100 simulations for CI/CD (adjust via environment variable)
            int simulationCount = int.TryParse(System.Environment.GetEnvironmentVariable("SMARTTARGET_SIMULATIONS"), out int count) 
                ? count 
                : 100;
            
            SmartTargetingRandomizedTests.RunSmartTargetingSimulations(simulationCount);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void SmartTargeting_ExtensiveSimulations_1000Runs()
        {
            SmartTargetingRandomizedTests.RunSmartTargetingSimulations(1000);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void SmartTargeting_ExtensiveSimulations_10000Runs()
        {
            SmartTargetingRandomizedTests.RunSmartTargetingSimulations(10000);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void SmartTargeting_MassiveSimulations_10MillionRuns()
        {
            SmartTargetingRandomizedTests.RunSmartTargetingSimulations(10_000_000);
        }
        
        /// <summary>
        /// Run randomized simulations with custom count via environment variable.
        /// Usage: SMARTTARGET_BENCH_COUNT=10000000 dotnet test --filter "SmartTargeting_CustomBenchmark"
        /// </summary>
        [Fact(Skip = "Manual performance testing only")]
        public void SmartTargeting_CustomBenchmark()
        {
            int simulationCount = int.TryParse(System.Environment.GetEnvironmentVariable("SMARTTARGET_BENCH_COUNT"), out int count) 
                ? count 
                : 1_000_000; // Default to 1 million if not specified
            
            SmartTargetingRandomizedTests.RunSmartTargetingSimulations(simulationCount);
        }
    }
}
