using UnityEngine;

/// <summary>
/// 自走棋式的商店稀有度曲线。
/// 每次进商店，概率向高稀有度偏移。
///
/// 参数说明：
///   bronzeStart = 第 1 次访问的 bronze 权重
///   bronzeStep  = 每次访问 bronze 的衰减量
///   silverStart / silverStep = silver 的起点和增长
///   goldStart / goldStep     = gold 的起点和增长
///
/// 当前配置（按用户需求）：
///   visit 1: 90 / 9 / 1
///   visit 2: 80 / 18 / 2
///   visit N: max(0, 100-10N) / 9N / N
/// </summary>
public static class ShopRarityWeights
{
    public const float BronzeStart = 90f;
    public const float BronzeStep  = -10f;

    public const float SilverStart = 9f;
    public const float SilverStep  = 9f;

    public const float GoldStart   = 1f;
    public const float GoldStep    = 1f;

    /// <summary>
    /// 根据访问次数计算三档权重。
    /// </summary>
    public static void GetWeights(int visitNumber, out float bronze, out float silver, out float gold)
    {
        int n = Mathf.Max(1, visitNumber);

        bronze = Mathf.Max(0f, BronzeStart + BronzeStep * (n - 1));
        silver = Mathf.Min(100f, SilverStart + SilverStep * (n - 1));
        gold   = Mathf.Min(100f, GoldStart   + GoldStep   * (n - 1));
    }

    /// <summary>调试用：把权重转成百分比字符串</summary>
    public static string Describe(int visitNumber)
    {
        GetWeights(visitNumber, out float b, out float s, out float g);
        float total = b + s + g;
        if (total <= 0) return "无可用权重";
        return $"Bronze {b/total*100:F1}% | Silver {s/total*100:F1}% | Gold {g/total*100:F1}%";
    }
}