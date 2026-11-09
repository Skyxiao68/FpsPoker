using System.Collections;
using UnityEngine;

/// <summary>
/// 窥牌工具（老千手套 = 看 1 张，冷读 = 看 2 张）：让 PokerUI 在牌局进行中
/// 把敌人的前 N 张牌正面画出来。
///
/// 旧实现是往 EnemySlot1/Face 的 Text 里写牌，但卡面换成 Art 图片之后，
/// 有图时 PokerUI 会把 Text 清空（PokerUI.cs 的 RenderSlots），所以那套写法
/// 表面上没报错、实际什么都不显示。现在改成只改 PokerUI 的「揭示张数」，
/// 由 PokerUI 自己用卡面图和暖色描边绘制，逻辑不再有两份。
///
/// 张数不在这里写死：查 <see cref="PokerItemTraits.OwnedRevealCount"/>，
/// 以后加第三件窥牌道具只改那张表。
/// </summary>
public class CheatGloveOverlay : MonoBehaviour
{
    [SerializeField]
    private PokerGameManager poker;

    private PokerUI ui;
    private bool subscribed;

    private void Start()
    {
        if (poker == null)
            poker = FindAnyObjectByType<PokerGameManager>();

        ui = FindAnyObjectByType<PokerUI>();

        if (poker == null || ui == null)
        {
            Debug.LogError("[CheatGlove] 找不到 PokerGameManager 或 PokerUI，Overlay 不生效");
            return;
        }

        // 等两帧：一帧给 PokerUI 建 UI，一帧给它先订阅 OnStateChanged
        StartCoroutine(SubscribeNextFrame());
    }

    private IEnumerator SubscribeNextFrame()
    {
        yield return null;
        yield return null;

        poker.OnStateChanged += OnStateChanged;
        subscribed = true;

        OnStateChanged(); // 进场景时敌人手牌可能已经发好了
        Debug.Log("[CheatGlove] Overlay 已订阅");
    }

    private void OnDisable()
    {
        if (subscribed && poker != null)
        {
            poker.OnStateChanged -= OnStateChanged;
            subscribed = false;
        }

        if (ui != null)
            ui.SetEnemyReveal(0);
    }

    private void OnStateChanged()
    {
        lastRevealCount = PokerItemTraits.OwnedRevealCount();
        ApplyReveal();
    }

    private int lastRevealCount = -1;

    /// <summary>
    /// Poker 的状态事件只在出牌时触发，但道具可能是在一手牌中途才拿到的
    /// （商店、debug 发道具）。这里低频轮询变化，避免「买了却要等下一手」。
    /// </summary>
    private void Update()
    {
        int now = PokerItemTraits.OwnedRevealCount();
        if (now == lastRevealCount)
            return;

        lastRevealCount = now;
        ApplyReveal();
    }

    private void ApplyReveal()
    {
        if (ui == null || poker == null)
            return;

        // 摊牌 / 牌局结束后 PokerUI 本来就显示全部手牌，这时把揭示张数归零，
        // 免得「窥的那张」和「摊牌亮出的牌」两套高亮打架。
        bool handOver =
            poker.State == PokerState.Showdown
            || poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;

        bool dealing = poker.EnemyHand == null || poker.EnemyHand.Count == 0;
        int reveal = handOver || dealing ? 0 : lastRevealCount;

        ui.SetEnemyReveal(reveal);
    }
}
