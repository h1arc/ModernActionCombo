using Xunit;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// XUnit wrapper for randomized OGCDResolver tests.
    /// </summary>
    public class OGCDResolverStressTests : IDisposable
    {
        public OGCDResolverStressTests()
        {
            // Ensure clean state for each test
        }
        
        public void Dispose()
        {
            // Clean up after tests
        }
        
        [Fact]
        public void OGCDResolver_RandomizedSimulations_ShouldPassAllScenarios()
        {
            // Run 100 simulations for CI/CD (adjust via environment variable)
            int simulationCount = int.TryParse(System.Environment.GetEnvironmentVariable("OGCD_SIMULATIONS"), out int count) 
                ? count 
                : 100;
            
            OGCDResolverRandomizedTests.RunOGCDResolverSimulations(simulationCount);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void OGCDResolver_ExtensiveSimulations_1000Runs()
        {
            OGCDResolverRandomizedTests.RunOGCDResolverSimulations(1000);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void OGCDResolver_ExtensiveSimulations_10000Runs()
        {
            OGCDResolverRandomizedTests.RunOGCDResolverSimulations(10000);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void OGCDResolver_MassiveSimulations_1MillionRuns()
        {
            OGCDResolverRandomizedTests.RunOGCDResolverSimulations(1_000_000);
        }
        
        /// <summary>
        /// Run randomized simulations with custom count via environment variable.
        /// Usage: OGCD_BENCH_COUNT=100000 dotnet test --filter "OGCDResolver_CustomBenchmark"
        /// </summary>
        [Fact(Skip = "Manual performance testing only")]
        public void OGCDResolver_CustomBenchmark()
        {
            int simulationCount = int.TryParse(System.Environment.GetEnvironmentVariable("OGCD_BENCH_COUNT"), out int count) 
                ? count 
                : 10_000; // Default to 10K if not specified
            
            OGCDResolverRandomizedTests.RunOGCDResolverSimulations(simulationCount);
        }
    }
}
