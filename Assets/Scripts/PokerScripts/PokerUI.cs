using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PokerUI : MonoBehaviour
{
    private PokerGameManager poker;

    // Text fields.
    private Text potText;
    private Text playerChipsText;
    private Text playerChipsValueText;
    private Text enemyChipsText;
    private Text betPillText;
    private Text playerHandText;
    private Text enemyHandText;
    private Text communityText; // 改成显示公共牌牌型文字
    private Text[] communitySlots = new Text[5];
    private Text[] playerSlots = new Text[3];
    private Text[] enemySlots = new Text[2];

    /// <summary>How many enemy cards are shown face up while the hand is still running (Cheat Glove).</summary>
    private int enemyRevealCount;

    /// <summary>Warm tint for a card that is only visible because of an item.</summary>
    private static readonly Color REVEAL_TINT = new Color(1f, 0.93f, 0.72f, 1f);
    private Text sliderMinLabel;
    private Text sliderMaxLabel;
    private Text playerHandTypeText;
    private Image turnBar;
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

    // 危险动作的「再点一次确认」窗口（现在只有 Fold 用）
    private const float FoldConfirmSeconds = 2.5f;
    private float foldArmedAt;
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
    private static readonly Color SLOT_BG = new Color(0.10f, 0.165f, 0.10f, 0.95f);   // #1A2A19
    private static readonly Color SLOT_EDGE = new Color(0.31f, 0.38f, 0.29f, 1f);       // #4F6149
    private static readonly Color PANEL_EDGE = new Color(0.31f, 0.38f, 0.29f, 1f);      // #4F6149
    private static readonly Color CTRL_EDGE = new Color(0.43f, 0.50f, 0.42f, 1f);       // #6E7F6A
    private static readonly Color CTRL_EDGE_OFF = new Color(0.23f, 0.29f, 0.22f, 1f);    // #3A4A38
    private static readonly Color TEXT_LIVE = new Color(0.94f, 0.94f, 0.94f, 1f);
    private static readonly Color TEXT_OFF = new Color(0.42f, 0.48f, 0.41f, 1f);       // #6C7A69
    private static readonly Color AMBER_HISTORY = new Color(0.56f, 0.61f, 0.55f, 1f);   // #8F9B8C
    // 参与当前 best-five 的牌用这个边框，把「为什么是你的最好五张」画出来
    private static readonly Color SLOT_EDGE_BEST = new Color(1.00f, 0.78f, 0.30f, 0.95f);
    private static readonly Color SLOT_BG_BEST = new Color(0.12f, 0.11f, 0.06f, 0.97f);
    // 动作按钮三档：退出（幽灵）/ 维持（Check、Match 同族）/ 推进（Raise 唯一重音）
    // 可点的按钮必须比面板/桌面明显亮一档，否则玩家分不清哪几个能按
    private static readonly Color BTN_FOLD = new Color(0.176f, 0.243f, 0.157f);           // #2D3E28
    private static readonly Color BTN_CHECK = new Color(0.208f, 0.290f, 0.212f);           // #354A36
    private static readonly Color BTN_MATCH = new Color(0.255f, 0.341f, 0.224f);           // #415739
    private static readonly Color BTN_RAISE = new Color(0.949f, 0.702f, 0.239f);           // #F2B33D
    private static readonly Color EDGE_FOLD = new Color(0.478f, 0.227f, 0.204f);          // #7A3A34
    private static readonly Color EDGE_CHECK = new Color(0.310f, 0.380f, 0.286f);         // #4F6149
    private static readonly Color EDGE_MATCH = new Color(0.561f, 0.627f, 0.498f);         // #8FA07F
    private static readonly Color EDGE_RAISE = new Color(1.000f, 0.878f, 0.627f);         // #FFE0A0
    private static readonly Color TEXT_ON_FOLD = new Color(0.851f, 0.549f, 0.522f);       // #D98C85
    private static readonly Color TEXT_ON_DARK = new Color(0.910f, 0.929f, 0.902f);       // #E8EDE6
    private static readonly Color TEXT_ON_RAISE = new Color(0.102f, 0.071f, 0.024f);      // #1A1206
    private Sprite actionButtonSprite;
    private static readonly Color BTN_NEUTRAL = new Color(0.278f, 0.298f, 0.341f);        // 快捷下注：比桌面亮，明确可点

    // Tracking for slider reset on street change.
    private Street lastStreet = Street.PreFlop;
    private bool hasInitialized = false;

    private Button swapButton;
    private GameObject swapChoiceRow;
    private Text swapPeekText;
    private Transform canvasRoot;
    private Button swapCard1Button;
    private Button swapCard2Button;
    private Button swapCard3Button;

    // M3 引导层（? 参照面板 + 本手结果行），自建 Canvas order 300。
    private PokerTutorialUI tutorial;

    // 牌桌自己的道具面板（共享 HUD 在 poker 场景已关）。
    private PokerItemsPanel itemsPanel;

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
        // match 0 只按宽度缩放：窗口一变扁（不是 16:9）上下就被裁掉，面板直接看不见。
        // 0.5 在宽高之间取中，任何窗口比例下整张桌子都还在画面里。
        scaler.matchWidthOrHeight = 0.5f;

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
            33
        );
        potText.fontStyle = FontStyle.Bold;

        // ---------- 敌人信息：左上角 ----------
        CreatePanel(
            canvasGO.transform,
            PANEL_BG,
            new Vector2(0.0204f, 0.598f),
            new Vector2(0.2153f, 0.912f)
        );

        enemyNameText = CreateText(
            canvasGO.transform,
            "EnemyNameText",
            "",
            new Vector2(0.0334f, 0.882f),
            new Vector2(0.1270f, 0.910f),
            TextAnchor.MiddleLeft,
            22
        );
        enemyNameText.fontStyle = FontStyle.Bold;

        enemyChipsText = CreateText(
            canvasGO.transform,
            "EnemyChipsText",
            "0 chips",
            new Vector2(0.129f, 0.882f),
            new Vector2(0.2103f, 0.910f),
            TextAnchor.MiddleRight,
            16
        );
        enemyChipsText.color = TEXT_STRONG;


        enemySlots[0] = CreateCardSlot(
            canvasGO.transform,
            "EnemySlot1",
            new Vector2(0.0297f, 0.658f),
            new Vector2(0.1144f, 0.8747f)
        );
        enemySlots[1] = CreateCardSlot(
            canvasGO.transform,
            "EnemySlot2",
            new Vector2(0.1213f, 0.658f),
            new Vector2(0.2060f, 0.8747f)
        );

        enemyHandTypeText = CreateText(
            canvasGO.transform,
            "EnemyHandType",
            "",
            new Vector2(0.0334f, 0.616f),
            new Vector2(0.2103f, 0.656f),
            TextAnchor.MiddleLeft,
            18
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
            new Vector2(0.33f, 0.684f),
            new Vector2(0.67f, 0.738f),
            TextAnchor.MiddleCenter,
            15
        );
        communityLabel.color = TEXT_STRONG;
        communityLabel.fontStyle = FontStyle.Bold;

        for (int i = 0; i < 5; i++)
        {
            // 公共牌比手牌大一档：148x213（屏幕像素）
            float x0 = 0.2496f + i * 0.1015f;
            communitySlots[i] = CreateCardSlot(
                canvasGO.transform,
                "CommunitySlot" + i,
                new Vector2(x0, 0.4369f),
                new Vector2(x0 + 0.0949f, 0.6798f)
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
            new Vector2(0.7813f, 0.156f),
            new Vector2(0.9762f, 0.470f)
        );

        playerChipsText = CreateText(
            canvasGO.transform,
            "PlayerChipsText",
            "You",
            new Vector2(0.7915f, 0.440f),
            new Vector2(0.860f, 0.468f),
            TextAnchor.MiddleLeft,
            22
        );
        playerChipsText.color = TEXT_STRONG;

        playerSlots[0] = CreateCardSlot(
            canvasGO.transform,
            "PlayerSlot1",
            new Vector2(0.7915f, 0.216f),
            new Vector2(0.8762f, 0.4327f)
        );
        playerSlots[1] = CreateCardSlot(
            canvasGO.transform,
            "PlayerSlot2",
            new Vector2(0.8813f, 0.216f),
            new Vector2(0.966f, 0.4327f)
        );
        // 第三张手牌：只有「六张取五」时才会显示（LayoutPlayerSlots 负责排布与显隐）。
        playerSlots[2] = CreateCardSlot(
            canvasGO.transform,
            "PlayerSlot3",
            new Vector2(0.8813f, 0.216f),
            new Vector2(0.966f, 0.4327f)
        );
        playerSlots[2].transform.parent.gameObject.SetActive(false);

        // 牌型说明放在玩家面板内部（面板下沿之上），不要吊在面板外面
        playerHandTypeText = CreateText(
            canvasGO.transform,
            "PlayerHandType",
            "",
            new Vector2(0.786f, 0.174f),
            new Vector2(0.972f, 0.214f),
            TextAnchor.MiddleLeft,
            20
        );
        playerHandTypeText.color = new Color(1.00f, 0.78f, 0.30f);

        // 轮到你时面板顶一条 2px 琥珀：状态有了落点，不用去日志里找
        GameObject turnBarGO = new GameObject("TurnBar", typeof(RectTransform), typeof(Image));
        turnBarGO.transform.SetParent(canvasGO.transform, false);
        RectTransform tbRT = turnBarGO.GetComponent<RectTransform>();
        tbRT.anchorMin = new Vector2(0.7813f, 0.4680f);
        tbRT.anchorMax = new Vector2(0.9762f, 0.4710f);
        tbRT.offsetMin = Vector2.zero;
        tbRT.offsetMax = Vector2.zero;
        turnBar = turnBarGO.GetComponent<Image>();
        turnBar.color = new Color(1.00f, 0.78f, 0.30f, 1f);
        turnBar.raycastTarget = false;
        turnBar.gameObject.SetActive(false);
        playerHandTypeText.lineSpacing = 1f;
        // 单行显示，靠 best-fit 自动缩到盒宽以内（Wrap + Truncate 会把后半句切掉）
        playerHandTypeText.horizontalOverflow = HorizontalWrapMode.Overflow;
        playerHandTypeText.verticalOverflow = VerticalWrapMode.Truncate;
        playerHandTypeText.resizeTextForBestFit = true;
        playerHandTypeText.resizeTextMinSize = 10;
        playerHandTypeText.resizeTextMaxSize = 18;

        // ---- Swap：改成玩家面板内右上角的中性小按钮 ----
        playerChipsValueText = CreateText(
            canvasGO.transform,
            "PlayerChipsValue",
            "0 chips",
            new Vector2(0.862f, 0.440f),
            new Vector2(0.966f, 0.468f),
            TextAnchor.MiddleRight,
            16
        );
        playerChipsValueText.color = TEXT_SOFT;

        swapButton = CreateButton(
            canvasGO.transform,
            "SwapBtn",

            "Swap",
            new Vector2(0.7813f, 0.478f),
            new Vector2(0.9762f, 0.508f),
            BTN_NEUTRAL,
            () => OnSwapClicked()
        );
        swapButton.GetComponentInChildren<Text>().fontSize = 14;

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

        float qbY0 = 0.098f,
            qbY1 = 0.150f;

        // 四个百分比按钮
        CreateButton(
            quickBetRow.transform,
            "Btn33",
            "33%",
            new Vector2(0.3300f, qbY0),
            new Vector2(0.4013f, qbY1),
            BTN_NEUTRAL,
            () => SetSliderToFraction(0.33f)
        );

        CreateButton(
            quickBetRow.transform,
            "Btn50",
            "50%",
            new Vector2(0.4064f, qbY0),
            new Vector2(0.4777f, qbY1),
            BTN_NEUTRAL,
            () => SetSliderToFraction(0.50f)
        );

        CreateButton(
            quickBetRow.transform,
            "Btn75",
            "75%",
            new Vector2(0.4828f, qbY0),
            new Vector2(0.5541f, qbY1),
            BTN_NEUTRAL,
            () => SetSliderToFraction(0.75f)
        );

        CreateButton(
            quickBetRow.transform,
            "BtnMax",
            "All in",
            new Vector2(0.5592f, qbY0),
            new Vector2(0.6305f, qbY1),
            BTN_NEUTRAL,
            () => SetSliderToFraction(1.0f)
        );

        // 减号
        CreateButton(
            quickBetRow.transform,
            "BtnMinus",
            "-",
            new Vector2(0.6360f, qbY0),
            new Vector2(0.6786f, qbY1),
            BTN_NEUTRAL,
            () => AdjustSlider(-StepAmount)
        );

        // 滑块
        raiseSlider = CreateSlider(
            quickBetRow.transform,
            "RaiseSlider",
            new Vector2(0.6836f, qbY0),
            new Vector2(0.8540f, qbY1),
            MinRaise,
            100,
            DefaultRaise
        );

        // 加号
        CreateButton(
            quickBetRow.transform,
            "BtnPlus",
            "+",
            new Vector2(0.8590f, qbY0),
            new Vector2(0.9040f, qbY1),
            BTN_NEUTRAL,
            () => AdjustSlider(StepAmount)
        );

        // 数值：做成一个小胶囊，别让数字裸在滑块边上
        GameObject valuePill = new GameObject("RaiseValuePill", typeof(RectTransform), typeof(Image));
        valuePill.transform.SetParent(quickBetRow.transform, false);
        RectTransform vpRT = valuePill.GetComponent<RectTransform>();
        vpRT.anchorMin = new Vector2(0.9090f, qbY0);
        vpRT.anchorMax = new Vector2(0.9761f, qbY1);
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        Image vpImg = valuePill.GetComponent<Image>();
        vpImg.color = new Color(0.176f, 0.196f, 0.176f, 1f);
        Outline vpEdge = valuePill.AddComponent<Outline>();
        vpEdge.effectColor = CTRL_EDGE;
        vpEdge.effectDistance = new Vector2(1f, -1f);

        raiseValueText = CreateText(
            valuePill.transform,
            "RaiseValueText",
            "10",
            Vector2.zero,
            Vector2.one,
            TextAnchor.MiddleCenter,
            20
        );
        raiseValueText.color = new Color(1.00f, 0.78f, 0.30f);
        raiseValueText.fontStyle = FontStyle.Bold;

        raiseSlider.onValueChanged.AddListener(value =>
        {
            int v = Mathf.RoundToInt(value);
            raiseValueText.text = v.ToString();
            // Raise 按钮上的金额也跟着滑条走（否则点 All in / 拖滑条后按钮还显示旧数字）
            if (raiseButton != null && raiseButton.interactable)
            {
                Text btnLabel = raiseButton.GetComponentInChildren<Text>();
                if (btnLabel != null)
                    btnLabel.text = "Raise " + v;
            }
        });

        // ---------- 操作按钮行（底部） ----------
        actionRow = new GameObject("ActionRow");
        actionRow.transform.SetParent(canvasGO.transform, false);
        RectTransform arRT = actionRow.AddComponent<RectTransform>();
        arRT.anchorMin = Vector2.zero;
        arRT.anchorMax = Vector2.one;
        arRT.offsetMin = Vector2.zero;
        arRT.offsetMax = Vector2.zero;

        float aY0 = 0.020f,
            aY1 = 0.086f;

        // 宽度按语义权重：退出窄、维持等宽、推进最宽；Check 与 Match 相邻（同一个选择的两面）
        foldButton = CreateButton(
            actionRow.transform,
            "FoldBtn",
            "Fold",
            new Vector2(0.3198f, aY0),
            new Vector2(0.4511f, aY1),
            BTN_FOLD,
            () => OnFoldClicked()
        );
        StyleActionButton(foldButton, EDGE_FOLD, TEXT_ON_FOLD, 19, false);

        checkButton = CreateButton(
            actionRow.transform,
            "CheckBtn",
            "Check",
            new Vector2(0.4573f, aY0),
            new Vector2(0.6136f, aY1),
            BTN_CHECK,
            () => poker.PlayerCheck()
        );
        StyleActionButton(checkButton, EDGE_CHECK, TEXT_ON_DARK, 20, false);

        matchButton = CreateButton(
            actionRow.transform,
            "MatchBtn",
            "Match",
            new Vector2(0.6198f, aY0),
            new Vector2(0.7761f, aY1),
            BTN_MATCH,
            () => poker.PlayerMatchRaise()
        );
        StyleActionButton(matchButton, EDGE_MATCH, TEXT_ON_DARK, 20, false);

        raiseButton = CreateButton(
            actionRow.transform,
            "RaiseBtn",
            "Raise",
            new Vector2(0.7823f, aY0),
            new Vector2(0.9761f, aY1),
            BTN_RAISE,
            () => OnRaiseClicked()
        );
        StyleActionButton(raiseButton, EDGE_RAISE, TEXT_ON_RAISE, 22, true);

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

        // ---------- 道具面板（poker 专用）----------
        // 共享的 FpsOwnedItemsHud 在 poker 场景里已关掉，这里给牌桌一份紧凑版本。
        itemsPanel = gameObject.AddComponent<PokerItemsPanel>();
        itemsPanel.Initialize(canvasGO.transform);
    }

    // =========================================================
    // 卡牌槽位 / 花色 / 按钮强调
    // =========================================================
    // ---------------------------------------------------------
    // 卡面美术
    // 55 张 sprite 放在 Resources/PokerUI/Cards 下（52 正面 + 3 背面）。
    // 卡槽仍然是「底图 + 文本」结构，文本留作没有 sprite 时的兜底。
    // ---------------------------------------------------------
    private const string CardSpriteRoot = "PokerUI/Cards";
    private const string CardBackSprite = "back";
    private static readonly Dictionary<string, Sprite> cardSpriteCache =
        new Dictionary<string, Sprite>();

    private static string CardSpriteKey(Card card)
    {
        if (card == null)
            return null;

        string rank;
        switch (card.Rank)
        {
            case Rank.Two: rank = "2"; break;
            case Rank.Three: rank = "3"; break;
            case Rank.Four: rank = "4"; break;
            case Rank.Five: rank = "5"; break;
            case Rank.Six: rank = "6"; break;
            case Rank.Seven: rank = "7"; break;
            case Rank.Eight: rank = "8"; break;
            case Rank.Nine: rank = "9"; break;
            case Rank.Ten: rank = "10"; break;
            case Rank.Jack: rank = "J"; break;
            case Rank.Queen: rank = "Q"; break;
            case Rank.King: rank = "K"; break;
            case Rank.Ace: rank = "A"; break;
            default: return null;
        }

        string suit;
        switch (card.Suit)
        {
            case Suit.Spades: suit = "S"; break;
            case Suit.Hearts: suit = "H"; break;
            case Suit.Clubs: suit = "C"; break;
            case Suit.Diamonds: suit = "D"; break;
            default: return null;
        }

        return rank + suit;
    }

    private static Sprite LoadCardSprite(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        Sprite cached;
        if (cardSpriteCache.TryGetValue(key, out cached))
            return cached;

        Sprite loaded = Resources.Load<Sprite>(CardSpriteRoot + "/" + key);
        cardSpriteCache[key] = loaded;
        return loaded;
    }

    /// <summary>
    /// 玩家手牌区的排布：手里有几张牌就摊几格（2 张或 3 张），
    /// 「六张取五」多出来的那张不会挤成一条缝。没用的格子直接关掉。
    /// </summary>
    private void LayoutPlayerSlots(int count)
    {
        const float left = 0.7915f;
        const float right = 0.966f;
        const float gap = 0.0051f;

        int shown = Mathf.Clamp(count, 1, playerSlots.Length);
        float width = (right - left - gap * (shown - 1)) / shown;

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] == null)
                continue;

            RectTransform rt = playerSlots[i].transform.parent as RectTransform;
            if (rt == null)
                continue;

            bool visible = i < shown;
            rt.gameObject.SetActive(visible);
            if (!visible)
                continue;

            float x0 = left + i * (width + gap);
            rt.anchorMin = new Vector2(x0, 0.216f);
            rt.anchorMax = new Vector2(x0 + width, 0.4327f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

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

        // 卡面图片：铺满槽位，留 2px 让下面那层边框还能露出来
        GameObject artGO = new GameObject("Art", typeof(RectTransform), typeof(Image));
        artGO.transform.SetParent(go.transform, false);
        RectTransform artRT = artGO.GetComponent<RectTransform>();
        artRT.anchorMin = Vector2.zero;
        artRT.anchorMax = Vector2.one;
        artRT.offsetMin = new Vector2(2f, 2f);
        artRT.offsetMax = new Vector2(-2f, -2f);
        Image artImg = artGO.GetComponent<Image>();
        artImg.color = Color.white;
        artImg.raycastTarget = false;
        artImg.enabled = false;
        Outline artOutline = artGO.AddComponent<Outline>();
        artOutline.effectColor = SLOT_EDGE;
        artOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Text face = CreateText(
            go.transform,
            "Face",
            "",
            Vector2.zero,
            Vector2.one,
            TextAnchor.MiddleCenter,
            26
        );
        face.raycastTarget = false;
        return face;
    }

    private void RenderSlots(Text[] slots, List<Card> cards, bool hidden)
    {
        RenderSlots(slots, cards, hidden, null);
    }

    /// <summary>
    /// Renders cards into slots. Cards present in <paramref name="bestFive"/> get an amber
    /// border so the player can see which cards their best hand is actually made of.
    /// </summary>
    private void RenderSlots(Text[] slots, List<Card> cards, bool hidden, List<Card> bestFive, int revealFirst = 0)
    {
        if (slots == null)
            return;
        int count = cards != null ? cards.Count : 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                continue;

            // revealFirst is how many cards stay face up even though the whole hand is hidden.
            bool hideThis = hidden && i >= revealFirst;
            bool revealed = hidden && i < revealFirst;

            bool inBest = false;
            if (!hideThis && bestFive != null && i < count && cards[i] != null)
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

            Image art = slots[i].transform.parent.Find("Art").GetComponent<Image>();
            Sprite sprite = null;
            string fallback = "";

            if (hideThis)
            {
                sprite = LoadCardSprite(CardBackSprite);
                fallback = "?";
            }
            else if (i < count)
            {
                sprite = LoadCardSprite(CardSpriteKey(cards[i]));
                fallback = cards[i].ToString();
            }

            // 有图就用图，没有图（或资源缺失）退回文字，不能出现空白槽
            if (sprite != null)
            {
                art.enabled = true;
                art.sprite = sprite;
                art.preserveAspect = true;
                slots[i].text = "";
            }
            else
            {
                art.enabled = false;
                slots[i].text = fallback;
            }

            // A revealed card keeps the normal face, tinted so it reads as "seen through an item".
            if (art != null)
                art.color = revealed ? REVEAL_TINT : Color.white;

            slots[i].color = hideThis ? TEXT_MUTED : Color.white;
            StyleSlot(slots[i], art, inBest, hideThis);
        }
    }

    /// <summary>
    /// Applies the "this card is in your best five" look to a slot. Resets to the plain look
    /// otherwise, so slots never keep a stale highlight.
    /// </summary>
    private static void StyleSlot(Text slot, Image art, bool inBest, bool hidden)
    {
        bool light = inBest && !hidden;
        bool hasArt = art != null && art.enabled;

        Image bg = slot.transform.parent.GetComponent<Image>();
        if (bg != null)
            bg.color = hasArt ? Color.clear : (light ? SLOT_BG_BEST : SLOT_BG);

        Outline outline = slot.transform.parent.GetComponent<Outline>();
        if (outline != null)
        {
            // 有卡面图时，槽位底层的边框会露在图外面（多出一圈错位的框），直接关掉
            outline.enabled = !hasArt;
            outline.effectColor = light ? SLOT_EDGE_BEST : SLOT_EDGE;
            outline.effectDistance = light ? new Vector2(2.5f, -2.5f) : new Vector2(1.5f, -1.5f);
        }

        // 有卡面图时，边框要画在图上（画在底下会被图盖住）
        Outline artOutline = art != null ? art.GetComponent<Outline>() : null;
        if (artOutline != null)
        {
            artOutline.effectColor = light ? SLOT_EDGE_BEST : SLOT_EDGE;
            artOutline.effectDistance = light ? new Vector2(2.5f, -2.5f) : new Vector2(1.5f, -1.5f);
        }

        slot.fontStyle = light ? FontStyle.Bold : FontStyle.Normal;
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

    // 按钮的层级由颜色分档承担，不再用缩放/提亮（整行跳动比没反馈更廉价）
    private static Color DimOf(Color c)
    {
        return new Color(
            Mathf.Lerp(0.20f, c.r, 0.42f),
            Mathf.Lerp(0.24f, c.g, 0.42f),
            Mathf.Lerp(0.20f, c.b, 0.42f),
            c.a * 0.75f
        );
    }

    private static void DimLabel(Button b, bool live, Color baseColor)
    {
        if (b == null)
            return;
        Text t = b.GetComponentInChildren<Text>();
        if (t == null)
            return;
        // 两个方向都要写：只压不还原的话，一旦某个回合被压暗，之后再也亮不回来
        t.color = live ? baseColor : DimOf(baseColor);
    }

    private void StyleActionButton(Button b, Color edgeColor, Color textColor, int fontSize, bool bold)
    {
        if (b == null)
            return;
        Outline edge = b.GetComponent<Outline>();
        if (edge != null)
            edge.effectColor = edgeColor;
        Text label = b.GetComponentInChildren<Text>();
        if (label != null)
        {
            label.color = textColor;
            label.fontSize = fontSize;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    // Fold 与超大额 Raise 都要按两下：第一下只改文案，第二下才真的执行
    private void OnFoldClicked()
    {
        if (foldArmedAt <= 0f)
        {
            foldArmedAt = Time.time;
            Text label = foldButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = "Sure?";
            return;
        }
        foldArmedAt = 0f;
        poker.PlayerFold();
    }

    private void OnRaiseClicked()
    {
        // 不做二次确认（Ben: all-in 确认太麻烦），快捷里的 All in 按钮直接下满
        int amount = Mathf.RoundToInt(raiseSlider.value);
        poker.PlayerRaise(amount);
    }

    // =========================================================
    // REFRESH
    // =========================================================
    /// <summary>
    /// Shows the first <paramref name="count"/> enemy cards face up while the hand is running.
    /// Used by Cheat Glove; showdown and the end of the hand reveal everything anyway.
    /// </summary>
    public void SetEnemyReveal(int count)
    {
        count = Mathf.Clamp(count, 0, 2);
        if (count == enemyRevealCount)
            return;
        enemyRevealCount = count;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (poker == null)
            return;

        potText.text = $"Pot: {poker.Pot}";
        playerChipsText.text = "You";
        if (playerChipsValueText != null)
            playerChipsValueText.text = $"{poker.PlayerChips} chips";
        enemyChipsText.text = $"{poker.EnemyChips} chips";

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

        // 已知牌刚好五张时，五张都是「最好的五张」，全高亮等于没高亮 —— 只在多出来的牌需要筛掉时才点
        int knownCards = (poker.PlayerHand != null ? poker.PlayerHand.Count : 0)
                         + (poker.CommunityCards != null ? poker.CommunityCards.Count : 0);
        List<Card> highlightFive = knownCards > 5 ? playerBestFive : null;

        LayoutPlayerSlots(poker.PlayerHand != null ? poker.PlayerHand.Count : 2);
        RenderSlots(playerSlots, poker.PlayerHand, false, highlightFive);
        RenderSlots(communitySlots, poker.CommunityCards, false, highlightFive);
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
        RenderSlots(enemySlots, poker.EnemyHand, !revealEnemy, enemyBest != null ? enemyBest.bestFive : null, enemyRevealCount);

        // 牌面已经是卡面图了，这里只留牌型名（颜色+位置已经说明它是「最好五张」），
        // 不再加 "Best five:" 前缀——加上去最长的那档会被挤到盒子外面。
        playerHandTypeText.text =
            playerBest != null && playerBestFive != null && playerBestFive.Count > 0
                ? playerBest.Describe()
                : "";
        // 只在摊牌时显示敌方是什么牌；平时不占位（不要 "Hidden" 之类的占位词）
        enemyHandTypeText.text = enemyBest != null ? enemyBest.Describe() : "";
        enemyHandTypeText.color = TEXT_SOFT;

        bool isPlayerTurn = poker.State == PokerState.PlayerTurn && !poker.IsDealing;
        bool mustMatch = poker.AwaitingPlayerMatch;
        int maxRaise = poker.GetPlayerMaxRaise();
        bool canRaise = maxRaise >= MinRaise;

        if (turnBar != null)
            turnBar.gameObject.SetActive(isPlayerTurn);

        checkButton.interactable = isPlayerTurn && !mustMatch;
        raiseButton.interactable = isPlayerTurn && !mustMatch && canRaise;
        matchButton.interactable = isPlayerTurn && mustMatch;
        foldButton.interactable = isPlayerTurn;

        // 按钮上直接写金额；全下不再二次确认
        Text raiseLabel = raiseButton.GetComponentInChildren<Text>();
        int raiseAmount = Mathf.RoundToInt(raiseSlider.value);
        raiseLabel.text = canRaise ? "Raise " + raiseAmount : "Raise";
        raiseLabel.color = TEXT_ON_RAISE;

        Text foldLabel = foldButton.GetComponentInChildren<Text>();
        foldLabel.text = foldArmedAt > 0f ? "Sure?" : "Fold";

        // 不能点的按钮：底压暗一档，字也退后；能点的保持本色
        DimLabel(foldButton, foldButton.interactable, TEXT_ON_FOLD);
        DimLabel(checkButton, checkButton.interactable, TEXT_ON_DARK);
        DimLabel(matchButton, matchButton.interactable, TEXT_ON_DARK);
        if (!raiseButton.interactable)
            raiseLabel.color = DimOf(TEXT_ON_RAISE);

        // 换手/换街时把 Fold 的一次确认复位，避免隔了很久还停在「Sure?」
        if (!isPlayerTurn)
        {
            foldArmedAt = 0f;
        }
        else
        {
            if (foldArmedAt > 0f && Time.time - foldArmedAt > FoldConfirmSeconds)
                foldArmedAt = 0f;
        }

        // Swap 按钮：持有换牌工具时始终显示（哪怕这手已经用完），
        // 按钮上带剩余次数，玩家才知道还剩几次。没有工具则整块隐藏。
        if (swapButton != null)
        {
            bool hasSwapTool = PokerItemTraits.OwnedSwapsPerHand() > 0;
            bool showSwap = hasSwapTool && poker.State == PokerState.PlayerTurn && !poker.IsDealing;

            swapButton.gameObject.SetActive(showSwap);
            swapButton.interactable = poker.CanSwapCard;

            Text swapLabel = swapButton.GetComponentInChildren<Text>();
            if (swapLabel != null)
                swapLabel.text = "Swap " + poker.SwapsRemaining;

            DimLabel(swapButton, poker.CanSwapCard, TEXT_ON_DARK);
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
        // 新的一条街开始时，把上一条街的琥珀帽标降亮度，只让最新的抢眼
        if (kind == LogKind.Street)
            DimOlderStreetLines();

        // 1. 创建新的文本对象
        GameObject textGO = new GameObject("Msg");
        textGO.transform.SetParent(messageLogContainer, false);

        // 2. 放到最后（VerticalLayoutGroup 里最新的在最下面）
        textGO.transform.SetAsLastSibling();

        // 3. 设置文本属性
        Text txt = textGO.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 15;

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
            StyleButtonLiveState(b, on);
        }
    }

    // 只有最新一条街道/结算行保持琥珀加粗，历史条目转 #8F9B8C
    private void DimOlderStreetLines()
    {
        if (messageLogContainer == null)
            return;
        foreach (Transform child in messageLogContainer)
        {
            Text t = child.GetComponent<Text>();
            if (t != null && t.fontStyle == FontStyle.Bold)
            {
                t.color = AMBER_HISTORY;
                t.fontStyle = FontStyle.Normal;
            }
        }
    }

    // 关闭态：描边降到 #3A4A38、文字降到 #6C7A69，能读但明显退后
    private static void StyleButtonLiveState(Button b, bool live)
    {
        Outline edge = b.GetComponent<Outline>();
        if (edge != null)
            edge.effectColor = live ? CTRL_EDGE : CTRL_EDGE_OFF;
        Text label = b.GetComponentInChildren<Text>();
        if (label != null)
            label.color = live ? TEXT_LIVE : TEXT_OFF;
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

        // 预知：换牌前把牌堆顶几张写出来，没这件道具就隐藏。
        if (swapPeekText != null)
        {
            string peek = PokerItemTraits.PrecogPeekCount() > 0
                ? poker.PeekDeckTop(PokerItemTraits.PrecogPeekCount())
                : string.Empty;

            bool show = !string.IsNullOrEmpty(peek);
            swapPeekText.gameObject.SetActive(show);
            if (show)
                swapPeekText.text = "Deck top: " + peek;
        }

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

        // ---- 预知：牌堆顶预览（只有持有该道具时才显示） ----
        swapPeekText = CreateText(
            swapChoiceRow.transform,
            "SwapPeek",
            "",
            new Vector2(0.28f, 0.58f),
            new Vector2(0.72f, 0.62f),
            TextAnchor.MiddleCenter,
            18
        );
        swapPeekText.color = new Color(0.95f, 0.78f, 0.30f);
        swapPeekText.gameObject.SetActive(false);

        // ---- 卡片 1 ----
        swapCard1Button = CreateButton(
            swapChoiceRow.transform,
            "SwapCard1",
            "—",
            new Vector2(0.305f, 0.41f),
            new Vector2(0.435f, 0.51f),
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
            new Vector2(0.445f, 0.41f),
            new Vector2(0.575f, 0.51f),
            new Color(0.45f, 0.30f, 0.65f),
            () =>
            {
                poker.PlayerSwapCard(1);
                swapChoiceRow.SetActive(false);
            }
        );

        // ---- 卡片 3（只有「六张取五」才出现）----
        swapCard3Button = CreateButton(
            swapChoiceRow.transform,
            "SwapCard3",
            "—",
            new Vector2(0.585f, 0.41f),
            new Vector2(0.715f, 0.51f),
            new Color(0.45f, 0.30f, 0.65f),
            () =>
            {
                poker.PlayerSwapCard(2);
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

        // 第三张只有「六张取五」才有，手牌不够时把按钮藏起来。
        if (swapCard3Button != null)
        {
            bool hasThird = poker.PlayerHand != null && poker.PlayerHand.Count > 2;
            swapCard3Button.gameObject.SetActive(hasThird);
            if (hasThird)
                SetButtonLabel(swapCard3Button, LabelForHandIndex(2));
        }
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
        if (actionButtonSprite == null)
            actionButtonSprite = Resources.Load<Sprite>("PokerUI/Rounded/panel_r10");
        if (actionButtonSprite != null)
        {
            img.sprite = actionButtonSprite;
            img.type = Image.Type.Sliced;
        }
        // 底色画在 Image 上，ColorTint 只做乘法：hover/pressed 变亮变暗，
        // 禁用只压暗（保持不透明，否则 Outline 的四份拷贝会从半透明底下面透出来变成一圈怪色）
        img.color = normalColor;
        Outline edge = go.AddComponent<Outline>();
        edge.effectColor = CTRL_EDGE;
        edge.effectDistance = new Vector2(1f, -1f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        cb.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.40f, 0.40f, 0.40f, 1f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
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
            25
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
        bgImg.color = new Color(0.15f, 0.17f, 0.15f);
        Outline bgEdge = bgGO.AddComponent<Outline>();
        bgEdge.effectColor = CTRL_EDGE;
        bgEdge.effectDistance = new Vector2(1f, -1f);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.47f);
        bgRT.anchorMax = new Vector2(1f, 0.53f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0.47f);
        fillAreaRT.anchorMax = new Vector2(1f, 0.53f);
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
        handleRT.sizeDelta = new Vector2(14f, 0f);
        Outline handleEdge = handleGO.AddComponent<Outline>();
        handleEdge.effectColor = new Color(0.25f, 0.30f, 0.24f, 1f);
        handleEdge.effectDistance = new Vector2(1f, -1f);

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
        Outline edge = go.AddComponent<Outline>();
        edge.effectColor = PANEL_EDGE;
        edge.effectDistance = new Vector2(1f, -1f);
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
