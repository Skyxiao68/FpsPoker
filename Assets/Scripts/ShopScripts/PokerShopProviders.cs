using System;
using System.Collections.Generic;
using UnityEngine;
using PokerShop;

namespace PokerShopBridge
{
    // =========================================================
    // 1. 筹码：直接用 GameFlowManager.PlayerChips
    // =========================================================
    public class PokerCurrencyProvider : ICurrencyProvider
    {
        public event Action BalanceChanged;

        public int GetBalance()
        {
            return GameFlowManager.Instance != null
                ? GameFlowManager.Instance.PlayerChips : 0;
        }

        public bool TrySpend(int amount)
        {
            if (GameFlowManager.Instance == null) return false;
            if (GameFlowManager.Instance.PlayerChips < amount) return false;
            GameFlowManager.Instance.PlayerChips -= amount;
            BalanceChanged?.Invoke();
            return true;
        }

        public void Refund(int amount)
        {
            if (GameFlowManager.Instance == null) return;
            GameFlowManager.Instance.PlayerChips += amount;
            BalanceChanged?.Invoke();
        }
    }

    // =========================================================
    // 2. 背包：ShopItem.id → ItemId 映射
    // =========================================================
    public class PokerInventoryProvider : IInventoryProvider
    {
        private static readonly Dictionary<string, ItemId> idMap =
            new Dictionary<string, ItemId>
        {
            { "card_swap",       ItemId.CardSwapTicket },
            { "double_swap",     ItemId.DoubleSwap },
            { "cheat_glove",     ItemId.CheatGlove },
            { "cold_read",       ItemId.ColdRead },
            { "queen_of_hearts", ItemId.QueenOfHearts },
            { "spade_tattoo",    ItemId.SpadeTattoo },
            { "red_shift",       ItemId.RedShift },
            { "flat_bonus",      ItemId.FlatBonus },
            { "lucky_high",      ItemId.LuckyHigh },
            { "pressure",        ItemId.Pressure },
            { "bait",            ItemId.Bait },
            { "draw_six",        ItemId.DrawSix },
            { "precog",          ItemId.Precog },
            { "monochrome",      ItemId.Monochrome },
            { "pair_up",         ItemId.PairUp },
            { "momentum",        ItemId.Momentum },
            { "four_flush",      ItemId.FourFlush },
            { "chop_breaker",    ItemId.ChopBreaker },
            { "suit_stack",      ItemId.SuitStack },
            { "deadeye",         ItemId.Deadeye },
            { "quickstep",       ItemId.Quickstep },
            { "iron_heart",      ItemId.IronHeart },
            { "headhunter",      ItemId.Headhunter },
            { "lifesteal",       ItemId.LifeSteal },
            { "marked",          ItemId.Marked },
            { "last_stand",      ItemId.LastStand },
            { "golden_bullet",   ItemId.GoldenBullet },
        };

        // 跨场景保留的购买计数
        private static readonly Dictionary<string, int> purchaseCounts =
            new Dictionary<string, int>();

        public int GetCount(ShopItem item)
        {
            return purchaseCounts.TryGetValue(item.id, out var n) ? n : 0;
        }

        public bool Grant(ShopItem item)
        {
            if (!idMap.TryGetValue(item.id, out var gameId))
            {
                Debug.LogWarning($"[Shop] '{item.id}' 没有映射到 ItemId");
                return false;
            }
            if (PlayerInventory.Instance == null)
            {
                Debug.LogWarning("[Shop] PlayerInventory.Instance 为 null");
                return false;
            }
            if (!InventorySlots.HasSpace(PlayerInventory.Instance))
            {
                Debug.LogWarning("[Shop] Shared inventory is full.");
                return false;
            }

            PlayerInventory.Instance.Add(gameId);
            purchaseCounts[item.id] = GetCount(item) + 1;

            // 双换比单换强，两件同时持有只会按高的算（见 PokerItemTraits）。
            // 买到双换就把单换退回，别让玩家白占一格背包。
            if (gameId == ItemId.DoubleSwap && PlayerInventory.Instance.Has(ItemId.CardSwapTicket))
            {
                PlayerInventory.Instance.Remove(ItemId.CardSwapTicket);
                purchaseCounts["card_swap"] = 0;
                Debug.Log("[Shop] Double Swap supersedes Card Swap — Card Swap returned.");
            }

            // 同理：冷读（看 2 张）比手套（看 1 张）强，买到就把手套退回。
            if (gameId == ItemId.ColdRead && PlayerInventory.Instance.Has(ItemId.CheatGlove))
            {
                PlayerInventory.Instance.Remove(ItemId.CheatGlove);
                purchaseCounts["cheat_glove"] = 0;
                Debug.Log("[Shop] Cold Read supersedes Cheat Glove — Cheat Glove returned.");
            }

            Debug.Log($"[Shop] Grant {item.id} → ItemId.{gameId}, 总计 {purchaseCounts[item.id]}");
            return true;
        }

        public void Revoke(ShopItem item)
        {
            if (purchaseCounts.ContainsKey(item.id))
                purchaseCounts[item.id] = Mathf.Max(0, purchaseCounts[item.id] - 1);
            if (idMap.TryGetValue(item.id, out var gameId))
                PlayerInventory.Instance?.Remove(gameId);
        }
    }

    // =========================================================
    // 3. 状态控制
    // =========================================================
    public class PokerGameStateControl : IGameStateControl
    {
        public bool CanOpenShop() => true;

        public void OnShopOpened(ShopContext ctx)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
        }

        public void OnShopClosed()
        {
        }
    }

    // =========================================================
    // 4. 随机 offer
    // =========================================================
    public class RandomOfferProvider : IOfferProvider
    {
        private readonly int count;
        public RandomOfferProvider(int count) { this.count = count; }

        public IEnumerable<ShopItem> GetOffers(ShopCatalog catalog, ShopContext ctx)
        {
            var pool = new List<ShopItem>(catalog.Visible);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
            }
            int n = Mathf.Min(count, pool.Count);
            for (int i = 0; i < n; i++)
                yield return pool[i];
        }
    }
}
