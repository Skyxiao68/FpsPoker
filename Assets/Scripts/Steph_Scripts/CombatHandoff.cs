/// <summary>
/// Everything the FPS scene needs to set up a duel, packaged by
/// PokerGameManager at showdown and carried across the scene load
/// by GameFlowManager.
/// </summary>
[System.Serializable]
public struct CombatHandoff
{
    public CombatStats playerStats;
    public CombatStats enemyStats;
    public int pot;
    public string enemyName;

    /// <summary>
    /// Matches EnemyAIParameters.combatStyle ("Melee", "Ranged",
    /// "Dodge") from EnemyGenerator - not the FPSPersonality enum
    /// names. CombatSceneController looks up an enemy prefab by
    /// this string.
    /// </summary>
    public string enemyCombatStyle;
}