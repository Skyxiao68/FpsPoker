using UnityEngine;


public class FpsAiAim : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FpsCharacterController characterController;

    [Header("Settings")]
    [SerializeField] private FPSPersonality personality;

    private float targetFirstSeenTime = -999f;
    private bool hadTargetLastFrame;
    private bool hasValidAim;
    private int lastAimFrame = -1;

    private float noiseSeedX;
    private float noiseSeedY;

    public float CurrentAimError { get; private set; } = 180f;

    public bool HasReacted { get; private set; }

    public bool CanFireAtTarget =>
        isActiveAndEnabled &&
        hadTargetLastFrame &&
        HasReacted &&
        hasValidAim &&
        lastAimFrame == Time.frameCount;

    private Transform Eyes =>
        characterController != null
            ? characterController.CameraTransform
            : transform;

    private void Awake()
    {
        if (characterController == null)
        {
            characterController =
                GetComponentInParent<FpsCharacterController>();
        }

        noiseSeedX = Random.Range(0f, 100f);
        noiseSeedY = Random.Range(0f, 100f);
    }

    public void Initialise(FPSPersonality assignedPersonality)
    {
        personality = assignedPersonality;
        ResetTarget();
    }

    /// <summary>
    /// Call when target identity changes, even if both targets are visible.
    /// A new opponent must earn its own reaction delay.
    /// </summary>
    public void ResetTarget()
    {
        targetFirstSeenTime = -999f;
        hadTargetLastFrame = false;
        HasReacted = false;
        hasValidAim = false;
        lastAimFrame = -1;
        CurrentAimError = 180f;

        if (characterController != null)
            characterController.SetLookInput(Vector2.zero);
    }

    public void AimAt(
        Vector3 worldPoint,
        Vector3 targetVelocity,
        bool targetVisible)
    {
        if (!isActiveAndEnabled ||
            characterController == null || personality == null)
        {
            ResetTarget();
            return;
        }

        hasValidAim = false;
        CurrentAimError = 180f;

        HandleReactionTimer(targetVisible);

        if (targetVisible && !HasReacted)
        {

            characterController.SetLookInput(Vector2.zero);
            return;
        }

        Vector3 aimPoint =
            ApplyLeading(
                worldPoint,
                targetVelocity
            );

        Vector3 desiredDirection =
            aimPoint - Eyes.position;

        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            characterController.SetLookInput(Vector2.zero);
            return;
        }

        desiredDirection.Normalize();

        Weapon weapon = characterController.EquippedWeapon;
        CurrentAimError = Vector3.Angle(weapon != null
            ? weapon.GetPredictedShotDirection(Eyes) : Eyes.forward,
            desiredDirection);
        hasValidAim = targetVisible;
        lastAimFrame = Time.frameCount;

        SteerToward(desiredDirection, targetVisible);
    }

    public void LookTowardDirection(Vector3 worldDirection)
    {
        ResetTarget();

        if (!isActiveAndEnabled ||
            characterController == null || personality == null)
            return;

        if (worldDirection.sqrMagnitude < 0.0001f)
        {
            characterController.SetLookInput(Vector2.zero);
            return;
        }

        // Patrol and search must be able to look around without creating
        // a firing-ready target or consuming the next reaction delay.
        SteerToward(worldDirection.normalized, false);
    }

    public void Idle()
    {
        ResetTarget();
    }

    private void OnDisable()
    {
        ResetTarget();
    }

    private void SteerToward(Vector3 desiredDirection, bool applyWobble)
    {
        Vector2 errorDegrees = CalculateAngularError(desiredDirection);

        if (applyWobble)
        {
            Weapon weapon = characterController.EquippedWeapon;
            if (weapon != null)
            {
                // Counter most of the known spray, while retaining the
                // personality's turn speed and natural aim error.
                errorDegrees -= weapon.ShotOffsetDegrees * 0.7f;
            }
            errorDegrees += CalculateAimWobble();
        }

        ApplyLookInput(ClampTurnRate(errorDegrees));
    }


    private void HandleReactionTimer(bool targetVisible)
    {
        if (!targetVisible)
        {
            ResetTarget();
            return;
        }

        if (targetVisible && !hadTargetLastFrame)
        {
            // Target just appeared - start the reaction clock.
            targetFirstSeenTime = Time.time;
        }

        hadTargetLastFrame = targetVisible;

        HasReacted =
            Time.time - targetFirstSeenTime >=
                Mathf.Max(0f, personality.reactionTime);
    }


    private Vector3 ApplyLeading(
        Vector3 worldPoint,
        Vector3 targetVelocity)
    {
        if (personality.targetLeadAccuracy <= 0f)
            return worldPoint;

        // This weapon is hitscan. Its trail speed is cosmetic, so leading
        // by distance invents bullet travel and overshoots moving targets.
        // Only anticipate the next controller update, scaled by skill.
        float leadTime = Mathf.Clamp(Time.deltaTime, 0f, 0.05f);

        return worldPoint +
               targetVelocity *
               leadTime *
               Mathf.Clamp01(personality.targetLeadAccuracy);
    }


    private Vector2 CalculateAngularError(Vector3 desiredDirection)
    {
        Vector3 currentForward = Eyes.forward;

        Vector3 currentFlat =
            new Vector3(
                currentForward.x,
                0f,
                currentForward.z
            );

        Vector3 desiredFlat =
            new Vector3(
                desiredDirection.x,
                0f,
                desiredDirection.z
            );

        float yawError = 0f;

        if (currentFlat.sqrMagnitude > 0.0001f &&
            desiredFlat.sqrMagnitude > 0.0001f)
        {
            yawError =
                Vector3.SignedAngle(
                    currentFlat.normalized,
                    desiredFlat.normalized,
                    Vector3.up
                );
        }

        float currentElevation =
            Mathf.Asin(
                Mathf.Clamp(currentForward.y, -1f, 1f)
            ) * Mathf.Rad2Deg;

        float desiredElevation =
            Mathf.Asin(
                Mathf.Clamp(desiredDirection.y, -1f, 1f)
            ) * Mathf.Rad2Deg;

        desiredElevation = Mathf.Clamp(
            desiredElevation,
            -characterController.LookPitchLimit,
            characterController.LookPitchLimit
        );

        float pitchError =
            desiredElevation - currentElevation;

        return new Vector2(yawError, pitchError);
    }


    private Vector2 CalculateAimWobble()
    {
        if (personality.aimError <= 0f)
            return Vector2.zero;

        float t =
            Time.time *
            personality.aimErrorFrequency;

        float x =
            Mathf.PerlinNoise(noiseSeedX, t) * 2f - 1f;

        float y =
            Mathf.PerlinNoise(noiseSeedY, t) * 2f - 1f;

        return new Vector2(x, y) * personality.aimError;
    }


    private Vector2 ClampTurnRate(Vector2 errorDegrees)
    {
        float maxDegreesThisFrame =
            Mathf.Max(0f, personality.aimTurnSpeed) *
            Mathf.Max(0f, Time.deltaTime);

        if (errorDegrees.magnitude > maxDegreesThisFrame)
        {
            errorDegrees =
                errorDegrees.normalized *
                maxDegreesThisFrame;
        }

        return errorDegrees;
    }

    private void ApplyLookInput(Vector2 errorDegrees)
    {
        float degreesPerUnit =
            characterController.LookDegreesPerInputUnit;

        if (degreesPerUnit <= 0.00001f)
        {
            characterController.SetLookInput(Vector2.zero);
            return;
        }

        Vector2 lookInput =
            new Vector2(
                errorDegrees.x / degreesPerUnit,
                errorDegrees.y / degreesPerUnit
            );

        characterController.SetLookInput(lookInput);
    }
}
