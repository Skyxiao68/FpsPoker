using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The only thing that survives the poker/combat scene boundary.
/// Everything else - PokerGameManager, every fighter, all of it -
/// gets destroyed on each SceneManager.LoadScene call, so anything
/// that needs to outlive that call lives here instead.
///
/// Holds two things: the persistent run economy (PlayerChips,
/// carried across every hand and every fight for the whole run),
/// and a one-shot handoff payload in each direction (poker->combat
/// stats+pot, combat->poker winner+pot). Each payload is consumed
/// exactly once via TryConsume*, so a stray extra scene load can't
/// replay stale data.
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public static GameFlowManager Instance { get; private set; }

    [Header("Scenes")]
    [Tooltip("Exact scene name as it appears in Build Settings.")]
    [SerializeField]
    private string pokerSceneName = "PokerScene";

    [Tooltip("Exact scene name as it appears in Build Settings.")]
    [SerializeField]
    private string combatSceneName = "CombatScene";

    [Header("Economy")]
    [Tooltip("Used only the very first time the run starts.")]
    [SerializeField]
    private int startingChips = 1000;

    [SerializeField]
    int startingEnemyChips = 1000;

    [Tooltip("每手牌的buy-in 金额")]
    [SerializeField]
    private int buyIn = 10;

    public int BuyIn => buyIn;

    /// <summary>
    /// The player's persistent chip total. -1 means "not yet
    /// initialised" so the very first read seeds startingChips
    /// rather than 0.
    /// </summary>
    public int PlayerChips { get; set; } = -1;
    public int EnemyChips { get; set; } = -1;

    /// <summary>
    /// 本次扑克场景加载是否是从战斗返回的。
    /// ReportCombatResult 时设为 true；扑克场景的 Bootstrap 消费完后清除。
    /// </summary>
    public bool ReturnedFromCombat { get; private set; } = false;

    /// <summary>
    /// 当前正在面对的敌人。
    /// null 表示还没有生成过，需要由扑克场景决定何时首次生成。
    /// 由 GameFlowManager 持有，跨场景保留。
    /// </summary>
    public EnemyAIParameters CurrentEnemy { get; private set; }

    private CombatHandoff? pendingHandoff;
    private PendingResult? pendingResult;

    private struct PendingResult
    {
        public CombatWinner winner;
        public int pot;
    }

    private void Awake()
    {
        // Standard persistent-singleton guard: if one already
        // exists (we came from a previous scene), this duplicate
        // from the freshly loaded scene gets destroyed instead.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (PlayerChips < 0)
        {
            PlayerChips = startingChips;
        }

        if (EnemyChips < 0)
        {
            EnemyChips = startingEnemyChips;
        }
    }

    // =========================================================
    // POKER -> COMBAT
    // =========================================================

    /// <summary>
    /// Called by PokerGameManager once a showdown resolves into
    /// combat (not on a fold or a tie). Snapshots the player's
    /// current chip count - since the poker scene is about to be
    /// destroyed - and loads the combat scene.
    /// </summary>
    public void BeginCombat(
        int currentPlayerChips,
        int currentEnemyChips,
        CombatStats playerStats,
        CombatStats enemyStats,
        int pot,
        string enemyName,
        string enemyCombatStyle
    )
    {
        PlayerChips = currentPlayerChips;
        EnemyChips = currentEnemyChips;

        pendingHandoff = new CombatHandoff
        {
            playerStats = playerStats,
            enemyStats = enemyStats,
            pot = pot,
            enemyName = enemyName,
            enemyCombatStyle = enemyCombatStyle,
        };

        SceneManager.LoadScene(combatSceneName);
    }

    /// <summary>
    /// Called once by the combat scene's setup script. Returns
    /// false if there's nothing pending - e.g. the combat scene
    /// was opened directly rather than reached via poker.
    /// </summary>
    public bool TryConsumeCombatHandoff(out CombatHandoff handoff)
    {
        if (pendingHandoff.HasValue)
        {
            handoff = pendingHandoff.Value;
            pendingHandoff = null;
            return true;
        }

        handoff = default;
        return false;
    }

    // =========================================================
    // COMBAT -> POKER
    // =========================================================

    /// <summary>
    /// Called by the combat scene's setup script the moment either
    /// fighter dies. The pot travels with the result rather than
    /// staying stored here, since PokerGameManager's own Pot field
    /// will already be reset to 0 by the time it exists again.
    /// </summary>
    public void ReportCombatResult(CombatWinner winner, int pot)
    {
        pendingResult = new PendingResult { winner = winner, pot = pot };
        ReturnedFromCombat = true;
        SceneManager.LoadScene(pokerSceneName);
    }

    /// <summary>
    /// Called once by PokerGameManager on load. Returns false if
    /// there's nothing pending - e.g. this is the very first hand
    /// of the run, with no prior combat to settle.
    /// </summary>
    public bool TryConsumeCombatResult(out CombatWinner winner, out int pot)
    {
        if (pendingResult.HasValue)
        {
            winner = pendingResult.Value.winner;
            pot = pendingResult.Value.pot;
            pendingResult = null;
            return true;
        }

        winner = CombatWinner.None;
        pot = 0;
        return false;
    }

    /// <summary>
    /// 由扑克场景消费掉"从战斗返回"这个状态，
    /// 防止后续开新局时被重复判为"从战斗返回"。
    /// </summary>
    public void ClearReturnedFromCombat()
    {
        ReturnedFromCombat = false;
    }

    /// <summary>
    /// 推进到下一个敌人：生成随机敌人并记录下来。
    /// 由扑克场景的 Bootstrap 在合适的时机（牌局结束时）调用。
    /// </summary>
    public void AdvanceToNextEnemy()
    {
        CurrentEnemy = EnemyGenerator.GenerateRandom();
        Debug.Log($"[GameFlow] 推进到新敌人: {EnemyGenerator.Describe(CurrentEnemy)}");
    }
}
