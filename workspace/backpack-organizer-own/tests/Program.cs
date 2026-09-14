using System;
using System.Collections.Generic;
using System.Linq;
using SephiriaBackpackOrganizer;

static class Program
{
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
    static LayoutObjective One(ItemMark mark, bool active, int level, int maximum = 3)
    {
        var result = new LayoutObjective(); result.Add(mark, active, level, maximum); return result;
    }
    static void Main()
    {
        // Potion belt is stored in the same native dictionary at y=100. It must
        // not invalidate readiness or enter the grid's charm/tablet counts.
        var mixed = new[] { (x:0,y:0), (x:3,y:5), (x:4,y:5), (x:0,y:100), (x:5,y:100), (x:0,y:-100) };
        var main = mixed.Where(p => InventoryScope.IsMainCell(p.x,p.y,34)).ToArray();
        Check(main.SequenceEqual(new[] { (x:0,y:0),(x:3,y:5) }), "main grid ignores potion belt and unused last-row cells");
        Check(!InventoryScope.IsMainCell(-1,1,34), "negative x must not alias previous row");
        Check(!InventoryScope.IsMainCell(6,0,34), "x beyond width must not alias next row");
        Check(!InventoryScope.IsMainCell(0,0,0), "empty storage");
        Check(!InventoryScope.IsMainCell(0,0,34,0), "invalid width");
        Check(!InventoryScope.IsMainCell(0,int.MaxValue,34), "coordinate multiplication does not overflow");
        foreach (int capacity in new[] { 24, 29, 34, 42 })
        {
            int total = 0;
            for (int y = -1; y <= 101; y++)
                for (int x = -1; x <= 6; x++) if (InventoryScope.IsMainCell(x,y,capacity)) total++;
            Check(total == capacity, "scope exactly matches native main-grid capacity");
        }
        var store = new MarkStore();
        Check(store.Resolve(1, true) == ItemMark.Max, "favorite supplies max");
        Check(store.Resolve(2, false) == ItemMark.None, "unfavorite default");
        Check(store.Toggle(2, false, false) == ItemMark.Raise, "middle first");
        Check(store.Toggle(2, false, false) == ItemMark.Max, "middle second");
        Check(store.Toggle(2, false, false) == ItemMark.None, "middle third");
        Check(store.Resolve(2, true) == ItemMark.None, "manual unmark survives favorite sync");
        Check(store.Toggle(1, true, true) == ItemMark.Enable, "control replaces auto max");
        Check(store.Toggle(1, true, true) == ItemMark.Negative, "control second");
        Check(store.Toggle(1, true, true) == ItemMark.None, "control third");
        Check(store.Resolve(1, true) == ItemMark.None, "manual default does not resurrect");
        Check(store.Toggle(1, true, false) == ItemMark.Raise, "switch from default to raise");
        Check(store.Resolve(1, false) == ItemMark.Raise, "unfavorite preserves explicit raise");
        Check(store.Resolve(3, true) == ItemMark.Max && store.Resolve(3, false) == ItemMark.None, "remove automatic max");
        Check(store.Resolve(4, true) == ItemMark.Max, "duplicate instance independent");
        store.Clear();
        Check(store.Resolve(1, true) == ItemMark.Max && store.Resolve(2, false) == ItemMark.None, "session reset");
        foreach (ItemMark mark in Enum.GetValues<ItemMark>())
        {
            Check(MarkStore.Next(mark, false) != ItemMark.Enable && MarkStore.Next(mark, false) != ItemMark.Negative, "middle group exclusive");
            Check(MarkStore.Next(mark, true) != ItemMark.Max && MarkStore.Next(mark, true) != ItemMark.Raise, "control group exclusive");
        }
        var full = One(ItemMark.Max, true, 3);
        var partial = One(ItemMark.Max, true, 2); partial.Ordinary = 1e20; partial.Raised = 9999; partial.Enabled = 9999;
        Check(full.CompareTo(partial) > 0, "no lower reward overrides max");
        var activated = One(ItemMark.Max, true, 0);
        var disabled = One(ItemMark.Max, false, 100);
        Check(activated.CompareTo(disabled) > 0, "activation precedes numeric level");
        var balanced = new LayoutObjective(); balanced.Add(ItemMark.Max, true, 2, 3); balanced.Add(ItemMark.Max, true, 2, 3);
        var skewed = new LayoutObjective(); skewed.Add(ItemMark.Max, true, 3, 3); skewed.Add(ItemMark.Max, true, 1, 3);
        Check(balanced.CompareTo(skewed) > 0, "same deficit distributes fairly");
        var keep = One(ItemMark.Enable, true, 0); var rise = One(ItemMark.Raise, true, 3);
        Check(keep.CompareTo(rise) > 0, "keep activation precedes raise");
        Check(One(ItemMark.Enable, true, 0).CompareTo(One(ItemMark.Enable, true, 8)) == 0, "enable needs no levels");
        Check(One(ItemMark.Raise, true, 3).CompareTo(One(ItemMark.Raise, true, 2)) > 0, "raise gains levels");
        Check(One(ItemMark.Raise, true, 3).CompareTo(One(ItemMark.Raise, true, 20)) == 0, "raise caps at max");
        Check(One(ItemMark.Negative, false, -1).CompareTo(One(ItemMark.Negative, false, -5)) == 0, "negative threshold not depth");
        Check(One(ItemMark.Negative, false, -1).CompareTo(One(ItemMark.Negative, true, 0)) > 0, "negative goal");
        var negative = One(ItemMark.Negative, false, -5); negative.Ordinary = 1e20;
        Check(rise.CompareTo(negative) > 0, "raise precedes negative");
        var stable = new LayoutObjective { Ordinary = 100, Stable = 0 };
        var moving = new LayoutObjective { Ordinary = 100, Stable = -8 };
        Check(stable.CompareTo(moving) > 0, "same value prefer fewer moves");
        var rng = new Random(12345);
        // Every valid translation preserves all objects and the relative shape, even when
        // source/destination overlap or the final inventory row is incomplete.
        foreach (int size in new[] { 24, 29, 34, 42 })
        foreach (var cells in new[] { new List<int>{0,1,2}, new List<int>{0,6,12}, new List<int>{7,8} })
        for (int target = 0; target < size; target++)
        {
            var slots = Enumerable.Range(0, size).ToList();
            bool moved = GroupMoves.Translate(slots, cells, target, 6);
            Check(slots.OrderBy(x => x).SequenceEqual(Enumerable.Range(0, size)), "group preserves permutation");
            if (moved)
            {
                int dx = target % 6 - cells[0] % 6, dy = target / 6 - cells[0] / 6;
                foreach (int cell in cells) Check(slots[(cell / 6 + dy) * 6 + cell % 6 + dx] == cell, "group shape and identity");
            }
            else Check(slots.SequenceEqual(Enumerable.Range(0, size)), "failed move is atomic");
        }
        for (int i = 0; i < 1000; i++)
        {
            var a = One(ItemMark.Max, true, rng.Next(4)); a.Ordinary = rng.NextDouble() * 1e8;
            var b = One(ItemMark.Max, true, rng.Next(4)); b.Ordinary = rng.NextDouble() * 1e8;
            Check(a.CompareTo(b) == -b.CompareTo(a), "comparator antisymmetry");
        }
        Console.WriteLine($"PASS {checks} assertions (mark rules and layered objectives)");
    }
}
