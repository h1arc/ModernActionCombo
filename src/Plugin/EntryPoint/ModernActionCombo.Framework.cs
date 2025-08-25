using System;
using System.Diagnostics;
using Dalamud.Plugin.Services;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo;

// Framework update + game state update loop
public sealed partial class ModernActionCombo
{
    private void OnFrameworkUpdate(IFramework framework)
    {
        if (!_initialized && ClientState.LocalPlayer != null)
        {
            PerformDeferredInitialization();
            return;
        }

        if (_initialized)
        {
            UpdateGameState();
        }
    }

    private void UpdateGameState()
    {
        try
        {
            Core.Runtime.PerformanceController.StartFrame();
            var workSw = Stopwatch.StartNew();

            var localPlayer = ClientState.LocalPlayer;
            uint currentJob = localPlayer?.ClassJob.RowId ?? 0u;
            uint currentLevel = (uint)(localPlayer?.Level ?? 1);
            uint currentTarget = (uint)(localPlayer?.TargetObject?.GameObjectId ?? 0);
            bool inCombat = Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat];

            if (currentJob != _lastKnownJob)
            {
                _lastKnownJob = currentJob;
                JobProviderRegistry.OnJobChanged(currentJob);
                _cooldownsToTrack = JobProviderRegistry.GetAllCooldownsToTrack();
                Logger.Debug($"🔄 Job changed to: {currentJob}");
            }

            if (currentLevel != _lastKnownLevel)
            {
                _lastKnownLevel = currentLevel;
                JobProviderRegistry.OnLevelChanged(currentLevel);
                Logger.Debug($"📈 Level changed to: {currentLevel}");
            }

            if (currentTarget != _lastKnownTargetId)
            {
                _lastKnownTargetId = currentTarget;
                var targetName = localPlayer?.TargetObject?.Name.TextValue ?? string.Empty;
                if (currentTarget != 0)
                    Logger.Debug($"🎯 Target changed: id={currentTarget} name='{targetName}'");
                else
                    Logger.Debug("🎯 Target cleared");
            }

            var currentInDuty = Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty];
            var currentDutyId = (uint)ClientState.TerritoryType;
            if (currentInDuty != _lastInDuty || (currentInDuty && currentDutyId != _lastDutyId))
            {
                _lastInDuty = currentInDuty;
                _lastDutyId = currentInDuty ? currentDutyId : 0;
                JobProviderRegistry.OnDutyStateChanged(currentInDuty, currentInDuty ? currentDutyId : null);
                var stateText = currentInDuty ? $"entered duty {currentDutyId}" : "left duty";
                Logger.Debug($"🏰 Duty state changed: {stateText}");
            }

            if (inCombat != _lastInCombat)
            {
                _lastInCombat = inCombat;
                JobProviderRegistry.OnCombatStateChanged(inCombat);
                var stateText = inCombat ? "entered combat" : "left combat";
                Logger.Debug($"⚔️ Combat state changed: {stateText}");
            }

            var isMoving = DetectMovement();

            GameStateCache.UpdateCoreState(
                jobId: currentJob,
                level: currentLevel,
                targetId: currentTarget,
                zoneId: (uint)(ClientState.TerritoryType),
                inCombat: inCombat,
                hasTarget: currentTarget != 0,
                inDuty: Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty],
                canAct: !Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Casting],
                isMoving: isMoving
            );

            GameStateCache.UpdateScalarState(
                gcdRemaining: 0.0f,
                currentMp: (uint)(ClientState.LocalPlayer?.CurrentMp ?? 0),
                maxMp: (uint)(ClientState.LocalPlayer?.MaxMp ?? 0)
            );

            bool runOptional = inCombat || Core.Runtime.PerformanceController.ShouldRunOutOfCombat(GameStateCache.FrameStamp);

            if (runOptional)
            {
                UpdateTargetDebuffs();
                UpdatePlayerBuffs();
                UpdateActionCooldowns();
                UpdatePartyMembers();
                // Mark cleansable flags for self and party this frame
                MarkCleansablesForPartyAndSelf();
            }

            if (ClientState.LocalPlayer != null)
            {
                UpdateSmartTargetHardTarget(ClientState.LocalPlayer);
                // Companion scanning: per-job enable and auto-disable in duties; still subject to performance controller cadence
                if (GameStateCache.IsCompanionScanEnabledForJob(currentJob) && !GameStateCache.InDuty && Core.Runtime.PerformanceController.ShouldRunCompanionScan(GameStateCache.FrameStamp))
                {
                    UpdateCompanionDetection(ClientState.LocalPlayer);
                }
            }

            JobProviderRegistry.UpdateActiveJobGauge();

            Span<uint> ids = stackalloc uint[12];
            byte count = 0;
            var self = ClientState.LocalPlayer;
            if (self != null) ids[count++] = (uint)self.GameObjectId;
            var tgt = self?.TargetObject?.GameObjectId ?? 0;
            if (tgt != 0) ids[count++] = (uint)tgt;
            var partyCount = SmartTargetingCache.PartyCount;
            for (int i = 0; i < partyCount && count < ids.Length; i++)
            {
                var pid = SmartTargetingCache.GetMemberIdByIndex(i);
                if (pid != 0) ids[count++] = pid;
            }
            var compId = SmartTargetingCache.GetCompanionId();
            if (compId != 0 && count < ids.Length) ids[count++] = compId;
            var hardId = SmartTargetingCache.GetHardTargetId();
            if (hardId != 0 && count < ids.Length) ids[count++] = hardId;
            GameStateCache.RebuildKnownObjectsForFrame(ids.Slice(0, count));

            // Debounced config persistence
            ConfigSaveScheduler.TryFlushIfDue();

            workSw.Stop();
            Core.Runtime.PerformanceController.EndFrame(workSw.Elapsed.TotalMilliseconds, inCombat);
        }
        catch (Exception ex)
        {
            Logger.Error("Error updating game state", ex);
        }
    }

    private void MarkCleansablesForPartyAndSelf()
    {
        try
        {
            var local = ClientState.LocalPlayer;
            if (local != null)
            {
                bool selfCleansable = false;
                foreach (var s in local.StatusList)
                {
                    if (s.StatusId == 0) continue;
                    if (Core.Data.GameStateCache.IsStatusCleansable(s, (uint)s.StatusId)) { selfCleansable = true; break; }
                }
                Core.Data.GameStateCache.UpdateCleansableFlag((uint)local.GameObjectId, selfCleansable);
            }

            var party = ModernActionCombo.PartyList;
            if (party != null && party.Length > 0)
            {
                foreach (var pm in party)
                {
                    var obj = pm?.GameObject as Dalamud.Game.ClientState.Objects.Types.IBattleChara;
                    if (obj == null) continue;
                    bool cleansable = false;
                    foreach (var s in obj.StatusList)
                    {
                        if (s.StatusId == 0) continue;
                        if (Core.Data.GameStateCache.IsStatusCleansable(s, (uint)s.StatusId)) { cleansable = true; break; }
                    }
                    Core.Data.GameStateCache.UpdateCleansableFlag((uint)obj.GameObjectId, cleansable);
                }
            }

            // Companion flagging (only when companion scanning is enabled and we are not in duty)
            if (!Core.Data.GameStateCache.InDuty)
            {
                var compId = Core.Data.SmartTargetingCache.GetCompanionId();
                if (compId != 0 && Core.Data.GameStateCache.TryGetKnownObject(compId, out var compObj) && compObj is Dalamud.Game.ClientState.Objects.Types.IBattleChara cbc)
                {
                    bool cleansable = false;
                    foreach (var s in cbc.StatusList)
                    {
                        if (s.StatusId == 0) continue;
                        if (Core.Data.GameStateCache.IsStatusCleansable(s, (uint)s.StatusId)) { cleansable = true; break; }
                    }
                    Core.Data.GameStateCache.UpdateCleansableFlag(compId, cleansable);
                }
            }
        }
        catch { /* non-critical */ }
    }
}
