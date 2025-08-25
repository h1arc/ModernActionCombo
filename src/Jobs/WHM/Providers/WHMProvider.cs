using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Attributes;
using ModernActionCombo.Core.Services;
using ModernActionCombo.Jobs.WHM.Data;

namespace ModernActionCombo.Jobs.WHM;

/// <summary>
/// Main WHM provider class - split across multiple files using partial classes.
/// This is the cleanest approach for file organization.
/// </summary>
[JobCombo(WHM.Data.WHMConstants.WHMJobId, JobName = "White Mage")]
public partial class WHMProvider : IJobProvider, IComboProvider, IOGCDProvider, IGaugeProvider, ITrackingProvider, 
    INamedComboRulesProvider, INamedOGCDRulesProvider, INamedSmartTargetRulesProvider, ILevelChangeHandler, IDutyStateHandler, ICombatStateHandler
{
    // Core implementation only
    public void InitializeTracking()
    {
        Logger.Info("🔮 WHM provider initialized with all capabilities");
        
        // Ensure smart targeting is initialized when the provider is registered
        // This is a fallback in case the static constructor doesn't trigger properly
        if (Core.Data.GameStateCache.JobId == WHMConstants.WHMJobId)
        {
            Logger.Warning("🔮 WHM InitializeTracking: Currently on WHM, ensuring smart targeting is initialized");
            InitializeSmartHealing();
        }
    }
    
    /// <summary>
    /// Static constructor to subscribe to job change events
    /// </summary>
    static WHMProvider()
    {
        // Subscribe to job change events from GameStateCache
        Core.Data.GameStateCache.JobChanged += OnJobChanged;
        Logger.Warning("🔮 WHM Provider static constructor: Subscribed to job change events");
        
        // Force initialization if we're already on WHM during startup
        if (Core.Data.GameStateCache.JobId == WHMConstants.WHMJobId)
        {
            Logger.Warning("🔮 WHM Provider static constructor: Already on WHM, forcing initialization");
            InitializeSmartHealing();
        }
    }
    
    /// <summary>
    /// Called when the player's job changes
    /// </summary>
    private static void OnJobChanged(uint oldJobId, uint newJobId)
    {
        Logger.Warning($"🔮 WHM: JobChanged event fired - oldJob={oldJobId}, newJob={newJobId}");
        
        // Only initialize when switching TO WHM
        if (newJobId == WHMConstants.WHMJobId)
        {
            Logger.Warning($"🔮 WHM: Job changed from {oldJobId} to WHM - initializing smart targeting");
            InitializeSmartHealing();
        }
    }
    
    /// <summary>
    /// Initializes the WHM provider with default configuration.
    /// Called during plugin startup to ensure proper state.
    /// </summary>
    public static void Initialize()
    {
        Logger.Warning("🔮 WHM Provider Initialize: Starting main initialization");
        
    // Initialize smart targeting for WHM abilities (uses enabled rules from config)
    InitializeSmartHealing();
        
    // Refresh oGCD rules to match current configuration
    RefreshOGCDRulesStatic();
    _ogcdRulesVersionBuilt = Core.Data.ConfigAwareActionCache.GetConfigVersion();
    Logger.Info("🔮 WHM provider initialized with smart targeting and configuration-aware oGCD rules");
    }
    
    /// <summary>
    /// Manual debug trigger for testing smart targeting initialization.
    /// Call this when you want to force re-initialization of smart targeting.
    /// </summary>
    public static void DebugTriggerSmartTargeting()
    {
        Logger.Warning("🔮 DEBUG: Manual trigger for WHM smart targeting");
        DebugForceInitialization();
        DebugAsylumTargeting();
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
