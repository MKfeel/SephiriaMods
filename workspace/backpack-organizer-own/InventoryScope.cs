namespace SephiriaBackpackOrganizer
{
    internal static class InventoryScope
    {
        // inventoryMatrix also contains potion-belt and other non-grid storage entries.
        // Match the cells CaptureState/IdxToPos actually rearranges, including partial rows.
        internal static bool IsMainCell(int x, int y, int storage, int width = 6)
        {
            return width > 0 && storage > 0 && x >= 0 && x < width && y >= 0
                && (long)y * width + x < storage;
        }
    }
}
