using System;
using System.Collections.Generic;
using ModernActionCombo.Core.Interfaces;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Cross-job utilities built on top of JobProviderRegistry.
/// Provides convenient access to capabilities and tracking data for all providers, not just active.
/// </summary>
public static class AllJobsService
{
    /// <summary>
    /// Enumerate all registered job providers and their names.
    /// </summary>
    public static IEnumerable<(uint jobId, IJobProvider provider, string name)> GetAllProviders()
    {
        // Uses JobProviderRegistry.GetRegisteredJobIds and GetProvider
        foreach (var id in JobProviderRegistry.GetRegisteredJobIds())
        {
            var p = JobProviderRegistry.GetProvider(id);
            if (p != null)
                yield return (id, p, JobProviderRegistry.GetJobName(id));
        }
    }
}
