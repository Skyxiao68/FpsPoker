using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 摊牌后的结算弹窗。
/// 显示：双方牌型对比、底池去向、花色→战斗属性的拆解。
/// 玩家点击 "Enter Combat" 后才真正进战斗。
///
/// 完全独立于 PokerUI，由 PokerCombatBridge 触发。
/// </summary>
public class ShowdownPanelUI : MonoBehaviour
{
    // 回调：玩家点击 Enter Combat
    private Action onEnterCombat;

    // 缓存 UI 引用，便于 Refresh
    private GameObject root;
    private Text resultTitleText;
    private Text playerHandText,
        playerTypeText;
    private Text enemyHandText,
        enemyTypeText;
    private Text potText;
    private Text buffBreakdownText;
    private Text baseLineText,
        multLineText,
        finalLineText;
    private Button enterCombatButton;

    // =========================================================
    // 创建 / 显示
    // =========================================================

    /// <summary>
    /// 显示结算面板。
    /// </summary>
    /// <param name="poker">当前的 PokerGameManager（用于读牌型和底池）</param>
    /// <param name="onEnter">玩家点击 Enter Combat 时的回调</param>
    public void Show(PokerGameManager poker, Action onEnter)
    {
        onEnterCombat = onEnter;

        if (root == null)
            BuildUI();

        Refresh(poker);
        root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    // =========================================================
    // 构建 UI
    // =========================================================

    private void BuildUI()
    {
        // 全屏遮罩
        root = new GameObject("ShowdownPanel");
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // 盖在 PokerUI (100) 之上

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        root.AddComponent<GraphicRaycaster>();

        // 半透明黑背景
        CreatePanel(root.transform, new Color(0, 0, 0, 0.78f), Vector2.zero, Vector2.one);

        // 中央卡片
        CreatePanel(
            root.transform,
            new Color(0.08f, 0.12f, 0.09f, 0.98f),
            new Vector2(0.18f, 0.10f),
            new Vector2(0.82f, 0.90f)
        );

        // ---- 标题 ----
        resultTitleText = CreateText(
            root.transform,
            "ResultTitle",
            "SHOWDOWN",
            new Vector2(0.20f, 0.83f),
            new Vector2(0.80f, 0.88f),
            TextAnchor.MiddleCenter,
            36
        );

        // ---- 双方牌型对比 ----
        // 左：玩家
        CreateText(
            root.transform,
            "YouLabel",
            "YOU",
            new Vector2(0.22f, 0.78f),
            new Vector2(0.49f, 0.82f),
            TextAnchor.MiddleCenter,
            16
        ).color = new Color(0.7f, 0.7f, 0.7f);

        playerHandText = CreateText(
            root.transform,
            "PlayerHand",
            "",
            new Vector2(0.22f, 0.70f),
            new Vector2(0.49f, 0.78f),
            TextAnchor.MiddleCenter,
            32
        );

        playerTypeText = CreateText(
            root.transform,
            "PlayerType",
            "",
            new Vector2(0.22f, 0.66f),
            new Vector2(0.49f, 0.70f),
            TextAnchor.MiddleCenter,
            20
        );

        // 中间分隔线
        CreatePanel(
            root.transform,
            new Color(0.4f, 0.4f, 0.4f, 0.6f),
            new Vector2(0.497f, 0.66f),
            new Vector2(0.503f, 0.80f)
        );

        // 右：敌人
        CreateText(
            root.transform,
            "EnemyLabel",
            "ENEMY",
            new Vector2(0.51f, 0.78f),
            new Vector2(0.78f, 0.82f),
            TextAnchor.MiddleCenter,
            16
        ).color = new Color(0.7f, 0.7f, 0.7f);

        enemyHandText = CreateText(
            root.transform,
            "EnemyHand",
            "",
            new Vector2(0.51f, 0.70f),
            new Vector2(0.78f, 0.78f),
            TextAnchor.MiddleCenter,
            32
        );

        enemyTypeText = CreateText(
            root.transform,
            "EnemyType",
            "",
            new Vector2(0.51f, 0.66f),
            new Vector2(0.78f, 0.70f),
            TextAnchor.MiddleCenter,
            20
        );

        // ---- 底池 ----
        potText = CreateText(
            root.transform,
            "PotLine",
            "",
            new Vector2(0.22f, 0.60f),
            new Vector2(0.78f, 0.65f),
            TextAnchor.MiddleCenter,
            22
        );

        // ---- Buff 拆解标题 ----
        CreateText(
            root.transform,
            "BuffHeader",
            "— COMBAT BUFFS —",
            new Vector2(0.22f, 0.55f),
            new Vector2(0.78f, 0.59f),
            TextAnchor.MiddleCenter,
            18
        ).color = new Color(0.6f, 0.6f, 0.6f);

        // ---- Buff 拆解（每花色一行） ----
        buffBreakdownText = CreateText(
            root.transform,
            "BuffBreakdown",
            "",
            new Vector2(0.28f, 0.36f),
            new Vector2(0.72f, 0.55f),
            TextAnchor.UpperLeft,
            22
        );

        // ---- Base / Mult / Final ----
        baseLineText = CreateText(
            root.transform,
            "BaseLine",
            "",
            new Vector2(0.28f, 0.31f),
            new Vector2(0.72f, 0.35f),
            TextAnchor.MiddleLeft,
            18
        );
        multLineText = CreateText(
            root.transform,
            "MultLine",
            "",
            new Vector2(0.28f, 0.27f),
            new Vector2(0.72f, 0.31f),
            TextAnchor.MiddleLeft,
            18
        );
        finalLineText = CreateText(
            root.transform,
            "FinalLine",
            "",
            new Vector2(0.28f, 0.22f),
            new Vector2(0.72f, 0.27f),
            TextAnchor.MiddleLeft,
            22
        );

        // ---- Enter Combat 按钮 ----
        enterCombatButton = CreateButton(
            root.transform,
            "EnterCombatBtn",
            "Enter Combat",
            new Vector2(0.35f, 0.12f),
            new Vector2(0.65f, 0.19f),
            new Color(0.2f, 0.6f, 0.35f),
            () =>
            {
                Hide();
                onEnterCombat?.Invoke();
            }
        );
    }

    // =========================================================
    // 填充数据
    // =========================================================

    private void Refresh(PokerGameManager poker)
    {
        bool playerWon = poker.Result == PokerResult.PlayerWinsShowdown;

        // ---- 标题 ----
        resultTitleText.text = playerWon ? "YOU WIN THE SHOWDOWN" : "YOU LOSE THE SHOWDOWN";
        resultTitleText.color = playerWon
            ? new Color(0.35f, 0.9f, 0.45f)
            : new Color(0.9f, 0.35f, 0.3f);

        // ---- 双方手牌 ----
        playerHandText.text = HandToString(poker.PlayerBestHand);
        playerTypeText.text =
            poker.PlayerBestHand != null ? poker.PlayerBestHand.GetDisplayName() : "";

        enemyHandText.text = HandToString(poker.EnemyBestHand);
        enemyTypeText.text =
            poker.EnemyBestHand != null ? poker.EnemyBestHand.GetDisplayName() : "";

        // 输赢方颜色
        playerTypeText.color = playerWon
            ? new Color(0.35f, 0.9f, 0.45f)
            : new Color(0.75f, 0.75f, 0.75f);
        enemyTypeText.color = !playerWon
            ? new Color(0.9f, 0.35f, 0.3f)
            : new Color(0.75f, 0.75f, 0.75f);

        // ---- 底池 ----
        int pot = poker.Pot;
        if (playerWon)
        {
            potText.text = $"Pot {pot}  →  you hold the pot, enemy challanges you to combat!";
            potText.color = new Color(0.9f, 0.85f, 0.4f);
        }
        else
        {
            potText.text = $"Pot {pot}  →  enemy holds the pot, challange them to combat!";
            potText.color = new Color(0.85f, 0.6f, 0.4f);
        }

        // ---- Buff 拆解 ----
        RenderBuffBreakdown(poker.PlayerBestHand);
    }

    private void RenderBuffBreakdown(HandResult bestHand)
    {
        if (bestHand == null || bestHand.bestFive == null)
        {
            buffBreakdownText.text = "";
            baseLineText.text = "";
            multLineText.text = "";
            finalLineText.text = "";
            return;
        }

        // ---- 累加各花色 ----
        float spades = 0,
            hearts = 0,
            clubs = 0,
            diamonds = 0;
        foreach (Card c in bestHand.bestFive)
        {
            int v = c.GetAttributeValue();
            switch (c.Suit)
            {
                case Suit.Spades:
                    spades += v;
                    break;
                case Suit.Hearts:
                    hearts += v;
                    break;
                case Suit.Clubs:
                    clubs += v;
                    break;
                case Suit.Diamonds:
                    diamonds += v;
                    break;
            }
        }

        float mult = bestHand.GetMultiplier();

        // ---- 四色配色：与手牌颜色一致 ----
        string cSpade = HexForSuit(Suit.Spades);
        string cHeart = HexForSuit(Suit.Hearts);
        string cClub = HexForSuit(Suit.Clubs);
        string cDiamond = HexForSuit(Suit.Diamonds);

        int nSpade = CountSuit(bestHand, Suit.Spades);
        int nHeart = CountSuit(bestHand, Suit.Hearts);
        int nClub = CountSuit(bestHand, Suit.Clubs);
        int nDiamond = CountSuit(bestHand, Suit.Diamonds);

        // ---- 数值来源括号 ----
        string detailSpade = DetailFor(bestHand, Suit.Spades);
        string detailHeart = DetailFor(bestHand, Suit.Hearts);
        string detailClub = DetailFor(bestHand, Suit.Clubs);
        string detailDiamond = DetailFor(bestHand, Suit.Diamonds);

        string parenSpade = string.IsNullOrEmpty(detailSpade) ? "" : $"   ({detailSpade})";
        string parenHeart = string.IsNullOrEmpty(detailHeart) ? "" : $"   ({detailHeart})";
        string parenClub = string.IsNullOrEmpty(detailClub) ? "" : $"   ({detailClub})";
        string parenDiamond = string.IsNullOrEmpty(detailDiamond) ? "" : $"   ({detailDiamond})";

        // ---- 整行都染上该花色颜色 ----
        string gray = "#111111";
        string cS = nSpade == 0 ? gray : cSpade;
        string cH = nHeart == 0 ? gray : cHeart;
        string cC = nClub == 0 ? gray : cClub;
        string cD = nDiamond == 0 ? gray : cDiamond;

        string line = "";
        line += $"<color={cS}>♠ Spades   x{nSpade}   ATK +{spades}{parenSpade}</color>\n";
        line += $"<color={cH}>♥ Hearts   x{nHeart}   HP  +{hearts}{parenHeart}</color>\n";
        line += $"<color={cC}>♣ Clubs    x{nClub}   ROF +{clubs}{parenClub}</color>\n";
        line += $"<color={cD}>♦ Diamonds x{nDiamond}   SPD +{diamonds}{parenDiamond}</color>";
        buffBreakdownText.text = line;

        // ---- Base / Mult / Final ----
        baseLineText.text = $"Base    ATK {spades}   HP {hearts}   ROF {clubs}   SPD {diamonds}";
        multLineText.text = $"Mult    x{mult:F2}   ({bestHand.GetDisplayName()})";
        finalLineText.text =
            $"<b>Final   ATK {spades * mult:F1}   HP {hearts * mult:F1}   "
            + $"ROF {clubs * mult:F1}   SPD {diamonds * mult:F1}</b>";
        finalLineText.color = new Color(1f, 0.9f, 0.4f);
    }

    // =========================================================
    // 工具
    // =========================================================

    private int CountSuit(HandResult h, Suit s)
    {
        if (h == null || h.bestFive == null)
            return 0;
        int n = 0;
        foreach (Card c in h.bestFive)
            if (c.Suit == s)
                n++;
        return n;
    }

    /// <summary>
    /// 返回某花色所有牌的面值，用 " + " 连接。
    /// 例如 ♥ 有 10、10、6 三张牌，返回 "10 + 10 + 6"。
    /// 没有该花色的牌时返回空字符串。
    /// </summary>
    private string DetailFor(HandResult h, Suit suit)
    {
        if (h == null || h.bestFive == null)
            return "";

        var vals = new System.Collections.Generic.List<int>();
        foreach (var c in h.bestFive)
            if (c.Suit == suit)
                vals.Add(c.GetAttributeValue());

        if (vals.Count == 0)
            return "";
        return string.Join(" + ", vals);
    }

    private string HandToString(HandResult h)
    {
        if (h == null || h.bestFive == null)
            return "";
        string s = "";
        foreach (Card c in h.bestFive)
        {
            string hex = HexForSuit(c.Suit);
            s += $"<color={hex}>{c}</color>  ";
        }
        return s;
    }

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
        text.supportRichText = true; // 允许 <color> / <b>

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
        cb.highlightedColor = new Color(
            Mathf.Clamp01(normalColor.r * 1.2f),
            Mathf.Clamp01(normalColor.g * 1.2f),
            Mathf.Clamp01(normalColor.b * 1.2f)
        );
        cb.pressedColor = new Color(
            normalColor.r * 0.75f,
            normalColor.g * 0.75f,
            normalColor.b * 0.75f
        );
        cb.selectedColor = normalColor;
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
            24
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

    /// <summary>
    /// 花色 → 颜色 Hex。手牌和 Buff 行共用，保证两处颜色一致。
    /// </summary>
    private string HexForSuit(Suit s)
    {
        switch (s)
        {
            case Suit.Spades:
                return "#A0A0A0"; // 浅灰（近似黑桃"黑"）
            case Suit.Hearts:
                return "#FF8080"; // 红
            case Suit.Clubs:
                return "#90E0FF"; // 浅蓝
            case Suit.Diamonds:
                return "#FFD060"; // 金橙
            default:
                return "#FFFFFF";
        }
    }

    /// <summary>把 "#RRGGBB" 转成 Color</summary>
    private Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out Color c))
            return c;
        return Color.white;
    }
}
