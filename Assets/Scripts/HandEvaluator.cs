using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Represents the ranking of poker hands from lowest to highest.
/// Used for comparing hands during showdown.
/// </summary>
public enum HandType
{
    HighCard = 0, // Highest card only
    OnePair = 1, // Two cards of the same rank
    TwoPair = 2, // Two different pairs
    ThreeOfAKind = 3, // Three cards of the same rank
    Straight = 4, // Five consecutive ranks
    Flush = 5, // Five cards of the same suit
    FullHouse = 6, // Three of a kind + a pair
    FourOfAKind = 7, // Four cards of the same rank
    StraightFlush = 8, // Five consecutive cards of the same suit
    RoyalFlush = 9, // A-K-Q-J-10 of the same suit
}

/// <summary>
/// Stores the result of a hand evaluation.
/// Contains the hand type, the best 5 cards, and tiebreaker values for comparison.
/// </summary>
public class HandResult
{
    public HandType handType; // The type of the evaluated hand
    public List<Card> bestFive; // The best 5 cards forming the hand

    /// <summary>
    /// Tiebreaker values used to compare hands of the same type.
    /// First value is the HandType, followed by rank values in descending importance.
    /// </summary>
    public int[] tiebreakers;

    /// <summary>
    /// Returns a multiplier based on hand strength.
    /// Used for combat calculations or AI decision weighting.
    /// </summary>
    public float GetMultiplier()
    {
        switch (handType)
        {
            case HandType.HighCard:
                return 1.0f;
            case HandType.OnePair:
                return 1.1f;
            case HandType.TwoPair:
                return 1.2f;
            case HandType.ThreeOfAKind:
                return 1.35f;
            case HandType.Straight:
                return 1.5f;
            case HandType.Flush:
                return 1.65f;
            case HandType.FullHouse:
                return 1.8f;
            case HandType.FourOfAKind:
                return 2.0f;
            case HandType.StraightFlush:
                return 2.5f;
            case HandType.RoyalFlush:
                return 3.0f;
            default:
                return 1.0f;
        }
    }

    /// <summary>
    /// Returns a human-readable name for the hand type (e.g., "Full House", "Straight").
    /// </summary>
    public string GetDisplayName()
    {
        switch (handType)
        {
            case HandType.HighCard:
                return "High Card";
            case HandType.OnePair:
                return "One Pair";
            case HandType.TwoPair:
                return "Two Pair";
            case HandType.ThreeOfAKind:
                return "Three of a Kind";
            case HandType.Straight:
                return "Straight";
            case HandType.Flush:
                return "Flush";
            case HandType.FullHouse:
                return "Full House";
            case HandType.FourOfAKind:
                return "Four of a Kind";
            case HandType.StraightFlush:
                return "Straight Flush";
            case HandType.RoyalFlush:
                return "Royal Flush";
            default:
                return "Unknown";
        }
    }
}

/// <summary>
/// Static class for evaluating poker hands.
/// Given a list of cards (typically 7: 2 hole + 5 community), finds the best 5-card combination.
/// </summary>
public static class HandEvaluator
{
    /// <summary>
    /// Main evaluation method: iterates over all possible 5-card combinations
    /// and returns the highest-ranking hand based on hand type and tiebreakers.
    /// </summary>
    public static HandResult Evaluate(List<Card> cards)
    {
        if (cards == null || cards.Count < 5)
        {
            Debug.LogError("Need at least 5 cards to evaluate hand");
            return null;
        }

        HandResult best = null;
        int n = cards.Count;

        // Generate all 5-card combinations using nested loops.
        for (int a = 0; a < n - 4; a++)
        for (int b = a + 1; b < n - 3; b++)
        for (int c = b + 1; c < n - 2; c++)
        for (int d = c + 1; d < n - 1; d++)
        for (int e = d + 1; e < n; e++)
        {
            List<Card> combo = new List<Card> { cards[a], cards[b], cards[c], cards[d], cards[e] };

            HandResult result = EvaluateFive(combo);

            // Keep the best hand found so far.
            if (best == null || CompareTiebreakers(result.tiebreakers, best.tiebreakers) > 0)
                best = result;
        }

        return best;
    }

    /// <summary>
    /// Evaluates exactly 5 cards and determines the hand type.
    /// Checks for flush, straight, and rank counts to classify the hand.
    /// </summary>
    private static HandResult EvaluateFive(List<Card> five)
    {
        // Sort cards in descending order by poker value.
        List<Card> sorted = new List<Card>(five);
        sorted.Sort((x, y) => y.GetPokerValue().CompareTo(x.GetPokerValue()));

        bool isFlush = IsFlush(sorted);
        bool isStraight = IsStraight(sorted, out int straightHigh);

        // Count how many times each rank appears.
        Dictionary<int, int> rankCount = new Dictionary<int, int>();
        foreach (Card c in sorted)
        {
            int v = c.GetPokerValue();
            if (!rankCount.ContainsKey(v))
                rankCount[v] = 0;
            rankCount[v]++;
        }

        // Sort ranks by count (descending), then by rank value (descending).
        List<int> ranksByCount = new List<int>(rankCount.Keys);
        ranksByCount.Sort(
            (x, y) =>
            {
                int cmp = rankCount[y].CompareTo(rankCount[x]);
                if (cmp != 0)
                    return cmp;
                return y.CompareTo(x);
            }
        );

        HandResult result = new HandResult();
        result.bestFive = sorted;

        // Royal Flush: straight flush with Ace high.
        if (isFlush && isStraight && straightHigh == 14)
        {
            result.handType = HandType.RoyalFlush;
            result.tiebreakers = new int[] { (int)HandType.RoyalFlush };
            return result;
        }

        // Straight Flush.
        if (isFlush && isStraight)
        {
            result.handType = HandType.StraightFlush;
            result.tiebreakers = new int[] { (int)HandType.StraightFlush, straightHigh };
            return result;
        }

        // Four of a Kind.
        if (rankCount[ranksByCount[0]] == 4)
        {
            result.handType = HandType.FourOfAKind;
            result.tiebreakers = new int[]
            {
                (int)HandType.FourOfAKind,
                ranksByCount[0],
                ranksByCount[1],
            };
            return result;
        }

        // Full House: three of a kind + a pair.
        if (rankCount[ranksByCount[0]] == 3 && rankCount[ranksByCount[1]] == 2)
        {
            result.handType = HandType.FullHouse;
            result.tiebreakers = new int[]
            {
                (int)HandType.FullHouse,
                ranksByCount[0],
                ranksByCount[1],
            };
            return result;
        }

        // Flush.
        if (isFlush)
        {
            result.handType = HandType.Flush;
            result.tiebreakers = new int[]
            {
                (int)HandType.Flush,
                sorted[0].GetPokerValue(),
                sorted[1].GetPokerValue(),
                sorted[2].GetPokerValue(),
                sorted[3].GetPokerValue(),
                sorted[4].GetPokerValue(),
            };
            return result;
        }

        // Straight.
        if (isStraight)
        {
            result.handType = HandType.Straight;
            result.tiebreakers = new int[] { (int)HandType.Straight, straightHigh };
            return result;
        }

        // Three of a Kind.
        if (rankCount[ranksByCount[0]] == 3)
        {
            result.handType = HandType.ThreeOfAKind;
            result.tiebreakers = new int[]
            {
                (int)HandType.ThreeOfAKind,
                ranksByCount[0],
                ranksByCount[1],
                ranksByCount[2],
            };
            return result;
        }

        // Two Pair.
        if (rankCount[ranksByCount[0]] == 2 && rankCount[ranksByCount[1]] == 2)
        {
            result.handType = HandType.TwoPair;
            result.tiebreakers = new int[]
            {
                (int)HandType.TwoPair,
                ranksByCount[0],
                ranksByCount[1],
                ranksByCount[2],
            };
            return result;
        }

        // One Pair.
        if (rankCount[ranksByCount[0]] == 2)
        {
            result.handType = HandType.OnePair;
            result.tiebreakers = new int[]
            {
                (int)HandType.OnePair,
                ranksByCount[0],
                ranksByCount[1],
                ranksByCount[2],
                ranksByCount[3],
            };
            return result;
        }

        // High Card.
        result.handType = HandType.HighCard;
        result.tiebreakers = new int[]
        {
            (int)HandType.HighCard,
            sorted[0].GetPokerValue(),
            sorted[1].GetPokerValue(),
            sorted[2].GetPokerValue(),
            sorted[3].GetPokerValue(),
            sorted[4].GetPokerValue(),
        };
        return result;
    }

    /// <summary>
    /// Checks if all cards in the list share the same suit.
    /// </summary>
    private static bool IsFlush(List<Card> five)
    {
        Suit s = five[0].Suit;
        for (int i = 1; i < five.Count; i++)
        {
            if (five[i].Suit != s)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Checks if the 5 cards form a consecutive sequence (Ace can be high or low).
    /// Handles the special "wheel" case: A-2-3-4-5 where Ace is the lowest card (high card = 5).
    /// </summary>
    private static bool IsStraight(List<Card> five, out int highCard)
    {
        // Collect unique rank values.
        List<int> values = new List<int>();
        foreach (Card c in five)
        {
            int v = c.GetPokerValue();
            if (!values.Contains(v))
                values.Add(v);
        }

        // A straight must have 5 distinct ranks.
        if (values.Count != 5)
        {
            highCard = 0;
            return false;
        }

        // Check for a normal straight (consecutive descending values).
        bool isNormal = true;
        for (int i = 0; i < 4; i++)
        {
            if (values[i] - 1 != values[i + 1])
            {
                isNormal = false;
                break;
            }
        }

        if (isNormal)
        {
            highCard = values[0];
            return true;
        }

        // Check for the wheel: A-2-3-4-5 (Ace plays as 1, high card is 5).
        if (values[0] == 14 && values[1] == 5 && values[2] == 4 && values[3] == 3 && values[4] == 2)
        {
            highCard = 5;
            return true;
        }

        highCard = 0;
        return false;
    }

    /// <summary>
    /// Compares two tiebreaker arrays.
    /// Returns positive if A is better, negative if B is better, 0 if equal.
    /// </summary>
    private static int CompareTiebreakers(int[] a, int[] b)
    {
        int len = Mathf.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            if (a[i] != b[i])
                return a[i] - b[i];
        }
        return a.Length - b.Length;
    }
}
