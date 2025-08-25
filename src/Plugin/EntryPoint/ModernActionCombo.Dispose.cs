using System;
using Dalamud.Plugin.Services;
using ModernActionCombo.Core.Services;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo;

// Disposal and cleanup, kept minimal here
public sealed partial class ModernActionCombo
{
    private void DisposeCore()
    {
        // Unsubscribe framework
        Framework.Update -= OnFrameworkUpdate;

        // Remove commands
        CommandManager?.RemoveHandler("/mac");
        CommandManager?.RemoveHandler("/modernactioncombo");
        CommandManager?.RemoveHandler("/macconfig");

        // Persist config
        try { ConfigurationStorage.SaveAll(); } catch { /* ignore */ }

        if (_initialized)
        {
            _configWindow?.Dispose();
            _mainSettingsWindow?.Dispose();
            _actionInterceptor?.Dispose();
            _smartTargetInterceptor?.Dispose();
            GameStateCache.Dispose();
        }

        _windowSystem?.RemoveAllWindows();

        Logger.Info("ModernActionCombo disposed successfully");
    }
}
