using System;
using System.Collections.Generic;
using System.Linq;
using PokerShop;
using UnityEngine;

namespace PokerShopDemo
{
    // ---------- Fake game side (touches nothing in the real game) ----------

    class FakeCurrency : ICurrencyProvider
    {
        int _chips = 1000;
        public event Action BalanceChanged;
        public int GetBalance() => _chips;
        public bool TrySpend(int a)
        {
            if (_chips < a) return false;
            _chips -= a; BalanceChanged?.Invoke(); return true;
        }
        public void Refund(int a) { _chips += a; BalanceChanged?.Invoke(); }
    }

    class FakeInventory : IInventoryProvider
    {
        readonly Dictionary<string, int> _counts = new Dictionary<string, int>();
        public int GetCount(ShopItem i) => _counts.TryGetValue(i.id, out var n) ? n : 0;

        public bool Grant(ShopItem i)
        {
            _counts[i.id] = GetCount(i) + 1;
            var def = i.payload as EnhancementDef;
            Debug.Log(def != null
                ? $"Bought: {i.displayName} -> {def.type}, amount {def.amount}, suit '{def.suit}'"
                : $"Bought: {i.displayName} (no payload)");
            return true;
        }

        public void Revoke(ShopItem i) { _counts[i.id] = Math.Max(0, GetCount(i) - 1); }
    }

    class FakeState : IGameStateControl
    {
        public bool CanOpenShop() => true;
        public void OnShopOpened(ShopContext c) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        public void OnShopClosed() { }
    }

    // ---------- Picks a few random items each visit ----------

    class RotatingStock : IOfferProvider
    {
        readonly int _count;
        public RotatingStock(int count = 3) { _count = count; }

        public IEnumerable<ShopItem> GetOffers(ShopCatalog catalog, ShopContext ctx) =>
            catalog.Visible.OrderBy(_ => UnityEngine.Random.value).Take(_count);
    }

    // ---------- The test harness: shop pops up before every round ----------

    public class ShopTestHarness : MonoBehaviour
    {
        [SerializeField] ShopCatalog catalog;
        [SerializeField] ShopUI shopUiPrefab;
        [SerializeField] int goodsPerVisit = 3;
        [SerializeField] float fakeRoundSeconds = 3f;

        int _round = 0;

        void Awake()
        {
            Shop.Init(catalog, new FakeCurrency(), new FakeInventory(), new FakeState(),
                      shopUiPrefab, new RotatingStock(goodsPerVisit));
            Shop.Service.ShopClosed += StartRound;
        }

        void Start() { OpenShopForNextRound(); }

        // Your real round manager does this at the start of each round.
        void OpenShopForNextRound()
        {
            _round++;
            Shop.Open(new ShopContext { Round = _round, Tag = "before" });
        }

        void StartRound()
        {
            Debug.Log($"Round {_round} starts. (play the round here)");
            Invoke(nameof(OpenShopForNextRound), fakeRoundSeconds);  // pretend the round lasts a few seconds
        }
    }
}