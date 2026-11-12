using System.Collections.Generic;
using PokerShop;
using UnityEngine;


public static class FpsItemCombatHandler
{
    public static bool HasItem(
        IReadOnlyList<FpsItemDefinition> items,
        ItemId itemId)
    {
        if (items == null || itemId == ItemId.None)
            return false;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].ItemId == itemId)
                return true;
        }

        return false;
    }

    public static FpsItemDefinition[] GetOwnedItems(
        ShopCatalog catalog,
        PlayerInventory inventory)
    {
        var owned = new List<FpsItemDefinition>();
        if (catalog == null && Shop.Service != null)
            catalog = Shop.Service.Catalog;
        if (catalog == null || catalog.items == null || inventory == null)
            return owned.ToArray();

        foreach (ShopItem shopItem in catalog.items)
        {
            if (shopItem == null
                || !(shopItem.payload is FpsItemDefinition fpsItem)
                || fpsItem.ItemId == ItemId.None
                || fpsItem.FpsItemId != shopItem.id
                || !inventory.Has(fpsItem.ItemId))
                continue;

            owned.Add(fpsItem);
        }

        return owned.ToArray();
    }

    public static float GetHeadshotMultiplier(
        float baseMultiplier,
        IReadOnlyList<FpsItemDefinition> items)
    {
        float multiplier = Mathf.Max(1f, baseMultiplier);
        if (items == null)
            return multiplier;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null)
                multiplier = Mathf.Max(multiplier, items[i].HeadshotMultiplier);
        }

        return multiplier;
    }

    public static CombatStats GetBonusStats(IReadOnlyList<FpsItemDefinition> items)
    {
        CombatStats total = new CombatStats();
        if (items == null)
            return total;

        for (int i = 0; i < items.Count; i++)
        {
            FpsItemDefinition item = items[i];
            if (item == null)
                continue;

            CombatStats bonus = item.BonusStats;
            total.attack += bonus.attack;
            total.health += bonus.health;
            total.fireRate += bonus.fireRate;
            total.moveSpeed += bonus.moveSpeed;
        }

        return total;
    }

    public static CombatStats Apply(
        CombatStats baseStats,
        IReadOnlyList<FpsItemDefinition> items)
    {
        CombatStats bonus = GetBonusStats(items);
        baseStats.attack += bonus.attack;
        baseStats.health += bonus.health;
        baseStats.fireRate += bonus.fireRate;
        baseStats.moveSpeed += bonus.moveSpeed;
        return baseStats;
    }
}
