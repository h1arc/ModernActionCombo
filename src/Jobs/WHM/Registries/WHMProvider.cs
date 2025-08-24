using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Attributes;

namespace ModernActionCombo.Jobs.WHM;

/// <summary>
/// Main WHM provider class - split across multiple files using partial classes.
/// This is the cleanest approach for file organization.
/// </summary>
[JobCombo(24, JobName = "White Mage")]
public partial class WHMProvider : IJobProvider, IComboProvider, IOGCDProvider, IGaugeProvider, ITrackingProvider, 
    INamedComboRulesProvider, INamedOGCDRulesProvider, INamedSmartTargetRulesProvider, ILevelChangeHandler, IDutyStateHandler, ICombatStateHandler
{
    // Core implementation only
    public void InitializeTracking()
    {
        ModernActionCombo.PluginLog?.Info("🔮 WHM provider initialized with all capabilities");
    }
    
    /// <summary>
    /// Initializes the WHM provider with default configuration.
    /// Called during plugin startup to ensure proper state.
    /// </summary>
    public static void Initialize()
    {
        // Initialize smart targeting for WHM abilities
        InitializeSmartHealing();
        
        // Refresh oGCD rules to match current configuration
        RefreshOGCDRulesStatic();
        ModernActionCombo.PluginLog?.Info("🔮 WHM provider initialized with smart targeting and configuration-aware oGCD rules");
    }
    
    public string GetJobDisplayInfo()
    {
        var parts = new[]
        {
            GetComboInfo(),      // From WHMProvider.Combo.cs
            GetGaugeInfo(),      // From WHMProvider.Gauge.cs  
            GetTrackingInfo()    // From WHMProvider.Tracking.cs
        };
        
        return $"WHM | {string.Join(" | ", parts)}";
    }
    
    // Partial method declarations - implemented in other files
    private static partial void InitializeSmartHealing(); // From WHMProvider.Combo.cs
    private partial string GetComboInfo();
    private partial string GetGaugeInfo();
    private partial string GetTrackingInfo();
}
