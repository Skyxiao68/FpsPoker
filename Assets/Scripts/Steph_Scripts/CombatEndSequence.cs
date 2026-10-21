using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Shows the duel result while the camera pans, then returns to poker.</summary>
public class CombatEndSequence : MonoBehaviour
{
    private const float Duration = 5f;
    private const float CameraRadius = 0.2f;

    public bool IsPlaying { get; private set; }

    private Action onComplete;
    private bool begun;
    private int startFrame;
    private float elapsed;
    private Transform playerRoot;
    private Transform enemyRoot;
    private Transform panCamera;
    private Vector3 cameraStart;
    private Quaternion rotationStart;
    private Vector3 cameraEnd;
    private Vector3 focusPoint;
    private Canvas resultCanvas;
    private TextMeshProUGUI skipText;

    public void Begin(CombatWinner winner, Health player,
        FpsCharacterController playerController, Health enemy, int pot, Action onComplete)
    {
        if (begun)
            return;

        begun = true;
        IsPlaying = true;
        this.onComplete = onComplete;
        startFrame = Time.frameCount;
        playerRoot = player != null ? player.transform
            : playerController != null ? playerController.transform : null;
        enemyRoot = enemy != null ? enemy.transform : null;

        // Preparation cleanup unlocks weapons, so finish it before the final locks.
        if (CombatStartSequence.Active != null)
            CombatStartSequence.Active.gameObject.SetActive(false);

        FreezeFighter(playerRoot);
        FreezeFighter(enemyRoot);
        PrepareCamera(playerController, winner == CombatWinner.Player ? player : enemy);
        CreateResultDisplay(winner, player, enemy, pot);
    }

    private static void FreezeFighter(Transform root)
    {
        if (root == null)
            return;

        foreach (FpsPlayerInputHandler input in root.GetComponentsInChildren<FpsPlayerInputHandler>())
            input.enabled = false;
        foreach (FPSAIEngine ai in root.GetComponentsInChildren<FPSAIEngine>())
            ai.enabled = false;
        foreach (FpsCharacterController controller in root.GetComponentsInChildren<FpsCharacterController>())
        {
            controller.Stop();
            controller.SetLookInput(Vector2.zero);
            controller.SetSprint(false);
            controller.SetFireInput(false);
            controller.enabled = false;
        }
        foreach (Weapon weapon in root.GetComponentsInChildren<Weapon>())
            weapon.SetCombatLocked(true);

        // Results have their own canvas because death may already have disabled the HUD.
        foreach (FPS_HUD hud in root.GetComponentsInChildren<FPS_HUD>(true))
        {
            hud.enabled = false;
            Canvas canvas = hud.healthText != null
                ? hud.healthText.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
                canvas.enabled = false;
        }
    }

    private void PrepareCamera(FpsCharacterController playerController, Health survivor)
    {
        Camera view = playerController != null
            ? playerController.CameraTransform.GetComponentInChildren<Camera>() : null;
        if (view == null)
            view = Camera.main;
        if (view == null)
            return;

        panCamera = view.transform;
        cameraStart = panCamera.position;
        rotationStart = panCamera.rotation;
        focusPoint = survivor != null ? survivor.transform.position + Vector3.up * 0.4f
            : cameraStart + panCamera.forward * 5f;

        Vector3 away = Vector3.ProjectOnPlane(cameraStart - focusPoint, Vector3.up);
        if (away.sqrMagnitude < 0.25f)
            away = Vector3.ProjectOnPlane(-panCamera.forward, Vector3.up);
        if (away.sqrMagnitude < 0.01f)
            away = Vector3.back;

        float distance = Mathf.Clamp(away.magnitude, 4f, 8f);
        cameraEnd = focusPoint + Quaternion.AngleAxis(25f, Vector3.up) *
            away.normalized * distance + Vector3.up * 1.8f;

        // Moving the actual camera keeps the first-person gun out of the pan.
        if (playerController != null && playerController.EquippedWeapon != null)
        {
            foreach (Renderer renderer in playerController.EquippedWeapon.GetComponentsInChildren<Renderer>())
                renderer.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsPlaying)
            return;

        bool paused = Time.timeScale <= 0f;
        if (resultCanvas != null)
            resultCanvas.enabled = !paused;
        if (paused || Time.frameCount == startFrame)
            return;

        elapsed += Time.unscaledDeltaTime;
        // A jump pressed on the killing frame must not skip the result immediately.
        if (elapsed >= Duration ||
            (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame))
        {
            Complete();
            return;
        }

        if (skipText != null)
            skipText.text = $"Continuing in {Mathf.CeilToInt(Duration - elapsed)}s  |  SPACE to skip";
    }

    private void LateUpdate()
    {
        if (!IsPlaying || panCamera == null || Time.timeScale <= 0f)
            return;

        float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Duration));
        Vector3 position = Vector3.Lerp(cameraStart, cameraEnd, progress);
        Vector3 travel = position - cameraStart;
        float distance = travel.magnitude;
        if (distance > 0.001f)
        {
            // Ignore the duelists, but stop the camera before scenery along its path.
            RaycastHit[] hits = Physics.SphereCastAll(cameraStart, CameraRadius,
                travel / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if ((playerRoot != null && hit.transform.IsChildOf(playerRoot)) ||
                    (enemyRoot != null && hit.transform.IsChildOf(enemyRoot)))
                    continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.05f));
            }
            position = cameraStart + travel.normalized * distance;
        }

        Vector3 direction = focusPoint - position;
        Quaternion rotation = direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction) : rotationStart;
        panCamera.SetPositionAndRotation(position,
            Quaternion.Slerp(rotationStart, rotation, progress));
    }

    private void CreateResultDisplay(CombatWinner winner, Health player, Health enemy, int pot)
    {
        GameObject canvasObject = new GameObject("Duel Results (FPS)",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        resultCanvas = canvasObject.GetComponent<Canvas>();
        resultCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        resultCanvas.sortingOrder = 1000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CreatePanel("Top Letterbox", new Vector2(0f, 0.92f), Vector2.one,
            new Color(0.02f, 0.025f, 0.035f, 0.94f));
        RectTransform panel = CreatePanel("Result Panel", Vector2.zero, new Vector2(1f, 0.3f),
            new Color(0.02f, 0.025f, 0.035f, 0.94f));

        bool playerWon = winner == CombatWinner.Player;
        Health survivor = playerWon ? player : enemy;
        int remainingHealth = survivor != null ? survivor.CurrentHealth : 0;
        int maxHealth = survivor != null ? survivor.MaxHealth : 0;
        string enemyName = enemy != null ? enemy.gameObject.name : "Enemy";
        CreateLabel(panel, "Winner", playerWon ? "YOU WIN" : enemyName + " WINS",
            52f, 0.63f, 0.96f, playerWon
                ? new Color(1f, 0.86f, 0.35f) : new Color(1f, 0.4f, 0.35f));
        CreateLabel(panel, "Victory Margin",
            $"Won by {remainingHealth} HP ({remainingHealth}/{maxHealth} health remaining)\n" +
            $"Pot won: {pot} chips", 28f, 0.27f, 0.66f, Color.white);
        skipText = CreateLabel(panel, "Skip Prompt", "Continuing in 5s  |  SPACE to skip",
            22f, 0.05f, 0.27f, new Color(0.8f, 0.83f, 0.9f));
    }

    private RectTransform CreatePanel(string panelName, Vector2 min, Vector2 max, Color color)
    {
        GameObject panelObject = new GameObject(panelName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rect = panelObject.GetComponent<RectTransform>();
        rect.SetParent(resultCanvas.transform, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = panelObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TextMeshProUGUI CreateLabel(RectTransform parent, string labelName,
        string value, float fontSize, float bottom, float top, Color color)
    {
        GameObject labelObject = new GameObject(labelName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.05f, bottom);
        rect.anchorMax = new Vector2(0.95f, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.raycastTarget = false;
        label.richText = false;
        return label;
    }

    private void Complete()
    {
        if (!IsPlaying)
            return;

        IsPlaying = false;
        if (skipText != null)
            skipText.text = "Duel complete";
        Action callback = onComplete;
        onComplete = null;
        callback?.Invoke();
    }

    private void OnDestroy()
    {
        IsPlaying = false;
        onComplete = null;
        if (resultCanvas != null)
            Destroy(resultCanvas.gameObject);
    }
}
