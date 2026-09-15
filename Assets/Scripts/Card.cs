// Represents the four suits in a standard playing card deck.
public enum Suit
{
    Spades, // ♠
    Hearts, // ♥
    Clubs, // ♣
    Diamonds, // ♦
}

/// <summary>
/// Represents the rank of a playing card (2 through Ace).
/// The enum values correspond to poker values: 2-10, J=11, Q=12, K=13, A=14.
/// </summary>
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
    Ace = 14,
}

/// <summary>
/// Represents a single playing card with a suit and rank.
/// </summary>
[System.Serializable]
public class Card
{
    public Suit Suit; // The suit of the card (Spades, Hearts, Clubs, Diamonds)
    public Rank Rank; // The rank of the card (Two through Ace)

    /// <summary>
    /// Returns the card's attribute value (used for FPS combat stats).
    /// Number cards (2-10) return their face value, Ace returns 11, Face cards return 10.
    /// </summary>
    public int GetAttributeValue()
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

    /// <summary>
    /// Returns the card's poker ranking value where Ace=14, King=13, etc.
    /// Used for hand evaluation and comparison during showdown.
    /// </summary>
    public int GetPokerValue()
    {
        return (int)Rank;
    }

    /// <summary>
    /// Returns a string representation of the card (e.g., "A♠", "10♥", "Q♦").
    /// Maps rank to display string and suit to Unicode suit character.
    /// </summary>
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
