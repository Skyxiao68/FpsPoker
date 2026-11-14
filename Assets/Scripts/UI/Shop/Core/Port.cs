using System;
using System.Collections.Generic;
namespace PokerShop
{
    public interface ICurrencyProvider
    {
        int GetBalance(); bool TrySpend(int amount); void Refund(int amount);
        event Action BalanceChanged;
    }
    public interface IInventoryProvider
    {
        int GetCount(ShopItem item);
        bool Grant(ShopItem item);    // read item.payload and apply it
        void Revoke(ShopItem item);
    }
    public interface IGameStateControl
    {
        bool CanOpenShop();
        void OnShopOpened(ShopContext context);
        void OnShopClosed();
    }
    public interface IOfferProvider
    {
        IEnumerable<ShopItem> GetOffers(ShopCatalog catalog, ShopContext context);
    }
    public class ShopContext { public int Round; public string Tag = ""; public object Custom; }
}