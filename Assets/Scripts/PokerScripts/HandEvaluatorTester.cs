using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Editor/test script to verify the HandEvaluator works correctly.
/// Tests all major hand types from Royal Flush down to High Card,
/// and also compares two hands of the same type.
/// </summary>
public class HandEvaluatorTester : MonoBehaviour
{
    /// <summary>
    /// Unity Start method.
    /// Runs a series of hand evaluation tests and logs PASS/FAIL for each.
    /// </summary>
    void Start()
    {
        // Test each major hand type.
        TestHand(
            "Royal Flush",
            new string[] { "AS", "KS", "QS", "JS", "TS", "2H", "3D" },
            HandType.RoyalFlush
        );
        TestHand(
            "Straight Flush",
            new string[] { "9H", "8H", "7H", "6H", "5H", "KS", "AD" },
            HandType.StraightFlush
        );
        TestHand(
            "Four of a Kind",
            new string[] { "7S", "7H", "7D", "7C", "9H", "2D", "3S" },
            HandType.FourOfAKind
        );
        TestHand(
            "Full House",
            new string[] { "8S", "8H", "8D", "5C", "5H", "KD", "QS" },
            HandType.FullHouse
        );
        TestHand(
            "Flush",
            new string[] { "KS", "TS", "7S", "5S", "2S", "9H", "JD" },
            HandType.Flush
        );
        TestHand(
            "Straight",
            new string[] { "6C", "7D", "8H", "9S", "TD", "KC", "AH" },
            HandType.Straight
        );
        TestHand(
            "Wheel Straight", // A-2-3-4-5 straight (Ace plays as low)
            new string[] { "AS", "2H", "3D", "4C", "5H", "KD", "QS" },
            HandType.Straight
        );
        TestHand(
            "Three of a Kind",
            new string[] { "4S", "4H", "4D", "9C", "6H", "KD", "QS" },
            HandType.ThreeOfAKind
        );
        TestHand(
            "Two Pair",
            new string[] { "5C", "5D", "8S", "8H", "TD", "AS", "2H" },
            HandType.TwoPair
        );
        TestHand(
            "One Pair",
            new string[] { "QD", "QS", "3H", "7C", "9D", "2S", "4H" },
            HandType.OnePair
        );
        TestHand(
            "High Card",
            new string[] { "AD", "9S", "7H", "5C", "3D", "KH", "JS" },
            HandType.HighCard
        );

        Debug.Log("---Same Hand Compare Test ---");

        // TwoPair comparison: A A 8 8 K vs 5 5 8 8 K.
        // The hand with the higher pair (Aces) should win.
        List<Card> hand1 = ParseHand(new string[] { "AS", "AH", "8S", "8H", "KD", "2C", "3D" });
        List<Card> hand2 = ParseHand(new string[] { "5C", "5D", "8D", "8C", "KH", "2S", "3H" });
        HandResult r1 = HandEvaluator.Evaluate(hand1);
        HandResult r2 = HandEvaluator.Evaluate(hand2);
        Debug.Log(
            $"Two Pair Compare: hand1={r1.handType}, hand2={r2.handType} | hand1 wins (A Pair > 5 Pair)"
        );
    }

    /// <summary>
    /// Runs a single test case: parses card strings, evaluates them,
    /// and checks whether the result matches the expected hand type.
    /// </summary>
    void TestHand(string name, string[] cardStrings, HandType expected)
    {
        List<Card> cards = ParseHand(cardStrings);
        HandResult result = HandEvaluator.Evaluate(cards);

        bool pass = result.handType == expected;
        string status = pass ? "PASS" : "FAIL";
        Debug.Log(
            $"[{status}] {name} | Result: {result.handType}, Expected: {expected} | Cards: {CardListToString(cards)}"
        );
    }

    /// <summary>
    /// Parses an array of card strings (e.g., "AS", "TH") into a list of Card objects.
    /// </summary>
    List<Card> ParseHand(string[] cardStrings)
    {
        List<Card> cards = new List<Card>();
        foreach (string s in cardStrings)
            cards.Add(ParseCard(s));
        return cards;
    }

    /// <summary>
    /// Parses a 2 or 3 character card string into a Card.
    /// Format: first char(s) = rank, last char = suit
    /// (e.g., "AS" = Ace of Spades, "TH" = 10 of Hearts).
    /// </summary>
    Card ParseCard(string s)
    {
        // The last character is the suit; everything before it is the rank.
        string rankPart = s.Substring(0, s.Length - 1);
        char suitChar = s[s.Length - 1];

        Card c = new Card();
        c.Rank = RankPartToRank(rankPart);
        c.Suit = SuitCharToSuit(suitChar);
        return c;
    }

    /// <summary>
    /// Converts a rank string to the corresponding Rank enum value.
    /// Supports "2" through "10", "T", "J", "Q", "K", "A".
    /// </summary>
    Rank RankPartToRank(string r)
    {
        switch (r)
        {
            case "2":
                return Rank.Two;
            case "3":
                return Rank.Three;
            case "4":
                return Rank.Four;
            case "5":
                return Rank.Five;
            case "6":
                return Rank.Six;
            case "7":
                return Rank.Seven;
            case "8":
                return Rank.Eight;
            case "9":
                return Rank.Nine;
            case "T":
            case "10":
                return Rank.Ten;
            case "J":
                return Rank.Jack;
            case "Q":
                return Rank.Queen;
            case "K":
                return Rank.King;
            case "A":
                return Rank.Ace;
            default:
                Debug.LogError("Cannot parse Rank: " + r);
                return Rank.Two;
        }
    }

    /// <summary>
    /// Converts a suit character ('S', 'H', 'C', 'D') to the corresponding Suit enum value.
    /// </summary>
    Suit SuitCharToSuit(char s)
    {
        switch (s)
        {
            case 'S':
                return Suit.Spades;
            case 'H':
                return Suit.Hearts;
            case 'C':
                return Suit.Clubs;
            case 'D':
                return Suit.Diamonds;
            default:
                Debug.LogError("Cannot parse Suit: " + s);
                return Suit.Spades;
        }
    }

    /// <summary>
    /// Utility method to join a list of Cards into a space-separated string for debug logging.
    /// </summary>
    string CardListToString(List<Card> cards)
    {
        string s = "";
        foreach (Card c in cards)
            s += c.ToString() + " ";
        return s;
    }
}
