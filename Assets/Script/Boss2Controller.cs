using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(EnemyHealth))]
public class Boss2Controller : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private GameObject largeSnowballPrefab;
    [SerializeField] private GameObject smallSnowballPrefab;
    [SerializeField] private Transform snowballSpawnPoint;
    [Min(0.1f)] [SerializeField] private float largeSnowballScale = 2f;

    [Header("Movement")]
    [Min(0f)] [SerializeField] private float moveSpeed = 1.3f;
    [Min(0f)] [SerializeField] private float stopDistance = 2.5f;
    [Tooltip("원본 걷기 스프라이트가 왼쪽을 보고 있으면 체크하세요. 이동 방향에 따라 X축으로 뒤집힙니다.")]
    [SerializeField] private bool spritesFaceLeft;

    [Header("Slam Attack")]
    [Min(0f)] [SerializeField] private float slamCooldown = 7f;
    [Min(0f)] [SerializeField] private float jumpUpDuration = 1.1666667f;
    [Min(0f)] [SerializeField] private float slamDownDuration = 1f;
    [Tooltip("Down 애니메이션 시작 후 피해가 적용되는 시간입니다. 기본값은 12 FPS 기준 4프레임입니다.")]
    [Min(0f)] [SerializeField] private float slamImpactDelay = 0.33333334f;
    [Min(0)] [SerializeField] private int slamDamage = 25;
    [Tooltip("내려찍기 피해 반경입니다. 보스를 선택하면 Scene 뷰에 원으로 표시됩니다.")]
    [Min(0f)] [SerializeField] private float slamRadius = 2f;
    [Header("Slam Range Display")]
    [SerializeField] private bool showSlamRangeInGame = true;
    [SerializeField] private Color slamRangeColor = new Color(1f, 0.05f, 0.05f, 0.45f);
    [Min(0.01f)] [SerializeField] private float slamRangeLineWidth = 0.06f;
    [Range(12, 128)] [SerializeField] private int slamRangeSegments = 64;

    [Header("Contact Damage")]
    [Tooltip("플레이어가 이 거리 안에 있으면 접촉 피해를 줍니다.")]
    [Min(0f)] [SerializeField] private float contactDamageRange = 1.2f;
    [Min(0)] [SerializeField] private int contactDamage = 10;
    [Min(0.05f)] [SerializeField] private float contactDamageInterval = 1f;

    [Header("Spiral Snow Attack")]
    [Min(0f)] [SerializeField] private float snowCooldown = 11f;
    [Min(0f)] [SerializeField] private float snowCastDuration = 2.4166665f;
    [Tooltip("큰 눈덩이 애니메이션에서 작은 눈덩이 발사를 시작하는 시점입니다. 0:30은 60 FPS 기준 0.5초입니다.")]
    [Min(0f)] [SerializeField] private float spiralStartDelay = 0.1f;
    [Min(0f)] [SerializeField] private float largeSnowballAnimationDuration = 4.5f;
    [Min(1)] [SerializeField] private int spiralBurstCount = 12;
    [Min(1)] [SerializeField] private int bulletsPerBurst = 4;
    [Min(0f)] [SerializeField] private float spiralBurstInterval = 0.18f;
    [SerializeField] private float spiralRotationPerBurst = 22.5f;
    [Min(0f)] [SerializeField] private float smallSnowballSpeed = 5f;
    [Min(0)] [SerializeField] private int smallSnowballDamage = 8;

    [Header("Skill Flow")]
    [SerializeField] private bool slamHasPriority = true;
    [Min(0f)] [SerializeField] private float globalSkillDelay = 1f;
    [SerializeField] private bool useSkillsImmediately;

    [Header("Animator States")]
    [SerializeField] private string idleStateName = "Boss2Idle";
    [SerializeField] private string walkStateName = "Boss2Walk";
    [SerializeField] private string jumpUpStateName = "Boss2JumpUp";
    [SerializeField] private string downStateName = "Boss2Down";
    [SerializeField] private string snowStateName = "Boss2Snow";
    [SerializeField] private string deathStateName = "Boss2Die";

    private Rigidbody2D body;
    private EnemyHealth health;
    private Transform player;
    private float slamCooldownRemaining;
    private float snowCooldownRemaining;
    private float nextSkillAllowedTime;
    private float nextContactDamageTime;
    private bool isUsingSkill;
    private bool isDead;
    private Coroutine skillRoutine;
    private string currentStateName;
    private LineRenderer slamRangeRenderer;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        health.Setup(health.maxHp);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (smallSnowballPrefab == null)
            smallSnowballPrefab = largeSnowballPrefab;

        if (largeSnowballPrefab == null)
            Debug.LogWarning($"{name}: Large Snowball Prefab이 비어 있습니다.", this);
        if (smallSnowballPrefab == null)
            Debug.LogWarning($"{name}: Small Snowball Prefab이 비어 있어 나선형 눈덩이 스킬을 사용할 수 없습니다.", this);

        BossController boss1Controller = GetComponent<BossController>();
        if (boss1Controller != null)
            boss1Controller.enabled = false;

        EnemyController enemyController = GetComponent<EnemyController>();
        if (enemyController != null)
            enemyController.enabled = false;

        body.gravityScale = 0f;
        body.freezeRotation = true;
        CreateSlamRangeRenderer();
        slamCooldownRemaining = useSkillsImmediately ? 0f : slamCooldown;
        snowCooldownRemaining = useSkillsImmediately ? 0f : snowCooldown;
    }

    private void OnEnable()
    {
        if (health != null)
            health.Died += HandleDeath;
    }

    private void Start()
    {
        PlayState(idleStateName);
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= HandleDeath;
        SetSlamRangeVisible(false);
        StopMovement();
    }

    private void Update()
    {
        if (isDead)
            return;

        FindPlayer();
        TryDealContactDamage();
        if (isUsingSkill)
            return;

        slamCooldownRemaining = Mathf.Max(0f, slamCooldownRemaining - Time.deltaTime);
        snowCooldownRemaining = Mathf.Max(0f, snowCooldownRemaining - Time.deltaTime);

        if (Time.time < nextSkillAllowedTime)
            return;

        bool slamReady = slamCooldownRemaining <= 0f && player != null;
        bool snowReady = snowCooldownRemaining <= 0f && player != null && smallSnowballPrefab != null;

        if (slamHasPriority)
        {
            if (slamReady) StartSlam();
            else if (snowReady) StartSnowAttack();
        }
        else
        {
            if (snowReady) StartSnowAttack();
            else if (slamReady) StartSlam();
        }
    }

    private void FixedUpdate()
    {
        if (isDead || isUsingSkill)
        {
            StopMovement();
            return;
        }

        if (player == null)
        {
            StopMovement();
            PlayState(idleStateName);
            return;
        }

        Vector2 offset = (Vector2)player.position - body.position;
        if (offset.sqrMagnitude <= stopDistance * stopDistance)
        {
            StopMovement();
            PlayState(idleStateName);
            return;
        }

        body.linearVelocity = offset.normalized * moveSpeed;
        PlayState(walkStateName);
        UpdateFacing(offset.x);
    }

    private void LateUpdate()
    {
        // Animator가 스프라이트 프레임을 갱신한 뒤 플레이어 방향을 최종 적용한다.
        // 걷기와 대기 상태에서는 실제 플레이어 위치를 기준으로 바라본다.
        bool followsPlayerFacing = currentStateName == walkStateName || currentStateName == idleStateName;
        if (!isDead && !isUsingSkill && player != null && followsPlayerFacing)
            UpdateFacing(player.position.x - transform.position.x);
    }

    private void StartSlam()
    {
        BeginSkill();
        skillRoutine = StartCoroutine(SlamRoutine());
    }

    private IEnumerator SlamRoutine()
    {
        SetSlamRangeVisible(true);
        PlayState(jumpUpStateName);

        if (jumpUpDuration > 0f)
            yield return new WaitForSeconds(jumpUpDuration);

        if (isDead)
            yield break;

        PlayState(downStateName);

        float impactDelay = Mathf.Min(slamImpactDelay, slamDownDuration);
        if (impactDelay > 0f)
            yield return new WaitForSeconds(impactDelay);

        if (isDead)
            yield break;

        DealSlamDamage();
        SetSlamRangeVisible(false);

        float remainingDownTime = Mathf.Max(0f, slamDownDuration - impactDelay);
        if (remainingDownTime > 0f)
            yield return new WaitForSeconds(remainingDownTime);

        if (isDead)
            yield break;

        slamCooldownRemaining = slamCooldown;
        FinishSkill();
    }

    private void DealSlamDamage()
    {
        if (player == null)
            return;

        if (((Vector2)player.position - (Vector2)transform.position).sqrMagnitude > slamRadius * slamRadius)
            return;

        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats == null)
            stats = player.GetComponentInParent<PlayerStats>();
        if (stats != null)
            stats.TakeDamage(slamDamage, health, false);
    }

    private void TryDealContactDamage()
    {
        if (player == null || contactDamage <= 0 || contactDamageRange <= 0f ||
            Time.time < nextContactDamageTime)
        {
            return;
        }

        if (((Vector2)player.position - (Vector2)transform.position).sqrMagnitude >
            contactDamageRange * contactDamageRange)
        {
            return;
        }

        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats == null)
            stats = player.GetComponentInParent<PlayerStats>();
        if (stats == null)
            return;

        stats.TakeDamage(contactDamage, health, false);
        nextContactDamageTime = Time.time + contactDamageInterval;
    }

    private void StartSnowAttack()
    {
        BeginSkill();
        skillRoutine = StartCoroutine(SnowAttackRoutine());
    }

    private IEnumerator SnowAttackRoutine()
    {
        PlayState(snowStateName);
        if (snowCastDuration > 0f)
            yield return new WaitForSeconds(snowCastDuration);

        Vector3 start = snowballSpawnPoint != null ? snowballSpawnPoint.position : transform.position;
        GameObject largeSnowball = largeSnowballPrefab != null
            ? Instantiate(largeSnowballPrefab, start, Quaternion.identity)
            : new GameObject("LargeSnowball");
        largeSnowball.transform.position = start;
        largeSnowball.transform.localScale *= largeSnowballScale;

        EnemyProjectile largeProjectile = largeSnowball.GetComponent<EnemyProjectile>();
        if (largeProjectile != null)
            largeProjectile.enabled = false;
        Collider2D largeCollider = largeSnowball.GetComponent<Collider2D>();
        if (largeCollider != null)
            largeCollider.enabled = false;

        if (spiralStartDelay > 0f)
            yield return new WaitForSeconds(spiralStartDelay);

        for (int burst = 0; burst < spiralBurstCount && !isDead; burst++)
        {
            Vector3 burstOrigin = snowballSpawnPoint != null
                ? snowballSpawnPoint.position
                : start;
            FireSpiralBurst(burstOrigin, burst * spiralRotationPerBurst);
            if (spiralBurstInterval > 0f)
                yield return new WaitForSeconds(spiralBurstInterval);
        }

        float spiralDuration = spiralBurstCount * spiralBurstInterval;
        float remainingAnimation = Mathf.Max(
            0f,
            largeSnowballAnimationDuration - spiralStartDelay - spiralDuration);
        if (remainingAnimation > 0f)
            yield return new WaitForSeconds(remainingAnimation);

        Destroy(largeSnowball);
        snowCooldownRemaining = snowCooldown;
        FinishSkill();
    }

    private void FireSpiralBurst(Vector3 origin, float rotationOffset)
    {
        int count = Mathf.Max(1, bulletsPerBurst);
        for (int i = 0; i < count; i++)
        {
            float angle = rotationOffset + 360f * i / count;
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
            GameObject bulletObject = Instantiate(smallSnowballPrefab, origin, Quaternion.identity);
            Animator bulletAnimator = bulletObject.GetComponent<Animator>();
            if (bulletAnimator != null)
                bulletAnimator.enabled = false;
            bulletObject.transform.localScale = Vector3.one;
            EnemyProjectile projectile = bulletObject.GetComponent<EnemyProjectile>();
            if (projectile == null)
                projectile = bulletObject.GetComponentInChildren<EnemyProjectile>();
            if (projectile != null)
                projectile.Launch(direction, smallSnowballSpeed, smallSnowballDamage);
        }
    }

    private void BeginSkill()
    {
        isUsingSkill = true;
        StopMovement();
        FacePlayer();
    }

    private void FinishSkill()
    {
        isUsingSkill = false;
        skillRoutine = null;
        nextSkillAllowedTime = Time.time + globalSkillDelay;
        PlayState(idleStateName);
    }

    private void StopMovement()
    {
        if (body != null && body.simulated)
            body.linearVelocity = Vector2.zero;
    }

    private void FindPlayer()
    {
        if (player != null)
            return;
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null)
            player = target.transform;
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

    private void PlayState(string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(stateName) || currentStateName == stateName)
        {
            return;
        }

        currentStateName = stateName;
        animator.Play(stateName, 0, 0f);
    }

    private void HandleDeath(EnemyHealth _)
    {
        BossExitPortal.EnsurePortalForDefeatedBoss(health);
        isDead = true;
        isUsingSkill = false;
        if (skillRoutine != null)
            StopCoroutine(skillRoutine);
        SetSlamRangeVisible(false);
        StopMovement();
        PlayState(deathStateName);
    }

    private void CreateSlamRangeRenderer()
    {
        GameObject rangeObject = new GameObject("Slam Range Display");
        rangeObject.transform.SetParent(transform, false);
        slamRangeRenderer = rangeObject.AddComponent<LineRenderer>();
        slamRangeRenderer.useWorldSpace = false;
        slamRangeRenderer.loop = true;
        slamRangeRenderer.textureMode = LineTextureMode.Stretch;
        slamRangeRenderer.material = new Material(Shader.Find("Sprites/Default"));
        slamRangeRenderer.sortingOrder = 100;
        slamRangeRenderer.enabled = false;
        UpdateSlamRangeRenderer();
    }

    private void UpdateSlamRangeRenderer()
    {
        if (slamRangeRenderer == null)
            return;

        int segments = Mathf.Max(12, slamRangeSegments);
        slamRangeRenderer.positionCount = segments;
        slamRangeRenderer.startWidth = slamRangeLineWidth;
        slamRangeRenderer.endWidth = slamRangeLineWidth;
        slamRangeRenderer.startColor = slamRangeColor;
        slamRangeRenderer.endColor = slamRangeColor;

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            slamRangeRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * slamRadius);
        }
    }

    private void SetSlamRangeVisible(bool visible)
    {
        if (slamRangeRenderer == null)
            return;
        UpdateSlamRangeRenderer();
        slamRangeRenderer.enabled = visible && showSlamRangeInGame;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.15f);
        Gizmos.DrawSphere(transform.position, slamRadius);
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.95f);
        Gizmos.DrawWireSphere(transform.position, slamRadius);
    }
}
