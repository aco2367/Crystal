using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(EnemyHealth))]
public class Boss3Controller : MonoBehaviour
{
    private enum WeaponMode { Sword, Bow, Shield }

    [Header("Player Copy References")]
    [Tooltip("실제로 화면에 표시되는 CharacterVisual/Body의 SpriteRenderer입니다.")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [Tooltip("실제로 화면에 표시되는 CharacterVisual/Body의 Animator입니다.")]
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private GameObject swordVisual;
    [SerializeField] private GameObject bowVisual;
    [SerializeField] private GameObject shieldVisual;
    [SerializeField] private GameObject swordWeapon;
    [SerializeField] private GameObject bowWeapon;
    [SerializeField] private GameObject shieldWeapon;
    [SerializeField] private Transform weaponPivot;

    [Header("Aim And Attack Points")]
    [Tooltip("플레이어의 발이 아니라 몸 중심을 조준하도록 더할 위치입니다.")]
    [SerializeField] private Vector2 playerAimOffset = new Vector2(0f, 0.35f);
    [SerializeField] private Transform swordHitPoint;
    [SerializeField] private Transform bowProjectileSpawnPoint;
    [SerializeField] private Transform shieldBashPoint;
    [SerializeField] private Vector2 swordPivotLocalPosition;
    [SerializeField] private Vector2 bowPivotLocalPosition;
    [SerializeField] private Vector2 shieldPivotLocalPosition = new Vector2(0.45f, -0.15f);
    [SerializeField] private float swordAimAngleOffset;
    [SerializeField] private float bowAimAngleOffset;
    [SerializeField] private float shieldAimAngleOffset;
    [Tooltip("플레이어가 왼쪽에 있을 때 무기 스프라이트를 Y축으로 뒤집어 거꾸로 보이지 않게 합니다.")]
    [SerializeField] private bool flipWeaponOnLeft = true;

    [Header("Shield Side Aim")]
    [Tooltip("플레이어 방패처럼 상하를 조준하지 않고 플레이어가 있는 좌우에 방패를 고정합니다.")]
    [SerializeField] private bool shieldUsesSideOnlyAim = true;
    [SerializeField] private float shieldSideOnlyRotation;
    [SerializeField] private bool shieldFlipOnLeft = true;
    [SerializeField] private bool shieldMirrorRotationOnLeft = true;

    [Header("Movement")]
    [Min(0f)] [SerializeField] private float moveSpeed = 2.3f;
    [Min(0f)] [SerializeField] private float swordStopDistance = 1.1f;
    [Min(0f)] [SerializeField] private float bowPreferredDistance = 4.5f;
    [SerializeField] private bool spritesFaceRight = true;

    [Header("Sword")]
    [Min(0f)] [SerializeField] private float swordCooldown = 3.5f;
    [Min(0f)] [SerializeField] private float swordRange = 1.5f;
    [Min(0)] [SerializeField] private int swordDamage = 25;
    [Min(0f)] [SerializeField] private float swordHitDelay = 0.25f;
    [Min(0f)] [SerializeField] private float swordAnimationDuration = 0.75f;
    [SerializeField] private string swordAttackState = "SwordAttack";

    [Header("Bow")]
    [Min(0f)] [SerializeField] private float bowCooldown = 5f;
    [Min(0)] [SerializeField] private int bowDamage = 18;
    [Min(0f)] [SerializeField] private float bowFireDelay = 0.35f;
    [Min(0f)] [SerializeField] private float bowAnimationDuration = 0.9f;
    [Min(0f)] [SerializeField] private float arrowSpeed = 8f;
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private string bowAttackState = "ArcherAttact";

    [Header("Shield")]
    [Min(0f)] [SerializeField] private float shieldCooldown = 8f;
    [Min(0f)] [SerializeField] private float shieldDuration = 2.5f;
    [Range(0f, 1f)] [SerializeField] private float shieldDamageReduction = 0.7f;
    [Min(0f)] [SerializeField] private float shieldBashRange = 1.7f;
    [Min(0)] [SerializeField] private int shieldBashDamage = 15;
    [Min(0f)] [SerializeField] private float shieldBashDelay = 0.3f;
    [Min(0f)] [SerializeField] private float shieldKnockback = 4f;
    [SerializeField] private string shieldAttackState = "TankAttack";

    [Header("Skill Flow")]
    [Min(0f)] [SerializeField] private float globalSkillDelay = 1f;
    [SerializeField] private bool useSkillsImmediately;

    private Rigidbody2D body;
    private EnemyHealth health;
    private Transform player;
    private WeaponMode currentMode;
    private float swordReadyTime;
    private float bowReadyTime;
    private float shieldReadyTime;
    private float nextSkillTime;
    private bool isUsingSkill;
    private bool isDead;
    private Coroutine skillRoutine;
    private Animator activeBodyAnimator;
    private SpriteRenderer activeSprite;
    private PlayerRole displayedPlayerRole;
    private bool hasAppliedPlayerAppearance;
    private Vector3 shieldBaseScale = Vector3.one;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<EnemyHealth>();
        if (bodyAnimator == null)
            bodyAnimator = transform.Find("CharacterVisual/Body")?.GetComponent<Animator>();
        if (bodySpriteRenderer == null && bodyAnimator != null)
            bodySpriteRenderer = bodyAnimator.GetComponent<SpriteRenderer>();
        // EnemyHealth가 다른 자식 무기 Animator가 아니라 실제 화면의 Body에 Die를 보내게 한다.
        if (health != null && bodyAnimator != null)
            health.animator = bodyAnimator;
        body.gravityScale = 0f;
        body.freezeRotation = true;

        // 복사된 Player 컴포넌트가 AI와 동시에 움직이지 않도록 막는다.
        PlayerController playerController = GetComponent<PlayerController>();
        if (playerController != null) playerController.enabled = false;
        PlayerStats playerStats = GetComponent<PlayerStats>();
        if (playerStats != null) playerStats.enabled = false;
        WeaponAimController aim = GetComponent<WeaponAimController>();
        if (aim != null) aim.enabled = false;
        if (shieldWeapon != null)
        {
            Vector3 scale = shieldWeapon.transform.localScale;
            shieldBaseScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), scale.z);
        }

        float start = useSkillsImmediately ? Time.time : Time.time + 1f;
        swordReadyTime = start;
        bowReadyTime = start;
        shieldReadyTime = start;
        HideRoleSourceObjects();
        ApplyPlayerRoleAppearance(PlayerRole.Sword);
        SetMode(WeaponMode.Sword);
    }

    private void OnEnable()
    {
        if (health != null) health.Died += HandleDeath;
    }

    private void OnDisable()
    {
        if (health != null) health.Died -= HandleDeath;
        StopMovement();
    }

    private void Update()
    {
        if (isDead) return;
        FindPlayer();
        RefreshAppearanceFromPlayer();
        FacePlayer();
        AimWeapon();
        if (isUsingSkill || player == null || Time.time < nextSkillTime) return;

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance <= swordRange && Time.time >= swordReadyTime)
            StartSkill(SwordRoutine());
        else if (distance <= shieldBashRange && Time.time >= shieldReadyTime)
            StartSkill(ShieldRoutine());
        else if (distance >= swordRange && Time.time >= bowReadyTime)
            StartSkill(BowRoutine());
    }

    private void FixedUpdate()
    {
        if (isDead || isUsingSkill || player == null)
        {
            StopMovement();
            SetMoving(false);
            return;
        }

        Vector2 offset = (Vector2)player.position - body.position;
        float desiredDistance = currentMode == WeaponMode.Bow ? bowPreferredDistance : swordStopDistance;
        Vector2 velocity = Vector2.zero;
        if (offset.magnitude > desiredDistance)
            velocity = offset.normalized * moveSpeed;
        else if (currentMode == WeaponMode.Bow && offset.magnitude < desiredDistance * 0.7f)
            velocity = -offset.normalized * moveSpeed;

        body.linearVelocity = velocity;
        SetMoving(velocity.sqrMagnitude > 0.001f);
    }

    private void StartSkill(IEnumerator routine)
    {
        isUsingSkill = true;
        StopMovement();
        SetMoving(false);
        skillRoutine = StartCoroutine(routine);
    }

    private IEnumerator SwordRoutine()
    {
        SetMode(WeaponMode.Sword);
        PlayWeaponAttack(swordWeapon, swordAttackState);
        yield return new WaitForSeconds(swordHitDelay);
        if (!isDead) DamagePlayerInRange(swordHitPoint, swordRange, swordDamage, 0f);
        yield return new WaitForSeconds(Mathf.Max(0f, swordAnimationDuration - swordHitDelay));
        swordReadyTime = Time.time + swordCooldown;
        FinishSkill();
    }

    private IEnumerator BowRoutine()
    {
        SetMode(WeaponMode.Bow);
        PlayWeaponAttack(bowWeapon, bowAttackState);
        yield return new WaitForSeconds(bowFireDelay);
        if (!isDead) FireArrow();
        yield return new WaitForSeconds(Mathf.Max(0f, bowAnimationDuration - bowFireDelay));
        bowReadyTime = Time.time + bowCooldown;
        FinishSkill();
    }

    private IEnumerator ShieldRoutine()
    {
        SetMode(WeaponMode.Shield);
        health.SetDamageTakenMultiplier(1f - shieldDamageReduction);
        PlayWeaponAttack(shieldWeapon, shieldAttackState);
        yield return new WaitForSeconds(shieldBashDelay);
        if (!isDead) DamagePlayerInRange(shieldBashPoint, shieldBashRange, shieldBashDamage, shieldKnockback);
        yield return new WaitForSeconds(Mathf.Max(0f, shieldDuration - shieldBashDelay));
        health.SetDamageTakenMultiplier(1f);
        shieldReadyTime = Time.time + shieldCooldown;
        FinishSkill();
    }

    private void FinishSkill()
    {
        isUsingSkill = false;
        skillRoutine = null;
        nextSkillTime = Time.time + globalSkillDelay;
        SetMode(ChooseTravelMode());
    }

    private WeaponMode ChooseTravelMode()
    {
        if (player != null && Vector2.Distance(transform.position, player.position) > bowPreferredDistance)
            return WeaponMode.Bow;
        return WeaponMode.Sword;
    }

    private void SetMode(WeaponMode mode)
    {
        currentMode = mode;
        // RoleSources는 화면에 띄우는 오브젝트가 아니라 직업별 외형/Controller 원본이다.
        HideRoleSourceObjects();
        SetActive(swordWeapon, mode == WeaponMode.Sword);
        SetActive(bowWeapon, mode == WeaponMode.Bow);
        SetActive(shieldWeapon, mode == WeaponMode.Shield);

        activeBodyAnimator = bodyAnimator;
        activeSprite = bodySpriteRenderer;
        if (weaponPivot != null)
        {
            Vector2 pivotPosition = mode == WeaponMode.Sword ? swordPivotLocalPosition :
                mode == WeaponMode.Bow ? bowPivotLocalPosition : shieldPivotLocalPosition;
            weaponPivot.localPosition = pivotPosition;
        }
        FacePlayer();
    }

    private void RefreshAppearanceFromPlayer()
    {
        if (player == null)
            return;

        PlayerController targetController = player.GetComponent<PlayerController>();
        if (targetController == null)
            targetController = player.GetComponentInParent<PlayerController>();
        if (targetController == null)
            return;

        if (!hasAppliedPlayerAppearance || displayedPlayerRole != targetController.currentRole)
            ApplyPlayerRoleAppearance(targetController.currentRole);
    }

    private void ApplyPlayerRoleAppearance(PlayerRole role)
    {
        displayedPlayerRole = role;
        hasAppliedPlayerAppearance = true;

        GameObject source;
        switch (role)
        {
            case PlayerRole.Archer:
                source = bowVisual;
                break;
            case PlayerRole.Tank:
                source = shieldVisual;
                break;
            default:
                source = swordVisual;
                break;
        }

        ApplyRoleVisual(source);
        activeBodyAnimator = bodyAnimator;
        activeSprite = bodySpriteRenderer;
    }

    private void HideRoleSourceObjects()
    {
        SetActive(swordVisual, false);
        SetActive(bowVisual, false);
        SetActive(shieldVisual, false);
    }

    private void ApplyRoleVisual(GameObject source)
    {
        if (source == null)
            return;

        SpriteRenderer sourceRenderer = source.GetComponent<SpriteRenderer>();
        Animator sourceAnimator = source.GetComponent<Animator>();

        if (bodySpriteRenderer != null && sourceRenderer != null)
        {
            bodySpriteRenderer.sprite = sourceRenderer.sprite;
            bodySpriteRenderer.color = sourceRenderer.color;
        }

        if (bodyAnimator != null && sourceAnimator != null &&
            sourceAnimator.runtimeAnimatorController != null)
        {
            bodyAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            bodyAnimator.Rebind();
            bodyAnimator.Update(0f);
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }

    private void PlayWeaponAttack(GameObject weapon, string stateName)
    {
        if (weapon == null) return;
        Animator weaponAnimator = weapon.GetComponent<Animator>();
        if (weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null && !string.IsNullOrWhiteSpace(stateName))
            weaponAnimator.Play(stateName, 0, 0f);
    }

    private void FireArrow()
    {
        if (arrowPrefab == null || player == null) return;
        Vector3 origin = bowProjectileSpawnPoint != null ? bowProjectileSpawnPoint.position :
            weaponPivot != null ? weaponPivot.position : transform.position;
        Vector2 target = (Vector2)player.position + playerAimOffset;
        Vector2 direction = (target - (Vector2)origin).normalized;
        GameObject arrow = Instantiate(arrowPrefab, origin, Quaternion.FromToRotation(Vector3.right, direction));
        EnemyProjectile projectile = arrow.GetComponent<EnemyProjectile>();
        if (projectile != null) projectile.Launch(direction, arrowSpeed, bowDamage);
    }

    private void DamagePlayerInRange(Transform attackPoint, float range, int damage, float knockback)
    {
        Vector2 center = attackPoint != null ? attackPoint.position : transform.position;
        if (player == null || Vector2.Distance(center, player.position) > range) return;
        PlayerStats stats = player.GetComponentInParent<PlayerStats>();
        if (stats != null) stats.TakeDamage(damage, health, false);
        if (knockback > 0f)
        {
            Rigidbody2D playerBody = player.GetComponentInParent<Rigidbody2D>();
            if (playerBody != null)
            {
                Vector2 direction = ((Vector2)player.position - center).normalized;
                playerBody.AddForce(direction * knockback, ForceMode2D.Impulse);
            }
        }
    }

    private void SetMoving(bool moving)
    {
        if (activeBodyAnimator != null) activeBodyAnimator.SetBool("IsMoving", moving);
    }

    private void FacePlayer()
    {
        if (player == null || activeSprite == null) return;
        bool playerIsLeft = player.position.x < transform.position.x;
        activeSprite.flipX = playerIsLeft == spritesFaceRight;
    }

    private void AimWeapon()
    {
        if (weaponPivot == null || player == null) return;
        Vector2 target = (Vector2)player.position + playerAimOffset;
        Vector2 direction = target - (Vector2)weaponPivot.position;

        if (currentMode == WeaponMode.Shield && shieldUsesSideOnlyAim)
        {
            bool playerIsLeft = player.position.x < transform.position.x;
            float sideSign = playerIsLeft ? -1f : 1f;
            weaponPivot.localPosition = new Vector3(
                Mathf.Abs(shieldPivotLocalPosition.x) * sideSign,
                shieldPivotLocalPosition.y,
                0f);

            float rotation = shieldMirrorRotationOnLeft && playerIsLeft
                ? -shieldSideOnlyRotation
                : shieldSideOnlyRotation;
            weaponPivot.localRotation = Quaternion.Euler(0f, 0f, rotation);

            if (shieldWeapon != null)
            {
                Vector3 scale = shieldBaseScale;
                if (shieldFlipOnLeft && playerIsLeft)
                    scale.x = -scale.x;
                shieldWeapon.transform.localScale = scale;
            }
            return;
        }

        float angleOffset = currentMode == WeaponMode.Sword ? swordAimAngleOffset :
            currentMode == WeaponMode.Bow ? bowAimAngleOffset : shieldAimAngleOffset;
        weaponPivot.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + angleOffset);

        GameObject activeWeapon = currentMode == WeaponMode.Sword ? swordWeapon :
            currentMode == WeaponMode.Bow ? bowWeapon : shieldWeapon;
        if (activeWeapon != null)
        {
            bool flip = flipWeaponOnLeft && direction.x < 0f;
            foreach (SpriteRenderer renderer in activeWeapon.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.flipY = flip;
        }
    }

    private void FindPlayer()
    {
        if (player != null) return;
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null && target != gameObject) player = target.transform;
    }

    private void StopMovement()
    {
        if (body != null && body.simulated) body.linearVelocity = Vector2.zero;
    }

    private void HandleDeath(EnemyHealth _)
    {
        BossExitPortal.EnsurePortalForDefeatedBoss(health);
        isDead = true;
        if (skillRoutine != null) StopCoroutine(skillRoutine);
        health.SetDamageTakenMultiplier(1f);
        SetActive(swordWeapon, false);
        SetActive(bowWeapon, false);
        SetActive(shieldWeapon, false);
        StopMovement();
        SetMoving(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.8f);
        Vector3 swordCenter = swordHitPoint != null ? swordHitPoint.position : transform.position;
        Gizmos.DrawWireSphere(swordCenter, swordRange);
        Gizmos.color = new Color(0.25f, 0.65f, 1f, 0.8f);
        Vector3 shieldCenter = shieldBashPoint != null ? shieldBashPoint.position : transform.position;
        Gizmos.DrawWireSphere(shieldCenter, shieldBashRange);
    }
}
