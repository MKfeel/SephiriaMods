using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SephiriaBackpackOrganizer
{
    public partial class InventorySorter
    {
        // EvaluateLayout refreshes the scratch grids; consume them before another evaluation.
        private LayoutObjective Objective(SearchContext ctx, List<Slot> slots)
        {
            double ordinary = EvaluateLayout(ctx, slots);
            var score = new LayoutObjective { Ordinary = ordinary };
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.hasItem) continue;
                if (slot.instanceID != ctx.original[i].instanceID) score.Stable--;
                if (!ctx.itemByInstance.TryGetValue(slot.instanceID, out var info) || !info.isCharm) continue;
                int level = (ctx.cellLevel[i] + info.enchant) * ctx.mysticFactor[i];
                bool active = !ctx.disabled[i] && level >= 0 && info.weaponOk && CriteriaSatisfied(ctx, info, slots, ctx.ignore[i], i);
                score.Add((ItemMark)info.manualPriorityRank, active, level, info.maxLevel);
                if (info.manualPriorityRank == (int)ItemMark.Enable) score.Ordinary -= Math.Max(0, level) * 100;
            }
            return score;
        }

        private List<Slot> RunHybridSearch(SearchContext ctx, List<Slot> original, out double before, out double after)
        {
            var best = CloneSlots(original);
            var bestScore = Objective(ctx, best);
            before = bestScore.Ordinary;
            long deadline = CreateSearchDeadline(plugin.SearchTimeBudgetMs.Value);
            var random = new Random(Environment.TickCount);
            var smart = plugin.EnableSmartStart.Value ? BuildSmartStart(ctx) : CloneSlots(original);
            var candidate = CloneSlots(original);
            // Independent starts explore different basins; keep the best globally.
            for (int start = 0; start < 8 && !ctx.cancelled && !SearchDeadlineReached(deadline); start++)
            {
                ctx.annealStarts++;
                var current = CloneSlots(start == 0 ? original : start == 1 ? smart : start % 2 == 0 ? original : best);
                if (start >= 2)
                {
                    // Include empty cells so random starts can relocate the entire arrangement.
                    for (int i = current.Count - 1; i > 0; i--) SwapSlots(current, i, random.Next(i + 1));
                    ScrambleForSearch(ctx, current, random);
                }
                if (!RestoreCompassBindings(ctx, current) || !CompassBindingsSatisfied(ctx, current)) continue;
                var currentScore = Objective(ctx, current);
                if (currentScore.CompareTo(bestScore) > 0) { CopySlots(current, best); bestScore = currentScore; }
                double observedLoss = 1;
                for (int step = 0; step < 18000 && !ctx.cancelled && !SearchDeadlineReached(deadline); step++)
                {
                    CopySlots(current, candidate);
                    EvaluateLayout(ctx, current);
                    int move = random.Next(100);
                    if (move < 20 && TryMoveBoundGroup(ctx, candidate, random)) { }
                    else if (move < 42) ShuffleNeighborhood(ctx, candidate, random);
                    else Mutate(ctx, candidate, random);
                    if (!RestoreCompassBindings(ctx, candidate) || !CompassBindingsSatisfied(ctx, candidate)) continue;
                    var score = Objective(ctx, candidate);
                    ctx.annealEvaluations++;
                    if (score.CompareTo(bestScore) > 0) { CopySlots(candidate, best); bestScore = score; }
                    double delta = score.FirstDifference(currentScore);
                    if (delta < 0) observedLoss = observedLoss * 0.95 + Math.Min(100, -delta) * 0.05;
                    double temperature = Math.Max(0.001, observedLoss * Math.Pow(1.0 - step / 18000.0, 3));
                    if (delta >= 0 || random.NextDouble() < Math.Exp(delta / temperature))
                    {
                        var old = current; current = candidate; candidate = old; currentScore = score;
                    }
                }
                if (!ctx.cancelled && !SearchDeadlineReached(deadline)) ctx.annealStartsCompleted++;
            }
            // Deterministic finishing: test every swap and every movable tablet's
            // destination+rotation together, which single random mutations often miss.
            for (int pass = 0; pass < 64 && !ctx.cancelled && !SearchDeadlineReached(deadline); pass++)
            {
                bool improved = false;
                for (int i = 0; i < best.Count && !ctx.cancelled && !SearchDeadlineReached(deadline); i++)
                {
                    if (!best[i].hasItem) continue;
                    int rotations = ctx.itemByInstance.TryGetValue(best[i].instanceID, out var info) && info.isStele && info.tabletRotatable ? 4 : 1;
                    for (int j = 0; j < best.Count && !ctx.cancelled && !SearchDeadlineReached(deadline); j++)
                    {
                        for (int rotation = 0; rotation < rotations && !ctx.cancelled && !SearchDeadlineReached(deadline); rotation++)
                        {
                            CopySlots(best, candidate);
                            SwapSlots(candidate, i, j);
                            if (rotations == 4) candidate[j].rotation = rotation;
                            if (!RestoreCompassBindings(ctx, candidate) || !CompassBindingsSatisfied(ctx, candidate)) continue;
                            var score = Objective(ctx, candidate);
                            ctx.annealEvaluations++;
                            if (score.CompareTo(bestScore) <= 0) continue;
                            CopySlots(candidate, best); bestScore = score; improved = true;
                            // The item at i changed; reevaluate its rotation eligibility.
                            break;
                        }
                        if (improved) break;
                    }
                    if (improved) break;
                }
                if (!improved) break;
            }
            ctx.searchBudgetReached = SearchDeadlineReached(deadline);
            after = bestScore.Ordinary;
            return best;
        }

        // Reinsert a whole compass chain, or move a currently useful pair/triple together.
        private bool TryMoveBoundGroup(SearchContext ctx, List<Slot> slots, Random random)
        {
            var cells = new List<int>();
            if (ctx.compassChains.Count > 0 && random.Next(2) == 0)
            {
                var chain = ctx.compassChains[random.Next(ctx.compassChains.Count)];
                foreach (int id in chain.instanceIDs)
                    for (int i = 0; i < slots.Count; i++) if (slots[i].hasItem && slots[i].instanceID == id) { cells.Add(i); break; }
            }
            else
            {
                int start = random.Next(slots.Count);
                for (int j = 0; j < slots.Count; j++)
                {
                    int i = (start + j) % slots.Count;
                    if (!slots[i].hasItem || !ctx.itemByInstance.TryGetValue(slots[i].instanceID, out var info)) continue;
                    int x = i % ctx.width;
                    if (info.isHourglass && x + 1 < ctx.width && i + 1 < slots.Count && IsMagic(ctx, slots[i + 1])) { cells.Add(i); cells.Add(i + 1); break; }
                    if (info.isRayShard && x > 0 && IsMagic(ctx, slots[i - 1])) { cells.Add(i - 1); cells.Add(i); break; }
                    if (info.isWhitePaper && x > 0 && x + 1 < ctx.width && i + 1 < slots.Count && slots[i - 1].charm is not null && slots[i + 1].charm is not null) { cells.Add(i - 1); cells.Add(i); cells.Add(i + 1); break; }
                }
            }
            return GroupMoves.Translate(slots, cells, random.Next(slots.Count), ctx.width);
        }

        private static bool IsMagic(SearchContext ctx, Slot slot) => slot.hasItem && ctx.itemByInstance.TryGetValue(slot.instanceID, out var info) && info.isMagicBook;

        private void ShuffleNeighborhood(SearchContext ctx, List<Slot> slots, Random random)
        {
            int anchor = random.Next(slots.Count);
            // Marked artifacts are useful anchors, but ordinary cells remain searchable.
            if (random.Next(2) == 0)
                for (int i = 0; i < slots.Count; i++)
                {
                    int k = (anchor + i) % slots.Count;
                    if (slots[k].hasItem && ctx.itemByInstance.TryGetValue(slots[k].instanceID, out var info) && info.manualPriorityRank != 0) { anchor = k; break; }
                }
            var cells = new List<int>();
            int ax = anchor % ctx.width, ay = anchor / ctx.width;
            for (int i = 0; i < slots.Count; i++)
                if (Math.Abs(i % ctx.width - ax) <= 1 && Math.Abs(i / ctx.width - ay) <= 1) cells.Add(i);
            for (int i = cells.Count - 1; i > 0; i--) SwapSlots(slots, cells[i], cells[random.Next(i + 1)]);
            foreach (int i in cells)
                if (slots[i].hasItem && ctx.itemByInstance.TryGetValue(slots[i].instanceID, out var info) && info.isStele && info.tabletRotatable) slots[i].rotation = random.Next(4);
        }
    }
}
