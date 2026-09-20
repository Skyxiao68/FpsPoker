using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Test script to verify Deck functionality.
/// Attaches to a GameObject in the Unity scene and runs tests on Start().
/// </summary>
public class DeckTester : MonoBehaviour
{
    /// <summary>
    /// Unity Start method.
    /// Creates a deck, shuffles it, draws 7 cards, and verifies the results.
    /// </summary>
    void Start()
    {
        // Create and initialize a new deck (52 cards).
        Deck deck = new Deck();
        deck.Initialize();
        deck.Shuffle();

        // Deal 7 cards and store them in a hand list.
        List<Card> hand = new List<Card>();
        for (int i = 0; i < 7; i++)
        {
            Card card = deck.Draw();
            if (card != null)
                hand.Add(card);
        }

        // Build a string representation of the 7 dealt cards.
        string handStr = "7 cards: ";
        foreach (Card c in hand)
        {
            handStr += c.ToString() + " ";
        }
        Debug.Log(handStr);

        // Log the remaining number of cards (should be 52 - 7 = 45).
        Debug.Log("Remains: " + deck.RemainingCount());

        // Verify that all 7 dealt cards are unique by using a HashSet.
        HashSet<string> uniqueCards = new HashSet<string>();
        foreach (Card c in hand)
        {
            uniqueCards.Add(c.ToString());
        }

        // Should print 7 if no duplicates were dealt.
        Debug.Log("Unique cards: " + uniqueCards.Count);
    }
}
