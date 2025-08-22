using System.Collections.Generic;
using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Attributes;
using ModernActionCombo.Core.Data;

namespace ModernActionCombo.Jobs.BLM;

/// <summary>
/// Black Mage combo implementation.
/// </summary>
[JobCombo(25, JobName = "Black Mage", SupportsComboProcessing = false)]
public class BLMCombo : IJobProvider
{
    public void InitializeTracking()
    {
        // TODO: Add BLM-specific tracking when implemented
    }
    
    public string GetJobDisplayInfo()
    {
        // TODO: Add actual elemental gauge tracking
        return "BLM | Elemental Gauge: Not Implemented | Enochian: Not Tracked";
    }
    
    public IReadOnlyList<ComboGrid> GetComboGrids() => [];
}
