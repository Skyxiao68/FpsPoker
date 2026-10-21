using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 教程导演：站在 TutorialScene 的牌桌上面，按 TutorialLessons 的五课剧本
/// 发牌、只放行该点的按钮、摆提示卡、处理 Skip / FINISH。
///
/// 它只在 TutorialScene 里存在；Poker 场景（Card）里没有任何教程层。
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    // 压在所有牌桌 UI 之上（PokerUI 100、? 面板 300）。
    private const int CanvasOrder = 320;

    private static readonly Color AMBER = new Color(1f, 0.78f, 0.30f, 1f);
    private static readonly Color CARD_BG = new Color(0.09f, 0.13f, 0.11f, 1f);
    private static readonly Color TEXT = new Color(0.93f, 0.95f, 0.92f, 1f);
    private static readonly Color TEXT_DIM = new Color(0.62f, 0.66f, 0.62f, 1f);

    private PokerUI ui;
    private PokerGameManager poker;
    private List<TutorialLesson> lessons;

    private int index = -1;
    private bool started;
    private bool finished;
    private bool waitingForClick;
    private bool autoPlay;
    private float nextAutoAt;

    private Canvas canvas;
    private GameObject dim;
    private Image dimImage;
    private GameObject highlight;
    private RectTransform highlightRect;
    private readonly System.Collections.Generic.List<GameObject> highlightFrames =
        new System.Collections.Generic.List<GameObject>();
    private GameObject callout;
    private Text titleText;
    private Text bodyText;
    private Text progressText;
    private Button nextButton;
    private Text nextLabel;
    private Button skipButton;

    private Button allowedButton;
    private PokerTutorialUI resultLine;
    private readonly List<Button> tableButtons = new List<Button>();
    private readonly List<Color> tableColors = new List<Color>();
    private Slider tableSlider;

    // 五课之后的免费练习桌：3 手，每手筹码重置，不写进度、不进战斗。
    private const int PracticeHands = 3;
    private const int PracticeChips = 200;
    private const int PracticeBuyIn = 10;
    private bool practice;
    private int practiceHand;
    private float nextPracticeAt;
    private bool practiceSettled;

    public int CurrentLesson { get { return index + 1; } }
    public bool IsFinished { get { return finished; } }

    public void Initialize(PokerUI uiRef, PokerGameManager pokerRef)
    {
        ui = uiRef;
        poker = pokerRef;
        lessons = TutorialLessons.Build();

        resultLine = uiRef.GetComponent<PokerTutorialUI>();

        BuildCanvas();
        BuildCallout();

        poker.OnStateChanged += HandleStateChanged;
        poker.OnHandEnded += HandleHandEnded;

        ui.CheckButton.onClick.AddListener(HandleCheckClicked);

        CacheTableControls();
        HideInventoryHud();
        StartCoroutine(StartFirstLesson());
    }

    // 背包 HUD 是 DontDestroyOnLoad 的，会压在我们的提示卡旁边（"ITEMS / None"）。
    // 教程用不到它：进来藏、离开前恢复，别把状态带到后面的牌桌上。
    private const string HudName = "Owned Items HUD";

    private static void SetInventoryHudVisible(bool visible)
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].gameObject.name == HudName)
                canvases[i].gameObject.SetActive(visible);
        }
    }

    private void HideInventoryHud()
    {
        SetInventoryHudVisible(false);
    }

    private IEnumerator StartFirstLesson()
    {
        yield return null;
        StartLesson(0);
    }

    private void CacheTableControls()
    {
        tableButtons.Clear();
        tableColors.Clear();
        Transform root = ui.TableCanvasRoot;
        if (root == null)
            return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            tableButtons.Add(buttons[i]);

            Image image = buttons[i].targetGraphic as Image;
            tableColors.Add(image != null ? image.color : Color.white);
        }

        tableSlider = ui.RaiseSlider;
    }

    // =========================================================
    // 课程推进
    // =========================================================

    private void StartLesson(int i)
    {
        index = i;
        TutorialLesson lesson = lessons[i];

        // 换课时把上一手的结果行收掉，别让它挂在下一课文案下面。
        if (resultLine != null)
            resultLine.ClearResult();

        waitingForClick = false;
        autoPlay = lesson.autoTo != "None";
        nextAutoAt = Time.time + 0.4f;

        allowedButton = lesson.gate == TutorialGate.ClickCheck ? ui.CheckButton : null;
        waitingForClick = lesson.gate == TutorialGate.ClickCheck;

        ShowCallout(lesson.title, lesson.body, BuiltInButtonLabel(lesson));
        SetHighlight(allowedButton);
        SetLessonHighlights(lesson.highlight);

        StartLessonHand(lesson);
        started = true;
    }

    private string BuiltInButtonLabel(TutorialLesson lesson)
    {
        if (lesson.gate == TutorialGate.Finish)
            return "FINISH";
        if (lesson.gate == TutorialGate.ClickCheck)
            return null;

        return "GOT IT";
    }

    private void StartLessonHand(TutorialLesson lesson)
    {
        EnemyAIParameters enemy = new EnemyAIParameters();
        enemy.enemyName = "Training Dummy";
        enemy.raiseTendency = 0f;
        enemy.foldTendency = 0f;
        enemy.aggression = 0f;
        enemy.bluffChance = 0f;

        poker.StartScriptedHand(200, lesson.enemyChips, 10, enemy, lesson.drawOrder);
    }

    private void HandleCheckClicked()
    {
        if (!started || finished || !waitingForClick)
            return;

        waitingForClick = false;
        Advance();
    }

    private void HandleStateChanged()
    {
        if (!started || finished)
            return;

        if (practice)
            return;

        if (index < 0)
            return;

        TutorialLesson lesson = lessons[index];

        // 到了这一课要停的街（autoTo）就停手，把牌桌交给玩家看。
        // 不判这个的话 autoPlay 会一路 check 到摊牌，玩家根本看不到翻牌。
        if (autoPlay && lesson.autoTo == poker.CurrentStreet.ToString())
        {
            autoPlay = false;
        }

        if (lesson.gate == TutorialGate.Showdown && poker.State == PokerState.Showdown)
        {
            autoPlay = false;
            allowedButton = null;
            waitingForClick = false;
            ShowShowdownCallout();
            return;
        }

        if (lesson.gate == TutorialGate.Finish && poker.State == PokerState.HandEnded)
        {
            autoPlay = false;
            allowedButton = null;
        }
    }

    private void HandleHandEnded()
    {
        if (!started || finished)
            return;

        if (practice)
        {
            OnPracticeHandEnded();
            return;
        }

        if (index < 0)
            return;

        autoPlay = false;
    }

    private void Advance()
    {
        if (index >= 0 && lessons[index].gate == TutorialGate.Showdown)
            SettleShowdown();

        if (index + 1 < lessons.Count)
        {
            StartLesson(index + 1);
        }
        else
        {
            StartPractice();
        }
    }

    // =========================================================
    // 免费练习桌：五课学完，自己打 3 手，筹码不进 run
    // =========================================================

    private void StartPractice()
    {
        practice = true;
        practiceHand = 1;
        practiceSettled = false;
        autoPlay = false;
        allowedButton = null;
        waitingForClick = false;

        SetHighlightRectangles(new RectTransform[0]);
        RestoreTableControls();

        // 练习桌是玩家自己打，不该再被压暗。
        if (dimImage != null)
            dimImage.color = new Color(0f, 0f, 0f, 0f);

        ShowPracticeBanner();
        StartPracticeHand();
    }

    /// <summary>练习桌要能点所有按钮：把上课时压暗的那批恢复原样。</summary>
    private void RestoreTableControls()
    {
        if (tableButtons.Count == 0)
            CacheTableControls();

        for (int i = 0; i < tableButtons.Count; i++)
        {
            if (tableButtons[i] == null)
                continue;

            tableButtons[i].interactable = true;

            Image image = tableButtons[i].targetGraphic as Image;
            if (image != null)
                image.color = tableColors[i];
        }

        if (tableSlider != null)
            tableSlider.interactable = true;
    }

    private void ShowPracticeBanner()
    {
        string body = "Chips are free here — win or lose, they reset.\n"
                    + "Play " + PracticeHands + " hands, then the real run starts.\n"
                    + "Hand " + practiceHand + " of " + PracticeHands;
        ShowCallout("Practice table", body, null);
        progressText.text = "";
    }

    private void StartPracticeHand()
    {
        practiceSettled = false;

        EnemyAIParameters enemy = new EnemyAIParameters();
        enemy.enemyName = "Training Dummy";
        enemy.raiseTendency = 0.35f;
        enemy.foldTendency = 0.15f;
        enemy.aggression = 0.4f;
        enemy.bluffChance = 0.1f;

        poker.StartNewHand(PracticeChips, PracticeChips, PracticeBuyIn, enemy);
    }

    private void OnPracticeHandEnded()
    {
        if (practiceSettled)
            return;

        practiceSettled = true;

        // 摊牌的结果要手动结算；弃牌那两种牌桌自己已经把底池划走了。
        if (poker.Result == PokerResult.PlayerWinsShowdown)
            poker.CreditPotToPlayer();
        else if (poker.Result == PokerResult.EnemyWinsShowdown)
            poker.ForfeitPot();

        nextPracticeAt = Time.time + 1.4f;
    }

    private void SettleShowdown()
    {
        if (poker.Result == PokerResult.PlayerWinsShowdown)
            poker.CreditPotToPlayer();
        else if (poker.Result == PokerResult.EnemyWinsShowdown)
            poker.ForfeitPot();
    }

    private void Complete()
    {
        if (finished)
            return;

        finished = true;
        TutorialProgress.MarkDone();
        SetInventoryHudVisible(true);

        if (GameFlowManager.Instance != null)
        {
            GameFlowManager.Instance.StartNewRun();
            SceneFader.LoadScene(GameFlowManager.Instance.PokerSceneName);
        }
        else
        {
            SceneFader.LoadScene("Card");
        }
    }

    private void Skip()
    {
        Complete();
    }

    // =========================================================
    // 每帧：自动打牌 + 只放行该点的按钮
    // =========================================================

    private void Update()
    {
        if (!started || finished)
            return;

        if (practice)
        {
            if (nextPracticeAt > 0f && Time.time >= nextPracticeAt)
            {
                nextPracticeAt = 0f;
                if (practiceHand < PracticeHands)
                {
                    practiceHand++;
                    ShowPracticeBanner();
                    StartPracticeHand();
                }
                else
                {
                    Complete();
                }
            }
            return;
        }

        if (autoPlay && Time.time >= nextAutoAt && !poker.IsDealing
            && poker.State == PokerState.PlayerTurn)
        {
            nextAutoAt = Time.time + 0.7f;
            if (poker.AwaitingPlayerMatch)
                poker.PlayerMatchRaise();
            else
                poker.PlayerCheck();
            return;
        }

        // 自动推进的课程到了目标街就停下来讲。
        if (autoPlay && lessons[index].autoTo == "Flop" && poker.CurrentStreet == Street.Flop)
        {
            autoPlay = false;
            allowedButton = null;
        }
    }

    private void LateUpdate()
    {
        if (!started || finished)
            return;

        // 练习桌：按钮全放开、结果行留给玩家看（这时候没有课程文案跟它抢位置）。
        if (practice)
            return;

        // 教程里只要我们自己那一块文案：桌面自己的结果行会把行文挤成两层。
        if (resultLine != null && resultLine.HasResult)
            resultLine.ClearResult();

        // 牌桌自己的按钮：只留这一课允许的那个，其余按死 + 压暗，
        // 让"现在能点哪个"一眼看得出来。放在 LateUpdate 覆盖 PokerUI 每帧的重算。
        // PokerUI 的 Canvas 可能比本组件晚一帧才建好，所以没缓存到就再抓一次。
        if (tableButtons.Count == 0)
            CacheTableControls();

        if (tableButtons.Count == 0)
            return;

        for (int i = 0; i < tableButtons.Count; i++)
        {
            Button b = tableButtons[i];
            if (b == null)
                continue;

            bool live = b == allowedButton;
            b.interactable = live;

            Image image = b.targetGraphic as Image;
            if (image != null)
            {
                Color baseColor = tableColors[i];
                image.color = live
                    ? baseColor
                    : new Color(baseColor.r * 0.38f, baseColor.g * 0.38f, baseColor.b * 0.38f, baseColor.a * 0.6f);
            }
        }

        if (tableSlider != null)
            tableSlider.interactable = false;
    }

    // =========================================================
    // UI
    // =========================================================

    private void BuildCanvas()
    {
        GameObject canvasGO = new GameObject("TutorialDirectorCanvas");
        canvasGO.transform.SetParent(transform, false);

        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = CanvasOrder;
        canvas.overrideSorting = true;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        dim = new GameObject("Dim");
        dim.transform.SetParent(canvasGO.transform, false);
        dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.45f);
        dimImage.raycastTarget = false;
        Stretch(dim.GetComponent<RectTransform>());

        highlight = new GameObject("Highlight");
        highlight.transform.SetParent(canvasGO.transform, false);
        highlightRect = highlight.AddComponent<RectTransform>();
        BuildHighlightBars();
        highlight.SetActive(false);
    }

    private void BuildCallout()
    {
        callout = new GameObject("Callout");
        callout.transform.SetParent(canvas.transform, false);
        Image bg = callout.AddComponent<Image>();
        bg.color = CARD_BG;

        RectTransform rt = callout.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.300f, 0.700f);
        rt.anchorMax = new Vector2(0.820f, 0.878f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 琥珀色顶线
        GameObject top = new GameObject("TopLine");
        top.transform.SetParent(callout.transform, false);
        Image topImage = top.AddComponent<Image>();
        topImage.color = AMBER;
        topImage.raycastTarget = false;
        RectTransform topRt = top.GetComponent<RectTransform>();
        topRt.anchorMin = new Vector2(0f, 1f);
        topRt.anchorMax = new Vector2(1f, 1f);
        topRt.pivot = new Vector2(0.5f, 1f);
        topRt.offsetMin = new Vector2(0f, -3f);
        topRt.offsetMax = Vector2.zero;

        titleText = CreateText(callout.transform, "Title", new Vector2(0.04f, 0.66f), new Vector2(0.72f, 0.94f), 18, FontStyle.Bold, AMBER);
        bodyText = CreateText(callout.transform, "Body", new Vector2(0.04f, 0.08f), new Vector2(0.72f, 0.66f), 14, FontStyle.Normal, TEXT);
        bodyText.lineSpacing = 1.05f;

        progressText = CreateText(callout.transform, "Progress", new Vector2(0.72f, 0.78f), new Vector2(0.96f, 0.94f), 13, FontStyle.Normal, TEXT_DIM);
        progressText.alignment = TextAnchor.MiddleRight;

        nextButton = CreateButton(callout.transform, "Next", "GOT IT", new Vector2(0.74f, 0.40f), new Vector2(0.96f, 0.66f), new Color(0.13f, 0.52f, 0.28f), () => Advance());
        nextLabel = nextButton.GetComponentInChildren<Text>();

        skipButton = CreateButton(callout.transform, "Skip", "skip", new Vector2(0.74f, 0.14f), new Vector2(0.96f, 0.34f), new Color(0.16f, 0.18f, 0.16f), Skip);
    }

    private void BuildHighlightBars()
    {
        // 空容器，真正的边框按需要创建（一节课可能同时高亮两张牌或两个筹码数）。
    }

    private GameObject CreateFrame(int slot)
    {
        GameObject frame = new GameObject("Frame" + slot);
        frame.transform.SetParent(highlight.transform, false);

        RectTransform rt = frame.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        CreateBar(frame.transform, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 3f), new Vector2(0f, -3f));
        CreateBar(frame.transform, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 3f), new Vector2(0f, 3f));
        CreateBar(frame.transform, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(3f, 0f), new Vector2(-3f, 0f));
        CreateBar(frame.transform, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(3f, 0f), new Vector2(3f, 0f));
        return frame;
    }

    /// <summary>
    /// 一条边：size 是厚度方向，offset 是往高亮框外侧推的偏移。
    /// </summary>
    private void CreateBar(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 offset)
    {
        GameObject bar = new GameObject(name);
        bar.transform.SetParent(parent, false);
        Image image = bar.AddComponent<Image>();
        image.color = AMBER;
        image.raycastTarget = false;

        RectTransform rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;
    }

    private Text CreateText(Transform parent, string name, Vector2 min, Vector2 max, int size, FontStyle style, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.UpperLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string label, Vector2 min, Vector2 max, Color color, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = color;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;
        if (action != null)
            button.onClick.AddListener(action);

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.fontStyle = FontStyle.Bold;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.text = label;
        text.raycastTarget = false;

        RectTransform textRt = textGO.GetComponent<RectTransform>();
        Stretch(textRt);
        return button;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void ShowCallout(string title, string body, string buttonLabel)
    {
        callout.SetActive(true);
        titleText.text = title;
        bodyText.text = body;

        // 文案长短不一：段落多/单段太长时缩到 13 号，别让最后一行溢出卡片。
        int paragraphs = body.Split('\n').Length;
        int approxLines = paragraphs + body.Length / 62;
        bodyText.fontSize = approxLines > 4 ? 13 : 14;

        progressText.text = (index + 1) + " / " + lessons.Count;

        bool showNext = !string.IsNullOrEmpty(buttonLabel);
        nextButton.gameObject.SetActive(showNext);
        if (showNext && nextLabel != null)
            nextLabel.text = buttonLabel;

        skipButton.gameObject.SetActive(true);
    }

    private void ShowShowdownCallout()
    {
        string mine = poker.PlayerBestHand != null ? poker.PlayerBestHand.Describe() : "?";
        string theirs = poker.EnemyBestHand != null ? poker.EnemyBestHand.Describe() : "?";

        string verdict;
        if (poker.Result == PokerResult.PlayerWinsShowdown)
            verdict = "You win the pot.";
        else if (poker.Result == PokerResult.EnemyWinsShowdown)
            verdict = "They win the pot.";
        else
            verdict = "It is a split.";

        string body = "Your " + mine + " against their " + theirs + ".\n"
                    + verdict + " " + lessons[index].body;

        ShowCallout(lessons[index].title, body, "GOT IT");
        SetHighlight(null);
    }

    /// <summary>按这一课要强调的东西取目标（可以多个）。</summary>
    private void SetLessonHighlights(string kind)
    {
        System.Collections.Generic.List<RectTransform> targets =
            new System.Collections.Generic.List<RectTransform>();

        if (kind == "playerCards")
        {
            RectTransform a = ui.PlayerCardSlot(0);
            RectTransform b = ui.PlayerCardSlot(1);
            if (a != null) targets.Add(a);
            if (b != null) targets.Add(b);
        }
        else if (kind == "chips")
        {
            if (ui.EnemyChipsRect != null) targets.Add(ui.EnemyChipsRect);
            if (ui.PlayerChipsRect != null) targets.Add(ui.PlayerChipsRect);
        }
        else if (kind == "check")
        {
            // 这一类由 allowedButton 那条路径负责。
            return;
        }

        SetHighlightRectangles(targets.ToArray());
    }

    private void SetHighlight(Button target)
    {
        if (target == null)
        {
            if (highlight != null)
                highlight.SetActive(false);
            return;
        }

        RectTransform rect = target.GetComponent<RectTransform>();
        SetHighlightRectangles(rect != null
            ? new RectTransform[] { rect }
            : new RectTransform[0]);
    }

    private void SetHighlightRectangles(RectTransform[] targets)
    {
        if (highlight == null)
            return;

        if (targets == null || targets.Length == 0)
        {
            highlight.SetActive(false);
            return;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        for (int i = 0; i < targets.Length; i++)
        {
            if (i >= highlightFrames.Count)
                highlightFrames.Add(CreateFrame(i));

            GameObject frame = highlightFrames[i];
            RectTransform frameRect = frame.GetComponent<RectTransform>();

            Vector3[] corners = new Vector3[4];
            targets[i].GetWorldCorners(corners);

            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            for (int c = 0; c < corners.Length; c++)
            {
                Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, corners[c]);
                min = Vector2.Min(min, screenPoint);
                max = Vector2.Max(max, screenPoint);
            }

            Vector2 localMin;
            Vector2 localMax;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, min, null, out localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, max, null, out localMax);

            frameRect.sizeDelta = localMax - localMin + new Vector2(10f, 6f);
            frameRect.anchoredPosition = (localMin + localMax) * 0.5f;
            frame.SetActive(true);
        }

        for (int i = targets.Length; i < highlightFrames.Count; i++)
            highlightFrames[i].SetActive(false);

        highlight.SetActive(true);
    }
}
