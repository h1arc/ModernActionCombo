using System;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ImGuiCol = Dalamud.Bindings.ImGui.ImGuiCol;
using ImGui = Dalamud.Bindings.ImGui.ImGui;

namespace ModernWrathCombo.UI.Core;

/// <summary>
/// Abstract base class for all UI components in the ModernWrathCombo system.
/// Provides ImRaii integration and automatic resource management.
/// Simplified version focused on essential functionality.
/// </summary>
public abstract class BaseUIComponent : IUIComponent, IHelpProvider, IStylable
{
    private bool _disposed = false;

    #region IUIComponent Properties

    public string ComponentId { get; }
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;

    #endregion

    #region IHelpProvider Properties

    public string? TooltipText { get; set; }
    public string? HelpText { get; set; }
    public bool HelpAsTooltip { get; set; } = true;

    #endregion

    #region IStylable Properties

    public float ItemWidth { get; set; } = 150f;
    public Vector4? TextColor { get; set; }
    public bool AutoResize { get; set; } = true;

    #endregion

    protected BaseUIComponent(string componentId)
    {
        ComponentId = componentId ?? throw new ArgumentNullException(nameof(componentId));
    }

    #region Core Rendering

    public bool Render()
    {
        if (!IsVisible)
            return false;

        bool valueChanged = false;

        // Use ImRaii for automatic state management
        using var id = ImRaii.PushId(ComponentId);

        // Handle disabled state
        using var disabled = ImRaii.Disabled(!IsEnabled);

        // Apply custom styling if specified
        using var styleColor = ApplyCustomStyling();

        try
        {
            // Call the derived class's render implementation
            valueChanged = RenderComponent();

            // Render help text/tooltip
            RenderHelp();
        }
        catch (Exception ex)
        {
            // Render error indicator
            RenderError($"Component error: {ex.Message}");
        }

        return valueChanged;
    }

    /// <summary>
    /// Derived classes implement their specific rendering logic here.
    /// ImRaii context is already set up with ID, disabled state, and styling.
    /// </summary>
    /// <returns>True if the component's value changed during this render</returns>
    protected abstract bool RenderComponent();

    #endregion

    #region Styling Support

    protected virtual ImRaii.Color ApplyCustomStyling()
    {
        if (TextColor.HasValue)
        {
            // Convert Vector4 to uint color format for Dalamud
            var color = TextColor.Value;
            var colorU32 = ((uint)(color.W * 255) << 24) | ((uint)(color.Z * 255) << 16) | ((uint)(color.Y * 255) << 8) | (uint)(color.X * 255);
            return ImRaii.PushColor(ImGuiCol.Text, colorU32);
        }

        return new ImRaii.Color(); // Empty disposable if no custom color
    }

    protected void PushItemWidth()
    {
        if (ItemWidth > 0)
            ImGui.PushItemWidth(ItemWidth);
    }

    protected void PopItemWidth()
    {
        if (ItemWidth > 0)
            ImGui.PopItemWidth();
    }

    #endregion

    #region Help Text Support

    protected virtual void RenderHelp()
    {
        if (string.IsNullOrEmpty(HelpText) && string.IsNullOrEmpty(TooltipText))
            return;

        if (HelpAsTooltip)
        {
            RenderTooltip();
        }
        else
        {
            RenderInlineHelp();
        }
    }

    protected virtual void RenderTooltip()
    {
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();

        if (!string.IsNullOrEmpty(TooltipText))
            ImGui.TextUnformatted(TooltipText);

        if (!string.IsNullOrEmpty(HelpText))
        {
            if (!string.IsNullOrEmpty(TooltipText))
                ImGui.Separator();
            ImGui.TextWrapped(HelpText);
        }

        ImGui.EndTooltip();
    }

    protected virtual void RenderInlineHelp()
    {
        if (string.IsNullOrEmpty(HelpText))
            return;

        // Convert ImGuiColors Vector4 to uint
        var greyColor = ImGuiColors.DalamudGrey;
        var greyU32 = ((uint)(greyColor.W * 255) << 24) | ((uint)(greyColor.Z * 255) << 16) | ((uint)(greyColor.Y * 255) << 8) | (uint)(greyColor.X * 255);
        using var textColor = ImRaii.PushColor(ImGuiCol.Text, greyU32);
        ImGui.TextWrapped(HelpText);
    }

    #endregion

    #region Error Handling

    protected virtual void RenderError(string errorMessage)
    {
        // Convert ImGuiColors Vector4 to uint
        var redColor = ImGuiColors.DalamudRed;
        var redU32 = ((uint)(redColor.W * 255) << 24) | ((uint)(redColor.Z * 255) << 16) | ((uint)(redColor.Y * 255) << 8) | (uint)(redColor.X * 255);
        using var textColor = ImRaii.PushColor(ImGuiCol.Text, redU32);
        ImGui.TextWrapped($"⚠ {errorMessage}");
    }

    #endregion

    #region Validation

    public virtual bool Validate()
    {
        // Base validation - ensure component is properly initialized
        return !string.IsNullOrEmpty(ComponentId);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            // Dispose managed resources
            OnDispose();
        }

        _disposed = true;
    }

    /// <summary>
    /// Override this method to perform component-specific cleanup.
    /// </summary>
    protected virtual void OnDispose()
    {
        // Default implementation - nothing to dispose
    }

    #endregion
}
