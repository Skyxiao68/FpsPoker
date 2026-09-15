[System.Serializable]
public class CombatSetup
{
    public int pot;                              // 底池总额
    public Dictionary<string, float> playerStats; // 玩家属性加成
    public Dictionary<string, float> enemyStats;  // 敌人属性加成
    public string enemyName;                     // 敌人名字（供 FPS 显示）
    public string enemyCombatStyle;              // 敌人战斗风格（Melee / Ranged / Dodge）
    public PokerResult pokerResult;              // 摊牌结果（玩家赢/敌人赢/平局）
}