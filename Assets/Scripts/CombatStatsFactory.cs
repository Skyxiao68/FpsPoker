using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 把扑克手牌转换成 FPS 战斗属性。
/// 纯静态工具类，不持有状态，也不引用任何场景对象。
/// </summary>
public static class CombatStatsFactory
{
    public static CombatStats Compute(
        List<Card> holeCards,
        HandResult bestHand,
        List<Card> communityCards)
    {
        float spades = 0f, hearts = 0f, clubs = 0f, diamonds = 0f;

        if (holeCards != null)
            foreach (Card c in holeCards)
                Accumulate(c, ref spades, ref hearts, ref clubs, ref diamonds);

        if (communityCards != null)
            foreach (Card c in communityCards)
                Accumulate(c, ref spades, ref hearts, ref clubs, ref diamonds);

        float mult = bestHand != null ? bestHand.GetMultiplier() : 1f;

        return new CombatStats
        {
            attack    = spades   * mult,
            health    = hearts   * mult,
            fireRate  = clubs    * mult,
            moveSpeed = diamonds * mult
        };
    }

    /// <summary>按花色把牌面数值累加到对应属性。</summary>
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