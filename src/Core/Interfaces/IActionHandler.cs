using System;
using ModernWrathCombo.Core.Data;

namespace ModernWrathCombo.Core.Interfaces;

/// <summary>
/// Minimal interface for action handlers.
/// Keep this as simple as possible for maximum performance.
/// </summary>
public interface IActionHandler
{
    /// <summary>
    /// Execute the combo logic with full game state and return the resolved action ID.
    /// This method must be extremely fast (<50ns target).
    /// </summary>
    /// <param name="originalActionId">The action that was originally pressed</param>
    /// <param name="gameState">Current game state information</param>
    /// <param name="targetEffects">Status effects on the current target</param>
    /// <param name="playerEffects">Status effects on the player</param>
    /// <param name="actionStates">Current action cooldown states</param>
    /// <returns>The action ID to execute instead (or same if no change)</returns>
    uint Execute(uint originalActionId, GameStateData gameState, ReadOnlySpan<StatusEffect> targetEffects, 
                ReadOnlySpan<StatusEffect> playerEffects, ReadOnlySpan<ActionState> actionStates);

    /// <summary>
    /// Legacy execute method for simple action resolution without game state.
    /// This method must be extremely fast (<50ns target).
    /// </summary>
    /// <param name="originalActionId">The action that was originally pressed</param>
    /// <returns>The action ID to execute instead (or same if no change)</returns>
    uint Execute(uint originalActionId);
}
