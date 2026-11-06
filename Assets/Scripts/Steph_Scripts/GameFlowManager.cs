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

    [Tooltip("Exact scene name as it appears in Build Settings.")]
    [SerializeField]
    private string shopSceneName = "ShopScene";

    [SerializeField]
    private string startSceneName = "StartScene";

    [Header("Economy")]
    [Tooltip("Used only the very first time the run starts.")]
    [SerializeField]
    private int startingChips = 60;

    [SerializeField]
    int startingEnemyChips = 60;

    [Tooltip("每手牌的buy-in 金额（保底值；实际买入 = 敌人筹码 ÷ buyInDivisor，见 CurrentBuyIn）")]
    [SerializeField]
    private int buyIn = 10;

    private int levelStartEnemyChips = -1;

    /// <summary>
    /// 买入 = 本关起始敌人筹码 ÷ 这个数。7 实测每关中位 1.5–4 手；
    /// 用常量而不是 SerializeField，免得场景里残留的旧值覆盖它。
    /// </summary>
    private const int BuyInDivisor = 7;

    /// <summary>买入下限，避免敌人筹码很小时买入变成 0。</summary>
    private const int MinBuyIn = 5;

    /// <summary>
    /// 本手买入。按本关起始敌人筹码的 1/7 走，让每关的手数不随关卡长度爆炸
    /// （底池随买入放大，所以买入越大一关越短）。敌人筹码未知时退回 buyIn。
    /// </summary>
    public int CurrentBuyIn
    {
        get
        {
            int basis = levelStartEnemyChips > 0 ? levelStartEnemyChips : EnemyChips;
            if (basis <= 0)
                return buyIn;

            int scaled = Mathf.RoundToInt(basis / (float)BuyInDivisor);
            return Mathf.Max(MinBuyIn, scaled);
        }
    }

    /// <summary>
    /// 本关开始时敌人的筹码。买入由它算（而不是实时敌人筹码），
    /// 否则敌人越打越少、买入跟着变少，反而永远输不到「付不起买入」那条线。
    /// </summary>
    public int LevelStartEnemyChips
    {
        get { return levelStartEnemyChips; }
        set { levelStartEnemyChips = value; }
    }

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

    /// <summary>是否从商店场景返回</summary>
    public bool ReturnedFromShop { get; private set; } = false;

    /// <summary>
    /// 当前正在面对的敌人。
    /// null 表示还没有生成过，需要由扑克场景决定何时首次生成。
    /// 由 GameFlowManager 持有，跨场景保留。
    /// </summary>
    public EnemyAIParameters CurrentEnemy { get; private set; }

    /// <summary>玩家进商店的次数，每次进店 +1。用来驱动稀有度递增</summary>
    public int ShopVisitCount { get; private set; } = 0;

    public int CurrentLevel { get; private set; } = 1;
    public bool AwaitingNextLevel { get; private set; }
    public bool IsGameOver { get; private set; }

    private CombatHandoff? pendingHandoff;
    private CombatHandoff? activeCombatHandoff;
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

        // 买入按「本关起始敌人筹码 ÷ 7」算；第一次进扑克就把基准固定下来，
        // 否则敌人越打越少、买入跟着缩水，「付不起买入」这条过关线永远不触发。
        if (levelStartEnemyChips <= 0 && EnemyChips > 0)
        {
            levelStartEnemyChips = EnemyChips;
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
        string enemyCombatStyle,
        FpsItemDefinition[] playerFpsItems
    )
    {
        PlayerChips = currentPlayerChips;
        EnemyChips = currentEnemyChips;

        pendingHandoff = new CombatHandoff
        {
            playerStats = playerStats,
            enemyStats = enemyStats,
            playerFpsItems = playerFpsItems,
            pot = pot,
            enemyName = enemyName,
            enemyCombatStyle = enemyCombatStyle,
        };
        activeCombatHandoff = pendingHandoff;

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
        activeCombatHandoff = null;
        pendingResult = new PendingResult { winner = winner, pot = pot };
        ReturnedFromCombat = true;
        SceneManager.LoadScene(pokerSceneName);
    }

    public bool RestartCurrentCombat()
    {
        if (SceneManager.GetActiveScene().name != combatSceneName || !activeCombatHandoff.HasValue)
            return false;

        pendingHandoff = activeCombatHandoff;
        SceneManager.LoadScene(combatSceneName);
        return true;
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

    public void BeginShop()
    {
        ReturnedFromShop = false; // 刚离开扑克，还没回来
        SceneManager.LoadScene(shopSceneName);
    }

    public void ReportShopResult()
    {
        if (PlayerChips < CurrentBuyIn)
        {
            ShowGameOver();
            return;
        }

        if (AwaitingNextLevel)
        {
            ReturnedFromShop = false;
            SceneManager.LoadScene(startSceneName);
            return;
        }

        ReturnedFromShop = true;
        SceneManager.LoadScene(pokerSceneName);
    }

    /// <summary>
    /// 第 level 关的敌人筹码：60 × 1.5^(level-1)，取整到 5 的倍数。
    /// 与 m3/shop-economy-sheet.csv 的曲线一致（60 / 90 / 135 / 200 / 305）。
    /// </summary>
    public static int EnemyChipsForLevel(int level)
    {
        int lv = Mathf.Max(1, level);
        float raw = 60f * Mathf.Pow(1.5f, lv - 1);
        return Mathf.RoundToInt(raw / 5f) * 5;
    }

    public void ShowNextLevelMenu()
    {
        if (AwaitingNextLevel)
            return;

        CurrentLevel++;
        AwaitingNextLevel = true;
        SceneManager.LoadScene(startSceneName);
    }

    public void BeginSelectedLevel()
    {
        if (!AwaitingNextLevel)
            return;

        if (PlayerChips < CurrentBuyIn)
        {
            ShowGameOver();
            return;
        }

        AwaitingNextLevel = false;
        ReturnedFromShop = false;
        EnemyChips = EnemyChipsForLevel(CurrentLevel);
        levelStartEnemyChips = EnemyChips;
        AdvanceToNextEncounter();
        SceneManager.LoadScene(pokerSceneName);
    }

    public void ShowGameOver()
    {
        IsGameOver = true;
        AwaitingNextLevel = false;
        SceneManager.LoadScene(startSceneName);
    }

    public void ClearReturnedFromShop()
    {
        ReturnedFromShop = false;
    }

    /// <summary>扑克场景名（教程结束时用它跳回牌桌）。</summary>
    public string PokerSceneName { get { return pokerSceneName; } }

    /// <summary>
    /// 开一局全新的 run：筹码回初始值、关卡回 1、清掉所有跨场景返回值。
    /// 教程结束/跳过之后调用一次，避免教程里的筹码影响正式局。
    /// </summary>
    public void StartNewRun()
    {
        PlayerChips = startingChips;
        EnemyChips = startingEnemyChips;
        levelStartEnemyChips = EnemyChips;
        CurrentLevel = 1;
        AwaitingNextLevel = false;
        IsGameOver = false;
        ReturnedFromCombat = false;
        ReturnedFromShop = false;
        pendingHandoff = null;
        pendingResult = null;
        AdvanceToNextEncounter();
    }

    /// <summary>
    /// 推进到下一个敌人：生成随机敌人并记录下来。
    /// 由扑克场景的 Bootstrap 在合适的时机（牌局结束时）调用。
    /// </summary>
    public void AdvanceToNextEncounter()
    {
        CurrentEnemy = EnemyGenerator.GenerateRandom();
        Debug.Log($"[GameFlow] 推进到新敌人: {EnemyGenerator.Describe(CurrentEnemy)}");
    }

    public void IncrementShopVisit()
    {
        ShopVisitCount++;
        Debug.Log($"[GameFlow] 商店访问次数 = {ShopVisitCount}");
    }
}
