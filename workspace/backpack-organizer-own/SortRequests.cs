using UnityEngine;
using Mirror;

namespace SephiriaBackpackOrganizer
{
    public partial class InventorySorter
    {
        public void Sort()
        {
            if (busy || requestPending) return;
            requestPending = true;
            requestedAt = Time.unscaledTime;
            TryStartRequest();
        }

        private void TryStartRequest()
        {
            if (!NetworkClient.active) { requestPending = false; return; }
            if (Time.unscaledTime - requestedAt > 10f)
            {
                requestPending = false; readySnapshot = null;
                Notify("整理失败"); Plugin.Log.LogWarning("等待背包数据一致或拖拽结束超时。"); return;
            }
            if (NetworkClient.localPlayer == null) return;
            var avatar = NetworkClient.localPlayer.GetComponent<PlayerAvatar>();
            var inv = avatar != null ? avatar.Inventory : null;
            if (inv == null || !inv.isClient || !inv.isLocalPlayer || inv.CurrentInventoryStorage <= 1 || IsDragging()) { readySnapshot = null; return; }
            // All three native maps must refer to the same objects before taking a snapshot.
            var ids = new System.Collections.Generic.HashSet<int>();
            int charmCount = 0, tabletCount = 0;
            foreach (var pair in inv.inventoryMatrix)
            {
                var item = pair.Value;
                int index = inv.PosToIdx(pair.Key);
                if (item == null || pair.Key.x < 0 || pair.Key.x >= 6 || pair.Key.y < 0 || index < 0 || index >= inv.CurrentInventoryStorage) { readySnapshot = null; return; }
                if (item.Entity == null) { readySnapshot = null; return; }
                if (!ids.Add(item.InstanceID)) { readySnapshot = null; return; }
                if (item.Entity.type == EItemType.Charm) charmCount++;
                if (item.Entity.type == EItemType.StoneTablet) tabletCount++;
                if (item.Entity.type == EItemType.Charm && (item.Charm == null || !inv.charms.TryGetValue(pair.Key, out var charm) || charm != item.Charm)) { readySnapshot = null; return; }
                if (item.Entity.type == EItemType.StoneTablet && (item.StoneTablet == null || !inv.stoneTablets.TryGetValue(pair.Key, out var tablet) || tablet != item.StoneTablet)) { readySnapshot = null; return; }
            }
            if (charmCount != inv.charms.Count || tabletCount != inv.stoneTablets.Count) { readySnapshot = null; return; }
            var snapshot = CaptureState(inv);
            if (!VerifyInventorySnapshot(inv, snapshot)) { readySnapshot = null; return; }
            if (readyInventory != inv || readySnapshot == null || !LayoutsEquivalent(snapshot, readySnapshot))
            {
                readyInventory = inv; readySnapshot = snapshot; readyFrame = Time.frameCount; return;
            }
            if (Time.frameCount <= readyFrame) return;
            requestPending = false; readySnapshot = null;
            StartRequestedSort();
        }

        private void RestartRequest(PendingEnhancedSort state)
        {
            state.ctx.cancelled = true;
            pendingEnhanced = null; pendingSearch = null; busy = false;
            requestPending = true; readySnapshot = null;
        }

        private static bool IsDragging()
        {
            if (ManualPriorityManager.Dragging) return true;
            if (UIManager.Instance == null) return false;
            var mouse = UIManager.Instance.GetElement<UI_NewItemPicker>();
            var controller = UIManager.Instance.GetElement<UI_NewItemPicker_Controller>();
            return (mouse != null && mouse.CurrentPickedUp != null) || (controller != null && controller.CurrentPickedUp != null);
        }

        private static bool SameItems(System.Collections.Generic.List<Slot> original, System.Collections.Generic.List<Slot> target)
        {
            if (target == null || original.Count != target.Count) return false;
            var items = new System.Collections.Generic.Dictionary<int, Slot>();
            foreach (var slot in original)
            {
                if (slot == null) return false;
                if (slot.hasItem)
                {
                    if (items.ContainsKey(slot.instanceID)) return false;
                    items.Add(slot.instanceID, slot);
                }
            }
            foreach (var slot in target)
            {
                if (slot == null) return false;
                if (!slot.hasItem) continue;
                if (!items.TryGetValue(slot.instanceID, out var before) || slot.entityID != before.entityID || slot.quantity != before.quantity) return false;
                items.Remove(slot.instanceID);
            }
            return items.Count == 0;
        }
    }
}
