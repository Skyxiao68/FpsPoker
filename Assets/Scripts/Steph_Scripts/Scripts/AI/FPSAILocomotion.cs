using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Uses the NavMeshAgent as a path solver only. Movement still goes
/// through the player's controller, with the same acceleration,
/// friction, air control and speed caps.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class FpsAiLocomotion : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FpsCharacterController characterController;
    [SerializeField] private NavMeshAgent agent;

    [Header("Settings")]
    [SerializeField] private FPSPersonality personality;

    [Tooltip("How close counts as having arrived.")]
    [SerializeField] private float arriveDistance = 1.2f;

    private CharacterController body;
    private NavMeshPath candidatePath;
    private NavMeshPath destinationPath;
    private readonly RaycastHit[] obstacleHits = new RaycastHit[16];
    private float nextRepathTime;
    private Vector3 requestedDestination;
    private Vector3 acceptedRequest;
    private Vector3 currentDestination;
    private Vector3 navigationPosition;
    private bool hasRequestedDestination;
    private bool hasDestination;
    private bool pathValid;
    private bool positionOnMesh;
    private Vector3 lastProgressPosition;
    private float lastProgressDistance;
    private float lastProgressTime;
    private float strafeSeed;
    private float nextSprintDecisionTime;
    private bool sprintChoice;
    private bool wasAllowedToSprint;

    public bool HasDestination => hasDestination;
    public bool IsStuck { get; private set; }
    public bool CanReachDestination =>
        isActiveAndEnabled && hasDestination && pathValid &&
        positionOnMesh && !IsStuck && AgentReady;

    public bool HasArrived
    {
        get
        {
            if (!hasRequestedDestination)
                return true;

            if (!hasDestination || !pathValid || !AgentReady)
                return false;

            // A newly requested goal must not count as reached while
            // the previous goal is waiting for its next path update.
            if ((requestedDestination - acceptedRequest).sqrMagnitude > 0.04f)
                return false;

            Vector3 offset = currentDestination - GroundPosition;
            float heightDifference = Mathf.Abs(offset.y);
            offset.y = 0f;
            float tolerance = Mathf.Max(0.05f, arriveDistance);

            return offset.sqrMagnitude <= tolerance * tolerance &&
                   heightDifference <= Mathf.Max(0.35f, body != null ? body.stepOffset : 0f) &&
                   (!agent.hasPath || agent.remainingDistance <= tolerance + agent.radius);
        }
    }

    public Vector3 CurrentPathDirection { get; private set; }

    private bool AgentReady =>
        agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

    private float RepathInterval =>
        Mathf.Max(0.05f, personality != null ? personality.repathInterval : 0.35f);

    private NavMeshQueryFilter NavigationFilter => new NavMeshQueryFilter
    {
        agentTypeID = agent.agentTypeID,
        areaMask = agent.areaMask
    };

    public Vector3 GroundPosition =>
        body != null
            ? transform.TransformPoint(body.center) -
              transform.up * body.height * Mathf.Abs(transform.lossyScale.y) * 0.5f
            : transform.position - Vector3.up * (agent != null ? agent.baseOffset : 0f);

    private void Awake()
    {
        if (characterController == null)
            characterController = GetComponent<FpsCharacterController>();

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        body = GetComponent<CharacterController>();
        candidatePath = new NavMeshPath();
        destinationPath = new NavMeshPath();
        ConfigureAgent();
        strafeSeed = Random.Range(0f, 100f);
        ResetProgress();
    }

    private void ConfigureAgent()
    {
        if (agent == null)
            return;

        agent.updatePosition = false;
        agent.updateRotation = false;
        agent.updateUpAxis = false;
        agent.acceleration = 999f;
        agent.angularSpeed = 999f;
        agent.stoppingDistance = Mathf.Max(0.05f, arriveDistance);
    }

    public void Initialise(FPSPersonality assignedPersonality)
    {
        personality = assignedPersonality;
        nextSprintDecisionTime = 0f;
    }

    /// <summary>
    /// Projects a tactical candidate onto this agent's navigation mesh
    /// and rejects disconnected islands and partial paths. Reuses its
    /// own path so probing candidates cannot replace the active route.
    /// </summary>
    public bool TryGetReachablePosition(Vector3 requested, out Vector3 reachable)
    {
        if (!isActiveAndEnabled)
        {
            reachable = requested;
            return false;
        }

        SyncAgentPosition();
        return TryCalculateReachablePosition(requested, candidatePath, out reachable);
    }

    private bool TryCalculateReachablePosition(
        Vector3 requested, NavMeshPath path, out Vector3 reachable)
    {
        reachable = requested;

        if (!AgentReady || !positionOnMesh ||
            !IsFinite(requested.x) || !IsFinite(requested.y) || !IsFinite(requested.z))
        {
            return false;
        }

        float projectionRadius = Mathf.Max(
            1f, Mathf.Max(agent.height, Mathf.Max(agent.radius * 2f, arriveDistance)));

        // Already projected navigation goals retain their exact floor.
        // For character-centred world positions, bias the wider search
        // down to foot height rather than selecting a floor overhead.
        if (!NavMesh.SamplePosition(requested, out NavMeshHit hit, 0.25f, NavigationFilter))
        {
            Vector3 footHeightRequest = requested + GroundPosition - transform.position;

            if (!NavMesh.SamplePosition(footHeightRequest, out hit, projectionRadius, NavigationFilter) ||
                Mathf.Abs(hit.position.y - footHeightRequest.y) > Mathf.Max(0.5f, agent.height * 0.5f))
            {
                return false;
            }
        }

        if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete)
            return false;

        reachable = hit.position;
        return true;
    }

    public void SetDestination(Vector3 worldPosition)
    {
        if (!isActiveAndEnabled)
            return;

        bool firstRequest = !hasRequestedDestination;
        requestedDestination = worldPosition;
        hasRequestedDestination = true;

        if (firstRequest)
        {
            nextRepathTime = 0f;
            ResetProgress();
        }

        RefreshPath();
    }

    private void RefreshPath()
    {
        if (!hasRequestedDestination || Time.time < nextRepathTime)
            return;

        nextRepathTime = Time.time + RepathInterval;
        SyncAgentPosition();

        if (!TryCalculateReachablePosition(requestedDestination, destinationPath, out Vector3 reachable) ||
            !agent.SetPath(destinationPath))
        {
            pathValid = false;
            hasDestination = false;

            if (AgentReady)
                agent.ResetPath();

            return;
        }

        // Small target motion can update the path without erasing a
        // genuine stuck condition every time the path is recalculated.
        bool goalChanged = !hasDestination ||
                           (reachable - currentDestination).sqrMagnitude > 1f;
        acceptedRequest = requestedDestination;
        currentDestination = reachable;
        hasDestination = true;
        pathValid = true;

        if (goalChanged)
            ResetProgress();
    }

    public void ClearDestination()
    {
        hasRequestedDestination = false;
        hasDestination = false;
        pathValid = false;
        nextRepathTime = 0f;
        ResetProgress();

        if (AgentReady)
            agent.ResetPath();
    }

    public void Tick(bool strafe, bool allowSprint)
    {
        Tick(strafe, allowSprint, transform.position + transform.forward);
    }

    public void Tick(bool strafe, bool allowSprint, Vector3 threatPosition)
    {
        if (!isActiveAndEnabled || characterController == null || !characterController.isActiveAndEnabled)
        {
            Stop();
            return;
        }

        SyncAgentPosition();
        RefreshPath();
        UpdateProgress();

        Vector3 pathDirection = ResolvePathDirection();
        Vector3 moveDirection = pathDirection;

        if (strafe && hasDestination && pathValid && positionOnMesh)
            moveDirection = ApplyStrafe(pathDirection, threatPosition);

        moveDirection = ResolveSafeDirection(moveDirection, pathDirection);
        CurrentPathDirection = moveDirection;

        if (moveDirection.sqrMagnitude < 0.0001f)
        {
            characterController.SetMoveInput(Vector2.zero);
            characterController.SetSprint(false);
            wasAllowedToSprint = false;
            return;
        }

        characterController.SetMoveDirection(moveDirection);
        HandleSprint(moveDirection, allowSprint);

        if (strafe)
            HandleJump();
    }

    public void Stop()
    {
        if (characterController != null)
        {
            characterController.SetMoveInput(Vector2.zero);
            characterController.SetSprint(false);
        }

        CurrentPathDirection = Vector3.zero;
        wasAllowedToSprint = false;
        ResetProgress();
    }

    private void OnDisable()
    {
        Stop();
        ClearDestination();
    }

    private void SyncAgentPosition()
    {
        positionOnMesh = false;

        if (!AgentReady)
            return;

        // Keep the solver at the physical character, without warping
        // the transform or transferring movement to the agent.
        agent.nextPosition = transform.position;
        float sampleRadius = Mathf.Max(1f, agent.height);

        if (!NavMesh.SamplePosition(GroundPosition, out NavMeshHit hit, sampleRadius, NavigationFilter))
            return;

        Vector3 offset = hit.position - GroundPosition;
        offset.y = 0f;
        positionOnMesh = offset.sqrMagnitude <=
                         Mathf.Pow(Mathf.Max(0.25f, agent.radius * 0.5f), 2f);
        navigationPosition = hit.position;
    }

    private Vector3 ResolvePathDirection()
    {
        if (!hasDestination || !pathValid || !positionOnMesh || !AgentReady || HasArrived)
            return Vector3.zero;

        if (agent.pathPending || !agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete)
        {
            pathValid = false;
            return Vector3.zero;
        }

        Vector3 direction = agent.desiredVelocity;

        // steeringTarget avoids allocating agent.path.corners every
        // frame, and still follows the next corner during avoidance.
        if (direction.sqrMagnitude < 0.0001f)
            direction = agent.steeringTarget - transform.position;

        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    private Vector3 ApplyStrafe(Vector3 pathDirection, Vector3 threatPosition)
    {
        if (personality == null || personality.strafeAmplitude <= 0f)
            return pathDirection;

        Vector3 toThreat = threatPosition - transform.position;
        toThreat.y = 0f;

        if (toThreat.sqrMagnitude < 0.0001f)
            return pathDirection;

        Vector3 right = Vector3.Cross(Vector3.up, toThreat.normalized);
        float wave = Mathf.PerlinNoise(
            strafeSeed, Time.time * Mathf.Max(0f, personality.strafeFrequency)) * 2f - 1f;
        Vector3 lateral = right * wave * Mathf.Clamp01(personality.strafeAmplitude);
        Vector3 result = Vector3.ClampMagnitude(pathDirection + lateral, 1f);

        if (CanMoveSafely(result))
            return result;

        // Dodge away from a nearby wall instead of persisting into it.
        Vector3 opposite = Vector3.ClampMagnitude(pathDirection - lateral, 1f);
        return CanMoveSafely(opposite) ? opposite : pathDirection;
    }

    private Vector3 ResolveSafeDirection(Vector3 desired, Vector3 pathDirection)
    {
        if (desired.sqrMagnitude < 0.0001f || !positionOnMesh || !AgentReady)
            return Vector3.zero;

        if (CanMoveSafely(desired))
            return desired;

        if (pathDirection.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        if (CanMoveSafely(pathDirection))
            return pathDirection;

        // A short local detour handles obstacles that have not carved
        // the baked mesh yet. The engine can choose another goal if
        // neither side produces real progress.
        Vector3 left = Quaternion.AngleAxis(-45f, Vector3.up) * pathDirection;
        Vector3 right = Quaternion.AngleAxis(45f, Vector3.up) * pathDirection;
        bool leftFirst = Mathf.Sin(strafeSeed) < 0f;
        Vector3 first = leftFirst ? left : right;
        Vector3 second = leftFirst ? right : left;

        if (CanMoveSafely(first))
            return first;

        return CanMoveSafely(second) ? second : Vector3.zero;
    }

    private bool CanMoveSafely(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f)
            return true;

        direction.y = 0f;
        direction.Normalize();
        float distance = Mathf.Max(agent.radius + 0.15f, characterController.HorizontalSpeed * 0.15f);
        Vector3 probe = navigationPosition + direction * distance;
        float maxHeightChange = body != null
            ? body.stepOffset + distance * Mathf.Tan(Mathf.Min(60f, body.slopeLimit) * Mathf.Deg2Rad)
            : distance;

        if (!NavMesh.SamplePosition(probe, out NavMeshHit end, Mathf.Max(0.3f, maxHeightChange), NavigationFilter))
            return false;

        Vector3 projection = end.position - probe;
        float heightChange = Mathf.Abs(projection.y);
        projection.y = 0f;

        if (projection.sqrMagnitude > 0.04f || heightChange > maxHeightChange ||
            NavMesh.Raycast(navigationPosition, end.position, out NavMeshHit edge, NavigationFilter))
        {
            return false;
        }

        return !HasBlockingObstacle(direction, distance);
    }

    private bool HasBlockingObstacle(Vector3 direction, float distance)
    {
        if (body == null)
            return false;

        Vector3 scale = transform.lossyScale;
        float radius = Mathf.Max(0.05f,
            body.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) - body.skinWidth);
        float halfHeight = Mathf.Max(radius, body.height * Mathf.Abs(scale.y) * 0.5f - body.skinWidth);
        Vector3 center = transform.TransformPoint(body.center);
        Vector3 extent = transform.up * (halfHeight - radius);
        // Horizontal clearance must respect the same step height as
        // the controller, so a low stair lip is not treated as a wall.
        Vector3 step = Mathf.Abs(direction.y) < 0.01f
            ? transform.up * Mathf.Min(body.stepOffset * Mathf.Abs(scale.y), extent.magnitude * 2f)
            : Vector3.zero;
        int count = Physics.CapsuleCastNonAlloc(
            center + extent, center - extent + step, radius, direction, obstacleHits,
            distance, Physics.AllLayers, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = obstacleHits[i].collider;

            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform) ||
                Physics.GetIgnoreLayerCollision(body.gameObject.layer, hit.gameObject.layer) ||
                Physics.GetIgnoreCollision(body, hit))
            {
                continue;
            }

            // Walkable slopes remain traversable; the navigation ray
            // has already checked that this movement stays on the mesh.
            if (Vector3.Dot(obstacleHits[i].normal, Vector3.up) > 0.65f)
                continue;

            return true;
        }

        return count == obstacleHits.Length;
    }

    private void ResetProgress()
    {
        lastProgressPosition = transform.position;
        lastProgressDistance = DistanceRemaining();
        lastProgressTime = Time.time;
        IsStuck = false;
    }

    private void UpdateProgress()
    {
        Vector3 travelled = transform.position - lastProgressPosition;
        travelled.y = 0f;

        if (!hasDestination || HasArrived ||
            (travelled.sqrMagnitude > 0.0225f && DistanceRemaining() < lastProgressDistance - 0.1f))
        {
            ResetProgress();
            return;
        }

        IsStuck = Time.time - lastProgressTime >= Mathf.Max(1f, RepathInterval * 3f);
    }

    private float DistanceRemaining()
    {
        if (AgentReady && agent.hasPath && IsFinite(agent.remainingDistance))
            return agent.remainingDistance;

        return Vector3.Distance(GroundPosition, currentDestination);
    }

    private void HandleSprint(Vector3 moveDirection, bool allowSprint)
    {
        if (!allowSprint || personality == null)
        {
            characterController.SetSprint(false);
            wasAllowedToSprint = false;
            return;
        }

        // Willingness is a chance, held for a short decision window
        // rather than a hard cutoff or a new coin toss each frame.
        if (!wasAllowedToSprint || Time.time >= nextSprintDecisionTime)
        {
            sprintChoice = Random.value < Mathf.Clamp01(personality.sprintWillingness);
            nextSprintDecisionTime = Time.time + Mathf.Max(0.75f, RepathInterval * 3f);
        }

        wasAllowedToSprint = true;
        float forwardAlignment = Vector3.Dot(transform.forward, moveDirection.normalized);
        Vector3 toGoal = currentDestination - GroundPosition;
        toGoal.y = 0f;
        float brakingDistance = Mathf.Max(2f, arriveDistance * 2f);
        characterController.SetSprint(
            sprintChoice && characterController.IsGrounded && !IsStuck &&
            forwardAlignment > 0.6f && toGoal.sqrMagnitude > brakingDistance * brakingDistance);
    }

    private void HandleJump()
    {
        if (personality == null || personality.jumpiness <= 0f ||
            !characterController.IsGrounded || IsStuck)
        {
            return;
        }

        FpsMovementSettings settings = characterController.MovementSettings;
        float jumpHeight = settings != null ? Mathf.Max(0f, settings.jumpHeight) : 1f;

        if (HasBlockingObstacle(Vector3.up, jumpHeight))
            return;

        if (Random.value < 1f - Mathf.Exp(-personality.jumpiness * Time.deltaTime))
            characterController.RequestJump();
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
