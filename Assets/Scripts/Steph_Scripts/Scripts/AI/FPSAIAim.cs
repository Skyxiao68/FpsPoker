using UnityEngine;


public class FpsAiAim : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FpsCharacterController characterController;

    [Header("Settings")]
    [SerializeField] private FPSPersonality personality;

    private float targetFirstSeenTime = -999f;
    private bool hadTargetLastFrame;

    private float noiseSeedX;
    private float noiseSeedY;

    public float CurrentAimError { get; private set; } = 180f;

    public bool HasReacted { get; private set; }

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
    }

    public void AimAt(
        Vector3 worldPoint,
        Vector3 targetVelocity,
        bool targetVisible)
    {
        if (characterController == null || personality == null)
            return;

        HandleReactionTimer(targetVisible);

        if (!HasReacted)
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

        Vector2 errorDegrees =
            CalculateAngularError(desiredDirection);

        CurrentAimError = errorDegrees.magnitude;

        errorDegrees += CalculateAimWobble();

        errorDegrees =
            ClampTurnRate(errorDegrees);

        ApplyLookInput(errorDegrees);
    }

    public void LookTowardDirection(Vector3 worldDirection)
    {
        if (characterController == null)
            return;

        if (worldDirection.sqrMagnitude < 0.0001f)
        {
            characterController.SetLookInput(Vector2.zero);
            return;
        }

        AimAt(
            Eyes.position + worldDirection.normalized * 20f,
            Vector3.zero,
            false
        );
    }

    public void Idle()
    {
        if (characterController != null)
            characterController.SetLookInput(Vector2.zero);

        CurrentAimError = 180f;
    }


    private void HandleReactionTimer(bool targetVisible)
    {
        if (targetVisible && !hadTargetLastFrame)
        {
            // Target just appeared - start the reaction clock.
            targetFirstSeenTime = Time.time;
        }

        hadTargetLastFrame = targetVisible;

        HasReacted =
            !targetVisible ||
            Time.time - targetFirstSeenTime >=
                personality.reactionTime;
    }


    private Vector3 ApplyLeading(
        Vector3 worldPoint,
        Vector3 targetVelocity)
    {
        if (personality.targetLeadAccuracy <= 0f)
            return worldPoint;

        float distance =
            Vector3.Distance(
                Eyes.position,
                worldPoint
            );


        float leadTime =
            Mathf.Clamp(
                distance / 120f,
                0f,
                0.25f
            );

        return worldPoint +
               targetVelocity *
               leadTime *
               personality.targetLeadAccuracy;
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
            personality.aimTurnSpeed *
            Time.deltaTime;

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
            return;

        Vector2 lookInput =
            new Vector2(
                errorDegrees.x / degreesPerUnit,
                errorDegrees.y / degreesPerUnit
            );

        characterController.SetLookInput(lookInput);
    }
}