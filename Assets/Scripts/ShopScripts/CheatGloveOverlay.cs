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

    private Text enemyHandText;
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

        // 找 EnemyHand 文本
        // PokerUI 结构：PokerUI/PokerCanvas/EnemyHand
        Transform t = transform.Find("PokerCanvas/EnemyHand");
        if (t == null)
        {
            // 如果 CheatGloveOverlay 挂的位置不同，尝试从场景里找
            var uiGO = GameObject.Find("PokerCanvas");
            if (uiGO != null)
                t = uiGO.transform.Find("EnemyHand");
        }

        if (t == null)
        {
            Debug.LogError("[CheatGlove] 找不到 EnemyHand 文本，Overlay 无法生效");
            yield break;
        }

        enemyHandText = t.GetComponent<Text>();
        if (enemyHandText == null)
        {
            Debug.LogError("[CheatGlove] EnemyHand 上没有 Text 组件");
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
        if (enemyHandText == null)
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

        // 只显示第一张，其余隐藏
        string first = CardToColored(poker.EnemyHand[0]);
        int remaining = poker.EnemyHand.Count - 1;
        string hidden = string.Join("   ", new string[remaining + 1]).Trim();
        // 简单处理：每个隐藏位置写 "??"
        string hiddenStr = "";
        for (int i = 0; i < remaining; i++)
            hiddenStr += "??  ";

        enemyHandText.text = $"{first}   {hiddenStr}".TrimEnd();
    }

    /// <summary>把单张牌转成带 <color> 的富文本字符串。</summary>
    private string CardToColored(Card c)
    {
        string hex;
        switch (c.Suit)
        {
            case Suit.Spades:
                hex = "#A0A0A0";
                break;
            case Suit.Hearts:
                hex = "#FF8080";
                break;
            case Suit.Clubs:
                hex = "#90E0FF";
                break;
            case Suit.Diamonds:
                hex = "#FFD060";
                break;
            default:
                hex = "#FFFFFF";
                break;
        }
        return $"<color={hex}>{c}</color>";
    }
}
