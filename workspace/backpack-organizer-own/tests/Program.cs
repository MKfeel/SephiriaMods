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
    static void Main(string[] args)
    {
        FreeGainProof Proof(double regen = 18) => new FreeGainProof
        {
            Schema = 1, Invariant = "same native effects",
            Stats = new Dictionary<string, double> { ["DEFENSE"] = 0, ["EVASION"] = 500, ["MP_REGEN"] = regen, ["MP_STEAL"] = 5 },
            Guards = new Dictionary<string, double> { ["supply"] = regen / 10, ["output"] = 96.424213248, ["level:other"] = 4 }
        };
        var primaryBefore = new LayoutObjective { Ordinary = 787.2072326645247, Stable = 0 };
        var primaryAfter = primaryBefore; primaryAfter.Stable = -1;
        Check(BuildLayoutAcceptance.Accepts(primaryAfter, primaryBefore, Proof(21), Proof()), "free regen outranks movement on equal primary objectives");
        Check(!BuildLayoutAcceptance.Accepts(primaryAfter, primaryBefore, null, null), "uncertified movement still rejected");
        Check(!BuildLayoutAcceptance.Accepts(primaryAfter, primaryBefore, Proof(), Proof()), "equal stats do not justify moving");
        var badProof = Proof(21); badProof.Stats["DEFENSE"] = -1;
        Check(!BuildLayoutAcceptance.Accepts(primaryAfter, primaryBefore, badProof, Proof()), "regen never buys defense loss");
        badProof = Proof(21); badProof.Guards["level:other"]--;
        Check(!BuildLayoutAcceptance.Dominates(badProof, Proof()), "other item level protected");
        badProof = Proof(21); badProof.Invariant = "changed homing or native position";
        Check(!BuildLayoutAcceptance.Dominates(badProof, Proof()), "native effects protected");
        badProof = Proof(21); badProof.Stats.Remove("MP_STEAL");
        Check(!BuildLayoutAcceptance.Dominates(badProof, Proof()), "missing stat fails closed");
        badProof = Proof(21); badProof.Stats["UNKNOWN"] = 100;
        Check(!BuildLayoutAcceptance.Dominates(badProof, Proof()), "unexpected stat fails closed");
        badProof = Proof(21); badProof.Guards.Clear();
        Check(!BuildLayoutAcceptance.Dominates(badProof, Proof()), "empty guards fail closed");
        badProof = Proof(double.NaN);
        Check(!BuildLayoutAcceptance.Dominates(badProof, Proof()), "nonfinite gain fails closed");
        primaryAfter.Ordinary--;
        Check(!BuildLayoutAcceptance.Accepts(primaryAfter, primaryBefore, Proof(21), Proof()), "primary score outranks free stats");
        primaryAfter = primaryBefore; primaryAfter.MaxActive--;
        Check(!BuildLayoutAcceptance.Accepts(primaryAfter, primaryBefore, Proof(21), Proof()), "manual mark outranks free stats");
        primaryAfter = primaryBefore; primaryAfter.Ordinary = double.NaN;
        Check(!BuildLayoutAcceptance.Accepts(primaryAfter, primaryBefore, Proof(21), Proof()), "nonfinite primary fails closed");
        primaryAfter = primaryBefore; primaryAfter.Ordinary -= 1e-10; primaryAfter.Stable = -1;
        Check(BuildLayoutAcceptance.Accepts(primaryAfter, primaryBefore, Proof(21), Proof()), "same numeric tolerance as worker");
        Check(BeltRewards.Score(true,6,44,6,6,2500,40000)==55000,"full belt adds 40000 once");
        Check(BeltRewards.Score(true,6,44,5,5,2500,40000)==12500,"partial row must not receive full bonus");
        Check(BeltRewards.Score(false,6,44,6,6,2500,40000)==0,"disabled belt scores nothing");
        Check(BeltRewards.Score(true,6,44,5,6,2500,40000)==52500,"burden fills a physical charm cell without changing old per-charm scoring");
        Check(BeltRewards.Score(true,6,5,5,5,2500,40000)==12500,"partial inventory row is not full width");
        Check(BeltRewards.Score(true,6,44,6,6,0,40000)==40000,"full bonus independent of per-charm setting");
        Check(BeltRewards.Score(true,6,44,6,6,2500,0)==15000,"zero full bonus preserves original scoring");
        var locks=new PaperLockRules {Width=3};
        locks.Items[1]=new PaperCategoryItem {Charm=true,Categories=new[]{"A"}};
        locks.Items[2]=new PaperCategoryItem {Charm=true,Paper=true};
        locks.Items[3]=new PaperCategoryItem {Charm=true,Categories=new[]{"A"}};
        locks.Items[4]=new PaperCategoryItem {Charm=true,Categories=new[]{"B"}};
        locks.Items[5]=new PaperCategoryItem {Charm=true,Categories=new[]{"A"}};
        locks.Targets.Add(new PaperComboLock {Instance=2,Categories=new[]{"A"}});
        Check(locks.Matches(new[]{1,2,3,4,5,0}),"original paper match rejected");
        Check(locks.Matches(new[]{1,2,5,4,3,0}),"same-category replacement neighbors blocked");
        Check(locks.Matches(new[]{4,0,3,1,2,5}),"whole valid paper triple cannot move");
        Check(!locks.Matches(new[]{1,2,4,3,5,0}),"different combo accepted");
        Check(!locks.Matches(new[]{1,2,0,3,5,4}),"missing neighbor accepted");
        Check(!locks.Matches(new[]{1,3,2,5,4,0}),"row boundary wraps paper neighbors");
        Check(!locks.Matches(new[]{1,3,4,5,2}),"partial last row paper accepted");
        locks.Items[1].Categories=new[]{"A","B"};locks.Items[3].Categories=new[]{"A","B"};
        Check(!locks.Matches(new[]{1,2,3}),"extra unrequested combo accepted");
        locks.Targets[0].Categories=new[]{"A","B"};
        Check(locks.Matches(new[]{1,2,3}),"multiple active combos not preserved");
        locks.Items[3].Categories=new[]{"A"};Check(!locks.Matches(new[]{1,2,3}),"one locked category lost");
        locks.Targets[0].Categories=new[]{"A"};locks.Items[1].Categories=new[]{"a"};
        Check(!locks.Matches(new[]{1,2,3}),"native category case changed");
        locks.Items[1].RowCategories=new[]{"A","B"};
        Check(locks.Matches(new[]{1,2,3}),"native row category ignored");
        Check(!locks.Matches(new[]{4,0,5,1,2,3}),"changed row category does not break paper");
        locks.Items[1].RowCategories=Array.Empty<string>();locks.Items[1].Categories=new[]{"A"};
        locks.Items[3].Paper=true;Check(!locks.Matches(new[]{1,2,3}),"new recursive paper dependency accepted");
        locks.Targets[0].Pinned=true;locks.PinnedCells[0]=1;locks.PinnedCells[1]=2;locks.PinnedCells[2]=3;
        Check(locks.Matches(new[]{1,2,3,4,5,0}),"preserved recursive component rejected");
        Check(!locks.Matches(new[]{4,2,3,1,5,0}),"recursive input neighbor moved");
        Check(locks.Matches(new[]{1,2,3,5,4,0}),"unrelated cells frozen with recursive paper");
        locks=new PaperLockRules {Width=3};
        Check(locks.Matches(new[]{2,1,3}),"unactivated paper was locked");
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
        if (args.Length > 0)
        {
            using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(args[0]));
            var root = document.RootElement;
            LayoutObjective Objective(string phase)
            {
                var q = root.GetProperty(phase).GetProperty("objective").EnumerateArray().Select(x => x.GetDouble()).ToArray();
                return new LayoutObjective { MaxActive = q[0], MaxLevels = q[1], MaxBalance = q[2], Enabled = q[3], Raised = q[4], Negative = q[5], Ordinary = q[6], Stable = q[7] };
            }
            FreeGainProof ReadProof(string phase) => System.Text.Json.JsonSerializer.Deserialize<FreeGainProof>(root.GetProperty(phase).GetProperty("freeGain").GetRawText(),
                new System.Text.Json.JsonSerializerOptions { IncludeFields = true, PropertyNameCaseInsensitive = true });
            Check(Objective("after").CompareTo(Objective("before")) < 0, "recorded worker payload demonstrates old rejection");
            Check(BuildLayoutAcceptance.Accepts(Objective("after"), Objective("before"), ReadProof("after"), ReadProof("before")), "C# accepts actual packaged worker free-gain payload");
            Check(ReadProof("after").Stats["MP_REGEN"] == 21 && ReadProof("before").Stats["MP_REGEN"] == 18, "actual packaged native stat values");
        }
        Console.WriteLine($"PASS {checks} assertions (mark rules and layered objectives)");
    }
}
