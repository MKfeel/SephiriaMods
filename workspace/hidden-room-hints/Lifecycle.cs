using System;
using BepInEx.Configuration;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SephiriaHiddenRoomHints
{
    public sealed partial class HiddenRoomHintPlugin
    {
        NetworkIdentity observedPlayer;
        PlayerAvatar localAvatar;

        internal void MarkDirty() => hintsDirty = true;

        internal void Register(HiddenRoomTriggerCollider wall, BreakableProp_HiddenPortal stone)
        {
            if (wall) hintsDirty |= registeredWalls.Add(wall);
            if (stone) hintsDirty |= registeredPortals.Add(stone);
        }

        internal void Unregister(HiddenRoomTriggerCollider wall, BreakableProp_HiddenPortal stone)
        {
            if (!ReferenceEquals(wall, null)) hintsDirty |= registeredWalls.Remove(wall);
            if (!ReferenceEquals(stone, null)) hintsDirty |= registeredPortals.Remove(stone);
        }

        void HandleSettingChanged(object sender, SettingChangedEventArgs args)
        {
            MarkDirty();
            routeElements.RemoveWhere(element => !element);
            foreach (var element in routeElements) RefreshRoute(element);
        }

        void ProcessEvents()
        {
            var player = NetworkClient.localPlayer;
            if (observedPlayer != player)
            {
                observedPlayer = player;
                localAvatar = player ? player.GetComponent<PlayerAvatar>() : null;
                MarkDirty();
            }
            // One known player's field, not a scene or object search.
            if (currentFloor != (localAvatar ? localAvatar.currentFloorGuid : "")) MarkDirty();
            if (!hintsDirty) return;
            // Coalesce callbacks until LateUpdate, after the native operation has finished.
            hintsDirty = false;
            RefreshHints();
        }
    }

    // Added only to hidden entrance objects. Unity callbacks cover disable/re-enable and destroy.
    public sealed class HiddenEntranceLifetime : MonoBehaviour
    {
        HiddenRoomTriggerCollider wall;
        BreakableProp_HiddenPortal stone;
        void Awake()
        {
            wall = GetComponent<HiddenRoomTriggerCollider>();
            stone = GetComponent<BreakableProp_HiddenPortal>();
        }
        void OnEnable() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.Register(wall, stone); }
        void Start() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.MarkDirty(); }
        void OnDisable() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.Unregister(wall, stone); }
        void OnDestroy() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.Unregister(wall, stone); }
    }

    [HarmonyPatch(typeof(CombatBehaviour), "Awake")]
    internal static class EntranceCreatedPatch
    {
        static void Postfix(CombatBehaviour __instance)
        {
            if (!(__instance is HiddenRoomTriggerCollider) && !(__instance is BreakableProp_HiddenPortal)) return;
            var plugin = HiddenRoomHintPlugin.Instance;
            if (!plugin) return;
            try
            {
                if (!__instance.GetComponent<HiddenEntranceLifetime>())
                    __instance.gameObject.AddComponent<HiddenEntranceLifetime>();
            }
            catch (Exception error) { plugin.Report(error); }
        }
    }

    // Called by Generate only after GenerateSuccess becomes true on each client.
    [HarmonyPatch(typeof(HorayModAPI), "NotifyFloorAllocatedClientside")]
    internal static class FloorReadyPatch
    {
        static void Postfix() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.MarkDirty(); }
    }

    // Spawn data and the initial network transform have been applied at this point.
    [HarmonyPatch(typeof(CombatBehaviour), "OnStartClient")]
    internal static class EntranceClientStartedPatch
    {
        static void Postfix(CombatBehaviour __instance)
        {
            if ((__instance is HiddenRoomTriggerCollider || __instance is BreakableProp_HiddenPortal)
                && HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.MarkDirty();
        }
    }

    [HarmonyPatch(typeof(FloorGenerator), "OnDestroy")]
    internal static class FloorRemovedPatch
    {
        static void Postfix() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.MarkDirty(); }
    }

    [HarmonyPatch(typeof(FloorGenerator), "CreateMapUI")]
    internal static class MapCreatedPatch
    {
        static void Postfix() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.MarkDirty(); }
    }

    [HarmonyPatch(typeof(HiddenRoomTriggerCollider), "ApplyDamage")]
    internal static class WallChangedPatch
    {
        static void Postfix() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.MarkDirty(); }
    }

    [HarmonyPatch(typeof(BreakableProp_HiddenPortal), "Connect")]
    internal static class PortalConnectedPatch
    {
        static void Postfix() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.MarkDirty(); }
    }

    [HarmonyPatch(typeof(BreakableProp_HiddenPortal), "DeserializeSyncVars")]
    internal static class PortalSyncedPatch
    {
        static void Postfix() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.MarkDirty(); }
    }

    [HarmonyPatch(typeof(BreakableProp_HiddenPortal), "HandleBreakServerSide")]
    internal static class PortalBrokenServerPatch
    {
        static void Postfix() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.MarkDirty(); }
    }

    [HarmonyPatch(typeof(BreakableProp_HiddenPortal), "HandleBreakClientSide")]
    internal static class PortalBrokenClientPatch
    {
        static void Postfix() { if (HiddenRoomHintPlugin.Instance) HiddenRoomHintPlugin.Instance.MarkDirty(); }
    }
}
