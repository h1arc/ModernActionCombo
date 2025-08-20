using System;
using System.Runtime.CompilerServices;
using ModernWrathCombo.Core.Data;

namespace ModernWrathCombo.Core.Abstracts;

/// <summary>
/// Base class for all custom combo implementations.
/// Handles action interception and replacement logic.
/// </summary>
public abstract class CustomCombo
{
    /// <summary>
    /// The original action ID that this combo intercepts.
    /// For example, Stone III (ID: 25) intercepts to provide optimal WHM DPS rotation.
    /// </summary>
    public abstract uint InterceptedAction { get; }

    /// <summary>
    /// Determines if this combo should be active based on current conditions.
    /// Override this to add job checks, level requirements, etc.
    /// </summary>
    public virtual bool ShouldActivate(GameStateData gameState) => true;

    /// <summary>
    /// The core combo logic that determines what action to return.
    /// This is called when the intercepted action is pressed.
    /// </summary>
    /// <param name="originalAction">The action that was originally pressed</param>
    /// <param name="gameState">Current game state data</param>
    /// <returns>The action ID to execute instead</returns>
    public abstract uint Invoke(uint originalAction, GameStateData gameState);

    /// <summary>
    /// Tries to invoke this combo for the given action.
    /// </summary>
    /// <param name="actionId">The action being pressed</param>
    /// <param name="gameState">Current game state</param>
    /// <param name="replacementAction">The action to use instead (if any)</param>
    /// <returns>True if this combo handled the action</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryInvoke(uint actionId, GameStateData gameState, out uint replacementAction)
    {
        if (actionId == InterceptedAction && ShouldActivate(gameState))
        {
            replacementAction = Invoke(actionId, gameState);
            return replacementAction != actionId; // Only replace if different
        }

        replacementAction = actionId;
        return false;
    }
}
