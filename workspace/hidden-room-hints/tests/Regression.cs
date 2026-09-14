using System;
using SephiriaHiddenRoomHints;
using UnityEngine;

static class Regression
{
    static int passed;
    static void Check(bool ok,string name) {if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
    static LibraryFloorGenerator Desert(float origin=0,int dir=0)
    {
        var floor=new LibraryFloorGenerator();floor.transform.position=new Vector3(origin,0);
        floor.hiddenRoomConnectRoomInstances.Add(new LibraryFloorRoomInstance());
        floor.hiddenRoomPassageDatas.Add(new LibraryFloorGenerator.PassageData{startPoint=new Vector2Int(10,20),dir=dir});
        return floor;
    }
    static void Main()
    {
        OpeningRegression.Run(Check);
        PortalRegression.Run(Check);
        Check(!HintRules.HasPlannedRoom(0) && !HintRules.HasPlannedRoom(-1) && HintRules.HasPlannedRoom(1),"route preview reflects positive planned count only");
        for(int dir=0;dir<4;dir++)
        {
            var desert=Desert(1000,dir);
            var trigger=new HiddenRoomTriggerCollider{hiddenRoomIndex=0,OnBreakServerside=desert.Break};
            trigger.transform.position=new Vector3(1010.5f,20.5f);
            Check(EntranceResolver.TryResolve(trigger,new FloorGenerator[]{Desert(),desert},out var hit)
                && ReferenceEquals(hit.Generator,desert) && ReferenceEquals(hit.Room,desert.hiddenRoomConnectRoomInstances[0]),"desert exact ownership direction "+dir);
        }
        var owner=Desert(1000);
        var t=new HiddenRoomTriggerCollider{hiddenRoomIndex=0,OnBreakServerside=owner.Break};t.transform.position=new Vector3(1010.5f,20.5f);
        owner.hiddenRoomPassageDatas.Clear();
        Check(!EntranceResolver.TryResolve(t,new FloorGenerator[]{Desert(1000)},out _),"unready owning generator does not borrow another floor's index");
        var client= new HiddenRoomTriggerCollider();client.transform.position=new Vector3(1010.5f,20.5f);
        var unique=Desert(1000);
        Check(EntranceResolver.TryResolve(client,new FloorGenerator[]{Desert(),unique},out var match)&&ReferenceEquals(match.Generator,unique),"client unsynced -1 index resolves unique exact coordinates");
        Check(!EntranceResolver.TryResolve(client,new FloorGenerator[]{unique,Desert(1000)},out _),"ambiguous overlapping generators produce no guessed map marker");
        client.transform.position=new Vector3(1011.5f,20.5f);
        Check(!EntranceResolver.TryResolve(client,new FloorGenerator[]{unique},out _),"adjacent wall is never substituted for entrance");
        var enhanced=new EnhancedProceduralFloorGenerator();
        enhanced.hiddenRoomConnectRoomInstances.Add(new TileBasedRoomInstance{size=new Vector2Int(1,1)});
        enhanced.hiddenRoomConnectPassageIndexs.Add(2);enhanced.hiddenRoomDiggingIndexs.Add(0);
        var et=new HiddenRoomTriggerCollider{hiddenRoomIndex=0,OnBreakServerside=enhanced.Break};et.transform.position=new Vector3(5.5f,3.5f);
        Check(EntranceResolver.TryResolve(et,new FloorGenerator[]{enhanced},out _),"enhanced generator retains valid selected digging lane");
        et.transform.position=new Vector3(6.5f,3.5f);
        Check(!EntranceResolver.TryResolve(et,new FloorGenerator[]{enhanced},out _),"enhanced neighboring lane rejected");
        et.transform.position=new Vector3(5.5f,40.5f);
        Check(!EntranceResolver.TryResolve(et,new FloorGenerator[]{enhanced},out _),"enhanced position outside owning room rejected");
        Console.WriteLine($"{passed} regression checks passed (data simulation, not Unity runtime).");
    }
}
