using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration parameters for the enemy AI's poker-playing behavior.
/// Each parameter controls a different aspect of decision-making.
/// </summary>
[System.Serializable]
public class EnemyAIParameters
{
    // Display name of the enemy (shown in UI).
    public string enemyName = "Enemy";

    /// <summary>
    /// Likelihood to initiate a voluntary bet (0 = never, 1 = always).
    /// Higher values make the AI more likely to raise when the player checks.
    /// </summary>
    [Range(0f, 1f)]
    public float raiseTendency = 0.5f;

    /// <summary>
    /// Likelihood to fold when faced with a raise (0 = never fold, 1 = always fold).
    /// Higher values make the AI more cautious.
    /// </summary>
    [Range(0f, 1f)]
    public float foldTendency = 0.3f;

    /// <summary>
    /// Size of bets relative to the pot (0 = small bets, 1 = large bets).
    /// Higher aggression produces bigger raise amounts.
    /// </summary>
    [Range(0f, 1f)]
    public float aggression = 0.5f;

    /// <summary>
    /// Probability of bluffing when holding a weak hand (0 = never bluff, 1 = always bluff).
    /// When triggered, the AI may bet or call despite low hand strength.
    /// </summary>
    [Range(0f, 1f)]
    public float bluffChance = 0.1f;

    // Describes the enemy's combat style (used for flavor / other systems).
    public string combatStyle = "Ranged";
}

/// <summary>
/// Result of the AI's decision-making process.
/// </summary>
public class EnemyDecision
{
    // The action chosen by the AI: Fold, Check, or Raise.
    public EnemyAction action;

    // The amount to raise. Only valid when action == Raise.
    public int raiseAmount;

    // The AI's estimated strength of its hand (0.0 = weakest, 1.0 = strongest).
    public float handStrength;
}

/// <summary>
/// Static class containing the AI's poker decision logic.
/// Uses hand strength, pot odds, street position, and personality parameters to decide actions.
/// </summary>
public static class EnemyPokerAI
{
    /// <summary>
    /// Main decision function: evaluates the AI's hand and decides whether to Fold, Check, or Raise.
    /// Takes into account the current game state, AI parameters, and betting context.
    /// </summary>
    /// <param name="p">AI personality parameters.</param>
    /// <param name="holeCards">AI's two private hole cards.</param>
    /// <param name="communityCards">Community cards currently on the table.</param>
    /// <param name="street">Current betting street (PreFlop, Flop, Turn, River).</param>
    /// <param name="pot">Current total pot size.</param>
    /// <param name="currentBet">Amount the player has bet/raised this round.</param>
    /// <param name="enemyChips">AI's remaining chips.</param>
    /// <param name="playerRaised">True if the player has raised this round.</param>
    /// <param name="playerRaiseAmount">The amount the player raised (if playerRaised is true).</param>
    /// <returns>An EnemyDecision containing the chosen action and raise amount.</returns>
    public static EnemyDecision Decide(
        EnemyAIParameters p,
        List<Card> holeCards,
        List<Card> communityCards,
        Street street,
        int pot,
        int currentBet,
        int enemyChips,
        bool playerRaised,
        int playerRaiseAmount,
        float playerAggression = 0.5f
    )
    {
        EnemyDecision decision = new EnemyDecision();

        // Step 1: Calculate hand strength (0.0 to 1.0) based on current cards.
        float strength = CalculateHandStrength(holeCards, communityCards, street);
        decision.handStrength = strength;

        // Step 2: Calculate pot odds - the ratio of the call amount to the total pot.
        // Used to decide whether calling a raise is mathematically worthwhile.
        float potOdds = 0f;
        if (playerRaised && playerRaiseAmount > 0)
            potOdds = (float)playerRaiseAmount / (pot + playerRaiseAmount);

        // Step 3: Street caution factor - AI becomes more cautious on later streets.
        // More community cards means more information, so the AI plays tighter.
        float caution = GetStreetCaution(street);

        // Step 4: Determine if this is a bluffing scenario (weak hand + bluff chance triggers).
        bool isBluff = Random.value < p.bluffChance && strength < 0.4f;

        // Step 5: Make the decision based on whether the player raised or not.
        if (playerRaised)
        {
            return DecideAgainstRaise(
                p,
                strength,
                potOdds,
                caution,
                playerAggression,
                isBluff,
                pot,
                street,
                enemyChips,
                playerRaiseAmount
            );
        }
        else
        {
            return DecideWhenUnraised(p, strength, caution, isBluff, pot, street, enemyChips);
        }
    }

    /// <summary>
    /// Decision-making logic when the player has raised.
    /// </summary>
    private static EnemyDecision DecideAgainstRaise(
        EnemyAIParameters p,
        float strength,
        float potOdds,
        float caution,
        float playerAggression,
        bool isBluff,
        int pot,
        Street street,
        int enemyChips,
        int playerRaiseAmount
    )
    {
        EnemyDecision d = new EnemyDecision();
        d.handStrength = strength;

        // ---------- 计算 fold 阈值 ----------
        // 基线：底池赔率 + 街道谨慎
        float foldThreshold = potOdds * 0.8f + caution;

        // 玩家越激进 → AI 越不应该 fold（怀疑玩家在诈唬）
        // 最高降低 0.25
        foldThreshold -= playerAggression * 0.25f;

        // 手牌强度修正
        if (strength > 0.6f)
            foldThreshold -= 0.15f;
        else if (strength < 0.3f)
            foldThreshold += 0.1f;

        // 底池相对筹码大小：底池越大越值得跟注
        float potRatio = (float)pot / Mathf.Max(1, enemyChips);
        foldThreshold -= Mathf.Clamp01(potRatio) * 0.15f;

        // 加注成本太高时，稍微保守
        float costRatio = (float)playerRaiseAmount / Mathf.Max(1, enemyChips);
        if (costRatio > 0.4f)
            foldThreshold += 0.1f;

        // 敌人人格微调
        foldThreshold += (p.foldTendency - 0.5f) * 0.3f;
        foldThreshold -= (p.raiseTendency - 0.5f) * 0.15f;

        // 如果已经诈唬，进一步降低 fold 概率（继续演）
        if (isBluff)
            foldThreshold -= 0.15f;

        // 钳制在合理范围，避免极端
        foldThreshold = Mathf.Clamp(foldThreshold, 0.02f, 0.65f);

        // ---------- 决策 ----------
        // Fold：手牌实在弱且成本高
        if (strength < foldThreshold)
        {
            d.action = EnemyAction.Fold;
            d.raiseAmount = 0;
            return d;
        }

        // 反加：手牌很强，且敌人激进，且有概率
        bool canAffordReRaise = enemyChips > playerRaiseAmount * 2;
        bool strongHand = strength > 0.72f;
        if (strongHand && canAffordReRaise && Random.value < p.aggression * 0.35f)
        {
            int reRaise = CalculateReRaiseAmount(p, pot, street, enemyChips, playerRaiseAmount);
            if (reRaise > playerRaiseAmount)
            {
                d.action = EnemyAction.Raise;
                d.raiseAmount = reRaise;
                return d;
            }
        }

        // 默认：跟注（用 Raise 表达，金额 = 玩家的加注额）
        d.action = EnemyAction.Raise;
        d.raiseAmount = playerRaiseAmount;
        return d;
    }

    /// <summary>
    /// Decision-making logic when the player has not raised.
    /// </summary>
    private static EnemyDecision DecideWhenUnraised(
        EnemyAIParameters p,
        float strength,
        float caution,
        bool isBluff,
        int pot,
        Street street,
        int enemyChips
    )
    {
        EnemyDecision d = new EnemyDecision();
        d.handStrength = strength;

        float raiseThreshold = 1f - p.raiseTendency - caution;

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
                d.action = EnemyAction.Raise;
                d.raiseAmount = raiseAmount;
                return d;
            }
        }

        d.action = EnemyAction.Check;
        d.raiseAmount = 0;
        return d;
    }

    /// <summary>
    /// Calculates the raise amount based on pot size, street, AI aggression, and remaining chips.
    /// Higher aggression and later streets produce larger bets.
    /// </summary>
    private static int CalculateRaiseAmount(
        EnemyAIParameters p, int pot, Street street, int enemyChips)
    {
        float baseRatio = 0.3f + p.aggression * 0.4f;
        float streetMult = GetStreetMultiplier(street);
        int amount = Mathf.RoundToInt(pot * baseRatio * streetMult);
        amount = Mathf.Max(amount, 5);
        amount = Mathf.Min(amount, enemyChips);
        return amount;
    }

    /// <summary>
    /// Calculates the re-raise amount based on pot size, street, AI aggression, and remaining chips.
    /// Higher aggression and later streets produce larger bets.
    /// </summary>
      private static int CalculateReRaiseAmount(
        EnemyAIParameters p, int pot, Street street, int enemyChips, int playerRaiseAmount)
    {
        float baseRatio = 0.5f + p.aggression * 0.5f;
        float streetMult = GetStreetMultiplier(street);
        int amount = Mathf.RoundToInt(pot * baseRatio * streetMult);
        amount = Mathf.Max(amount, playerRaiseAmount + 5);
        amount = Mathf.Min(amount, enemyChips);
        return amount;
    }

    /// <summary>
    /// Returns a multiplier for raise size based on the current betting street.
    /// </summary>
    private static float GetStreetMultiplier(Street street)
    {
        switch (street)
        {
            case Street.PreFlop: return 0.6f;
            case Street.Flop:    return 1.0f;
            case Street.Turn:    return 1.3f;
            case Street.River:   return 1.6f;
            default:             return 1.0f;
        }
    }



    /// <summary>
    /// Returns a caution value that increases with each betting street.
    /// The AI becomes more risk-averse as more community cards are revealed.
    /// </summary>
    private static float GetStreetCaution(Street street)
    {
        switch (street)
        {
            case Street.PreFlop: return 0f;
            case Street.Flop:    return 0.05f;
            case Street.Turn:    return 0.1f;
            case Street.River:   return 0.15f;
            default:             return 0f;
        }
    }


    /// <summary>
    /// Public method to evaluate the strength of a hand (returns 0.0 to 1.0).
    /// Uses pre-flop heuristics before community cards, or actual hand evaluation after.
    /// </summary>
    public static float CalculateHandStrength(List<Card> hole, List<Card> community, Street street)
    {
        if (street == Street.PreFlop)
            return EvaluatePreFlopStrength(hole);
        else
            return EvaluatePostFlopStrength(hole, community);
    }

    /// <summary>
    /// Pre-flop hand evaluation based on heuristics.
    /// Considers pair status, suitedness, rank gap, and high card bonuses.
    /// </summary>
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

        // Pocket pairs are very strong: scale from 0.45 (22) to 1.0 (AA).
        if (pair)
            return Mathf.Lerp(0.45f, 1.0f, (hi - 2) / 12f);

        // Base score from average card quality.
        float baseScore = (hi + lo) / 28f;

        // Suited cards have flush potential.
        if (suited)
            baseScore += 0.3f;

        // Connected cards have straight potential.
        int gap = hi - lo;
        if (gap == 1)
            baseScore += 0.08f;
        else if (gap == 2)
            baseScore += 0.04f;

        // Ace bonus for high card value.
        if (hi == (int)Rank.Ace)
            baseScore += 0.05f;

        return Mathf.Clamp01(baseScore);
    }

    /// <summary>
    /// Post-flop hand strength evaluation using the actual HandEvaluator.
    /// Maps hand types to strength values (0.2 for High Card, 1.0 for Royal Flush).
    /// </summary>
    private static float EvaluatePostFlopStrength(List<Card> hole, List<Card> community)
    {
        List<Card> all = new List<Card>(hole);
        all.AddRange(community);

        // Need at least 5 cards to evaluate a full hand.
        if (all.Count < 5)
            return 0.3f;

        HandResult result = HandEvaluator.Evaluate(all);
        if (result == null)
            return 0.3f;

        // Map each hand type to a strength value between 0 and 1.
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
