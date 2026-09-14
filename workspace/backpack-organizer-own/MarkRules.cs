using System;
using System.Collections.Generic;

namespace SephiriaBackpackOrganizer
{
    // Pure rules, shared with the executable tests. None is also a manual override.
    internal enum ItemMark { None, Max, Raise, Enable, Negative }

    internal sealed class MarkStore
    {
        private readonly Dictionary<int, ItemMark> overrides = new Dictionary<int, ItemMark>();
        internal ItemMark Resolve(int id, bool favorite) => overrides.TryGetValue(id, out var mark) ? mark : favorite ? ItemMark.Max : ItemMark.None;
        internal ItemMark Toggle(int id, bool favorite, bool control)
        {
            ItemMark next = Next(Resolve(id, favorite), control);
            overrides[id] = next;
            return next;
        }
        internal static ItemMark Next(ItemMark current, bool control) => control
            ? current == ItemMark.Enable ? ItemMark.Negative : current == ItemMark.Negative ? ItemMark.None : ItemMark.Enable
            : current == ItemMark.Max ? ItemMark.Raise : current == ItemMark.Raise ? ItemMark.None : ItemMark.Max;
        internal void Clear() => overrides.Clear();
    }

    // Lexicographic comparison: no amount of a later reward buys an earlier loss.
    internal struct LayoutObjective : IComparable<LayoutObjective>
    {
        internal double MaxActive, MaxLevels, MaxBalance, Enabled, Raised, Negative, Ordinary, Stable;
        public int CompareTo(LayoutObjective other)
        {
            double delta = FirstDifference(other);
            return delta > 0 ? 1 : delta < 0 ? -1 : 0;
        }
        internal double FirstDifference(LayoutObjective b)
        {
            if (MaxActive != b.MaxActive) return MaxActive - b.MaxActive;
            if (MaxLevels != b.MaxLevels) return MaxLevels - b.MaxLevels;
            if (MaxBalance != b.MaxBalance) return (MaxBalance - b.MaxBalance) / 10;
            if (Enabled != b.Enabled) return Enabled - b.Enabled;
            if (Raised != b.Raised) return Raised - b.Raised;
            if (Negative != b.Negative) return Negative - b.Negative;
            if (Ordinary != b.Ordinary) return (Ordinary - b.Ordinary) / 10000;
            return (Stable - b.Stable) / 100;
        }
        internal void Add(ItemMark mark, bool active, int level, int maximum)
        {
            int effective = active ? Math.Min(Math.Max(level, 0), Math.Max(maximum, 0)) : 0;
            switch (mark)
            {
                case ItemMark.Max:
                    MaxActive += active ? 1 : 0;
                    int deficit = Math.Max(0, maximum - effective);
                    MaxLevels -= deficit;
                    MaxBalance -= (double)deficit * deficit;
                    break;
                case ItemMark.Enable: Enabled += active ? 1 : 0; break;
                case ItemMark.Raise: Raised += effective; break;
                case ItemMark.Negative: Negative += level < 0 ? 1 : 0; break;
            }
        }
    }

    internal static class GroupMoves
    {
        internal static bool Translate<T>(List<T> slots, List<int> cells, int target, int width)
        {
            if (cells.Count < 2 || width <= 0 || target < 0 || target >= slots.Count) return false;
            var distinct = new HashSet<int>(cells);
            if (distinct.Count != cells.Count) return false;
            foreach (int cell in cells) if (cell < 0 || cell >= slots.Count) return false;
            int root = cells[0];
            int dx = target % width - root % width, dy = target / width - root / width;
            var destinations = new List<int>();
            foreach (int cell in cells)
            {
                int x = cell % width + dx, y = cell / width + dy, pos = y * width + x;
                if (x < 0 || x >= width || y < 0 || pos >= slots.Count) return false;
                destinations.Add(pos);
            }
            var group = new List<T>(); var displaced = new List<T>(); var holes = new List<int>();
            foreach (int cell in cells) { group.Add(slots[cell]); if (!destinations.Contains(cell)) holes.Add(cell); }
            foreach (int cell in destinations) if (!cells.Contains(cell)) displaced.Add(slots[cell]);
            for (int i = 0; i < holes.Count; i++) slots[holes[i]] = displaced[i];
            for (int i = 0; i < destinations.Count; i++) slots[destinations[i]] = group[i];
            return true;
        }
    }
}
