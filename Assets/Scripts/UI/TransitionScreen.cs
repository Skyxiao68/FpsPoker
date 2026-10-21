using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SimpleTransitions : MonoBehaviour
{
    // ================= EDIT THIS LIST ONLY =================
    // from   : scene you are leaving (file name, no folder)
    // scene  : scene you are going to
    // button : exact name of a Button that starts it      (optional)
    // watch  : exact name of an object that, when it appears, starts it (optional)
    // title  : text on the black screen, {level} = level number ("" = none)
    // level  : 0 = leave, 1 = reset to level 1, 2 = add 1
    class Rule
    {
        public string button, watch, from, scene, title;
        public int level;
        public bool wasPresent;
    }

        static readonly Rule[] Rules =
    {
        new Rule { button = "StartButton",  from = "StartScene", scene = "Card", title = "Level {level}", level = 1 },
        new Rule { watch  = "HandFinished", from = "Card",       scene = "FPS",  title = "Showdown" },
        new Rule { watch  = "FightWon",     from = "FPS",        scene = "Card", title = "Deadwood Deals" },
        new Rule { button = "DoneButton",                        title = "Level {level}", level = 2 },
    };
    // ================= END OF LIST =================

    // ---------- Look ----------
    const float FadeTime = 0.4f;
    const float HoldTime = 1f;
    static readonly bool FadeInAtLaunch = true;
    static readonly bool UseShadow = false;
    static readonly bool ShowDebug = true;                     
    const string FontName = "TransitionFont";                
    const string FontKeyword = "Durango";                      
    static readonly Color TextColour = new Color(0.72f, 0.45f, 0.20f);  // copper
    static readonly Color OutlineColour = new Color(0.16f, 0.06f, 0.02f);  // dark brown
    const float OutlineThickness = 0.006f;                                  // fraction of screen height
    const int TextSizeDivider = 6;                             // bigger number = smaller text

    static SimpleTransitions inst;
    static int level = 1;

    float alpha, scanTimer;
    string text = "", prevScene = "", fontStatus = "";
    bool busy, scanned, loadingByUs;
    Rule current;
    Texture2D black;
    GUIStyle style;
    Font font;
    readonly HashSet<Button> hooked = new HashSet<Button>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { inst = null; level = 1; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        var go = new GameObject("SimpleTransitions");
        DontDestroyOnLoad(go);
        inst = go.AddComponent<SimpleTransitions>();
    }

    public static void Load(string scene, string title)
    {
        if (inst == null || inst.busy) return;
        inst.StartCoroutine(inst.Run(new Rule { scene = scene, title = title }));
    }

    void Awake()
    {
        black = new Texture2D(1, 1);
        black.SetPixel(0, 0, Color.white);
        black.Apply();
        LoadFont();

        prevScene = SceneManager.GetActiveScene().name;
        SceneManager.sceneLoaded += OnSceneLoaded;
        if (FadeInAtLaunch) { alpha = 1f; StartCoroutine(Launch()); }
    }

    void OnDestroy() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    void LoadFont()
    {
        font = null;
        var all = Resources.LoadAll<Font>("");
        string names = "";

        foreach (var f in all)
        {
            names += "'" + f.name + "' ";
            bool exact = f.name == FontName;
            bool keyword = f.name.IndexOf(FontKeyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (font == null && (exact || keyword)) font = f;
        }

        fontStatus = font != null ? "font: " + font.name : "NO FONT. Found: " + names;
        Debug.Log("SimpleTransitions: " + fontStatus);
    }

    IEnumerator Launch()
    {
        busy = true;
        yield return null;
        yield return Fade(1f, 0f);
        busy = false;
    }

    // Runs whenever ANY scene finishes loading, including loads done by other scripts
    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        scanned = false;
        hooked.Clear();
        string from = prevScene;
        prevScene = s.name;
        if (loadingByUs) return;                       // our own transition handles it

        // Something else changed the scene: cover instantly, then fade in
        StopAllCoroutines();
        Rule r = current;
        bool applied = r != null;
        if (r == null)
            r = System.Array.Find(Rules, x => x.scene == s.name && (string.IsNullOrEmpty(x.from) || x.from == from));
        if (r != null) { if (!applied) ApplyLevel(r); text = Format(r.title); }
        else text = "";
        current = null;

        Time.timeScale = 1f;
        alpha = 1f;
        busy = true;
        StartCoroutine(RevealOnly());
    }

    IEnumerator RevealOnly()
    {
        yield return null;
        if (text != "") yield return new WaitForSecondsRealtime(HoldTime);
        yield return Fade(1f, 0f);
        text = "";
        busy = false;
    }

    void Update()
    {

        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer > 0f) return;
        scanTimer = 0.25f;

        foreach (var r in Rules)
        {
            if (!string.IsNullOrEmpty(r.button))
            {
                foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (b.name != r.button || !hooked.Add(b)) continue;

                    // Switch off any Inspector OnClick entries (these are usually the instant scene load)
                    for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                        b.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);

                    var rule = r;
                    b.onClick.AddListener(() => { if (!busy) StartCoroutine(Run(rule)); });
                    Debug.Log($"SimpleTransitions: hooked button '{b.name}'");
                }
            }

            if (!string.IsNullOrEmpty(r.watch))
            {
                bool present = GameObject.Find(r.watch) != null;
                if (!scanned) r.wasPresent = present;
                else if (present && !r.wasPresent && !busy) { r.wasPresent = true; StartCoroutine(Run(r)); }
                else if (!present) r.wasPresent = false;
            }
        }
        scanned = true;
    }

    IEnumerator Run(Rule r)
    {
        busy = true;
        current = r;
        ApplyLevel(r);
        text = Format(r.title);
        Debug.Log($"SimpleTransitions: going to '{r.scene}'");

        yield return Fade(0f, 1f);                     // cover the screen

        Time.timeScale = 1f;
        loadingByUs = true;
        var op = SceneManager.LoadSceneAsync(r.scene);
        if (op == null)
        {
            Debug.LogError($"SimpleTransitions: scene '{r.scene}' can't be loaded. Check the name and the Scene List.");
            loadingByUs = false; current = null;
            yield return Fade(1f, 0f);
            text = ""; busy = false;
            yield break;
        }
        while (!op.isDone) yield return null;
        yield return null;
        loadingByUs = false;
        current = null;

        if (text != "") yield return new WaitForSecondsRealtime(HoldTime);
        yield return Fade(1f, 0f);                     // reveal the new scene
        text = ""; busy = false;
    }

    void ApplyLevel(Rule r)
    {
        if (r.level == 1) level = 1;
        else if (r.level == 2) level++;
    }

    string Format(string t) => (t ?? "").Replace("{level}", level.ToString());

    IEnumerator Fade(float from, float to)
    {
        for (float t = 0; t < FadeTime; t += Time.unscaledDeltaTime)
        {
            alpha = Mathf.Lerp(from, to, t / FadeTime);
            yield return null;
        }
        alpha = to;
    }

    void OnGUI()
    {
        if (ShowDebug)
        {
            GUI.depth = -1001;
            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 1200, 30), "Transitions: " + fontStatus);

            if (!busy)
                for (int i = 0; i < Rules.Length; i++)
                    if (GUI.Button(new Rect(Screen.width - 190, 10 + i * 36, 180, 32),
                                   (i + 1) + ": to " + Rules[i].scene))
                        StartCoroutine(Run(Rules[i]));
        }

        if (alpha <= 0f) return;
        GUI.depth = -1000;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);

        if (string.IsNullOrEmpty(text)) return;

        if (style == null || (font != null && style.font != font))
        {
            style = new GUIStyle { alignment = TextAnchor.MiddleCenter };
            if (font != null) style.font = font; else style.fontStyle = FontStyle.Bold;
        }
        style.fontSize = Screen.height / TextSizeDivider;
        GUI.color = Color.white;

        float w = Screen.width, h = Screen.height;
        float o = Mathf.Max(2f, h * OutlineThickness);

        // Outline: draw the text in 8 directions around the middle
        style.normal.textColor = new Color(OutlineColour.r, OutlineColour.g, OutlineColour.b, alpha);
        for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                GUI.Label(new Rect(x * o, y * o, w, h), text, style);
            }

        // Copper text on top
        style.normal.textColor = new Color(TextColour.r, TextColour.g, TextColour.b, alpha);
        GUI.Label(new Rect(0, 0, w, h), text, style);
    }
}