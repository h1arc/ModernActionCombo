using System;
using System.Collections.Generic;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// Configuration data for job-specific settings.
/// Maintains configuration state for combo grids and other job features.
/// </summary>
public class JobConfiguration
{
    /// <summary>
    /// Enabled combo grids by name. If a grid isn't in this dictionary, it's considered disabled.
    /// </summary>
    public Dictionary<string, bool> EnabledComboGrids { get; set; } = new();
    
    /// <summary>
    /// Enabled oGCD rules by name. If a rule isn't in this dictionary, it's considered enabled by default.
    /// </summary>
    public Dictionary<string, bool> EnabledOGCDRules { get; set; } = new();
    
    /// <summary>
    /// Enabled combo rules by name (format: "GridName.RuleName"). If a rule isn't in this dictionary, it's considered enabled by default.
    /// </summary>
    public Dictionary<string, bool> EnabledComboRules { get; set; } = new();
    
    /// <summary>
    /// Enabled smart target rules by name. If a rule isn't in this dictionary, it's considered enabled by default.
    /// </summary>
    public Dictionary<string, bool> EnabledSmartTargetRules { get; set; } = new();
    
    /// <summary>
    /// Job-specific settings. Key is setting name, value is the setting value.
    /// </summary>
    public Dictionary<string, object> JobSettings { get; set; } = new();
    
    /// <summary>
    /// Checks if a specific combo grid is enabled.
    /// </summary>
    public bool IsComboGridEnabled(string gridName)
    {
        return EnabledComboGrids.TryGetValue(gridName, out bool enabled) && enabled;
    }
    
    /// <summary>
    /// Checks if a specific oGCD rule is enabled. Rules are disabled by default unless explicitly enabled.
    /// </summary>
    public bool IsOGCDRuleEnabled(string ruleName)
    {
        return EnabledOGCDRules.TryGetValue(ruleName, out bool enabled) && enabled;
    }
    
    /// <summary>
    /// Checks if a specific combo rule is enabled. Rules are disabled by default unless explicitly enabled.
    /// </summary>
    public bool IsComboRuleEnabled(string gridName, string ruleName)
    {
        var key = $"{gridName}.{ruleName}";
        return EnabledComboRules.TryGetValue(key, out bool enabled) && enabled;
    }
    
    /// <summary>
    /// Checks if a specific smart target rule is enabled. Rules are enabled by default unless explicitly disabled.
    /// </summary>
    public bool IsSmartTargetRuleEnabled(string ruleName)
    {
        return EnabledSmartTargetRules.TryGetValue(ruleName, out bool enabled) ? enabled : true;
    }
    
    /// <summary>
    /// Enables or disables a combo grid.
    /// </summary>
    public void SetComboGridEnabled(string gridName, bool enabled)
    {
        EnabledComboGrids[gridName] = enabled;
    }
    
    /// <summary>
    /// Enables or disables an oGCD rule.
    /// </summary>
    public void SetOGCDRuleEnabled(string ruleName, bool enabled)
    {
        EnabledOGCDRules[ruleName] = enabled;
    }
    
    /// <summary>
    /// Enables or disables a combo rule.
    /// </summary>
    public void SetComboRuleEnabled(string gridName, string ruleName, bool enabled)
    {
        var key = $"{gridName}.{ruleName}";
        EnabledComboRules[key] = enabled;
    }
    
    /// <summary>
    /// Enables or disables a smart target rule.
    /// </summary>
    public void SetSmartTargetRuleEnabled(string ruleName, bool enabled)
    {
        EnabledSmartTargetRules[ruleName] = enabled;
    }
    
    /// <summary>
    /// Gets a job-specific setting with a default value.
    /// </summary>
    public T GetSetting<T>(string settingName, T defaultValue = default!)
    {
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
        JobSettings[settingName] = value!;
    }
}

/// <summary>
/// Central configuration manager for all jobs.
/// Handles loading, saving, and accessing job configurations.
/// </summary>
public static class ConfigurationManager
{
    private static readonly Dictionary<uint, JobConfiguration> _jobConfigurations = new();
    
    /// <summary>
    /// Gets the configuration for a specific job. Creates a new one if it doesn't exist.
    /// </summary>
    public static JobConfiguration GetJobConfiguration(uint jobId)
    {
        if (!_jobConfigurations.TryGetValue(jobId, out var config))
        {
            config = new JobConfiguration();
            _jobConfigurations[jobId] = config;
        }
        return config;
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
    public static bool IsOGCDEnabled(uint jobId)
    {
        var config = GetJobConfiguration(jobId);
        return config.GetSetting("OGCDEnabled", false); // Disabled by default for complete opt-in
    }
    
    /// <summary>
    /// Checks if smart targeting is enabled overall for the current job.
    /// </summary>
    public static bool IsSmartTargetingEnabled(uint jobId)
    {
        var config = GetJobConfiguration(jobId);
        return config.GetSetting("SmartTargetingEnabled", false); // Disabled by default for complete opt-in
    }
    
    /// <summary>
    /// Sets whether oGCDs are enabled overall for the current job.
    /// </summary>
    public static void SetOGCDEnabled(uint jobId, bool enabled)
    {
        var config = GetJobConfiguration(jobId);
        config.SetSetting("OGCDEnabled", enabled);
        ConfigAwareActionCache.IncrementConfigVersion();
    }
    
    /// <summary>
    /// Sets whether smart targeting is enabled overall for the current job.
    /// </summary>
    public static void SetSmartTargetingEnabled(uint jobId, bool enabled)
    {
        var config = GetJobConfiguration(jobId);
        config.SetSetting("SmartTargetingEnabled", enabled);
        ConfigAwareActionCache.IncrementConfigVersion();
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
        
        ModernActionCombo.PluginLog?.Info($"💾 {(enabled ? "Enabled" : "Disabled")} combo grid '{gridName}' for job {jobId} - config version incremented");
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
        
        ModernActionCombo.PluginLog?.Info($"💾 {(enabled ? "Enabled" : "Disabled")} oGCD rule '{ruleName}' for job {jobId} - config version incremented");
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
        
        ModernActionCombo.PluginLog?.Info($"💾 {(enabled ? "Enabled" : "Disabled")} combo rule '{gridName}.{ruleName}' for job {jobId} - config version incremented");
    }
    
    /// <summary>
    /// Sets the enabled state of a smart target rule for a specific job.
    /// Uses ConfigAwareActionCache and config-aware JobProviderRegistry for optimal performance.
    /// </summary>
    public static void SetSmartTargetRuleEnabled(uint jobId, string ruleName, bool enabled)
    {
        var config = GetJobConfiguration(jobId);
        config.SetSmartTargetRuleEnabled(ruleName, enabled);
        
        // Increment the config version - this will automatically invalidate JobProviderRegistry's fast resolver
        ConfigAwareActionCache.IncrementConfigVersion();
        
        ModernActionCombo.PluginLog?.Info($"💾 {(enabled ? "Enabled" : "Disabled")} smart target rule '{ruleName}' for job {jobId} - config version incremented");
    }
    
    /// <summary>
    /// Gets all job configurations.
    /// </summary>
    public static IReadOnlyDictionary<uint, JobConfiguration> GetAllConfigurations()
    {
        return _jobConfigurations.AsReadOnly();
    }
    
    /// <summary>
    /// Clears all configurations (useful for testing or reset).
    /// </summary>
    public static void ClearAll()
    {
        _jobConfigurations.Clear();
        ModernActionCombo.PluginLog?.Info("🗑️ Cleared all job configurations");
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
        
        // Initialize the config version for ConfigAwareActionCache
        ConfigAwareActionCache.IncrementConfigVersion();
        
        // Initialize WHM provider with configuration
        Jobs.WHM.WHMProvider.Initialize();
        
        ModernActionCombo.PluginLog?.Info("📋 Initialized default job configurations using centralized policy");
    }
}
