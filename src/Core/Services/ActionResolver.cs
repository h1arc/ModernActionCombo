using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ModernWrathCombo.Core.Interfaces;
using ModernWrathCombo.Core.Data;

namespace ModernWrathCombo.Core.Services;

/// <summary>
/// Ultra-fast action resolver using dictionary lookups.
/// Target: <50ns resolution time with zero allocations.
/// </summary>
public sealed class ActionResolver
{
    private readonly Dictionary<uint, IActionHandler> _handlers = new();

    /// <summary>
    /// Register an action handler for specific action IDs.
    /// Call during initialization only.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // Keep initialization cold
    public void RegisterHandler(uint actionId, IActionHandler handler)
    {
        _handlers[actionId] = handler;
    }

    /// <summary>
    /// Register an action handler for multiple action IDs.
    /// Useful for combo families (Stone, Stone2, Glare, etc.)
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)] // Keep initialization cold
    public void RegisterHandler(ReadOnlySpan<uint> actionIds, IActionHandler handler)
    {
        foreach (var actionId in actionIds)
        {
            _handlers[actionId] = handler;
        }
    }

    /// <summary>
    /// Resolve an action with full game state context.
    /// This is the hot path - must be <50ns.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Resolve(uint originalActionId, GameStateData gameState, ReadOnlySpan<StatusEffect> targetEffects, 
                       ReadOnlySpan<StatusEffect> playerEffects, ReadOnlySpan<ActionState> actionStates)
    {
        // Fast dictionary lookup - O(1) average case
        if (_handlers.TryGetValue(originalActionId, out var handler))
        {
            return handler.Execute(originalActionId, gameState, targetEffects, playerEffects, actionStates);
        }

        // No handler registered, return original action
        return originalActionId;
    }

    /// <summary>
    /// Get the next action for a specific job (for UI debugging).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint GetNextAction(uint jobId)
    {
        // Create minimal test game state for the job
        var gameState = new GameStateData(jobId, 90, true, 1, 0f);
        
        // For WHM, test with Glare3 as the common action
        if (jobId == 24) // WHM job ID
        {
            return Resolve(25842, gameState, ReadOnlySpan<StatusEffect>.Empty, 
                          ReadOnlySpan<StatusEffect>.Empty, ReadOnlySpan<ActionState>.Empty);
        }

        return 0; // No action for unknown jobs
    }

    /// <summary>
    /// Legacy resolve method for simple action resolution.
    /// This is the hot path - must be <50ns.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint Resolve(uint originalActionId)
    {
        // Fast dictionary lookup - O(1) average case
        if (_handlers.TryGetValue(originalActionId, out var handler))
        {
            return handler.Execute(originalActionId);
        }

        // No handler registered, return original action
        return originalActionId;
    }

    /// <summary>
    /// Bulk resolve multiple actions with full game state.
    /// More efficient than individual calls for batch processing.
    /// </summary>
    public void ResolveBatch(ReadOnlySpan<uint> originalActions, Span<uint> resolvedActions, 
                           GameStateData gameState, ReadOnlySpan<StatusEffect> targetEffects, 
                           ReadOnlySpan<StatusEffect> playerEffects, ReadOnlySpan<ActionState> actionStates)
    {
        if (originalActions.Length != resolvedActions.Length)
            throw new ArgumentException("Input and output spans must have the same length");

        for (int i = 0; i < originalActions.Length; i++)
        {
            resolvedActions[i] = Resolve(originalActions[i], gameState, targetEffects, playerEffects, actionStates);
        }
    }

    /// <summary>
    /// Legacy bulk resolve without game state.
    /// </summary>
    public void ResolveBatch(ReadOnlySpan<uint> originalActions, Span<uint> resolvedActions)
    {
        if (originalActions.Length != resolvedActions.Length)
            throw new ArgumentException("Input and output spans must have the same length");

        for (int i = 0; i < originalActions.Length; i++)
        {
            resolvedActions[i] = Resolve(originalActions[i]);
        }
    }

    /// <summary>
    /// Clear all registered handlers (for testing/reinitialization).
    /// </summary>
    public void ClearHandlers() => _handlers.Clear();

    /// <summary>
    /// Get the number of registered handlers (for diagnostics).
    /// </summary>
    public int HandlerCount => _handlers.Count;
}
