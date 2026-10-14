using System;
using System.Collections.Generic;
using System.Linq;
namespace PokerShop
{
    public enum PurchaseResult { Success, UnknownItem, NotOffered, AtLimit, NotEnoughFunds, GrantFailed, Closed }

    sealed class AllVisibleOffers : IOfferProvider
    {
        public IEnumerable<ShopItem> GetOffers(ShopCatalog c, ShopContext x) => c.Visible;
    }

    public class ShopService
    {
        public readonly ShopCatalog Catalog;
        readonly ICurrencyProvider _currency; readonly IInventoryProvider _inventory;
        readonly IGameStateControl _state; readonly IOfferProvider _offers;

        public bool IsOpen { get; private set; }
        public ShopContext Context { get; private set; }
        public IReadOnlyList<ShopItem> CurrentOffers { get; private set; } = new List<ShopItem>();

        public event Action<ShopContext> ShopOpened;
        public event Action ShopClosed;
        public event Action<ShopItem> ItemPurchased;
        public event Action<ShopItem, PurchaseResult> PurchaseFailed;
        public event Action Changed;

        public ShopService(ShopCatalog cat, ICurrencyProvider cur, IInventoryProvider inv,
                           IGameStateControl st, IOfferProvider offers = null)
        {
            Catalog = cat; _currency = cur; _inventory = inv; _state = st;
            _offers = offers ?? new AllVisibleOffers();
            _currency.BalanceChanged += () => Changed?.Invoke();
        }

        public int Balance => _currency.GetBalance();
        public bool AtLimit(ShopItem i) => i.maxOwned > 0 && _inventory.GetCount(i) >= i.maxOwned;
        public bool CanAfford(ShopItem i) => Balance >= i.price;

        public bool Open(ShopContext ctx = null)
        {
            if (IsOpen || !_state.CanOpenShop()) return false;
            Context = ctx ?? new ShopContext();
            CurrentOffers = _offers.GetOffers(Catalog, Context).ToList(); // rolled once per visit
            IsOpen = true;
            _state.OnShopOpened(Context);
            ShopOpened?.Invoke(Context);
            return true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; _state.OnShopClosed(); ShopClosed?.Invoke();
        }

        public PurchaseResult TryPurchase(string itemId)
        {
            var item = Catalog.Find(itemId);
            if (item == null) return Fail(null, PurchaseResult.UnknownItem);
            if (!IsOpen) return Fail(item, PurchaseResult.Closed);
            if (!CurrentOffers.Contains(item)) return Fail(item, PurchaseResult.NotOffered);
            if (AtLimit(item)) return Fail(item, PurchaseResult.AtLimit);
            if (!_currency.TrySpend(item.price)) return Fail(item, PurchaseResult.NotEnoughFunds);
            if (!_inventory.Grant(item)) { _currency.Refund(item.price); return Fail(item, PurchaseResult.GrantFailed); }
            ItemPurchased?.Invoke(item); Changed?.Invoke();
            return PurchaseResult.Success;
        }

        PurchaseResult Fail(ShopItem i, PurchaseResult r) { PurchaseFailed?.Invoke(i, r); return r; }
    }
}