using System;

namespace ModernWrathCombo.UI.Core;

/// <summary>
/// Base interface for all UI components in the ModernWrathCombo system.
/// Provides automatic resource management through ImRaii integration.
/// </summary>
public interface IUIComponent : IDisposable
{
    /// <summary>
    /// Unique identifier for this component.
    /// </summary>
    string ComponentId { get; }

    /// <summary>
    /// Whether this component is enabled for user interaction.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Whether this component is visible on screen.
    /// </summary>
    bool IsVisible { get; set; }

    /// <summary>
    /// Renders the component and returns whether any values changed.
    /// Uses ImRaii for automatic resource management and state handling.
    /// </summary>
    /// <returns>True if any component values changed during rendering</returns>
    bool Render();

    /// <summary>
    /// Validates the component's current state.
    /// </summary>
    /// <returns>True if the component is in a valid state</returns>
    bool Validate();
}

/// <summary>
/// Interface for components that provide help text and tooltips.
/// </summary>
public interface IHelpProvider
{
    /// <summary>
    /// Tooltip text to display on hover.
    /// </summary>
    string? TooltipText { get; set; }

    /// <summary>
    /// Extended help text for the component.
    /// </summary>
    string? HelpText { get; set; }

    /// <summary>
    /// Whether to show help as tooltip (true) or inline (false).
    /// </summary>
    bool HelpAsTooltip { get; set; }
}

/// <summary>
/// Interface for components that support custom styling.
/// </summary>
public interface IStylable
{
    /// <summary>
    /// Item width for the component (0 = auto).
    /// </summary>
    float ItemWidth { get; set; }

    /// <summary>
    /// Custom text color (null = default).
    /// </summary>
    System.Numerics.Vector4? TextColor { get; set; }

    /// <summary>
    /// Whether the component should auto-resize.
    /// </summary>
    bool AutoResize { get; set; }
}
