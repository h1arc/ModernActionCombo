using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Centralized configuration policy manager.
/// Single source of truth for all configuration enabling/disabling logic.
/// </summary>
public static class ConfigurationPolicy
{
    /// <summary>
    /// Sets the complete configuration policy for a job - determines what should be enabled by default.
    /// This is the ONLY place where default behavior is defined.
    /// </summary>
    public static void ApplyDefaultPolicy(uint jobId)
    {
        switch (jobId)
        {
            case 24: // White Mage
                ApplyWHMDefaults();
                break;
            case 21: // Warrior  
                ApplyWARDefaults();
                break;
            case 25: // Black Mage
                ApplyBLMDefaults();
                break;
            default:
                // Unknown job - apply completely opt-in policy (nothing enabled)
                ApplyOptInPolicy(jobId);
                break;
        }
        
        ModernActionCombo.PluginLog?.Info($"📋 Applied default configuration policy for job {jobId}");
    }
    
    /// <summary>
    /// Completely resets a job's configuration to the default policy.
    /// This is the ONLY place reset logic should exist.
    /// </summary>
    public static void ResetToDefaults(uint jobId)
    {
        var config = ConfigurationManager.GetJobConfiguration(jobId);
        config.JobSettings.Clear();
        
        // Reapply the default policy
        ApplyDefaultPolicy(jobId);
        
        // Increment config version for cache invalidation
        ConfigAwareActionCache.IncrementConfigVersion();
        
        var jobName = GetJobName(jobId);
        ModernActionCombo.PluginLog?.Info($"🔄 Reset {jobName} configuration to policy defaults");
    }
    
    /// <summary>
    /// WHM default policy - currently completely opt-in (nothing enabled by default).
    /// Change this method to modify WHM's default behavior.
    /// </summary>
    private static void ApplyWHMDefaults()
    {
        // Currently: Complete opt-in policy
        // Combo grids: Disabled by default - user must enable
        // Combo rules: Disabled by default - user must enable  
        // oGCD rules: Disabled by default - user must enable
        
        // If we wanted to enable things by default, we would do:
        // ConfigurationManager.SetComboGridEnabled(24, "Single Target DPS", true);
        // ConfigurationManager.SetOGCDRuleEnabled(24, "Lucid Dreaming", true);
        
        // But for now, everything is opt-in for maximum user control
    }
    
    /// <summary>
    /// Warrior default policy - currently completely opt-in.
    /// </summary>
    private static void ApplyWARDefaults()
    {
        // Complete opt-in policy - nothing enabled by default
    }
    
    /// <summary>
    /// Black Mage default policy - currently completely opt-in.
    /// </summary>
    private static void ApplyBLMDefaults()
    {
        // Complete opt-in policy - nothing enabled by default
    }
    
    /// <summary>
    /// Generic opt-in policy for unknown jobs.
    /// </summary>
    private static void ApplyOptInPolicy(uint jobId)
    {
        // Nothing enabled by default - complete user control
    }
    
    /// <summary>
    /// Gets a human-readable job name for logging.
    /// </summary>
    private static string GetJobName(uint jobId)
    {
        return jobId switch
        {
            24 => "White Mage",
            21 => "Warrior", 
            25 => "Black Mage",
            _ => $"Job {jobId}"
        };
    }
    
    /// <summary>
    /// Policy query: Should combo grids be enabled by default for this job?
    /// Currently returns false for all jobs (opt-in policy).
    /// </summary>
    public static bool ShouldEnableComboGridsByDefault(uint jobId) => false;
    
    /// <summary>
    /// Policy query: Should oGCD rules be enabled by default for this job?
    /// Currently returns false for all jobs (opt-in policy).
    /// </summary>
    public static bool ShouldEnableOGCDRulesByDefault(uint jobId) => false;
    
    /// <summary>
    /// Policy query: Should individual combo rules be enabled by default for this job?
    /// Currently returns false for all jobs (opt-in policy).
    /// </summary>
    public static bool ShouldEnableComboRulesByDefault(uint jobId) => false;
}
