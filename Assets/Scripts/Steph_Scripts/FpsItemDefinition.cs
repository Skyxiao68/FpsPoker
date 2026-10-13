using UnityEngine;

[CreateAssetMenu(fileName = "FpsItem", menuName = "FpsItem/Definition")]
public class FpsItemDefinition : ScriptableObject
{
    [SerializeField] private string fpsItemId;
    [SerializeField] private ItemId itemId;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private CombatStats bonusStats;

    public string FpsItemId => fpsItemId;
    public ItemId ItemId => itemId;
    public string DisplayName => displayName;
    public string Description => description;
    public CombatStats BonusStats => bonusStats;
}
