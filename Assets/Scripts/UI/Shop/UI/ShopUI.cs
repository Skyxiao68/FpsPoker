using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PokerShop
{
    public class ShopUI : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] Transform itemContainer, categoryContainer;
        [SerializeField] Transform[] cardSlots;
        [SerializeField] ShopItemView itemPrefab;
        [SerializeField] Button categoryButtonPrefab, closeButton;
        [SerializeField] TMP_Text balanceText, messageText;

        ShopService _shop; string _category;
        readonly List<GameObject> _spawned = new List<GameObject>();

        public void Bind(ShopService shop)
        {
            if (_shop != null)
            {
                _shop.Changed -= Refresh; _shop.ShopClosed -= Hide;
                _shop.PurchaseFailed -= OnFail; _shop.ItemPurchased -= OnBought;
            }
            _shop = shop;
            _shop.Changed += Refresh; _shop.ShopClosed += Hide;
            _shop.PurchaseFailed += OnFail; _shop.ItemPurchased += OnBought;
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => _shop.Close());
        }

        public void Show() { root.SetActive(true); _category = null; Msg(""); BuildCategories(); Refresh(); }
        void Hide() => root.SetActive(false);

        void Update() { if (_shop != null && _shop.IsOpen && Input.GetKeyDown(KeyCode.Escape)) _shop.Close(); }

        void BuildCategories()
        {
            if (categoryContainer == null || categoryButtonPrefab == null) return;
            foreach (Transform c in categoryContainer) Destroy(c.gameObject);
            AddCat("All", null);
            foreach (var c in _shop.CurrentOffers.Select(i => i.category).Distinct()) AddCat(c, c);
        }

        void AddCat(string label, string cat)
        {
            var b = Instantiate(categoryButtonPrefab, categoryContainer);
            b.GetComponentInChildren<TMP_Text>().text = label;
            b.onClick.AddListener(() => { _category = cat; Refresh(); });
        }

        void Refresh()
        {
            if (_shop == null || !root.activeSelf) return;
            balanceText.text = _shop.Balance.ToString("N0");

            foreach (var g in _spawned) Destroy(g);
            _spawned.Clear();

            var items = _shop.CurrentOffers
                .Where(i => _category == null || i.category == _category).ToList();

            if (cardSlots == null || cardSlots.Length == 0)
                Debug.LogWarning("[PokerShop] Card Slots list is empty, using Item Container as the parent.");

            for (int n = 0; n < items.Count; n++)
            {
                var item = items[n];
                Transform parent = (cardSlots != null && n < cardSlots.Length && cardSlots[n] != null)
                    ? cardSlots[n] : itemContainer;

                var v = Instantiate(itemPrefab, parent);
                var rt = (RectTransform)v.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;

                v.Setup(item, _shop.AtLimit(item), _shop.CanAfford(item), () => _shop.TryPurchase(item.id));
                _spawned.Add(v.gameObject);
            }
        }

        void OnBought(ShopItem i) => Msg($"Purchased {i.displayName}!");
        void OnFail(ShopItem i, PurchaseResult r) => Msg(r switch
        {
            PurchaseResult.NotEnoughFunds => "Not enough chips.",
            PurchaseResult.AtLimit => "You can't carry any more of these.",
            PurchaseResult.Closed => "Shop is closed.",
            _ => "Purchase failed."
        });
        void Msg(string s) { if (messageText) messageText.text = s; }
    }
}