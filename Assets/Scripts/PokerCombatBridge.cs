using UnityEngine;
using System.Collections;

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

    [Tooltip("摊牌后到进入战斗前的展示时间，(秒)")]
    [SerializeField]
    private float showdownRevealDelay = 3.0f;

    /// <summary>
    /// True 表示这个场景刚从战斗场景回来并处理了战斗结果。
    /// PokerGameTester 可以据此决定是否延迟开新局。
    /// </summary>
    public bool ProcessedCombatResult { get; private set; } = false;
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
            ProcessedCombatResult = true; 
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
        if (settlingDirectly)
            return;

        // 弃牌和平局已经在 PokerGameManager 内部结算
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

        StartCoroutine(ShowdownThenCombat());

    }
    private IEnumerator ShowdownThenCombat()
    {
        // 打出摊牌信息供玩家查看
        Debug.Log($"[Bridge] === 摊牌结果 ===");
        Debug.Log($"[Bridge] 玩家牌型：{poker.PlayerBestHand.GetDisplayName()}");
        Debug.Log($"[Bridge] 敌人牌型：{poker.EnemyBestHand.GetDisplayName()}");
        Debug.Log($"[Bridge] 底池：{poker.Pot}");
        Debug.Log($"[Bridge] {showdownRevealDelay} 秒后进入战斗...");

        // UI 订阅者可以在 OnMessage 里显示摊牌信息
        // PokerUI 会自动显示敌人手牌（因为 State == Showdown）

        // 等一会，让玩家看清双方手牌
        yield return new WaitForSeconds(showdownRevealDelay);

        // 计算 CombatStats（用最佳 5 张）
        CombatStats playerStats = CombatStatsFactory.Compute(poker.PlayerBestHand);
        CombatStats enemyStats = CombatStatsFactory.Compute(poker.EnemyBestHand);

        int pot = poker.Pot;
        string enemyName = poker.EnemyParams != null
            ? poker.EnemyParams.enemyName : "Enemy";
        string enemyStyle = poker.EnemyParams != null
            ? poker.EnemyParams.combatStyle : "Melee";

        Debug.Log($"[Bridge] BeginCombat: pot={pot}, " +
                  $"playerChips={poker.PlayerChips}, enemyChips={poker.EnemyChips}, " +
                  $"style={enemyStyle}");

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
