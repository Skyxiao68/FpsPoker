public static class InventorySlots
{
    public const int Capacity = 3;

    public static int Used(PlayerInventory inventory)
    {
        if (inventory == null)
            return 0;

        int used = 0;
        foreach (var entry in inventory.All)
        {
            if (entry.Value > 0)
                used += entry.Value;
        }

        return used;
    }

    public static bool HasSpace(PlayerInventory inventory)
    {
        return inventory != null && Used(inventory) < Capacity;
    }
}
