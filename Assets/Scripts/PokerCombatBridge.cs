using System.Collections.Generic;
using UnityEngine;

public class PokerCombatBridge : MonoBehaviour
{
    [SerializeField]
    private PokerGameManager poker;

    // 防止在「独立结算」时递归触发 OnHandEnded
    private bool settlingDirectly = false;

    // =========================================================
    // 生命周期
    // =========================================================

    private void Start()
    {
        if (poker == null)
        {
            Debug.LogError("PokerCombatBridge：未绑定 PokerGameManager。");
            return;
        }

        if (GameFlowManager.Instance != null)
        {
            int savedChips = GameFlowManager.Instance.PlayerChips;
            if (savedChips >= 0)
                poker.RestorePlayerChips(savedChips);

            if (
                GameFlowManager.Instance.TryConsumeCombatResult(
                    out CombatWinner winner,
                    out int pot
                )
            )
            {
                ApplyCombatResult(winner, pot);
            }
        }
        else
        {
            Debug.Log("[Bridge] 未找到 GameFlowManager，进入独立测试模式。");
        }

        // 补一次检查：如果扑克已经卡在 Showdown（桥接启动晚了）
        if (
            poker.State == PokerState.Showdown
            && (
                poker.Result == PokerResult.PlayerWinsShowdown
                || poker.Result == PokerResult.EnemyWinsShowdown
            )
        )
        {
            Debug.Log("[Bridge] 检测到扑克卡在 Showdown，补触发 HandleHandEnded");
            HandleHandEnded();
        }

        Debug.Log($"[Bridge] poker entity ID = {poker.GetEntityId()}");

        Debug.Log(
            $"[Bridge] Start 执行，GameFlowManager.Instance = {(GameFlowManager.Instance == null ? "null" : "存在")}"
        );
    }

    private void OnEnable()
    {
        if (poker != null)
            poker.OnHandEnded += HandleHandEnded;
    }

    private void OnDisable()
    {
        if (poker != null)
            poker.OnHandEnded -= HandleHandEnded;
    }

    // =========================================================
    // 扑克 → 战斗
    // =========================================================

    private void HandleHandEnded()
    {
        // 避免被自己的结算调用递归触发
        if (settlingDirectly)
            return;

        // 只有摊牌出胜负才需要处理。
        // 弃牌和平局在 PokerGameManager 内部已经结算。
        if (
            poker.Result != PokerResult.PlayerWinsShowdown
            && poker.Result != PokerResult.EnemyWinsShowdown
        )
            return;

        // 降级：没有 GameFlowManager 时直接结算，方便独立测试
        if (GameFlowManager.Instance == null)
        {
            SettleDirectly();
            return;
        }

        // 打包双方属性
        CombatStats playerStats = CombatStatsFactory.Compute(
            poker.PlayerHand,
            poker.PlayerBestHand,
            poker.CommunityCards
        );

        CombatStats enemyStats = CombatStatsFactory.Compute(
            poker.EnemyHand,
            poker.EnemyBestHand,
            poker.CommunityCards
        );

        int pot = poker.Pot;
        string enemyName = poker.EnemyParams != null ? poker.EnemyParams.enemyName : "Enemy";
        string enemyStyle = poker.EnemyParams != null ? poker.EnemyParams.combatStyle : "Melee";

        poker.MarkReadyForCombat();

        GameFlowManager.Instance.BeginCombat(
            poker.PlayerChips,
            playerStats,
            enemyStats,
            pot,
            enemyName,
            enemyStyle
        );
    }

    // =========================================================
    // 战斗 → 扑克
    // =========================================================

    private void ApplyCombatResult(CombatWinner winner, int pot)
    {
        poker.SetPendingPot(pot);

        settlingDirectly = true;
        if (winner == CombatWinner.Player)
        {
            poker.CreditPotToPlayer();
            Debug.Log($"[Bridge] 玩家战斗胜利，底池 {pot} 归玩家。");
        }
        else
        {
            poker.ForfeitPot();
            Debug.Log($"[Bridge] 玩家战斗失败，底池 {pot} 被没收。");
        }
        settlingDirectly = false;
    }

    // =========================================================
    // 独立测试模式的直接结算
    // =========================================================

    private void SettleDirectly()
    {
        settlingDirectly = true;

        if (poker.Result == PokerResult.PlayerWinsShowdown)
        {
            poker.CreditPotToPlayer();
            Debug.Log("[Bridge] 独立模式：玩家摊牌赢，底池归玩家。");
        }
        else
        {
            poker.ForfeitPot();
            Debug.Log("[Bridge] 独立模式：敌人摊牌赢，底池被没收。");
        }

        settlingDirectly = false;
    }
}
