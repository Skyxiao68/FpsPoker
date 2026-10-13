using UnityEngine;

/// <summary>
/// Decides when to hold the trigger, then presses it through
/// FpsCharacterController.SetFireInput - the same call the player's
/// input script makes. Weapon data is read only for range/readiness;
/// firing, spray pattern accumulation and recoil remain with the controller.
/// </summary>
public class FpsAiTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FpsCharacterController characterController;
    [SerializeField] private FpsAiAim aim;

    [Header("Settings")]
    [SerializeField] private FPSPersonality personality;

    private bool triggerHeld;
    private float burstEndTime;
    private float nextBurstTime;
    private FpsAiPerception perception;
    private Weapon previousWeapon;
    private readonly RaycastHit[] shotHits = new RaycastHit[32];

    public bool IsFiring => triggerHeld;

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponentInParent<FpsCharacterController>();

        if (aim == null)
            aim = GetComponentInParent<FpsAiAim>();

        perception = GetComponentInParent<FpsAiPerception>();
    }

    public void Initialise(FPSPersonality assignedPersonality)
    {
        ReleaseTrigger();
        personality = assignedPersonality;
    }

    /// <summary>
    /// Call every frame. Pass false whenever the AI should not be
    /// shooting at all (no target, repositioning, evading).
    /// </summary>
    public void Tick(bool wantsToShoot)
    {
        if (!isActiveAndEnabled || characterController == null ||
            !characterController.isActiveAndEnabled || personality == null)
        {
            ReleaseTrigger();
            return;
        }

        Weapon weapon = characterController.EquippedWeapon;

        if (weapon != previousWeapon)
        {
            ReleaseTrigger();
            previousWeapon = weapon;
        }

        if (!wantsToShoot || !IsOnTarget(weapon))
        {
            ReleaseTrigger();
            return;
        }

        UpdateBurst(weapon);
    }

    public void ReleaseTrigger()
    {
        bool wasHeld = triggerHeld;
        triggerHeld = false;

        if (characterController != null)
            characterController.SetFireInput(false);

        // Start the cooldown as soon as the burst ends.
        if (wasHeld)
        {
            nextBurstTime = Time.time +
                (personality != null ? Mathf.Max(0f, personality.burstCooldown) : 0f);
        }
    }

    private bool IsOnTarget(Weapon weapon)
    {
        if (aim == null || !aim.CanFireAtTarget ||
            perception == null || !perception.isActiveAndEnabled ||
            !perception.CanSeeTarget || !perception.HasTarget ||
            weapon == null || !weapon.isActiveAndEnabled ||
            weapon.Settings == null || weapon.FirePoint == null)
            return false;

        if (aim.CurrentAimError > Mathf.Max(0f, personality.fireAngleTolerance))
            return false;

        Transform eyes = characterController.CameraTransform;
        Vector3 toTarget = perception.AimPoint - eyes.position;
        float distance = toTarget.magnitude;

        if (distance <= 0.0001f || distance > weapon.Settings.range)
            return false;

        if (!perception.HasLineOfSight(eyes.position, perception.AimPoint))
            return false;

        return HasClearFiringPath(eyes, distance);
    }

    private bool HasClearFiringPath(Transform eyes, float distance)
    {
        // Hitscan originates at the camera. Seeing a head over cover must
        // not allow a burst while the actual aim ray still points at a wall.
        int hitCount = Physics.RaycastNonAlloc(
            eyes.position,
            eyes.forward,
            shotHits,
            distance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore
        );

        // A saturated buffer cannot establish which collider is closest.
        if (hitCount == shotHits.Length)
            return false;

        Collider firstCollider = null;
        float firstDistance = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider candidate = shotHits[i].collider;

            if (candidate == null ||
                candidate.transform.IsChildOf(characterController.transform))
                continue;

            if (shotHits[i].distance < firstDistance)
            {
                firstDistance = shotHits[i].distance;
                firstCollider = candidate;
            }
        }

        // Empty space is allowed within the personality's aim tolerance;
        // this preserves natural misses while refusing obstructed shots.
        return firstCollider == null || perception.IsTargetCollider(firstCollider);
    }

    private void UpdateBurst(Weapon weapon)
    {
        if (triggerHeld)
        {
            if (Time.time >= burstEndTime)
                ReleaseTrigger();

            return;
        }

        if (Time.time < nextBurstTime)
            return;

        // A semi-automatic weapon consumes its press even when its fire
        // timer is not ready. Wait to pull, without firing the weapon directly.
        if (!weapon.CanFire)
            return;

        // Start a new burst.
        triggerHeld = true;

        burstEndTime =
            Time.time + Mathf.Max(0.02f, personality.burstDuration);

        characterController.SetFireInput(true);
    }

    private void OnDisable()
    {
        ReleaseTrigger();
    }
}
