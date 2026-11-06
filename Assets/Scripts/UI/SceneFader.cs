using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 场景切换时的黑场淡入：新场景一加载就盖一张全屏黑图，再用 0.3 秒把它淡掉。
///
/// 为什么需要：测试反馈「一个屏幕直接跳到另一个屏幕，闪得眼睛疼」。
/// 只在进入场景时淡入，不需要改任何 LoadScene 调用点，所有场景自动生效。
/// 淡入期间整块图吃掉射线，避免玩家在黑屏时误点到还没准备好的一层 UI。
/// </summary>
public static class SceneFader
{
    private const float FadeSeconds = 0.30f;
    private static readonly Color FadeColor = Color.black;

    private sealed class Runner : MonoBehaviour
    {
        private Image veil;
        private float startedAt;

        private void Awake()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;          // 压过教程层(320)与暂停菜单(500)
            gameObject.AddComponent<CanvasScaler>();

            GameObject veilGO = new GameObject("Veil", typeof(RectTransform), typeof(Image));
            veilGO.transform.SetParent(transform, false);

            RectTransform rt = veilGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            veil = veilGO.GetComponent<Image>();
            veil.color = FadeColor;
            veil.raycastTarget = true;

            startedAt = Time.unscaledTime;
        }

        private void Update()
        {
            float t = (Time.unscaledTime - startedAt) / FadeSeconds;

            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            Color c = veil.color;
            c.a = 1f - Mathf.Clamp01(t);
            veil.color = c;

            // 透明度低到一定程度就不再挡点击了
            veil.raycastTarget = c.a > 0.35f;
        }
    }

    /// <summary>最近一次进入场景是否真的起了淡入（调试/自检用，不影响表现）。</summary>
    public static bool LastFadeStarted { get; private set; }

    /// <summary>
    /// 先淡到黑再加载场景：教程结束、主菜单进教程这类「跳章」用，
    /// 免得直接瞬切一屏。淡入淡出总时长仍是一次 FadeSeconds。
    /// </summary>
    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || !Application.isPlaying)
        {
            if (!string.IsNullOrEmpty(sceneName))
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            return;
        }

        if (!FadeInProgress)
        {
            FadeInProgress = true;
            GameObject go = new GameObject("SceneFaderOut");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<OutRunner>().SceneName = sceneName;
            return;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    private static bool FadeInProgress;

    private sealed class OutRunner : MonoBehaviour
    {
        public string SceneName;
        private Image veil;
        private float startedAt;
        private bool hasLoaded;

        private void Awake()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            gameObject.AddComponent<CanvasScaler>();

            GameObject veilGO = new GameObject("Veil", typeof(RectTransform), typeof(Image));
            veilGO.transform.SetParent(transform, false);

            RectTransform rt = veilGO.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            veil = veilGO.GetComponent<Image>();
            Color c = FadeColor;
            c.a = 0f;
            veil.color = c;
            veil.raycastTarget = true;

            startedAt = Time.unscaledTime;
        }

        private void Update()
        {
            float t = (Time.unscaledTime - startedAt) / FadeSeconds;
            Color c = veil.color;
            c.a = Mathf.Clamp01(t);
            veil.color = c;

            if (t >= 1f && !hasLoaded)
            {
                hasLoaded = true;
                FadeInProgress = false;
                UnityEngine.SceneManagement.SceneManager.LoadScene(SceneName);
                Destroy(gameObject);
            }
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateFader()
    {
        LastFadeStarted = true;
        GameObject go = new GameObject("SceneFader");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
    }
}
