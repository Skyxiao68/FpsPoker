using System.Collections.Generic;
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

    [Tooltip("How far up from the target's origin to check for line of sight (avoids ground clipping).")]
    [SerializeField] private float visibilityHeightOffset = 1.5f;

    [Tooltip("How far up from the target's origin to actually aim the weapon.")]
    [SerializeField] private float aimHeightOffset = 1.0f;

    [Tooltip("Seconds between expensive target scans.")]
    [SerializeField] private float scanInterval = 0.2f;

    private Transform target;
    private Transform selfRoot;
    private Collider targetBody;
    private Health targetHealth;
    private float nextScanTime;

    // Only confirmed sightings update these records.
    private Vector3 lastKnownPosition;
    private Vector3 lastKnownAimPoint;
    private Vector3 lastKnownVelocity;
    private float lastSeenTime = -999f;
    private bool hasObservation;
    private bool hasVelocitySample;
    private Vector3 targetVelocity;

    private Collider[] scanResults = new Collider[32];
    private RaycastHit[] sightHits = new RaycastHit[16];
    private readonly HashSet<Transform> scannedTargets =
        new HashSet<Transform>();

    public Transform Target => target;

    public bool HasTarget => target != null;

    public bool CanSeeTarget { get; private set; }

    public bool HasRecentMemory =>
        hasObservation &&
        personality != null &&
        Time.time - lastSeenTime <= Mathf.Max(0f, personality.memoryDuration);

    public Vector3 LastKnownPosition => lastKnownPosition;

    public Vector3 TargetVelocity => targetVelocity;

    public Vector3 LastKnownVelocity => lastKnownVelocity;

    public float TimeSinceLastSeen =>
        hasObservation
            ? Mathf.Max(0f, Time.time - lastSeenTime)
            : Mathf.Infinity;

    /// <summary>
    /// The actual visible body point, or its last observed position when hidden.
    /// </summary>
    public Vector3 AimPoint => lastKnownAimPoint;

    public float DistanceToTarget =>
        target != null && hasObservation
            ? Vector3.Distance(transform.position, lastKnownPosition)
            : Mathf.Infinity;

    private Transform Eyes => eyes != null ? eyes : transform;

    private void Awake()
    {
        Health ownHealth = GetComponentInParent<Health>();
        FpsCharacterController ownController =
            GetComponentInParent<FpsCharacterController>();

        selfRoot = ownHealth != null
            ? ownHealth.transform
            : ownController != null ? ownController.transform : transform;
    }

    public void Initialise(FPSPersonality assignedPersonality)
    {
        personality = assignedPersonality;
    }

    public void Tick()
    {
        if (!isActiveAndEnabled || personality == null)
        {
            CanSeeTarget = false;
            targetVelocity = Vector3.zero;
            hasVelocitySample = false;
            return;
        }

        // Do not spend the memory duration pursuing a dead or removed actor.
        if ((target != null && !IsValidTarget(target, targetHealth)) ||
            (hasObservation && target == null))
        {
            ForgetTarget();
        }

        Vector3 visiblePoint;
        CanSeeTarget = TryGetVisiblePoint(target, targetBody, out visiblePoint);

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + Mathf.Max(0f, scanInterval);

            // Keep a visible opponent instead of hopping between nearby actors
            // or between several colliders belonging to the same character.
            if (!CanSeeTarget)
                CanSeeTarget = AcquireTarget(out visiblePoint);
        }

        if (CanSeeTarget)
        {
            ObserveTarget(visiblePoint);
        }
        else
        {
            // The next sighting begins a fresh velocity sample; movement
            // behind cover must never feed aiming or update the search point.
            hasVelocitySample = false;
            targetVelocity = Vector3.zero;

            if (!HasRecentMemory && (target != null || hasObservation))
                ForgetTarget();
        }
    }

    private bool AcquireTarget(out Vector3 visiblePoint)
    {
        int count;
        do
        {
            count = Physics.OverlapSphereNonAlloc(
                transform.position,
                Mathf.Max(0f, personality.sightRange),
                scanResults,
                targetMask,
                QueryTriggerInteraction.Ignore
            );

            // A crowded scene must not silently omit the visible opponent.
            if (count == scanResults.Length)
                System.Array.Resize(ref scanResults, scanResults.Length * 2);
            else
                break;
        } while (true);

        scannedTargets.Clear();
        Transform best = null;
        Collider bestBody = null;
        Health bestHealth = null;
        float bestDistanceSquared = Mathf.Infinity;
        visiblePoint = Vector3.zero;

        for (int i = 0; i < count; i++)
        {
            Collider candidate = scanResults[i];
            if (candidate == null || IsOwnCollider(candidate))
                continue;

            Transform candidateTarget = ResolveTargetIdentity(candidate);
            if (candidateTarget == null || !scannedTargets.Add(candidateTarget))
                continue;

            Health candidateHealth = candidateTarget.GetComponent<Health>();
            if (!IsValidTarget(candidateTarget, candidateHealth))
                continue;

            Collider body = FindTargetBody(candidateTarget, candidate);
            Vector3 candidatePoint;
            if (!TryGetVisiblePoint(candidateTarget, body, out candidatePoint))
                continue;

            float distanceSquared =
                (candidateTarget.position - transform.position).sqrMagnitude;

            if (distanceSquared >= bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            best = candidateTarget;
            bestBody = body;
            bestHealth = candidateHealth;
            visiblePoint = candidatePoint;
        }

        if (best == null)
            return false;

        if (target != best)
        {
            hasObservation = false;
            hasVelocitySample = false;
            targetVelocity = Vector3.zero;
            lastKnownVelocity = Vector3.zero;
        }

        target = best;
        targetBody = bestBody;
        targetHealth = bestHealth;
        return true;
    }

    private bool TryGetVisiblePoint(
        Transform candidateTarget,
        Collider body,
        out Vector3 visiblePoint)
    {
        visiblePoint = Vector3.zero;
        if (candidateTarget == null || !candidateTarget.gameObject.activeInHierarchy ||
            (body != null && (!body.enabled || !body.gameObject.activeInHierarchy)))
            return false;

        // Prefer the configured aim height. If it is behind cover, aim at
        // the visible upper or lower body instead of shooting into the cover.
        for (int sample = 0; sample < 3; sample++)
        {
            float height = sample == 0
                ? aimHeightOffset
                : sample == 1
                    ? visibilityHeightOffset
                    : Mathf.Min(aimHeightOffset, visibilityHeightOffset) * 0.5f;

            Vector3 point = candidateTarget.position + Vector3.up * height;
            if (body != null)
            {
                Bounds bounds = body.bounds;
                float inset = Mathf.Min(0.05f, bounds.size.y * 0.1f);
                point.x = bounds.center.x;
                point.z = bounds.center.z;
                point.y = Mathf.Clamp(point.y, bounds.min.y + inset, bounds.max.y - inset);
            }

            Vector3 toPoint = point - Eyes.position;
            float sightRange = Mathf.Max(0f, personality.sightRange);
            if (toPoint.sqrMagnitude > sightRange * sightRange)
                continue;

            if (Vector3.Angle(Eyes.forward, toPoint) > personality.fieldOfView * 0.5f)
                continue;

            if (!HasLineOfSight(Eyes.position, point, candidateTarget))
                continue;

            visiblePoint = point;
            return true;
        }

        return false;
    }

    /// <summary>Checks a sightline, including from a prospective tactical position.</summary>
    public bool HasLineOfSight(Vector3 origin, Vector3 point)
    {
        return HasLineOfSight(origin, point, target);
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 point, Transform sightTarget)
    {
        Vector3 direction = point - origin;
        float distance = direction.magnitude;
        if (distance <= 0.0001f)
            return true;

        int count;
        do
        {
            count = Physics.RaycastNonAlloc(
                origin,
                direction / distance,
                sightHits,
                distance,
                obstructionMask,
                QueryTriggerInteraction.Ignore
            );

            if (count == sightHits.Length)
                System.Array.Resize(ref sightHits, sightHits.Length * 2);
            else
                break;
        } while (true);

        float nearestBlocker = Mathf.Infinity;
        float nearestTarget = Mathf.Infinity;
        for (int i = 0; i < count; i++)
        {
            Collider hit = sightHits[i].collider;
            if (hit == null || IsOwnCollider(hit))
                continue;

            if (BelongsToTarget(hit, sightTarget))
                nearestTarget = Mathf.Min(nearestTarget, sightHits[i].distance);
            else
                nearestBlocker = Mathf.Min(nearestBlocker, sightHits[i].distance);
        }

        // The target's own capsule is a successful sighting, not an obstruction.
        return float.IsPositiveInfinity(nearestBlocker) || nearestTarget < nearestBlocker;
    }

    public bool IsTargetCollider(Collider collider)
    {
        return BelongsToTarget(collider, target);
    }

    private bool BelongsToTarget(Collider collider, Transform candidateTarget)
    {
        return collider != null && candidateTarget != null &&
            (collider.transform.IsChildOf(candidateTarget) ||
             ResolveTargetIdentity(collider) == candidateTarget);
    }

    private bool IsOwnCollider(Collider collider)
    {
        return collider != null &&
            collider.transform.IsChildOf(selfRoot != null ? selfRoot : transform);
    }

    private bool IsValidTarget(Transform candidateTarget, Health candidateHealth)
    {
        Transform owner = selfRoot != null ? selfRoot : transform;
        return candidateTarget != null && candidateTarget != owner &&
            !candidateTarget.IsChildOf(owner) &&
            candidateTarget.gameObject.activeInHierarchy &&
            (candidateHealth == null || !candidateHealth.IsDead);
    }

    private static Transform ResolveTargetIdentity(Collider collider)
    {
        Health health = collider.GetComponentInParent<Health>();
        if (health != null)
            return health.transform;

        FpsCharacterController controller =
            collider.GetComponentInParent<FpsCharacterController>();
        if (controller != null)
            return controller.transform;

        return collider.attachedRigidbody != null
            ? collider.attachedRigidbody.transform
            : collider.transform;
    }

    private static Collider FindTargetBody(Transform candidateTarget, Collider fallback)
    {
        CharacterController capsule = candidateTarget.GetComponent<CharacterController>();
        if (capsule != null)
            return capsule;

        Collider body = candidateTarget.GetComponent<Collider>();
        return body != null && !body.isTrigger ? body : fallback;
    }

    private void ObserveTarget(Vector3 visiblePoint)
    {
        float sampleDuration = Time.time - lastSeenTime;
        if (hasVelocitySample && sampleDuration > 0.0001f)
        {
            Vector3 measuredVelocity =
                (target.position - lastKnownPosition) / sampleDuration;
            targetVelocity = Vector3.Lerp(
                targetVelocity,
                measuredVelocity,
                1f - Mathf.Exp(-10f * sampleDuration)
            );
        }
        else
        {
            targetVelocity = Vector3.zero;
        }

        lastKnownPosition = target.position;
        lastKnownAimPoint = visiblePoint;
        lastKnownVelocity = targetVelocity;
        lastSeenTime = Time.time;
        hasObservation = true;
        hasVelocitySample = true;
    }

    public void ForgetTarget()
    {
        target = null;
        targetBody = null;
        targetHealth = null;
        CanSeeTarget = false;
        hasObservation = false;
        hasVelocitySample = false;
        targetVelocity = Vector3.zero;
        lastKnownVelocity = Vector3.zero;
        lastSeenTime = -999f;
        nextScanTime = 0f;
    }

    private void OnDisable()
    {
        ForgetTarget();
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
