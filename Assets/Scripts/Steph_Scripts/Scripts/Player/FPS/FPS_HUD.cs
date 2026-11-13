using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FPS_HUD : MonoBehaviour
{
    public Health health;
    public TextMeshProUGUI healthText;
    public GameObject reloadAnim;
    public GameObject damageFlash;
    private FpsCharacterController controller;
    private Transform hudCanvasRoot;
    private GameObject healthAmmoPanel;
    private Image healthBarFill;
    private TextMeshProUGUI ammoHeadingText;
    private TextMeshProUGUI ammoText;
    private TextMeshProUGUI countdownText;
    private GameObject pokerBuffPanel;
    private TextMeshProUGUI pokerBuffEmptyText;
    private readonly GameObject[] pokerBuffTiles = new GameObject[4];
    private readonly Image[] pokerBuffIcons = new Image[4];
    private readonly TextMeshProUGUI[] pokerBuffNames = new TextMeshProUGUI[4];
    private readonly TextMeshProUGUI[] pokerBuffValues = new TextMeshProUGUI[4];
    private CombatStats pokerBuffStats;
    private Weapon observedWeapon;
    private CanvasGroup damageFlashGroup;
    private CanvasGroup reloadAnimGroup;
    private RectTransform hitMarker;
    private CanvasGroup hitMarkerGroup;
    private readonly List<Image> hitMarkerArms = new List<Image>(4);
    private float hitMarkerEndTime;
    private const float HitMarkerDuration = 0.15f;
    private const float DamageFlashDuration = 0.3f;
    private const float ReloadFadeDuration = 0.3f;
    private float damageFlashRemaining;
    private bool wasReloading;
    private bool fadingReload;
    private string lastHealthDisplay;
    private string lastAmmoDisplay;
    private bool wasPreparing;
    private float duelCalloutEndTime;

    void OnEnable()
    {
        if (health == null)
            health = GetComponent<Health>();
        controller = GetComponent<FpsCharacterController>();
        if (health != null)
            health.OnDamaged += UpdateHealth;
        SetupEffect(damageFlash, ref damageFlashGroup);
        SetupEffect(reloadAnim, ref reloadAnimGroup);
        if (healthText != null)
        {
            if (hudCanvasRoot == null)
                hudCanvasRoot = healthText.transform.parent;
            CreateHealthAmmoPanel();

            // Add ammo beside the existing health HUD at runtime, so the
            // scene/prefab does not need another serialized UI reference.
            ammoText = Instantiate(healthText, healthAmmoPanel.transform, false);
            ammoText.gameObject.name = "Ammo (FPS)";
            ConfigureHudValue(ammoText, new Vector2(70f, -111f),
                new Vector2(292f, 32f), 25f);

            countdownText = Instantiate(healthText, hudCanvasRoot, false);
            countdownText.gameObject.name = "Duel Countdown (FPS)";
            countdownText.fontSize = 72f;
            countdownText.fontStyle = FontStyles.Bold;
            countdownText.alignment = TextAlignmentOptions.Center;
            countdownText.color = new Color(1f, 0.9f, 0.2f, 1f);
            countdownText.raycastTarget = false;
            RectTransform countdownRect = countdownText.rectTransform;
            countdownRect.anchorMin = new Vector2(0.5f, 0.5f);
            countdownRect.anchorMax = new Vector2(0.5f, 0.5f);
            countdownRect.pivot = new Vector2(0.5f, 0.5f);
            countdownRect.anchoredPosition = new Vector2(0f, 100f);
            countdownRect.sizeDelta = new Vector2(500f, 120f);
            countdownText.gameObject.SetActive(false);

            CreatePokerBuffPanel();

            CreateHitMarker();
        }
        wasPreparing = false;
        duelCalloutEndTime = 0f;
        BindWeapon(controller != null ? controller.EquippedWeapon : null);
        RefreshDisplay();
    }

    void OnDisable()
    {
        if (health != null)
            health.OnDamaged -= UpdateHealth;
        BindWeapon(null);
        ResetEffect(damageFlash, damageFlashGroup);
        ResetEffect(reloadAnim, reloadAnimGroup);
        if (ammoText != null)
            Destroy(ammoText.gameObject);
        if (healthAmmoPanel != null)
            healthAmmoPanel.SetActive(false);
        if (countdownText != null)
            Destroy(countdownText.gameObject);
        if (pokerBuffPanel != null)
            Destroy(pokerBuffPanel);
        if (hitMarker != null)
            Destroy(hitMarker.gameObject);
        ammoText = null;
        countdownText = null;
        pokerBuffPanel = null;
        pokerBuffEmptyText = null;
        System.Array.Clear(pokerBuffTiles, 0, pokerBuffTiles.Length);
        System.Array.Clear(pokerBuffIcons, 0, pokerBuffIcons.Length);
        System.Array.Clear(pokerBuffNames, 0, pokerBuffNames.Length);
        System.Array.Clear(pokerBuffValues, 0, pokerBuffValues.Length);
        hitMarker = null;
        hitMarkerGroup = null;
        hitMarkerArms.Clear();
        lastHealthDisplay = null;
        lastAmmoDisplay = null;
        wasPreparing = false;
        damageFlashRemaining = 0f;
        wasReloading = false;
        fadingReload = false;
    }

    private void CreateHealthAmmoPanel()
    {
        if (healthAmmoPanel != null)
        {
            healthAmmoPanel.SetActive(true);
            ConfigureHudValue(healthText, new Vector2(70f, -29f),
                new Vector2(292f, 34f), 28f);
            return;
        }

        healthAmmoPanel = new GameObject("Health and Ammo (FPS)",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = healthAmmoPanel.GetComponent<RectTransform>();
        panelRect.SetParent(hudCanvasRoot, false);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-24f, 24f);
        panelRect.sizeDelta = new Vector2(390f, 158f);

        Image background = healthAmmoPanel.GetComponent<Image>();
        background.color = new Color(0.12f, 0.075f, 0.045f, 0.96f);
        background.raycastTarget = false;
        Outline outline = healthAmmoPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.78f, 0.48f, 0.19f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject accentObject = new GameObject("Status Accent",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.SetParent(panelRect, false);
        accentRect.anchorMin = accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(5f, 44f);
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = new Color(0.82f, 0.52f, 0.2f, 1f);
        accentImage.raycastTarget = false;

        CreateBuffLabel(panelRect, "Health Heading", "HEALTH", 13f,
            FontStyles.Bold, new Vector2(70f, -8f), new Vector2(180f, 19f),
            new Color(0.9f, 0.68f, 0.34f));
        ammoHeadingText = CreateBuffLabel(panelRect, "Ammo Heading", "AMMUNITION", 13f,
            FontStyles.Bold, new Vector2(70f, -88f), new Vector2(180f, 19f),
            new Color(0.9f, 0.68f, 0.34f));

        CreateHealthPlusIcon(panelRect, new Vector2(16f, -30f));
        CreateBulletIcon(panelRect, new Vector2(17f, -101f));

        healthText.transform.SetParent(panelRect, false);
        ConfigureHudValue(healthText, new Vector2(70f, -29f),
            new Vector2(292f, 34f), 28f);

        GameObject barObject = new GameObject("Health Bar",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        barRect.SetParent(panelRect, false);
        barRect.anchorMin = barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 1f);
        barRect.anchoredPosition = new Vector2(70f, -69f);
        barRect.sizeDelta = new Vector2(292f, 7f);
        Image barBackground = barObject.GetComponent<Image>();
        barBackground.color = new Color(0.24f, 0.17f, 0.09f, 1f);
        barBackground.raycastTarget = false;

        GameObject fillObject = new GameObject("Health Fill",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.SetParent(barRect, false);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        healthBarFill = fillObject.GetComponent<Image>();
        healthBarFill.type = Image.Type.Filled;
        healthBarFill.fillMethod = Image.FillMethod.Horizontal;
        healthBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthBarFill.color = new Color(0.63f, 0.72f, 0.36f, 1f);
        healthBarFill.raycastTarget = false;
    }

    private void ConfigureHudValue(TextMeshProUGUI text, Vector2 position,
        Vector2 size, float fontSize)
    {
        RectTransform rect = text.rectTransform;
        rect.SetParent(healthAmmoPanel.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.color = new Color(0.92f, 0.98f, 0.97f, 1f);
        text.raycastTarget = false;
        text.outlineWidth = 0.15f;
        text.outlineColor = Color.black;
    }

    private void CreateStatusIcon(RectTransform parent, string objectName,
        string shopItemId, Vector2 position)
    {
        GameObject iconObject = new GameObject(objectName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(42f, 42f);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = FpsOwnedItemsHud.GetIcon(shopItemId);
        icon.preserveAspect = true;
        icon.color = icon.sprite != null ? Color.white : new Color(0.94f, 0.68f, 0.22f);
        icon.raycastTarget = false;
    }

    private void CreateHealthPlusIcon(RectTransform parent, Vector2 position)
    {
        GameObject iconObject = new GameObject("Health Plus Icon",
            typeof(RectTransform));
        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(42f, 42f);
        CreateIconPiece(rect, "Plus Vertical", new Vector2(0f, 0f),
            new Vector2(10f, 34f));
        CreateIconPiece(rect, "Plus Horizontal", new Vector2(0f, 0f),
            new Vector2(34f, 10f));
    }

    private void CreateBulletIcon(RectTransform parent, Vector2 position)
    {
        GameObject bullet = new GameObject("Ammo Bullet Icon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = bullet.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position + new Vector2(18f, -20f);
        rect.sizeDelta = new Vector2(16f, 36f);
        rect.localRotation = Quaternion.Euler(0f, 0f, -35f);
        Image image = bullet.GetComponent<Image>();
        image.color = new Color(0.94f, 0.68f, 0.22f, 1f);
        image.raycastTarget = false;
        CreateIconPiece(rect, "Bullet Casing", new Vector2(0f, -5f),
            new Vector2(16f, 22f));
    }

    private void CreateIconPiece(RectTransform parent, string objectName,
        Vector2 offset, Vector2 size)
    {
        GameObject piece = new GameObject(objectName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = piece.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
        Image image = piece.GetComponent<Image>();
        image.color = new Color(0.94f, 0.68f, 0.22f, 1f);
        image.raycastTarget = false;
    }

    void UpdateHealth(int amount, int currentHealth)
    {
        ShowDamageFlash();
        RefreshDisplay();
    }

    public void SetPokerBuffs(CombatStats stats)
    {
        pokerBuffStats = stats;
        RefreshPokerBuffPanel();
    }

    private void CreatePokerBuffPanel()
    {
        pokerBuffPanel = new GameObject("Player Buffs (FPS)",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = pokerBuffPanel.GetComponent<RectTransform>();
        panelRect.SetParent(hudCanvasRoot, false);
        panelRect.anchorMin = panelRect.anchorMax = Vector2.zero;
        panelRect.pivot = Vector2.zero;
        panelRect.anchoredPosition = new Vector2(24f, 188f);
        panelRect.sizeDelta = new Vector2(566f, 158f);

        Image background = pokerBuffPanel.GetComponent<Image>();
        background.color = new Color(0.12f, 0.075f, 0.045f, 0.96f);
        background.raycastTarget = false;
        Outline outline = pokerBuffPanel.AddComponent<Outline>();
        outline.effectColor = new Color(0.78f, 0.48f, 0.19f, 0.9f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject accentObject = new GameObject("Buff Accent",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform accentRect = accentObject.GetComponent<RectTransform>();
        accentRect.SetParent(panelRect, false);
        accentRect.anchorMin = accentRect.anchorMax = new Vector2(0f, 1f);
        accentRect.pivot = new Vector2(0f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(5f, 42f);
        Image accentImage = accentObject.GetComponent<Image>();
        accentImage.color = new Color(0.82f, 0.52f, 0.2f, 1f);
        accentImage.raycastTarget = false;

        CreateBuffLabel(panelRect, "Buff Heading", "PLAYER BUFFS", 21f,
            FontStyles.Bold, new Vector2(18f, -8f), new Vector2(270f, 29f),
            new Color(0.91f, 0.98f, 0.97f));
        CreateBuffLabel(panelRect, "Buff Subheading", "POKER HAND BONUSES", 13f,
            FontStyles.Bold, new Vector2(-18f, -13f), new Vector2(240f, 20f),
            new Color(0.94f, 0.68f, 0.22f), true);

        string[] names = { "DAMAGE", "HEALTH", "FIRE RATE", "MOVE SPEED" };
        string[] iconIds = { "deadeye", "iron_heart", "golden_bullet", "quickstep" };
        for (int i = 0; i < pokerBuffTiles.Length; i++)
        {
            GameObject tile = new GameObject(names[i] + " Buff",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pokerBuffTiles[i] = tile;
            RectTransform tileRect = tile.GetComponent<RectTransform>();
            tileRect.SetParent(panelRect, false);
            tileRect.anchorMin = tileRect.anchorMax = new Vector2(0f, 1f);
            tileRect.pivot = new Vector2(0f, 1f);
            tileRect.anchoredPosition = new Vector2(14f + i * 137f, -48f);
            tileRect.sizeDelta = new Vector2(132f, 96f);
            Image tileBackground = tile.GetComponent<Image>();
            tileBackground.color = new Color(0.19f, 0.12f, 0.07f, 0.98f);
            tileBackground.raycastTarget = false;

            GameObject iconObject = new GameObject("Buff Icon",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(tileRect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(9f, 0f);
            iconRect.sizeDelta = new Vector2(48f, 48f);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = FpsOwnedItemsHud.GetIcon(iconIds[i]);
            icon.preserveAspect = true;
            icon.color = icon.sprite != null ? Color.white : new Color(0.94f, 0.68f, 0.22f);
            icon.raycastTarget = false;
            pokerBuffIcons[i] = icon;

            pokerBuffNames[i] = CreateBuffLabel(tileRect, "Buff Name", names[i],
                11f, FontStyles.Bold, new Vector2(64f, -14f), new Vector2(64f, 18f),
                new Color(0.76f, 0.86f, 0.87f));
            pokerBuffValues[i] = CreateBuffLabel(tileRect, "Buff Value", "+0", 25f,
                FontStyles.Bold, new Vector2(64f, -35f), new Vector2(64f, 35f),
                new Color(0.94f, 0.68f, 0.22f));
        }

        pokerBuffEmptyText = CreateBuffLabel(panelRect, "No Buffs",
            "No poker stat bonuses this duel", 18f, FontStyles.Bold,
            new Vector2(18f, -61f), new Vector2(520f, 70f),
            new Color(0.78f, 0.86f, 0.87f));
        pokerBuffEmptyText.alignment = TextAlignmentOptions.MidlineLeft;
        RefreshPokerBuffPanel();
    }

    private TextMeshProUGUI CreateBuffLabel(RectTransform parent, string objectName,
        string value, float size, FontStyles style, Vector2 position,
        Vector2 sizeDelta, Color color, bool rightAligned = false)
    {
        GameObject labelObject = new GameObject(objectName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rightAligned
            ? Vector2.one : new Vector2(0f, 1f);
        rect.pivot = rightAligned ? Vector2.one : new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = rightAligned
            ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.outlineWidth = 0.15f;
        label.outlineColor = Color.black;
        return label;
    }

    private void RefreshPokerBuffPanel()
    {
        if (pokerBuffEmptyText == null)
            return;

        float[] values =
        {
            pokerBuffStats.attack,
            pokerBuffStats.health,
            pokerBuffStats.fireRate,
            pokerBuffStats.moveSpeed
        };

        int activeBuffs = 0;
        for (int i = 0; i < values.Length; i++)
        {
            bool active = Mathf.Abs(values[i]) > 0.005f;
            pokerBuffTiles[i].SetActive(active);
            if (!active)
                continue;

            activeBuffs++;
            pokerBuffValues[i].text = i == 0
                ? PokerStatUnits.FormatAttackPercent(values[i])
                : FormatBonus(values[i]);
            pokerBuffValues[i].color = values[i] >= 0f
                ? new Color(0.94f, 0.68f, 0.22f)
                : new Color(1f, 0.42f, 0.35f);
        }

        pokerBuffEmptyText.gameObject.SetActive(activeBuffs == 0);
    }

    public static string FormatPokerBuffs(CombatStats stats)
    {
        var bonuses = new List<string>(4);
        if (Mathf.Abs(stats.attack) > 0.005f)
            bonuses.Add("DMG " + PokerStatUnits.FormatAttackPercent(stats.attack));
        if (Mathf.Abs(stats.health) > 0.005f)
            bonuses.Add("HP " + FormatBonus(stats.health));
        if (Mathf.Abs(stats.fireRate) > 0.005f)
            bonuses.Add("RATE " + FormatBonus(stats.fireRate));
        if (Mathf.Abs(stats.moveSpeed) > 0.005f)
            bonuses.Add("SPEED " + FormatBonus(stats.moveSpeed));
        return bonuses.Count > 0 ? string.Join("   ", bonuses) : "None";
    }

    private static string FormatBonus(float value)
    {
        return (value > 0f ? "+" : "") + value.ToString("0.#");
    }

    private void RefreshCountdown()
    {
        if (countdownText == null)
            return;

        CombatStartSequence sequence = CombatStartSequence.Active;
        if (sequence != null && sequence.IsPreparing)
        {
            wasPreparing = true;
            countdownText.text = Mathf.Max(1,
                Mathf.CeilToInt(sequence.RemainingSeconds)).ToString();
            countdownText.gameObject.SetActive(true);
            return;
        }

        if (wasPreparing)
        {
            wasPreparing = false;
            duelCalloutEndTime = Time.time + 0.75f;
        }

        bool showDuel = Time.time < duelCalloutEndTime;
        if (showDuel)
            countdownText.text = "DUEL!";
        countdownText.gameObject.SetActive(showDuel);
    }

    void LateUpdate()
    {
        // Ammo can change on a shot, a reload, or a weapon swap.
        BindWeapon(controller != null ? controller.EquippedWeapon : null);
        RefreshDisplay();
        RefreshEffects(controller != null ? controller.EquippedWeapon : null);
        RefreshCountdown();
        RefreshHitMarker();
    }

    private void SetupEffect(GameObject effect, ref CanvasGroup group)
    {
        if (effect == null)
            return;

        group = effect.GetComponent<CanvasGroup>();
        if (group == null)
            group = effect.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        effect.SetActive(false);
    }

    private static void ResetEffect(GameObject effect, CanvasGroup group)
    {
        if (effect == null || group == null)
            return;

        group.alpha = 0f;
        effect.SetActive(false);
    }

    private void ShowDamageFlash()
    {
        if (damageFlash == null || damageFlashGroup == null)
            return;

        damageFlashRemaining = DamageFlashDuration;
        damageFlashGroup.alpha = 1f;
        damageFlash.SetActive(true);
    }

    private void RefreshEffects(Weapon weapon)
    {
        if (damageFlash != null && damageFlashGroup != null &&
            damageFlash.activeSelf)
        {
            damageFlashRemaining -= Time.unscaledDeltaTime;
            damageFlashGroup.alpha = Mathf.Clamp01(
                damageFlashRemaining / DamageFlashDuration);
            if (damageFlashRemaining <= 0f)
                damageFlash.SetActive(false);
        }

        bool isReloading = weapon != null && weapon.IsReloading;
        if (isReloading && !wasReloading && reloadAnim != null &&
            reloadAnimGroup != null)
        {
            fadingReload = false;
            reloadAnimGroup.alpha = 1f;
            reloadAnim.SetActive(true);
        }
        else if (!isReloading && wasReloading && reloadAnimGroup != null)
        {
            fadingReload = true;
        }
        wasReloading = isReloading;

        if (fadingReload && reloadAnim != null && reloadAnimGroup != null)
        {
            reloadAnimGroup.alpha = Mathf.MoveTowards(reloadAnimGroup.alpha, 0f,
                Time.unscaledDeltaTime / ReloadFadeDuration);
            if (reloadAnimGroup.alpha <= 0f)
            {
                reloadAnim.SetActive(false);
                fadingReload = false;
            }
        }
    }

    private void BindWeapon(Weapon weapon)
    {
        if (observedWeapon == weapon)
            return;

        if (observedWeapon != null)
            observedWeapon.OnHitConfirmed -= ShowHitMarker;
        observedWeapon = weapon;
        if (observedWeapon != null)
            observedWeapon.OnHitConfirmed += ShowHitMarker;
    }

    private void CreateHitMarker()
    {
        GameObject markerObject = new GameObject("Hit Marker (FPS)",
            typeof(RectTransform), typeof(CanvasGroup));
        hitMarker = markerObject.GetComponent<RectTransform>();
        hitMarker.SetParent(hudCanvasRoot, false);
        hitMarker.anchorMin = hitMarker.anchorMax = new Vector2(0.5f, 0.5f);
        hitMarker.pivot = new Vector2(0.5f, 0.5f);
        hitMarker.anchoredPosition = Vector2.zero;
        hitMarker.sizeDelta = new Vector2(42f, 42f);
        hitMarker.SetAsLastSibling();
        hitMarkerGroup = markerObject.GetComponent<CanvasGroup>();
        hitMarkerGroup.blocksRaycasts = false;
        hitMarkerGroup.interactable = false;

        CreateHitMarkerArm("Upper Left", new Vector2(-9f, 9f), -45f);
        CreateHitMarkerArm("Upper Right", new Vector2(9f, 9f), 45f);
        CreateHitMarkerArm("Lower Left", new Vector2(-9f, -9f), 45f);
        CreateHitMarkerArm("Lower Right", new Vector2(9f, -9f), -45f);
        markerObject.SetActive(false);
    }

    private void CreateHitMarkerArm(string armName, Vector2 offset, float angle)
    {
        GameObject armObject = new GameObject(armName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform arm = armObject.GetComponent<RectTransform>();
        arm.SetParent(hitMarker, false);
        arm.anchorMin = arm.anchorMax = new Vector2(0.5f, 0.5f);
        arm.pivot = new Vector2(0.5f, 0.5f);
        arm.anchoredPosition = offset;
        arm.sizeDelta = new Vector2(12f, 2.5f);
        arm.localRotation = Quaternion.Euler(0f, 0f, angle);
        Image image = armObject.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        hitMarkerArms.Add(image);
    }

    private void ShowHitMarker(bool isHeadshot)
    {
        if (hitMarker == null || hitMarkerGroup == null)
            return;

        Color markerColor = isHeadshot ? Color.red : Color.white;
        foreach (Image arm in hitMarkerArms)
            arm.color = markerColor;
        hitMarkerEndTime = Time.unscaledTime + HitMarkerDuration;
        hitMarkerGroup.alpha = 1f;
        hitMarker.gameObject.SetActive(true);
    }

    private void RefreshHitMarker()
    {
        if (hitMarker == null || !hitMarker.gameObject.activeSelf)
            return;

        float remaining = hitMarkerEndTime - Time.unscaledTime;
        if (remaining <= 0f)
        {
            hitMarker.gameObject.SetActive(false);
            return;
        }

        hitMarkerGroup.alpha = Mathf.Clamp01(remaining / HitMarkerDuration);
    }

    private void RefreshDisplay()
    {
        if (healthText == null)
            return;

        string display = health != null
            ? health.CurrentHealth + " / " + health.MaxHealth : "-- / --";
        if (display != lastHealthDisplay)
        {
            healthText.text = display;
            lastHealthDisplay = display;
        }

        if (healthBarFill != null)
        {
            float healthRatio = health != null ? health.NormalizedHealth : 0f;
            healthBarFill.fillAmount = Mathf.Clamp01(healthRatio);
            healthBarFill.color = healthRatio > 0.5f
                ? new Color(0.63f, 0.72f, 0.36f, 1f)
                : healthRatio > 0.25f
                    ? new Color(1f, 0.72f, 0.18f, 1f)
                    : new Color(1f, 0.31f, 0.27f, 1f);
        }

        if (ammoText == null)
            return;

        Weapon weapon = controller != null ? controller.EquippedWeapon : null;
        string ammoDisplay = "-- / --";
        bool isReloading = false;
        if (weapon != null)
        {
            ammoDisplay = weapon.AmmoInMagazine.ToString("00") + " / " +
                weapon.MagazineCapacity.ToString("00");
            isReloading = weapon.IsReloading;
        }

        if (ammoDisplay != lastAmmoDisplay)
        {
            ammoText.text = ammoDisplay;
            lastAmmoDisplay = ammoDisplay;
        }

        if (ammoHeadingText != null)
            ammoHeadingText.text = isReloading
                ? "AMMUNITION  •  RELOADING"
                : "AMMUNITION";
        ammoText.color = isReloading
            ? new Color(1f, 0.72f, 0.18f, 1f)
            : new Color(0.92f, 0.98f, 0.97f, 1f);
    }
}

