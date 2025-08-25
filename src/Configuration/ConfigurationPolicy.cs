using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Enums;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// Centralized configuration policy manager.
/// Single source of truth for all configuration enabling/disabling logic.
/// </summary>
/// <remarks>
/// Scales to all jobs via a compact dispatch table. Per-job default implementations
/// live in partial files (ConfigurationPolicy.{JOB}.cs) to keep this core file small.
/// </remarks>
public static partial class ConfigurationPolicy
{
    // Fast O(1) dispatch by JobID (0..42). Null => apply opt-in policy.
    private static readonly Action?[] _applyDefaultsByJob = BuildPolicyDispatch();

    private static Action?[] BuildPolicyDispatch()
    {
        // Max enum value currently 42 (PCT). Use a dense array for zero-allocation lookups.
        var arr = new Action?[43];

        // Healers
        arr[(int)JobID.WHM] = ApplyWHMDefaults;

        // Tanks
        arr[(int)JobID.WAR] = ApplyWARDefaults;

        // Magical DPS
        arr[(int)JobID.BLM] = ApplyBLMDefaults;

        return arr;
    }

    /// <summary>
    /// Sets the complete configuration policy for a job - determines what should be enabled by default.
    /// This is the ONLY place where default behavior is defined.
    /// </summary>
    public static void ApplyDefaultPolicy(uint jobId)
    {
        var table = _applyDefaultsByJob;
        if (jobId < (uint)table.Length)
        {
            var action = table[(int)jobId];
            if (action is not null)
                action();
            else
                ApplyOptInPolicy(jobId);
        }
        else
        {
            ApplyOptInPolicy(jobId);
        }

    Logger.Info($"📋 Applied default configuration policy for job {JobHelper.GetJobName(jobId)} ({jobId})");
    }
    
    /// <summary>
    /// Enum-friendly overload.
    /// </summary>
    public static void ApplyDefaultPolicy(JobID job) => ApplyDefaultPolicy((uint)job);
    
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
        
        var jobName = JobHelper.GetJobName(jobId);
        ModernActionCombo.PluginLog?.Info($"🔄 Reset {jobName} configuration to policy defaults");
    }
    
    /// <summary>
    /// Enum-friendly overload.
    /// </summary>
    public static void ResetToDefaults(JobID job) => ResetToDefaults((uint)job);
    
    /// <summary>
    /// WHM default policy - currently completely opt-in (nothing enabled by default).
    /// Change this method to modify WHM's default behavior.
    /// </summary>
    // Per-job defaults live in partials to keep this file compact.
    
    /// <summary>
    /// Warrior default policy - currently completely opt-in.
    /// </summary>
    // See ConfigurationPolicy.WAR.cs
    
    /// <summary>
    /// Black Mage default policy - currently completely opt-in.
    /// </summary>
    // See ConfigurationPolicy.BLM.cs
    
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
    // Removed in favor of JobHelper.GetJobName(jobId)
    
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
