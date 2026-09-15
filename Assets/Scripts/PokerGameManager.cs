using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PokerGameManager : MonoBehaviour
{
    public PokerState State { get; private set; } = PokerState.NotStarted;
    public PokerResult Result { get; private set; } = PokerResult.None;
    public Street CurrentStreet { get; private set; } = Street.PreFlop;
    public EnemyAIParameters EnemyParams => enemyParams;
    public bool IsDealing { get; private set; } = false;

    public int PlayerChips { get; private set; }
    public int EnemyChips { get; private set; }
    public int Pot { get; private set; }
    public int CurrentBet { get; private set; }
    public int BuyIn { get; private set; }

    public List<Card> PlayerHand { get; private set; } = new List<Card>();
    public List<Card> EnemyHand { get; private set; } = new List<Card>();
    public List<Card> CommunityCards { get; private set; } = new List<Card>();

    public HandResult PlayerBestHand { get; private set; }
    public HandResult EnemyBestHand { get; private set; }

    private Deck deck;
    private EnemyAIParameters enemyParams;
    private bool playerHasRaised = false;
    public bool AwaitingPlayerMatch { get; private set; } = false;

    [Tooltip("Each community card deal delay （seconds）")]
    public float dealDelay = 0.6f;

    // ========== 事件 ==========
    public event Action OnStateChanged;
    public event Action<string> OnMessage;
    public event Action OnHandEnded;

    // ========== 初始化 ==========
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

        // 检查 buy-in
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

        // 支付 buy-in
        State = PokerState.PayingBuyIn;
        PlayerChips -= BuyIn;
        EnemyChips -= BuyIn;
        Pot += BuyIn * 2;
        LogMessage($"Both players paid the buy-in {BuyIn}，pot {Pot}");

        // 洗牌 + 发底牌
        deck = new Deck();
        deck.Initialize();
        deck.Shuffle();

        PlayerHand.Add(deck.Draw());
        EnemyHand.Add(deck.Draw());
        PlayerHand.Add(deck.Draw());
        EnemyHand.Add(deck.Draw());

        LogMessage($"Player hand: {CardListToString(PlayerHand)}");

        // 进入 Pre-Flop 下注
        CurrentStreet = Street.PreFlop;
        StartPlayerTurn();
    }

    // ========== 玩家操作 ==========

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
            LogMessage($"Invalid raise amount（current chips {PlayerChips}）");
            return;
        }

        PlayerChips -= amount;
        Pot += amount;
        CurrentBet = amount;
        playerHasRaised = true;

        LogMessage($"Player raises {amount}（{CurrentStreet}），pot {Pot}");

        State = PokerState.EnemyTurn;
        OnStateChanged?.Invoke();
        ResolveEnemyTurn(playerRaised: true, raiseAmount: amount);
    }

    public void PlayerMatchRaise()
    {
        if (!CanPlayerAct())
            return;
        if (!AwaitingPlayerMatch || CurrentBet <= 0)
        {
            LogMessage("Current there is no raise to match");
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
        LogMessage($"Player matches the raise {CurrentBet}，pot {Pot}");

        AwaitingPlayerMatch = false;
        AdvanceStreet();
    }

    public void PlayerFold()
    {
        if (State != PokerState.PlayerTurn)
            return;

        LogMessage($"Player folds，losing the pot {Pot}");
        EnemyChips += Pot;
        Pot = 0;
        AwaitingPlayerMatch = false;
        Result = PokerResult.EnemyWinsByFold;
        State = PokerState.PlayerFolded;
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

    private bool CanPlayerAct()
    {
        return State == PokerState.PlayerTurn && !IsDealing;
    }

    // ========== 敌人行动 ==========

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
            $"[AI] Hand Strength {decision.handStrength:F2}，Decision {decision.action}，Raise {decision.raiseAmount}"
        );

        switch (decision.action)
        {
            case EnemyAction.Fold:
                EnemyFolds();
                break;

            case EnemyAction.Check:
                if (playerRaised)
                {
                    LogMessage("Enemy cannot Check Player Raise，change to Fold");
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
                    // 玩家已加注，AI 的 Raise 是响应：要么匹配，要么反加
                    HandleEnemyResponse(decision.raiseAmount, raiseAmount);
                }
                else
                {
                    // AI 主动加注
                    EnemyInitiatesRaise(decision.raiseAmount);
                }
                break;
        }
    }

    // AI 响应玩家的加注
    private void HandleEnemyResponse(int aiAmount, int playerRaiseAmount)
    {
        if (aiAmount <= playerRaiseAmount)
        {
            // 匹配
            if (EnemyChips < playerRaiseAmount)
            {
                LogMessage("Enemy chips insufficient, change to Fold");
                EnemyFolds();
                return;
            }
            EnemyChips -= playerRaiseAmount;
            Pot += playerRaiseAmount;
            CurrentBet = playerRaiseAmount;
            LogMessage($"Enemy matches the raise {playerRaiseAmount}，Pot {Pot}");
            AdvanceStreet();
        }
        else
        {
            // 反加
            if (EnemyChips < aiAmount)
            {
                LogMessage("Enemy chips insufficient, change to Match");
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
            LogMessage($"Enemy raises {aiAmount}，Pot {Pot}。Player must Match or Fold。");
            State = PokerState.PlayerTurn;
            OnStateChanged?.Invoke();
        }
    }

    // AI 主动加注
    private void EnemyInitiatesRaise(int amount)
    {
        if (amount <= 0)
            amount = Mathf.Max(1, BuyIn / 2);

        if (EnemyChips < amount)
        {
            LogMessage("Enemy chips insufficient, change to Check");
            EnemyChecks();
            return;
        }

        EnemyChips -= amount;
        Pot += amount;
        CurrentBet = amount;
        AwaitingPlayerMatch = true;

        LogMessage($"Enemy raises {amount}，Pot {Pot}。Player must Match or Fold。");
        State = PokerState.PlayerTurn;
        OnStateChanged?.Invoke();
    }

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

        // 普通高牌
        result.handType = HandType.HighCard;
        int h = Mathf.Max(a.GetPokerValue(), b.GetPokerValue());
        int l = Mathf.Min(a.GetPokerValue(), b.GetPokerValue());
        result.tiebreakers = new int[] { (int)HandType.HighCard, h, l, 0, 0, 0 };
        return result;
    }

    private void EnemyChecks()
    {
        LogMessage($"Enemy checks（{CurrentStreet}）");
        AdvanceStreet();
    }

    private void EnemyFolds()
    {
        LogMessage($"Enemy folds，player wins the pot {Pot}");
        PlayerChips += Pot;
        Pot = 0;
        Result = PokerResult.PlayerWinsByFold;
        State = PokerState.EnemyFolded;
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

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
            LogMessage($"Enemy matches the raise {amount}，pot {Pot}");
            AdvanceStreet();
        }
        else
        {
            AwaitingPlayerMatch = true;
            LogMessage($"Enemy raises {amount}，pot {Pot}。Player must Match or Fold。");
            State = PokerState.PlayerTurn;
            OnStateChanged?.Invoke();
        }
    }

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
            LogMessage($"Flip open community cards：{c}");
            OnStateChanged?.Invoke();
        }

        CurrentStreet = nextStreet;
        IsDealing = false;
        StartPlayerTurn();
    }

    private void StartPlayerTurn()
    {
        State = PokerState.PlayerTurn;
        LogMessage($"—— {CurrentStreet} Player turn ——");
        OnStateChanged?.Invoke();
    }

    private void ResolveShowdown()
    {
        CurrentStreet = Street.Showdown;

        List<Card> playerAll = new List<Card>(PlayerHand);
        playerAll.AddRange(CommunityCards);
        PlayerBestHand = HandEvaluator.Evaluate(playerAll);

        List<Card> enemyAll = new List<Card>(EnemyHand);
        enemyAll.AddRange(CommunityCards);
        EnemyBestHand = HandEvaluator.Evaluate(enemyAll);

        LogMessage($"Player's best hand：{PlayerBestHand.GetDisplayName()}");
        LogMessage($"Enemy's best hand：{EnemyBestHand.GetDisplayName()}");

        int cmp = CompareHands(PlayerBestHand, EnemyBestHand);

        if (cmp > 0)
        {
            PlayerChips += Pot;
            LogMessage($"Showdown ：PlayerWins ，receive pot {Pot}");
            Result = PokerResult.PlayerWinsShowdown;
        }
        else if (cmp < 0)
        {
            EnemyChips += Pot;
            LogMessage($"Showdown ：EnemyWins ，receive pot {Pot}");
            Result = PokerResult.EnemyWinsShowdown;
        }
        else
        {
            int half = Pot / 2;
            PlayerChips += half;
            EnemyChips += Pot - half;
            LogMessage($"Showdown ：Tie ，split pot");
            Result = PokerResult.TieShowdown;
        }

        Pot = 0;
        State = PokerState.HandEnded;
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

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

    private void LogMessage(string msg)
    {
        Debug.Log($"[Poker] {msg}");
        OnMessage?.Invoke(msg);
    }

    private string CardListToString(List<Card> cards)
    {
        string s = "";
        foreach (Card c in cards)
            s += c.ToString() + " ";
        return s;
    }
}
