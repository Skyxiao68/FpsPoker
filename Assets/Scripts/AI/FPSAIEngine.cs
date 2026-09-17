using UnityEngine;
using UnityEngine.AI;


[RequireComponent(typeof(FpsCharacterController))]
public class FPSAIEngine : MonoBehaviour
{
    public enum AIState
    {
        Idle,
        Patrol,
        Engage,
        Reposition,
        Evade,
        Search
    }

    [Header("Personality")]
    public FPSPersonality personality;

    [Header("Drivers")]
    [SerializeField] private FpsAiPerception perception;
    [SerializeField] private FpsAiLocomotion locomotion;
    [SerializeField] private FpsAiAim aim;
    [SerializeField] private FpsAiTrigger trigger;

    [Header("Patrol")]
    [Tooltip("Optional. Leave empty to hold position when idle.")]
    [SerializeField] private Transform[] patrolPoints;

    [Header("Debug")]
    [SerializeField] private AIState currentState = AIState.Idle;

    public float HealthFraction { get; set; } = 1f;

    public AIState CurrentState => currentState;

    private int patrolIndex;
    private float evadeEndTime;
    private float stateEnterTime;

    private void Awake()
    {
        ResolveDrivers();
    }

    private void Start()
    {
        DistributePersonality();

        ChangeState(
            patrolPoints != null && patrolPoints.Length > 0
                ? AIState.Patrol
                : AIState.Idle
        );
    }

    private void ResolveDrivers()
    {
        if (perception == null)
            perception = GetComponent<FpsAiPerception>();

        if (locomotion == null)
            locomotion = GetComponent<FpsAiLocomotion>();

        if (aim == null)
            aim = GetComponent<FpsAiAim>();

        if (trigger == null)
            trigger = GetComponent<FpsAiTrigger>();
    }

    public void DistributePersonality()
    {
        if (personality == null)
        {
            Debug.LogWarning(
                $"{name}: No FPSPersonality assigned."
            );

            return;
        }

        perception?.Initialise(personality);
        locomotion?.Initialise(personality);
        aim?.Initialise(personality);
        trigger?.Initialise(personality);
    }


    private void Update()
    {
        if (personality == null)
            return;

        perception?.Tick();

        EvaluateTransitions();

        RunCurrentState();
    }


    private void EvaluateTransitions()
    {
        bool canSee =
            perception != null && perception.CanSeeTarget;

        bool remembers =
            perception != null && perception.HasRecentMemory;

        // Evade overrides everything until its timer expires.
        if (currentState == AIState.Evade)
        {
            if (Time.time < evadeEndTime)
                return;

            ChangeState(
                canSee ? AIState.Engage : AIState.Search
            );

            return;
        }

        if (ShouldEvade())
        {
            evadeEndTime =
                Time.time + personality.evadeDuration;

            ChangeState(AIState.Evade);
            return;
        }

        if (canSee)
        {
            float distance = perception.DistanceToTarget;

            bool tooClose = distance < personality.minRange;

            bool tooFar =
                distance > personality.preferredRange * 1.35f;

            ChangeState(
                tooClose || tooFar
                    ? AIState.Reposition
                    : AIState.Engage
            );

            return;
        }

        if (remembers)
        {
            ChangeState(AIState.Search);
            return;
        }

        ChangeState(
            patrolPoints != null && patrolPoints.Length > 0
                ? AIState.Patrol
                : AIState.Idle
        );
    }

    private bool ShouldEvade()
    {
        if (personality.evadeHealthThreshold <= 0f)
            return false;

        return HealthFraction <=
               personality.evadeHealthThreshold;
    }

    private void ChangeState(AIState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        stateEnterTime = Time.time;

        // Always drop the trigger on a state change so a burst
        // can't leak across states.
        trigger?.ReleaseTrigger();
    }

    // =========================================================
    // STATES
    // =========================================================

    private void RunCurrentState()
    {
        switch (currentState)
        {
            case AIState.Idle:
                RunIdle();
                break;

            case AIState.Patrol:
                RunPatrol();
                break;

            case AIState.Engage:
                RunEngage();
                break;

            case AIState.Reposition:
                RunReposition();
                break;

            case AIState.Evade:
                RunEvade();
                break;

            case AIState.Search:
                RunSearch();
                break;
        }
    }

    private void RunIdle()
    {
        locomotion?.Stop();
        aim?.Idle();
        trigger?.Tick(false);
    }

    private void RunPatrol()
    {
        if (patrolPoints == null || patrolPoints.Length == 0)
        {
            RunIdle();
            return;
        }

        Transform point = patrolPoints[patrolIndex];

        if (point == null)
        {
            AdvancePatrol();
            return;
        }

        locomotion?.SetDestination(point.position);

        if (locomotion != null && locomotion.HasArrived)
            AdvancePatrol();

        locomotion?.Tick(false, false);

        // Look where it's walking.
        aim?.LookTowardDirection(
            locomotion != null
                ? locomotion.CurrentPathDirection
                : transform.forward
        );

        trigger?.Tick(false);
    }

    private void AdvancePatrol()
    {
        patrolIndex =
            (patrolIndex + 1) % patrolPoints.Length;
    }

    private void RunEngage()
    {
        // Hold roughly at preferred range while strafing.
        Vector3 targetPosition =
            perception.Target.position;

        Vector3 away =
            transform.position - targetPosition;

        away.y = 0f;

        Vector3 holdPosition =
            targetPosition +
            away.normalized *
            personality.preferredRange;

        locomotion?.SetDestination(holdPosition);
        locomotion?.Tick(true, false);

        AimAtTarget();

        trigger?.Tick(true);
    }

    private void RunReposition()
    {
        Vector3 targetPosition =
            perception.Target != null
                ? perception.Target.position
                : perception.LastKnownPosition;

        float distance = perception.DistanceToTarget;

        Vector3 destination;

        if (distance < personality.minRange)
        {
            // Back off.
            Vector3 away =
                transform.position - targetPosition;

            away.y = 0f;

            destination =
                transform.position +
                away.normalized *
                (personality.minRange - distance + 2f);
        }
        else
        {
            // Close in. Aggression pulls the stopping point
            // tighter than preferred range.
            float closeRange =
                Mathf.Lerp(
                    personality.preferredRange,
                    personality.minRange,
                    personality.aggression
                );

            Vector3 toTarget =
                targetPosition - transform.position;

            toTarget.y = 0f;

            destination =
                targetPosition -
                toTarget.normalized * closeRange;
        }

        locomotion?.SetDestination(destination);
        locomotion?.Tick(true, true);

        AimAtTarget();

        // Aggressive personalities shoot while closing; cautious
        // ones close first, then fire.
        trigger?.Tick(personality.aggression > 0.5f);
    }

    private void RunEvade()
    {
        Vector3 threat =
            perception != null &&
            perception.HasRecentMemory
                ? perception.LastKnownPosition
                : transform.position + transform.forward;

        Vector3 away = transform.position - threat;
        away.y = 0f;

        Vector3 destination =
            transform.position +
            away.normalized * 12f;

        locomotion?.SetDestination(destination);
        locomotion?.Tick(true, true);

        // Keep eyes on the threat while backing off - this is what
        // makes "dodge and counterattack" read as deliberate.
        if (perception != null && perception.CanSeeTarget)
            AimAtTarget();
        else
            aim?.LookTowardDirection(away);

        trigger?.Tick(
            personality.combatStyle == CombatStyle.DodgeCounter
        );
    }

    private void RunSearch()
    {
        if (perception == null)
        {
            RunIdle();
            return;
        }

        locomotion?.SetDestination(
            perception.LastKnownPosition
        );

        locomotion?.Tick(false, true);

        Vector3 toLastKnown =
            perception.LastKnownPosition -
            transform.position;

        toLastKnown.y = 0f;

        aim?.LookTowardDirection(
            toLastKnown.sqrMagnitude > 0.01f
                ? toLastKnown
                : transform.forward
        );

        trigger?.Tick(false);
    }

    private void AimAtTarget()
    {
        if (aim == null || perception == null)
            return;

        aim.AimAt(
            perception.AimPoint,
            perception.TargetVelocity,
            perception.CanSeeTarget
        );
    }
}