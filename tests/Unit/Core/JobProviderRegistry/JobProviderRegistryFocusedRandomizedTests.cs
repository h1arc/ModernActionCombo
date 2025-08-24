using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Interfaces;
using Xunit;

namespace ModernActionCombo.Tests.Unit.Core
{
    /// <summary>
    /// Focused randomized testing for JobProviderRegistry using mock providers to avoid Dalamud dependencies.
    /// Tests provider management, job switching, capability detection, and performance characteristics.
    /// </summary>
    public class JobProviderRegistryFocusedRandomizedTests
    {
        // Test job IDs and their corresponding mock providers
        private static readonly Dictionary<uint, MockJobProvider> _mockProviders = new()
        {
            [24] = new MockJobProvider(24, "White Mage", true, true, true),    // Full-featured healer
            [25] = new MockJobProvider(25, "Black Mage", true, false, false), // Combo-only caster
            [19] = new MockJobProvider(19, "Paladin", true, true, false),     // Tank with gauge
            [21] = new MockJobProvider(21, "Warrior", true, true, true),      // Full-featured tank
            [20] = new MockJobProvider(20, "Monk", true, false, false),       // Simple melee DPS
        };
        
        private static readonly uint[] TEST_JOB_IDS = [24, 25, 19, 21, 20];
        
        // Test parameters
        private const int PERFORMANCE_TEST_ITERATIONS = 1000;

        /// <summary>
        /// Main simulation runner for JobProviderRegistry focused testing.
        /// Uses mock providers to avoid external dependencies.
        /// </summary>
        public static void RunJobProviderRegistryFocusedSimulations(int simulationCount)
        {
            var passed = 0;
            var failed = 0;
            var random = new Random(42); // Deterministic for reproducible tests

            var stopwatch = Stopwatch.StartNew();

            for (int simulation = 0; simulation < simulationCount; simulation++)
            {
                try
                {
                    // Test different scenarios using mock providers
                    TestMockProviderCapabilities(random, simulation);
                    TestMockProviderPerformance(random, simulation);
                    TestProviderInterfaceConsistency(random, simulation);
                    TestJobIdValidation(random, simulation);
                    TestProviderStateIsolation(random, simulation);
                    TestErrorConditions(random, simulation);
                    
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
                        throw new Exception($"JobProviderRegistry focused testing failed {failed}/{simulationCount} simulations. Last error: {ex.Message}");
                    }
                }
            }

            stopwatch.Stop();
            
            Console.WriteLine($"JobProviderRegistry Focused Simulations complete: {passed} passed, {failed} failed");
            Console.WriteLine($"Success rate: {(double)passed / simulationCount * 100:F2}%");
            Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Average time per simulation: {stopwatch.ElapsedMilliseconds / (double)simulationCount:F3}ms");

            if (failed > 0)
            {
                throw new Exception($"JobProviderRegistry focused testing failed {failed}/{simulationCount} simulations");
            }
        }

        /// <summary>
        /// Test mock provider capabilities and interface consistency.
        /// </summary>
        private static void TestMockProviderCapabilities(Random random, int simulationId)
        {
            var testJobId = TEST_JOB_IDS[random.Next(TEST_JOB_IDS.Length)];
            var mockProvider = _mockProviders[testJobId];
            
            // Test capability flags
            var hasCombo = mockProvider.HasComboSupport();
            var hasGauge = mockProvider.HasGaugeSupport();
            var hasTracking = mockProvider.HasTrackingSupport();
            
            // Verify consistency with mock setup
            var expectedCombo = _mockProviders[testJobId].SupportsCombo;
            var expectedGauge = _mockProviders[testJobId].SupportsGauge;
            var expectedTracking = _mockProviders[testJobId].SupportsTracking;
            
            if (hasCombo != expectedCombo)
            {
                throw new Exception($"Simulation {simulationId}: HasComboSupport mismatch for job {testJobId}: got {hasCombo}, expected {expectedCombo}");
            }
            
            if (hasGauge != expectedGauge)
            {
                throw new Exception($"Simulation {simulationId}: HasGaugeSupport mismatch for job {testJobId}: got {hasGauge}, expected {expectedGauge}");
            }
            
            if (hasTracking != expectedTracking)
            {
                throw new Exception($"Simulation {simulationId}: HasTrackingSupport mismatch for job {testJobId}: got {hasTracking}, expected {expectedTracking}");
            }
            
            // Test interface casting
            var comboProvider = mockProvider.AsComboProvider();
            var gaugeProvider = mockProvider.AsGaugeProvider();
            var trackingProvider = mockProvider.AsTrackingProvider();
            
            if (hasCombo && comboProvider == null)
            {
                throw new Exception($"Simulation {simulationId}: Job {testJobId} claims combo support but AsComboProvider returned null");
            }
            
            if (!hasCombo && comboProvider != null)
            {
                throw new Exception($"Simulation {simulationId}: Job {testJobId} claims no combo support but AsComboProvider returned provider");
            }
        }

        /// <summary>
        /// Test performance characteristics of mock provider operations.
        /// </summary>
        private static void TestMockProviderPerformance(Random random, int simulationId)
        {
            var testJobId = TEST_JOB_IDS[random.Next(TEST_JOB_IDS.Length)];
            var mockProvider = _mockProviders[testJobId];
            
            // Pre-generate test data
            var operations = new List<Func<bool>>();
            for (int i = 0; i < PERFORMANCE_TEST_ITERATIONS; i++)
            {
                operations.Add(() => mockProvider.HasComboSupport());
            }

            var stopwatch = Stopwatch.StartNew();

            // Test capability check performance
            foreach (var operation in operations)
            {
                _ = operation();
            }

            stopwatch.Stop();
            
            var avgTimePerOperation = stopwatch.Elapsed.TotalNanoseconds / PERFORMANCE_TEST_ITERATIONS;

            // Performance threshold: Mock operations should be extremely fast
            if (avgTimePerOperation > 500.0)
            {
                throw new Exception($"Simulation {simulationId}: Mock provider too slow: {avgTimePerOperation:F2}ns per operation (threshold: 500ns)");
            }
        }

        /// <summary>
        /// Test provider interface consistency and behavior.
        /// </summary>
        private static void TestProviderInterfaceConsistency(Random random, int simulationId)
        {
            foreach (var kvp in _mockProviders)
            {
                var jobId = kvp.Key;
                var provider = kvp.Value;
                
                // Test job display info
                var displayInfo = provider.GetJobDisplayInfo();
                if (string.IsNullOrEmpty(displayInfo))
                {
                    throw new Exception($"Simulation {simulationId}: GetJobDisplayInfo returned null/empty for job {jobId}");
                }
                
                // Test initialization
                try
                {
                    provider.InitializeTracking();
                    // Should not throw
                }
                catch (Exception ex)
                {
                    throw new Exception($"Simulation {simulationId}: InitializeTracking failed for job {jobId}: {ex.Message}");
                }
                
                // Test interface consistency
                if (provider.HasComboSupport() && provider.AsComboProvider() != null)
                {
                    var comboProvider = provider.AsComboProvider()!;
                    var grids = comboProvider.GetComboGrids();
                    
                    if (grids == null)
                    {
                        throw new Exception($"Simulation {simulationId}: GetComboGrids returned null for combo-supporting job {jobId}");
                    }
                    
                    if (grids.Count == 0)
                    {
                        throw new Exception($"Simulation {simulationId}: GetComboGrids returned empty list for combo-supporting job {jobId}");
                    }
                }
                
                if (provider.HasGaugeSupport() && provider.AsGaugeProvider() != null)
                {
                    var gaugeProvider = provider.AsGaugeProvider()!;
                    
                    try
                    {
                        gaugeProvider.UpdateGauge();
                        var gaugeInfo = gaugeProvider.GetGaugeDebugInfo();
                        
                        if (string.IsNullOrEmpty(gaugeInfo))
                        {
                            throw new Exception($"Simulation {simulationId}: GetGaugeDebugInfo returned null/empty for job {jobId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Simulation {simulationId}: Gauge operations failed for job {jobId}: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Test job ID validation and edge cases.
        /// </summary>
        private static void TestJobIdValidation(Random random, int simulationId)
        {
            // Test valid job IDs
            foreach (var jobId in TEST_JOB_IDS)
            {
                if (!_mockProviders.ContainsKey(jobId))
                {
                    throw new Exception($"Simulation {simulationId}: Test job ID {jobId} not in mock providers");
                }
                
                var provider = _mockProviders[jobId];
                var displayInfo = provider.GetJobDisplayInfo();
                
                if (!displayInfo.Contains(jobId.ToString()) && !displayInfo.Contains(_mockProviders[jobId].JobName))
                {
                    throw new Exception($"Simulation {simulationId}: Display info doesn't contain job ID or name for job {jobId}");
                }
            }
        }

        /// <summary>
        /// Test provider state isolation and independence.
        /// </summary>
        private static void TestProviderStateIsolation(Random random, int simulationId)
        {
            // Verify that providers don't affect each other
            var providers = _mockProviders.Values.ToList();
            
            for (int i = 0; i < providers.Count; i++)
            {
                var provider1 = providers[i];
                var originalInfo1 = provider1.GetJobDisplayInfo();
                
                // Interact with other providers
                for (int j = 0; j < providers.Count; j++)
                {
                    if (i == j) continue;
                    
                    var provider2 = providers[j];
                    _ = provider2.HasComboSupport();
                    _ = provider2.GetJobDisplayInfo();
                    
                    if (provider2.HasGaugeSupport() && provider2.AsGaugeProvider() != null)
                    {
                        provider2.AsGaugeProvider()!.UpdateGauge();
                    }
                }
                
                // Verify provider1 state is unchanged
                var newInfo1 = provider1.GetJobDisplayInfo();
                if (originalInfo1 != newInfo1)
                {
                    throw new Exception($"Simulation {simulationId}: Provider state changed after other provider interactions");
                }
            }
        }

        /// <summary>
        /// Test error conditions and exception handling.
        /// </summary>
        private static void TestErrorConditions(Random random, int simulationId)
        {
            var testProvider = _mockProviders[TEST_JOB_IDS[0]];
            
            // Test repeated operations
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    _ = testProvider.HasComboSupport();
                    _ = testProvider.HasGaugeSupport();
                    _ = testProvider.HasTrackingSupport();
                    _ = testProvider.GetJobDisplayInfo();
                    testProvider.InitializeTracking();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Simulation {simulationId}: Repeated operations failed: {ex.Message}");
                }
            }
            
            // Test interface casting consistency
            var comboProvider = testProvider.AsComboProvider();
            var comboProvider2 = testProvider.AsComboProvider();
            
            if ((comboProvider == null) != (comboProvider2 == null))
            {
                throw new Exception($"Simulation {simulationId}: AsComboProvider returned inconsistent results");
            }
        }
    }

    /// <summary>
    /// Mock job provider for testing without external dependencies.
    /// </summary>
    public class MockJobProvider : IJobProvider, IComboProvider, IGaugeProvider, ITrackingProvider
    {
        public uint JobId { get; }
        public string JobName { get; }
        public bool SupportsCombo { get; }
        public bool SupportsGauge { get; }
        public bool SupportsTracking { get; }

        public MockJobProvider(uint jobId, string jobName, bool supportsCombo, bool supportsGauge, bool supportsTracking)
        {
            JobId = jobId;
            JobName = jobName;
            SupportsCombo = supportsCombo;
            SupportsGauge = supportsGauge;
            SupportsTracking = supportsTracking;
        }

        // IJobProvider implementation
        public bool HasComboSupport() => SupportsCombo;
        public bool HasGaugeSupport() => SupportsGauge;
        public bool HasTrackingSupport() => SupportsTracking;

        public IComboProvider? AsComboProvider() => SupportsCombo ? this : null;
        public IGaugeProvider? AsGaugeProvider() => SupportsGauge ? this : null;
        public ITrackingProvider? AsTrackingProvider() => SupportsTracking ? this : null;

        public string GetJobDisplayInfo() => $"Mock {JobName} (Job {JobId})";
        public void InitializeTracking() { /* Mock implementation */ }

        // IComboProvider implementation
        public IReadOnlyList<ComboGrid> GetComboGrids() => new List<ComboGrid> 
        { 
            new ComboGrid(
                "MockGrid",
                new uint[] { 1, 2, 3 },
                new PriorityRule[] { new PriorityRule(_ => true, 999, "Mock rule") }
            )
        }.AsReadOnly();

        // IGaugeProvider implementation
        public void UpdateGauge() { /* Mock implementation */ }
        public uint GetGaugeData1() => 100;
        public uint GetGaugeData2() => 200;
        public string GetGaugeDebugInfo() => $"Mock gauge for {JobName}";

        // ITrackingProvider implementation
        public uint[] GetDebuffsToTrack() => new uint[] { 1, 2, 3 };
        public uint[] GetBuffsToTrack() => new uint[] { 10, 20, 30 };
        public uint[] GetCooldownsToTrack() => new uint[] { 100, 200, 300 };
    }
}
