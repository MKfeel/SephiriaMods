using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SephiriaHiddenRoomHints
{
    internal sealed class Entrance
    {
        internal FloorGenerator Generator;
        internal object Room;
        internal Vector2 Direction;
        internal int Index;
    }

    internal static class EntranceResolver
    {
        static readonly FieldInfo BreakHandlers = AccessTools.Field(typeof(HiddenRoomTriggerCollider), "OnBreakServerside");
        static readonly FieldInfo EnhancedRooms = AccessTools.Field(typeof(EnhancedProceduralFloorGenerator), "hiddenRoomConnectRoomInstances");
        static readonly FieldInfo EnhancedPassages = AccessTools.Field(typeof(EnhancedProceduralFloorGenerator), "hiddenRoomConnectPassageIndexs");
        static readonly FieldInfo EnhancedDigging = AccessTools.Field(typeof(EnhancedProceduralFloorGenerator), "hiddenRoomDiggingIndexs");
        static readonly FieldInfo LibraryRooms = AccessTools.Field(typeof(LibraryFloorGenerator), "hiddenRoomConnectRoomInstances");
        static readonly FieldInfo LibraryPassages = AccessTools.Field(typeof(LibraryFloorGenerator), "hiddenRoomPassageDatas");

        internal static bool TryResolve(HiddenRoomTriggerCollider trigger, FloorGenerator[] floors, out Entrance result)
        {
            result = null;
            // The server's break callback directly records the owning generator (both implementations).
            var handlers = BreakHandlers?.GetValue(trigger) as Delegate;
            if (handlers != null)
                foreach (var handler in handlers.GetInvocationList())
                    if (handler.Target is FloorGenerator owner)
                        return owner && TryCandidate(trigger, owner, trigger.hiddenRoomIndex, out result);

            // Clients have no server delegate. Require a unique exact spatial match; retry while generating.
            foreach (var floor in floors)
            {
                if (!floor) continue;
                // hiddenRoomIndex is not a SyncVar. On clients it can remain -1.
                var rooms = floor is LibraryFloorGenerator
                    ? LibraryRooms?.GetValue(floor) as System.Collections.IList
                    : floor is EnhancedProceduralFloorGenerator
                        ? EnhancedRooms?.GetValue(floor) as System.Collections.IList : null;
                if (rooms == null) continue;
                for (int index = 0; index < rooms.Count; index++)
                {
                    if (!TryCandidate(trigger, floor, index, out var candidate)) continue;
                    if (result != null) { result = null; return false; }
                    result = candidate;
                }
            }
            return result != null;
        }

        static bool TryCandidate(HiddenRoomTriggerCollider trigger, FloorGenerator floor, int index, out Entrance result)
        {
            result = null;
            if (index < 0) return false;
            Vector2 position = trigger.transform.position - floor.transform.position;
            if (floor is EnhancedProceduralFloorGenerator enhanced)
            {
                var rooms = EnhancedRooms?.GetValue(enhanced) as IList<TileBasedRoomInstance>;
                var passages = EnhancedPassages?.GetValue(enhanced) as IList<int>;
                var digging = EnhancedDigging?.GetValue(enhanced) as IList<int>;
                if (rooms == null || passages == null || digging == null || index >= rooms.Count
                    || index >= passages.Count || index >= digging.Count || rooms[index] == null) return false;
                var room = rooms[index];
                var tiles = GridDungeonGenerator.GetPassageTileIdx(room.Metadata.type, passages[index], out var dir);
                int dig = digging[index];
                if (tiles == null || dig < 0 || dig >= tiles.Length) return false;
                Vector2 normal = Direction((int)dir);
                Vector2 start = new Vector2(room.pos.x * 26, room.pos.y * 18) + (Vector2)tiles[dig] + Vector2.one * 0.5f;
                float depth = normal.x == 0 ? room.size.y * 18 : room.size.x * 26;
                var delta = position - start;
                if (!HintRules.OnDiggingLane(delta.x, delta.y, -(int)normal.x, -(int)normal.y, depth)) return false;
                result = new Entrance { Generator = floor, Room = room, Direction = normal, Index = index };
                return true;
            }
            if (floor is LibraryFloorGenerator library)
            {
                var rooms = LibraryRooms?.GetValue(library) as IList<LibraryFloorRoomInstance>;
                var passages = LibraryPassages?.GetValue(library) as IList<LibraryFloorGenerator.PassageData>;
                if (rooms == null || passages == null || index >= rooms.Count || index >= passages.Count
                    || rooms[index] == null || passages[index] == null) return false;
                var passage = passages[index];
                Vector2 expected = (Vector2)passage.startPoint + Vector2.one * 0.5f;
                if ((position - expected).sqrMagnitude > 0.15f * 0.15f) return false;
                result = new Entrance { Generator = floor, Room = rooms[index], Direction = Direction(passage.dir), Index = index };
                return result.Direction != Vector2.zero;
            }
            return false;
        }

        static Vector2 Direction(int direction)
        {
            switch (direction) { case 0: return Vector2.up; case 1: return Vector2.right;
                case 2: return Vector2.down; case 3: return Vector2.left; default: return Vector2.zero; }
        }
    }
}
