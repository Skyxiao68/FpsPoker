using System.Collections.Generic;
using PokerShop;
using PokerShopBridge;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A read-only inventory panel shared by poker and FPS scenes. It uses the
// existing catalog and inventory without changing either game's item logic.
public class FpsOwnedItemsHud : MonoBehaviour
{
    private static FpsOwnedItemsHud instance;
    private static ShopCatalog catalog;
    private static FpsItemDefinition[] combatItems;

    private Canvas overlayCanvas;
    private RectTransform panel;
    private TextMeshProUGUI body;
    private bool inCombat;
    private float nextRefreshTime;
    private string lastDisplay;

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

        GameObject panelObject = new GameObject("Items Panel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel = panelObject.GetComponent<RectTransform>();
        panel.SetParent(transform, false);
        Image background = panelObject.GetComponent<Image>();
        background.color = new Color(0.06f, 0.09f, 0.07f, 0.78f);
        background.raycastTarget = false;

        CreateText("Items Heading", "ITEMS", 20f,
            new Color(1f, 0.85f, 0.55f), true);
        body = CreateText("Owned Item Descriptions", "None", 14f,
            Color.white, false);
    }

    private TextMeshProUGUI CreateText(string objectName, string value,
        float size, Color color, bool heading)
    {
        GameObject labelObject = new GameObject(objectName,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = labelObject.GetComponent<RectTransform>();
        rect.SetParent(panel, false);
        if (heading)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -9f);
            rect.sizeDelta = new Vector2(300f, 28f);
        }
        else
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12f, 12f);
            rect.offsetMax = new Vector2(-12f, -42f);
        }

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.text = value;
        label.fontSize = size;
        label.fontStyle = heading ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.color = color;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    private void Configure(bool poker, bool combat)
    {
        inCombat = combat;
        overlayCanvas.enabled = poker || combat;
        if (!overlayCanvas.enabled)
            return;

        overlayCanvas.sortingOrder = poker ? 150 : 20;
        Vector2 corner = poker ? Vector2.one : new Vector2(0f, 1f);
        panel.anchorMin = panel.anchorMax = corner;
        panel.pivot = corner;
        panel.anchoredPosition = poker
            ? new Vector2(-18f, -60f) : new Vector2(24f, -24f);
        panel.sizeDelta = poker
            ? new Vector2(340f, 230f) : new Vector2(440f, 230f);
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
        if (body == null || !overlayCanvas.enabled)
            return;

        FindCatalog();
        string display = FormatOwnedItems(catalog, PlayerInventory.Instance,
            inCombat ? combatItems : null);

        // 一件道具都没有时整块面板收起来：空着一块 "ITEMS / None" 只会挡住牌桌，
        // 不管是从主菜单进场还是没逛商店进下一关，看到的都是这块占位卡。
        bool hasItems = !string.IsNullOrEmpty(display) && display != "None";
        if (panel != null && panel.gameObject.activeSelf != hasItems)
            panel.gameObject.SetActive(hasItems);

        if (display != lastDisplay)
        {
            body.text = display;
            lastDisplay = display;
        }
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
            default:
                return Describe(id.ToString(), "Owned item.", count);
        }
    }
}
