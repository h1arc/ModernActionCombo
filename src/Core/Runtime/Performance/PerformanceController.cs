using System;
using System.Diagnostics;
using System.Numerics;

namespace ModernActionCombo.Core.Runtime;

/// <summary>
/// Lightweight auto-throttling controller. Tracks frame time and plugin work time,
/// computes a ratio, and exposes simple guidance for optional work (e.g., companion scan).
/// It is FPS-agnostic: decisions are based on our work cost vs observed frame budget,
/// so a user capping to 30 FPS won't trigger throttling if our work is small.
/// </summary>
public static class PerformanceController
{
    // EMA smoothing
    private const float Alpha = 0.12f; // smoothing factor ~ 8-9 frame memory

    // Global enable/disable (disabled by default - users can enable if they want auto-throttling)
    public static bool AutoThrottleEnabled { get; set; } = false;

    private static long _lastFrameTicks;
    private static float _emaFrameMs;
    private static float _emaWorkMs;
    private static int _level; // 0=OK,1=Throttle,2=Degraded
    private static bool _inCombat;

    // Optional work cadence control
    private static uint _lastCompanionScanFrame;

    private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public static float AvgFrameMs => _emaFrameMs;
    public static float AvgWorkMs => _emaWorkMs;
    public static float Fps => _emaFrameMs > 0 ? 1000f / _emaFrameMs : 0f;
    public static int Level => _level;
    public static bool IsThrottling => AutoThrottleEnabled && _level >= 1;
    public static bool IsDegraded => AutoThrottleEnabled && _level >= 2;

    public static string StatusShort => !AutoThrottleEnabled ? "Disabled" : _level switch
    {
        0 => "OK",
        1 => "OK (throttling to preserve responsiveness)",
        _ => "Plugin may exhibit reduced responsiveness"
    };

    public static string StatusDetail =>
        $"FPS≈{Fps:F1} | Work {AvgWorkMs:F2}ms of {AvgFrameMs:F2}ms ({(AvgFrameMs > 0 ? (AvgWorkMs/AvgFrameMs*100f) : 0):F1}%)";

    public static Vector4 StatusColor => !AutoThrottleEnabled ? new Vector4(0.6f, 0.6f, 0.6f, 1f) : _level switch
    {
        0 => new Vector4(0.2f, 0.8f, 0.2f, 1f),
        1 => new Vector4(0.8f, 0.6f, 0.2f, 1f),
        _ => new Vector4(0.9f, 0.3f, 0.3f, 1f)
    };

    public static void StartFrame()
    {
        var nowTicks = _stopwatch.ElapsedTicks;
        if (_lastFrameTicks != 0)
        {
            var dtMs = (nowTicks - _lastFrameTicks) * 1000f / Stopwatch.Frequency;
            _emaFrameMs = _emaFrameMs <= 0 ? dtMs : (1 - Alpha) * _emaFrameMs + Alpha * dtMs;
        }
        _lastFrameTicks = nowTicks;
    }

    public static void EndFrame(double workMs, bool inCombat)
    {
        _inCombat = inCombat;
        var w = (float)workMs;
        _emaWorkMs = _emaWorkMs <= 0 ? w : (1 - Alpha) * _emaWorkMs + Alpha * w;
        RecomputeLevel();
    }

    private static void RecomputeLevel()
    {
        if (_emaFrameMs <= 0) { _level = 0; return; }
        var ratio = _emaWorkMs / _emaFrameMs; // portion of frame we consume

        // Separate thresholds for combat vs non-combat
        // Aim to keep our share under ~20% in combat, ~30% out of combat
        var t1 = _inCombat ? 0.22f : 0.30f; // throttle onset
        var t2 = _inCombat ? 0.35f : 0.50f; // degraded onset

        // Additional guard if absolute frame time is very high (stuttery), even if ratio small
        // Only trip when we also exceed a tiny absolute work floor to avoid false positives
        bool hitchGuard = _emaFrameMs > 40 && _emaWorkMs > 1.5f;

        _level = ratio >= t2 || hitchGuard && ratio >= t1 ? 2 : (ratio >= t1 ? 1 : 0);
    }

    /// <summary>
    /// Whether to run the companion scan on this frame.
    /// - Always when not throttling
    /// - When throttling (level 1): run if in combat, otherwise skip to save CPU
    /// - When degraded (level 2): run only every N frames in combat, skip out of combat
    /// </summary>
    public static bool ShouldRunCompanionScan(uint currentFrame)
    {
    if (!AutoThrottleEnabled) return true;
        if (_level == 0) return true;
        if (!_inCombat) return false; // skip out of combat when throttling

        if (_level == 1) return true; // keep responsiveness in combat

        // level 2: allow every 5 frames (~100ms at 50 FPS)
        const uint interval = 5;
        if (currentFrame - _lastCompanionScanFrame >= interval)
        {
            _lastCompanionScanFrame = currentFrame;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Helper for optional non-critical work out of combat.
    /// Runs always in combat or when not throttling; otherwise runs every N frames
    /// based on throttle level.
    /// </summary>
    public static bool ShouldRunOutOfCombat(uint currentFrame, uint intervalLevel1 = 2, uint intervalLevel2 = 5)
    {
    if (!AutoThrottleEnabled) return true;
        if (_inCombat || _level == 0) return true;
        var interval = _level == 1 ? intervalLevel1 : intervalLevel2;
        return interval == 0 || (currentFrame % interval) == 0;
    }
}
