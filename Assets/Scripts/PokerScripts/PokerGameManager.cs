using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Main game manager for the poker game.
/// Handles game state, betting rounds, card dealing, and showdown resolution.
/// The player acts first, then the AI responds based on its personality parameters.
///
/// This script is intentionally isolated from the FPS / combat system.
/// To bridge poker results to combat, subscribe to OnHandEnded from a separate
/// bridge script and read the public properties (Pot, PlayerBestHand, etc.).
/// </summary>
public class PokerGameManager : MonoBehaviour
{
    // ========== Public State Properties (for UI access) ==========

    /// <summary>
    /// Current game state (e.g. NotStarted, PlayerTurn, EnemyTurn, HandEnded).
    /// </summary>
    public PokerState State { get; private set; } = PokerState.NotStarted;

    /// <summary>
    /// Result of the last hand (who won and how).
    /// </summary>
    public PokerResult Result { get; private set; } = PokerResult.None;

    /// <summary>
    /// Current betting street (PreFlop, Flop, Turn, River, Showdown).
    /// </summary>
    public Street CurrentStreet { get; private set; } = Street.PreFlop;

    /// <summary>
    /// Current enemy AI configuration (personality parameters).
    /// </summary>
    public EnemyAIParameters EnemyParams => enemyParams;

    /// <summary>
    /// True while community cards are being dealt (animation delay in progress).
    /// </summary>
    public bool IsDealing { get; private set; } = false;

    /// <summary>
    /// True when the enemy raised and the player must respond (Match or Fold).
    /// </summary>
    public bool AwaitingPlayerMatch { get; private set; } = false;

    // ========== Chip and Betting State ==========

    public int PlayerChips { get; private set; } // Player's remaining chips.
    public int EnemyChips { get; private set; } // Enemy's remaining chips.
    public int Pot { get; private set; } // Total chips currently in the pot.
    public int CurrentBet { get; private set; } // The amount the player must match.
    public int BuyIn { get; private set; } // Ante / buy-in each player pays per hand.

    // ========== Card State for the Current Hand ==========

    public List<Card> PlayerHand { get; private set; } = new List<Card>(); // Player's 2 hole cards.
    public List<Card> EnemyHand { get; private set; } = new List<Card>(); // Enemy's 2 hole cards.
    public List<Card> CommunityCards { get; private set; } = new List<Card>(); // Shared community cards (Flop/Turn/River).

    // ========== Best Hand Results (evaluated at showdown) ==========

    public HandResult PlayerBestHand { get; private set; } // Player's best 5-card hand at showdown.
    public HandResult EnemyBestHand { get; private set; } // Enemy's best 5-card hand at showdown.

    // ========== Private Fields ==========

    private Deck deck; // The shuffled deck used for the current hand.
    private EnemyAIParameters enemyParams; // Reference to current enemy AI parameters.
    private bool playerHasRaised = false; // Tracks whether the player raised this street.
    private int playerRaiseCount = 0; // Tracks how many raises the player has made this street.
    private int playerTotalActions = 0; // Tracks total actions taken by the player this street.

    [Tooltip("Delay between each community card being revealed (seconds)")]
    public float dealDelay = 0.6f; // Visual delay between dealing community cards.

    // ========== Events for UI to subscribe to ==========

    /// <summary>Fired whenever the game state changes (for UI refresh).</summary>
    public event Action OnStateChanged;

    /// <summary>Fired when a new log message should be displayed to the player.</summary>
    public event Action<string> OnMessage;

    /// <summary>
    /// Fired when a hand ends (win, lose, fold, showdown).
    /// External systems (e.g., a combat bridge) can subscribe to this
    /// to react to the result without modifying this script.
    /// </summary>
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
        // Save starting chips and enemy parameters for this hand.
        PlayerChips = playerStartingChips;
        EnemyChips = enemyStartingChips;
        BuyIn = buyIn;
        enemyParams = parameters;

        // Reset all per-hand state.
        Pot = 0;
        CurrentBet = 0;
        playerHasRaised = false;
        AwaitingPlayerMatch = false;
        playerRaiseCount = 0;
        playerTotalActions = 0;
        IsDealing = false;
        Result = PokerResult.None;
        PlayerBestHand = null;
        EnemyBestHand = null;
        CurrentStreet = Street.PreFlop;

        // Clear any cards from the previous hand.
        PlayerHand.Clear();
        EnemyHand.Clear();
        CommunityCards.Clear();

        // Player must have at least the buy-in amount to play.
        if (PlayerChips < BuyIn)
        {
            LogMessage("Player has insufficient chips to pay the buy-in, game over");
            State = PokerState.NotStarted;
            OnStateChanged?.Invoke();
            return;
        }

        // If the enemy can't afford the buy-in, the player advances by default.
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

        // Shuffle and deal 2 hole cards each (alternating draw order).
        deck = new Deck();
        deck.Initialize();
        deck.Shuffle();

        PlayerHand.Add(deck.Draw());
        EnemyHand.Add(deck.Draw());
        PlayerHand.Add(deck.Draw());
        EnemyHand.Add(deck.Draw());

        LogMessage($"Player hand: {CardListToString(PlayerHand)}");

        // Begin the Pre-Flop betting round with the player acting first.
        CurrentStreet = Street.PreFlop;
        StartPlayerTurn();
    }

    /// <summary>玩家最大可加注额：受对手筹码限制，保证敌人总能跟注</summary>
    public int GetPlayerMaxRaise()
    {
        return Mathf.Max(0, Mathf.Min(PlayerChips, EnemyChips));
    }

    /// <summary>敌人最大可加注额：受玩家筹码限制，保证玩家总能跟注</summary>
    public int GetEnemyMaxRaise()
    {
        return Mathf.Max(0, Mathf.Min(EnemyChips, PlayerChips));
    }

    // ========== Player Actions ==========

    /// <summary>
    /// Player chooses to check. Only valid when the enemy has not raised.
    /// </summary>
    public void PlayerCheck()
    {
        if (!CanPlayerAct())
            return;

        // If the enemy raised, the player can't check - must Match or Fold.
        if (AwaitingPlayerMatch)
        {
            LogMessage("Enemy has raised, you can only Match or Fold");
            return;
        }

        LogMessage($"Player checks ({CurrentStreet})");

        playerTotalActions++;

        // Hand over control to the enemy AI.
        State = PokerState.EnemyTurn;
        OnStateChanged?.Invoke();
        ResolveEnemyTurn(playerRaised: false, raiseAmount: 0);
    }

    /// <summary>
    /// Player raises the bet by the given amount, then the AI responds.
    /// </summary>
    public void PlayerRaise(int amount)
    {
        if (!CanPlayerAct())
            return;

        // Can't raise while already facing an enemy raise - use Match instead.
        if (AwaitingPlayerMatch)
        {
            LogMessage("Enemy has raised, please use Match or Fold");
            return;
        }

        int maxRaise = GetPlayerMaxRaise();

        // 无法加注（对方没筹码了）→ 只能 Check
        if (maxRaise <= 0)
        {
            LogMessage("Cannot raise: either you or the enemy has no chips left");
            return;
        }

        // 加注额为 0 或负数 → 视为 Check
        if (amount <= 0)
        {
            LogMessage("Raise amount invalid, Auto Checking instead");
            PlayerCheck();
            return;
        }

        // 超过上限 → 压到上限，而不是拒绝
        if (amount > maxRaise)
        {
            LogMessage(
                $"Raise amount {amount} exceeds max {maxRaise}, Auto Raising {maxRaise} instead"
            );
            amount = maxRaise;
        }

        // Deduct chips from player and add to pot.
        PlayerChips -= amount;
        Pot += amount;
        CurrentBet = amount;
        playerHasRaised = true;

        playerRaiseCount++;
        playerTotalActions++;

        LogMessage($"Player raises {amount} ({CurrentStreet}), pot {Pot}");

        // Enemy's turn to respond to the raise.
        State = PokerState.EnemyTurn;
        OnStateChanged?.Invoke();
        ResolveEnemyTurn(playerRaised: true, raiseAmount: amount);
    }

    /// <summary>
    /// Player matches the enemy's raise to stay in the hand.
    /// </summary>
    public void PlayerMatchRaise()
    {
        if (!CanPlayerAct())
            return;

        // Nothing to match if there's no pending raise.
        if (!AwaitingPlayerMatch || CurrentBet <= 0)
        {
            LogMessage("There is currently no raise to match");
            return;
        }

        // 理论上不会走到这里，因为敌方加注已受玩家筹码限制
        // 保留为兜底：直接 Check 并推进，不 Fold
        if (PlayerChips < CurrentBet)
        {
            LogMessage($"无法跟注 {CurrentBet}（筹码 {PlayerChips}），转为 Check 并推进");
            AwaitingPlayerMatch = false;
            CurrentBet = 0;
            AdvanceStreet();
            return;
        }

        // Pay the match amount into the pot.
        PlayerChips -= CurrentBet;
        Pot += CurrentBet;
        LogMessage($"Player matches the raise {CurrentBet}, pot {Pot}");
        playerTotalActions++;

        // Betting for this street is resolved - advance to the next street.
        AwaitingPlayerMatch = false;
        AdvanceStreet();
    }

    /// <summary>
    /// Player folds, forfeiting the pot to the enemy.
    /// </summary>
    public void PlayerFold()
    {
        if (State != PokerState.PlayerTurn)
            return;

        LogMessage($"Player folds, losing the pot {Pot}");

        playerTotalActions++;

        // Enemy takes the pot.
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

    /// <summary>
    /// 返回玩家最近的激进度（0-1）。
    /// 0 = 从不加注，1 = 每次行动都加注。
    /// 前 3 次行动之前返回 0.5（中立）。
    /// </summary>
    public float GetPlayerAggression()
    {
        if (playerTotalActions < 3)
            return 0.5f;
        return Mathf.Clamp01((float)playerRaiseCount / playerTotalActions);
    }

    // ========== Enemy AI Turn ==========

    /// <summary>
    /// Calls the AI decision function and executes the chosen action.
    /// </summary>
    private void ResolveEnemyTurn(bool playerRaised, int raiseAmount)
    {
        // Ask the AI what it wants to do based on the current context.
        EnemyDecision decision = EnemyPokerAI.Decide(
            enemyParams,
            EnemyHand,
            CommunityCards,
            CurrentStreet,
            Pot,
            CurrentBet,
            EnemyChips,
            playerRaised,
            raiseAmount,
            GetPlayerAggression()
        );

        // Log AI reasoning for debugging.
        Debug.Log(
            $"[AI] Hand Strength {decision.handStrength:F2}, "
                + $"Decision {decision.action}, Raise {decision.raiseAmount}"
        );

        // Execute whatever action the AI chose.
        switch (decision.action)
        {
            case EnemyAction.Fold:
                EnemyFolds();
                break;

            case EnemyAction.Check:
                // AI wanted to check but player already raised - fall back to fold.
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
                // If the player raised, this is a response (match or re-raise).
                // Otherwise, the AI is initiating a bet.
                if (playerRaised)
                    HandleEnemyResponse(decision.raiseAmount, raiseAmount);
                else
                    EnemyInitiatesRaise(decision.raiseAmount);
                break;
        }
    }

    /// <summary>
    /// Handles the AI's response to a player raise: either match or re-raise.
    /// </summary>
    private void HandleEnemyResponse(int aiAmount, int playerRaiseAmount)
    {
        int maxRaise = GetEnemyMaxRaise();

        if (aiAmount <= playerRaiseAmount || maxRaise <= playerRaiseAmount)
        {
            // AI amount is at most the player's raise - treat it as a call (match).
            if (EnemyChips < playerRaiseAmount)
            {
                LogMessage("Enemy chips insufficient, changing to check");
                EnemyChecks();
                return;
            }
            EnemyChips -= playerRaiseAmount;
            Pot += playerRaiseAmount;
            CurrentBet = playerRaiseAmount;
            LogMessage($"Enemy matches the raise {playerRaiseAmount}, pot {Pot}");

            // Betting resolved - advance to the next street.
            AdvanceStreet();
            return;
        }

        if (aiAmount > maxRaise)
        {
            // AI wants to raise more than it can afford - cap it at max.
            LogMessage($"Enemy raise {aiAmount} exceeds max {maxRaise}, capping to max");
            aiAmount = maxRaise;
        }

        if (aiAmount <= playerRaiseAmount)
        {
            EnemyChips -= playerRaiseAmount;
            Pot += playerRaiseAmount;
            CurrentBet = playerRaiseAmount;
            LogMessage($"Enemy matches the raise {playerRaiseAmount}, pot {Pot}");
            AdvanceStreet();
            return;
        }


        // Execute the re-raise and give the turn back to the player.
        EnemyChips -= aiAmount;
        Pot += aiAmount;
        CurrentBet = aiAmount;
        AwaitingPlayerMatch = true;
        LogMessage($"Enemy raises {aiAmount}, pot {Pot}. Player must Match or Fold.");
        State = PokerState.PlayerTurn;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// AI initiates a raise (no prior player raise in this street).
    /// The player must then Match or Fold.
    /// </summary>
    private void EnemyInitiatesRaise(int amount)
    {
        int maxRaise = GetEnemyMaxRaise();

        if (maxRaise <= 0)
        {
            LogMessage("Enemy cannot raise: either player or enemy has no chips left");
            EnemyChecks();
            return;
        }

        // Default bet size if AI returned an invalid amount.
        if (amount <= 0)
            amount = Mathf.Max(1, BuyIn / 2);

        // Can't afford to raise - check instead.
        if (amount > maxRaise)
        {
            amount = maxRaise;
            LogMessage($"Enemy raise {amount} exceeds max {maxRaise}, capping to max"); 
        }

        // Deduct chips from enemy and add to pot.
        EnemyChips -= amount;
        Pot += amount;
        CurrentBet = amount;
        AwaitingPlayerMatch = true;

        LogMessage($"Enemy raises {amount}, pot {Pot}. Player must Match or Fold.");

        // Turn goes back to the player.
        State = PokerState.PlayerTurn;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Enemy checks - advance to the next street.
    /// </summary>
    private void EnemyChecks()
    {
        LogMessage($"Enemy checks ({CurrentStreet})");
        AdvanceStreet();
    }

    /// <summary>
    /// Enemy folds - player wins the pot immediately.
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

    // ========== Street Progression ==========

    /// <summary>
    /// Advances to the next betting street, dealing community cards as needed.
    /// </summary>
    private void AdvanceStreet()
    {
        // Reset per-street betting state.
        CurrentBet = 0;
        playerHasRaised = false;
        AwaitingPlayerMatch = false;

        // Move to the next street and deal cards accordingly.
        switch (CurrentStreet)
        {
            case Street.PreFlop:
                // Deal 3 cards for the Flop.
                StartCoroutine(DealCommunityCards(3, Street.Flop));
                break;
            case Street.Flop:
                // Deal 1 card for the Turn.
                StartCoroutine(DealCommunityCards(1, Street.Turn));
                break;
            case Street.Turn:
                // Deal 1 card for the River.
                StartCoroutine(DealCommunityCards(1, Street.River));
                break;
            case Street.River:
                // No more cards - resolve the showdown.
                ResolveShowdown();
                break;
        }
    }

    /// <summary>
    /// Coroutine that reveals community cards one at a time with a delay.
    /// </summary>
    private IEnumerator DealCommunityCards(int count, Street nextStreet)
    {
        // Mark as dealing so the player can't act.
        IsDealing = true;
        State = PokerState.Dealing;
        OnStateChanged?.Invoke();

        // Reveal each card with a delay between them.
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(dealDelay);
            Card c = deck.Draw();
            CommunityCards.Add(c);
            LogMessage($"Revealed community card: {c}");
            OnStateChanged?.Invoke();
        }

        // Update the street and hand the turn back to the player.
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

    // ========== Showdown ==========

    /// <summary>
    /// Resolves the showdown: evaluates both hands, compares them, and awards the pot.
    /// </summary>
    private void ResolveShowdown()
    {
        CurrentStreet = Street.Showdown;

        // Combine each player's hole cards with the community cards.
        List<Card> playerAll = new List<Card>(PlayerHand);
        playerAll.AddRange(CommunityCards);
        PlayerBestHand = HandEvaluator.Evaluate(playerAll);

        List<Card> enemyAll = new List<Card>(EnemyHand);
        enemyAll.AddRange(CommunityCards);
        EnemyBestHand = HandEvaluator.Evaluate(enemyAll);

        LogMessage($"Player's best hand: {PlayerBestHand.GetDisplayName()}");
        LogMessage($"Enemy's best hand: {EnemyBestHand.GetDisplayName()}");

        // Compare hands: >0 player wins, <0 enemy wins, 0 tie.
        int cmp = CompareHands(PlayerBestHand, EnemyBestHand);

        if (cmp == 0)
        {
            // Tie: settle immediately, no combat.
            int half = Pot / 2;
            PlayerChips += half;
            EnemyChips += Pot - half;
            Pot = 0;
            LogMessage("Showdown: Tie, split pot");
            Result = PokerResult.TieShowdown;
            State = PokerState.HandEnded;
            OnStateChanged?.Invoke();
            OnHandEnded?.Invoke();
            return;
        }

        // Non-tie: record the winner, KEEP the pot frozen, and wait for
        // the bridge to hand off to combat (or settle directly if no bridge).
        Result = cmp > 0 ? PokerResult.PlayerWinsShowdown : PokerResult.EnemyWinsShowdown;

        State = PokerState.Showdown;
        LogMessage($"Showdown resolved: {Result}. Pot {Pot} waiting for combat handoff.");
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

    /// <summary>
    /// Compares two hand results using their tiebreaker arrays.
    /// Returns positive if A wins, negative if B wins, 0 for a tie.
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

    // 供桥接层使用的 API
    // 这些方法都是纯数据操作，不包含任何战斗逻辑。
    // 由 PokerCombatBridge（或任何外部系统）调用。

    /// <summary>恢复玩家筹码（例如从战斗场景返回后）。</summary>
    public void RestorePlayerChips(int chips)
    {
        PlayerChips = chips;
        OnStateChanged?.Invoke();
    }

    /// <summary>恢复敌人筹码（例如从战斗场景返回后）。</summary>
    public void RestoreEnemyChips(int chips)
    {
        EnemyChips = chips;
        OnStateChanged?.Invoke();
    }

    /// <summary>把底池金额写回（例如由 GameFlowManager 携带返回）。</summary>
    public void SetPendingPot(int pot)
    {
        Pot = pot;
    }

    /// <summary>标记本手牌暂停，等待战斗交接。</summary>
    public void MarkReadyForCombat()
    {
        State = PokerState.Showdown;
        OnStateChanged?.Invoke();
    }

    /// <summary>结算冻结的底池，归玩家所有。</summary>
    public void CreditPotToPlayer()
    {
        PlayerChips += Pot;
        Pot = 0;
        State = PokerState.HandEnded;
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

    /// <summary>结算冻结的底池，归敌人所有（玩家失去底池）。</summary>
    public void ForfeitPot()
    {
        Pot = 0;
        State = PokerState.HandEnded;
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

    // ========== Utility ==========

    /// <summary>
    /// Logs a message to the Unity console and notifies any UI listeners.
    /// </summary>
    private void LogMessage(string msg)
    {
        Debug.Log($"[Poker] {msg}");
        OnMessage?.Invoke(msg);
    }

    /// <summary>
    /// Converts a list of cards into a space-separated string for logging.
    /// </summary>
    private string CardListToString(List<Card> cards)
    {
        string s = "";
        foreach (Card c in cards)
            s += c.ToString() + " ";
        return s;
    }
}
