using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main game manager for the poker game.
/// Handles game state, betting rounds, card dealing, and showdown resolution.
/// The player acts first, then the AI responds based on its personality parameters.
/// </summary>
public class PokerGameManager : MonoBehaviour
{
    // ========== Public State Properties (for UI access) ==========

    /// <summary>Current game state (e.g., PlayerTurn, EnemyTurn, HandEnded).</summary>
    public PokerState State { get; private set; } = PokerState.NotStarted;

    /// <summary>Result of the last hand (who won and how).</summary>
    public PokerResult Result { get; private set; } = PokerResult.None;

    /// <summary>Current betting street (PreFlop, Flop, Turn, River, Showdown).</summary>
    public Street CurrentStreet { get; private set; } = Street.PreFlop;

    /// <summary>Current enemy AI configuration (personality, aggression, etc.).</summary>
    public EnemyAIParameters EnemyParams => enemyParams;

    /// <summary>True while community cards are being dealt (animation delay).</summary>
    public bool IsDealing { get; private set; } = false;

    // ========== Chip and Betting State ==========

    public int PlayerChips { get; private set; }
    public int EnemyChips { get; private set; }
    public int Pot { get; private set; }
    public int CurrentBet { get; private set; }
    public int BuyIn { get; private set; }

    // ========== Card State for the Current Hand ==========

    public List<Card> PlayerHand { get; private set; } = new List<Card>();
    public List<Card> EnemyHand { get; private set; } = new List<Card>();
    public List<Card> CommunityCards { get; private set; } = new List<Card>();

    // ========== Best Hand Results (evaluated at showdown) ==========

    public HandResult PlayerBestHand { get; private set; }
    public HandResult EnemyBestHand { get; private set; }

    // ========== Private Fields ==========

    private Deck deck;
    private EnemyAIParameters enemyParams;
    private bool playerHasRaised = false;

    /// <summary>True when the enemy has raised and the player must respond (Match or Fold).</summary>
    public bool AwaitingPlayerMatch { get; private set; } = false;

    [Tooltip("Delay between each community card being revealed (seconds)")]
    public float dealDelay = 0.6f;

    // ========== Events for UI to subscribe to ==========

    /// <summary>Fired whenever the game state changes (for UI refresh).</summary>
    public event Action OnStateChanged;

    /// <summary>Fired when a new log message should be displayed to the player.</summary>
    public event Action<string> OnMessage;

    /// <summary>Fired when a hand ends (win, lose, fold, showdown).</summary>
    public event Action OnHandEnded;

    // ========== Hand Initialization ==========

    /// <summary>
    /// Starts a new poker hand with the given chip counts, buy-in amount, and enemy parameters.
    /// Resets all state, collects buy-ins, shuffles the deck, and deals hole cards.
    /// </summary>
    public void StartNewHand(
        int playerStartingChips,
        int enemyStartingChips,
        int buyIn,
        EnemyAIParameters parameters
    )
    {
        PlayerChips = playerStartingChips;
        EnemyChips = enemyStartingChips;
        BuyIn = buyIn;
        enemyParams = parameters;

        Pot = 0;
        CurrentBet = 0;
        playerHasRaised = false;
        AwaitingPlayerMatch = false;
        IsDealing = false;
        Result = PokerResult.None;
        PlayerBestHand = null;
        EnemyBestHand = null;
        CurrentStreet = Street.PreFlop;

        PlayerHand.Clear();
        EnemyHand.Clear();
        CommunityCards.Clear();

        // Check if both players have enough chips for the buy-in.
        if (PlayerChips < BuyIn)
        {
            LogMessage("Player has insufficient chips to pay the buy-in, game over");
            State = PokerState.NotStarted;
            OnStateChanged?.Invoke();
            return;
        }
        if (EnemyChips < BuyIn)
        {
            LogMessage("Enemy has insufficient chips, player advances");
            State = PokerState.HandEnded;
            OnStateChanged?.Invoke();
            return;
        }

        // Collect buy-in from both players into the pot.
        State = PokerState.PayingBuyIn;
        PlayerChips -= BuyIn;
        EnemyChips -= BuyIn;
        Pot += BuyIn * 2;
        LogMessage($"Both players paid the buy-in {BuyIn}, pot {Pot}");

        // Shuffle the deck and deal 2 hole cards to each player.
        deck = new Deck();
        deck.Initialize();
        deck.Shuffle();

        PlayerHand.Add(deck.Draw());
        EnemyHand.Add(deck.Draw());
        PlayerHand.Add(deck.Draw());
        EnemyHand.Add(deck.Draw());

        LogMessage($"Player hand: {CardListToString(PlayerHand)}");

        // Begin Pre-Flop betting round.
        CurrentStreet = Street.PreFlop;
        StartPlayerTurn();
    }

    // ========== Player Actions ==========

    /// <summary>
    /// Player chooses to check. Only valid when no bet is currently on the table.
    /// </summary>
    public void PlayerCheck()
    {
        if (!CanPlayerAct())
            return;
        if (AwaitingPlayerMatch)
        {
            LogMessage("Enemy has raised, you can only Match or Fold");
            return;
        }

        LogMessage($"Player checks ({CurrentStreet})");
        State = PokerState.EnemyTurn;
        OnStateChanged?.Invoke();
        ResolveEnemyTurn(playerRaised: false, raiseAmount: 0);
    }

    /// <summary>
    /// Player raises the bet by a specified amount.
    /// Deducts chips from player and adds to pot, then AI responds.
    /// </summary>
    public void PlayerRaise(int amount)
    {
        if (!CanPlayerAct())
            return;
        if (AwaitingPlayerMatch)
        {
            LogMessage("Enemy has raised, please use Match or Fold");
            return;
        }

        if (amount <= 0 || amount > PlayerChips)
        {
            LogMessage($"Invalid raise amount (current chips {PlayerChips})");
            return;
        }

        PlayerChips -= amount;
        Pot += amount;
        CurrentBet = amount;
        playerHasRaised = true;

        LogMessage($"Player raises {amount} ({CurrentStreet}), pot {Pot}");

        State = PokerState.EnemyTurn;
        OnStateChanged?.Invoke();
        ResolveEnemyTurn(playerRaised: true, raiseAmount: amount);
    }

    /// <summary>
    /// Player matches the enemy's raise amount to stay in the hand.
    /// </summary>
    public void PlayerMatchRaise()
    {
        if (!CanPlayerAct())
            return;
        if (!AwaitingPlayerMatch || CurrentBet <= 0)
        {
            LogMessage("There is currently no raise to match");
            return;
        }

        if (PlayerChips < CurrentBet)
        {
            LogMessage("Insufficient chips to match, automatically folding");
            PlayerFold();
            return;
        }

        PlayerChips -= CurrentBet;
        Pot += CurrentBet;
        LogMessage($"Player matches the raise {CurrentBet}, pot {Pot}");

        AwaitingPlayerMatch = false;
        AdvanceStreet();
    }

    /// <summary>
    /// Player folds, losing the pot to the enemy.
    /// </summary>
    public void PlayerFold()
    {
        if (State != PokerState.PlayerTurn)
            return;

        LogMessage($"Player folds, losing the pot {Pot}");
        EnemyChips += Pot;
        Pot = 0;
        AwaitingPlayerMatch = false;
        Result = PokerResult.EnemyWinsByFold;
        State = PokerState.PlayerFolded;
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

    /// <summary>
    /// Returns true if the player is allowed to perform an action right now.
    /// </summary>
    private bool CanPlayerAct()
    {
        return State == PokerState.PlayerTurn && !IsDealing;
    }

    // ========== Enemy AI Turn ==========

    /// <summary>
    /// Calls the AI decision function and executes the chosen action.
    /// </summary>
    private void ResolveEnemyTurn(bool playerRaised, int raiseAmount)
    {
        EnemyDecision decision = EnemyPokerAI.Decide(
            enemyParams,
            EnemyHand,
            CommunityCards,
            CurrentStreet,
            Pot,
            CurrentBet,
            EnemyChips,
            playerRaised,
            raiseAmount
        );

        Debug.Log(
            $"[AI] Hand Strength {decision.handStrength:F2}, Decision {decision.action}, Raise {decision.raiseAmount}"
        );

        switch (decision.action)
        {
            case EnemyAction.Fold:
                EnemyFolds();
                break;

            case EnemyAction.Check:
                if (playerRaised)
                {
                    LogMessage("Enemy cannot Check a Player Raise, changing to Fold");
                    EnemyFolds();
                }
                else
                {
                    EnemyChecks();
                }
                break;

            case EnemyAction.Raise:
                if (playerRaised)
                {
                    // Player already raised, so AI's Raise is a response:
                    // either match or re-raise.
                    HandleEnemyResponse(decision.raiseAmount, raiseAmount);
                }
                else
                {
                    // AI initiates a raise.
                    EnemyInitiatesRaise(decision.raiseAmount);
                }
                break;
        }
    }

    /// <summary>
    /// Handles the AI's response to a player raise.
    /// If the AI amount is less than or equal to the player's raise, it matches.
    /// Otherwise it re-raises, and the player must respond.
    /// </summary>
    private void HandleEnemyResponse(int aiAmount, int playerRaiseAmount)
    {
        if (aiAmount <= playerRaiseAmount)
        {
            // Match the player's raise.
            if (EnemyChips < playerRaiseAmount)
            {
                LogMessage("Enemy chips insufficient, changing to Fold");
                EnemyFolds();
                return;
            }
            EnemyChips -= playerRaiseAmount;
            Pot += playerRaiseAmount;
            CurrentBet = playerRaiseAmount;
            LogMessage($"Enemy matches the raise {playerRaiseAmount}, Pot {Pot}");
            AdvanceStreet();
        }
        else
        {
            // Re-raise.
            if (EnemyChips < aiAmount)
            {
                LogMessage("Enemy chips insufficient, changing to Match");
                EnemyChips -= playerRaiseAmount;
                Pot += playerRaiseAmount;
                CurrentBet = playerRaiseAmount;
                AdvanceStreet();
                return;
            }
            EnemyChips -= aiAmount;
            Pot += aiAmount;
            CurrentBet = aiAmount;
            AwaitingPlayerMatch = true;
            LogMessage($"Enemy raises {aiAmount}, Pot {Pot}. Player must Match or Fold.");
            State = PokerState.PlayerTurn;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// AI initiates a raise (no prior player raise).
    /// The player must then Match or Fold.
    /// </summary>
    private void EnemyInitiatesRaise(int amount)
    {
        if (amount <= 0)
            amount = Mathf.Max(1, BuyIn / 2);

        if (EnemyChips < amount)
        {
            LogMessage("Enemy chips insufficient, changing to Check");
            EnemyChecks();
            return;
        }

        EnemyChips -= amount;
        Pot += amount;
        CurrentBet = amount;
        AwaitingPlayerMatch = true;

        LogMessage($"Enemy raises {amount}, Pot {Pot}. Player must Match or Fold.");
        State = PokerState.PlayerTurn;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Evaluates a pre-flop hand (only hole cards available).
    /// Used for early AI decisions or quick strength estimation.
    /// </summary>
    private HandResult EvaluatePreFlop(List<Card> holeCards)
    {
        HandResult result = new HandResult();
        result.bestFive = new List<Card>(holeCards);

        if (holeCards.Count < 2)
        {
            result.handType = HandType.HighCard;
            result.tiebreakers = new int[] { (int)HandType.HighCard, 0 };
            return result;
        }

        Card a = holeCards[0];
        Card b = holeCards[1];

        if (a.Rank == b.Rank)
        {
            result.handType = HandType.OnePair;
            result.tiebreakers = new int[] { (int)HandType.OnePair, a.GetPokerValue(), 0, 0, 0 };
            return result;
        }

        if (a.Suit == b.Suit)
        {
            result.handType = HandType.TwoPair;
            int hi = Mathf.Max(a.GetPokerValue(), b.GetPokerValue());
            int lo = Mathf.Min(a.GetPokerValue(), b.GetPokerValue());
            result.tiebreakers = new int[] { (int)HandType.TwoPair, hi, lo, 0, 0 };
            return result;
        }

        // Regular high card.
        result.handType = HandType.HighCard;
        int h = Mathf.Max(a.GetPokerValue(), b.GetPokerValue());
        int l = Mathf.Min(a.GetPokerValue(), b.GetPokerValue());
        result.tiebreakers = new int[] { (int)HandType.HighCard, h, l, 0, 0, 0 };
        return result;
    }

    /// <summary>
    /// Enemy checks. Advances to the next street.
    /// </summary>
    private void EnemyChecks()
    {
        LogMessage($"Enemy checks ({CurrentStreet})");
        AdvanceStreet();
    }

    /// <summary>
    /// Enemy folds. Player wins the pot.
    /// </summary>
    private void EnemyFolds()
    {
        LogMessage($"Enemy folds, player wins the pot {Pot}");
        PlayerChips += Pot;
        Pot = 0;
        Result = PokerResult.PlayerWinsByFold;
        State = PokerState.EnemyFolded;
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

    /// <summary>
    /// Enemy raises by a given amount. If the player already raised, this is a match;
    /// otherwise it's a new raise and the player must respond.
    /// </summary>
    private void EnemyRaises(int amount)
    {
        if (amount <= 0)
            amount = Mathf.Max(1, BuyIn / 2);

        if (EnemyChips < amount)
        {
            LogMessage("Enemy has insufficient chips to match, folding");
            EnemyFolds();
            return;
        }

        EnemyChips -= amount;
        Pot += amount;
        CurrentBet = amount;

        if (playerHasRaised)
        {
            LogMessage($"Enemy matches the raise {amount}, pot {Pot}");
            AdvanceStreet();
        }
        else
        {
            AwaitingPlayerMatch = true;
            LogMessage($"Enemy raises {amount}, pot {Pot}. Player must Match or Fold.");
            State = PokerState.PlayerTurn;
            OnStateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Advances to the next betting street, dealing community cards as needed.
    /// </summary>
    private void AdvanceStreet()
    {
        CurrentBet = 0;
        playerHasRaised = false;
        AwaitingPlayerMatch = false;

        switch (CurrentStreet)
        {
            case Street.PreFlop:
                StartCoroutine(DealCommunityCards(3, Street.Flop));
                break;
            case Street.Flop:
                StartCoroutine(DealCommunityCards(1, Street.Turn));
                break;
            case Street.Turn:
                StartCoroutine(DealCommunityCards(1, Street.River));
                break;
            case Street.River:
                ResolveShowdown();
                break;
        }
    }

    /// <summary>
    /// Coroutine that deals community cards one by one with a delay.
    /// </summary>
    private IEnumerator DealCommunityCards(int count, Street nextStreet)
    {
        IsDealing = true;
        State = PokerState.Dealing;
        OnStateChanged?.Invoke();

        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(dealDelay);
            Card c = deck.Draw();
            CommunityCards.Add(c);
            LogMessage($"Revealed community card: {c}");
            OnStateChanged?.Invoke();
        }

        CurrentStreet = nextStreet;
        IsDealing = false;
        StartPlayerTurn();
    }

    /// <summary>
    /// Sets the state to PlayerTurn and notifies listeners.
    /// </summary>
    private void StartPlayerTurn()
    {
        State = PokerState.PlayerTurn;
        LogMessage($"—— {CurrentStreet} Player turn ——");
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Resolves the showdown: evaluates both hands, compares them, and awards the pot.
    /// </summary>
    private void ResolveShowdown()
    {
        CurrentStreet = Street.Showdown;

        List<Card> playerAll = new List<Card>(PlayerHand);
        playerAll.AddRange(CommunityCards);
        PlayerBestHand = HandEvaluator.Evaluate(playerAll);

        List<Card> enemyAll = new List<Card>(EnemyHand);
        enemyAll.AddRange(CommunityCards);
        EnemyBestHand = HandEvaluator.Evaluate(enemyAll);

        LogMessage($"Player's best hand: {PlayerBestHand.GetDisplayName()}");
        LogMessage($"Enemy's best hand: {EnemyBestHand.GetDisplayName()}");

        int cmp = CompareHands(PlayerBestHand, EnemyBestHand);

        if (cmp > 0)
        {
            PlayerChips += Pot;
            LogMessage($"Showdown: Player wins, receives pot {Pot}");
            Result = PokerResult.PlayerWinsShowdown;
        }
        else if (cmp < 0)
        {
            EnemyChips += Pot;
            LogMessage($"Showdown: Enemy wins, receives pot {Pot}");
            Result = PokerResult.EnemyWinsShowdown;
        }
        else
        {
            int half = Pot / 2;
            PlayerChips += half;
            EnemyChips += Pot - half;
            LogMessage($"Showdown: Tie, split pot");
            Result = PokerResult.TieShowdown;
        }

        Pot = 0;
        State = PokerState.HandEnded;
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

    /// <summary>
    /// Compares two hand results. Returns positive if A wins, negative if B wins, 0 for tie.
    /// </summary>
    private int CompareHands(HandResult a, HandResult b)
    {
        int len = Mathf.Min(a.tiebreakers.Length, b.tiebreakers.Length);
        for (int i = 0; i < len; i++)
        {
            if (a.tiebreakers[i] != b.tiebreakers[i])
                return a.tiebreakers[i] - b.tiebreakers[i];
        }
        return a.tiebreakers.Length - b.tiebreakers.Length;
    }

    /// <summary>
    /// Logs a message to the Unity console and notifies any UI listeners.
    /// </summary>
    private void LogMessage(string msg)
    {
        Debug.Log($"[Poker] {msg}");
        OnMessage?.Invoke(msg);
    }

    /// <summary>
    /// Converts a list of cards to a readable string.
    /// </summary>
    private string CardListToString(List<Card> cards)
    {
        string s = "";
        foreach (Card c in cards)
            s += c.ToString() + " ";
        return s;
    }
}
