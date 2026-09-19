using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Uses the NavMeshAgent as a path solver only - it never moves the
/// transform. The solved direction is handed to the same
/// SetMoveDirection the player's input would drive, so the AI is
/// bound by identical acceleration, friction, air control and
/// speed caps.
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

    private float nextRepathTime;
    private Vector3 currentDestination;
    private bool hasDestination;

    private float strafeSeed;

    public bool HasArrived =>
        !hasDestination ||
        Vector3.Distance(
            transform.position,
            currentDestination
        ) <= arriveDistance;

    public Vector3 CurrentPathDirection { get; private set; }

    private void Awake()
    {
        if (characterController == null)
        {
            characterController =
                GetComponent<FpsCharacterController>();
        }

        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        ConfigureAgent();

        strafeSeed = Random.Range(0f, 100f);
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
    }

    public void Initialise(FPSPersonality assignedPersonality)
    {
        personality = assignedPersonality;
    }

    public void SetDestination(Vector3 worldPosition)
    {
        currentDestination = worldPosition;
        hasDestination = true;

        if (Time.time < nextRepathTime)
            return;

        nextRepathTime =
            Time.time +
            (personality != null
                ? personality.repathInterval
                : 0.35f);

        if (agent != null && agent.isOnNavMesh)
            agent.SetDestination(worldPosition);
    }

    public void ClearDestination()
    {
        hasDestination = false;

        if (agent != null && agent.isOnNavMesh)
            agent.ResetPath();
    }

    public void Tick(bool strafe, bool allowSprint)
    {
        if (characterController == null)
            return;

        SyncAgentPosition();

        Vector3 moveDirection = ResolvePathDirection();

        if (strafe)
        {
            moveDirection =
                ApplyStrafe(moveDirection);
        }

        CurrentPathDirection = moveDirection;

        if (moveDirection.sqrMagnitude < 0.0001f)
        {
            characterController.SetMoveInput(Vector2.zero);
            characterController.SetSprint(false);
            return;
        }

        characterController.SetMoveDirection(moveDirection);

        HandleSprint(
            moveDirection,
            allowSprint
        );

        HandleJump();
    }

    public void Stop()
    {
        if (characterController == null)
            return;

        characterController.SetMoveInput(Vector2.zero);
        characterController.SetSprint(false);

        CurrentPathDirection = Vector3.zero;
    }

    private void SyncAgentPosition()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        agent.nextPosition = transform.position;
    }

    private Vector3 ResolvePathDirection()
    {
        if (!hasDestination || agent == null || !agent.isOnNavMesh)
            return Vector3.zero;

        if (HasArrived)
            return Vector3.zero;

        Vector3 direction = agent.desiredVelocity;


        if (direction.sqrMagnitude < 0.0001f &&
            agent.hasPath &&
            agent.path.corners.Length > 1)
        {
            direction =
                agent.path.corners[1] - transform.position;
        }

        direction.y = 0f;

        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector3.zero;
    }


    private Vector3 ApplyStrafe(Vector3 forwardDirection)
    {
        if (personality == null ||
            personality.strafeAmplitude <= 0f)
        {
            return forwardDirection;
        }

        Vector3 basis =
            forwardDirection.sqrMagnitude > 0.0001f
                ? forwardDirection
                : transform.forward;

        Vector3 right =
            Vector3.Cross(Vector3.up, basis).normalized;

        float wave =
            Mathf.PerlinNoise(
                strafeSeed,
                Time.time * personality.strafeFrequency
            ) * 2f - 1f;

        Vector3 result =
            forwardDirection +
            right * wave * personality.strafeAmplitude;

        result.y = 0f;

        return result.sqrMagnitude > 0.0001f
            ? result.normalized
            : Vector3.zero;
    }



    private void HandleSprint(
        Vector3 moveDirection,
        bool allowSprint)
    {
        if (!allowSprint || personality == null)
        {
            characterController.SetSprint(false);
            return;
        }


        float forwardAlignment =
            Vector3.Dot(
                transform.forward,
                moveDirection
            );

        bool wantsSprint =
            forwardAlignment > 0.6f &&
            personality.sprintWillingness > 0.5f;

        characterController.SetSprint(wantsSprint);
    }

    private void HandleJump()
    {
        if (personality == null ||
            personality.jumpiness <= 0f)
        {
            return;
        }

        if (!characterController.IsGrounded)
            return;

        if (Random.value <
            personality.jumpiness * Time.deltaTime)
        {
            characterController.RequestJump();
        }
    }
}