using System;

namespace ModernActionCombo.Core.Data;

public static unsafe partial class GameStateCache
{
    #region Job Gauge Updates
    
    /// <summary>
    /// Updates job gauge data in the cache based on current job.
    /// Only updates when the current job matches, preventing unnecessary updates.
    /// </summary>
    public static void UpdateJobGauge(uint jobId, uint gaugeData1, uint gaugeData2)
    {
        // Only update if this matches the current job
        if (JobId != jobId) 
            return;
        
        // Coalesce: skip if values unchanged to avoid dirtying cache lines
        if (Lane(GaugeData1Index) == gaugeData1 && Lane(GaugeData2Index) == gaugeData2)
            return;

        // Write lanes directly
        Lane(GaugeData1Index) = gaugeData1;
        Lane(GaugeData2Index) = gaugeData2;
    }
    
    /// <summary>
    /// Updates WHM gauge data (convenience method).
    /// Called by WHMProvider through the registry system.
    /// </summary>
    public static void UpdateWHMGauge(byte healingLilies, uint lilyTimer, byte bloodLily)
    {
        // No job check needed - registry ensures only active job calls this
        
        // Pack lily data into GaugeData1: [bloodLily:8][healingLilies:8][reserved:16]
        var gaugeData1 = (uint)((bloodLily << 8) | healingLilies);
        UpdateJobGauge(JobId, gaugeData1, lilyTimer);
    }
    
    /// <summary>
    /// Example: Updates BLM gauge data (convenience method).
    /// Only updates when current job is BLM (25) or THM (7).
    /// This shows the pattern for adding future jobs.
    /// </summary>
    public static void UpdateBLMGauge(byte umbralStacks, byte astralStacks, uint elementTimer, byte polyglot)
    {
        // Only update if current job is BLM or THM
        var currentJob = JobId;
        if (currentJob != 25 && currentJob != 7) 
            return;
            
        // Pack BLM data: [polyglot:8][astralStacks:8][umbralStacks:8][reserved:8]
        var gaugeData1 = (uint)((polyglot << 24) | (astralStacks << 16) | (umbralStacks << 8));
        UpdateJobGauge(currentJob, gaugeData1, elementTimer);
    }
    
    #endregion
}
