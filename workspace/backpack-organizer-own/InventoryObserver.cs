using Mirror;

namespace SephiriaBackpackOrganizer
{
    internal static class InventoryObserver
    {
        private static GridInventory observed;
        internal static void Detach()
        {
            if (!ReferenceEquals(observed, null))
            {
                observed.OnItemAddedForClient -= Added;
                observed.OnItemRemovedForClient -= Removed;
            }
            observed = null;
        }
        internal static void Poll()
        {
            var avatar = NetworkClient.active && NetworkClient.localPlayer != null ? NetworkClient.localPlayer.GetComponent<PlayerAvatar>() : null;
            var next = avatar != null ? avatar.Inventory : null;
            if (ReferenceEquals(next, observed)) return;
            if (!ReferenceEquals(observed, null))
            {
                observed.OnItemAddedForClient -= Added;
                observed.OnItemRemovedForClient -= Removed;
            }
            observed = next;
            ManualPriorityManager.Clear();
            if (observed == null) return;
            observed.OnItemAddedForClient += Added;
            observed.OnItemRemovedForClient += Removed;
            foreach (var item in observed.inventoryMatrix.Values) ManualPriorityManager.Observe(item);
            ManualPriorityManager.RefreshVisibleBadges();
        }
        private static void Added(NewItemOwnInstance item)
        {
            ManualPriorityManager.Observe(item);
            ManualPriorityManager.InventoryChanged();
        }
        private static void Removed(ItemPosition position) { ManualPriorityManager.InventoryChanged(); }
    }
}
