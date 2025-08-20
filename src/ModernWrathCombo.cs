using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using ModernWrathCombo.Core.Services;
using ModernWrathCombo.Core.Data;
using ModernWrathCombo.Jobs.WHM;
using ModernWrathCombo.UI.Windows;

namespace ModernWrathCombo;

/// <summary>
/// ModernWrathCombo - Simple high-performance combo system for FFXIV.
/// Hugely inspired by WrathCombo.
/// </summary>
public sealed class ModernWrathCombo : IDalamudPlugin
{
    public string Name => "ModernWrathCombo";

    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

    private ActionInterceptor? _actionInterceptor;
    private GameState? _gameState;
    private readonly WindowSystem _windowSystem;
    private DebugPanelWindow? _debugWindow;
    private bool _initialized = false;

    /// <summary>
    /// Initializes the ModernWrathCombo plugin.
    /// </summary>
    public ModernWrathCombo(IDalamudPluginInterface pluginInterface)
    {
        PluginLog.Information("=== ModernWrathCombo Starting ===");
        
        // Only do basic setup in constructor - no service initialization yet
        _windowSystem = new WindowSystem("ModernWrathCombo");
        
        // Register UI with Dalamud (safe to do early)
        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += () => EnsureInitialized(() => _debugWindow!.IsOpen = true);
        pluginInterface.UiBuilder.OpenMainUi += () => EnsureInitialized(() => _debugWindow!.IsOpen = true);
        
        // Register commands (safe to do early)
        CommandManager.AddHandler("/mwc", new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Open ModernWrathCombo debug panel"
        });
        
        CommandManager.AddHandler("/modernwrathcombo", new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Open ModernWrathCombo debug panel"
        });
        
        // Use Framework.Update to initialize once the game is fully loaded
        Framework.Update += OnFrameworkUpdate;
        
        PluginLog.Information("✓ Basic plugin setup complete - waiting for game to be ready");
    }

    /// <summary>
    /// Framework update handler - used for deferred initialization.
    /// </summary>
    private void OnFrameworkUpdate(IFramework framework)
    {
        // Only initialize once, and only when player is available
        if (!_initialized && ClientState.LocalPlayer != null)
        {
            // Now we're on the main thread with services ready - safe to initialize
            PerformDeferredInitialization();
            
            // Remove the framework update handler since we're done with it
            Framework.Update -= OnFrameworkUpdate;
        }
    }

    /// <summary>
    /// Performs the actual initialization once the game is loaded and services are ready.
    /// </summary>
    private void PerformDeferredInitialization()
    {
        PluginLog.Information("=== Starting Deferred Initialization ===");
        
        // Initialize throttled logger first
        Logger.Initialize(PluginLog);
        PluginLog.Information("✓ Logger initialized");
        
        // Initialize high-performance game state cache (30ms updates)
        GameStateCache.Initialize(Framework, ClientState, Condition, 30);
        PluginLog.Information("✓ Game state cache initialized");
        
        // Initialize core systems with cached state instead of live API calls
        _gameState = new GameState(ClientState, Condition);
        _actionInterceptor = new ActionInterceptor(_gameState, GameInteropProvider);
        PluginLog.Information("✓ Action interceptor initialized");
        
        // Initialize UI system
        _debugWindow = new DebugPanelWindow(_actionInterceptor, _gameState);
        _windowSystem.AddWindow(_debugWindow);
        PluginLog.Information("✓ Debug panel UI ready - use /mwc to open");
        
        _initialized = true;
        PluginLog.Information("=== ModernWrathCombo Ready ===");
    }

    /// <summary>
    /// Ensures initialization has occurred before executing an action.
    /// </summary>
    private void EnsureInitialized(Action action)
    {
        if (_initialized)
        {
            action();
        }
        else
        {
            PluginLog.Warning("Action requested before initialization complete - deferring");
        }
    }

    /// <summary>
    /// Command handler for debug panel commands.
    /// </summary>
    private void OnCommand(string command, string args)
    {
        EnsureInitialized(() =>
        {
            _debugWindow!.IsOpen = !_debugWindow.IsOpen;
            PluginLog.Information($"Debug panel toggled: {(_debugWindow.IsOpen ? "Open" : "Closed")}");
        });
    }

    /// <summary>
    /// Plugin disposal and cleanup.
    /// </summary>
    public void Dispose()
    {
        // Remove framework update handler if still attached
        Framework.Update -= OnFrameworkUpdate;
        
        // Clean up commands
        CommandManager?.RemoveHandler("/mwc");
        CommandManager?.RemoveHandler("/modernwrathcombo");
        
        // Clean up UI (only if initialized)
        if (_initialized)
        {
            _debugWindow?.Dispose();
            _actionInterceptor?.Dispose();
            GameStateCache.Dispose();
        }
        
        _windowSystem?.RemoveAllWindows();
        
        PluginLog.Information("ModernWrathCombo disposed successfully");
    }
}
