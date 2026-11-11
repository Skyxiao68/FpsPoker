using UnityEngine;

namespace PokerShop
{
    /// <summary>
    /// 商店的「买完要留多少钱」护栏（Ben 2026-11-10）。
    ///
    /// 历史 bug：只按 `GameFlowManager.CurrentBuyIn` 判，而它用的是**刚打完那一关**的
    /// 起始敌人筹码（L1 的 60 → 买入 9）。可下一关的买入会涨（L2 的 90 → 13），
    /// 于是玩家 120 筹码买 110 金卡、剩 10 看似"够 9"，护栏放行，进下一关却付不起
    /// 13，直接 GG。
    ///
    /// 所以这里按「即将要打的那一手」的买入来留钱：
    /// - 已经过了本关、正在等下一关（`AwaitingNextLevel`）⇒ `CurrentLevel` 就是下一关；
    /// - 还在本关里（商店中途开）⇒ 下一手仍是本关，但要为下一关留够。
    /// 只读 GameFlowManager 的公开成员，不修改他的脚本。
    /// </summary>
    public static class PokerBuyInReserve
    {
        /// <summary>买入 = 本关起始敌人筹码 ÷ 7（与 GameFlowManager 里的算法保持一致）。</summary>
        const int BuyInDivisor = 7;

        /// <summary>买入下限，和 GameFlowManager.MinBuyIn 对齐。</summary>
        const int MinBuyIn = 5;

        /// <summary>M3 一共五关；第 5 关之后没有下一关，就按第 5 关的买入留。</summary>
        const int MaxLevel = 5;

        /// <summary>某一关的买入。</summary>
        public static int BuyInForLevel(int level)
        {
            int enemy = GameFlowManager.EnemyChipsForLevel(Mathf.Clamp(level, 1, MaxLevel));
            return Mathf.Max(MinBuyIn, Mathf.RoundToInt(enemy / (float)BuyInDivisor));
        }

        /// <summary>此刻买东西必须留下的筹码。</summary>
        public static int Required()
        {
            var flow = GameFlowManager.Instance;
            if (flow == null) return MinBuyIn;

            // AwaitingNextLevel 为真时 CurrentLevel 已经被 +1 过，它就是下一关。
            int upcoming = flow.AwaitingNextLevel ? flow.CurrentLevel : flow.CurrentLevel + 1;

            return Mathf.Max(flow.CurrentBuyIn, BuyInForLevel(upcoming));
        }
    }
}
