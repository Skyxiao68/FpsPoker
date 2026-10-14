using UnityEngine;

[RequireComponent(typeof(FpsCharacterController))]
[DefaultExecutionOrder(-100)]
public class FPSAIEngine : MonoBehaviour
{
    public enum AIState { Idle, Patrol, Engage, Reposition, Evade, Search, Prepare, Reload, UnderFire }

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
    private Vector3 searchFocus;
    private Vector3 searchDestination;
    private bool searchUsesMemory;
    private bool hasSearchDestination;
    private float searchPauseEndTime;
    private int searchStep;
    private float orbitSide;
    private float nextOrbitSwitchTime;
    private bool preDuel;
    private Transform preDuelOpponent;
    private Vector3 reloadThreatPosition;
    private Vector3 reloadThreatEye;
    private Transform reloadThreatRoot;
    private bool hasReloadThreat;
    private float underFireEndTime;
    private Vector3 underFireThreatPosition;
    private Vector3 underFireThreatEye;
    private Transform underFireThreatRoot;
    private readonly RaycastHit[] coverHits = new RaycastHit[32];

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
        underFireEndTime = 0f;
        underFireThreatRoot = null;
        hasTacticalDestination = false;
        hasSearchDestination = false;
        trackedTarget = null;
        preDuel = false;
        preDuelOpponent = null;
        hasReloadThreat = false;
        reloadThreatRoot = null;
        nextOrbitSwitchTime = Time.time + Random.Range(2f, 4f);
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
        preDuel = false;
        preDuelOpponent = null;
    }

    public void SetPreDuel(bool preparing, Transform opponent)
    {
        preDuel = preparing;
        preDuelOpponent = preparing ? opponent : null;
        trigger?.ReleaseTrigger();
        if (!preparing)
        {
            locomotion?.ClearDestination();
            perception?.ForgetTarget();
            aim?.ResetTarget();
        }
    }

    private void HandleDamaged(int amount, int currentHealth)
    {
        HealthFraction = health.NormalizedHealth;
        damagedSinceRetreat = true;
        if (personality == null || currentHealth <= 0)
            return;

        alertEndTime = Time.time + Mathf.Max(0f, personality.memoryDuration);
        underFireEndTime = Time.time + Mathf.Max(1.5f, personality.evadeDuration);

        if (health.LastDamageHasSource)
        {
            Vector3 shotOrigin = health.LastShotOrigin;
            Vector3 fromShot = shotOrigin - transform.position;
            float distance = fromShot.magnitude;
            float closeRange = Mathf.Clamp(personality.minRange * 1.5f, 5f, 10f);
            bool close = distance <= closeRange && health.LastAttacker != null;

            if (close)
            {
                perception?.AlertFromNearbyDamage(health.LastAttacker);
                underFireThreatPosition = health.LastAttacker.position;
                underFireThreatEye = shotOrigin;
                underFireThreatRoot = health.LastAttacker;
            }
            else
            {
                // Distant impacts reveal an incoming bearing, not the
                // shooter's exact position or identity.
                Vector3 bearing = FlatDirection(fromShot);
                float guessedDistance = Mathf.Max(closeRange,
                    Mathf.Min(personality.preferredRange, personality.sightRange * 0.6f));
                underFireThreatPosition = transform.position + bearing * guessedDistance;
                underFireThreatEye = underFireThreatPosition + Vector3.up * 1.5f;
                underFireThreatRoot = null;
            }
        }
        else if (perception != null && perception.HasRecentMemory)
        {
            underFireThreatPosition = perception.LastKnownPosition;
            underFireThreatEye = perception.AimPoint;
            underFireThreatRoot = perception.Target;
        }
        else
        {
            underFireThreatPosition = transform.position + transform.forward *
                Mathf.Max(5f, personality.minRange);
            underFireThreatEye = underFireThreatPosition + Vector3.up * 1.5f;
            underFireThreatRoot = null;
        }

        nextTacticalTime = 0f;
        ChangeState(AIState.UnderFire);
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

        if (preDuel)
        {
            ChangeState(AIState.Prepare);
            if (preDuelOpponent != null)
            {
                FpsCharacterController opponentController =
                    preDuelOpponent.GetComponent<FpsCharacterController>();
                Vector3 eye = opponentController != null
                    ? opponentController.CameraTransform.position :
                    preDuelOpponent.position + Vector3.up * 1.5f;
                RunCover(preDuelOpponent.position, eye, preDuelOpponent);
            }
            else
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
        bool remembers = perception != null && perception.HasSearchMemory;

        Weapon weapon = characterController != null ? characterController.EquippedWeapon : null;
        if (weapon != null && !weapon.IsReloading &&
            weapon.AmmoInMagazine < weapon.MagazineCapacity && !canSee &&
            (!remembers || weapon.AmmoInMagazine <= Mathf.Max(1, weapon.MagazineCapacity / 3)))
        {
            // Top up during a lull or when only a few rounds remain and
            // the opponent has gone behind cover.
            weapon.TryReload();
        }

        if (Time.time < underFireEndTime)
        {
            ChangeState(AIState.UnderFire);
            return;
        }

        if (weapon != null && weapon.IsReloading)
        {
            if (currentState != AIState.Reload)
            {
                hasReloadThreat = remembers;
                if (remembers)
                {
                    reloadThreatPosition = perception.LastKnownPosition;
                    reloadThreatEye = perception.AimPoint;
                    reloadThreatRoot = perception.Target;
                }
                ChangeState(AIState.Reload);
            }
            return;
        }

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
            bool attackOpportunity = HasAttackOpportunity(distance, weapon);
            bool tooFar = distance > EngagementRange * 1.35f && !attackOpportunity;
            if (currentState == AIState.Reposition)
            {
                float closeExit = Mathf.Min(MinimumRange + margin, EngagementRange);
                tooClose = distance < closeExit;
                tooFar = distance > Mathf.Max(EngagementRange * 1.1f, closeExit + margin) &&
                    !attackOpportunity;
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

    private bool HasAttackOpportunity(float distance, Weapon weapon)
    {
        if (perception == null || !perception.CanSeeTarget ||
            characterController == null || weapon == null ||
            weapon.Settings == null || weapon.IsReloading ||
            weapon.AmmoInMagazine <= 0)
            return false;

        float usefulRange = Mathf.Min(weapon.Settings.range * 0.9f,
            personality.sightRange * 0.65f);
        return distance >= MinimumRange && distance <= usefulRange &&
            perception.HasLineOfSight(characterController.CameraTransform.position,
                perception.AimPoint);
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
            case AIState.Reload: RunReload(); break;
            case AIState.UnderFire: RunUnderFire(); break;
            case AIState.Prepare: RunIdle(); break;
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
        Weapon weapon = characterController != null ? characterController.EquippedWeapon : null;
        if (weapon != null && !weapon.IsReloading &&
            weapon.AmmoInMagazine > 0 &&
            weapon.AmmoInMagazine <= Mathf.Max(1, weapon.MagazineCapacity / 3))
        {
            // With few rounds left, defend a lane and find cover instead
            // of pushing into a long exposed exchange. Keep taking safe
            // shots while moving, and reload once the lane is screened.
            RunCover(threat, perception.AimPoint, perception.Target, true);
            if (HasCoverFrom(perception.AimPoint,
                    characterController.CameraTransform.position, perception.Target))
                weapon.TryReload();
            AimAtTarget();
            trigger?.Tick(!weapon.IsReloading);
            return;
        }
        if (personality.strafeAmplitude > 0f &&
            (Time.time >= nextOrbitSwitchTime || (locomotion != null && locomotion.IsStuck)))
        {
            orbitSide = -orbitSide;
            nextOrbitSwitchTime = Time.time + Random.Range(2f, 4f);
            nextTacticalTime = 0f;
        }
        UpdateCombatDestination(threat);
        bool closing = perception.DistanceToTarget > EngagementRange * 1.1f;
        bool strafe = !repositioning || personality.combatStyle != CombatStyle.Rusher;
        locomotion?.Tick(strafe, repositioning && closing, threat);
        AimAtTarget();
        // A visible, aligned opponent is still a valid shot while closing
        // or retreating to the preferred range. Trigger checks the actual
        // sightline, weapon range, and readiness before pressing.
        trigger?.Tick(true);
    }

    private void UpdateCombatDestination(Vector3 threat)
    {
        if (locomotion == null || Time.time < nextTacticalTime)
            return;
        nextTacticalTime = Time.time + DecisionInterval;
        Vector3 away = FlatDirection(transform.position - threat);
        Weapon weapon = characterController != null ? characterController.EquippedWeapon : null;
        bool goodLane = HasAttackOpportunity(perception.DistanceToTarget, weapon);
        float range = goodLane && personality.combatStyle != CombatStyle.Rusher
            ? Mathf.Max(EngagementRange, perception.DistanceToTarget)
            : EngagementRange;
        float bestScore = float.NegativeInfinity;
        Vector3 best = transform.position;
        bool found = false;
        Vector3 eyeOffset = characterController.CameraTransform.position - locomotion.GroundPosition;
        Vector3 tangent = Vector3.Cross(Vector3.up, away) * orbitSide;
        float sideStep = Mathf.Min(5.5f,
            range * 0.35f * Mathf.Clamp01(personality.strafeAmplitude));
        bool currentlyCovered = HasCoverFrom(perception.AimPoint,
            characterController.CameraTransform.position, perception.Target);

        // Keep a good firing lane; don't chase an exact circle every frame.
        for (int i = -1; i < 10; i++)
        {
            Vector3 radial = Quaternion.AngleAxis(i * 45f, Vector3.up) * away;
            if (i >= 8 && sideStep < 0.5f)
                continue;
            Vector3 requested = i < 0 ? transform.position :
                i < 8 ? threat + radial * range :
                transform.position + tangent * (i == 8 ? sideStep : -sideStep);
            if (!locomotion.TryGetReachablePosition(requested, out Vector3 candidate))
                continue;
            float travel = Vector3.Distance(transform.position, candidate);
            float distance = Vector3.Distance(candidate, threat);
            float score = -Mathf.Abs(distance - range) * 2f - travel * 0.35f;
            if (distance < MinimumRange)
                score -= (MinimumRange - distance) * 4f;
            score += perception.HasLineOfSight(candidate + eyeOffset, perception.AimPoint) ? range : -range;
            bool covered = HasCoverFrom(perception.AimPoint, candidate + eyeOffset,
                perception.Target);
            if (covered)
                score += range * 0.25f;
            else if (currentlyCovered)
                score -= range * 0.2f;
            if (i >= 8)
            {
                // Moving a few metres sideways makes a firing position
                // harder to hold against than a stationary range target.
                Vector3 displacement = candidate - transform.position;
                displacement.y = 0f;
                score += Vector3.Dot(displacement, tangent) * 1.5f;
            }
            else if (personality.combatStyle == CombatStyle.RangedRoaming && i >= 0)
            {
                Vector3 displacement = candidate - transform.position;
                displacement.y = 0f;
                if (displacement.sqrMagnitude > 0.01f)
                    score += Vector3.Dot(displacement.normalized, tangent) *
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

    private void RunReload()
    {
        if (perception != null && perception.CanSeeTarget)
        {
            hasReloadThreat = true;
            reloadThreatPosition = perception.LastKnownPosition;
            reloadThreatEye = perception.AimPoint;
            reloadThreatRoot = perception.Target;
        }

        if (hasReloadThreat)
            RunCover(reloadThreatPosition, reloadThreatEye, reloadThreatRoot);
        else
            RunIdle();
    }

    private void RunUnderFire()
    {
        // Movement starts on the next AI update after damage, including
        // while the weapon is reloading or the attacker is out of view.
        bool canCounter = perception != null && perception.CanSeeTarget;
        RunCover(underFireThreatPosition, underFireThreatEye,
            underFireThreatRoot, canCounter);

        if (canCounter)
        {
            AimAtTarget();
            Weapon weapon = characterController != null
                ? characterController.EquippedWeapon : null;
            trigger?.Tick(weapon != null && !weapon.IsReloading);
        }
        else
        {
            aim?.LookTowardDirection(underFireThreatPosition - transform.position);
            trigger?.Tick(false);
        }
    }

    private void RunCover(Vector3 threat, Vector3 threatEye, Transform threatRoot,
        bool preserveCombatAim = false)
    {
        if (!preserveCombatAim)
            trigger?.Tick(false);
        if (locomotion == null || characterController == null)
        {
            RunIdle();
            return;
        }

        if (Time.time >= nextTacticalTime || locomotion.IsStuck)
        {
            nextTacticalTime = Time.time + DecisionInterval;
            Vector3 away = FlatDirection(transform.position - threat);
            float step = Mathf.Max(2.5f, Mathf.Min(EngagementRange * 0.5f, 7f));
            Vector3 eyeOffset = characterController.CameraTransform.position - locomotion.GroundPosition;
            float bestScore = float.NegativeInfinity;
            bool found = false;
            Vector3 best = transform.position;

            for (int i = -1; i < 8; i++)
            {
                Vector3 direction = Quaternion.AngleAxis(i * 45f, Vector3.up) * away;
                Vector3 requested = i < 0 ? transform.position : transform.position + direction * step;
                if (!locomotion.TryGetReachablePosition(requested, out Vector3 candidate))
                    continue;

                float travel = Vector3.Distance(transform.position, candidate);
                float distance = Vector3.Distance(candidate, threat);
                bool covered = HasCoverFrom(threatEye, candidate + eyeOffset, threatRoot);
                float score = (covered ? step * 4f : 0f) + distance * 0.3f - travel * 0.4f;
                // When a hit has confirmed that this position is exposed,
                // moving out of the firing lane beats waiting in place.
                if (currentState == AIState.UnderFire)
                    score += travel < 1f ? -step * 3f : step;
                if (hasTacticalDestination && !locomotion.IsStuck &&
                    (candidate - tacticalDestination).sqrMagnitude < 2.25f)
                    score += step;
                if (locomotion.IsStuck && travel < 1.5f)
                    score -= step * 2f;
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = candidate;
                found = true;
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
            }
        }

        locomotion.Tick(false, true, threat);
        Vector3 path = locomotion.CurrentPathDirection;
        if (!preserveCombatAim)
            aim?.LookTowardDirection(path.sqrMagnitude > 0.01f
                ? path : threat - transform.position);
    }

    private bool HasCoverFrom(Vector3 source, Vector3 candidateEye, Transform threatRoot)
    {
        Vector3 ray = candidateEye - source;
        float distance = ray.magnitude;
        if (distance < 0.1f)
            return false;

        int count = Physics.RaycastNonAlloc(source, ray / distance, coverHits,
            distance - 0.05f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        if (count == coverHits.Length)
            return false;

        for (int i = 0; i < count; i++)
        {
            Collider hit = coverHits[i].collider;
            if (hit == null || hit.transform.IsChildOf(transform) ||
                (threatRoot != null && hit.transform.IsChildOf(threatRoot)) ||
                (CombatStartSequence.Active != null &&
                 CombatStartSequence.Active.ContainsCollider(hit)))
                continue;

            return true;
        }

        return false;
    }

    private void BeginSearch()
    {
        searchUsesMemory = perception != null && perception.HasSearchMemory;
        searchOrigin = searchUsesMemory ? perception.LastKnownPosition : transform.position;
        searchForward = searchUsesMemory && perception.LastKnownVelocity.sqrMagnitude > 0.01f
            ? FlatDirection(perception.LastKnownVelocity)
            : searchUsesMemory
                ? FlatDirection(searchOrigin - transform.position)
            : Time.time < alertEndTime
                ? FlatDirection(underFireThreatPosition - transform.position)
                : FlatDirection(transform.forward);
        searchFocus = searchOrigin;
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
        if (searchUsesMemory && !perception.HasSearchMemory)
        {
            searchUsesMemory = false;
            hasSearchDestination = false;
            locomotion.ClearDestination();
        }

        if (!searchUsesMemory)
        {
            locomotion.Stop();
            SweepSearchArea(searchOrigin + searchForward * Mathf.Max(3f, MinimumRange));
            return;
        }

        if (hasSearchDestination &&
            (locomotion.IsStuck || !locomotion.CanReachDestination))
        {
            locomotion.ClearDestination();
            hasSearchDestination = false;
            searchPauseEndTime = 0f;
            searchStep++;
        }

        if (hasSearchDestination && locomotion.HasArrived)
        {
            locomotion.Stop();
            if (searchPauseEndTime <= 0f)
                searchPauseEndTime = Time.time +
                    Mathf.Max(0.6f, personality.reactionTime * 2f);
            SweepSearchArea(searchFocus);
            if (Time.time >= searchPauseEndTime)
            {
                searchStep++;
                hasSearchDestination = false;
                searchPauseEndTime = 0f;
                locomotion.ClearDestination();
            }
            return;
        }

        if (!hasSearchDestination && Time.time >= nextTacticalTime)
        {
            nextTacticalTime = Time.time + DecisionInterval;
            if (!ChooseSearchDestination())
                searchStep++;
        }

        if (!hasSearchDestination)
        {
            locomotion.Stop();
            SweepSearchArea(searchFocus);
            return;
        }

        locomotion.Tick(false, true, searchFocus);
        aim?.LookTowardDirection(searchFocus - characterController.CameraTransform.position);
    }

    private bool ChooseSearchDestination()
    {
        Vector3 center = perception.EstimatedSearchCenter;
        float reach = Mathf.Min(perception.PossibleTravelDistance,
            personality.sightRange * 0.7f);
        Vector3 side = Vector3.Cross(Vector3.up, searchForward);
        float offset = reach * 0.65f;
        switch (searchStep % 6)
        {
            case 0: searchFocus = center; break;
            case 1: searchFocus = center + searchForward * offset; break;
            case 2: searchFocus = center + side * offset; break;
            case 3: searchFocus = center - side * offset; break;
            case 4: searchFocus = center - searchForward * offset * 0.75f; break;
            default: searchFocus = center + searchForward * reach; break;
        }

        float standoff = Mathf.Max(MinimumRange + 1f,
            Mathf.Min(EngagementRange * 0.55f, personality.sightRange * 0.25f));
        Vector3 towardUs = FlatDirection(transform.position - searchFocus);
        Vector3 threatEye = searchOrigin + Vector3.up * 1.5f;
        Vector3 eyeOffset = characterController.CameraTransform.position - locomotion.GroundPosition;
        bool currentlyCovered = HasCoverFrom(threatEye,
            characterController.CameraTransform.position, perception.Target);
        float bestScore = float.NegativeInfinity;
        bool found = false;
        Vector3 best = transform.position;

        // Probe a few observation angles. Never path directly to a guessed
        // player position; stop outside the likely contact distance.
        for (int i = 0; i < 5; i++)
        {
            float angle = i == 0 ? 0f : (i % 2 == 1 ? 1f : -1f) *
                (i < 3 ? 55f : 105f);
            Vector3 requested = searchFocus +
                Quaternion.AngleAxis(angle, Vector3.up) * towardUs * standoff;
            if (!locomotion.TryGetReachablePosition(requested, out Vector3 candidate))
                continue;

            float distanceToFocus = Vector3.Distance(candidate, searchFocus);
            if (distanceToFocus < MinimumRange + 0.5f)
                continue;

            Vector3 candidateEye = candidate + eyeOffset;
            bool covered = HasCoverFrom(threatEye, candidateEye, perception.Target);
            bool canObserve = perception.HasLineOfSight(candidateEye,
                searchFocus + Vector3.up * 1.5f);
            float travel = Vector3.Distance(candidate, transform.position);
            float score = (covered ? standoff * 1.3f : 0f) +
                (canObserve ? standoff : -standoff * 0.35f) -
                Mathf.Abs(distanceToFocus - standoff) - travel * 0.45f;
            if (currentlyCovered && !covered)
                score -= standoff;
            if (searchStep > 0 && travel < 1.5f)
                score -= standoff;
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
                found = true;
            }
        }

        if (!found)
            return false;

        searchDestination = best;
        hasSearchDestination = true;
        locomotion.SetDestination(searchDestination);
        return true;
    }

    private void SweepSearchArea(Vector3 focus)
    {
        Vector3 direction = focus - characterController.CameraTransform.position;
        if (direction.sqrMagnitude < 0.01f)
            direction = searchForward;
        float width = searchUsesMemory
            ? Mathf.Clamp(25f + perception.PossibleTravelDistance, 25f, 75f)
            : 60f;
        float sweep = Mathf.Sin((Time.time - stateEnterTime) *
            Mathf.Max(0.8f, personality.aimTurnSpeed / 140f)) * width;
        aim?.LookTowardDirection(Quaternion.AngleAxis(sweep, Vector3.up) * direction);
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
