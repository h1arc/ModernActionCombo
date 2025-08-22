using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Data;
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
    
    #region Tracking Logic Helpers
    
    private static bool HasActiveDia()
    {
        return GameStateCache.GetTargetDebuffTimeRemaining(WHMConstants.DiaDebuffId) > 0;
    }
    
    private static bool HasPresenceOfMind()
    {
        return GameStateCache.HasPlayerBuff(WHMConstants.PresenceOfMindBuffId);
    }
    
    private static bool IsAssizeReady()
    {
        return GameStateCache.IsActionReady(WHMConstants.Assize);
    }
    
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
