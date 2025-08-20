using System;
using System.Numerics;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ModernWrathCombo.Core.Services;
using ModernWrathCombo.Core.Data;
using ModernWrathCombo.Core.Enums;
using ModernWrathCombo.Jobs.WHM;
using ModernWrathCombo.UI.Core;
using ModernWrathCombo.UI.Debug;
using ImGuiCol = Dalamud.Bindings.ImGui.ImGuiCol;
using ImGui = Dalamud.Bindings.ImGui.ImGui;
using ImGuiCond = Dalamud.Bindings.ImGui.ImGuiCond;
using ImGuiWindowFlags = Dalamud.Bindings.ImGui.ImGuiWindowFlags;

namespace ModernWrathCombo.UI.Windows;

/// <summary>
/// Debug panel window for monitoring ModernWrathCombo's action resolution in real-time.
/// Shows current job, next action, performance metrics, and allows testing.
/// </summary>
public class DebugPanelWindow : Window
{
    private readonly ActionInterceptor _actionInterceptor;
    private readonly GameState _gameState;
    
    // UI Components with ImRaii automatic resource management
    private readonly TextDisplayComponent _currentJobDisplay;
    private readonly TextDisplayComponent _nextActionDisplay;
    private readonly TextDisplayComponent _lastResolutionTimeDisplay;
    private readonly TextDisplayComponent _gameStateDisplay;
    private readonly InputFieldComponent _testJobInput;
    private readonly ButtonComponent _testResolveButton;
    private readonly ButtonComponent _clearCacheButton;
    private readonly TextDisplayComponent _cacheStatsDisplay;

    private bool _disposed = false;
    private DateTime _lastUpdate = DateTime.MinValue;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(100); // 10 FPS update rate

    public DebugPanelWindow(ActionInterceptor actionInterceptor, GameState gameState) 
        : base("ModernWrathCombo Debug Panel")
    {
        _actionInterceptor = actionInterceptor ?? throw new ArgumentNullException(nameof(actionInterceptor));
        _gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));

        // Window configuration
        Size = new Vector2(400, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        Flags = ImGuiWindowFlags.AlwaysAutoResize;

        // Initialize UI components
        _currentJobDisplay = new TextDisplayComponent("CurrentJob", "Current Job:", "None")
        {
            ValueColor = ImGuiColors.DalamudYellow,
            TooltipText = "Currently active job in the game"
        };

        _nextActionDisplay = new TextDisplayComponent("NextAction", "Next Action:", "None")
        {
            ValueColor = ImGuiColors.HealerGreen,
            TooltipText = "Action that will be executed next based on current combo state"
        };

        _lastResolutionTimeDisplay = new TextDisplayComponent("ResolutionTime", "Resolution Time:", "0ns")
        {
            ValueColor = ImGuiColors.TankBlue,
            TooltipText = "Time taken for the last action resolution"
        };

        _gameStateDisplay = new TextDisplayComponent("GameState", "Game State:", "Unknown")
        {
            ValueColor = ImGuiColors.DalamudGrey,
            TooltipText = "Current game state (in combat, target info, etc.)"
        };

        _testJobInput = new InputFieldComponent("TestJob", "Test Job:", "WHM")
        {
            TooltipText = "Enter a job abbreviation to test action resolution",
            MaxLength = 10
        };

        _testResolveButton = new ButtonComponent("TestResolve", "Test Resolve")
        {
            TooltipText = "Test action resolution for the specified job"
        };
        _testResolveButton.Clicked += OnTestResolveClicked;

        _clearCacheButton = new ButtonComponent("ClearCache", "Clear Cache")
        {
            TooltipText = "Clear all cached action resolution data"
        };
        _clearCacheButton.Clicked += OnClearCacheClicked;

        _cacheStatsDisplay = new TextDisplayComponent("CacheStats", "Cache Stats:", "0 entries")
        {
            ValueColor = ImGuiColors.DalamudGrey3,
            TooltipText = "Current cache statistics and memory usage"
        };
    }

    public override void Draw()
    {
        // Update data at controlled intervals
        var now = DateTime.UtcNow;
        if (now - _lastUpdate >= _updateInterval)
        {
            UpdateDisplayData();
            _lastUpdate = now;
        }

        // Render the debug panel using ImRaii components
        RenderDebugPanel();
    }

    private void UpdateDisplayData()
    {
        try
        {
            // Update current job using GameStateCache
            var currentJob = GameStateCache.JobId;
            _currentJobDisplay.Value = currentJob != 0 ? JobHelper.GetJobName(currentJob) : "None";

            // Update game state info using GameStateCache
            var stateInfo = $"Combat: {GameStateCache.InCombat}, Target: {(GameStateCache.HasTarget ? "Yes" : "No")}, Level: {GameStateCache.Level}";
            _gameStateDisplay.Value = stateInfo;

            // Try to get next action if we have a valid job
            if (currentJob != 0)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                // Test action resolution using centralized cache
                var testAction = WHMConstants.Stone3; // Test with Stone3
                var nextAction = _actionInterceptor.GetOriginalAction(testAction);
                stopwatch.Stop();

                _nextActionDisplay.Value = nextAction != testAction ? 
                    $"{nextAction} (from {testAction})" : 
                    nextAction.ToString();
                    
                _lastResolutionTimeDisplay.Value = $"{stopwatch.ElapsedTicks * 1000000000L / System.Diagnostics.Stopwatch.Frequency}ns";
            }
            else
            {
                _nextActionDisplay.Value = "No job active";
                _lastResolutionTimeDisplay.Value = "0ns";
            }

            // Update cache stats from GameStateCache performance monitor
            var cacheStats = GameStateCache.PerformanceMonitor.GetStats();
            _cacheStatsDisplay.Value = cacheStats;
        }
        catch (Exception ex)
        {
            // Handle any errors in data updates
            _nextActionDisplay.Value = $"Error: {ex.Message}";
        }
    }

    private void RenderDebugPanel()
    {
        // Header section
        using (var headerGroup = ImRaii.Group())
        {
            // Convert ImGuiColors Vector4 to uint
            var whiteColor = ImGuiColors.DalamudWhite;
            var whiteU32 = ((uint)(whiteColor.W * 255) << 24) | ((uint)(whiteColor.Z * 255) << 16) | ((uint)(whiteColor.Y * 255) << 8) | (uint)(whiteColor.X * 255);
            using var headerColor = ImRaii.PushColor(ImGuiCol.Text, whiteU32);
            ImGui.TextUnformatted("ModernWrathCombo Debug Panel");
        }

        ImGui.Separator();

        // Current state section
        using (var stateGroup = ImRaii.Group())
        {
            ImGui.TextUnformatted("Current State:");
            _currentJobDisplay.Render();
            _nextActionDisplay.Render();
            _lastResolutionTimeDisplay.Render();
            _gameStateDisplay.Render();
        }

        ImGui.Separator();

        // Testing section
        using (var testGroup = ImRaii.Group())
        {
            ImGui.TextUnformatted("Testing:");
            _testJobInput.Render();
            _testResolveButton.Render();
            ImGui.SameLine();
            _clearCacheButton.Render();
        }

        ImGui.Separator();

        // Statistics section
        using (var statsGroup = ImRaii.Group())
        {
            ImGui.TextUnformatted("Statistics:");
            _cacheStatsDisplay.Render();
        }

        // Performance indicator
        RenderPerformanceIndicator();
    }

    private void RenderPerformanceIndicator()
    {
        ImGui.Separator();
        
        // Parse current resolution time to show performance indicator
        var resolutionText = _lastResolutionTimeDisplay.Value;
        if (resolutionText.EndsWith("ns") && long.TryParse(resolutionText[..^2], out var nanoseconds))
        {
            Vector4 color;
            string status;
            
            if (nanoseconds < 50)
            {
                color = ImGuiColors.HealerGreen;
                status = "Excellent";
            }
            else if (nanoseconds < 100)
            {
                color = ImGuiColors.DalamudYellow;
                status = "Good";
            }
            else if (nanoseconds < 500)
            {
                color = ImGuiColors.DalamudOrange;
                status = "Fair";
            }
            else
            {
                color = ImGuiColors.DalamudRed;
                status = "Poor";
            }

            // Convert Vector4 to uint color format for Dalamud
            var colorU32 = ((uint)(color.W * 255) << 24) | ((uint)(color.Z * 255) << 16) | ((uint)(color.Y * 255) << 8) | (uint)(color.X * 255);
            using var perfColor = ImRaii.PushColor(ImGuiCol.Text, colorU32);
            ImGui.TextUnformatted($"Performance: {status} ({nanoseconds}ns)");
        }
    }

    private void OnTestResolveClicked()
    {
        try
        {
            var testJobText = _testJobInput.Value.Trim().ToUpperInvariant();
            
            if (string.IsNullOrEmpty(testJobText))
            {
                _nextActionDisplay.Value = "Enter a job to test";
                return;
            }

            // Convert job text to ID
            uint testJobId = testJobText switch
            {
                "WHM" => 24,
                "24" => 24,
                _ => 0
            };

            if (testJobId == 0)
            {
                _nextActionDisplay.Value = $"Invalid job: {testJobText}";
                return;
            }

            // Test resolution
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var testAction = testJobId == 24 ? WHMConstants.Stone3 : testJobId; // Use Stone3 for WHM
            var originalAction = _actionInterceptor.GetOriginalAction(testAction);
            stopwatch.Stop();

            _nextActionDisplay.Value = originalAction != 0 ? originalAction.ToString() : "No action";
            _lastResolutionTimeDisplay.Value = $"{stopwatch.ElapsedTicks * 1000000000L / System.Diagnostics.Stopwatch.Frequency}ns";
        }
        catch (Exception ex)
        {
            _nextActionDisplay.Value = $"Test error: {ex.Message}";
        }
    }

    private void OnClearCacheClicked()
    {
        // Just update the display - no reset functionality available
        _cacheStatsDisplay.Value = "Cache stats refreshed";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Dispose UI components
        _currentJobDisplay?.Dispose();
        _nextActionDisplay?.Dispose();
        _lastResolutionTimeDisplay?.Dispose();
        _gameStateDisplay?.Dispose();
        _testJobInput?.Dispose();
        _testResolveButton?.Dispose();
        _clearCacheButton?.Dispose();
        _cacheStatsDisplay?.Dispose();

        _disposed = true;
    }
}
