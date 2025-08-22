using System;
using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Jobs.WHM.Data;

namespace ModernActionCombo.Jobs.WHM;

/// <summary>
/// WHM Provider - Gauge Management Implementation
/// Separated into its own file for maintainability.
/// </summary>
public partial class WHMProvider // : IGaugeProvider is already declared in main file
{
    #region IGaugeProvider Implementation
    
    public void UpdateGauge()
    {
        // Only update if we're the active job
        var currentJob = GameStateCache.JobId;
        if (currentJob != 24 && currentJob != 6) // WHM or CNJ
            return;
            
        try
        {
            // Get WHM gauge data from Dalamud
            var whmGauge = ModernActionCombo.JobGauges.Get<Dalamud.Game.ClientState.JobGauge.Types.WHMGauge>();
            
            // Update GameStateCache with current gauge data
            GameStateCache.UpdateWHMGauge(
                healingLilies: whmGauge.Lily,
                lilyTimer: (uint)whmGauge.LilyTimer,
                bloodLily: whmGauge.BloodLily
            );
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Warning($"Failed to update WHM gauge: {ex.Message}");
        }
    }
    
    public uint GetGaugeData1() => GameStateCache.GetGaugeData1();
    
    public uint GetGaugeData2() => GameStateCache.GetGaugeData2();
    
    public string GetGaugeDebugInfo() => WHMJobGauge.GetDebugInfo();
    
    #endregion
    
    #region Partial Method Implementation
    
    private partial string GetGaugeInfo()
    {
        return $"Lilies: {WHMJobGauge.HealingLilies}/3, Blood: {WHMJobGauge.BloodLily}/3";
    }
    
    #endregion
}
