using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using ModernActionCombo.Core.Services;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Jobs.WHM;
using ModernActionCombo.UI.Components;

namespace ModernActionCombo.UI.Windows;

/// <summary>
/// Main job configuration launcher window.
/// Acts as a centralized hub for all job configuration - NO separate windows should be used.
/// 
/// ✅ STANDARDIZED APPROACH (Use this for all jobs):
/// - Job Overview tab: Shows current job status and all supported jobs list
/// - {JobName} tab: Shows all job-specific configuration in a single scrollable interface
/// - Global Controls tab: Shows global controls and debug tools
/// 
/// All job configuration is embedded directly in the {JobName} tab in sections:
/// - Combo Grids: Combo configuration options
/// - oGCD Configuration: oGCD rules and settings  
/// - Smart Target Configuration: Smart targeting rules
/// - Advanced Settings: Job-specific advanced settings
/// - Controls: Job-specific controls and debug tools
/// 
/// ❌ DO NOT create separate job-specific configuration windows.
/// ❌ DO NOT use sub-tabs within job tabs (consolidated approach).
/// ❌ DO NOT use the "Configure" button approach (deprecated).
/// 
/// This provides a consistent, centralized user experience across all jobs.
/// </summary>
public class JobConfigWindow : Window, IDisposable
{
    private readonly ActionInterceptor _actionInterceptor;
    private readonly WindowSystem _windowSystem;
    private bool _disposed = false;
    
    // Job data
    private readonly (string name, uint jobId, bool implemented)[] _supportedJobs = 
    {
        ("White Mage", 24, true),
        ("Warrior", 21, false),
        ("Black Mage", 25, false),
        ("Dark Knight", 32, false),
        ("Scholar", 28, false),
        ("Astrologian", 33, false)
    };

    public JobConfigWindow(ActionInterceptor actionInterceptor, WindowSystem windowSystem) 
        : base("MAC/Job Configuration###MacJobConfig")
    {
        _actionInterceptor = actionInterceptor ?? throw new ArgumentNullException(nameof(actionInterceptor));
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));

        Size = new Vector2(500, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {        
        // Main tab bar
        if (ImGui.BeginTabBar("MainConfigTabs"))
        {
            // Job Overview Tab - Only show job info and supported jobs list (magenta content)
            if (ImGui.BeginTabItem("Job Overview"))
            {
                ImGui.Spacing();
                DrawJobOverviewTab();
                ImGui.EndTabItem();
            }
            
            // Current Job Configuration Tab (only show if current job is supported)
            var currentJobId = GameStateCache.JobId;
            var currentJobInfo = Array.Find(_supportedJobs, j => j.jobId == currentJobId);
            if (currentJobInfo.implemented)
            {
                if (ImGui.BeginTabItem($"{currentJobInfo.name}"))
                {
                    ImGui.Spacing();
                    DrawCurrentJobConfigTab(currentJobId, currentJobInfo.name);
                    ImGui.EndTabItem();
                }
            }
            
            // Global Controls Tab - Show global controls and debug tools (red content)
            if (ImGui.BeginTabItem("Global Controls"))
            {
                ImGui.Spacing();
                DrawGlobalControlsTab();
                ImGui.EndTabItem();
            }
            
            ImGui.EndTabBar();
        }
    }
    
    /// <summary>
    /// Draws the job overview tab with current job info and all supported jobs (magenta content).
    /// </summary>
    private void DrawJobOverviewTab()
    {
        // Current job info (magenta content)
        var currentJobId = GameStateCache.JobId;
        var currentJobName = JobProviderRegistry.GetJobName(currentJobId);
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), $"Current Job: {currentJobName} (ID: {currentJobId})");
        ImGui.Spacing();
        
        // Quick access status for current job
        DrawCurrentJobQuickAccess();
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // All supported jobs list
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "All Supported Jobs:");
        ImGui.Spacing();
        
        DrawJobsList();
    }
    
    /// <summary>
    /// Draws the current job configuration tab with embedded config content (green content).
    /// This is the centralized approach for all job configuration - everything should be embedded here.
    /// </summary>
    private void DrawCurrentJobConfigTab(uint jobId, string jobName)
    {
        // Embed the job configuration directly here instead of opening separate windows
        // This is the new standardized pattern: ALL job configuration goes in this tab
        switch (jobId)
        {
            case 24: // WHM - Show all the configuration content in tabbed format
                DrawWHMConfigEmbedded();
                break;
                
            // Future jobs should follow the same pattern:
            // case 21: // WAR
            //     DrawWARConfigEmbedded();
            //     break;
            // case 25: // BLM
            //     DrawBLMConfigEmbedded(); 
            //     break;
                
            default:
                ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.4f, 1.0f), $"Configuration for {jobName} is not yet implemented.");
                ImGui.Spacing();
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "When implemented, all configuration options will be available here.");
                break;
        }
    }
    
    /// <summary>
    /// Draws the global controls tab (red content).
    /// </summary>
    private void DrawGlobalControlsTab()
    {
        // Global Controls (red box content)
        DrawGlobalControls();
    }
    
    /// <summary>
    /// Draws embedded WHM configuration content (green box content).
    /// All WHM settings consolidated into a single tab.
    /// </summary>
    private void DrawWHMConfigEmbedded()
    {
        // All WHM configuration in a single scrollable area
        
        // Combos & Rotations Section
        ImRaiiComponents.SectionHeader("Combo Grids:", new Vector4(0.3f, 0.8f, 1.0f, 1.0f));
        DrawWHMComboConfiguration();
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // oGCD Configuration Section
        DrawWHMOGCDConfiguration();
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Smart Target Configuration Section
        DrawWHMSmartTargetConfiguration();
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Advanced Settings Section
        // ImRaiiComponents.SectionHeader("Advanced WHM Settings:", new Vector4(1.0f, 0.8f, 0.3f, 1.0f));
        DrawWHMAdvancedSettings();
        
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Controls Section
        // ImRaiiComponents.SectionHeader("WHM Controls:", new Vector4(0.8f, 0.3f, 1.0f, 1.0f));
        DrawWHMControls();
        
        ImGui.Spacing();
        
        // Debug Section
        DrawWHMDebugSection();
    }
    
    /// <summary>
    /// Draws WHM combo and rotation configuration.
    /// </summary>
    private void DrawWHMComboConfiguration()
    {
        var jobConfig = ConfigurationManager.GetJobConfiguration(24); // WHM job ID
        var provider = JobProviderRegistry.GetProvider(24);
        
        if (provider?.AsComboProvider() is IComboProvider comboProvider)
        {
            var comboGrids = comboProvider.GetComboGrids();
            
            // Check if the provider supports named combo rules for individual controls
            INamedComboRulesProvider? comboRulesProvider = provider as INamedComboRulesProvider;
            IReadOnlyDictionary<string, IReadOnlyList<NamedComboRule>>? namedComboRules = null;
            if (comboRulesProvider != null)
            {
                namedComboRules = comboRulesProvider.GetNamedComboRules();
            }
            
            for (int gi = 0; gi < comboGrids.Count; gi++)
            {
                var grid = comboGrids[gi];
                // Main grid checkbox with numbering; keep a stable ID using ## suffix
                var isGridEnabled = jobConfig.IsComboGridEnabled(grid.Name);
                var gridLabel = $"{gi + 1}. {grid.Name}##ComboGrid_{grid.Name}";

                if (ImGui.Checkbox(gridLabel, ref isGridEnabled))
                {
                    ConfigurationManager.SetComboGridEnabled(24, grid.Name, isGridEnabled);

                    // Refresh combo rules if supported
                    if (comboRulesProvider != null)
                    {
                        comboRulesProvider.RefreshComboRules();
                    }

                    var status = isGridEnabled ? "enabled" : "disabled";
                    ModernActionCombo.PluginLog?.Info($"White Mage '{grid.Name}' combo {status}");
                }

                // Show grid details
                using (var gridIndent = ImRaiiComponents.BeginIndent())
                {
                    // Individual rule checkboxes (only if grid is enabled and named rules are available)
                    if (isGridEnabled && namedComboRules != null && namedComboRules.TryGetValue(grid.Name, out var gridRules))
                    {
                        ImGui.Spacing();
                        using var rulesIndent = ImRaiiComponents.BeginIndent();

                        for (int i = 0; i < gridRules.Count; i++)
                        {
                            var namedRule = gridRules[i];
                            var isRuleEnabled = jobConfig.IsComboRuleEnabled(grid.Name, namedRule.Name);
                            var tempEnabled = isRuleEnabled; // temp for ImGui

                            // Visible label is numbered; ID remains stable via the ## suffix
                            var label = $"{i + 1}. {namedRule.Name}##ComboRule_{grid.Name}_{namedRule.Name}";
                            if (ImGui.Checkbox(label, ref tempEnabled))
                            {
                                ConfigurationManager.SetComboRuleEnabled(24, grid.Name, namedRule.Name, tempEnabled);

                                // Refresh the combo rules in the provider
                                comboRulesProvider?.RefreshComboRules();

                                var status = tempEnabled ? "enabled" : "disabled";
                                ModernActionCombo.PluginLog?.Info($"White Mage combo rule '{grid.Name}.{namedRule.Name}' {status}");
                            }
                        }
                    }
                }
                ImGui.Spacing();
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.4f, 1.0f), "No White Mage provider found or no combo support.");
        }
    }
    
    /// <summary>
    /// Draws WHM oGCD configuration.
    /// </summary>
    private void DrawWHMOGCDConfiguration()
    {
        var jobConfig = ConfigurationManager.GetJobConfiguration(24);
        var provider = JobProviderRegistry.GetProvider(24);
        
        ImRaiiComponents.SectionHeader("oGCD Configuration:", new Vector4(1.0f, 0.6f, 0.3f, 1.0f));
        
        // Overall oGCD enable toggle
        var ogcdEnabled = ConfigurationManager.IsOGCDEnabled(24);
        if (ImRaiiComponents.LabeledCheckbox(
            "Enable oGCDs", "",
            ref ogcdEnabled))
        {
            ConfigurationManager.SetOGCDEnabled(24, ogcdEnabled);
            
            // Refresh the oGCD rules in the provider
            if (provider is WHMProvider)
            {
                WHMProvider.RefreshOGCDRulesStatic();
            }
            
            var status = ogcdEnabled ? "enabled" : "disabled";
            ModernActionCombo.PluginLog?.Info($"White Mage oGCDs globally {status}");
        }
        
        // Individual oGCD rule checkboxes (only if oGCDs are enabled overall)
        if (ogcdEnabled && provider is WHMProvider whmProvider)
        {
            ImGui.Spacing();
            using var indent = ImRaiiComponents.BeginIndent();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "The following will be used for calculating optimal oGCD usage:");
            
            var ogcdRules = whmProvider.GetOGCDRules();
            
            foreach (var namedRule in ogcdRules)
            {
                var isEnabled = jobConfig.IsOGCDRuleEnabled(namedRule.Name);
                
                if (ImGui.Checkbox($"{namedRule.Name}", ref isEnabled))
                {
                    ConfigurationManager.SetOGCDRuleEnabled(24, namedRule.Name, isEnabled);
                    
                    // Refresh the oGCD rules in the provider
                    WHMProvider.RefreshOGCDRulesStatic();
                    
                    var status = isEnabled ? "enabled" : "disabled";
                    ModernActionCombo.PluginLog?.Info($"White Mage oGCD rule '{namedRule.Name}' {status}");
                }
                
                ImGui.Spacing();
            }
        }
    }
    
    /// <summary>
    /// Draws WHM smart target configuration.
    /// </summary>
    private void DrawWHMSmartTargetConfiguration()
    {
        var jobConfig = ConfigurationManager.GetJobConfiguration(24);
        var provider = JobProviderRegistry.GetProvider(24);
        
        ImRaiiComponents.SectionHeader("Smart Target Configuration:", new Vector4(0.3f, 1.0f, 0.6f, 1.0f));
        
        // Overall smart targeting enable toggle
    var smartTargetingEnabled = ConfigurationManager.IsSmartTargetingEnabled(24);
        if (ImRaiiComponents.LabeledCheckbox(
            "Enable Smart Targeting", "",
            ref smartTargetingEnabled))
        {
            ConfigurationManager.SetSmartTargetingEnabled(24, smartTargetingEnabled);
            
            // Refresh the smart target rules in the provider
            if (provider is WHMProvider)
            {
                WHMProvider.RefreshSmartTargetRulesStatic();
            }

            var status = smartTargetingEnabled ? "enabled" : "disabled";
            ModernActionCombo.PluginLog?.Info($"White Mage smart targeting globally {status}");
        }
        
        // Individual smart target rule checkboxes (only if smart targeting is enabled overall)
        if (smartTargetingEnabled && provider is INamedSmartTargetRulesProvider smartTargetProvider)
        {
            ImGui.Spacing();
            using var indent = ImRaiiComponents.BeginIndent();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "The following abilities will resolve according to the following rule:"
                + "\nHard Target > Ally if Missing HP% > Self\nEsuna will smart target if there is a party member with an effect that can be cleansed.");
            ImGui.Spacing();

            var smartTargetRules = smartTargetProvider.GetNamedSmartTargetRules();
            
            foreach (var namedRule in smartTargetRules)
            {
                var isEnabled = jobConfig.IsSmartTargetRuleEnabled(namedRule.Name);
                
                if (ImGui.Checkbox($"{namedRule.Name}", ref isEnabled))
                {
                    ConfigurationManager.SetSmartTargetRuleEnabled(24, namedRule.Name, isEnabled);
                    
                    // Refresh the smart target rules in the provider
                    smartTargetProvider.RefreshSmartTargetRules();
                    
                    var status = isEnabled ? "enabled" : "disabled";
                    ModernActionCombo.PluginLog?.Info($"White Mage smart target rule '{namedRule.Name}' {status}");
                }
                
                ImGui.Spacing();
            }
        }
    }
    
    /// <summary>
    /// Draws WHM debug section.
    /// </summary>
    private void DrawWHMDebugSection()
    {
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Debug Tools:");
        
        if (ImGui.Button("Debug White Mage Config"))
        {
            var config = ConfigurationManager.GetJobConfiguration(24);
            ModernActionCombo.PluginLog?.Info("🔍 White Mage Config Debug:");
            ModernActionCombo.PluginLog?.Info($"  Combo Grids: {string.Join(", ", config.EnabledComboGrids.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            ModernActionCombo.PluginLog?.Info($"  Combo Rules: {string.Join(", ", config.EnabledComboRules.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            ModernActionCombo.PluginLog?.Info($"  oGCD Rules: {string.Join(", ", config.EnabledOGCDRules.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            ModernActionCombo.PluginLog?.Info($"  Smart Target Rules: {string.Join(", ", config.EnabledSmartTargetRules.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        }
    }
    
    /// <summary>
    /// Draws WHM advanced settings.
    /// </summary>
    private void DrawWHMAdvancedSettings()
    {
        var jobConfig = ConfigurationManager.GetJobConfiguration(24);
        
        ImRaiiComponents.SectionHeader("Advanced WHM Settings:");
        
        // DoT refresh timing setting
        var dotRefreshTime = jobConfig.GetSetting("DotRefreshTime", 3.0f);
        if (ImRaiiComponents.LabeledDragFloat("DoT Refresh Time (seconds)", "How early to refresh DoTs before they expire", ref dotRefreshTime, 0.1f, 1.0f, 6.0f, "%.1f"))
        {
            jobConfig.SetSetting("DotRefreshTime", dotRefreshTime);
            ModernActionCombo.PluginLog?.Debug($"WHM DoT refresh time set to {dotRefreshTime:F1}s");
        }
        
        // MP threshold for Lucid Dreaming
        var lucidMpThreshold = jobConfig.GetSetting("LucidMpThreshold", 6500u);
        int lucidMpInt = (int)lucidMpThreshold;
        if (ImRaiiComponents.LabeledDragInt("Lucid Dreaming MP Threshold", "Use Lucid Dreaming when MP drops below this value", ref lucidMpInt, 10.0f, 3000, 8000))
        {
            jobConfig.SetSetting("LucidMpThreshold", (uint)lucidMpInt);
            ModernActionCombo.PluginLog?.Debug($"WHM Lucid Dreaming MP threshold set to {lucidMpInt}");
        }
        ImGui.Spacing();
        
        // Per-job Companion (Chocobo) Settings
        ImRaiiComponents.SectionHeader("Companion (Chocobo) Settings:");
        var compEnabled = jobConfig.GetSetting("CompanionScanEnabled", true);
        if (ImGui.Checkbox("Enable Companion Scanning (per-job)", ref compEnabled))
        {
            jobConfig.SetSetting("CompanionScanEnabled", compEnabled);
            Core.Services.ConfigSaveScheduler.NotifyChanged();
        }
        var compOverride = jobConfig.GetSetting("CompanionOverrideEnabled", false);
        if (ImGui.Checkbox("Allow Companion Override when much lower HP (per-job)", ref compOverride))
        {
            jobConfig.SetSetting("CompanionOverrideEnabled", compOverride);
            Core.Services.ConfigSaveScheduler.NotifyChanged();
        }
        if (compOverride)
        {
            var delta = jobConfig.GetSetting("CompanionOverrideDelta", 0.25f);
            if (ImGui.SliderFloat("Override HP Delta (per-job)", ref delta, 0.05f, 0.5f, "%.02f"))
            {
                jobConfig.SetSetting("CompanionOverrideDelta", delta);
                Core.Services.ConfigSaveScheduler.NotifyChanged();
            }
        }
    }
    
    /// <summary>
    /// Draws WHM-specific controls.
    /// </summary>
    private void DrawWHMControls()
    {
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "White Mage Controls:");
        
        if (ImGui.Button("Reset White Mage Settings"))
        {
            ConfigurationPolicy.ResetToDefaults(24);
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Reload White Mage Provider"))
        {
            JobProviderRegistry.OnJobChanged(24);
            _actionInterceptor.ClearCache();
            ModernActionCombo.PluginLog?.Info("🔄 Reloaded White Mage provider");
        }
    }
    
    private void DrawCurrentJobQuickAccess()
    {
        var currentJobId = GameStateCache.JobId;
        var jobInfo = Array.Find(_supportedJobs, j => j.jobId == currentJobId);
        
        if (jobInfo.implemented)
        {
            ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), "✓ Current job is supported!");
        }
        else if (jobInfo.jobId != 0) // Job is in our list but not implemented
        {
            ImGui.TextColored(new Vector4(1.0f, 0.6f, 0.2f, 1.0f), "⚠ Current job support is planned but not yet implemented.");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.4f, 1.0f), "✗ Current job is not yet supported.");
        }
    }
    
    private void DrawJobsList()
    {
        foreach (var (name, jobId, implemented) in _supportedJobs)
        {
            if (implemented)
            {
                // Implemented job - show status
                ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), "✓");
                ImGui.SameLine();
                ImGui.Text($"{name} - Configuration available in the '{name}' tab");
            }
            else
            {
                // Planned job - show status
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "○");
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), $"{name} (Coming Soon)");
            }
        }
    }
    
    private void DrawGlobalControls()
    {
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Global Controls:");
        
        if (ImGui.Button("Reset All Job Settings"))
        {
            ConfigurationManager.ClearAll();
            ConfigurationManager.InitializeDefaults();
            // ConfigurationManager will handle config version increment automatically
            ModernActionCombo.PluginLog?.Info("🔄 Reset all job configurations to defaults");
        }
        
        ImGui.SameLine();
        if (ImGui.Button("Reload All Providers"))
        {
            var currentJobId = GameStateCache.JobId;
            JobProviderRegistry.OnJobChanged(currentJobId);
            // No need to clear cache - providers will work with existing cache
            ModernActionCombo.PluginLog?.Info("🔄 Reloaded all job providers");
        }
        
        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Commands: /macconfig (this window), /mac (debug panel)");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    public override void OnClose()
    {
        // Persist configuration when the config window is closed
        try { ConfigurationStorage.SaveAll(); } catch { /* ignore */ }
        base.OnClose();
    }
}
