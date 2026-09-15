using UnityEngine;

public class PokerGameTester : MonoBehaviour
{
    private PokerGameManager poker;
    private PokerUI ui;
    private EnemyAIParameters enemyParams;

    void Start()
    {
        
        poker = gameObject.AddComponent<PokerGameManager>();

        // 创建 UI
        GameObject uiGO = new GameObject("PokerUI");
        uiGO.transform.SetParent(transform);
        ui = uiGO.AddComponent<PokerUI>();

        poker.OnHandEnded += () =>
        {
            Debug.Log($"Match Over：{poker.Result} | Player {poker.PlayerChips} | Enemy {poker.EnemyChips}");
        };

        
        enemyParams = new EnemyAIParameters
        {
            enemyName = "RedHat",
            raiseTendency = 0.7f,
            foldTendency = 0.1f,
            combatStyle = "Aggressive"
        };

        
        ui.Initialize(poker);
        ui.SetNextHandCallback(StartNewHand);

        StartNewHand();
    }

    private void StartNewHand()
    {
        int playerChips = poker.PlayerChips > 0 ? poker.PlayerChips : 100;
        int enemyChips = poker.EnemyChips > 0 ? poker.EnemyChips : 100;
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