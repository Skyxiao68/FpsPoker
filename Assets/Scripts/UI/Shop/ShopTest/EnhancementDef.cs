using UnityEngine;

public enum EnhancementType { ExtraStartingCard, CardSwap, SuitBoost, Insurance }

[CreateAssetMenu(menuName = "PokerShopDemo/Enhancement")]
public class EnhancementDef : ScriptableObject
{
    public EnhancementType type;
    public int amount = 1;      // e.g. 1 extra card, 25 = 25% insurance
    public string suit = "";    // only used by SuitBoost
}