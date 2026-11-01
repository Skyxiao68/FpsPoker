using System.Collections;
using UnityEngine;

/// <summary>
/// Test/bootstrap entry point for the poker game.
/// Creates the game manager and UI, generates enemy parameters,
/// starts new hands, and provides keyboard shortcuts for testing actions.
/// </summary>
public class PokerGameTester : MonoBehaviour
{
    // Reference to the core poker game logic.
    [SerializeField]
    private PokerGameManager poker;

    // Reference to the dynamically built poker UI.
    [SerializeField]
    private PokerUI ui;

    [SerializeField]
    private PokerCombatBridge bridge;

    [Tooltip("从战斗返回后,等多少再开新局(让玩家看到结果)")]
    [SerializeField]
    private float postCombatDelay = 1.5f;

    // Parameters that define the current enemy AI behavior and identity.
    private EnemyAIParameters enemyParams;

    /// <summary>
    /// Unity Start method.
    /// Sets up the game manager, UI, enemy, and starts the first hand.
    /// </summary>
    void Start()
    {

        GameObject uiGO = new GameObject("PokerUI");
        uiGO.transform.SetParent(transform);
        ui = uiGO.AddComponent<PokerUI>();


        //进入扑克场景时，解锁鼠标并显示光标
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (poker == null)
        {
            Debug.LogError("[PokerTester] poker is null");
            return;
        }

        Debug.Log($"[PokerTester] poker entity ID = {poker.GetEntityId()}");

        poker.OnHandEnded += () =>
        {
            Debug.Log($"牌局结束：{poker.Result} | 玩家 {poker.PlayerChips} | 敌人 {poker.EnemyChips}");
        };
        if (ui != null)
        {
            ui.Initialize(poker);
            ui.SetNextHandCallback(StartNewHand);
        }

        //从战斗场景返回后，延迟开新局
        bool fromCombat = bridge != null && bridge.ProcessedCombatResult;
        if (fromCombat)
        {
            Debug.Log($"[PokerTester] 从战斗返回，延迟 {postCombatDelay} 秒后开新局");
             StartCoroutine(DelayedStartNewHand(postCombatDelay));
        }
        else
        {
            StartNewHand();
        }

        
    }

    private IEnumerator DelayedStartNewHand(float delay)
    {
        yield return new WaitForSeconds(delay);
        StartNewHand();
    }

    /// <summary>
    /// Starts a new poker hand.
    /// Preserves chip counts if the game is already in progress,
    /// otherwise uses default starting chips.
    /// Generates a new enemy when appropriate.
    /// </summary>
    private void StartNewHand()
    {
        // Keep existing chips if available; otherwise start with 100 chips.
        int playerChips = poker.PlayerChips > 0 ? poker.PlayerChips : 1000;
        int enemyChips = poker.EnemyChips > 0 ? poker.EnemyChips : 1000;

        // If the previous hand has ended or the game has not started,
        // generate a new random enemy for the next hand.
        if (
            poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded
            || poker.State == PokerState.NotStarted
        )
        {
            enemyParams = EnemyGenerator.GenerateRandom();
            Debug.Log($"[Enemy] New Enemy: {EnemyGenerator.Describe(enemyParams)}");
        }

        // Start a new hand with the current chips, buy-in, and enemy parameters.
        poker.StartNewHand(playerChips, enemyChips, buyIn: 10, parameters: enemyParams);
    }

    /// <summary>
    /// Unity Update method.
    /// Provides keyboard shortcuts for testing player actions.
    /// </summary>
    void Update()
    {
        if (poker == null)
            return;

        // Player action shortcuts, only active during the player's turn.
        if (poker.State == PokerState.PlayerTurn)
        {
            if (Input.GetKeyDown(KeyCode.C))
                poker.PlayerCheck(); // C = Check
            if (Input.GetKeyDown(KeyCode.R))
                poker.PlayerRaise(10); // R = Raise 10
            if (Input.GetKeyDown(KeyCode.F))
                poker.PlayerFold(); // F = Fold
            if (Input.GetKeyDown(KeyCode.M))
                poker.PlayerMatchRaise(); // M = Match raise
        }

        // Press Space to start a new hand after the current hand is over.
        if (
            poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded
        )
        {
            if (Input.GetKeyDown(KeyCode.Space))
                StartNewHand();
        }
    }
}
