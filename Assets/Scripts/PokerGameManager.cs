using System;
using System.Collections.Generic;
using UnityEngine;

public class PokerGameManager : MonoBehaviour
{
   
    public PokerState State { get; private set; } = PokerState.NotStarted;
    public PokerResult Result { get; private set; } = PokerResult.None;

   
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
    private bool awaitingPlayerMatch = false;     

    
    public event Action OnStateChanged;
    public event Action<string> OnMessage;
    public event Action OnHandEnded;

    // ========== 初始化 ==========
    public void StartNewHand(int playerStartingChips, int enemyStartingChips, int buyIn, EnemyAIParameters parameters)
    {
        PlayerChips = playerStartingChips;
        EnemyChips = enemyStartingChips;
        BuyIn = buyIn;
        enemyParams = parameters;

        Pot = 0;
        CurrentBet = 0;
        playerHasRaised = false;
        awaitingPlayerMatch = false;
        Result = PokerResult.None;
        PlayerBestHand = null;
        EnemyBestHand = null;

        PlayerHand.Clear();
        EnemyHand.Clear();
        CommunityCards.Clear();

        // 检查 buy-in
        if (PlayerChips < BuyIn)
        {
            LogMessage("Player insufficient chips, cannot pay buy-in, game over");
            State = PokerState.NotStarted;
            OnStateChanged?.Invoke();
            return;
        }
        if (EnemyChips < BuyIn)
        {
            LogMessage("Enemy insufficient chips, player wins");
            State = PokerState.HandEnded;
            OnStateChanged?.Invoke();
            return;
        }

        // 支付 buy-in
        State = PokerState.PayingBuyIn;
        PlayerChips -= BuyIn;
        EnemyChips -= BuyIn;
        Pot += BuyIn * 2;
        LogMessage($"Both Parties pay buy-in {BuyIn}，Pot {Pot}");

        // 发牌
        State = PokerState.Dealing;
        deck = new Deck();
        deck.Initialize();
        deck.Shuffle();

        PlayerHand.Add(deck.Draw());
        EnemyHand.Add(deck.Draw());
        PlayerHand.Add(deck.Draw());
        EnemyHand.Add(deck.Draw());

        for (int i = 0; i < 5; i++)
            CommunityCards.Add(deck.Draw());

        LogMessage($"PlayerHand：{CardListToString(PlayerHand)}");
        LogMessage($"CommunityCards：{CardListToString(CommunityCards)}");

        State = PokerState.PlayerTurn;
        OnStateChanged?.Invoke();
    }

    

    public void PlayerCheck()
    {
        if (State != PokerState.PlayerTurn) return;

        
        if (awaitingPlayerMatch)
        {
            LogMessage("Enemy Raised，You can only Match or Fold");
            return;
        }

        LogMessage("Player Check");
        State = PokerState.EnemyTurn;
        OnStateChanged?.Invoke();
        ResolveEnemyTurn(playerRaised: false, raiseAmount: 0);
    }

    public void PlayerRaise(int amount)
    {
        if (State != PokerState.PlayerTurn) return;

        
        if (awaitingPlayerMatch)
        {
            LogMessage("Enemy Raised，Please use Match or Fold");
            return;
        }

        if (amount <= 0 || amount > PlayerChips)
        {
            LogMessage($"Raise amount invalid (Current chips {PlayerChips})");
            return;
        }

        PlayerChips -= amount;
        Pot += amount;
        CurrentBet = amount;
        playerHasRaised = true;

        LogMessage($"Player raises {amount}，Pot {Pot}");

        State = PokerState.EnemyTurn;
        OnStateChanged?.Invoke();
        ResolveEnemyTurn(playerRaised: true, raiseAmount: amount);
    }

    public void PlayerMatchRaise()
    {
        if (State != PokerState.PlayerTurn) return;

        if (!awaitingPlayerMatch || CurrentBet <= 0)
        {
            LogMessage("Currenty no raise to match");
            return;
        }

        if (PlayerChips < CurrentBet)
        {
            LogMessage("Chips not enough to match，Auto Fold"); 
            PlayerFold();
            return;
        }

        PlayerChips -= CurrentBet;
        Pot += CurrentBet;
        LogMessage($"Player matches raise {CurrentBet}，Pot {Pot}");

        awaitingPlayerMatch = false;
        State = PokerState.Showdown;
        OnStateChanged?.Invoke();
        ResolveShowdown();
    }

    public void PlayerFold()
    {
        if (State != PokerState.PlayerTurn) return;

        LogMessage($"Player Fold，lose pot {Pot}");
        EnemyChips += Pot;
        Pot = 0;
        awaitingPlayerMatch = false;
        Result = PokerResult.EnemyWinsByFold;
        State = PokerState.PlayerFolded;
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

    // ========== 敌人行动 ==========

    private void ResolveEnemyTurn(bool playerRaised, int raiseAmount)
    {
        List<Card> enemyAll = new List<Card>(EnemyHand);
        enemyAll.AddRange(CommunityCards);
        EnemyBestHand = HandEvaluator.Evaluate(enemyAll);

        EnemyAction action = EnemyPokerAI.Decide(
            enemyParams, EnemyBestHand, playerRaised, raiseAmount, EnemyChips);

        switch (action)
        {
            case EnemyAction.Fold:
                EnemyFolds();
                break;
            case EnemyAction.Check:
                
                if (playerRaised)
                {
                   
                    LogMessage("Enemy cannot Check player's raise, switching to Fold");
                    EnemyFolds();
                }
                else
                {
                    EnemyChecks();
                }
                break;
            case EnemyAction.Raise:
                EnemyRaises(raiseAmount);
                break;
        }
    }

    private void EnemyChecks()
    {
        LogMessage("Enemy Check");
        State = PokerState.Showdown;
        OnStateChanged?.Invoke();
        ResolveShowdown();
    }

    private void EnemyFolds()
    {
        LogMessage($"Enemy Fold，Player wins pot {Pot}");
        PlayerChips += Pot;
        Pot = 0;
        Result = PokerResult.PlayerWinsByFold;
        State = PokerState.EnemyFolded;
        OnStateChanged?.Invoke();
        OnHandEnded?.Invoke();
    }

    private void EnemyRaises(int amount)
    {
        if (amount <= 0) amount = Mathf.Max(1, BuyIn / 2);

        if (EnemyChips < amount)
        {
            
            LogMessage("Enemy chips not enough, cannot match, switching to Fold");
            EnemyFolds();
            return;
        }

        EnemyChips -= amount;
        Pot += amount;
        CurrentBet = amount;

        if (playerHasRaised)
        {
           
            LogMessage($"Enemy matches raise {amount}，Pot {Pot}");
            State = PokerState.Showdown;
            OnStateChanged?.Invoke();
            ResolveShowdown();
        }
        else
        {
            
            awaitingPlayerMatch = true;
            LogMessage($"Enemy raises {amount}，Pot {Pot}。Player must Match or Fold。");
            State = PokerState.PlayerTurn;
            OnStateChanged?.Invoke();
        }
    }

    

    private void ResolveShowdown()
    {
        List<Card> playerAll = new List<Card>(PlayerHand);
        playerAll.AddRange(CommunityCards);
        PlayerBestHand = HandEvaluator.Evaluate(playerAll);

        List<Card> enemyAll = new List<Card>(EnemyHand);
        enemyAll.AddRange(CommunityCards);
        EnemyBestHand = HandEvaluator.Evaluate(enemyAll);

        LogMessage($"PlayerBestHand：{PlayerBestHand.GetDisplayName()}");
        LogMessage($"EnemyBestHand：{EnemyBestHand.GetDisplayName()}");

        int cmp = CompareHands(PlayerBestHand, EnemyBestHand);

        if (cmp > 0)
        {
            PlayerChips += Pot;
            LogMessage($"ShowDown：PlayerWins，Receive Pot {Pot}");
            Result = PokerResult.PlayerWinsShowdown;
        }
        else if (cmp < 0)
        {
            EnemyChips += Pot;
            LogMessage($"ShowDown：EnemyWins，Receive Pot {Pot}");
            Result = PokerResult.EnemyWinsShowdown;
        }
        else
        {
            int half = Pot / 2;
            PlayerChips += half;
            EnemyChips += Pot - half;
            LogMessage($"ShowDown：Tie，Split Pot {Pot}");
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

    // ========== 工具 ==========
    private void LogMessage(string msg)
    {
        Debug.Log($"[Poker] {msg}");
        OnMessage?.Invoke(msg);
    }

    private string CardListToString(List<Card> cards)
    {
        string s = "";
        foreach (Card c in cards) s += c.ToString() + " ";
        return s;
    }
}