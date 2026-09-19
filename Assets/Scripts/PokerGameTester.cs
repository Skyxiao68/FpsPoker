using UnityEngine;

/// <summary>
/// Test/bootstrap entry point for the poker game.
/// Creates the game manager and UI, generates enemy parameters,
/// starts new hands, and provides keyboard shortcuts for testing actions.
/// </summary>
public class PokerGameTester : MonoBehaviour
{
    // Reference to the core poker game logic.
    private PokerGameManager poker;

    // Reference to the dynamically built poker UI.
    private PokerUI ui;

    // Parameters that define the current enemy AI behavior and identity.
    private EnemyAIParameters enemyParams;

    /// <summary>
    /// Unity Start method.
    /// Sets up the game manager, UI, enemy, and starts the first hand.
    /// </summary>
    void Start()
    {
        // Add the game manager component to this GameObject.
        poker = gameObject.AddComponent<PokerGameManager>();

        // Create a separate GameObject for the UI and attach the PokerUI component.
        GameObject uiGO = new GameObject("PokerUI");
        uiGO.transform.SetParent(transform);
        ui = uiGO.AddComponent<PokerUI>();

        // Log the result and chip counts whenever a hand ends.
        poker.OnHandEnded += () =>
        {
            Debug.Log(
                $"Match Over：{poker.Result} | Player {poker.PlayerChips} | Enemy {poker.EnemyChips}"
            );
        };

        // Generate the first enemy for the first hand.
        enemyParams = EnemyGenerator.GenerateRandom();
        Debug.Log($"[Enemy] {EnemyGenerator.Describe(enemyParams)}");

        // Initialize the UI and connect the "Next Game" button to StartNewHand.
        ui.Initialize(poker);
        ui.SetNextHandCallback(StartNewHand);

        // Start the first hand.
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
