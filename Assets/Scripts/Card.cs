using UnityEngine;

public enum Suit
{
    Spades,
    Hearts,
    Clubs,
    Diamonds, 
}

public enum Rank
{
    Two = 2, 
    Three = 3, 
    Four = 4, 
    Five = 5,
    Six = 6, 
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14
}

[System.Serializable]
public class Card
{
    public Suit Suit;
    public Rank Rank;

    public int GetAttirbuteValue()
    {
        if (Rank >= Rank.Two && Rank <= Rank.Ten)
        {
            return (int)Rank;
        }
        else if (Rank == Rank.Ace)
        {
            return 11; 
        }
        else
        {
            return 10; 
        }
    }

    public int GetPokerValue()
    {
        return (int)Rank; 
    }

    public override string ToString()
    {
        string rankStr = " "; 
        switch (Rank)
        {
            case Rank.Two:
                rankStr = "2";
                break;
            case Rank.Three:
                rankStr = "3";
                break;
            case Rank.Four:
                rankStr = "4";
                break;
            case Rank.Five:
                rankStr = "5";
                break;
            case Rank.Six:
                rankStr = "6";
                break;
            case Rank.Seven:
                rankStr = "7";
                break;
            case Rank.Eight:
                rankStr = "8";
                break;
            case Rank.Nine:
                rankStr = "9";
                break;
            case Rank.Ten:
                rankStr = "10";
                break;
            case Rank.Jack:
                rankStr = "J";
                break;
            case Rank.Queen:
                rankStr = "Q";
                break;
            case Rank.King:
                rankStr = "K";
                break;
            case Rank.Ace:
                rankStr = "A";
                break;
        }

        string suitStr = "";
        switch (Suit)
        {
            case Suit.Spades:
                suitStr = "♠";
                break;
            case Suit.Hearts:
                suitStr = "♥";
                break;
            case Suit.Clubs:
                suitStr = "♣";
                break;
            case Suit.Diamonds:
                suitStr = "♦";
                break;
        }

        return rankStr + suitStr;
    }
}


