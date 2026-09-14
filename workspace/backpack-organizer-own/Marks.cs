using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SephiriaBackpackOrganizer
{
    internal static class ManualPriorityManager
    {
        private static readonly MarkStore Store = new MarkStore();
        private static readonly Dictionary<int, int> Entities = new Dictionary<int, int>();
        private static readonly Dictionary<int, ItemMark> Effective = new Dictionary<int, ItemMark>();
        internal static int Revision { get; private set; }
        internal static int InventoryRevision { get; private set; }
        internal static int Count => Effective.Count;
        internal static bool Dragging;

        internal static ItemMark Observe(NewItemOwnInstance item)
        {
            if (!Mirror.NetworkClient.active || item == null || item.Charm == null) return ItemMark.None;
            Entities[item.InstanceID] = item.EntityID;
            bool favorite = SaveManager.Current != null && SaveManager.Current.GetBool("Item_Favorite_" + item.EntityID, false);
            var mark = Store.Resolve(item.InstanceID, favorite);
            if (!Effective.TryGetValue(item.InstanceID, out var previous) || previous != mark)
            {
                Effective[item.InstanceID] = mark;
                Revision++;
            }
            return mark;
        }
        internal static int GetRank(int id) => Effective.TryGetValue(id, out var mark) ? (int)mark : 0;
        internal static void InventoryChanged() { InventoryRevision++; }
        internal static void Toggle(NewItemOwnInstance item, bool control)
        {
            Observe(item);
            bool favorite = SaveManager.Current != null && SaveManager.Current.GetBool("Item_Favorite_" + item.EntityID, false);
            Effective[item.InstanceID] = Store.Toggle(item.InstanceID, favorite, control);
            Revision++;
            RefreshVisibleBadges();
        }
        internal static Dictionary<int, int> PruneAndSnapshot(HashSet<int> present)
        {
            var snapshot = new Dictionary<int, int>();
            foreach (int id in present)
                if (GetRank(id) != 0) snapshot[id] = GetRank(id);
            return snapshot;
        }
        internal static void Clear()
        {
            Store.Clear(); Entities.Clear(); Effective.Clear(); Dragging = false; Revision++;
            RefreshVisibleBadges();
        }
        internal static void FavoriteChanged()
        {
            foreach (var pair in Entities)
            {
                bool favorite = SaveManager.Current != null && SaveManager.Current.GetBool("Item_Favorite_" + pair.Value, false);
                Effective[pair.Key] = Store.Resolve(pair.Key, favorite);
            }
            Revision++;
            RefreshVisibleBadges();
        }
        internal static int RefreshVisibleBadges()
        {
            int shown = 0;
            foreach (var icon in Resources.FindObjectsOfTypeAll<UI_NewInventoryIcon>())
                if (icon != null && icon.gameObject.scene.IsValid() && ManualPriorityBadge.GetOrCreate(icon).Refresh()) shown++;
            return shown;
        }
        internal static string Label(int rank) => rank == 1 ? "↑↑" : rank == 2 ? "↑" : rank == 3 ? "↓" : rank == 4 ? "↓↓" : "";
    }

    [HarmonyPatch(typeof(UI_NewInventoryIcon), "OnPointerClick")]
    internal static class ManualPriorityClickPatch
    {
        private static bool Prefix(UI_NewInventoryIcon __instance, PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Middle) return true;
            if (Plugin.Instance == null || !Plugin.Instance.ManualPriorityEnabled.Value || __instance == null || !__instance.Showing || __instance.Item == null || __instance.Item.Charm == null || __instance.Inventory == null || !__instance.Inventory.isLocalPlayer) return true;
            bool control = Keyboard.current != null ? Keyboard.current.ctrlKey.isPressed : Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            ManualPriorityManager.Toggle(__instance.Item, control);
            return false;
        }
    }

    [HarmonyPatch(typeof(SaveData), "SetBool")]
    internal static class FavoriteChangePatch
    {
        private static void Postfix(string key)
        {
            if (key != null && key.StartsWith("Item_Favorite_", StringComparison.Ordinal)) ManualPriorityManager.FavoriteChanged();
        }
    }
    [HarmonyPatch(typeof(UI_NewInventoryIcon), "OnBeginDrag")]
    internal static class BeginDragPatch
    {
        private static void Postfix(UI_NewInventoryIcon __instance, PointerEventData eventData)
        {
            if (__instance.Inventory != null && __instance.Inventory.isLocalPlayer && eventData.button == PointerEventData.InputButton.Left) ManualPriorityManager.Dragging = true;
        }
    }
    [HarmonyPatch(typeof(UI_NewInventoryIcon), "OnEndDrag")]
    internal static class EndDragPatch { private static void Postfix() { ManualPriorityManager.Dragging = false; } }
}
