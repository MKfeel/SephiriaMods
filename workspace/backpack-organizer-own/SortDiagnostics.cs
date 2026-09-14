using System;
using System.Collections.Generic;

namespace SephiriaBackpackOrganizer
{
    public partial class InventorySorter
    {
        private static string FormatObjective(LayoutObjective s) =>
            $"双上启用={s.MaxActive},缺级={-s.MaxLevels},缺级平方和={-s.MaxBalance},单下启用={s.Enabled},单上有效等级={s.Raised},双下负等级数={s.Negative},普通分={s.Ordinary:R},稳定分={s.Stable}";

        // Main-thread snapshots only: never query Unity/Mirror objects from the worker.
        // Diagnostics must not turn an otherwise successful sort into a failed request.
        private void LogSortDiagnostics(PendingEnhancedSort state, List<Slot> slots, string phase)
        {
            try
            {
                var ctx = state.ctx;
                var predicted = Objective(ctx, slots);
                var actualMarks = new LayoutObjective();
                if (phase == "开始") state.diagnosticBefore = predicted;
                Plugin.Log.LogInfo($"整理诊断 #{state.diagnosticId} {phase} UTC={DateTime.UtcNow:O} frame={UnityEngine.Time.frameCount} storage={ctx.storage} marksRevision={state.marksRevision}/{ManualPriorityManager.Revision} inventoryRevision={state.inventoryRevision}/{ManualPriorityManager.InventoryRevision} budgetMs={plugin.SearchTimeBudgetMs.Value} budgetReached={ctx.searchBudgetReached} host={Mirror.NetworkServer.active}");
                Plugin.Log.LogInfo($"整理目标 #{state.diagnosticId} {phase} 预测[{FormatObjective(predicted)}]");
                if (phase == "结束")
                    Plugin.Log.LogInfo($"整理取舍 #{state.diagnosticId} 分层比较={predicted.CompareTo(state.diagnosticBefore)} 开始[{FormatObjective(state.diagnosticBefore)}] 结束[{FormatObjective(predicted)}]；正数仅代表完整分层目标改善，不代表普通分或游戏分提高。");
                int mismatches = 0;
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i];
                    if (!slot.hasItem || !ctx.itemByInstance.TryGetValue(slot.instanceID, out var info)) continue;
                    string prefix = $"整理物品 #{state.diagnosticId} {phase} id={slot.instanceID} entity={slot.entityID} cell=({i % ctx.width},{i / ctx.width}) rotation={slot.rotation} quantity={slot.quantity}";
                    if (!info.isCharm || slot.charm == null)
                    {
                        Plugin.Log.LogInfo(prefix + $" tablet={info.isStele}");
                        continue;
                    }
                    int level = (ctx.cellLevel[i] + info.enchant) * ctx.mysticFactor[i];
                    bool active = !ctx.disabled[i] && level >= 0 && info.weaponOk && CriteriaSatisfied(ctx, info, slots, ctx.ignore[i], i);
                    int actualLevel = slot.charm.DisplayedLevel;
                    bool actualActive = slot.charm.IsEffectEnabled;
                    actualMarks.Add((ItemMark)info.manualPriorityRank, actualActive, actualLevel, info.maxLevel);
                    bool differs = level != actualLevel || active != actualActive;
                    if (differs) mismatches++;
                    Plugin.Log.LogInfo(prefix + $" type={slot.charm.GetType().Name} mark={ManualPriorityManager.Label(info.manualPriorityRank)} priority={info.priority} max={info.maxLevel} predictedLevel={level} displayedLevel={actualLevel} effectLevel={slot.charm.EffectEnabledLevel} limitedEffectLevel={slot.charm.limitedEffectEnabledLevel} predictedActive={active} actualActive={actualActive} base={ctx.baseLevel[i]} cellLevel={ctx.cellLevel[i]} enchant={info.enchant} multiplier={ctx.mysticFactor[i]} disabled={ctx.disabled[i]} ignore={ctx.ignore[i]} weaponOk={info.weaponOk} mismatch={differs}");
                }
                Plugin.Log.LogInfo($"整理校验 #{state.diagnosticId} {phase} 原生标记统计[{FormatObjective(actualMarks)}] 预测差异件数={mismatches}；这是当前帧同步值，差异需结合后续快照判断，非最终模型错误结论。原生统计不计算普通分与稳定分。");
                Plugin.Log.LogInfo($"整理基础格 #{state.diagnosticId} {phase} base=[{string.Join(",", ctx.baseLevel)}] mystic=[{string.Join(",", ctx.mysticFactor)}]");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"整理诊断 #{state.diagnosticId} {phase} 采集失败（不影响整理）：{ex}");
            }
        }
    }
}
