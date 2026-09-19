using UnityEngine;

/// <summary>
/// Lives only in the combat scene. Reads the pending handoff from
/// GameFlowManager, applies the combat stats to the player, spawns
/// the correct enemy for the archetype, then watches for either
/// fighter's death to resolve the duel and send control back to
/// poker.
///
/// This is the scene-local replacement for the earlier standalone
/// CombatEncounterManager - since it only ever needs to exist for
/// the lifetime of one duel, letting the scene unload clean it up
/// is simpler than a persistent manager tracking subscriptions.
/// </summary>
public class CombatSceneController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Health playerHealth;
    [SerializeField] private FpsCharacterController playerController;

    [Header("Enemy Spawning")]
    [SerializeField] private Transform enemySpawnPoint;

    [System.Serializable]
    public struct EnemyPrefabEntry
    {
        [Tooltip("Must match EnemyAIParameters.combatStyle values " +
                 "from EnemyGenerator - \"Melee\", \"Ranged\", or " +
                 "\"Dodge\" - not the FPSPersonality enum names.")]
        public string combatStyleKey;

        public GameObject prefab;
    }

    [SerializeField] private EnemyPrefabEntry[] enemyPrefabs;

    [Header("Debug (used only if this scene is opened directly)")]
    [SerializeField] private CombatStats debugPlayerStats =
        new CombatStats { attack = 10, health = 20, fireRate = 5, moveSpeed = 2 };

    [SerializeField] private CombatStats debugEnemyStats =
        new CombatStats { attack = 10, health = 20, fireRate = 5, moveSpeed = 2 };

    [SerializeField] private int debugPot = 20;
    [SerializeField] private string debugEnemyCombatStyle = "Ranged";

    private int currentPot;
    private Health enemyHealth;
    private bool resolved;

    private void Start()
    {
        CombatHandoff handoff = ResolveHandoff();

        currentPot = handoff.pot;

        ApplyStats(playerHealth, playerController, handoff.playerStats);

        SpawnEnemy(handoff);
    }

    // =========================================================
    // HANDOFF
    // =========================================================

    private CombatHandoff ResolveHandoff()
    {
        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.TryConsumeCombatHandoff(out CombatHandoff handoff))
        {
            return handoff;
        }

        Debug.LogWarning(
            "CombatSceneController: no pending handoff from " +
            "GameFlowManager - using debug values. Expected if " +
            "you entered this scene directly rather than via poker."
        );

        return new CombatHandoff
        {
            playerStats = debugPlayerStats,
            enemyStats = debugEnemyStats,
            pot = debugPot,
            enemyName = "Debug Enemy",
            enemyCombatStyle = debugEnemyCombatStyle
        };
    }

    // =========================================================
    // ENEMY SPAWNING
    // =========================================================

    private void SpawnEnemy(CombatHandoff handoff)
    {
        GameObject prefab = FindPrefabFor(handoff.enemyCombatStyle);

        if (prefab == null)
        {
            Debug.LogError(
                $"CombatSceneController: no enemy prefab " +
                $"registered for combat style " +
                $"'{handoff.enemyCombatStyle}'."
            );
            return;
        }

        Vector3 position =
            enemySpawnPoint != null
                ? enemySpawnPoint.position
                : Vector3.zero;

        Quaternion rotation =
            enemySpawnPoint != null
                ? enemySpawnPoint.rotation
                : Quaternion.identity;

        GameObject enemyInstance =
            Instantiate(prefab, position, rotation);

        enemyInstance.name = handoff.enemyName;

        enemyHealth = enemyInstance.GetComponent<Health>();

        FpsCharacterController enemyController =
            enemyInstance.GetComponent<FpsCharacterController>();

        ApplyStats(enemyHealth, enemyController, handoff.enemyStats);

        if (playerHealth != null)
            playerHealth.OnDeath += HandleDeath;

        if (enemyHealth != null)
            enemyHealth.OnDeath += HandleDeath;
    }

    private GameObject FindPrefabFor(string combatStyleKey)
    {
        if (enemyPrefabs == null)
            return null;

        foreach (EnemyPrefabEntry entry in enemyPrefabs)
        {
            if (entry.combatStyleKey == combatStyleKey)
                return entry.prefab;
        }

        return null;
    }

    // =========================================================
    // STAT APPLICATION
    // =========================================================
    //
    // These formulas are placeholders - swap them for whatever
    // your group settles on for actual balance. What matters
    // structurally is cloning the ScriptableObject settings rather
    // than editing the shared asset directly: Weapon and
    // FpsCharacterController both read weaponSettings/
    // movementSettings by reference every frame, so mutating the
    // original asset would persist after play mode stops and would
    // affect every other instance sharing it.

    private void ApplyStats(
        Health health,
        FpsCharacterController controller,
        CombatStats stats)
    {
        if (health != null)
        {
            int bonusHealth = Mathf.RoundToInt(stats.health);
            health.SetMaxHealth(health.MaxHealth + bonusHealth);
        }

        if (controller == null)
            return;

        ApplyWeaponStats(controller.EquippedWeapon, stats);
        ApplyMovementStats(controller, stats);
    }

    private void ApplyWeaponStats(Weapon weapon, CombatStats stats)
    {
        if (weapon == null || weapon.Settings == null)
            return;

        WeaponSettings runtimeSettings =
            ScriptableObject.Instantiate(weapon.Settings);

        runtimeSettings.damage +=
            Mathf.RoundToInt(stats.attack);

        // WeaponSettings.fireRate is the cooldown between shots
        // (smaller = faster). CombatStats.fireRate represents
        // attack SPEED (bigger = faster), so this scales inversely
        // rather than additively.
        runtimeSettings.fireRate =
            Mathf.Max(
                0.02f,
                weapon.Settings.fireRate /
                (1f + stats.fireRate / 100f)
            );

        weapon.SetSettings(runtimeSettings);
    }

    private void ApplyMovementStats(
        FpsCharacterController controller,
        CombatStats stats)
    {
        if (controller.MovementSettings == null)
            return;

        FpsMovementSettings runtimeSettings =
            ScriptableObject.Instantiate(controller.MovementSettings);

        runtimeSettings.moveSpeed += stats.moveSpeed;

        controller.SetMovementSettings(runtimeSettings);
    }

    // =========================================================
    // RESOLUTION
    // =========================================================

    private void HandleDeath(Health deceased)
    {
        if (resolved)
            return;

        resolved = true;

        CombatWinner winner =
            deceased == playerHealth
                ? CombatWinner.Enemy
                : CombatWinner.Player;

        EndSubscriptions();

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.ReportCombatResult(
                winner,
                currentPot
            );
        }
        else
        {
            Debug.LogWarning(
                "CombatSceneController: no GameFlowManager to " +
                "report the result to - the poker scene won't " +
                "receive the outcome."
            );
        }
    }

    private void EndSubscriptions()
    {
        if (playerHealth != null)
            playerHealth.OnDeath -= HandleDeath;

        if (enemyHealth != null)
            enemyHealth.OnDeath -= HandleDeath;
    }

    private void OnDestroy()
    {
        EndSubscriptions();
    }
}