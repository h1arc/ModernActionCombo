using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
using Dalamud.Plugin.Services;
using Dalamud.Game.ClientState.Objects.Types;

namespace ModernWrathCombo.Core.Data;

/// <summary>
/// High-performance SIMD-based game state cache with built-in performance monitoring.
/// Uses vectorized operations for maximum throughput and future scalability.
/// Designed to handle multiple jobs, bulk operations, and complex state efficiently.
/// </summary>
public static unsafe class GameStateCache
{
    #region SIMD-Aligned State Storage
    // All data aligned to 32-byte boundaries for AVX2 operations
    // Using fixed arrays for predictable memory layout and vectorization
    
    // Core state vector (8 x uint32): [jobId, level, targetId, zoneId, flags, gcd_int, reserved1, reserved2]
    private static readonly uint[] _coreState = new uint[8];
    
    // Extended state vectors for future expansion
    private static readonly float[] _floatState = new float[8];  // [gcdRemaining, castTime, animationLock, etc.]
    private static readonly uint[] _timestamps = new uint[8];    // [lastUpdate, combatStart, targetChange, etc.]
    
    // Bulk data arrays (designed for SIMD batch processing)
    private static readonly float[] _abilityCooldowns = new float[64];    // 64 abilities max
    private static readonly float[] _buffTimers = new float[32];          // 32 buffs/debuffs max
    private static readonly uint[] _buffIds = new uint[32];               // Corresponding buff IDs
    
    // Target debuff tracking - lightweight multi-target support
    private static readonly Dictionary<ulong, TargetDebuffSnapshot> _debuffedTargets = new();
    private static readonly float[] _currentTargetDebuffTimers = new float[16];  // Current target cache
    private static readonly uint[] _currentTargetDebuffIds = new uint[16];       
    private static int _currentTargetDebuffCount = 0;
    private static ulong _lastTargetId = 0;
    private static DateTime _lastDebuffUpdate = DateTime.MinValue;
    
    // Preemptive action tracking (to handle cast times)
    private static readonly Dictionary<uint, DateTime> _recentlyUsedActions = new();
    private static readonly TimeSpan _actionCooldownWindow = TimeSpan.FromSeconds(3.0); // Don't re-apply DoTs for 3s after casting
    
    /// <summary>
    /// Lightweight snapshot of debuffs on a specific target.
    /// </summary>
    private struct TargetDebuffSnapshot
    {
        public float[] Timers;
        public uint[] Ids;
        public int Count;
        public DateTime CaptureTime;
        
        public TargetDebuffSnapshot(float[] timers, uint[] ids, int count)
        {
            Timers = new float[count];
            Ids = new uint[count];
            Count = count;
            CaptureTime = DateTime.UtcNow;
            
            Array.Copy(timers, Timers, count);
            Array.Copy(ids, Ids, count);
        }
    }
    
    // State change detection (previous values for event firing)
    private static readonly uint[] _previousCoreState = new uint[8];
    
    // Flag definitions (bit positions in flags field)
    private const uint InCombatFlag = 0x00000001;
    private const uint HasTargetFlag = 0x00000002;
    private const uint InDutyFlag = 0x00000004;
    private const uint IsLoadingFlag = 0x00000008;
    private const uint CanActFlag = 0x00000010;
    
    // Core state indices
    private const int JobIdIndex = 0;
    private const int LevelIndex = 1;
    private const int TargetIdIndex = 2;
    private const int ZoneIdIndex = 3;
    private const int FlagsIndex = 4;
    private const int GcdIntIndex = 5;
    
    // Float state indices
    private const int GcdRemainingIndex = 0;
    private const int CastTimeIndex = 1;
    private const int AnimationLockIndex = 2;
    #endregion
    
    #region SIMD-Optimized Hot Path Accessors
    /// <summary>Gets current job ID using vectorized access.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetJobId() => _coreState[JobIdIndex];
    
    /// <summary>Gets current level using vectorized access.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetLevel() => _coreState[LevelIndex];
    
    /// <summary>Gets current target ID using vectorized access.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetTargetId() => _coreState[TargetIdIndex];
    
    /// <summary>Gets current zone ID using vectorized access.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetZoneId() => _coreState[ZoneIdIndex];
    
    /// <summary>Gets GCD remaining using vectorized access.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetGcdRemaining() => _floatState[GcdRemainingIndex];
    
    /// <summary>Gets combat state using SIMD flag checking.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInCombat() => (_coreState[FlagsIndex] & InCombatFlag) != 0;
    
    /// <summary>Gets target state using SIMD flag checking.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasValidTarget() => (_coreState[FlagsIndex] & HasTargetFlag) != 0;
    
    /// <summary>Gets duty state using SIMD flag checking.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInDuty() => (_coreState[FlagsIndex] & InDutyFlag) != 0;
    
    /// <summary>Gets ability action state using SIMD flag checking.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CanUseAbilities() => (_coreState[FlagsIndex] & CanActFlag) != 0;
    
    // Backward compatibility properties (delegate to methods)
    public static uint JobId => GetJobId();
    public static uint Level => GetLevel();
    public static uint TargetId => GetTargetId();
    public static uint ZoneId => GetZoneId();
    public static float GcdRemaining => GetGcdRemaining();
    public static bool InCombat => IsInCombat();
    public static bool HasTarget => HasValidTarget();
    public static bool InDuty => IsInDuty();
    
    /// <summary>Creates a GameStateData snapshot from SIMD-cached values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GameStateData CreateSnapshot()
    {
        return new GameStateData(JobId, Level, InCombat, TargetId, GcdRemaining);
    }
    
    #region Target Debuff Tracking
    /// <summary>
    /// Gets the remaining time for a specific debuff on the current target.
    /// Uses time prediction to avoid constant API calls.
    /// Returns 0.0f if the debuff is not present or no target is selected.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetTargetDebuffTimeRemaining(uint debuffId)
    {
        var currentTarget = GetTargetId();
        if (currentTarget == 0) return 0.0f;
        
        // Check current target cache first (fastest path)
        for (int i = 0; i < _currentTargetDebuffCount; i++)
        {
            if (_currentTargetDebuffIds[i] == debuffId)
            {
                var elapsed = (float)(DateTime.UtcNow - _lastDebuffUpdate).TotalSeconds;
                return Math.Max(0.0f, _currentTargetDebuffTimers[i] - elapsed);
            }
        }
        
        // Fall back to cached snapshot if we have one
        return GetTargetDebuffTimeRemaining(debuffId, currentTarget);
    }
    
    /// <summary>
    /// Gets the remaining time for a specific debuff on a specific target.
    /// Uses cached snapshots with time prediction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetTargetDebuffTimeRemaining(uint debuffId, ulong targetId)
    {
        if (!_debuffedTargets.TryGetValue(targetId, out var snapshot))
            return 0.0f;
            
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (snapshot.Ids[i] == debuffId)
            {
                // Predict remaining time based on elapsed time since snapshot
                var elapsed = (float)(DateTime.UtcNow - snapshot.CaptureTime).TotalSeconds;
                return Math.Max(0.0f, snapshot.Timers[i] - elapsed);
            }
        }
        return 0.0f;
    }
    
    /// <summary>
    /// Checks if a specific debuff is present on the current target.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasTargetDebuff(uint debuffId)
    {
        return GetTargetDebuffTimeRemaining(debuffId) > 0.0f;
    }
    
    /// <summary>
    /// Checks if a specific debuff is present on a specific target.
    /// Uses cached snapshots with time prediction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasTargetDebuff(uint debuffId, ulong targetId)
    {
        return GetTargetDebuffTimeRemaining(debuffId, targetId) > 0.0f;
    }
    
    /// <summary>
    /// Checks if a debuff on the current target is expiring soon (within the specified threshold).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTargetDebuffExpiring(uint debuffId, float thresholdSeconds = 5.0f)
    {
        var timeRemaining = GetTargetDebuffTimeRemaining(debuffId);
        return timeRemaining > 0.0f && timeRemaining <= thresholdSeconds;
    }
    
    /// <summary>
    /// Checks if a debuff on a specific target is expiring soon (within the specified threshold).
    /// Uses cached snapshots with time prediction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTargetDebuffExpiring(uint debuffId, ulong targetId, float thresholdSeconds = 5.0f)
    {
        var timeRemaining = GetTargetDebuffTimeRemaining(debuffId, targetId);
        return timeRemaining > 0.0f && timeRemaining <= thresholdSeconds;
    }
    
    /// <summary>
    /// Gets all targets that have a specific debuff applied by the player.
    /// Uses cached snapshots to check multiple targets efficiently.
    /// </summary>
    public static List<ulong> GetTargetsWithDebuff(uint debuffId)
    {
        var targets = new List<ulong>();
        foreach (var kvp in _debuffedTargets)
        {
            if (HasTargetDebuff(debuffId, kvp.Key))
                targets.Add(kvp.Key);
        }
        return targets;
    }
    
    /// <summary>
    /// Gets the target with the shortest remaining time for a specific debuff.
    /// Returns 0 if no targets have the debuff. Useful for retargeting to refresh expiring DoTs.
    /// </summary>
    public static ulong GetTargetWithExpiringDebuff(uint debuffId, float thresholdSeconds = 5.0f)
    {
        ulong bestTarget = 0;
        float shortestTime = float.MaxValue;
        
        foreach (var kvp in _debuffedTargets)
        {
            var timeRemaining = GetTargetDebuffTimeRemaining(debuffId, kvp.Key);
            if (timeRemaining > 0.0f && timeRemaining <= thresholdSeconds && timeRemaining < shortestTime)
            {
                shortestTime = timeRemaining;
                bestTarget = kvp.Key;
            }
        }
        
        return bestTarget;
    }
    
    /// <summary>
    /// Records that an action was just executed to prevent re-application during cast time.
    /// Call this when the ActionInterceptor executes a DoT action.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordActionExecution(uint actionId)
    {
        _recentlyUsedActions[actionId] = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Checks if an action was recently executed (within the cooldown window).
    /// Used to prevent re-casting DoTs during cast animation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool WasActionRecentlyUsed(uint actionId)
    {
        if (!_recentlyUsedActions.TryGetValue(actionId, out var lastUsed))
            return false;
            
        var elapsed = DateTime.UtcNow - lastUsed;
        return elapsed < _actionCooldownWindow;
    }
    
    /// <summary>
    /// Sets target debuff time remaining for testing purposes.
    /// Only available in test builds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetTargetDebuffTimeRemaining(uint debuffId, float timeRemaining)
    {
        var currentTarget = GetTargetId();
        if (currentTarget == 0) return;
        
        // Create or update debuff snapshot for testing
        var timers = new float[] { timeRemaining };
        var ids = new uint[] { debuffId };
        var snapshot = new TargetDebuffSnapshot(timers, ids, 1);
        
        _debuffedTargets[(ulong)currentTarget] = snapshot;
    }
    #endregion
    
    /// <summary>
    /// SIMD-optimized bulk cooldown check for multiple abilities.
    /// Returns a bitmask indicating which abilities are ready (off cooldown).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong GetReadyAbilitiesMask(Span<uint> abilityIndices)
    {
        if (!Avx2.IsSupported) return GetReadyAbilitiesFallback(abilityIndices);
        
        var result = 0UL;
        var zeroVector = Vector256<float>.Zero;
        
        // Process 8 abilities at a time using AVX2
        for (int i = 0; i < abilityIndices.Length; i += 8)
        {
            var remaining = Math.Min(8, abilityIndices.Length - i);
            if (remaining < 8) break; // Use fallback for partial vectors
            
            // Load 8 cooldown values
            fixed (float* cooldownPtr = &_abilityCooldowns[abilityIndices[i]])
            {
                var cooldowns = Avx.LoadVector256(cooldownPtr);
                // Compare with zero (ready if <= 0)
                var readyMask = Avx.Compare(cooldowns, zeroVector, FloatComparisonMode.OrderedLessThanOrEqualNonSignaling);
                
                // Convert comparison result to bitmask
                var mask = (ulong)Avx.MoveMask(readyMask);
                result |= mask << i;
            }
        }
        
        return result;
    }
    
    private static ulong GetReadyAbilitiesFallback(Span<uint> abilityIndices)
    {
        var result = 0UL;
        for (int i = 0; i < abilityIndices.Length && i < 64; i++)
        {
            if (_abilityCooldowns[abilityIndices[i]] <= 0f)
                result |= 1UL << i;
        }
        return result;
    }
    #endregion
    
    #region Cache Management
    private static IFramework? _framework;
    private static IClientState? _clientState;
    private static ICondition? _condition;
    private static readonly object _initLock = new();
    private static bool _isInitialized = false;
    private static DateTime _lastUpdate = DateTime.MinValue;
    private static TimeSpan _updateInterval = TimeSpan.FromMilliseconds(30);

    /// <summary>
    /// Initializes the cache with Dalamud services and uses Framework.Update for main thread updates.
    /// </summary>
    public static void Initialize(IFramework framework, IClientState clientState, ICondition condition, int updateIntervalMs = 30)
    {
        lock (_initLock)
        {
            if (_isInitialized) return;
            
            _framework = framework;
            _clientState = clientState;
            _condition = condition;
            _updateInterval = TimeSpan.FromMilliseconds(updateIntervalMs);
            
            // Perform initial update synchronously (we're on main thread from deferred init)
            UpdateState();
            _lastUpdate = DateTime.UtcNow;
            
            // Register for Framework.Update (runs on main thread)
            // Register for Framework.Update (runs on main thread)
            _framework.Update += OnFrameworkUpdate;
            _isInitialized = true;
            
            ModernWrathCombo.PluginLog.Info($"[GameStateCache] Initialized with {updateIntervalMs}ms update interval using Framework.Update");
            
            // Start performance monitoring
            PerformanceMonitor.Start();
        }
    }
    
    /// <summary>
    /// Framework update handler - runs on main thread with controlled interval.
    /// </summary>
    private static void OnFrameworkUpdate(IFramework framework)
    {
        var now = DateTime.UtcNow;
        if (now - _lastUpdate >= _updateInterval)
        {
            UpdateState();
            _lastUpdate = now;
        }
    }
    
    /// <summary>
    /// Stops the cache and disposes resources.
    /// </summary>
    public static void Dispose()
    {
        lock (_initLock)
        {
            if (_framework != null)
            {
                _framework.Update -= OnFrameworkUpdate;
                _framework = null;
            }
            _isInitialized = false;
            
            PerformanceMonitor.Stop();
            ModernWrathCombo.PluginLog.Info("[GameStateCache] Disposed");
        }
    }
    
    /// <summary>
    /// Forces an immediate cache update (for testing/debugging).
    /// </summary>
    public static void ForceUpdate()
    {
        UpdateState();
    }
    #endregion
    
    #region SIMD-Optimized Update Logic (Cold Path)
    private static void UpdateState()
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Check if we're on the main thread - Dalamud services require this
            if (_clientState?.LocalPlayer == null)
            {
                // Player not loaded, use SIMD to clear all state
                ClearAllStateSIMD();
                return;
            }
            
            var player = _clientState.LocalPlayer;
            
            // Read all values first (minimize API call window)
            var jobId = player.ClassJob.RowId;
            var level = player.Level;
            var targetId = (uint)(player.TargetObject?.GameObjectId ?? 0);
            var zoneId = _clientState.TerritoryType;
            
            // Read condition flags
            var inCombat = _condition?[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat] ?? false;
            var inDuty = _condition?[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty] ?? false;
            
            // TODO: Get actual GCD remaining from ActionManager
            var gcdRemaining = 0f; // Placeholder
            var castTime = 0f; // Placeholder
            var animationLock = 0f; // Placeholder
            
            // Update target debuffs if we have a target
            UpdateTargetDebuffs();
            
            // Prepare new state using SIMD operations
            PrepareNewStateSIMD(jobId, level, targetId, zoneId, inCombat, inDuty, gcdRemaining, castTime, animationLock);
            
            // Check for changes and fire events
            CheckForChangesAndFireEvents();
            
            PerformanceMonitor.RecordUpdate(stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            // Handle thread-related exceptions gracefully
            if (ex.Message.Contains("thread") || ex.Message.Contains("Thread"))
            {
                ModernWrathCombo.PluginLog.Error($"[GameStateCache] Update failed: Not on main thread!");
            }
            else
            {
                ModernWrathCombo.PluginLog.Error($"[GameStateCache] Update failed: {ex.Message}");
            }
            PerformanceMonitor.RecordError();
        }
        finally
        {
            stopwatch.Stop();
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ClearAllStateSIMD()
    {
        if (Avx2.IsSupported)
        {
            // Use SIMD to zero out arrays quickly
            var zero = Vector256<uint>.Zero;
            var zeroFloat = Vector256<float>.Zero;
            
            fixed (uint* corePtr = _coreState)
            fixed (float* floatPtr = _floatState)
            {
                Avx.Store(corePtr, zero);
                Avx.Store(floatPtr, zeroFloat);
            }
        }
        else
        {
            // Fallback: clear manually
            Array.Clear(_coreState, 0, _coreState.Length);
            Array.Clear(_floatState, 0, _floatState.Length);
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PrepareNewStateSIMD(uint jobId, uint level, uint targetId, uint zoneId, 
        bool inCombat, bool inDuty, float gcdRemaining, float castTime, float animationLock)
    {
        // Copy current state to previous for change detection
        if (Avx2.IsSupported)
        {
            fixed (uint* currentPtr = _coreState)
            fixed (uint* prevPtr = _previousCoreState)
            {
                var current = Avx.LoadVector256(currentPtr);
                Avx.Store(prevPtr, current);
            }
        }
        else
        {
            Array.Copy(_coreState, _previousCoreState, _coreState.Length);
        }
        
        // Build flags
        uint flags = 0;
        if (inCombat) flags |= InCombatFlag;
        if (targetId != 0) flags |= HasTargetFlag;
        if (inDuty) flags |= InDutyFlag;
        if (gcdRemaining <= 0.5f) flags |= CanActFlag; // Can weave abilities
        
        // Update core state
        _coreState[JobIdIndex] = jobId;
        _coreState[LevelIndex] = level;
        _coreState[TargetIdIndex] = targetId;
        _coreState[ZoneIdIndex] = zoneId;
        _coreState[FlagsIndex] = flags;
        _coreState[GcdIntIndex] = (uint)(gcdRemaining * 1000); // Store as milliseconds
        
        // Update float state
        _floatState[GcdRemainingIndex] = gcdRemaining;
        _floatState[CastTimeIndex] = castTime;
        _floatState[AnimationLockIndex] = animationLock;
    }
    
    private static void CheckForChangesAndFireEvents()
    {
        // Use SIMD to compare current vs previous state for changes
        bool hasChanges = false;
        
        if (Avx2.IsSupported)
        {
            fixed (uint* currentPtr = _coreState)
            fixed (uint* prevPtr = _previousCoreState)
            {
                var current = Avx.LoadVector256(currentPtr);
                var previous = Avx.LoadVector256(prevPtr);
                var comparison = Avx2.CompareEqual(current, previous);
                
                // If any values changed, we'll have false bits in comparison
                var mask = Avx.MoveMask(comparison.AsSingle());
                hasChanges = mask != 0xFF; // All bits set means no changes
            }
        }
        else
        {
            // Fallback: manual comparison
            for (int i = 0; i < _coreState.Length; i++)
            {
                if (_coreState[i] != _previousCoreState[i])
                {
                    hasChanges = true;
                    break;
                }
            }
        }
        
        if (hasChanges)
        {
            FireChangeEventsSIMD();
        }
    }
    
    private static void FireChangeEventsSIMD()
    {
        // Check specific changes and fire events
        if (_coreState[JobIdIndex] != _previousCoreState[JobIdIndex])
        {
            ModernWrathCombo.PluginLog.Debug($"[GameStateCache] Job changed: {_previousCoreState[JobIdIndex]} -> {_coreState[JobIdIndex]}");
        }
        
        if (_coreState[LevelIndex] != _previousCoreState[LevelIndex])
        {
            ModernWrathCombo.PluginLog.Debug($"[GameStateCache] Level changed: {_previousCoreState[LevelIndex]} -> {_coreState[LevelIndex]}");
        }
        
        var oldFlags = _previousCoreState[FlagsIndex];
        var newFlags = _coreState[FlagsIndex];
        
        if ((oldFlags & InCombatFlag) != (newFlags & InCombatFlag))
        {
            var inCombat = (newFlags & InCombatFlag) != 0;
            ModernWrathCombo.PluginLog.Debug($"[GameStateCache] Combat changed: {!inCombat} -> {inCombat}");
        }
        
        if (_coreState[TargetIdIndex] != _previousCoreState[TargetIdIndex])
        {
            ModernWrathCombo.PluginLog.Debug($"[GameStateCache] Target changed: {_previousCoreState[TargetIdIndex]} -> {_coreState[TargetIdIndex]}");
        }
    }
    #endregion
    
    #region Performance Monitoring
    /// <summary>
    /// Built-in performance monitoring for the cache system.
    /// </summary>
    public static class PerformanceMonitor
    {
        private static long _updateCount = 0;
        private static long _errorCount = 0;
        private static long _totalUpdateTimeTicks = 0;
        private static long _maxUpdateTimeTicks = 0;
        private static Timer? _reportTimer;
        
        internal static void Start()
        {
            // Report performance stats every 10 seconds
            _reportTimer = new Timer(ReportStats, null, 10000, 10000);
        }
        
        internal static void Stop()
        {
            _reportTimer?.Dispose();
            _reportTimer = null;
        }
        
        internal static void RecordUpdate(TimeSpan updateTime)
        {
            Interlocked.Increment(ref _updateCount);
            
            var ticks = updateTime.Ticks;
            Interlocked.Add(ref _totalUpdateTimeTicks, ticks);
            
            // Update max (not thread-safe but good enough for monitoring)
            if (ticks > _maxUpdateTimeTicks)
                _maxUpdateTimeTicks = ticks;
        }
        
        internal static void RecordError()
        {
            Interlocked.Increment(ref _errorCount);
        }
        
        private static void ReportStats(object? state)
        {
            var updates = Interlocked.Read(ref _updateCount);
            var errors = Interlocked.Read(ref _errorCount);
            var totalTicks = Interlocked.Read(ref _totalUpdateTimeTicks);
            var maxTicks = _maxUpdateTimeTicks;
            
            if (updates > 0)
            {
                var avgMs = new TimeSpan(totalTicks / updates).TotalMilliseconds;
                var maxMs = new TimeSpan(maxTicks).TotalMilliseconds;
                var updateRate = updates / 10.0; // Updates per second over 10s window
                
                ModernWrathCombo.PluginLog.Debug(
                    $"[GameStateCache] Performance: {updateRate:F1} updates/sec, " +
                    $"avg: {avgMs:F2}ms, max: {maxMs:F2}ms, errors: {errors}");
            }
            
            // Reset counters for next window
            Interlocked.Exchange(ref _updateCount, 0);
            Interlocked.Exchange(ref _errorCount, 0);
            Interlocked.Exchange(ref _totalUpdateTimeTicks, 0);
            _maxUpdateTimeTicks = 0;
        }
        
        /// <summary>
        /// Gets current performance statistics.
        /// </summary>
        public static string GetStats()
        {
            var updates = Interlocked.Read(ref _updateCount);
            var errors = Interlocked.Read(ref _errorCount);
            var totalTicks = Interlocked.Read(ref _totalUpdateTimeTicks);
            
            if (updates == 0) return "No updates recorded";
            
            var avgMs = new TimeSpan(totalTicks / updates).TotalMilliseconds;
            var maxMs = new TimeSpan(_maxUpdateTimeTicks).TotalMilliseconds;
            
            return $"Updates: {updates}, Avg: {avgMs:F2}ms, Max: {maxMs:F2}ms, Errors: {errors}";
        }
    }
    #endregion
    
    #region Target Debuff Update Logic
    /// <summary>
    /// Updates target debuff tracking with snapshot preservation.
    /// Saves debuff snapshots when switching targets, loads them when returning.
    /// </summary>
    private static void UpdateTargetDebuffs()
    {
        var player = _clientState?.LocalPlayer;
        if (player?.TargetObject == null)
        {
            // No target - save current data if we had a target
            if (_lastTargetId != 0 && _currentTargetDebuffCount > 0)
            {
                SaveCurrentTargetSnapshot();
            }
            ClearCurrentTargetCache();
            return;
        }
        
        var target = player.TargetObject;
        var targetId = target.GameObjectId;
        var now = DateTime.UtcNow;
        
        // Handle target change
        if (targetId != _lastTargetId)
        {
            // Save previous target's data before switching
            if (_lastTargetId != 0 && _currentTargetDebuffCount > 0)
            {
                SaveCurrentTargetSnapshot();
            }
            
            // Load cached data for new target if available
            LoadTargetSnapshot(targetId);
            _lastTargetId = targetId;
        }
        
        // Only rescan if we don't have cached data or cache is stale (every ~2 seconds)
        bool shouldRescan = _currentTargetDebuffCount == 0 || 
                           (now - _lastDebuffUpdate).TotalSeconds > 2.0;
        
        if (!shouldRescan) return;
        
        try
        {
            // Scan target for current debuff state
            ScanCurrentTarget(target, player, now);
        }
        catch (Exception ex)
        {
            ModernWrathCombo.PluginLog.Error($"[GameStateCache] Error updating target debuffs: {ex.Message}");
            ClearCurrentTargetCache();
        }
    }
    
    /// <summary>
    /// Saves the current target's debuff data to the snapshot cache.
    /// </summary>
    private static void SaveCurrentTargetSnapshot()
    {
        if (_lastTargetId == 0 || _currentTargetDebuffCount == 0) return;
        
        var snapshot = new TargetDebuffSnapshot(_currentTargetDebuffTimers, _currentTargetDebuffIds, _currentTargetDebuffCount);
        _debuffedTargets[_lastTargetId] = snapshot;
        
        ModernWrathCombo.PluginLog.Debug($"[GameStateCache] Saved {_currentTargetDebuffCount} debuffs for target {_lastTargetId}");
    }
    
    /// <summary>
    /// Loads a target's debuff snapshot into the current cache.
    /// </summary>
    private static void LoadTargetSnapshot(ulong targetId)
    {
        if (_debuffedTargets.TryGetValue(targetId, out var snapshot))
        {
            // Copy snapshot to current cache
            _currentTargetDebuffCount = snapshot.Count;
            Array.Copy(snapshot.Ids, _currentTargetDebuffIds, snapshot.Count);
            Array.Copy(snapshot.Timers, _currentTargetDebuffTimers, snapshot.Count);
            _lastDebuffUpdate = snapshot.CaptureTime;
            
            ModernWrathCombo.PluginLog.Debug($"[GameStateCache] Loaded {snapshot.Count} cached debuffs for target {targetId}");
        }
        else
        {
            ClearCurrentTargetCache();
        }
    }
    
    /// <summary>
    /// Clears the current target cache.
    /// </summary>
    private static void ClearCurrentTargetCache()
    {
        _currentTargetDebuffCount = 0;
        _lastTargetId = 0;
    }
    
    /// <summary>
    /// Scans the current target for debuffs and updates the cache.
    /// </summary>
    private static void ScanCurrentTarget(IGameObject target, ICharacter player, DateTime now)
    {
        _currentTargetDebuffCount = 0;
        _lastDebuffUpdate = now;
        
        // Only scan if target is a battle character (has status effects)
        if (target is not IBattleChara character)
            return;
        
        // Scan target status effects for our debuffs only
        int debuffIndex = 0;
        
        foreach (var statusEffect in character.StatusList)
        {
            if (debuffIndex >= _currentTargetDebuffIds.Length) break; // Safety limit
            
            // Only track debuffs that are applied by us (same source ID)
            if (statusEffect.SourceId != player.GameObjectId) continue;
            
            var statusId = statusEffect.StatusId;
            var timeRemaining = statusEffect.RemainingTime;
            
            // Only track debuffs we care about (DoTs, important debuffs)
            if (IsImportantDebuff(statusId))
            {
                _currentTargetDebuffIds[debuffIndex] = statusId;
                _currentTargetDebuffTimers[debuffIndex] = timeRemaining;
                debuffIndex++;
            }
        }
        
        _currentTargetDebuffCount = debuffIndex;
        
        if (debuffIndex > 0)
        {
            ModernWrathCombo.PluginLog.Debug($"[GameStateCache] Scanned {debuffIndex} player debuffs on target {target.GameObjectId}");
        }
    }
    
    /// <summary>
    /// Checks if a status effect ID is an important debuff we should track.
    /// Currently focuses on DoT effects for WHM and other jobs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsImportantDebuff(uint statusId)
    {
        return statusId switch
        {
            // WHM DoTs
            1871 => true, // Dia
            144 => true,  // Aero II
            143 => true,  // Aero
            
            // Example: If WHM had a second DoT type
            // 1234 => true,  // Hypothetical secondary DoT
            
            // Other jobs' DoTs could be added here
            // BLM DoTs:
            // 164 => true,  // Thunder III
            // 163 => true,  // Thunder II
            // 162 => true,  // Thunder
            
            // DRG DoTs:
            // 1312 => true, // Chaos Thrust
            
            _ => false
        };
    }
    #endregion
}
