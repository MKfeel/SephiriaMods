using System;
using SephiriaHiddenRoomHints;
using UnityEngine;

static class PortalRegression
{
    static LibraryFloorGenerator Floor(float origin=1000)
    {
        var floor=new LibraryFloorGenerator{hiddenRoomPassageType=LibraryFloorGenerator.HiddenRoomPassageType.Portal};
        floor.transform.position=new Vector3(origin,0);
        floor.roomList.Add(new LibraryFloorRoomInstance{pos=new Vector2Int(0,0),Size=new Vector2Int(20,20)});
        floor.hiddenRoomInstances.Add(new LibraryFloorRoomInstance{pos=new Vector2Int(30,0),Size=new Vector2Int(10,10)});
        return floor;
    }
    internal static void Run(Action<bool,string> check)
    {
        var stone=new BreakableProp_HiddenPortal{isConnected=true,passageDir=HiddenPortalSide.Entrance,source=new Vector2(1005,5),targetPosition=new Vector2(1035,5)};
        var floor=Floor();floor.allHiddenPortal_Entrance.Add(stone);
        check(PortalResolver.TryResolve(stone,new FloorGenerator[]{floor},out var entry)&&ReferenceEquals(entry.Generator,floor),"connected portal stone resolves without wall trigger or passage");
        check(ReferenceEquals(entry.Room,floor.roomList[0]),"map associates stone with its real source room");
        stone.IsBroken=true;check(!PortalResolver.IsCandidate(stone),"broken stone stops being a hint");stone.IsBroken=false;
        stone.isConnected=false;check(!PortalResolver.IsCandidate(stone),"unpaired decorative portal stone not marked");stone.isConnected=true;
        stone.passageDir=HiddenPortalSide.Exit;check(!PortalResolver.IsCandidate(stone),"portal exit not mistaken for entrance");stone.passageDir=HiddenPortalSide.Entrance;
        floor.GenerateSuccess=false;check(!PortalResolver.TryResolve(stone,new FloorGenerator[]{floor},out _),"wait until portal generation completes");floor.GenerateSuccess=true;
        floor.allHiddenPortal_Entrance.Clear();
        check(PortalResolver.TryResolve(stone,new FloorGenerator[]{Floor(0),floor},out entry)&&ReferenceEquals(entry.Generator,floor),"client uses synced target in correct hidden room");
        stone.targetPosition=new Vector2(1035,40);
        check(!PortalResolver.TryResolve(stone,new FloorGenerator[]{floor},out _),"client unrelated target rejected");stone.targetPosition=new Vector2(1035,5);
        stone.source=new Vector2(1080,5);check(!PortalResolver.TryResolve(stone,new FloorGenerator[]{floor},out _),"client source outside floor rejected");stone.source=new Vector2(1005,5);
        check(!PortalResolver.TryResolve(stone,new FloorGenerator[]{floor,Floor()},out _),"ambiguous overlapping floors rejected");
        floor.hiddenRoomPassageType=LibraryFloorGenerator.HiddenRoomPassageType.Digging;
        check(!PortalResolver.TryResolve(stone,new FloorGenerator[]{floor},out _),"wall passage floor not used as portal owner");
        check(!PortalResolver.IsCandidate(null),"destroyed portal reference ignored");
    }
}
