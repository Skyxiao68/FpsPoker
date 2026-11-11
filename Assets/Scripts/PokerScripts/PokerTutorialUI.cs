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
public partial class PokerTutorialUI : MonoBehaviour
{
    private const int CanvasOrder = 300;
    private const string FontResource = "LegacyRuntime.ttf";

    private PokerGameManager poker;

    private GameObject canvasRoot;
    private GameObject helpPanel;
    private Button helpButton;
    private Text resultText;
    private Image resultBack;

    private float resultHideAt = -1f;

    // 结果行停 10 秒再淡出 0.6 秒：短到不会跟着进下一手，长到能读完一句话。
    private const float ResultStaySeconds = 10f;
    private const float ResultFadeSeconds = 0.6f;

    // 参考面板：四行属性 = 数值 / 数值条填充 / 与对手的差值 / 一句真实换算
    private readonly Text[] helpRowValue = new Text[4];
    private readonly Image[] helpRowBar = new Image[4];
    private readonly Text[] helpRowDelta = new Text[4];
    private readonly Text[] helpRowPlain = new Text[4];
    private Text helpFormula;
    private readonly Text[] helpSurvival = new Text[4];

    // 三层折叠：0 = HAND RANKINGS，1 = THIS HAND，2 = THE POT；-1 表示全收起
    private int helpOpenSection = -1;
    private readonly GameObject[] helpBodies = new GameObject[3];
    private readonly Text[] helpHeaderLabels = new Text[3];

    // 牌型表两种画法（真牌 / 文字 pips），先都做出来给 Ben 对比
    private GameObject helpRanksCards;

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
            SetResultRowVisible(!covered);
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
        scaler.matchWidthOrHeight = 0.5f; // 与 PokerCanvas 一致，避免窗口不是 16:9 时被裁

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
        helpOpenSection = -1;
        ApplyHelpSections();
        RefreshHelpStats();
        helpPanel.SetActive(true);
        SetHelpButtonVisible(false);
    }

    /// <summary>
    /// 把四行属性、算式、生存线刷成「当前这手牌算出来的值」。
    /// 数值走 ItemEffectBridge（和实战同一个函数），否则买了物品后面板与战斗各说一套。
    /// </summary>
    private void RefreshHelpStats()
    {
        if (poker == null || helpRowValue[0] == null)
            return;

        HandResult best = poker.PlayerBestHand;
        if (best == null && poker.PlayerHand != null)
        {
            List<Card> known = new List<Card>(poker.PlayerHand);
            known.AddRange(poker.CommunityCards);
            if (known.Count >= 5)
                best = HandEvaluator.Evaluate(known);
        }

        bool hasMine = best != null;
        CombatStats mine = hasMine ? ItemEffectBridge.GetCombatStats(best) : default(CombatStats);
        float[] values = hasMine
            ? new[] { mine.attack, mine.health, mine.fireRate, mine.moveSpeed }
            : new[] { 0f, 0f, 0f, 0f };

        float[] theirs = new float[4];
        bool hasTheirs = poker.EnemyBestHand != null;
        if (hasTheirs)
        {
            CombatStats e = CombatStatsFactory.Compute(poker.EnemyBestHand);
            theirs = new[] { e.attack, e.health, e.fireRate, e.moveSpeed };
        }

        string[] plain = !hasMine
            ? new[] { "—", "—", "—", "—" }
            : new[]
            {
                "adds +" + Mathf.RoundToInt(mine.attack) + " to shot damage",
                mine.health > 0.5f
                    ? "+" + Mathf.RoundToInt(mine.health) + " extra max health"
                    : "no extra health — you fight at base HP",
                mine.fireRate > 0.5f
                    ? "cooldown ÷" + (1f + mine.fireRate / 100f).ToString("0.00")
                        + " · mag " + Mathf.Max(30, Mathf.RoundToInt(mine.fireRate))
                    : "no fire-rate bonus",
                mine.moveSpeed > 0.5f
                    ? "+" + Mathf.RoundToInt(mine.moveSpeed) + " move speed"
                    : "base move speed",
            };

        for (int i = 0; i < 4; i++)
        {
            bool on = hasMine;
            helpRowValue[i].text = on
                ? (Mathf.Abs(values[i] - Mathf.Round(values[i])) < 0.05f
                    ? Mathf.RoundToInt(values[i]).ToString()
                    : values[i].ToString("0.0"))
                : "—";
            helpRowValue[i].color = on && values[i] > 0.5f ? TEXT_STRONG : TEXT_MUTED;

            helpRowBar[i].fillAmount = 1f;
            RectTransform fill = helpRowBar[i].rectTransform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(on ? PokerStatUnits.BarFill(i, values[i]) : 0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            if (!hasTheirs || !on)
            {
                helpRowDelta[i].text = "—";
                helpRowDelta[i].color = TEXT_MUTED;
            }
            else
            {
                float d = values[i] - theirs[i];
                bool better = d > 0.05f;
                bool worse = d < -0.05f;
                helpRowDelta[i].text = better
                    ? "▲ +" + Mathf.Abs(d).ToString("0.0")
                    : worse ? "▼ −" + Mathf.Abs(d).ToString("0.0") : "=";
                helpRowDelta[i].color = better
                    ? new Color(0.42f, 0.82f, 0.48f)
                    : worse ? new Color(0.87f, 0.47f, 0.42f) : TEXT_MUTED;
            }

            helpRowPlain[i].text = plain[i];
            helpRowPlain[i].color = !on ? TEXT_MUTED : values[i] > 0.5f ? PLAIN : WARN;
        }

        // ---- 算式实例：花色点数 → 合计 → × 倍率 → 四项 ----
        if (best == null)
        {
            helpFormula.text = "Two cards so far — the numbers appear once five cards are known.";
        }
        else
        {
            int spade = 0, heart = 0, club = 0, diamond = 0;
            List<Card> five = best.bestFive ?? new List<Card>();
            foreach (Card c in five)
            {
                if (c == null)
                    continue;
                if (c.Suit == Suit.Spades) spade += c.GetAttributeValue();
                else if (c.Suit == Suit.Hearts) heart += c.GetAttributeValue();
                else if (c.Suit == Suit.Clubs) club += c.GetAttributeValue();
                else diamond += c.GetAttributeValue();
            }
            helpFormula.text = string.Format(
                "<color={0}>♠{1}</color> <color={2}>♥{3}</color> <color={4}>♣{5}</color> <color={6}>♦{7}</color>"
                    + "  ×  <color=#FFC74C>{8:0.00}</color>  →  ATK {9:0} · HP {10:0} · ROF {11:0} · SPD {12:0}",
                HEX_SPADE, spade, HEX_HEART, heart, HEX_CLUB, club, HEX_DIAMOND, diamond,
                best.GetMultiplier(),
                values[0], values[1], values[2], values[3]
            );
        }

        // ---- 生存线：买入 / 我的筹码 / 还能买几次 / 买不起就结束 ----
        GameFlowManager gfm = GameFlowManager.Instance;
        int buyIn = gfm != null ? gfm.CurrentBuyIn : (poker != null ? poker.BuyIn : 0);
        int stack = poker != null ? poker.PlayerChips : 0;
        int left = buyIn > 0 ? stack / buyIn : 0;

        helpSurvival[0].text = buyIn.ToString();
        helpSurvival[1].text = stack.ToString();
        helpSurvival[2].text = left.ToString();
        helpSurvival[3].text = buyIn > 0 ? "below " + buyIn + " the run ends" : "—";
    }

    private void CloseHelp()
    {
        helpPanel.SetActive(false);
        SetHelpButtonVisible(true);
    }

    /// <summary>牌型表的一行：用哪几张真牌演示 + 对应的 HandType（名字和倍率都从它取）。</summary>
    private class RankRow
    {
        public readonly HandType type;
        public readonly string[] cards;

        public RankRow(HandType type, string[] cards)
        {
            this.type = type;
            this.cards = cards;
        }
    }

    private static readonly Dictionary<string, Sprite> helpCardSprites = new Dictionary<string, Sprite>();

    /// <summary>从 Resources 取一张牌面；取不到返回 null（调用方保留文字兜底）。</summary>
    private static Sprite HelpCardSprite(string key)
    {
        Sprite cached;
        if (helpCardSprites.TryGetValue(key, out cached))
            return cached;
        Sprite sprite = Resources.Load<Sprite>("PokerUI/Cards/" + key);
        helpCardSprites[key] = sprite;
        return sprite;
    }

    /// <summary>在参考面板里摆一张小牌面（纯展示，不吃点击）。</summary>
    private Image CreateCardImage(Transform parent, string name, string key, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.sprite = HelpCardSprite(key);
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
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
        // 结果行贴在公共牌正下方，不能压到牌面：牌的下沿是 0.450，
        // 所以整行收在 0.404–0.440，并加一层半透明底衬保证任何桌布上都读得清。
        GameObject backGO = new GameObject("TutorialResultBack", typeof(RectTransform), typeof(Image));
        backGO.transform.SetParent(canvasRoot.transform, false);
        RectTransform backRT = backGO.GetComponent<RectTransform>();
        backRT.anchorMin = new Vector2(0.26f, 0.392f);
        backRT.anchorMax = new Vector2(0.74f, 0.434f);
        backRT.offsetMin = Vector2.zero;
        backRT.offsetMax = Vector2.zero;
        Image back = backGO.GetComponent<Image>();
        back.color = new Color(0.055f, 0.09f, 0.063f, 0.72f);
        back.raycastTarget = false;
        resultBack = back;

        resultText = CreateText(
            canvasRoot.transform,
            "TutorialResultLine",
            "",
            new Vector2(0.27f, 0.396f),
            new Vector2(0.73f, 0.431f),
            TextAnchor.MiddleCenter,
            15
        );
        resultText.color = TEXT_STRONG;
        resultText.raycastTarget = false; // 结果行不该吃掉桌面的点击
        resultText.horizontalOverflow = HorizontalWrapMode.Wrap;
        resultText.verticalOverflow = VerticalWrapMode.Truncate;
        resultText.gameObject.SetActive(false);
        backGO.SetActive(false);
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
        SetResultRowVisible(true);
        resultHideAt = Time.time + ResultStaySeconds;
    }

    // 结果行和它的底衬永远一起开关，避免只剩一块黑底或只剩一行字
    private void SetResultRowVisible(bool visible)
    {
        if (resultText != null)
            resultText.gameObject.SetActive(visible);
        if (resultBack != null)
            resultBack.gameObject.SetActive(visible);
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
            SetResultRowVisible(false);
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
