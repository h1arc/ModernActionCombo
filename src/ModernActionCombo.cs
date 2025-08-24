using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using ModernActionCombo.Core.Services;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Enums;
using ModernActionCombo.UI.Windows;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Reflection;
using System;

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
    [PluginService] public static IPartyList PartyList { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;

    private ActionInterceptor? _actionInterceptor;
    private SmartTargetInterceptor? _smartTargetInterceptor;
    private GameState? _gameState;
    private readonly WindowSystem _windowSystem;
    private DebugPanelWindow? _debugWindow;
    private JobConfigWindow? _configWindow;
    private MainSettingsWindow? _mainSettingsWindow;
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
        pluginInterface.UiBuilder.OpenConfigUi += () => EnsureInitialized(() => _mainSettingsWindow!.IsOpen = true);
        pluginInterface.UiBuilder.OpenMainUi += () => EnsureInitialized(() => _configWindow!.IsOpen = true);
        
        // Register commands (safe to do early)
        CommandManager.AddHandler("/mac", new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Open ModernActionCombo debug panel"
        });
        
        CommandManager.AddHandler("/modernactioncombo", new Dalamud.Game.Command.CommandInfo(OnCommand)
        {
            HelpMessage = "Open ModernActionCombo debug panel"
        });
        
        CommandManager.AddHandler("/macconfig", new Dalamud.Game.Command.CommandInfo(OnConfigCommand)
        {
            HelpMessage = "Open ModernActionCombo job configuration"
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
            
            // Update party member data for SmartTargeting
            UpdatePartyMembers();
            
            // Update hard target detection (needs to run every frame for responsive targeting)
            if (ClientState.LocalPlayer != null)
            {
                UpdateSmartTargetHardTarget(ClientState.LocalPlayer);
                
                // Update companion scanning (secondary priority system)
                UpdateCompanionDetection(ClientState.LocalPlayer);
            }
            
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
    /// Updates party member data for SmartTargeting.
    /// Simplified version that works with available Dalamud APIs.
    /// </summary>
    private void UpdatePartyMembers()
    {
        try
        {
            var localPlayer = ClientState.LocalPlayer;
            if (localPlayer == null)
            {
                return; // Not logged in
            }
            
            var partyList = PartyList;
            if (partyList == null || partyList.Length == 0)
            {
                // Solo play - just add self as party member
                UpdateSoloPlayerData();
                return;
            }
            
            // Prepare data arrays for party members
            Span<uint> memberIds = stackalloc uint[8];
            Span<float> hpPercentages = stackalloc float[8];
            Span<uint> statusFlags = stackalloc uint[8];
            
            byte memberCount = 0;
            
            // Process each party member
            for (int i = 0; i < Math.Min(partyList.Length, 8); i++)
            {
                var member = partyList[i];
                if (member?.GameObject == null) continue;
                
                var memberObject = member.GameObject;
                
                // 🔍 DEBUG: Log member info for troubleshooting
                PluginLog.Debug($"Party member {i}: {memberObject.Name}, ID: {memberObject.GameObjectId:X}, " +
                               $"ObjectKind: {memberObject.ObjectKind}, SubKind: {memberObject.SubKind}");
                
                // Convert GameObjectId to uint (truncate if necessary)
                memberIds[memberCount] = (uint)(memberObject.GameObjectId & 0xFFFFFFFF);
                
                // Try to get HP data if it's a battle character
                if (memberObject is IBattleChara battleChara)
                {
                    hpPercentages[memberCount] = battleChara.CurrentHp > 0 && battleChara.MaxHp > 0 
                        ? (float)battleChara.CurrentHp / battleChara.MaxHp 
                        : 0.0f;
                }
                else
                {
                    // Fallback for non-battle characters
                    hpPercentages[memberCount] = 1.0f;
                }
                
                // Build status flags
                uint flags = 0;
                
                // Basic status checks
                if (memberObject is IBattleChara bc && bc.CurrentHp > 0) 
                    flags |= 1u << 0; // AliveFlag
                else if (!(memberObject is IBattleChara))
                    flags |= 1u << 0; // Assume alive if not a battle character
                
                // Range and LoS checks (simplified - assume true if in party)
                flags |= 1u << 1; // InRangeFlag  
                flags |= 1u << 2; // InLosFlag
                flags |= 1u << 3; // TargetableFlag
                
                // Self check
                if (memberObject.GameObjectId == localPlayer.GameObjectId)
                    flags |= 1u << 4; // SelfFlag
                
                // Note: Hard target detection is now handled separately in UpdateSmartTargetHardTarget()
                // No need to set HardTargetFlag here
                
                // Role flags based on job ID
                var jobId = member.ClassJob.RowId;
                if (JobHelper.IsTank(jobId)) flags |= 1u << 6; // TankFlag
                else if (JobHelper.IsHealer(jobId)) flags |= 1u << 7; // HealerFlag
                else if (JobHelper.IsDPS(jobId)) flags |= 1u << 8; // MeleeFlag (generic DPS)
                
                // 🔥 CRITICAL: AllyFlag for party members (was missing!)
                // Party members are always allies and can receive healing
                flags |= 1u << 10; // AllyFlag
                
                statusFlags[memberCount] = flags;
                memberCount++;
            }
            
            // Update SmartTargeting cache
            SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, memberCount);
            
            // 🔍 DEBUG: Check for non-party targets (like chocobos)
            CheckForNonPartyTargets(localPlayer, memberIds.Slice(0, memberCount));
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"Error updating party members: {ex.Message}");
            // Fallback to solo mode
            UpdateSoloPlayerData();
        }
    }

    /// <summary>
    /// Checks for non-party targets like chocobos, minions, etc. that might be valid heal targets.
    /// These don't show up in the party list but can be manually targeted.
    /// </summary>
    private void CheckForNonPartyTargets(Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter localPlayer, ReadOnlySpan<uint> partyMemberIds)
    {
        var currentTarget = localPlayer.TargetObject;
        
        PluginLog.Debug($"🐦 CheckForNonPartyTargets called - Target: {currentTarget?.Name ?? "NULL"}");
        
        if (currentTarget == null) return;
        
        var targetId = (uint)(currentTarget.GameObjectId & 0xFFFFFFFF);
        
        // Skip if target is already in party
        for (int i = 0; i < partyMemberIds.Length; i++)
        {
            if (partyMemberIds[i] == targetId)
            {
                PluginLog.Debug($"🐦 Target {currentTarget.Name} is already in party, skipping");
                return;
            }
        }
        
        // This is a non-party target - log details for debugging
        PluginLog.Warning($"🐦 Non-party target detected: {currentTarget.Name}, ID: {targetId:X}, " +
                       $"ObjectKind: {currentTarget.ObjectKind}, SubKind: {currentTarget.SubKind}");
        
        // Check if it's a chocobo or other companion
        if (currentTarget.Name.ToString().Contains("Chocobo") || 
            currentTarget.ObjectKind.ToString().Contains("Companion"))
        {
            PluginLog.Warning($"🐦 CHOCOBO/COMPANION DETECTED: {currentTarget.Name} - this needs special handling for smart targeting!");
        }
    }

    /// <summary>
    /// Updates party data for solo play (just the local player).
    /// </summary>
    private void UpdateSoloPlayerData()
    {
        var localPlayer = ClientState.LocalPlayer;
        if (localPlayer == null) return;
        
        Span<uint> memberIds = stackalloc uint[1];
        Span<float> hpPercentages = stackalloc float[1];
        Span<uint> statusFlags = stackalloc uint[1];
        
        memberIds[0] = (uint)(localPlayer.GameObjectId & 0xFFFFFFFF);
        hpPercentages[0] = localPlayer.CurrentHp > 0 && localPlayer.MaxHp > 0 
            ? (float)localPlayer.CurrentHp / localPlayer.MaxHp 
            : 1.0f;
        
        // Solo player flags: Alive + InRange + InLoS + Targetable + Self + Ally
        uint flags = (1u << 0) | (1u << 1) | (1u << 2) | (1u << 3) | (1u << 4) | (1u << 10);
        
        // Add role flag
        var jobId = localPlayer.ClassJob.RowId;
        if (JobHelper.IsTank(jobId)) flags |= 1u << 6;
        else if (JobHelper.IsHealer(jobId)) flags |= 1u << 7;
        else if (JobHelper.IsDPS(jobId)) flags |= 1u << 8;
        
        statusFlags[0] = flags;
        
        SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, 1);
    }
    
    /// <summary>
    /// Updates the hard target tracking for SmartTargeting.
    /// Modern approach: Use ActionManager.CanUseActionOnTarget() for validation.
    /// This handles ALL the complexity - range, LoS, friend/foe, chocobo support, etc.
    /// </summary>
    private void UpdateSmartTargetHardTarget(Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter localPlayer)
    {
        var currentTarget = localPlayer?.TargetObject;
        if (currentTarget == null)
        {
            SmartTargetingCache.UpdateHardTarget(0, false);
            return;
        }
        
        var targetId = (uint)(currentTarget.GameObjectId & 0xFFFFFFFF);
        
        // Modern validation: Let the game engine decide if we can use healing abilities on this target
        // This automatically handles: chocobos, companions, party members, range, LoS, friend/foe, etc.
        bool canHeal = CanUseHealingActionOnTarget(currentTarget);
        
        // Special handling for BattleNpc targets (like chocobos)
        if (currentTarget.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc && localPlayer != null)
        {
            bool isChocobo = IsPlayerChocobo(currentTarget, localPlayer);
            bool chocoboTargetingEnabled = GetChocoboTargetingSetting();
            
            // Override canHeal for chocobos based on setting
            if (isChocobo)
            {
                canHeal = canHeal && chocoboTargetingEnabled;
            }
        }
        
        SmartTargetingCache.UpdateHardTarget(targetId, canHeal);
    }
    
    /// <summary>
    /// Check if we can use healing actions on a target using game engine validation.
    /// Uses ActionManager.CanUseActionOnTarget() which handles all edge cases.
    /// </summary>
    private unsafe bool CanUseHealingActionOnTarget(Dalamud.Game.ClientState.Objects.Types.IGameObject target)
    {
        // Test with a basic healing action (Cure - available at level 2, minimal requirements)
        const uint CureActionId = 120; // WHM Cure
        
        try
        {
            // Use the game's own action validation system
            // Cast to ClientStructs GameObject pointer
            var gameObjectPtr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;
            bool canUse = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.CanUseActionOnTarget(CureActionId, gameObjectPtr);
            return canUse;
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"🎯 Error checking CanUseActionOnTarget for {target.Name}: {ex.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// Check if a BattleNpc target is the player's chocobo.
    /// Uses reflection-based ownership detection with name-based fallback.
    /// </summary>
    private bool IsPlayerChocobo(Dalamud.Game.ClientState.Objects.Types.IGameObject target, Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter localPlayer)
    {
        // Check if it's a BattleNpc (chocobos are BattleNpcs)
        if (target.ObjectKind != Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc)
            return false;
            
        // Use reflection to check OwnerId if available
        try
        {
            var ownerIdProperty = target.GetType().GetProperty("OwnerId");
            if (ownerIdProperty != null)
            {
                var ownerId = (uint?)ownerIdProperty.GetValue(target);
                if (ownerId == localPlayer.GameObjectId)
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            PluginLog.Debug($"🔍 Reflection failed for {target.Name}: {ex.Message}");
        }
        
        // Fallback: name-based detection for edge cases
        var targetName = target.Name.ToString().ToLower();
        var playerName = localPlayer.Name.ToString().ToLower();
        
        if (!string.IsNullOrEmpty(playerName) && 
            targetName.Contains(playerName) && 
            targetName.Contains("chocobo"))
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Get the chocobo targeting setting for WHM.
    /// </summary>
    private bool GetChocoboTargetingSetting()
    {
        var jobConfig = ConfigurationManager.GetJobConfiguration(24); // WHM
        return jobConfig.GetSetting("SmartTargetIncludeChocobos", false);
    }
    
    /// <summary>
    /// Updates companion detection for secondary priority system.
    /// Scans nearby objects for player-owned companions (chocobos, etc.)
    /// that are not in the party but can receive healing.
    /// </summary>
    private void UpdateCompanionDetection(Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter localPlayer)
    {
        try
        {
            bool companionTargetingEnabled = GetChocoboTargetingSetting();
            bool inDuty = ClientState.IsPvP || (Condition != null && Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty]);
            
            // Update companion system state for fast exit paths
            SmartTargetingCache.UpdateCompanionSystemState(companionTargetingEnabled, inDuty);
            
            // Fast exit: Setting disabled or in duty
            if (!companionTargetingEnabled || inDuty)
            {
                SmartTargetingCache.UpdateCompanionData(0, 1.0f, false);
                return;
            }
            
            uint bestCompanionId = 0;
            float bestCompanionHp = 1.0f; // Start with full HP
            bool foundValidCompanion = false;
            
            // Scan nearby objects for companions
            var gameObjects = ObjectTable?.Where(o => o != null && o.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc);
            if (gameObjects != null)
            {
                foreach (var obj in gameObjects)
                {
                    // Check if this is a player-owned companion
                    if (IsPlayerChocobo(obj, localPlayer))
                    {
                        // Check if we can heal it
                        bool canHeal = CanUseHealingActionOnTarget(obj);
                        
                        if (canHeal)
                        {
                            // Get HP percentage
                            float hpPercent = GetHpPercentage(obj);
                            
                            // Take the companion with the lowest HP
                            if (hpPercent < bestCompanionHp)
                            {
                                bestCompanionId = (uint)(obj.GameObjectId & 0xFFFFFFFF);
                                bestCompanionHp = hpPercent;
                                foundValidCompanion = true;
                            }
                        }
                    }
                }
            }
            
            // Update companion cache with best companion found (or clear if none)
            SmartTargetingCache.UpdateCompanionData(bestCompanionId, bestCompanionHp, foundValidCompanion);
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"🐎 Error in companion detection: {ex.Message}");
            // Clear companion data on error
            SmartTargetingCache.UpdateCompanionData(0, 1.0f, false);
        }
    }
    
    /// <summary>
    /// Get HP percentage for any game object.
    /// </summary>
    private float GetHpPercentage(Dalamud.Game.ClientState.Objects.Types.IGameObject obj)
    {
        try
        {
            if (obj is Dalamud.Game.ClientState.Objects.Types.IBattleChara battleChara)
            {
                if (battleChara.MaxHp > 0)
                {
                    return (float)battleChara.CurrentHp / battleChara.MaxHp;
                }
            }
            return 1.0f; // Default to full HP if we can't read it
        }
        catch
        {
            return 1.0f; // Default to full HP on error
        }
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
        _smartTargetInterceptor = new SmartTargetInterceptor();
        
        // Initialize the new grid-based combo system
        JobProviderRegistry.Initialize();
        PluginLog.Information("✓ Grid-based combo system initialized");
        
        // Initialize configuration system with defaults
        ConfigurationManager.InitializeDefaults();
        PluginLog.Information("✓ Configuration system initialized with defaults");
        
        PluginLog.Information("✓ Action interceptor initialized");
        PluginLog.Information("✓ Smart target interceptor initialized");
        
        // Initialize UI system
        _debugWindow = new DebugPanelWindow(_actionInterceptor, _gameState);
        _configWindow = new JobConfigWindow(_actionInterceptor, _gameState, _windowSystem);
        _mainSettingsWindow = new MainSettingsWindow(_actionInterceptor, _gameState, _windowSystem);
        _windowSystem.AddWindow(_debugWindow);
        _windowSystem.AddWindow(_configWindow);
        _windowSystem.AddWindow(_mainSettingsWindow);
        PluginLog.Information("✓ Debug panel UI ready - use /mac to open");
        PluginLog.Information("✓ Configuration UI ready - use /macconfig to open");
        PluginLog.Information("✓ Main settings UI ready - access via plugin config menu");
        
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
    /// Command handler for configuration window commands.
    /// </summary>
    private void OnConfigCommand(string command, string args)
    {
        EnsureInitialized(() =>
        {
            _configWindow!.IsOpen = !_configWindow.IsOpen;
            PluginLog.Information($"Configuration window toggled: {(_configWindow.IsOpen ? "Open" : "Closed")}");
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
        CommandManager?.RemoveHandler("/macconfig");
        
        // Clean up UI (only if initialized)
        if (_initialized)
        {
            _debugWindow?.Dispose();
            _configWindow?.Dispose();
            _mainSettingsWindow?.Dispose();
            _actionInterceptor?.Dispose();
            _smartTargetInterceptor?.Dispose();
            GameStateCache.Dispose();
        }
        
        _windowSystem?.RemoveAllWindows();
        
        PluginLog.Information("ModernActionCombo disposed successfully");
    }
}
