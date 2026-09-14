using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SephiriaHiddenRoomHints
{
    internal static class PortalResolver
    {
        static readonly FieldInfo Entrances = AccessTools.Field(typeof(LibraryFloorGenerator), "allHiddenPortal_Entrance");
        static readonly FieldInfo Rooms = AccessTools.Field(typeof(LibraryFloorGenerator), "roomList");
        static readonly FieldInfo HiddenRooms = AccessTools.Field(typeof(LibraryFloorGenerator), "hiddenRoomInstances");

        internal static bool IsCandidate(BreakableProp_HiddenPortal portal) => portal
            && portal.isConnected && portal.passageDir == HiddenPortalSide.Entrance && !portal.IsBroken;

        internal static bool TryResolve(BreakableProp_HiddenPortal portal, FloorGenerator[] floors, out Entrance result)
        {
            result = null;
            if (!IsCandidate(portal)) return false;
            foreach (var floor in floors)
            {
                if (!(floor is LibraryFloorGenerator library) || !library || !library.GenerateSuccess
                    || library.hiddenRoomPassageType != LibraryFloorGenerator.HiddenRoomPassageType.Portal) continue;
                var registered = Entrances.GetValue(library) as IList<IHiddenPortal>;
                var rooms = Rooms.GetValue(library) as IList<LibraryFloorRoomInstance>;
                var hidden = HiddenRooms.GetValue(library) as IList<LibraryFloorRoomInstance>;
                Vector2 source = (Vector2)portal.GetExitPosition() - (Vector2)library.transform.position;
                Vector2 destination = portal.targetPosition - (Vector2)library.transform.position;
                var sourceRoom = FindUniqueRoom(rooms, source);
                // Server registration is authoritative. Network clients can use the synced target,
                // but require both a source room and a destination inside a generated hidden room.
                bool owned = registered != null && registered.Contains(portal);
                if (!owned && (sourceRoom == null || FindUniqueRoom(hidden, destination) == null)) continue;
                if (result != null) { result = null; return false; }
                result = new Entrance { Generator = library, Room = sourceRoom, Index = -1, Direction = Vector2.zero };
            }
            return result != null;
        }

        static LibraryFloorRoomInstance FindUniqueRoom(IList<LibraryFloorRoomInstance> rooms, Vector2 point)
        {
            LibraryFloorRoomInstance match = null;
            if (rooms == null) return null;
            foreach (var room in rooms)
            {
                if (room == null || point.x < room.pos.x || point.y < room.pos.y
                    || point.x >= room.pos.x + room.Size.x || point.y >= room.pos.y + room.Size.y) continue;
                if (match != null) return null;
                match = room;
            }
            return match;
        }
    }
}
