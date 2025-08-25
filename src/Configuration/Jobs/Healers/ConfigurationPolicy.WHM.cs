using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Enums;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo.Core.Services;

/// <summary>
/// White Mage (WHM) default configuration policy.
/// Split into partial to keep the core policy file small and maintainable.
/// </summary>
public static partial class ConfigurationPolicy
{
    /// <summary>
    /// WHM default policy - all features disabled by default.
    /// Users can enable features as needed.
    /// </summary>
    private static void ApplyWHMDefaults()
    {
        // All features disabled by default - users enable what they want
        // Smart targeting is disabled by default
        // Individual rules are disabled by default
        // OGCDs are disabled by default
        // Combo grids are disabled by default
    }
}
