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

        float baseMult = bestHand.GetMultiplier();
        float mult = baseMult
            + PokerItemTraits.MultiplierBonus(bestHand.handType)
            + PokerItemTraits.MomentumBonus(PokerGameManager.LastHandRaiseCount);

        // 倍率类道具（平A加成 / 赌高牌）：整体等比缩放，保持四项之间的比例不变。
        if (!Mathf.Approximately(mult, baseMult) && baseMult > 0f)
        {
            float k = mult / baseMult;
            stats.attack *= k;
            stats.health *= k;
            stats.fireRate *= k;
            stats.moveSpeed *= k;
        }

        float spadesSum = 0f, heartsSum = 0f, clubsSum = 0f, diamondsSum = 0f;
        foreach (var c in bestHand.bestFive)
        {
            float v = c.GetAttributeValue();
            if (c.Suit == Suit.Spades) spadesSum += v;
            else if (c.Suit == Suit.Hearts) heartsSum += v;
            else if (c.Suit == Suit.Clubs) clubsSum += v;
            else if (c.Suit == Suit.Diamonds) diamondsSum += v;
        }

        // ---- 四色归一：整手都按「点数总和最高的花色」结算 ----
        // 并列时按 ♠ ♥ ♣ ♦ 的顺序取靠前的那个。
        if (PokerItemTraits.HasMonochrome())
        {
            float spade = spadesSum, heart = heartsSum, club = clubsSum, diamond = diamondsSum;
            float[] totals = { spade, heart, club, diamond };
            int best = 0;
            for (int i = 1; i < totals.Length; i++)
                if (totals[i] > totals[best])
                    best = i;

            float all = spade + heart + club + diamond;

            stats.attack = 0f;
            stats.health = 0f;
            stats.fireRate = 0f;
            stats.moveSpeed = 0f;

            switch (best)
            {
                case 0: stats.attack = all * mult; break;
                case 1: stats.health = all * mult; break;
                case 2: stats.fireRate = all * mult; break;
                default: stats.moveSpeed = all * mult; break;
            }

            return stats;
        }

        // ---- 黑桃刺青：♠ 按 70% 兼给 HP ----
        if (PokerItemTraits.Has(ItemId.SpadeTattoo))
            stats.health += spadesSum * PokerItemTraits.SpadeHealthShare * mult;

        // ---- 红移：♦ 按 50% 兼给 ATK ----
        if (PokerItemTraits.Has(ItemId.RedShift))
            stats.attack += diamondsSum * PokerItemTraits.DiamondAttackShare * mult;

        // ---- 红桃皇后：♥ 按 50% 兼给 ATK（保留原来的 HP） ----
        if (PokerItemTraits.Has(ItemId.QueenOfHearts))
            stats.attack += heartsSum * PokerItemTraits.QueenHeartsAttackShare * mult;

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