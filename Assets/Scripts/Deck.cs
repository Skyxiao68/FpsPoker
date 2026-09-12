using System.Collections.Generic;
using UnityEngine;

public class Deck
{
    private List<Card> cards = new List<Card>();
    private int drawIndex = 0;

    
    public void Initialize()
    {
        cards.Clear();
        drawIndex = 0;

        foreach (Suit suit in System.Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank rank in System.Enum.GetValues(typeof(Rank)))
            {
                Card card = new Card();
                card.Suit = suit;
                card.Rank = rank;
                cards.Add(card);
            }
        }
    }

  
    public void Shuffle()
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Card temp = cards[i];
            cards[i] = cards[j];
            cards[j] = temp;
        }
        drawIndex = 0;
    }


    public Card Draw()
    {
        if (drawIndex >= cards.Count)
        {
            Debug.LogWarning("empty Deck");
            return null;
        }
        Card card = cards[drawIndex];
        drawIndex++;
        return card;
    }

    // 剩余牌数
    public int RemainingCount()
    {
        return cards.Count - drawIndex;
    }

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