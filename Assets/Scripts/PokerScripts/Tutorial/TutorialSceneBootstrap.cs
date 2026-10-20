using UnityEngine;

/// <summary>
/// TutorialScene 的入口：不读也不写 GameFlowManager 的经济，固定 200/200 筹码，
/// 只负责把牌桌 UI 接起来，然后把控制权交给 TutorialDirector。
/// </summary>
public class TutorialSceneBootstrap : MonoBehaviour
{
    private PokerGameManager poker;
    private PokerUI ui;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        poker = FindAnyObjectByType<PokerGameManager>();
        ui = FindAnyObjectByType<PokerUI>();

        if (poker == null)
        {
            Debug.LogError("[TutorialSceneBootstrap] 场景里没有 PokerGameManager");
            return;
        }

        if (ui == null)
        {
            GameObject uiGO = new GameObject("PokerUI");
            uiGO.transform.SetParent(transform);
            ui = uiGO.AddComponent<PokerUI>();
        }

        // 教程里不换牌、没有战斗桥，也不碰玩家背包。
        ui.Initialize(poker);
        ui.SetNextHandCallback(null);
        ui.SetNextHandVisible(false);

        TutorialDirector director = gameObject.AddComponent<TutorialDirector>();
        director.Initialize(ui, poker);

        Debug.Log("[Tutorial] 教程场景启动：5 课，可 Skip");
    }
}
