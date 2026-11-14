/// <summary>
/// Result of a resolved FPS duel. Reported back to
/// GameFlowManager, which relays it to PokerGameManager on the
/// next poker scene load.
/// </summary>
public enum CombatWinner
{
    None,
    Player,
    Enemy
}