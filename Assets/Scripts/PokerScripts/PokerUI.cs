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
    private Text betPillText;
    private Text playerHandText;
    private Text enemyHandText;
    private Text communityText; // 改成显示公共牌牌型文字
    private Text[] communitySlots = new Text[5];
    private Text[] playerSlots = new Text[2];
    private Text[] enemySlots = new Text[2];
    private Text sliderMinLabel;
    private Text sliderMaxLabel;
    private Text playerHandTypeText;
    private Text enemyHandTypeText;
    private Transform messageLogContainer;
    private ScrollRect messageLogScroll;
    private bool stickToLogBottom = true;
    private const int MaxLogEntries = 200;
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
    private bool nextHandVisible = true;

    // Callback.
    private System.Action onNextHand;

    // Constants.
    private const int MinRaise = 1;
    private const int StepAmount = 10;
    private const int DefaultRaise = 10;

    // 视觉常量（P1–P5 改造：颜色与文字层级集中在这里）
    private static readonly Color PANEL_BG = new Color(0f, 0f, 0f, 0.42f);
    private static readonly Color PANEL_BG_DARK = new Color(0f, 0f, 0f, 0.50f);
    private static readonly Color TEXT_STRONG = new Color(0.88f, 0.88f, 0.88f);
    private static readonly Color TEXT_SOFT = new Color(0.80f, 0.80f, 0.80f);
    private static readonly Color TEXT_MUTED = new Color(0.62f, 0.62f, 0.62f);
    private static readonly Color SLOT_BG = new Color(0.02f, 0.02f, 0.02f, 0.95f);
    private static readonly Color SLOT_EDGE = new Color(0.40f, 0.40f, 0.40f, 0.70f);
    // 参与当前 best-five 的牌用这个边框，把「为什么是你的最好五张」画出来
    private static readonly Color SLOT_EDGE_BEST = new Color(1.00f, 0.78f, 0.30f, 0.95f);
    private static readonly Color SLOT_BG_BEST = new Color(0.12f, 0.11f, 0.06f, 0.97f);
    private static readonly Color BTN_FOLD = new Color(0.52f, 0.18f, 0.16f);
    private static readonly Color BTN_CHECK = new Color(0.12f, 0.48f, 0.30f);
    private static readonly Color BTN_RAISE = new Color(0.82f, 0.52f, 0.12f);
    private static readonly Color BTN_MATCH = new Color(0.68f, 0.42f, 0.10f);
    private static readonly Color BTN_NEUTRAL = new Color(0.22f, 0.22f, 0.26f);

    // Tracking for slider reset on street change.
    private Street lastStreet = Street.PreFlop;
    private bool hasInitialized = false;

    private Button swapButton;
    private GameObject swapChoiceRow;
    private Transform canvasRoot;
    private Button swapCard1Button;
    private Button swapCard2Button;

    // M3 引导层（? 参照面板 + 本手结果行），自建 Canvas order 300。
    private PokerTutorialUI tutorial;

    // 教程场景按名字定位牌桌上的控件（只读，不改行为）。
    public Button CheckButton { get { return checkButton; } }
    public Button RaiseButton { get { return raiseButton; } }
    public Button MatchButton { get { return matchButton; } }
    public Button FoldButton { get { return foldButton; } }
    public Slider RaiseSlider { get { return raiseSlider; } }
    public Transform TableCanvasRoot
    {
        get
        {
            // 教程场景里这个引用会变成已销毁对象（Canvas 被重建过），
            // 直接按名字找回活的那个，别让调用方拿到 null。
            if (canvasRoot == null)
            {
                GameObject live = GameObject.Find("PokerCanvas");
                if (live != null)
                    canvasRoot = live.transform;
            }
            return canvasRoot;
        }
    }


    public void Initialize(PokerGameManager manager)
    {
        poker = manager;
        BuildUI();

        poker.OnStateChanged += RefreshUI;

        // 修复这里：直接订阅 AddMessage
        poker.OnMessage += AddMessage;

        poker.OnHandEnded += RefreshUI;

        RefreshUI();
    }

    public void SetNextHandCallback(System.Action callback)
    {
        onNextHand = callback;
    }

    /// <summary>教程高亮用：玩家第 index 张手牌的槽位。</summary>
    public RectTransform PlayerCardSlot(int index)
    {
        if (playerSlots == null || index < 0 || index >= playerSlots.Length || playerSlots[index] == null)
            return null;
        return playerSlots[index].transform.parent as RectTransform;
    }

    /// <summary>教程高亮用：双方筹码数字那一行。</summary>
    public RectTransform PlayerChipsRect
    {
        get { return playerChipsText != null ? playerChipsText.rectTransform : null; }
    }

    public RectTransform EnemyChipsRect
    {
        get { return enemyChipsText != null ? enemyChipsText.rectTransform : null; }
    }

    /// <summary>
    /// 教程场景里没有"下一手"这回事：整行藏起来，别让玩家点了没反应。
    /// </summary>
    public void SetNextHandVisible(bool visible)
    {
        nextHandVisible = visible;
        if (nextHandRow != null && !visible)
            nextHandRow.SetActive(false);
    }

    // =========================================================
    // BUILD UI
    // =========================================================
    private void BuildUI()
    {
        // ---------- Canvas ----------
        GameObject canvasGO = new GameObject("PokerCanvas");
        canvasRoot = canvasGO.transform;
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGO.transform.SetParent(transform);

        // 毛毡背景
        CreatePanel(canvasGO.transform, new Color(0.10f, 0.16f, 0.10f), Vector2.zero, Vector2.one);

        // ---------- 顶栏：底池居中 ----------
        CreatePanel(
            canvasGO.transform,
            new Color(0, 0, 0, 0.55f),
            new Vector2(0f, 0.93f),
            new Vector2(1f, 1f)
        );

        potText = CreateText(
            canvasGO.transform,
            "PotText",
            "Pot: 0",
            new Vector2(0.35f, 0.93f),
            new Vector2(0.65f, 1f),
            TextAnchor.MiddleCenter,
            40
        );
        potText.fontStyle = FontStyle.Bold;

        // ---------- 敌人信息：左上角 ----------
        CreatePanel(
            canvasGO.transform,
            PANEL_BG,
            new Vector2(0.02f, 0.73f),
            new Vector2(0.28f, 0.91f)
        );

        enemyNameText = CreateText(
            canvasGO.transform,
            "EnemyNameText",
            "",
            new Vector2(0.04f, 0.872f),
            new Vector2(0.26f, 0.902f),
            TextAnchor.MiddleLeft,
            22
        );
        enemyNameText.fontStyle = FontStyle.Bold;

        enemyChipsText = CreateText(
            canvasGO.transform,
            "EnemyChipsText",
            "Enemy: 0",
            new Vector2(0.04f, 0.842f),
            new Vector2(0.26f, 0.872f),
            TextAnchor.MiddleLeft,
            20
        );
        enemyChipsText.color = TEXT_STRONG;

        Text enemyLabel = CreateText(
            canvasGO.transform,
            "EnemyLabel",
            "Enemy Hand",
            new Vector2(0.04f, 0.818f),
            new Vector2(0.26f, 0.842f),
            TextAnchor.MiddleLeft,
            13
        );
        enemyLabel.color = TEXT_MUTED;

        enemySlots[0] = CreateCardSlot(
            canvasGO.transform,
            "EnemySlot1",
            new Vector2(0.045f, 0.757f),
            new Vector2(0.120f, 0.815f)
        );
        enemySlots[1] = CreateCardSlot(
            canvasGO.transform,
            "EnemySlot2",
            new Vector2(0.132f, 0.757f),
            new Vector2(0.207f, 0.815f)
        );

        enemyHandTypeText = CreateText(
            canvasGO.transform,
            "EnemyHandType",
            "",
            new Vector2(0.04f, 0.733f),
            new Vector2(0.26f, 0.757f),
            TextAnchor.MiddleLeft,
            14
        );
        enemyHandTypeText.color = TEXT_SOFT;

        // ---------- 日志：左下角 ----------
        CreatePanel(
            canvasGO.transform,
            new Color(0, 0, 0, 0.45f),
            new Vector2(0.02f, 0.02f),
            new Vector2(0.30f, 0.26f)
        );

        GameObject msgLogGO = new GameObject("MessageLog");
        msgLogGO.transform.SetParent(canvasGO.transform, false);
        RectTransform msgLogRT = msgLogGO.AddComponent<RectTransform>();
        msgLogRT.anchorMin = new Vector2(0.03f, 0.03f);
        msgLogRT.anchorMax = new Vector2(0.29f, 0.25f);
        msgLogRT.offsetMin = Vector2.zero;
        msgLogRT.offsetMax = Vector2.zero;

        // Viewport：裁掉溢出的历史消息，右侧留 10px 给滚动条
        GameObject viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(msgLogGO.transform, false);
        RectTransform viewportRT = viewportGO.AddComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.offsetMin = Vector2.zero;
        viewportRT.offsetMax = new Vector2(-10f, 0f);
        viewportGO.AddComponent<RectMask2D>();

        // 透明命中层：没有 Graphic 时鼠标滚轮找不到 ScrollRect，日志就不会滚
        Image viewportHit = viewportGO.AddComponent<Image>();
        viewportHit.color = new Color(0f, 0f, 0f, 0f);
        viewportHit.raycastTarget = true;

        // Content：按内容长高，最旧的在上、最新的在下
        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        RectTransform contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.offsetMin = Vector2.zero;
        contentRT.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.LowerLeft;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 3f;
        vlg.padding = new RectOffset(2, 2, 2, 2);

        ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect sr = msgLogGO.AddComponent<ScrollRect>();
        sr.viewport = viewportRT;
        sr.content = contentRT;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 14f;
        messageLogScroll = sr;
        messageLogContainer = contentGO.transform;

        // 右侧细滚动条（纯代码拼，不依赖素材）
        GameObject barGO = new GameObject("Scrollbar");
        barGO.transform.SetParent(msgLogGO.transform, false);
        RectTransform barRT = barGO.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(1f, 0f);
        barRT.anchorMax = new Vector2(1f, 1f);
        barRT.pivot = new Vector2(1f, 0.5f);
        barRT.sizeDelta = new Vector2(7f, 0f);
        barRT.anchoredPosition = Vector2.zero;
        Image barBg = barGO.AddComponent<Image>();
        barBg.color = new Color(1f, 1f, 1f, 0.05f);

        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(barGO.transform, false);
        RectTransform handleRT = handleGO.AddComponent<RectTransform>();
        handleRT.anchorMin = Vector2.zero;
        handleRT.anchorMax = Vector2.one;
        handleRT.offsetMin = Vector2.zero;
        handleRT.offsetMax = Vector2.zero;
        Image handleImg = handleGO.AddComponent<Image>();
        handleImg.color = new Color(1f, 1f, 1f, 0.22f);

        Scrollbar sb = barGO.AddComponent<Scrollbar>();
        sb.direction = Scrollbar.Direction.BottomToTop;
        sb.handleRect = handleRT;
        sb.targetGraphic = handleImg;

        sr.verticalScrollbar = sb;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        // ---------- 公共牌：正中央 5 个常驻槽位 ----------
        Text communityLabel = CreateText(
            canvasGO.transform,
            "CommunityLabel",
            "COMMUNITY",
            new Vector2(0.35f, 0.665f),
            new Vector2(0.65f, 0.700f),
            TextAnchor.MiddleCenter,
            13
        );
        communityLabel.color = TEXT_MUTED;

        for (int i = 0; i < 5; i++)
        {
            float x0 = 0.290f + i * 0.086f;
            communitySlots[i] = CreateCardSlot(
                canvasGO.transform,
                "CommunitySlot" + i,
                new Vector2(x0, 0.505f),
                new Vector2(x0 + 0.076f, 0.645f)
            );
        }

        // 公共牌牌型（原来那个 52 号大字留作牌型说明）
        communityText = CreateText(
            canvasGO.transform,
            "CommunityHandType",
            "",
            new Vector2(0.15f, 0.455f),
            new Vector2(0.85f, 0.495f),
            TextAnchor.MiddleCenter,
            18
        );
        communityText.color = TEXT_SOFT;

        // ---------- 玩家信息：右下角（与敌方镜像） ----------
        CreatePanel(
            canvasGO.transform,
            PANEL_BG,
            new Vector2(0.72f, 0.19f),
            new Vector2(0.98f, 0.37f)
        );

        playerChipsText = CreateText(
            canvasGO.transform,
            "PlayerChipsText",
            "Player: 0",
            new Vector2(0.74f, 0.340f),
            new Vector2(0.96f, 0.362f),
            TextAnchor.MiddleLeft,
            20
        );
        playerChipsText.color = TEXT_STRONG;

        Text playerLabel = CreateText(
            canvasGO.transform,
            "PlayerLabel",
            "Your Hand",
            new Vector2(0.74f, 0.320f),
            new Vector2(0.96f, 0.338f),
            TextAnchor.MiddleLeft,
            13
        );
        playerLabel.color = TEXT_MUTED;

        playerSlots[0] = CreateCardSlot(
            canvasGO.transform,
            "PlayerSlot1",
            new Vector2(0.740f, 0.238f),
            new Vector2(0.830f, 0.310f)
        );
        playerSlots[1] = CreateCardSlot(
            canvasGO.transform,
            "PlayerSlot2",
            new Vector2(0.842f, 0.238f),
            new Vector2(0.932f, 0.310f)
        );

        // 牌型说明放在玩家面板内部（面板下沿之上），不要吊在面板外面
        playerHandTypeText = CreateText(
            canvasGO.transform,
            "PlayerHandType",
            "",
            new Vector2(0.735f, 0.190f),
            new Vector2(0.972f, 0.228f),
            TextAnchor.UpperLeft,
            12
        );
        playerHandTypeText.color = TEXT_STRONG;
        playerHandTypeText.lineSpacing = 1f;
        playerHandTypeText.verticalOverflow = VerticalWrapMode.Overflow;

        // ---- Swap：改成玩家面板内右上角的中性小按钮 ----
        swapButton = CreateButton(
            canvasGO.transform,
            "SwapBtn",
            "Swap",
            new Vector2(0.872f, 0.340f),
            new Vector2(0.968f, 0.362f),
            BTN_NEUTRAL,
            () => OnSwapClicked()
        );
        swapButton.GetComponentInChildren<Text>().fontSize = 13;

        // ---------- 下注提示（顶栏，底池正下方） ----------
        // 玩家反馈「看不出对手下了多少」，所以把当前注额直接摆到桌面上，
        // 而不是留在左下角那行只给开发看的调试状态里。
        betPillText = CreateText(
            canvasGO.transform,
            "BetPillText",
            "",
            new Vector2(0.30f, 0.885f),
            new Vector2(0.70f, 0.925f),
            TextAnchor.MiddleCenter,
            17
        );
        betPillText.color = new Color(1.00f, 0.78f, 0.30f);
        betPillText.fontStyle = FontStyle.Bold;
        betPillText.gameObject.SetActive(false);

        // ---------- 快捷下注行（底部中段） ----------
        quickBetRow = new GameObject("QuickBetRow");
        quickBetRow.transform.SetParent(canvasGO.transform, false);
        RectTransform qbrRT = quickBetRow.AddComponent<RectTransform>();
        qbrRT.anchorMin = Vector2.zero;
        qbrRT.anchorMax = Vector2.one;
        qbrRT.offsetMin = Vector2.zero;
        qbrRT.offsetMax = Vector2.zero;

        float qbY0 = 0.11f,
            qbY1 = 0.17f;

        // 四个百分比按钮
        CreateButton(
            quickBetRow.transform,
            "Btn33",
            "33%",
            new Vector2(0.33f, qbY0),
            new Vector2(0.40f, qbY1),
            BTN_NEUTRAL,
            () => SetSliderToFraction(0.33f)
        );

        CreateButton(
            quickBetRow.transform,
            "Btn50",
            "50%",
            new Vector2(0.41f, qbY0),
            new Vector2(0.48f, qbY1),
            BTN_NEUTRAL,
            () => SetSliderToFraction(0.50f)
        );

        CreateButton(
            quickBetRow.transform,
            "Btn75",
            "75%",
            new Vector2(0.49f, qbY0),
            new Vector2(0.56f, qbY1),
            BTN_NEUTRAL,
            () => SetSliderToFraction(0.75f)
        );

        CreateButton(
            quickBetRow.transform,
            "BtnMax",
            "Max",
            new Vector2(0.57f, qbY0),
            new Vector2(0.64f, qbY1),
            BTN_NEUTRAL,
            () => SetSliderToFraction(1.0f)
        );

        // 减号
        CreateButton(
            quickBetRow.transform,
            "BtnMinus",
            "-",
            new Vector2(0.65f, qbY0),
            new Vector2(0.69f, qbY1),
            BTN_NEUTRAL,
            () => AdjustSlider(-StepAmount)
        );

        // 滑块
        raiseSlider = CreateSlider(
            quickBetRow.transform,
            "RaiseSlider",
            new Vector2(0.70f, qbY0),
            new Vector2(0.87f, qbY1),
            MinRaise,
            100,
            DefaultRaise
        );

        // 加号
        CreateButton(
            quickBetRow.transform,
            "BtnPlus",
            "+",
            new Vector2(0.88f, qbY0),
            new Vector2(0.92f, qbY1),
            BTN_NEUTRAL,
            () => AdjustSlider(StepAmount)
        );

        // 数值：做成一个小胶囊，别让数字裸在滑块边上
        GameObject valuePill = new GameObject("RaiseValuePill", typeof(RectTransform), typeof(Image));
        valuePill.transform.SetParent(quickBetRow.transform, false);
        RectTransform vpRT = valuePill.GetComponent<RectTransform>();
        vpRT.anchorMin = new Vector2(0.930f, qbY0);
        vpRT.anchorMax = new Vector2(0.985f, qbY1);
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        Image vpImg = valuePill.GetComponent<Image>();
        vpImg.color = new Color(0.03f, 0.03f, 0.03f, 0.95f);

        raiseValueText = CreateText(
            valuePill.transform,
            "RaiseValueText",
            "10",
            Vector2.zero,
            Vector2.one,
            TextAnchor.MiddleCenter,
            18
        );
        raiseValueText.color = TEXT_STRONG;
        raiseValueText.fontStyle = FontStyle.Bold;

        raiseSlider.onValueChanged.AddListener(
            (value) => raiseValueText.text = Mathf.RoundToInt(value).ToString()
        );

        // ---------- 操作按钮行（底部） ----------
        actionRow = new GameObject("ActionRow");
        actionRow.transform.SetParent(canvasGO.transform, false);
        RectTransform arRT = actionRow.AddComponent<RectTransform>();
        arRT.anchorMin = Vector2.zero;
        arRT.anchorMax = Vector2.one;
        arRT.offsetMin = Vector2.zero;
        arRT.offsetMax = Vector2.zero;

        float aY0 = 0.02f,
            aY1 = 0.09f;

        foldButton = CreateButton(
            actionRow.transform,
            "FoldBtn",
            "Fold",
            new Vector2(0.33f, aY0),
            new Vector2(0.48f, aY1),
            BTN_FOLD,
            () => poker.PlayerFold()
        );

        checkButton = CreateButton(
            actionRow.transform,
            "CheckBtn",
            "Check",
            new Vector2(0.49f, aY0),
            new Vector2(0.64f, aY1),
            BTN_CHECK,
            () => poker.PlayerCheck()
        );

        raiseButton = CreateButton(
            actionRow.transform,
            "RaiseBtn",
            "Raise",
            new Vector2(0.65f, aY0),
            new Vector2(0.80f, aY1),
            BTN_RAISE,
            () => poker.PlayerRaise(Mathf.RoundToInt(raiseSlider.value))
        );

        matchButton = CreateButton(
            actionRow.transform,
            "MatchBtn",
            "Match",
            new Vector2(0.81f, aY0),
            new Vector2(0.96f, aY1),
            BTN_MATCH,
            () => poker.PlayerMatchRaise()
        );

        // ---------- Next / Enter Combat 行 ----------
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
            "Next Hand",
            new Vector2(0.38f, 0.02f),
            new Vector2(0.62f, 0.09f),
            new Color(0.2f, 0.6f, 0.35f),
            () => onNextHand?.Invoke()
        );

        nextHandRow.SetActive(false);

        // ---------- 参照面板（M3）----------
        // 放在 BuildUI 末尾：此时 poker 已注入（Initialize 里先 poker = manager 再 BuildUI）。
        tutorial = gameObject.AddComponent<PokerTutorialUI>();
        tutorial.Initialize(poker);
    }

    // =========================================================
    // 卡牌槽位 / 花色 / 按钮强调
    // =========================================================
    private Text CreateCardSlot(Transform parent, string name, Vector2 aMin, Vector2 aMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = SLOT_BG;
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = SLOT_EDGE;
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        return CreateText(
            go.transform,
            "Face",
            "",
            Vector2.zero,
            Vector2.one,
            TextAnchor.MiddleCenter,
            26
        );
    }

    private void RenderSlots(Text[] slots, List<Card> cards, bool hidden)
    {
        RenderSlots(slots, cards, hidden, null);
    }

    /// <summary>
    /// Renders cards into slots. Cards present in <paramref name="bestFive"/> get an amber
    /// border so the player can see which cards their best hand is actually made of.
    /// </summary>
    private void RenderSlots(Text[] slots, List<Card> cards, bool hidden, List<Card> bestFive)
    {
        if (slots == null)
            return;
        int count = cards != null ? cards.Count : 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            bool inBest = false;
            if (!hidden && bestFive != null && i < count && cards[i] != null)
            {
                foreach (Card c in bestFive)
                {
                    if (c != null && c.Rank == cards[i].Rank && c.Suit == cards[i].Suit)
                    {
                        inBest = true;
                        break;
                    }
                }
            }

            if (hidden)
            {
                slots[i].text = "?";
                slots[i].color = TEXT_MUTED;
            }
            else if (i < count)
            {
                slots[i].text = cards[i].ToString();
                slots[i].color = SuitColor(cards[i].Suit);
            }
            else
            {
                slots[i].text = "";
            }

            StyleSlot(slots[i], inBest, hidden);
        }
    }

    /// <summary>
    /// Applies the "this card is in your best five" look to a slot. Resets to the plain look
    /// otherwise, so slots never keep a stale highlight.
    /// </summary>
    private static void StyleSlot(Text slot, bool inBest, bool hidden)
    {
        Image bg = slot.transform.parent.GetComponent<Image>();
        if (bg != null)
            bg.color = inBest && !hidden ? SLOT_BG_BEST : SLOT_BG;

        Outline outline = slot.transform.parent.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = inBest && !hidden ? SLOT_EDGE_BEST : SLOT_EDGE;
            outline.effectDistance = inBest && !hidden
                ? new Vector2(2.5f, -2.5f)
                : new Vector2(1.5f, -1.5f);
        }

        slot.fontStyle = inBest && !hidden ? FontStyle.Bold : FontStyle.Normal;
    }

    private Color SuitColor(Suit suit)
    {
        switch (suit)
        {
            case Suit.Spades:
                return new Color(0.93f, 0.93f, 0.93f);
            case Suit.Hearts:
                return new Color(1.00f, 0.55f, 0.58f);
            case Suit.Clubs:
                return new Color(0.66f, 0.88f, 1.00f);
            case Suit.Diamonds:
                return new Color(1.00f, 0.85f, 0.45f);
            default:
                return Color.white;
        }
    }

    private void SetButtonEmphasis(Button button, Color baseColor, bool primary)
    {
        if (button == null)
            return;
        button.transform.localScale = primary ? new Vector3(1.05f, 1.05f, 1f) : Vector3.one;
        Image img = button.GetComponent<Image>();
        if (img != null)
        {
            img.color = primary
                ? new Color(
                    Mathf.Min(1f, baseColor.r * 1.15f),
                    Mathf.Min(1f, baseColor.g * 1.15f),
                    Mathf.Min(1f, baseColor.b * 1.15f),
                    baseColor.a
                )
                : baseColor;
        }
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

        bool showBet = poker.CurrentBet > 0
            && poker.State != PokerState.HandEnded
            && poker.State != PokerState.Showdown;
        if (betPillText != null)
        {
            betPillText.gameObject.SetActive(showBet);
            if (showBet)
                betPillText.text = poker.LastBetByEnemy
                    ? $"OPPONENT BET {poker.LastBetAmount}   ·   {poker.CurrentBet} TO CALL"
                    : $"YOU BET {poker.LastBetAmount}";
        }

        // PlayerBestHand / EnemyBestHand 只在 showdown 时由 GameManager 算出来（PokerGameManager.cs:650/654），
        // 手牌中途要显示「我现在是什么牌」就得自己按当前已知的牌算一遍。
        HandResult playerBest =
            poker.PlayerBestHand ?? EvaluateLive(poker.PlayerHand, poker.CommunityCards);
        List<Card> playerBestFive = playerBest != null ? playerBest.bestFive : null;

        RenderSlots(playerSlots, poker.PlayerHand, false, playerBestFive);
        RenderSlots(communitySlots, poker.CommunityCards, false, playerBestFive);
        communityText.text = "";

        bool revealEnemy =
            poker.State == PokerState.Showdown
            || poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;

        HandResult enemyBest =
            revealEnemy
                ? (poker.EnemyBestHand ?? EvaluateLive(poker.EnemyHand, poker.CommunityCards))
                : null;
        RenderSlots(enemySlots, poker.EnemyHand, !revealEnemy, enemyBest != null ? enemyBest.bestFive : null);

        // 翻牌前只有两张牌：牌面已经画在上面了，再写一行它们的名字只是重复。
        // 五张牌凑齐后才有牌型可说，这一行从翻牌开始出现。
        playerHandTypeText.text =
            playerBest != null && playerBestFive != null && playerBestFive.Count > 0
                ? "Best five: " + playerBest.Describe() + "\n" + CardStrColored(playerBestFive)
                : "";
        enemyHandTypeText.text = enemyBest != null ? enemyBest.Describe() : "";

        bool isPlayerTurn = poker.State == PokerState.PlayerTurn && !poker.IsDealing;
        bool mustMatch = poker.AwaitingPlayerMatch;
        int maxRaise = poker.GetPlayerMaxRaise();
        bool canRaise = maxRaise >= MinRaise;

        checkButton.interactable = isPlayerTurn && !mustMatch;
        raiseButton.interactable = isPlayerTurn && !mustMatch && canRaise;
        matchButton.interactable = isPlayerTurn && mustMatch;
        foldButton.interactable = isPlayerTurn;

        // 可用动作放大 + 提亮，玩家一眼看到现在能点什么
        SetButtonEmphasis(
            checkButton,
            BTN_CHECK,
            isPlayerTurn && !mustMatch
        );
        SetButtonEmphasis(
            foldButton,
            BTN_FOLD,
            isPlayerTurn && !mustMatch
        );
        SetButtonEmphasis(
            raiseButton,
            BTN_RAISE,
            isPlayerTurn && !mustMatch && canRaise
        );
        SetButtonEmphasis(matchButton, BTN_MATCH, isPlayerTurn && mustMatch);

        // Swap 按钮：只有在可以换牌时才亮
        if (swapButton != null)
        {
            swapButton.interactable = poker.CanSwapCard;
            swapButton.gameObject.SetActive(poker.CanSwapCard);
        }

        if (swapChoiceRow != null && swapChoiceRow.activeSelf && !poker.CanSwapCard)
        {
            swapChoiceRow.SetActive(false);
        }

        bool handOver =
            poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;

        quickBetRow.SetActive(!handOver && canRaise);
        actionRow.SetActive(!handOver);
        nextHandRow.SetActive(handOver && nextHandVisible);

        int sliderMax = Mathf.Max(MinRaise, maxRaise);
        raiseSlider.minValue = MinRaise;
        raiseSlider.maxValue = sliderMax;
        raiseSlider.interactable = canRaise && isPlayerTurn && !mustMatch;

        if (!hasInitialized || poker.CurrentStreet != lastStreet)
        {
            lastStreet = poker.CurrentStreet;
            hasInitialized = true;
            float resetValue = Mathf.Clamp(DefaultRaise, MinRaise, sliderMax);
            raiseSlider.value = resetValue;
        }
        if (raiseSlider.value > sliderMax)
            raiseSlider.value = sliderMax;
        raiseValueText.text = canRaise ? Mathf.RoundToInt(raiseSlider.value).ToString() : "-";

        SetQuickBetButtonsInteractable(canRaise && isPlayerTurn && !mustMatch);
    }

    /// <summary>
    /// Best five from the cards known right now (hole cards + community so far).
    /// Returns null while fewer than five cards are known, so the hand line stays
    /// empty instead of naming a hand that is not settled yet.
    /// </summary>
    private static HandResult EvaluateLive(List<Card> hole, List<Card> community)
    {
        List<Card> all = new List<Card>();
        if (hole != null)
            all.AddRange(hole);
        if (community != null)
            all.AddRange(community);
        if (all.Count < 5)
            return null;
        return HandEvaluator.Evaluate(all);
    }

    private string CardStrColored(List<Card> cards)
    {
        if (cards == null || cards.Count == 0)
            return "";
        string s = "";
        foreach (Card c in cards)
        {
            string hex;
            switch (c.Suit)
            {
                case Suit.Spades:
                    hex = "#A0A0A0";
                    break;
                case Suit.Hearts:
                    hex = "#FF8080";
                    break;
                case Suit.Clubs:
                    hex = "#90E0FF";
                    break;
                case Suit.Diamonds:
                    hex = "#FFD060";
                    break;
                default:
                    hex = "#FFFFFF";
                    break;
            }
            s += $"<color={hex}>{c}</color>  ";
        }
        return s;
    }

    private void AddMessage(string msg, LogKind kind)
    {
        // 1. 创建新的文本对象
        GameObject textGO = new GameObject("Msg");
        textGO.transform.SetParent(messageLogContainer, false);

        // 2. 放到最后（VerticalLayoutGroup 里最新的在最下面）
        textGO.transform.SetAsLastSibling();

        // 3. 设置文本属性
        Text txt = textGO.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 16;

        // 一眼分得出是谁的动作：你=白、对手=冷灰、街道/结算=琥珀加粗、其余=系统灰
        Color lineColor;
        switch (kind)
        {
            case LogKind.Player:
                lineColor = new Color(0.94f, 0.94f, 0.94f, 1f);
                break;
            case LogKind.Enemy:
                lineColor = new Color(0.72f, 0.79f, 0.88f, 1f);
                break;
            case LogKind.Street:
                lineColor = new Color(1.00f, 0.78f, 0.30f, 1f);
                break;
            default:
                lineColor = new Color(0.78f, 0.78f, 0.78f, 1f);
                break;
        }
        txt.color = lineColor;
        txt.fontStyle = kind == LogKind.Street ? FontStyle.Bold : FontStyle.Normal;

        // 街道行前面空一行，把一条街的动作聚成一块
        txt.text = kind == LogKind.Street && messageLogContainer.childCount > 1 ? "\n" + msg : msg;
        txt.alignment = TextAnchor.UpperLeft;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.raycastTarget = false; // 让滚轮/拖动都落到 ScrollRect 上

        // 4. 超过上限时裁掉最旧的一条（历史不再按时间消失，只按条数封顶）
        while (messageLogContainer.childCount > MaxLogEntries)
        {
            Transform oldest = messageLogContainer.GetChild(0);
            oldest.SetParent(null); // 先脱离父物体，避免在销毁时触发布局重建
            Destroy(oldest.gameObject);
        }

        // 5. 强制刷新布局，防止 UI 延迟或重叠
        LayoutRebuilder.ForceRebuildLayoutImmediate(
            messageLogContainer.GetComponent<RectTransform>()
        );

        // 6. 玩家贴在底部时跟着新消息走；正在往上翻历史就不打断他
        if (stickToLogBottom && messageLogScroll != null)
            messageLogScroll.verticalNormalizedPosition = 0f;
    }

    // 每帧末量一次：玩家是不是还贴在日志底部。放到 LateUpdate 里量，
    // 是因为 ScrollRect.onValueChanged 在赋值当帧读到的是旧位置，判不准。
    private void LateUpdate()
    {
        if (messageLogScroll == null || messageLogScroll.content == null || messageLogScroll.viewport == null)
            return;

        float contentH = messageLogScroll.content.rect.height;
        float viewH = messageLogScroll.viewport.rect.height;
        // 内容装得下，或者已经滚到底部 2% 以内，都算“贴着底部”
        stickToLogBottom = contentH <= viewH + 1f || messageLogScroll.verticalNormalizedPosition <= 0.02f;
    }

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

    /// <summary>
    /// 点击 Swap 按钮：显示换牌选择弹窗，让玩家选换哪张。
    /// </summary>
    private void OnSwapClicked()
    {
        Debug.Log(
            $"[Swap] 按钮被点击。CanSwapCard={poker.CanSwapCard}, "
                + $"SwapsRemaining={poker.SwapsRemaining}"
        );

        if (swapChoiceRow == null)
            BuildSwapChoiceRow();

        // 已经显示 → 关闭
        if (swapChoiceRow.activeSelf)
        {
            swapChoiceRow.SetActive(false);
            return;
        }

        RefreshSwapChoiceContent();
        swapChoiceRow.SetActive(true);
    }

    private void BuildSwapChoiceRow()
    {
        swapChoiceRow = new GameObject("SwapChoiceRow");
        swapChoiceRow.transform.SetParent(swapButton.transform.parent, false);
        RectTransform rt = swapChoiceRow.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // ---- 全屏暗色遮罩（点击外部也不关闭，但视觉上聚焦弹窗） ----
        CreatePanel(
            swapChoiceRow.transform,
            new Color(0f, 0f, 0f, 0.55f),
            Vector2.zero,
            Vector2.one
        );

        // ---- 中央卡片 ----
        CreatePanel(
            swapChoiceRow.transform,
            new Color(0.12f, 0.16f, 0.14f, 0.98f),
            new Vector2(0.28f, 0.35f),
            new Vector2(0.72f, 0.65f)
        );

        // ---- 标题 ----
        CreateText(
            swapChoiceRow.transform,
            "SwapTitle",
            "Swap which one？",
            new Vector2(0.28f, 0.58f),
            new Vector2(0.72f, 0.64f),
            TextAnchor.MiddleCenter,
            26
        );

        // ---- 提示 ----
        CreateText(
            swapChoiceRow.transform,
            "SwapHint",
            "Discard 1 hand card and draw a new one from the deck",
            new Vector2(0.28f, 0.53f),
            new Vector2(0.72f, 0.58f),
            TextAnchor.MiddleCenter,
            16
        ).color = new Color(0.65f, 0.65f, 0.65f);

        // ---- 卡片 1 ----
        swapCard1Button = CreateButton(
            swapChoiceRow.transform,
            "SwapCard1",
            "—",
            new Vector2(0.31f, 0.41f),
            new Vector2(0.49f, 0.51f),
            new Color(0.45f, 0.30f, 0.65f),
            () =>
            {
                poker.PlayerSwapCard(0);
                swapChoiceRow.SetActive(false);
            }
        );

        // ---- 卡片 2 ----
        swapCard2Button = CreateButton(
            swapChoiceRow.transform,
            "SwapCard2",
            "—",
            new Vector2(0.51f, 0.41f),
            new Vector2(0.69f, 0.51f),
            new Color(0.45f, 0.30f, 0.65f),
            () =>
            {
                poker.PlayerSwapCard(1);
                swapChoiceRow.SetActive(false);
            }
        );

        // ---- 取消按钮 ----
        CreateButton(
            swapChoiceRow.transform,
            "SwapCancel",
            "Cancel",
            new Vector2(0.42f, 0.36f),
            new Vector2(0.58f, 0.40f),
            BTN_NEUTRAL,
            () => swapChoiceRow.SetActive(false)
        );

        swapChoiceRow.SetActive(false);
    }

    /// <summary>每次打开时把当前手牌信息填进两个按钮的 label</summary>
    private void RefreshSwapChoiceContent()
    {
        if (swapCard1Button == null || swapCard2Button == null)
            return;

        SetButtonLabel(swapCard1Button, LabelForHandIndex(0));
        SetButtonLabel(swapCard2Button, LabelForHandIndex(1));
    }

    private string LabelForHandIndex(int i)
    {
        var hand = poker.PlayerHand;
        if (hand == null || i >= hand.Count)
            return "—";
        var c = hand[i];
        string hex;
        switch (c.Suit)
        {
            case Suit.Spades:
                    hex = "#A0A0A0";
                    break;
                case Suit.Hearts:
                    hex = "#FF8080";
                    break;
                case Suit.Clubs:
                    hex = "#90E0FF";
                    break;
                case Suit.Diamonds:
                    hex = "#FFD060";
                    break;
                default:
                    hex = "#FFFFFF";
                    break;
        }
        return $"Swap <color={hex}>{c}</color>";
    }

    private void SetButtonLabel(Button btn, string text)
    {
        var labelT = btn.transform.Find("Label");
        if (labelT == null)
            return;
        var txt = labelT.GetComponent<Text>();
        if (txt != null)
            txt.text = text;
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
