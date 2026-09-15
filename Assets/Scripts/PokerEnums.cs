public enum PokerState
{
    NotStarted,
    PayingBuyIn,
    Dealing,
    PlayerTurn,
    EnemyTurn,
    Showdown,
    PlayerFolded,
    EnemyFolded,
    HandEnded,
}

public enum PlayerAction
{
    Check,
    Raise,
    Fold,
}

public enum EnemyAction
{
    Check,
    Raise,
    Fold,
}

public enum PokerResult
{
    None,
    PlayerWinsByFold,
    EnemyWinsByFold,
    PlayerWinsShowdown,
    EnemyWinsShowdown,
    TieShowdown,
}

public enum Street
{
    PreFlop,
    Flop,
    Turn,
    River,
    Showdown,
}
