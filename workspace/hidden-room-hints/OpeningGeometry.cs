using System;
using System.Collections.Generic;

namespace SephiriaHiddenRoomHints
{
    internal readonly struct Cell : IEquatable<Cell>
    {
        internal readonly int X, Y;
        internal Cell(int x, int y) { X = x; Y = y; }
        public bool Equals(Cell other) => X == other.X && Y == other.Y;
        public override bool Equals(object other) => other is Cell c && Equals(c);
        public override int GetHashCode() => unchecked(X * 397 ^ Y);
    }
    internal readonly struct Edge
    {
        internal readonly Cell A, B;
        internal Edge(Cell a, Cell b) { A = a; B = b; }
    }
    internal static class OpeningGeometry
    {
        // Matches DrawPassage's interior SetWallTile(cell, null) domain, not its side walls.
        internal static bool Library(Cell start, Cell end, int direction, int size,
            Func<Cell, bool> wall, HashSet<Cell> removed)
        {
            if (size < 1 || size > 128 || direction < 0 || direction > 3) return false;
            bool vertical = direction == 0 || direction == 2;
            if (vertical ? start.X != end.X : start.Y != end.Y) return false;
            long length = vertical ? Math.Abs((long)end.Y - start.Y) + 1 : Math.Abs((long)end.X - start.X) + 1;
            if (length > 4096) return false; // Corrupt/mismatched data: no partial outline.
            int x0 = vertical ? start.X : Math.Min(start.X, end.X);
            int x1 = vertical ? start.X + size - 1 : Math.Max(start.X, end.X);
            int y0 = vertical ? Math.Min(start.Y, end.Y) : start.Y - size;
            int y1 = vertical ? Math.Max(start.Y, end.Y) : start.Y;
            for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++)
            { var c = new Cell(x, y); if (wall(c)) removed.Add(c); }
            return true;
        }

        // Matches OpenHiddenPassage's do/while: stop at the first completely empty row.
        // removed also simulates the first call's mutations before the second side is processed.
        internal static bool Enhanced(Cell[] lanes, int inwardX, int inwardY,
            Func<Cell, bool> wall, HashSet<Cell> removed)
        {
            if (lanes == null || lanes.Length < 1 || lanes.Length > 2 || Math.Abs(inwardX) + Math.Abs(inwardY) != 1) return false;
            for (int depth = 0; depth < 4096; depth++)
            {
                bool any = false;
                foreach (var lane in lanes)
                {
                    var cell = new Cell(lane.X + inwardX * depth, lane.Y + inwardY * depth);
                    if (!removed.Contains(cell) && wall(cell)) { removed.Add(cell); any = true; }
                }
                if (!any) return true;
            }
            return false;
        }

        internal static List<Edge> Boundary(HashSet<Cell> cells)
        {
            var result = new List<Edge>();
            foreach (var c in cells)
            {
                int x = c.X, y = c.Y;
                if (!cells.Contains(new Cell(x, y - 1))) result.Add(new Edge(new Cell(x,y), new Cell(x+1,y)));
                if (!cells.Contains(new Cell(x + 1, y))) result.Add(new Edge(new Cell(x+1,y), new Cell(x+1,y+1)));
                if (!cells.Contains(new Cell(x, y + 1))) result.Add(new Edge(new Cell(x+1,y+1), new Cell(x,y+1)));
                if (!cells.Contains(new Cell(x - 1, y))) result.Add(new Edge(new Cell(x,y+1), new Cell(x,y)));
            }
            return result;
        }
    }
}
