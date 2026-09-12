using System.Collections.Generic;
using UnityEngine;


public class DeckTester : MonoBehaviour
{
    void Start()
    {
        Deck deck = new Deck();
        deck.Initialize();
        deck.Shuffle();

        
        List<Card> hand = new List<Card>();
        for (int i = 0; i < 7; i++)
        {
            Card card = deck.Draw();
            if (card != null)
                hand.Add(card);
        }

        // 打印手牌
        string handStr = "7 cards：";
        foreach (Card c in hand)
        {
            handStr += c.ToString() + " ";
        }
        Debug.Log(handStr);

       
        Debug.Log("remains：" + deck.RemainingCount()); // 应为 52 - 7 = 45

        
        HashSet<string> uniqueCards = new HashSet<string>();
        foreach (Card c in hand)
        {
            uniqueCards.Add(c.ToString());
        }
        Debug.Log("card counts：" + uniqueCards.Count); // 应为 7
    }
}