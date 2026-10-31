using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static partial class CombatVisualIntegration
{
    static void EvaluateRifleAimPose(Animator animator)
    {
        if (animator == null || !animator.isHuman || animator.runtimeAnimatorController == null)
            throw new InvalidOperationException("Rifle attachment requires an initialized Humanoid Animator.");

        Vector3 localPosition = animator.transform.localPosition;
        Quaternion localRotation = animator.transform.localRotation;
        animator.Rebind();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.SetBool("Grounded", true);
        animator.SetBool("Sprinting", false);
        animator.SetBool("Reloading", false);
        animator.SetBool("Dead", false);
        animator.SetFloat("Speed", 0f);
        animator.SetFloat("MoveX", 0f);
        animator.SetFloat("MoveY", 0f);
        animator.SetFloat("LocomotionRate", 1f);
        animator.Play("Base Layer.Locomotion", 0, 0f);
        int rifleLayer = animator.GetLayerIndex("Rifle Upper Body");
        if (rifleLayer < 0)
            throw new InvalidOperationException("The Humanoid controller has no rifle layer.");
        animator.SetLayerWeight(rifleLayer, 1f);
        animator.Play("Rifle Upper Body.Rifle Aim", rifleLayer, 0f);
        animator.Update(0f);
        animator.transform.localPosition = localPosition;
        animator.transform.localRotation = localRotation;
    }

    static void AlignWorldRifle(FpsCharacterController fighter, Animator animator,
        Transform rifle, Transform support)
    {
        EvaluateRifleAimPose(animator);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        if (hand == null || leftHand == null || rifle == null || support == null)
            throw new InvalidOperationException("Rifle attachment requires both Humanoid hands and grip transforms.");

        rifle.SetParent(hand, false);
        rifle.localScale = Vector3.one;
        Quaternion rotation = Quaternion.LookRotation(fighter.transform.forward, fighter.transform.up);
        Vector3 triggerGrip = new Vector3(.025f, -.055f, .015f);
        rifle.SetPositionAndRotation(hand.position - rotation * triggerGrip, rotation);
        support.SetParent(rifle, false);
        support.SetPositionAndRotation(leftHand.position, leftHand.rotation);
        EditorUtility.SetDirty(rifle);
        EditorUtility.SetDirty(support);
    }

    [MenuItem("Tools/Combat Visuals/Validate Rifle Alignment")]
    public static void ValidateRifleAlignment()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Exit Play mode before validating saved rifle attachments.");
        Directory.CreateDirectory(Work);
        var report = new StringBuilder();
        Scene scene = EditorSceneManager.NewPreviewScene();
        var lightObject = new GameObject("Rifle Alignment Light", typeof(Light));
        var cameraObject = new GameObject("Rifle Alignment Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(lightObject, scene);
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 2f;
        light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.scene = scene;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.14f, .18f, .23f);
        camera.nearClipPlane = .01f;
        camera.fieldOfView = 38f;

        string[] paths =
        {
            "Assets/prefabs/FPS/Player(FPS).prefab",
            "Assets/prefabs/FPS/enemies/Ranged.prefab",
            "Assets/prefabs/FPS/enemies/Melee.prefab",
            "Assets/prefabs/FPS/enemies/Dodge.prefab"
        };
        int failures = 0;
        try
        {
            foreach (string path in paths)
            {
                GameObject instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                SceneManager.MoveGameObjectToScene(instance, scene);
                try
                {
                    instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    foreach (Camera sourceCamera in instance.GetComponentsInChildren<Camera>(true))
                        sourceCamera.enabled = false;
                    FirstPersonRifleVisual viewmodel = instance.GetComponentInChildren<FirstPersonRifleVisual>(true);
                    if (viewmodel != null)
                        viewmodel.gameObject.SetActive(false);
                    CharacterAnimationDriver driver = instance.GetComponentInChildren<CharacterAnimationDriver>(true);
                    driver.ConfigureLocalPlayer(false);
                    driver.enabled = false;
                    Animator animator = driver.GetComponent<Animator>();
                    foreach (Renderer renderer in driver.GetComponentsInChildren<Renderer>())
                        renderer.shadowCastingMode = ShadowCastingMode.On;
                    Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    Transform rifle = hand.Find("World Rifle");
                    if (rifle == null)
                        throw new InvalidOperationException("Missing hand-attached rifle: " + path);
                    EvaluateRifleAimPose(animator);
                    string name = Path.GetFileNameWithoutExtension(path);
                    camera.transform.position = new Vector3(2.6f, 1.1f, 3.6f);
                    camera.transform.LookAt(new Vector3(0f, .15f, .25f));
                    Capture(camera, Work + "rifle-alignment-" + name + ".png");
                    foreach (string state in new[] { "Rifle Aim", "Rifle Fire" })
                    {
                        for (int phase = 0; phase < 3; phase++)
                        {
                            animator.Play("Rifle Upper Body." + state, 1, phase * .4f);
                            animator.Update(0f);
                            float forward = Vector3.Dot(rifle.forward, instance.transform.forward);
                            float up = Vector3.Dot(rifle.up, instance.transform.up);
                            bool pass = forward > .8f && up > .8f;
                            if (!pass)
                                failures++;
                            report.AppendLine((pass ? "PASS " : "FAIL ") + name + " " + state + " " +
                                (phase * .4f).ToString("F1") + ": barrel=" + forward.ToString("F3") +
                                "; upright=" + up.ToString("F3") + "; localRotation=" + rifle.localEulerAngles);
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            File.WriteAllText(Work + "rifle-alignment.txt", report.ToString());
        }
        if (failures > 0)
            throw new InvalidOperationException(failures + " rifle alignment checks failed. See " + Work + "rifle-alignment.txt");
    }
}
