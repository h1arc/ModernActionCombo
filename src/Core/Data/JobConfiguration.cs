using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ModernActionCombo.Core.Services;
using ModernActionCombo.Core.Interfaces;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// Configuration data for job-specific settings.
/// Maintains configuration state for combo grids and other job features.
/// </summary>
/// <summary>
/// Per-job configuration snapshot with copy-on-write updates for thread-safe, lock-free reads.
/// </summary>
public sealed class JobConfiguration
{
    // Hot flags cached as fields for fast reads in hot paths (also mirrored in JobSettings for compatibility)
    private volatile bool _ogcdEnabled;            // default false (opt-in)
    private volatile bool _smartTargetingEnabled;  // default false (opt-in)

    public bool OGCDEnabled => _ogcdEnabled;
    public bool SmartTargetingEnabled => _smartTargetingEnabled;

    /// <summary>
    /// Enabled combo grids by name. If a grid isn't in this dictionary, it's considered disabled.
    /// Backed by copy-on-write replacement for safe concurrent reads.
    /// </summary>
    public Dictionary<string, bool> EnabledComboGrids { get; private set; }
    
    /// <summary>
    /// Enabled oGCD rules by name. If a rule isn't in this dictionary, it's considered enabled by default.
    /// </summary>
    public Dictionary<string, bool> EnabledOGCDRules { get; private set; }
    
    /// <summary>
    /// Enabled combo rules by name (format: "GridName.RuleName"). If a rule isn't in this dictionary, it's considered enabled by default.
    /// </summary>
    public Dictionary<string, bool> EnabledComboRules { get; private set; }
    
    /// <summary>
    /// Enabled smart target rules by name. If a rule isn't in this dictionary, it's considered enabled by default.
    /// </summary>
    public Dictionary<string, bool> EnabledSmartTargetRules { get; private set; }
    
    /// <summary>
    /// Job-specific settings. Key is setting name, value is the setting value.
    /// </summary>
    public Dictionary<string, object> JobSettings { get; private set; }

    public JobConfiguration()
    {
        // Use Ordinal comparer for fast, culture-invariant lookups
        EnabledComboGrids = new Dictionary<string, bool>(StringComparer.Ordinal);
        EnabledOGCDRules = new Dictionary<string, bool>(StringComparer.Ordinal);
        EnabledComboRules = new Dictionary<string, bool>(StringComparer.Ordinal);
        EnabledSmartTargetRules = new Dictionary<string, bool>(StringComparer.Ordinal);
        JobSettings = new Dictionary<string, object>(StringComparer.Ordinal);
    }
    
    /// <summary>
    /// Checks if a specific combo grid is enabled.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsComboGridEnabled(string gridName)
    {
        // Not found => disabled by default
        var dict = EnabledComboGrids;
        return dict.TryGetValue(gridName, out bool enabled) && enabled;
    }
    
    /// <summary>
    /// Checks if a specific oGCD rule is enabled. Rules are disabled by default unless explicitly enabled.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsOGCDRuleEnabled(string ruleName)
    {
        var dict = EnabledOGCDRules;
        return dict.TryGetValue(ruleName, out bool enabled) && enabled;
    }
    
    /// <summary>
    /// Checks if a specific combo rule is enabled. Rules are disabled by default unless explicitly enabled.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsComboRuleEnabled(string gridName, string ruleName)
    {
        // Format: Grid.Rule (kept for compatibility with UI display)
        var key = string.Concat(gridName, ".", ruleName);
        var dict = EnabledComboRules;
        return dict.TryGetValue(key, out bool enabled) && enabled;
    }
    
    /// <summary>
    /// Checks if a specific smart target rule is enabled. Rules are enabled by default unless explicitly disabled.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsSmartTargetRuleEnabled(string ruleName)
    {
        var dict = EnabledSmartTargetRules;
        return dict.TryGetValue(ruleName, out bool enabled) ? enabled : true;
    }
    
    /// <summary>
    /// Enables or disables a combo grid.
    /// </summary>
    public void SetComboGridEnabled(string gridName, bool enabled)
    {
        var current = EnabledComboGrids;
        // Copy-on-write replacement
        var next = new Dictionary<string, bool>(current, StringComparer.Ordinal)
        {
            [gridName] = enabled
        };
        EnabledComboGrids = next;
    }
    
    /// <summary>
    /// Enables or disables an oGCD rule.
    /// </summary>
    public void SetOGCDRuleEnabled(string ruleName, bool enabled)
    {
        var current = EnabledOGCDRules;
        var next = new Dictionary<string, bool>(current, StringComparer.Ordinal)
        {
            [ruleName] = enabled
        };
        EnabledOGCDRules = next;
    }
    
    /// <summary>
    /// Enables or disables a combo rule.
    /// </summary>
    public void SetComboRuleEnabled(string gridName, string ruleName, bool enabled)
    {
        var key = string.Concat(gridName, ".", ruleName);
        var current = EnabledComboRules;
        var next = new Dictionary<string, bool>(current, StringComparer.Ordinal)
        {
            [key] = enabled
        };
        EnabledComboRules = next;
    }
    
    /// <summary>
    /// Enables or disables a smart target rule.
    /// </summary>
    public void SetSmartTargetRuleEnabled(string ruleName, bool enabled)
    {
        var current = EnabledSmartTargetRules;
        var next = new Dictionary<string, bool>(current, StringComparer.Ordinal)
        {
            [ruleName] = enabled
        };
        EnabledSmartTargetRules = next;
    }
    
    /// <summary>
    /// Gets a job-specific setting with a default value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetSetting<T>(string settingName, T defaultValue = default!)
    {
        // Fast-path for hot settings
        if (typeof(T) == typeof(bool))
        {
            if (string.Equals(settingName, "OGCDEnabled", StringComparison.Ordinal))
            {
                bool v = _ogcdEnabled; // copy volatile to local
                return Unsafe.As<bool, T>(ref v);
            }
            if (string.Equals(settingName, "SmartTargetingEnabled", StringComparison.Ordinal))
            {
                bool v = _smartTargetingEnabled;
                return Unsafe.As<bool, T>(ref v);
            }
        }
        if (JobSettings.TryGetValue(settingName, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
    }
    
    /// <summary>
    /// Sets a job-specific setting.
    /// </summary>
    public void SetSetting<T>(string settingName, T value)
    {
        // Copy-on-write for JobSettings to avoid concurrent enumeration issues
        var current = JobSettings;
        var next = new Dictionary<string, object>(current, StringComparer.Ordinal)
        {
            [settingName] = value!
        };
        JobSettings = next;

        // Keep fast-path flags mirrored
        if (value is bool b)
        {
            if (string.Equals(settingName, "OGCDEnabled", StringComparison.Ordinal))
                _ogcdEnabled = b;
            else if (string.Equals(settingName, "SmartTargetingEnabled", StringComparison.Ordinal))
                _smartTargetingEnabled = b;
        }
    }
}

/// <summary>
/// Central configuration manager for all jobs.
/// Handles loading, saving, and accessing job configurations.
/// </summary>
public static class ConfigurationManager
{
    // Concurrent for safe access across UI/game threads; writes are infrequent.
    private static readonly ConcurrentDictionary<uint, JobConfiguration> _jobConfigurations = new();

    // Optional enforcement flag (disabled): when true, WHM SmartTargeting and rules are forced on.
    // Keep this false so users can toggle SmartTarget settings in the UI.
    private const uint WHM_JOB_ID = 24;
    private static readonly bool ForceWHMSmartTargetingOn = false;
    
    /// <summary>
    /// Gets the configuration for a specific job. Creates a new one if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static JobConfiguration GetJobConfiguration(uint jobId)
    {
        return _jobConfigurations.GetOrAdd(jobId, static _ => new JobConfiguration());
    }
    
    /// <summary>
    /// Checks if a combo grid is enabled for the current job.
    /// </summary>
    public static bool IsComboGridEnabled(uint jobId, string gridName)
    {
        var config = GetJobConfiguration(jobId);
        return config.IsComboGridEnabled(gridName);
    }
    
    /// <summary>
    /// Checks if an oGCD rule is enabled for the current job.
    /// </summary>
    public static bool IsOGCDRuleEnabled(uint jobId, string ruleName)
    {
        var config = GetJobConfiguration(jobId);
        return config.IsOGCDRuleEnabled(ruleName);
    }
    
    /// <summary>
    /// Checks if a combo rule is enabled for the current job.
    /// </summary>
    public static bool IsComboRuleEnabled(uint jobId, string gridName, string ruleName)
    {
        var config = GetJobConfiguration(jobId);
        return config.IsComboRuleEnabled(gridName, ruleName);
    }
    
    /// <summary>
    /// Checks if a smart target rule is enabled for the current job.
    /// </summary>
    public static bool IsSmartTargetRuleEnabled(uint jobId, string ruleName)
    {
        var config = GetJobConfiguration(jobId);
        return config.IsSmartTargetRuleEnabled(ruleName);
    }
    
    /// <summary>
    /// Checks if oGCDs are enabled overall for the current job.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOGCDEnabled(uint jobId)
    {
        var config = GetJobConfiguration(jobId);
        return config.OGCDEnabled; // Disabled by default for complete opt-in
    }
    
    /// <summary>
    /// Checks if smart targeting is enabled overall for the current job.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSmartTargetingEnabled(uint jobId)
    {
        var config = GetJobConfiguration(jobId);
        return config.SmartTargetingEnabled; // Disabled by default for complete opt-in
    }
    
    /// <summary>
    /// Sets whether oGCDs are enabled overall for the current job.
    /// </summary>
    public static void SetOGCDEnabled(uint jobId, bool enabled)
    {
        var config = GetJobConfiguration(jobId);
        config.SetSetting("OGCDEnabled", enabled);
        ConfigAwareActionCache.IncrementConfigVersion();
    Core.Services.ConfigSaveScheduler.NotifyChanged();
    }
    
    /// <summary>
    /// Sets whether smart targeting is enabled overall for the current job.
    /// </summary>
    public static void SetSmartTargetingEnabled(uint jobId, bool enabled)
    {
        // Enforce WHM SmartTargeting always on if force flag set
        if (ForceWHMSmartTargetingOn && jobId == WHM_JOB_ID)
        {
            enabled = true;
        }
        var config = GetJobConfiguration(jobId);
        config.SetSetting("SmartTargetingEnabled", enabled);
        ConfigAwareActionCache.IncrementConfigVersion();
    Core.Services.ConfigSaveScheduler.NotifyChanged();
    }
    
    /// <summary>
    /// Sets the enabled state of a combo grid for a specific job.
    /// Uses ConfigAwareActionCache and config-aware JobProviderRegistry for optimal performance.
    /// </summary>
    public static void SetComboGridEnabled(uint jobId, string gridName, bool enabled)
    {
        var config = GetJobConfiguration(jobId);
        config.SetComboGridEnabled(gridName, enabled);
        
        // Increment the config version - this will automatically invalidate JobProviderRegistry's fast resolver
        ConfigAwareActionCache.IncrementConfigVersion();
    Core.Services.ConfigSaveScheduler.NotifyChanged();
        
    Logger.Info($"💾 {(enabled ? "Enabled" : "Disabled")} combo grid '{gridName}' for job {jobId} - config version incremented");
    }
    
    /// <summary>
    /// Sets the enabled state of an oGCD rule for a specific job.
    /// Uses ConfigAwareActionCache and config-aware JobProviderRegistry for optimal performance.
    /// </summary>
    public static void SetOGCDRuleEnabled(uint jobId, string ruleName, bool enabled)
    {
        var config = GetJobConfiguration(jobId);
        config.SetOGCDRuleEnabled(ruleName, enabled);
        
        // Increment the config version - this will automatically invalidate JobProviderRegistry's fast resolver
        ConfigAwareActionCache.IncrementConfigVersion();
    Core.Services.ConfigSaveScheduler.NotifyChanged();
        
    Logger.Info($"💾 {(enabled ? "Enabled" : "Disabled")} oGCD rule '{ruleName}' for job {jobId} - config version incremented");
    }
    
    /// <summary>
    /// Sets the enabled state of a combo rule for a specific job.
    /// Uses ConfigAwareActionCache and config-aware JobProviderRegistry for optimal performance.
    /// </summary>
    public static void SetComboRuleEnabled(uint jobId, string gridName, string ruleName, bool enabled)
    {
        var config = GetJobConfiguration(jobId);
        config.SetComboRuleEnabled(gridName, ruleName, enabled);
        
        // Increment the config version - this will automatically invalidate JobProviderRegistry's fast resolver
        ConfigAwareActionCache.IncrementConfigVersion();
    Core.Services.ConfigSaveScheduler.NotifyChanged();
        
    Logger.Info($"💾 {(enabled ? "Enabled" : "Disabled")} combo rule '{gridName}.{ruleName}' for job {jobId} - config version incremented");
    }
    
    /// <summary>
    /// Sets the enabled state of a smart target rule for a specific job.
    /// Uses ConfigAwareActionCache and config-aware JobProviderRegistry for optimal performance.
    /// </summary>
    public static void SetSmartTargetRuleEnabled(uint jobId, string ruleName, bool enabled)
    {
        // Enforce WHM SmartTarget rules always on if force flag set
        if (ForceWHMSmartTargetingOn && jobId == WHM_JOB_ID)
        {
            enabled = true;
        }
        var config = GetJobConfiguration(jobId);
        config.SetSmartTargetRuleEnabled(ruleName, enabled);
        
        // Increment the config version - this will automatically invalidate JobProviderRegistry's fast resolver
        ConfigAwareActionCache.IncrementConfigVersion();
    Core.Services.ConfigSaveScheduler.NotifyChanged();
        
    Logger.Info($"💾 {(enabled ? "Enabled" : "Disabled")} smart target rule '{ruleName}' for job {jobId} - config version incremented");
    }
    
    /// <summary>
    /// Gets all job configurations.
    /// </summary>
    public static IReadOnlyDictionary<uint, JobConfiguration> GetAllConfigurations()
    {
        // Snapshot to a read-only copy for safe external enumeration
    return new System.Collections.ObjectModel.ReadOnlyDictionary<uint, JobConfiguration>(
            new Dictionary<uint, JobConfiguration>(_jobConfigurations));
    }
    
    /// <summary>
    /// Clears all configurations (useful for testing or reset).
    /// </summary>
    public static void ClearAll()
    {
    _jobConfigurations.Clear();
        Logger.Info("🗑️ Cleared all job configurations");
    }
    
    /// <summary>
    /// Initializes default configurations for known jobs.
    /// Uses centralized ConfigurationPolicy for consistency.
    /// </summary>
    public static void InitializeDefaults()
    {
        // Apply centralized configuration policies
        ConfigurationPolicy.ApplyDefaultPolicy(24); // WHM
        ConfigurationPolicy.ApplyDefaultPolicy(21); // WAR
        ConfigurationPolicy.ApplyDefaultPolicy(25); // BLM
        
        // Enforce WHM smart targeting and all rules on by default (and persist soon after)
        if (ForceWHMSmartTargetingOn)
        {
            try { ForceEnableSmartTargetingAndRules(WHM_JOB_ID); } catch { /* best-effort */ }
        }

        // Initialize the config version for ConfigAwareActionCache
        ConfigAwareActionCache.IncrementConfigVersion();
        
        // Initialize WHM provider with configuration
        Jobs.WHM.WHMProvider.Initialize();
        
    Logger.Info("📋 Initialized default job configurations using centralized policy");
    }

    /// <summary>
    /// Forces SmartTargetingEnabled = true and all SmartTarget rules = true for the specified job if it exposes named rules.
    /// Intended for test/validation to guarantee behavior regardless of existing config state.
    /// </summary>
    public static void ForceEnableSmartTargetingAndRules(uint jobId)
    {
        // Always turn on the main flag first
        SetSmartTargetingEnabled(jobId, true);

        // If the job provider exposes named smart target rules, enable them all
        var provider = Core.Services.JobProviderRegistry.GetProvider(jobId);
        if (provider is INamedSmartTargetRulesProvider smartRulesProvider)
        {
            foreach (var named in smartRulesProvider.GetNamedSmartTargetRules())
            {
                SetSmartTargetRuleEnabled(jobId, named.Name, true);
            }
        }
    }
}
