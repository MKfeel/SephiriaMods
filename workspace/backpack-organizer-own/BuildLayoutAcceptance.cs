using System;
using System.Collections.Generic;

namespace SephiriaBackpackOrganizer
{
    // Worker evidence is deliberately separate from the legacy 2.5.5 objective.
    // It can only override movement count when every primary component ties.
    internal sealed class FreeGainProof
    {
        public int Schema { get; set; }
        public string Invariant { get; set; }
        public Dictionary<string, double> Stats { get; set; }
        public Dictionary<string, double> Guards { get; set; }
    }

    internal static class BuildLayoutAcceptance
    {
        private const double Tolerance = 1e-8;
        internal static bool Accepts(LayoutObjective after, LayoutObjective before, FreeGainProof afterProof, FreeGainProof beforeProof)
        {
            if (!Finite(after.Ordinary) || !Finite(before.Ordinary)) return false;
            var a = after; var b = before;
            a.Stable = b.Stable = 0;
            if (Math.Abs(a.Ordinary - b.Ordinary) <= Tolerance) a.Ordinary = b.Ordinary;
            int primary = a.CompareTo(b);
            return primary > 0 || primary == 0 && (after.Stable >= before.Stable || Dominates(afterProof, beforeProof));
        }

        internal static bool Dominates(FreeGainProof a, FreeGainProof b)
        {
            if (a == null || b == null || a.Schema != 1 || b.Schema != 1 || string.IsNullOrEmpty(a.Invariant) || a.Invariant != b.Invariant) return false;
            string[] keys = { "DEFENSE", "EVASION", "MP_REGEN", "MP_STEAL" };
            if (a.Stats == null || b.Stats == null || a.Stats.Count != keys.Length || b.Stats.Count != keys.Length) return false;
            bool improved = false;
            foreach (string key in keys)
            {
                if (!a.Stats.TryGetValue(key, out var x) || !b.Stats.TryGetValue(key, out var y) || !Finite(x) || !Finite(y) || x < y - Tolerance) return false;
                improved |= x > y + Tolerance;
            }
            if (a.Guards == null || b.Guards == null || a.Guards.Count == 0 || a.Guards.Count != b.Guards.Count) return false;
            foreach (var entry in b.Guards)
                if (!a.Guards.TryGetValue(entry.Key, out var x) || !Finite(x) || !Finite(entry.Value) || x < entry.Value - Tolerance) return false;
            return improved;
        }
        private static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
    }
}
