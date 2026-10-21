using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>Category of a log line, so the table can colour it correctly.</summary>
public enum LogKind
{
    System,
    Player,
    Enemy,
    Street
}

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

    private int swapsRemainingThisHand = 0;

    /// <summary>本手牌剩余的换牌次数</summary>
    public int SwapsRemaining => swapsRemainingThisHand;

    /// <summary>当前是否可以换牌</summary>
    public bool CanSwapCard =>
        State == PokerState.PlayerTurn
        && !IsDealing
        && !AwaitingPlayerMatch
        && swapsRemainingThisHand > 0;

    // ========== Chip and Betting State ==========

    public int PlayerChips { get; private set; } // Player's remaining chips.
    public int EnemyChips { get; private set; } // Enemy's remaining chips.
    public int Pot { get; private set; } // Total chips currently in the pot.
    public int CurrentBet { get; private set; } // The amount the player must match.
    // 桌面上那行「谁下了多少」用的：最后一次下注的金额与下注方。
    public int LastBetAmount { get; private set; }
    public bool LastBetByEnemy { get; private set; }
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
    public event Action<string, LogKind> OnMessage;

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
        PrepareHand(playerStartingChips, enemyStartingChips, buyIn, parameters, null);
    }

    /// <summary>
    /// 教程用：按脚本顺序发牌。scriptedDrawOrder 依次是玩家第一张、敌人第一张、
    /// 玩家第二张、敌人第二张，之后是公共牌（翻牌 3、转牌 1、河牌 1）。
    /// 其它流程与随机局完全一致，随机局不会走这里。
    /// </summary>
    public void StartScriptedHand(
        int playerStartingChips,
        int enemyStartingChips,
        int buyIn,
        EnemyAIParameters parameters,
        System.Collections.Generic.List<Card> scriptedDrawOrder
    )
    {
        PrepareHand(playerStartingChips, enemyStartingChips, buyIn, parameters, scriptedDrawOrder);
    }

    private void PrepareHand(
        int playerStartingChips,
        int enemyStartingChips,
        int buyIn,
        EnemyAIParameters parameters,
        System.Collections.Generic.List<Card> scriptedDrawOrder
    )
    {
        // 发牌协程可能还在跑（教程会在手牌中途重开一手，或玩家快速连开），
        // 不掐掉的话上一个协程会把旧牌继续塞进新一手的公共牌区。
        StopAllCoroutines();

        // Save starting chips and enemy parameters for this hand.
        PlayerChips = playerStartingChips;
        EnemyChips = enemyStartingChips;
        BuyIn = buyIn;
        enemyParams = parameters;

        // Reset all per-hand state.
        Pot = 0;
        CurrentBet = 0;
        LastBetAmount = 0;
        LastBetByEnemy = false;
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
            LogMessage($"You cannot cover the {BuyIn}-chip buy-in — your run ends here.");
            State = PokerState.NotStarted;
            OnStateChanged?.Invoke();
            return;
        }

        // If the enemy can't afford the buy-in, the player advances by default.
        if (EnemyChips < BuyIn)
        {
            LogMessage($"The opponent is below the {BuyIn}-chip buy-in — level cleared.");
            State = PokerState.HandEnded;
            OnStateChanged?.Invoke();
            OnHandEnded?.Invoke();
            return;
        }

        // Collect buy-in from both players into the pot.
        State = PokerState.PayingBuyIn;
        PlayerChips -= BuyIn;
        EnemyChips -= BuyIn;
        Pot += BuyIn * 2;
        LogMessage($"You both put in {BuyIn} to sit down. Pot: {Pot}.");

        // Shuffle and deal 2 hole cards each (alternating draw order).
        deck = new Deck();
        deck.Initialize();
        deck.Shuffle();
        if (scriptedDrawOrder != null)
            deck.StackNext(scriptedDrawOrder);

        PlayerHand.Add(deck.Draw());
        EnemyHand.Add(deck.Draw());
        PlayerHand.Add(deck.Draw());
        EnemyHand.Add(deck.Draw());

        LogMessage($"Your hand: {CardsText(PlayerHand)}");

        swapsRemainingThisHand =
            PlayerInventory.Instance != null
                ? PlayerInventory.Instance.GetCount(ItemId.CardSwapTicket)
                : 0;
        // Begin the Pre-Flop betting round with the player acting first.
        CurrentStreet = Street.PreFlop;
        StartPlayerTurn();
    }

    /// <summary>
    /// 玩家花一次换牌机会，把第 index 张手牌换成新牌。
    /// </summary>
    public void PlayerSwapCard(int handIndex)
    {
        if (!CanSwapCard)
        {
            LogMessage("You cannot swap a card right now.");
            return;
        }
        if (handIndex < 0 || handIndex >= PlayerHand.Count)
        {
            LogMessage("That card cannot be swapped.");
            return;
        }

        Card oldCard = PlayerHand[handIndex];
        Card newCard = deck.Draw();
        PlayerHand[handIndex] = newCard;
        swapsRemainingThisHand--;

        LogMessage($"Swapped {CardText(oldCard)} for {CardText(newCard)}. Swaps left: {swapsRemainingThisHand}.", LogKind.Player);
        OnStateChanged?.Invoke();
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
            LogMessage("The opponent raised — match it or fold.");
            return;
        }

        LogMessage("You check.", LogKind.Player);

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
            LogMessage("The opponent raised — match it or fold.");
            return;
        }

        int maxRaise = GetPlayerMaxRaise();

        // 无法加注（对方没筹码了）→ 只能 Check
        if (maxRaise <= 0)
        {
            LogMessage("You cannot raise — one of the stacks is empty.");
            return;
        }

        // 加注额为 0 或负数 → 视为 Check
        if (amount <= 0)
        {
            LogMessage("No amount was set, so you check instead.");
            PlayerCheck();
            return;
        }

        // 超过上限 → 压到上限，而不是拒绝
        if (amount > maxRaise)
        {
            LogMessage($"The most you can raise is {maxRaise}, so you raise {maxRaise}.");
            amount = maxRaise;
        }

        // Deduct chips from player and add to pot.
        PlayerChips -= amount;
        Pot += amount;
        CurrentBet = amount;
        LastBetAmount = amount;
        LastBetByEnemy = false;
        playerHasRaised = true;

        playerRaiseCount++;
        playerTotalActions++;

        LogMessage($"You raise {amount}. Pot: {Pot}.", LogKind.Player);

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
            LogMessage("There is no raise to match.");
            return;
        }

        // 理论上不会走到这里，因为敌方加注已受玩家筹码限制
        // 保留为兜底：直接 Check 并推进，不 Fold
        if (PlayerChips < CurrentBet)
        {
            LogMessage($"You cannot cover {CurrentBet} with {PlayerChips} chips — you check instead.");
            AwaitingPlayerMatch = false;
            CurrentBet = 0;
            AdvanceStreet();
            return;
        }

        // Pay the match amount into the pot.
        PlayerChips -= CurrentBet;
        Pot += CurrentBet;
        LogMessage($"You match {CurrentBet}. Pot: {Pot}.", LogKind.Player);
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

        LogMessage($"You fold. The {Pot} in the pot goes to the opponent.", LogKind.Player);

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
                    LogMessage("The opponent cannot check against your raise — they fold.", LogKind.Enemy);
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
                LogMessage("The opponent is short on chips and checks.", LogKind.Enemy);
                EnemyChecks();
                return;
            }
            EnemyChips -= playerRaiseAmount;
            Pot += playerRaiseAmount;
            CurrentBet = playerRaiseAmount;
            LogMessage($"The opponent matches your raise of {playerRaiseAmount}. Pot: {Pot}.", LogKind.Enemy);

            // Betting resolved - advance to the next street.
            AdvanceStreet();
            return;
        }

        if (aiAmount > maxRaise)
        {
            // AI wants to raise more than it can afford - cap it at max.
            LogMessage($"The opponent puts in as much as they can: {maxRaise}.", LogKind.Enemy);
            aiAmount = maxRaise;
        }

        if (aiAmount <= playerRaiseAmount)
        {
            EnemyChips -= playerRaiseAmount;
            Pot += playerRaiseAmount;
            CurrentBet = playerRaiseAmount;
            LogMessage($"The opponent matches your raise of {playerRaiseAmount}. Pot: {Pot}.", LogKind.Enemy);
            AdvanceStreet();
            return;
        }

        // Execute the re-raise and give the turn back to the player.
        EnemyChips -= aiAmount;
        Pot += aiAmount;
        CurrentBet = aiAmount;
        LastBetAmount = aiAmount;
        LastBetByEnemy = true;
        AwaitingPlayerMatch = true;
        LogMessage($"The opponent raises {aiAmount}. Pot: {Pot} — match or fold.", LogKind.Enemy);
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
            LogMessage("The opponent cannot raise — one of the stacks is empty.", LogKind.Enemy);
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
            LogMessage($"The opponent puts in as much as they can: {maxRaise}.", LogKind.Enemy);
        }

        // Deduct chips from enemy and add to pot.
        EnemyChips -= amount;
        Pot += amount;
        CurrentBet = amount;
        LastBetAmount = amount;
        LastBetByEnemy = true;
        AwaitingPlayerMatch = true;

        LogMessage($"The opponent raises {amount}. Pot: {Pot} — match or fold.", LogKind.Enemy);

        // Turn goes back to the player.
        State = PokerState.PlayerTurn;
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Enemy checks - advance to the next street.
    /// </summary>
    private void EnemyChecks()
    {
        LogMessage("The opponent checks.", LogKind.Enemy);
        AdvanceStreet();
    }

    /// <summary>
    /// Enemy folds - player wins the pot immediately.
    /// </summary>
    private void EnemyFolds()
    {
        LogMessage($"The opponent folds. You take the {Pot} in the pot.", LogKind.Enemy);
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
        if (IsDealing)
        {
            Debug.LogWarning("[Poker] AdvanceStreet 被调用，但正在发牌中，忽略");
            return;
        }

        // Reset per-street betting state.
        CurrentBet = 0;
        LastBetAmount = 0;
        LastBetByEnemy = false;
        playerHasRaised = false;
        AwaitingPlayerMatch = false;

        IsDealing = true;
        State = PokerState.Dealing;
        OnStateChanged?.Invoke();

        // Move to the next street and deal cards accordingly.
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

                IsDealing = false;
                ResolveShowdown();
                break;
        }
    }

    /// <summary>
    /// Coroutine that reveals community cards one at a time with a delay.
    /// </summary>
    private IEnumerator DealCommunityCards(int count, Street nextStreet)
    {
        CurrentStreet = nextStreet;
        OnStateChanged?.Invoke();

        // Reveal each card with a delay between them.
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(dealDelay);
            Card c = deck.Draw();
            CommunityCards.Add(c);
            OnStateChanged?.Invoke();
        }

        IsDealing = false;
        StartPlayerTurn();
    }

    /// <summary>
    /// Sets the state to PlayerTurn and notifies listeners.
    /// </summary>
    private void StartPlayerTurn()
    {
        State = PokerState.PlayerTurn;
        string streetName = CurrentStreet.ToString().ToUpper();
        string tableCards = CardsText(CommunityCards);
        LogMessage(
            tableCards.Length > 0 ? $"{streetName}: {tableCards} — your move" : $"{streetName} — your move",
            LogKind.Street
        );
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

        // 牌型同名时（例如双方都是 Ace high）要靠牌面看出谁大，所以把五张牌一起写出来
        LogMessage($"Your best five: {PlayerBestHand.Describe()} — {CardsText(PlayerBestHand.bestFive)}");
        LogMessage($"Their best five: {EnemyBestHand.Describe()} — {CardsText(EnemyBestHand.bestFive)}");

        // Compare hands: >0 player wins, <0 enemy wins, 0 tie.
        int cmp = CompareHands(PlayerBestHand, EnemyBestHand);

        if (cmp == 0)
        {
            // Tie: settle immediately, no combat.
            int half = Pot / 2;
            PlayerChips += half;
            EnemyChips += Pot - half;
            Pot = 0;
            LogMessage("Tie — the pot is split.");
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
        LogMessage(
            Result == PokerResult.PlayerWinsShowdown
                ? $"You have the better hand. Pot {Pot} — win the duel to claim it."
                : $"The opponent has the better hand. Pot {Pot} — win the duel to take it back.",
            LogKind.Street
        );
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
        EnemyChips += Pot;
        Pot = 0;
        State = PokerState.HandEnded;
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

    // ========== Utility ==========

    /// <summary>
    /// Logs a message to the Unity console and notifies any UI listeners.
    /// </summary>
    private void LogMessage(string msg, LogKind kind = LogKind.System)
    {
        // Rich-text tags are for the table only; keep the console readable.
        Debug.Log($"[Poker] {Regex.Replace(msg, "<[^>]+>", "")}");
        OnMessage?.Invoke(msg, kind);
    }

    /// <summary>One card, coloured by suit the same way the table colours card faces.</summary>
    private static string CardText(Card c)
    {
        if (c == null)
            return "?";

        string hex;
        switch (c.Suit)
        {
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
                hex = "#EDEDED";
                break;
        }

        return $"<color={hex}>{c}</color>";
    }

    /// <summary>Cards separated by spaces, each coloured by suit.</summary>
    private static string CardsText(List<Card> cards)
    {
        if (cards == null || cards.Count == 0)
            return "";

        string s = "";
        for (int i = 0; i < cards.Count; i++)
        {
            if (i > 0)
                s += " ";
            s += CardText(cards[i]);
        }
        return s;
    }

}
