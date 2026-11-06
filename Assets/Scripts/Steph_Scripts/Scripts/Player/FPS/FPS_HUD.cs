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
    private TextMeshProUGUI ammoText;
    private TextMeshProUGUI countdownText;
    private TextMeshProUGUI pokerBuffText;
    private string pokerBuffDisplay = "POKER BUFFS\nNone";
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
            healthText.fontSize = 42f;
            healthText.enableAutoSizing = false;
            RectTransform healthRect = healthText.rectTransform;
            healthRect.sizeDelta = new Vector2(
                Mathf.Max(440f, healthRect.sizeDelta.x),
                Mathf.Max(64f, healthRect.sizeDelta.y));

            // Add ammo beside the existing health HUD at runtime, so the
            // scene/prefab does not need another serialized UI reference.
            ammoText = Instantiate(healthText, healthText.transform.parent, false);
            ammoText.gameObject.name = "Ammo (FPS)";
            ammoText.fontSize = 34f;
            ammoText.raycastTarget = false;
            RectTransform label = ammoText.rectTransform;
            label.anchoredPosition = healthText.rectTransform.anchoredPosition +
                new Vector2(0f, -64f);
            label.sizeDelta = new Vector2(Mathf.Max(440f, label.sizeDelta.x), 52f);

            countdownText = Instantiate(healthText, healthText.transform.parent, false);
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

            pokerBuffText = Instantiate(healthText, healthText.transform.parent, false);
            pokerBuffText.gameObject.name = "Poker Buffs (FPS)";
            pokerBuffText.fontSize = 18f;
            pokerBuffText.alignment = TextAlignmentOptions.BottomLeft;
            pokerBuffText.color = new Color(1f, 0.9f, 0.7f, 1f);
            pokerBuffText.raycastTarget = false;
            RectTransform buffRect = pokerBuffText.rectTransform;
            buffRect.anchorMin = buffRect.anchorMax = new Vector2(0f, 0f);
            buffRect.pivot = new Vector2(0f, 0f);
            buffRect.anchoredPosition = new Vector2(24f, 260f);
            buffRect.sizeDelta = new Vector2(520f, 90f);
            pokerBuffText.text = pokerBuffDisplay;

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
        if (countdownText != null)
            Destroy(countdownText.gameObject);
        if (pokerBuffText != null)
            Destroy(pokerBuffText.gameObject);
        if (hitMarker != null)
            Destroy(hitMarker.gameObject);
        ammoText = null;
        countdownText = null;
        pokerBuffText = null;
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

    void UpdateHealth(int amount, int currentHealth)
    {
        ShowDamageFlash();
        RefreshDisplay();
    }

    public void SetPokerBuffs(CombatStats stats)
    {
        pokerBuffDisplay = "POKER BUFFS\n" + FormatPokerBuffs(stats);
        if (pokerBuffText != null)
            pokerBuffText.text = pokerBuffDisplay;
    }

    public static string FormatPokerBuffs(CombatStats stats)
    {
        var bonuses = new List<string>(4);
        if (Mathf.Abs(stats.attack) > 0.005f)
            bonuses.Add("DMG " + FormatBonus(stats.attack));
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
        hitMarker.SetParent(healthText.transform.parent, false);
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
            ? "HP " + health.CurrentHealth + "/" + health.MaxHealth : "";
        if (display != lastHealthDisplay)
        {
            healthText.text = display;
            lastHealthDisplay = display;
        }

        if (ammoText == null)
            return;

        Weapon weapon = controller != null ? controller.EquippedWeapon : null;
        string ammoDisplay = "";
        if (weapon != null)
        {
            ammoDisplay = "Ammo " + weapon.AmmoInMagazine + "/" + weapon.MagazineCapacity;
            if (weapon.IsReloading)
                ammoDisplay += "  Reloading";
        }

        if (ammoDisplay != lastAmmoDisplay)
        {
            ammoText.text = ammoDisplay;
            lastAmmoDisplay = ammoDisplay;
        }
    }
}
