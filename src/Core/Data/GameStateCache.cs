using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// Ultra-fast, pure .NET 9 game state cache with SIMD optimizations.
/// Completely decoupled from any external dependencies.
/// Uses Vector256 for core state and aggressive inlining for maximum performance.
/// </summary>
public static unsafe class GameStateCache
{
    #region Constants
    
    /// <summary>
    /// Sentinel value indicating that debuff/buff/cooldown tracking hasn't been initialized yet.
    /// Used to distinguish between "not present" (0.0f) and "not yet tracked" (-999.0f).
    /// </summary>
    public const float UNINITIALIZED_SENTINEL = -999.0f;
    
    #endregion
    
    #region Core SIMD State (8x uint32 = 256 bits)
    
    // SIMD-optimized core state vector (8 uint values in 256 bits)
    private static readonly uint* _coreState;
    private static readonly Vector256<uint> _zeroVector = Vector256<uint>.Zero;
    
    // Core state indices (matches Vector256 layout)
    private const int JobIdIndex = 0;
    private const int LevelIndex = 1;
    private const int TargetIdIndex = 2;
    private const int ZoneIdIndex = 3;
    private const int FlagsIndex = 4;
    private const int GaugeData1Index = 5;
    private const int GaugeData2Index = 6;
    private const int TimestampIndex = 7;
    
    // Bit flags for boolean state (packed into FlagsIndex)
    private const uint InCombatFlag = 1u << 0;
    private const uint HasTargetFlag = 1u << 1;
    private const uint InDutyFlag = 1u << 2;
    private const uint CanActFlag = 1u << 3;
    private const uint IsMovingFlag = 1u << 4;
    
    #endregion
    
    #region Scalar State (for non-SIMD data)
    
    private static float _gcdRemaining;
    private static uint _currentMp;
    private static uint _maxMp;
    private static long _lastUpdateTicks;
    private static bool _isInitialized;
    
    // Buff/Debuff tracking (simple arrays for now)
    private static readonly Dictionary<uint, float> _playerBuffs = new();
    private static readonly Dictionary<uint, float> _targetDebuffs = new();
    private static readonly Dictionary<uint, float> _actionCooldowns = new();
    
    #endregion
    
    #region Static Constructor
    
    static GameStateCache()
    {
        // Allocate aligned memory for SIMD operations
        var memory = (uint*)NativeMemory.AlignedAlloc(32, 32); // 8 uints, 32-byte aligned
        _coreState = memory;
        
        // Initialize to zero using SIMD
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256.Store(_zeroVector, _coreState);
        }
        else
        {
            // Fallback for non-SIMD systems
            for (int i = 0; i < 8; i++)
                _coreState[i] = 0;
        }
        
        // Pre-fill dictionaries with sentinel values for commonly tracked effects
        InitializeCommonTrackingData();
    }
    
    /// <summary>
    /// Pre-fills the tracking dictionaries with sentinel values for commonly monitored effects.
    /// This allows us to distinguish between "not present" (0.0f) and "not yet tracked" (UNINITIALIZED_SENTINEL).
    /// Uses the JobTrackingDataRegistry to discover what needs to be tracked.
    /// </summary>
    private static void InitializeCommonTrackingData()
    {
        // Initialize the tracking data registry first
        JobProviderRegistry.Initialize();
        
        // Get all debuffs to track from all registered jobs
        foreach (var debuffId in JobProviderRegistry.GetAllDebuffsToTrack())
        {
            _targetDebuffs[debuffId] = UNINITIALIZED_SENTINEL;
        }
        
        // Get all buffs to track from all registered jobs
        foreach (var buffId in JobProviderRegistry.GetAllBuffsToTrack())
        {
            _playerBuffs[buffId] = UNINITIALIZED_SENTINEL;
        }
        
        // Get all cooldowns to track from all registered jobs
        foreach (var actionId in JobProviderRegistry.GetAllCooldownsToTrack())
        {
            _actionCooldowns[actionId] = UNINITIALIZED_SENTINEL;
        }
        
        ModernActionCombo.PluginLog?.Info($"📊 GameStateCache initialized tracking for {_targetDebuffs.Count} debuffs, {_playerBuffs.Count} buffs, {_actionCooldowns.Count} cooldowns");
    }
    
    #endregion
    
    #region Pure Data Access (No Dependencies)
    
    /// <summary>Get current job ID</summary>
    public static uint JobId => _coreState[JobIdIndex];
    
    /// <summary>Get current level</summary>
    public static uint Level => _coreState[LevelIndex];
    
    /// <summary>Get current target ID</summary>
    public static uint TargetId => _coreState[TargetIdIndex];
    
    /// <summary>Get current zone ID</summary>
    public static uint ZoneId => _coreState[ZoneIdIndex];
    
    /// <summary>Get GCD remaining time</summary>
    public static float GcdRemaining => _gcdRemaining;
    
    /// <summary>Check if player is in combat</summary>
    public static bool InCombat => (_coreState[FlagsIndex] & InCombatFlag) != 0;
    
    /// <summary>Check if player has a valid target</summary>
    public static bool HasTarget => (_coreState[FlagsIndex] & HasTargetFlag) != 0;
    
    /// <summary>Check if player is in a duty</summary>
    public static bool InDuty => (_coreState[FlagsIndex] & InDutyFlag) != 0;
    
    /// <summary>Check if player can use abilities</summary>
    public static bool CanUseAbilities => (_coreState[FlagsIndex] & CanActFlag) != 0;
    
    /// <summary>Check if player is moving</summary>
    public static bool IsMoving => (_coreState[FlagsIndex] & IsMovingFlag) != 0;
    
    /// <summary>
    /// Centralized check for whether combo processing should be active.
    /// Combines all global prerequisites: InCombat, CanUseAbilities, etc.
    /// Use this instead of manually checking InCombat in individual rules.
    /// </summary>
    public static bool CanProcessCombos => InCombat && CanUseAbilities;
    
    /// <summary>Get current MP</summary>
    public static uint CurrentMp => _currentMp;
    
    /// <summary>Get maximum MP</summary>
    public static uint MaxMp => _maxMp;
    
    /// <summary>Get current MP as a percentage (0.0 to 1.0)</summary>
    public static float MpPercentage => _maxMp > 0 ? (float)_currentMp / _maxMp : 0f;
    
    /// <summary>Check if MP is below a threshold percentage</summary>
    public static bool IsMpLow(float threshold = 0.3f) => MpPercentage < threshold;
    
    /// <summary>Check if MP is sufficient for an ability cost</summary>
    public static bool HasMpFor(uint cost) => _currentMp >= cost;
    
    /// <summary>Check if cache is initialized</summary>
    public static bool IsInitialized => _isInitialized;
    
    /// <summary>Get generic gauge data 1 (job-specific usage)</summary>
    public static uint GetGaugeData1() => _coreState[GaugeData1Index];
    
    /// <summary>Get generic gauge data 2 (job-specific usage)</summary>
    public static uint GetGaugeData2() => _coreState[GaugeData2Index];
    
    /// <summary>
    /// Check if we can weave oGCD abilities. 
    /// Allows up to 2 oGCDs during a standard 2.5s GCD window.
    /// </summary>
    public static bool CanWeave(int ogcdCount = 1)
    {
        // If GCD is not active (0 remaining), we can always weave
        if (_gcdRemaining <= 0)
            return true;
            
        // Standard oGCD takes about 0.7-0.8 seconds to execute
        // We need at least 0.8s * ogcdCount remaining on GCD to safely weave
        var timeNeeded = 0.8f * ogcdCount;
        
        return _gcdRemaining >= timeNeeded;
    }
    
    #endregion
    
    #region Pure Update Methods (Data Provider Agnostic)
    
    /// <summary>
    /// Updates core game state using SIMD operations.
    /// Provider-agnostic - can be called by any data source.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateCoreState(
        uint jobId, 
        uint level, 
        uint targetId, 
        uint zoneId,
        bool inCombat,
        bool hasTarget,
        bool inDuty,
        bool canAct,
        bool isMoving,
        uint gaugeData1 = 0,
        uint gaugeData2 = 0)
    {
        // Pack boolean flags
        uint flags = 0;
        if (inCombat) flags |= InCombatFlag;
        if (hasTarget) flags |= HasTargetFlag;
        if (inDuty) flags |= InDutyFlag;
        if (canAct) flags |= CanActFlag;
        if (isMoving) flags |= IsMovingFlag;
        
        // Update using SIMD if available
        if (Vector256.IsHardwareAccelerated)
        {
            var newState = Vector256.Create(
                jobId, level, targetId, zoneId,
                flags, gaugeData1, gaugeData2,
                (uint)Environment.TickCount
            );
            Vector256.Store(newState, _coreState);
        }
        else
        {
            // Fallback for non-SIMD systems
            _coreState[JobIdIndex] = jobId;
            _coreState[LevelIndex] = level;
            _coreState[TargetIdIndex] = targetId;
            _coreState[ZoneIdIndex] = zoneId;
            _coreState[FlagsIndex] = flags;
            _coreState[GaugeData1Index] = gaugeData1;
            _coreState[GaugeData2Index] = gaugeData2;
            _coreState[TimestampIndex] = (uint)Environment.TickCount;
        }
        
        _lastUpdateTicks = Environment.TickCount64;
        _isInitialized = true;
    }
    
    /// <summary>
    /// Updates scalar state (non-SIMD data).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateScalarState(
        float gcdRemaining,
        uint currentMp,
        uint maxMp)
    {
        _gcdRemaining = gcdRemaining;
        _currentMp = currentMp;
        _maxMp = maxMp;
    }
    
    #endregion
    
    #region Extended Game State Methods
    
    /// <summary>Get remaining time on target debuff in seconds. Returns 0 if not present, UNINITIALIZED_SENTINEL if not yet tracked.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetTargetDebuffTimeRemaining(uint debuffId)
    {
        if (_targetDebuffs.TryGetValue(debuffId, out var timeRemaining))
        {
            // Return sentinel value as-is to indicate uninitialized state
            if (timeRemaining == UNINITIALIZED_SENTINEL)
                return UNINITIALIZED_SENTINEL;
                
            // Calculate remaining time based on when it was last updated
            var elapsed = (Environment.TickCount64 - _lastUpdateTicks) / 1000.0f;
            var remaining = timeRemaining - elapsed;
            return Math.Max(0, remaining);
        }
        return 0.0f; // Not in dictionary = definitely not present
    }
    
    /// <summary>Check if target has the specified debuff.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TargetHasDebuff(uint debuffId)
    {
        var timeRemaining = GetTargetDebuffTimeRemaining(debuffId);
        return timeRemaining > 0; // Only positive values indicate active debuff
    }
    
    /// <summary>Check if debuff tracking has been initialized for the specified debuff.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDebuffTrackingInitialized(uint debuffId)
    {
        var timeRemaining = GetTargetDebuffTimeRemaining(debuffId);
        return timeRemaining != UNINITIALIZED_SENTINEL;
    }
    
    /// <summary>Check if player has the specified buff.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasPlayerBuff(uint buffId)
    {
        return GetPlayerBuffTimeRemaining(buffId) > 0;
    }
    
    /// <summary>Get remaining time on player buff in seconds. Returns 0 if not present, UNINITIALIZED_SENTINEL if not yet tracked.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetPlayerBuffTimeRemaining(uint buffId)
    {
        if (_playerBuffs.TryGetValue(buffId, out var timeRemaining))
        {
            // Return sentinel value as-is to indicate uninitialized state
            if (timeRemaining == UNINITIALIZED_SENTINEL)
                return UNINITIALIZED_SENTINEL;
                
            // Calculate remaining time based on when it was last updated
            var elapsed = (Environment.TickCount64 - _lastUpdateTicks) / 1000.0f;
            var remaining = timeRemaining - elapsed;
            return Math.Max(0, remaining);
        }
        return 0.0f; // Not in dictionary = definitely not present
    }
    
    /// <summary>Check if buff tracking has been initialized for the specified buff.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBuffTrackingInitialized(uint buffId)
    {
        var timeRemaining = GetPlayerBuffTimeRemaining(buffId);
        return timeRemaining != UNINITIALIZED_SENTINEL;
    }
    
    /// <summary>Check if action is ready (off cooldown). Uses real game cooldown data.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsActionReady(uint actionId)
    {
        var cooldown = GetActionCooldown(actionId);
        
        // If cooldown tracking is uninitialized, assume action is NOT ready
        // This prevents spamming abilities when we don't have reliable cooldown data
        if (cooldown == UNINITIALIZED_SENTINEL)
            return false;
            
        // Action is ready if cooldown is 0 or negative (meaning it's off cooldown)
        return cooldown <= 0;
    }
    
    /// <summary>Get remaining cooldown time for action in seconds. Returns 0 if ready, UNINITIALIZED_SENTINEL if not yet tracked.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetActionCooldown(uint actionId)
    {
        if (_actionCooldowns.TryGetValue(actionId, out var cooldownRemaining))
        {
            // Return sentinel value as-is to indicate uninitialized state
            if (cooldownRemaining == UNINITIALIZED_SENTINEL)
                return UNINITIALIZED_SENTINEL;
                
            // Calculate remaining cooldown based on when it was last updated
            var elapsed = (Environment.TickCount64 - _lastUpdateTicks) / 1000.0f;
            var remaining = cooldownRemaining - elapsed;
            return Math.Max(0, remaining);
        }
        return 0.0f; // Not in dictionary = definitely ready
    }
    
    /// <summary>Check if cooldown tracking has been initialized for the specified action.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCooldownTrackingInitialized(uint actionId)
    {
        var cooldown = GetActionCooldown(actionId);
        return cooldown != UNINITIALIZED_SENTINEL;
    }
    
    /// <summary>
    /// Comprehensive OGCD readiness check. Combines all relevant conditions:
    /// - Global combo processing is enabled (InCombat, CanUseAbilities)
    /// - Action is off cooldown (cooldown <= 0)
    /// - Cooldown tracking is initialized (prevents false positives)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOGCDReady(uint actionId)
    {
        // Fast exit: Use centralized combo eligibility check
        if (!CanProcessCombos) return false;
        
        // Check cooldown state
        var cooldown = GetActionCooldown(actionId);
        
        // If cooldown tracking is uninitialized, assume NOT ready
        if (cooldown == UNINITIALIZED_SENTINEL) return false;
        
        // OGCD is ready if cooldown is 0 or negative (off cooldown)
        return cooldown <= 0;
    }
    
    /// <summary>
    /// Updates player buffs. Call this when buff state changes.
    /// </summary>
    public static void UpdatePlayerBuffs(Dictionary<uint, float> buffs)
    {
        // Only update existing keys, preserve UNINITIALIZED_SENTINEL values
        var keysToUpdate = new List<uint>(_playerBuffs.Keys);
        foreach (var buffId in keysToUpdate)
        {
            if (buffs.ContainsKey(buffId))
            {
                _playerBuffs[buffId] = buffs[buffId];
            }
            else if (_playerBuffs[buffId] != UNINITIALIZED_SENTINEL)
            {
                // Buff expired, set to 0
                _playerBuffs[buffId] = 0.0f;
            }
            // If it's UNINITIALIZED_SENTINEL, leave it alone
        }
    }
    
    /// <summary>
    /// Updates target debuffs. Call this when target or debuff state changes.
    /// </summary>
    public static void UpdateTargetDebuffs(Dictionary<uint, float> debuffs)
    {
        // Only update existing keys, preserve UNINITIALIZED_SENTINEL values
        var keysToUpdate = new List<uint>(_targetDebuffs.Keys);
        foreach (var debuffId in keysToUpdate)
        {
            if (debuffs.ContainsKey(debuffId))
            {
                _targetDebuffs[debuffId] = debuffs[debuffId];
            }
            else if (_targetDebuffs[debuffId] != UNINITIALIZED_SENTINEL)
            {
                // Debuff expired, set to 0
                _targetDebuffs[debuffId] = 0.0f;
            }
            // If it's UNINITIALIZED_SENTINEL, leave it alone
        }
    }
    
    /// <summary>
    /// Updates action cooldowns. Call this when cooldown state changes.
    /// </summary>
    public static void UpdateActionCooldowns(Dictionary<uint, float> cooldowns)
    {
        // Only update existing keys, preserve UNINITIALIZED_SENTINEL values
        var keysToUpdate = new List<uint>(_actionCooldowns.Keys);
        foreach (var actionId in keysToUpdate)
        {
            if (cooldowns.ContainsKey(actionId))
            {
                _actionCooldowns[actionId] = cooldowns[actionId];
            }
            else if (_actionCooldowns[actionId] != UNINITIALIZED_SENTINEL)
            {
                // Cooldown finished, set to 0
                _actionCooldowns[actionId] = 0.0f;
            }
            // If it's UNINITIALIZED_SENTINEL, leave it alone
        }
    }
    
    #endregion
    
    #region Action Usage Tracking
    
    /// <summary>
    /// Record that we just used an action, getting its cooldown duration from the game data.
    /// Call this after successfully executing an action to maintain accurate cooldown tracking.
    /// </summary>
    public static void RecordActionUsed(uint actionId)
    {
        var dataManager = ModernActionCombo.DataManager;
        if (dataManager != null)
        {
            var actionSheet = dataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            var action = actionSheet?.GetRow(actionId);
            
            if (action != null)
            {
                // TODO: Find the correct property name for recast time in Lumina.Excel.Sheets.Action
                // Common candidates: Recast100ms, CooldownGroup, etc.
                // For now, use a smart default based on action type
                var cooldownSeconds = actionId switch
                {
                    136 => 120.0f,   // Presence of Mind - 2 minute cooldown
                    16535 => 30.0f,  // Afflatus Misery - 30 second cooldown
                    16532 => 30.0f,  // Dia - 30 second duration (DoT)
                    _ => 2.5f        // Default GCD for most actions
                };
                
                _actionCooldowns[actionId] = cooldownSeconds;
                _lastUpdateTicks = Environment.TickCount64;
            }
        }
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Get milliseconds since last update.
    /// </summary>
    public static long TimeSinceLastUpdate => Environment.TickCount64 - _lastUpdateTicks;
    
    /// <summary>
    /// Check if data is stale (older than threshold).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsStale(long thresholdMs = 100) => TimeSinceLastUpdate > thresholdMs;
    
    /// <summary>
    /// Create a snapshot of current game state for combo processing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameStateData CreateSnapshot()
    {
        return new GameStateData(
            _coreState[JobIdIndex],
            _coreState[LevelIndex],
            InCombat,
            _coreState[TargetIdIndex],
            _gcdRemaining
        );
    }
    
    #endregion
    
    #region Testing Support
    
    /// <summary>
    /// Resets the cache to uninitialized state. FOR TESTING ONLY.
    /// </summary>
    public static void ResetForTesting()
    {
        // Reset core state to zero
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256.Store(_zeroVector, _coreState);
        }
        else
        {
            for (int i = 0; i < 8; i++)
                _coreState[i] = 0;
        }
        
        // Reset scalar state
        _gcdRemaining = 0.0f;
        _currentMp = 0;
        _maxMp = 0;
        _lastUpdateTicks = 0;
        _isInitialized = false;
        
        // Clear all tracking dictionaries
        _playerBuffs.Clear();
        _targetDebuffs.Clear();
        _actionCooldowns.Clear();
        
        // Re-initialize common tracking data
        InitializeCommonTrackingData();
    }
    
    #endregion
    
    #region Job Gauge Updates
    
    /// <summary>
    /// Updates job gauge data in the cache based on current job.
    /// Only updates when the current job matches, preventing unnecessary updates.
    /// </summary>
    public static void UpdateJobGauge(uint jobId, uint gaugeData1, uint gaugeData2)
    {
        // Only update if this matches the current job
        if (JobId != jobId) 
            return;
            
        _coreState[GaugeData1Index] = gaugeData1;
        _coreState[GaugeData2Index] = gaugeData2;
    }
    
    /// <summary>
    /// Updates WHM gauge data (convenience method).
    /// Called by WHMProvider through the registry system.
    /// </summary>
    public static void UpdateWHMGauge(byte healingLilies, uint lilyTimer, byte bloodLily)
    {
        // No job check needed - registry ensures only active job calls this
        
        // Pack lily data into GaugeData1: [bloodLily:8][healingLilies:8][reserved:16]
        var gaugeData1 = (uint)((bloodLily << 8) | healingLilies);
        UpdateJobGauge(JobId, gaugeData1, lilyTimer);
    }
    
    /// <summary>
    /// Example: Updates BLM gauge data (convenience method).
    /// Only updates when current job is BLM (25) or THM (7).
    /// This shows the pattern for adding future jobs.
    /// </summary>
    public static void UpdateBLMGauge(byte umbralStacks, byte astralStacks, uint elementTimer, byte polyglot)
    {
        // Only update if current job is BLM or THM
        var currentJob = JobId;
        if (currentJob != 25 && currentJob != 7) 
            return;
            
        // Pack BLM data: [polyglot:8][astralStacks:8][umbralStacks:8][reserved:8]
        var gaugeData1 = (uint)((polyglot << 24) | (astralStacks << 16) | (umbralStacks << 8));
        UpdateJobGauge(currentJob, gaugeData1, elementTimer);
    }
    
    #endregion
    
    #region Cleanup
    
    /// <summary>
    /// Cleanup allocated memory. Call on shutdown.
    /// </summary>
    public static void Dispose()
    {
        if (_coreState != null)
        {
            NativeMemory.AlignedFree(_coreState);
        }
        
        // Cleanup SmartTargeting memory
        SmartTargetingCache.Dispose();
    }
    
    #endregion
    
    #region SmartTargeting Integration
    
    /// <summary>
    /// Gets the best smart target using percentage-based healing decisions.
    /// More fair across different job HP pools (tank vs caster equality).
    /// Default 1.0f threshold means anyone with ANY missing HP is eligible.
    /// Integrates with SmartTargetingCache for sub-50ns performance.
    /// Priority: Hard Target > Best Party Member > Self
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetSmartTarget(float hpThreshold = 1.0f) =>
        SmartTargetingCache.GetSmartTarget(hpThreshold);
    
    /// <summary>
    /// Check if SmartTargeting is ready and has valid party data.
    /// </summary>
    public static bool IsSmartTargetingReady => SmartTargetingCache.IsReady;
    
    /// <summary>
    /// Hard target detection is now automatic via status flags.
    /// This method is kept for compatibility but does nothing.
    /// Hard targets are detected automatically when UpdateSmartTargetData is called.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetSmartTargetHardTarget(uint memberId)
    {
        // No-op: Hard targets are now detected automatically from status flags
        // The game will set the HardTargetFlag in the status when calling UpdateSmartTargetData
    }
    
    /// <summary>
    /// Check if a specific target is valid for smart targeting.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidSmartTarget(uint memberId) =>
        SmartTargetingCache.IsValidTarget(memberId);
    
    /// <summary>
    /// Check if a specific target needs healing below the threshold.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TargetNeedsHealing(uint memberId, float threshold = 0.95f) =>
        SmartTargetingCache.NeedsHealing(memberId, threshold);
    
    #endregion
}
