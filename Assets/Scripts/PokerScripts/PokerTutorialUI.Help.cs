using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 参考面板（"?"）的第二部分：L0 常驻层 + 三层折叠。
///
/// 设计分工：Game Designer 定的信息分层（必看 = 我现在是什么 / 为什么是这个数 / 离输还有多远），
/// UI Designer 定的栅格与字号。核心修正两条：
///   1. 数值走 ItemEffectBridge.GetCombatStats（和实战同一个函数），不再用 CombatStatsFactory；
///   2. 四行分别带单位标（points / ÷ cooldown）与一句真实换算，避免四项被横向比较。
/// </summary>
public partial class PokerTutorialUI
{
    private static readonly Color ROW_BG = new Color(0.102f, 0.137f, 0.118f, 1f);
    private static readonly Color ROW_BG_ZERO = new Color(0.094f, 0.125f, 0.102f, 1f);
    private static readonly Color BAR_TRACK = new Color(0.173f, 0.227f, 0.196f, 1f);
    private static readonly Color HEADER_BG = new Color(0.200f, 0.259f, 0.220f, 1f);
    private static readonly Color AMBER = new Color(1.00f, 0.78f, 0.30f);
    private static readonly Color PLAIN = new Color(0.78f, 0.78f, 0.78f);
    private static readonly Color WARN = new Color(0.95f, 0.60f, 0.55f);
    private static readonly Color GOOD = new Color(0.42f, 0.82f, 0.48f);

    private static readonly string[] StatNames = { "Attack", "Health", "Rate of Fire", "Speed" };
    private static readonly string[] StatAbbr = { "ATK", "HP", "ROF", "SPD" };
    private static readonly string[] StatUnit = { "points", "points", "÷ cooldown", "points" };
    private static readonly string[] StatHex = { HEX_SPADE, HEX_HEART, HEX_CLUB, HEX_DIAMOND };
    private static readonly string[] StatGlyph = { "♠", "♥", "♣", "♦" };

    private static readonly string[] SectionTitles = { "HAND RANKINGS", "THIS HAND, DECOMPOSED", "THE POT" };
    private static readonly string[] SectionSummary =
    {
        "10 types · ×1.00–×3.00",
        "which five · suit chain · item delta",
        "3 lines",
    };

    private static readonly Vector2 RankStylePos = new Vector2(0.628f, 0.893f);

    // =========================================================
    // 构建
    // =========================================================
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
            new Vector2(0.09f, 0.893f),
            new Vector2(0.60f, 0.937f),
            TextAnchor.MiddleLeft,
            24
        ).color = TEXT_STRONG;

        CreateButton(
            helpPanel.transform,
            "HelpCloseX",
            "✕",
            new Vector2(0.848f, 0.893f),
            new Vector2(0.906f, 0.937f),
            BTN_HELP,
            CloseHelp
        );

        BuildHelpStatRows();
        BuildHelpFormula();
        BuildHelpSurvival();
        BuildHelpSections();

        helpPanel.SetActive(false);
    }

    /// <summary>四行属性：花色 / 全称+缩写 / 数值条 / 数值 / 与对手差值 / 一句真实换算。</summary>
    private void BuildHelpStatRows()
    {
        const float top = 0.862f;
        const float pitch = 0.060f;
        const float height = 0.052f;

        for (int i = 0; i < 4; i++)
        {
            float yTop = top - i * pitch;
            float yBot = yTop - height;

            CreatePanel(
                helpPanel.transform,
                ROW_BG,
                new Vector2(0.090f, yBot),
                new Vector2(0.910f, yTop)
            );

            Text glyph = CreateText(
                helpPanel.transform,
                "HelpSuit" + i,
                StatGlyph[i],
                new Vector2(0.096f, yBot),
                new Vector2(0.124f, yTop),
                TextAnchor.MiddleCenter,
                16
            );
            glyph.color = HexColor(StatHex[i]);

            CreateText(
                helpPanel.transform,
                "HelpName" + i,
                StatNames[i] + "  <size=11><color=#8A9686>" + StatAbbr[i] + "</color></size>",
                new Vector2(0.128f, yBot),
                new Vector2(0.300f, yTop),
                TextAnchor.MiddleLeft,
                15
            ).color = TEXT_STRONG;

            // 单位标：让「40 点伤害」和「40% 射速」不再看起来可比
            CreateText(
                helpPanel.transform,
                "HelpUnit" + i,
                StatUnit[i],
                new Vector2(0.128f, yBot),
                new Vector2(0.300f, yBot + 0.014f),
                TextAnchor.LowerLeft,
                9
            ).color = TEXT_MUTED;

            // 数值条：轨道 + 填充（fill 的 anchorMax.x 由 RefreshHelpStats 设定）
            Image track = CreatePanel(
                helpPanel.transform,
                BAR_TRACK,
                new Vector2(0.312f, yBot + 0.018f),
                new Vector2(0.548f, yBot + 0.034f)
            );
            track.raycastTarget = false;

            Image fillImg = CreateRectImage(track.transform, "Fill", HexColor(StatHex[i]));
            fillImg.raycastTarget = false;
            helpRowBar[i] = fillImg;

            helpRowValue[i] = CreateText(
                helpPanel.transform,
                "HelpValue" + i,
                "—",
                new Vector2(0.556f, yBot),
                new Vector2(0.600f, yTop),
                TextAnchor.MiddleRight,
                18
            );
            helpRowValue[i].fontStyle = FontStyle.Bold;

            helpRowDelta[i] = CreateText(
                helpPanel.transform,
                "HelpDelta" + i,
                "—",
                new Vector2(0.606f, yBot),
                new Vector2(0.664f, yTop),
                TextAnchor.MiddleRight,
                12
            );

            helpRowPlain[i] = CreateText(
                helpPanel.transform,
                "HelpPlain" + i,
                "",
                new Vector2(0.672f, yBot),
                new Vector2(0.905f, yTop),
                TextAnchor.MiddleLeft,
                11
            );
            helpRowPlain[i].horizontalOverflow = HorizontalWrapMode.Wrap;
            helpRowPlain[i].verticalOverflow = VerticalWrapMode.Truncate;
        }
    }

    /// <summary>算式块：花色点数 → 合计 → × 倍率 → 四项（一行看完整条链路）。</summary>
    private void BuildHelpFormula()
    {
        CreatePanel(
            helpPanel.transform,
            ROW_BG,
            new Vector2(0.090f, 0.556f),
            new Vector2(0.910f, 0.622f)
        );

        CreateText(
            helpPanel.transform,
            "HelpFormulaLabel",
            "HOW ONE NUMBER IS BUILT",
            new Vector2(0.100f, 0.599f),
            new Vector2(0.560f, 0.616f),
            TextAnchor.MiddleLeft,
            10
        ).color = TEXT_MUTED;

        helpFormula = CreateText(
            helpPanel.transform,
            "HelpFormula",
            "",
            new Vector2(0.100f, 0.562f),
            new Vector2(0.900f, 0.596f),
            TextAnchor.MiddleLeft,
            13
        );
        helpFormula.color = TEXT_SOFT;

        // 取舍提示：这块面板真正欠玩家的一句话
        CreatePanel(
            helpPanel.transform,
            AMBER,
            new Vector2(0.088f, 0.508f),
            new Vector2(0.092f, 0.548f)
        );

        CreateText(
            helpPanel.transform,
            "HelpTradeoff",
            "Spread across four suits: all four stats live, each one weak.\n"
                + "Stack one suit: that stat spikes, the other three read "
                + "<color=#FFC74C><b>0</b></color>.",
            new Vector2(0.100f, 0.504f),
            new Vector2(0.905f, 0.552f),
            TextAnchor.MiddleLeft,
            12
        ).color = TEXT_SOFT;

        CreateText(
            helpPanel.transform,
            "HelpNote",
            "<b>Every street</b> recomputes these numbers — the turn or river can make them lower.",
            new Vector2(0.090f, 0.480f),
            new Vector2(0.910f, 0.498f),
            TextAnchor.MiddleLeft,
            11
        ).color = TEXT_MUTED;
    }

    /// <summary>生存线：买入 / 我的筹码 / 还能买几次 / 买不起就结束。</summary>
    private void BuildHelpSurvival()
    {
        CreatePanel(
            helpPanel.transform,
            ROW_BG,
            new Vector2(0.090f, 0.396f),
            new Vector2(0.910f, 0.472f)
        );

        string[] labels = { "BUY-IN", "YOUR STACK", "BUY-INS LEFT", "BELOW BUY-IN" };
        for (int i = 0; i < 4; i++)
        {
            float x0 = 0.100f + i * 0.202f;

            CreateText(
                helpPanel.transform,
                "HelpSurvLabel" + i,
                labels[i],
                new Vector2(x0, 0.438f),
                new Vector2(x0 + 0.190f, 0.456f),
                TextAnchor.MiddleLeft,
                10
            ).color = TEXT_MUTED;

            helpSurvival[i] = CreateText(
                helpPanel.transform,
                "HelpSurvValue" + i,
                "—",
                new Vector2(x0, 0.402f),
                new Vector2(x0 + 0.190f, 0.436f),
                TextAnchor.MiddleLeft,
                15
            );
            helpSurvival[i].fontStyle = FontStyle.Bold;
            helpSurvival[i].color = i == 0 || i == 2 ? AMBER : TEXT_STRONG;
        }

        if (helpSurvival[3] != null)
        {
            helpSurvival[3].fontSize = 12;
            helpSurvival[3].color = WARN;
        }
    }

    /// <summary>三层折叠：表头固定在下方，展开体从表头往上长（同一时间只开一个）。</summary>
    private void BuildHelpSections()
    {
        for (int i = 0; i < 3; i++)
        {
            float yTop = 0.332f - i * 0.044f;
            float yBot = yTop - 0.032f;

            int captured = i;
            Button head = CreateButton(
                helpPanel.transform,
                "HelpSection" + i,
                "",
                new Vector2(0.090f, yBot),
                new Vector2(0.910f, yTop),
                HEADER_BG,
                () => ToggleSection(captured)
            );

            helpHeaderLabels[i] = CreateText(
                helpPanel.transform,
                "HelpSectionLabel" + i,
                SectionTitles[i],
                new Vector2(0.100f, yBot),
                new Vector2(0.400f, yTop),
                TextAnchor.MiddleLeft,
                12
            );
            helpHeaderLabels[i].fontStyle = FontStyle.Bold;
            helpHeaderLabels[i].raycastTarget = false;

            Text summary = CreateText(
                helpPanel.transform,
                "HelpSectionSummary" + i,
                SectionSummary[i],
                new Vector2(0.410f, yBot),
                new Vector2(0.870f, yTop),
                TextAnchor.MiddleLeft,
                11
            );
            summary.color = TEXT_MUTED;
            summary.raycastTarget = false;

            Text chevron = CreateText(
                helpPanel.transform,
                "HelpSectionChevron" + i,
                "▸",
                new Vector2(0.872f, yBot),
                new Vector2(0.902f, yTop),
                TextAnchor.MiddleCenter,
                12
            );
            chevron.color = TEXT_MUTED;
            chevron.raycastTarget = false;

            GameObject body = new GameObject("HelpSectionBody" + i);
            body.transform.SetParent(helpPanel.transform, false);
            RectTransform bodyRT = body.AddComponent<RectTransform>();
            bodyRT.anchorMin = new Vector2(0.090f, 0.338f);
            bodyRT.anchorMax = new Vector2(0.910f, 0.556f);
            bodyRT.offsetMin = Vector2.zero;
            bodyRT.offsetMax = Vector2.zero;
            CreatePanel(body.transform, ROW_BG, Vector2.zero, Vector2.one);
            helpBodies[i] = body;
        }

        BuildRanksBody(helpBodies[0].transform);
        BuildThisHandBody(helpBodies[1].transform);
        BuildPotBody(helpBodies[2].transform);

        ApplyHelpSections();
    }

    // =========================================================
    // 折叠交互
    // =========================================================
    private void ToggleSection(int index)
    {
        helpOpenSection = helpOpenSection == index ? -1 : index;
        ApplyHelpSections();
    }

    private void ApplyHelpSections()
    {
        for (int i = 0; i < helpBodies.Length; i++)
        {
            bool open = helpOpenSection == i;
            if (helpBodies[i] != null)
                helpBodies[i].SetActive(open);
            if (helpHeaderLabels[i] != null)
                helpHeaderLabels[i].color = open ? AMBER : TEXT_STRONG;
            Transform chevron = helpPanel.transform.Find("HelpSectionChevron" + i);
            if (chevron != null)
            {
                Text t = chevron.GetComponent<Text>();
                if (t != null)
                {
                    t.text = open ? "▾" : "▸";
                    t.color = open ? AMBER : TEXT_MUTED;
                }
            }
        }

        if (helpRanksCards != null)
            helpRanksCards.SetActive(true);
    }

    // =========================================================
    // 三层折叠的内容
    // =========================================================
    private static readonly RankRow[] RankRows =
    {
        new RankRow(HandType.RoyalFlush, new[] { "AS", "KS", "QS", "JS", "10S" }),
        new RankRow(HandType.StraightFlush, new[] { "9H", "8H", "7H", "6H", "5H" }),
        new RankRow(HandType.FourOfAKind, new[] { "9S", "9H", "9D", "9C", "KS" }),
        new RankRow(HandType.FullHouse, new[] { "KS", "KH", "KD", "4S", "4H" }),
        new RankRow(HandType.Flush, new[] { "AH", "JH", "8H", "5H", "2H" }),
        new RankRow(HandType.Straight, new[] { "9S", "8H", "7D", "6C", "5S" }),
        new RankRow(HandType.ThreeOfAKind, new[] { "KS", "KH", "KD", "7S", "2H" }),
        new RankRow(HandType.TwoPair, new[] { "KS", "KH", "7D", "7C", "3S" }),
        new RankRow(HandType.OnePair, new[] { "KS", "KH", "8D", "5C", "2S" }),
        new RankRow(HandType.HighCard, new[] { "AS", "JD", "8C", "5H", "2S" }),
    };

    /// <summary>
    /// 牌型表两种画法：
    /// CARDS = 5 列 × 2 行，每格摆 5 张真牌（34×50）——「一眼就看得出是这五张」；
    /// PIPS  = 2 列 × 5 行，纯文字 ♠A ♠K …——省 50 个 Graphic，行更密。
    /// </summary>
    private void BuildRanksBody(Transform body)
    {
        // 折叠体只有 0.218 屏高（实测 1050×157 画布），而且比例和屏幕不一样，
        // 所以这里直接用「body 本地比例」摆，不再从屏幕坐标换算。
        GameObject cardsRoot = new GameObject("HelpRanksCards");
        cardsRoot.transform.SetParent(body, false);
        FullStretch(cardsRoot);
        helpRanksCards = cardsRoot;

        const float cardW = 0.0335f;
        const float cardH = 0.300f;
        const float cardGap = 0.0035f;
        float[] rowTop = { 0.985f, 0.500f };

        for (int i = 0; i < RankRows.Length; i++)
        {
            int col = i % 5;
            int row = i / 5;
            float x0 = 0.012f + col * 0.198f;

            for (int c = 0; c < RankRows[i].cards.Length; c++)
            {
                float cx = x0 + c * (cardW + cardGap);
                CreateCardImage(
                    cardsRoot.transform,
                    "RankCard",
                    RankRows[i].cards[c],
                    new Vector2(cx, rowTop[row] - cardH),
                    new Vector2(cx + cardW, rowTop[row])
                );
            }

            HandResult probe = new HandResult { handType = RankRows[i].type };
            CreateText(
                cardsRoot.transform,
                "RankLabel",
                probe.GetDisplayName() + "  <color=#FFC74C>×" + probe.GetMultiplier().ToString("0.00") + "</color>",
                new Vector2(x0, rowTop[row] - cardH - 0.140f),
                new Vector2(x0 + 0.190f, rowTop[row] - cardH - 0.005f),
                TextAnchor.MiddleLeft,
                10
            ).color = TEXT_SOFT;
        }
    }

    /// <summary>L2：这一手是怎么算出来的（与摊牌面板同一套拆解写法）。</summary>
    private void BuildThisHandBody(Transform body)
    {
        CreateText(
            body,
            "HelpDecomp",
            "Pick <b>Lower of</b> the seven cards: the five that score best.\n"
                + "Suit points: A = 11, K/Q/J = 10, 2–10 = their number.\n"
                + "Sum each suit, then multiply all four by the hand multiplier.\n"
                + "<color=#FFC74C>Buy items and these numbers change — the panel already includes them.</color>",
            new Vector2(0.014f, 0.030f),
            new Vector2(0.950f, 0.960f),
            TextAnchor.UpperLeft,
            12
        ).color = TEXT_SOFT;
    }

    /// <summary>L3：底池三条（弃牌那句按真实结算写：买入先扣、弃牌不退）。</summary>
    private void BuildPotBody(Transform body)
    {
        CreateText(
            body,
            "HelpPot",
            "Showdown — the pot freezes; win the duel to take it.\n"
                + "Fold — you lose this hand's buy-in and the pot. No duel, no stats.\n"
                + "Buy-in — if the opponent can't pay it, the level is yours. If you can't, the run ends.",
            new Vector2(0.014f, 0.070f),
            new Vector2(0.950f, 0.950f),
            TextAnchor.UpperLeft,
            12
        ).color = TEXT_SOFT;
    }

    // =========================================================
    // 小工具
    // =========================================================
    private static Color HexColor(string hex)
    {
        Color c;
        return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.white;
    }

    private Image CreateRectImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }
}
