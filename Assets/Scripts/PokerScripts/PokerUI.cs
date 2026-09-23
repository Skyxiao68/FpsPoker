using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PokerUI : MonoBehaviour
{
    private PokerGameManager poker;

    // Text fields.
    private Text potText;
    private Text playerChipsText;
    private Text enemyChipsText;
    private Text stateText;
    private Text playerHandText;
    private Text enemyHandText;
    private Text communityText;
    private Text playerHandTypeText;
    private Text enemyHandTypeText;
    private Text messageText;
    private Text enemyNameText;
    private Text raiseValueText;

    // Buttons.
    private Button checkButton;
    private Button raiseButton;
    private Button foldButton;
    private Button matchButton;
    private Button nextHandButton;

    // Slider.
    private Slider raiseSlider;

    // Containers.
    private GameObject quickBetRow;
    private GameObject actionRow;
    private GameObject nextHandRow;

    // Callback.
    private System.Action onNextHand;

    // Constants.
    private const int MinRaise = 1;
    private const int StepAmount = 10;
    private const int DefaultRaise = 10;

    // Tracking for slider reset on street change.
    private Street lastStreet = Street.PreFlop;
    private bool hasInitialized = false;

    public void Initialize(PokerGameManager manager)
    {
        poker = manager;
        BuildUI();

        poker.OnStateChanged += RefreshUI;
        poker.OnMessage += (msg) =>
        {
            messageText.text = msg;
        };
        poker.OnHandEnded += RefreshUI;

        RefreshUI();
    }

    public void SetNextHandCallback(System.Action callback)
    {
        onNextHand = callback;
    }

    // =========================================================
    // BUILD UI
    // =========================================================
    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("PokerCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGO.transform.SetParent(transform);

        CreatePanel(canvasGO.transform, new Color(0.1f, 0.15f, 0.1f), Vector2.zero, Vector2.one);

        // ---- Message bar at the very top ----
        CreatePanel(
            canvasGO.transform,
            new Color(0f, 0f, 0f, 0.55f),
            new Vector2(0.0f, 0.94f),
            new Vector2(1.0f, 1.0f)
        );

        messageText = CreateText(
            canvasGO.transform,
            "Message",
            "",
            new Vector2(0.02f, 0.94f),
            new Vector2(0.98f, 1.0f),
            TextAnchor.MiddleCenter,
            26
        );

        // ---- Top info ----
        potText = CreateText(
            canvasGO.transform,
            "PotText",
            "Pot: 0",
            new Vector2(0.10f, 0.87f),
            new Vector2(0.36f, 0.94f),
            TextAnchor.MiddleCenter,
            34
        );
        playerChipsText = CreateText(
            canvasGO.transform,
            "PlayerChipsText",
            "Player: 0",
            new Vector2(0.37f, 0.87f),
            new Vector2(0.63f, 0.94f),
            TextAnchor.MiddleCenter,
            34
        );
        enemyChipsText = CreateText(
            canvasGO.transform,
            "EnemyChipsText",
            "Enemy: 0",
            new Vector2(0.64f, 0.87f),
            new Vector2(0.90f, 0.94f),
            TextAnchor.MiddleCenter,
            34
        );

        // ---- Enemy ----
        enemyNameText = CreateText(
            canvasGO.transform,
            "EnemyNameText",
            "",
            new Vector2(0.2f, 0.80f),
            new Vector2(0.8f, 0.87f),
            TextAnchor.MiddleCenter,
            32
        );

        CreateText(
            canvasGO.transform,
            "EnemyLabel",
            "Enemy Hand",
            new Vector2(0.4f, 0.75f),
            new Vector2(0.6f, 0.80f),
            TextAnchor.MiddleCenter,
            22
        );

        enemyHandText = CreateText(
            canvasGO.transform,
            "EnemyHand",
            "?? ??",
            new Vector2(0.3f, 0.65f),
            new Vector2(0.7f, 0.75f),
            TextAnchor.MiddleCenter,
            48
        );

        enemyHandTypeText = CreateText(
            canvasGO.transform,
            "EnemyHandType",
            "",
            new Vector2(0.3f, 0.60f),
            new Vector2(0.7f, 0.65f),
            TextAnchor.MiddleCenter,
            26
        );

        // ---- Community ----
        CreateText(
            canvasGO.transform,
            "CommunityLabel",
            "Community Cards",
            new Vector2(0.4f, 0.54f),
            new Vector2(0.6f, 0.60f),
            TextAnchor.MiddleCenter,
            22
        );

        communityText = CreateText(
            canvasGO.transform,
            "Community",
            "",
            new Vector2(0.05f, 0.44f),
            new Vector2(0.95f, 0.54f),
            TextAnchor.MiddleCenter,
            52
        );

        // ---- Player ----
        CreateText(
            canvasGO.transform,
            "PlayerLabel",
            "Player Hand",
            new Vector2(0.4f, 0.38f),
            new Vector2(0.6f, 0.44f),
            TextAnchor.MiddleCenter,
            22
        );

        playerHandText = CreateText(
            canvasGO.transform,
            "PlayerHand",
            "",
            new Vector2(0.3f, 0.28f),
            new Vector2(0.7f, 0.38f),
            TextAnchor.MiddleCenter,
            52
        );

        playerHandTypeText = CreateText(
            canvasGO.transform,
            "PlayerHandType",
            "",
            new Vector2(0.3f, 0.23f),
            new Vector2(0.7f, 0.28f),
            TextAnchor.MiddleCenter,
            26
        );

        // ---- State ----
        stateText = CreateText(
            canvasGO.transform,
            "StateText",
            "",
            new Vector2(0.05f, 0.17f),
            new Vector2(0.95f, 0.23f),
            TextAnchor.MiddleCenter,
            24
        );

        // =========================================================
        // Quick bet row
        // =========================================================
        quickBetRow = new GameObject("QuickBetRow");
        quickBetRow.transform.SetParent(canvasGO.transform, false);
        RectTransform qbrRT = quickBetRow.AddComponent<RectTransform>();
        qbrRT.anchorMin = Vector2.zero;
        qbrRT.anchorMax = Vector2.one;
        qbrRT.offsetMin = Vector2.zero;
        qbrRT.offsetMax = Vector2.zero;

        float qbY0 = 0.09f,
            qbY1 = 0.15f;

        CreateButton(
            quickBetRow.transform,
            "Btn33",
            "33%",
            new Vector2(0.03f, qbY0),
            new Vector2(0.09f, qbY1),
            new Color(0.15f, 0.35f, 0.65f),
            () => SetSliderToFraction(0.33f)
        );

        CreateButton(
            quickBetRow.transform,
            "Btn50",
            "50%",
            new Vector2(0.10f, qbY0),
            new Vector2(0.16f, qbY1),
            new Color(0.15f, 0.35f, 0.65f),
            () => SetSliderToFraction(0.50f)
        );

        CreateButton(
            quickBetRow.transform,
            "Btn75",
            "75%",
            new Vector2(0.17f, qbY0),
            new Vector2(0.23f, qbY1),
            new Color(0.15f, 0.35f, 0.65f),
            () => SetSliderToFraction(0.75f)
        );

        CreateButton(
            quickBetRow.transform,
            "BtnMax",
            "Max",
            new Vector2(0.24f, qbY0),
            new Vector2(0.30f, qbY1),
            new Color(0.15f, 0.35f, 0.65f),
            () => SetSliderToFraction(1.0f)
        );

        raiseSlider = CreateSlider(
            quickBetRow.transform,
            "RaiseSlider",
            new Vector2(0.33f, qbY0),
            new Vector2(0.74f, qbY1),
            MinRaise,
            100,
            DefaultRaise
        );

        raiseValueText = CreateText(
            quickBetRow.transform,
            "RaiseValueText",
            DefaultRaise.ToString(),
            new Vector2(0.74f, qbY0),
            new Vector2(0.82f, qbY1),
            TextAnchor.MiddleCenter,
            24
        );

        raiseSlider.onValueChanged.AddListener(
            (value) =>
            {
                raiseValueText.text = Mathf.RoundToInt(value).ToString();
            }
        );

        CreateButton(
            quickBetRow.transform,
            "BtnMinus",
            "-",
            new Vector2(0.83f, qbY0),
            new Vector2(0.89f, qbY1),
            new Color(0.25f, 0.25f, 0.25f),
            () => AdjustSlider(-StepAmount)
        );

        CreateButton(
            quickBetRow.transform,
            "BtnPlus",
            "+",
            new Vector2(0.90f, qbY0),
            new Vector2(0.96f, qbY1),
            new Color(0.25f, 0.25f, 0.25f),
            () => AdjustSlider(StepAmount)
        );

        // =========================================================
        // Action row
        // =========================================================
        actionRow = new GameObject("ActionRow");
        actionRow.transform.SetParent(canvasGO.transform, false);
        RectTransform arRT = actionRow.AddComponent<RectTransform>();
        arRT.anchorMin = Vector2.zero;
        arRT.anchorMax = Vector2.one;
        arRT.offsetMin = Vector2.zero;
        arRT.offsetMax = Vector2.zero;

        float aY0 = 0.01f,
            aY1 = 0.08f;

        foldButton = CreateButton(
            actionRow.transform,
            "FoldBtn",
            "Fold",
            new Vector2(0.03f, aY0),
            new Vector2(0.25f, aY1),
            new Color(0.72f, 0.22f, 0.17f),
            () => poker.PlayerFold()
        );

        checkButton = CreateButton(
            actionRow.transform,
            "CheckBtn",
            "Check",
            new Vector2(0.26f, aY0),
            new Vector2(0.48f, aY1),
            new Color(0.12f, 0.54f, 0.31f),
            () => poker.PlayerCheck()
        );

        raiseButton = CreateButton(
            actionRow.transform,
            "RaiseBtn",
            "Raise",
            new Vector2(0.49f, aY0),
            new Vector2(0.71f, aY1),
            new Color(0.85f, 0.55f, 0.11f),
            () => poker.PlayerRaise(Mathf.RoundToInt(raiseSlider.value))
        );

        matchButton = CreateButton(
            actionRow.transform,
            "MatchBtn",
            "Match",
            new Vector2(0.72f, aY0),
            new Vector2(0.94f, aY1),
            new Color(0.2f, 0.45f, 0.7f),
            () => poker.PlayerMatchRaise()
        );

        // =========================================================
        // Next hand row
        // =========================================================
        nextHandRow = new GameObject("NextHandRow");
        nextHandRow.transform.SetParent(canvasGO.transform, false);
        RectTransform nhrRT = nextHandRow.AddComponent<RectTransform>();
        nhrRT.anchorMin = Vector2.zero;
        nhrRT.anchorMax = Vector2.one;
        nhrRT.offsetMin = Vector2.zero;
        nhrRT.offsetMax = Vector2.zero;

        nextHandButton = CreateButton(
            nextHandRow.transform,
            "NextHandBtn",
            "Next Game",
            new Vector2(0.03f, 0.01f),
            new Vector2(0.94f, 0.08f),
            new Color(0.2f, 0.6f, 0.35f),
            () => onNextHand?.Invoke()
        );

        nextHandRow.SetActive(false);
    }

    // =========================================================
    // REFRESH
    // =========================================================
    private void RefreshUI()
    {
        if (poker == null)
            return;

        potText.text = $"Pot: {poker.Pot}";
        playerChipsText.text = $"Player: {poker.PlayerChips}";
        enemyChipsText.text = $"Enemy: {poker.EnemyChips}";

        if (poker.EnemyParams != null && enemyNameText != null)
            enemyNameText.text = poker.EnemyParams.enemyName;

        string dealingStr = poker.IsDealing ? " (Dealing...)" : "";
        stateText.text =
            $"Stage: {poker.CurrentStreet}    State: {poker.State}{dealingStr}    "
            + $"Bet: {poker.CurrentBet}    Community: {poker.CommunityCards.Count}/5";

        playerHandText.text = CardStr(poker.PlayerHand);
        communityText.text = CardStr(poker.CommunityCards);

        bool revealEnemy =
            poker.State == PokerState.Showdown
            || poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;

        enemyHandText.text = revealEnemy ? CardStr(poker.EnemyHand) : "?? ??";

        playerHandTypeText.text =
            poker.PlayerBestHand != null
                ? $"Hand Type: {poker.PlayerBestHand.GetDisplayName()}"
                : "";

        enemyHandTypeText.text =
            revealEnemy && poker.EnemyBestHand != null
                ? $"Hand Type: {poker.EnemyBestHand.GetDisplayName()}"
                : "";

        bool isPlayerTurn = poker.State == PokerState.PlayerTurn && !poker.IsDealing;
        bool mustMatch = poker.AwaitingPlayerMatch;

        int maxRaise = poker.GetPlayerMaxRaise();
        bool canRaise = maxRaise >= MinRaise;

        checkButton.interactable = isPlayerTurn && !mustMatch;
        raiseButton.interactable = isPlayerTurn && !mustMatch;
        matchButton.interactable = isPlayerTurn && mustMatch;
        foldButton.interactable = isPlayerTurn;


        bool handOver =
            poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;

        //// 无法加注时（maxRaise == 0），隐藏整条快捷下注行
        quickBetRow.SetActive(!handOver & canRaise);
        actionRow.SetActive(!handOver);
        nextHandRow.SetActive(handOver);

        // ---- Slider bounds ----
        int sliderMax = Mathf.Max(MinRaise, maxRaise);
        raiseSlider.minValue = MinRaise;
        raiseSlider.maxValue = maxRaise;
        raiseSlider.interactable = canRaise && isPlayerTurn && !mustMatch;

        // ---- Reset slider to DefaultRaise on street change or first init ----
        if (!hasInitialized || poker.CurrentStreet != lastStreet)
        {
            lastStreet = poker.CurrentStreet;
            hasInitialized = true;

            float resetValue = Mathf.Clamp(DefaultRaise, MinRaise, maxRaise);
            raiseSlider.value = resetValue;
        }

        if (raiseSlider.value > maxRaise)
            raiseSlider.value = maxRaise;

        raiseValueText.text = canRaise ? Mathf.RoundToInt(raiseSlider.value).ToString() : "-";

        // 把快捷下注按钮的可交互状态跟 canRaise 绑定
        SetQuickBetButtonsInteractable(canRaise && isPlayerTurn && !mustMatch);
    }

    /// <summary>
    /// 根据是否能加注，动态改变加注按钮的文字。
    /// 能加注时显示 Raise，不能加注时显示 Check（但仍然禁用，避免误导）。
    /// </summary>


    private void SetQuickBetButtonsInteractable(bool on)
    {
        if (quickBetRow == null)
            return;
        foreach (Button b in quickBetRow.GetComponentsInChildren<Button>(true))
        {
            // 滑块的加减按钮也在这里，
            b.interactable = on;
        }
    }

    // =========================================================
    // SLIDER HELPERS
    // =========================================================
    private void SetSliderToFraction(float fraction)
    {
        float target = Mathf.Lerp(raiseSlider.minValue, raiseSlider.maxValue, fraction);
        raiseSlider.value = Mathf.Round(target);
    }

    private void AdjustSlider(int delta)
    {
        float target = raiseSlider.value + delta;
        raiseSlider.value = Mathf.Clamp(target, raiseSlider.minValue, raiseSlider.maxValue);
    }

    // =========================================================
    // UI BUILDERS
    // =========================================================
    private Text CreateText(
        Transform parent,
        string name,
        string content,
        Vector2 anchorMin,
        Vector2 anchorMax,
        TextAnchor align,
        int fontSize
    )
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return text;
    }

    private Button CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color normalColor,
        UnityEngine.Events.UnityAction onClick
    )
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = normalColor;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        ColorBlock cb = btn.colors;
        cb.normalColor = normalColor;
        cb.highlightedColor = Brighten(normalColor, 1.25f);
        cb.pressedColor = Darken(normalColor, 0.7f);
        cb.selectedColor = normalColor;
        cb.disabledColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.1f;
        btn.colors = cb;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text txt = CreateText(
            go.transform,
            "Label",
            label,
            Vector2.zero,
            Vector2.one,
            TextAnchor.MiddleCenter,
            22
        );
        txt.raycastTarget = false;

        return btn;
    }

    private Slider CreateSlider(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float minValue,
        float maxValue,
        float initialValue
    )
    {
        GameObject sliderGO = new GameObject(name);
        sliderGO.transform.SetParent(parent, false);

        RectTransform sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = anchorMin;
        sliderRT.anchorMax = anchorMax;
        sliderRT.offsetMin = Vector2.zero;
        sliderRT.offsetMax = Vector2.zero;

        Slider slider = sliderGO.AddComponent<Slider>();

        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.4f);
        bgRT.anchorMax = new Vector2(1f, 0.6f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0.4f);
        fillAreaRT.anchorMax = new Vector2(1f, 0.6f);
        fillAreaRT.offsetMin = new Vector2(8f, 0f);
        fillAreaRT.offsetMax = new Vector2(-8f, 0f);

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.35f, 0.6f, 0.45f);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        GameObject handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(8f, 0f);
        handleAreaRT.offsetMax = new Vector2(-8f, 0f);

        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        Image handleImg = handleGO.AddComponent<Image>();
        handleImg.color = new Color(0.95f, 0.95f, 0.95f);
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(24f, 0f);

        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = Mathf.Clamp(initialValue, minValue, maxValue);

        return slider;
    }

    private Image CreatePanel(Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject("Panel");
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    // =========================================================
    // UTILS
    // =========================================================
    private Color Brighten(Color c, float factor)
    {
        return new Color(
            Mathf.Clamp01(c.r * factor),
            Mathf.Clamp01(c.g * factor),
            Mathf.Clamp01(c.b * factor),
            c.a
        );
    }

    private Color Darken(Color c, float factor)
    {
        return new Color(c.r * factor, c.g * factor, c.b * factor, c.a);
    }

    private string CardStr(List<Card> cards)
    {
        if (cards == null || cards.Count == 0)
            return "";
        string s = "";
        foreach (Card c in cards)
            s += c.ToString() + "  ";
        return s;
    }
}
