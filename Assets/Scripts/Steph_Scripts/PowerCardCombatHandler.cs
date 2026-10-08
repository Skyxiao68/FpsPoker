using System.Collections.Generic;


public static class PowerCardCombatHandler
{
    public static CombatStats Apply(
        CombatStats baseStats,
        IReadOnlyList<PowerCardDefinition> cards)
    {
        if (cards == null)
            return baseStats;


        int count = cards.Count < GameFlowManager.MaxPowerCards
            ? cards.Count
            : GameFlowManager.MaxPowerCards;

        for (int i = 0; i < count; i++)
        {
            PowerCardDefinition card = cards[i];
            if (card == null)
                continue;

            CombatStats bonus = card.BonusStats;
            baseStats.attack += bonus.attack;
            baseStats.health += bonus.health;
            baseStats.fireRate += bonus.fireRate;
            baseStats.moveSpeed += bonus.moveSpeed;
        }

        return baseStats;
    }
}
