using System;
using System.Collections.Generic;
using System.Collections.Frozen;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using ModernActionCombo.Core.Attributes;
using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Unified registry for all job providers.
/// Automatically discovers and manages IJobProvider implementations with composition interfaces.
/// Split into partials for maintainability and performance-focused organization.
/// </summary>
public static partial class JobProviderRegistry
{
    // Providers are immutable after initialization for lock-free, thread-safe reads
    private static FrozenDictionary<uint, IJobProvider> _providers = FrozenDictionary<uint, IJobProvider>.Empty;
    private static FrozenDictionary<uint, string> _jobNames = FrozenDictionary<uint, string>.Empty;
    private static bool _initialized = false;
    private static IJobProvider? _activeProvider;

    /// <summary>
    /// Initialize the registry by scanning for [JobCombo] attributed classes.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var providerTypes = assembly.GetTypes()
                .Where(t => t.GetCustomAttribute<JobComboAttribute>() != null)
                .Where(t => typeof(IJobProvider).IsAssignableFrom(t))
                .Where(t => !t.IsAbstract && !t.IsInterface);

            var providersBuilder = new Dictionary<uint, IJobProvider>();
            var jobNamesBuilder = new Dictionary<uint, string>();

            foreach (var type in providerTypes)
            {
                var attribute = type.GetCustomAttribute<JobComboAttribute>()!;
                var provider = (IJobProvider)Activator.CreateInstance(type)!;

                // Initialize tracking for this job
                provider.InitializeTracking();

                providersBuilder[attribute.JobId] = provider;
                if (!string.IsNullOrWhiteSpace(attribute.JobName))
                    jobNamesBuilder[attribute.JobId] = attribute.JobName!;
            }

            _providers = providersBuilder.ToFrozenDictionary();
            _jobNames = jobNamesBuilder.ToFrozenDictionary();

            // Log capabilities
            foreach (var (jobId, provider) in _providers)
            {
                var capabilities = new List<string>(3);
                if (provider.HasComboSupport()) capabilities.Add("Combo");
                if (provider.HasGaugeSupport()) capabilities.Add("Gauge");
                if (provider.HasTrackingSupport()) capabilities.Add("Tracking");

                Logger.Info($"✅ Registered provider for Job {jobId} ({GetJobName(jobId)}) - Capabilities: {string.Join(", ", capabilities)}");
            }

            // Initialize job-specific features after all providers are registered
            InitializeJobSpecificFeatures();

            _initialized = true;
            Logger.Info($"🚀 JobProviderRegistry initialized with {_providers.Count} providers");
        }
        catch (Exception ex)
        {
            Logger.Error($"❌ Failed to initialize JobProviderRegistry: {ex}");
        }
    }

    /// <summary>
    /// Initialize job-specific features after all providers are registered.
    /// This ensures features like SmartTargeting are available even on startup.
    /// </summary>
    private static void InitializeJobSpecificFeatures()
    {
        try
        {
            // Initialize WHM SmartTargeting regardless of current job
            // This ensures the rules are always available
            const uint WHMJobId = 24; // WHM Job ID
            if (_providers.ContainsKey(WHMJobId))
            {
                Logger.Info("🔮 Initializing WHM SmartTargeting during startup");
                
                // Use reflection to call WHMProvider.Initialize() to avoid namespace issues
                var whmProviderType = _providers[WHMJobId].GetType();
                var initializeMethod = whmProviderType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                
                if (initializeMethod != null)
                {
                    initializeMethod.Invoke(null, null);
                    Logger.Info("🔮 WHM SmartTargeting initialized successfully during startup");
                }
                else
                {
                    Logger.Warning("🔮 WHM Initialize method not found");
                }
            }
            
            // Add other job-specific initializations here as needed
            
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to initialize job-specific features: {ex}");
        }
    }
}
