// Defines all possible states of the poker game.
// Used by PokerGameManager to control game flow and by PokerUI to refresh the display.
public enum PokerState
{
    NotStarted, // Game has not started yet
    PayingBuyIn, // Both players are paying the buy-in into the pot
    Dealing, // Community cards are being dealt (animation delay)
    PlayerTurn, // Waiting for the player to act
    EnemyTurn, // Waiting for the enemy AI to act
    Showdown, // Both hands are revealed and compared
    PlayerFolded, // Player folded; enemy wins the pot
    EnemyFolded, // Enemy folded; player wins the pot
    HandEnded, // The hand is over (via showdown or fold)
}

// Actions available to the player during betting rounds.
public enum PlayerAction
{
    Check, // Player checks (no bet)
    Raise, // Player raises the bet
    Fold, // Player folds and forfeits the pot
}

// Actions available to the enemy AI during betting rounds.
public enum EnemyAction
{
    Check, // Enemy checks (no bet)
    Raise, // Enemy raises the bet
    Fold, // Enemy folds and forfeits the pot
}

// Represents the outcome of a poker hand.
public enum PokerResult
{
    None, // No result yet
    PlayerWinsByFold, // Player wins because enemy folded
    EnemyWinsByFold, // Enemy wins because player folded
    PlayerWinsShowdown, // Player wins at showdown
    EnemyWinsShowdown, // Enemy wins at showdown
    TieShowdown, // Both players tie at showdown (split pot)
}

// Represents the betting streets in Texas Hold'em poker.
// PreFlop: Before community cards, Flop: 3 cards, Turn: 4th card, River: 5th card, Showdown: final comparison.
public enum Street
{
    PreFlop, // Before any community cards are dealt
    Flop, // First 3 community cards
    Turn, // 4th community card
    River, // 5th community card
    Showdown, // Final hand comparison
}
