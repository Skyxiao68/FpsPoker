using UnityEngine;
namespace PokerShop
{
    public static class Shop
    {
        public static ShopService Service { get; private set; }
        static ShopUI _prefab, _ui;

        public static void Init(ShopCatalog cat, ICurrencyProvider cur, IInventoryProvider inv,
                                IGameStateControl st, ShopUI uiPrefab, IOfferProvider offers = null)
        {
            Service = new ShopService(cat, cur, inv, st, offers);
            _prefab = uiPrefab;
            if (_ui != null) Object.Destroy(_ui.gameObject);
            _ui = null;
        }

        public static bool Open(ShopContext ctx = null)
        {
            if (Service == null) { Debug.LogError("Call Shop.Init first"); return false; }
            if (!Service.Open(ctx)) return false;
            if (_ui == null) _ui = Object.Instantiate(_prefab);
            _ui.Bind(Service); _ui.Show();
            return true;
        }
        public static void Close() => Service?.Close();
        public static bool IsOpen => Service != null && Service.IsOpen;
    }
}