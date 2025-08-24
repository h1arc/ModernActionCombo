using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace ModernActionCombo.UI.Components;

/// <summary>
/// Reusable ImRaii components for consistent UI styling across the plugin.
/// All components use ImRaii for proper resource management and cleaner code.
/// 
/// Example usage:
/// 
/// // Basic labeled controls
/// ImRaiiComponents.LabeledDragFloat("Speed", "Movement speed multiplier", ref speed, 0.1f, 0.0f, 5.0f);
/// ImRaiiComponents.LabeledCheckbox("Enable Feature", "Toggles the feature on/off", ref enabled);
/// 
/// // Using scoped patterns
/// using (var tabBar = ImRaiiComponents.BeginTabBar("MyTabs"))
/// {
///     using (var tab = ImRaiiComponents.BeginTabItem("Tab 1"))
///     {
///         ImGui.Text("Tab content here");
///     }
/// }
/// 
/// // Advanced components
/// ImRaiiComponents.CollapsibleSection("Advanced Settings", ref showAdvanced, null, () =>
/// {
///     ImGui.Text("Advanced content here");
/// });
/// 
/// ImRaiiComponents.HelpMarker("This explains what the setting does");
/// </summary>
public static class ImRaiiComponents
{
    /// <summary>
    /// Draws a labeled drag float with description text below using ImRaii patterns.
    /// Allows double-click for manual input.
    /// </summary>
    /// <param name="label">The main label for the drag control</param>
    /// <param name="description">Description text shown below the control</param>
    /// <param name="value">Reference to the value to modify</param>
    /// <param name="speed">Drag speed (default 0.01f for fine control)</param>
    /// <param name="min">Minimum value</param>
    /// <param name="max">Maximum value</param>
    /// <param name="format">Display format for the value</param>
    /// <returns>True if the value was changed</returns>
    public static bool LabeledDragFloat(string label, string description, ref float value, float speed = 0.01f, float min = 0.0f, float max = 0.0f, string format = "%.1f")
    {
        using var group = ImRaii.Group();
        
        ImGui.Text(label);
        ImGui.Spacing();
        
        bool changed = ImGui.DragFloat($"##{label}", ref value, speed, min, max, format);
        
        if (!string.IsNullOrEmpty(description))
        {
            using var descColor = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
            ImGui.Text(description);
        }
        
        ImGui.Spacing();
        return changed;
    }
    
    /// <summary>
    /// Draws a labeled drag integer with description text below using ImRaii patterns.
    /// Allows double-click for manual input.
    /// </summary>
    /// <param name="label">The main label for the drag control</param>
    /// <param name="description">Description text shown below the control</param>
    /// <param name="value">Reference to the value to modify</param>
    /// <param name="speed">Drag speed (default 1.0f)</param>
    /// <param name="min">Minimum value</param>
    /// <param name="max">Maximum value</param>
    /// <param name="format">Display format for the value</param>
    /// <returns>True if the value was changed</returns>
    public static bool LabeledDragInt(string label, string description, ref int value, float speed = 1.0f, int min = 0, int max = 0, string format = "%d")
    {
        using var group = ImRaii.Group();
        
        ImGui.Text(label);
        ImGui.Spacing();
        
        bool changed = ImGui.DragInt($"##{label}", ref value, speed, min, max, format);
        
        if (!string.IsNullOrEmpty(description))
        {
            using var descColor = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
            ImGui.Text(description);
        }
        
        ImGui.Spacing();
        return changed;
    }
    
    /// <summary>
    /// Draws a checkbox with description text below using ImRaii patterns.
    /// </summary>
    /// <param name="label">The main label for the checkbox</param>
    /// <param name="description">Description text shown below the checkbox</param>
    /// <param name="value">Reference to the boolean value to modify</param>
    /// <returns>True if the value was changed</returns>
    public static bool LabeledCheckbox(string label, string description, ref bool value)
    {
        using var group = ImRaii.Group();
        
        bool changed = ImGui.Checkbox(label, ref value);
        
        if (!string.IsNullOrEmpty(description))
        {
            using var descColor = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.7f, 0.7f, 0.7f, 1.0f));
            ImGui.Text(description);
        }
        
        ImGui.Spacing();
        return changed;
    }
    
    /// <summary>
    /// Draws a section header with consistent styling using ImRaii for color management.
    /// </summary>
    /// <param name="title">The section title</param>
    /// <param name="color">Optional color for the title (defaults to yellow)</param>
    public static void SectionHeader(string title, Vector4? color = null)
    {
        var headerColor = color ?? new Vector4(1.0f, 0.8f, 0.2f, 1.0f);
        using var headerStyle = ImRaii.PushColor(ImGuiCol.Text, headerColor);
        ImGui.Text(title);
        ImGui.Spacing();
    }
    
    /// <summary>
    /// Draws a separator with spacing.
    /// </summary>
    public static void SectionSeparator()
    {
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }
    
    /// <summary>
    /// Creates a properly scoped child window using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <param name="id">Unique identifier for the child window</param>
    /// <param name="size">Size of the child window</param>
    /// <param name="border">Whether to draw a border</param>
    /// <param name="flags">Additional window flags</param>
    /// <returns>ImRaii.IEndObject for use in using statements</returns>
    public static ImRaii.IEndObject BeginChild(string id, Vector2 size = default, bool border = false, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
    {
        return ImRaii.Child(id, size, border, flags);
    }
    
    /// <summary>
    /// Creates a properly scoped tab bar using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <param name="id">Unique identifier for the tab bar</param>
    /// <param name="flags">Tab bar flags</param>
    /// <returns>ImRaii.IEndObject for use in using statements</returns>
    public static ImRaii.IEndObject BeginTabBar(string id, ImGuiTabBarFlags flags = ImGuiTabBarFlags.None)
    {
        return ImRaii.TabBar(id, flags);
    }
    
    /// <summary>
    /// Creates a properly scoped tab item using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <param name="label">Label for the tab</param>
    /// <param name="flags">Tab item flags</param>
    /// <returns>ImRaii.IEndObject for use in using statements</returns>
    public static ImRaii.IEndObject BeginTabItem(string label, ImGuiTabItemFlags flags = ImGuiTabItemFlags.None)
    {
        return ImRaii.TabItem(label, flags);
    }
    
    /// <summary>
    /// Creates a properly scoped tab item with close button using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <param name="label">Label for the tab</param>
    /// <param name="open">Reference to open state</param>
    /// <param name="flags">Tab item flags</param>
    /// <returns>ImRaii.IEndObject for use in using statements</returns>
    public static ImRaii.IEndObject BeginTabItem(string label, ref bool open, ImGuiTabItemFlags flags = ImGuiTabItemFlags.None)
    {
        return ImRaii.TabItem(label, ref open, flags);
    }
    
    /// <summary>
    /// Creates a properly scoped group using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <returns>ImRaii.IEndObject for use in using statements</returns>
    public static ImRaii.IEndObject BeginGroup()
    {
        return ImRaii.Group();
    }
    
    /// <summary>
    /// Creates a properly scoped indentation using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <param name="amount">Amount to indent (default uses ImGui's default)</param>
    /// <returns>ImRaii.Indent for use in using statements</returns>
    public static ImRaii.Indent BeginIndent(float amount = 0.0f)
    {
        return amount == 0.0f ? ImRaii.PushIndent(1, true) : ImRaii.PushIndent(amount, false, true);
    }
    
    /// <summary>
    /// Creates a properly scoped disabled state using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <param name="disabled">Whether to disable the elements</param>
    /// <returns>ImRaii.IEndObject for use in using statements</returns>
    public static ImRaii.IEndObject BeginDisabled(bool disabled = true)
    {
        return ImRaii.Disabled(disabled);
    }
    
    /// <summary>
    /// Creates a properly scoped color push using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <param name="idx">ImGui color index</param>
    /// <param name="color">Color value</param>
    /// <returns>ImRaii.Color for use in using statements</returns>
    public static ImRaii.Color PushColor(ImGuiCol idx, Vector4 color)
    {
        return ImRaii.PushColor(idx, color);
    }
    
    /// <summary>
    /// Creates a properly scoped style variable push using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <param name="idx">Style variable index</param>
    /// <param name="value">Style value</param>
    /// <returns>ImRaii.Style for use in using statements</returns>
    public static ImRaii.Style PushStyle(ImGuiStyleVar idx, float value)
    {
        return ImRaii.PushStyle(idx, value);
    }
    
    /// <summary>
    /// Creates a properly scoped style variable push using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <param name="idx">Style variable index</param>
    /// <param name="value">Style value</param>
    /// <returns>ImRaii.Style for use in using statements</returns>
    public static ImRaii.Style PushStyle(ImGuiStyleVar idx, Vector2 value)
    {
        return ImRaii.PushStyle(idx, value);
    }
    
    /// <summary>
    /// Creates a properly scoped ID push using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <param name="id">ID to push</param>
    /// <returns>ImRaii.Id for use in using statements</returns>
    public static ImRaii.Id PushId(string id)
    {
        return ImRaii.PushId(id);
    }
    
    /// <summary>
    /// Creates a properly scoped ID push using ImRaii.
    /// Use in a using statement for automatic cleanup.
    /// </summary>
    /// <param name="id">ID to push</param>
    /// <returns>ImRaii.Id for use in using statements</returns>
    public static ImRaii.Id PushId(int id)
    {
        return ImRaii.PushId(id);
    }
    
    /// <summary>
    /// Draws a collapsible section with header and content using ImRaii patterns.
    /// Automatically manages tree node state and styling.
    /// </summary>
    /// <param name="label">The section label</param>
    /// <param name="isOpen">Reference to the open state</param>
    /// <param name="headerColor">Optional header color</param>
    /// <param name="drawContent">Action to draw the content when expanded</param>
    /// <returns>True if the section is open</returns>
    public static bool CollapsibleSection(string label, ref bool isOpen, Vector4? headerColor = null, Action? drawContent = null)
    {
        var color = headerColor ?? new Vector4(1.0f, 0.8f, 0.2f, 1.0f);
        
        using var headerStyle = ImRaii.PushColor(ImGuiCol.Text, color);
        isOpen = ImGui.CollapsingHeader(label, ImGuiTreeNodeFlags.DefaultOpen);
        
        if (isOpen && drawContent != null)
        {
            using var indent = ImRaii.PushIndent(1, true);
            ImGui.Spacing();
            drawContent();
            ImGui.Spacing();
        }
        
        return isOpen;
    }
    
    /// <summary>
    /// Draws a button with optional disabled state and styling using ImRaii.
    /// </summary>
    /// <param name="label">Button label</param>
    /// <param name="size">Button size (default uses ImGui default)</param>
    /// <param name="disabled">Whether the button should be disabled</param>
    /// <param name="buttonColor">Optional button color</param>
    /// <returns>True if button was clicked and not disabled</returns>
    public static bool StyledButton(string label, Vector2 size = default, bool disabled = false, Vector4? buttonColor = null)
    {
        using var disabledScope = ImRaii.Disabled(disabled);
        
        if (buttonColor.HasValue)
        {
            using var colorScope = ImRaii.PushColor(ImGuiCol.Button, buttonColor.Value);
            return ImGui.Button(label, size);
        }
        
        return ImGui.Button(label, size);
    }
    
    /// <summary>
    /// Draws a tooltip that appears when hovering over the last item using ImRaii.
    /// </summary>
    /// <param name="text">Tooltip text</param>
    /// <param name="textColor">Optional text color</param>
    public static void ItemTooltip(string text, Vector4? textColor = null)
    {
        if (!ImGui.IsItemHovered()) return;
        
        using var tooltip = ImRaii.Tooltip();
        
        if (textColor.HasValue)
        {
            using var colorScope = ImRaii.PushColor(ImGuiCol.Text, textColor.Value);
            ImGui.Text(text);
        }
        else
        {
            ImGui.Text(text);
        }
    }
    
    /// <summary>
    /// Draws a help marker (?) that shows a tooltip on hover using ImRaii.
    /// </summary>
    /// <param name="helpText">The help text to display</param>
    /// <param name="markerColor">Optional color for the marker</param>
    public static void HelpMarker(string helpText, Vector4? markerColor = null)
    {
        var color = markerColor ?? new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
        
        using var textColor = ImRaii.PushColor(ImGuiCol.Text, color);
        ImGui.TextDisabled("(?)");
        
        ItemTooltip(helpText);
    }
    
    /// <summary>
    /// Creates a bordered group with optional title using ImRaii patterns.
    /// </summary>
    /// <param name="title">Optional title for the group</param>
    /// <param name="borderColor">Optional border color</param>
    /// <param name="titleColor">Optional title color</param>
    /// <param name="drawContent">Action to draw the group content</param>
    public static void BorderedGroup(string? title = null, Vector4? borderColor = null, Vector4? titleColor = null, Action? drawContent = null)
    {
        var border = borderColor ?? new Vector4(0.5f, 0.5f, 0.5f, 1.0f);
        var titleCol = titleColor ?? new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
        
        using var group = ImRaii.Group();
        
        // Draw title if provided
        if (!string.IsNullOrEmpty(title))
        {
            using var titleStyle = ImRaii.PushColor(ImGuiCol.Text, titleCol);
            ImGui.Text(title);
        }
        
        // Draw border
        using (var childWindow = ImRaii.Child("BorderedContent", new Vector2(0, 0), true))
        {
            if (!string.IsNullOrEmpty(title))
                ImGui.Spacing();
            
            drawContent?.Invoke();
        }
    }
    
    /// <summary>
    /// Creates a two-column layout using ImRaii patterns.
    /// </summary>
    /// <param name="leftWidth">Width of the left column (0 for auto)</param>
    /// <param name="drawLeft">Action to draw left column content</param>
    /// <param name="drawRight">Action to draw right column content</param>
    public static void TwoColumnLayout(float leftWidth = 0.0f, Action? drawLeft = null, Action? drawRight = null)
    {
        using var table = ImRaii.Table("TwoColumnLayout", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit);
        
        if (leftWidth > 0)
        {
            ImGui.TableSetupColumn("Left", ImGuiTableColumnFlags.WidthFixed, leftWidth);
            ImGui.TableSetupColumn("Right", ImGuiTableColumnFlags.WidthStretch);
        }
        else
        {
            ImGui.TableSetupColumn("Left", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Right", ImGuiTableColumnFlags.WidthStretch);
        }
        
        ImGui.TableNextRow();
        
        // Left column
        ImGui.TableSetColumnIndex(0);
        drawLeft?.Invoke();
        
        // Right column
        ImGui.TableSetColumnIndex(1);
        drawRight?.Invoke();
    }
}
