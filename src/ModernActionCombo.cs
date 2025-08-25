using System;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;

namespace ModernActionCombo;

/// <summary>
/// ModernActionCombo - Simple high-performance combo system for FFXIV.
/// </summary>
public sealed partial class ModernActionCombo : IDalamudPlugin
{
    public string Name => "ModernActionCombo";

    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    [PluginService] public static IJobGauges JobGauges { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IPartyList PartyList { get; private set; } = null!;
        [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;
        [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;

    private readonly WindowSystem _windowSystem;

    /// <summary>
    /// Initializes the ModernActionCombo plugin.
    /// </summary>
    public ModernActionCombo(IDalamudPluginInterface pluginInterface)
    {
        PluginLog.Information("=== ModernActionCombo Starting ===");

        // Only do basic UI/command wiring here; heavy init is deferred
        _windowSystem = new WindowSystem("ModernActionCombo");
	_pluginInterface = pluginInterface;

    // Initialize configuration storage/path immediately so early unloads can still persist without errors
    EnsureConfigLoaded();

        // UI hooks
        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += () => EnsureInitialized(() => _mainSettingsWindow!.IsOpen = true);
        pluginInterface.UiBuilder.OpenMainUi += () => EnsureInitialized(() => _configWindow!.IsOpen = true);

        // Commands
        CommandManager.AddHandler("/mac", new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Open ModernActionCombo config"
        });
        CommandManager.AddHandler("/modernactioncombo", new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Open ModernActionCombo config"
        });
        CommandManager.AddHandler("/macconfig", new Dalamud.Game.Command.CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Open ModernActionCombo job configuration"
        });

    // Deferred init tick
    Framework.Update += OnFrameworkUpdate;

        PluginLog.Information("✓ Basic plugin setup complete - waiting for game to be ready");
    }

    public void Dispose() => DisposeCore();
}

