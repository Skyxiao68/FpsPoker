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
    [SerializeField] private string pokerSceneName = "PokerScene";

    [Tooltip("Exact scene name as it appears in Build Settings.")]
    [SerializeField] private string combatSceneName = "CombatScene";

    [Header("Economy")]
    [Tooltip("Used only the very first time the run starts.")]
    [SerializeField] private int startingChips = 1000;

    /// <summary>
    /// The player's persistent chip total. -1 means "not yet
    /// initialised" so the very first read seeds startingChips
    /// rather than 0.
    /// </summary>
    public int PlayerChips { get; set; } = -1;

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
            PlayerChips = startingChips;
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
        CombatStats playerStats,
        CombatStats enemyStats,
        int pot,
        string enemyName,
        string enemyCombatStyle)
    {
        PlayerChips = currentPlayerChips;

        pendingHandoff = new CombatHandoff
        {
            playerStats = playerStats,
            enemyStats = enemyStats,
            pot = pot,
            enemyName = enemyName,
            enemyCombatStyle = enemyCombatStyle
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
        pendingResult = new PendingResult
        {
            winner = winner,
            pot = pot
        };

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
}