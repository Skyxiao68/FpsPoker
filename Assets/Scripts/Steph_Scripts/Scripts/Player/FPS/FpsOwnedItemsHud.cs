using System.Collections.Generic;
using System.Text;
using PokerShop;
using PokerShopBridge;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public class FpsOwnedItemsHud : MonoBehaviour
{
    private static FpsOwnedItemsHud instance;
    private static ShopCatalog catalog;
    private static FpsItemDefinition[] combatItems;

    private const float PanelWidth = 370f;
    private const float RowHeight = 58f;
    private const float RowsTop = 54f;
    private const float MaxPanelHeight = 560f;

    private Canvas overlayCanvas;
    private RectTransform panel;
    private TextMeshProUGUI heading;
    private TextMeshProUGUI subtitle;
    private RectTransform rowsRoot;
    private bool inCombat;
    private float nextRefreshTime;
    private string lastDisplay;

    private sealed class ItemDisplayEntry
    {
        public string name;
        public string description;
        public int count;
        public Sprite icon;

        public ItemDisplayEntry(string name, string description, int count, Sprite icon)
        {
            this.name = name;
            this.description = description;
            this.count = count;
            this.icon = icon;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterForScenes()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool poker = FindAnyObjectByType<PokerGameManager>() != null;
        bool combat = FindAnyObjectByType<CombatSceneController>() != null;
        if (!poker && !combat && instance == null)
            return;

        if (instance == null)
        {
            GameObject hud = new GameObject("Owned Items HUD",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            instance = hud.AddComponent<FpsOwnedItemsHud>();
            DontDestroyOnLoad(hud);
        }

        if (poker)
            combatItems = null;
        instance.Configure(poker, combat);
    }

    public static void SetCombatItems(FpsItemDefinition[] items)
    {
        combatItems = items;
        if (instance != null)
            instance.RefreshNow();
    }

    private void Awake()
    {
        overlayCanvas = GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);

        GameObject panelObject = new GameObject("Items Panel", typeof(RectTransform));
        panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(transform, false);

        heading = CreateText("Items Heading", "FPS LOADOUT", 20f,
            new Color(0.9f, 0.98f, 0.97f), new Vector2(4f, -4f),
            new Vector2(300f, 26f), true);
        subtitle = CreateText("Items Subheading", "ACTIVE THIS DUEL", 11.5f,
            new Color(0.27f, 0.83f, 0.78f), new Vector2(5f, -30f),
            new Vector2(300f, 18f), true);

        GameObject rowsObject = new GameObject("FPS Item Rows", typeof(RectTransform));
        rowsRoot = rowsObject.GetComponent<RectTransform>();
        rowsRoot.SetParent(panel, false);
        rowsRoot.anchorMin = new Vector2(0f, 1f);
        rowsRoot.anchorMax = new Vector2(1f, 1f);
        rowsRoot.pivot = new Vector2(0.5f, 1f);
        rowsRoot.anchoredPosition = Vector2.zero;
        rowsRoot.sizeDelta = Vector2.zero;
    }

    private TextMeshProUGUI CreateText(string objectName, string value,
        float size, Color color, Vector2 position, Vector2 sizeDelta, bool bold)
    {
        GameObject labelObject = new GameObject(objectName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(panel, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = size;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = color;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.outlineWidth = 0.15f;
        label.outlineColor = Color.black;
        return label;
    }

    private void Configure(bool poker, bool combat)
    {
        inCombat = combat;

        // Poker has its own item panel (PokerItemsPanel) with a compact row per item.
        // Keep this shared paragraph HUD for the FPS side only, so the two never stack.
        overlayCanvas.enabled = combat;
        if (!overlayCanvas.enabled)
            return;

        overlayCanvas.sortingOrder = poker ? 150 : 20;
        Vector2 corner = Vector2.one;
        panel.anchorMin = panel.anchorMax = corner;
        panel.pivot = corner;
        panel.anchoredPosition = poker
            ? new Vector2(-18f, -60f) : new Vector2(-28f, -28f);
        panel.sizeDelta = poker
            ? new Vector2(340f, 230f) : new Vector2(PanelWidth, 160f);
        FindCatalog();
        RefreshNow();
    }

    private void Update()
    {
        if (!overlayCanvas.enabled || Time.unscaledTime < nextRefreshTime)
            return;
        nextRefreshTime = Time.unscaledTime + 0.25f;
        RefreshNow();
    }

    private static void FindCatalog()
    {
        if (catalog != null)
            return;

        if (Shop.Service != null && Shop.Service.Catalog != null)
        {
            catalog = Shop.Service.Catalog;
            return;
        }

        ShopCatalog[] loaded = Resources.FindObjectsOfTypeAll<ShopCatalog>();
        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null && loaded[i].items != null &&
                loaded[i].items.Count > 0)
            {
                catalog = loaded[i];
                return;
            }
        }
    }

    private void RefreshNow()
    {
        if (heading == null || !overlayCanvas.enabled)
            return;

        FindCatalog();
        List<ItemDisplayEntry> entries = GetFpsItemEntries(
            catalog, PlayerInventory.Instance, inCombat ? combatItems : null);
        string display = GetDisplayKey(entries);

        // 一件道具都没有时整块面板收起来：空着一块 "ITEMS / None" 只会挡住牌桌，
        // 不管是从主菜单进场还是没逛商店进下一关，看到的都是这块占位卡。
        bool hasItems = entries.Count > 0;
        if (panel != null && panel.gameObject.activeSelf != hasItems)
            panel.gameObject.SetActive(hasItems);

        if (display != lastDisplay)
        {
            RebuildRows(entries);
            lastDisplay = display;
        }
    }

    public static Sprite GetIcon(string shopItemId)
    {
        if (string.IsNullOrEmpty(shopItemId))
            return null;

        FindCatalog();
        ShopItem item = catalog != null ? catalog.Find(shopItemId) : null;
        if (item != null && item.icon != null)
            return item.icon;

        ShopItem[] loaded = Resources.FindObjectsOfTypeAll<ShopItem>();
        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null && loaded[i].id == shopItemId && loaded[i].icon != null)
                return loaded[i].icon;
        }

        return null;
    }

    private static List<ItemDisplayEntry> GetFpsItemEntries(
        ShopCatalog sourceCatalog,
        PlayerInventory inventory,
        FpsItemDefinition[] duelItems)
    {
        var entries = new List<ItemDisplayEntry>();
        var displayed = new Dictionary<ItemId, int>();

        if (sourceCatalog != null && inventory != null)
        {
            foreach (ShopItem shopItem in sourceCatalog.items)
            {
                if (shopItem == null || !(shopItem.payload is FpsItemDefinition fpsItem) ||
                    fpsItem.ItemId == ItemId.None)
                    continue;

                int count = inventory.GetCount(fpsItem.ItemId) -
                    DisplayedCount(displayed, fpsItem.ItemId);
                if (count <= 0)
                    continue;

                entries.Add(new ItemDisplayEntry(fpsItem.DisplayName,
                    fpsItem.Description, count, shopItem.icon));
                displayed[fpsItem.ItemId] = DisplayedCount(displayed, fpsItem.ItemId) + count;
            }
        }

        if (duelItems != null)
        {
            foreach (FpsItemDefinition fpsItem in duelItems)
            {
                if (fpsItem == null || fpsItem.ItemId == ItemId.None ||
                    DisplayedCount(displayed, fpsItem.ItemId) > 0)
                    continue;

                int count = inventory != null
                    ? Mathf.Max(1, inventory.GetCount(fpsItem.ItemId)) : 1;
                entries.Add(new ItemDisplayEntry(fpsItem.DisplayName,
                    fpsItem.Description, count, GetIcon(fpsItem.FpsItemId)));
                displayed[fpsItem.ItemId] = count;
            }
        }

        if (inventory != null)
        {
            foreach (KeyValuePair<ItemId, int> owned in inventory.All)
            {
                if (!IsFpsItem(owned.Key))
                    continue;

                int count = owned.Value - DisplayedCount(displayed, owned.Key);
                if (count <= 0)
                    continue;

                entries.Add(new ItemDisplayEntry(owned.Key.ToString(),
                    "FPS duel item", count, GetIconByItemId(sourceCatalog, owned.Key)));
                displayed[owned.Key] = DisplayedCount(displayed, owned.Key) + count;
            }
        }

        return entries;
    }

    private static Sprite GetIconByItemId(ShopCatalog sourceCatalog, ItemId id)
    {
        if (sourceCatalog != null)
        {
            foreach (ShopItem shopItem in sourceCatalog.items)
            {
                if (shopItem != null && shopItem.payload is FpsItemDefinition fpsItem &&
                    fpsItem.ItemId == id && shopItem.icon != null)
                    return shopItem.icon;
            }
        }

        return GetIcon(id.ToString());
    }

    private static bool IsFpsItem(ItemId id)
    {
        switch (id)
        {
            case ItemId.Deadeye:
            case ItemId.Quickstep:
            case ItemId.IronHeart:
            case ItemId.Headhunter:
            case ItemId.LifeSteal:
            case ItemId.Marked:
            case ItemId.LastStand:
            case ItemId.GoldenBullet:
                return true;
            default:
                return false;
        }
    }

    private static string GetDisplayKey(List<ItemDisplayEntry> entries)
    {
        var key = new StringBuilder();
        foreach (ItemDisplayEntry entry in entries)
        {
            key.Append(entry.name).Append('|')
                .Append(entry.description).Append('|')
                .Append(entry.count).Append(';');
        }
        return key.ToString();
    }

    private void RebuildRows(List<ItemDisplayEntry> entries)
    {
        for (int i = rowsRoot.childCount - 1; i >= 0; i--)
            Destroy(rowsRoot.GetChild(i).gameObject);

        float panelHeight = RowsTop + entries.Count * RowHeight;
        panel.sizeDelta = new Vector2(PanelWidth, panelHeight);
        float fit = panelHeight > MaxPanelHeight ? MaxPanelHeight / panelHeight : 1f;
        panel.localScale = new Vector3(fit, fit, 1f);
        subtitle.text = entries.Count == 1
            ? "1 ITEM ACTIVE THIS DUEL"
            : $"{entries.Count} ITEMS ACTIVE THIS DUEL";

        for (int i = 0; i < entries.Count; i++)
            CreateItemRow(entries[i], i);
    }

    private void CreateItemRow(ItemDisplayEntry entry, int index)
    {
        GameObject rowObject = new GameObject("FPS Item Row",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.SetParent(rowsRoot, false);
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.anchoredPosition = new Vector2(0f, -RowsTop - index * RowHeight);
        rowRect.sizeDelta = new Vector2(0f, RowHeight - 5f);
        Image rowImage = rowObject.GetComponent<Image>();
        rowImage.color = new Color(0.07f, 0.12f, 0.14f, 0.9f);
        rowImage.raycastTarget = false;

        GameObject iconObject = new GameObject("Item Icon",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(rowRect, false);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconRect.anchoredPosition = new Vector2(7f, -5f);
        iconRect.sizeDelta = new Vector2(44f, 44f);
        Image icon = iconObject.GetComponent<Image>();
        icon.sprite = entry.icon;
        icon.preserveAspect = true;
        icon.color = entry.icon != null ? Color.white : new Color(0.2f, 0.82f, 0.78f);
        icon.raycastTarget = false;

        TextMeshProUGUI nameText = CreateRowText(rowRect, "Item Name",
            entry.name, 17f, FontStyles.Bold, new Vector2(60f, -5f),
            new Vector2(220f, 21f), Color.white);
        nameText.alignment = TextAlignmentOptions.MidlineLeft;

        TextMeshProUGUI descriptionText = CreateRowText(rowRect, "Item Description",
            entry.description, 12f, FontStyles.Normal, new Vector2(60f, -26f),
            new Vector2(PanelWidth - 70f, 26f), new Color(0.78f, 0.86f, 0.87f));
        descriptionText.alignment = TextAlignmentOptions.TopLeft;
        descriptionText.textWrappingMode = TextWrappingModes.Normal;
        descriptionText.maxVisibleLines = 2;

        if (entry.count > 1)
        {
            TextMeshProUGUI countText = CreateRowText(rowRect, "Item Count",
                "x" + entry.count, 14f, FontStyles.Bold,
                new Vector2(-8f, -6f), new Vector2(40f, 20f),
                new Color(0.37f, 0.94f, 0.84f));
            RectTransform countRect = countText.rectTransform;
            countRect.anchorMin = countRect.anchorMax = Vector2.one;
            countRect.pivot = Vector2.one;
            countRect.anchoredPosition = new Vector2(-8f, -6f);
            countText.alignment = TextAlignmentOptions.MidlineRight;
        }
    }

    private TextMeshProUGUI CreateRowText(RectTransform parent, string objectName,
        string value, float size, FontStyles style, Vector2 position,
        Vector2 sizeDelta, Color color)
    {
        GameObject labelObject = new GameObject(objectName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.outlineWidth = 0.15f;
        label.outlineColor = Color.black;
        return label;
    }

    public static string FormatOwnedItems(ShopCatalog sourceCatalog,
        PlayerInventory inventory, FpsItemDefinition[] duelItems)
    {
        var entries = new List<string>();
        var displayed = new Dictionary<ItemId, int>();
        PokerInventoryProvider purchases = new PokerInventoryProvider();

        if (sourceCatalog != null && inventory != null)
        {
            foreach (ShopItem item in sourceCatalog.items)
            {
                if (item == null)
                    continue;
                ItemId id = InventoryIdFor(item);
                if (id == ItemId.None)
                    continue;

                int remaining = inventory.GetCount(id) - DisplayedCount(displayed, id);
                int count = id == ItemId.CardSwapTicket
                    ? Mathf.Min(remaining, purchases.GetCount(item))
                    : remaining;
                if (count <= 0)
                    continue;

                entries.Add(Describe(item.displayName, item.description, count));
                displayed[id] = DisplayedCount(displayed, id) + count;
            }
        }

        if (duelItems != null)
        {
            foreach (FpsItemDefinition item in duelItems)
            {
                if (item == null || DisplayedCount(displayed, item.ItemId) > 0)
                    continue;
                int count = inventory != null
                    ? Mathf.Max(1, inventory.GetCount(item.ItemId)) : 1;
                entries.Add(Describe(item.DisplayName, item.Description, count));
                displayed[item.ItemId] = count;
            }
        }

        if (inventory != null)
        {
            foreach (KeyValuePair<ItemId, int> owned in inventory.All)
            {
                int remaining = owned.Value - DisplayedCount(displayed, owned.Key);
                if (remaining > 0)
                    entries.Add(DescribeFallback(owned.Key, remaining));
            }
        }

        return entries.Count > 0 ? string.Join("\n\n", entries) : "None";
    }

    private static int DisplayedCount(Dictionary<ItemId, int> shown, ItemId id)
    {
        return shown.TryGetValue(id, out int count) ? count : 0;
    }

    private static ItemId InventoryIdFor(ShopItem item)
    {
        if (item.payload is FpsItemDefinition fpsItem)
            return fpsItem.ItemId;

        switch (item.id)
        {
            case "card_swap":
            case "double_swap": return ItemId.CardSwapTicket;
            case "cheat_glove": return ItemId.CheatGlove;
            case "queen_of_hearts": return ItemId.QueenOfHearts;
            default: return ItemId.None;
        }
    }

    private static string Describe(string itemName, string description, int count)
    {
        string name = string.IsNullOrWhiteSpace(itemName) ? "Item" : itemName;
        string quantity = count > 1 ? " x" + count : "";
        return string.IsNullOrWhiteSpace(description)
            ? name + quantity : name + quantity + ": " + description;
    }

    private static string DescribeFallback(ItemId id, int count)
    {
        switch (id)
        {
            case ItemId.CardSwapTicket:
                return Describe("Swap Ticket", "Swap a card during poker.", count);
            case ItemId.CheatGlove:
                return Describe("Cheat Glove", "See an enemy card.", count);
            case ItemId.QueenOfHearts:
                return Describe("Queen of Hearts", "Hearts grant damage instead of health.", count);
            case ItemId.Deadeye:
                return Describe("Deadeye", "Adds damage to FPS shots.", count);
            case ItemId.Quickstep:
                return Describe("Quickstep", "Adds FPS move and attack speed.", count);
            case ItemId.IronHeart:
                return Describe("Iron Heart", "Adds FPS maximum health.", count);
            case ItemId.Headhunter:
                return Describe("Headhunter", "Raises FPS headshot damage from 1.75x to 2.5x.", count);
            case ItemId.LifeSteal:
                return Describe("LifeSteal", "Gain 2 health for every 10 damage dealt.", count);
            case ItemId.Marked:
                return Describe("Marked", "Headshots reveal the enemy through walls for 10 seconds.", count);
            case ItemId.LastStand:
                return Describe("LastStand", "Survive one lethal hit with 1 health and slow time for 10 seconds.", count);
            case ItemId.GoldenBullet:
                return Describe("GoldenBullet", "The first shot after each reload deals triple damage.", count);
            default:
                return Describe(id.ToString(), "Owned item.", count);
        }
    }
}
