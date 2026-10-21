using System.Collections;
using UnityEngine;

public class PokerSceneBootstrap : MonoBehaviour
{
    [SerializeField] private PokerGameManager poker;
    [SerializeField] private PokerUI ui;
    [SerializeField] private PokerCombatBridge bridge;

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
            uiGO.AddComponent<CheatGloveOverlay>();

        if (poker == null)
        {
            Debug.LogError("[PokerSceneBootstrap] poker is null");
            return;
        }

        Debug.Log($"[PokerSceneBootstrap] poker entity ID = {poker.GetEntityId()}");

        poker.OnHandEnded += () =>
        {
            Debug.Log($"牌局结束：{poker.Result} | 玩家 {poker.PlayerChips} | 敌人 {poker.EnemyChips}");
            handStarted = false;
        };

        ui.Initialize(poker);
        ui.SetNextHandCallback(StartNewHand);

        handStarted = false;

        // ---- 从哪个场景返回？ ----
        bool fromCombat = GameFlowManager.Instance != null
            && GameFlowManager.Instance.ReturnedFromCombat;
        bool fromShop = GameFlowManager.Instance != null
            && GameFlowManager.Instance.ReturnedFromShop;

        if (fromShop)
        {
            Debug.Log("[Bootstrap] 从商店返回，开始下一关");
            GameFlowManager.Instance.ClearReturnedFromShop();
            GameFlowManager.Instance.AdvanceToNextEncounter();
            GameFlowManager.Instance.EnemyChips = 500;
        }
        else if (fromCombat)
        {
            Debug.Log("[Bootstrap] 从战斗返回，继续本关");
            GameFlowManager.Instance.ClearReturnedFromCombat();
            // 什么都不做，让下面的 StartNewHand 判断破产
        }

        // 无论哪种路径，都开新局（StartNewHand 内部会处理破产）
        StartNewHand();
    }

    public void StartNewHand()
    {
        if (handStarted)
        {
            Debug.LogWarning("[Bootstrap] StartNewHand 重复调用，忽略");
            return;
        }
        handStarted = true;

        // ========== 1. 从 GFM 读原始筹码 ==========
        int playerChips;
        int enemyChips;

        if (GameFlowManager.Instance != null)
        {
            playerChips = GameFlowManager.Instance.PlayerChips;
            enemyChips = GameFlowManager.Instance.EnemyChips;
        }
        else
        {
            playerChips = poker.PlayerChips;
            enemyChips = poker.EnemyChips;
        }

        // ========== 2. BuyIn ==========
        int buyIn = GameFlowManager.Instance != null
            ? GameFlowManager.Instance.CurrentBuyIn : 10;

        // ========== 3. 敌人破产检查（在开局之前） ==========
        // enemyChips < 0 表示"未初始化"，不算破产
        // enemyChips 在 [0, buyIn) 范围内才算敌人破产
        bool enemyBroke = enemyChips >= 0 && enemyChips < buyIn;

        if (enemyBroke && GameFlowManager.Instance != null)
        {
            Debug.Log($"[Bootstrap] 敌人破产 (筹码 {enemyChips} < buyIn {buyIn}) → 进商店");
            GameFlowManager.Instance.PlayerChips = playerChips;
            GameFlowManager.Instance.BeginShop();
            return;   //  不开局
        }

        // ========== 4. 玩家破产 ==========
        if (playerChips <= 0)
        {
            Debug.LogWarning($"[Bootstrap] 玩家破产 (筹码 {playerChips})，暂用 100（正式版应 Game Over）");
            playerChips = 100;
        }

        // ========== 5. 敌人的 fallback 只处理"未初始化" ==========
        if (enemyChips < 0)
        {
            Debug.Log("[Bootstrap] 敌人筹码未初始化，使用默认 500");
            enemyChips = 500;
            if (GameFlowManager.Instance != null)
                GameFlowManager.Instance.EnemyChips = enemyChips;
        }

        // ========== 6. 敌人参数 ==========
        EnemyAIParameters enemy;
        if (GameFlowManager.Instance != null)
        {
            if (GameFlowManager.Instance.CurrentEnemy == null)
            {
                GameFlowManager.Instance.AdvanceToNextEncounter();
                Debug.Log("[Bootstrap] 首次进入，初始化敌人");
            }
            enemy = GameFlowManager.Instance.CurrentEnemy;
        }
        else
        {
            enemy = EnemyGenerator.GenerateRandom();
            Debug.Log($"[Standalone] New Enemy: {EnemyGenerator.Describe(enemy)}");
        }

        // ========== 7. 开新局 ==========
        poker.StartNewHand(playerChips, enemyChips, buyIn, enemy);
    }
}