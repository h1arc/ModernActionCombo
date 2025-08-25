using System.Runtime.CompilerServices;

namespace ModernActionCombo.Core.Data;

public static unsafe partial class GameStateCache
{
    #region SmartTargeting Integration
    // Per-frame known object cache (IDs -> weak references). Cleared when FrameStamp changes.
    private static uint _knownFrameStamp;
    private static readonly Dictionary<uint, Dalamud.Game.ClientState.Objects.Types.IGameObject?> _knownObjects = new();
    
    /// <summary>
    /// Gets the best smart target using percentage-based healing decisions.
    /// More fair across different job HP pools (tank vs caster equality).
    /// Default 1.0f threshold means anyone with ANY missing HP is eligible.
    /// Integrates with SmartTargetingCache for sub-50ns performance.
    /// Priority: Hard Target > Best Party Member > Self
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetSmartTarget(float hpThreshold = 1.0f) =>
        SmartTargetingCache.GetSmartTarget(hpThreshold);
    
    /// <summary>
    /// Check if SmartTargeting is ready and has valid party data.
    /// </summary>
    public static bool IsSmartTargetingReady => SmartTargetingCache.IsReady;
    public static bool DidPartyChangeThisFrame => SmartTargetingCache._partyChangedThisFrame;
    
    /// <summary>
    /// Hard target detection is now automatic via status flags.
    /// This method is kept for compatibility but does nothing.
    /// Hard targets are detected automatically when UpdateSmartTargetData is called.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetSmartTargetHardTarget(uint memberId)
    {
        // No-op: Hard targets are now detected automatically from status flags
        // The game will set the HardTargetFlag in the status when calling UpdateSmartTargetData
    }
    
    /// <summary>
    /// Check if a specific target is valid for smart targeting.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsValidSmartTarget(uint memberId) =>
        SmartTargetingCache.IsValidTarget(memberId);
    
    /// <summary>
    /// Check if a specific target needs healing below the threshold.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TargetNeedsHealing(uint memberId, float threshold = 0.95f) =>
        SmartTargetingCache.NeedsHealing(memberId, threshold);

    /// <summary>
    /// Provider-agnostic wrapper: update party data once per frame.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateSmartTargetParty(Span<uint> memberIds, Span<float> hpPercentages, Span<uint> statusFlags, byte memberCount)
    {
        SmartTargetingCache.UpdatePartyData(memberIds, hpPercentages, statusFlags, memberCount);
    }

    /// <summary>
    /// Provider-agnostic wrapper: update hard target validity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateSmartTargetHardTarget(uint targetId, bool isValid)
    {
        SmartTargetingCache.UpdateHardTarget(targetId, isValid);
    }

    /// <summary>
    /// Provider-agnostic wrapper: update companion system toggles and data.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateSmartTargetCompanionState(bool enabled, bool inDuty)
    {
        SmartTargetingCache.UpdateCompanionSystemState(enabled, inDuty);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateSmartTargetCompanionData(uint companionId, float hpPercent, bool isValid)
    {
        SmartTargetingCache.UpdateCompanionData(companionId, hpPercent, isValid);
    }

    /// <summary>
    /// Clears and repopulates the per-frame known object map. Call once per Framework tick.
    /// Only stores objects by ID that we care about (self, target, party, companion, hard target).
    /// </summary>
    public static void RebuildKnownObjectsForFrame(ReadOnlySpan<uint> objectIds)
    {
        if (_knownFrameStamp == FrameStamp) return;
        _knownObjects.Clear();
        _knownFrameStamp = FrameStamp;
        try
        {
            // Small, fixed set of IDs per frame; avoid heap allocations by using stackalloc flags
            int n = objectIds.Length;
            if (n <= 0) return;
            Span<bool> resolved = n <= 32 ? stackalloc bool[n] : new bool[n];
            int resolvedCount = 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static int IndexOfId(ReadOnlySpan<uint> ids, uint id)
            {
                for (int i = 0; i < ids.Length; i++) if (ids[i] == id) return i;
                return -1;
            }

            // Local player (direct)
            var local = ModernActionCombo.ClientState.LocalPlayer;
            if (local != null)
            {
                var id = (uint)local.GameObjectId;
                var idx = IndexOfId(objectIds, id);
                if (idx >= 0 && !resolved[idx]) { _knownObjects[id] = local; resolved[idx] = true; resolvedCount++; }
            }

            // Current target (direct)
            var targetObj = ModernActionCombo.ClientState.LocalPlayer?.TargetObject;
            if (targetObj != null && resolvedCount < n)
            {
                var id = (uint)targetObj.GameObjectId;
                var idx = IndexOfId(objectIds, id);
                if (idx >= 0 && !resolved[idx]) { _knownObjects[id] = targetObj; resolved[idx] = true; resolvedCount++; }
            }

            // Party members (direct)
            var party = ModernActionCombo.PartyList;
            if (party != null && party.Length > 0 && resolvedCount < n)
            {
                for (int i = 0; i < party.Length && resolvedCount < n; i++)
                {
                    var m = party[i];
                    var obj = m?.GameObject;
                    if (obj is null) continue;
                    var id = (uint)obj.GameObjectId;
                    var idx = IndexOfId(objectIds, id);
                    if (idx >= 0 && !resolved[idx]) { _knownObjects[id] = obj; resolved[idx] = true; resolvedCount++; }
                }
            }

            // Resolve any remaining IDs via ObjectTable scan with early exit
            if (resolvedCount < n)
            {
                var table = ModernActionCombo.ObjectTable;
                if (table is null) return;
                foreach (var obj in table)
                {
                    if (obj is null) continue;
                    var id = (uint)obj.GameObjectId;
                    var idx = IndexOfId(objectIds, id);
                    if (idx >= 0 && !resolved[idx]) { _knownObjects[id] = obj; resolved[idx] = true; resolvedCount++; if (resolvedCount >= n) break; }
                }
            }
        }
        catch
        {
            // Ignore resolution errors in headless contexts
        }
    }

    /// <summary>
    /// Try get a known object by ID from the per-frame map. Fast path for hooks.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetKnownObject(uint id, out Dalamud.Game.ClientState.Objects.Types.IGameObject? obj)
    {
        if (_knownFrameStamp != FrameStamp)
        {
            obj = null; return false;
        }
        return _knownObjects.TryGetValue(id, out obj!);
    }
    
    #endregion

    #region Cleansable Debuff Tracking and Per-Job Companion Settings
    private static uint _cleansableFrameStamp;
    private static readonly HashSet<uint> _cleansableIds = new();
    private static readonly Dictionary<uint, bool> _statusCleansableCache = new(256);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureCleansableFrame()
    {
        if (_cleansableFrameStamp != FrameStamp)
        {
            _cleansableIds.Clear();
            _cleansableFrameStamp = FrameStamp;
        }
    }

    /// <summary>
    /// Mark whether an entity has at least one cleansable debuff this frame.
    /// Call from party/companion update passes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateCleansableFlag(uint id, bool hasCleansable)
    {
        if (id == 0) return;
        EnsureCleansableFrame();
        if (hasCleansable) _cleansableIds.Add(id); else _cleansableIds.Remove(id);
    }

    /// <summary>
    /// Query if an entity has a cleansable debuff this frame.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasCleansableDebuff(uint id)
    {
        EnsureCleansableFrame();
        return _cleansableIds.Contains(id);
    }

    /// <summary>
    /// Determine if a status is cleansable using Dalamud runtime property when available, else fallback to Lumina Status sheet heuristics.
    /// Caches results by StatusId for performance.
    /// </summary>
    public static bool IsStatusCleansable(object statusObj, uint statusId)
    {
        if (statusId == 0) return false;
        if (_statusCleansableCache.TryGetValue(statusId, out var cached)) return cached;

        bool result = false;
        try
        {
            // Try runtime property first (Dalamud often exposes CanDispel on Status)
            var prop = statusObj.GetType().GetProperty("CanDispel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                if (prop.GetValue(statusObj) is bool b) result = b;
            }
            else
            {
                // Excel fallback via Lumina
                var dm = ModernActionCombo.DataManager;
                var sheet = dm?.GetExcelSheet<Lumina.Excel.Sheets.Status>();
                var row = sheet?.GetRow(statusId);
                if (row != null)
                {
                    // Reflect on the row for a property that indicates dispellable
                    var rowType = row.GetType();
                    var canDispelProp = rowType.GetProperty("CanDispel") ?? rowType.GetProperty("CanDispell") ?? rowType.GetProperty("IsRemovable");
                    if (canDispelProp != null && canDispelProp.PropertyType == typeof(bool))
                    {
                        if (canDispelProp.GetValue(row) is bool rb) result = rb;
                    }
                }
            }
        }
        catch { /* best-effort */ }

        _statusCleansableCache[statusId] = result;
        return result;
    }

    /// <summary>
    /// Per-job companion scan enabled flag. Defaults to true if not set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCompanionScanEnabledForJob(uint jobId)
    {
        var cfg = ConfigurationManager.GetJobConfiguration(jobId);
        if (!cfg.JobSettings.ContainsKey("CompanionScanEnabled"))
            return GlobalSettings.CompanionScanEnabled;
        return cfg.GetSetting("CompanionScanEnabled", GlobalSettings.CompanionScanEnabled);
    }

    /// <summary>
    /// Per-job companion override setting for smart targeting (companion may override party target when significantly lower HP).
    /// Defaults to false if not set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsCompanionOverrideEnabledForJob(uint jobId)
    {
        var cfg = ConfigurationManager.GetJobConfiguration(jobId);
        if (!cfg.JobSettings.ContainsKey("CompanionOverrideEnabled"))
            return GlobalSettings.CompanionOverrideWhenLowerHp;
        return cfg.GetSetting("CompanionOverrideEnabled", GlobalSettings.CompanionOverrideWhenLowerHp);
    }

    /// <summary>
    /// Per-job companion override delta (HP% difference required). Defaults to 0.25f.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetCompanionOverrideDeltaForJob(uint jobId)
    {
        var cfg = ConfigurationManager.GetJobConfiguration(jobId);
        if (!cfg.JobSettings.ContainsKey("CompanionOverrideDelta"))
            return GlobalSettings.CompanionOverrideHpDelta;
        return cfg.GetSetting("CompanionOverrideDelta", GlobalSettings.CompanionOverrideHpDelta);
    }
    #endregion
}
