// Data-only game API doubles. These tests do not initialize Unity or claim in-game validation.
using System;
using System.Collections.Generic;
using System.Reflection;
namespace HarmonyLib { static class AccessTools { public static FieldInfo Field(Type t, string n) => t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); } }
namespace UnityEngine
{
    public class Object { public static implicit operator bool(Object o) => o != null; }
    public class Transform { public Vector3 position; }
    public struct Vector2
    {
        public float x,y;
        public Vector2(float x,float y) { this.x=x; this.y=y; }
        public float sqrMagnitude => x*x+y*y;
        public static Vector2 zero => new Vector2(0,0);
        public static Vector2 one => new Vector2(1,1);
        public static Vector2 up => new Vector2(0,1);
        public static Vector2 down => new Vector2(0,-1);
        public static Vector2 right => new Vector2(1,0);
        public static Vector2 left => new Vector2(-1,0);
        public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
        public static Vector2 operator -(Vector2 a,Vector2 b)=>new Vector2(a.x-b.x,a.y-b.y);
        public static Vector2 operator *(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
        public static bool operator ==(Vector2 a,Vector2 b)=>a.x==b.x && a.y==b.y;
        public static bool operator !=(Vector2 a,Vector2 b)=>!(a==b);
        public override bool Equals(object obj)=>obj is Vector2 b && this==b;
        public override int GetHashCode()=>HashCode.Combine(x,y);
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z=0) {this.x=x;this.y=y;this.z=z;}
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static implicit operator Vector2(Vector3 a)=>new Vector2(a.x,a.y);
    }
    public struct Vector2Int
    {
        public int x,y;
        public Vector2Int(int x,int y) {this.x=x;this.y=y;}
        public static explicit operator Vector2(Vector2Int a)=>new Vector2(a.x,a.y);
    }
}
public class FloorGenerator : UnityEngine.Object { public UnityEngine.Transform transform=new UnityEngine.Transform(); public bool GenerateSuccess=true; }
public class HiddenRoomTriggerCollider
{
    public UnityEngine.Transform transform=new UnityEngine.Transform();
    public int hiddenRoomIndex=-1;
    public Action<UnityEngine.Vector3,int> OnBreakServerside;
}
public class TileBasedRoomInstance
{
    public UnityEngine.Vector2Int pos,size;
    public MetadataRecord Metadata=new MetadataRecord();
    public class MetadataRecord {public int type;}
}
public class LibraryFloorRoomInstance { public UnityEngine.Vector2Int pos,Size; }
public class EnhancedProceduralFloorGenerator : FloorGenerator
{
    public List<TileBasedRoomInstance> hiddenRoomConnectRoomInstances=new List<TileBasedRoomInstance>();
    public List<int> hiddenRoomConnectPassageIndexs=new List<int>();
    public List<int> hiddenRoomDiggingIndexs=new List<int>();
    public void Break(UnityEngine.Vector3 p,int i) {}
}
public class LibraryFloorGenerator : FloorGenerator
{
    public enum HiddenRoomPassageType { Digging, Portal }
    public HiddenRoomPassageType hiddenRoomPassageType;
    public List<IHiddenPortal> allHiddenPortal_Entrance=new List<IHiddenPortal>();
    public List<LibraryFloorRoomInstance> roomList=new List<LibraryFloorRoomInstance>();
    public List<LibraryFloorRoomInstance> hiddenRoomInstances=new List<LibraryFloorRoomInstance>();
    public class PassageData {public UnityEngine.Vector2Int startPoint; public int dir;}
    public List<LibraryFloorRoomInstance> hiddenRoomConnectRoomInstances=new List<LibraryFloorRoomInstance>();
    public List<PassageData> hiddenRoomPassageDatas=new List<PassageData>();
    public void Break(UnityEngine.Vector3 p,int i) {}
}
public enum HiddenPortalSide { Entrance, Exit }
public interface IHiddenPortal {}
public class BreakableProp_HiddenPortal : UnityEngine.Object, IHiddenPortal
{
    public bool isConnected,IsBroken;
    public HiddenPortalSide passageDir;
    public UnityEngine.Vector2 source,targetPosition;
    public UnityEngine.Vector2 GetExitPosition()=>source;
}
public static class GridDungeonGenerator
{
    public static UnityEngine.Vector2Int[] GetPassageTileIdx(int type,int passage,out int dir)
    {dir=passage;return new[]{new UnityEngine.Vector2Int(5,0)};}
}
