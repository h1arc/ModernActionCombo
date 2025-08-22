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
    public static byte HealingLilies => (byte)(GameStateCache.GetGaugeData1() & 0xFF);
    
    /// <summary>Gets the timer until next lily in milliseconds.</summary>
    public static uint LilyTimer => GameStateCache.GetGaugeData2();
    
    /// <summary>Gets the current Blood Lily count (0-3).</summary>
    public static byte BloodLily => (byte)((GameStateCache.GetGaugeData1() >> 8) & 0xFF);
    
    /// <summary>True if we have at least one Healing Lily available.</summary>
    public static bool CanUseLily => HealingLilies > 0;
    
    /// <summary>True if we have full Healing Lilies (3/3).</summary>
    public static bool HasFullLilies => HealingLilies == 3;
    
    /// <summary>True if we're about to get a new lily (< 10s on timer with 2+ lilies).</summary>
    public static bool AlmostFullLilies => HasOvercapRisk;
    
    /// <summary>True if Blood Lily is ready for Afflatus Misery (3/3).</summary>
    public static bool BloodLilyReady => BloodLily >= 3;
    
    /// <summary>True if we're at risk of overcapping lilies (20s lily timer).</summary>
    public static bool HasOvercapRisk => HealingLilies >= 3 || (HealingLilies >= 2 && LilyTimer >= 10000);
    
    /// <summary>
    /// Checks if the current job is WHM or Conjurer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsForJob(uint jobId) => jobId == 24 || jobId == 6; // WHM or CNJ
    
    /// <summary>
    /// Gets a debug string representation of the current gauge state.
    /// </summary>
    public static string GetDebugInfo() => 
        $"Lilies: {HealingLilies}/3, Timer: {LilyTimer}ms, Blood: {BloodLily}/3, " +
        $"CanUse: {CanUseLily}, Full: {HasFullLilies}, AlmostFull: {AlmostFullLilies}, " +
        $"BloodReady: {BloodLilyReady}, OvercapRisk: {HasOvercapRisk}";
}
