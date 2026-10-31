using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Presents the existing FPS controller through a retargeted humanoid Animator.
/// Movement, damage, firing and reload timing remain owned by the gameplay scripts.
/// Place this component on the Animator object so Unity calls OnAnimatorIK.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(120)]
public sealed class CharacterAnimationDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FpsCharacterController character;
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;
    [Tooltip("Optional wrapper above the Animator. Only this visual wrapper receives landing offsets.")]
    [SerializeField] private Transform visualMotionRoot;
    [SerializeField] private Transform rifleVisual;
    [SerializeField] private Transform supportGrip;
    [Tooltip("Hide the world body and rifle from a nearby first-person camera, retaining their shadows.")]
    [SerializeField] private bool localPlayer;
    [Min(0.1f)] [SerializeField] private float firstPersonHideDistance = 1.7f;

    [Header("Rifle attachment")]
    [SerializeField] private bool attachRifleToHand;
    [SerializeField] private Vector3 rifleLocalPosition;
    [SerializeField] private Vector3 rifleLocalEulerAngles;

    [Header("Animation response")]
    [Min(0f)] [SerializeField] private float locomotionDamping = 0.1f;
    [SerializeField] private string rifleLayerName = "Rifle Upper Body";
    [Range(0f, 1f)] [SerializeField] private float sprintRifleLayerWeight = 0.3f;
    [Min(0f)] [SerializeField] private float hitIKReleaseTime = 0.2f;

    [Header("Humanoid aiming")]
    [SerializeField] private bool useAimIK = true;
    [Range(0f, 1f)] [SerializeField] private float rightHandAimWeight = 1f;
    [Range(0f, 1f)] [SerializeField] private float supportHandWeight = 1f;
    [Range(0f, 1f)] [SerializeField] private float headLookWeight = 0.65f;
    [Range(1f, 80f)] [SerializeField] private float maximumAimPitch = 65f;
    [Range(0f, 70f)] [SerializeField] private float reloadLowerAngle = 40f;
    [Min(0.1f)] [SerializeField] private float poseResponse = 12f;

    [Header("Procedural airborne pose")]
    [SerializeField] private bool useAirbornePose = true;
    [Range(0f, 50f)] [SerializeField] private float airborneThighAngle = 24f;
    [Range(0f, 100f)] [SerializeField] private float airborneKneeAngle = 48f;
    [Range(0f, 0.2f)] [SerializeField] private float landingDip = 0.08f;
    [Min(0.05f)] [SerializeField] private float landingRecovery = 0.22f;

    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int LocomotionRate = Animator.StringToHash("LocomotionRate");
    private static readonly int Sprinting = Animator.StringToHash("Sprinting");
    private static readonly int Grounded = Animator.StringToHash("Grounded");
    private static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
    private static readonly int Reloading = Animator.StringToHash("Reloading");
    private static readonly int Dead = Animator.StringToHash("Dead");
    private static readonly int Fire = Animator.StringToHash("Fire");
    private static readonly int Hit = Animator.StringToHash("Hit");
    private static readonly int Jump = Animator.StringToHash("Jump");
    private static readonly int Land = Animator.StringToHash("Land");

    private readonly Dictionary<int, AnimatorControllerParameterType> parameters =
        new Dictionary<int, AnimatorControllerParameterType>();
    private RuntimeAnimatorController cachedController;
    private Weapon subscribedWeapon;
    private Health subscribedHealth;
    private int rifleLayer = -1;
    private bool hasGroundSample;
    private bool wasGrounded;
    private bool wasDead;
    private float previousVerticalSpeed;
    private float airborneTime;
    private float airborneBlend;
    private float landingAmount;
    private float ikWeight;
    private float reloadBlend;
    private float releaseIKUntil;
    private Vector3 visualLocalPosition;
    private Quaternion visualLocalRotation;
    private bool capturedVisualPose;
    private bool attachedRifle;
    private Renderer[] bodyRenderers;
    private ShadowCastingMode[] originalShadowModes;
    private bool bodyHidden;
    private Camera viewingCamera;

    /// <summary>Editor integration can configure a generated visual without exposing gameplay state.</summary>
    public void Configure(FpsCharacterController owner, Animator visualAnimator,
        Transform rifle = null, Transform leftHandGrip = null, Transform motionRoot = null)
    {
        Unsubscribe();
        RestoreVisualPose();
        character = owner;
        animator = visualAnimator != null ? visualAnimator : GetComponent<Animator>();
        health = owner != null ? owner.GetComponent<Health>() : null;
        rifleVisual = rifle;
        supportGrip = leftHandGrip;
        visualMotionRoot = motionRoot;
        attachedRifle = false;
        capturedVisualPose = false;
        cachedController = null;
        ResolveReferences();
        CacheAnimatorParameters();
        CacheVisibilityRenderers();
        if (isActiveAndEnabled)
            Subscribe();
    }

    /// <summary>World bodies stay visible to the result camera and cast shadows in first person.</summary>
    public void ConfigureLocalPlayer(bool isLocalPlayer)
    {
        RestoreVisibility();
        localPlayer = isLocalPlayer;
        CacheVisibilityRenderers();
    }

    private void Awake()
    {
        ResolveReferences();
        CacheAnimatorParameters();
        CacheVisibilityRenderers();
    }

    private void OnEnable()
    {
        ResolveReferences();
        hasGroundSample = false;
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        RestoreVisualPose();
        RestoreVisibility();
    }

    private void ResolveReferences()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
        if (character == null)
            character = GetComponentInParent<FpsCharacterController>();
        if (health == null && character != null)
            health = character.GetComponent<Health>();

        // Animations are cosmetic; root motion must never move the FPS controller.
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        if (!capturedVisualPose && visualMotionRoot != null &&
            (character == null || visualMotionRoot != character.transform))
        {
            visualLocalPosition = visualMotionRoot.localPosition;
            visualLocalRotation = visualMotionRoot.localRotation;
            capturedVisualPose = true;
        }
    }

    private void Subscribe()
    {
        if (subscribedHealth != health)
        {
            if (subscribedHealth != null)
            {
                subscribedHealth.OnDamaged -= HandleDamage;
                subscribedHealth.OnDeath -= HandleDeath;
            }
            subscribedHealth = health;
            if (subscribedHealth != null)
            {
                subscribedHealth.OnDamaged += HandleDamage;
                subscribedHealth.OnDeath += HandleDeath;
            }
        }

        Weapon equipped = character != null ? character.EquippedWeapon : null;
        if (subscribedWeapon == equipped)
            return;
        if (subscribedWeapon != null)
            subscribedWeapon.OnShotFired -= HandleShot;
        subscribedWeapon = equipped;
        if (subscribedWeapon != null)
            subscribedWeapon.OnShotFired += HandleShot;
    }

    private void Unsubscribe()
    {
        if (subscribedHealth != null)
        {
            subscribedHealth.OnDamaged -= HandleDamage;
            subscribedHealth.OnDeath -= HandleDeath;
        }
        if (subscribedWeapon != null)
            subscribedWeapon.OnShotFired -= HandleShot;
        subscribedHealth = null;
        subscribedWeapon = null;
    }

    private void CacheAnimatorParameters()
    {
        parameters.Clear();
        cachedController = animator != null ? animator.runtimeAnimatorController : null;
        rifleLayer = -1;
        if (animator == null || cachedController == null)
            return;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
            parameters[parameter.nameHash] = parameter.type;
        if (!string.IsNullOrEmpty(rifleLayerName))
            rifleLayer = animator.GetLayerIndex(rifleLayerName);
    }

    private void Update()
    {
        if (character == null || animator == null || animator.runtimeAnimatorController == null)
            return;
        if (cachedController != animator.runtimeAnimatorController)
            CacheAnimatorParameters();
        Subscribe();
        AttachRifleIfRequested();

        bool dead = health != null && health.IsDead;
        bool controllerActive = character.isActiveAndEnabled;
        bool grounded = !controllerActive || character.IsGrounded;
        bool sprinting = controllerActive && character.IsSprinting && character.HorizontalSpeed > 0.1f;
        bool reloading = subscribedWeapon != null && subscribedWeapon.IsReloading;
        Vector3 velocity = dead || !controllerActive ? Vector3.zero : character.Velocity;
        Vector3 localVelocity = character.transform.InverseTransformDirection(velocity);
        Vector2 direction = new Vector2(localVelocity.x, localVelocity.z);
        float speed = direction.magnitude;
        direction = speed > 0.05f ? direction / speed : Vector2.zero;

        if (wasDead && !dead)
        {
            // Health.ResetHealth has no event. Rebind when its state changes to restore a fallen rig.
            animator.Rebind();
            animator.Update(0f);
            hasGroundSample = false;
            airborneBlend = 0f;
            landingAmount = 0f;
        }

        SetFloat(MoveX, direction.x, locomotionDamping);
        SetFloat(MoveY, direction.y, locomotionDamping);
        SetFloat(Speed, speed, locomotionDamping);
        // The run clip covers roughly 4 m/s. Scale only the locomotion state
        // above that pace so high movement/sprint stats do not slide the feet.
        SetFloat(LocomotionRate, Mathf.Max(1f, speed / 4f), locomotionDamping);
        SetFloat(VerticalSpeed, velocity.y, 0f);
        SetBool(Sprinting, sprinting && !dead);
        SetBool(Grounded, grounded);
        SetBool(Reloading, reloading && !dead);
        SetBool(Dead, dead);

        if (!dead && hasGroundSample)
        {
            if (wasGrounded && !grounded && velocity.y > 0.5f)
                SetTrigger(Jump);
            if (!wasGrounded && grounded)
            {
                SetTrigger(Land);
                if (airborneTime > 0.15f)
                    landingAmount = Mathf.Clamp01(Mathf.Abs(previousVerticalSpeed) / 8f);
            }
        }
        airborneTime = grounded ? 0f : airborneTime + Time.deltaTime;
        airborneBlend = Mathf.MoveTowards(airborneBlend,
            !grounded && !dead && useAirbornePose ? 1f : 0f, Time.deltaTime * 7f);
        landingAmount = Mathf.MoveTowards(landingAmount, 0f, Time.deltaTime / landingRecovery);

        float response = 1f - Mathf.Exp(-poseResponse * Time.deltaTime);
        float targetIK = !dead && !sprinting && Time.time >= releaseIKUntil ? 1f : 0f;
        ikWeight = Mathf.Lerp(ikWeight, targetIK, response);
        reloadBlend = Mathf.Lerp(reloadBlend, reloading && !dead ? 1f : 0f, response);
        if (rifleLayer >= 0)
        {
            float weight = dead || Time.time < releaseIKUntil ? 0f :
                (sprinting ? sprintRifleLayerWeight : 1f);
            // A death clip must control the arms as soon as the character dies.
            animator.SetLayerWeight(rifleLayer, dead ? 0f :
                Mathf.Lerp(animator.GetLayerWeight(rifleLayer), weight, response));
        }
        wasGrounded = grounded;
        previousVerticalSpeed = velocity.y;
        hasGroundSample = true;
        wasDead = dead;
    }

    private void LateUpdate()
    {
        UpdateVisibility();
        if (!capturedVisualPose || visualMotionRoot == null)
            return;
        float dip = landingAmount * landingDip;
        visualMotionRoot.localPosition = visualLocalPosition + Vector3.down * dip;
        visualMotionRoot.localRotation = visualLocalRotation *
            Quaternion.Euler(landingAmount * 4f + airborneBlend * 3f, 0f, 0f);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        // Run on the final rifle layer, after its masked pose has been blended.
        int finalIKLayer = rifleLayer >= 0 ? rifleLayer : 0;
        if (layerIndex != finalIKLayer || animator == null || !animator.isHuman || character == null)
            return;
        if (useAirbornePose && airborneBlend > 0.001f)
        {
            RotateBone(HumanBodyBones.LeftUpperLeg, -airborneThighAngle * airborneBlend);
            RotateBone(HumanBodyBones.RightUpperLeg, -airborneThighAngle * airborneBlend);
            RotateBone(HumanBodyBones.LeftLowerLeg, airborneKneeAngle * airborneBlend);
            RotateBone(HumanBodyBones.RightLowerLeg, airborneKneeAngle * airborneBlend);
        }

        Transform eyes = character.CameraTransform;
        float weight = useAimIK ? ikWeight : 0f;
        animator.SetLookAtWeight(weight * (1f - reloadBlend * 0.5f), 0f, headLookWeight, 0f, 0.65f);
        animator.SetLookAtPosition(eyes.position + eyes.forward * 25f);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
        if (weight < 0.001f || rifleVisual == null)
            return;

        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform shoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        if (hand == null || shoulder == null)
            return;

        Vector3 animatedHandPosition = animator.GetIKPosition(AvatarIKGoal.RightHand);
        Quaternion animatedHandRotation = animator.GetIKRotation(AvatarIKGoal.RightHand);
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Quaternion supportGoalOffset = leftHand != null
            ? Quaternion.Inverse(leftHand.rotation) * animator.GetIKRotation(AvatarIKGoal.LeftHand)
            : Quaternion.identity;

        float pitch = Mathf.Asin(Mathf.Clamp(eyes.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        pitch = Mathf.Clamp(pitch, -maximumAimPitch, maximumAimPitch);
        pitch = Mathf.Lerp(pitch, -reloadLowerAngle, reloadBlend);
        Quaternion aimRotation = Quaternion.AngleAxis(-pitch, character.transform.right);
        float handWeight = weight * rightHandAimWeight;
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, handWeight);
        animator.SetIKPosition(AvatarIKGoal.RightHand,
            shoulder.position + aimRotation * (animatedHandPosition - shoulder.position));
        animator.SetIKRotation(AvatarIKGoal.RightHand, aimRotation * animatedHandRotation);

        if (supportGrip != null)
        {
            // The grip is under the right-hand rifle. Predict its post-IK position
            // here because Unity solves both hands after this callback returns.
            Quaternion supportRotation = Quaternion.Slerp(Quaternion.identity, aimRotation, handWeight);
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, weight * supportHandWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, weight * supportHandWeight);
            animator.SetIKPosition(AvatarIKGoal.LeftHand,
                shoulder.position + supportRotation * (supportGrip.position - shoulder.position));
            animator.SetIKRotation(AvatarIKGoal.LeftHand,
                supportRotation * supportGrip.rotation * supportGoalOffset);
        }
    }

    private void RotateBone(HumanBodyBones boneId, float degrees)
    {
        Transform bone = animator.GetBoneTransform(boneId);
        if (bone == null || bone.parent == null)
            return;
        Vector3 axis = bone.parent.InverseTransformDirection(character.transform.right);
        animator.SetBoneLocalRotation(boneId, Quaternion.AngleAxis(degrees, axis) * bone.localRotation);
    }

    private void AttachRifleIfRequested()
    {
        if (!attachRifleToHand || attachedRifle || rifleVisual == null || !animator.isHuman)
            return;
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null)
            return;
        rifleVisual.SetParent(hand, false);
        rifleVisual.localPosition = rifleLocalPosition;
        rifleVisual.localRotation = Quaternion.Euler(rifleLocalEulerAngles);
        attachedRifle = true;
    }

    private void HandleShot()
    {
        if (health == null || !health.IsDead)
            SetTrigger(Fire);
    }

    private void HandleDamage(int amount, int currentHealth)
    {
        if (currentHealth <= 0 || amount <= 0)
            return;
        SetTrigger(Hit);
        releaseIKUntil = Time.time + hitIKReleaseTime;
        // Give the damage clip the torso/arms for its impact, rather than
        // leaving the rifle-hold override masking the visible reaction.
        if (rifleLayer >= 0)
            animator.SetLayerWeight(rifleLayer, 0f);
    }

    private void HandleDeath(Health deadHealth)
    {
        SetBool(Dead, true);
        ResetTrigger(Fire);
        ResetTrigger(Hit);
        ResetTrigger(Jump);
        ResetTrigger(Land);
        ikWeight = 0f;
        airborneBlend = 0f;
        landingAmount = 0f;
        if (rifleLayer >= 0)
            animator.SetLayerWeight(rifleLayer, 0f);
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType type)
    {
        return animator != null && animator.isActiveAndEnabled &&
            parameters.TryGetValue(hash, out AnimatorControllerParameterType actual) && actual == type;
    }

    private void SetFloat(int hash, float value, float damping)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Float))
            animator.SetFloat(hash, value, damping, Time.deltaTime);
    }

    private void SetBool(int hash, bool value)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Bool))
            animator.SetBool(hash, value);
    }

    private void SetTrigger(int hash)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Trigger))
            animator.SetTrigger(hash);
    }

    private void ResetTrigger(int hash)
    {
        if (HasParameter(hash, AnimatorControllerParameterType.Trigger))
            animator.ResetTrigger(hash);
    }

    private void RestoreVisualPose()
    {
        if (!capturedVisualPose || visualMotionRoot == null)
            return;
        visualMotionRoot.localPosition = visualLocalPosition;
        visualMotionRoot.localRotation = visualLocalRotation;
    }

    private void CacheVisibilityRenderers()
    {
        RestoreVisibility();
        bodyRenderers = GetComponentsInChildren<Renderer>(true);
        originalShadowModes = new ShadowCastingMode[bodyRenderers.Length];
        for (int i = 0; i < bodyRenderers.Length; i++)
            originalShadowModes[i] = localPlayer && bodyRenderers[i].shadowCastingMode == ShadowCastingMode.ShadowsOnly
                ? ShadowCastingMode.On : bodyRenderers[i].shadowCastingMode;
        viewingCamera = null;
    }

    private void UpdateVisibility()
    {
        if (!localPlayer || character == null)
            return;
        if (viewingCamera == null || !viewingCamera.isActiveAndEnabled || !viewingCamera.CompareTag("MainCamera"))
            viewingCamera = Camera.main;
        bool hide = viewingCamera != null &&
            (viewingCamera.transform.position - character.transform.position).sqrMagnitude <
            firstPersonHideDistance * firstPersonHideDistance;
        if (hide == bodyHidden || bodyRenderers == null)
            return;
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null)
                bodyRenderers[i].shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : originalShadowModes[i];
        }
        bodyHidden = hide;
    }

    private void RestoreVisibility()
    {
        if (!bodyHidden || bodyRenderers == null || originalShadowModes == null)
            return;
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            if (bodyRenderers[i] != null)
                bodyRenderers[i].shadowCastingMode = originalShadowModes[i];
        }
        bodyHidden = false;
    }
}
