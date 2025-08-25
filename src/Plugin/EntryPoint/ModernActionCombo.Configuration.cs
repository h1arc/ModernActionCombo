using System;
using System.IO;
using Dalamud.Plugin;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo;

// Configuration path resolution and load/save helpers
public sealed partial class ModernActionCombo
{
    private IDalamudPluginInterface? _pluginInterface;
    private bool _configLoaded;

    private void EnsureConfigLoaded()
    {
        if (_configLoaded) return;

        try
        {
            var baseDir = _pluginInterface?.GetPluginConfigDirectory() ??
                          Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                              "XIVLauncher", "pluginConfigs", "ModernActionCombo");

            var cfgPath = Path.Combine(baseDir, "ModernActionCombo.json");
            ConfigurationStorage.Initialize(cfgPath);

            var loaded = ConfigurationStorage.LoadAll();
            if (loaded)
            {
                PluginLog.Information($"Config loaded from {cfgPath}");
            }
            else
            {
                // First run (no existing config): apply default policy and persist immediately
                PluginLog.Information($"No existing config found. Applying default policy and creating {cfgPath}");
                try
                {
                    Core.Data.ConfigurationManager.InitializeDefaults();
                    ConfigurationStorage.SaveAll();
                    PluginLog.Information("Default configuration created successfully");
                }
                catch (Exception createEx)
                {
                    PluginLog.Warning($"Failed to create default configuration: {createEx.Message}");
                }
            }

            _configLoaded = true;
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"Failed to initialize configuration: {ex.Message}");
        }
    }
}
