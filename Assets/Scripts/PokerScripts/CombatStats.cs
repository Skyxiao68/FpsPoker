[System.Serializable]
public struct CombatStats
{
    public float attack; // 攻击力
    public float health; // 最大生命
    public float fireRate; // 攻速
    public float moveSpeed; // 移速

    public override string ToString()
    {
        return $"ATK:{attack} HP:{health} ROF:{fireRate} SPD:{moveSpeed}";
    }
}
