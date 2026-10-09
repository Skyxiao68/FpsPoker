using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PokerUI : MonoBehaviour
{
    private PokerGameManager poker;

    // Text fields.
    private Text potText;
    private Text playerChipsText;
    private Text enemyChipsText;
    private Text stateText;
    private Text playerHandText;
    private Text enemyHandText;
    private Text communityText;
    private Text playerHandTypeText;
    private Text enemyHandTypeText;
    private Transform messageLogContainer;
    private const int MaxLogEntries = 6;
    private Text enemyNameText;
    private Text raiseValueText;

    // Buttons.
    private Button checkButton;
    private Button raiseButton;
    private Button foldButton;
    private Button matchButton;
    private Button nextHandButton;

    // Slider.
    private Slider raiseSlider;

    // Containers.
    private GameObject quickBetRow;
    private GameObject actionRow;
    private GameObject nextHandRow;

    // Callback.
    private System.Action onNextHand;

    // Constants.
    private const int MinRaise = 1;
    private const int StepAmount = 10;
    private const int DefaultRaise = 10;

    // Tracking for slider reset on street change.
    private Street lastStreet = Street.PreFlop;
    private bool hasInitialized = false;

    private Button swapButton;
    private GameObject swapChoiceRow;
    private Transform canvasRoot;
    private Button swapCard1Button;
    private Button swapCard2Button;

    public void Initialize(PokerGameManager manager)
    {
        poker = manager;
        BuildUI();

        poker.OnStateChanged += RefreshUI;

        // 修复这里：直接订阅 AddMessage
        poker.OnMessage += AddMessage;

        poker.OnHandEnded += RefreshUI;

        RefreshUI();
    }

    public void SetNextHandCallback(System.Action callback)
    {
        onNextHand = callback;
    }

    // =========================================================
    // BUILD UI
    // =========================================================
    private void BuildUI()
    {
        // ---------- Canvas ----------
        GameObject canvasGO = new GameObject("PokerCanvas");
        canvasRoot = canvasGO.transform;
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGO.transform.SetParent(transform);

        // 毛毡背景
        CreatePanel(canvasGO.transform, new Color(0.10f, 0.16f, 0.10f), Vector2.zero, Vector2.one);

        // ---------- 顶栏：底池居中 ----------
        CreatePanel(
            canvasGO.transform,
            new Color(0, 0, 0, 0.55f),
            new Vector2(0f, 0.93f),
            new Vector2(1f, 1f)
        );

        potText = CreateText(
            canvasGO.transform,
            "PotText",
            "Pot: 0",
            new Vector2(0.35f, 0.93f),
            new Vector2(0.65f, 1f),
            TextAnchor.MiddleCenter,
            32
        );

        // ---------- 敌人信息：左上角 ----------
        CreatePanel(
            canvasGO.transform,
            new Color(0, 0, 0, 0.42f),
            new Vector2(0.02f, 0.72f),
            new Vector2(0.28f, 0.91f)
        );

        enemyNameText = CreateText(
            canvasGO.transform,
            "EnemyNameText",
            "",
            new Vector2(0.04f, 0.86f),
            new Vector2(0.26f, 0.90f),
            TextAnchor.MiddleLeft,
            24
        );

        enemyChipsText = CreateText(
            canvasGO.transform,
            "EnemyChipsText",
            "Enemy: 0",
            new Vector2(0.04f, 0.82f),
            new Vector2(0.26f, 0.86f),
            TextAnchor.MiddleLeft,
            20
        );
        enemyChipsText.color = new Color(0.85f, 0.85f, 0.85f);

        Text enemyLabel = CreateText(
            canvasGO.transform,
            "EnemyLabel",
            "Enemy Hand",
            new Vector2(0.04f, 0.78f),
            new Vector2(0.26f, 0.82f),
            TextAnchor.MiddleLeft,
            14
        );
        enemyLabel.color = new Color(0.6f, 0.6f, 0.6f);

        enemyHandText = CreateText(
            canvasGO.transform,
            "EnemyHand",
            "?? ??",
            new Vector2(0.04f, 0.73f),
            new Vector2(0.26f, 0.79f),
            TextAnchor.MiddleLeft,
            32
        );

        enemyHandTypeText = CreateText(
            canvasGO.transform,
            "EnemyHandType",
            "",
            new Vector2(0.04f, 0.70f),
            new Vector2(0.26f, 0.73f),
            TextAnchor.MiddleLeft,
            16
        );
        enemyHandTypeText.color = new Color(0.8f, 0.8f, 0.8f);

        // ---------- 日志：左下角 ----------
        CreatePanel(
            canvasGO.transform,
            new Color(0, 0, 0, 0.45f),
            new Vector2(0.02f, 0.02f),
            new Vector2(0.30f, 0.26f)
        );

        GameObject msgLogGO = new GameObject("MessageLog");
        msgLogGO.transform.SetParent(canvasGO.transform, false);
        RectTransform msgLogRT = msgLogGO.AddComponent<RectTransform>();
        msgLogRT.anchorMin = new Vector2(0.03f, 0.03f);
        msgLogRT.anchorMax = new Vector2(0.29f, 0.25f);
        msgLogRT.offsetMin = Vector2.zero;
        msgLogRT.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = msgLogGO.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft; // 从底部往上堆
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 4f;

        ContentSizeFitter csf = msgLogGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        messageLogContainer = msgLogGO.transform;

        // ---------- 公共牌：正中央 ----------
        Text communityLabel = CreateText(
            canvasGO.transform,
            "CommunityLabel",
            "Community Cards",
            new Vector2(0.35f, 0.66f),
            new Vector2(0.65f, 0.70f),
            TextAnchor.MiddleCenter,
            16
        );
        communityLabel.color = new Color(0.6f, 0.6f, 0.6f);

        communityText = CreateText(
            canvasGO.transform,
            "Community",
            "",
            new Vector2(0.15f, 0.53f),
            new Vector2(0.85f, 0.65f),
            TextAnchor.MiddleCenter,
            52
        );

        // ---------- 玩家信息：右下角 ----------
        // 玩家信息面板背景
        CreatePanel(
            canvasGO.transform,
            new Color(0, 0, 0, 0.42f),
            new Vector2(0.72f, 0.19f),
            new Vector2(0.98f, 0.37f)
        );

        // 玩家筹码
        playerChipsText = CreateText(
            canvasGO.transform,
            "PlayerChipsText",
            "Player: 0",
            new Vector2(0.74f, 0.32f),
            new Vector2(0.96f, 0.36f),
            TextAnchor.MiddleLeft,
            20
        );
        playerChipsText.color = new Color(0.85f, 0.85f, 0.85f);

        // Player Hand 标签
        Text playerLabel = CreateText(
            canvasGO.transform,
            "PlayerLabel",
            "Player Hand",
            new Vector2(0.74f, 0.29f),
            new Vector2(0.96f, 0.32f),
            TextAnchor.MiddleLeft,
            14
        );
        playerLabel.color = new Color(0.6f, 0.6f, 0.6f);

        // 玩家手牌
        playerHandText = CreateText(
            canvasGO.transform,
            "PlayerHand",
            "",
            new Vector2(0.74f, 0.23f),
            new Vector2(0.96f, 0.29f),
            TextAnchor.MiddleLeft,
            36
        );

        // 玩家牌型
        playerHandTypeText = CreateText(
            canvasGO.transform,
            "PlayerHandType",
            "",
            new Vector2(0.74f, 0.19f),
            new Vector2(0.96f, 0.23f),
            TextAnchor.MiddleLeft,
            16
        );
        playerHandTypeText.color = new Color(0.8f, 0.8f, 0.8f);

        // ---- Swap 按钮：放在玩家面板上方 ----
        swapButton = CreateButton(
            canvasGO.transform,
            "SwapBtn",
            "Swap",
            new Vector2(0.72f, 0.38f),
            new Vector2(0.98f, 0.43f),
            new Color(0.45f, 0.30f, 0.65f),
            () => OnSwapClicked()
        );

        // ---------- 状态栏 ----------
        stateText = CreateText(
            canvasGO.transform,
            "StateText",
            "",
            new Vector2(0.02f, 0.27f),
            new Vector2(0.30f, 0.31f),
            TextAnchor.MiddleLeft,
            14
        );
        stateText.color = new Color(0.6f, 0.6f, 0.6f);

        // ---------- 快捷下注行（底部中段） ----------
        quickBetRow = new GameObject("QuickBetRow");
        quickBetRow.transform.SetParent(canvasGO.transform, false);
        RectTransform qbrRT = quickBetRow.AddComponent<RectTransform>();
        qbrRT.anchorMin = Vector2.zero;
        qbrRT.anchorMax = Vector2.one;
        qbrRT.offsetMin = Vector2.zero;
        qbrRT.offsetMax = Vector2.zero;

        float qbY0 = 0.11f,
            qbY1 = 0.17f;

        // 四个百分比按钮
        CreateButton(
            quickBetRow.transform,
            "Btn33",
            "33%",
            new Vector2(0.33f, qbY0),
            new Vector2(0.40f, qbY1),
            new Color(0.15f, 0.35f, 0.65f),
            () => SetSliderToFraction(0.33f)
        );

        CreateButton(
            quickBetRow.transform,
            "Btn50",
            "50%",
            new Vector2(0.41f, qbY0),
            new Vector2(0.48f, qbY1),
            new Color(0.15f, 0.35f, 0.65f),
            () => SetSliderToFraction(0.50f)
        );

        CreateButton(
            quickBetRow.transform,
            "Btn75",
            "75%",
            new Vector2(0.49f, qbY0),
            new Vector2(0.56f, qbY1),
            new Color(0.15f, 0.35f, 0.65f),
            () => SetSliderToFraction(0.75f)
        );

        CreateButton(
            quickBetRow.transform,
            "BtnMax",
            "Max",
            new Vector2(0.57f, qbY0),
            new Vector2(0.64f, qbY1),
            new Color(0.15f, 0.35f, 0.65f),
            () => SetSliderToFraction(1.0f)
        );

        // 减号
        CreateButton(
            quickBetRow.transform,
            "BtnMinus",
            "-",
            new Vector2(0.65f, qbY0),
            new Vector2(0.69f, qbY1),
            new Color(0.25f, 0.25f, 0.25f),
            () => AdjustSlider(-StepAmount)
        );

        // 滑块
        raiseSlider = CreateSlider(
            quickBetRow.transform,
            "RaiseSlider",
            new Vector2(0.70f, qbY0),
            new Vector2(0.87f, qbY1),
            MinRaise,
            100,
            DefaultRaise
        );

        // 加号
        CreateButton(
            quickBetRow.transform,
            "BtnPlus",
            "+",
            new Vector2(0.88f, qbY0),
            new Vector2(0.92f, qbY1),
            new Color(0.25f, 0.25f, 0.25f),
            () => AdjustSlider(StepAmount)
        );

        // 数值
        raiseValueText = CreateText(
            quickBetRow.transform,
            "RaiseValueText",
            "10",
            new Vector2(0.93f, qbY0),
            new Vector2(0.96f, qbY1),
            TextAnchor.MiddleCenter,
            22
        );

        raiseSlider.onValueChanged.AddListener(
            (value) => raiseValueText.text = Mathf.RoundToInt(value).ToString()
        );

        // ---------- 操作按钮行（底部） ----------
        actionRow = new GameObject("ActionRow");
        actionRow.transform.SetParent(canvasGO.transform, false);
        RectTransform arRT = actionRow.AddComponent<RectTransform>();
        arRT.anchorMin = Vector2.zero;
        arRT.anchorMax = Vector2.one;
        arRT.offsetMin = Vector2.zero;
        arRT.offsetMax = Vector2.zero;

        float aY0 = 0.02f,
            aY1 = 0.09f;

        foldButton = CreateButton(
            actionRow.transform,
            "FoldBtn",
            "Fold",
            new Vector2(0.33f, aY0),
            new Vector2(0.48f, aY1),
            new Color(0.72f, 0.22f, 0.17f),
            () => poker.PlayerFold()
        );

        checkButton = CreateButton(
            actionRow.transform,
            "CheckBtn",
            "Check",
            new Vector2(0.49f, aY0),
            new Vector2(0.64f, aY1),
            new Color(0.12f, 0.54f, 0.31f),
            () => poker.PlayerCheck()
        );

        raiseButton = CreateButton(
            actionRow.transform,
            "RaiseBtn",
            "Raise",
            new Vector2(0.65f, aY0),
            new Vector2(0.80f, aY1),
            new Color(0.85f, 0.55f, 0.11f),
            () => poker.PlayerRaise(Mathf.RoundToInt(raiseSlider.value))
        );

        matchButton = CreateButton(
            actionRow.transform,
            "MatchBtn",
            "Match",
            new Vector2(0.81f, aY0),
            new Vector2(0.96f, aY1),
            new Color(0.2f, 0.45f, 0.7f),
            () => poker.PlayerMatchRaise()
        );

        // ---------- Next / Enter Combat 行 ----------
        nextHandRow = new GameObject("NextHandRow");
        nextHandRow.transform.SetParent(canvasGO.transform, false);
        RectTransform nhrRT = nextHandRow.AddComponent<RectTransform>();
        nhrRT.anchorMin = Vector2.zero;
        nhrRT.anchorMax = Vector2.one;
        nhrRT.offsetMin = Vector2.zero;
        nhrRT.offsetMax = Vector2.zero;

        nextHandButton = CreateButton(
            nextHandRow.transform,
            "NextHandBtn",
            "Next Hand",
            new Vector2(0.38f, 0.02f),
            new Vector2(0.62f, 0.09f),
            new Color(0.2f, 0.6f, 0.35f),
            () => onNextHand?.Invoke()
        );

        nextHandRow.SetActive(false);
    }

    // =========================================================
    // REFRESH
    // =========================================================
    private void RefreshUI()
    {
        if (poker == null)
            return;

        potText.text = $"Pot: {poker.Pot}";
        playerChipsText.text = $"Player: {poker.PlayerChips}";
        enemyChipsText.text = $"Enemy: {poker.EnemyChips}";

        if (poker.EnemyParams != null && enemyNameText != null)
            enemyNameText.text = poker.EnemyParams.enemyName;

        string dealingStr = poker.IsDealing ? " (Dealing...)" : "";
        stateText.text =
            $"{poker.CurrentStreet} | {poker.State}{dealingStr} | Bet: {poker.CurrentBet} | Community: {poker.CommunityCards.Count}/5";

        playerHandText.text = CardStrColored(poker.PlayerHand);
        communityText.text = CardStrColored(poker.CommunityCards);

        bool revealEnemy =
            poker.State == PokerState.Showdown
            || poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;

        enemyHandText.text = revealEnemy ? CardStrColored(poker.EnemyHand) : "?? ??";

        playerHandTypeText.text =
            poker.PlayerBestHand != null ? $"{poker.PlayerBestHand.GetDisplayName()}" : "";
        enemyHandTypeText.text =
            revealEnemy && poker.EnemyBestHand != null
                ? $"{poker.EnemyBestHand.GetDisplayName()}"
                : "";

        bool isPlayerTurn = poker.State == PokerState.PlayerTurn && !poker.IsDealing;
        bool mustMatch = poker.AwaitingPlayerMatch;
        int maxRaise = poker.GetPlayerMaxRaise();
        bool canRaise = maxRaise >= MinRaise;

        checkButton.interactable = isPlayerTurn && !mustMatch;
        raiseButton.interactable = isPlayerTurn && !mustMatch && canRaise;
        matchButton.interactable = isPlayerTurn && mustMatch;
        foldButton.interactable = isPlayerTurn;

        // Swap 按钮：只有在可以换牌时才亮
        if (swapButton != null)
        {
            swapButton.interactable = poker.CanSwapCard;
        }

        if (swapChoiceRow != null && swapChoiceRow.activeSelf && !poker.CanSwapCard)
        {
            swapChoiceRow.SetActive(false);
        }

        bool handOver =
            poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;

        quickBetRow.SetActive(!handOver && canRaise);
        actionRow.SetActive(!handOver);
        nextHandRow.SetActive(handOver);

        int sliderMax = Mathf.Max(MinRaise, maxRaise);
        raiseSlider.minValue = MinRaise;
        raiseSlider.maxValue = sliderMax;
        raiseSlider.interactable = canRaise && isPlayerTurn && !mustMatch;

        if (!hasInitialized || poker.CurrentStreet != lastStreet)
        {
            lastStreet = poker.CurrentStreet;
            hasInitialized = true;
            float resetValue = Mathf.Clamp(DefaultRaise, MinRaise, sliderMax);
            raiseSlider.value = resetValue;
        }
        if (raiseSlider.value > sliderMax)
            raiseSlider.value = sliderMax;
        raiseValueText.text = canRaise ? Mathf.RoundToInt(raiseSlider.value).ToString() : "-";

        SetQuickBetButtonsInteractable(canRaise && isPlayerTurn && !mustMatch);
    }

    private string CardStrColored(List<Card> cards)
    {
        if (cards == null || cards.Count == 0)
            return "";
        string s = "";
        foreach (Card c in cards)
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
            s += $"<color={hex}>{c}</color>  ";
        }
        return s;
    }

    private void AddMessage(string msg)
    {
        // 1. 创建新的文本对象
        GameObject textGO = new GameObject("Msg");
        textGO.transform.SetParent(messageLogContainer, false);

        // 2. 设置为第一个子物体（新消息出现在顶部，旧消息往下排）
        textGO.transform.SetAsLastSibling();

        // 3. 设置文本属性
        Text txt = textGO.AddComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.text = msg;
        txt.fontSize = 18;
        txt.color = new Color(0.9f, 0.9f, 0.9f, 1f); // 稍微偏灰的白，不刺眼
        txt.alignment = TextAnchor.MiddleLeft;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;

        // 4. 如果超过最大条数，销毁最旧的一条（末尾的）
        if (messageLogContainer.childCount > MaxLogEntries)
        {
            Transform oldest = messageLogContainer.GetChild(0);
            oldest.SetParent(null); // 先脱离父物体，避免在销毁时触发布局重建
            Destroy(oldest.gameObject);
        }

        // 5. 强制刷新布局，防止 UI 延迟或重叠
        LayoutRebuilder.ForceRebuildLayoutImmediate(
            messageLogContainer.GetComponent<RectTransform>()
        );

        StartCoroutine(FadeOutAndDestroy(textGO, 9f)); // 9秒后开始淡出
    }

    private System.Collections.IEnumerator FadeOutAndDestroy(GameObject go, float delay)
    {
        // 等待显示时间
        yield return new WaitForSeconds(delay);

        if (go == null)
        {
            yield break;
        }

        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = go.AddComponent<CanvasGroup>();

        float fadeDuration = 2f;
        float t = 0f;

        while (t < fadeDuration)
        {
            if (go == null || cg == null)
            {
                yield break;
            }

            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            yield return null;
        }

        Destroy(go);
    }

    private void SetQuickBetButtonsInteractable(bool on)
    {
        if (quickBetRow == null)
            return;
        foreach (Button b in quickBetRow.GetComponentsInChildren<Button>(true))
        {
            // 滑块的加减按钮也在这里，
            b.interactable = on;
        }
    }

    // =========================================================
    // SLIDER HELPERS
    // =========================================================
    private void SetSliderToFraction(float fraction)
    {
        float target = Mathf.Lerp(raiseSlider.minValue, raiseSlider.maxValue, fraction);
        raiseSlider.value = Mathf.Round(target);
    }

    private void AdjustSlider(int delta)
    {
        float target = raiseSlider.value + delta;
        raiseSlider.value = Mathf.Clamp(target, raiseSlider.minValue, raiseSlider.maxValue);
    }

    /// <summary>
    /// 点击 Swap 按钮：显示换牌选择弹窗，让玩家选换哪张。
    /// </summary>
    private void OnSwapClicked()
    {
        Debug.Log(
            $"[Swap] 按钮被点击。CanSwapCard={poker.CanSwapCard}, "
                + $"SwapsRemaining={poker.SwapsRemaining}"
        );

        if (swapChoiceRow == null)
            BuildSwapChoiceRow();

        // 已经显示 → 关闭
        if (swapChoiceRow.activeSelf)
        {
            swapChoiceRow.SetActive(false);
            return;
        }

        RefreshSwapChoiceContent();
        swapChoiceRow.SetActive(true);
    }

    private void BuildSwapChoiceRow()
    {
        swapChoiceRow = new GameObject("SwapChoiceRow");
        swapChoiceRow.transform.SetParent(swapButton.transform.parent, false);
        RectTransform rt = swapChoiceRow.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // ---- 全屏暗色遮罩（点击外部也不关闭，但视觉上聚焦弹窗） ----
        CreatePanel(
            swapChoiceRow.transform,
            new Color(0f, 0f, 0f, 0.55f),
            Vector2.zero,
            Vector2.one
        );

        // ---- 中央卡片 ----
        CreatePanel(
            swapChoiceRow.transform,
            new Color(0.12f, 0.16f, 0.14f, 0.98f),
            new Vector2(0.28f, 0.35f),
            new Vector2(0.72f, 0.65f)
        );

        // ---- 标题 ----
        CreateText(
            swapChoiceRow.transform,
            "SwapTitle",
            "Swap which one？",
            new Vector2(0.28f, 0.58f),
            new Vector2(0.72f, 0.64f),
            TextAnchor.MiddleCenter,
            26
        );

        // ---- 提示 ----
        CreateText(
            swapChoiceRow.transform,
            "SwapHint",
            "Discard 1 hand card and draw a new one from the deck",
            new Vector2(0.28f, 0.53f),
            new Vector2(0.72f, 0.58f),
            TextAnchor.MiddleCenter,
            16
        ).color = new Color(0.65f, 0.65f, 0.65f);

        // ---- 卡片 1 ----
        swapCard1Button = CreateButton(
            swapChoiceRow.transform,
            "SwapCard1",
            "—",
            new Vector2(0.31f, 0.41f),
            new Vector2(0.49f, 0.51f),
            new Color(0.45f, 0.30f, 0.65f),
            () =>
            {
                poker.PlayerSwapCard(0);
                swapChoiceRow.SetActive(false);
            }
        );

        // ---- 卡片 2 ----
        swapCard2Button = CreateButton(
            swapChoiceRow.transform,
            "SwapCard2",
            "—",
            new Vector2(0.51f, 0.41f),
            new Vector2(0.69f, 0.51f),
            new Color(0.45f, 0.30f, 0.65f),
            () =>
            {
                poker.PlayerSwapCard(1);
                swapChoiceRow.SetActive(false);
            }
        );

        // ---- 取消按钮 ----
        CreateButton(
            swapChoiceRow.transform,
            "SwapCancel",
            "Cancel",
            new Vector2(0.42f, 0.36f),
            new Vector2(0.58f, 0.40f),
            new Color(0.25f, 0.25f, 0.25f),
            () => swapChoiceRow.SetActive(false)
        );

        swapChoiceRow.SetActive(false);
    }

    /// <summary>每次打开时把当前手牌信息填进两个按钮的 label</summary>
    private void RefreshSwapChoiceContent()
    {
        if (swapCard1Button == null || swapCard2Button == null)
            return;

        SetButtonLabel(swapCard1Button, LabelForHandIndex(0));
        SetButtonLabel(swapCard2Button, LabelForHandIndex(1));
    }

    private string LabelForHandIndex(int i)
    {
        var hand = poker.PlayerHand;
        if (hand == null || i >= hand.Count)
            return "—";
        var c = hand[i];
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
        return $"Swap <color={hex}>{c}</color>";
    }

    private void SetButtonLabel(Button btn, string text)
    {
        var labelT = btn.transform.Find("Label");
        if (labelT == null)
            return;
        var txt = labelT.GetComponent<Text>();
        if (txt != null)
            txt.text = text;
    }

    // =========================================================
    // UI BUILDERS
    // =========================================================
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
        cb.highlightedColor = Brighten(normalColor, 1.25f);
        cb.pressedColor = Darken(normalColor, 0.7f);
        cb.selectedColor = normalColor;
        cb.disabledColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.1f;
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
            22
        );
        txt.raycastTarget = false;

        return btn;
    }

    private Slider CreateSlider(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float minValue,
        float maxValue,
        float initialValue
    )
    {
        GameObject sliderGO = new GameObject(name);
        sliderGO.transform.SetParent(parent, false);

        RectTransform sliderRT = sliderGO.AddComponent<RectTransform>();
        sliderRT.anchorMin = anchorMin;
        sliderRT.anchorMax = anchorMax;
        sliderRT.offsetMin = Vector2.zero;
        sliderRT.offsetMax = Vector2.zero;

        Slider slider = sliderGO.AddComponent<Slider>();

        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(sliderGO.transform, false);
        Image bgImg = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.15f, 0.15f);
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0.4f);
        bgRT.anchorMax = new Vector2(1f, 0.6f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;

        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0f, 0.4f);
        fillAreaRT.anchorMax = new Vector2(1f, 0.6f);
        fillAreaRT.offsetMin = new Vector2(8f, 0f);
        fillAreaRT.offsetMax = new Vector2(-8f, 0f);

        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        Image fillImg = fillGO.AddComponent<Image>();
        fillImg.color = new Color(0.35f, 0.6f, 0.45f);
        RectTransform fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;

        GameObject handleAreaGO = new GameObject("Handle Slide Area");
        handleAreaGO.transform.SetParent(sliderGO.transform, false);
        RectTransform handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = new Vector2(8f, 0f);
        handleAreaRT.offsetMax = new Vector2(-8f, 0f);

        GameObject handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(handleAreaGO.transform, false);
        Image handleImg = handleGO.AddComponent<Image>();
        handleImg.color = new Color(0.95f, 0.95f, 0.95f);
        RectTransform handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(24f, 0f);

        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = Mathf.Clamp(initialValue, minValue, maxValue);

        return slider;
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

    // =========================================================
    // UTILS
    // =========================================================
    private Color Brighten(Color c, float factor)
    {
        return new Color(
            Mathf.Clamp01(c.r * factor),
            Mathf.Clamp01(c.g * factor),
            Mathf.Clamp01(c.b * factor),
            c.a
        );
    }

    private Color Darken(Color c, float factor)
    {
        return new Color(c.r * factor, c.g * factor, c.b * factor, c.a);
    }

    private string CardStr(List<Card> cards)
    {
        if (cards == null || cards.Count == 0)
            return "";
        string s = "";
        foreach (Card c in cards)
            s += c.ToString() + "  ";
        return s;
    }
}
