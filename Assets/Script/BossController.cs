using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(EnemyHealth))]
public class BossController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform seedSpawnPoint;
    [SerializeField] private Transform[] goblinSpawnPoints;
    [SerializeField] private GameObject seedProjectilePrefab;
    [SerializeField] private GameObject goblinPrefab;

    [Header("Movement")]
    [Min(0f)] [SerializeField] private float moveSpeed = 1.4f;
    [Min(0f)] [SerializeField] private float stopDistance = 2.5f;
    [SerializeField] private bool spritesFaceLeft;

    [Header("Contact Damage")]
    [Tooltip("보스 중심에서 이 거리 안에 있는 플레이어에게 피해를 줍니다.")]
    [Min(0f)] [SerializeField] private float contactDamageRange = 1.2f;
    [Min(0)] [SerializeField] private int contactDamage = 10;
    [Tooltip("플레이어가 범위 안에 계속 있을 때 피해를 다시 주는 간격입니다.")]
    [Min(0.05f)] [SerializeField] private float contactDamageInterval = 1f;

    [Header("Enemy Separation")]
    [Tooltip("보스가 다른 몬스터와 같은 위치에 겹치는 것을 방지합니다.")]
    [SerializeField] private bool useEnemySeparation = true;
    [Min(0.01f)] [SerializeField] private float separationRadius = 0.8f;
    [Min(0f)] [SerializeField] private float separationStrength = 1.5f;
    [Min(0f)] [SerializeField] private float maxSeparationSpeed = 1f;
    [Tooltip("Enemy 레이어만 선택하는 것을 권장합니다.")]
    [SerializeField] private LayerMask separationLayerMask = ~0;

    [Header("Seed Throw")]
    [Min(0f)] [SerializeField] private float seedCooldown = 5f;
    [Tooltip("씨앗 던지기 애니메이션 길이입니다. 애니메이션 종료 후 투사체를 발사합니다.")]
    [Min(0f)] [SerializeField] private float seedSkillDuration = 2f;
    [Tooltip("스킬 시작 후 씨앗이 실제로 발사되는 시점입니다.")]
    [Min(0f)] [SerializeField] private float seedFireDelay = 1.9f;
    [Min(0f)] [SerializeField] private float seedProjectileSpeed = 7f;
    [Min(0)] [SerializeField] private int seedDamage = 10;
    [Min(1)] [SerializeField] private int seedProjectileCount = 5;
    [Range(0f, 180f)] [SerializeField] private float seedFanAngle = 60f;

    [Header("Goblin Summon")]
    [Min(0f)] [SerializeField] private float summonCooldown = 10f;
    [Tooltip("소환 애니메이션 길이입니다. 이 시간이 끝난 뒤 고블린이 생성되고 보스가 다시 움직입니다.")]
    [Min(0f)] [SerializeField] private float summonSkillDuration = 2.9166667f;
    [Min(1)] [SerializeField] private int goblinCount = 2;
    [Tooltip("Spawn Point를 사용하지 않을 때 무작위 소환 영역의 중심 위치를 보정합니다. 아래로 내리려면 Y에 음수를 입력하세요.")]
    [SerializeField] private Vector2 summonCenterOffset = new Vector2(0f, -0.6f);
    [Min(0f)] [SerializeField] private float minimumSummonRadius = 0.8f;
    [Min(0f)] [SerializeField] private float randomSummonRadius = 1.5f;

    [Header("Depth Sorting")]
    [Tooltip("보스와 소환된 고블린의 Y 위치에 따라 앞뒤 순서를 자동으로 정합니다.")]
    [SerializeField] private bool useYSorting = true;
    [Tooltip("보스의 앞뒤 정렬 기준선을 발 쪽으로 내립니다. 기준선이 몸통에 있으면 음수 값을 더 크게 설정하세요.")]
    [SerializeField] private float bossSortingPivotYOffset = -0.8f;
    [SerializeField] private int sortingOrderOffset;

    [Header("Skill Flow")]
    [Tooltip("두 스킬의 쿨타임이 동시에 끝났을 때 소환을 먼저 사용합니다.")]
    [SerializeField] private bool summonHasPriority = true;
    [Tooltip("한 스킬이 끝난 직후 다른 스킬이 바로 이어지는 것을 막는 공용 후딜입니다.")]
    [Min(0f)] [SerializeField] private float globalSkillDelay = 1f;
    [Tooltip("켜면 전투 시작 시 쿨타임을 모두 채우고 시작합니다.")]
    [SerializeField] private bool useSkillsImmediately;

    [Header("Animator Parameters")]
    [SerializeField] private string movingParameter = "IsMoving";
    [SerializeField] private string seedTriggerParameter = "SeedThrow";
    [SerializeField] private string summonTriggerParameter = "SummonGoblin";

    private Rigidbody2D body;
    private EnemyHealth health;
    private Transform player;
    private float seedCooldownRemaining;
    private float summonCooldownRemaining;
    private float nextSkillAllowedTime;
    private float nextContactDamageTime;
    private bool isUsingSkill;
    private bool isDead;
    private bool skillEffectExecuted;
    private BossSkill currentSkill;
    private Coroutine skillCoroutine;
    private readonly Collider2D[] separationResults = new Collider2D[24];

    public bool IsUsingSkill => isUsingSkill;
    public float SeedCooldownRemaining => seedCooldownRemaining;
    public float SummonCooldownRemaining => summonCooldownRemaining;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();

        // EnemyHealth의 Max Hp를 보스 시작 체력으로 사용한다.
        if (health != null)
            health.Setup(health.maxHp);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (useYSorting)
            ConfigureYSorting(gameObject, sortingOrderOffset, bossSortingPivotYOffset);

        // 일반 적의 추적/근접 공격과 보스 전용 행동이 동시에 실행되지 않게 한다.
        EnemyController enemyController = GetComponent<EnemyController>();
        if (enemyController != null)
            enemyController.enabled = false;

        body.gravityScale = 0f;
        body.freezeRotation = true;

        seedCooldownRemaining = useSkillsImmediately ? 0f : seedCooldown;
        summonCooldownRemaining = useSkillsImmediately ? 0f : summonCooldown;
    }

    private void OnEnable()
    {
        if (health != null)
            health.Died += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= HandleDeath;

        StopBossMovement();
    }

    private void Update()
    {
        if (isDead)
            return;

        FindPlayerIfNeeded();
        TryDealContactDamage();

        if (isUsingSkill)
            return;

        seedCooldownRemaining = Mathf.Max(0f, seedCooldownRemaining - Time.deltaTime);
        summonCooldownRemaining = Mathf.Max(0f, summonCooldownRemaining - Time.deltaTime);

        if (Time.time < nextSkillAllowedTime)
            return;

        TryStartReadySkill();
    }

    private void FixedUpdate()
    {
        if (isDead || isUsingSkill)
        {
            StopBossMovement();
            return;
        }

        MoveTowardsPlayer();
    }

    private void TryStartReadySkill()
    {
        bool canSummon = summonCooldownRemaining <= 0f && goblinPrefab != null;
        bool canThrowSeed = seedCooldownRemaining <= 0f && seedProjectilePrefab != null && player != null;

        if (summonHasPriority)
        {
            if (canSummon)
                StartSkill(BossSkill.SummonGoblin);
            else if (canThrowSeed)
                StartSkill(BossSkill.SeedThrow);
        }
        else
        {
            if (canThrowSeed)
                StartSkill(BossSkill.SeedThrow);
            else if (canSummon)
                StartSkill(BossSkill.SummonGoblin);
        }
    }

    private void StartSkill(BossSkill skill)
    {
        if (isUsingSkill || isDead)
            return;

        isUsingSkill = true;
        currentSkill = skill;
        skillEffectExecuted = false;
        StopBossMovement();
        FacePlayer();

        string triggerName = skill == BossSkill.SeedThrow
            ? seedTriggerParameter
            : summonTriggerParameter;
        SetAnimatorTrigger(triggerName);

        skillCoroutine = StartCoroutine(RunSkill(skill));
    }

    private IEnumerator RunSkill(BossSkill skill)
    {
        if (skill == BossSkill.SummonGoblin)
        {
            if (summonSkillDuration > 0f)
                yield return new WaitForSeconds(summonSkillDuration);

            // Animation Event가 없거나 누락되어도 애니메이션 종료 시 자동 소환한다.
            ExecuteSkillEffect(skill);
            FinishSkill(skill);
            yield break;
        }

        float fireDelay = Mathf.Clamp(seedFireDelay, 0f, seedSkillDuration);
        if (fireDelay > 0f)
            yield return new WaitForSeconds(fireDelay);

        ExecuteSkillEffect(skill);

        float remainingDuration = Mathf.Max(0f, seedSkillDuration - fireDelay);
        if (remainingDuration > 0f)
            yield return new WaitForSeconds(remainingDuration);

        FinishSkill(skill);
    }

    private void ExecuteSkillEffect(BossSkill skill)
    {
        if (skillEffectExecuted || isDead)
            return;

        skillEffectExecuted = true;

        if (skill == BossSkill.SeedThrow)
            SpawnSeedProjectile();
        else
            SpawnGoblins();
    }

    private void FinishSkill(BossSkill skill)
    {
        if (skill == BossSkill.SeedThrow)
            seedCooldownRemaining = seedCooldown;
        else
            summonCooldownRemaining = summonCooldown;

        isUsingSkill = false;
        skillCoroutine = null;
        nextSkillAllowedTime = Time.time + globalSkillDelay;
    }

    private void SpawnSeedProjectile()
    {
        if (seedProjectilePrefab == null || player == null)
            return;

        Vector3 spawnPosition = seedSpawnPoint != null ? seedSpawnPoint.position : transform.position;
        Vector2 centerDirection = ((Vector2)player.position - (Vector2)spawnPosition).normalized;
        int projectileCount = Mathf.Max(1, seedProjectileCount);

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = projectileCount == 1
                ? 0f
                : Mathf.Lerp(-seedFanAngle * 0.5f, seedFanAngle * 0.5f, i / (float)(projectileCount - 1));
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * centerDirection;
            SpawnSingleSeedProjectile(spawnPosition, direction);
        }
    }

    private void SpawnSingleSeedProjectile(Vector3 spawnPosition, Vector2 direction)
    {
        if (seedProjectilePrefab == null || player == null)
            return;

        GameObject projectileObject = Instantiate(seedProjectilePrefab, spawnPosition, Quaternion.identity);

        // BossProjectilePrefab 1은 Boss2의 큰 눈덩이와 공유한다.
        // Boss1이 씨앗으로 사용할 때만 Snow 애니메이션을 꺼서
        // Animator의 위치/스프라이트 변화가 씨앗 비행을 덮어쓰지 않게 한다.
        Animator[] projectileAnimators = projectileObject.GetComponentsInChildren<Animator>(true);
        foreach (Animator projectileAnimator in projectileAnimators)
        {
            if (projectileAnimator != null)
                projectileAnimator.enabled = false;
        }

        EnemyProjectile projectile = projectileObject.GetComponent<EnemyProjectile>();

        if (projectile == null)
            projectile = projectileObject.GetComponentInChildren<EnemyProjectile>();

        if (projectile != null)
            projectile.Launch(direction, seedProjectileSpeed, seedDamage);
        else
            Debug.LogWarning($"{seedProjectilePrefab.name}에 EnemyProjectile이 없습니다.", projectileObject);
    }

    private void SpawnGoblins()
    {
        if (goblinPrefab == null)
            return;

        int count = Mathf.Max(1, goblinCount);

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition;

            if (goblinSpawnPoints != null && goblinSpawnPoints.Length > 0)
            {
                Transform spawnPoint = goblinSpawnPoints[i % goblinSpawnPoints.Length];
                spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
            }
            else
            {
                float minimumRadius = Mathf.Min(minimumSummonRadius, randomSummonRadius);
                float radius = Mathf.Sqrt(Random.Range(
                    minimumRadius * minimumRadius,
                    randomSummonRadius * randomSummonRadius));
                Vector2 offset = Random.insideUnitCircle.normalized * radius;
                spawnPosition = transform.position +
                    new Vector3(summonCenterOffset.x + offset.x, summonCenterOffset.y + offset.y, 0f);
            }

            GameObject goblin = Instantiate(goblinPrefab, spawnPosition, Quaternion.identity);

            EnemyHealth[] summonedHealthComponents = goblin.GetComponentsInChildren<EnemyHealth>(true);
            foreach (EnemyHealth summonedHealth in summonedHealthComponents)
            {
                if (summonedHealth != null)
                    summonedHealth.DisableDeathRewards();
            }

            if (useYSorting)
                ConfigureYSorting(goblin, sortingOrderOffset, 0f);
        }
    }

    private void TryDealContactDamage()
    {
        if (player == null || contactDamage <= 0 || contactDamageRange <= 0f ||
            Time.time < nextContactDamageTime)
        {
            return;
        }

        Vector2 offset = (Vector2)player.position - (Vector2)transform.position;
        if (offset.sqrMagnitude > contactDamageRange * contactDamageRange)
            return;

        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats == null)
            stats = player.GetComponentInParent<PlayerStats>();

        if (stats == null)
            return;

        stats.TakeDamage(contactDamage, health, false);
        nextContactDamageTime = Time.time + contactDamageInterval;
    }

    private static void ConfigureYSorting(GameObject target, int orderOffset, float pivotYOffset)
    {
        if (target == null)
            return;

        YSortRenderer ySort = target.GetComponent<YSortRenderer>();
        if (ySort == null)
            ySort = target.AddComponent<YSortRenderer>();

        ySort.SetOrderOffset(orderOffset);
        ySort.SetPivotYOffset(pivotYOffset);
    }

    private void MoveTowardsPlayer()
    {
        if (player == null || body == null)
        {
            StopBossMovement();
            return;
        }

        Vector2 offset = (Vector2)player.position - body.position;
        Vector2 moveVelocity = offset.sqrMagnitude > stopDistance * stopDistance
            ? offset.normalized * moveSpeed
            : Vector2.zero;
        Vector2 velocity = moveVelocity + GetSeparationVelocity();

        if (velocity.sqrMagnitude <= 0.0001f)
        {
            StopBossMovement();
            return;
        }

        body.linearVelocity = velocity;
        SetAnimatorBool(movingParameter, moveVelocity.sqrMagnitude > 0.0001f);
        UpdateFacing(velocity.x);
    }

    private Vector2 GetSeparationVelocity()
    {
        if (!useEnemySeparation || separationRadius <= 0f ||
            separationStrength <= 0f || maxSeparationSpeed <= 0f)
        {
            return Vector2.zero;
        }

        int hitCount = Physics2D.OverlapCircleNonAlloc(
            body.position,
            separationRadius,
            separationResults,
            separationLayerMask);

        Vector2 separation = Vector2.zero;
        int neighborCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = separationResults[i];
            if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;

            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            BossController boss = hit.GetComponentInParent<BossController>();
            if (enemy == null && boss == null)
                continue;

            Vector2 away = body.position - (Vector2)hit.bounds.center;
            float distance = away.magnitude;

            if (distance <= 0.001f)
                away = Random.insideUnitCircle.normalized;
            else
                away /= distance;

            float weight = 1f - Mathf.Clamp01(distance / separationRadius);
            separation += away * weight;
            neighborCount++;
        }

        if (neighborCount == 0)
            return Vector2.zero;

        separation = separation / neighborCount * separationStrength;
        return Vector2.ClampMagnitude(separation, maxSeparationSpeed);
    }

    private void StopBossMovement()
    {
        if (body != null && body.simulated)
            body.linearVelocity = Vector2.zero;

        SetAnimatorBool(movingParameter, false);
    }

    private void FacePlayer()
    {
        if (player != null)
            UpdateFacing(player.position.x - transform.position.x);
    }

    private void UpdateFacing(float directionX)
    {
        if (spriteRenderer == null || Mathf.Abs(directionX) < 0.01f)
            return;

        bool facingLeft = directionX < 0f;
        spriteRenderer.flipX = facingLeft != spritesFaceLeft;
    }

    private void FindPlayerIfNeeded()
    {
        if (player != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            player = playerObject.transform;
    }

    private void HandleDeath(EnemyHealth _)
    {
        BossExitPortal.EnsurePortalForDefeatedBoss(health);
        isDead = true;
        isUsingSkill = false;

        if (skillCoroutine != null)
        {
            StopCoroutine(skillCoroutine);
            skillCoroutine = null;
        }

        StopBossMovement();
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
            return;

        animator.SetBool(parameterName, value);
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        if (!HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
            return;

        animator.ResetTrigger(parameterName);
        animator.SetTrigger(parameterName);
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (animator == null || animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == type)
                return true;
        }

        return false;
    }

    // 해당 애니메이션의 실제 발동 프레임에 Event로 연결할 수 있다.
    public void AnimationEvent_ThrowSeed()
    {
        if (isUsingSkill && currentSkill == BossSkill.SeedThrow)
            ExecuteSkillEffect(BossSkill.SeedThrow);
    }

    public void AnimationEvent_SummonGoblin()
    {
        if (isUsingSkill && currentSkill == BossSkill.SummonGoblin)
            ExecuteSkillEffect(BossSkill.SummonGoblin);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.1f, 0.65f);
        Gizmos.DrawWireSphere(transform.position, contactDamageRange);
    }

    private enum BossSkill
    {
        SeedThrow,
        SummonGoblin
    }
}

public class YSortRenderer : MonoBehaviour
{
    [Tooltip("비워두면 이 오브젝트의 위치를 정렬 기준으로 사용합니다. 발 위치 오브젝트를 넣는 것을 권장합니다.")]
    [SerializeField] private Transform sortingPivot;
    [SerializeField] private float pivotYOffset;
    [Min(1)] [SerializeField] private int precision = 100;
    [SerializeField] private int orderOffset;

    private SpriteRenderer[] spriteRenderers;
    private SortingGroup sortingGroup;

    private void Awake()
    {
        sortingGroup = GetComponent<SortingGroup>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void LateUpdate()
    {
        float y = (sortingPivot != null ? sortingPivot.position.y : transform.position.y) + pivotYOffset;
        int order = orderOffset - Mathf.RoundToInt(y * precision);

        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = order;
            return;
        }

        if (spriteRenderers == null)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].sortingOrder = order + i;
        }
    }

    public void SetOrderOffset(int value)
    {
        orderOffset = value;
    }

    public void SetPivotYOffset(float value)
    {
        pivotYOffset = value;
    }
}
