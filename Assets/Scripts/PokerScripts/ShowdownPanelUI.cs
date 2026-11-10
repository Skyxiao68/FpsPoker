using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 摊牌后的结算弹窗。
///
/// 面板要回答三件事，顺序就是阅读顺序：
///   1. 这手牌值多少      → 结果横幅（谁赢、底池归谁）
///   2. 我带走什么        → 四行增量 + 与对手的差值徽标
///   3. 凭什么            → 倍率标尺 + 可展开的推导
///
/// 重要约束：面板给不出「进战斗后的终值」——玩家 HP 是 max(100, 原上限)+增量，
/// ATK 要叠武器基础值，ROF 是 base/(1+v/100)，SPD 是 +=，这些基础值都活在战斗
/// 场景里。所以这里一律只讲「增量」，数字带 +，不再假装是最终属性。
///
/// 完全独立于 PokerUI，由 PokerCombatBridge 触发。
/// </summary>
public class ShowdownPanelUI : MonoBehaviour
{
    // ---- 回调 ----
    private Action onEnterCombat;

    // ---- 卡片几何（画布 1280x720 上的锚点）----
    private const float CARD_X0 = 0.205f;
    private const float CARD_X1 = 0.795f;
    private const float CARD_TOP = 0.860f;
    private const float CARD_BOTTOM = 0.140f;
    private const float CANVAS_W = 1280f;
    private const float CANVAS_H = 720f;
    private const float CARD_W = (CARD_X1 - CARD_X0) * CANVAS_W;   // 755.2
    private const float PAD_X = 26f;                                // 内容左右内边距
    private const float CONTENT_W = CARD_W - PAD_X * 2f;            // 703.2
    private const float BASE_H = (CARD_TOP - CARD_BOTTOM) * CANVAS_H; // 518.4
    private const float EXPANDED_EXTRA = 108f;   // 展开块的默认高度，BuildDetails 会按有无物品行改写

    // ---- 行几何（相对卡片顶边，向下为正）----
    private const float ROW_H = 41f;
    private const float ROWS_TOP = 162f;
    private const float BANNER_TOP = 18f;
    private const float BANNER_H = 64f;
    private const float STAKE_TOP = 82f;
    private const float TYPE_LABEL_TOP = 108f;
    private const float TYPE_NAME_TOP = 128f;
    private const float NET_TOP = 332f;
    private const float BAR_HEADER_TOP = 362f;
    private const float BAR_TOP = 384f;
    private const float BAR_ANCHOR_TOP = 404f;
    private const float COLLAPSE_TOP = 426f;
    private const float BUTTON_TOP = 458f;
    private const float BUTTON_H = 48f;

    // 列（相对内容区左边 0..703）
    private const float COL_SUIT_X = 0f;
    private const float COL_SUIT_W = 40f;
    private const float COL_NAME_X = 44f;
    private const float COL_NAME_W = 200f;
    private const float COL_EFFECT_X = 248f;
    private const float COL_EFFECT_W = 162f;
    private const float COL_VALUE_X = 410f;
    private const float COL_VALUE_W = 190f;
    private const float COL_BADGE_X = 620f;
    private const float COL_BADGE_W = 83f;

    // 倍率标尺（相对内容区）
    private const float BAR_X = 24f;
    private const float BAR_W = 655f;
    private const float BAR_H = 16f;
    private const float MULT_MIN = 1.0f;
    private const float MULT_MAX = 3.0f;

    // ---- 静态文本与配色 ----
    private static readonly string[] StatSigil = { "♠", "♥", "♣", "♦" };
    private static readonly string[] StatFullName = { "Attack", "Health", "Rate of Fire", "Speed" };
    private static readonly string[] StatAbbrev = { "ATK", "HP", "ROF", "SPD" };
    private static readonly string[] StatEffect =
    {
        "damage per shot",
        "bonus max health",
        "fire rate",
        "movement speed",
    };

    private static readonly Color CARD_BG = new Color(0.086f, 0.125f, 0.102f, 0.97f);
    private static readonly Color CARD_EDGE = new Color(0.310f, 0.380f, 0.286f, 1f);
    private static readonly Color BAND_BG = new Color(0.122f, 0.173f, 0.122f, 1f);
    private static readonly Color BAND_BG_LOSE = new Color(0.157f, 0.118f, 0.110f, 1f);
    private static readonly Color BAND_EDGE = new Color(0.184f, 0.235f, 0.165f, 1f);
    private static readonly Color RULE = new Color(0.165f, 0.208f, 0.173f, 1f);
    private static readonly Color DETAIL_BG = new Color(0f, 0f, 0f, 0.22f);
    private static readonly Color TEXT_STRONG = new Color(0.910f, 0.929f, 0.902f);
    private static readonly Color TEXT_SOFT = new Color(0.659f, 0.710f, 0.639f);   // #A8B5A3
    private static readonly Color TEXT_MUTED = new Color(0.470f, 0.520f, 0.450f);  // 只用于 >=14 号
    private static readonly Color AMBER = new Color(0.949f, 0.702f, 0.239f);        // #F2B33D，与按钮一致
    private static readonly Color WIN_GREEN = new Color(0.420f, 0.820f, 0.478f);
    private static readonly Color LOSE_RED = new Color(0.870f, 0.470f, 0.420f);
    private static readonly Color TRACK_DARK = new Color(0.165f, 0.208f, 0.173f, 1f);

    // ---- 运行时引用 ----
    private GameObject root;
    private RectTransform cardRect;
    private RectTransform buttonRect;
    private RectTransform detailsRoot;

    private Text bannerText;
    private Image resultBar;
    private Image resultBanner;
    private Text stakeText;
    private Text playerLabelText, enemyLabelText;
    private Text playerTypeText, enemyTypeText;

    private readonly Text[] statSuit = new Text[4];
    private readonly Text[] statName = new Text[4];
    private readonly Text[] statEffect = new Text[4];
    private readonly Text[] statValue = new Text[4];
    private readonly Text[] statBadge = new Text[4];
    private readonly Image[] statBadgeBg = new Image[4];
    private readonly Image[] statRule = new Image[4];

    private Text netText;
    private Text netNoteText;

    private Text multNameText;
    private Text multValueText;
    private readonly Image[] trackSegments = new Image[3];
    private Image trackFill;
    private RectTransform pointerRect;
    private readonly Text[] anchorLabels = new Text[4];
    private readonly Image[] anchorTicks = new Image[4];

    private Text collapseLabel;
    private Text collapseSummary;
    private Button collapseButton;
    private Text detailsLine1, detailsLine2, detailsLine3, detailsLine4, detailsItem, detailsEnemy;
    private float detailsHeight = EXPANDED_EXTRA;

    private bool expanded;
    private float expandT; // 0..1

    /// <summary>面板是否正开着。桌面上的结果行据此避让。</summary>
    public static bool IsOpen { get; private set; }

    // =========================================================
    // 显示 / 隐藏
    // =========================================================

    public void Show(PokerGameManager poker, Action onEnter)
    {
        onEnterCombat = onEnter;

        if (root == null)
            BuildUI();

        expanded = false;
        expandT = 0f;
        ApplyExpandedGeometry();
        Refresh(poker);

        root.SetActive(true);
        IsOpen = true;
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
        IsOpen = false;
    }

    private void Update()
    {
        if (root == null || !root.activeSelf)
            return;

        float target = expanded ? 1f : 0f;
        if (!Mathf.Approximately(expandT, target))
        {
            float step = Time.unscaledDeltaTime / 0.12f;
            expandT = Mathf.MoveTowards(expandT, target, step);
            ApplyExpandedGeometry();
        }
    }

    // =========================================================
    // 构建
    // =========================================================

    private void BuildUI()
    {
        root = new GameObject("ShowdownPanel");
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // 盖在 PokerUI (100) 之上

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(CANVAS_W, CANVAS_H);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        // 遮罩（铺满整屏，用 stretch 而不是卡片坐标）
        Image dim = AddImage(root.transform, "Dim", 0, 0, CANVAS_W, CANVAS_H, new Color(0f, 0f, 0f, 0.70f));
        StretchToParent(dim.rectTransform);
        dim.rectTransform.SetAsFirstSibling();

        // 卡片（锚点随展开状态变化）
        Image card = AddImage(root.transform, "Card", 0, 0, 10, 10, CARD_BG);
        cardRect = card.rectTransform;
        ApplyRounded(card, "PokerUI/Rounded/panel_r14", CARD_EDGE);
        SetCardAnchors(BASE_H, CARD_TOP);

        // ---- 1. 结果横幅 ----
        resultBanner = AddImage(cardRect, "Banner", PAD_X, BANNER_TOP, CONTENT_W, BANNER_H, BAND_BG);
        ApplyRounded(resultBanner, "PokerUI/Rounded/panel_r10", BAND_EDGE);

        resultBar = AddImage(cardRect, "BannerBar", PAD_X, BANNER_TOP, 6f, BANNER_H, AMBER);

        bannerText = AddText(
            cardRect, "BannerText", "", PAD_X + 18f, BANNER_TOP, CONTENT_W - 30f, BANNER_H,
            28, TextAnchor.MiddleLeft, TEXT_STRONG);
        bannerText.fontStyle = FontStyle.Bold;
        bannerText.lineSpacing = 0.92f;
        bannerText.verticalOverflow = VerticalWrapMode.Overflow;

        // ---- 2. 赌注句 ----
        stakeText = AddText(
            cardRect, "StakeLine", "", PAD_X, STAKE_TOP, CONTENT_W, 18,
            14, TextAnchor.MiddleLeft, TEXT_SOFT);

        // ---- 3. 双方：列标题 + 牌型 ----
        playerLabelText = AddText(
            cardRect, "YouLabel", "YOUR HAND", PAD_X, TYPE_LABEL_TOP, 300, 18,
            13, TextAnchor.MiddleLeft, TEXT_MUTED);
        enemyLabelText = AddText(
            cardRect, "EnemyLabel", "THEIR HAND", PAD_X + 360, TYPE_LABEL_TOP, 300, 18,
            13, TextAnchor.MiddleLeft, TEXT_MUTED);

        playerTypeText = AddText(
            cardRect, "PlayerType", "", PAD_X, TYPE_NAME_TOP, 340, 30,
            20, TextAnchor.MiddleLeft, TEXT_STRONG);
        enemyTypeText = AddText(
            cardRect, "EnemyType", "", PAD_X + 360, TYPE_NAME_TOP, 340, 30,
            20, TextAnchor.MiddleLeft, TEXT_SOFT);

        // ---- 4. 四行属性 ----
        for (int i = 0; i < 4; i++)
        {
            float top = ROWS_TOP + i * ROW_H;

            statSuit[i] = AddText(
                cardRect, "Suit" + i, StatSigil[i], PAD_X + COL_SUIT_X, top, COL_SUIT_W, ROW_H,
                16, TextAnchor.MiddleLeft, TEXT_SOFT);

            // 全称 + 小号缩写：新玩家不认识 ATK，老玩家靠缩写扫读
            statName[i] = AddText(
                cardRect, "Name" + i, "", PAD_X + COL_NAME_X, top, COL_NAME_W, ROW_H,
                15, TextAnchor.MiddleLeft, TEXT_STRONG);
            statName[i].supportRichText = true;

            statEffect[i] = AddText(
                cardRect, "Effect" + i, StatEffect[i], PAD_X + COL_EFFECT_X, top, COL_EFFECT_W, ROW_H,
                12, TextAnchor.MiddleLeft, TEXT_SOFT);

            statValue[i] = AddText(
                cardRect, "Value" + i, "+0", PAD_X + COL_VALUE_X, top, COL_VALUE_W, ROW_H,
                20, TextAnchor.MiddleRight, TEXT_STRONG);

            statBadgeBg[i] = AddImage(
                cardRect, "Badge" + i, PAD_X + COL_BADGE_X, top + (ROW_H - 24f) / 2f, 76f, 24f, BAND_BG);
            ApplyRounded(statBadgeBg[i], "PokerUI/Rounded/panel_r10", BAND_EDGE);

            statBadge[i] = AddText(
                cardRect, "BadgeText" + i, "=", PAD_X + COL_BADGE_X, top + (ROW_H - 24f) / 2f, 76f, 24f,
                13, TextAnchor.MiddleCenter, TEXT_SOFT);
            statBadge[i].raycastTarget = false;

            if (i < 3)
            {
                statRule[i] = AddImage(
                    cardRect, "Rule" + i, PAD_X, top + ROW_H - 1f, 600f, 1f, RULE);
            }
        }

        // ---- 5. 净胜句 ----
        netText = AddText(
            cardRect, "NetLine", "", PAD_X, NET_TOP, 460, 24,
            15, TextAnchor.MiddleLeft, TEXT_STRONG);
        netNoteText = AddText(
            cardRect, "NetNote", "Increment only — Not total stats", PAD_X + CONTENT_W - 250f, NET_TOP, 250, 24,
            12, TextAnchor.MiddleRight, TEXT_SOFT);

        // ---- 6. 倍率标尺 ----
        multNameText = AddText(
            cardRect, "MultName", "", PAD_X + BAR_X, BAR_HEADER_TOP, 300, 18,
            17, TextAnchor.MiddleLeft, TEXT_STRONG);
        multValueText = AddText(
            cardRect, "MultValue", "", PAD_X + CONTENT_W - 200f, BAR_HEADER_TOP, 200, 18,
            20, TextAnchor.MiddleRight, AMBER);
        multValueText.fontStyle = FontStyle.Bold;

        float[] segX = { BAR_X, BAR_X + 165f, BAR_X + 330f };
        float[] segW = { 161f, 161f, BAR_W - 330f };
        for (int i = 0; i < 3; i++)
            trackSegments[i] = AddImage(cardRect, "Track" + i, PAD_X + segX[i], BAR_TOP, segW[i], BAR_H, TRACK_DARK);

        trackFill = AddImage(cardRect, "TrackFill", PAD_X + BAR_X, BAR_TOP, BAR_W, BAR_H, AMBER);
        trackFill.color = new Color(AMBER.r, AMBER.g, AMBER.b, 0.85f);

        pointerRect = AddRect(cardRect, "Pointer", PAD_X + BAR_X, BAR_TOP - 3f, 2f, BAR_H + 6f);
        Image pointerImg = pointerRect.gameObject.AddComponent<Image>();
        pointerImg.color = AMBER;
        pointerImg.raycastTarget = false;

        RectTransform head = AddRect(cardRect, "PointerHead", PAD_X + BAR_X - 3f, BAR_TOP - 8f, 8f, 8f);
        Image headImg = head.gameObject.AddComponent<Image>();
        headImg.color = AMBER;
        headImg.raycastTarget = false;
        head.localRotation = Quaternion.Euler(0f, 0f, 45f);

        float[] mults = { 1.0f, 1.5f, 2.0f, 3.0f };
        float[] labelX = { BAR_X - 8f, BAR_X + 164f - 40f, BAR_X + 330f - 40f, BAR_X + BAR_W - 72f };
        TextAnchor[] aligns = { TextAnchor.UpperLeft, TextAnchor.UpperCenter, TextAnchor.UpperCenter, TextAnchor.UpperRight };
        for (int i = 0; i < 4; i++)
        {
            anchorLabels[i] = AddText(
                cardRect, "Tick" + i, "", PAD_X + labelX[i], BAR_ANCHOR_TOP, 80f, 18,
                12, aligns[i], TEXT_SOFT);
            anchorTicks[i] = AddImage(
                cardRect, "TickMark" + i, PAD_X + BAR_X + (mults[i] - MULT_MIN) / (MULT_MAX - MULT_MIN) * BAR_W - 0.5f,
                BAR_TOP + BAR_H, 1f, 4f, RULE);
        }

        // ---- 7. 折叠行 ----
        collapseButton = AddButton(
            cardRect, "CollapseRow", "", PAD_X, COLLAPSE_TOP, CONTENT_W, 24, new Color(0f, 0f, 0f, 0f),
            ToggleExpanded);
        collapseLabel = AddText(
            cardRect, "CollapseLabel", "", PAD_X, COLLAPSE_TOP, 420, 24,
            13, TextAnchor.MiddleLeft, TEXT_SOFT);
        collapseLabel.raycastTarget = false;
        collapseSummary = AddText(
            cardRect, "CollapseSummary", "", PAD_X + CONTENT_W - 380f, COLLAPSE_TOP, 380, 24,
            12, TextAnchor.MiddleRight, TEXT_SOFT);
        collapseSummary.raycastTarget = false;

        // ---- 7b. 展开的推导块（默认隐藏）----
        Image detailsBg = AddImage(
            cardRect, "Details", PAD_X, COLLAPSE_TOP + 30f, CONTENT_W, 96f, DETAIL_BG);
        detailsRoot = detailsBg.rectTransform;
        ApplyRounded(detailsBg, "PokerUI/Rounded/panel_r10", RULE);

        Text[] detailLines = new Text[6];
        string[] names = { "D1", "D2", "D3", "D4", "D5", "D6" };
        Color[] colors = { TEXT_STRONG, TEXT_SOFT, TEXT_SOFT, TEXT_SOFT, TEXT_SOFT, AMBER };
        for (int i = 0; i < 6; i++)
        {
            detailLines[i] = AddText(
                detailsRoot, names[i], "", 12f, 8f + i * 20f, CONTENT_W - 24f, 18f,
                12, TextAnchor.MiddleLeft, colors[i]);
            SetChildRect(detailLines[i].rectTransform, 12f, 8f + i * 20f, CONTENT_W - 24f, 18f);
        }
        detailsLine1 = detailLines[0];
        detailsLine2 = detailLines[1];
        detailsLine3 = detailLines[2];
        detailsLine4 = detailLines[3];
        detailsEnemy = detailLines[4];
        detailsItem = detailLines[5];
        SetDetailsVisible(false);

        // ---- 8. 主按钮 ----
        Button enterBtn = AddButton(
            cardRect, "EnterCombatBtn", "", PAD_X + 132f, BUTTON_TOP, 440f, BUTTON_H, AMBER,
            () => { Hide(); onEnterCombat?.Invoke(); });
        buttonRect = enterBtn.GetComponent<RectTransform>();
        ApplyRounded(enterBtn.GetComponent<Image>(), "PokerUI/Rounded/panel_r10", AMBER);
        Text enterLabel = AddText(
            enterBtn.transform, "Label", "Enter Combat", 0, 0, 440, BUTTON_H,
            20, TextAnchor.MiddleCenter, new Color(0.102f, 0.071f, 0.024f));
        enterLabel.fontStyle = FontStyle.Bold;
        enterLabel.raycastTarget = false;
        StretchToParent(enterLabel.rectTransform);
    }

    // =========================================================
    // 展开 / 收起
    // =========================================================

    private void ToggleExpanded()
    {
        expanded = !expanded;
        detailsRoot.gameObject.SetActive(true);
    }

    private void ApplyExpandedGeometry()
    {
        if (cardRect == null)
            return;

        float ease = EaseOutCubic(expandT);
        float top = CARD_TOP + 0.014f * ease;   // 展开时整块上移一点，避免底部出屏
        float h = Mathf.Lerp(BASE_H, BASE_H + detailsHeight, ease);
        SetCardAnchors(h, top);

        if (buttonRect != null)
        {
            float buttonTop = BUTTON_TOP + detailsHeight * ease;
            SetRect(buttonRect, PAD_X + 132f, buttonTop, 440f, BUTTON_H);
        }

        if (detailsRoot != null)
            SetDetailsVisible(expandT > 0.5f);
    }

    private void SetDetailsVisible(bool visible)
    {
        if (detailsRoot != null)
            detailsRoot.gameObject.SetActive(visible);
        Text[] lines = { detailsLine1, detailsLine2, detailsLine3, detailsLine4, detailsEnemy, detailsItem };
        foreach (Text line in lines)
            if (line != null)
                line.gameObject.SetActive(visible);
    }

    private void SetCardAnchors(float heightPx, float top)
    {
        float bottom = top - heightPx / CANVAS_H;
        cardRect.anchorMin = new Vector2(CARD_X0, bottom);
        cardRect.anchorMax = new Vector2(CARD_X1, top);
        cardRect.offsetMin = Vector2.zero;
        cardRect.offsetMax = Vector2.zero;
    }

    private static float EaseOutCubic(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        return 1f - inv * inv * inv;
    }

    // =========================================================
    // 填充数据
    // =========================================================

    private void Refresh(PokerGameManager poker)
    {
        bool playerWon = poker.Result == PokerResult.PlayerWinsShowdown;
        int pot = poker.Pot;

        // ---- 横幅 ----
        bannerText.text = playerWon
            ? "YOU WON  ·  Pot goes to you!\n<size=15><color=#A8B5A3>Win the duel to defend it.</color></size>"
            : "YOU LOST  ·  Pot goes to them\n<size=15><color=#A8B5A3>Win the duel to claim it.</color></size>";
        bannerText.color = playerWon ? WIN_GREEN : LOSE_RED;
        resultBar.color = playerWon ? WIN_GREEN : LOSE_RED;
        resultBanner.color = playerWon ? BAND_BG : BAND_BG_LOSE;

        // ---- 牌型 ----
        playerTypeText.text = poker.PlayerBestHand != null ? poker.PlayerBestHand.Describe() : "";
        enemyTypeText.text = poker.EnemyBestHand != null ? poker.EnemyBestHand.Describe() : "";
        playerTypeText.color = playerWon ? WIN_GREEN : LOSE_RED;
        enemyTypeText.color = TEXT_SOFT;

        // ---- 增量 ----
        CombatStats yours = ItemEffectBridge.GetCombatStats(poker.PlayerBestHand);
        // 与实战一致：赢了这一手，面板显示的数字也要含"最高 10% 补给最低"的加成
        if (playerWon)
            yours = ItemEffectBridge.ApplyWinBonus(yours);
        CombatStats theirs = CombatStatsFactory.Compute(poker.EnemyBestHand);
        float[] you = { yours.attack, yours.health, yours.fireRate, yours.moveSpeed };
        float[] them = { theirs.attack, theirs.health, theirs.fireRate, theirs.moveSpeed };

        int favour = 0;
        for (int i = 0; i < 4; i++)
        {
            statName[i].text =
                StatFullName[i] + " <size=11><color=#8A9686>" + StatAbbrev[i] + "</color></size>";

            statSuit[i].text = SuitGlyphsFor(i);

            statValue[i].text = FormatIncrement(i, you[i]);
            statValue[i].color = TEXT_STRONG;

            float diff = you[i] - them[i];
            bool ahead = diff > 0.05f;
            bool behind = diff < -0.05f;
            if (ahead) favour++;

            string fmt = i == 2 ? "0" : "0.#";
            string unit = i == 2 ? "%" : "";
            statBadge[i].text = ahead
                ? "▲ +" + Mathf.Abs(diff).ToString(fmt) + unit
                : behind
                    ? "▼ −" + Mathf.Abs(diff).ToString(fmt) + unit
                    : "=";
            statBadge[i].color = ahead ? WIN_GREEN : behind ? LOSE_RED : TEXT_SOFT;
            statBadgeBg[i].color = ahead ? new Color(0.10f, 0.16f, 0.10f, 1f)
                : behind ? new Color(0.16f, 0.10f, 0.10f, 1f)
                : BAND_BG;
        }

        netText.text = $"This hand: {favour} of 4 in your favour";

        // ---- 倍率标尺 ----
        float mult = poker.PlayerBestHand != null
            ? poker.PlayerBestHand.GetMultiplier()
                + PokerItemTraits.MultiplierBonus(poker.PlayerBestHand.handType)
                + PokerItemTraits.MomentumBonus(PokerGameManager.LastHandRaiseCount)
            : 1f;
        string multName = poker.PlayerBestHand != null ? poker.PlayerBestHand.GetDisplayName() : "—";
        multNameText.text = multName;
        multValueText.text = "×" + mult.ToString("0.00");

        float t = Mathf.Clamp01((mult - MULT_MIN) / (MULT_MAX - MULT_MIN));
        float fillW = BAR_W * t;
        SetRect(trackFill.rectTransform, PAD_X + BAR_X, BAR_TOP, Mathf.Max(2f, fillW), BAR_H);
        SetRect(pointerRect, PAD_X + BAR_X + fillW - 1f, BAR_TOP - 3f, 2f, BAR_H + 6f);
        RectTransform head = pointerRect.parent.Find("PointerHead") as RectTransform;
        if (head != null)
            SetRect(head, PAD_X + BAR_X + fillW - 4f, BAR_TOP - 8f, 8f, 8f);

        float[] mults = { 1.0f, 1.5f, 2.0f, 3.0f };
        string[] tickText =
        {
            "1.0 <color=#8A9686>High card</color>",
            "1.5 <color=#8A9686>Straight</color>",
            "2.0 <color=#8A9686>Quads</color>",
            "3.0 <color=#8A9686>Royal</color>",
        };
        for (int i = 0; i < 4; i++)
        {
            anchorLabels[i].text = tickText[i];
            anchorLabels[i].color = Mathf.Abs(mult - mults[i]) < 0.02f ? AMBER : TEXT_SOFT;
        }

        // ---- 折叠摘要 ----
        collapseLabel.text = (expanded ? "▾  " : "▸  ") + "How this was worked out";
        collapseSummary.text = SuitCountSummary(poker.PlayerBestHand) + "  ·  ×" + mult.ToString("0.00");

        // ---- 展开明细 ----
        BuildDetails(poker, you, them, mult);
    }

    /// <summary>
    /// 展开面板里的四行：花色链 / 倍率 / 物品改写 / 对方增量。
    /// </summary>
    private void BuildDetails(PokerGameManager poker, float[] you, float[] them, float mult)
    {
        HandResult hand = poker.PlayerBestHand;
        if (hand == null || hand.bestFive == null || hand.bestFive.Count == 0)
        {
            detailsLine1.text = "";
            detailsLine2.text = "";
            detailsLine3.text = "";
            detailsLine4.text = "";
            detailsEnemy.text = "";
            detailsItem.text = "";
            return;
        }

        float[] sums = SuitSums(hand.bestFive);

        // 1. 计入最好五张的是哪几张
        detailsLine1.text = CardsInline(hand.bestFive);

        // 2. 每个花色：(牌值 + 牌值)
        detailsLine2.text =
            $"{StatSigil[0]} ({SuitValueText(hand.bestFive, 0)})    "
            + $"{StatSigil[1]} ({SuitValueText(hand.bestFive, 1)})    "
            + $"{StatSigil[2]} ({SuitValueText(hand.bestFive, 2)})    "
            + $"{StatSigil[3]} ({SuitValueText(hand.bestFive, 3)})";

        // 3. 花色合计 × 倍率
        detailsLine3.text =
            $"{Mathf.RoundToInt(sums[0])} / {Mathf.RoundToInt(sums[1])} / "
            + $"{Mathf.RoundToInt(sums[2])} / {Mathf.RoundToInt(sums[3])}"
            + $" × {mult:0.00} ({hand.GetDisplayName()})";

        // 4. 我的增量
        detailsLine4.text =
            $"Your stats: ATK {FormatIncrement(0, you[0])}  ·  HP {FormatIncrement(1, you[1])}"
            + $"  ·  ROF {FormatIncrement(2, you[2])}  ·  SPD {FormatIncrement(3, you[3])}";

        // 5. 对方的增量
        detailsEnemy.text =
            $"Opponent hand: ATK {FormatIncrement(0, them[0])}  ·  HP {FormatIncrement(1, them[1])}"
            + $"  ·  ROF {FormatIncrement(2, them[2])}  ·  SPD {FormatIncrement(3, them[3])}";

        // 6. 物品改写（有才占一行）
        bool queen = PlayerInventory.Instance != null
            && PlayerInventory.Instance.Has(ItemId.QueenOfHearts);
        detailsItem.text = queen ? "Queens of Hearts: hearts count as ATK instead of HP" : "";
        detailsItem.gameObject.SetActive(queen);

        detailsHeight = queen ? 128f : 108f;
        SetRect(detailsRoot, PAD_X, COLLAPSE_TOP + 30f, CONTENT_W, detailsHeight);
    }

    // =========================================================
    // 小工具
    // =========================================================

    private static string FormatIncrement(int index, float value)
    {
        if (index == 2)
            return "+" + Mathf.RoundToInt(value) + "%";
        return "+" + value.ToString("0.#");
    }

    /// <summary>某一项属性实际吃了哪些花色（红桃皇后会把 ♥ 挪到 ATK）。</summary>
    private string SuitGlyphsFor(int statIndex)
    {
        bool queen = PlayerInventory.Instance != null
            && PlayerInventory.Instance.Has(ItemId.QueenOfHearts);

        // 默认映射：0 ♠ / 1 ♥ / 2 ♣ / 3 ♦
        if (!queen)
            return StatSigil[statIndex];

        if (statIndex == 0)
            return StatSigil[0] + StatSigil[1];
        if (statIndex == 1)
            return "·";
        return StatSigil[statIndex];
    }

    private static int CountSuit(HandResult hand, Suit suit)
    {
        int n = 0;
        foreach (Card c in hand.bestFive)
            if (c.Suit == suit)
                n++;
        return n;
    }

    private static float[] SuitSums(List<Card> cards)
    {
        float[] sums = new float[4];
        foreach (Card c in cards)
        {
            float v = c.GetAttributeValue();
            switch (c.Suit)
            {
                case Suit.Spades: sums[0] += v; break;
                case Suit.Hearts: sums[1] += v; break;
                case Suit.Clubs: sums[2] += v; break;
                case Suit.Diamonds: sums[3] += v; break;
            }
        }
        return sums;
    }

    /// <summary>把这五张牌按原样列出来（如 A♠ K♠ Q♦ J♦ J♣）。</summary>
    private static string CardsInline(List<Card> cards)
    {
        if (cards == null || cards.Count == 0)
            return "";
        string s = "";
        for (int i = 0; i < cards.Count; i++)
        {
            if (i > 0)
                s += "  ";
            s += cards[i].ToString();
        }
        return s;
    }

    /// <summary>某个花色的牌值拆解（如 11+10）；该花色没有牌时返回 0。</summary>
    private static string SuitValueText(List<Card> cards, int suitIndex)
    {
        List<int> values = new List<int>();
        foreach (Card c in cards)
        {
            if ((int)c.Suit == suitIndex)
                values.Add(c.GetAttributeValue());
        }
        if (values.Count == 0)
            return "0";
        values.Sort();
        values.Reverse();
        string s = "";
        for (int i = 0; i < values.Count; i++)
        {
            if (i > 0)
                s += "+";
            s += values[i];
        }
        return s;
    }

    private static string SuitCountSummary(HandResult hand)
    {
        if (hand == null || hand.bestFive == null || hand.bestFive.Count == 0)
            return "";
        return $"{StatSigil[0]}{CountSuit(hand, Suit.Spades)} "
            + $"{StatSigil[1]}{CountSuit(hand, Suit.Hearts)} "
            + $"{StatSigil[2]}{CountSuit(hand, Suit.Clubs)} "
            + $"{StatSigil[3]}{CountSuit(hand, Suit.Diamonds)}";
    }

    private void ApplyRounded(Image img, string resourcePath, Color edgeColor)
    {
        if (img == null)
            return;
        Sprite sp = Resources.Load<Sprite>(resourcePath);
        if (sp != null)
        {
            img.sprite = sp;
            img.type = Image.Type.Sliced;
        }
        Outline edge = img.gameObject.GetComponent<Outline>();
        if (edge == null)
            edge = img.gameObject.AddComponent<Outline>();
        edge.effectColor = edgeColor;
        edge.effectDistance = new Vector2(1f, -1f);
    }

    // ---- 画布元素构造：x/y 以卡片左上角为原点，y 向下为正 ----

    private RectTransform AddRect(Transform parent, string name, float x, float yTop, float w, float h)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        SetRect(rt, x, yTop, w, h);
        return rt;
    }

    private void SetRect(RectTransform rt, float x, float yTop, float w, float h)
    {
        // 左上角锚定：卡片高度变化（展开推导）时上方元素不跟着缩放
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -yTop);
        rt.sizeDelta = new Vector2(w, h);
    }

    /// <summary>展开块内部四行文字：同样按父物体左上角锚定。</summary>
    private void SetChildRect(RectTransform rt, float x, float yTop, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -yTop);
        rt.sizeDelta = new Vector2(w, h);
    }

    private void StretchToParent(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private Text AddText(
        Transform parent, string name, string content,
        float x, float yTop, float w, float h,
        int fontSize, TextAnchor align, Color color)
    {
        RectTransform rt = AddRect(parent, name, x, yTop, w, h);
        Text text = rt.gameObject.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = align;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Image AddImage(
        Transform parent, string name,
        float x, float yTop, float w, float h, Color color)
    {
        RectTransform rt = AddRect(parent, name, x, yTop, w, h);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private Button AddButton(
        Transform parent, string name, string label,
        float x, float yTop, float w, float h,
        Color normalColor, UnityEngine.Events.UnityAction onClick)
    {
        Image img = AddImage(parent, name, x, yTop, w, h, normalColor);
        img.raycastTarget = true;

        Button btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null)
            btn.onClick.AddListener(onClick);

        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        cb.selectedColor = Color.white;
        cb.disabledColor = new Color(0.62f, 0.62f, 0.62f, 1f);
        btn.colors = cb;

        if (!string.IsNullOrEmpty(label))
        {
            Text txt = AddText(
                img.transform, "Label", label, 0, 0, w, h,
                20, TextAnchor.MiddleCenter, Color.black);
            txt.raycastTarget = false;
            StretchToParent(txt.rectTransform);
        }
        return btn;
    }
}
