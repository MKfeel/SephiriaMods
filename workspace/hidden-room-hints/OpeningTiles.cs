using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SephiriaHiddenRoomHints
{
    internal static class OpeningTiles
    {
        static readonly System.Reflection.FieldInfo HiddenRooms = AccessTools.Field(typeof(EnhancedProceduralFloorGenerator), "hiddenRoomInstances");
        static readonly System.Reflection.FieldInfo HiddenPassages = AccessTools.Field(typeof(EnhancedProceduralFloorGenerator), "hiddenRoomPassageIndexs");
        static readonly System.Reflection.FieldInfo ConnectedPassages = AccessTools.Field(typeof(EnhancedProceduralFloorGenerator), "hiddenRoomConnectPassageIndexs");
        static readonly System.Reflection.FieldInfo Digging = AccessTools.Field(typeof(EnhancedProceduralFloorGenerator), "hiddenRoomDiggingIndexs");
        static readonly System.Reflection.FieldInfo LibraryPassages = AccessTools.Field(typeof(LibraryFloorGenerator), "hiddenRoomPassageDatas");
        static readonly System.Reflection.FieldInfo PassageSize = AccessTools.Field(typeof(LibraryFloorGenerator), "passageSize");

        internal static bool TryGet(Entrance entry, out Tilemap map, out HashSet<Cell> cells)
        {
            map = null; cells = new HashSet<Cell>();
            if (entry == null || !entry.Generator || !entry.Generator.GenerateSuccess || !(entry.Generator is TileFloorGenerator floor) || !floor.wall) return false;
            map = floor.wall;
            var tiles = map;
            System.Func<Cell, bool> hasWall = c => tiles.HasTile(new Vector3Int(c.X, c.Y, 0));
            int i = entry.Index;
            if (floor is LibraryFloorGenerator library)
            {
                var passages = LibraryPassages.GetValue(library) as IList<LibraryFloorGenerator.PassageData>;
                if (library.hiddenRoomPassageType != LibraryFloorGenerator.HiddenRoomPassageType.Digging
                    || passages == null || i < 0 || i >= passages.Count || passages[i] == null) return false;
                var p = passages[i];
                return OpeningGeometry.Library(new Cell(p.startPoint.x, p.startPoint.y), new Cell(p.endPoint.x, p.endPoint.y),
                    p.dir, (int)PassageSize.GetValue(library), hasWall, cells);
            }
            if (floor is EnhancedProceduralFloorGenerator enhanced)
            {
                var hidden = HiddenRooms.GetValue(enhanced) as IList<TileBasedRoomInstance>;
                var hiddenPassages = HiddenPassages.GetValue(enhanced) as IList<int>;
                var connected = ConnectedPassages.GetValue(enhanced) as IList<int>;
                var digging = Digging.GetValue(enhanced) as IList<int>;
                if (i < 0 || hidden == null || hiddenPassages == null || connected == null || digging == null
                    || i >= hidden.Count || i >= hiddenPassages.Count || i >= connected.Count || i >= digging.Count) return false;
                // Original RPC opens the hidden side first, then the connected room, using the same digging index.
                return Trace(hidden[i], hiddenPassages[i], digging[i], hasWall, cells)
                    && Trace(entry.Room as TileBasedRoomInstance, connected[i], digging[i], hasWall, cells);
            }
            return false;
        }

        static bool Trace(TileBasedRoomInstance room, int passage, int digging,
            System.Func<Cell,bool> wall, HashSet<Cell> cells)
        {
            if (room == null) return false;
            var tiles = GridDungeonGenerator.GetPassageTileIdx(room.Metadata.type, passage, out var direction);
            int dx = 0, dy = 0, width;
            switch (GridDungeonGenerator.GetReverse(direction))
            {
                case EGridRoomPassageDir.Up: dy = 1; width = 1; break;
                case EGridRoomPassageDir.Down: dy = -1; width = 1; break;
                case EGridRoomPassageDir.Right: dx = 1; width = 2; break;
                case EGridRoomPassageDir.Left: dx = -1; width = 2; break;
                default: return false;
            }
            if (tiles == null || digging < 0 || digging + width > tiles.Length) return false;
            var lanes = new Cell[width];
            for (int j = 0; j < width; j++) lanes[j] = new Cell(room.pos.x * 26 + tiles[digging+j].x, room.pos.y * 18 + tiles[digging+j].y);
            return OpeningGeometry.Enhanced(lanes, dx, dy, wall, cells);
        }
    }
}
