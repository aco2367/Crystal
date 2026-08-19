using UnityEngine;

using System.Collections;
using System.Collections.Generic;

public enum EnemyKind
{
    Basic,
    Ranged,
    Tank,
    Boss
}

[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Type")]
    public EnemyKind enemyKind = EnemyKind.Basic;
    [Tooltip("켜면 적 종류별 코드 프리셋이 Inspector의 체력과 공격력 등을 덮어씁니다.")]
    public bool applyPresetOnStart;
    [Tooltip("프리셋의 크기와 전투 방식은 적용하되 Inspector의 Max Hp와 Attack Damage는 유지합니다.")]
    public bool preserveInspectorCombatStats = true;

    [Header("Stats")]
    public int maxHp = 30;
    public int attackDamage = 10;
    public float moveSpeed = 2f;
    public float attackRange = 1.2f;
    public float attackCooldown = 1f;
    public float stopDistance = 0.8f;

    [Header("Attack Timing")]
    [Tooltip("체크하면 공격 중에는 이동하지 않습니다.")]
    public bool lockMovementWhileAttacking = true;
    [Tooltip("공격 애니메이션 시작 후 실제 피해가 들어갈 때까지의 시간입니다.")]
    [Min(0f)] public float attackHitDelay = 0.35f;
    [Tooltip("공격 시작 후 이동을 다시 허용할 때까지의 시간입니다.")]
    [Min(0f)] public float attackMovementLockDuration = 0.7f;
    [Tooltip("체크하면 이 설정을 탱커 적에게만 적용합니다.")]
    public bool useAttackTimingForTankOnly = true;
    [Tooltip("타격 순간 대상이 공격 범위를 벗어났을 때 허용할 추가 거리입니다.")]
    [Min(0f)] public float attackHitRangeTolerance = 0.25f;

    [Header("Ranged")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 7f;
    public float rangedPreferredDistance = 4f;

    [Header("Defense Phase")]
    public float playerAggroRange = 2.5f;
    public bool preferPlayerWhenClose = true;
    [Tooltip("HomeBase가 이 거리 안에 있으면 남은 웨이포인트를 생략하고 집으로 직행합니다. 실제 공격 거리는 Attack Range를 사용합니다.")]
    [Min(0f)] public float homeBaseWaypointBypassDistance = 3f;

    [Header("Player Detection")]
    public bool requirePlayerDetection = true;
    public float playerDetectionRange = 6f;
    public bool drawDetectionRangeAlways = true;
    public Color detectionRangeColor = new Color(1f, 0.75f, 0f, 0.35f);

    [Header("Pathfinding")]
    public bool useAStarMovement;
    public AStarPathfinder2D aStarPathfinder;
    public bool fallbackToDirectMovementWhenPathFails;
    public float aStarRepathInterval = 1.5f;
    public float aStarTargetMoveThreshold = 1f;
    public float pathWaypointReachDistance = 0.15f;
    public EnemyMovementMode movementMode = EnemyMovementMode.Default;
    public float waypointReachDistance = 0.2f;
    [Tooltip("체크하면 웨이포인트 이동을 시작할 때 현재 위치에서 가장 가까운 지점부터 이동합니다.")]
    public bool startFromNearestWaypoint = true;

    [Header("Collision")]
    public bool ignoreSameLayerCollision = true;
    public bool warnIfIgnoringDefaultLayer = true;

    [Header("Enemy Separation")]
    [Tooltip("적들이 완전히 같은 위치에 겹쳐 한 마리처럼 보이는 것을 방지합니다.")]
    public bool useEnemySeparation = true;
    [Tooltip("이 거리 안에 있는 다른 적과 서로 벌어집니다. 크게 할수록 적 사이 간격이 넓어집니다.")]
    [Min(0.01f)] public float separationRadius = 0.4f;
    [Tooltip("서로 벌어지는 힘입니다. 낮게 설정하면 어느 정도 겹친 상태를 유지할 수 있습니다.")]
    [Min(0f)] public float separationStrength = 1f;
    [Tooltip("분리 때문에 추가되는 최대 이동 속도입니다.")]
    [Min(0f)] public float maxSeparationSpeed = 0.75f;
    [Tooltip("분리 대상으로 검사할 레이어입니다. 적 전용 레이어만 선택하는 것을 권장합니다.")]
    public LayerMask separationLayerMask = ~0;

    [Header("Visual")]
    public SpriteRenderer spriteRenderer;
    public Animator animator;
    public string movingParameter = "IsMoving";
    public string attackTriggerParameter = "Attack";
    public bool useHorizontalFlip = true;
    [Tooltip("원본 몬스터 스프라이트가 왼쪽을 바라보면 체크합니다.")]
    public bool spritesFaceLeft;
    [Min(0f)] public float flipDirectionThreshold = 0.01f;
    public Vector2 visualScale = Vector2.one;

    private EnemyHealth health;
    private Rigidbody2D rb;
    private Collider2D enemyCollider;
    private Transform player;
    private PlayerStats playerStats;
    private Transform homeTarget;
    private HomeBase targetHomeBase;
    private float lastAttackTime;
    private bool isAttacking;
    private Coroutine meleeAttackCoroutine;
    private readonly List<Vector2> currentPath = new List<Vector2>();
    private readonly List<Transform> assignedWaypoints = new List<Transform>();
    private Coroutine temporaryMoveSpeedCoroutine;
    private int currentPathIndex;
    private int currentWaypointIndex;
    private float nextAStarRepathTime;
    private Vector2 lastAStarTargetPosition;
    private readonly Collider2D[] separationResults = new Collider2D[24];

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        rb = GetComponent<Rigidbody2D>();
        enemyCollider = GetComponent<Collider2D>();
        aStarPathfinder = aStarPathfinder != null ? aStarPathfinder : AStarPathfinder2D.Instance;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        ConfigureEnemyCollision();
    }

    private void Start()
    {
        if (applyPresetOnStart)
        {
            ApplyPresetWithOptionalStatPreservation(enemyKind);
        }

        health.Setup(maxHp);
        FindPlayer();
    }

    private void FixedUpdate()
    {
        FindPlayerIfNeeded();

        if (ShouldUseTimedMeleeAttack() && isAttacking)
        {
            StopMovement();
            return;
        }

        HandleMovement();
        ApplyEnemySeparation();
    }

    private void HandleMovement()
    {
        if (TryHandleWaypointMovement())
            return;

        Transform moveTarget = GetMoveTarget();

        if (moveTarget == null)
        {
            StopMovement();
            return;
        }

        Vector2 direction = ((Vector2)moveTarget.position - rb.position).normalized;
        float distance = GetDistanceToTarget(moveTarget);

        if (ShouldUseAStarMovement())
        {
            HandleAStarMovement(moveTarget, direction, distance);
            return;
        }

        if (enemyKind == EnemyKind.Ranged)
        {
            HandleRangedMovement(direction, distance);
        }
        else
        {
            HandleMeleeMovement(direction, distance);
        }
    }

    private void ApplyEnemySeparation()
    {
        if (!useEnemySeparation || rb == null || separationRadius <= 0f ||
            separationStrength <= 0f || maxSeparationSpeed <= 0f)
        {
            return;
        }

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            rb.position,
            separationRadius,
            separationResults,
            separationLayerMask);

        Vector2 separation = Vector2.zero;
        int neighborCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = separationResults[i];

            if (hit == null || hit == enemyCollider)
                continue;

            EnemyController other = hit.GetComponentInParent<EnemyController>();

            if (other == null || other == this || !other.isActiveAndEnabled)
                continue;

            Vector2 away = rb.position - (Vector2)other.transform.position;
            float distance = away.magnitude;

            // 두 적의 중심이 완전히 같아도 서로 반대 방향으로 갈라지게 합니다.
            if (distance <= 0.001f)
            {
                away = GetInstanceID() < other.GetInstanceID() ? Vector2.left : Vector2.right;
                distance = 0.001f;
            }

            float proximity = 1f - Mathf.Clamp01(distance / separationRadius);
            separation += away.normalized * proximity;
            neighborCount++;
        }

        if (neighborCount == 0)
            return;

        Vector2 separationVelocity = separation / neighborCount * separationStrength;
        separationVelocity = Vector2.ClampMagnitude(separationVelocity, maxSeparationSpeed);
        rb.linearVelocity += separationVelocity;
    }

    private void Update()
    {
        FindPlayerIfNeeded();

        Transform attackTarget = GetAttackTarget();

        if (attackTarget == null)
            return;

        float distance = GetDistanceToTarget(attackTarget);

        if (distance > attackRange)
            return;

        if (enemyKind == EnemyKind.Ranged)
        {
            TryShoot(attackTarget);
        }
        else
        {
            TryMeleeAttack(attackTarget);
        }
    }

    public void SetTarget(Transform newTarget, HomeBase homeBaseTarget = null)
    {
        homeTarget = newTarget;
        targetHomeBase = homeBaseTarget;
        FindPlayer();
    }

    public void ConfigureMovement(EnemyMovementMode newMovementMode, List<Transform> waypoints)
    {
        movementMode = newMovementMode;
        assignedWaypoints.Clear();
        currentWaypointIndex = 0;

        if (waypoints == null)
            return;

        foreach (Transform waypoint in waypoints)
        {
            if (waypoint != null)
                assignedWaypoints.Add(waypoint);
        }

        if (startFromNearestWaypoint && assignedWaypoints.Count > 1)
            currentWaypointIndex = FindNearestWaypointIndex();
    }

    private int FindNearestWaypointIndex()
    {
        return FindNearestWaypointIndex(0);
    }

    private int FindNearestWaypointIndex(int startIndex)
    {
        int clampedStartIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, assignedWaypoints.Count - 1));
        int nearestIndex = clampedStartIndex;
        float nearestSqrDistance = float.MaxValue;
        Vector2 currentPosition = rb != null ? rb.position : (Vector2)transform.position;

        for (int i = clampedStartIndex; i < assignedWaypoints.Count; i++)
        {
            Transform waypoint = assignedWaypoints[i];

            if (waypoint == null)
                continue;

            float sqrDistance = ((Vector2)waypoint.position - currentPosition).sqrMagnitude;

            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    private Transform GetMoveTarget()
    {
        if (ShouldTargetPlayer())
            return player;

        if (homeTarget != null)
            return homeTarget;

        return player;
    }

    private Transform GetAttackTarget()
    {
        if (ShouldTargetPlayer())
            return player;

        if (homeTarget != null)
            return homeTarget;

        return player;
    }

    private bool ShouldTargetPlayer()
    {
        if (player == null || playerStats == null || playerStats.isDead)
            return false;

        if (requirePlayerDetection && !IsPlayerInDetectionRange())
            return false;

        if (homeTarget == null)
            return true;

        if (!preferPlayerWhenClose)
            return false;

        float distanceToPlayer = GetDistanceToTarget(player);
        return distanceToPlayer <= playerAggroRange;
    }

    private bool IsPlayerInDetectionRange()
    {
        if (player == null)
            return false;

        if (playerDetectionRange <= 0f)
            return true;

        return Vector2.Distance(transform.position, player.position) <= playerDetectionRange;
    }

    private float GetDistanceToTarget(Transform target)
    {
        if (target == null)
            return float.MaxValue;

        Collider2D targetCollider = target.GetComponent<Collider2D>();

        if (targetCollider == null)
            targetCollider = target.GetComponentInChildren<Collider2D>();

        if (enemyCollider != null && targetCollider != null)
        {
            Vector2 enemyPoint = enemyCollider.ClosestPoint(targetCollider.transform.position);
            Vector2 targetPoint = targetCollider.ClosestPoint(transform.position);
            return Vector2.Distance(enemyPoint, targetPoint);
        }

        return Vector2.Distance(transform.position, target.position);
    }

    private float GetDistanceToHomeBase()
    {
        if (targetHomeBase != null)
            return GetDistanceToTarget(targetHomeBase.transform);

        return GetDistanceToTarget(homeTarget);
    }

    private void HandleMeleeMovement(Vector2 direction, float distance)
    {
        if (distance <= stopDistance)
        {
            StopMovement();
            return;
        }

        rb.linearVelocity = direction * moveSpeed;
        SetMovingAnimation(true);
    }

    private void HandleRangedMovement(Vector2 direction, float distance)
    {
        if (distance > rangedPreferredDistance)
        {
            rb.linearVelocity = direction * moveSpeed;
            SetMovingAnimation(true);
            return;
        }

        if (distance < rangedPreferredDistance * 0.7f)
        {
            rb.linearVelocity = -direction * moveSpeed;
            SetMovingAnimation(true);
            return;
        }

        rb.linearVelocity = Vector2.zero;
        SetMovingAnimation(false);
    }

    private bool TryHandleWaypointMovement()
    {
        bool usesWaypoints = movementMode == EnemyMovementMode.Waypoint
            || movementMode == EnemyMovementMode.WaypointThenDirect
            || movementMode == EnemyMovementMode.WaypointThenAStar;

        // 플레이어가 어그로 범위 안에 있으면 웨이포인트를 잠시 중단하고 플레이어를 추적합니다.
        // 플레이어가 범위를 벗어나면 현재 인덱스 이후의 가장 가까운 웨이포인트부터 재개합니다.
        if (usesWaypoints && ShouldTargetPlayer())
            return false;

        bool canContinueToHome = movementMode == EnemyMovementMode.WaypointThenDirect
            || movementMode == EnemyMovementMode.WaypointThenAStar;

        // 적이 밀리거나 유인되어 이미 집 앞에 도착했다면 남은 경로를 되짚지 않고 바로 공격합니다.
        if (usesWaypoints && canContinueToHome && homeTarget != null &&
            homeBaseWaypointBypassDistance > 0f &&
            GetDistanceToHomeBase() <= homeBaseWaypointBypassDistance)
        {
            currentWaypointIndex = assignedWaypoints.Count;
            return false;
        }

        if (!usesWaypoints || assignedWaypoints.Count == 0 || currentWaypointIndex >= assignedWaypoints.Count)
            return false;

        currentWaypointIndex = FindNearestWaypointIndex(currentWaypointIndex);

        Transform waypoint = assignedWaypoints[currentWaypointIndex];

        if (waypoint == null)
        {
            currentWaypointIndex++;
            return true;
        }

        Vector2 toWaypoint = (Vector2)waypoint.position - rb.position;

        if (toWaypoint.magnitude <= waypointReachDistance)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= assignedWaypoints.Count)
            {
                if (movementMode == EnemyMovementMode.Waypoint)
                    StopMovement();

                return movementMode == EnemyMovementMode.Waypoint;
            }

            waypoint = assignedWaypoints[currentWaypointIndex];
            toWaypoint = (Vector2)waypoint.position - rb.position;
        }

        rb.linearVelocity = toWaypoint.normalized * moveSpeed;
        SetMovingAnimation(true);
        return true;
    }

    private bool ShouldUseAStarMovement()
    {
        if (movementMode == EnemyMovementMode.AStar || movementMode == EnemyMovementMode.WaypointThenAStar)
            return true;

        if (movementMode == EnemyMovementMode.Direct || movementMode == EnemyMovementMode.Waypoint)
            return false;

        return useAStarMovement;
    }

    private void HandleAStarMovement(Transform moveTarget, Vector2 direction, float distance)
    {
        if (aStarPathfinder == null)
            aStarPathfinder = AStarPathfinder2D.Instance;

        if (aStarPathfinder == null)
        {
            HandleMissingAStarPath(direction, distance);
            return;
        }

        Vector2 targetPosition = moveTarget.position;

        if (enemyKind == EnemyKind.Ranged)
        {
            if (distance < rangedPreferredDistance * 0.7f)
            {
                targetPosition = (Vector2)transform.position - direction * rangedPreferredDistance;
            }
            else if (distance <= rangedPreferredDistance)
            {
                StopMovement();
                return;
            }
        }
        else if (distance <= stopDistance)
        {
            StopMovement();
            return;
        }

        RefreshAStarPathIfNeeded(targetPosition);

        if (currentPath.Count == 0 || currentPathIndex >= currentPath.Count)
        {
            HandleMissingAStarPath(direction, distance);
            return;
        }

        Vector2 waypoint = currentPath[currentPathIndex];
        Vector2 toWaypoint = waypoint - rb.position;

        if (toWaypoint.magnitude <= pathWaypointReachDistance)
        {
            currentPathIndex++;

            if (currentPathIndex >= currentPath.Count)
            {
                StopMovement();
                return;
            }

            waypoint = currentPath[currentPathIndex];
            toWaypoint = waypoint - rb.position;
        }

        rb.linearVelocity = toWaypoint.normalized * moveSpeed;
        SetMovingAnimation(true);
    }

    private void RefreshAStarPathIfNeeded(Vector2 targetPosition)
    {
        bool targetMovedEnough = ((Vector2)lastAStarTargetPosition - targetPosition).sqrMagnitude
            > aStarTargetMoveThreshold * aStarTargetMoveThreshold;

        if (Time.time < nextAStarRepathTime && !targetMovedEnough)
            return;

        float repathInterval = Mathf.Max(0.05f, aStarRepathInterval);
        nextAStarRepathTime = Time.time + repathInterval * Random.Range(0.85f, 1.15f);
        lastAStarTargetPosition = targetPosition;

        if (aStarPathfinder.TryFindPath(rb.position, targetPosition, currentPath))
        {
            currentPathIndex = currentPath.Count > 1 ? 1 : 0;
        }
        else
        {
            currentPath.Clear();
            currentPathIndex = 0;
        }
    }

    private void HandleMissingAStarPath(Vector2 direction, float distance)
    {
        if (fallbackToDirectMovementWhenPathFails)
        {
            if (enemyKind == EnemyKind.Ranged)
                HandleRangedMovement(direction, distance);
            else
                HandleMeleeMovement(direction, distance);

            return;
        }

        StopMovement();
    }

    private void StopMovement()
    {
        rb.linearVelocity = Vector2.zero;

        SetMovingAnimation(false);
    }

    private void TryMeleeAttack(Transform attackTarget)
    {
        if (isAttacking || Time.time < lastAttackTime + attackCooldown)
            return;

        lastAttackTime = Time.time;
        PlayAttackAnimation(attackTarget);

        if (!ShouldUseTimedMeleeAttack())
        {
            DealDamageToTarget(attackTarget);
            return;
        }

        if (meleeAttackCoroutine != null)
            StopCoroutine(meleeAttackCoroutine);

        meleeAttackCoroutine = StartCoroutine(TimedMeleeAttack(attackTarget));
    }

    private bool ShouldUseTimedMeleeAttack()
    {
        return lockMovementWhileAttacking &&
            (!useAttackTimingForTankOnly || enemyKind == EnemyKind.Tank);
    }

    private IEnumerator TimedMeleeAttack(Transform attackTarget)
    {
        isAttacking = true;
        StopMovement();

        float hitDelay = Mathf.Max(0f, attackHitDelay);
        float lockDuration = Mathf.Max(hitDelay, attackMovementLockDuration);

        if (hitDelay > 0f)
            yield return new WaitForSeconds(hitDelay);

        if (IsAttackTargetStillValid(attackTarget))
            DealDamageToTarget(attackTarget);

        float remainingLockTime = lockDuration - hitDelay;

        if (remainingLockTime > 0f)
            yield return new WaitForSeconds(remainingLockTime);

        isAttacking = false;
        meleeAttackCoroutine = null;
    }

    private bool IsAttackTargetStillValid(Transform attackTarget)
    {
        if (attackTarget == null || !attackTarget.gameObject.activeInHierarchy)
            return false;

        return GetDistanceToTarget(attackTarget) <= attackRange + attackHitRangeTolerance;
    }

    private void OnDisable()
    {
        if (meleeAttackCoroutine != null)
        {
            StopCoroutine(meleeAttackCoroutine);
            meleeAttackCoroutine = null;
        }

        isAttacking = false;
    }

    private void TryShoot(Transform attackTarget)
    {
        if (Time.time < lastAttackTime + attackCooldown)
            return;

        lastAttackTime = Time.time;
        PlayAttackAnimation(attackTarget);

        bool targetIsPlayer = player != null && attackTarget == player;

        if (!targetIsPlayer)
        {
            DealDamageToTarget(attackTarget);
            return;
        }

        Vector2 direction = ((Vector2)attackTarget.position - (Vector2)transform.position).normalized;

        if (projectilePrefab != null)
        {
            GameObject projectileObject = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            EnemyProjectile projectile = projectileObject.GetComponent<EnemyProjectile>();

            if (projectile != null)
            {
                projectile.Launch(direction, projectileSpeed, attackDamage);
            }
        }
        else
        {
            DealDamageToTarget(attackTarget);
            Debug.LogWarning("원거리 몬스터 Projectile Prefab이 비어 있어서 즉시 피해로 처리했습니다.");
        }
    }

    private void DealDamageToTarget(Transform attackTarget)
    {
        if (player != null && attackTarget == player && playerStats != null)
        {
            EnemyHealth attackerHealth = GetComponent<EnemyHealth>();
            if (attackerHealth == null)
                attackerHealth = GetComponentInParent<EnemyHealth>();
            playerStats.TakeDamage(attackDamage, attackerHealth, false);
            return;
        }

        if (homeTarget != null && attackTarget == homeTarget && targetHomeBase != null)
        {
            targetHomeBase.TakeDamage(attackDamage);
        }
    }

    private void FindPlayerIfNeeded()
    {
        if (playerStats == null || player == null)
        {
            FindPlayer();
        }
    }

    public void ApplyTemporaryMoveSpeedMultiplier(float multiplier, float duration)
    {
        if (temporaryMoveSpeedCoroutine != null)
            StopCoroutine(temporaryMoveSpeedCoroutine);

        temporaryMoveSpeedCoroutine = StartCoroutine(TemporaryMoveSpeedRoutine(multiplier, duration));
    }

    private IEnumerator TemporaryMoveSpeedRoutine(float multiplier, float duration)
    {
        float originalMoveSpeed = moveSpeed;
        moveSpeed = Mathf.Max(0.01f, moveSpeed * Mathf.Clamp(multiplier, 0.05f, 1f));

        yield return new WaitForSeconds(Mathf.Max(0f, duration));

        moveSpeed = originalMoveSpeed;
        temporaryMoveSpeedCoroutine = null;
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            playerStats = playerObject.GetComponent<PlayerStats>();

            if (playerStats == null)
                playerStats = playerObject.GetComponentInParent<PlayerStats>();

            if (playerStats == null)
                playerStats = playerObject.GetComponentInChildren<PlayerStats>();

            if (playerStats != null)
            {
                player = playerStats.transform;
                return;
            }
        }

        playerStats = FindObjectOfType<PlayerStats>();

        if (playerStats != null)
        {
            player = playerStats.transform;
        }
    }

    public void ApplyRuntimeScaling(float multiplier)
    {
        multiplier = Mathf.Max(0.1f, multiplier);

        if (applyPresetOnStart)
        {
            ApplyPresetWithOptionalStatPreservation(enemyKind);
            applyPresetOnStart = false;
        }

        maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * multiplier));
        attackDamage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * multiplier));
        moveSpeed *= Mathf.Lerp(1f, multiplier, 0.25f);

        if (health != null)
        {
            health.Setup(maxHp);
        }
    }

    public void ApplyHealthAndAttackScaling(float multiplier)
    {
        multiplier = Mathf.Max(0.1f, multiplier);

        if (applyPresetOnStart)
        {
            ApplyPresetWithOptionalStatPreservation(enemyKind);
            applyPresetOnStart = false;
        }

        maxHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * multiplier));
        attackDamage = Mathf.Max(1, Mathf.RoundToInt(attackDamage * multiplier));

        if (health != null)
            health.Setup(maxHp);
    }
    public void ApplyPreset(EnemyKind kind)
    {
        enemyKind = kind;

        switch (enemyKind)
        {
            case EnemyKind.Basic:
                moveSpeed = 2.2f;
                attackRange = 0.5f;
                attackCooldown = 1f;
                stopDistance = 0.35f;
                visualScale = Vector2.one;
                break;

            case EnemyKind.Ranged:
                moveSpeed = 1.8f;
                attackRange = 5f;
                attackCooldown = 1.4f;
                stopDistance = 0.8f;
                rangedPreferredDistance = 3.5f;
                visualScale = Vector2.one;
                break;

            case EnemyKind.Tank:
                moveSpeed = 1.2f;
                attackRange = 1.5f;
                attackCooldown = 1.2f;
                stopDistance = 1f;
                visualScale = new Vector2(1.5f, 1.5f);
                break;

            case EnemyKind.Boss:
                moveSpeed = 1.4f;
                attackRange = 1.8f;
                attackCooldown = 1f;
                stopDistance = 1.2f;
                visualScale = new Vector2(2f, 2f);
                break;
        }

        transform.localScale = new Vector3(visualScale.x, visualScale.y, 1f);
    }

    private void ApplyPresetWithOptionalStatPreservation(EnemyKind kind)
    {
        int inspectorMaxHp = maxHp;
        int inspectorAttackDamage = attackDamage;

        ApplyPreset(kind);

        if (!preserveInspectorCombatStats)
            return;

        maxHp = Mathf.Max(1, inspectorMaxHp);
        attackDamage = Mathf.Max(0, inspectorAttackDamage);
    }

    private void ConfigureEnemyCollision()
    {
        if (!ignoreSameLayerCollision)
            return;

        if (gameObject.layer == 0 && warnIfIgnoringDefaultLayer)
        {
            Debug.LogWarning($"{name}: Default 레이어끼리 충돌 무시는 위험해서 건너뜁니다. Enemy 전용 레이어를 만든 뒤 적 프리팹에 적용하세요.");
            return;
        }

        Physics2D.IgnoreLayerCollision(gameObject.layer, gameObject.layer, true);
    }

    private void SetMovingAnimation(bool isMoving)
    {
        if (isMoving)
            UpdateFacingFromMovement();

        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(movingParameter))
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == movingParameter && parameter.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(movingParameter, isMoving);
                return;
            }
        }
    }

    private void PlayAttackAnimation(Transform attackTarget)
    {
        if (attackTarget != null)
        {
            float attackDirectionX = attackTarget.position.x - transform.position.x;
            UpdateHorizontalFacing(attackDirectionX);
        }

        if (animator == null ||
            animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(attackTriggerParameter))
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == attackTriggerParameter &&
                parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.ResetTrigger(attackTriggerParameter);
                animator.SetTrigger(attackTriggerParameter);
                return;
            }
        }
    }

    private void UpdateFacingFromMovement()
    {
        float directionX = rb != null ? rb.linearVelocity.x : 0f;

        UpdateHorizontalFacing(directionX);
    }

    private void UpdateHorizontalFacing(float directionX)
    {
        if (!useHorizontalFlip || spriteRenderer == null || Mathf.Abs(directionX) <= flipDirectionThreshold)
            return;

        bool facingLeft = directionX < 0f;
        spriteRenderer.flipX = facingLeft != spritesFaceLeft;
    }

    private void OnDrawGizmos()
    {
        if (drawDetectionRangeAlways)
            DrawDetectionRangeGizmo();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDetectionRangeAlways)
            DrawDetectionRangeGizmo();
    }

    private void DrawDetectionRangeGizmo()
    {
        if (!requirePlayerDetection || playerDetectionRange <= 0f)
            return;

        Gizmos.color = detectionRangeColor;
        Gizmos.DrawWireSphere(transform.position, playerDetectionRange);
    }
}

