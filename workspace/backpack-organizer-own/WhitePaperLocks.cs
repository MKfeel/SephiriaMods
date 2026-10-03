using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SephiriaBackpackOrganizer
{
    public partial class InventorySorter
    {
        private static void CapturePaperLocks(SearchContext ctx)
        {
            var rules = ctx.paperLocks;
            rules.Width = ctx.width;
            foreach (var item in ctx.items)
            {
                var source = new PaperCategoryItem { Charm = item.isCharm, Paper = item.isWhitePaper };
                if (item.isCharm) source.Categories = item.slot.charm.GetItemCategory().Distinct().ToArray();
                if (item.slot.charm is Charm_3Elemental_ByRow row)
                    source.RowCategories = (string[])row.lineCategory.Clone();
                rules.Items.Add(item.slot.instanceID, source);
            }
            foreach (var item in ctx.whitePapers)
            {
                var categories = rules.Items[item.slot.instanceID].Categories;
                if (categories.Length == 0) continue;
                var paper = (Charm_WhitePaper)item.slot.charm;
                if (paper.GetType() != typeof(Charm_WhitePaper) || paper.match != 2)
                    throw new InvalidOperationException("白纸锁定：暂不支持该复制类型，已保留原布局。");
                var target = new PaperComboLock { Instance = item.slot.instanceID, Categories = categories };
                rules.Targets.Add(target);
                int first = item.index, last = item.index;
                while (first % ctx.width > 0 && ctx.original[first - 1].hasItem && rules.Items[ctx.original[first - 1].instanceID].Paper) first--;
                while (last % ctx.width < ctx.width - 1 && last + 1 < ctx.storage && ctx.original[last + 1].hasItem && rules.Items[ctx.original[last + 1].instanceID].Paper) last++;
                if (first != last)
                {
                    target.Pinned = true;
                    int left = first % ctx.width > 0 ? first - 1 : first;
                    int right = last % ctx.width < ctx.width - 1 && last + 1 < ctx.storage ? last + 1 : last;
                    for (int c = left; c <= right; c++) rules.PinnedCells[c] = ctx.original[c].instanceID;
                }
                Plugin.Log.LogInfo($"白纸锁定 id={target.Instance} 连击=[{string.Join(",", categories)}] 相邻白纸保护={target.Pinned}");
            }
            if (!PaperLocksSatisfied(ctx, ctx.original))
                throw new InvalidOperationException("白纸原生连击与邻居快照不一致，已保留原布局，请待刷新完成后重试。");
        }
        private static bool PaperLocksSatisfied(SearchContext ctx, List<Slot> slots)
        {
            if (ctx.paperLocks.Targets.Count == 0) return true;
            if (ctx.paperCells == null || ctx.paperCells.Length != slots.Count) ctx.paperCells = new int[slots.Count];
            for (int i = 0; i < slots.Count; i++) ctx.paperCells[i] = slots[i].hasItem ? slots[i].instanceID : 0;
            return ctx.paperLocks.Matches(ctx.paperCells);
        }
        // Main thread only: compare synced native assignedCategory after refresh/application.
        private static bool NativePaperLocksSatisfied(SearchContext ctx, List<Slot> slots)
        {
            foreach (var target in ctx.paperLocks.Targets)
            {
                var slot = slots.Find(s => s.hasItem && s.instanceID == target.Instance);
                if (slot == null || !(slot.charm is Charm_WhitePaper paper) || !new HashSet<string>(paper.assignedCategory, StringComparer.Ordinal).SetEquals(target.Categories)) return false;
            }
            return true;
        }
        private static bool WaitForPaperSync(PendingEnhancedSort state)
        {
            if (state.paperMismatchSince == 0) state.paperMismatchSince = Stopwatch.GetTimestamp();
            return (Stopwatch.GetTimestamp() - state.paperMismatchSince) * 1000.0 / Stopwatch.Frequency < state.acknowledgementTimeoutMs;
        }
    }
}
