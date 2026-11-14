using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace PokerShop
{
    public class ShopItemView : MonoBehaviour
    {
        [SerializeField] Image icon;
        [SerializeField] TMP_Text nameText, priceText, buyLabel;
        [SerializeField] Button buyButton;

        [Header("Description（可选，没接线就不显示）")]
        [SerializeField] TMP_Text descriptionText;

        [Header("Sell（可选，没接线就没有卖出按钮）")]
        [SerializeField] Button sellButton;
        [SerializeField] TMP_Text sellLabel;

        /// <summary>旧签名：不带卖出。</summary>
        public void Setup(ShopItem item, bool atLimit, bool canAfford, Action onBuy)
        {
            Setup(item, atLimit, canAfford, onBuy, null, 0, false);
        }

        public void Setup(ShopItem item, bool atLimit, bool canAfford, Action onBuy,
                          Action onSell, int refund, bool canSell)
        {
            if (icon) icon.sprite = item.icon;
            nameText.text = item.displayName;
            priceText.text = PokerItemPricing.Price(item).ToString("N0");

            // 买之前就能看到这件道具做什么（playtest 两份反馈都提过这条）。
            if (descriptionText) descriptionText.text = item.description;

            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBuy());
            buyButton.interactable = !atLimit && canAfford;
            if (buyLabel) buyLabel.text = atLimit ? "Maxed" : "Buy";

            if (sellButton)
            {
                sellButton.onClick.RemoveAllListeners();
                if (onSell != null) sellButton.onClick.AddListener(() => onSell());
                sellButton.gameObject.SetActive(canSell);
                sellButton.interactable = canSell;
                if (sellLabel) sellLabel.text = $"Sell +{refund}";
            }
        }
    }
}
