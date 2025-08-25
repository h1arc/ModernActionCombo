using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Services;
using ModernActionCombo.Jobs.WHM.Data;

namespace ModernActionCombo.Jobs.WHM;

/// <summary>
/// WHM Provider - Tracking Data Implementation
/// Separated into its own file for maintainability.
/// </summary>
public partial class WHMProvider // : ITrackingProvider is already declared in main file
{
    #region ITrackingProvider Implementation
    
    public uint[] GetDebuffsToTrack() => WHMConstants.DebuffsToTrack;
    
    public uint[] GetBuffsToTrack() => WHMConstants.BuffsToTrack;
    
    public uint[] GetCooldownsToTrack() => WHMConstants.CooldownsToTrack;
    
    #endregion
    
    #region Partial Method Implementation
    
    private partial string GetTrackingInfo()
    {
        var diaTime = GameStateCache.GetTargetDebuffTimeRemaining(WHMConstants.DiaDebuffId);
        var pomTime = GameStateCache.GetPlayerBuffTimeRemaining(WHMConstants.PresenceOfMindBuffId);
        
        return $"Dia: {diaTime:F1}s, PoM: {pomTime:F1}s";
    }
    
    #endregion
}
