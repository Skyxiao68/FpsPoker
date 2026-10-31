using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class CombatVisualIntegration
{
    [MenuItem("Tools/Combat Visuals/Apply Combat Improvements")]
    public static void ApplyCombatImprovements()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before updating combat assets.");
        Directory.CreateDirectory(Work);
        foreach (string path in new[] { "Assets/prefabs/FPS/Player(FPS).prefab", "Assets/prefabs/FPS/enemies/Ranged.prefab", "Assets/prefabs/FPS/enemies/Melee.prefab", "Assets/prefabs/FPS/enemies/Dodge.prefab" })
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try { ImproveFighter(root.GetComponent<FpsCharacterController>()); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Scene scene = SceneManager.GetSceneByPath("Assets/Scenes/FPS.unity");
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/FPS.unity", OpenSceneMode.Additive);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var fighter in root.GetComponentsInChildren<FpsCharacterController>(true)) ImproveFighter(fighter);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (opened) EditorSceneManager.CloseScene(scene, true);
        AssetDatabase.SaveAssets();
        File.WriteAllText(Work + "combat-improvements.txt", "Updated rifle attachments and animated head/body hitboxes in FPS and all fighter prefabs.");
        ValidateRifleAlignment();
    }

    static void ImproveFighter(FpsCharacterController fighter)
    {
        var driver = fighter.GetComponentInChildren<CharacterAnimationDriver>(true);
        if (driver == null) throw new InvalidOperationException("Missing animated body: " + fighter.name);
        Animator animator = driver.GetComponent<Animator>();
        Transform rifle = animator.GetBoneTransform(HumanBodyBones.RightHand).Find("World Rifle");
        if (rifle == null) throw new InvalidOperationException("Missing world rifle: " + fighter.name);
        AlignWorldRifle(fighter, animator, rifle, rifle.Find("Support Hand Grip"));
        InstallHitboxes(fighter, animator);
    }

    static void InstallHitboxes(FpsCharacterController fighter, Animator animator)
    {
        foreach (CombatHitbox existing in fighter.GetComponentsInChildren<CombatHitbox>(true))
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        Health owner = fighter.GetComponent<Health>();
        var regions = new List<CombatHitbox>();
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        var headObject = Child("Head Hitbox", head).gameObject;
        headObject.layer = owner.gameObject.layer;
        Vector3 headCenter = head.position + fighter.transform.up * .105f;
        var sphere = headObject.AddComponent<SphereCollider>();
        sphere.center = headObject.transform.InverseTransformPoint(headCenter);
        sphere.radius = .155f / HitboxScale(headObject.transform);
        var headRegion = headObject.AddComponent<CombatHitbox>(); headRegion.Configure(owner, true); regions.Add(headRegion);

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        Vector3 torsoBottom = hips.position - fighter.transform.up * .12f;
        Vector3 torsoTop = headCenter - fighter.transform.up * .17f;
        var torsoObject = Child("Torso Hitbox", chest).gameObject;
        torsoObject.layer = owner.gameObject.layer;
        torsoObject.transform.rotation = fighter.transform.rotation;
        var torso = torsoObject.AddComponent<BoxCollider>();
        torso.center = torsoObject.transform.InverseTransformPoint((torsoTop + torsoBottom) * .5f);
        torso.size = new Vector3(.43f, Mathf.Max(.3f, Vector3.Distance(torsoTop, torsoBottom)), .28f) / HitboxScale(torsoObject.transform);
        var torsoRegion = torsoObject.AddComponent<CombatHitbox>(); torsoRegion.Configure(owner, false); regions.Add(torsoRegion);
        foreach (var side in new[] { true, false })
        {
            var arm = side ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
            var forearm = side ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
            var hand = side ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            var thigh = side ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
            var shin = side ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
            var foot = side ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
            AddLimbHitbox(owner, animator, arm, forearm, .08f, regions);
            AddLimbHitbox(owner, animator, forearm, hand, .065f, regions);
            AddLimbHitbox(owner, animator, thigh, shin, .115f, regions);
            AddLimbHitbox(owner, animator, shin, foot, .09f, regions);
        }
        var rig = fighter.GetComponent<CharacterHitboxes>();
        if (rig == null) rig = fighter.gameObject.AddComponent<CharacterHitboxes>();
        rig.Configure(regions.ToArray());
        EditorUtility.SetDirty(rig);
    }

    static void AddLimbHitbox(Health owner, Animator animator, HumanBodyBones startId, HumanBodyBones endId, float radius, List<CombatHitbox> regions)
    {
        Transform start = animator.GetBoneTransform(startId);
        Transform end = animator.GetBoneTransform(endId);
        if (start == null || end == null) return;
        var limb = Child(startId + " Hitbox", start).gameObject;
        limb.layer = owner.gameObject.layer;
        limb.transform.rotation = Quaternion.FromToRotation(Vector3.up, end.position - start.position);
        float scale = HitboxScale(limb.transform);
        var capsule = limb.AddComponent<CapsuleCollider>();
        capsule.center = limb.transform.InverseTransformPoint((start.position + end.position) * .5f);
        capsule.radius = radius / scale;
        capsule.height = Vector3.Distance(start.position, end.position) / scale + radius * 1.2f / scale;
        var region = limb.AddComponent<CombatHitbox>(); region.Configure(owner, false); regions.Add(region);
    }

    static float HitboxScale(Transform bone)
    {
        Vector3 scale = bone.lossyScale;
        return Mathf.Max(.001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
    }
}
