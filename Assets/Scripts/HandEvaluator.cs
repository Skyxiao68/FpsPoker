using System.Collections.Generic;
using UnityEngine;

public enum HandType
{
    HighCard = 0,
    OnePair = 1,
    TwoPair = 2,
    ThreeOfAKind = 3,
    Straight = 4,
    Flush = 5,
    FullHouse = 6,
    FourOfAKind = 7,
    StraightFlush = 8,
    RoyalFlush = 9
}

public class HandResult
{
    public HandType handType;
    public List<Card> bestFive;
    public int[] tiebreakers; 

    public float GetMultiplier()
    {
        switch (handType)
        {
            case HandType.HighCard:       return 1.0f;
            case HandType.OnePair:        return 1.1f;
            case HandType.TwoPair:        return 1.2f;
            case HandType.ThreeOfAKind:   return 1.35f;
            case HandType.Straight:       return 1.5f;
            case HandType.Flush:          return 1.65f;
            case HandType.FullHouse:      return 1.8f;
            case HandType.FourOfAKind:    return 2.0f;
            case HandType.StraightFlush:  return 2.5f;
            case HandType.RoyalFlush:     return 3.0f;
            default:                      return 1.0f;
        }
    }

    public string GetDisplayName()
    {
        switch (handType)
        {
            case HandType.HighCard:      return "High Card";
            case HandType.OnePair:       return "One Pair";
            case HandType.TwoPair:       return "Two Pair";
            case HandType.ThreeOfAKind:  return "Three of a Kind";
            case HandType.Straight:      return "Straight";
            case HandType.Flush:         return "Flush";
            case HandType.FullHouse:     return "Full House";
            case HandType.FourOfAKind:   return "Four of a Kind";
            case HandType.StraightFlush: return "Straight Flush";
            case HandType.RoyalFlush:    return "Royal Flush";
            default:                     return "Unknown";
        }
    }
}

public static class HandEvaluator
{
    
    public static HandResult Evaluate(List<Card> cards)
    {
        if (cards == null || cards.Count < 5)
        {
            Debug.LogError("需要至少 5 张牌来评估");
            return null;
        }

        HandResult best = null;
        int n = cards.Count;

        
        for (int a = 0; a < n - 4; a++)
        for (int b = a + 1; b < n - 3; b++)
        for (int c = b + 1; c < n - 2; c++)
        for (int d = c + 1; d < n - 1; d++)
        for (int e = d + 1; e < n; e++)
        {
            List<Card> combo = new List<Card>
            {
                cards[a], cards[b], cards[c], cards[d], cards[e]
            };

            HandResult result = EvaluateFive(combo);

            if (best == null || CompareTiebreakers(result.tiebreakers, best.tiebreakers) > 0)
                best = result;
        }

        return best;
    }

    
    private static HandResult EvaluateFive(List<Card> five)
    {
        
        List<Card> sorted = new List<Card>(five);
        sorted.Sort((x, y) => y.GetPokerValue().CompareTo(x.GetPokerValue()));

        bool isFlush = IsFlush(sorted);
        bool isStraight = IsStraight(sorted, out int straightHigh);

        
        Dictionary<int, int> rankCount = new Dictionary<int, int>();
        foreach (Card c in sorted)
        {
            int v = c.GetPokerValue();
            if (!rankCount.ContainsKey(v)) rankCount[v] = 0;
            rankCount[v]++;
        }

        
        List<int> ranksByCount = new List<int>(rankCount.Keys);
        ranksByCount.Sort((x, y) =>
        {
            int cmp = rankCount[y].CompareTo(rankCount[x]);
            if (cmp != 0) return cmp;
            return y.CompareTo(x);
        });

        HandResult result = new HandResult();
        result.bestFive = sorted;

        
        if (isFlush && isStraight && straightHigh == 14)
        {
            result.handType = HandType.RoyalFlush;
            result.tiebreakers = new int[] { (int)HandType.RoyalFlush };
            return result;
        }

        
        if (isFlush && isStraight)
        {
            result.handType = HandType.StraightFlush;
            result.tiebreakers = new int[] { (int)HandType.StraightFlush, straightHigh };
            return result;
        }

        
        if (rankCount[ranksByCount[0]] == 4)
        {
            result.handType = HandType.FourOfAKind;
            result.tiebreakers = new int[] { (int)HandType.FourOfAKind, ranksByCount[0], ranksByCount[1] };
            return result;
        }

        
        if (rankCount[ranksByCount[0]] == 3 && rankCount[ranksByCount[1]] == 2)
        {
            result.handType = HandType.FullHouse;
            result.tiebreakers = new int[] { (int)HandType.FullHouse, ranksByCount[0], ranksByCount[1] };
            return result;
        }

        
        if (isFlush)
        {
            result.handType = HandType.Flush;
            result.tiebreakers = new int[]
            {
                (int)HandType.Flush,
                sorted[0].GetPokerValue(), sorted[1].GetPokerValue(), sorted[2].GetPokerValue(),
                sorted[3].GetPokerValue(), sorted[4].GetPokerValue()
            };
            return result;
        }

        
        if (isStraight)
        {
            result.handType = HandType.Straight;
            result.tiebreakers = new int[] { (int)HandType.Straight, straightHigh };
            return result;
        }

        
        if (rankCount[ranksByCount[0]] == 3)
        {
            result.handType = HandType.ThreeOfAKind;
            result.tiebreakers = new int[]
            {
                (int)HandType.ThreeOfAKind, ranksByCount[0], ranksByCount[1], ranksByCount[2]
            };
            return result;
        }

        
        if (rankCount[ranksByCount[0]] == 2 && rankCount[ranksByCount[1]] == 2)
        {
            result.handType = HandType.TwoPair;
            result.tiebreakers = new int[]
            {
                (int)HandType.TwoPair, ranksByCount[0], ranksByCount[1], ranksByCount[2]
            };
            return result;
        }

        
        if (rankCount[ranksByCount[0]] == 2)
        {
            result.handType = HandType.OnePair;
            result.tiebreakers = new int[]
            {
                (int)HandType.OnePair, ranksByCount[0], ranksByCount[1], ranksByCount[2], ranksByCount[3]
            };
            return result;
        }

        
        result.handType = HandType.HighCard;
        result.tiebreakers = new int[]
        {
            (int)HandType.HighCard,
            sorted[0].GetPokerValue(), sorted[1].GetPokerValue(), sorted[2].GetPokerValue(),
            sorted[3].GetPokerValue(), sorted[4].GetPokerValue()
        };
        return result;
    }

    private static bool IsFlush(List<Card> five)
    {
        Suit s = five[0].Suit;
        for (int i = 1; i < five.Count; i++)
        {
            if (five[i].Suit != s) return false;
        }
        return true;
    }

    private static bool IsStraight(List<Card> five, out int highCard)
    {
        List<int> values = new List<int>();
        foreach (Card c in five)
        {
            int v = c.GetPokerValue();
            if (!values.Contains(v)) values.Add(v);
        }

        
        if (values.Count != 5) { highCard = 0; return false; }

        
        bool isNormal = true;
        for (int i = 0; i < 4; i++)
        {
            if (values[i] - 1 != values[i + 1]) { isNormal = false; break; }
        }

        if (isNormal)
        {
            highCard = values[0];
            return true;
        }

        
        if (values[0] == 14 && values[1] == 5 && values[2] == 4 && values[3] == 3 && values[4] == 2)
        {
            highCard = 5;
            return true;
        }

        highCard = 0;
        return false;
    }

    
    private static int CompareTiebreakers(int[] a, int[] b)
    {
        int len = Mathf.Min(a.Length, b.Length);
        for (int i = 0; i < len; i++)
        {
            if (a[i] != b[i]) return a[i] - b[i];
        }
        return a.Length - b.Length;
    }
}