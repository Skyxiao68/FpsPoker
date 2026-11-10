using UnityEngine;

namespace PokerShop
{
    /// <summary>
    /// 商店的「买完要留多少钱」护栏（Ben 2026-11-10）。
    ///
    /// 历史 bug：只按 `GameFlowManager.CurrentBuyIn` 判，而它用的是**刚打完那一关**的
    /// 起始敌人筹码（L1 的 60 → 买入 9）。可下一关的买入会涨（L2 的 90 → 13），
    /// 于是玩家 111 筹码买 110 金卡、剩 1 看似"不够 9"会拦；但如果剩 10~12，
    /// 护栏放行，进下一关却付不起 13，直接 GG。
    ///
    /// 所以这里取三者最大：当前买入、即将这一关的买入、再下一关的买入。
    /// 只读取 GameFlowManager 的公开成员（`CurrentLevel`、`CurrentBuyIn`、静态
    /// `EnemyChipsForLevel`），不修改他的脚本。
    /// </summary>
    public static class PokerBuyInReserve
    {
        /// <summary>买入 = 本关起始敌人筹码 ÷ 7（与 GameFlowManager 里的算法保持一致）。</summary>
        const int BuyInDivisor = 7;

        /// <summary>买入下限，和 GameFlowManager.MinBuyIn 对齐。</summary>
        const int MinBuyIn = 5;

        /// <summary>某一关的买入。</summary>
        public static int BuyInForLevel(int level)
        {
            int enemy = GameFlowManager.EnemyChipsForLevel(Mathf.Max(1, level));
            return Mathf.Max(MinBuyIn, Mathf.RoundToInt(enemy / (float)BuyInDivisor));
        }

        /// <summary>此刻买东西必须留下的筹码。</summary>
        public static int Required()
        {
            var flow = GameFlowManager.Instance;
            if (flow == null) return MinBuyIn;

            int current = flow.CurrentBuyIn;
            int thisLevel = BuyInForLevel(flow.CurrentLevel);
            int nextLevel = BuyInForLevel(flow.CurrentLevel + 1);

            return Mathf.Max(current, Mathf.Max(thisLevel, nextLevel));
        }
    }
}
