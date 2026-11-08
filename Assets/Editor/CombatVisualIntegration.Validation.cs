using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static partial class CombatVisualIntegration
{
    static FpsCharacterController testPlayer;
    static FpsCharacterController testEnemy;
    static double testStarted;
    static int testStage;
    static int observedShots;
    static bool sawAirborne;
    static StringBuilder testReport;

    [InitializeOnLoadMethod]
    static void ResumeValidation()
    { EditorApplication.playModeStateChanged += ValidationStateChanged; }

    [MenuItem("Tools/Combat Visuals/Run Play Validation")]
    static partial void BeginPlayValidation()
    {
        Directory.CreateDirectory(Work);
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play validation requires edit mode.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before validation.");
        SessionState.SetBool("CombatVisualValidation", true);
        EditorApplication.isPlaying = true;
    }
    static void ValidationStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("CombatVisualValidation", false))
        {
            testStarted = EditorApplication.timeSinceStartup;
            testStage = 0; testReport = new StringBuilder(); observedShots = 0; sawAirborne = false;
            Application.logMessageReceived += CaptureTestLog;
            EditorApplication.update += TickValidation;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= TickValidation;
            Application.logMessageReceived -= CaptureTestLog;
            SessionState.SetBool("CombatVisualValidation", false);
        }
    }
    static void CaptureTestLog(string message, string trace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
            testReport?.AppendLine("ERROR " + message + "\n" + trace);
    }
    static void Check(bool condition, string description)
    { if (!condition) throw new InvalidOperationException("FAIL " + description); testReport.AppendLine("PASS " + description); }

    static void TickValidation()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
        double elapsed = EditorApplication.timeSinceStartup - testStarted;
        try
        {
            if (testStage == 0 && elapsed > 1)
            {
                // Only Play-mode copies are deactivated. Exiting play restores the user's scene.
                foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()) root.SetActive(false);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Visual Validation Ground"; ground.transform.position = new Vector3(0,-1.55f,0); ground.transform.localScale = new Vector3(100,1,100);
                var light = new GameObject("Validation Light", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 2; light.transform.rotation = Quaternion.Euler(40,-25,0);
                testPlayer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/FPS/Player(FPS).prefab"), Vector3.zero, Quaternion.identity).GetComponent<FpsCharacterController>();
                var input = testPlayer.GetComponent<FpsPlayerInputHandler>(); if (input != null) input.enabled = false;
                var settings = UnityEngine.Object.Instantiate(testPlayer.EquippedWeapon.Settings); settings.magazineSize = 3; settings.damage = 0; settings.reloadTime = 1; testPlayer.EquippedWeapon.SetSettings(settings);
                testPlayer.EquippedWeapon.SetCombatLocked(false);
                testPlayer.EquippedWeapon.OnShotFired += () => observedShots++;
                testEnemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/FPS/enemies/Ranged.prefab"), new Vector3(0,0,5), Quaternion.Euler(0,180,0)).GetComponent<FpsCharacterController>();
                var ai = testEnemy.GetComponent<FPSAIEngine>(); if (ai != null) ai.enabled = false;
                foreach (var cam in testEnemy.GetComponentsInChildren<Camera>()) cam.enabled = false;
                testStarted = EditorApplication.timeSinceStartup; testStage = 1;
            }
            else if (testStage == 1 && elapsed > .7)
            {
                Check(testPlayer.IsGrounded, "player grounded on unchanged controller capsule");
                Check(testPlayer.GetComponentInChildren<Animator>().isHuman, "Western player avatar is Humanoid");
                ValidateRuntimeRifleVisuals(testPlayer, testEnemy);
                var camera = testPlayer.CameraTransform.GetComponentInChildren<Camera>(); Capture(camera, Work + "play-idle.png");
                testPlayer.EquippedWeapon.Fire();
                Check(observedShots == 1 && testPlayer.EquippedWeapon.AmmoInMagazine == 2, "accepted shot consumes one round and emits one visual event");
                testStarted = EditorApplication.timeSinceStartup; testStage = 2;
            }
            else if (testStage == 2 && elapsed > .12)
            {
                Capture(testPlayer.CameraTransform.GetComponentInChildren<Camera>(), Work + "play-fire.png");
                Check(testPlayer.EquippedWeapon.TryReload(), "reload starts through existing weapon API");
                testStarted = EditorApplication.timeSinceStartup; testStage = 3;
            }
            else if (testStage == 3 && elapsed > .5)
            {
                var vm = testPlayer.GetComponentInChildren<FirstPersonRifleVisual>();
                Check(testPlayer.EquippedWeapon.IsReloading && vm.transform.localPosition.y < -.5f, "rifle and hands lower during reload");
                Capture(testPlayer.CameraTransform.GetComponentInChildren<Camera>(), Work + "play-reload.png");
                testStage = 4;
            }
            else if (testStage == 4 && elapsed > 1.6)
            {
                var vm = testPlayer.GetComponentInChildren<FirstPersonRifleVisual>();
                Check(!testPlayer.EquippedWeapon.IsReloading && testPlayer.EquippedWeapon.AmmoInMagazine == 3 && vm.transform.localPosition.y > -.1f, "hands return only after weapon reload completes");
                Check(observedShots == 1, "reload does not emit a phantom shot");
                var oneRound = UnityEngine.Object.Instantiate(testPlayer.EquippedWeapon.Settings);
                oneRound.magazineSize = 1; testPlayer.EquippedWeapon.SetSettings(oneRound);
                testPlayer.EquippedWeapon.Fire();
                Check(observedShots == 2 && testPlayer.EquippedWeapon.IsReloading, "last round emits a shot before automatic reload");
                testStarted = EditorApplication.timeSinceStartup; testStage = 41;
            }
            else if (testStage == 41 && elapsed > .12)
            {
                var animator = testPlayer.GetComponentInChildren<CharacterAnimationDriver>().GetComponent<Animator>();
                Check(animator.GetCurrentAnimatorStateInfo(1).IsName("Rifle Fire") || animator.GetNextAnimatorStateInfo(1).IsName("Rifle Fire"), "last-round rifle firing animation plays during reload start");
                testStage = 42;
            }
            else if (testStage == 42 && elapsed > 1.5)
            {
                var animator = testPlayer.GetComponentInChildren<CharacterAnimationDriver>().GetComponent<Animator>();
                Check(observedShots == 2 && !animator.GetCurrentAnimatorStateInfo(1).IsName("Rifle Fire"), "automatic reload finishes without a stale firing animation");
                testPlayer.SetMoveInput(new Vector2(1,1)); testPlayer.SetSprint(true);
                testStarted = EditorApplication.timeSinceStartup; testStage = 5;
            }
            else if (testStage == 5 && elapsed > .5)
            {
                var animator = testPlayer.GetComponentInChildren<CharacterAnimationDriver>().GetComponent<Animator>();
                Check(testPlayer.HorizontalSpeed > .5f && animator.GetFloat("Speed") > .5f && animator.GetFloat("MoveX") > .1f && animator.GetBool("Sprinting"), "directional movement and sprint reach Animator parameters");
                testPlayer.RequestJump();
                testStarted = EditorApplication.timeSinceStartup; testStage = 6;
            }
            else if (testStage == 6)
            {
                if (!testPlayer.IsGrounded) sawAirborne = true;
                if (elapsed > .25)
                {
                    var animator = testPlayer.GetComponentInChildren<CharacterAnimationDriver>().GetComponent<Animator>();
                    Check(sawAirborne && !animator.GetBool("Grounded"), "jump/airborne state follows actual controller");
                    testStage = 7;
                }
            }
            else if (testStage == 7 && elapsed > 2)
            {
                testPlayer.Stop(); testPlayer.SetMoveInput(Vector2.zero); testPlayer.SetSprint(false);
                Check(sawAirborne && testPlayer.IsGrounded, "jump returns to grounded landing");
                testEnemy.GetComponent<Health>().TakeDamage(1);
                testStarted = EditorApplication.timeSinceStartup; testStage = 8;
            }
            else if (testStage == 8 && elapsed > .12)
            {
                var animator = testEnemy.GetComponentInChildren<CharacterAnimationDriver>().GetComponent<Animator>();
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Hit Reaction") || animator.GetNextAnimatorStateInfo(0).IsName("Hit Reaction"), "enemy damage plays hit reaction");
                testEnemy.GetComponent<Health>().TakeDamage(int.MaxValue);
                testStarted = EditorApplication.timeSinceStartup; testStage = 9;
            }
            else if (testStage == 9 && elapsed > 1)
            {
                var animator = testEnemy.GetComponentInChildren<CharacterAnimationDriver>().GetComponent<Animator>();
                Check(animator.GetBool("Dead") && animator.GetCurrentAnimatorStateInfo(0).IsName("Death") && animator.GetLayerWeight(1) == 0, "death overrides rifle pose and plays nonlooping death");
                foreach (string type in new[] { "Melee", "Dodge" })
                {
                    var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/FPS/enemies/" + type + ".prefab"), new Vector3(20,0,20), Quaternion.identity);
                    Check(instance.GetComponentInChildren<Animator>().isHuman && instance.GetComponentInChildren<CharacterAnimationDriver>() != null, type + " prefab has valid animated Western character");
                    UnityEngine.Object.Destroy(instance);
                }
                PrepareShotActors(testPlayer, testEnemy);
                testStarted = EditorApplication.timeSinceStartup;
                testStage = 10;
            }
            else if (testStage == 10 && elapsed > .2)
            {
                ValidateShotFeatures(testPlayer, testEnemy);
                FinishValidation();
            }
            if (elapsed > 20) throw new TimeoutException("Visual play validation timed out at stage " + testStage);
        }
        catch (Exception error) { testReport.AppendLine(error.ToString()); FinishValidation(); }
    }
    static void FinishValidation()
    {
        File.WriteAllText(Work + "play-validation.txt", testReport.ToString());
        EditorApplication.update -= TickValidation;
        Application.logMessageReceived -= CaptureTestLog;
        Debug.Log("Combat visual Play validation finished. See Temp/AssetIntegration/play-validation.txt.");
        EditorApplication.isPlaying = false;
    }
}


