using UnityEngine;

public class PokerGameTester : MonoBehaviour
{
    private PokerGameManager poker;
    private PokerUI ui;
    private EnemyAIParameters enemyParams;

    void Start()
    {
        poker = gameObject.AddComponent<PokerGameManager>();

        GameObject uiGO = new GameObject("PokerUI");
        uiGO.transform.SetParent(transform);
        ui = uiGO.AddComponent<PokerUI>();

        poker.OnHandEnded += () =>
        {
            Debug.Log($"Match Over：{poker.Result} | Player {poker.PlayerChips} | Enemy {poker.EnemyChips}");
        };

        // 生成第一局的敌人
        enemyParams = EnemyGenerator.GenerateRandom();
        Debug.Log($"[Enemy] {EnemyGenerator.Describe(enemyParams)}");

        ui.Initialize(poker);
        ui.SetNextHandCallback(StartNewHand);

        StartNewHand();
    }

    private void StartNewHand()
    {
        int playerChips = poker.PlayerChips > 0 ? poker.PlayerChips : 100;
        int enemyChips = poker.EnemyChips > 0 ? poker.EnemyChips : 100;

        // 如果这一局已经结束，就生成新敌人
        if (poker.State == PokerState.HandEnded ||
            poker.State == PokerState.PlayerFolded ||
            poker.State == PokerState.EnemyFolded ||
            poker.State == PokerState.NotStarted)
        {
            enemyParams = EnemyGenerator.GenerateRandom();
            Debug.Log($"[Enemy] New Enemy: {EnemyGenerator.Describe(enemyParams)}");
        }

        poker.StartNewHand(playerChips, enemyChips, buyIn: 10, parameters: enemyParams);
    }

    void Update()
    {
        if (poker == null) return;

        if (poker.State == PokerState.PlayerTurn)
        {
            if (Input.GetKeyDown(KeyCode.C)) poker.PlayerCheck();
            if (Input.GetKeyDown(KeyCode.R)) poker.PlayerRaise(10);
            if (Input.GetKeyDown(KeyCode.F)) poker.PlayerFold();
            if (Input.GetKeyDown(KeyCode.M)) poker.PlayerMatchRaise();
        }

        if (poker.State == PokerState.HandEnded ||
            poker.State == PokerState.PlayerFolded ||
            poker.State == PokerState.EnemyFolded)
        {
            if (Input.GetKeyDown(KeyCode.Space)) StartNewHand();
        }
    }
}