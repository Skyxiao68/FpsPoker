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
    /// <summary>
    /// 这一手的"最终倍率"（牌型倍率 + 倍率类道具 + 加注惯性），面板和实战都用它。
    /// </summary>
    public static float EffectiveMultiplier(HandResult bestHand)
    {
        if (bestHand == null) return 1f;
        return bestHand.GetMultiplier()
            + PokerItemTraits.MultiplierBonus(bestHand.handType)
            + PokerItemTraits.MomentumBonus(PokerGameManager.LastHandRaiseCount);
    }

    public static CombatStats GetCombatStats(HandResult bestHand)
    {
        var stats = CombatStatsFactory.Compute(bestHand);

        if (bestHand == null || bestHand.bestFive == null)
            return stats;

        float baseMult = bestHand.GetMultiplier();
        float mult = EffectiveMultiplier(bestHand);

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
    /// 赢摊牌奖励（Ben 2026-11-10 定）：其余三项各按自身总量 +15%，最高项不动。
    /// 只作用于这一手的临时战斗数值，不跨手累加。
    /// </summary>
    public static CombatStats ApplyWinBonus(CombatStats stats)
    {
        return ApplyWinBonus(stats, out _, out _, out _);
    }

    /// <summary>
    /// 同上，另外回报最高项下标与"三项一共加了多少"，供日志与面板使用。
    /// </summary>
    public static CombatStats ApplyWinBonus(
        CombatStats stats,
        out int highestIndex,
        out float totalAdded
    )
    {
        return ApplyWinBonus(stats, out highestIndex, out totalAdded, out _);
    }

    /// <summary>每一项（除最高项外）的总量提升比例。</summary>
    public const float WinBonusFactor = 0.15f;

    /// <summary>
    /// ATK 单独走"固定点数"（Ben 2026-11-10 定）：FPS 侧 1 点 ATK = 1 点伤害，
    /// 而左轮基础伤害只有 5，所以按比例给会把伤害翻倍，改成固定 +2 点（约 +40% 伤害）。
    /// </summary>
    public const float AttackBonusPoints = 2f;

    /// <summary>
    /// 完整版本：额外回报"最高项"是哪一项、总共加了多少、它的倍数。
    /// 取四项里相对基线倍数最高的一项，其余三项各按自身总量 +15%，
    /// 即 增量 += 0.15 × (基线 + 增量)；最高项自己不加。
    /// 倍数并列时按 ATK → SPD → HP → ROF 取（缺输出比缺耐久更快被感知）。
    /// </summary>
    public static CombatStats ApplyWinBonus(
        CombatStats stats,
        out int highestIndex,
        out float totalAdded,
        out float highestRatio
    )
    {
        highestIndex = -1;
        totalAdded = 0f;
        highestRatio = 1f;

        float[] v = { stats.attack, stats.health, stats.fireRate, stats.moveSpeed };

        int[] priority =
        {
            PokerStatUnits.Attack,
            PokerStatUnits.MoveSpeed,
            PokerStatUnits.Health,
            PokerStatUnits.FireRate,
        };

        int pick = -1;
        float strongest = float.MinValue;
        for (int k = 0; k < priority.Length; k++)
        {
            int i = priority[k];
            float r = PokerStatUnits.Ratio(i, v[i]);
            if (pick < 0 || r > strongest + 0.0001f)
            {
                pick = i;
                strongest = r;
            }
        }

        // 其余三项：ATK 走固定点数，HP / ROF / SPD 按各自总量 +15%（避免跨单位搬运）。
        for (int i = 0; i < 4; i++)
        {
            if (i == pick) continue;
            float add = i == PokerStatUnits.Attack
                ? AttackBonusPoints
                : WinBonusFactor * (PokerStatUnits.Baseline(i) + v[i]);
            switch (i)
            {
                case 0: stats.attack += add; break;
                case 1: stats.health += add; break;
                case 2: stats.fireRate += add; break;
                case 3: stats.moveSpeed += add; break;
            }
            totalAdded += add;
        }

        highestIndex = pick;
        highestRatio = strongest;
        return stats;
    }
}