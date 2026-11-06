using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 老千手套：在 PokerUI 刷新敌人手牌之后，覆盖显示第一张。
/// 完全不动 PokerUI.cs —— 用"后订阅"+"覆盖"的方式生效。
///
/// 挂载：和 PokerUI 同一个 GameObject，或者 UI 生成的父级上。
/// </summary>
public class CheatGloveOverlay : MonoBehaviour
{
    [SerializeField]
    private PokerGameManager poker;

    private Text firstCardText;
    private Text secondCardText;
    private bool subscribed = false;

    private void Start()
    {
        if (poker == null)
            poker = FindAnyObjectByType<PokerGameManager>();

        if (poker == null)
        {
            Debug.LogError("[CheatGlove] 找不到 PokerGameManager");
            return;
        }

        // 等一帧，保证 PokerUI.BuildUI 已经执行
        StartCoroutine(SubscribeNextFrame());
    }

    private IEnumerator SubscribeNextFrame()
    {
        // 等两帧：一帧给 PokerUI 创建 UI，一帧给它订阅事件
        yield return null;
        yield return null;

        // 找敌人两个卡牌槽位（PokerUI 结构：PokerCanvas/EnemySlot1/Face）
        firstCardText = FindSlotText("EnemySlot1");
        secondCardText = FindSlotText("EnemySlot2");

        if (firstCardText == null)
        {
            Debug.LogError("[CheatGlove] 找不到 EnemySlot1 的文本，Overlay 无法生效");
            yield break;
        }

        if (secondCardText == null)
        {
            Debug.LogError("[CheatGlove] 找不到 EnemySlot2 的文本，Overlay 无法生效");
            yield break;
        }

        // 我们比 PokerUI 晚订阅，所以每次事件都会在它之后执行 → 覆盖生效
        poker.OnStateChanged += OnStateChanged;
        subscribed = true;

        OnStateChanged(); // 立即刷新一次，防止刚进入场景时敌人手牌已经显示

        Debug.Log("[CheatGlove] Overlay 已订阅");
    }

    private void OnDisable()
    {
        if (subscribed && poker != null)
        {
            poker.OnStateChanged -= OnStateChanged;
            subscribed = false;
        }
    }

    private void OnStateChanged()
    {
        if (firstCardText == null || secondCardText == null)
            return;

        // 没有物品就什么都不做
        if (PlayerInventory.Instance == null || !PlayerInventory.Instance.Has(ItemId.CheatGlove))
            return;

        // 摊牌 / 牌局结束时，PokerUI 已经显示完整手牌，不覆盖
        bool revealEnemy =
            poker.State == PokerState.Showdown
            || poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;
        if (revealEnemy)
            return;

        if (poker.EnemyHand == null || poker.EnemyHand.Count == 0)
            return;

        // 只显示第一张，第二张保持隐藏
        firstCardText.text = CardToColored(poker.EnemyHand[0]);
        secondCardText.text = "?";
    }

    /// <summary>从 PokerCanvas 下按名字找卡牌槽位里的文字（新 UI 是槽位结构）。</summary>
    private Text FindSlotText(string slotName)
    {
        Transform uiRoot = transform.Find("PokerCanvas");
        if (uiRoot == null)
        {
            var uiGO = GameObject.Find("PokerCanvas");
            if (uiGO != null)
                uiRoot = uiGO.transform;
        }
        if (uiRoot == null)
            return null;

        Transform slot = uiRoot.Find(slotName);
        if (slot == null)
            return null;

        Transform face = slot.Find("Face");
        return face != null ? face.GetComponent<Text>() : slot.GetComponent<Text>();
    }

    /// <summary>把单张牌转成带 <color> 的富文本字符串。</summary>
    private string CardToColored(Card c)
    {
        string hex;
        switch (c.Suit)
        {
            case Suit.Spades:
                hex = "#EDEDED";
                break;
            case Suit.Hearts:
                hex = "#FF8C94";
                break;
            case Suit.Clubs:
                hex = "#A8E0FF";
                break;
            case Suit.Diamonds:
                hex = "#FFD973";
                break;
            default:
                hex = "#FFFFFF";
                break;
        }
        return $"<color={hex}>{c}</color>";
    }
}
