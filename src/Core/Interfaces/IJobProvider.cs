using System;
using System.Collections.Generic;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Core.Interfaces;

/// <summary>
/// Core interface that all job providers must implement.
/// Keeps only the essential methods, delegates specifics to composition interfaces.
/// </summary>
public interface IJobProvider
{
    /// <summary>
    /// Initialize any job-specific tracking needed.
    /// </summary>
    void InitializeTracking();
    
    /// <summary>
    /// Get display information for the job (shown in debug UI).
    /// </summary>
    string GetJobDisplayInfo();
}

/// <summary>
/// Optional interface for jobs that support combo processing.
/// Implement this if your job has combo grids.
/// </summary>
public interface IComboProvider
{
    /// <summary>
    /// Get all combo grids for this job.
    /// </summary>
    IReadOnlyList<ComboGrid> GetComboGrids();
}

/// <summary>
/// Optional interface for jobs that have gauge management.
/// Implement this if your job has a job gauge.
/// </summary>
public interface IGaugeProvider
{
    /// <summary>
    /// Update gauge data for this job.
    /// </summary>
    void UpdateGauge();
    
    /// <summary>
    /// Get first gauge data value.
    /// </summary>
    uint GetGaugeData1();
    
    /// <summary>
    /// Get second gauge data value.
    /// </summary>
    uint GetGaugeData2();
    
    /// <summary>
    /// Get debug information about the current gauge state.
    /// </summary>
    string GetGaugeDebugInfo();
}

/// <summary>
/// Optional interface for jobs that need custom tracking data.
/// Implement this if your job tracks specific debuffs/buffs/cooldowns.
/// </summary>
public interface ITrackingProvider
{
    /// <summary>
    /// Get debuff IDs that should be tracked on targets for this job.
    /// </summary>
    uint[] GetDebuffsToTrack();
    
    /// <summary>
    /// Get buff IDs that should be tracked on the player for this job.
    /// </summary>
    uint[] GetBuffsToTrack();
    
    /// <summary>
    /// Get action IDs that should have cooldowns tracked for this job.
    /// </summary>
    uint[] GetCooldownsToTrack();
}

/// <summary>
/// Extension methods to safely check for optional interfaces.
/// </summary>
public static class JobProviderExtensions
{
    public static bool HasComboSupport(this IJobProvider provider) => provider is IComboProvider;
    public static bool HasGaugeSupport(this IJobProvider provider) => provider is IGaugeProvider;
    public static bool HasTrackingSupport(this IJobProvider provider) => provider is ITrackingProvider;
    
    public static IComboProvider? AsComboProvider(this IJobProvider provider) => provider as IComboProvider;
    public static IGaugeProvider? AsGaugeProvider(this IJobProvider provider) => provider as IGaugeProvider;
    public static ITrackingProvider? AsTrackingProvider(this IJobProvider provider) => provider as ITrackingProvider;
}

/// <summary>
/// A rotation grid containing priority rules for specific actions.
/// Represents a single rotation context (e.g., single target, AoE, opener).
/// </summary>
public readonly struct ComboGrid
{
    public readonly string Name;
    public readonly uint[] TriggerActions;
    public readonly PriorityRule[] Rules;
    
    public ComboGrid(string name, uint[] triggers, PriorityRule[] rules)
    {
        Name = name;
        TriggerActions = triggers;
        Rules = rules;
    }
    
    /// <summary>
    /// Checks if this grid handles the specified action.
    /// </summary>
    public bool HandlesAction(uint actionId) => Array.IndexOf(TriggerActions, actionId) >= 0;
    
    /// <summary>
    /// Evaluates the priority rules for the given game state.
    /// Returns the first matching rule's action, or the original action if no rules match.
    /// </summary>
    public uint Evaluate(uint originalActionId, GameStateData gameState)
    {
        for (int i = 0; i < Rules.Length; i++)
        {
            var rule = Rules[i];
            
            try
            {
                if (rule.Condition(gameState))
                {
                    return rule.GetResultAction(gameState);
                }
            }
            catch
            {
                // Silent failure - continue to next rule
            }
        }
        
        return originalActionId; // Fallback if no rules match
    }
}

/// <summary>
/// A single priority rule in a rotation grid.
/// Uses implicit tuple conversion for clean syntax.
/// Supports both static action IDs and dynamic action resolution functions.
/// </summary>
public readonly struct PriorityRule
{
    public readonly Func<GameStateData, bool> Condition;
    public readonly Func<GameStateData, uint> ActionResolver;
    public readonly string Description;
    
    public PriorityRule(Func<GameStateData, bool> condition, uint action, string desc = "")
    {
        Condition = condition;
        ActionResolver = _ => action; // Static action
        Description = desc;
    }
    
    public PriorityRule(Func<GameStateData, bool> condition, Func<GameStateData, uint> actionResolver, string desc = "")
    {
        Condition = condition;
        ActionResolver = actionResolver;
        Description = desc;
    }
    
    /// <summary>
    /// Gets the result action for the given game state.
    /// </summary>
    public uint GetResultAction(GameStateData gameState) => ActionResolver(gameState);
    
    /// <summary>
    /// Implicit conversion from tuple with static action ID.
    /// Enables syntax: (condition, actionId, "description")
    /// </summary>
    public static implicit operator PriorityRule((Func<GameStateData, bool> condition, uint action, string desc) tuple)
        => new(tuple.condition, tuple.action, tuple.desc);
    
    /// <summary>
    /// Implicit conversion from tuple with dynamic action resolver.
    /// Enables syntax: (condition, actionResolverFunction, "description")
    /// </summary>
    public static implicit operator PriorityRule((Func<GameStateData, bool> condition, Func<GameStateData, uint> actionResolver, string desc) tuple)
        => new(tuple.condition, tuple.actionResolver, tuple.desc);
    
    /// <summary>
    /// Implicit conversion from tuple without description.
    /// Enables syntax: (condition, action) or (condition, actionResolver)
    /// </summary>
    public static implicit operator PriorityRule((Func<GameStateData, bool> condition, uint action) tuple)
        => new(tuple.condition, tuple.action, "");
    
    /// <summary>
    /// Implicit conversion from tuple with action resolver, no description.
    /// </summary>
    public static implicit operator PriorityRule((Func<GameStateData, bool> condition, Func<GameStateData, uint> actionResolver) tuple)
        => new(tuple.condition, tuple.actionResolver, "");
}
