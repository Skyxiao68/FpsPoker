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
                if (level > 0)
                    Row("Skill tier", $"{FpsAiDifficulty.TierName(level)} ({FpsAiDifficulty.SkillForLevel(level):P0})");
                DrawSkillValues(personality);

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

        Row("Skill tier", $"{FpsAiDifficulty.TierName(previewLevel)} " +
            $"({FpsAiDifficulty.SkillForLevel(previewLevel):P0})");
        Row("Tier summary", FpsAiDifficulty.TierTrait(previewLevel));
        Row("Bonus health", $"+{25 * (previewLevel - 1)}");

        FPSPersonality scaled = FpsAiDifficulty.CreateForLevel(previewPersonality, previewLevel);
        try
        {
            DrawSkillValues(scaled);
        }
        finally
        {
            DestroyImmediate(scaled);
        }

        EditorGUILayout.HelpBox(
            $"Skill rises evenly from level 1 (base asset) to level {FpsAiDifficulty.MaxSkillLevel} " +
            "(expert) and holds there; health keeps adding 25 per level. Damage, move speed, " +
            "sprint and fire rate never scale. Uses FpsAiDifficulty, the same code the combat scene runs.",
            MessageType.None);
    }

    private static void DrawSkillValues(FPSPersonality personality)
    {
        Row("Reaction delay", $"{personality.reactionTime:F3} s");
        Row("Aim wobble", $"{personality.aimError:F3} deg");
        Row("Firing tolerance", $"{personality.fireAngleTolerance:F3} deg");
        Row("Shot discipline", $"{Mathf.Clamp01(personality.shotDiscipline):P0}");
        Row("Turn speed", $"{personality.aimTurnSpeed:F1} deg/s");
        Row("Target lead", $"{Mathf.Clamp01(personality.targetLeadAccuracy):P0}");
        Row("Recoil control", $"{Mathf.Clamp01(personality.recoilControl):P0}");
        Row("Head aim", $"{Mathf.Clamp01(personality.headAimBias):P0}");
        Row("Game sense", $"{Mathf.Clamp01(personality.gameSense):P0}");
        Row("Memory", $"{personality.memoryDuration:F1} s");
        Row("Field of view", $"{personality.fieldOfView:F0} deg");
        Row("Strafe", $"{personality.strafeAmplitude:F2} amp, {personality.strafeFrequency:F2} freq");
        Row("Decision interval", $"{personality.repathInterval:F3} s");
    }

    private static void Row(string label, string value)
    {
        EditorGUILayout.LabelField(label, value);
    }
}
