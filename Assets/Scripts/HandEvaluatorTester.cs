using System.Collections.Generic;
using UnityEngine;

public class HandEvaluatorTester : MonoBehaviour
{
    void Start()
    {
        TestHand("Royal Flush",   new string[] { "AS", "KS", "QS", "JS", "TS", "2H", "3D" }, HandType.RoyalFlush);
        TestHand("Straight Flush", new string[] { "9H", "8H", "7H", "6H", "5H", "KS", "AD" }, HandType.StraightFlush);
        TestHand("Four of a Kind", new string[] { "7S", "7H", "7D", "7C", "9H", "2D", "3S" }, HandType.FourOfAKind);
        TestHand("Full House",     new string[] { "8S", "8H", "8D", "5C", "5H", "KD", "QS" }, HandType.FullHouse);
        TestHand("Flush",          new string[] { "KS", "TS", "7S", "5S", "2S", "9H", "JD" }, HandType.Flush);
        TestHand("Straight",       new string[] { "6C", "7D", "8H", "9S", "TD", "KC", "AH" }, HandType.Straight);
        TestHand("Wheel Straight", new string[] { "AS", "2H", "3D", "4C", "5H", "KD", "QS" }, HandType.Straight);
        TestHand("Three of a Kind",new string[] { "4S", "4H", "4D", "9C", "6H", "KD", "QS" }, HandType.ThreeOfAKind);
        TestHand("Two Pair",       new string[] { "5C", "5D", "8S", "8H", "TD", "AS", "2H" }, HandType.TwoPair);
        TestHand("One Pair",       new string[] { "QD", "QS", "3H", "7C", "9D", "2S", "4H" }, HandType.OnePair);
        TestHand("High Card",      new string[] { "AD", "9S", "7H", "5C", "3D", "KH", "JS" }, HandType.HighCard);

        
        Debug.Log("---Same Hand Compare Test ---");

        // Twopair ：A A 8 8 K vs 5 5 8 8 K，
        List<Card> hand1 = ParseHand(new string[] { "AS", "AH", "8S", "8H", "KD", "2C", "3D" });
        List<Card> hand2 = ParseHand(new string[] { "5C", "5D", "8D", "8C", "KH", "2S", "3H" });
        HandResult r1 = HandEvaluator.Evaluate(hand1);
        HandResult r2 = HandEvaluator.Evaluate(hand2);
        Debug.Log($"Two Pair Compare：hand1={r1.handType}, hand2={r2.handType} |  hand1 Win（A Pair > 5 Pair）");
    }

    void TestHand(string name, string[] cardStrings, HandType expected)
    {
        List<Card> cards = ParseHand(cardStrings);
        HandResult result = HandEvaluator.Evaluate(cards);

        bool pass = result.handType == expected;
        string status = pass ? "PASS" : "FAIL";
        Debug.Log($"[{status}] {name} | Result：{result.handType}，Expected：{expected} | Card：{CardListToString(cards)}");
    }

    List<Card> ParseHand(string[] cardStrings)
    {
        List<Card> cards = new List<Card>();
        foreach (string s in cardStrings) cards.Add(ParseCard(s));
        return cards;
    }

    Card ParseCard(string s)
    {
        // Format："AS" = Ace of Spades，"TH" = 10 of Hearts
        string rankPart = s.Substring(0, s.Length - 1);
        char suitChar = s[s.Length - 1];

        Card c = new Card();
        c.Rank = RankPartToRank(rankPart);
        c.Suit = SuitCharToSuit(suitChar);
        return c;
    }

    Rank RankPartToRank(string r)
    {
        switch (r)
        {
            case "2":  return Rank.Two;
            case "3":  return Rank.Three;
            case "4":  return Rank.Four;
            case "5":  return Rank.Five;
            case "6":  return Rank.Six;
            case "7":  return Rank.Seven;
            case "8":  return Rank.Eight;
            case "9":  return Rank.Nine;
            case "T":  case "10": return Rank.Ten;
            case "J":  return Rank.Jack;
            case "Q":  return Rank.Queen;
            case "K":  return Rank.King;
            case "A":  return Rank.Ace;
            default:
                Debug.LogError("Cannot Analyze Rank：" + r);
                return Rank.Two;
        }
    }

    Suit SuitCharToSuit(char s)
    {
        switch (s)
        {
            case 'S': return Suit.Spades;
            case 'H': return Suit.Hearts;
            case 'C': return Suit.Clubs;
            case 'D': return Suit.Diamonds;
            default:
                Debug.LogError("Cannot Analyze Suit：" + s);
                return Suit.Spades;
        }
    }

    string CardListToString(List<Card> cards)
    {
        string s = "";
        foreach (Card c in cards) s += c.ToString() + " ";
        return s;
    }
}
