using UnityEngine;

public static class EnemyGenerator
{
    // 原型基础参数
    private class Archetype
    {
        public string name;
        public float raise;
        public float fold;
        public float aggression;
        public float bluff;
    }

    private static readonly Archetype[] Archetypes = new Archetype[]
    {
        // 紧凶：加注多，弃牌少，激进，少量诈唬
        new Archetype
        {
            name = "Shark",
            raise = 0.8f,
            fold = 0.2f,
            aggression = 0.8f,
            bluff = 0.15f,
        },
        // 松凶：什么都加，爱诈唬
        new Archetype
        {
            name = "Maniac",
            raise = 0.9f,
            fold = 0.1f,
            aggression = 0.9f,
            bluff = 0.4f,
        },
        // 紧弱：保守，容易弃牌
        new Archetype
        {
            name = "Nit",
            raise = 0.3f,
            fold = 0.7f,
            aggression = 0.3f,
            bluff = 0.0f,
        },
        // 松弱：爱跟不爱加，不爱弃
        new Archetype
        {
            name = "Caller",
            raise = 0.2f,
            fold = 0.1f,
            aggression = 0.2f,
            bluff = 0.05f,
        },
        // 均衡
        new Archetype
        {
            name = "Regular",
            raise = 0.5f,
            fold = 0.4f,
            aggression = 0.5f,
            bluff = 0.1f,
        },
        // 疯狂诈唬型
        new Archetype
        {
            name = "Bluffer",
            raise = 0.6f,
            fold = 0.3f,
            aggression = 0.6f,
            bluff = 0.6f,
        },
        // 岩石型：极度保守
        new Archetype
        {
            name = "Rock",
            raise = 0.15f,
            fold = 0.8f,
            aggression = 0.2f,
            bluff = 0.0f,
        },
    };

    // 随机生成一个敌人
    public static EnemyAIParameters GenerateRandom()
    {
        Archetype a = Archetypes[Random.Range(0, Archetypes.Length)];

        EnemyAIParameters p = new EnemyAIParameters();
        p.enemyName = a.name + " " + GetRandomSuffix();

        // 在原型基础上加 ±0.1 的随机浮动
        p.raiseTendency = Mathf.Clamp01(a.raise + Random.Range(-0.1f, 0.1f));
        p.foldTendency = Mathf.Clamp01(a.fold + Random.Range(-0.1f, 0.1f));
        p.aggression = Mathf.Clamp01(a.aggression + Random.Range(-0.1f, 0.1f));
        p.bluffChance = Mathf.Clamp01(a.bluff + Random.Range(-0.05f, 0.05f));

        // 战斗风格随机
        string[] styles = { "Melee", "Ranged", "Dodge" };
        p.combatStyle = styles[Random.Range(0, styles.Length)];

        return p;
    }

    // 只生成名字（如果只需要名字）
    public static string GenerateRandomName()
    {
        Archetype a = Archetypes[Random.Range(0, Archetypes.Length)];
        return a.name + " " + GetRandomSuffix();
    }

    private static string GetRandomSuffix()
    {
        string[] suffixes =
        {
            "Red",
            "Blue",
            "Green",
            "Black",
            "White",
            "Gold",
            "Silver",
            "Iron",
            "Shadow",
            "Ghost",
        };
        return suffixes[Random.Range(0, suffixes.Length)];
    }

    // 打印当前参数（调试用）
    public static string Describe(EnemyAIParameters p)
    {
        return $"{p.enemyName} | Raise:{p.raiseTendency:F2} Fold:{p.foldTendency:F2} "
            + $"Aggr:{p.aggression:F2} Bluff:{p.bluffChance:F2} | {p.combatStyle}";
    }
}
