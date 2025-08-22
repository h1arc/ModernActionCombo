using System.Collections.Generic;
using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Attributes;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Jobs.WAR;

/// <summary>
/// Warrior combo - display-only for now.
/// </summary>
[JobCombo(21, JobName = "Warrior", SupportsComboProcessing = false)]
public class WARCombo : IJobProvider
{
    public void InitializeTracking()
    {
        // TODO: Add WAR-specific tracking when implemented
    }
    
    public string GetJobDisplayInfo()
    {
        // TODO: Add actual beast gauge tracking
        return "WAR | Beast Gauge: Not Implemented | Storm's Eye: Not Tracked";
    }
    
    public IReadOnlyList<ComboGrid> GetComboGrids() => [];
}
