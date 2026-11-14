using UnityEngine;

/// <summary>
/// Cosmetic motion for a common pivot containing the rifle and its posed hands.
/// Reads the existing controller/weapon state; never changes aim, fire timing or reload UI.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(120)]
public sealed class FirstPersonRifleVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Weapon weapon;
    [SerializeField] private FpsCharacterController characterController;
    [SerializeField] private Health health;
    [Tooltip("Both hands and the rifle must be children of this dedicated cosmetic pivot.")]
    [SerializeField] private Transform motionRoot;
    [SerializeField] private Transform viewCamera;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float walkBob = 0.009f;
    [SerializeField, Min(0f)] private float sprintBob = 0.018f;
    [SerializeField, Min(0f)] private float shotKickback = 0.055f;
    [SerializeField, Min(0f)] private float shotPitch = 3.2f;
    [SerializeField] private Vector3 reloadOffset = new Vector3(0.02f, -0.85f, -0.16f);
    [SerializeField] private Vector3 reloadEuler = new Vector3(24f, 0f, -8f);

    private Weapon subscribedWeapon;
    private Health subscribedHealth;
    private Renderer[] visualRenderers;
    private WradRifleGripPose gripPose;
    private Vector3 restPosition;
    private Quaternion restRotation;
    private Quaternion previousViewRotation;
    private Vector3 positionOffset;
    private Vector3 rotationOffset;
    private Vector3 lookSway;
    private float stride;
    private float movementBlend;
    private float sprintBlend;
    private float reloadBlend;
    private float recoil;
    private float recoilRoll;
    private float damageKick;
    private float damageRoll;
    private float landingKick;
    private float previousVerticalSpeed;
    private bool wasGrounded;
    private bool initialized;
    private bool forceHidden;

    /// <summary>Used by the asset installer; references remain serialized in the scene.</summary>
    public void Configure(Weapon sourceWeapon, FpsCharacterController controller,
        Health sourceHealth, Transform visualPivot, Transform cameraTransform)
    {
        Unsubscribe();
        weapon = sourceWeapon;
        characterController = controller;
        health = sourceHealth;
        motionRoot = visualPivot;
        viewCamera = cameraTransform;
        initialized = false;
        if (Application.isPlaying)
        {
            Initialize();
            if (isActiveAndEnabled) Subscribe();
        }
    }

    private void Awake() => Initialize();

    private void OnEnable()
    {
        Initialize();
        Subscribe();
    }

    private void Initialize()
    {
        if (initialized) return;
        if (weapon == null) weapon = GetComponentInParent<Weapon>();
        if (characterController == null) characterController = GetComponentInParent<FpsCharacterController>();
        if (health == null) health = GetComponentInParent<Health>();
        if (motionRoot == null) motionRoot = transform;
        if (viewCamera == null && characterController != null) viewCamera = characterController.CameraTransform;
        restPosition = motionRoot.localPosition;
        restRotation = motionRoot.localRotation;
        previousViewRotation = viewCamera != null ? viewCamera.rotation : Quaternion.identity;
        visualRenderers = motionRoot.GetComponentsInChildren<Renderer>(true);
        gripPose = motionRoot.GetComponentInChildren<WradRifleGripPose>(true);
        initialized = true;
    }

    private void Subscribe()
    {
        if (subscribedWeapon != weapon)
        {
            if (subscribedWeapon != null) subscribedWeapon.OnShotFired -= OnShotFired;
            subscribedWeapon = weapon;
            if (subscribedWeapon != null) subscribedWeapon.OnShotFired += OnShotFired;
        }
        if (subscribedHealth != health)
        {
            if (subscribedHealth != null) subscribedHealth.OnDamaged -= OnDamaged;
            subscribedHealth = health;
            if (subscribedHealth != null) subscribedHealth.OnDamaged += OnDamaged;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedWeapon != null) subscribedWeapon.OnShotFired -= OnShotFired;
        if (subscribedHealth != null) subscribedHealth.OnDamaged -= OnDamaged;
        subscribedWeapon = null;
        subscribedHealth = null;
    }

    private void OnShotFired()
    {
        if (health != null && health.IsDead) return;
        recoil = Mathf.Min(recoil + 1f, 1.65f);
        recoilRoll = recoilRoll <= 0f ? 0.55f : -0.55f;
        if (gripPose != null) gripPose.PressTrigger();
    }

    private void OnDamaged(int damage, int remainingHealth)
    {
        damageKick = Mathf.Clamp(damageKick + damage / 25f, 0.35f, 1.6f);
        damageRoll = -damageRoll;
        if (Mathf.Abs(damageRoll) < 0.1f) damageRoll = 2.8f;
    }

    private void LateUpdate()
    {
        if (!initialized || motionRoot == null) return;
        // A result pan disables the controller and its renderers. Never re-enable
        // renderers here: forceRenderingOff is independent of that existing flow.
        bool hide = (health != null && health.IsDead) ||
                    (characterController != null && !characterController.enabled);
        SetHidden(hide);
        float dt = Time.deltaTime;
        if (dt <= 0f || hide) return;

        bool reloading = weapon != null && weapon.IsReloading;
        reloadBlend = Mathf.MoveTowards(reloadBlend, reloading ? 1f : 0f,
            dt * (reloading ? 7f : 4.5f));
        float lowered = Mathf.SmoothStep(0f, 1f, reloadBlend);
        float visibleMotion = 1f - lowered;
        float speed = characterController != null ? characterController.HorizontalSpeed : 0f;
        bool grounded = characterController == null || characterController.IsGrounded;
        bool sprinting = characterController != null && characterController.IsSprinting && speed > 0.5f;
        movementBlend = Mathf.Lerp(movementBlend, grounded ? Mathf.Clamp01(speed / 5f) : 0f, Damp(9f, dt));
        sprintBlend = Mathf.Lerp(sprintBlend, sprinting ? 1f : 0f, Damp(7f, dt));
        stride += dt * Mathf.Lerp(8f, 12f, sprintBlend) * Mathf.Clamp(speed / 4f, 0f, 1.6f);
        float amplitude = Mathf.Lerp(walkBob, sprintBob, sprintBlend) * movementBlend;
        Vector3 bob = new Vector3(Mathf.Sin(stride) * amplitude,
            Mathf.Cos(stride * 2f) * amplitude * 0.75f, 0f);

        float verticalSpeed = characterController != null ? characterController.Velocity.y : 0f;
        if (grounded && !wasGrounded && previousVerticalSpeed < -1f)
            landingKick = Mathf.Clamp(-previousVerticalSpeed * 0.008f, 0.015f, 0.1f);
        wasGrounded = grounded;
        previousVerticalSpeed = verticalSpeed;
        landingKick = Mathf.Lerp(landingKick, 0f, Damp(10f, dt));
        float airborneOffset = grounded ? 0f : Mathf.Clamp(-verticalSpeed * 0.005f, -0.045f, 0.045f);

        if (viewCamera != null)
        {
            Vector3 delta = (Quaternion.Inverse(previousViewRotation) * viewCamera.rotation).eulerAngles;
            previousViewRotation = viewCamera.rotation;
            Vector3 targetSway = new Vector3(
                Mathf.Clamp(-Mathf.DeltaAngle(0f, delta.x) * 0.4f, -2f, 2f),
                Mathf.Clamp(-Mathf.DeltaAngle(0f, delta.y) * 0.4f, -2f, 2f), 0f);
            lookSway = Vector3.Lerp(lookSway, targetSway, Damp(9f, dt));
        }

        Vector3 localVelocity = characterController != null
            ? characterController.transform.InverseTransformDirection(characterController.Velocity) : Vector3.zero;
        Vector3 targetPosition = reloadOffset * lowered +
            (bob + new Vector3(0f, airborneOffset - landingKick - sprintBlend * 0.025f,
                -recoil * shotKickback - damageKick * 0.025f)) * visibleMotion;
        Vector3 targetRotation = reloadEuler * lowered +
            (lookSway + new Vector3(-recoil * shotPitch + damageKick * 2.2f + sprintBlend * 4f,
                0f, -localVelocity.x * 0.35f + recoilRoll * recoil + damageKick * damageRoll)) * visibleMotion;
        positionOffset = Vector3.Lerp(positionOffset, targetPosition, Damp(28f, dt));
        rotationOffset = Vector3.Lerp(rotationOffset, targetRotation, Damp(28f, dt));
        motionRoot.localPosition = restPosition + positionOffset;
        motionRoot.localRotation = restRotation * Quaternion.Euler(rotationOffset);

        recoil = Mathf.Lerp(recoil, 0f, Damp(11f, dt));
        damageKick = Mathf.Lerp(damageKick, 0f, Damp(8f, dt));
    }

    private static float Damp(float rate, float dt) => 1f - Mathf.Exp(-rate * dt);

    private void SetHidden(bool hidden)
    {
        if (forceHidden == hidden) return;
        forceHidden = hidden;
        if (visualRenderers == null) return;
        foreach (Renderer visual in visualRenderers)
            if (visual != null) visual.forceRenderingOff = hidden;
    }

    private void OnDisable()
    {
        Unsubscribe();
        SetHidden(false);
        if (initialized && motionRoot != null)
        {
            motionRoot.localPosition = restPosition;
            motionRoot.localRotation = restRotation;
        }
        positionOffset = rotationOffset = lookSway = Vector3.zero;
        movementBlend = sprintBlend = reloadBlend = recoil = damageKick = landingKick = 0f;
        wasGrounded = false;
        if (viewCamera != null) previousViewRotation = viewCamera.rotation;
    }
}
