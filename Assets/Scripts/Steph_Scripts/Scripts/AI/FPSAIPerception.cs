using UnityEngine;

public class FpsAiPerception : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private FPSPersonality personality;

    [Header("References")]
    [Tooltip("Eye position. Use the AI's camera parent so its " +
             "sightline matches where it's actually looking.")]
    [SerializeField] private Transform eyes;

    [Header("Targeting")]
    [Tooltip("Layers that count as a target.")]
    [SerializeField] private LayerMask targetMask;

    [Tooltip("Layers that block line of sight.")]
    [SerializeField] private LayerMask obstructionMask;

    [Tooltip("How far up from the target's origin to aim.")]
    [SerializeField] private float targetHeightOffset = 1.2f;

    [Tooltip("Seconds between expensive target scans.")]
    [SerializeField] private float scanInterval = 0.2f;

    private Transform target;
    private float nextScanTime;

    // Memory
    private Vector3 lastKnownPosition;
    private float lastSeenTime = -999f;

    // Target velocity, estimated for aim leading.
    private Vector3 lastTargetPosition;
    private Vector3 targetVelocity;

    public Transform Target => target;

    public bool HasTarget => target != null;

    public bool CanSeeTarget { get; private set; }

    public bool HasRecentMemory =>
        personality != null &&
        Time.time - lastSeenTime <= personality.memoryDuration;

    public Vector3 LastKnownPosition => lastKnownPosition;

    public Vector3 TargetVelocity => targetVelocity;

    /// <summary>
    /// The world point the aim driver should track.
    /// </summary>
    public Vector3 AimPoint =>
        target != null
            ? target.position + Vector3.up * targetHeightOffset
            : lastKnownPosition;

    public float DistanceToTarget =>
        target != null
            ? Vector3.Distance(transform.position, target.position)
            : Mathf.Infinity;

    private Transform Eyes => eyes != null ? eyes : transform;

    public void Initialise(FPSPersonality assignedPersonality)
    {
        personality = assignedPersonality;
    }

    public void Tick()
    {
        if (personality == null)
            return;

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            AcquireTarget();
        }

        CanSeeTarget = EvaluateVisibility();

        if (CanSeeTarget)
        {
            lastSeenTime = Time.time;
            lastKnownPosition = target.position;
        }

        EstimateTargetVelocity();
    }



    private void AcquireTarget()
    {

        Collider[] candidates =
            Physics.OverlapSphere(
                transform.position,
                personality.sightRange,
                targetMask,
                QueryTriggerInteraction.Ignore
            );

        Transform best = null;
        float bestDistance = Mathf.Infinity;

        foreach (Collider candidate in candidates)
        {
            if (candidate.transform.IsChildOf(transform))
                continue;

            float distance =
                Vector3.Distance(
                    transform.position,
                    candidate.transform.position
                );

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate.transform;
            }
        }

        if (best != null || !HasRecentMemory)
            target = best;
    }


    private bool EvaluateVisibility()
    {
        if (target == null)
            return false;

        Vector3 eyePosition = Eyes.position;

        Vector3 toTarget =
            (target.position + Vector3.up * targetHeightOffset) -
            eyePosition;

        float distance = toTarget.magnitude;

        if (distance > personality.sightRange)
            return false;

        float angle =
            Vector3.Angle(
                Eyes.forward,
                toTarget
            );

        if (angle > personality.fieldOfView * 0.5f)
            return false;

        if (Physics.Raycast(
                eyePosition,
                toTarget.normalized,
                distance,
                obstructionMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        return true;
    }


    private void EstimateTargetVelocity()
    {
        if (target == null)
        {
            targetVelocity = Vector3.zero;
            return;
        }

        if (Time.deltaTime > 0f)
        {
            Vector3 delta =
                target.position - lastTargetPosition;

            targetVelocity =
                Vector3.Lerp(
                    targetVelocity,
                    delta / Time.deltaTime,
                    10f * Time.deltaTime
                );
        }

        lastTargetPosition = target.position;
    }

    private void OnDrawGizmosSelected()
    {
        if (personality == null)
            return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            personality.sightRange
        );
    }
}