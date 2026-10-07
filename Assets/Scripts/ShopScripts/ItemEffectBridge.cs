using UnityEngine;

/// <summary>
/// 所有物品对战斗属性计算的修饰集中在这里。
///
/// Bridge 调用 GetCombatStats 而不是直接调 CombatStatsFactory。
/// 敌人不应用玩家的物品，所以敌人的计算还是走原版 CombatStatsFactory。
/// </summary>
public static class ItemEffectBridge
{
    /// <summary>
    /// 计算玩家的最终战斗属性，包含所有物品修饰。
    /// </summary>
    public static CombatStats GetCombatStats(HandResult bestHand)
    {
        var stats = CombatStatsFactory.Compute(bestHand);

        if (bestHand == null || bestHand.bestFive == null)
            return stats;

        // ---- 红桃皇后：把红桃贡献的 HP 挪到 ATK ----
        if (PlayerInventory.Instance != null
            && PlayerInventory.Instance.Has(ItemId.QueenOfHearts))
        {
            float heartsSum = 0;
            foreach (var c in bestHand.bestFive)
                if (c.Suit == Suit.Hearts)
                    heartsSum += c.GetAttributeValue();

            float mult = bestHand.GetMultiplier();
            float heartsHP = heartsSum * mult;

            stats.health -= heartsHP;
            stats.attack += heartsHP;

            // 防止浮点误差留负数
            if (stats.health < 0) stats.health = 0;
        }

        return stats;
    }
}