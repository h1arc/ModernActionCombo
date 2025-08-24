using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using ModernActionCombo.Core.Services;
using ModernActionCombo.Core.Data;
using ModernActionCombo.UI.Components;

namespace ModernActionCombo.UI.Windows;

/// <summary>
/// Simple debug panel window for ModernActionCombo monitoring.
/// </summary>
public class DebugPanelWindow : Window, IDisposable
{
    private readonly ActionInterceptor _actionInterceptor;
    private readonly GameState _gameStateCore;
    private bool _disposed = false;

    public DebugPanelWindow(ActionInterceptor actionInterceptor, GameState gameState) 
        : base("ModernActionCombo Debug Panel")
    {
        _actionInterceptor = actionInterceptor ?? throw new ArgumentNullException(nameof(actionInterceptor));
        _gameStateCore = gameState ?? throw new ArgumentNullException(nameof(gameState));

        Size = new Vector2(480, 500);
    }

    public override void Draw()
    {
        // Trigger job detection when debug panel is drawn
        var currentJobName = JobProviderRegistry.GetJobName(GameStateCache.JobId);
        ImGui.TextColored(new Vector4(0.3f, 0.8f, 1.0f, 1.0f), $"=== {currentJobName} Debug Panel ===");
        ImGui.Separator();
        ImGui.Spacing();
        
        // Combat state section
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "Combat State:");
        using (var indent1 = ImRaiiComponents.BeginIndent())
        {
            ImGui.Text($"Next Ability: {GetNextAbility()}");
        }
        ImGui.Spacing();
        
        // Job gauge section - generic display
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "Job Gauge:");
        using (var indent2 = ImRaiiComponents.BeginIndent())
        {
            // Generic gauge data display
            var gaugeData1 = GameStateCache.GetGaugeData1();
            var gaugeData2 = GameStateCache.GetGaugeData2();
            
            ImGui.Text($"Gauge1: {gaugeData1}");
            ImGui.Text($"Gauge2: {gaugeData2}");
        }
        ImGui.Spacing();
        
        // Timers section - generic tracking data
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "Tracking Data:");
        using (var indent = ImRaiiComponents.BeginIndent())
        {
            // Show generic tracking info for active job
            var activeProvider = JobProviderRegistry.GetActiveProvider();
            if (activeProvider != null)
            {
                ImGui.Text($"Active Job Provider: {activeProvider.GetType().Name}");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "No active job provider");
            }
            
            // Movement and state tracking
            var movingColor = GameStateCache.IsMoving ? new Vector4(0.2f, 1.0f, 0.2f, 1.0f) : new Vector4(0.8f, 0.8f, 0.8f, 1.0f);
            var movingText = GameStateCache.IsMoving ? "Moving" : "Stationary";
            ImGui.TextColored(movingColor, $"Movement: {movingText}");
            
            // Additional game state info
            var combatColor = GameStateCache.InCombat ? new Vector4(1.0f, 0.4f, 0.4f, 1.0f) : new Vector4(0.8f, 0.8f, 0.8f, 1.0f);
            var combatText = GameStateCache.InCombat ? "In Combat" : "Out of Combat";
            ImGui.TextColored(combatColor, $"Combat: {combatText}");
            
            ImGui.Text($"Level: {GameStateCache.Level}");
            ImGui.Text($"Current MP: {GameStateCache.CurrentMp} / {GameStateCache.MaxMp}");
        }
        ImGui.Spacing();
        
        // Debug info section
        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "=== Job System ===");
        ImGui.Text($"Current Job: {JobProviderRegistry.GetJobName(GameStateCache.JobId)}");
        ImGui.Text($"Job ID: {GameStateCache.JobId}");
        ImGui.Text($"Combo Support: {(JobProviderRegistry.CurrentJobSupportsComboProcessing() ? "Yes" : "Display Only")}");
        ImGui.Text($"Job Info: {JobProviderRegistry.GetJobDisplayInfo()}");
        ImGui.Spacing();
        
        // Action Interceptor settings
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "=== Action Interceptor ===");
        
        // Mode selection
        var isPerformanceMode = ActionInterceptor.Mode == ActionInterceptionMode.PerformanceMode;
        var performanceModeAvailable = _actionInterceptor.IsPerformanceModeAvailable();
        
        if (!performanceModeAvailable)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey3);
        }
        
        if (ImGui.Checkbox("Performance Mode (Direct Input)", ref isPerformanceMode))
        {
            if (performanceModeAvailable)
            {
                var newMode = isPerformanceMode ? ActionInterceptionMode.PerformanceMode : ActionInterceptionMode.IconReplacement;
                _actionInterceptor.SwitchMode(newMode);
                ModernActionCombo.PluginLog?.Info($"Action mode switched to: {newMode}");
            }
            else
            {
                ModernActionCombo.PluginLog?.Warning("Performance Mode not available - UseAction address not found");
            }
        }
        
        if (!performanceModeAvailable)
        {
            ImGui.PopStyleColor();
        }
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "(?)");
        if (ImGui.IsItemHovered())
        {
            if (performanceModeAvailable)
            {
                ImGui.SetTooltip("Icon Replacement: Changes action icons on hotbar (traditional mode)\nPerformance Mode: Direct action execution for maximum speed");
            }
            else
            {
                ImGui.SetTooltip("Performance Mode not available - UseAction address not found\nUsing Icon Replacement mode only");
            }
        }

        var currentMode = ActionInterceptor.Mode == ActionInterceptionMode.PerformanceMode ? "Performance Mode (Direct Execution)" : "Icon Replacement (Traditional)";
        ImGui.Text($"Current Mode: {currentMode}");
        
        if (ImGui.Button("Clear Action Cache"))
        {
            _actionInterceptor.ClearCache();
            ModernActionCombo.PluginLog?.Info("Action cache cleared manually");
        }
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1.0f), "(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Clears the action resolution cache.\nUseful for testing or after job changes.");
        }

        ImGui.Spacing();
        
        // Generic gauge debug info
        var gaugeDebugInfo = "Generic gauge data";
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), $"Game State Cache: {_gameStateCore?.GetType().Name ?? "N/A"}");
        ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), $"Job Gauge: {gaugeDebugInfo}");
        
        // Job-specific warnings - only show if available
        var gauge1 = (int)GameStateCache.GetGaugeData1();
        var gauge2 = (int)GameStateCache.GetGaugeData2();
        
        // Generic gauge status display
        if (gauge1 > 0 || gauge2 > 0)
        {
            ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), "Gauge Status: Active");
        }
        else
        {
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Gauge Status: Empty");
        }
    }
    
    private string GetNextAbility()
    {
        // Generic next ability logic based on current job
        return "Next Ability (Job-Agnostic)";
    }
    
    private int GetGauge1()
    {
        return (int)GameStateCache.GetGaugeData1();
    }
    
    private int GetGauge2()
    {
        return (int)GameStateCache.GetGaugeData2();
    }
    
    private float GetGenericTimer()
    {
        // Generic timer tracking - would be replaced with job-specific logic
        return 0.0f;
    }
    
    private float GetTrackingTimer()
    {
        // Generic tracking timer
        return 0.0f;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}
