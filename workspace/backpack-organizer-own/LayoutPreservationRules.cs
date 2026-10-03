using System;
using System.Collections.Generic;

namespace SephiriaBackpackOrganizer
{
    // Pure production rules shared with the offline tests.
    internal static class BeltRewards
    {
        internal static double Score(bool enabled, int width, int storage, int ordinaryCharms, int allCharms, double perCharm, double fullRow)
        {
            if (!enabled) return 0;
            return ordinaryCharms * perCharm + (width > 0 && storage >= width && allCharms == width ? fullRow : 0);
        }
    }

    internal sealed class PaperCategoryItem
    {
        internal bool Charm, Paper;
        internal string[] Categories = new string[0], RowCategories = new string[0];
        private int Count => RowCategories.Length == 0 ? Categories.Length : 1;
        private string At(int index, int cell, int width) => RowCategories.Length == 0 ? Categories[index] : RowCategories[cell / width % RowCategories.Length];
        private bool Has(string category, int cell, int width)
        {
            for (int i = 0; i < Count; i++) if (string.Equals(At(i, cell, width), category, StringComparison.Ordinal)) return true;
            return false;
        }
        internal static bool MatchesIntersection(string[] target, PaperCategoryItem left, int leftCell, PaperCategoryItem right, int rightCell, int width)
        {
            if (left == null || right == null || !left.Charm || !right.Charm || left.Paper || right.Paper) return false;
            foreach (string category in target)
                if (!left.Has(category, leftCell, width) || !right.Has(category, rightCell, width)) return false;
            for (int i = 0; i < left.Count; i++)
            {
                string category = left.At(i, leftCell, width);
                if (right.Has(category, rightCell, width) && Array.IndexOf(target, category) < 0) return false;
            }
            return true;
        }
    }
    internal sealed class PaperComboLock
    {
        internal int Instance;
        internal string[] Categories;
        internal bool Pinned;
    }
    internal sealed class PaperLockRules
    {
        internal int Width;
        internal readonly Dictionary<int, PaperCategoryItem> Items = new Dictionary<int, PaperCategoryItem>();
        internal readonly List<PaperComboLock> Targets = new List<PaperComboLock>();
        internal readonly Dictionary<int, int> PinnedCells = new Dictionary<int, int>();
        internal bool IsLocked(int instance)
        {
            foreach (var target in Targets) if (target.Instance == instance) return true;
            return false;
        }
        internal bool Matches(IList<int> cells)
        {
            foreach (var pin in PinnedCells)
                if (pin.Key < 0 || pin.Key >= cells.Count || cells[pin.Key] != pin.Value) return false;
            foreach (var target in Targets)
            {
                int cell = -1;
                for (int i = 0; i < cells.Count; i++) if (cells[i] == target.Instance) { cell = i; break; }
                if (cell < 0) return false;
                // Recursive paper components keep their original cells and all input neighbors.
                if (target.Pinned) continue;
                if (Width <= 0 || cell % Width == 0 || cell % Width == Width - 1 || cell + 1 >= cells.Count) return false;
                Items.TryGetValue(cells[cell - 1], out var left); Items.TryGetValue(cells[cell + 1], out var right);
                if (!PaperCategoryItem.MatchesIntersection(target.Categories, left, cell - 1, right, cell + 1, Width)) return false;
            }
            return true;
        }
    }
}
