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
        int spadesCount = 0, heartsCount = 0, clubsCount = 0, diamondsCount = 0;
        foreach (var c in bestHand.bestFive)
        {
            float v = c.GetAttributeValue();
            if (c.Suit == Suit.Spades) { spadesSum += v; spadesCount++; }
            else if (c.Suit == Suit.Hearts) { heartsSum += v; heartsCount++; }
            else if (c.Suit == Suit.Clubs) { clubsSum += v; clubsCount++; }
            else if (c.Suit == Suit.Diamonds) { diamondsSum += v; diamondsCount++; }
        }

        // ---- 花色堆叠：同花色每多一张，该花色倍率 ×1.3（封顶 ×2）----
        // 基础四项本身就是「花色点数和 × 倍率」，所以这里直接按花色缩放四项，
        // 再把四个 sum 一起缩放，后面的兼给类道具（刺青/红移/红桃）用的是同一套数字。
        if (PokerItemTraits.HasSuitStack())
        {
            float fS = PokerItemTraits.SuitStackFactor(spadesCount);
            float fH = PokerItemTraits.SuitStackFactor(heartsCount);
            float fC = PokerItemTraits.SuitStackFactor(clubsCount);
            float fD = PokerItemTraits.SuitStackFactor(diamondsCount);

            stats.attack *= fS;
            stats.health *= fH;
            stats.fireRate *= fC;
            stats.moveSpeed *= fD;

            spadesSum *= fS;
            heartsSum *= fH;
            clubsSum *= fC;
            diamondsSum *= fD;
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
    /// 赢摊牌奖励（Ben 2026-11-10 定 P2）：选中"相对自己基线最弱"的那一项，
    /// 让它的**总量**涨 15%，即 增量 += 0.15 × (基线 + 增量)。
    /// 只作用于这一手的临时战斗数值，不跨手累加，也不写回任何持久数据。
    /// 受益项必须是"吃到过花色"的（增量为 0 的项不参与），四项全空则不发。
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
        return ApplyWinBonus(stats, out lowestIndex, out amount, out _);
    }

    /// <summary>总战力提升比例：选中项的总量涨 WinBonusFactor。</summary>
    public const float WinBonusFactor = 0.15f;

    /// <summary>
    /// 完整版本：额外回报选中项的倍数，供日志与面板说明"这份奖励是从哪一项的强度换来的"。
    /// 选弱项按"相对基线的倍数"比，且只在吃过花色的项里选；
    /// 倍数并列时按 ATK → SPD → HP → ROF 取（缺输出比缺耐久更快被感知）。
    /// </summary>
    public static CombatStats ApplyWinBonus(
        CombatStats stats,
        out int lowestIndex,
        out float amount,
        out float ratio
    )
    {
        lowestIndex = -1;
        amount = 0f;
        ratio = 1f;

        float[] v = { stats.attack, stats.health, stats.fireRate, stats.moveSpeed };

        // 并列时按这个顺序取靠前的一项，保证结果可复现。
        int[] priority =
        {
            PokerStatUnits.Attack,
            PokerStatUnits.MoveSpeed,
            PokerStatUnits.Health,
            PokerStatUnits.FireRate,
        };

        int pick = -1;
        float weakest = float.MaxValue;
        for (int k = 0; k < priority.Length; k++)
        {
            int i = priority[k];
            if (v[i] <= 0.0001f) continue; // 没吃到花色的项不能当受益项，否则永远是 0
            float r = PokerStatUnits.Ratio(i, v[i]);
            if (pick < 0 || r < weakest - 0.0001f)
            {
                pick = i;
                weakest = r;
            }
        }

        // 四项全是 0（这一手什么都没吃）⇒ 不发，也不编一个假奖励出来。
        if (pick < 0) return stats;

        // 让这一项的"总量"涨 15%：增量 += 0.15 × (基线 + 增量)。
        amount = WinBonusFactor * (PokerStatUnits.Baseline(pick) + v[pick]);
        switch (pick)
        {
            case 0: stats.attack += amount; break;
            case 1: stats.health += amount; break;
            case 2: stats.fireRate += amount; break;
            case 3: stats.moveSpeed += amount; break;
        }

        lowestIndex = pick;
        ratio = weakest;
        return stats;
    }
}