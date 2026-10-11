using UnityEngine;
namespace PokerShop
{
    [CreateAssetMenu(menuName = "PokerShop/Item")]
    public class ShopItem : ScriptableObject
    {
        public string id;                 // unique, never change later
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;
        public int price;
        public string category = "Weapons";   // Weapons, Buffs...
        public string tier = "";
        public int maxOwned = 1;          // 1 = one-time, 0 = unlimited, N = stack to N
        public bool hidden;
        public ScriptableObject payload;  // opaque: the other dev's weapon/buff data
    }
}