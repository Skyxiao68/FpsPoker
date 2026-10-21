using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// M3 onboarding layer for the poker table. Two pieces, one shared canvas:
///
/// 1. A "?" button (always visible) that opens a reference panel with
///    the hand rankings, the stat mapping, and the pot rules.
/// 2. A result line that explains *why* a hand ended and what it did to
///    the stack, so a defeat never reads as a silent number change.
///
/// The hand-by-hand scripted walkthrough (dim + highlight + callout) lives in
/// PokerTutorialFlow.cs and builds its own canvas above this one; the one-time
/// briefing card that used to live here was replaced by it.
///
/// The canvas is built here at sortingOrder 300 so it covers the showdown
/// panel (order 200) and can never be hidden behind the table UI.
/// Text content is English, matching the rest of the in-game UI; the
/// comments are Chinese per this project's convention.
/// </summary>
public class PokerTutorialUI : MonoBehaviour
{
    private const int CanvasOrder = 300;
    private const string FontResource = "LegacyRuntime.ttf";

    private PokerGameManager poker;

    private GameObject canvasRoot;
    private GameObject helpPanel;
    private Button helpButton;
    private Text resultText;

    private float resultHideAt = -1f;

    // 结果行停 10 秒再淡出 0.6 秒：短到不会跟着进下一手，长到能读完一句话。
    private const float ResultStaySeconds = 10f;
    private const float ResultFadeSeconds = 0.6f;

    // 参考面板右栏的四项属性当前数值（打开面板时按当前 best five 现算）
    private Text helpAtkValue;
    private Text helpHpValue;
    private Text helpRofValue;
    private Text helpSpdValue;
    private Text helpStatsLine;

    // ---- 视觉常量（与 PokerUI / ShowdownPanelUI 同一套暗色桌布语言） ----
    private static readonly Color OVERLAY = new Color(0f, 0f, 0f, 0.92f);
    private static readonly Color CARD_BG = new Color(0.14f, 0.19f, 0.16f, 1f);
    private static readonly Color CARD_EDGE = new Color(0.30f, 0.45f, 0.36f, 0.55f);
    private static readonly Color TEXT_STRONG = new Color(0.93f, 0.93f, 0.93f);
    private static readonly Color TEXT_SOFT = new Color(0.78f, 0.78f, 0.78f);
    private static readonly Color TEXT_MUTED = new Color(0.60f, 0.62f, 0.60f);
    private static readonly Color BTN_HELP = new Color(0.20f, 0.26f, 0.22f);
    private static readonly Color BTN_PRIMARY = new Color(0.22f, 0.58f, 0.36f);

    // 花色配色：与 PokerUI.SuitColor 一致（牌面用的那一套），不是 hex 那一套。
    private const string HEX_SPADE = "#A0A0A0";
    private const string HEX_HEART = "#FF8080";
    private const string HEX_CLUB = "#90E0FF";
    private const string HEX_DIAMOND = "#FFD060";

    // =========================================================
    // ENTRY
    // =========================================================
    public void Initialize(PokerGameManager manager)
    {
        poker = manager;

        BuildCanvas();
        BuildHelp();
        BuildHelpButton();
        BuildResultLine();

        if (poker != null)
        {
            poker.OnStateChanged += HandleStateChanged;
            poker.OnHandEnded += ShowHandResult;
        }
    }

    private void OnDestroy()
    {
        if (poker != null)
        {
            poker.OnStateChanged -= HandleStateChanged;
            poker.OnHandEnded -= ShowHandResult;
        }
    }

    private void Update()
    {
        if (resultText != null && resultHideAt > 0f && resultText.gameObject.activeSelf)
        {
            float elapsed = Time.time - resultHideAt;
            if (elapsed >= 0f)
            {
                Color fading = resultText.color;
                fading.a = Mathf.Clamp01(1f - elapsed / ResultFadeSeconds);
                resultText.color = fading;

                if (fading.a <= 0f)
                    ClearResultLine();
            }
        }

        // 摊牌面板（ShowdownPanelUI，order 200）会盖住桌子中间那块，结果行在它上面叠字。
        // 面板自己已经写了赢家和底池走向，所以面板开着的时候先把结果行收起来。
        // ? 参照面板同理（同一块地方）。
        if (resultText != null && resultText.text.Length > 0)
        {
            bool covered =
                ShowdownPanelUI.IsOpen || (helpPanel != null && helpPanel.activeSelf);
            resultText.gameObject.SetActive(!covered);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (helpPanel != null && helpPanel.activeSelf)
                CloseHelp();
        }
    }

    // =========================================================
    // CANVAS
    // =========================================================
    private void BuildCanvas()
    {
        canvasRoot = new GameObject("PokerTutorialCanvas");
        canvasRoot.transform.SetParent(transform, false);

        Canvas canvas = canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasOrder;
        // 子 canvas 必须 overrideSorting，否则射线排序跟着父 canvas（order 100），
        // 右下角的 ? 按钮和面板里的 CLOSE 都会被桌面吃掉点击。
        canvas.overrideSorting = true;

        CanvasScaler scaler = canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        canvasRoot.AddComponent<GraphicRaycaster>();
    }

    // =========================================================
    // 1. ? 帮助面板
    // =========================================================
    private void BuildHelpButton()
    {
        helpButton = CreateButton(
            canvasRoot.transform,
            "HelpBtn",
            "?",
            new Vector2(0.955f, 0.935f),
            new Vector2(0.992f, 0.978f),
            BTN_HELP,
            ToggleHelp
        );
        helpButton.GetComponentInChildren<Text>().fontSize = 20;
    }

    private void SetHelpButtonVisible(bool visible)
    {
        if (helpButton != null)
            helpButton.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 教程里不需要这张参考面板（每课的提示卡已经把该看的说了），
    /// 开着它只会跟课程卡互相压住，所以由 TutorialSceneBootstrap 关掉。
    /// </summary>
    public void SetHelpAvailable(bool available)
    {
        if (!available)
            CloseHelp();

        SetHelpButtonVisible(available);
    }

    private void ToggleHelp()
    {
        if (helpPanel == null)
            return;

        if (helpPanel.activeSelf)
            CloseHelp();
        else
            OpenHelp();
    }

    private void OpenHelp()
    {
        RefreshHelpTexts();
        RefreshHelpStats();
        helpPanel.SetActive(true);
        SetHelpButtonVisible(false);
    }

    private Text CreateStatValue(float topY)
    {
        Text t = CreateText(
            helpPanel.transform,
            "HelpStatValue",
            "—",
            new Vector2(0.795f, topY),
            new Vector2(0.910f, topY + 0.040f),
            TextAnchor.MiddleRight,
            15
        );
        t.color = new Color(0.93f, 0.93f, 0.93f);
        t.fontStyle = FontStyle.Bold;
        return t;
    }

    /// <summary>
    /// 把右栏的四项数字刷成「当前这手牌算出来的值」。
    /// 测试反馈：只知道缩写、不知道 ROF 是什么、也不知道自己现在有多少，
    /// 所以这里既给全称（在左列文字里）也给当前数值。
    /// </summary>
    private void RefreshHelpStats()
    {
        if (poker == null || helpAtkValue == null)
            return;

        HandResult best = poker.PlayerBestHand;

        if (best == null && poker.PlayerHand != null)
        {
            List<Card> known = new List<Card>(poker.PlayerHand);
            known.AddRange(poker.CommunityCards);

            if (known.Count >= 5)
                best = HandEvaluator.Evaluate(known);
        }

        if (best == null)
        {
            helpAtkValue.text = "—";
            helpHpValue.text = "—";
            helpRofValue.text = "—";
            helpSpdValue.text = "—";
            helpStatsLine.text = "Two cards so far — the numbers appear once five cards are known.";
            return;
        }

        CombatStats stats = CombatStatsFactory.Compute(best);

        helpAtkValue.text = stats.attack.ToString("0.0");
        helpHpValue.text = stats.health.ToString("0.0");
        helpRofValue.text = stats.fireRate.ToString("0.0");
        helpSpdValue.text = stats.moveSpeed.ToString("0.0");
        helpStatsLine.text = "Right now: " + best.Describe() + " ×" + best.GetMultiplier().ToString("0.00");
    }

    private void CloseHelp()
    {
        helpPanel.SetActive(false);
        SetHelpButtonVisible(true);
    }

    private void BuildHelp()
    {
        helpPanel = new GameObject("HelpPanel");
        helpPanel.transform.SetParent(canvasRoot.transform, false);
        FullStretch(helpPanel);

        CreatePanel(helpPanel.transform, OVERLAY, Vector2.zero, Vector2.one);
        CreatePanel(
            helpPanel.transform,
            CARD_BG,
            new Vector2(0.06f, 0.05f),
            new Vector2(0.94f, 0.95f)
        );

        CreateText(
            helpPanel.transform,
            "HelpTitle",
            "TABLE REFERENCE",
            new Vector2(0.09f, 0.895f),
            new Vector2(0.91f, 0.935f),
            TextAnchor.MiddleLeft,
            24
        ).color = TEXT_STRONG;

        // ---- 左栏：牌型从高到低 ----
        CreateText(
            helpPanel.transform,
            "HelpHandsLabel",
            "HAND RANKINGS — highest first",
            new Vector2(0.09f, 0.845f),
            new Vector2(0.49f, 0.880f),
            TextAnchor.MiddleLeft,
            14
        ).color = TEXT_MUTED;

        // 倍率直接问 HandEvaluator，文档和规则改数字时这里不会各说一套。
        string[] ranks =
        {
            RankLine(HandType.RoyalFlush, "A K Q J 10, all one suit"),
            RankLine(HandType.StraightFlush, "five in a row, one suit"),
            RankLine(HandType.FourOfAKind, "four cards of one rank"),
            RankLine(HandType.FullHouse, "three of a rank plus a pair"),
            RankLine(HandType.Flush, "five of one suit, any order"),
            RankLine(HandType.Straight, "five in a row, mixed suits"),
            RankLine(HandType.ThreeOfAKind, "three of one rank"),
            RankLine(HandType.TwoPair, "two pairs of different ranks"),
            RankLine(HandType.OnePair, "two cards of one rank"),
            RankLine(HandType.HighCard, "nothing else; top card wins"),
        };

        float y = 0.805f;
        float rowHeight = 0.042f;
        foreach (string rank in ranks)
        {
            Text row = CreateText(
                helpPanel.transform,
                "HelpRank",
                rank,
                new Vector2(0.09f, y - rowHeight),
                new Vector2(0.49f, y),
                TextAnchor.MiddleLeft,
                14
            );
            row.color = TEXT_SOFT;
            row.resizeTextForBestFit = false;
            y -= rowHeight + 0.004f;
        }

        // ---- 右栏：属性映射 ----
        CreateText(
            helpPanel.transform,
            "HelpStatsLabel",
            "YOUR HAND BECOMES YOUR FIGHTER",
            new Vector2(0.51f, 0.845f),
            new Vector2(0.91f, 0.880f),
            TextAnchor.MiddleLeft,
            14
        ).color = TEXT_MUTED;

        CreateText(
            helpPanel.transform,
            "HelpSpade",
            "<color=" + HEX_SPADE + ">♠ SPADES</color>  →  ATK  (damage per shot)",
            new Vector2(0.51f, 0.790f),
            new Vector2(0.91f, 0.830f),
            TextAnchor.MiddleLeft,
            15
        ).color = TEXT_SOFT;

        CreateText(
            helpPanel.transform,
            "HelpHeart",
            "<color=" + HEX_HEART + ">♥ HEARTS</color>  →  HP  (how much you survive)",
            new Vector2(0.51f, 0.750f),
            new Vector2(0.91f, 0.790f),
            TextAnchor.MiddleLeft,
            15
        ).color = TEXT_SOFT;

        CreateText(
            helpPanel.transform,
            "HelpClub",
            "<color=" + HEX_CLUB + ">♣ CLUBS</color>  →  ROF  (rate of fire)",
            new Vector2(0.51f, 0.710f),
            new Vector2(0.91f, 0.750f),
            TextAnchor.MiddleLeft,
            15
        ).color = TEXT_SOFT;

        CreateText(
            helpPanel.transform,
            "HelpDiamond",
            "<color=" + HEX_DIAMOND + ">♦ DIAMONDS</color>  →  SPD  (movement speed)",
            new Vector2(0.51f, 0.670f),
            new Vector2(0.91f, 0.710f),
            TextAnchor.MiddleLeft,
            15
        ).color = TEXT_SOFT;

        helpAtkValue = CreateStatValue(0.790f);
        helpHpValue = CreateStatValue(0.750f);
        helpRofValue = CreateStatValue(0.710f);
        helpSpdValue = CreateStatValue(0.670f);

        helpStatsLine = CreateText(
            helpPanel.transform,
            "HelpStatsLine",
            "",
            new Vector2(0.51f, 0.575f),
            new Vector2(0.91f, 0.612f),
            TextAnchor.MiddleLeft,
            13
        );
        helpStatsLine.color = new Color(1.00f, 0.78f, 0.30f);

        CreateText(
            helpPanel.transform,
            "HelpStatsNote",
            "Only your best five cards count. The hand rank multiplies the totals.",
            new Vector2(0.51f, 0.615f),
            new Vector2(0.91f, 0.665f),
            TextAnchor.MiddleLeft,
            13
        ).color = TEXT_MUTED;

        // ---- 右栏：规则 ----
        CreateText(
            helpPanel.transform,
            "HelpRulesLabel",
            "THE POT",
            new Vector2(0.51f, 0.505f),
            new Vector2(0.91f, 0.540f),
            TextAnchor.MiddleLeft,
            14
        ).color = TEXT_MUTED;

        CreateText(
            helpPanel.transform,
            "HelpRules",
            "A showdown only freezes the pot — you still have to win the duel to take it.\n"
                + "Folding hands the pot to the opponent; it never costs you the stack.\n"
                + "A tie splits the pot evenly.\n"
                + "Below the buy-in, you cannot sit down — that is how a level ends.",
            new Vector2(0.51f, 0.300f),
            new Vector2(0.91f, 0.500f),
            TextAnchor.UpperLeft,
            14
        ).color = TEXT_SOFT;

        CreateButton(
            helpPanel.transform,
            "HelpClose",
            "CLOSE",
            new Vector2(0.40f, 0.095f),
            new Vector2(0.60f, 0.155f),
            BTN_HELP,
            CloseHelp
        );

        helpPanel.SetActive(false);
    }

    /// <summary>
    /// "Three of a Kind — three of one rank  ×1.35"：牌型名用枚举，倍数用 HandEvaluator，
    /// 只看这份表就能算出「这手牌给我多少属性」。
    /// </summary>
    private static string RankLine(HandType type, string detail)
    {
        HandResult probe = new HandResult { handType = type };
        return probe.GetDisplayName() + " — " + detail + "  ×" + probe.GetMultiplier().ToString("0.00");
    }

    private void RefreshHelpTexts()
    {
        // 面板内容目前是静态文案，只有 #REF 这类动态项才需要刷新；
        // 保留这个入口，后面接 per-level 数据时在这里填。
    }

    // =========================================================
    // 2. 本手结果 / 失败原因行
    // =========================================================
    private void BuildResultLine()
    {
        resultText = CreateText(
            canvasRoot.transform,
            "TutorialResultLine",
            "",
            new Vector2(0.08f, 0.452f),
            new Vector2(0.92f, 0.500f),
            TextAnchor.MiddleCenter,
            17
        );
        resultText.color = TEXT_STRONG;
        resultText.raycastTarget = false; // 结果行不该吃掉桌面的点击
        resultText.gameObject.SetActive(false);
    }

    private void HandleStateChanged()
    {
        if (poker == null)
            return;

        // 新一手开始了 → 收掉上一手的结果行
        if (
            poker != null
            && poker.State == PokerState.PlayerTurn
            && poker.CurrentStreet == Street.PreFlop
            && resultText != null
            && resultText.gameObject.activeSelf
        )
        {
            ClearResultLine();
        }
    }

    private void ShowHandResult()
    {
        if (poker == null || resultText == null)
            return;

        string line = BuildResultSentence();
        if (string.IsNullOrEmpty(line))
            return;

        Color shown = ResultColour();
        shown.a = 1f;
        resultText.text = line;
        resultText.color = shown;
        resultText.gameObject.SetActive(true);
        resultHideAt = Time.time + ResultStaySeconds;
    }

    private string BuildResultSentence()
    {
        int buyIn = poker.BuyIn;
        int playerChips = poker.PlayerChips;
        int enemyChips = poker.EnemyChips;

        string handName = poker.PlayerBestHand != null
            ? poker.PlayerBestHand.Describe()
            : "your hand";
        string enemyHandName = poker.EnemyBestHand != null
            ? poker.EnemyBestHand.Describe()
            : "their hand";

        string line;
        switch (poker.Result)
        {
            case PokerResult.EnemyWinsByFold:
                line = $"You folded — the opponent takes this pot. Stack: {playerChips}.";
                break;
            case PokerResult.PlayerWinsByFold:
                line = $"Opponent folded — this pot is yours. Stack: {playerChips}.";
                break;
            case PokerResult.PlayerWinsShowdown:
                line =
                    $"Your {handName} beats their {enemyHandName}. "
                    + (poker.Pot > 0
                        ? $"Pot {poker.Pot} waits on the duel."
                        : "The pot is yours.");
                break;
            case PokerResult.EnemyWinsShowdown:
                line =
                    $"Their {enemyHandName} beats your {handName}. "
                    + (poker.Pot > 0
                        ? $"Pot {poker.Pot} waits on the duel."
                        : "The pot goes to them.");
                break;
            case PokerResult.TieShowdown:
                line = $"Showdown tie ({handName}). The pot is split evenly.";
                break;
            default:
                return null;
        }

        // 失败原因：这一手之后谁付不起下一手
        if (playerChips >= 0 && playerChips < buyIn)
        {
            line += $" You are at {playerChips}, below the {buyIn} buy-in — this level is over.";
        }
        else if (enemyChips >= 0 && enemyChips < buyIn)
        {
            line += $" The opponent is at {enemyChips}, below the {buyIn} buy-in — level cleared.";
        }

        return line;
    }

    private Color ResultColour()
    {
        switch (poker.Result)
        {
            case PokerResult.PlayerWinsByFold:
            case PokerResult.PlayerWinsShowdown:
                return new Color(0.55f, 0.90f, 0.65f);
            case PokerResult.EnemyWinsByFold:
            case PokerResult.EnemyWinsShowdown:
                return new Color(0.95f, 0.60f, 0.55f);
            default:
                return TEXT_STRONG;
        }
    }

    /// <summary>
    /// 教程换课时把上一手的结果行收掉，免得新课程下面挂着上一手的结论。
    /// </summary>
    public void ClearResult()
    {
        ClearResultLine();
    }

    /// <summary>结果行当前是否有内容（教程用它把桌面自己的结论压掉）。</summary>
    public bool HasResult
    {
        get { return resultText != null && resultText.text.Length > 0; }
    }

    private void ClearResultLine()
    {
        resultHideAt = -1f;
        if (resultText != null)
        {
            Color opaque = resultText.color;
            opaque.a = 1f;
            resultText.color = opaque;
            resultText.text = "";
            resultText.gameObject.SetActive(false);
        }
    }

    // =========================================================
    // UI BUILDERS（与 PokerUI 同款实现，跨类私有 helper 没法复用）
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
        text.font = Resources.GetBuiltinResource<Font>(FontResource);
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;

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
            20
        );
        txt.raycastTarget = false;

        return btn;
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

    private static void FullStretch(GameObject go)
    {
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

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
}
