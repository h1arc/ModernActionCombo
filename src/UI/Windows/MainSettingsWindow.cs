using System;
using System.Linq;
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
/// Main plugin settings window accessed via the ConfigUI hook.
/// Contains global plugin settings like Direct Input mode and cache refresh rates.
/// </summary>
public class MainSettingsWindow : Window, IDisposable
{
    private readonly ActionInterceptor _actionInterceptor;
    private readonly GameState _gameState;
    private readonly WindowSystem _windowSystem;
    
    // Settings state
    private bool _directInputMode = false;
    private int _primaryCacheRefreshMs = 50;    // 25ms - 100ms range
    private int _secondaryCacheRefreshMs = 100; // 50ms - 250ms range
    
    // Debounced save mechanism to prevent excessive saves during slider dragging
    private long _lastSettingsChangeTime = 0;
    private bool _pendingSettingsSave = false;
    private const long SaveDebounceMs = 500; // Wait 500ms after last change before saving
    
    private bool _disposed = false;

    public MainSettingsWindow(ActionInterceptor actionInterceptor, GameState gameState, WindowSystem windowSystem) 
        : base("ModernActionCombo - Main Settings###MacMainSettings")
    {
        _actionInterceptor = actionInterceptor ?? throw new ArgumentNullException(nameof(actionInterceptor));
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
        _windowSystem = windowSystem ?? throw new ArgumentNullException(nameof(windowSystem));

        Size = new Vector2(450, 350);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        LoadSettings();
    }

    /// <summary>
    /// Load settings from configuration manager.
    /// </summary>
    private void LoadSettings()
    {
        try
        {
            // Load current cache refresh rates from SmartTargetingCache
            _primaryCacheRefreshMs = (int)SmartTargetingCache.GetPrimaryCacheRefreshRate();
            _secondaryCacheRefreshMs = (int)SmartTargetingCache.GetSecondaryCacheRefreshRate();
            
            // TODO: Load DirectInput mode from global config when available
            _directInputMode = false; // TODO: Load from global config
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Warning($"Failed to load main settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Save settings to configuration manager.
    /// </summary>
    private void SaveSettings()
    {
        try
        {
            // Apply cache refresh rate settings immediately (no cache invalidation needed)
            SmartTargetingCache.SetPrimaryCacheRefreshRate(_primaryCacheRefreshMs);
            SmartTargetingCache.SetSecondaryCacheRefreshRate(_secondaryCacheRefreshMs);
            
            // TODO: Save to global configuration once that system is implemented
            ModernActionCombo.PluginLog?.Debug($"Settings applied: DirectInput={_directInputMode}, " +
                                             $"PrimaryCache={_primaryCacheRefreshMs}ms, SecondaryCache={_secondaryCacheRefreshMs}ms");
            
            _pendingSettingsSave = false;
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Warning($"Failed to save main settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Mark settings as changed and schedule a debounced save.
    /// This prevents excessive saves during slider dragging.
    /// </summary>
    private void MarkSettingsChanged()
    {
        _lastSettingsChangeTime = Environment.TickCount64;
        _pendingSettingsSave = true;
    }

    /// <summary>
    /// Check if enough time has passed since last change and save if needed.
    /// Call this from Draw() to handle debounced saves.
    /// </summary>
    private void ProcessPendingSave()
    {
        if (_pendingSettingsSave && Environment.TickCount64 - _lastSettingsChangeTime >= SaveDebounceMs)
        {
            SaveSettings();
        }
    }

    public override void Draw()
    {
        // Process any pending debounced saves
        ProcessPendingSave();
        
        ImGui.TextColored(new Vector4(0.3f, 0.8f, 1.0f, 1.0f), "🔧 ModernActionCombo - Main Settings");
        ImGui.Separator();
        ImGui.Spacing();

        // Plugin Status Section
        DrawPluginStatus();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Core Settings Section
        DrawCoreSettings();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Performance Settings Section  
        DrawPerformanceSettings();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Action Buttons
        DrawActionButtons();
    }

    /// <summary>
    /// Draw plugin status and quick info.
    /// </summary>
    private void DrawPluginStatus()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "📊 Plugin Status");
        
        using (var indent = ImRaiiComponents.BeginIndent())
        {
            var currentJobName = JobProviderRegistry.GetJobName(GameStateCache.JobId);
            var isJobSupported = JobProviderRegistry.HasProvider(GameStateCache.JobId);
            
            ImGui.Text($"Current Job: {currentJobName}");
            
            if (isJobSupported)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1.0f), "✓ Supported");
            }
            else
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.8f, 0.6f, 0.2f, 1.0f), "⚠ Not Implemented");
            }
            
            ImGui.Text($"In Combat: {(GameStateCache.InCombat ? "Yes" : "No")}");
            ImGui.Text($"In Duty: {(GameStateCache.InDuty ? "Yes" : "No")}");
            ImGui.Text($"Can Act: {(GameStateCache.CanUseAbilities ? "Yes" : "No")}");
        }
    }

    /// <summary>
    /// Draw core plugin settings.
    /// </summary>
    private void DrawCoreSettings()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "⚙️ Core Settings");
        
        using (var indent = ImRaiiComponents.BeginIndent())
        {
            // Direct Input Mode Toggle
            var directInputChanged = ImGui.Checkbox("Direct Input Mode", ref _directInputMode);
            if (directInputChanged)
            {
                MarkSettingsChanged();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("Enable direct input mode for action processing.");
                ImGui.Text("⚠️ This is an advanced setting that may affect performance.");
                ImGui.Text("Only enable if you understand the implications.");
                ImGui.EndTooltip();
            }
            
            // Show current input mode status
            ImGui.SameLine();
            if (_directInputMode)
            {
                ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1.0f), "(Enabled)");
            }
            else
            {
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "(Standard Mode)");
            }
        }
    }

    /// <summary>
    /// Draw performance and cache settings.
    /// </summary>
    private void DrawPerformanceSettings()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "⚡ Performance Settings");
        
        using (var indent = ImRaiiComponents.BeginIndent())
        {
            // Primary Cache Refresh Rate (25ms - 100ms)
            ImGui.Text("Primary Cache Refresh Rate:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            
            var primaryChanged = ImGui.SliderInt("##PrimaryCacheMs", ref _primaryCacheRefreshMs, 25, 100, "%d ms");
            if (primaryChanged)
            {
                MarkSettingsChanged();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("How often the main targeting cache refreshes.");
                ImGui.Text("Lower values = more responsive, higher CPU usage");
                ImGui.Text("Higher values = less responsive, lower CPU usage");
                ImGui.Text("Recommended: 50ms for balanced performance");
                ImGui.EndTooltip();
            }

            // Secondary Cache Refresh Rate (50ms - 250ms)  
            ImGui.Text("Secondary Cache Refresh Rate:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200);
            
            var secondaryChanged = ImGui.SliderInt("##SecondaryCacheMs", ref _secondaryCacheRefreshMs, 50, 250, "%d ms");
            if (secondaryChanged)
            {
                MarkSettingsChanged();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.Text("How often the companion detection cache refreshes.");
                ImGui.Text("Lower values = more responsive companion targeting");
                ImGui.Text("Higher values = less CPU usage for companion detection");
                ImGui.Text("Recommended: 100ms (companions change HP slowly)");
                ImGui.EndTooltip();
            }

            ImGui.Spacing();
            
            // Performance Impact Indicator
            var totalRefreshLoad = (1000.0f / _primaryCacheRefreshMs) + (1000.0f / _secondaryCacheRefreshMs);
            ImGui.Text($"Total Refresh Load: {totalRefreshLoad:F1} ops/sec");
            
            if (totalRefreshLoad > 50)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.8f, 0.4f, 0.2f, 1.0f), "(High)");
            }
            else if (totalRefreshLoad > 30)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.8f, 0.8f, 0.2f, 1.0f), "(Medium)");
            }
            else
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.2f, 0.8f, 0.2f, 1.0f), "(Low)");
            }
            
            // Pending save indicator
            if (_pendingSettingsSave)
            {
                ImGui.Spacing();
                var timeLeft = SaveDebounceMs - (Environment.TickCount64 - _lastSettingsChangeTime);
                ImGui.TextColored(new Vector4(0.8f, 0.6f, 0.2f, 1.0f), $"⏳ Saving in {Math.Max(0, timeLeft / 100) / 10.0f:F1}s");
            }
        }
    }

    /// <summary>
    /// Draw action buttons for settings management.
    /// </summary>
    private void DrawActionButtons()
    {
        ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), "🎮 Actions");
        
        using (var indent = ImRaiiComponents.BeginIndent())
        {
            // Reset to Defaults button
            if (ImGui.Button("Reset to Defaults"))
            {
                _directInputMode = false;
                _primaryCacheRefreshMs = 50;
                _secondaryCacheRefreshMs = 100;
                MarkSettingsChanged();
            }
            
            ImGui.SameLine();
            
            // Open Job Config button
            if (ImGui.Button("Open Job Configuration"))
            {
                // Find and open the job config window
                var jobConfigWindow = _windowSystem.Windows.OfType<JobConfigWindow>().FirstOrDefault();
                if (jobConfigWindow != null)
                {
                    jobConfigWindow.IsOpen = true;
                    ModernActionCombo.PluginLog?.Information("Job configuration opened from main settings");
                }
                else
                {
                    ModernActionCombo.PluginLog?.Warning("Job configuration window not found");
                }
            }
            
            ImGui.Spacing();
            
            // Info text
            ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1.0f), "Access this window via the Settings button in Plugin Installer");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        SaveSettings();
        _disposed = true;
    }
}
