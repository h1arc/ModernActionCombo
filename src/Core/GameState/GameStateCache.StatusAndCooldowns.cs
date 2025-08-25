using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ModernActionCombo.Core.Data;

public static unsafe partial class GameStateCache
{
    #region Extended Game State Methods

    /// <summary>Get remaining time on target debuff in seconds. Returns 0 if not present, UNINITIALIZED_SENTINEL if not yet tracked.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetTargetDebuffTimeRemaining(uint debuffId)
    {
        if (_targetDebuffsExpiry.TryGetValue(debuffId, out var expiry))
        {
            if (expiry == UNINITIALIZED_TICKS) return UNINITIALIZED_SENTINEL;
            var now = Environment.TickCount64;
            var remaining = (expiry - now) / 1000.0f;
            return remaining > 0 ? remaining : 0.0f;
        }
        return 0.0f;
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
    public static bool IsDebuffTrackingInitialized(uint debuffId) => _targetDebuffsExpiry.ContainsKey(debuffId);
    
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
        if (_playerBuffsExpiry.TryGetValue(buffId, out var expiry))
        {
            if (expiry == UNINITIALIZED_TICKS) return UNINITIALIZED_SENTINEL;
            var now = Environment.TickCount64;
            var remaining = (expiry - now) / 1000.0f;
            return remaining > 0 ? remaining : 0.0f;
        }
        return 0.0f;
    }
    
    /// <summary>Check if buff tracking has been initialized for the specified buff.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsBuffTrackingInitialized(uint buffId) => _playerBuffsExpiry.ContainsKey(buffId);
    
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
        if (_actionCooldownsExpiry.TryGetValue(actionId, out var expiry))
        {
            if (expiry == UNINITIALIZED_TICKS) return UNINITIALIZED_SENTINEL;
            var now = Environment.TickCount64;
            var remaining = (expiry - now) / 1000.0f;
            return remaining > 0 ? remaining : 0.0f;
        }
        return 0.0f;
    }
    
    /// <summary>Check if cooldown tracking has been initialized for the specified action.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCooldownTrackingInitialized(uint actionId) => _actionCooldownsExpiry.ContainsKey(actionId);
    
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
        var now = Environment.TickCount64;
        // Ensure any incoming buffs are tracked (no-op if already present)
        foreach (var kv in buffs)
        {
            _playerBuffsExpiry.TryAdd(kv.Key, UNINITIALIZED_TICKS);
        }

        // Copy keys to a pooled array to avoid concurrent modification issues without GC allocations
        int count = _playerBuffsExpiry.Count;
        var pool = ArrayPool<uint>.Shared;
        var keysArray = pool.Rent(count);
        try
        {
            _playerBuffsExpiry.Keys.CopyTo(keysArray, 0);
            var keys = keysArray.AsSpan(0, count);
            var nowTicks = Environment.TickCount64;
            foreach (var buffId in keys)
            {
                if (buffs.TryGetValue(buffId, out var remainingSec))
                {
                    ref var valRef = ref CollectionsMarshal.GetValueRefOrNullRef(_playerBuffsExpiry, buffId);
                    valRef = remainingSec == UNINITIALIZED_SENTINEL ? UNINITIALIZED_TICKS : nowTicks + (long)(remainingSec * 1000.0f);
                }
                else
                {
                    ref var valRef = ref CollectionsMarshal.GetValueRefOrNullRef(_playerBuffsExpiry, buffId);
                    if (valRef != UNINITIALIZED_TICKS)
                        valRef = nowTicks; // expired => remaining 0
                }
            }
        }
        finally
        {
            pool.Return(keysArray);
        }
    }
    
    /// <summary>
    /// Updates target debuffs. Call this when target or debuff state changes.
    /// </summary>
    public static void UpdateTargetDebuffs(Dictionary<uint, float> debuffs)
    {
        var now = Environment.TickCount64;
        // Ensure any incoming debuffs are tracked (no-op if already present)
        foreach (var kv in debuffs)
        {
            _targetDebuffsExpiry.TryAdd(kv.Key, UNINITIALIZED_TICKS);
        }

        int count = _targetDebuffsExpiry.Count;
        var pool = ArrayPool<uint>.Shared;
        var keysArray = pool.Rent(count);
        try
        {
            _targetDebuffsExpiry.Keys.CopyTo(keysArray, 0);
            var keys = keysArray.AsSpan(0, count);
            var nowTicks = Environment.TickCount64;
            foreach (var debuffId in keys)
            {
                if (debuffs.TryGetValue(debuffId, out var remainingSec))
                {
                    ref var valRef = ref CollectionsMarshal.GetValueRefOrNullRef(_targetDebuffsExpiry, debuffId);
                    valRef = remainingSec == UNINITIALIZED_SENTINEL ? UNINITIALIZED_TICKS : nowTicks + (long)(remainingSec * 1000.0f);
                }
                else
                {
                    ref var valRef = ref CollectionsMarshal.GetValueRefOrNullRef(_targetDebuffsExpiry, debuffId);
                    if (valRef != UNINITIALIZED_TICKS)
                        valRef = nowTicks; // expired => remaining 0
                }
            }
        }
        finally
        {
            pool.Return(keysArray);
        }
    }
    
    /// <summary>
    /// Updates action cooldowns. Call this when cooldown state changes.
    /// </summary>
    public static void UpdateActionCooldowns(Dictionary<uint, float> cooldowns)
    {
        var now = Environment.TickCount64;
        // Ensure any incoming cooldowns are tracked (no-op if already present)
        foreach (var kv in cooldowns)
        {
            _actionCooldownsExpiry.TryAdd(kv.Key, UNINITIALIZED_TICKS);
        }

        int count = _actionCooldownsExpiry.Count;
        var pool = ArrayPool<uint>.Shared;
        var keysArray = pool.Rent(count);
        try
        {
            _actionCooldownsExpiry.Keys.CopyTo(keysArray, 0);
            var keys = keysArray.AsSpan(0, count);
            var nowTicks = Environment.TickCount64;
            foreach (var actionId in keys)
            {
                if (cooldowns.TryGetValue(actionId, out var remainingSec))
                {
                    ref var valRef = ref CollectionsMarshal.GetValueRefOrNullRef(_actionCooldownsExpiry, actionId);
                    valRef = remainingSec == UNINITIALIZED_SENTINEL ? UNINITIALIZED_TICKS : nowTicks + (long)(remainingSec * 1000.0f);
                }
                else
                {
                    ref var valRef = ref CollectionsMarshal.GetValueRefOrNullRef(_actionCooldownsExpiry, actionId);
                    if (valRef != UNINITIALIZED_TICKS)
                        valRef = nowTicks; // finished => remaining 0
                }
            }
        }
        finally
        {
            pool.Return(keysArray);
        }
    }

    #endregion
}
