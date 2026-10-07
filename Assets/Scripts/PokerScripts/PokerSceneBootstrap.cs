using System.Collections;
using UnityEngine;

/// <summary>
/// 扑克场景的入口脚本。
///
/// 职责（仅此三项）：
///   1. 创建 PokerUI 并初始化
///   2. 设置 Next Hand 回调
///   3. 启动第一手牌（或从战斗返回后延迟启动）
///
/// 明确不做的事（已拆分到其他脚本）：
///   - 键盘测试输入      → PokerDebugInput
///   - 筹码恢复          → PokerCombatBridge.Awake
///   - 敌人参数生成      → 暂时留在这里，后续会移到 GameFlowManager
/// </summary>
public class PokerSceneBootstrap : MonoBehaviour
{
    [SerializeField]
    private PokerGameManager poker;

    [SerializeField]
    private PokerUI ui;

    [SerializeField]
    private PokerCombatBridge bridge;

    private bool handStarted = false;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameObject uiGO = null;
        if (ui == null)
        {
            uiGO = new GameObject("PokerUI");
            uiGO.transform.SetParent(transform);
            ui = uiGO.AddComponent<PokerUI>();
        }
        else
        {
            uiGO = ui.gameObject;
        }

        if (uiGO.GetComponent<CheatGloveOverlay>() == null)
        {
            uiGO.AddComponent<CheatGloveOverlay>();
        }

        if (poker == null)
        {
            Debug.LogError("[PokerSceneBootstrap] poker is null");
            return;
        }

        Debug.Log($"[PokerSceneBootstrap] poker entity ID = {poker.GetEntityId()}");

        poker.OnHandEnded += () =>
        {
            Debug.Log(
                $"牌局结束：{poker.Result} | 玩家 {poker.PlayerChips} | 敌人 {poker.EnemyChips}"
            );
            handStarted = false;
        };

        ui.Initialize(poker);
        ui.SetNextHandCallback(StartNewHand);

        // 判断是否从战斗返回：直接读 GameFlowManager，不再依赖 bridge
        // 从战斗场景返回：不自动开新局，等玩家点 Next Match
        bool fromCombat =
            GameFlowManager.Instance != null && GameFlowManager.Instance.ReturnedFromCombat;

        if (fromCombat)
        {
            Debug.Log("[Bootstrap] 从战斗返回，等待玩家点击 Next Match");
            GameFlowManager.Instance.ClearReturnedFromCombat();
        }
        else
        {
            // 首次进入扑克场景：自动开新局
            StartNewHand();
        }
    }

    /// <summary>
    /// 启动新的一手牌。
    /// 已经进行中则保留筹码；否则使用默认筹码。
    /// 牌局结束后会生成新敌人。
    /// </summary>
    public void StartNewHand()
    {
        if (handStarted)
        {
            Debug.LogWarning("[Bootstrap] StartNewHand 被重复调用，忽略");
            return;
        }
        handStarted = true;

        // ---------- 1. 筹码：优先用 GameFlowManager 的权威值 ----------
        int playerChips;
        int enemyChips;

        if (GameFlowManager.Instance != null)
        {
            playerChips = GameFlowManager.Instance.PlayerChips;
            enemyChips = GameFlowManager.Instance.EnemyChips;
        }
        else
        {
            // 独立测试路径：Bridge 可能已经恢复了 poker 的筹码
            playerChips = poker.PlayerChips;
            enemyChips = poker.EnemyChips;
        }

        if (playerChips <= 0)
            playerChips = 100;
        if (enemyChips <= 0)
            enemyChips = 100;

        // ---------- 2. BuyIn：从 GameFlowManager 读，独立模式 fallback 到 10 ----------
        int buyIn = GameFlowManager.Instance != null ? GameFlowManager.Instance.BuyIn : 10;

        // ---------- 3. 是否需要换新敌人 ----------
        bool needNewEnemy =
            poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded
            || poker.State == PokerState.NotStarted;

        EnemyAIParameters enemy;

        if (needNewEnemy)
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.AdvanceToNextEnemy();
                enemy = GameFlowManager.Instance.CurrentEnemy;
            }
            else
            {
                enemy = EnemyGenerator.GenerateRandom();
                Debug.Log($"[Standalone] New Enemy: {EnemyGenerator.Describe(enemy)}");
            }
        }
        else
        {
            // 同一敌人继续打下一手牌
            enemy = GameFlowManager.Instance != null ? GameFlowManager.Instance.CurrentEnemy : null;

            if (enemy == null)
            {
                enemy = EnemyGenerator.GenerateRandom();
                Debug.Log($"[Fallback] New Enemy: {EnemyGenerator.Describe(enemy)}");
            }
        }

        // ---------- 4. 开新局 ----------
        poker.StartNewHand(playerChips, enemyChips, buyIn, enemy);
    }
}
