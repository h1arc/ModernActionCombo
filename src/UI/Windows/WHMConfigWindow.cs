using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using ModernActionCombo.Core.Services;
using ModernActionCombo.Core.Data;
using ModernActionCombo.UI.Components;

namespace ModernActionCombo.UI.Windows;

/// <summary>
/// DEPRECATED: White Mage specific configuration window.
/// 
/// ⚠️ This approach is deprecated. All job configuration is now centralized in JobConfigWindow.
/// WHM settings are now available in the "White Mage" tab of the main configuration window.
/// 
/// This file is kept for reference but should not be used for new jobs.
/// New jobs should implement their configuration directly in JobConfigWindow.DrawCurrentJobConfigTab().
/// </summary>
[Obsolete("Job-specific configuration windows are deprecated. Use JobConfigWindow with embedded configuration instead.")]
public class WHMConfigWindow : BaseJobConfigWindow
{
    public WHMConfigWindow(ActionInterceptor actionInterceptor, GameState gameState) 
        : base("White Mage", 24, actionInterceptor, gameState)
    {
    }

    protected override void DrawJobSpecificContent()
    {
        // Draw the unified combo grids section (includes individual rule controls)
        DrawComboGridSection();
        
        ImRaiiComponents.SectionSeparator();
        
        // Draw the oGCD rules section
        DrawOGCDRulesSection();
        
        ImRaiiComponents.SectionSeparator();
        
        // Draw the smart target rules section
        DrawSmartTargetSection();
        
        ImRaiiComponents.SectionSeparator();
        
        // WHM-specific advanced settings
        DrawWHMAdvancedSettings();
    }
    
    /// <summary>
    /// Draws WHM-specific advanced settings.
    /// </summary>
    private void DrawWHMAdvancedSettings()
    {
        var jobConfig = ConfigurationManager.GetJobConfiguration(_jobId);
        
        ImRaiiComponents.SectionHeader("Advanced WHM Settings:");
        
        // DoT refresh timing setting
        var dotRefreshTime = jobConfig.GetSetting("DotRefreshTime", 3.0f);
        if (ImRaiiComponents.LabeledDragFloat(
            "DoT Refresh Time (in seconds)", 
            "How early to refresh DoTs before they expire", 
            ref dotRefreshTime, 0.01f, 1.0f, 6.0f))
        {
            jobConfig.SetSetting("DotRefreshTime", dotRefreshTime);
            ModernActionCombo.PluginLog?.Debug($"WHM DoT refresh time set to {dotRefreshTime:F1}s");
        }
        
        // MP threshold for Lucid Dreaming
        var lucidMpThreshold = jobConfig.GetSetting("LucidMpThreshold", 6500u);
        int lucidMpInt = (int)lucidMpThreshold;
        if (ImRaiiComponents.LabeledDragInt(
            "Lucid Dreaming MP Threshold", 
            "Use Lucid Dreaming when MP drops below this value", 
            ref lucidMpInt, 10.0f, 3000, 8000))
        {
            jobConfig.SetSetting("LucidMpThreshold", (uint)lucidMpInt);
            ModernActionCombo.PluginLog?.Debug($"WHM Lucid Dreaming MP threshold set to {lucidMpInt}");
        }
        
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Note: These settings will be integrated into the combo logic in future updates.");
    }
}
