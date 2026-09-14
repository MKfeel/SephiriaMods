using UnityEngine;
using Mirror;

namespace SephiriaBackpackOrganizer
{
    public partial class InventorySorter
    {
        private string lastWaitReason;

        public void Sort()
        {
            if (busy || requestPending) return;
            requestPending = true;
            requestedAt = Time.unscaledTime;
            lastWaitReason = null;
            Notify("整理中…");
            // Poll starts/continues the accepted request under its exception handler.
        }

        private void TryStartRequest()
        {
            if (!NetworkClient.active) { requestPending = false; readySnapshot = null; Plugin.Log.LogInfo("整理请求取消：不在游戏会话中。"); return; }
            if (Time.unscaledTime - requestedAt > 10f)
            {
                requestPending = false; readySnapshot = null;
                Plugin.Log.LogWarning("整理就绪检查超时（10秒），最后等待原因：" + (lastWaitReason ?? "未知"));
                Notify("整理失败"); return;
            }
            if (NetworkClient.localPlayer == null) { WaitForReady("本地玩家尚未就绪"); return; }
            var avatar = NetworkClient.localPlayer.GetComponent<PlayerAvatar>();
            var inv = avatar != null ? avatar.Inventory : null;
            if (inv == null) { WaitForReady("玩家背包尚未创建"); return; }
            if (!inv.isClient || !inv.isLocalPlayer) { WaitForReady($"背包网络身份未就绪：isClient={inv.isClient}, isLocalPlayer={inv.isLocalPlayer}"); return; }
            if (inv.CurrentInventoryStorage <= 1) { WaitForReady($"主背包容量尚未就绪：{inv.CurrentInventoryStorage}"); return; }
            if (IsDragging()) { WaitForReady("正在拖拽或抓取背包物品"); return; }
            // Compare the SAME main-grid scope in all three native maps, not the potion belt.
            var ids = new System.Collections.Generic.HashSet<int>();
            int charmCount = 0, tabletCount = 0;
            foreach (var pair in inv.inventoryMatrix)
            {
                if (!InventoryScope.IsMainCell(pair.Key.x, pair.Key.y, inv.CurrentInventoryStorage)) continue;
                var item = pair.Value;
                if (item == null) { WaitForReady($"主背包格 ({pair.Key.x},{pair.Key.y}) 的物品尚未同步"); return; }
                if (item.Entity == null) { WaitForReady($"物品定义未就绪：instance={item.InstanceID}, entity={item.EntityID}"); return; }
                if (!ids.Add(item.InstanceID)) { WaitForReady($"主背包实例重复：{item.InstanceID}"); return; }
                if (item.Entity.type == EItemType.Charm) charmCount++;
                if (item.Entity.type == EItemType.StoneTablet) tabletCount++;
                if (item.Entity.type == EItemType.Charm && (item.Charm == null || !inv.charms.TryGetValue(pair.Key, out var charm) || charm != item.Charm)) { WaitForReady($"神器映射未同步：instance={item.InstanceID}, cell=({pair.Key.x},{pair.Key.y})"); return; }
                if (item.Entity.type == EItemType.StoneTablet && (item.StoneTablet == null || !inv.stoneTablets.TryGetValue(pair.Key, out var tablet) || tablet != item.StoneTablet)) { WaitForReady($"石板映射未同步：instance={item.InstanceID}, cell=({pair.Key.x},{pair.Key.y})"); return; }
            }
            int mappedCharms = 0, mappedTablets = 0;
            foreach (var pair in inv.charms)
                if (InventoryScope.IsMainCell(pair.Key.x, pair.Key.y, inv.CurrentInventoryStorage)) mappedCharms++;
            foreach (var pair in inv.stoneTablets)
                if (InventoryScope.IsMainCell(pair.Key.x, pair.Key.y, inv.CurrentInventoryStorage)) mappedTablets++;
            if (charmCount != mappedCharms || tabletCount != mappedTablets) { WaitForReady($"主背包映射数量不一致：神器 {charmCount}/{mappedCharms}，石板 {tabletCount}/{mappedTablets}"); return; }
            var snapshot = CaptureState(inv);
            if (!VerifyInventorySnapshot(inv, snapshot)) { WaitForReady("主背包快照与物品集合不一致"); return; }
            if (readyInventory != inv || readySnapshot == null || !LayoutsEquivalent(snapshot, readySnapshot))
            {
                WaitForReady("等待主背包布局跨帧一致");
                readyInventory = inv; readySnapshot = snapshot; readyFrame = Time.frameCount; return;
            }
            if (Time.frameCount <= readyFrame) return;
            requestPending = false; readySnapshot = null;
            StartRequestedSort();
        }

        private void WaitForReady(string reason)
        {
            readySnapshot = null;
            if (reason == lastWaitReason) return;
            lastWaitReason = reason;
            Plugin.Log.LogInfo("整理等待：" + reason);
        }

        private void RestartRequest(PendingEnhancedSort state)
        {
            state.ctx.cancelled = true;
            pendingEnhanced = null; pendingSearch = null; busy = false;
            requestPending = true; readySnapshot = null;
            lastWaitReason = "搜索期间背包或标记变化，重新取快照";
            Plugin.Log.LogInfo("整理等待：" + lastWaitReason);
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
