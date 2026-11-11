public static class InventorySlots
{
    public const int Capacity = 5;

    public static int Used(PlayerInventory inventory)
    {
        if (inventory == null)
            return 0;

        // 一格 = 一种道具。同名叠加只加数量，不占新格（Ben 2026-11-08 定）。
        int used = 0;
        foreach (var entry in inventory.All)
        {
            if (entry.Value > 0)
                used++;
        }

        return used;
    }

    public static bool HasSpace(PlayerInventory inventory)
    {
        return inventory != null && Used(inventory) < Capacity;
    }
}
