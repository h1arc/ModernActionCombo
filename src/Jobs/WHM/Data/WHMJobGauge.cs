using System.Runtime.CompilerServices;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Jobs.WHM.Data;

/// <summary>
/// White Mage job gauge implementation.
/// Completely decoupled from Dalamud - uses GameStateCache for gauge data.
/// Tracks Healing Lily and Blood Lily for overcap protection and optimal usage.
/// </summary>
public static class WHMJobGauge
{
    public const uint JobId = 24; // WHM Job ID
    
    // Direct access to gauge data from GameStateCache
    /// <summary>Gets the current number of Healing Lilies (0-3).</summary>
    public static byte HealingLilies { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (byte)(GameStateCache.GetGaugeData1() & 0xFF); }
    
    /// <summary>Gets the timer until next lily in milliseconds.</summary>
    public static uint LilyTimer { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => GameStateCache.GetGaugeData2(); }
    
    /// <summary>Gets the current Blood Lily count (0-3).</summary>
    public static byte BloodLily { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => (byte)((GameStateCache.GetGaugeData1() >> 8) & 0xFF); }
    
    /// <summary>True if we have at least one Healing Lily available.</summary>
    public static bool CanUseLily { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => HealingLilies > 0; }
    
    /// <summary>True if we have full Healing Lilies (3/3).</summary>
    public static bool HasFullLilies { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => HealingLilies == 3; }
    
    /// <summary>True if we're about to get a new lily (< 10s on timer with 2+ lilies).</summary>
    public static bool AlmostFullLilies { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => HasOvercapRisk; }
    
    /// <summary>True if Blood Lily is ready for Afflatus Misery (3/3).</summary>
    public static bool BloodLilyReady { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => BloodLily >= 3; }
    
    /// <summary>True if we're at risk of overcapping lilies (20s lily timer).</summary>
    public static bool HasOvercapRisk { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => HealingLilies >= 3 || (HealingLilies >= 2 && LilyTimer >= 10000); }
    
    /// <summary>
    /// Checks if the current job is WHM or Conjurer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsForJob(uint jobId) => WHMConstants.IsJob(jobId);
    
    /// <summary>
    /// Gets a debug string representation of the current gauge state.
    /// </summary>
    public static string GetDebugInfo()
    {
        var lilies = HealingLilies;
        var timer = LilyTimer;
        var blood = BloodLily;
        var canUse = lilies > 0;
        var full = lilies == 3;
        var overcap = lilies >= 3 || (lilies >= 2 && timer >= 10000);
        return $"Lilies: {lilies}/3, Timer: {timer}ms, Blood: {blood}/3, CanUse: {canUse}, Full: {full}, AlmostFull: {overcap}, BloodReady: {blood >= 3}, OvercapRisk: {overcap}";
    }
}
