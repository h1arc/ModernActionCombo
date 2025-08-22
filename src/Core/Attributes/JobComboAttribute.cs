using System;

namespace ModernActionCombo.Core.Attributes;

/// <summary>
/// Attribute to mark classes as job combo implementations.
/// Used by JobComboRegistry for automatic discovery and registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class JobComboAttribute : Attribute
{
    /// <summary>
    /// The FFXIV job ID this combo implementation is for.
    /// </summary>
    public uint JobId { get; }
    
    /// <summary>
    /// Display name for the job (optional).
    /// </summary>
    public string? JobName { get; set; }
    
    /// <summary>
    /// Whether this job supports actual combo processing or just display.
    /// </summary>
    public bool SupportsComboProcessing { get; set; } = true;
    
    public JobComboAttribute(uint jobId)
    {
        JobId = jobId;
    }
}
