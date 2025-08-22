using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using ModernActionCombo.Core.Services;
using ModernActionCombo.Core.Data;
using ModernActionCombo.UI.Windows;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace ModernActionCombo;

/// <summary>
/// ModernActionCombo - Simple high-performance combo system for FFXIV.
/// Hugely inspired by WrathCombo.
/// </summary>
public sealed class ModernActionCombo : IDalamudPlugin
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

    private ActionInterceptor? _actionInterceptor;
    private GameState? _gameState;
    private readonly WindowSystem _windowSystem;
    private DebugPanelWindow? _debugWindow;
    private bool _initialized = false;

    // Movement detection via AgentMap (more reliable than position tracking)
    private System.Numerics.Vector3 _lastPosition;
    private long _lastPositionUpdate;
    private const float MOVEMENT_THRESHOLD = 0.01f; // Minimum distance to consider movement
    
    // Movement timing for leeway (WrathCombo-inspired)
    private DateTime? _movementStarted;
    private DateTime? _movementStopped;
    private const double MOVEMENT_LEEWAY_SECONDS = 0.1; // Must be moving for 100ms to count
    
    // Job change detection
    private uint _lastKnownJob = 0;
    
    // Level change detection
    private uint _lastKnownLevel = 0;
    
    // Duty state detection
    private bool _lastInDuty = false;
    private uint _lastDutyId = 0;
    
    // Combat state detection
    private bool _lastInCombat = false;

    /// <summary>
    /// Initializes the ModernActionCombo plugin.
    /// </summary>
    public ModernActionCombo(IDalamudPluginInterface pluginInterface)
    {
        PluginLog.Information("=== ModernActionCombo Starting ===");
        
        // Only do basic setup in constructor - no service initialization yet
        _windowSystem = new WindowSystem("ModernActionCombo");
        
        // Register UI with Dalamud (safe to do early)
        pluginInterface.UiBuilder.Draw += _windowSystem.Draw;
        pluginInterface.UiBuilder.OpenConfigUi += () => EnsureInitialized(() => _debugWindow!.IsOpen = true);
        pluginInterface.UiBuilder.OpenMainUi += () => EnsureInitialized(() => _debugWindow!.IsOpen = true);
        
        // Register commands (safe to do early)
        CommandManager.AddHandler("/mac", new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Open ModernActionCombo debug panel"
        });
        
        CommandManager.AddHandler("/modernactioncombo", new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Open ModernActionCombo debug panel"
        });
        
        // Use Framework.Update to initialize once the game is fully loaded
        Framework.Update += OnFrameworkUpdate;
        
        PluginLog.Information("✓ Basic plugin setup complete - waiting for game to be ready");
    }

    /// <summary>
    /// Framework update handler - used for deferred initialization and ongoing game state updates.
    /// </summary>
    private void OnFrameworkUpdate(IFramework framework)
    {
        // Only initialize once, and only when player is available
        if (!_initialized && ClientState.LocalPlayer != null)
        {
            // Now we're on the main thread with services ready - safe to initialize
            PerformDeferredInitialization();
            return;
        }
        
        // If initialized, update game state on every framework tick
        if (_initialized && _gameState != null)
        {
            UpdateGameState();
        }
    }
    
    /// <summary>
    /// Updates game state and synchronizes with the GameStateCache.
    /// Called every framework tick to keep state current.
    /// </summary>
    private void UpdateGameState()
    {
        try
        {
            // Update the GameState with current data
            _gameState!.Update();
            
            // Detect job changes and notify the registry
            if (_gameState.CurrentJob != _lastKnownJob)
            {
                _lastKnownJob = _gameState.CurrentJob;
                JobProviderRegistry.OnJobChanged(_gameState.CurrentJob);
                PluginLog.Debug($"🔄 Job changed to: {_gameState.CurrentJob}");
            }
            
            // Detect level changes and notify the registry
            if (_gameState.Level != _lastKnownLevel)
            {
                _lastKnownLevel = _gameState.Level;
                JobProviderRegistry.OnLevelChanged(_gameState.Level);
                PluginLog.Debug($"📈 Level changed to: {_gameState.Level}");
            }
            
            // Detect duty state changes and notify the registry
            var currentInDuty = Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty];
            var currentDutyId = (uint)ClientState.TerritoryType;
            if (currentInDuty != _lastInDuty || (currentInDuty && currentDutyId != _lastDutyId))
            {
                _lastInDuty = currentInDuty;
                _lastDutyId = currentInDuty ? currentDutyId : 0;
                JobProviderRegistry.OnDutyStateChanged(currentInDuty, currentInDuty ? currentDutyId : null);
                var stateText = currentInDuty ? $"entered duty {currentDutyId}" : "left duty";
                PluginLog.Debug($"🏰 Duty state changed: {stateText}");
            }
            
            // Detect combat state changes and notify the registry
            if (_gameState.InCombat != _lastInCombat)
            {
                _lastInCombat = _gameState.InCombat;
                JobProviderRegistry.OnCombatStateChanged(_gameState.InCombat);
                var stateText = _gameState.InCombat ? "entered combat" : "left combat";
                PluginLog.Debug($"⚔️ Combat state changed: {stateText}");
            }
            
            // Synchronize the high-performance GameStateCache with current state
            // Detect movement via position tracking
            var isMoving = DetectMovement();
            
            GameStateCache.UpdateCoreState(
                jobId: _gameState.CurrentJob,
                level: _gameState.Level,
                targetId: _gameState.CurrentTarget,
                zoneId: (uint)(ClientState.TerritoryType),
                inCombat: _gameState.InCombat,
                hasTarget: _gameState.HasTarget,
                inDuty: Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty],
                canAct: !Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Casting],
                isMoving: isMoving
            );
            
            // Update scalar state
            GameStateCache.UpdateScalarState(
                gcdRemaining: _gameState.GlobalCooldownRemaining,
                currentMp: (uint)(ClientState.LocalPlayer?.CurrentMp ?? 0),
                maxMp: (uint)(ClientState.LocalPlayer?.MaxMp ?? 0)
            );
            
            // Update target debuffs (THIS WAS MISSING!)
            UpdateTargetDebuffs();
            
            // Update player buffs (THIS WAS MISSING!)
            UpdatePlayerBuffs();
            
            // Update action cooldowns (THIS WAS MISSING!)
            UpdateActionCooldowns();
            
            // Update job-specific gauge data through registry
            JobProviderRegistry.UpdateActiveJobGauge();
        }
        catch (Exception ex)
        {
            PluginLog.Error(ex, "Error updating game state");
        }
    }

    /// <summary>
    /// Updates target debuff tracking in GameStateCache.
    /// </summary>
    private void UpdateTargetDebuffs()
    {
        var debuffs = new Dictionary<uint, float>();
        
        var target = ClientState.LocalPlayer?.TargetObject;
        if (target != null && target is IBattleChara battleChara)
        {
            // Get all status effects on the target
            foreach (var status in battleChara.StatusList)
            {
                if (status.StatusId != 0)
                {
                    debuffs[status.StatusId] = status.RemainingTime;
                }
            }
        }
        
        GameStateCache.UpdateTargetDebuffs(debuffs);
    }

    /// <summary>
    /// Updates player buff tracking in GameStateCache.
    /// </summary>
    private void UpdatePlayerBuffs()
    {
        var buffs = new Dictionary<uint, float>();
        
        var player = ClientState.LocalPlayer;
        if (player != null)
        {
            // Get all status effects on the player - LocalPlayer is already IBattleChara
            foreach (var status in player.StatusList)
            {
                if (status.StatusId != 0)
                {
                    buffs[status.StatusId] = status.RemainingTime;
                }
            }
        }
        
        GameStateCache.UpdatePlayerBuffs(buffs);
    }

    /// <summary>
    /// Updates action cooldown tracking in GameStateCache.
    /// </summary>
    private void UpdateActionCooldowns()
    {
        var cooldowns = new Dictionary<uint, float>();
        
        // Get all actions to track from the registry
        try
        {
            var actionsToTrack = JobProviderRegistry.GetAllCooldownsToTrack();
            
            foreach (var actionId in actionsToTrack)
            {
                unsafe
                {
                    // Use ActionManager to get real cooldown data like WrathCombo does
                    var actionManager = ActionManager.Instance();
                    var cooldownRemaining = actionManager->GetRecastTime(ActionType.Action, actionId) - 
                                          actionManager->GetRecastTimeElapsed(ActionType.Action, actionId);
                    cooldowns[actionId] = Math.Max(0, cooldownRemaining);
                }
            }
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog.Warning($"Error updating action cooldowns: {ex.Message}");
            // Fallback: assume all abilities are ready if we can't get cooldown data
            cooldowns[136] = 0.0f;    // Presence of Mind
            cooldowns[16535] = 0.0f;  // Afflatus Misery
            cooldowns[16531] = 0.0f;  // Afflatus Solace  
            cooldowns[16534] = 0.0f;  // Afflatus Rapture
        }
        
        GameStateCache.UpdateActionCooldowns(cooldowns);
    }

    /// <summary>
    /// Performs the actual initialization once the game is loaded and services are ready.
    /// </summary>
    private void PerformDeferredInitialization()
    {
        PluginLog.Information("=== Starting Deferred Initialization ===");
        
        // Initialize pure logger with Dalamud provider
        var dalamudLogger = new DalamudLoggerAdapter(PluginLog);
        Logger.Initialize(dalamudLogger);
        PluginLog.Information("✓ Pure logger initialized");
        
        // Note: GameStateCache would be updated by an external data provider
        // For now it exists but won't have real data until provider is implemented
        PluginLog.Information("✓ Pure game state cache ready");
        
        // Initialize core systems
        _gameState = new GameState(ClientState, Condition);
        _actionInterceptor = new ActionInterceptor(_gameState, GameInteropProvider);
        
        // Initialize the new grid-based combo system
        JobProviderRegistry.Initialize();
        PluginLog.Information("✓ Grid-based combo system initialized");
        PluginLog.Information("✓ Action interceptor initialized");
        
        // Initialize UI system
        _debugWindow = new DebugPanelWindow(_actionInterceptor, _gameState);
        _windowSystem.AddWindow(_debugWindow);
        PluginLog.Information("✓ Debug panel UI ready - use /mac to open");
        
        _initialized = true;
        PluginLog.Information("=== ModernActionCombo Ready ===");
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
    /// Detects player movement using AgentMap (more reliable than position tracking).
    /// Includes movement leeway to avoid flickering between moving/stationary states.
    /// Based on WrathCombo's approach but modernized.
    /// </summary>
    private bool DetectMovement()
    {
        var player = ClientState.LocalPlayer;
        if (player == null) return false;

        bool isCurrentlyMoving = false;
        
        // Try to use AgentMap for more accurate movement detection
        try
        {
            unsafe
            {
                var agentMap = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
                if (agentMap != null)
                {
                    // Use AgentMap's IsPlayerMoving for most reliable detection
                    isCurrentlyMoving = agentMap->IsPlayerMoving;
                    
                    // Also check for jumping or other movement states
                    if (!isCurrentlyMoving)
                    {
                        // Fallback to position-based detection if AgentMap says not moving
                        var currentPosition = player.Position;
                        var currentTime = Environment.TickCount64;
                        
                        if (_lastPositionUpdate != 0)
                        {
                            var distance = System.Numerics.Vector3.Distance(currentPosition, _lastPosition);
                            if (distance > MOVEMENT_THRESHOLD && currentTime - _lastPositionUpdate > 50)
                            {
                                isCurrentlyMoving = true;
                            }
                        }
                        
                        // Update position tracking
                        if (currentTime - _lastPositionUpdate > 100)
                        {
                            _lastPosition = currentPosition;
                            _lastPositionUpdate = currentTime;
                        }
                    }
                }
                else
                {
                    // Fallback to position tracking if AgentMap unavailable
                    var currentPosition = player.Position;
                    var currentTime = Environment.TickCount64;
                    
                    if (_lastPositionUpdate == 0)
                    {
                        _lastPosition = currentPosition;
                        _lastPositionUpdate = currentTime;
                        return false;
                    }
                    
                    var distance = System.Numerics.Vector3.Distance(currentPosition, _lastPosition);
                    isCurrentlyMoving = distance > MOVEMENT_THRESHOLD;
                    
                    if (currentTime - _lastPositionUpdate > 100)
                    {
                        _lastPosition = currentPosition;
                        _lastPositionUpdate = currentTime;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback to position tracking on any errors
            PluginLog.Warning($"AgentMap movement detection failed, using position fallback: {ex.Message}");
            
            var currentPosition = player.Position;
            var currentTime = Environment.TickCount64;
            
            if (_lastPositionUpdate == 0)
            {
                _lastPosition = currentPosition;
                _lastPositionUpdate = currentTime;
                return false;
            }
            
            var distance = System.Numerics.Vector3.Distance(currentPosition, _lastPosition);
            isCurrentlyMoving = distance > MOVEMENT_THRESHOLD;
            
            if (currentTime - _lastPositionUpdate > 100)
            {
                _lastPosition = currentPosition;
                _lastPositionUpdate = currentTime;
            }
        }
        
        // Apply movement leeway to avoid flickering
        var now = DateTime.Now;
        
        if (isCurrentlyMoving)
        {
            if (_movementStarted == null)
            {
                _movementStarted = now;
            }
            _movementStopped = null;
        }
        else
        {
            if (_movementStopped == null)
            {
                _movementStopped = now;
            }
            _movementStarted = null;
        }
        
        // Only consider "moving" if we've been moving for the leeway period
        if (_movementStarted != null)
        {
            var timeMoving = now - _movementStarted.Value;
            return timeMoving.TotalSeconds >= MOVEMENT_LEEWAY_SECONDS;
        }
        
        return false;
    }

    /// <summary>
    /// Plugin disposal and cleanup.
    /// </summary>
    public void Dispose()
    {
        // Remove framework update handler
        Framework.Update -= OnFrameworkUpdate;
        
        // Clean up commands
        CommandManager?.RemoveHandler("/mac");
        CommandManager?.RemoveHandler("/modernactioncombo");
        
        // Clean up UI (only if initialized)
        if (_initialized)
        {
            _debugWindow?.Dispose();
            _actionInterceptor?.Dispose();
            GameStateCache.Dispose();
        }
        
        _windowSystem?.RemoveAllWindows();
        
        PluginLog.Information("ModernActionCombo disposed successfully");
    }
}
