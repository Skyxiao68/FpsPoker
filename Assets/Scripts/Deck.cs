using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a standard 52-card deck with initialization, shuffling, and drawing.
/// The deck is represented as a list of Card objects, and a draw index tracks
/// which card will be drawn next.
/// </summary>
public class Deck
{
    // The list of all 52 cards in the deck.
    private List<Card> cards = new List<Card>();

    // Tracks which card is next to be drawn from the deck.
    // Cards before this index have already been drawn.
    private int drawIndex = 0;

    /// <summary>
    /// Creates a full 52-card deck by iterating through all suits and ranks.
    /// Clears any existing cards and resets the draw index.
    /// </summary>
    public void Initialize()
    {
        cards.Clear();
        drawIndex = 0;

        // Loop through every possible suit.
        foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
        {
            // Loop through every possible rank.
            foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
            {
                Card card = new Card();
                card.Suit = suit;
                card.Rank = rank;
                cards.Add(card);
            }
        }
    }

    /// <summary>
    /// Shuffles the deck using the Fisher-Yates algorithm.
    /// Randomizes the order of cards and resets the draw index to 0.
    /// </summary>
    public void Shuffle()
    {
        // Iterate from the last card down to the second card.
        for (int i = cards.Count - 1; i > 0; i--)
        {
            // Pick a random index from 0 to i (inclusive).
            int j = Random.Range(0, i + 1);

            // Swap cards[i] and cards[j].
            Card temp = cards[i];
            cards[i] = cards[j];
            cards[j] = temp;
        }

        // After shuffling, restart drawing from the beginning.
        drawIndex = 0;
    }

    /// <summary>
    /// Draws the next card from the deck and increments the draw index.
    /// </summary>
    /// <returns>The next Card, or null if the deck is empty (all cards have been drawn).</returns>
    public Card Draw()
    {
        // If the draw index has reached the end, the deck is empty.
        if (drawIndex >= cards.Count)
        {
            Debug.LogWarning("Empty Deck");
            return null;
        }

        // Retrieve the current card and advance the draw index.
        Card card = cards[drawIndex];
        drawIndex++;
        return card;
    }

    /// <summary>
    /// Returns the number of cards remaining in the deck (not yet drawn).
    /// </summary>
    public int RemainingCount()
    {
        return cards.Count - drawIndex;
    }

    /// <summary>
    /// Prints all cards in the deck to the console for debugging purposes.
    /// Note: This prints the entire deck, including cards that have already been drawn.
    /// </summary>
    public void PrintAllCards()
    {
        string output = "";
        foreach (Card c in cards)
        {
            output += c.ToString() + " ";
        }
        Debug.Log(output);
    }
}
