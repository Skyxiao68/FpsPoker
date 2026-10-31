using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static partial class CombatVisualIntegration
{
    [MenuItem("Tools/Combat Visuals/Install Rifle and Character Animations")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before installing visuals.");
        Directory.CreateDirectory(Work);
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory(Output + "/Materials");
        AssetDatabase.Refresh();
        var controller = BuildController();
        string[] paths = { "Assets/prefabs/FPS/Player(FPS).prefab", "Assets/prefabs/FPS/enemies/Ranged.prefab", "Assets/prefabs/FPS/enemies/Melee.prefab", "Assets/prefabs/FPS/enemies/Dodge.prefab" };
        foreach (string path in paths)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { Integrate(root.GetComponent<FpsCharacterController>(), path.Contains("Player(FPS)"), controller); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        // Scene fighters are independent copies; they also need visuals, not only spawned prefabs.
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/FPS.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/FPS.unity", OpenSceneMode.Additive);
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (var fighter in root.GetComponentsInChildren<FpsCharacterController>(true))
                Integrate(fighter, fighter.GetComponent<FpsPlayerInputHandler>() != null, controller);
        InstallEnvironment(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Work + "installed.txt", "Rifle visuals and character animations installed in FPS scene and all four fighter prefabs.\n" + DateTime.Now);
        Validate();
    }

    static AnimationClip Clip(string relative)
    {
        string path = Animations + relative + ".fbx";
        var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) throw new InvalidOperationException("Missing animation: " + path);
        return clip;
    }
    static AnimatorController BuildController()
    {
        string path = Output + "/WesternRifle.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        // Keep asset GUIDs stable when rerunning the installer.
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        else
        {
            controller.layers = Array.Empty<AnimatorControllerLayer>();
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub != controller) UnityEngine.Object.DestroyImmediate(sub, true);
            controller.AddLayer("Base Layer");
        }
        foreach (string p in new[] { "MoveX", "MoveY", "Speed", "VerticalSpeed", "LocomotionRate" }) controller.AddParameter(p, AnimatorControllerParameterType.Float);
        foreach (string p in new[] { "Sprinting", "Grounded", "Reloading", "Dead" }) controller.AddParameter(p, AnimatorControllerParameterType.Bool);
        foreach (string p in new[] { "Fire", "Hit", "Jump", "Land" }) controller.AddParameter(p, AnimatorControllerParameterType.Trigger);
        var baseLayer = controller.layers[0]; baseLayer.defaultWeight = 1;
        var sm = baseLayer.stateMachine;
        var locomotion = sm.AddState("Locomotion");
        var speeds = new BlendTree { name = "Idle Walk Run", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(speeds, controller);
        speeds.AddChild(Clip("Idles/HumanM@Idle01"), 0);
        speeds.AddChild(Directional(controller, "Walk"), 2);
        speeds.AddChild(Directional(controller, "Run"), 4);
        locomotion.motion = speeds;
        locomotion.speedParameterActive = true; locomotion.speedParameter = "LocomotionRate";
        sm.defaultState = locomotion;
        var air = sm.AddState("Airborne"); air.motion = Clip("Idles/HumanM@Idle01"); air.speed = 0;
        Transition(locomotion, air, "Grounded", AnimatorConditionMode.IfNot, .1f);
        Transition(air, locomotion, "Grounded", AnimatorConditionMode.If, .1f);
        var hit = sm.AddState("Hit Reaction"); hit.motion = Clip("Combat/HumanM@Damage01");
        var hitTransition = sm.AddAnyStateTransition(hit); hitTransition.AddCondition(AnimatorConditionMode.If, 0, "Hit"); hitTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead"); hitTransition.duration = .06f; hitTransition.canTransitionToSelf = true;
        Exit(hit, locomotion, .8f, .12f);
        var death = sm.AddState("Death"); death.motion = Clip("Combat/HumanM@Death01");
        var deathTransition = sm.AddAnyStateTransition(death); deathTransition.AddCondition(AnimatorConditionMode.If, 0, "Dead"); deathTransition.duration = .08f; deathTransition.canTransitionToSelf = false;
        Transition(death, locomotion, "Dead", AnimatorConditionMode.IfNot, .1f);

        var mask = new AvatarMask { name = "Rifle Upper Body Mask" };
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (var part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers, AvatarMaskBodyPart.LeftHandIK, AvatarMaskBodyPart.RightHandIK }) mask.SetHumanoidBodyPartActive(part, true);
        AssetDatabase.AddObjectToAsset(mask, controller);
        controller.AddLayer("Rifle Upper Body");
        var layers = controller.layers;
        layers[0] = baseLayer;
        var upper = layers[1]; upper.defaultWeight = 1; upper.avatarMask = mask; upper.iKPass = true; upper.blendingMode = AnimatorLayerBlendingMode.Override;
        var aim = upper.stateMachine.AddState("Rifle Aim"); aim.motion = Clip("Combat/Rifle/HumanM@Rifle_Aim01"); upper.stateMachine.defaultState = aim;
        var fire = upper.stateMachine.AddState("Rifle Fire"); fire.motion = Clip("Combat/Rifle/HumanM@Rifle_Aim01_Shoot01");
        var shoot = upper.stateMachine.AddAnyStateTransition(fire); shoot.AddCondition(AnimatorConditionMode.If, 0, "Fire"); shoot.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead"); shoot.duration = .035f; shoot.canTransitionToSelf = true;
        Exit(fire, aim, .9f, .08f);
        layers[1] = upper; controller.layers = layers;
        EditorUtility.SetDirty(controller);
        return controller;
    }
    static BlendTree Directional(AnimatorController controller, string type)
    {
        var tree = new BlendTree { name = type + " Direction", blendType = BlendTreeType.SimpleDirectional2D, blendParameter = "MoveX", blendParameterY = "MoveY", useAutomaticThresholds = false };
        AssetDatabase.AddObjectToAsset(tree, controller);
        string[] directions = { "Forward", "ForwardRight", "Right", "BackwardRight", "Backward", "BackwardLeft", "Left", "ForwardLeft" };
        Vector2[] points = { Vector2.up, new Vector2(.707f,.707f), Vector2.right, new Vector2(.707f,-.707f), Vector2.down, new Vector2(-.707f,-.707f), Vector2.left, new Vector2(-.707f,.707f) };
        for (int i = 0; i < directions.Length; i++) tree.AddChild(Clip("Movement/" + type + "/HumanM@" + type + "01_" + directions[i]), points[i]);
        return tree;
    }
    static void Transition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float duration)
    { var t = from.AddTransition(to); t.hasExitTime = false; t.duration = duration; t.AddCondition(mode, 0, parameter); }
    static void Exit(AnimatorState from, AnimatorState to, float time, float duration)
    { var t = from.AddTransition(to); t.hasExitTime = true; t.exitTime = time; t.duration = duration; }

    static Transform Child(string name, Transform parent, Vector3 position = default)
    { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t; }
    static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
    { var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }

    static void Integrate(FpsCharacterController fighter, bool player, AnimatorController controller)
    {
        if (fighter == null) throw new InvalidOperationException("Fighter is missing its movement controller.");
        Weapon weapon = fighter.EquippedWeapon;
        // Remove only this installer's generated visual roots on reruns.
        foreach (Transform t in fighter.GetComponentsInChildren<Transform>(true).ToArray())
            if (t != null && (t.name == "Character Visuals" || t.name == "Rifle Viewmodel")) UnityEngine.Object.DestroyImmediate(t.gameObject);
        foreach (var r in fighter.GetComponentsInChildren<MeshRenderer>(true))
            if (r.GetComponentInParent<Canvas>() == null && r.GetComponent<ParticleSystem>() == null) r.enabled = false;

        var wrapper = Child("Character Visuals", fighter.transform, Vector3.down);
        string modelName = player ? "sheriff" : fighter.name.Contains("Dodge") ? "outlow" : "gunman";
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Western + "Low-PolyWesternStarterPack/Character/Prefabs/" + modelName + ".prefab"), wrapper);
        model.name = modelName; model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * 1.05f;
        ConvertMaterials(model);
        Animator animator = model.GetComponent<Animator>();
        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman) throw new InvalidOperationException(modelName + " must have a valid Humanoid avatar.");
        animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        EvaluateRifleAimPose(animator);
        model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        var worldRifle = CreateRifle(rightHand, "World Rifle");
        Quaternion forwardPose = fighter.transform.rotation;
        worldRifle.SetPositionAndRotation(rightHand.position, forwardPose);
        worldRifle.localScale = Vector3.one;
        var support = Child("Support Hand Grip", worldRifle);
        support.SetPositionAndRotation(leftHand.position, leftHand.rotation);
        AlignWorldRifle(fighter, animator, worldRifle, support);
        var muzzle = Child("Muzzle", worldRifle, new Vector3(0, .035f, 1.12f));
        var driver = model.AddComponent<CharacterAnimationDriver>();
        driver.Configure(fighter, animator, worldRifle, support, wrapper);
        driver.ConfigureLocalPlayer(player);
        InstallHitboxes(fighter, animator);
        if (player) foreach (Renderer r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        if (player) BuildFirstPerson(fighter, weapon);
        else WireMuzzle(weapon, muzzle);
        Set(weapon, "weaponModel", null);
        EditorUtility.SetDirty(fighter.gameObject);
    }

    static Transform CreateRifle(Transform parent, string name)
    {
        var root = Child(name, parent);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Western + "LowPolyWesternWeaponPack/Weapon/Prefabs/rifle.prefab");
        var mesh = UnityEngine.Object.Instantiate(prefab, root); mesh.name = "Western Rifle";
        mesh.transform.localPosition = Vector3.zero;
        mesh.transform.localRotation = Quaternion.Euler(0, 90, 0); // Model muzzle points along -X.
        mesh.transform.localScale = Vector3.one;
        foreach (var r in mesh.GetComponentsInChildren<Renderer>()) r.enabled = true;
        return root;
    }

    static void BuildFirstPerson(FpsCharacterController fighter, Weapon weapon)
    {
        var camera = fighter.CameraTransform.GetComponentInChildren<Camera>(true);
        if (camera == null) throw new InvalidOperationException("Local player camera is missing.");
        var motion = Child("Rifle Viewmodel", camera.transform);
        var rifle = CreateRifle(motion, "First Person Rifle"); rifle.localPosition = new Vector3(.18f, -.24f, .23f);
        var muzzle = Child("Muzzle", rifle, new Vector3(0, .035f, 1.12f));
        var arms = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/WRAD_ARMS/arms.fbx"), motion);
        arms.name = "WRAD Arms"; arms.transform.localScale = Vector3.one * .11f; arms.transform.localPosition = new Vector3(0, -.20f, .14f); arms.transform.localRotation = Quaternion.Euler(0, 210, 0);
        WradRifleMeshUtility.PrepareFirstPersonArms(arms.transform);
        foreach (var a in arms.GetComponentsInChildren<Animator>()) UnityEngine.Object.DestroyImmediate(a);
        foreach (Renderer r in arms.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        foreach (Renderer r in rifle.GetComponentsInChildren<Renderer>()) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
        var rightGrip = Child("Right Hand Grip", rifle, new Vector3(.025f, -.055f, .015f));
        var leftGrip = Child("Left Hand Grip", rifle, new Vector3(-.025f, -.035f, .36f));
        // WRAD grip solver and fingers are configured by the companion runtime component.
        var pose = motion.gameObject.AddComponent<WradRifleGripPose>();
        pose.Configure(arms.transform, rightGrip, leftGrip);
        pose.ApplyPose();
        var visual = motion.gameObject.AddComponent<FirstPersonRifleVisual>();
        visual.Configure(weapon, fighter, fighter.GetComponent<Health>(), motion, camera.transform);
        WireMuzzle(weapon, muzzle);
    }

    static void WireMuzzle(Weapon weapon, Transform muzzle)
    {
        Set(weapon, "firePoint", muzzle);
        var old = new SerializedObject(weapon).FindProperty("muzzleFlash").objectReferenceValue as ParticleSystem;
        if (old != null) old.gameObject.SetActive(false);
        var flash = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/prefabs/visual/muzzleflash.prefab"), muzzle);
        flash.name = "Rifle Muzzle Flash"; flash.transform.localPosition = Vector3.zero; flash.transform.localRotation = Quaternion.identity; flash.transform.localScale = Vector3.one * .05f;
        Set(weapon, "muzzleFlash", flash.GetComponent<ParticleSystem>());
    }

    static void ConvertMaterials(GameObject model)
    {
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                var source = mats[i]; if (source == null || source.shader.name.StartsWith("Universal Render Pipeline/")) continue;
                string path = Output + "/Materials/" + source.name + "_URP.mat";
                var converted = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (converted == null)
                {
                    converted = new Material(Shader.Find("Universal Render Pipeline/Lit")); converted.name = source.name + " URP";
                    if (source.HasProperty("_MainTex")) converted.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
                    if (source.HasProperty("_Color")) converted.SetColor("_BaseColor", source.GetColor("_Color"));
                    converted.SetFloat("_Smoothness", .15f);
                    AssetDatabase.CreateAsset(converted, path);
                }
                mats[i] = converted;
            }
            r.sharedMaterials = mats;
        }
    }

    [MenuItem("Tools/Combat Visuals/Validate Scene Setup")]
    static void Validate()
    {
        Directory.CreateDirectory(Work);
        var report = new StringBuilder();
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Output + "/WesternRifle.controller");
        if (controller == null) throw new InvalidOperationException("Missing generated controller.");
        report.AppendLine("Controller: " + controller.name + "; clips=" + controller.animationClips.Length);
        foreach (var c in UnityEngine.Object.FindObjectsByType<FpsCharacterController>(FindObjectsInactive.Include))
        {
            if (EditorUtility.IsPersistent(c)) continue;
            var d = c.GetComponentInChildren<CharacterAnimationDriver>(true);
            if (d == null) throw new InvalidOperationException("Missing driver: " + c.name);
            var a = d.GetComponent<Animator>();
            if (!a.isHuman || a.applyRootMotion || a.runtimeAnimatorController != controller) throw new InvalidOperationException("Invalid animation setup: " + c.name);
            if (c.EquippedWeapon.FirePoint == null || c.EquippedWeapon.FirePoint.name != "Muzzle") throw new InvalidOperationException("Missing rifle muzzle: " + c.name);
            report.AppendLine("PASS " + c.name + ": humanoid, root motion disabled, rifle attached, muzzle wired.");
        }
        File.WriteAllText(Work + "validation.txt", report.ToString());
    }
    static void Preview()
    {
        // Filled by the QA partial; images are written outside Assets.
        RenderPreviews();
    }
    static partial void RenderPreviews();
    static partial void BeginPlayValidation();
}




