using UnityEngine;

/// <summary>
/// Generates random enemy AI configurations based on predefined archetypes.
/// Each archetype represents a different playing style (e.g., aggressive, passive, bluffer).
/// </summary>
public static class EnemyGenerator
{
    /// <summary>
    /// Internal class to store archetype base parameters for different AI personalities.
    /// </summary>
    private class Archetype
    {
        public string name; // Archetype name (e.g., "Shark")
        public float raise; // Base raise tendency
        public float fold; // Base fold tendency
        public float aggression; // Base aggression level
        public float bluff; // Base bluff chance
    }

    /// <summary>
    /// Predefined archetypes covering common poker player styles.
    /// </summary>
    private static readonly Archetype[] Archetypes = new Archetype[]
    {
        // Tight-Aggressive: raises often, rarely folds, aggressive, minimal bluffing
        new Archetype
        {
            name = "Shark",
            raise = 0.8f,
            fold = 0.2f,
            aggression = 0.8f,
            bluff = 0.15f,
        },
        // Loose-Aggressive: raises on anything, frequently bluffs
        new Archetype
        {
            name = "Maniac",
            raise = 0.9f,
            fold = 0.1f,
            aggression = 0.9f,
            bluff = 0.4f,
        },
        // Tight-Passive: conservative, folds often
        new Archetype
        {
            name = "Nit",
            raise = 0.3f,
            fold = 0.7f,
            aggression = 0.3f,
            bluff = 0.0f,
        },
        // Loose-Passive: calls often, rarely raises or folds
        new Archetype
        {
            name = "Caller",
            raise = 0.2f,
            fold = 0.1f,
            aggression = 0.2f,
            bluff = 0.05f,
        },
        // Balanced: moderate in all areas
        new Archetype
        {
            name = "Regular",
            raise = 0.5f,
            fold = 0.4f,
            aggression = 0.5f,
            bluff = 0.1f,
        },
        // Deceiver: constantly bluffs
        new Archetype
        {
            name = "Bluffer",
            raise = 0.6f,
            fold = 0.3f,
            aggression = 0.6f,
            bluff = 0.6f,
        },
        // Extreme Passive: never raises, folds very easily
        new Archetype
        {
            name = "Rock",
            raise = 0.15f,
            fold = 0.8f,
            aggression = 0.2f,
            bluff = 0.0f,
        },
    };

    /// <summary>
    /// Generates a random enemy AI by selecting a random archetype and adding slight parameter variation.
    /// </summary>
    /// <returns>An EnemyAIParameters object with randomized values.</returns>
    public static EnemyAIParameters GenerateRandom()
    {
        // Pick a random archetype from the predefined list.
        Archetype a = Archetypes[Random.Range(0, Archetypes.Length)];

        EnemyAIParameters p = new EnemyAIParameters();
        p.enemyName = a.name + " " + GetRandomSuffix();

        // Add ±0.1 random variation to each archetype parameter for diversity.
        p.raiseTendency = Mathf.Clamp01(a.raise + Random.Range(-0.1f, 0.1f));
        p.foldTendency = Mathf.Clamp01(a.fold + Random.Range(-0.1f, 0.1f));
        p.aggression = Mathf.Clamp01(a.aggression + Random.Range(-0.1f, 0.1f));
        p.bluffChance = Mathf.Clamp01(a.bluff + Random.Range(-0.05f, 0.05f));

        // Randomize combat style for the FPS component.
        string[] styles = { "Melee", "Ranged", "Dodge" };
        p.combatStyle = styles[Random.Range(0, styles.Length)];

        return p;
    }

    /// <summary>
    /// Generates a random enemy name without full parameters.
    /// </summary>
    /// <returns>A randomly generated enemy name string.</returns>
    public static string GenerateRandomName()
    {
        Archetype a = Archetypes[Random.Range(0, Archetypes.Length)];
        return a.name + " " + GetRandomSuffix();
    }

    /// <summary>
    /// Returns a random suffix (color) to append to the enemy name.
    /// </summary>
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

    /// <summary>
    /// Returns a formatted string describing the enemy's AI parameters (for debug logging).
    /// </summary>
    /// <param name="p">The enemy AI parameters to describe.</param>
    /// <returns>A formatted description string.</returns>
    public static string Describe(EnemyAIParameters p)
    {
        return $"{p.enemyName} | Raise:{p.raiseTendency:F2} Fold:{p.foldTendency:F2} "
            + $"Aggr:{p.aggression:F2} Bluff:{p.bluffChance:F2} | {p.combatStyle}";
    }
}
