using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Poker-side "what am I carrying" panel.
//
// The shared FPS HUD (FpsOwnedItemsHud) prints one long paragraph per item, which
// pushed everything else off the poker table. This one is poker-only: a compact row
// per item (monogram tile, name, count, one-line effect), long wording kept in a
// footer that follows the pointer. Combat-only items sink to the bottom in muted
// colour so they never crowd out the three items that actually matter at the table.
public class PokerItemsPanel : MonoBehaviour
{
    private const int MaxRows = 6;
    private const float RowHeight = 30f;
    private const float HeaderHeight = 26f;
    private const float FooterHeight = 20f;
    private const float PanelWidth = 150f;
    private const float RowInset = 8f;
    private const float PanelHeight = HeaderHeight + RowHeight * MaxRows + FooterHeight;

    private static readonly Color PANEL_BG = new Color(0.055f, 0.085f, 0.065f, 0.92f);
    private static readonly Color ROW_BG = new Color(0.098f, 0.133f, 0.110f, 1f);
    private static readonly Color ROW_BG_COMBAT = new Color(0.078f, 0.098f, 0.086f, 1f);
    private static readonly Color TILE_BG = new Color(0.145f, 0.180f, 0.150f, 1f);
    private static readonly Color EDGE = new Color(0.310f, 0.380f, 0.310f, 1f);
    private static readonly Color AMBER = new Color(1.00f, 0.78f, 0.30f, 1f);
    private static readonly Color TEXT_STRONG = new Color(0.910f, 0.929f, 0.902f, 1f);
    private static readonly Color TEXT_SOFT = new Color(0.660f, 0.710f, 0.640f, 1f);
    private static readonly Color TEXT_MUTED = new Color(0.470f, 0.520f, 0.450f, 1f);

    private struct Row
    {
        public GameObject root;
        public Image background;
        public Image tile;
        public Text monogram;
        public Text count;
    }

    private RectTransform panelRect;
    private Image panelImage;
    private Text footer;
    private RectTransform tipRect;
    private Text tipTitle;
    private Text tipBody;
    private Text tipTag;
    private Image tipTagBg;
    private const float TipWidth = 220f;
    private readonly Row[] rows = new Row[MaxRows];
    private readonly List<ItemId> order = new List<ItemId>();
    private float nextRefresh;
    private string lastSignature = "";
    private string footerDefault;
    private int hoverIndex = -1;
    private int shownRows;
    private int combatStartIndex;

    private static readonly ItemId[] PokerOrder =
    {
        ItemId.DoubleSwap, ItemId.CardSwapTicket, ItemId.ColdRead, ItemId.CheatGlove,
        ItemId.Precog, ItemId.Monochrome, ItemId.DrawSix,
        ItemId.PairUp, ItemId.Momentum,
        ItemId.QueenOfHearts, ItemId.SpadeTattoo, ItemId.RedShift, ItemId.FlatBonus, ItemId.LuckyHigh,
        ItemId.Pressure, ItemId.Bait
    };

    private static readonly ItemId[] CombatOrder =
    {
        ItemId.Deadeye, ItemId.Quickstep, ItemId.IronHeart
    };

    public void Initialize(Transform canvasRoot)
    {
        Build(canvasRoot != null ? canvasRoot : FindLiveCanvas());
        Refresh(true);
    }

    // 牌桌重建 / 场景重载会销毁旧 Canvas，面板会被留在根节点上（无 Canvas 就不渲染），
    // 所以每帧自检一次父节点，必要时挂回当前 PokerCanvas。
    private static Transform FindLiveCanvas()
    {
        GameObject live = GameObject.Find("PokerCanvas");
        return live != null ? live.transform : null;
    }

    private void ReparentIfOrphaned()
    {
        if (panelRect == null || panelRect.parent != null)
            return;

        Transform live = FindLiveCanvas();
        if (live == null)
            return;

        panelRect.SetParent(live, false);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-18f, -74f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        if (tipRect != null && tipRect.parent == null)
            tipRect.SetParent(live, false);
    }

    private void Update()
    {
        ReparentIfOrphaned();
        if (panelRect == null)
            return;
        if (Time.unscaledTime < nextRefresh)
            return;
        nextRefresh = Time.unscaledTime + 0.25f;
        Refresh(false);
    }

    private void Build(Transform canvasRoot)
    {
        GameObject panelObject = new GameObject("PokerItemsPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.SetParent(canvasRoot, false);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-18f, -74f);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        panelImage = panelObject.GetComponent<Image>();
        panelImage.sprite = Resources.Load<Sprite>("PokerUI/Rounded/panel_r10");
        panelImage.type = Image.Type.Sliced;
        panelImage.color = PANEL_BG;
        panelImage.raycastTarget = false;

        Outline outline = panelObject.AddComponent<Outline>();
        outline.effectColor = EDGE;
        outline.effectDistance = new Vector2(1f, -1f);

        CreateText(panelRect, "ItemsHeading", "ITEMS", 12f, AMBER, FontStyle.Bold,
            TextAnchor.MiddleLeft, new Vector2(RowInset, -5f),
            new Vector2(PanelWidth - RowInset * 2f, 18f));

        for (int i = 0; i < MaxRows; i++)
            rows[i] = CreateRow(i);

        // 细节只在 hover 时出现：平时面板只有「图标 + 数量」，尽量小。
        footer = CreateText(panelRect, "ItemsFooter", "", 9.5f, TEXT_SOFT,
            FontStyle.Normal, TextAnchor.UpperLeft,
            new Vector2(RowInset, 5f),
            new Vector2(PanelWidth - RowInset * 2f, FooterHeight - 10f));
        footer.rectTransform.anchorMin = Vector2.zero;
        footer.rectTransform.anchorMax = Vector2.zero;
        footer.rectTransform.pivot = Vector2.zero;
        footer.horizontalOverflow = HorizontalWrapMode.Wrap;
        footer.verticalOverflow = VerticalWrapMode.Truncate;
        footerDefault = "";
        footer.text = footerDefault;
        footer.gameObject.SetActive(false);

        BuildTooltip(canvasRoot);
    }

    // Balatro 式浮动 tooltip：悬停某行时在面板左侧浮出一块，标题 + 说明 + 分类铭牌。
    private void BuildTooltip(Transform canvasRoot)
    {
        GameObject tipObject = new GameObject("PokerItemsTooltip",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tipRect = tipObject.GetComponent<RectTransform>();
        tipRect.SetParent(canvasRoot != null ? canvasRoot : FindLiveCanvas(), false);
        tipRect.anchorMin = tipRect.anchorMax = new Vector2(1f, 1f);
        tipRect.pivot = new Vector2(1f, 1f);

        Image bg = tipObject.GetComponent<Image>();
        bg.sprite = Resources.Load<Sprite>("PokerUI/Rounded/panel_r10");
        bg.type = Image.Type.Sliced;
        bg.color = PANEL_BG;
        bg.raycastTarget = false;

        Outline outline = tipObject.AddComponent<Outline>();
        outline.effectColor = EDGE;
        outline.effectDistance = new Vector2(1f, -1f);

        tipTitle = CreateText(tipRect, "TipTitle", "", 12f, AMBER, FontStyle.Bold,
            TextAnchor.MiddleLeft, new Vector2(10f, -8f), new Vector2(TipWidth - 20f, 16f));

        tipBody = CreateText(tipRect, "TipBody", "", 10f, TEXT_STRONG, FontStyle.Normal,
            TextAnchor.UpperLeft, new Vector2(10f, -28f), new Vector2(TipWidth - 20f, 40f));
        tipBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        tipBody.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject tagObject = new GameObject("TipTag",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform tagRect = tagObject.GetComponent<RectTransform>();
        tagRect.SetParent(tipRect, false);
        tagRect.anchorMin = tagRect.anchorMax = new Vector2(0.5f, 0f);
        tagRect.pivot = new Vector2(0.5f, 0f);
        tagRect.anchoredPosition = new Vector2(0f, 8f);
        tagRect.sizeDelta = new Vector2(TipWidth - 20f, 18f);
        tipTagBg = tagObject.GetComponent<Image>();
        tipTagBg.sprite = Resources.Load<Sprite>("PokerUI/Rounded/panel_r10");
        tipTagBg.type = Image.Type.Sliced;
        tipTagBg.color = ROW_BG;
        tipTagBg.raycastTarget = false;

        tipTag = CreateText(tagRect, "TipTagLabel", "", 9.5f, AMBER, FontStyle.Bold,
            TextAnchor.MiddleCenter, Vector2.zero, tagRect.sizeDelta);
        tipTag.rectTransform.anchorMin = Vector2.zero;
        tipTag.rectTransform.anchorMax = Vector2.one;
        tipTag.rectTransform.offsetMin = Vector2.zero;
        tipTag.rectTransform.offsetMax = Vector2.zero;

        tipRect.gameObject.SetActive(false);
    }

    private Row CreateRow(int index)
    {
        Row row = new Row();

        GameObject rootObject = new GameObject("ItemRow" + index,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        row.root = rootObject;
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.SetParent(panelRect, false);
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.anchoredPosition = new Vector2(RowInset, -HeaderHeight - RowHeight * index);
        rootRect.sizeDelta = new Vector2(PanelWidth - RowInset * 2f, RowHeight - 4f);
        row.background = rootObject.GetComponent<Image>();
        row.background.sprite = Resources.Load<Sprite>("PokerUI/Rounded/panel_r10");
        row.background.type = Image.Type.Sliced;
        row.background.color = ROW_BG;
        row.background.raycastTarget = true;

        GameObject tileObject = new GameObject("Tile",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform tileRect = tileObject.GetComponent<RectTransform>();
        tileRect.SetParent(rootRect, false);
        tileRect.anchorMin = tileRect.anchorMax = new Vector2(0f, 0.5f);
        tileRect.pivot = new Vector2(0f, 0.5f);
        tileRect.anchoredPosition = new Vector2(5f, 0f);
        tileRect.sizeDelta = new Vector2(22f, 22f);
        row.tile = tileObject.GetComponent<Image>();
        row.tile.sprite = Resources.Load<Sprite>("PokerUI/Rounded/panel_r10");
        row.tile.type = Image.Type.Sliced;
        row.tile.color = TILE_BG;
        row.tile.raycastTarget = false;

        row.monogram = CreateText(tileRect, "Monogram", "?", 12f, AMBER,
            FontStyle.Bold, TextAnchor.MiddleCenter, Vector2.zero, tileRect.sizeDelta);
        row.monogram.rectTransform.anchorMin = Vector2.zero;
        row.monogram.rectTransform.anchorMax = Vector2.one;
        row.monogram.rectTransform.offsetMin = Vector2.zero;
        row.monogram.rectTransform.offsetMax = Vector2.zero;

        row.count = CreateText(rootRect, "Count", "", 12f, AMBER,
            FontStyle.Bold, TextAnchor.MiddleRight, new Vector2(-6f, 0f),
            new Vector2(56f, 18f));
        row.count.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        row.count.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        row.count.rectTransform.pivot = new Vector2(1f, 0.5f);

        EventTrigger trigger = rootObject.AddComponent<EventTrigger>();
        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => ShowTip(index));
        trigger.triggers.Add(enter);
        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => HideTip());
        trigger.triggers.Add(exit);

        row.root.SetActive(false);
        return row;
    }

    private static Text CreateText(Transform parent, string objectName, string value,
        float size, Color color, FontStyle style, TextAnchor anchor,
        Vector2 topLeft, Vector2 sizeDelta)
    {
        GameObject labelObject = new GameObject(objectName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = sizeDelta;

        Text label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.text = value;
        label.fontSize = Mathf.RoundToInt(size);
        label.fontStyle = style;
        label.alignment = anchor;
        label.color = color;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        return label;
    }

    private void ShowTip(int index)
    {
        if (tipRect == null || index < 0 || index >= order.Count)
            return;

        ItemMeta meta = MetaFor(order[index]);
        int count = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetCount(order[index]) : 1;
        bool combat = index >= combatStartIndex;

        tipTitle.text = count > 1 ? meta.Name + "  x" + count : meta.Name;
        tipBody.text = string.IsNullOrEmpty(meta.Long) ? meta.Short : meta.Long;
        tipTag.text = combat ? "COMBAT TOOL" : "POKER TOOL";
        tipTag.color = combat ? TEXT_STRONG : new Color(0.06f, 0.07f, 0.06f, 1f);
        tipTagBg.color = combat ? new Color(0.235f, 0.255f, 0.235f, 1f) : AMBER;

        int lines = Mathf.Clamp(Mathf.CeilToInt(tipBody.text.Length / 34f), 1, 4);
        float bodyHeight = lines * 13f;
        tipBody.rectTransform.sizeDelta = new Vector2(TipWidth - 20f, bodyHeight);
        tipRect.sizeDelta = new Vector2(TipWidth, 28f + bodyHeight + 30f);

        float rowTop = -74f - HeaderHeight - RowHeight * index - 4f;
        tipRect.anchoredPosition = new Vector2(-18f - PanelWidth - 10f, rowTop);
        tipRect.gameObject.SetActive(true);
        hoverIndex = index;
    }

    private void HideTip()
    {
        hoverIndex = -1;
        if (tipRect != null)
            tipRect.gameObject.SetActive(false);
    }

    // 高度 = 标题 + 已显示行数 + （hover 时有）细节区。
    private void ApplyHeight()
    {
        if (panelRect == null)
            return;
        bool detail = footer != null && footer.gameObject.activeSelf;
        panelRect.sizeDelta = new Vector2(PanelWidth,
            HeaderHeight + RowHeight * shownRows + (detail ? FooterHeight : 0f));
    }

    private void Refresh(bool force)
    {
        if (panelRect == null)
            return;

        PlayerInventory inventory = PlayerInventory.Instance;
        if (inventory == null)
        {
            panelRect.gameObject.SetActive(false);
            return;
        }

        order.Clear();
        string signature = "";
        for (int i = 0; i < PokerOrder.Length; i++)
        {
            int c = inventory.GetCount(PokerOrder[i]);
            if (c > 0)
            {
                order.Add(PokerOrder[i]);
                signature += PokerOrder[i] + ":" + c + ";";
            }
        }
        int combatStart = order.Count;
        for (int i = 0; i < CombatOrder.Length; i++)
        {
            int c = inventory.GetCount(CombatOrder[i]);
            if (c > 0)
            {
                order.Add(CombatOrder[i]);
                signature += CombatOrder[i] + ":" + c + ";";
            }
        }

        if (!force && signature == lastSignature)
            return;
        lastSignature = signature;

        bool any = order.Count > 0;
        panelRect.gameObject.SetActive(any);
        if (!any)
            return;

        int shown = Mathf.Min(order.Count, MaxRows);
        for (int i = 0; i < MaxRows; i++)
        {
            bool visible = i < shown;
            rows[i].root.SetActive(visible);
            if (!visible)
                continue;

            ItemId id = order[i];
            ItemMeta meta = MetaFor(id);
            bool combat = i >= combatStart;

            rows[i].background.color = combat ? ROW_BG_COMBAT : ROW_BG;
            rows[i].monogram.text = meta.Monogram;
            rows[i].monogram.color = combat ? TEXT_MUTED : AMBER;
            int count = inventory.GetCount(id);
            // 永久工具不消耗，数量没有意义，直接用 ∞ 表示「一直在」。
            rows[i].count.text = PokerItemTraits.IsPermanentTool(id) ? "∞" : "x" + count;
            rows[i].count.color = combat ? TEXT_MUTED : AMBER;
        }

        // 平时只有「图标 + 数量」；hover 才长出一块细节区。
        shownRows = shown;
        combatStartIndex = combatStart;
        footerDefault = order.Count > shown ? "+" + (order.Count - shown) + " more" : "";
        if (footer != null && hoverIndex < 0)
        {
            footer.text = footerDefault;
            footer.gameObject.SetActive(!string.IsNullOrEmpty(footerDefault));
        }
        ApplyHeight();
    }

    private struct ItemMeta
    {
        public string Name;
        public string Monogram;
        public string Short;
        public string Long;
    }

    private static ItemMeta MetaFor(ItemId id)
    {
        switch (id)
        {
            case ItemId.CardSwapTicket:
                return new ItemMeta
                {
                    Name = "Card Swap",
                    Monogram = "S",
                    Short = "Swap one card every hand",
                    Long = "One swap per hand, every hand. Permanent tool — never used up.",
                };
            case ItemId.DoubleSwap:
                return new ItemMeta
                {
                    Name = "Double Swap",
                    Monogram = "D",
                    Short = "Swap two cards every hand",
                    Long = "Two swaps per hand, every hand. Permanent tool — never used up.",
                };
            case ItemId.CheatGlove:
                return new ItemMeta
                {
                    Name = "Cheat Glove",
                    Monogram = "G",
                    Short = "Peek at one enemy card",
                    Long = "Reveals one of the enemy's hole cards for this hand. Permanent tool.",
                };
            case ItemId.ColdRead:
                return new ItemMeta
                {
                    Name = "Cold Read",
                    Monogram = "R",
                    Short = "Peek at both enemy cards",
                    Long = "Reveals both of the enemy's hole cards for this hand. Permanent tool.",
                };
            case ItemId.QueenOfHearts:
                return new ItemMeta
                {
                    Name = "Queen of Hearts",
                    Monogram = "Q",
                    Short = "Hearts also feed ATK (50%)",
                    Long = "Hearts still give HP, and now also add 50% of their points to ATK.",
                };
            case ItemId.SpadeTattoo:
                return new ItemMeta
                {
                    Name = "Spade Tattoo",
                    Monogram = "T",
                    Short = "Spades also feed health",
                    Long = "Every spade still adds ATK, and now adds 70% of its value to HP too.",
                };
            case ItemId.RedShift:
                return new ItemMeta
                {
                    Name = "Red Shift",
                    Monogram = "X",
                    Short = "Diamonds also feed attack",
                    Long = "Every diamond still adds SPD, and now adds 50% of its value to ATK too.",
                };
            case ItemId.FlatBonus:
                return new ItemMeta
                {
                    Name = "Flat Bonus",
                    Monogram = "F",
                    Short = "Every hand type ×+0.10",
                    Long = "Adds +0.10 to the hand multiplier, whatever the hand turns out to be.",
                };
            case ItemId.LuckyHigh:
                return new ItemMeta
                {
                    Name = "Lucky High",
                    Monogram = "H",
                    Short = "High card ×1.30, others −0.05",
                    Long = "High card pays 1.30 instead of 1.00; every stronger hand loses 0.05.",
                };
            case ItemId.Pressure:
                return new ItemMeta
                {
                    Name = "Pressure",
                    Monogram = "P",
                    Short = "Your raises scare them more",
                    Long = "When you raise, the opponent folds more often. It does nothing while you check.",
                };
            case ItemId.Bait:
                return new ItemMeta
                {
                    Name = "Bait",
                    Monogram = "B",
                    Short = "Your checks invite a bet",
                    Long = "When you check, the opponent is more likely to bet into you.",
                };
            case ItemId.PairUp:
                return new ItemMeta
                {
                    Name = "Pair Up",
                    Monogram = "U",
                    Short = "A pair counts as two pair",
                    Long = "A single pair is settled as two pair: it beats any other single pair, but still loses to a real two pair or better.",
                };
            case ItemId.Momentum:
                return new ItemMeta
                {
                    Name = "Momentum",
                    Monogram = "M",
                    Short = "Each raise adds +0.05 (max +0.15)",
                    Long = "Every raise you make this hand adds 0.05 to the showdown multiplier, up to 0.15. Checking adds nothing.",
                };
            case ItemId.Precog:
                return new ItemMeta
                {
                    Name = "Precog",
                    Monogram = "V",
                    Short = "See the deck top every hand",
                    Long = "The next three cards from the deck are shown at the start of every hand.",
                };
            case ItemId.Monochrome:
                return new ItemMeta
                {
                    Name = "Monochrome",
                    Monogram = "M",
                    Short = "Whole hand counts as one suit",
                    Long = "Every card counts as the suit with the highest total points; the rest read 0.",
                };
            case ItemId.DrawSix:
                return new ItemMeta
                {
                    Name = "Draw Six",
                    Monogram = "6",
                    Short = "A third hole card",
                    Long = "You are dealt three hole cards and keep the best five of the eight you can see.",
                };
            case ItemId.Deadeye:
                return new ItemMeta
                {
                    Name = "Deadeye",
                    Monogram = "D",
                    Short = "Combat only · +shot damage",
                    Long = "Only pays out in the duel; it does nothing at the poker table.",
                };
            case ItemId.Quickstep:
                return new ItemMeta
                {
                    Name = "Quickstep",
                    Monogram = "V",
                    Short = "Combat only · +move speed",
                    Long = "Only pays out in the duel; it does nothing at the poker table.",
                };
            case ItemId.IronHeart:
                return new ItemMeta
                {
                    Name = "Iron Heart",
                    Monogram = "I",
                    Short = "Combat only · +max health",
                    Long = "Only pays out in the duel; it does nothing at the poker table.",
                };
            default:
                return new ItemMeta
                {
                    Name = id.ToString(),
                    Monogram = "?",
                    Short = "Owned item",
                    Long = "Owned item.",
                };
        }
    }
}
