using System.Collections;
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
    private Weapon itemWeapon;
    private Health markedTarget;
    private float markedEndTime;
    private Canvas markedCanvas;
    private RectTransform markedLabelRect;
    private TextMeshProUGUI markedLabel;
    private Canvas damageNumberCanvas;
    private RectTransform damageNumberCanvasRect;
    private Renderer[] markedTargetRenderers;
    private Material[][] markedOriginalMaterials;
    private Material markedRevealMaterial;
    private Coroutine lastStandSlowRoutine;
    private float lastStandPreviousTimeScale;
    private float lastStandAppliedTimeScale;
    private int lifeStealDamageRemainder;
    private bool hasLifeSteal;
    private bool hasMarked;
    private bool hasLastStand;
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

        ApplyStats(playerHealth, playerController, handoff.playerStats, 100, handoff.playerFpsItems);
        FPS_HUD playerHud = playerController != null
            ? playerController.GetComponent<FPS_HUD>() : null;
        if (playerHud != null)
            playerHud.SetPokerBuffs(handoff.playerStats);
        FpsOwnedItemsHud.SetCombatItems(handoff.playerFpsItems);
        ConfigurePlayerItemEffects(handoff.playerFpsItems);

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

        if (enemyHealth != null && extraLevels > 0)
        {
            long scaledHealth = (long)enemyHealth.MaxHealth + 25L * extraLevels;
            enemyHealth.SetMaxHealth((int)System.Math.Min(int.MaxValue, scaledHealth));
        }

        FPSAIEngine enemyAI = enemyInstance.GetComponentInChildren<FPSAIEngine>();
        if (enemyAI == null || enemyAI.personality == null)
            return;

        runtimeEnemyPersonality = FpsAiDifficulty.CreateForLevel(enemyAI.personality, currentLevel);
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
        string levelLine = $"LV {currentLevel}  {FpsAiDifficulty.TierName(currentLevel)}";
        if (extraLevels > 0)
            levelLine += $"  +{25L * extraLevels} HP";
        buffsText.text = levelLine + "\n" + FpsAiDifficulty.TierTrait(currentLevel) +
            "\nPOKER " + FPS_HUD.FormatPokerBuffs(enemyPokerStats);

        RefreshEnemyHealthBar();
    }

    private void ConfigurePlayerItemEffects(FpsItemDefinition[] items)
    {
        hasLifeSteal = FpsItemCombatHandler.HasItem(items, ItemId.LifeSteal);
        hasMarked = FpsItemCombatHandler.HasItem(items, ItemId.Marked);
        hasLastStand = FpsItemCombatHandler.HasItem(items, ItemId.LastStand);
        itemWeapon = playerController != null ? playerController.EquippedWeapon : null;

        if (itemWeapon != null)
        {
            itemWeapon.SetGoldenBullet(
                FpsItemCombatHandler.HasItem(items, ItemId.GoldenBullet));
            itemWeapon.OnDamageNumber += HandleDamageNumber;
            if (hasLifeSteal)
                itemWeapon.OnDamageDealt += HandleDamageDealt;
            if (hasMarked)
                itemWeapon.OnHeadshotDealt += HandleHeadshotDealt;
        }

        if (hasLastStand && playerHealth != null)
            playerHealth.OnLethalDamage += HandleLethalDamage;
    }

    private void HandleDamageDealt(int amount)
    {
        if (!hasLifeSteal || playerHealth == null || amount <= 0)
            return;

        long accumulatedDamage = (long)lifeStealDamageRemainder + amount;
        long healing = accumulatedDamage / 10L * 2L;
        lifeStealDamageRemainder = (int)(accumulatedDamage % 10L);
        if (healing > 0L)
            playerHealth.Heal((int)System.Math.Min(int.MaxValue, healing));
    }

    private void HandleDamageNumber(Health target, int amount, bool headshot)
    {
        if (target == null || amount <= 0)
            return;

        EnsureDamageNumberCanvas();
        if (damageNumberCanvas == null)
            return;

        GameObject labelObject = new GameObject("Damage Number",
            typeof(RectTransform), typeof(CanvasRenderer),
            typeof(TextMeshProUGUI), typeof(CanvasGroup));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.SetParent(damageNumberCanvas.transform, false);
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(180f, 64f);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = "-" + amount;
        label.fontSize = headshot ? 52f : 44f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = headshot
            ? new Color(1f, 0.66f, 0.12f, 1f)
            : new Color(0.9f, 0.98f, 0.97f, 1f);
        label.outlineWidth = 0.28f;
        label.outlineColor = new Color(0f, 0.025f, 0.035f, 1f);
        label.raycastTarget = false;

        Vector3 origin = target.transform.position;
        float horizontalOffset = Random.Range(-24f, 24f);
        StartCoroutine(AnimateDamageNumber(labelRect,
            labelObject.GetComponent<CanvasGroup>(), target, origin, horizontalOffset));
    }

    private void EnsureDamageNumberCanvas()
    {
        if (damageNumberCanvas != null)
            return;

        GameObject canvasObject = new GameObject("FPS Damage Numbers",
            typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        damageNumberCanvas = canvasObject.GetComponent<Canvas>();
        damageNumberCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        damageNumberCanvas.overrideSorting = true;
        damageNumberCanvas.sortingOrder = 210;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        damageNumberCanvasRect = canvasObject.transform as RectTransform;
    }

    private IEnumerator AnimateDamageNumber(RectTransform labelRect,
        CanvasGroup group, Health target, Vector3 origin, float horizontalOffset)
    {
        const float duration = 0.9f;
        float elapsed = 0f;
        while (elapsed < duration && labelRect != null)
        {
            float progress = Mathf.Clamp01(elapsed / duration);
            Vector3 targetPosition = target != null
                ? target.transform.position : origin;
            Vector3 worldPosition = targetPosition +
                Vector3.up * (2.15f + progress * 0.65f);
            Camera viewer = GetPlayerCamera();
            if (viewer != null)
            {
                Vector3 screenPoint = viewer.WorldToScreenPoint(worldPosition);
                if (screenPoint.z > 0f &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        damageNumberCanvasRect, screenPoint, null, out Vector2 localPoint))
                {
                    labelRect.anchoredPosition = localPoint +
                        Vector2.right * horizontalOffset;
                    group.alpha = 1f - progress;
                    labelRect.gameObject.SetActive(true);
                }
                else
                {
                    group.alpha = 0f;
                }
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (labelRect != null)
            Destroy(labelRect.gameObject);
    }

    private Camera GetPlayerCamera()
    {
        Transform cameraTransform = playerController != null
            ? playerController.CameraTransform : null;
        Camera viewer = cameraTransform != null
            ? cameraTransform.GetComponentInChildren<Camera>() : null;
        return viewer != null ? viewer : Camera.main;
    }

    private void HandleHeadshotDealt(Health target)
    {
        if (!hasMarked || target == null || target.IsDead)
            return;

        if (markedTarget != target)
        {
            RestoreMarkedTargetMaterials();
            markedTarget = target;
            ApplyMarkedTargetMaterials(target);
        }

        markedEndTime = Time.unscaledTime + 10f;
        EnsureMarkedTargetIndicator();
        RefreshMarkedTargetIndicator();
    }

    private bool HandleLethalDamage(int incomingDamage)
    {
        if (!hasLastStand)
            return false;

        hasLastStand = false;
        lastStandSlowRoutine = StartCoroutine(SlowTimeForLastStand());
        return true;
    }

    private IEnumerator SlowTimeForLastStand()
    {
        lastStandPreviousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        lastStandAppliedTimeScale = Mathf.Min(lastStandPreviousTimeScale, 0.2f);
        Time.timeScale = lastStandAppliedTimeScale;

        float remaining = 10f;
        while (remaining > 0f)
        {
            yield return null;
            if (Time.timeScale <= 0f)
                continue;
            if (!Mathf.Approximately(Time.timeScale, lastStandAppliedTimeScale))
                Time.timeScale = lastStandAppliedTimeScale;
            remaining -= Time.unscaledDeltaTime;
        }

        Time.timeScale = lastStandPreviousTimeScale;
        lastStandSlowRoutine = null;
    }

    private void StopLastStandSlow()
    {
        if (lastStandSlowRoutine == null)
            return;

        StopCoroutine(lastStandSlowRoutine);
        lastStandSlowRoutine = null;
        if (Time.timeScale > 0f)
            Time.timeScale = lastStandPreviousTimeScale;
    }

    private void EnsureMarkedTargetIndicator()
    {
        if (markedCanvas != null)
            return;

        GameObject canvasObject = new GameObject("Marked Target Overlay",
            typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        markedCanvas = canvasObject.GetComponent<Canvas>();
        markedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        markedCanvas.overrideSorting = true;
        markedCanvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        GameObject labelObject = new GameObject("Marked Target Label",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        markedLabelRect = labelObject.GetComponent<RectTransform>();
        markedLabelRect.SetParent(markedCanvas.transform, false);
        markedLabelRect.anchorMin = markedLabelRect.anchorMax = new Vector2(0.5f, 0.5f);
        markedLabelRect.pivot = new Vector2(0.5f, 0.5f);
        markedLabelRect.sizeDelta = new Vector2(260f, 48f);

        markedLabel = labelObject.GetComponent<TextMeshProUGUI>();
        markedLabel.fontSize = 22f;
        markedLabel.fontStyle = FontStyles.Bold;
        markedLabel.alignment = TextAlignmentOptions.Center;
        markedLabel.color = new Color(1f, 0.25f, 0.12f, 1f);
        markedLabel.outlineWidth = 0.25f;
        markedLabel.outlineColor = Color.black;
        markedLabel.raycastTarget = false;
        markedLabel.text = "MARKED";
        markedLabelRect.gameObject.SetActive(false);
    }

    private void ApplyMarkedTargetMaterials(Health target)
    {
        if (markedRevealMaterial == null)
        {
            Shader revealShader = Resources.Load<Shader>("MarkedReveal");
            if (revealShader == null)
                return;

            markedRevealMaterial = new Material(revealShader);
            markedRevealMaterial.SetColor("_BaseColor",
                new Color(1f, 0.12f, 0.04f, 0.82f));
        }

        Renderer[] allRenderers = target.GetComponentsInChildren<Renderer>(true);
        var bodyRenderers = new System.Collections.Generic.List<Renderer>(allRenderers.Length);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] is MeshRenderer || allRenderers[i] is SkinnedMeshRenderer)
                bodyRenderers.Add(allRenderers[i]);
        }

        markedTargetRenderers = bodyRenderers.ToArray();
        markedOriginalMaterials = new Material[markedTargetRenderers.Length][];
        for (int i = 0; i < markedTargetRenderers.Length; i++)
        {
            Renderer targetRenderer = markedTargetRenderers[i];
            if (targetRenderer == null)
                continue;

            Material[] originalMaterials = targetRenderer.sharedMaterials;
            markedOriginalMaterials[i] = originalMaterials;
            Material[] revealMaterials = new Material[originalMaterials.Length];
            for (int j = 0; j < revealMaterials.Length; j++)
                revealMaterials[j] = markedRevealMaterial;
            targetRenderer.sharedMaterials = revealMaterials;
        }
    }

    private void RestoreMarkedTargetMaterials()
    {
        if (markedTargetRenderers != null && markedOriginalMaterials != null)
        {
            int rendererCount = Mathf.Min(
                markedTargetRenderers.Length, markedOriginalMaterials.Length);
            for (int i = 0; i < rendererCount; i++)
            {
                if (markedTargetRenderers[i] != null && markedOriginalMaterials[i] != null)
                    markedTargetRenderers[i].sharedMaterials = markedOriginalMaterials[i];
            }
        }

        markedTargetRenderers = null;
        markedOriginalMaterials = null;
    }

    private void RefreshMarkedTargetIndicator()
    {
        if (markedLabelRect == null)
            return;

        if (markedTarget == null || markedTarget.IsDead ||
            Time.unscaledTime >= markedEndTime)
        {
            markedLabelRect.gameObject.SetActive(false);
            RestoreMarkedTargetMaterials();
            markedTarget = null;
            return;
        }

        Camera viewer = GetPlayerCamera();
        if (viewer == null)
        {
            markedLabelRect.gameObject.SetActive(false);
            return;
        }

        Vector3 worldPosition = markedTarget.transform.position + Vector3.up * 2f;
        Vector3 viewport = viewer.WorldToViewportPoint(worldPosition);
        if (viewport.z < 0f)
        {
            viewport.x = 1f - viewport.x;
            viewport.y = 1f - viewport.y;
        }

        viewport.x = Mathf.Clamp(viewport.x, 0.04f, 0.96f);
        viewport.y = Mathf.Clamp(viewport.y, 0.06f, 0.94f);
        Vector2 screenPoint = new Vector2(
            viewport.x * Screen.width,
            viewport.y * Screen.height);
        RectTransform canvasRect = markedCanvas.transform as RectTransform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, null, out Vector2 localPoint))
        {
            markedLabelRect.anchoredPosition = localPoint;
            float remaining = Mathf.Max(0f, markedEndTime - Time.unscaledTime);
            markedLabel.text = $"MARKED  {remaining:0.0}s";
            markedLabelRect.gameObject.SetActive(true);
        }
    }

    private void LateUpdate()
    {
        RefreshEnemyHealthBar();
        RefreshMarkedTargetIndicator();
    }

    private void RefreshEnemyHealthBar()
    {
        if (enemyHealth == null || enemyHealthBar == null || enemyHealthFill == null)
            return;

        if (resolved || enemyHealth.IsDead)
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
        int minimumHealth = 1,
        FpsItemDefinition[] fpsItems = null)
    {
        CombatStats itemStats = FpsItemCombatHandler.GetBonusStats(fpsItems);

        if (health != null)
        {
            int bonusHealth = Mathf.RoundToInt(stats.health + itemStats.health);
            long totalHealth = (long)Mathf.Max(minimumHealth, health.MaxHealth) + bonusHealth;
            health.SetMaxHealth((int)System.Math.Max(minimumHealth,
                System.Math.Min(int.MaxValue, totalHealth)));
        }

        if (controller == null)
            return;

        ApplyWeaponStats(controller.EquippedWeapon, stats, itemStats, fpsItems);
        ApplyMovementStats(controller, stats, itemStats);
    }

    private void ApplyWeaponStats(Weapon weapon, CombatStats stats,
        CombatStats itemStats, FpsItemDefinition[] fpsItems)
    {
        if (weapon == null || weapon.Settings == null)
            return;

        WeaponSettings runtimeSettings =
            ScriptableObject.Instantiate(weapon.Settings);

        int baseDamage = Mathf.Max(0,
            weapon.Settings.damage + Mathf.RoundToInt(itemStats.attack));
        runtimeSettings.damage = Mathf.Max(0,
            baseDamage + Mathf.RoundToInt(baseDamage * stats.attack / 100f));

        runtimeSettings.headshotMultiplier = FpsItemCombatHandler.GetHeadshotMultiplier(
            runtimeSettings.headshotMultiplier, fpsItems);

        // WeaponSettings.fireRate is the cooldown between shots
        // (smaller = faster). CombatStats.fireRate represents
        // attack SPEED (bigger = faster), so this scales inversely
        // rather than additively.
        float attackSpeed = Mathf.Max(0.01f,
            1f + (stats.fireRate + itemStats.fireRate) / 100f);
        runtimeSettings.fireRate =
            Mathf.Max(
                0.02f,
                weapon.Settings.fireRate / attackSpeed
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
        CombatStats stats,
        CombatStats itemStats)
    {
        if (controller.MovementSettings == null)
            return;

        FpsMovementSettings runtimeSettings =
            ScriptableObject.Instantiate(controller.MovementSettings);

        runtimeSettings.moveSpeed += stats.moveSpeed + itemStats.moveSpeed;

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
        StopLastStandSlow();

        CombatWinner winner =
            deceased == playerHealth
                ? CombatWinner.Enemy
                : CombatWinner.Player;

        EndSubscriptions();

        CombatEndSequence endSequence = gameObject.AddComponent<CombatEndSequence>();
        endSequence.Begin(winner, playerHealth, playerController, enemyHealth,
            currentPot, () => ReportResult(winner));
    }

    private void ReportResult(CombatWinner winner)
    {
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

    private void EndItemEffectSubscriptions()
    {
        if (playerHealth != null)
            playerHealth.OnLethalDamage -= HandleLethalDamage;
        if (itemWeapon != null)
        {
            itemWeapon.OnDamageNumber -= HandleDamageNumber;
            itemWeapon.OnDamageDealt -= HandleDamageDealt;
            itemWeapon.OnHeadshotDealt -= HandleHeadshotDealt;
        }
    }

    private void OnDestroy()
    {
        EndSubscriptions();
        EndItemEffectSubscriptions();
        RestoreMarkedTargetMaterials();
        StopLastStandSlow();
        if (markedRevealMaterial != null)
            Destroy(markedRevealMaterial);
        if (runtimeEnemyPersonality != null)
            Destroy(runtimeEnemyPersonality);
    }
}
