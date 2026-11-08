using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fits the generic WRAD skeleton to two rifle grips without a Humanoid avatar or
/// changes to the imported model. The rifle and arms share the same motion pivot.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class WradRifleGripPose : MonoBehaviour
{
    [Serializable]
    private struct RestBone
    {
        public Transform bone;
        public Quaternion rotation;
    }

    [SerializeField] private Transform armsRoot;
    [Tooltip("Palm position at the trigger/stock grip. +Z points down the barrel, +Y points to the rifle top.")]
    [SerializeField] private Transform rightGrip;
    [Tooltip("Palm position under the fore-end. +Z points down the barrel, +Y points to the rifle top.")]
    [SerializeField] private Transform leftGrip;
    [SerializeField, HideInInspector] private RestBone[] restBones = Array.Empty<RestBone>();
    private Dictionary<string, Transform> bonesByName;
    private Dictionary<Transform, Quaternion> rotationsByBone;
    private static readonly string[,,] FingerNames = CreateFingerNames();
    private float triggerPulse;

    /// <summary>Creates an editable, serialized grip pose on a scene/prefab instance.</summary>
    public static WradRifleGripPose BakePose(Transform modelRoot, Transform triggerGrip, Transform supportGrip)
    {
        if (modelRoot == null) return null;
        WradRifleGripPose pose = modelRoot.GetComponent<WradRifleGripPose>();
        if (pose == null) pose = modelRoot.gameObject.AddComponent<WradRifleGripPose>();
        pose.Configure(modelRoot, triggerGrip, supportGrip);
        pose.ApplyPose();
        return pose;
    }

    public void Configure(Transform modelRoot, Transform triggerGrip, Transform supportGrip)
    {
        RestoreRestPose();
        armsRoot = modelRoot;
        rightGrip = triggerGrip;
        leftGrip = supportGrip;
        Transform[] bones = armsRoot != null ? armsRoot.GetComponentsInChildren<Transform>(true) : Array.Empty<Transform>();
        restBones = new RestBone[Mathf.Max(0, bones.Length - 1)];
        int index = 0;
        foreach (Transform bone in bones)
        {
            if (bone == armsRoot) continue;
            restBones[index++] = new RestBone { bone = bone, rotation = bone.localRotation };
        }
        CacheBones();
    }

    private void Awake() => ApplyPose();

    public void PressTrigger() => triggerPulse = 1f;

    private void LateUpdate()
    {
        if (Time.deltaTime <= 0f) return;
        ApplyPose();
        triggerPulse = Mathf.MoveTowards(triggerPulse, 0f, Time.deltaTime * 9f);
    }

    [ContextMenu("Apply Rifle Grip Pose")]
    public void ApplyPose()
    {
        if (armsRoot == null || restBones == null || restBones.Length == 0) return;
        RestoreRestPose();
        CurlFingers(-1f, true);
        CurlFingers(1f, false);
        PoseArm(rightGrip, true);
        PoseArm(leftGrip, false);
    }

    private void RestoreRestPose()
    {
        if (restBones == null) return;
        foreach (RestBone rest in restBones)
            if (rest.bone != null) rest.bone.localRotation = rest.rotation;
    }

    private void CurlFingers(float direction, bool triggerHand)
    {
        int sideIndex = triggerHand ? 0 : 1;
        for (int i = 0; i < 4; i++)
        {
            bool trigger = triggerHand && i == 0;
            float proximal = trigger ? 22f + triggerPulse * 12f : 48f + i * 2f;
            float intermediate = trigger ? 35f + triggerPulse * 10f : 65f;
            float distal = trigger ? 18f : 38f;
            Curl(FingerNames[sideIndex, i, 0], proximal * direction);
            Curl(FingerNames[sideIndex, i, 1], intermediate * direction);
            Curl(FingerNames[sideIndex, i, 2], distal * direction);
        }
        Curl(FingerNames[sideIndex, 4, 0], 12f * direction);
        Curl(FingerNames[sideIndex, 4, 1], 24f * direction);
        Curl(FingerNames[sideIndex, 4, 2], 18f * direction);
    }

    private void Curl(string name, float degrees)
    {
        Transform bone = FindBone(name);
        if (bone != null) bone.localRotation *= Quaternion.AngleAxis(degrees, Vector3.forward);
    }

    private void PoseArm(Transform grip, bool triggerHand)
    {
        if (grip == null) return;
        Transform upper = FindBone(triggerHand ? "bicep.r" : "bicep.l");
        Transform lower = FindBone(triggerHand ? "forearm.r" : "forearm.l");
        Transform wrist = FindBone(triggerHand ? "wrist.r" : "wrist.l");
        if (upper == null || lower == null || wrist == null) return;

        // WRAD's palm lies in its local Y/Z plane. The right palm faces inward
        // against the stock; the supporting left palm faces up under the fore-end.
        Vector3 fingers = triggerHand ? (grip.forward + grip.up * 0.35f).normalized : grip.right;
        Vector3 thumbSide = triggerHand ? Vector3.ProjectOnPlane(grip.up, fingers).normalized : grip.forward;
        Quaternion wristRotation = Quaternion.LookRotation(thumbSide, fingers);
        // Halfway from wrist to knuckles, in the model's actual imported units.
        Vector3 palmOffset = Vector3.Scale(new Vector3(0f, 0.0048f, 0f), wrist.lossyScale);
        Vector3 wristPosition = grip.position - wristRotation * palmOffset;
        Vector3 elbowPole = upper.position + grip.right * (triggerHand ? 0.4f : -0.4f)
            - grip.up * 0.55f - grip.forward * 0.15f;
        SolveTwoBone(upper, lower, wrist, wristPosition, elbowPole);
        wrist.rotation = wristRotation;

        // Twist bones carry forearm weights separately from the wrist. Share its
        // axial twist so the supporting hand's roll doesn't pinch the skin.
        Quaternion wristDelta = wrist.localRotation * Quaternion.Inverse(RestRotation(wrist));
        Quaternion twist = new Quaternion(0f, wristDelta.y, 0f, wristDelta.w);
        float norm = Mathf.Sqrt(twist.y * twist.y + twist.w * twist.w);
        if (norm > 0.0001f)
        {
            twist = new Quaternion(0f, twist.y / norm, 0f, twist.w / norm);
            Transform twist0 = FindBone(triggerHand ? "forearm.Twist0.r" : "forearm.Twist0.l");
            Transform twist1 = FindBone(triggerHand ? "forearm.Twist1.r" : "forearm.Twist1.l");
            if (twist0 != null) twist0.localRotation *= Quaternion.Slerp(Quaternion.identity, twist, 0.33f);
            if (twist1 != null) twist1.localRotation *= Quaternion.Slerp(Quaternion.identity, twist, 0.33f);
        }
    }

    private static void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
    {
        Vector3 start = upper.position;
        float upperLength = Vector3.Distance(start, lower.position);
        float lowerLength = Vector3.Distance(lower.position, end.position);
        Vector3 toTarget = target - start;
        float targetDistance = toTarget.magnitude;
        if (upperLength < 0.0001f || lowerLength < 0.0001f || targetDistance < 0.0001f) return;
        Vector3 forward = toTarget / targetDistance;
        float reach = Mathf.Clamp(targetDistance, Mathf.Abs(upperLength - lowerLength) + 0.0001f,
            upperLength + lowerLength - 0.0001f);
        Vector3 bend = Vector3.ProjectOnPlane(pole - start, forward).normalized;
        if (bend.sqrMagnitude < 0.001f) bend = Vector3.ProjectOnPlane(Vector3.down, forward).normalized;
        float along = (upperLength * upperLength + reach * reach - lowerLength * lowerLength) / (2f * reach);
        float away = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
        Vector3 elbow = start + forward * along + bend * away;
        Vector3 oldUpper = lower.position - start;
        Vector3 oldNormal = Vector3.Cross(oldUpper, end.position - lower.position);
        Vector3 newUpper = elbow - start;
        Vector3 newNormal = Vector3.Cross(newUpper, start + forward * reach - elbow);
        if (oldNormal.sqrMagnitude > 0.000001f && newNormal.sqrMagnitude > 0.000001f)
        {
            // Align the bend plane as well as the bone direction. A direction-only
            // swing can turn a forward elbow bend into a sideways skin collapse.
            Quaternion originalFrame = Quaternion.LookRotation(oldUpper, oldNormal);
            Quaternion targetFrame = Quaternion.LookRotation(newUpper, newNormal);
            upper.rotation = targetFrame * Quaternion.Inverse(originalFrame) * upper.rotation;
        }
        else
        {
            upper.rotation = Quaternion.FromToRotation(oldUpper, newUpper) * upper.rotation;
        }
        lower.rotation = Quaternion.FromToRotation(end.position - lower.position,
            start + forward * reach - lower.position) * lower.rotation;
    }

    private Quaternion RestRotation(Transform bone)
    {
        if (rotationsByBone == null) CacheBones();
        return rotationsByBone.TryGetValue(bone, out Quaternion rotation) ? rotation : Quaternion.identity;
    }

    private Transform FindBone(string name)
    {
        if (bonesByName == null) CacheBones();
        return bonesByName.TryGetValue(name, out Transform bone) ? bone : null;
    }

    private void CacheBones()
    {
        bonesByName = new Dictionary<string, Transform>(StringComparer.Ordinal);
        rotationsByBone = new Dictionary<Transform, Quaternion>();
        if (restBones == null) return;
        foreach (RestBone rest in restBones)
        {
            if (rest.bone == null) continue;
            bonesByName[rest.bone.name] = rest.bone;
            rotationsByBone[rest.bone] = rest.rotation;
        }
    }

    private static string[,,] CreateFingerNames()
    {
        var names = new string[2, 5, 3];
        string[] fingers = { "index", "middle", "ring", "pinky", "thumb" };
        for (int side = 0; side < 2; side++)
            for (int finger = 0; finger < 5; finger++)
                for (int joint = 0; joint < 3; joint++)
                    names[side, finger, joint] = "finger_" + fingers[finger] + (joint + 1) + (side == 0 ? ".r" : ".l");
        return names;
    }
}
