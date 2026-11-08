using UnityEditor;
using UnityEngine;

/// <summary>Read-only diagnostics. No scene components or gameplay hooks required.</summary>
public sealed class FpsAiAccuracyDebugWindow : EditorWindow
{
    [SerializeField] private FPSPersonality previewPersonality;
    [SerializeField] private int previewLevel = 1;
    private Vector2 scroll;
    private double nextRefresh;

    [MenuItem("Tools/FPS/AI Accuracy Debug")]
    private static void Open()
    {
        GetWindow<FpsAiAccuracyDebugWindow>("FPS AI Accuracy");
    }

    private void OnEnable()
    {
        minSize = new Vector2(380f, 420f);
        EditorApplication.update += Refresh;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Refresh;
    }

    private void Refresh()
    {
        if (EditorApplication.timeSinceStartup < nextRefresh)
            return;
        nextRefresh = EditorApplication.timeSinceStartup + 0.2;
        Repaint();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("FPS AI Accuracy", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Read-only diagnostics. Lower aim wobble and firing tolerance mean tighter aim. " +
            "These are angular settings, not a measured hit percentage.", MessageType.Info);

        DrawLiveStats();
        EditorGUILayout.Space(12f);
        DrawPreview();
        EditorGUILayout.EndScrollView();
    }

    private static void DrawLiveStats()
    {
        EditorGUILayout.LabelField("Live encounter", EditorStyles.boldLabel);
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play mode in the FPS scene to see live AI stats.", MessageType.Info);
            return;
        }

        CombatSceneController[] encounters = Object.FindObjectsByType<CombatSceneController>(
            FindObjectsInactive.Exclude);
        FPSAIEngine[] enemies = Object.FindObjectsByType<FPSAIEngine>(
            FindObjectsInactive.Include);

        if (enemies.Length == 0)
            EditorGUILayout.HelpBox("Waiting for an FPS AI to spawn.", MessageType.Info);

        foreach (FPSAIEngine enemy in enemies)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("AI", enemy, typeof(FPSAIEngine), true);

                // Read the captured encounter level, rather than the manager's next level.
                // SerializedObject lets this editor-only tool inspect private state without
                // adding an accessor or changing the combat controller.
                int level = 0;
                foreach (CombatSceneController encounter in encounters)
                {
                    if (encounter.gameObject.scene != enemy.gameObject.scene)
                        continue;
                    SerializedProperty property = new SerializedObject(encounter).FindProperty("currentLevel");
                    if (property != null)
                        level = property.intValue;
                    break;
                }

                Row("Encounter level", level > 0 ? level.ToString() : "Unavailable (no initialized encounter)");
                Row("State", enemy.CurrentState.ToString());
                Row("AI active", enemy.isActiveAndEnabled ? "Yes" : "No");

                FPSPersonality personality = enemy.personality;
                if (personality == null)
                {
                    EditorGUILayout.HelpBox("No personality assigned.", MessageType.Warning);
                    continue;
                }

                Row("Personality", personality.personalityName);
                DrawAccuracy(personality.aimError, personality.fireAngleTolerance);
                Row("Turn speed", $"{personality.aimTurnSpeed:F1} deg/s");
                Row("Reaction delay", $"{personality.reactionTime:F3} s");
                Row("Target lead", $"{Mathf.Clamp01(personality.targetLeadAccuracy):P1}");
                Row("Wobble frequency", $"{personality.aimErrorFrequency:F2}");

                // Resolve the actual drivers, including serialized references on other objects.
                SerializedObject serializedEnemy = new SerializedObject(enemy);
                FpsAiAim aim = serializedEnemy.FindProperty("aim")?.objectReferenceValue as FpsAiAim;
                FpsAiTrigger trigger = serializedEnemy.FindProperty("trigger")?.objectReferenceValue as FpsAiTrigger;
                FpsAiPerception perception = serializedEnemy.FindProperty("perception")?.objectReferenceValue as FpsAiPerception;
                bool visible = perception != null && perception.CanSeeTarget;
                Row("Target visible", visible ? "Yes" : "No");
                Row("Reaction complete", aim != null && aim.HasReacted ? "Yes" : "No");
                Row("Current aim error", visible && aim != null && aim.HasReacted
                    ? $"{aim.CurrentAimError:F3} deg" : "N/A (no reacted visible target)");
                Row("Aim ready to fire", aim != null && aim.CanFireAtTarget ? "Yes" : "No");
                Row("Trigger held", trigger != null && trigger.IsFiring ? "Yes" : "No");
            }
        }
    }

    private void DrawPreview()
    {
        EditorGUILayout.LabelField("Level preview", EditorStyles.boldLabel);
        previewPersonality = (FPSPersonality)EditorGUILayout.ObjectField(
            "Base personality asset", previewPersonality, typeof(FPSPersonality), false);
        previewLevel = Mathf.Max(1, EditorGUILayout.IntField("Preview level", previewLevel));
        if (previewPersonality == null)
        {
            EditorGUILayout.HelpBox("Select the enemy prefab's base FPS personality asset to preview level scaling.", MessageType.Info);
            return;
        }

        // Mirrors CombatSceneController.ApplyEnemyLevelProgression. Preview only;
        // never clone, assign, or modify the source personality.
        int extraLevels = previewLevel - 1;
        float aimError = previewPersonality.aimError;
        float tolerance = previewPersonality.fireAngleTolerance;
        if (extraLevels > 0)
        {
            aimError = Mathf.Max(Mathf.Min(0.3f, aimError), aimError * Mathf.Pow(0.88f, extraLevels));
            tolerance = Mathf.Max(Mathf.Min(2.5f, tolerance), tolerance * Mathf.Pow(0.96f, extraLevels));
        }

        Row("Base aim wobble", $"{previewPersonality.aimError:F3} deg");
        Row("Base firing tolerance", $"{previewPersonality.fireAngleTolerance:F3} deg");
        DrawAccuracy(aimError, tolerance);
        EditorGUILayout.HelpBox(
            "Each level after 1 multiplies base wobble by 0.88 and base firing tolerance by 0.96. " +
            "Floors are 0.3 deg and 2.5 deg respectively (or the base value if already lower). " +
            "Turn speed, reaction delay, target lead and wobble frequency do not scale with level. " +
            "Preview mirrors the current combat progression formula.", MessageType.None);
    }

    private static void DrawAccuracy(float aimError, float tolerance)
    {
        Row("Aim wobble (level-scaled)", $"{aimError:F3} deg");
        Row("Firing tolerance (level-scaled)", $"{tolerance:F3} deg");
    }

    private static void Row(string label, string value)
    {
        EditorGUILayout.LabelField(label, value);
    }
}
