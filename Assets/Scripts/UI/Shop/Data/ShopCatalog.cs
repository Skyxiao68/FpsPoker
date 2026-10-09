using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PokerShop
{
    [CreateAssetMenu(menuName = "PokerShop/Catalog")]
    public class ShopCatalog : ScriptableObject
    {
        public List<ShopItem> items = new List<ShopItem>();

        public IEnumerable<ShopItem> Visible => items.Where(i => i != null && !i.hidden);

        public ShopItem Find(string id) => items.FirstOrDefault(i => i != null && i.id == id);
    }
}