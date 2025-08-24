using Xunit;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// XUnit wrapper for randomized ActionInterceptor tests.
    /// </summary>
    public class ActionInterceptorStressTests : IDisposable
    {
        public ActionInterceptorStressTests()
        {
            // Ensure clean state for each test
        }
        
        public void Dispose()
        {
            // Clean up after tests
        }
        
        [Fact]
        public void ActionInterceptor_RandomizedSimulations_ShouldPassAllScenarios()
        {
            // Run 100 simulations for CI/CD (adjust via environment variable)
            int simulationCount = int.TryParse(System.Environment.GetEnvironmentVariable("ACTIONINT_SIMULATIONS"), out int count) 
                ? count 
                : 100;
            
            ActionInterceptorRandomizedTests.RunActionInterceptorSimulations(simulationCount);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void ActionInterceptor_ExtensiveSimulations_1000Runs()
        {
            ActionInterceptorRandomizedTests.RunActionInterceptorSimulations(1000);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void ActionInterceptor_ExtensiveSimulations_10000Runs()
        {
            ActionInterceptorRandomizedTests.RunActionInterceptorSimulations(10000);
        }
        
        [Fact(Skip = "Manual performance testing only")]
        public void ActionInterceptor_MassiveSimulations_100000Runs()
        {
            ActionInterceptorRandomizedTests.RunActionInterceptorSimulations(100_000);
        }
        
        /// <summary>
        /// Run randomized simulations with custom count via environment variable.
        /// Usage: ACTIONINT_BENCH_COUNT=50000 dotnet test --filter "ActionInterceptor_CustomBenchmark"
        /// </summary>
        [Fact(Skip = "Manual performance testing only")]
        public void ActionInterceptor_CustomBenchmark()
        {
            int simulationCount = int.TryParse(System.Environment.GetEnvironmentVariable("ACTIONINT_BENCH_COUNT"), out int count) 
                ? count 
                : 10_000; // Default to 10K if not specified
            
            ActionInterceptorRandomizedTests.RunActionInterceptorSimulations(simulationCount);
        }
    }
}
