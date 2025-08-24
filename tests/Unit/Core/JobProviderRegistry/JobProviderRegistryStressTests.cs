using Xunit;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// XUnit wrapper for randomized JobProviderRegistry tests.
    /// </summary>
    public class JobProviderRegistryStressTests : IDisposable
    {
        public JobProviderRegistryStressTests()
        {
            // Ensure clean state for each test
        }
        
        public void Dispose()
        {
            // Clean up after tests
        }
        
        [Fact]
        public void JobProviderRegistry_RandomizedSimulations_ShouldPassAllScenarios()
        {
            // Run 100 simulations for CI/CD (adjust via environment variable)
            int simulationCount = int.TryParse(System.Environment.GetEnvironmentVariable("JOBREGISTRY_SIMULATIONS"), out int count) 
                ? count 
                : 100;
            
            JobProviderRegistryFocusedRandomizedTests.RunJobProviderRegistryFocusedSimulations(simulationCount);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void JobProviderRegistry_ExtensiveSimulations_1000Runs()
        {
            JobProviderRegistryFocusedRandomizedTests.RunJobProviderRegistryFocusedSimulations(1000);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void JobProviderRegistry_ExtensiveSimulations_10000Runs()
        {
            JobProviderRegistryFocusedRandomizedTests.RunJobProviderRegistryFocusedSimulations(10000);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void JobProviderRegistry_MassiveSimulations_100000Runs()
        {
            JobProviderRegistryFocusedRandomizedTests.RunJobProviderRegistryFocusedSimulations(100_000);
        }
        
        /// <summary>
        /// Run randomized simulations with custom count via environment variable.
        /// Usage: JOBREGISTRY_BENCH_COUNT=50000 dotnet test --filter "JobProviderRegistry_CustomBenchmark"
        /// </summary>
        [Fact(Skip = "Manual performance testing only")]
        public void JobProviderRegistry_CustomBenchmark()
        {
            int simulationCount = int.TryParse(System.Environment.GetEnvironmentVariable("JOBREGISTRY_BENCH_COUNT"), out int count) 
                ? count 
                : 10_000; // Default to 10K if not specified
            
            JobProviderRegistryFocusedRandomizedTests.RunJobProviderRegistryFocusedSimulations(simulationCount);
        }
    }
}
