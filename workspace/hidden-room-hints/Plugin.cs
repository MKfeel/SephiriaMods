using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SephiriaHiddenRoomHints
{
    [BepInPlugin(Guid, "Sephiria Hidden Room Hints", "0.7.3")]
    public sealed partial class HiddenRoomHintPlugin : BaseUnityPlugin
    {
        internal const string Guid = "codex.sephiria.hidden-room-hints";
        internal static HiddenRoomHintPlugin Instance;
        ConfigEntry<bool> enableTestMode, forceHiddenRoomOnFirstFloor;
        ConfigEntry<KeyCode> instantKillKey;
        ConfigEntry<bool> showSystemMessage, showMapMarker, showWallMarker, showScreenNotice, showRouteMarker;
        readonly Dictionary<int, Hint> hints = new Dictionary<int, Hint>();
        readonly HashSet<int> announced = new HashSet<int>();
        Harmony harmony;
        float nextErrorTime;
        string currentFloor = "", screenNotice = "";
        float screenNoticeUntil;
        bool hintsDirty = true;
        readonly HashSet<HiddenRoomTriggerCollider> registeredWalls = new HashSet<HiddenRoomTriggerCollider>();
        readonly HashSet<BreakableProp_HiddenPortal> registeredPortals = new HashSet<BreakableProp_HiddenPortal>();
        readonly HashSet<int> activeWalls = new HashSet<int>();
        readonly List<int> staleHints = new List<int>();
        readonly HashSet<UI_WorldMapStageElement> routeElements = new HashSet<UI_WorldMapStageElement>();

        sealed class Hint
        {
            internal HiddenRoomTriggerCollider Trigger;
            internal Entrance Entrance;
            internal RectTransform Pointer;
            internal Vector3? Position;
            internal RectTransform MapMarker;
            internal UI_Map Map;
            internal UnityEngine.Tilemaps.Tilemap PositionMap;
            internal Vector3 LocalPosition;
            internal float PositionHp;
            internal void Destroy()
            {
                if (Pointer) Object.Destroy(Pointer.gameObject);
                if (MapMarker) Object.Destroy(MapMarker.gameObject);
            }
        }

        void Awake()
        {
            Instance = this;
            enableTestMode = Config.Bind("Testing", "EnableTestMode", false, "启用旧版测试功能，默认关闭。");
            forceHiddenRoomOnFirstFloor = Config.Bind("Testing", "ForceHiddenRoomOnFirstFloor", true, "测试模式下第一层生成隐藏房。");
            instantKillKey = Config.Bind("Testing", "InstantKillKey", KeyCode.F8, "测试模式秒杀键。");
            showSystemMessage = Config.Bind("Display", "ShowSystemMessage", true, "显示本层隐藏房系统消息。");
            showMapMarker = Config.Bind("Display", "ShowMapMarker", true, "在完整地图关联房间的入口侧显示标记。");
            showWallMarker = Config.Bind("Display", "ShowWallMarker", true, "隐藏墙和传送石入口统一显示黄色感叹号。");
            showScreenNotice = Config.Bind("Display", "ShowScreenNotice", true, "屏幕左上角显示短暂提示。");
            showRouteMarker = Config.Bind("Display", "ShowRouteMarker", true, "在路线选择节点标记已安排隐藏房的区域。");
            Config.SettingChanged += HandleSettingChanged;
            harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(HiddenRoomHintPlugin).Assembly);
            Logger.LogInfo("Sephiria Hidden Room Hints 0.7.3 loaded: event-driven entrance registration / no scene discovery scans.");
        }

        void Update()
        {
            try
            {
                if (enableTestMode.Value)
                {
                    ForceFirstFloorHiddenRoom();
                    if (WasInstantKillPressed()) InstantKillAllEnemies();
                }
            }
            catch (Exception error) { Report(error); }
        }

        internal void Report(Exception error)
        {
            if (Time.unscaledTime < nextErrorTime) return;
            nextErrorTime = Time.unscaledTime + 10f;
            Logger.LogError(error);
        }

        void RefreshHints()
        {
            var local = NetworkClient.localPlayer ? NetworkClient.localPlayer.GetComponent<PlayerAvatar>() : null;
            string floorGuid = local ? local.currentFloorGuid : "";
            if (currentFloor != floorGuid) { currentFloor = floorGuid; announced.Clear(); screenNotice = ""; }
            var triggers = registeredWalls;
            var active = activeWalls;
            active.Clear();
            foreach (var trigger in triggers)
                if (trigger && trigger.isActiveAndEnabled && trigger.hp > 0) active.Add(trigger.GetInstanceID());
            staleHints.Clear();
            foreach (int key in hints.Keys)
                if (!active.Contains(key) || !hints[key].Trigger)
                    staleHints.Add(key);
            foreach (int key in staleHints) { hints[key].Destroy(); hints.Remove(key); announced.Remove(key); }
            var floors = FloorGenerator.FloorGenerators.ToArray();
            foreach (var trigger in triggers)
            {
                if (!trigger || !trigger.isActiveAndEnabled || trigger.hp <= 0) continue;
                int id = trigger.GetInstanceID();
                if (!hints.TryGetValue(id, out var hint))
                { hint = new Hint { Trigger = trigger }; hints.Add(id, hint); }
                // Retry unresolved entries; never substitute a nearby room.
                if (hint.Entrance == null || !hint.Entrance.Generator)
                    if (EntranceResolver.TryResolve(trigger, floors, out var entry))
                    {
                        hint.Entrance = entry;
                        Logger.LogInfo($"Hidden entrance: generator={entry.Generator.GetType().Name}, floor={entry.Generator.name}, guid={entry.Generator.guid}, index={trigger.hiddenRoomIndex}, position={trigger.transform.position}, direction={entry.Direction}");
                    }
                UpdateWall(hint);
                UpdateMap(hint);
            }
            // Count only the local player's floor, once the full scan has completed.
            bool newHint = false;
            int localCount = 0;
            foreach (var hint in hints.Values)
                if (hint.Entrance != null && hint.Entrance.Generator && !string.IsNullOrEmpty(floorGuid)
                    && hint.Entrance.Generator.guid == floorGuid)
                { localCount++; newHint |= announced.Add(hint.Trigger.GetInstanceID()); }
            RefreshPortals(floors);
            foreach (var hint in portals.Values)
                if (hint.Entrance != null && hint.Entrance.Generator && !string.IsNullOrEmpty(floorGuid)
                    && hint.Entrance.Generator.guid == floorGuid)
                { localCount++; newHint |= announced.Add(hint.Stone.GetInstanceID()); }
            if (newHint) ShowTestNotice($"本层存在隐藏入口（{localCount}处）", Color.yellow);
        }

        void UpdateWall(Hint hint)
        {
            var floor = hint.Entrance?.Generator as TileFloorGenerator;
            if (hint.Position.HasValue && floor && floor.GenerateSuccess && floor.wall
                && hint.PositionMap == floor.wall && hint.PositionHp == hint.Trigger.hp)
            {
                hint.Position = floor.wall.transform.TransformPoint(hint.LocalPosition);
                return;
            }
            hint.Position = null;
            if (!showWallMarker.Value) return;
            if (!OpeningTiles.TryGet(hint.Entrance, out var map, out var cells) || cells.Count == 0) return;
            // Select the connected-room-facing edge of the exact opening cells, never a collider guess.
            Vector2 direction = hint.Entrance.Direction;
            float front = cells.Min(c => c.X*direction.x + c.Y*direction.y);
            Vector3 sum = Vector3.zero; int count=0;
            foreach(var cell in cells)
                if(Mathf.Abs(cell.X*direction.x+cell.Y*direction.y-front)<0.01f)
                { sum += map.GetCellCenterWorld(new Vector3Int(cell.X,cell.Y,0));count++; }
            if(count>0)
            {
                hint.Position=sum/count;
                hint.PositionMap=map;
                hint.LocalPosition=map.transform.InverseTransformPoint(hint.Position.Value);
                hint.PositionHp=hint.Trigger.hp;
            }
        }

        void UpdateMap(Hint hint)
        {
            var entry = hint.Entrance;
            var map = entry != null && entry.Generator ? entry.Generator.mapInstance : null;
            if (hint.MapMarker && (!showMapMarker.Value || map != hint.Map))
            { Object.Destroy(hint.MapMarker.gameObject); hint.MapMarker = null; }
            if (!showMapMarker.Value || !map || !map.contentsChild || map.rooms == null) return;

            UI_Map_Room icon = null;
            foreach (var room in map.rooms)
            {
                if (room is UI_Map_EnhancedProceduralDungeonRoom enhanced && ReferenceEquals(enhanced.room, entry.Room)) icon = room;
                if (room is UI_Map_LibraryProceduralDungeonRoom library && ReferenceEquals(library.Room, entry.Room)) icon = room;
                if (icon) break;
            }
            if (!icon) return;
            if (!hint.MapMarker)
            {
                hint.MapMarker = CreateEntranceMarker(map.contentsChild,new Vector2(12,22));
                hint.Map = map;
                hint.MapMarker.SetAsLastSibling();
            }
            Vector2 size = icon.GetRoomIconSize();
            hint.MapMarker.anchoredPosition = icon.GetIconCenterAnchoredPosition()
                + Vector2.Scale(entry.Direction, new Vector2(Mathf.Max(0, size.x / 2 - 5), Mathf.Max(0, size.y / 2 - 5)));
        }

        internal void RefreshRoute(UI_WorldMapStageElement element)
        {
            if (!element) return;
            routeElements.Add(element);
            var label = element.transform.Find("HiddenRoomRouteBadge");
            bool show = showRouteMarker.Value && element.floor != null && HintRules.HasPlannedRoom(element.floor.hiddenRoomCount);
            if (!show) { if (label && label.gameObject.activeSelf) label.gameObject.SetActive(false); return; }
            if (!label)
            {
                var template = element.GetComponentInChildren<TMP_Text>(true);
                var go = new GameObject("HiddenRoomRouteBadge", typeof(RectTransform));
                var rect = go.GetComponent<RectTransform>();
                rect.SetParent(element.transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(0, -3);
                rect.sizeDelta = new Vector2(68, 20);
                var background = go.AddComponent<Image>();
                background.color = new Color(0.13f, 0.09f, 0.02f, 0.94f);
                background.raycastTarget = false;
                var textGo = new GameObject("Label", typeof(RectTransform));
                var textRect = textGo.GetComponent<RectTransform>();
                textRect.SetParent(rect, false);
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                var text = textGo.AddComponent<TextMeshProUGUI>();
                if (template && template.font) text.font = template.font;
                text.text = "隐藏房";
                text.fontSize = 14;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(1, 0.85f, 0.25f);
                text.raycastTarget = false;
                label = rect;
            }
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
        }

        void OnGUI()
        {
            if (showScreenNotice.Value && Time.unscaledTime <= screenNoticeUntil && screenNotice.Length != 0)
            {
                Color old = GUI.color;
                GUI.color = new Color(1, 0.92f, 0.45f, 0.95f);
                GUI.Box(new Rect(16, 16, 460, 44), screenNotice);
                GUI.color = old;
            }
        }

        void OnDestroy()
        {
            Config.SettingChanged -= HandleSettingChanged;
            harmony?.UnpatchSelf();
            DestroyPortals();
            foreach (var hint in hints.Values) hint.Destroy();
            hints.Clear();
            foreach (var element in routeElements)
            { if (!element) continue; var badge = element.transform.Find("HiddenRoomRouteBadge"); if (badge) Object.Destroy(badge.gameObject); }
            if (Instance == this) Instance = null;
        }
    }

    [HarmonyPatch(typeof(UI_WorldMapStageElement), "SetFloor")]
    internal static class RoutePatch
    {
        static void Postfix(UI_WorldMapStageElement __instance)
        {
            var plugin = HiddenRoomHintPlugin.Instance;
            if (!plugin) return;
            try { plugin.RefreshRoute(__instance); } catch (Exception error) { plugin.Report(error); }
        }
    }
}
