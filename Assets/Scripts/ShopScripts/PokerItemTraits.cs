using UnityEngine;

/// <summary>
/// 扑克侧道具的属性表。
///
/// 这些数字以前散落在三个地方（<see cref="PokerGameManager"/> 的换牌次数、
/// <see cref="CheatGloveOverlay"/> 的窥牌张数、<see cref="PokerItemsPanel"/> 的显示），
/// 加一件新道具就要改三处。现在统一从这里取。
///
/// Ben 2026-11-08 定：Card Swap 与 Double Swap **由消耗品改成永久工具** ——
/// Card Swap 每手可以换 1 张，Double Swap 每手换 2 张，换完不再消耗库存。
/// </summary>
public static class PokerItemTraits
{
    /// <summary>每手最多换几次牌。同类不叠加，取拥有的最好那件。</summary>
    public static int SwapsPerHand(ItemId id)
    {
        switch (id)
        {
            case ItemId.CardSwapTicket: return 1;
            case ItemId.DoubleSwap:     return 2;
            default:                    return 0;
        }
    }

    /// <summary>每手能提前看到几张对手手牌。</summary>
    public static int RevealCount(ItemId id)
    {
        switch (id)
        {
            case ItemId.CheatGlove: return 1;
            case ItemId.ColdRead:   return 2;
            default:                return 0;
        }
    }

    /// <summary>永久工具：不消耗、不叠加，背包里显示 ∞。</summary>
    public static bool IsPermanentTool(ItemId id)
    {
        return SwapsPerHand(id) > 0 || RevealCount(id) > 0 || id == ItemId.Precog;
    }

    // ------------------------------------------------------------------
    // 花色 / 倍率类道具（Ben 2026-11-08 批的花色倍率批）
    // ------------------------------------------------------------------

    /// <summary>黑桃刺青：♠ 除了 ATK 之外，还按这个比例兼给 HP。</summary>
    public const float SpadeHealthShare = 0.7f;

    /// <summary>红移：♦ 除了 SPD 之外，还按这个比例兼给 ATK。</summary>
    public const float DiamondAttackShare = 0.5f;

    /// <summary>
    /// 红桃皇后：♥ 除了 HP 之外，还按这个比例兼给 ATK。
    /// Ben 2026-11-09 要求跟另外两件花色类保持一致（不再整块搬走 HP）。
    /// </summary>
    public const float QueenHeartsAttackShare = 0.5f;

    /// <summary>平A加成：所有牌型倍率加成。</summary>
    public const float FlatMultiplierBonus = 0.10f;

    /// <summary>
    /// 赌高牌：把高牌从 1.00 提到 1.30，代价是其它牌型各降 0.05。
    /// </summary>
    public const float LuckyHighBonus = 0.30f;
    public const float LuckyHighPenalty = -0.05f;

    /// <summary>
    /// 单对升格：把「一对」当成「两对」结算。
    /// 倍率上是 1.10 → 1.20（+0.10），比较时只要对手也是单对就判你赢。
    /// </summary>
    public const float PairUpBonus = 0.10f;

    /// <summary>加注惯性：本手每加注一次加多少倍率、最多加多少。</summary>
    public const float MomentumPerRaise = 0.05f;
    public const float MomentumCap = 0.15f;

    /// <summary>单对升格在不在背包里。</summary>
    public static bool HasPairUp()
    {
        return Has(ItemId.PairUp);
    }

    /// <summary>
    /// 加注惯性：按本手加注次数算倍率加成（封顶 MomentumCap）。
    /// 本手加注次数由 PokerGameManager 在结算前传进来。
    /// </summary>
    public static float MomentumBonus(int raisesThisHand)
    {
        if (!Has(ItemId.Momentum) || raisesThisHand <= 0)
            return 0f;

        return Mathf.Min(MomentumCap, MomentumPerRaise * raisesThisHand);
    }

    /// <summary>这件道具在不在背包里。</summary>
    public static bool Has(ItemId id)
    {
        return PlayerInventory.Instance != null && PlayerInventory.Instance.Has(id);
    }

    /// <summary>
    /// 牌型倍率的修正量（叠加所有持有道具）。0 表示没有任何倍率类道具。
    /// 结算面板和实战都走这里，避免两边算出来不一样。
    /// </summary>
    public static float MultiplierBonus(HandType handType)
    {
        float bonus = 0f;

        if (Has(ItemId.FlatBonus))
            bonus += FlatMultiplierBonus;

        if (Has(ItemId.LuckyHigh))
            bonus += handType == HandType.HighCard ? LuckyHighBonus : LuckyHighPenalty;

        if (HasPairUp() && handType == HandType.OnePair)
            bonus += PairUpBonus;

        return bonus;
    }

    /// <summary>
    /// 施压：你加注时，对手的 fold 阈值往上抬这么多（更怕、更容易丢牌）。
    /// 必须叠加在 <c>EnemyPokerAI</c> 的 <c>Mathf.Clamp</c> **之前** ——
    /// 钳位之后再加会把阈值顶出 0.65，对手就无脑弃牌了。
    /// </summary>
    public const float PressureFoldBonus = 0.08f;

    /// <summary>
    /// 诱敌：你过牌时，对手的加注阈值往下压这么多（更敢下注）。
    /// </summary>
    public const float BaitRaiseBonus = 0.08f;

    /// <summary>
    /// 六张取五：手里的 hole card 比对手多几张。
    /// Ben 2026-11-09 定：玩家 2 张变 3 张（摊牌从七张里选五张变成八张里选五张）。
    /// </summary>
    public static int ExtraHoleCards()
    {
        return Has(ItemId.DrawSix) ? 1 : 0;
    }

    /// <summary>持有施压时返回阈值修正，否则 0。</summary>
    public static float FoldThresholdBonus()
    {
        return Has(ItemId.Pressure) ? PressureFoldBonus : 0f;
    }

    /// <summary>持有诱敌时返回加注阈值修正，否则 0。</summary>
    public static float RaiseThresholdBonus()
    {
        return Has(ItemId.Bait) ? BaitRaiseBonus : 0f;
    }

    /// <summary>四色归一：整手牌都按「点数总和最高的那个花色」结算。</summary>
    public static bool HasMonochrome()
    {
        return Has(ItemId.Monochrome);
    }

    /// <summary>预知：每手牌开始就能看到牌堆顶几张。</summary>
    public static int PrecogPeekCount()
    {
        return Has(ItemId.Precog) ? 3 : 0;
    }

    // ------------------------------------------------------------------
    // 第三批（Ben 2026-11-09 批准）：四张同花 / 断平 / 花色堆叠
    // ------------------------------------------------------------------

    /// <summary>四张同花：同花色凑到几张就按同花结算。</summary>
    public const int FourFlushSuitCount = 4;

    /// <summary>花色堆叠：每多一张同花色，该花色倍率乘这个系数。</summary>
    public const float SuitStackStep = 1.3f;

    /// <summary>花色堆叠：倍率上限。</summary>
    public const float SuitStackCap = 2.0f;

    /// <summary>四张同花在不在背包里。</summary>
    public static bool HasFourFlush()
    {
        return Has(ItemId.FourFlush);
    }

    /// <summary>断平在不在背包里。</summary>
    public static bool HasChopBreaker()
    {
        return Has(ItemId.ChopBreaker);
    }

    /// <summary>花色堆叠在不在背包里。</summary>
    public static bool HasSuitStack()
    {
        return Has(ItemId.SuitStack);
    }

    /// <summary>
    /// 花色堆叠：某个花色在手牌里出现 count 张时，该花色的倍率。
    /// 1 张以内不涨，之后每多一张 ×1.3，封顶 SuitStackCap。
    /// </summary>
    public static float SuitStackFactor(int count)
    {
        if (!HasSuitStack() || count <= 1)
            return 1f;

        return Mathf.Min(SuitStackCap, Mathf.Pow(SuitStackStep, count - 1));
    }

    private static readonly ItemId[] KnownIds =
    {
        ItemId.CardSwapTicket,
        ItemId.DoubleSwap,
        ItemId.CheatGlove,
        ItemId.ColdRead,
    };

    /// <summary>背包里所有换牌类道具能给到的每手换牌次数。</summary>
    public static int OwnedSwapsPerHand()
    {
        if (PlayerInventory.Instance == null)
            return 0;

        int best = 0;
        for (int i = 0; i < KnownIds.Length; i++)
        {
            if (PlayerInventory.Instance.GetCount(KnownIds[i]) <= 0)
                continue;

            int value = SwapsPerHand(KnownIds[i]);
            if (value > best)
                best = value;
        }
        return best;
    }

    /// <summary>背包里所有窥牌类道具能给到的最大窥牌张数。</summary>
    public static int OwnedRevealCount()
    {
        if (PlayerInventory.Instance == null)
            return 0;

        int best = 0;
        for (int i = 0; i < KnownIds.Length; i++)
        {
            if (PlayerInventory.Instance.GetCount(KnownIds[i]) <= 0)
                continue;

            int value = RevealCount(KnownIds[i]);
            if (value > best)
                best = value;
        }
        return best;
    }
}
