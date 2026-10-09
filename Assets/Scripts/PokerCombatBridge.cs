using System.Collections;
using UnityEngine;

/// <summary>
/// 连接扑克系统与战斗系统的唯一脚本。
///
/// PokerGameManager 不知道战斗存在。
/// CombatSceneController 不知道扑克存在。
/// 这个桥接脚本是唯一同时知道两边的地方。
///
/// 生命周期：
///   Awake 时恢复筹码 + 处理战斗结果（早于 PokerGameTester.Start）
///   PokerGameTester.Start 里再 StartNewHand 开新局
/// </summary>
public class PokerCombatBridge : MonoBehaviour
{
    [SerializeField]
    private PokerGameManager poker;

    private bool settlingDirectly = false;

    // 生命周期（用 Awake 确保先于 PokerGameTester.Start 执行）

    private void Awake()
    {
        if (poker == null)
        {
            Debug.LogError("PokerCombatBridge：未绑定 PokerGameManager。");
            return;
        }

        Debug.Log($"[Bridge] poker entity ID = {poker.GetEntityId()}");

        if (GameFlowManager.Instance == null)
        {
            Debug.Log("[Bridge] 未找到 GameFlowManager，进入独立测试模式。");
            return;
        }

        // 恢复双方的跨场景筹码
        int savedPlayerChips = GameFlowManager.Instance.PlayerChips;
        int savedEnemyChips = GameFlowManager.Instance.EnemyChips;

        if (savedPlayerChips >= 0)
            poker.RestorePlayerChips(savedPlayerChips);
        if (savedEnemyChips >= 0)
            poker.RestoreEnemyChips(savedEnemyChips);

        // 是否刚从战斗场景返回？
        if (GameFlowManager.Instance.TryConsumeCombatResult(out CombatWinner winner, out int pot))
        {
            Debug.Log($"[Bridge] 收到战斗结果：{winner}, pot {pot}");
            ApplyCombatResult(winner, pot);
        }

        Debug.Log("[Bridge] Awake 完成，已恢复筹码和处理战斗结果");
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

    // 扑克 → 战斗

    private void HandleHandEnded()
    {
        //  任何牌局结束都同步一次筹码到 GFM
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.PlayerChips = poker.PlayerChips;
            GameFlowManager.Instance.EnemyChips = poker.EnemyChips;
            Debug.Log($"[Bridge] 筹码同步 → Player={poker.PlayerChips}, Enemy={poker.EnemyChips}");
        }

        if (settlingDirectly)
            return;

        if (
            poker.Result != PokerResult.PlayerWinsShowdown
            && poker.Result != PokerResult.EnemyWinsShowdown
        )
            return;

        if (GameFlowManager.Instance == null)
        {
            SettleDirectly();
            return;
        }

        ShowdownPanel();
    }

    private ShowdownPanelUI showdownPanel;

    private void ShowdownPanel()
    {
        // 首次创建
        if (showdownPanel == null)
        {
            GameObject panelGO = new GameObject("ShowdownPanel");
            panelGO.transform.SetParent(transform);
            showdownPanel = panelGO.AddComponent<ShowdownPanelUI>();
        }

        showdownPanel.Show(poker, OnEnterCombatClicked);

        Debug.Log("[Bridge] 摊牌面板已显示，等待玩家点击 Enter Combat");
    }

    /// <summary>
    /// 玩家在结算面板里点击 Enter Combat 时触发。
    /// </summary>
    private void OnEnterCombatClicked()
    {
        Debug.Log("[Bridge] 玩家点击 Enter Combat，开始进入战斗");

        // 计算 CombatStats
        CombatStats playerStats = ItemEffectBridge.GetCombatStats(poker.PlayerBestHand);
        CombatStats enemyStats = CombatStatsFactory.Compute(poker.EnemyBestHand); // 敌人不应用玩家物品

        int pot = poker.Pot;
        string enemyName = poker.EnemyParams != null ? poker.EnemyParams.enemyName : "Enemy";
        string enemyStyle = poker.EnemyParams != null ? poker.EnemyParams.combatStyle : "Melee";

        Debug.Log(
            $"[Bridge] BeginCombat: pot={pot}, "
                + $"playerChips={poker.PlayerChips}, enemyChips={poker.EnemyChips}, "
                + $"style={enemyStyle}"
        );

        poker.MarkReadyForCombat();

        GameFlowManager.Instance.BeginCombat(
            poker.PlayerChips,
            poker.EnemyChips,
            playerStats,
            enemyStats,
            pot,
            enemyName,
            enemyStyle
        );
    }

    // 战斗 → 扑克

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

        //  显式同步筹码到 GameFlowManager（不依赖 OnHandEnded 事件）
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.PlayerChips = poker.PlayerChips;
            GameFlowManager.Instance.EnemyChips = poker.EnemyChips;
            Debug.Log(
                $"[Bridge] ApplyCombatResult → 同步 GFM: "
                    + $"Player={poker.PlayerChips}, Enemy={poker.EnemyChips}"
            );
        }
    }

    // 独立测试模式的直接结算

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
