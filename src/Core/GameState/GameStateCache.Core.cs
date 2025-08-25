using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// Ultra-fast, pure .NET 9 game state cache with SIMD optimizations.
/// Completely decoupled from any external dependencies.
/// Uses Vector256 for core state and aggressive inlining for maximum performance.
/// </summary>
public static unsafe partial class GameStateCache
{
    #region Constants
    
    /// <summary>
    /// Sentinel value indicating that debuff/buff/cooldown tracking hasn't been initialized yet.
    /// Used to distinguish between "not present" (0.0f) and "not yet tracked" (-999.0f).
    /// </summary>
    public const float UNINITIALIZED_SENTINEL = -999.0f;
    
    #endregion
    
    #region Core SIMD State (8x uint32 = 256 bits)

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct CoreState
    {
        public uint JobId;
        public uint Level;
        public uint TargetId;
        public uint ZoneId;
        public uint Flags;
        public uint Gauge1;
        public uint Gauge2;
        public uint Timestamp;
    }

    private static CoreState _core;
    private static readonly Vector256<uint> _zeroVector = Vector256<uint>.Zero;
    private static uint _frameStamp;

    // Core state indices (matches CoreState layout order)
    private const int JobIdIndex = 0;
    private const int LevelIndex = 1;
    private const int TargetIdIndex = 2;
    private const int ZoneIdIndex = 3;
    private const int FlagsIndex = 4;
    private const int GaugeData1Index = 5;
    private const int GaugeData2Index = 6;
    private const int TimestampIndex = 7;

    [Flags]
    private enum StateFlags : uint
    {
        None = 0,
        InCombat = 1u << 0,
        HasTarget = 1u << 1,
        InDuty = 1u << 2,
        CanAct = 1u << 3,
        IsMoving = 1u << 4,
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ref uint Lane(int index)
    {
        // Treat the CoreState struct as a contiguous array of 8 uints
        return ref System.Runtime.CompilerServices.Unsafe.Add(ref System.Runtime.CompilerServices.Unsafe.As<CoreState, uint>(ref _core), index);
    }
    
    #endregion
    
    #region Scalar State (for non-SIMD data)
    
    private static float _gcdRemaining;
    private static uint _currentMp;
    private static uint _maxMp;
    private static long _lastUpdateTicks;
    private static bool _isInitialized;
    
    // Tracking via absolute expiry ticks (Environment.TickCount64 based)
    private const long UNINITIALIZED_TICKS = long.MinValue;
    private static readonly Dictionary<uint, long> _playerBuffsExpiry = new();
    private static readonly Dictionary<uint, long> _targetDebuffsExpiry = new();
    private static readonly Dictionary<uint, long> _actionCooldownsExpiry = new();
    
    #endregion
    
    #region Events
    
    /// <summary>
    /// Event fired when the player's job changes. Provides old and new job IDs.
    /// </summary>
    public static event Action<uint, uint>? JobChanged;
    
    #endregion
    
    #region Static Constructor
    
    static GameStateCache()
    {
        // Initialize to zero using SIMD if available
        if (Vector256.IsHardwareAccelerated)
        {
            System.Runtime.CompilerServices.Unsafe.As<CoreState, Vector256<uint>>(ref _core) = _zeroVector;
        }
        else
        {
            for (int i = 0; i < 8; i++) Lane(i) = 0u;
        }
        // Tracking dictionaries remain empty until explicitly initialized by the plugin
    }
    
    /// <summary>
    /// Pre-fills the tracking dictionaries with sentinel values for commonly monitored effects.
    /// This allows us to distinguish between "not present" (0.0f) and "not yet tracked" (UNINITIALIZED_SENTINEL).
    /// Uses the JobTrackingDataRegistry to discover what needs to be tracked.
    /// </summary>
    public static void InitializeTrackingFromRegistry()
    {
        // Materialize to avoid multiple enumeration and pre-size dictionaries
        var debuffs = new List<uint>(JobProviderRegistry.GetAllDebuffsToTrack());
        var buffs = new List<uint>(JobProviderRegistry.GetAllBuffsToTrack());
        var cooldowns = new List<uint>(JobProviderRegistry.GetAllCooldownsToTrack());
        _targetDebuffsExpiry.EnsureCapacity(Math.Max(_targetDebuffsExpiry.Count, debuffs.Count));
        _playerBuffsExpiry.EnsureCapacity(Math.Max(_playerBuffsExpiry.Count, buffs.Count));
        _actionCooldownsExpiry.EnsureCapacity(Math.Max(_actionCooldownsExpiry.Count, cooldowns.Count));

        foreach (var debuffId in debuffs)
            _targetDebuffsExpiry[debuffId] = UNINITIALIZED_TICKS;
        foreach (var buffId in buffs)
            _playerBuffsExpiry[buffId] = UNINITIALIZED_TICKS;
        foreach (var actionId in cooldowns)
            _actionCooldownsExpiry[actionId] = UNINITIALIZED_TICKS;

    Logger.Info($"📊 GameStateCache initialized tracking for {_targetDebuffsExpiry.Count} debuffs, {_playerBuffsExpiry.Count} buffs, {_actionCooldownsExpiry.Count} cooldowns");
    }
    
    #endregion
    
    #region Pure Data Access (No Dependencies)
    
    /// <summary>Get current job ID</summary>
    public static uint JobId => Lane(JobIdIndex);
    
    /// <summary>Get current level</summary>
    public static uint Level => Lane(LevelIndex);
    
    /// <summary>Get current target ID</summary>
    public static uint TargetId => Lane(TargetIdIndex);
    
    /// <summary>Get current zone ID</summary>
    public static uint ZoneId => Lane(ZoneIdIndex);
    
    /// <summary>Get GCD remaining time</summary>
    public static float GcdRemaining => _gcdRemaining;
    
    /// <summary>Check if player is in combat</summary>
    public static bool InCombat => ((StateFlags)Lane(FlagsIndex) & StateFlags.InCombat) != 0;
    
    /// <summary>Check if player has a valid target</summary>
    public static bool HasTarget => ((StateFlags)Lane(FlagsIndex) & StateFlags.HasTarget) != 0;
    
    /// <summary>Check if player is in a duty</summary>
    public static bool InDuty => ((StateFlags)Lane(FlagsIndex) & StateFlags.InDuty) != 0;
    
    /// <summary>Check if player can use abilities</summary>
    public static bool CanUseAbilities => ((StateFlags)Lane(FlagsIndex) & StateFlags.CanAct) != 0;
    
    /// <summary>Check if player is moving</summary>
    public static bool IsMoving => ((StateFlags)Lane(FlagsIndex) & StateFlags.IsMoving) != 0;
    
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

    /// <summary>Monotonically increasing frame stamp advanced each core update (per Framework tick).</summary>
    public static uint FrameStamp => _frameStamp;
    
    /// <summary>Get generic gauge data 1 (job-specific usage)</summary>
    public static uint GetGaugeData1() => Lane(GaugeData1Index);
    
    /// <summary>Get generic gauge data 2 (job-specific usage)</summary>
    public static uint GetGaugeData2() => Lane(GaugeData2Index);
    
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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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
    // Check for job change before updating
    var oldJobId = Lane(JobIdIndex);
    var jobChanged = _isInitialized && oldJobId != jobId;
    
    // Pack boolean flags
    StateFlags flags = StateFlags.None;
    if (inCombat) flags |= StateFlags.InCombat;
    if (hasTarget) flags |= StateFlags.HasTarget;
    if (inDuty) flags |= StateFlags.InDuty;
    if (canAct) flags |= StateFlags.CanAct;
    if (isMoving) flags |= StateFlags.IsMoving;
        
        // Update using SIMD if available
        if (Vector256.IsHardwareAccelerated)
        {
            var newState = Vector256.Create(
                jobId, level, targetId, zoneId,
                (uint)flags, gaugeData1, gaugeData2,
                (uint)Environment.TickCount
            );
            System.Runtime.CompilerServices.Unsafe.As<CoreState, Vector256<uint>>(ref _core) = newState;
        }
        else
        {
            Lane(JobIdIndex) = jobId;
            Lane(LevelIndex) = level;
            Lane(TargetIdIndex) = targetId;
            Lane(ZoneIdIndex) = zoneId;
            Lane(FlagsIndex) = (uint)flags;
            Lane(GaugeData1Index) = gaugeData1;
            Lane(GaugeData2Index) = gaugeData2;
            Lane(TimestampIndex) = (uint)Environment.TickCount;
        }
        
        _lastUpdateTicks = Environment.TickCount64;
        _isInitialized = true;
        
        // Fire job change event if job changed
        if (jobChanged)
        {
            try
            {
                JobChanged?.Invoke(oldJobId, jobId);
            }
            catch
            {
                // Don't let event handler exceptions crash the state update
            }
        }
    unchecked { _frameStamp++; }
    }
    
    /// <summary>
    /// Updates scalar state (non-SIMD data).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static GameStateData CreateSnapshot()
    {
        return new GameStateData(
            Lane(JobIdIndex),
            Lane(LevelIndex),
            InCombat,
            Lane(TargetIdIndex),
            _gcdRemaining
        );
    }
    
    #endregion
    
    #region Cleanup
    
    /// <summary>
    /// Cleanup allocated memory. Call on shutdown.
    /// </summary>
    public static void Dispose()
    {
        // No core memory to free with struct overlay
        SmartTargetingCache.Dispose();
    }
    
    #endregion
}
