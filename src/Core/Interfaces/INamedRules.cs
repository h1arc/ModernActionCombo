using System.Collections.Generic;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Core.Interfaces;

/// <summary>
/// Represents a named combo rule with configuration support.
/// Used for individual rule enable/disable in combo grids.
/// </summary>
public readonly struct NamedComboRule
{
    public readonly string Name;
    public readonly PriorityRule Rule;
    
    public NamedComboRule(string name, PriorityRule rule)
    {
        Name = name;
        Rule = rule;
    }
}

/// <summary>
/// Represents a named oGCD rule with configuration support.
/// Used for individual oGCD enable/disable.
/// </summary>
public readonly struct NamedOGCDRule
{
    public readonly string Name;
    public readonly OGCDResolver.DirectCacheOGCDRule Rule;
    
    public NamedOGCDRule(string name, OGCDResolver.DirectCacheOGCDRule rule)
    {
        Name = name;
        Rule = rule;
    }
}

/// <summary>
/// Represents a named smart target rule with configuration support.
/// Used for individual smart target rule enable/disable.
/// </summary>
public readonly struct NamedSmartTargetRule
{
    public readonly string Name;
    public readonly SmartTargetRule Rule;
    
    public NamedSmartTargetRule(string name, SmartTargetRule rule)
    {
        Name = name;
        Rule = rule;
    }
}

/// <summary>
/// Interface for providers that support named combo rule configuration.
/// Allows individual combo rules to be enabled/disabled.
/// </summary>
public interface INamedComboRulesProvider
{
    /// <summary>
    /// Gets all named combo rules for this job, organized by combo grid.
    /// </summary>
    IReadOnlyDictionary<string, IReadOnlyList<NamedComboRule>> GetNamedComboRules();
    
    /// <summary>
    /// Refreshes the combo rules when configuration changes.
    /// Called by the configuration system when individual rules are enabled/disabled.
    /// </summary>
    void RefreshComboRules();
}

/// <summary>
/// Interface for providers that support named oGCD rule configuration.
/// Allows individual oGCD rules to be enabled/disabled.
/// </summary>
public interface INamedOGCDRulesProvider
{
    /// <summary>
    /// Gets all named oGCD rules for this job.
    /// </summary>
    IReadOnlyList<NamedOGCDRule> GetNamedOGCDRules();
    
    /// <summary>
    /// Refreshes the oGCD rules when configuration changes.
    /// Called by the configuration system when oGCD rules are enabled/disabled.
    /// </summary>
    void RefreshOGCDRules();
}

/// <summary>
/// Interface for providers that support named smart target rule configuration.
/// Allows individual smart target rules to be enabled/disabled.
/// </summary>
public interface INamedSmartTargetRulesProvider
{
    /// <summary>
    /// Gets all named smart target rules for this job.
    /// </summary>
    IReadOnlyList<NamedSmartTargetRule> GetNamedSmartTargetRules();
    
    /// <summary>
    /// Refreshes the smart target rules when configuration changes.
    /// Called by the configuration system when smart target rules are enabled/disabled.
    /// </summary>
    void RefreshSmartTargetRules();
}
