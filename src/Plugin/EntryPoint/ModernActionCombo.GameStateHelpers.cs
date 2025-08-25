using System;
using System.Reflection;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.Enums;
using FFXIVClientStructs.FFXIV.Client.Game;
using ModernActionCombo.Core.Data;
using ModernActionCombo.Core.Runtime;
using ModernActionCombo.Core.Services;

namespace ModernActionCombo;

// Methods used by the game loop split out into a helper partial
public sealed partial class ModernActionCombo
{
    private void UpdateTargetDebuffs()
    {
        var debuffs = _scratchDebuffs;
        debuffs.Clear();

        var target = ClientState.LocalPlayer?.TargetObject;
        if (target is IBattleChara battleChara)
        {
            foreach (var status in battleChara.StatusList)
            {
                if (status.StatusId != 0)
                    debuffs[status.StatusId] = status.RemainingTime;
            }
        }
        GameStateCache.UpdateTargetDebuffs(debuffs);
    }

    private void UpdatePlayerBuffs()
    {
        var buffs = _scratchBuffs;
        buffs.Clear();

        var player = ClientState.LocalPlayer;
        if (player != null)
        {
            foreach (var status in player.StatusList)
            {
                if (status.StatusId != 0)
                    buffs[status.StatusId] = status.RemainingTime;
            }
        }
        GameStateCache.UpdatePlayerBuffs(buffs);
    }

    private unsafe void UpdateActionCooldowns()
    {
        var cooldowns = _scratchCooldowns;
        cooldowns.Clear();
        try
        {
            // Use cached list; refresh if not available (e.g., at startup)
            if (_cooldownsToTrack.Length == 0)
                _cooldownsToTrack = JobProviderRegistry.GetAllCooldownsToTrack();
            foreach (var actionId in _cooldownsToTrack)
            {
                var actionManager = ActionManager.Instance();
                var cooldownRemaining = actionManager->GetRecastTime(ActionType.Action, actionId) -
                                        actionManager->GetRecastTimeElapsed(ActionType.Action, actionId);
                cooldowns[actionId] = Math.Max(0, cooldownRemaining);
            }
        }
        catch (Exception ex)
        {
            Logger.Warning($"Error updating action cooldowns: {ex.Message}");
        }
        GameStateCache.UpdateActionCooldowns(cooldowns);
    }

    // Update party members for SmartTargeting using zero-allocation stack buffers (max 8)
    private void UpdatePartyMembers()
    {
        var local = ClientState.LocalPlayer;
        if (local == null)
            return;

        var party = ModernActionCombo.PartyList;

        // Local bit layout must match SmartTargetingCache flags
        const uint AliveFlag = 1u << 0;
        const uint InRangeFlag = 1u << 1;
        const uint InLosFlag = 1u << 2;
        const uint TargetableFlag = 1u << 3;
        const uint SelfFlag = 1u << 4;
        const uint AllyFlag = 1u << 10;

        if (party == null || party.Length == 0)
        {
            Span<uint> memberIds = stackalloc uint[1];
            Span<float> hpPercentages = stackalloc float[1];
            Span<uint> statusFlags = stackalloc uint[1];

            memberIds[0] = (uint)local.GameObjectId;
            if (local is IBattleChara lbc && lbc.MaxHp > 0)
                hpPercentages[0] = (float)lbc.CurrentHp / lbc.MaxHp;
            else
                hpPercentages[0] = 1.0f;

            uint flags = AliveFlag | InRangeFlag | InLosFlag | TargetableFlag | SelfFlag | AllyFlag;
            statusFlags[0] = flags;
            GameStateCache.UpdateSmartTargetParty(memberIds, hpPercentages, statusFlags, 1);
            return;
        }

        Span<uint> ids = stackalloc uint[8];
        Span<float> hps = stackalloc float[8];
        Span<uint> flagsArr = stackalloc uint[8];
        byte count = 0;

        var max = Math.Min(party.Length, 8);
        for (int i = 0; i < max; i++)
        {
            var member = party[i];
            var obj = member?.GameObject;
            if (obj is null) continue;

            ids[count] = (uint)obj.GameObjectId;
            if (obj is IBattleChara bc && bc.MaxHp > 0)
                hps[count] = (float)bc.CurrentHp / bc.MaxHp;
            else
                hps[count] = 1.0f;

            uint flags = InRangeFlag | InLosFlag | TargetableFlag | AllyFlag;
            if (obj is IBattleChara aliveBc)
            {
                if (aliveBc.CurrentHp > 0) flags |= AliveFlag;
            }
            else
            {
                // Non-battle objects are treated as alive for targeting
                flags |= AliveFlag;
            }
            if (obj.GameObjectId == local.GameObjectId) flags |= SelfFlag;
            flagsArr[count] = flags;
            count++;
        }

        if (count == 0)
        {
            // fallback to solo
            Span<uint> memberIds = stackalloc uint[1];
            Span<float> hpPercentages = stackalloc float[1];
            Span<uint> statusFlags = stackalloc uint[1];
            memberIds[0] = (uint)local.GameObjectId;
            hpPercentages[0] = (local is IBattleChara slbc && slbc.MaxHp > 0) ? (float)slbc.CurrentHp / slbc.MaxHp : 1.0f;
            statusFlags[0] = AliveFlag | InRangeFlag | InLosFlag | TargetableFlag | SelfFlag | AllyFlag;
            GameStateCache.UpdateSmartTargetParty(memberIds, hpPercentages, statusFlags, 1);
        }
        else
        {
            GameStateCache.UpdateSmartTargetParty(ids, hps, flagsArr, count);
        }
    }

    // Use engine validation to decide if current hard target is valid for healing
    private void UpdateSmartTargetHardTarget(Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter localPlayer)
    {
        var currentTarget = localPlayer?.TargetObject;
        if (currentTarget == null)
        {
            GameStateCache.UpdateSmartTargetHardTarget(0, false);
            return;
        }

        var targetId = (uint)currentTarget.GameObjectId;
        bool canHeal = CanUseHealingActionOnTarget(currentTarget);
        GameStateCache.UpdateSmartTargetHardTarget(targetId, canHeal);
    }

    // Companion scan: pick the lowest-HP valid companion this frame
    private void UpdateCompanionDetection(Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter localPlayer)
    {
        bool inDuty = Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty];
        bool includeChocobo = !inDuty && Core.Data.GameStateCache.IsCompanionScanEnabledForJob(Core.Data.GameStateCache.JobId);

        // Update system state based on setting and duty status
        GameStateCache.UpdateSmartTargetCompanionState(includeChocobo, inDuty);
        if (inDuty)
        {
            GameStateCache.UpdateSmartTargetCompanionData(0, 1.0f, false);
            return;
        }

    if (!includeChocobo)
        {
            // Feature disabled; ensure no companion selected
            GameStateCache.UpdateSmartTargetCompanionData(0, 1.0f, false);
            return;
        }

    uint bestId = 0;
    float bestHp = 1.0f;
    bool found = false;
    int candidateCount = 0;

        var table = ObjectTable;
        if (table != null)
        {
            foreach (var obj in table)
            {
                // Accept either explicit Companion kind or BattleNpc of Chocobo subkind
                bool isChocoboKind = obj.ObjectKind == ObjectKind.Companion
                                      || (obj is Dalamud.Game.ClientState.Objects.Types.IBattleNpc bn && bn.BattleNpcKind == BattleNpcSubKind.Chocobo);
                if (!isChocoboKind)
                {
                    continue;
                }

                if (obj is not IBattleChara bc)
                {
                    continue;
                }

                // Only consider our own chocobo (owner = local player). Try unsafe OwnerId first, IBattleNpc.OwnerId, reflection, then name fallback.
                bool owned = IsOwnedByLocalPlayerUnsafe(obj) || IsOwnedByLocalPlayer(obj) || IsPlayersChocoboByName(obj);
                if (!owned)
                {
                    continue;
                }

                if (bc.MaxHp <= 0) continue;
                candidateCount++;
                float hpPct = (float)bc.CurrentHp / bc.MaxHp;
                if (hpPct < bestHp)
                {
                    bestHp = hpPct;
                    bestId = (uint)obj.GameObjectId;
                    found = true;
                }
            }
        }

        GameStateCache.UpdateSmartTargetCompanionData(bestId, bestHp, found);
        if (found)
        {
            try
            {
                // Mark cleansable flag for companion this frame
                if (GameStateCache.TryGetKnownObject(bestId, out var compObj) && compObj is IBattleChara cbc)
                {
                    bool cleansable = false;
                    foreach (var s in cbc.StatusList)
                    {
                        if (s.StatusId == 0) continue;
                        if (Core.Data.GameStateCache.IsStatusCleansable(s, (uint)s.StatusId)) { cleansable = true; break; }
                    }
                    Core.Data.GameStateCache.UpdateCleansableFlag(bestId, cleansable);
                }
            }
            catch { /* best-effort */ }
        }
    }

    // Attempts to read an OwnerId property and compare with the local player's GameObjectId
    private bool IsOwnedByLocalPlayer(Dalamud.Game.ClientState.Objects.Types.IGameObject obj)
    {
        try
        {
            var local = ClientState.LocalPlayer;
            if (local == null) return false;
            var type = obj.GetType();
            if (!_ownerIdPropertyCache.TryGetValue(type, out var prop))
            {
                prop = type.GetProperty("OwnerId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                _ownerIdPropertyCache[type] = prop;
            }
            if (prop == null) return false;
            var val = prop.GetValue(obj);
            if (val == null) return false;
            // Handle common numeric representations
            ulong owner = val switch
            {
                ulong u64 => u64,
                long i64 => unchecked((ulong)i64),
                uint u32 => u32,
                int i32 => unchecked((uint)i32),
                _ => 0UL
            };
            return owner != 0 && owner == (ulong)local.GameObjectId;
        }
        catch { return false; }
    }

    // Fallback for clients where OwnerId is not exposed: match on name pattern
    private bool IsPlayersChocoboByName(Dalamud.Game.ClientState.Objects.Types.IGameObject obj)
    {
        try
        {
            var local = ClientState.LocalPlayer;
            if (local == null) return false;
            var name = obj.Name?.TextValue;
            if (string.IsNullOrEmpty(name)) return false;
            var playerName = local.Name?.TextValue;
            if (string.IsNullOrEmpty(playerName)) return false;

            // Basic English fallback: "<Player>'s Chocobo"; keep case-insensitive
            // This won’t cover all locales but serves as a safe fallback when OwnerId is missing.
            return name.IndexOf(playerName, StringComparison.OrdinalIgnoreCase) >= 0
                   && name.IndexOf("Chocobo", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch { return false; }
    }

    // Direct OwnerId check from ClientStructs for reliability across environments
    private unsafe bool IsOwnedByLocalPlayerUnsafe(Dalamud.Game.ClientState.Objects.Types.IGameObject obj)
    {
        try
        {
            var local = ClientState.LocalPlayer;
            if (local == null) return false;
            var ptr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)obj.Address;
            if (ptr == null) return false;
            return ptr->OwnerId == (uint)local.GameObjectId;
        }
        catch { return false; }
    }

    // Engine-side healability test using Cure (WHM) as a permissive check
    private unsafe bool CanUseHealingActionOnTarget(Dalamud.Game.ClientState.Objects.Types.IGameObject target)
    {
        const uint CureActionId = 120; // WHM Cure
        try
        {
            var ptr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;
            return ActionManager.CanUseActionOnTarget(CureActionId, ptr);
        }
        catch
        {
            return false;
        }
    }
}
