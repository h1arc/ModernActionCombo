using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using ModernActionCombo.Core.Services;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Jobs.WHM;
using ModernActionCombo.UI.Components;

namespace ModernActionCombo.UI.Windows;

/// <summary>
/// Base class for job-specific configuration windows.
/// Provides common functionality for all job config windows.
/// </summary>
public abstract class BaseJobConfigWindow : Window, IDisposable
{
    protected readonly ActionInterceptor _actionInterceptor;
    protected readonly GameState _gameState;
    protected readonly uint _jobId;
    protected readonly string _jobName;
    protected bool _disposed = false;

    protected BaseJobConfigWindow(string jobName, uint jobId, ActionInterceptor actionInterceptor, GameState gameState) 
        : base($"{jobName} Configuration###JobConfig_{jobId}")
    {
        _jobName = jobName;
        _jobId = jobId;
        _actionInterceptor = actionInterceptor ?? throw new ArgumentNullException(nameof(actionInterceptor));
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));

        Size = new Vector2(500, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.TextColored(new Vector4(0.3f, 0.8f, 1.0f, 1.0f), $"=== {_jobName} Configuration ===");
        ImGui.Separator();
        ImGui.Spacing();
        
        // Tab bar for organizing content - using ImRaii for proper cleanup
        using var tabBar = ImRaiiComponents.BeginTabBar("JobConfigTabs");
        
        // Combos & Rotations Tab
        using (var tabItem = ImRaiiComponents.BeginTabItem("Combos & Rotations"))
        {
            ImGui.Spacing();
            DrawComboGridSection();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            DrawOGCDRulesSection();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            DrawSmartTargetSection();
        }
        
        // Job-Specific Settings Tab
        using (var tabItem = ImRaiiComponents.BeginTabItem($"{_jobName} Settings"))
        {
            ImGui.Spacing();
            DrawJobSpecificContent();
        }
        
        // Controls Tab
        using (var tabItem = ImRaiiComponents.BeginTabItem("Controls"))
        {
            ImGui.Spacing();
            DrawCommonControls();
            DrawDebugSection();
        }
    }
    
    /// <summary>
    /// Override this to draw job-specific configuration content.
    /// </summary>
    protected abstract void DrawJobSpecificContent();
    
    /// <summary>
    /// Draws common controls available for all jobs.
    /// </summary>
    protected virtual void DrawCommonControls()
    {
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Job Controls:");
        
        if (ImGui.Button($"Reset {_jobName} Settings"))
        {
            // Use centralized policy for consistent reset behavior
            ConfigurationPolicy.ResetToDefaults(_jobId);
        }
        
        ImGui.SameLine();
        if (ImGui.Button($"Reload {_jobName} Provider"))
        {
            JobProviderRegistry.OnJobChanged(_jobId);
            _actionInterceptor.ClearCache();
            ModernActionCombo.PluginLog?.Info($"🔄 Reloaded {_jobName} provider");
        }
    }
    
    /// <summary>
    /// Draws debug controls and information.
    /// </summary>
    protected virtual void DrawDebugSection()
    {
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Debug Tools:");
        
        if (ImGui.Button($"Debug {_jobName} Config"))
        {
            var config = ConfigurationManager.GetJobConfiguration(_jobId);
            ModernActionCombo.PluginLog?.Info($"🔍 {_jobName} Config Debug:");
            ModernActionCombo.PluginLog?.Info($"  Combo Grids: {string.Join(", ", config.EnabledComboGrids.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            ModernActionCombo.PluginLog?.Info($"  Combo Rules: {string.Join(", ", config.EnabledComboRules.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            ModernActionCombo.PluginLog?.Info($"  oGCD Rules: {string.Join(", ", config.EnabledOGCDRules.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
            ModernActionCombo.PluginLog?.Info($"  Smart Target Rules: {string.Join(", ", config.EnabledSmartTargetRules.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
        }
    }
    
    /// <summary>
    /// Helper method to draw individual combo rules configuration section.
    /// DEPRECATED: This functionality is now merged into DrawComboGridSection().
    /// </summary>
    [Obsolete("Individual combo rules are now integrated into DrawComboGridSection()")]
    protected void DrawComboRulesSection()
    {
        // This method is deprecated - combo rules are now shown within each combo grid
        // See DrawComboGridSection() for the unified view
    }
    
    /// <summary>
    /// Helper method to draw oGCD rules configuration section.
    /// </summary>
    protected void DrawOGCDRulesSection()
    {
        var jobConfig = ConfigurationManager.GetJobConfiguration(_jobId);
        var provider = JobProviderRegistry.GetProvider(_jobId);
        
        ImRaiiComponents.SectionHeader("oGCD Configuration:", new Vector4(1.0f, 0.6f, 0.3f, 1.0f));
        
        // Overall oGCD enable toggle
        var ogcdEnabled = ConfigurationManager.IsOGCDEnabled(_jobId);
        if (ImRaiiComponents.LabeledCheckbox(
            "Enable oGCDs", "",
            ref ogcdEnabled))
        {
            ConfigurationManager.SetOGCDEnabled(_jobId, ogcdEnabled);
            
            // Refresh the oGCD rules in the provider
            if (provider is Jobs.WHM.WHMProvider)
            {
                Jobs.WHM.WHMProvider.RefreshOGCDRulesStatic();
            }
            
            var status = ogcdEnabled ? "enabled" : "disabled";
            ModernActionCombo.PluginLog?.Info($"{_jobName} oGCDs globally {status}");
        }
        
        // Individual oGCD rule checkboxes (only if oGCDs are enabled overall)
        if (ogcdEnabled && provider is Jobs.WHM.WHMProvider whmProvider)
        {
            ImGui.Spacing();
            using var indent = ImRaiiComponents.BeginIndent();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Individual oGCD Rules:");
            
            var ogcdRules = whmProvider.GetOGCDRules();
            
            foreach (var namedRule in ogcdRules)
            {
                var isEnabled = jobConfig.IsOGCDRuleEnabled(namedRule.Name);
                
                if (ImGui.Checkbox($"{namedRule.Name}", ref isEnabled))
                {
                    ConfigurationManager.SetOGCDRuleEnabled(_jobId, namedRule.Name, isEnabled);
                    
                    // Refresh the oGCD rules in the provider
                    Jobs.WHM.WHMProvider.RefreshOGCDRulesStatic();
                    
                    var status = isEnabled ? "enabled" : "disabled";
                    ModernActionCombo.PluginLog?.Info($"{_jobName} oGCD rule '{namedRule.Name}' {status}");
                }
                
                ImGui.Spacing();
            }
        }
    }
    
    /// <summary>
    /// Helper method to draw smart target rules configuration section.
    /// </summary>
    protected void DrawSmartTargetSection()
    {
        var jobConfig = ConfigurationManager.GetJobConfiguration(_jobId);
        var provider = JobProviderRegistry.GetProvider(_jobId);
        
        ImRaiiComponents.SectionHeader("Smart Target Configuration:", new Vector4(0.3f, 1.0f, 0.6f, 1.0f));
        
        // Overall smart targeting enable toggle
        var smartTargetingEnabled = ConfigurationManager.IsSmartTargetingEnabled(_jobId);
        if (ImRaiiComponents.LabeledCheckbox(
            "Enable Smart Targeting", "",
            ref smartTargetingEnabled))
        {
            ConfigurationManager.SetSmartTargetingEnabled(_jobId, smartTargetingEnabled);
            
            // Refresh the smart target rules in the provider
            if (provider is Jobs.WHM.WHMProvider)
            {
                Jobs.WHM.WHMProvider.RefreshSmartTargetRulesStatic();
            }
            
            var status = smartTargetingEnabled ? "enabled" : "disabled";
            ModernActionCombo.PluginLog?.Info($"{_jobName} smart targeting globally {status}");
        }
        
        // Individual smart target rule checkboxes (only if smart targeting is enabled overall)
        if (smartTargetingEnabled && provider is INamedSmartTargetRulesProvider smartTargetProvider)
        {
            ImGui.Spacing();
            using var indent = ImRaiiComponents.BeginIndent();
            ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "Individual Smart Target Rules:");
            
            var smartTargetRules = smartTargetProvider.GetNamedSmartTargetRules();
            
            foreach (var namedRule in smartTargetRules)
            {
                var isEnabled = jobConfig.IsSmartTargetRuleEnabled(namedRule.Name);
                
                if (ImGui.Checkbox($"{namedRule.Name}", ref isEnabled))
                {
                    ConfigurationManager.SetSmartTargetRuleEnabled(_jobId, namedRule.Name, isEnabled);
                    
                    // Refresh the smart target rules in the provider
                    smartTargetProvider.RefreshSmartTargetRules();
                    
                    var status = isEnabled ? "enabled" : "disabled";
                    ModernActionCombo.PluginLog?.Info($"{_jobName} smart target rule '{namedRule.Name}' {status}");
                }
                
                ImGui.Spacing();
            }
        }
    }

    /// <summary>
    /// Helper method to draw combo grid configuration section.
    /// Now includes both grid enables and individual rule checkboxes in unified view.
    /// </summary>
    protected void DrawComboGridSection()
    {
        var jobConfig = ConfigurationManager.GetJobConfiguration(_jobId);
        var provider = JobProviderRegistry.GetProvider(_jobId);
        
        if (provider?.AsComboProvider() is IComboProvider comboProvider)
        {
            var comboGrids = comboProvider.GetComboGrids();
            
            ImRaiiComponents.SectionHeader("Combo Grids:");
            
            // Check if the provider supports named combo rules for individual controls
            INamedComboRulesProvider? comboRulesProvider = provider as INamedComboRulesProvider;
            IReadOnlyDictionary<string, IReadOnlyList<NamedComboRule>>? namedComboRules = null;
            if (comboRulesProvider != null)
            {
                namedComboRules = comboRulesProvider.GetNamedComboRules();
            }
            
            foreach (var grid in comboGrids)
            {
                // Main grid checkbox
                var isGridEnabled = jobConfig.IsComboGridEnabled(grid.Name);
                
                if (ImGui.Checkbox($"Enable {grid.Name}", ref isGridEnabled))
                {
                    ConfigurationManager.SetComboGridEnabled(_jobId, grid.Name, isGridEnabled);
                    
                    // Refresh combo rules if supported
                    if (comboRulesProvider != null)
                    {
                        comboRulesProvider.RefreshComboRules();
                    }
                    
                    var status = isGridEnabled ? "enabled" : "disabled";
                    ModernActionCombo.PluginLog?.Info($"{_jobName} '{grid.Name}' combo {status}");
                }
                
                // Show grid details
                using (var gridIndent = ImRaiiComponents.BeginIndent())
                {
                    // Individual rule checkboxes (only if grid is enabled and named rules are available)
                    if (isGridEnabled && namedComboRules != null && namedComboRules.TryGetValue(grid.Name, out var gridRules))
                    {
                        ImGui.Spacing();
                        using var rulesIndent = ImRaiiComponents.BeginIndent();
                        
                        foreach (var namedRule in gridRules)
                        {
                            var isRuleEnabled = jobConfig.IsComboRuleEnabled(grid.Name, namedRule.Name);
                            var tempEnabled = isRuleEnabled; // Create a temp variable for ImGui
                            
                            if (ImGui.Checkbox($"{namedRule.Name}##ComboRule_{grid.Name}_{namedRule.Name}", ref tempEnabled))
                            {
                                ConfigurationManager.SetComboRuleEnabled(_jobId, grid.Name, namedRule.Name, tempEnabled);
                                
                                // Refresh the combo rules in the provider
                                comboRulesProvider?.RefreshComboRules();
                                
                                var status = tempEnabled ? "enabled" : "disabled";
                                ModernActionCombo.PluginLog?.Info($"{_jobName} combo rule '{grid.Name}.{namedRule.Name}' {status}");
                            }
                        }
                    }
                }
                ImGui.Spacing();
            }
        }
        else
        {
            ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.4f, 1.0f), $"No {_jobName} provider found or no combo support.");
        }
    }

    public virtual void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
