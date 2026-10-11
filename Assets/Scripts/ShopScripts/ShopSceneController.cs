using PokerShop;
using PokerShopBridge;
using UnityEngine;

/// <summary>
/// ShopScene 的入口脚本。
/// 初始化商店系统，商店关闭后返回扑克场景。
/// </summary>
public class ShopSceneController : MonoBehaviour
{
    [SerializeField]
    private ShopCatalog catalog;

    [SerializeField]
    private ShopUI shopUiPrefab;

    [SerializeField]
    private int goodsPerVisit = 3;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var currency = new PokerCurrencyProvider();
        var inventory = new PokerInventoryProvider();
        var state = new PokerGameStateControl();
        // 访问次数 +1，然后传给 OfferProvider
        int visitNum = 1;
        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.IncrementShopVisit();
            visitNum = GameFlowManager.Instance.ShopVisitCount;
        }

        var offers = new WeightedRandomOfferProvider(
            goodsPerVisit,
            visitNum,
            item => item.maxOwned > 0 && inventory.GetCount(item) >= item.maxOwned
        );

        Shop.Init(catalog, currency, inventory, state, shopUiPrefab, offers);
        Shop.Service.ShopClosed += OnShopClosed;

        Shop.Open(new ShopContext { Round = 0, Tag = "postEncounter" });

        Debug.Log($"[ShopScene] 商店打开，玩家筹码 = {currency.GetBalance()}");
    }

    private void OnShopClosed()
    {
        Shop.Service.ShopClosed -= OnShopClosed;

        if (GameFlowManager.Instance != null)
        {
            Debug.Log($"[ShopScene] 商店关闭，玩家剩余 = {GameFlowManager.Instance.PlayerChips}");
            GameFlowManager.Instance.ReportShopResult();
        }
        else
        {
            Debug.LogError("[ShopScene] GameFlowManager 不存在，无法返回");
        }
    }
}
