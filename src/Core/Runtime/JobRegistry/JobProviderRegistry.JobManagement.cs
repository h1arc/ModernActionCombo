using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ModernActionCombo.Core.Attributes;
using ModernActionCombo.Core.Enums;
using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo.Core.Services;

public static partial class JobProviderRegistry
{
    #region Job Management
    /// <summary>
    /// Called when the player's job changes to activate the appropriate provider.
    /// </summary>
    public static void OnJobChanged(uint jobId)
    {
        if (_providers.TryGetValue(jobId, out var provider))
        {
            _activeProvider = provider;
            // Invalidate fast resolver cache on job change
            _fastResolver = null;
            _cachedJobId = 0;
            _cachedConfigVersion = 0;
            Logger.Debug("🔄 Activated provider for job {0}", jobId);
        }
        else
        {
            _activeProvider = null;
            _fastResolver = null;
            _cachedJobId = 0;
            _cachedConfigVersion = 0;
            Logger.Debug("❓ No provider found for job {0}", jobId);
        }
    }

    /// <summary>
    /// Enum-friendly overload.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void OnJobChanged(JobID job) => OnJobChanged((uint)job);

    /// <summary>
    /// Get the currently active job provider.
    /// </summary>
    public static IJobProvider? GetActiveProvider() => _activeProvider;

    /// <summary>
    /// Get provider for a specific job.
    /// </summary>
    public static IJobProvider? GetProvider(uint jobId)
        => _providers.TryGetValue(jobId, out var provider) ? provider : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IJobProvider? GetProvider(JobID job) => GetProvider((uint)job);

    /// <summary>
    /// Check if a job has a registered provider.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasProvider(uint jobId) => _providers.ContainsKey(jobId);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasProvider(JobID job) => HasProvider((uint)job);

    /// <summary>
    /// Get all registered job IDs.
    /// </summary>
    public static uint[] GetRegisteredJobIds() => _providers.Keys.ToArray();

    /// <summary>
    /// Get job name for display with fallback to JobHelper.
    /// </summary>
    public static string GetJobName(uint jobId)
    {
        if (_jobNames.TryGetValue(jobId, out var name))
            return name;
        return JobHelper.GetJobName(jobId);
    }

    /// <summary>
    /// Check if the current job supports combo processing.
    /// </summary>
    public static bool CurrentJobSupportsComboProcessing() => _activeProvider != null;

    /// <summary>
    /// Check if the current job supports OGCD resolution.
    /// </summary>
    public static bool HasOGCDSupport() => _activeProvider is IOGCDProvider;

    /// <summary>
    /// Get job display info from the active provider.
    /// </summary>
    public static string GetJobDisplayInfo() => _activeProvider?.GetJobDisplayInfo() ?? "No job active";
    #endregion
}
