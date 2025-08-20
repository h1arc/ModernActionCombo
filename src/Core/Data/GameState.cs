using System;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;

namespace ModernWrathCombo.Core.Data;

/// <summary>
/// Game state manager for tracking current game information.
/// Provides real-time updates of job, combat state, and other game data.
/// </summary>
public class GameState
{
    private readonly IClientState? _clientState;
    private readonly ICondition? _condition;

    // Current game state
    public uint CurrentJob { get; private set; } = 0;
    public uint Level { get; private set; } = 1;
    public bool InCombat { get; private set; } = false;
    public uint CurrentTarget { get; private set; } = 0;
    public float GlobalCooldownRemaining { get; private set; } = 0f;

    // Derived properties for convenience
    public bool HasTarget => CurrentTarget != 0;
    public bool CanUseAbility => GlobalCooldownRemaining <= 0.5f;

    /// <summary>
    /// Creates a default GameState with no job active.
    /// </summary>
    public GameState()
    {
        // Default constructor for dependency injection
    }

    /// <summary>
    /// Creates a GameState connected to Dalamud services.
    /// </summary>
    public GameState(IClientState clientState, ICondition condition)
    {
        _clientState = clientState;
        _condition = condition;
    }

    /// <summary>
    /// Updates game state from Dalamud services.
    /// Call this regularly to keep state current.
    /// </summary>
    public void Update()
    {
        if (_clientState?.LocalPlayer != null)
        {
            CurrentJob = _clientState.LocalPlayer.ClassJob.RowId;
            Level = _clientState.LocalPlayer.Level;
            CurrentTarget = (uint)(_clientState.LocalPlayer.TargetObject?.GameObjectId ?? 0);
        }

        if (_condition != null)
        {
            InCombat = _condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];
        }

        // TODO: Get actual GCD remaining from game state
        GlobalCooldownRemaining = 0f;
    }

    /// <summary>
    /// Updates the current job information.
    /// </summary>
    public void UpdateJob(uint jobId, uint level)
    {
        CurrentJob = jobId;
        Level = level;
    }

    /// <summary>
    /// Updates combat state information.
    /// </summary>
    public void UpdateCombat(bool inCombat, uint currentTarget, float gcdRemaining)
    {
        InCombat = inCombat;
        CurrentTarget = currentTarget;
        GlobalCooldownRemaining = gcdRemaining;
    }

    /// <summary>
    /// Creates a GameStateData snapshot for use with action handlers.
    /// </summary>
    public GameStateData CreateSnapshot()
    {
        return new GameStateData(CurrentJob, Level, InCombat, CurrentTarget, GlobalCooldownRemaining);
    }
}

/// <summary>
/// High-performance game state data for action resolution.
/// Uses readonly structs and aggressive inlining for maximum speed.
/// </summary>
public readonly ref struct GameStateData
{
    public readonly uint JobId;
    public readonly uint Level;
    public readonly bool InCombat;
    public readonly uint CurrentTarget;
    public readonly float GlobalCooldownRemaining;

    public GameStateData(uint jobId, uint level, bool inCombat, uint currentTarget, float gcdRemaining)
    {
        JobId = jobId;
        Level = level;
        InCombat = inCombat;
        CurrentTarget = currentTarget;
        GlobalCooldownRemaining = gcdRemaining;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanUseAbility() => GlobalCooldownRemaining <= 0.5f; // Can weave if <0.5s GCD remaining

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidTarget() => CurrentTarget != 0;
}

/// <summary>
/// Status effect information for DoT/buff tracking.
/// </summary>
public readonly struct StatusEffect
{
    public readonly uint Id;
    public readonly float RemainingDuration;
    public readonly uint StackCount;
    public readonly uint SourceId; // Who applied this effect

    public StatusEffect(uint id, float duration, uint stacks = 1, uint sourceId = 0)
    {
        Id = id;
        RemainingDuration = duration;
        StackCount = stacks;
        SourceId = sourceId;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsExpiringSoon(float threshold) => RemainingDuration <= threshold;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsActive() => RemainingDuration > 0;
}

/// <summary>
/// Action information and cooldown state.
/// </summary>
public readonly struct ActionState
{
    public readonly uint Id;
    public readonly float CooldownRemaining;
    public readonly uint MaxCharges;
    public readonly uint CurrentCharges;

    public ActionState(uint id, float cooldown, uint maxCharges = 1, uint currentCharges = 1)
    {
        Id = id;
        CooldownRemaining = cooldown;
        MaxCharges = maxCharges;
        CurrentCharges = currentCharges;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsReady() => CooldownRemaining <= 0 && CurrentCharges > 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsOnCooldown() => CooldownRemaining > 0;
}
