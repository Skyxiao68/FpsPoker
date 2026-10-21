#if UNITY_EDITOR || DEBUG
using UnityEngine;

/// <summary>
/// 开发期的键盘测试输入。
///
/// 只在 Editor 和 Development Build 里编译；
/// 正式发布构建时整个文件会被 C# 预处理器剔除。
///
/// 快捷键：
///   C     = Check
///   R     = Raise 10
///   F     = Fold
///   M     = Match
///   Space = 开新局（牌局结束后）
/// </summary>
public class PokerDebugInput : MonoBehaviour
{
    [SerializeField]
    private PokerSceneBootstrap bootstrap;

    [SerializeField]
    private PokerGameManager poker;

    private void Awake()
    {
        // 如果没手动指定，就在场景里找
        if (bootstrap == null)
            bootstrap = FindAnyObjectByType<PokerSceneBootstrap>();
        if (poker == null)
            poker = FindAnyObjectByType<PokerGameManager>();
    }

    private void Update()
    {
        if (poker == null)
            return;

        // 玩家行动快捷键（仅玩家回合生效）
        if (poker.State == PokerState.PlayerTurn)
        {
            if (Input.GetKeyDown(KeyCode.C))
                poker.PlayerCheck();
            if (Input.GetKeyDown(KeyCode.R))
                poker.PlayerRaise(10);
            if (Input.GetKeyDown(KeyCode.F))
                poker.PlayerFold();
            if (Input.GetKeyDown(KeyCode.M))
                poker.PlayerMatchRaise();
        }

        // 牌局结束后，按 Space 开新局
        if (
            poker.State == PokerState.HandEnded
            || poker.State == PokerState.PlayerFolded
            || poker.State == PokerState.EnemyFolded
        )
        {
            if (Input.GetKeyDown(KeyCode.Space) && bootstrap != null)
                bootstrap.StartNewHand();
        }
    }
}
#endif
