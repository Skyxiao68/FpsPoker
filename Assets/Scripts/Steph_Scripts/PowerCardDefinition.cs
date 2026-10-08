using UnityEngine;

[CreateAssetMenu(fileName = "PowerCard", menuName = "Power Cards/FPS Power Card")]
public class PowerCardDefinition : ScriptableObject
{
    [SerializeField] private string cardId;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private CombatStats bonusStats;

    public string CardId => cardId;
    public string DisplayName => displayName;
    public string Description => description;
    public CombatStats BonusStats => bonusStats;
}
