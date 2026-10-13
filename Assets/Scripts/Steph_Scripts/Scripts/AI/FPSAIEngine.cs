using UnityEngine;

[RequireComponent(typeof(FpsCharacterController))]
[DefaultExecutionOrder(-100)]
public class FPSAIEngine : MonoBehaviour
{
    public enum AIState { Idle, Patrol, Engage, Reposition, Evade, Search }

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

    private Health health;
    private FpsCharacterController characterController;
    private Transform trackedTarget;
    private int patrolIndex;
    private float stateEnterTime;
    private float evadeEndTime;
    private float nextEvadeTime;
    private bool lowHealthRetreatUsed;
    private bool damagedSinceRetreat;
    private float alertEndTime;
    private float nextTacticalTime;
    private Vector3 tacticalDestination;
    private Vector3 evadeOrigin;
    private bool hasTacticalDestination;
    private Vector3 searchOrigin;
    private Vector3 searchForward;
    private Vector3 searchDestination;
    private bool searchUsesMemory;
    private bool hasSearchDestination;
    private float searchPauseEndTime;
    private int searchStep;
    private float orbitSide;

    private float DecisionInterval => Mathf.Max(0.05f, personality.repathInterval);
    private bool HasPatrolPoints => patrolPoints != null && patrolPoints.Length > 0;
    private AIState RestingState => HasPatrolPoints ? AIState.Patrol : AIState.Idle;

    // Archetypes use existing range/aggression controls without changing
    // the asset or raising the character's movement and aim limits.
    private float EngagementRange
    {
        get
        {
            float aggression = Mathf.Clamp01(personality.aggression);
            float pressure = personality.combatStyle == CombatStyle.Rusher
                ? Mathf.Lerp(0.65f, 1f, aggression)
                : aggression * (personality.combatStyle == CombatStyle.DodgeCounter ? 0.1f : 0.25f);
            float range = Mathf.Lerp(Mathf.Max(personality.minRange, personality.preferredRange),
                personality.minRange, pressure);
            Weapon weapon = characterController != null ? characterController.EquippedWeapon : null;
            if (weapon != null && weapon.Settings != null)
                range = Mathf.Min(range, Mathf.Max(0f, weapon.Settings.range) * 0.85f);
            return Mathf.Max(0.5f, range);
        }
    }

    private float MinimumRange => Mathf.Min(Mathf.Max(0f, personality.minRange), EngagementRange);

    private void Awake()
    {
        ResolveDrivers();
        health = GetComponent<Health>();
        characterController = GetComponent<FpsCharacterController>();
        orbitSide = Random.value < 0.5f ? -1f : 1f;
    }

    private void OnEnable()
    {
        if (health == null)
            health = GetComponent<Health>();
        if (health != null)
            health.OnDamaged += HandleDamaged;
        lowHealthRetreatUsed = false;
        damagedSinceRetreat = false;
        nextEvadeTime = 0f;
        alertEndTime = 0f;
        hasTacticalDestination = false;
        hasSearchDestination = false;
        trackedTarget = null;
        currentState = AIState.Idle;
        aim?.ResetTarget();
        perception?.ForgetTarget();
        trigger?.ReleaseTrigger();
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDamaged -= HandleDamaged;
        locomotion?.ClearDestination();
        locomotion?.Stop();
        aim?.Idle();
        trigger?.ReleaseTrigger();
    }

    private void HandleDamaged(int amount, int currentHealth)
    {
        HealthFraction = health.NormalizedHealth;
        damagedSinceRetreat = true;
        if (personality != null)
            alertEndTime = Time.time + Mathf.Max(0f, personality.memoryDuration);
    }

    private void Start()
    {
        DistributePersonality();
        ChangeState(RestingState);
    }

    private void ResolveDrivers()
    {
        if (perception == null) perception = GetComponent<FpsAiPerception>();
        if (locomotion == null) locomotion = GetComponent<FpsAiLocomotion>();
        if (aim == null) aim = GetComponent<FpsAiAim>();
        if (trigger == null) trigger = GetComponent<FpsAiTrigger>();
    }

    public void DistributePersonality()
    {
        ResolveDrivers();
        if (personality == null)
        {
            Debug.LogWarning($"{name}: No FPSPersonality assigned.");
            return;
        }
        perception?.Initialise(personality);
        locomotion?.Initialise(personality);
        aim?.Initialise(personality);
        trigger?.Initialise(personality);
        hasTacticalDestination = false;
    }

    private void Update()
    {
        if (health != null)
            HealthFraction = health.NormalizedHealth;
        if (personality == null || (health != null && health.IsDead))
        {
            RunIdle();
            return;
        }

        perception?.Tick();
        Transform observedTarget = perception != null ? perception.Target : null;
        if (observedTarget != trackedTarget)
        {
            trackedTarget = observedTarget;
            aim?.ResetTarget();
            trigger?.ReleaseTrigger();
            hasTacticalDestination = false;
        }
        if (HealthFraction > personality.evadeHealthThreshold)
            lowHealthRetreatUsed = false;

        EvaluateTransitions();
        RunCurrentState();
    }

    private void EvaluateTransitions()
    {
        bool canSee = perception != null && perception.CanSeeTarget;
        bool remembers = perception != null && perception.HasRecentMemory;

        if (currentState == AIState.Evade)
        {
            if (Time.time < evadeEndTime)
                return;
            // Leave a counterattack window instead of immediately entering
            // another retreat while health remains below the threshold.
            nextEvadeTime = Time.time + Mathf.Max(personality.evadeDuration,
                Mathf.Max(personality.burstCooldown, personality.reactionTime));
            ChangeState(canSee ? AIState.Engage : remembers ? AIState.Search : RestingState);
            return;
        }

        if ((canSee || remembers) && ShouldEvade())
        {
            lowHealthRetreatUsed = true;
            damagedSinceRetreat = false;
            evadeEndTime = Time.time + personality.evadeDuration;
            ChangeState(AIState.Evade);
            return;
        }

        if (canSee)
        {
            float distance = perception.DistanceToTarget;
            // Separate enter/exit distances prevent boundary jitter from
            // continually cancelling movement plans and weapon bursts.
            float margin = Mathf.Max(0.5f, EngagementRange * 0.1f);
            bool tooClose = distance < MinimumRange;
            bool tooFar = distance > EngagementRange * 1.35f;
            if (currentState == AIState.Reposition)
            {
                float closeExit = Mathf.Min(MinimumRange + margin, EngagementRange);
                tooClose = distance < closeExit;
                tooFar = distance > Mathf.Max(EngagementRange * 1.1f, closeExit + margin);
            }
            ChangeState(tooClose || tooFar ? AIState.Reposition : AIState.Engage);
            return;
        }

        ChangeState(remembers || Time.time < alertEndTime ? AIState.Search : RestingState);
    }

    private bool ShouldEvade()
    {
        return personality.evadeHealthThreshold > 0f && personality.evadeDuration > 0f &&
            HealthFraction <= personality.evadeHealthThreshold && Time.time >= nextEvadeTime &&
            (!lowHealthRetreatUsed || damagedSinceRetreat);
    }

    private void ChangeState(AIState newState)
    {
        if (currentState == newState)
            return;
        currentState = newState;
        stateEnterTime = Time.time;
        hasTacticalDestination = false;
        nextTacticalTime = 0f;
        locomotion?.ClearDestination();
        trigger?.ReleaseTrigger();
        if (newState == AIState.Search)
            BeginSearch();
        if (newState == AIState.Evade)
            evadeOrigin = transform.position;
    }

    private void RunCurrentState()
    {
        switch (currentState)
        {
            case AIState.Patrol: RunPatrol(); break;
            case AIState.Engage: RunCombat(false); break;
            case AIState.Reposition: RunCombat(true); break;
            case AIState.Evade: RunEvade(); break;
            case AIState.Search: RunSearch(); break;
            default: RunIdle(); break;
        }
    }

    private void RunIdle()
    {
        locomotion?.ClearDestination();
        locomotion?.Stop();
        aim?.Idle();
        trigger?.Tick(false);
    }

    private void RunPatrol()
    {
        trigger?.Tick(false);
        if (!HasPatrolPoints || locomotion == null)
        {
            RunIdle();
            return;
        }
        if (locomotion.IsStuck)
        {
            AdvancePatrol();
            locomotion.ClearDestination();
            nextTacticalTime = 0f;
        }
        if (Time.time >= nextTacticalTime)
        {
            nextTacticalTime = Time.time + DecisionInterval;
            // Skip missing/unreachable entries without retaining old input.
            bool found = false;
            for (int checkedPoints = 0; checkedPoints < patrolPoints.Length; checkedPoints++)
            {
                Transform point = patrolPoints[patrolIndex];
                if (point != null && locomotion.TryGetReachablePosition(point.position, out Vector3 reachable))
                {
                    locomotion.SetDestination(reachable);
                    found = true;
                    break;
                }
                AdvancePatrol();
            }
            if (!found)
            {
                RunIdle();
                return;
            }
        }
        locomotion.Tick(false, false);
        aim?.LookTowardDirection(locomotion.CurrentPathDirection);
        if (locomotion.HasDestination && locomotion.HasArrived)
        {
            AdvancePatrol();
            nextTacticalTime = 0f;
        }
    }

    private void AdvancePatrol() => patrolIndex = (patrolIndex + 1) % patrolPoints.Length;

    private void RunCombat(bool repositioning)
    {
        if (perception == null || !perception.CanSeeTarget || perception.Target == null)
        {
            trigger?.Tick(false);
            return;
        }
        Vector3 threat = perception.LastKnownPosition;
        UpdateCombatDestination(threat);
        bool closing = perception.DistanceToTarget > EngagementRange * 1.1f;
        bool strafe = !repositioning || personality.combatStyle != CombatStyle.Rusher;
        locomotion?.Tick(strafe, repositioning && closing, threat);
        AimAtTarget();
        trigger?.Tick(!repositioning || personality.aggression > 0.5f ||
            personality.combatStyle == CombatStyle.DodgeCounter);
    }

    private void UpdateCombatDestination(Vector3 threat)
    {
        if (locomotion == null || Time.time < nextTacticalTime)
            return;
        nextTacticalTime = Time.time + DecisionInterval;
        Vector3 away = FlatDirection(transform.position - threat);
        float range = EngagementRange;
        float bestScore = float.NegativeInfinity;
        Vector3 best = transform.position;
        bool found = false;
        Vector3 eyeOffset = characterController.CameraTransform.position - locomotion.GroundPosition;

        // Keep a good firing lane; don't chase an exact circle every frame.
        for (int i = -1; i < 8; i++)
        {
            Vector3 radial = Quaternion.AngleAxis(i * 45f, Vector3.up) * away;
            Vector3 requested = i < 0 ? transform.position : threat + radial * range;
            if (!locomotion.TryGetReachablePosition(requested, out Vector3 candidate))
                continue;
            float travel = Vector3.Distance(transform.position, candidate);
            float distance = Vector3.Distance(candidate, threat);
            float score = -Mathf.Abs(distance - range) * 2f - travel * 0.35f;
            if (distance < MinimumRange)
                score -= (MinimumRange - distance) * 4f;
            score += perception.HasLineOfSight(candidate + eyeOffset, perception.AimPoint) ? range : -range;
            if (personality.combatStyle == CombatStyle.RangedRoaming && i >= 0)
            {
                Vector3 tangent = Vector3.Cross(Vector3.up, away) * orbitSide;
                score += Vector3.Dot(FlatDirection(candidate - transform.position), tangent) *
                    personality.strafeAmplitude * range * 0.3f;
            }
            if (hasTacticalDestination && !locomotion.IsStuck &&
                (candidate - tacticalDestination).sqrMagnitude < 2.25f)
                score += range * 0.15f;
            if (locomotion.IsStuck && travel < 1.5f)
                score -= range;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                found = true;
            }
        }
        if (found)
        {
            tacticalDestination = best;
            hasTacticalDestination = true;
            locomotion.SetDestination(best);
        }
        else
        {
            hasTacticalDestination = false;
            locomotion.ClearDestination();
            locomotion.Stop();
        }
    }

    private void RunEvade()
    {
        if (perception == null)
        {
            RunIdle();
            return;
        }
        Vector3 threat = perception.LastKnownPosition;
        if (Time.time >= nextTacticalTime && locomotion != null)
        {
            nextTacticalTime = Time.time + DecisionInterval;
            Vector3 away = FlatDirection(evadeOrigin - threat);
            float retreatDistance = Mathf.Max(2f, EngagementRange * 0.65f);
            float bestScore = float.NegativeInfinity;
            bool found = false;
            Vector3 best = transform.position;
            for (int i = -1; i < 8; i++)
            {
                if (i < 0 && !hasTacticalDestination)
                    continue;
                Vector3 direction = Quaternion.AngleAxis(i * 45f, Vector3.up) * away;
                Vector3 requested = i < 0 ? tacticalDestination : evadeOrigin + direction * retreatDistance;
                if (!locomotion.TryGetReachablePosition(requested, out Vector3 candidate))
                    continue;
                float score = Vector3.Distance(candidate, threat) +
                    Vector3.Dot(FlatDirection(candidate - evadeOrigin), away) * retreatDistance;
                Vector3 eyeOffset = characterController.CameraTransform.position - locomotion.GroundPosition;
                // Prefer cover over merely running farther from the threat.
                if (!perception.HasLineOfSight(candidate + eyeOffset, perception.AimPoint))
                    score += retreatDistance * 3f;
                if (i < 0 && !locomotion.IsStuck)
                    score += retreatDistance;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    found = true;
                }
            }
            if (found)
            {
                tacticalDestination = best;
                hasTacticalDestination = true;
                locomotion.SetDestination(best);
            }
            else
            {
                locomotion.ClearDestination();
                locomotion.Stop();
            }
        }
        locomotion?.Tick(false, true, threat);
        bool counter = perception.CanSeeTarget && personality.combatStyle == CombatStyle.DodgeCounter;
        if (counter)
            AimAtTarget();
        else
            aim?.LookTowardDirection(locomotion != null && locomotion.CurrentPathDirection.sqrMagnitude > 0.01f
                ? locomotion.CurrentPathDirection : FlatDirection(transform.position - threat));
        trigger?.Tick(counter);
    }

    private void BeginSearch()
    {
        searchUsesMemory = perception != null && perception.HasRecentMemory;
        searchOrigin = searchUsesMemory ? perception.LastKnownPosition : transform.position;
        searchForward = searchUsesMemory && perception.LastKnownVelocity.sqrMagnitude > 0.01f
            ? FlatDirection(perception.LastKnownVelocity) : FlatDirection(transform.forward);
        searchDestination = searchOrigin;
        hasSearchDestination = false;
        searchStep = 0;
        searchPauseEndTime = 0f;
    }

    private void RunSearch()
    {
        trigger?.Tick(false);
        if (locomotion == null || perception == null)
        {
            RunIdle();
            return;
        }
        if (searchUsesMemory && !perception.HasRecentMemory)
            BeginSearch();
        if (searchUsesMemory && !hasSearchDestination)
        {
            if (locomotion.TryGetReachablePosition(searchOrigin, out searchDestination))
            {
                hasSearchDestination = true;
                locomotion.SetDestination(searchDestination);
            }
            else
                searchUsesMemory = false;
        }

        if (!searchUsesMemory || locomotion.HasArrived || !locomotion.CanReachDestination || searchPauseEndTime > 0f)
        {
            locomotion.Stop();
            if (searchPauseEndTime <= 0f)
                searchPauseEndTime = Time.time + Mathf.Max(personality.reactionTime, DecisionInterval);
            // Damage without a sighting causes a local sweep, never a
            // revelation of the shooter's hidden position.
            Vector3 scanDirection = Quaternion.AngleAxis(
                (Time.time - stateEnterTime) * Mathf.Min(personality.aimTurnSpeed, 120f), Vector3.up) * searchForward;
            aim?.LookTowardDirection(scanDirection);
            if (searchUsesMemory && Time.time >= searchPauseEndTime && Time.time >= nextTacticalTime)
            {
                nextTacticalTime = Time.time + DecisionInterval;
                float radius = Mathf.Max(1.5f, Mathf.Min(EngagementRange * 0.3f, personality.sightRange * 0.2f));
                Vector3 direction = Quaternion.AngleAxis(searchStep++ * 90f, Vector3.up) * searchForward;
                if (locomotion.TryGetReachablePosition(searchOrigin + direction * radius, out Vector3 candidate))
                {
                    searchDestination = candidate;
                    locomotion.SetDestination(candidate);
                    searchPauseEndTime = 0f;
                }
            }
            return;
        }

        locomotion.Tick(false, true);
        Vector3 pathDirection = locomotion.CurrentPathDirection;
        aim?.LookTowardDirection(pathDirection.sqrMagnitude > 0.01f
            ? pathDirection : searchDestination - transform.position);
    }

    private Vector3 FlatDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
            direction.y = 0f;
        }
        return direction.normalized;
    }

    private void AimAtTarget()
    {
        if (aim != null && perception != null)
            aim.AimAt(perception.AimPoint, perception.TargetVelocity, perception.CanSeeTarget);
    }
}
