using System.Runtime.InteropServices;

namespace ModernActionCombo.Core.Data;

/// <summary>
/// Targeting modes for different ability types
/// </summary>
public enum TargetingMode : byte
{
    SmartAbility = 0,
    GroundTarget = 1,
    GroundTargetSpecial = 2,
    Cleanse = 3
}

/// <summary>
/// Rule describing how an action should be smart-targeted.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct SmartTargetRule
{
    public readonly uint ActionId;
    public readonly TargetingMode Mode;
    public readonly uint SecondaryActionId; // for special cases (e.g., Liturgy)
    public readonly uint RequiredBuffId;    // buff needed for secondary action
    public readonly string? DisplayName;    // optional UI display name (ignored by logic)

    public SmartTargetRule(uint actionId, TargetingMode mode = TargetingMode.SmartAbility)
    { ActionId = actionId; Mode = mode; SecondaryActionId = 0; RequiredBuffId = 0; DisplayName = null; }

    public SmartTargetRule(uint actionId, TargetingMode mode, string displayName)
    { ActionId = actionId; Mode = mode; SecondaryActionId = 0; RequiredBuffId = 0; DisplayName = displayName; }

    public SmartTargetRule(uint actionId, uint secondaryActionId, uint requiredBuffId, TargetingMode mode)
    { ActionId = actionId; Mode = mode; SecondaryActionId = secondaryActionId; RequiredBuffId = requiredBuffId; DisplayName = null; }

    public SmartTargetRule(uint actionId, uint secondaryActionId, uint requiredBuffId, TargetingMode mode, string displayName)
    { ActionId = actionId; Mode = mode; SecondaryActionId = secondaryActionId; RequiredBuffId = requiredBuffId; DisplayName = displayName; }
}
