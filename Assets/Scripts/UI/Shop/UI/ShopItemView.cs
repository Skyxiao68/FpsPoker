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

        public void Setup(ShopItem item, bool atLimit, bool canAfford, Action onBuy)
        {
            if (icon) icon.sprite = item.icon;
            nameText.text = item.displayName;
            priceText.text = item.price.ToString("N0");
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBuy());
            buyButton.interactable = !atLimit && canAfford;
            if (buyLabel) buyLabel.text = atLimit ? "Maxed" : "Buy";
        }
    }
}