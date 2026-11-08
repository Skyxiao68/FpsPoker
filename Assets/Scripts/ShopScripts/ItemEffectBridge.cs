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

    /// <summary>
    /// 赢一手奖励（Ben 2026-11-08 定）：把四项里最高属性的 10% 白给最低属性。
    /// 只作用于"这一手"的临时战斗数值，不跨手累加，也不写回任何持久数据。
    /// </summary>
    public static CombatStats ApplyWinBonus(CombatStats stats)
    {
        return ApplyWinBonus(stats, out _, out _);
    }

    /// <summary>
    /// 同上，另外回报加到哪一项、加了多少，供结算面板显示。
    /// </summary>
    public static CombatStats ApplyWinBonus(CombatStats stats, out int lowestIndex, out float amount)
    {
        lowestIndex = -1;
        amount = 0f;

        float[] v = { stats.attack, stats.health, stats.fireRate, stats.moveSpeed };

        // 并列时取靠前的一项（ATK → HP → ROF → SPD），保证结果可复现。
        int hi = 0, lo = 0;
        for (int i = 1; i < 4; i++)
        {
            if (v[i] > v[hi]) hi = i;
            if (v[i] < v[lo]) lo = i;
        }

        // 四项相等（或全 0）时没有"最高/最低"可分，直接跳过。
        if (v[hi] <= 0f || Mathf.Abs(v[hi] - v[lo]) < 0.0001f)
            return stats;

        amount = v[hi] * 0.1f;
        switch (lo)
        {
            case 0: stats.attack += amount; break;
            case 1: stats.health += amount; break;
            case 2: stats.fireRate += amount; break;
            case 3: stats.moveSpeed += amount; break;
        }

        lowestIndex = lo;
        return stats;
    }
}