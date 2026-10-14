using System.Collections.Generic;
using PokerShop;
using UnityEngine;

namespace PokerShopBridge
{
    /// <summary>
    /// 按稀有度掷骰子再抽物品的 OfferProvider。
    /// 每个物品根据自己的 tier 字段（"bronze"/"silver"/"gold"）进入对应池子。
    /// 未设置 tier 的物品默认归为 bronze。
    /// </summary>
    public class WeightedRandomOfferProvider : IOfferProvider
    {
        private readonly int count;
        private readonly int visitNumber;
        private readonly System.Func<ShopItem, bool> isAtLimit;

        public WeightedRandomOfferProvider(
            int count,
            int visitNumber,
            System.Func<ShopItem, bool> isAtLimit = null
        )
        {
            this.count = count;
            this.visitNumber = Mathf.Max(1, visitNumber);
            this.isAtLimit = isAtLimit;
        }

        public IEnumerable<ShopItem> GetOffers(ShopCatalog catalog, ShopContext ctx)
        {
            // ---- 1. 按 tier 分组 ----
            var byTier = new Dictionary<string, List<ShopItem>>();
            byTier["bronze"] = new List<ShopItem>();
            byTier["silver"] = new List<ShopItem>();
            byTier["gold"] = new List<ShopItem>();

            foreach (var item in catalog.Visible)
            {
                // ★ 跳过已达上限的（已买过 maxOwned 次）
                if (isAtLimit != null && isAtLimit(item))
                    continue;

                string tier = string.IsNullOrEmpty(item.tier) ? "bronze" : item.tier.ToLower();
                if (!byTier.ContainsKey(tier))
                    byTier[tier] = new List<ShopItem>();
                byTier[tier].Add(item);
            }

            // ---- 2. 计算权重 ----
            ShopRarityWeights.GetWeights(
                visitNumber,
                out float bronzeW,
                out float silverW,
                out float goldW
            );

            // 某个池子为空 → 它的权重归零
            if (byTier["bronze"].Count == 0)
                bronzeW = 0f;
            if (byTier["silver"].Count == 0)
                silverW = 0f;
            if (byTier["gold"].Count == 0)
                goldW = 0f;

            float totalW = bronzeW + silverW + goldW;

            Debug.Log(
                $"[Shop] 访问 #{visitNumber} 概率：{ShopRarityWeights.Describe(visitNumber)}"
            );

            // ---- 3. 兜底：所有权重为 0 就随机抽 ----
            if (totalW <= 0)
            {
                var all = new List<ShopItem>(catalog.Visible);
                Shuffle(all);
                int fallbackN = Mathf.Min(count, all.Count);
                for (int i = 0; i < fallbackN; i++)
                    yield return all[i];
                yield break;
            }

            // ---- 4. 逐个 slot 抽物品，避免重复 ----
            var picked = new List<ShopItem>();
            var used = new HashSet<ShopItem>();

            int attempts = 0;
            const int maxAttempts = 100;

            while (picked.Count < count && attempts < maxAttempts)
            {
                attempts++;

                // 掷稀有度
                float roll = UnityEngine.Random.Range(0f, totalW);
                string chosenTier;
                if (roll < bronzeW)
                    chosenTier = "bronze";
                else if (roll < bronzeW + silverW)
                    chosenTier = "silver";
                else
                    chosenTier = "gold";

                var pool = byTier[chosenTier];

                // 空池或全部用光 → 回退到别的池
                if (pool == null || pool.Count == 0)
                {
                    pool = FallbackPool(byTier, chosenTier);
                    if (pool == null || pool.Count == 0)
                        break;
                }

                // 从池子里挑一个未被用过的
                var available = new List<ShopItem>();
                foreach (var it in pool)
                    if (!used.Contains(it))
                        available.Add(it);

                // 都拿过了 → 允许重复
                if (available.Count == 0)
                    available = pool;

                var pick = available[UnityEngine.Random.Range(0, available.Count)];
                picked.Add(pick);
                used.Add(pick);
            }

            foreach (var item in picked)
                yield return item;
        }

        /// <summary>如果目标 tier 池为空，按 silver → gold → bronze 顺序找非空池。</summary>
        private List<ShopItem> FallbackPool(
            Dictionary<string, List<ShopItem>> byTier,
            string preferred
        )
        {
            if (preferred == "gold")
            {
                if (byTier["silver"].Count > 0)
                    return byTier["silver"];
                if (byTier["bronze"].Count > 0)
                    return byTier["bronze"];
            }
            else if (preferred == "silver")
            {
                if (byTier["gold"].Count > 0)
                    return byTier["gold"];
                if (byTier["bronze"].Count > 0)
                    return byTier["bronze"];
            }
            else // bronze
            {
                if (byTier["silver"].Count > 0)
                    return byTier["silver"];
                if (byTier["gold"].Count > 0)
                    return byTier["gold"];
            }
            return null;
        }

        private void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }
    }
}
