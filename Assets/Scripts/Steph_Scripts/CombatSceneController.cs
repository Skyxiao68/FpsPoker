using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [Min(1)]
    [InspectorName("Enemy Level")]
    [Tooltip("Enemy level when testing the FPS scene with debug values. Level 1 uses base health and accuracy; each later level adds 25 health and improves aim.")]
    [SerializeField] private int debugLevel = 1;
    [SerializeField] private string debugEnemyCombatStyle = "Ranged";
    [Tooltip("Only used when this combat scene is opened directly.")]
    [SerializeField] private FpsItemDefinition[] debugFpsItems;

    private int currentPot;
    private int currentLevel;
    private Health enemyHealth;
    private RectTransform enemyHealthBar;
    private RectTransform enemyHealthFill;
    private CombatStats enemyPokerStats;
    private FPSPersonality runtimeEnemyPersonality;
    private bool resolved;

    private void Start()
    {
        CombatHandoff handoff = ResolveHandoff(out bool usingDebugValues);

        currentPot = handoff.pot;
        currentLevel = !usingDebugValues && GameFlowManager.Instance != null
            ? Mathf.Max(1, GameFlowManager.Instance.CurrentLevel)
            : Mathf.Max(1, debugLevel);

        CombatStats playerStats = FpsItemCombatHandler.Apply(
            handoff.playerStats, handoff.playerFpsItems);

        if (handoff.playerFpsItems != null)
        {
            if (handoff.playerFpsItems.Length > 0)
                Debug.Log($"[FpsItem] Player stats: {handoff.playerStats} -> {playerStats}");

            for (int i = 0; i < handoff.playerFpsItems.Length; i++)
            {
                FpsItemDefinition item = handoff.playerFpsItems[i];
                if (item != null)
                    Debug.Log($"[FpsItem] Applied {item.DisplayName} ({item.FpsItemId})");
            }
        }

        ApplyStats(playerHealth, playerController, playerStats, 100);
        FPS_HUD playerHud = playerController != null
            ? playerController.GetComponent<FPS_HUD>() : null;
        if (playerHud != null)
            playerHud.SetPokerBuffs(handoff.playerStats);
        FpsOwnedItemsHud.SetCombatItems(handoff.playerFpsItems);

        SpawnEnemy(handoff);
    }

    // =========================================================
    // HANDOFF
    // =========================================================

    private CombatHandoff ResolveHandoff(out bool usingDebugValues)
    {
        if (GameFlowManager.Instance != null &&
            GameFlowManager.Instance.TryConsumeCombatHandoff(out CombatHandoff handoff))
        {
            usingDebugValues = false;
            return handoff;
        }

        usingDebugValues = true;
        Debug.LogWarning(
            "CombatSceneController: no pending handoff from " +
            "GameFlowManager - using debug values. Expected if " +
            "you entered this scene directly rather than via poker."
        );

        return new CombatHandoff
        {
            playerStats = debugPlayerStats,
            enemyStats = debugEnemyStats,
            playerFpsItems = debugFpsItems,
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
            CombatStartSequence missingEnemySequence = FindAnyObjectByType<CombatStartSequence>();
            if (missingEnemySequence != null)
                missingEnemySequence.Begin(playerController, null);
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
        ApplyEnemyLevelProgression(enemyInstance);
        enemyPokerStats = handoff.enemyStats;
        CreateEnemyHealthBar(enemyInstance.transform);

        // The barrier owns the countdown. Give it the fully configured
        // fighters so neither can shoot during the preparation phase.
        CombatStartSequence startSequence = FindAnyObjectByType<CombatStartSequence>();
        if (startSequence != null)
            startSequence.Begin(playerController, enemyInstance);

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

    private void ApplyEnemyLevelProgression(GameObject enemyInstance)
    {
        int extraLevels = Mathf.Max(0, currentLevel - 1);
        if (extraLevels == 0)
            return;

        if (enemyHealth != null)
        {
            long scaledHealth = (long)enemyHealth.MaxHealth + 25L * extraLevels;
            enemyHealth.SetMaxHealth((int)System.Math.Min(int.MaxValue, scaledHealth));
        }

        FPSAIEngine enemyAI = enemyInstance.GetComponentInChildren<FPSAIEngine>();
        if (enemyAI == null || enemyAI.personality == null)
            return;

        // Clone the prefab's personality so later levels never change the
        // shared asset or another enemy's original tuning.
        FPSPersonality original = enemyAI.personality;
        runtimeEnemyPersonality = ScriptableObject.Instantiate(original);
        runtimeEnemyPersonality.aimError = Mathf.Max(
            Mathf.Min(0.3f, original.aimError),
            original.aimError * Mathf.Pow(0.88f, extraLevels));
        runtimeEnemyPersonality.fireAngleTolerance = Mathf.Max(
            Mathf.Min(2.5f, original.fireAngleTolerance),
            original.fireAngleTolerance * Mathf.Pow(0.96f, extraLevels));
        enemyAI.personality = runtimeEnemyPersonality;
        enemyAI.DistributePersonality();
    }

    private void CreateEnemyHealthBar(Transform enemyRoot)
    {
        if (enemyHealth == null)
            return;

        GameObject barObject = new GameObject("Enemy Health Bar",
            typeof(RectTransform), typeof(Canvas));
        enemyHealthBar = barObject.GetComponent<RectTransform>();
        enemyHealthBar.SetParent(enemyRoot, false);
        enemyHealthBar.sizeDelta = new Vector2(120f, 18f);
        enemyHealthBar.localScale = Vector3.one * 0.01f;
        Canvas canvas = barObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 10;

        GameObject backgroundObject = new GameObject("Background",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform background = backgroundObject.GetComponent<RectTransform>();
        background.SetParent(enemyHealthBar, false);
        background.anchorMin = background.anchorMax = new Vector2(0.5f, 0.5f);
        background.sizeDelta = new Vector2(120f, 18f);
        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        backgroundImage.raycastTarget = false;

        GameObject fillObject = new GameObject("Health Fill",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        enemyHealthFill = fillObject.GetComponent<RectTransform>();
        enemyHealthFill.SetParent(background, false);
        enemyHealthFill.anchorMin = enemyHealthFill.anchorMax = new Vector2(0f, 0.5f);
        enemyHealthFill.pivot = new Vector2(0f, 0.5f);
        enemyHealthFill.anchoredPosition = new Vector2(2f, 0f);
        enemyHealthFill.sizeDelta = new Vector2(116f, 14f);
        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(0.9f, 0.2f, 0.15f, 1f);
        fillImage.raycastTarget = false;

        GameObject buffsObject = new GameObject("Enemy Buffs",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform buffsRect = buffsObject.GetComponent<RectTransform>();
        buffsRect.SetParent(enemyHealthBar, false);
        buffsRect.anchorMin = buffsRect.anchorMax = new Vector2(0.5f, 0.5f);
        buffsRect.pivot = new Vector2(0.5f, 0.5f);
        buffsRect.anchoredPosition = new Vector2(0f, 40f);
        buffsRect.sizeDelta = new Vector2(240f, 56f);
        TextMeshProUGUI buffsText = buffsObject.GetComponent<TextMeshProUGUI>();
        buffsText.fontSize = 12f;
        buffsText.alignment = TextAlignmentOptions.Bottom;
        buffsText.color = new Color(1f, 0.9f, 0.7f, 1f);
        buffsText.raycastTarget = false;
        int extraLevels = Mathf.Max(0, currentLevel - 1);
        string levelBuff = extraLevels > 0
            ? $"LV {currentLevel}  +{25L * extraLevels} HP, improved aim"
            : "LV 1";
        buffsText.text = levelBuff + "\nPOKER " +
            FPS_HUD.FormatPokerBuffs(enemyPokerStats);

        RefreshEnemyHealthBar();
    }

    private void LateUpdate()
    {
        RefreshEnemyHealthBar();
    }

    private void RefreshEnemyHealthBar()
    {
        if (enemyHealth == null || enemyHealthBar == null || enemyHealthFill == null)
            return;

        if (enemyHealth.IsDead)
        {
            enemyHealthBar.gameObject.SetActive(false);
            return;
        }

        enemyHealthFill.sizeDelta = new Vector2(
            116f * Mathf.Clamp01(enemyHealth.NormalizedHealth), 14f);

        CharacterController body = enemyHealth.GetComponent<CharacterController>();
        Collider fallback = body == null ? enemyHealth.GetComponentInChildren<Collider>() : null;
        float top = body != null ? body.bounds.max.y
            : fallback != null ? fallback.bounds.max.y
            : enemyHealth.transform.position.y + 2f;
        enemyHealthBar.position = enemyHealth.transform.position +
            Vector3.up * (top - enemyHealth.transform.position.y + 0.35f);

        Transform viewer = playerController != null
            ? playerController.CameraTransform
            : Camera.main != null ? Camera.main.transform : null;
        if (viewer != null)
            enemyHealthBar.rotation = viewer.rotation;
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
        CombatStats stats,
        int minimumHealth = 1)
    {
        if (health != null)
        {
            int bonusHealth = Mathf.RoundToInt(stats.health);
            long totalHealth = (long)Mathf.Max(minimumHealth, health.MaxHealth) + bonusHealth;
            health.SetMaxHealth((int)System.Math.Max(minimumHealth,
                System.Math.Min(int.MaxValue, totalHealth)));
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

        // Clubs/fire-rate buff can expand the magazine, but a small buff
        // must never replace the weapon's authored capacity.
        int baseCapacity = Mathf.Max(1, weapon.Settings.magazineSize);
        int buffCapacity = stats.fireRate > 0f
            ? Mathf.FloorToInt(stats.fireRate) : 0;
        runtimeSettings.magazineSize = Mathf.Max(baseCapacity, buffCapacity);

        // Each extra shot adds only a small reload penalty. The authored
        // reload time remains the baseline for this weapon.
        runtimeSettings.reloadTime = weapon.Settings.reloadTime +
            0.12f * (runtimeSettings.magazineSize - baseCapacity);

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
        if (runtimeEnemyPersonality != null)
            Destroy(runtimeEnemyPersonality);
    }
}
