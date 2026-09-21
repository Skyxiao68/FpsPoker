using UnityEngine;

/// <summary>
/// 把扑克手牌转换成 FPS 战斗属性。
/// 使用摊牌时评估出的最佳 5 张牌（bestHand.bestFive）。
/// 纯静态工具类，不持有状态，也不引用任何场景对象。
/// </summary>
public static class CombatStatsFactory
{
    public static CombatStats Compute(HandResult bestHand)
    {
        if (bestHand == null || bestHand.bestFive == null || bestHand.bestFive.Count == 0)
        {
            Debug.LogWarning("CombatStatsFactory: bestHand 为空，返回默认数值");
            return new CombatStats();
        }

        float spades = 0f, hearts = 0f, clubs = 0f, diamonds = 0f;

        foreach (Card c in bestHand.bestFive)
            Accumulate(c, ref spades, ref hearts, ref clubs, ref diamonds);

        float mult = bestHand.GetMultiplier();

        return new CombatStats
        {
            attack    = spades   * mult,
            health    = hearts   * mult,
            fireRate  = clubs    * mult,
            moveSpeed = diamonds * mult
        };
    }

    private static void Accumulate(
        Card c, ref float s, ref float h, ref float cl, ref float d)
    {
        switch (c.Suit)
        {
            case Suit.Spades:   s  += c.GetAttributeValue(); break;
            case Suit.Hearts:   h  += c.GetAttributeValue(); break;
            case Suit.Clubs:    cl += c.GetAttributeValue(); break;
            case Suit.Diamonds: d  += c.GetAttributeValue(); break;
        }
    }
}