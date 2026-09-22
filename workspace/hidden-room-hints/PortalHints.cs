using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SephiriaHiddenRoomHints
{
    public sealed partial class HiddenRoomHintPlugin
    {
        readonly Dictionary<int, PortalHint> portals = new Dictionary<int, PortalHint>();
        RectTransform pointerCanvas;
        readonly HashSet<int> activePortals = new HashSet<int>();
        sealed class PortalHint
        {
            internal BreakableProp_HiddenPortal Stone;
            internal Entrance Entrance;
            internal RectTransform Pointer, MapMarker;
            internal UI_Map Map;
            internal void Destroy()
            {
                if (Pointer) Object.Destroy(Pointer.gameObject);
                if (MapMarker) Object.Destroy(MapMarker.gameObject);
            }
        }

        void RefreshPortals(FloorGenerator[] floors)
        {
            var stones = registeredPortals;
            var active = activePortals;
            active.Clear();
            foreach (var stone in stones)
            {
                if (!stone || !stone.gameObject.activeInHierarchy || !PortalResolver.IsCandidate(stone)) continue;
                int id = stone.GetInstanceID(); active.Add(id);
                if (!portals.TryGetValue(id, out var hint))
                { hint = new PortalHint { Stone = stone }; portals.Add(id, hint); }
                if (hint.Entrance == null || !hint.Entrance.Generator)
                    if (PortalResolver.TryResolve(stone, floors, out var entry))
                    {
                        hint.Entrance = entry;
                        Logger.LogInfo($"Hidden portal stone: floor={entry.Generator.name}, guid={entry.Generator.guid}, source={stone.GetExitPosition()}, target={stone.targetPosition}, connected={stone.isConnected}, broken={stone.IsBroken}");
                    }
                UpdatePortalMap(hint);
            }
            staleHints.Clear();
            foreach (int id in portals.Keys) if (!active.Contains(id)) staleHints.Add(id);
            foreach (int id in staleHints) { portals[id].Destroy(); portals.Remove(id); announced.Remove(id); }
        }

        void LateUpdate()
        {
            try
            {
                ProcessEvents();
                foreach (var hint in portals.Values)
                {
                    Vector3? position = null;
                    if (showWallMarker.Value && hint.Stone && hint.Stone.gameObject.activeInHierarchy && PortalResolver.IsCandidate(hint.Stone))
                    { var p = hint.Stone.GetExitPosition(); position = new Vector3(p.x,p.y,hint.Stone.transform.position.z); }
                    TrackMarker(ref hint.Pointer, hint.Entrance, position);
                }
                foreach (var hint in hints.Values)
                    TrackMarker(ref hint.Pointer, hint.Entrance, hint.Trigger && hint.Trigger.isActiveAndEnabled && hint.Trigger.hp > 0 ? hint.Position : null);
            }
            catch (System.Exception error) { Report(error); }
        }

        void TrackMarker(ref RectTransform pointer, Entrance entry, Vector3? position)
        {
            var gameCamera = GameCamera.Instance;
            var camera = gameCamera ? gameCamera.Camera : null;
            bool visible = showWallMarker.Value && camera && position.HasValue && entry != null && entry.Generator
                && gameCamera.CurrentSeeingFloor == entry.Generator;
            Vector3 screen = Vector3.zero;
            if (visible)
            {
                screen = camera.WorldToScreenPoint(position.Value);
                visible = screen.z > 0 && camera.pixelRect.Contains(new Vector2(screen.x, screen.y));
            }
            if (pointer && pointer.gameObject.activeSelf != visible) pointer.gameObject.SetActive(visible);
            if (!visible) return;
            // A marker denotes an exact visible entrance; no off-screen clamped marker or arrow.
            if (!pointerCanvas)
            {
                var go = new GameObject("Hidden Entrance Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                go.transform.SetParent(transform, false); pointerCanvas = go.GetComponent<RectTransform>();
                var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
                var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = 0.5f;
            }
            if (!pointer) { pointer = CreateEntranceMarker(pointerCanvas, new Vector2(20,38)); pointer.pivot = new Vector2(0.5f,0); }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(pointerCanvas, screen, null, out var point);
            pointer.anchoredPosition = point + new Vector2(0,8);
        }

        static RectTransform CreateEntranceMarker(Transform parent, Vector2 size)
        {
            var go = new GameObject("Hidden Entrance !",typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=Vector2.one*0.5f;rect.sizeDelta=size;
            var graphic=go.AddComponent<EntranceExclamation>();graphic.color=new Color(1,0.85f,0.05f,1);graphic.raycastTarget=false;
            return rect;
        }

        void UpdatePortalMap(PortalHint hint)
        {
            var entry=hint.Entrance;
            var map=entry!=null && entry.Generator ? entry.Generator.mapInstance : null;
            if(hint.MapMarker && (!showMapMarker.Value || hint.Map!=map)) {Object.Destroy(hint.MapMarker.gameObject);hint.MapMarker=null;}
            if(!showMapMarker.Value || !map || !map.contentsChild || map.rooms==null || !(entry.Room is LibraryFloorRoomInstance room))return;

            UI_Map_LibraryProceduralDungeonRoom icon=null;
            foreach(var candidate in map.rooms)
                if(candidate is UI_Map_LibraryProceduralDungeonRoom library && ReferenceEquals(library.Room,room)) {icon=library;break;}
            if(!icon)return;
            if(!hint.MapMarker) { hint.MapMarker=CreateEntranceMarker(map.contentsChild,new Vector2(12,22)); hint.Map=map; }
            Vector2 local=hint.Stone.GetExitPosition()-(Vector2)entry.Generator.transform.position-(Vector2)room.pos;
            hint.MapMarker.anchoredPosition=icon.GetIconCenterAnchoredPosition()-icon.GetRoomIconSize()*0.5f+local*2+Vector2.one*2;
        }
        void DestroyPortals()
        {
            foreach(var hint in portals.Values)hint.Destroy();portals.Clear();
            if(pointerCanvas)Object.Destroy(pointerCanvas.gameObject);
        }
    }

    // Same font-independent yellow exclamation for world-overlay and full-map entrances.
    internal sealed class EntranceExclamation : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect;
            float x=r.center.x, width=r.width*0.38f;
            AddBox(vh,new Rect(x-width/2-1.5f,r.yMin+1,width+3,r.height*0.19f+3),new Color(0.12f,0.08f,0,0.95f));
            AddBox(vh,new Rect(x-width/2-1.5f,r.yMin+r.height*0.34f-1.5f,width+3,r.height*0.6f+3),new Color(0.12f,0.08f,0,0.95f));
            AddBox(vh,new Rect(x-width/2,r.yMin+2.5f,width,r.height*0.19f),color);
            AddBox(vh,new Rect(x-width/2,r.yMin+r.height*0.34f,width,r.height*0.6f),color);
        }
        static void AddBox(VertexHelper vh,Rect r,Color c)
        {
            int n=vh.currentVertCount;
            vh.AddVert(new Vector3(r.xMin,r.yMin,0),c,Vector2.zero);vh.AddVert(new Vector3(r.xMin,r.yMax,0),c,Vector2.zero);
            vh.AddVert(new Vector3(r.xMax,r.yMax,0),c,Vector2.zero);vh.AddVert(new Vector3(r.xMax,r.yMin,0),c,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}
