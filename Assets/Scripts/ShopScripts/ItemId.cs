/// <summary>
/// 所有物品的 ID。
/// MVP 阶段用 enum，未来扩展成 ScriptableObject 时保留它作为 key。
/// </summary>

public enum ItemId
{
    None = 0,
    CardSwapTicket,   // 换牌券（永久工具：每手换 1 张）
    DoubleSwap,       // 双换（永久工具：每手换 2 张）
    CheatGlove,       // 老千手套（永久工具：看 1 张）
    ColdRead,         // 冷读（永久工具：看 2 张）
    QueenOfHearts,    // 红桃皇后
    SpadeTattoo,      // 黑桃刺青（♠ 兼给 HP）
    RedShift,         // 红移（♦ 兼给 ATK）
    FlatBonus,        // 平A加成（所有牌型倍率 +0.10）
    LuckyHigh,        // 赌高牌（高牌 1.00 → 1.30，其余 −0.05）
    Pressure,         // 施压（你加注时对手更怕）
    Bait,             // 诱敌（你过牌时对手更敢下注）
    DrawSix,          // 六张取五（多一张手牌，摊牌取最好五张）
    Precog,           // 预知（永久工具：每手都能看到牌堆顶 3 张）
    Monochrome,       // 四色归一（全手按点数总和最高的花色结算）
    PairUp,           // 单对升格（「一对」按「两对」结算）
    Momentum,         // 加注惯性（本手每加注一次，摊牌倍率 +0.05，上限 +0.15）
    FourFlush,        // 四张同花（4 张同花色即按同花结算）
    ChopBreaker,      // 断平（摊牌平局判你赢）
    SuitStack,        // 花色堆叠（同花色每多一张，该属性 ×1.3，封顶 ×2）
    Deadeye,
    Quickstep,
    IronHeart,
    Headhunter,       // Steph 的战斗侧道具（真工程/他的分支里用到，这里保留以免合并时冲突）
    LifeSteal,
    Marked,
    LastStand,
    GoldenBullet,
}
