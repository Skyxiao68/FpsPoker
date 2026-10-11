/// <summary>
/// 所有物品的 ID。
/// MVP 阶段用 enum，未来扩展成 ScriptableObject 时保留它作为 key。
/// </summary>

public enum ItemId
{
    None = 0,
    CardSwapTicket,   // 换牌券
    CheatGlove,       // 老千手套
    QueenOfHearts,    // 红桃皇后
}