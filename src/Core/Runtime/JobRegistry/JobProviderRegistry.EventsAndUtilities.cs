using System;
using System.Collections.Generic;
using System.Reflection;
using ModernActionCombo.Core.Attributes;
using ModernActionCombo.Core.Interfaces;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo.Core.Services;

public static partial class JobProviderRegistry
{
    #region Events
    /// <summary>
    /// Called when the player's level changes to update ability availability.
    /// This can invalidate cached action resolutions.
    /// </summary>
    public static void OnLevelChanged(uint newLevel)
    {
        // Invalidate fast resolver cache on level change
        _fastResolver = null;
        _cachedJobId = 0;
        _cachedConfigVersion = 0;

        // Notify only the active provider
        try
        {
            if (_activeProvider is ILevelChangeHandler levelHandler)
            {
                levelHandler.OnLevelChanged(newLevel);
            }
        }
        catch (Exception ex)
        {
            ModernActionCombo.PluginLog?.Warning($"Active provider failed to handle level change: {ex.Message}");
        }

    Logger.Debug("📈 Level changed to {0} - fast resolver reset; active provider notified", newLevel);
    }

    /// <summary>
    /// Called when duty state changes (entering/leaving duties).
    /// This can affect rotation priorities and available actions.
    /// </summary>
    public static void OnDutyStateChanged(bool inDuty, uint? dutyId = null)
    {
        try
        {
            if (_activeProvider is IDutyStateHandler dutyHandler)
            {
                dutyHandler.OnDutyStateChanged(inDuty, dutyId);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("Active provider failed to handle duty state change: {0}", ex.Message);
        }

        var stateText = inDuty ? $"entered duty {dutyId}" : "left duty";
    Logger.Debug("🏰 Duty state changed: {0} - active provider notified", stateText);
    }

    /// <summary>
    /// Called when combat state changes (entering/leaving combat).
    /// This can affect action priorities and rotations.
    /// </summary>
    public static void OnCombatStateChanged(bool inCombat)
    {
        try
        {
            if (_activeProvider is ICombatStateHandler combatHandler)
            {
                combatHandler.OnCombatStateChanged(inCombat);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning("Active provider failed to handle combat state change: {0}", ex.Message);
        }

        var stateText = inCombat ? "entered combat" : "left combat";
    Logger.Debug("⚔️ Combat state changed: {0} - active provider notified", stateText);
    }
    #endregion

    #region Gauge Management
    public static void UpdateActiveJobGauge()
    {
        if (_activeProvider?.AsGaugeProvider() is IGaugeProvider gaugeProvider)
        {
            try
            {
                gaugeProvider.UpdateGauge();
            }
            catch (Exception ex)
            {
                Logger.Error($"❌ Failed to update gauge for active job: {ex}");
            }
        }
    }

    public static void UpdateGauge(uint jobId)
    {
        if (_activeProvider != null && _providers.TryGetValue(jobId, out var provider) && provider == _activeProvider)
        {
            if (provider.AsGaugeProvider() is IGaugeProvider gaugeProvider)
            {
                try
                {
                    gaugeProvider.UpdateGauge();
                }
                catch (Exception ex)
                {
                    Logger.Error($"❌ Failed to update gauge for job {jobId}: {ex}");
                }
            }
        }
    }
    #endregion

    #region Tracking Data
    public static uint[] GetAllDebuffsToTrack()
    {
        // Active-only semantics: we only track what the current job cares about
        if (_activeProvider?.AsTrackingProvider() is ITrackingProvider trackingProvider)
        {
            try { return trackingProvider.GetDebuffsToTrack(); }
            catch (Exception ex)
            {
                Logger.Error($"❌ Error getting debuffs from active provider: {ex}");
            }
        }
        return Array.Empty<uint>();
    }

    public static uint[] GetAllBuffsToTrack()
    {
        if (_activeProvider?.AsTrackingProvider() is ITrackingProvider trackingProvider)
        {
            try { return trackingProvider.GetBuffsToTrack(); }
            catch (Exception ex)
            {
                Logger.Error($"❌ Error getting buffs from active provider: {ex}");
            }
        }
        return Array.Empty<uint>();
    }

    public static uint[] GetAllCooldownsToTrack()
    {
        if (_activeProvider?.AsTrackingProvider() is ITrackingProvider trackingProvider)
        {
            try { return trackingProvider.GetCooldownsToTrack(); }
            catch (Exception ex)
            {
                Logger.Error($"❌ Error getting cooldowns from active provider: {ex}");
            }
        }
        return Array.Empty<uint>();
    }
    #endregion

    #region Utility
    // Per-frame OGCD suggestions cache
    private static uint _ogcdCacheFrame;
    private static uint[] _ogcdCache = Array.Empty<uint>();

    /// <summary>
    /// Gets suggested oGCDs, materialized once per GameState frame to avoid re-enumeration cost.
    /// </summary>
    public static IEnumerable<uint> GetSuggestedOGCDs()
    {
        if (_activeProvider is not IOGCDProvider ogcdProvider)
            return Array.Empty<uint>();

        var frame = Core.Data.GameStateCache.FrameStamp;
        if (_ogcdCacheFrame == frame && _ogcdCache.Length > 0)
            return _ogcdCache;

        try
        {
            var list = new List<uint>(8);
            foreach (var id in ogcdProvider.GetSuggestedOGCDs())
            {
                list.Add(id);
            }
            _ogcdCache = list.ToArray();
            _ogcdCacheFrame = frame;
            return _ogcdCache;
        }
        catch (Exception ex)
        {
            Logger.Warning($"Failed to materialize OGCD suggestions: {ex.Message}");
            _ogcdCache = Array.Empty<uint>();
            _ogcdCacheFrame = frame;
            return _ogcdCache;
        }
    }

    /// <summary>
    /// Fast span access for hot paths to avoid IEnumerable allocations.
    /// </summary>
    public static ReadOnlySpan<uint> GetSuggestedOGCDsSpan()
    {
        var e = GetSuggestedOGCDs();
        if (e is uint[] arr) return arr;
        // Fallback: materialize now
        var list = new List<uint>(8);
        foreach (var id in e) list.Add(id);
        _ogcdCache = list.ToArray();
        _ogcdCacheFrame = Core.Data.GameStateCache.FrameStamp;
        return _ogcdCache;
    }

    public static string GetAllDebugInfo()
    {
        if (!_initialized || _providers.Count == 0)
            return "No providers registered";

        var info = new List<string>
        {
            $"=== Job Provider Registry ({_providers.Count} providers) ===",
            $"Active Provider: {(_activeProvider != null ? "Yes" : "No")}"
        };

        foreach (var kvp in _providers)
        {
            try
            {
                var type = kvp.Value.GetType();
                var attribute = type.GetCustomAttribute<JobComboAttribute>();
                info.Add($"Job {kvp.Key} ({attribute?.JobName ?? GetJobName(kvp.Key)}): {kvp.Value.GetJobDisplayInfo()}");
            }
            catch (Exception ex)
            {
                info.Add($"Job {kvp.Key}: Error - {ex.Message}");
            }
        }

        return string.Join("\n", info);
    }
    #endregion
}
