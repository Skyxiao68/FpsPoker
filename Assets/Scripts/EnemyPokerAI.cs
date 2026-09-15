using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemyAIParameters
{
    public string enemyName = "Enemy";

    [Range(0f, 1f)]
    public float raiseTendency = 0.5f; // 主动加注倾向

    [Range(0f, 1f)]
    public float foldTendency = 0.3f; // 面对加注时的弃牌倾向

    [Range(0f, 1f)]
    public float aggression = 0.5f; // 加注金额激进程度

    [Range(0f, 1f)]
    public float bluffChance = 0.1f; // 诈唬概率
    public string combatStyle = "Melee";
}

public class EnemyDecision
{
    public EnemyAction action;
    public int raiseAmount; // 只在 action == Raise 时有效
    public float handStrength;
}

public static class EnemyPokerAI
{
    // 主决策函数
    public static EnemyDecision Decide(
        EnemyAIParameters p,
        List<Card> holeCards,
        List<Card> communityCards,
        Street street,
        int pot,
        int currentBet,
        int enemyChips,
        bool playerRaised,
        int playerRaiseAmount
    )
    {
        EnemyDecision decision = new EnemyDecision();

        // 1. 计算手牌强度 0-1
        float strength = CalculateHandStrength(holeCards, communityCards, street);
        decision.handStrength = strength;

        // 2. 计算底池赔率
        float potOdds = 0f;
        if (playerRaised && playerRaiseAmount > 0)
            potOdds = (float)playerRaiseAmount / (pot + playerRaiseAmount);

        // 3. 街道谨慎度：越往后越谨慎
        float streetCaution = GetStreetCaution(street);

        // 4. 是否是诈唬轮
        bool isBluff = Random.value < p.bluffChance && strength < 0.4f;

        // 5. 决策
        if (playerRaised)
        {
            // 面对玩家加注
            float foldThreshold = potOdds + streetCaution + (1f - p.raiseTendency) * 0.3f;
            if (isBluff)
                foldThreshold -= 0.2f;

            if (strength < foldThreshold)
            {
                decision.action = EnemyAction.Fold;
                return decision;
            }

            // 强度足够：匹配（在 PokerGameManager 里会处理成 match）
            // 高强度和激进 AI 有概率反加
            if (strength > 0.75f && Random.value < p.aggression * 0.5f)
            {
                int reRaise = CalculateRaiseAmount(p, pot, street, enemyChips);
                if (reRaise > playerRaiseAmount && enemyChips >= reRaise)
                {
                    decision.action = EnemyAction.Raise;
                    decision.raiseAmount = reRaise;
                    return decision;
                }
            }

            decision.action = EnemyAction.Raise;
            decision.raiseAmount = playerRaiseAmount;
            return decision;
        }
        else
        {
            // 玩家没加注，AI 主动决策
            float raiseThreshold = 1f - p.raiseTendency - streetCaution;

            if (strength > 0.7f)
                raiseThreshold -= 0.3f;
            else if (strength > 0.5f)
                raiseThreshold -= 0.15f;
            else if (strength < 0.3f)
                raiseThreshold += 0.2f;

            if (isBluff)
                raiseThreshold -= 0.25f;

            if (Random.value > raiseThreshold)
            {
                int raiseAmount = CalculateRaiseAmount(p, pot, street, enemyChips);
                if (raiseAmount > 0 && enemyChips >= raiseAmount)
                {
                    decision.action = EnemyAction.Raise;
                    decision.raiseAmount = raiseAmount;
                    return decision;
                }
            }

            decision.action = EnemyAction.Check;
            return decision;
        }
    }

    // 根据底池、街道、激进程度计算加注金额
    private static int CalculateRaiseAmount(
        EnemyAIParameters p,
        int pot,
        Street street,
        int enemyChips
    )
    {
        float baseRatio = 0.3f + p.aggression * 0.4f;
        float streetMult = 1f;
        switch (street)
        {
            case Street.PreFlop:
                streetMult = 0.6f;
                break;
            case Street.Flop:
                streetMult = 1.0f;
                break;
            case Street.Turn:
                streetMult = 1.3f;
                break;
            case Street.River:
                streetMult = 1.6f;
                break;
        }

        int amount = Mathf.RoundToInt(pot * baseRatio * streetMult);
        amount = Mathf.Max(amount, 5);
        amount = Mathf.Min(amount, enemyChips);
        return amount;
    }

    private static float GetStreetCaution(Street street)
    {
        switch (street)
        {
            case Street.PreFlop:
                return 0f;
            case Street.Flop:
                return 0.05f;
            case Street.Turn:
                return 0.1f;
            case Street.River:
                return 0.15f;
            default:
                return 0f;
        }
    }

    // 手牌强度评估
    public static float CalculateHandStrength(List<Card> hole, List<Card> community, Street street)
    {
        if (street == Street.PreFlop)
            return EvaluatePreFlopStrength(hole);
        else
            return EvaluatePostFlopStrength(hole, community);
    }

    // Pre-Flop：基于两张底牌
    private static float EvaluatePreFlopStrength(List<Card> hole)
    {
        if (hole == null || hole.Count < 2)
            return 0.3f;

        Card a = hole[0];
        Card b = hole[1];
        int hi = Mathf.Max((int)a.Rank, (int)b.Rank);
        int lo = Mathf.Min((int)a.Rank, (int)b.Rank);
        bool suited = a.Suit == b.Suit;
        bool pair = a.Rank == b.Rank;

        // 对子
        if (pair)
            return Mathf.Lerp(0.45f, 1.0f, (hi - 2) / 12f);

        float baseScore = (hi + lo) / 28f;
        if (suited)
            baseScore += 0.1f;

        int gap = hi - lo;
        if (gap == 1)
            baseScore += 0.08f;
        else if (gap == 2)
            baseScore += 0.04f;

        if (hi == (int)Rank.Ace)
            baseScore += 0.05f;

        return Mathf.Clamp01(baseScore);
    }

    // Post-Flop：使用 HandEvaluator
    private static float EvaluatePostFlopStrength(List<Card> hole, List<Card> community)
    {
        List<Card> all = new List<Card>(hole);
        all.AddRange(community);
        if (all.Count < 5)
            return 0.3f;

        HandResult result = HandEvaluator.Evaluate(all);
        if (result == null)
            return 0.3f;

        switch (result.handType)
        {
            case HandType.HighCard:
                return 0.2f;
            case HandType.OnePair:
                return 0.4f;
            case HandType.TwoPair:
                return 0.55f;
            case HandType.ThreeOfAKind:
                return 0.7f;
            case HandType.Straight:
                return 0.8f;
            case HandType.Flush:
                return 0.85f;
            case HandType.FullHouse:
                return 0.9f;
            case HandType.FourOfAKind:
                return 0.95f;
            case HandType.StraightFlush:
                return 0.98f;
            case HandType.RoyalFlush:
                return 1.0f;
            default:
                return 0.3f;
        }
    }
}
