using UnityEngine;

/// <summary>
/// Decides when to hold the trigger, then presses it through
/// FpsCharacterController.SetFireInput - the same call the player's
/// input script makes. It has no reference to the Weapon at all, so
/// there is no path by which the AI could bypass fire rate, spray
/// pattern accumulation or recoil.
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

    public bool IsFiring => triggerHeld;

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<FpsCharacterController>();

        if (aim == null)
            aim = GetComponent<FpsAiAim>();
    }

    public void Initialise(FPSPersonality assignedPersonality)
    {
        personality = assignedPersonality;
    }

    /// <summary>
    /// Call every frame. Pass false whenever the AI should not be
    /// shooting at all (no target, repositioning, evading).
    /// </summary>
    public void Tick(bool wantsToShoot)
    {
        if (characterController == null || personality == null)
            return;

        if (!wantsToShoot || !IsOnTarget())
        {
            ReleaseTrigger();
            return;
        }

        UpdateBurst();
    }

    public void ReleaseTrigger()
    {
        if (!triggerHeld)
            return;

        triggerHeld = false;

        characterController.SetFireInput(false);

        // Start the cooldown as soon as the burst ends.
        nextBurstTime =
            Time.time + personality.burstCooldown;
    }

    private bool IsOnTarget()
    {
        if (aim == null)
            return true;

        if (!aim.HasReacted)
            return false;

        return aim.CurrentAimError <=
               personality.fireAngleTolerance;
    }

    private void UpdateBurst()
    {
        if (triggerHeld)
        {
            if (Time.time >= burstEndTime)
                ReleaseTrigger();

            return;
        }

        if (Time.time < nextBurstTime)
            return;

        // Start a new burst.
        triggerHeld = true;

        burstEndTime =
            Time.time + personality.burstDuration;

        characterController.SetFireInput(true);
    }

    private void OnDisable()
    {
        if (characterController != null)
            characterController.SetFireInput(false);

        triggerHeld = false;
    }
}