using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PokerUI : MonoBehaviour
{
    private PokerGameManager poker;

    private Text potText;
    private Text playerChipsText;
    private Text enemyChipsText;
    private Text stateText;
    private Text playerHandText;
    private Text enemyHandText;
    private Text communityText;
    private Text playerHandTypeText;
    private Text enemyHandTypeText;
    private Text messageText;

    private Button checkButton;
    private Button raiseButton;
    private Button foldButton;
    private Button matchButton;
    private Button nextHandButton;
    private InputField raiseInput;

    private System.Action onNextHand;

    private Text enemyNameText;

    public void Initialize(PokerGameManager manager)
    {
        poker = manager;
        BuildUI();

        poker.OnStateChanged += RefreshUI;
        poker.OnMessage += (msg) =>
        {
            messageText.text = msg;
        };
        poker.OnHandEnded += RefreshUI;

        RefreshUI();
    }

    public void SetNextHandCallback(System.Action callback)
    {
        onNextHand = callback;
    }

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("PokerCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGO.transform.SetParent(transform);

        // 背景
        CreatePanel(canvasGO.transform, new Color(0.1f, 0.15f, 0.1f), Vector2.zero, Vector2.one);

        // 顶部信息
        potText = CreateText(
            canvasGO.transform,
            "PotText",
            "Pot：0",
            new Vector2(0.05f, 0.9f),
            new Vector2(0.35f, 0.98f),
            TextAnchor.MiddleLeft,
            44
        );
        playerChipsText = CreateText(
            canvasGO.transform,
            "PlayerChipsText",
            "Player：0",
            new Vector2(0.4f, 0.9f),
            new Vector2(0.65f, 0.98f),
            TextAnchor.MiddleCenter,
            38
        );
        enemyChipsText = CreateText(
            canvasGO.transform,
            "EnemyChipsText",
            "Enemy：0",
            new Vector2(0.7f, 0.9f),
            new Vector2(0.95f, 0.98f),
            TextAnchor.MiddleRight,
            38
        );

        CreateText(
            canvasGO.transform,
            "EnemyLabel",
            "Enemy Hand",
            new Vector2(0.4f, 0.78f),
            new Vector2(0.6f, 0.83f),
            TextAnchor.MiddleCenter,
            32
        );

        enemyNameText = CreateText(
            canvasGO.transform,
            "EnemyNameText",
            "",
            new Vector2(0.35f, 0.84f),
            new Vector2(0.65f, 0.89f),
            TextAnchor.MiddleCenter,
            38
        );
        enemyHandText = CreateText(
            canvasGO.transform,
            "EnemyHand",
            "?? ??",
            new Vector2(0.3f, 0.68f),
            new Vector2(0.7f, 0.78f),
            TextAnchor.MiddleCenter,
            48
        );
        enemyHandTypeText = CreateText(
            canvasGO.transform,
            "EnemyHandType",
            "",
            new Vector2(0.3f, 0.63f),
            new Vector2(0.7f, 0.68f),
            TextAnchor.MiddleCenter,
            32
        );

        CreateText(
            canvasGO.transform,
            "CommunityLabel",
            "Community Cards",
            new Vector2(0.4f, 0.55f),
            new Vector2(0.6f, 0.6f),
            TextAnchor.MiddleCenter,
            32
        );
        communityText = CreateText(
            canvasGO.transform,
            "Community",
            "",
            new Vector2(0.05f, 0.44f),
            new Vector2(0.95f, 0.55f),
            TextAnchor.MiddleCenter,
            56
        );

        CreateText(
            canvasGO.transform,
            "PlayerLabel",
            "Player Hand",
            new Vector2(0.4f, 0.36f),
            new Vector2(0.6f, 0.41f),
            TextAnchor.MiddleCenter,
            32
        );
        playerHandText = CreateText(
            canvasGO.transform,
            "PlayerHand",
            "",
            new Vector2(0.3f, 0.26f),
            new Vector2(0.7f, 0.36f),
            TextAnchor.MiddleCenter,
            56
        );
        playerHandTypeText = CreateText(
            canvasGO.transform,
            "PlayerHandType",
            "",
            new Vector2(0.3f, 0.21f),
            new Vector2(0.7f, 0.26f),
            TextAnchor.MiddleCenter,
            32
        );

        stateText = CreateText(
            canvasGO.transform,
            "StateText",
            "",
            new Vector2(0.05f, 0.15f),
            new Vector2(0.95f, 0.2f),
            TextAnchor.MiddleCenter,
            34
        );

        float btnY0 = 0.04f,
            btnY1 = 0.13f;
        checkButton = CreateButton(
            canvasGO.transform,
            "CheckBtn",
            "Check",
            new Vector2(0.05f, btnY0),
            new Vector2(0.18f, btnY1),
            () => poker.PlayerCheck()
        );
        raiseInput = CreateInputField(
            canvasGO.transform,
            "RaiseInput",
            "10",
            new Vector2(0.20f, btnY0),
            new Vector2(0.30f, btnY1)
        );
        raiseButton = CreateButton(
            canvasGO.transform,
            "RaiseBtn",
            "Raise",
            new Vector2(0.31f, btnY0),
            new Vector2(0.44f, btnY1),
            () =>
            {
                int amt = 10;
                int.TryParse(raiseInput.text, out amt);
                poker.PlayerRaise(amt);
            }
        );
        matchButton = CreateButton(
            canvasGO.transform,
            "MatchBtn",
            "Match",
            new Vector2(0.46f, btnY0),
            new Vector2(0.59f, btnY1),
            () => poker.PlayerMatchRaise()
        );
        foldButton = CreateButton(
            canvasGO.transform,
            "FoldBtn",
            "Fold",
            new Vector2(0.61f, btnY0),
            new Vector2(0.74f, btnY1),
            () => poker.PlayerFold()
        );
        nextHandButton = CreateButton(
            canvasGO.transform,
            "NextHandBtn",
            "Next Game",
            new Vector2(0.76f, btnY0),
            new Vector2(0.95f, btnY1),
            () => onNextHand?.Invoke()
        );

        // 消息
        messageText = CreateText(
            canvasGO.transform,
            "Message",
            "",
            new Vector2(0.05f, 0.0f),
            new Vector2(0.95f, 0.035f),
            TextAnchor.MiddleCenter,
            18
        );
    }

    private void RefreshUI()
    {
        if (poker == null)
        {
            return;
        }

        potText.text = $"Pot：{poker.Pot}";
        playerChipsText.text = $"Player：{poker.PlayerChips}";
        enemyChipsText.text = $"Enemy：{poker.EnemyChips}";

        if (poker.EnemyParams != null && enemyNameText != null)
        {
            enemyNameText.text = poker.EnemyParams.enemyName;
        }

        string dealingStr = poker.IsDealing ? "（Dealing…）" : "";
        stateText.text =
            $"Stage：{poker.CurrentStreet}    State：{poker.State}{dealingStr}    "
            + $"Current Bet：{poker.CurrentBet}    Community Cards：{poker.CommunityCards.Count}";

        playerHandText.text = CardStr(poker.PlayerHand);
        communityText.text = CardStr(poker.CommunityCards);

        bool revealEnemy =
            poker.State == PokerState.Showdown
            || poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;

        enemyHandText.text = revealEnemy ? CardStr(poker.EnemyHand) : "?? ??";

        playerHandTypeText.text =
            poker.PlayerBestHand != null
                ? $"Hand Type：{poker.PlayerBestHand.GetDisplayName()}"
                : "";

        enemyHandTypeText.text =
            revealEnemy && poker.EnemyBestHand != null
                ? $"Hand Type：{poker.EnemyBestHand.GetDisplayName()}"
                : "";

        bool isPlayerTurn = poker.State == PokerState.PlayerTurn && !poker.IsDealing;
        bool mustMatch = poker.AwaitingPlayerMatch;

        checkButton.interactable = isPlayerTurn && !mustMatch;
        raiseButton.interactable = isPlayerTurn && !mustMatch;
        matchButton.interactable = isPlayerTurn && mustMatch;
        foldButton.interactable = isPlayerTurn;

        bool handOver =
            poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;
        nextHandButton.interactable = handOver;
    }

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
        UnityEngine.Events.UnityAction onClick
    )
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.3f, 0.25f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

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
            20
        );
        txt.raycastTarget = false;

        return btn;
    }

    private InputField CreateInputField(
        Transform parent,
        string name,
        string defaultText,
        Vector2 anchorMin,
        Vector2 anchorMax
    )
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f);

        InputField input = go.AddComponent<InputField>();
        input.text = defaultText;
        input.contentType = InputField.ContentType.IntegerNumber;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Text txt = CreateText(
            go.transform,
            "Text",
            defaultText,
            Vector2.zero,
            Vector2.one,
            TextAnchor.MiddleCenter,
            20
        );
        input.textComponent = txt;

        return input;
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
