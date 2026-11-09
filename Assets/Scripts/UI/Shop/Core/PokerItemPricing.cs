using System.Collections.Generic;
using UnityEngine;

namespace PokerShop
{
    /// <summary>
    /// 扑克道具的逐关价格表（Ben 2026-11-08 的经济表）。
    ///
    /// 以前 `ShopItem.price` 只有一个值，五关都是同一个价；实际曲线是
    /// 铜 25/40/55/85/130、银 65/90/135/205/310、金 110/155/230/350/530。
    /// 这里按「当前关卡」把价格查出来，商店显示与扣钱都走同一个入口。
    /// 表里没有的道具仍然用 asset 上的 `price`。
    /// </summary>
    public static class PokerItemPricing
    {
        private static readonly Dictionary<string, int[]> ById = new Dictionary<string, int[]>
        {
            // 铜档
            { "cheat_glove",     new[] { 25,  40,  55,  85,  130 } },
            { "pressure",        new[] { 25,  40,  55,  85,  130 } },
            { "momentum",        new[] { 25,  40,  55,  85,  130 } },
            // 银档
            { "card_swap",       new[] { 65,  90, 135, 205,  310 } },
            { "pair_up",         new[] { 65,  90, 135, 205,  310 } },
            { "cold_read",       new[] { 65,  90, 135, 205,  310 } },
            { "spade_tattoo",    new[] { 65,  90, 135, 205,  310 } },
            { "red_shift",       new[] { 65,  90, 135, 205,  310 } },
            { "flat_bonus",      new[] { 65,  90, 135, 205,  310 } },
            { "bait",            new[] { 65,  90, 135, 205,  310 } },
            // 金档
            { "double_swap",     new[] { 110, 155, 230, 350, 530 } },
            { "precog",          new[] { 110, 155, 230, 350, 530 } },
            { "monochrome",      new[] { 110, 155, 230, 350, 530 } },
            { "queen_of_hearts", new[] { 110, 155, 230, 350, 530 } },
            { "lucky_high",      new[] { 110, 155, 230, 350, 530 } },
            { "draw_six",        new[] { 110, 155, 230, 350, 530 } },
        };

        /// <summary>这件道具在当前关卡的价格。</summary>
        public static int Price(ShopItem item)
        {
            if (item == null)
                return 0;

            if (!ById.TryGetValue(item.id, out int[] curve) || curve == null || curve.Length == 0)
                return item.price;

            int level = Mathf.Clamp(GameData.CurrentLevel, 1, curve.Length);
            return curve[level - 1];
        }
    }
}
