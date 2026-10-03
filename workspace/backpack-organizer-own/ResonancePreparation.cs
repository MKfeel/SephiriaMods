using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SephiriaBackpackOrganizer
{
    public partial class InventorySorter
    {
        private bool resonanceAttempted;
        private enum ResonanceStage { None, Applying, Waiting, Restoring }
        private static JObject CaptureResonance(SearchContext ctx)
        {
            foreach (var info in ctx.items)
                if (info.slot.charm is Charm_GrowthMoveSpeed growth && growth.hasGrowthQuest && growth.reward != null)
                    return new JObject { ["uid"] = info.slot.instanceID.ToString(), ["sourceId"] = info.slot.entityID, ["rewardId"] = growth.reward.id, ["threshold"] = growth.moveSpeedPercentForGrowth };
            return null;
        }
        private void StartResonanceApply(PendingEnhancedSort state)
        {
            if ((string)state.ctx.buildModel?.Result?["phase"] != "resonance") return;
            var quest = JObject.Parse(state.ctx.buildModel.Json)["resonance"];
            state.resonanceUid = int.Parse((string)quest["uid"]);
            state.resonanceSourceId = (int)quest["sourceId"];
            state.resonanceRewardId = (int)quest["rewardId"];
            state.resonanceThreshold = (float)quest["threshold"];
            state.resonanceStage = ResonanceStage.Applying;
            resonanceAttempted = true;
            Plugin.Log.LogInfo("共鸣石准备：先应用达标移速布局，等待游戏原生成长，再恢复原格位并进行流派整理。");
        }
        // The native growth task replaces entity ID while preserving instance ID.
        // Accept exactly that replacement at the expected position. Any unrelated
        // user/network change remains subject to the normal cancellation checks.
        private bool ObserveResonanceEvolution(PendingEnhancedSort state)
        {
            if (state.resonanceStage != ResonanceStage.Applying && state.resonanceStage != ResonanceStage.Waiting) return false;
            if (!BuildWeaponUnchanged(state.ctx) || state.marksRevision != ManualPriorityManager.Revision || IsDragging()) return false;
            var observed = CaptureState(state.inv);
            int c = observed.FindIndex(x => x.hasItem && x.instanceID == state.resonanceUid && x.entityID == state.resonanceRewardId);
            if (c < 0 || state.expected == null || c >= state.expected.Count || state.expected[c].instanceID != state.resonanceUid || state.expected[c].entityID != state.resonanceSourceId) return false;
            var expected = CloneSlots(state.expected); expected[c] = observed[c].Clone();
            if (!LayoutsEquivalent(observed, expected)) return false;
            var position = state.inv.IdxToPos(c);
            bool rewardReady = observed[c].charm != null && state.inv.charms.TryGetValue(position, out var mapped) && mapped == observed[c].charm;
            if (!rewardReady || !VerifyInventorySnapshot(state.inv, observed))
            {
                if (state.resonanceWaitingSince <= 0) state.resonanceWaitingSince = Time.unscaledTime;
                if (Time.unscaledTime - state.resonanceWaitingSince > state.acknowledgementTimeoutMs / 1000f) RestartRequest(state);
                return true;
            }
            Plugin.Log.LogInfo($"共鸣石原生成长已确认：{state.resonanceSourceId} -> {state.resonanceRewardId}；当前移速 {state.inv.UnitAvatar.GetMoveSpeedMultiplier() * 100f:F2}。");
            RestoreAfterResonance(state, observed);
            return true;
        }
        private void RestoreAfterResonance(PendingEnhancedSort state, List<Slot> current)
        {
            var byUid = current.Where(x => x.hasItem).ToDictionary(x => x.instanceID);
            var target = new List<Slot>();
            foreach (var old in state.original)
            {
                if (!old.hasItem) { target.Add(Slot.Empty()); continue; }
                if (!byUid.TryGetValue(old.instanceID, out var now)) { RestartRequest(state); return; }
                var copy = now.Clone(); copy.rotation = old.rotation; target.Add(copy);
            }
            state.resonanceStage = ResonanceStage.Restoring;
            // Restore only geometry, retaining the evolved native item and its ID.
            state.original = CloneSlots(target);
            BeginMovePlan(state, current, target, rollingBack: true);
        }
        private bool CompleteResonancePhase(PendingEnhancedSort state)
        {
            if (state.resonanceStage == ResonanceStage.None) return false;
            if (state.resonanceStage == ResonanceStage.Restoring || state.rollingBack)
            {
                state.applying = false; state.stopwatch.Stop();
                RestartRequest(state);
                Plugin.Log.LogInfo("共鸣石准备阶段结束，重新采集原生属性并执行完整流派整理。");
                return true;
            }
            state.resonanceStage = ResonanceStage.Waiting;
            state.resonanceWaitingSince = Time.unscaledTime;
            state.applying = false;
            return true;
        }
        private void PollResonance(PendingEnhancedSort state)
        {
            if (ObserveResonanceEvolution(state)) return;
            if (!BuildWeaponUnchanged(state.ctx) || state.marksRevision != ManualPriorityManager.Revision || IsDragging() || !LayoutsEquivalent(CaptureState(state.inv), state.expected))
            { RestartRequest(state); return; }
            // Await the game/server's ordinary OnUpdate and inventory replication.
            // Never set quest counters, movement stats, or item IDs ourselves.
            if (Time.unscaledTime - state.resonanceWaitingSince < Math.Max(2f, state.acknowledgementTimeoutMs / 1000f)) return;
            Plugin.Log.LogWarning($"共鸣石未在等待期内成长（实际移速 {state.inv.UnitAvatar.GetMoveSpeedMultiplier() * 100f:F2}，需要 {state.resonanceThreshold:F0}）；恢复原格位后继续正常整理。");
            RestoreAfterResonance(state, CaptureState(state.inv));
        }
    }
}
