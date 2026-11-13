using UnityEngine;

/// <summary>
/// 四项战斗数值的量纲并不相同，所以不能直接比大小：
///   ATK = 点数，直接加到武器伤害上（CombatSceneController.ApplyWeaponStats）
///   HP  = 点数，加到 Health.maxHealth
///   ROF = 百分比，冷却时间 = 基础 / (1 + ROF/100)
///   SPD = 绝对速度，加到 FpsMovementSettings.moveSpeed（默认 5）
///
/// 面板要回答"这一手到底谁更强"时，先把四项统一换算成
/// "相对基线的倍数"再比较；1 表示不增不减。
/// 基线取玩家侧的真实基础值，改这里会直接改变面板的比较结论。
/// </summary>
public static class PokerStatUnits
{
    public const int Attack = 0;
    public const int Health = 1;
    public const int FireRate = 2;
    public const int MoveSpeed = 3;

    /// <summary>CombatSceneController 里占位用的基础伤害（attack = 10）。</summary>
    public const float AttackBase = 10f;

    public const float AttackPercentPerPoint = 2f;

    public static float AttackMultiplier(float attack)
    {
        return Mathf.Max(0f, 1f + attack * AttackPercentPerPoint / 100f);
    }

    public static string FormatAttackPercent(float attack)
    {
        float percent = attack * AttackPercentPerPoint;
        return (percent >= 0f ? "+" : "") + percent.ToString("0.#") + "%";
    }

    /// <summary>Health.maxHealth 的默认值。</summary>
    public const float HealthBase = 100f;

    /// <summary>FpsMovementSettings.moveSpeed 的默认值。</summary>
    public const float MoveSpeedBase = 5f;

    /// <summary>把某一项换算成"相对基线的倍数"：1 = 不增不减。</summary>
    public static float Ratio(int stat, float value)
    {
        switch (stat)
        {
            case Attack: return AttackMultiplier(value);
            case Health: return 1f + value / HealthBase;
            case FireRate: return 1f + value / 100f;
            case MoveSpeed: return 1f + value / MoveSpeedBase;
            default: return 1f;
        }
    }

    /// <summary>两项比较用：正值表示我方更强，单位是"倍数差"。</summary>
    public static float RatioGap(int stat, float mine, float theirs)
    {
        return Ratio(stat, mine) - Ratio(stat, theirs);
    }

    /// <summary>
    /// 进度条用：把倍数折算成 0..1，永远不到 1，所以数值再大也还有差别。
    /// ratio 2 → 0.5，ratio 4 → 0.75，ratio 1 → 0。
    /// </summary>
    public static float BarFill(int stat, float value)
    {
        float ratio = Mathf.Max(0.0001f, Ratio(stat, value));
        return Mathf.Clamp01(1f - 1f / ratio);
    }

    // 空项保底已在 2026-11-10 按 Ben 的要求撤销（buff 太强）：空项就是 0，不再兜底。

    /// <summary>某一项在"不增不减"时的基线值（ROF 是百分比，基线即 100）。</summary>
    public static float Baseline(int stat)
    {
        switch (stat)
        {
            case Attack: return AttackBase;
            case Health: return HealthBase;
            case FireRate: return 100f;
            case MoveSpeed: return MoveSpeedBase;
            default: return 1f;
        }
    }
}
