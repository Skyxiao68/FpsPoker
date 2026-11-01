using UnityEngine;

/// <summary>
/// 教程进度：只记一件事 —— 玩家是否已经通过（或跳过）教程。
/// 过了就永不再自动播；教程入口留到以后做，这里只负责这个开关。
/// </summary>
public static class TutorialProgress
{
    // 0 / 缺省 = 还没看过；1 = 已通过或已跳过。
    private const string Key = "poker.tutorial";

    public static bool IsDone
    {
        get { return PlayerPrefs.GetInt(Key, 0) != 0; }
    }

    public static void MarkDone()
    {
        PlayerPrefs.SetInt(Key, 1);
        PlayerPrefs.Save();
    }

    public static void Reset()
    {
        PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save();
    }
}
