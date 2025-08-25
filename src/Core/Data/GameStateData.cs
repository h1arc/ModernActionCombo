using System.Runtime.CompilerServices;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// High-performance game state data for action resolution.
/// Uses readonly ref struct to avoid heap allocations in hot paths.
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
