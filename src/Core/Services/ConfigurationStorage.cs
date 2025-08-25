using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Persists job configurations to a JSON file and restores them on startup.
/// Keeps a simple, robust schema with versioning for future migrations.
/// </summary>
public static class ConfigurationStorage
{
    private static string? _configPath;
    // Global flags not tied to a specific job
    public static bool DirectInputEnabled { get; set; }

    private sealed class PersistedJobConfiguration
    {
        public bool OGCDEnabled { get; set; }
        public bool SmartTargetingEnabled { get; set; }
        public Dictionary<string, bool> EnabledComboGrids { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, bool> EnabledOGCDRules { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, bool> EnabledComboRules { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, bool> EnabledSmartTargetRules { get; set; } = new(StringComparer.Ordinal);
        public Dictionary<string, object> JobSettings { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class PersistedConfig
    {
        public uint Version { get; set; } = 1;
        public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
        public bool DirectInputEnabled { get; set; }
        public bool AutoThrottleEnabled { get; set; }
        public Dictionary<uint, PersistedJobConfiguration> Jobs { get; set; } = new();
    }

    /// <summary>
    /// Initializes storage with a file path. Creates the directory if needed.
    /// </summary>
    public static void Initialize(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
            throw new ArgumentException("Config path cannot be null or empty", nameof(configPath));

        var dir = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _configPath = configPath;
    }

    /// <summary>
    /// Loads all configurations from disk and applies them to ConfigurationManager.
    /// Returns true if a config file was found and loaded successfully.
    /// </summary>
    public static bool LoadAll()
    {
        try
        {
            if (_configPath == null || !File.Exists(_configPath))
                return false;

            var json = File.ReadAllText(_configPath);
            var data = JsonConvert.DeserializeObject<PersistedConfig>(json);
            if (data == null) return false;

            foreach (var kvp in data.Jobs)
            {
                var jobId = kvp.Key;
                var pj = kvp.Value;
                var cfg = ConfigurationManager.GetJobConfiguration(jobId);

                // Apply flags first
                ConfigurationManager.SetOGCDEnabled(jobId, pj.OGCDEnabled);
                ConfigurationManager.SetSmartTargetingEnabled(jobId, pj.SmartTargetingEnabled);

                // Apply dictionaries
                foreach (var (grid, enabled) in pj.EnabledComboGrids)
                    ConfigurationManager.SetComboGridEnabled(jobId, grid, enabled);

                foreach (var (rule, enabled) in pj.EnabledOGCDRules)
                    ConfigurationManager.SetOGCDRuleEnabled(jobId, rule, enabled);

                foreach (var (compound, enabled) in pj.EnabledComboRules)
                {
                    // compound = "Grid.Rule"
                    var idx = compound.IndexOf('.')
;                   if (idx > 0)
                    {
                        var grid = compound.Substring(0, idx);
                        var rule = compound.Substring(idx + 1);
                        ConfigurationManager.SetComboRuleEnabled(jobId, grid, rule, enabled);
                    }
                }

                foreach (var (rule, enabled) in pj.EnabledSmartTargetRules)
                    ConfigurationManager.SetSmartTargetRuleEnabled(jobId, rule, enabled);

                // Apply JobSettings (primitive types recommended)
                foreach (var (key, val) in pj.JobSettings)
                {
                    try { cfg.SetSetting(key, val); }
                    catch { /* ignore type mismatches */ }
                }
            }

            // Apply global flags
            try
            {
                DirectInputEnabled = data.DirectInputEnabled;
                Core.Runtime.PerformanceController.AutoThrottleEnabled = data.AutoThrottleEnabled;
            }
            catch { /* ignore */ }

            ModernActionCombo.PluginLog?.Info($"💾 Loaded configuration from '{_configPath}'");
            return true;
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Warning($"⚠️ Failed to load configuration: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Saves all configurations to disk.
    /// </summary>
    public static void SaveAll()
    {
        try
        {
            if (_configPath == null)
            {
                // Not initialized: nothing to persist. Avoid noisy errors on early unload.
                ModernActionCombo.PluginLog?.Debug("Config save skipped: storage not initialized");
                return;
            }

            var snapshot = ConfigurationManager.GetAllConfigurations();
            var data = new PersistedConfig { Version = 1, SavedAtUtc = DateTime.UtcNow };
            // Persist global flags
            data.DirectInputEnabled = DirectInputEnabled;
            data.AutoThrottleEnabled = Core.Runtime.PerformanceController.AutoThrottleEnabled;

            foreach (var kvp in snapshot)
            {
                var jobId = kvp.Key;
                var cfg = kvp.Value;
                var pj = new PersistedJobConfiguration
                {
                    OGCDEnabled = cfg.OGCDEnabled,
                    SmartTargetingEnabled = cfg.SmartTargetingEnabled,
                    EnabledComboGrids = new Dictionary<string, bool>(cfg.EnabledComboGrids, StringComparer.Ordinal),
                    EnabledOGCDRules = new Dictionary<string, bool>(cfg.EnabledOGCDRules, StringComparer.Ordinal),
                    EnabledComboRules = new Dictionary<string, bool>(cfg.EnabledComboRules, StringComparer.Ordinal),
                    EnabledSmartTargetRules = new Dictionary<string, bool>(cfg.EnabledSmartTargetRules, StringComparer.Ordinal),
                    JobSettings = new Dictionary<string, object>(cfg.JobSettings, StringComparer.Ordinal)
                };

                data.Jobs[jobId] = pj;
            }

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(_configPath, json);
            ModernActionCombo.PluginLog?.Info($"💾 Saved configuration to '{_configPath}'");
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Error($"❌ Failed to save configuration: {ex}");
        }
    }
}
