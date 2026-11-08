using System.Collections.Generic;
using PokerShop;
using UnityEngine;


public static class FpsItemCombatHandler
{
    public static FpsItemDefinition[] GetOwnedItems(
        ShopCatalog catalog,
        PlayerInventory inventory)
    {
        var owned = new List<FpsItemDefinition>();
        if (catalog == null || inventory == null)
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

    public static CombatStats Apply(
        CombatStats baseStats,
        IReadOnlyList<FpsItemDefinition> items)
    {
        if (items == null)
            return baseStats;

        for (int i = 0; i < items.Count; i++)
        {
            FpsItemDefinition item = items[i];
            if (item == null)
                continue;

            CombatStats bonus = item.BonusStats;
            baseStats.attack += bonus.attack;
            baseStats.health += bonus.health;
            baseStats.fireRate += bonus.fireRate;
            baseStats.moveSpeed += bonus.moveSpeed;
        }

        return baseStats;
    }
}
