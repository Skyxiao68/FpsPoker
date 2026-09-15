using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamically builds the poker game UI using Unity's UI system.
/// Creates a canvas with all necessary text fields, buttons, and input fields.
/// Subscribes to PokerGameManager events to update the display in real-time.
/// </summary>
public class PokerUI : MonoBehaviour
{
    // Reference to the poker game logic manager.
    private PokerGameManager poker;

    // UI text elements for displaying game state.
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

    // UI interactive elements.
    private Button checkButton;
    private Button raiseButton;
    private Button foldButton;
    private Button matchButton;
    private Button nextHandButton;
    private InputField raiseInput;

    // Callback invoked when the "Next Game" button is clicked.
    private System.Action onNextHand;

    // Text element that displays the enemy's name.
    private Text enemyNameText;

    /// <summary>
    /// Initializes the UI by building it and subscribing to game manager events.
    /// </summary>
    public void Initialize(PokerGameManager manager)
    {
        poker = manager;
        BuildUI();

        // Refresh the UI whenever the game state changes.
        poker.OnStateChanged += RefreshUI;

        // Show temporary game messages at the bottom of the screen.
        poker.OnMessage += (msg) =>
        {
            messageText.text = msg;
        };

        // Refresh the UI when a hand ends.
        poker.OnHandEnded += RefreshUI;

        RefreshUI();
    }

    /// <summary>
    /// Sets the callback that is called when the "Next Game" button is clicked.
    /// </summary>
    public void SetNextHandCallback(System.Action callback)
    {
        onNextHand = callback;
    }

    /// <summary>
    /// Builds the entire poker UI at runtime.
    /// </summary>
    private void BuildUI()
    {
        // Create the root canvas GameObject.
        GameObject canvasGO = new GameObject("PokerCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        // Configure canvas scaling so the UI adapts to different screen sizes.
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        // Add a GraphicRaycaster so UI buttons and input fields can receive clicks.
        canvasGO.AddComponent<GraphicRaycaster>();
        canvasGO.transform.SetParent(transform);

        // Create a full-screen background panel.
        CreatePanel(canvasGO.transform, new Color(0.1f, 0.15f, 0.1f), Vector2.zero, Vector2.one);

        // Top info: pot amount.
        potText = CreateText(
            canvasGO.transform,
            "PotText",
            "Pot：0",
            new Vector2(0.05f, 0.9f),
            new Vector2(0.35f, 0.98f),
            TextAnchor.MiddleLeft,
            44
        );

        // Top info: player chip count.
        playerChipsText = CreateText(
            canvasGO.transform,
            "PlayerChipsText",
            "Player：0",
            new Vector2(0.4f, 0.9f),
            new Vector2(0.65f, 0.98f),
            TextAnchor.MiddleCenter,
            38
        );

        // Top info: enemy chip count.
        enemyChipsText = CreateText(
            canvasGO.transform,
            "EnemyChipsText",
            "Enemy：0",
            new Vector2(0.7f, 0.9f),
            new Vector2(0.95f, 0.98f),
            TextAnchor.MiddleRight,
            38
        );

        // Label for the enemy hand area.
        CreateText(
            canvasGO.transform,
            "EnemyLabel",
            "Enemy Hand",
            new Vector2(0.4f, 0.78f),
            new Vector2(0.6f, 0.83f),
            TextAnchor.MiddleCenter,
            32
        );

        // Enemy name display.
        enemyNameText = CreateText(
            canvasGO.transform,
            "EnemyNameText",
            "",
            new Vector2(0.35f, 0.84f),
            new Vector2(0.65f, 0.89f),
            TextAnchor.MiddleCenter,
            38
        );

        // Enemy hand cards. Hidden as "?? ??" until showdown or hand end.
        enemyHandText = CreateText(
            canvasGO.transform,
            "EnemyHand",
            "?? ??",
            new Vector2(0.3f, 0.68f),
            new Vector2(0.7f, 0.78f),
            TextAnchor.MiddleCenter,
            48
        );

        // Enemy best hand type display.
        enemyHandTypeText = CreateText(
            canvasGO.transform,
            "EnemyHandType",
            "",
            new Vector2(0.3f, 0.63f),
            new Vector2(0.7f, 0.68f),
            TextAnchor.MiddleCenter,
            32
        );

        // Label for the community cards area.
        CreateText(
            canvasGO.transform,
            "CommunityLabel",
            "Community Cards",
            new Vector2(0.4f, 0.55f),
            new Vector2(0.6f, 0.6f),
            TextAnchor.MiddleCenter,
            32
        );

        // Community cards display.
        communityText = CreateText(
            canvasGO.transform,
            "Community",
            "",
            new Vector2(0.05f, 0.44f),
            new Vector2(0.95f, 0.55f),
            TextAnchor.MiddleCenter,
            56
        );

        // Label for the player hand area.
        CreateText(
            canvasGO.transform,
            "PlayerLabel",
            "Player Hand",
            new Vector2(0.4f, 0.36f),
            new Vector2(0.6f, 0.41f),
            TextAnchor.MiddleCenter,
            32
        );

        // Player hand cards display.
        playerHandText = CreateText(
            canvasGO.transform,
            "PlayerHand",
            "",
            new Vector2(0.3f, 0.26f),
            new Vector2(0.7f, 0.36f),
            TextAnchor.MiddleCenter,
            56
        );

        // Player best hand type display.
        playerHandTypeText = CreateText(
            canvasGO.transform,
            "PlayerHandType",
            "",
            new Vector2(0.3f, 0.21f),
            new Vector2(0.7f, 0.26f),
            TextAnchor.MiddleCenter,
            32
        );

        // Current stage / state / bet information.
        stateText = CreateText(
            canvasGO.transform,
            "StateText",
            "",
            new Vector2(0.05f, 0.15f),
            new Vector2(0.95f, 0.2f),
            TextAnchor.MiddleCenter,
            34
        );

        // Bottom action buttons and raise input.
        float btnY0 = 0.04f,
            btnY1 = 0.13f;

        // Check button.
        checkButton = CreateButton(
            canvasGO.transform,
            "CheckBtn",
            "Check",
            new Vector2(0.05f, btnY0),
            new Vector2(0.18f, btnY1),
            () => poker.PlayerCheck()
        );

        // Raise amount input field.
        raiseInput = CreateInputField(
            canvasGO.transform,
            "RaiseInput",
            "10",
            new Vector2(0.20f, btnY0),
            new Vector2(0.30f, btnY1)
        );

        // Raise button. Parses the input field and sends the raise amount.
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

        // Match button. Used when the player must match the enemy's raise.
        matchButton = CreateButton(
            canvasGO.transform,
            "MatchBtn",
            "Match",
            new Vector2(0.46f, btnY0),
            new Vector2(0.59f, btnY1),
            () => poker.PlayerMatchRaise()
        );

        // Fold button.
        foldButton = CreateButton(
            canvasGO.transform,
            "FoldBtn",
            "Fold",
            new Vector2(0.61f, btnY0),
            new Vector2(0.74f, btnY1),
            () => poker.PlayerFold()
        );

        // Next game button. Invokes the callback set by SetNextHandCallback.
        nextHandButton = CreateButton(
            canvasGO.transform,
            "NextHandBtn",
            "Next Game",
            new Vector2(0.76f, btnY0),
            new Vector2(0.95f, btnY1),
            () => onNextHand?.Invoke()
        );

        // Bottom message text.
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

    /// <summary>
    /// Refreshes all UI text and button interactability based on the current game state.
    /// </summary>
    private void RefreshUI()
    {
        if (poker == null)
        {
            return;
        }

        // Update pot and chip displays.
        potText.text = $"Pot：{poker.Pot}";
        playerChipsText.text = $"Player：{poker.PlayerChips}";
        enemyChipsText.text = $"Enemy：{poker.EnemyChips}";

        // Update enemy name if enemy parameters are available.
        if (poker.EnemyParams != null && enemyNameText != null)
        {
            enemyNameText.text = poker.EnemyParams.enemyName;
        }

        // Show a dealing indicator while cards are being dealt.
        string dealingStr = poker.IsDealing ? "（Dealing…）" : "";
        stateText.text =
            $"Stage：{poker.CurrentStreet}    State：{poker.State}{dealingStr}    "
            + $"Current Bet：{poker.CurrentBet}    Community Cards：{poker.CommunityCards.Count}";

        // Update player hand and community cards.
        playerHandText.text = CardStr(poker.PlayerHand);
        communityText.text = CardStr(poker.CommunityCards);

        // Reveal the enemy hand only when the hand is over or at showdown.
        bool revealEnemy =
            poker.State == PokerState.Showdown
            || poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;

        // Show enemy cards or hide them with question marks.
        enemyHandText.text = revealEnemy ? CardStr(poker.EnemyHand) : "?? ??";

        // Show player's best hand type.
        playerHandTypeText.text =
            poker.PlayerBestHand != null
                ? $"Hand Type：{poker.PlayerBestHand.GetDisplayName()}"
                : "";

        // Show enemy's best hand type only when the enemy hand is revealed.
        enemyHandTypeText.text =
            revealEnemy && poker.EnemyBestHand != null
                ? $"Hand Type：{poker.EnemyBestHand.GetDisplayName()}"
                : "";

        // Determine whether it is currently the player's turn.
        bool isPlayerTurn = poker.State == PokerState.PlayerTurn && !poker.IsDealing;

        // If the player is awaiting a match, they must Match or Fold.
        bool mustMatch = poker.AwaitingPlayerMatch;

        // Enable or disable action buttons based on the current situation.
        checkButton.interactable = isPlayerTurn && !mustMatch;
        raiseButton.interactable = isPlayerTurn && !mustMatch;
        matchButton.interactable = isPlayerTurn && mustMatch;
        foldButton.interactable = isPlayerTurn;

        // Determine whether the hand is over.
        bool handOver =
            poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded;

        // The Next Game button is only clickable after the hand is over.
        nextHandButton.interactable = handOver;
    }

    /// <summary>
    /// Helper method that creates a Text element with anchor-based positioning.
    /// </summary>
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

    /// <summary>
    /// Helper method that creates a Button with a text label and click action.
    /// </summary>
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

        // Button background image.
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.3f, 0.25f);

        // Button component and click listener.
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Create the button label text.
        Text txt = CreateText(
            go.transform,
            "Label",
            label,
            Vector2.zero,
            Vector2.one,
            TextAnchor.MiddleCenter,
            20
        );

        // Prevent the label from blocking clicks on the button.
        txt.raycastTarget = false;

        return btn;
    }

    /// <summary>
    /// Helper method that creates an InputField for entering the raise amount.
    /// </summary>
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

        // Input field background image.
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.15f);

        // InputField component. Only allows integer numbers.
        InputField input = go.AddComponent<InputField>();
        input.text = defaultText;
        input.contentType = InputField.ContentType.IntegerNumber;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Create the text component used by the InputField.
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

    /// <summary>
    /// Helper method that creates a full-stretch Image panel.
    /// </summary>
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

    /// <summary>
    /// Converts a list of Card objects into a display string.
    /// </summary>
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
