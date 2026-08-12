using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public enum PlayerRole
{
    Sword,
    Archer,
    Tank
}

public class PlayerController : MonoBehaviour
{
    private PlayerStats stats;
    private Rigidbody2D rb;

    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.down;
    private Vector2 attackDirection = Vector2.down;

    private float lastAttackTime;
    private float lastSkillTime = -999f;
    private bool isDodging;
    private bool isUsingSkill;
    private bool isDead;
    private Coroutine tankSkillCoroutine;
    private Coroutine weaponAttackResetCoroutine;
    private Coroutine attackFacingLockCoroutine;
    private Sprite swordWeaponIdleSprite;
    private Sprite archerWeaponIdleSprite;
    private Sprite tankWeaponIdleSprite;
    private PlayerAugmentController augmentController;
    private WeaponAimController weaponAimController;
    private Vector3 initialLocalScale;
    private Vector3 initialPlayerVisualLocalScale;
    private bool isFacingLeft;
    private bool isAttackFacingLocked;

    [Header("Role")]
    public PlayerRole currentRole = PlayerRole.Sword;

    public float SkillCooldownRemaining
    {
        get
        {
            return Mathf.Max(0f, lastSkillTime + GetCurrentSkillCooldown() - Time.time);
        }
    }

    public float SkillCooldownDuration
    {
        get
        {
            return GetCurrentSkillCooldown();
        }
    }

    public PlayerRole CurrentRole => currentRole;

    public void SetRole(PlayerRole newRole)
    {
        ChangeRole(newRole);
    }
    [Header("Sword Stats")]
    public int swordMaxHp = 250;
    public int swordAttackPower = 30;
    public float swordMoveSpeed = 4.8f;
    public float swordAttackSpeed = 1f;
    public float swordCriticalChance = 0.1f;
    public float swordCriticalDamage = 1.5f;

    [Header("Archer Stats")]
    public int archerMaxHp = 180;
    public int archerAttackPower = 26;
    public float archerMoveSpeed = 5.3f;
    public float archerAttackSpeed = 1.2f;
    public float archerCriticalChance = 0.2f;
    public float archerCriticalDamage = 1.5f;

    [Header("Tank Stats")]
    public int tankMaxHp = 400;
    public int tankAttackPower = 22;
    public float tankMoveSpeed = 4.2f;
    public float tankAttackSpeed = 0.8f;
    public float tankCriticalChance = 0.1f;
    public float tankCriticalDamage = 1.5f;

    [Header("Main Appearance")]
    public Transform playerVisualRoot;
    public SpriteRenderer playerSpriteRenderer;
    public Animator playerAnimator;
    public int playerSortingOrder = 10;

    [Header("Animation Parameters")]
    public string isMovingParameter = "IsMoving";
    public string moveXParameter = "MoveX";
    public string moveYParameter = "MoveY";
    public string deathTriggerParameter = "Die";
    public bool hideWeaponOnDeath = true;
    public bool useHorizontalFlip = true;
    public bool swordSpritesFaceLeft = true;
    public bool archerSpritesFaceLeft;
    public bool tankSpritesFaceLeft;
    public bool playCharacterAttackAnimation;
    public float attackFacingLockDuration = 0.2f;

    [Header("Role Appearance Sources")]
    public GameObject swordVisualSource;
    public GameObject archerVisualSource;
    public GameObject tankVisualSource;

    [Header("Attack Visualizer")]
    public AttackRangeVisualizer attackRangeVisualizer;

    [Header("Attack Layer")]
    public LayerMask enemyLayer;
    public Transform attackCenter;

    [Header("Weapon")]
    public Transform weaponTip;
    public GameObject swordWeaponObject;
    public GameObject archerWeaponObject;
    public GameObject tankWeaponObject;
    public Animator swordWeaponAnimator;
    public Animator archerWeaponAnimator;
    public Animator tankWeaponAnimator;
    public string weaponAttackTrigger = "Attack";
    public string swordWeaponIdleState = "Idle";
    public string archerWeaponIdleState = "";
    public string tankWeaponIdleState = "Idle";
    public string swordWeaponAttackState = "SwordAttack";
    public string archerWeaponAttackState = "ArcherAttact";
    public string tankWeaponAttackState = "TankAttack";
    public int weaponSortingOrder = 4;

    [Header("Sword Attack")]
    public float swordRange = 1.2f;
    public float swordAngle = 180f;

    [Header("Archer Attack")]
    public float archerRange = 6f;
    public GameObject playerProjectilePrefab;
    public Transform projectileSpawnPoint;
    public float playerProjectileSpeed = 9f;

    [Header("Tank Attack")]
    public float tankRange = 1.5f;

    [Header("Sword Skill - Whirlwind")]
    public float swordSkillCooldown = 8f;
    public float swordSkillDuration = 2f;
    public float swordSkillRadius = 2.2f;
    public float swordSkillDamageMultiplier = 2f;
    public int swordSkillHitCount = 2;

    [Header("Tank Skill - Endurance")]
    public float tankSkillCooldown = 12f;
    public float tankSkillDuration = 4f;
    public float tankSkillRadius = 2.3f;
    public float tankSkillDamageMultiplier = 2f;
    public float tankSkillShieldPercent = 0.2f;
    public float tankSkillDamageReduction = 0.5f;

    [Header("Archer Skill - Retreat Shot")]
    public float archerSkillCooldown = 4f;
    public float archerSkillBackstepDistance = 2f;
    public float archerSkillBackstepDuration = 0.15f;
    public float archerSkillProjectileRange = 6f;
    public float archerSkillProjectileSpeed = 11f;
    public float archerSkillDamageMultiplier = 1.2f;
    public float archerSkillSpreadAngle = 18f;

    [Header("Dodge")]
    [Tooltip("체크하면 Dodge Force 대신 Dodge Distance / Dodge Duration으로 대쉬 속도를 계산합니다.")]
    public bool useDodgeDistance = true;
    [Min(0f)] public float dodgeDistance = 2f;
    public float dodgeForce = 8f;
    public float dodgeDuration = 0.2f;
    public float dodgeCooldown = 1f;

    [Header("Dash Afterimage")]
    public bool useDashAfterimage = true;
    [Min(0.01f)] public float afterimageInterval = 0.035f;
    [Min(0.01f)] public float afterimageLifetime = 0.18f;
    public Color afterimageColor = new Color(1f, 1f, 1f, 0.45f);
    public int afterimageSortingOrderOffset = -1;

    private float lastDodgeTime;
    private bool hasAppliedRoleAppearance;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        rb = GetComponent<Rigidbody2D>();
        augmentController = GetComponent<PlayerAugmentController>();
        if (augmentController == null)
            augmentController = gameObject.AddComponent<PlayerAugmentController>();
        weaponAimController = GetComponent<WeaponAimController>();
        initialLocalScale = transform.localScale;
        Transform visualTransform = GetPlayerVisualTransform();
        initialPlayerVisualLocalScale = visualTransform != null
            ? visualTransform.localScale
            : Vector3.one;

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        CacheWeaponIdleSprites();
        HideRoleSources();

        if (GameSession.Instance != null)
        {
            currentRole = GameSession.Instance.playerRole;
        }

        ChangeRole(currentRole);
    }

    private void Update()
    {
        if (isDead || (stats != null && stats.isDead))
            return;

        ReadMoveInput();
        if (augmentController != null)
            augmentController.NotifyMovement(moveInput);
        ReadMouseDirection();
        ReadRoleInput();
        UpdateMovementAnimation();
        UpdateAttackRangeVisualizer();

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            NormalAttack();
        }

        if (KeyBindingManager.WasPressedThisFrame(GameKeyAction.Skill))
        {
            UseSkill();
        }

        if (KeyBindingManager.WasPressedThisFrame(GameKeyAction.Interact))
        {
            Interact();
        }

        if (KeyBindingManager.WasPressedThisFrame(GameKeyAction.Dodge))
        {
            Dodge();
        }
    }

    private void FixedUpdate()
    {
        if (rb == null || stats == null)
            return;

        if (isDead || stats.isDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isDodging)
            return;

        rb.linearVelocity = moveInput * stats.moveSpeed;
    }

    public void HandleDeath()
    {
        isDead = true;
        moveInput = Vector2.zero;
        StopAllCoroutines();
        isDodging = false;
        isUsingSkill = false;
        isAttackFacingLocked = false;
        tankSkillCoroutine = null;
        attackFacingLockCoroutine = null;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (attackRangeVisualizer != null)
        {
            attackRangeVisualizer.gameObject.SetActive(false);
        }

        if (playerAnimator != null)
        {
            SetAnimatorBool(isMovingParameter, false);
            SetAnimatorTrigger(deathTriggerParameter);
        }

        if (hideWeaponOnDeath)
        {
            SetWeaponObjectActive(swordWeaponObject, false);
            SetWeaponObjectActive(archerWeaponObject, false);
            SetWeaponObjectActive(tankWeaponObject, false);
        }
    }

    public void HandleRespawn()
    {
        isDead = false;
        moveInput = Vector2.zero;
        isDodging = false;
        isUsingSkill = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (attackRangeVisualizer != null)
        {
            attackRangeVisualizer.gameObject.SetActive(true);
        }

        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.enabled = true;
        }

        ApplyAppearanceFromSource(GetCurrentVisualSource());
        ApplyWeaponForCurrentRole();
    }

    private void ReadMoveInput()
    {
        if (Keyboard.current == null)
        {
            moveInput = Vector2.zero;
            return;
        }

        float x = 0f;
        float y = 0f;

        if (KeyBindingManager.IsPressed(GameKeyAction.MoveLeft))
            x -= 1f;

        if (KeyBindingManager.IsPressed(GameKeyAction.MoveRight))
            x += 1f;

        if (KeyBindingManager.IsPressed(GameKeyAction.MoveDown))
            y -= 1f;

        if (KeyBindingManager.IsPressed(GameKeyAction.MoveUp))
            y += 1f;

        moveInput = new Vector2(x, y).normalized;

        if (moveInput != Vector2.zero)
        {
            lastMoveDirection = moveInput;
        }
    }

    private void ReadMouseDirection()
    {
        if (Mouse.current == null || Camera.main == null)
            return;

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        mouseWorldPosition.z = 0f;

        Vector2 direction = mouseWorldPosition - GetAttackCenterPosition();

        if (direction != Vector2.zero)
        {
            attackDirection = direction.normalized;
        }
    }

    private void ReadRoleInput()
    {
        if (Keyboard.current == null || isUsingSkill)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            ChangeRole(PlayerRole.Sword);

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            ChangeRole(PlayerRole.Archer);

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            ChangeRole(PlayerRole.Tank);
    }

    private void ChangeRole(PlayerRole newRole)
    {
        if (hasAppliedRoleAppearance && currentRole == newRole)
            return;

        float hpPercent = 1f;
        int maxHpBonus = 0;
        int attackPowerBonus = 0;
        float moveSpeedBonus = 0f;
        float attackSpeedBonus = 0f;
        float criticalChanceBonus = 0f;
        float criticalDamageBonus = 0f;

        if (stats != null && hasAppliedRoleAppearance)
        {
            hpPercent = stats.maxHp > 0 ? (float)stats.hp / stats.maxHp : 1f;
            GetRoleBaseStats(
                currentRole,
                out int oldMaxHp,
                out int oldAttackPower,
                out float oldMoveSpeed,
                out float oldAttackSpeed,
                out float oldCriticalChance,
                out float oldCriticalDamage);

            maxHpBonus = stats.maxHp - oldMaxHp;
            attackPowerBonus = stats.attackPower - oldAttackPower;
            moveSpeedBonus = stats.moveSpeed - oldMoveSpeed;
            attackSpeedBonus = stats.attackSpeed - oldAttackSpeed;
            criticalChanceBonus = stats.criticalChance - oldCriticalChance;
            criticalDamageBonus = stats.criticalDamage - oldCriticalDamage;
        }

        currentRole = newRole;

        if (stats != null)
        {
            stats.ClearDefenseBuffs();
            ApplyRoleStats();

            if (hasAppliedRoleAppearance)
            {
                stats.maxHp = Mathf.Max(1, stats.maxHp + maxHpBonus);
                stats.attackPower = Mathf.Max(0, stats.attackPower + attackPowerBonus);
                stats.moveSpeed = Mathf.Max(0.01f, stats.moveSpeed + moveSpeedBonus);
                stats.attackSpeed = Mathf.Max(0.01f, stats.attackSpeed + attackSpeedBonus);
                stats.criticalChance = Mathf.Clamp01(stats.criticalChance + criticalChanceBonus);
                stats.criticalDamage = Mathf.Max(1f, stats.criticalDamage + criticalDamageBonus);
                stats.hp = Mathf.Clamp(Mathf.RoundToInt(stats.maxHp * hpPercent), 1, stats.maxHp);
            }
        }

        GameObject source = GetCurrentVisualSource();
        ApplyAppearanceFromSource(source);
        ApplyWeaponForCurrentRole();
        hasAppliedRoleAppearance = true;

        Debug.Log($"역할 변경: {currentRole}");
    }

    private void GetRoleBaseStats(
        PlayerRole role,
        out int roleMaxHp,
        out int roleAttackPower,
        out float roleMoveSpeed,
        out float roleAttackSpeed,
        out float roleCriticalChance,
        out float roleCriticalDamage)
    {
        switch (role)
        {
            case PlayerRole.Archer:
                roleMaxHp = archerMaxHp;
                roleAttackPower = archerAttackPower;
                roleMoveSpeed = archerMoveSpeed;
                roleAttackSpeed = archerAttackSpeed;
                roleCriticalChance = archerCriticalChance;
                roleCriticalDamage = archerCriticalDamage;
                return;

            case PlayerRole.Tank:
                roleMaxHp = tankMaxHp;
                roleAttackPower = tankAttackPower;
                roleMoveSpeed = tankMoveSpeed;
                roleAttackSpeed = tankAttackSpeed;
                roleCriticalChance = tankCriticalChance;
                roleCriticalDamage = tankCriticalDamage;
                return;

            default:
                roleMaxHp = swordMaxHp;
                roleAttackPower = swordAttackPower;
                roleMoveSpeed = swordMoveSpeed;
                roleAttackSpeed = swordAttackSpeed;
                roleCriticalChance = swordCriticalChance;
                roleCriticalDamage = swordCriticalDamage;
                return;
        }
    }

    private void ApplyRoleStats()
    {
        switch (currentRole)
        {
            case PlayerRole.Sword:
                stats.ApplyRoleStats(swordMaxHp, swordAttackPower, swordMoveSpeed, swordAttackSpeed, swordCriticalChance, swordCriticalDamage);
                break;
            case PlayerRole.Archer:
                stats.ApplyRoleStats(archerMaxHp, archerAttackPower, archerMoveSpeed, archerAttackSpeed, archerCriticalChance, archerCriticalDamage);
                break;
            case PlayerRole.Tank:
                stats.ApplyRoleStats(tankMaxHp, tankAttackPower, tankMoveSpeed, tankAttackSpeed, tankCriticalChance, tankCriticalDamage);
                break;
        }
    }

    private GameObject GetCurrentVisualSource()
    {
        switch (currentRole)
        {
            case PlayerRole.Sword:
                return swordVisualSource;
            case PlayerRole.Archer:
                return archerVisualSource;
            case PlayerRole.Tank:
                return tankVisualSource;
        }

        return null;
    }

    private void ApplyAppearanceFromSource(GameObject source)
    {
        if (source == null)
            return;

        SpriteRenderer sourceSpriteRenderer = source.GetComponentInChildren<SpriteRenderer>(true);
        Animator sourceAnimator = source.GetComponentInChildren<Animator>(true);

        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.gameObject.SetActive(true);
            playerSpriteRenderer.enabled = true;
            playerSpriteRenderer.sortingOrder = playerSortingOrder;

            if (sourceSpriteRenderer != null)
            {
                playerSpriteRenderer.sprite = sourceSpriteRenderer.sprite;
                playerSpriteRenderer.color = sourceSpriteRenderer.color;
                playerSpriteRenderer.flipX = false;
                playerSpriteRenderer.flipY = sourceSpriteRenderer.flipY;
            }
        }

        if (playerAnimator != null)
        {
            playerAnimator.enabled = true;

            if (sourceAnimator != null && sourceAnimator.runtimeAnimatorController != null)
            {
                playerAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            }
        }
    }

    private void HideRoleSources()
    {
        DisableSourceRenderer(swordVisualSource);
        DisableSourceRenderer(archerVisualSource);
        DisableSourceRenderer(tankVisualSource);
    }

    private void DisableSourceRenderer(GameObject source)
    {
        if (source == null)
            return;

        SpriteRenderer[] renderers = source.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == playerSpriteRenderer)
                continue;

            renderer.enabled = false;
        }
    }

    private void ApplyWeaponForCurrentRole()
    {
        SetWeaponObjectActive(swordWeaponObject, currentRole == PlayerRole.Sword);
        SetWeaponObjectActive(archerWeaponObject, currentRole == PlayerRole.Archer);
        SetWeaponObjectActive(tankWeaponObject, currentRole == PlayerRole.Tank);

        GameObject currentWeapon = GetCurrentWeaponObject();

        if (currentWeapon != null)
        {
            SpriteRenderer[] renderers = currentWeapon.GetComponentsInChildren<SpriteRenderer>(true);

            foreach (SpriteRenderer renderer in renderers)
            {
                renderer.enabled = true;
                renderer.sortingOrder = weaponSortingOrder;
            }

        }
    }

    private void SetWeaponObjectActive(GameObject weaponObject, bool active)
    {
        if (weaponObject != null)
        {
            weaponObject.SetActive(active);
        }
    }

    private GameObject GetCurrentWeaponObject()
    {
        switch (currentRole)
        {
            case PlayerRole.Sword:
                return swordWeaponObject;
            case PlayerRole.Archer:
                return archerWeaponObject;
            case PlayerRole.Tank:
                return tankWeaponObject;
        }

        return null;
    }

    private void UpdateMovementAnimation()
    {
        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        if (playerAnimator != null)
        {
            SetAnimatorBool(isMovingParameter, isMoving);
            float animationMoveX = Mathf.Abs(moveInput.x) > 0.01f ? GetCurrentBaseHorizontalMoveX() : 0f;
            SetAnimatorFloat(moveXParameter, animationMoveX);
            SetAnimatorFloat(moveYParameter, moveInput.y);
        }

        if (useHorizontalFlip && !isAttackFacingLocked)
        {
            if (moveInput.x < -0.01f)
            {
                SetPlayerFacingLeft(true);
            }
            else if (moveInput.x > 0.01f)
            {
                SetPlayerFacingLeft(false);
            }
        }
    }

    private void SetPlayerFacingLeft(bool facingLeft)
    {
        isFacingLeft = facingLeft;

        Transform visualTransform = GetPlayerVisualTransform();
        if (visualTransform == null)
            return;

        Vector3 scale = initialPlayerVisualLocalScale;
        bool shouldFlipCharacter = facingLeft != GetCurrentSpritesFaceLeft();
        scale.x = Mathf.Abs(scale.x) * (shouldFlipCharacter ? -1f : 1f);
        visualTransform.localScale = scale;

        transform.localScale = initialLocalScale;

        if (playerSpriteRenderer != null)
        {
            playerSpriteRenderer.flipX = false;
        }
    }

    private Transform GetPlayerVisualTransform()
    {
        if (playerVisualRoot != null)
            return playerVisualRoot;

        if (playerSpriteRenderer != null)
            return playerSpriteRenderer.transform;

        return null;
    }

    private float GetCurrentBaseHorizontalMoveX()
    {
        return GetCurrentSpritesFaceLeft() ? -1f : 1f;
    }

    private bool GetCurrentSpritesFaceLeft()
    {
        switch (currentRole)
        {
            case PlayerRole.Sword:
                return swordSpritesFaceLeft;
            case PlayerRole.Archer:
                return archerSpritesFaceLeft;
            case PlayerRole.Tank:
                return tankSpritesFaceLeft;
            default:
                return true;
        }
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        foreach (AnimatorControllerParameter parameter in playerAnimator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Bool)
            {
                playerAnimator.SetBool(parameterName, value);
                return;
            }
        }
    }

    private void SetAnimatorFloat(string parameterName, float value)
    {
        if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        foreach (AnimatorControllerParameter parameter in playerAnimator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Float)
            {
                playerAnimator.SetFloat(parameterName, value);
                return;
            }
        }
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        foreach (AnimatorControllerParameter parameter in playerAnimator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                playerAnimator.SetTrigger(parameterName);
                return;
            }
        }
    }

    private void SetWeaponAnimatorTrigger(string parameterName)
    {
        Animator weaponAnimator = GetCurrentWeaponAnimator();

        if (weaponAnimator == null || weaponAnimator.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        string attackStateName = GetCurrentWeaponAttackStateName();
        bool playedAttackState = false;

        if (!string.IsNullOrWhiteSpace(attackStateName))
        {
            ResetAnimatorTriggerIfExists(weaponAnimator, parameterName);
            weaponAnimator.Play(attackStateName, 0, 0f);
            weaponAnimator.Update(0f);
            playedAttackState = true;
        }

        if (!playedAttackState)
        {
            foreach (AnimatorControllerParameter parameter in weaponAnimator.parameters)
            {
                if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    ResetAnimatorTriggerIfExists(weaponAnimator, parameterName);
                    weaponAnimator.SetTrigger(parameterName);
                    break;
                }
            }
        }

        if (playedAttackState)
        {
            RestartWeaponAttackReset(weaponAnimator);
        }
    }

    private Animator GetCurrentWeaponAnimator()
    {
        switch (currentRole)
        {
            case PlayerRole.Sword:
                return swordWeaponAnimator != null
                    ? swordWeaponAnimator
                    : swordWeaponObject != null ? swordWeaponObject.GetComponentInChildren<Animator>(true) : null;
            case PlayerRole.Archer:
                return archerWeaponAnimator != null
                    ? archerWeaponAnimator
                    : archerWeaponObject != null ? archerWeaponObject.GetComponentInChildren<Animator>(true) : null;
            case PlayerRole.Tank:
                return tankWeaponAnimator != null
                    ? tankWeaponAnimator
                    : tankWeaponObject != null ? tankWeaponObject.GetComponentInChildren<Animator>(true) : null;
        }

        return null;
    }

    private string GetCurrentWeaponAttackStateName()
    {
        switch (currentRole)
        {
            case PlayerRole.Sword:
                return swordWeaponAttackState;
            case PlayerRole.Archer:
                return archerWeaponAttackState;
            case PlayerRole.Tank:
                return tankWeaponAttackState;
        }

        return null;
    }

    private void RestartWeaponAttackReset(Animator weaponAnimator)
    {
        if (weaponAttackResetCoroutine != null)
        {
            StopCoroutine(weaponAttackResetCoroutine);
        }

        weaponAttackResetCoroutine = StartCoroutine(ResetWeaponAfterAttack(weaponAnimator, currentRole));
    }

    private IEnumerator ResetWeaponAfterAttack(Animator weaponAnimator, PlayerRole role)
    {
        float waitTime = 0.5f;

        if (weaponAnimator != null)
        {
            AnimatorStateInfo stateInfo = weaponAnimator.GetCurrentAnimatorStateInfo(0);

            if (stateInfo.length > 0f)
            {
                waitTime = stateInfo.length + 0.05f;
            }
        }

        yield return new WaitForSeconds(waitTime);

        if (currentRole == role)
        {
            string idleStateName = GetWeaponIdleStateName(role);

            if (weaponAnimator != null && weaponAnimator.runtimeAnimatorController != null && !string.IsNullOrWhiteSpace(idleStateName))
            {
                weaponAnimator.Play(idleStateName, 0, 0f);
                weaponAnimator.Update(0f);
            }

            RestoreWeaponIdleSprite(role);
        }

        weaponAttackResetCoroutine = null;
    }

    private void CacheWeaponIdleSprites()
    {
        swordWeaponIdleSprite = GetWeaponSprite(swordWeaponObject);
        archerWeaponIdleSprite = GetWeaponSprite(archerWeaponObject);
        tankWeaponIdleSprite = GetWeaponSprite(tankWeaponObject);
    }

    private Sprite GetWeaponSprite(GameObject weaponObject)
    {
        if (weaponObject == null)
            return null;

        SpriteRenderer renderer = weaponObject.GetComponentInChildren<SpriteRenderer>(true);
        return renderer != null ? renderer.sprite : null;
    }

    private string GetWeaponIdleStateName(PlayerRole role)
    {
        switch (role)
        {
            case PlayerRole.Sword:
                return swordWeaponIdleState;
            case PlayerRole.Archer:
                return archerWeaponIdleState;
            case PlayerRole.Tank:
                return tankWeaponIdleState;
        }

        return null;
    }

    private void ResetAnimatorTriggerIfExists(Animator animator, string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.ResetTrigger(parameterName);
                return;
            }
        }
    }

    private void RestoreWeaponIdleSprite(PlayerRole role)
    {
        GameObject weaponObject = GetCurrentWeaponObject();

        if (weaponObject == null)
            return;

        SpriteRenderer renderer = weaponObject.GetComponentInChildren<SpriteRenderer>(true);

        if (renderer == null)
            return;

        switch (role)
        {
            case PlayerRole.Sword:
                if (swordWeaponIdleSprite != null)
                    renderer.sprite = swordWeaponIdleSprite;
                break;
            case PlayerRole.Archer:
                if (archerWeaponIdleSprite != null)
                    renderer.sprite = archerWeaponIdleSprite;
                break;
            case PlayerRole.Tank:
                if (tankWeaponIdleSprite != null)
                    renderer.sprite = tankWeaponIdleSprite;
                break;
        }
    }

    private void UpdateAttackRangeVisualizer()
    {
        if (attackRangeVisualizer == null)
            return;

        Transform rangeTransform = attackRangeVisualizer.transform;

        if (rangeTransform.parent == transform)
        {
            Vector3 centerPosition = GetAttackCenterPosition();
            rangeTransform.position = centerPosition;
            rangeTransform.localRotation = Quaternion.identity;
            rangeTransform.localScale = new Vector3(isFacingLeft ? -1f : 1f, 1f, 1f);
        }
        else
        {
            rangeTransform.position = GetAttackCenterPosition();
            rangeTransform.rotation = Quaternion.identity;
        }

        attackRangeVisualizer.DrawRange(currentRole, attackDirection, swordRange, swordAngle, archerRange, tankRange);
    }

    private void NormalAttack()
    {
        if (stats == null || stats.isDead || isUsingSkill)
            return;

        float attackDelay = 1f / stats.attackSpeed;

        if (Time.time < lastAttackTime + attackDelay)
            return;

        lastAttackTime = Time.time;
        int damage = CalculateDamage();

        FaceMouseSideForAttack();
        StartAttackFacingLock();

        switch (currentRole)
        {
            case PlayerRole.Sword:
                SwordAttack(damage);
                if (augmentController != null && augmentController.HasSwordWave())
                    FireProjectile(attackDirection, Mathf.RoundToInt(stats.attackPower * augmentController.swordWaveDamageMultiplier), augmentController.swordWaveRange, augmentController.swordWaveSpeed, true, augmentController.swordWaveProjectilePrefab);
                break;
            case PlayerRole.Archer:
                ArcherAttack(damage);
                break;
            case PlayerRole.Tank:
                TankAttack(damage);
                break;
        }

        if (playCharacterAttackAnimation && playerAnimator != null)
        {
            SetAnimatorTrigger("Attack");
        }

        SetWeaponAnimatorTrigger(weaponAttackTrigger);

        if (weaponAimController != null)
        {
            weaponAimController.PlayAttackSlash();
        }
    }

    private void StartAttackFacingLock()
    {
        if (attackFacingLockCoroutine != null)
        {
            StopCoroutine(attackFacingLockCoroutine);
        }

        float duration = attackFacingLockDuration;

        if (weaponAimController != null)
        {
            duration = Mathf.Max(duration, weaponAimController.attackDuration + weaponAimController.returnDuration);
        }

        attackFacingLockCoroutine = StartCoroutine(AttackFacingLockRoutine(duration));
    }

    private IEnumerator AttackFacingLockRoutine(float duration)
    {
        isAttackFacingLocked = true;
        yield return new WaitForSeconds(Mathf.Max(0.01f, duration));
        isAttackFacingLocked = false;
        attackFacingLockCoroutine = null;
    }

    private void FaceMouseSideForAttack()
    {
        if (!useHorizontalFlip)
            return;

        if (!TryGetMouseWorldPosition(out Vector3 mouseWorldPosition))
            return;

        float deltaX = mouseWorldPosition.x - transform.position.x;

        if (deltaX < -0.01f)
        {
            SetPlayerFacingLeft(true);
        }
        else if (deltaX > 0.01f)
        {
            SetPlayerFacingLeft(false);
        }
    }

    private bool TryGetMouseWorldPosition(out Vector3 mouseWorldPosition)
    {
        mouseWorldPosition = Vector3.zero;

        if (Mouse.current == null || Camera.main == null)
            return false;

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
        mouseWorldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        mouseWorldPosition.z = 0f;
        return true;
    }

    private int CalculateDamage(float multiplier = 1f, float bonusCritChance = 0f)
    {
        bool isCritical = Random.value < Mathf.Clamp01(stats.criticalChance + bonusCritChance);
        int damage = Mathf.RoundToInt(stats.attackPower * multiplier);

        if (isCritical)
        {
            damage = Mathf.RoundToInt(damage * stats.criticalDamage);
        }

        return damage;
    }

    private void SwordAttack(int damage)
    {
        Vector2 center = GetAttackCenterPosition();
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(center, swordRange, enemyLayer);

        foreach (Collider2D enemy in hitEnemies)
        {
            Vector2 directionToEnemy = ((Vector2)enemy.transform.position - center).normalized;
            float angle = Vector2.Angle(attackDirection, directionToEnemy);

            if (angle <= swordAngle * 0.5f)
            {
                DamageEnemy(enemy, damage);
            }
        }
    }

    private void ArcherAttack(int damage)
    {
        if (augmentController != null && augmentController.HasMultishot())
        {
            int multishotDamage = Mathf.Max(1, Mathf.RoundToInt(damage * augmentController.multishotDamageMultiplier));
            FireProjectile(RotateVector(attackDirection, -augmentController.multishotSpreadAngle), multishotDamage, archerRange, playerProjectileSpeed);
            FireProjectile(RotateVector(attackDirection, augmentController.multishotSpreadAngle), multishotDamage, archerRange, playerProjectileSpeed);
            return;
        }

        FireProjectile(attackDirection, damage, archerRange, playerProjectileSpeed);
    }

    private void TankAttack(int damage)
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(GetAttackCenterPosition(), tankRange, enemyLayer);

        foreach (Collider2D enemy in hitEnemies)
        {
            DamageEnemy(enemy, damage);
        }
    }

    private Vector3 GetAttackCenterPosition()
    {
        return attackCenter != null ? attackCenter.position : transform.position;
    }

    private void UseSkill()
    {
        if (stats == null || stats.isDead || isUsingSkill)
            return;

        float cooldown = GetCurrentSkillCooldown();

        if (Time.time < lastSkillTime + cooldown)
        {
            Debug.Log($"스킬 쿨타임: {lastSkillTime + cooldown - Time.time:0.0}초");
            return;
        }

        lastSkillTime = Time.time;

        switch (currentRole)
        {
            case PlayerRole.Sword:
                StartCoroutine(SwordSkillRoutine());
                break;
            case PlayerRole.Archer:
                StartCoroutine(ArcherSkillRoutine());
                break;
            case PlayerRole.Tank:
                if (tankSkillCoroutine != null)
                    StopCoroutine(tankSkillCoroutine);
                tankSkillCoroutine = StartCoroutine(TankSkillRoutine());
                break;
        }
    }

    private float GetCurrentSkillCooldown()
    {
        float baseCooldown;

        switch (currentRole)
        {
            case PlayerRole.Sword:
                baseCooldown = swordSkillCooldown;
                break;
            case PlayerRole.Archer:
                baseCooldown = archerSkillCooldown;
                break;
            case PlayerRole.Tank:
                baseCooldown = tankSkillCooldown;
                break;
            default:
                baseCooldown = 1f;
                break;
        }

        float cooldownReduction = stats != null
            ? Mathf.Clamp(stats.skillCooldownReduction, 0f, 0.9f)
            : 0f;

        return Mathf.Max(0.05f, baseCooldown * (1f - cooldownReduction));
    }

    public void ReduceSkillCooldownRemaining(float percent)
    {
        float cooldown = GetCurrentSkillCooldown();
        if (cooldown <= 0f)
            return;

        lastSkillTime -= cooldown * Mathf.Clamp01(percent);
    }

    public void ReduceDodgeCooldownRemaining(float percent)
    {
        if (dodgeCooldown <= 0f)
            return;

        lastDodgeTime -= dodgeCooldown * Mathf.Clamp01(percent);
    }

    private IEnumerator SwordSkillRoutine()
    {
        isUsingSkill = true;
        moveInput = Vector2.zero;
        rb.linearVelocity = Vector2.zero;

        if (playerAnimator != null)
            SetAnimatorTrigger("Skill");

        int damage = CalculateDamage(swordSkillDamageMultiplier);
        int hitCount = Mathf.Max(1, swordSkillHitCount);
        float hitInterval = swordSkillDuration / hitCount;

        for (int i = 0; i < hitCount; i++)
        {
            DamageEnemiesInCircle(swordSkillRadius, damage, PlayerDamageSource.Skill);
            yield return new WaitForSeconds(hitInterval);
        }

        isUsingSkill = false;
    }

    private IEnumerator TankSkillRoutine()
    {
        isUsingSkill = true;

        if (playerAnimator != null)
            SetAnimatorTrigger("Skill");

        int shieldAmount = Mathf.RoundToInt(stats.maxHp * tankSkillShieldPercent);
        stats.AddShield(shieldAmount);
        stats.SetDamageReduction(tankSkillDamageReduction);

        int damage = CalculateDamage(tankSkillDamageMultiplier);
        DamageEnemiesInCircle(tankSkillRadius, damage, PlayerDamageSource.Skill);

        isUsingSkill = false;

        yield return new WaitForSeconds(tankSkillDuration);

        stats.ClearDefenseBuffs();
        tankSkillCoroutine = null;
    }

    private IEnumerator ArcherSkillRoutine()
    {
        isUsingSkill = true;

        if (playerAnimator != null)
            SetAnimatorTrigger("Skill");

        Vector2 forward = attackDirection != Vector2.zero ? attackDirection : lastMoveDirection;
        Vector2 backward = -forward.normalized;

        if (rb != null)
        {
            float duration = Mathf.Max(0.01f, archerSkillBackstepDuration);
            float speed = archerSkillBackstepDistance / duration;
            float elapsed = 0f;

            moveInput = Vector2.zero;
            isDodging = true;
            rb.linearVelocity = backward * speed;
            StartCoroutine(DashAfterimageRoutine(duration));

            while (elapsed < duration)
            {
                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            rb.linearVelocity = Vector2.zero;
            isDodging = false;
        }
        else
        {
            transform.position += (Vector3)(backward * archerSkillBackstepDistance);
        }

        int damage = CalculateDamage(archerSkillDamageMultiplier, 0.4f);
        FireProjectile(RotateVector(forward, -archerSkillSpreadAngle), damage, archerSkillProjectileRange, archerSkillProjectileSpeed);
        FireProjectile(forward, damage, archerSkillProjectileRange, archerSkillProjectileSpeed);
        FireProjectile(RotateVector(forward, archerSkillSpreadAngle), damage, archerSkillProjectileRange, archerSkillProjectileSpeed);

        yield return new WaitForSeconds(0.1f);
        isUsingSkill = false;
    }

    private void DamageEnemiesInCircle(float radius, int damage)
    {
        DamageEnemiesInCircle(radius, damage, PlayerDamageSource.NormalAttack);
    }

    private void DamageEnemiesInCircle(float radius, int damage, PlayerDamageSource source)
    {
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, radius, enemyLayer);

        foreach (Collider2D enemy in hitEnemies)
        {
            DamageEnemy(enemy, damage, source);
        }
    }

    private void DamageEnemy(Collider2D enemy, int damage)
    {
        DamageEnemy(enemy, damage, PlayerDamageSource.NormalAttack);
    }

    private void DamageEnemy(Collider2D enemy, int damage, PlayerDamageSource source)
    {
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();

        if (enemyHealth == null)
            enemyHealth = enemy.GetComponentInParent<EnemyHealth>();

        if (enemyHealth == null)
            enemyHealth = enemy.GetComponentInChildren<EnemyHealth>();

        if (enemyHealth != null)
        {
            int finalDamage = augmentController != null
                ? augmentController.ModifyOutgoingDamage(enemyHealth, damage, source)
                : damage;
            bool wasAlive = enemyHealth.hp > 0;
            enemyHealth.TakeDamage(finalDamage);
            if (augmentController != null)
            {
                augmentController.OnHitEnemy(enemyHealth, finalDamage, source);
                if (wasAlive && enemyHealth.hp <= 0)
                    augmentController.OnEnemyKilled();
            }
        }
    }

    private void FireProjectile(Vector2 direction, int damage, float range, float speed)
    {
        FireProjectile(direction, damage, range, speed, false);
    }

    private void FireProjectile(Vector2 direction, int damage, float range, float speed, bool pierceEnemies)
    {
        FireProjectile(direction, damage, range, speed, pierceEnemies, playerProjectilePrefab);
    }

    private void FireProjectile(Vector2 direction, int damage, float range, float speed, bool pierceEnemies, GameObject projectilePrefab)
    {
        if (projectilePrefab == null)
        {
            Debug.LogWarning("Player Projectile Prefab이 연결되지 않았습니다.");
            return;
        }

        Vector3 spawnPosition = weaponTip != null
            ? weaponTip.position
            : projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : transform.position + (Vector3)(direction.normalized * 0.4f);

        GameObject projectileObject = Instantiate(projectilePrefab, spawnPosition, Quaternion.identity);
        PlayerProjectile projectile = projectileObject.GetComponent<PlayerProjectile>();

        if (projectile != null)
        {
            projectile.Launch(direction, speed, damage, range, enemyLayer);
            projectile.ownerAugmentController = augmentController;
            projectile.damageSource = PlayerDamageSource.Projectile;
            projectile.destroyOnHit = !pierceEnemies;
        }
        else
        {
            Debug.LogWarning("Player Projectile Prefab에 PlayerProjectile 스크립트가 없습니다.");
        }
    }

    private Vector2 RotateVector(Vector2 vector, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        ).normalized;
    }

    private void Interact()
    {
        Debug.Log("상호작용");
    }

    private void Dodge()
    {
        if (rb == null || stats == null || stats.isDead || isUsingSkill)
            return;

        if (Time.time < lastDodgeTime + dodgeCooldown)
            return;

        Vector2 dodgeDirection = moveInput != Vector2.zero ? moveInput : lastMoveDirection;

        lastDodgeTime = Time.time;
        StartCoroutine(DodgeRoutine(dodgeDirection));
    }

    private IEnumerator DodgeRoutine(Vector2 direction)
    {
        isDodging = true;
        float duration = Mathf.Max(0.01f, dodgeDuration);
        float speed = useDodgeDistance
            ? dodgeDistance / duration
            : dodgeForce;
        rb.linearVelocity = direction.normalized * speed;
        StartCoroutine(DashAfterimageRoutine(duration));

        yield return new WaitForSeconds(duration);

        rb.linearVelocity = Vector2.zero;
        isDodging = false;
    }

    private IEnumerator DashAfterimageRoutine(float duration)
    {
        if (!useDashAfterimage || playerSpriteRenderer == null)
            yield break;

        float elapsed = 0f;
        float interval = Mathf.Max(0.01f, afterimageInterval);

        while (elapsed < duration)
        {
            CreateAfterimage();
            yield return new WaitForSeconds(interval);
            elapsed += interval;
        }
    }

    private void CreateAfterimage()
    {
        if (playerSpriteRenderer == null || playerSpriteRenderer.sprite == null)
            return;

        GameObject afterimage = new GameObject("DashAfterimage");
        afterimage.transform.position = playerSpriteRenderer.transform.position;
        afterimage.transform.rotation = playerSpriteRenderer.transform.rotation;
        afterimage.transform.localScale = playerSpriteRenderer.transform.lossyScale;

        SpriteRenderer renderer = afterimage.AddComponent<SpriteRenderer>();
        renderer.sprite = playerSpriteRenderer.sprite;
        renderer.flipX = playerSpriteRenderer.flipX;
        renderer.flipY = playerSpriteRenderer.flipY;
        renderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
        renderer.sortingOrder = playerSpriteRenderer.sortingOrder + afterimageSortingOrderOffset;
        renderer.color = afterimageColor;

        StartCoroutine(FadeAndDestroyAfterimage(renderer, afterimageLifetime));
    }

    private IEnumerator FadeAndDestroyAfterimage(SpriteRenderer renderer, float lifetime)
    {
        if (renderer == null)
            yield break;

        float duration = Mathf.Max(0.01f, lifetime);
        float elapsed = 0f;
        Color startColor = renderer.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            renderer.color = color;
            yield return null;
        }

        if (renderer != null)
            Destroy(renderer.gameObject);
    }
}




