using System;
using Dalamud.Plugin.Services;
using ModernActionCombo.UI.Windows;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo;

// Initialization, command handlers, and EnsureInitialized helper
public sealed partial class ModernActionCombo
{
    private void PerformDeferredInitialization()
    {
        PluginLog.Information("=== Starting Deferred Initialization ===");

        // Ensure configuration storage is initialized and loaded before other systems
        EnsureConfigLoaded();

        var dalamudLogger = new DalamudLoggerAdapter(PluginLog);
        Logger.Initialize(dalamudLogger);
        Logger.Info("✓ Pure logger initialized");

        // Config defaults would be applied via ConfigurationStorage when loaded in the main init path

        // Initialize registry and tracking before hooks, to avoid cold-start work on first hook pass
        JobProviderRegistry.Initialize();

        // Activate the current job provider so tracking seeds with the right IDs (debuffs/buffs/cooldowns)
        try
        {
            var jobId = (uint)(ClientState.LocalPlayer?.ClassJob.RowId ?? 0u);
            if (jobId != 0)
            {
                JobProviderRegistry.OnJobChanged(jobId);
            }
        }
        catch { /* ignore and continue with empty provider if unavailable */ }

        GameStateCache.InitializeTrackingFromRegistry();
        Logger.Info("✓ Registry and cache tracking initialized");

    // SmartTargeting tuning options removed; defaults are now built-in.

        // Hooks last so they see a warm cache and registry
        _actionInterceptor = new ActionInterceptor(GameInteropProvider);
        // Apply persisted Direct Input preference immediately
        try
        {
            var desired = ConfigurationStorage.DirectInputEnabled ? ActionInterceptionMode.DirectInput : ActionInterceptionMode.Standard;
            _actionInterceptor.SwitchMode(desired);
        }
        catch { /* best-effort */ }
        _smartTargetInterceptor = new SmartTargetInterceptor();

        _configWindow = new JobConfigWindow(_actionInterceptor, _windowSystem);
        _mainSettingsWindow = new MainSettingsWindow(_actionInterceptor, _windowSystem);
        _windowSystem.AddWindow(_configWindow);
        _windowSystem.AddWindow(_mainSettingsWindow);

        _initialized = true;
        Logger.Info("=== ModernActionCombo Ready ===");
    }

    private void EnsureInitialized(Action action)
    {
        if (_initialized) action();
        else PluginLog.Information("Initialization pending; UI action deferred");
    }

    private void OnCommand(string command, string args)
    {
        EnsureInitialized(() =>
        {
            var a = (args ?? string.Empty).Trim();
            if (a.Equals("stdebug on", StringComparison.OrdinalIgnoreCase))
            {
                Core.Data.SmartTargetingCache.SetDebugTraceEnabled(true);
                PluginLog.Information("SmartTarget debug trace ENABLED");
                return;
            }
            if (a.Equals("stdebug off", StringComparison.OrdinalIgnoreCase))
            {
                Core.Data.SmartTargetingCache.SetDebugTraceEnabled(false);
                PluginLog.Information("SmartTarget debug trace DISABLED");
                return;
            }
            if (a.Equals("stdebug status", StringComparison.OrdinalIgnoreCase))
            {
                var info = Core.Data.SmartTargetingCache.GetDebugInfo();
                var compId = Core.Data.SmartTargetingCache.GetCompanionId();
                var compHp = Core.Data.SmartTargetingCache.GetCompanionHpPercent();
                var hardId = Core.Data.SmartTargetingCache.GetHardTargetId();
                PluginLog.Information($"SmartTarget status: {info}; companionId={compId}, companionHp={compHp:P1}, hardTarget={hardId}");
                return;
            }
                _configWindow!.IsOpen = true;
        });
    }

    private void OnConfigCommand(string command, string args)
    {
        EnsureInitialized(() => { _configWindow!.IsOpen = !_configWindow.IsOpen; });
    }
}
