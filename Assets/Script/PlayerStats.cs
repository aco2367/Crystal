using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Level")]
    public int level = 1;
    public int exp = 0;
    public int expToNextLevel = 100;

    [Header("HP")]
    public int hp = 100;
    public int maxHp = 100;

    [Header("Combat")]
    public int attackPower = 10;
    public float attackSpeed = 1f;
    public float criticalChance = 0.1f;
    public float criticalDamage = 1.5f;
    [Range(0f, 0.9f)] public float skillCooldownReduction;

    [Header("Movement")]
    public float moveSpeed = 5f;

    [Header("Currency")]
    public int gold = 0;
    public int crystal = 0;

    [Header("Training Points")]
    [Min(0)] public int trainingPoints;
    [Min(0)] public int attackTrainingLevel;
    [Min(0)] public int attackSpeedTrainingLevel;
    [Min(0)] public int maxHpTrainingLevel;

    [Header("Augment Points")]
    [Min(0)] public int augmentPoints;

    [Header("Training Settings")]
    [Min(0)] public int attackPowerPerTrainingPoint = 3;
    [Min(0f)] public float attackSpeedPerTrainingPoint = 0.1f;
    [Min(0)] public int maxHpPerTrainingPoint = 20;
    [Min(0)] public int maxAttackTrainingLevel = 10;
    [Min(0)] public int maxAttackSpeedTrainingLevel = 10;
    [Min(0)] public int maxHpTrainingLevelLimit = 10;

    [Header("Defense Buff")]
    public int shield;
    public float damageReduction;

    [Header("Hit Flash")]
    public bool useHitFlash = true;
    public Color hitFlashColor = Color.white;
    [Min(0f)] public float hitFlashDuration = 0.08f;
    public Material hitFlashMaterial;
    public SpriteRenderer[] hitFlashRenderers;

    [Header("Death")]
    public bool isDead;
    public GameObject gameOverPanel;
    [Tooltip("사망 애니메이션이 끝난 뒤 게임오버 창을 표시할 때까지 기다리는 시간입니다.")]
    [Min(0f)] public float deathAnimationDuration = 1f;
    [Tooltip("사망 클립 마지막 Animation Event로 완료 시점을 직접 알릴 때 체크합니다.")]
    public bool useDeathAnimationEvent;

    private PlayerController playerController;
    private Rigidbody2D rb;
    private Coroutine deathSequenceCoroutine;
    private Coroutine hitFlashCoroutine;
    private Material runtimeHitFlashMaterial;
    private SpriteRenderer[] activeHitFlashRenderers;
    private Color[] activeHitFlashOriginalColors;
    private Material[] activeHitFlashOriginalMaterials;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody2D>();

        FindGameOverPanelIfNeeded();

        level = Mathf.Max(1, level);
        hp = Mathf.Clamp(hp, 0, maxHp);

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    public void ApplyRoleStats(int newMaxHp, int newAttackPower, float newMoveSpeed, float newAttackSpeed, float newCriticalChance, float newCriticalDamage)
    {
        float hpPercent = maxHp > 0 ? (float)hp / maxHp : 1f;

        maxHp = Mathf.Max(1, newMaxHp);
        attackPower = Mathf.Max(0, newAttackPower);
        moveSpeed = newMoveSpeed;
        attackSpeed = Mathf.Max(0.01f, newAttackSpeed);
        criticalChance = Mathf.Clamp01(newCriticalChance);
        criticalDamage = Mathf.Max(1f, newCriticalDamage);

        hp = Mathf.Clamp(Mathf.RoundToInt(maxHp * hpPercent), 1, maxHp);
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null, false);
    }

    public void TakeDamage(int damage, EnemyHealth meleeAttacker, bool isProjectile)
    {
        if (isDead)
            return;

        PlayerAugmentController augmentController = GetComponent<PlayerAugmentController>();
        int rawDamage = damage;
        if (augmentController != null)
            rawDamage = augmentController.ModifyIncomingEnemyDamage(rawDamage, meleeAttacker != null ? meleeAttacker.transform : null);

        // EnemyController의 Attack Damage를 기본 최종 피해량으로 사용한다.
        int incomingDamage = Mathf.Max(0, rawDamage);
        incomingDamage = Mathf.RoundToInt(incomingDamage * (1f - Mathf.Clamp01(damageReduction)));
        incomingDamage = Mathf.Max(1, incomingDamage);

        if (augmentController != null)
            augmentController.TryIndomitable(ref incomingDamage);

        if (shield > 0)
        {
            int blockedDamage = Mathf.Min(shield, incomingDamage);
            shield -= blockedDamage;
            incomingDamage -= blockedDamage;
        }

        hp = Mathf.Max(0, hp - incomingDamage);
        PlayHitFlash();

        if (augmentController != null && meleeAttacker != null && !isProjectile)
            augmentController.ReflectMeleeDamage(meleeAttacker, rawDamage);

        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(0.1f, 0.1f);
        }

        Debug.Log($"플레이어 피해: {incomingDamage}, 보호막: {shield}, 현재 HP: {hp}");

        if (hp <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (isDead)
            return;

        hp = Mathf.Min(maxHp, hp + amount);
        Debug.Log($"회복: {amount}, 현재 HP: {hp}");
    }

    public void AddShield(int amount)
    {
        shield = Mathf.Max(shield, amount);
        Debug.Log($"보호막 생성: {shield}");
    }

    public void SetDamageReduction(float reduction)
    {
        damageReduction = Mathf.Clamp01(reduction);
    }

    public void ClearDefenseBuffs()
    {
        shield = 0;
        damageReduction = 0f;
    }

    public void AddExp(int amount)
    {
        if (isDead)
            return;

        exp += amount;
        Debug.Log($"경험치 획득: {amount}");

        while (exp >= expToNextLevel)
        {
            exp -= expToNextLevel;
            LevelUp();
        }
    }

    public void AddGold(int amount)
    {
        gold += amount;
        Debug.Log($"골드 획득: {amount}, 현재 골드: {gold}");
    }

    public bool SpendGold(int amount)
    {
        amount = Mathf.Max(0, amount);

        if (gold < amount)
            return false;

        gold -= amount;
        Debug.Log($"골드 사용: {amount}, 현재 골드: {gold}");
        return true;
    }

    public bool IncreaseAttackTraining()
    {
        if (trainingPoints <= 0 || attackTrainingLevel >= maxAttackTrainingLevel)
            return false;

        trainingPoints--;
        attackTrainingLevel++;
        attackPower += attackPowerPerTrainingPoint;
        return true;
    }

    public bool DecreaseAttackTraining()
    {
        if (attackTrainingLevel <= 0)
            return false;

        attackTrainingLevel--;
        trainingPoints++;
        attackPower = Mathf.Max(0, attackPower - attackPowerPerTrainingPoint);
        return true;
    }

    public bool IncreaseAttackSpeedTraining()
    {
        if (trainingPoints <= 0 || attackSpeedTrainingLevel >= maxAttackSpeedTrainingLevel)
            return false;

        trainingPoints--;
        attackSpeedTrainingLevel++;
        attackSpeed += attackSpeedPerTrainingPoint;
        return true;
    }

    public bool DecreaseAttackSpeedTraining()
    {
        if (attackSpeedTrainingLevel <= 0)
            return false;

        attackSpeedTrainingLevel--;
        trainingPoints++;
        attackSpeed = Mathf.Max(0.01f, attackSpeed - attackSpeedPerTrainingPoint);
        return true;
    }

    public bool IncreaseMaxHpTraining()
    {
        if (trainingPoints <= 0 || maxHpTrainingLevel >= maxHpTrainingLevelLimit)
            return false;

        trainingPoints--;
        maxHpTrainingLevel++;
        maxHp += maxHpPerTrainingPoint;
        return true;
    }

    public bool DecreaseMaxHpTraining()
    {
        if (maxHpTrainingLevel <= 0)
            return false;

        maxHpTrainingLevel--;
        trainingPoints++;
        maxHp = Mathf.Max(1, maxHp - maxHpPerTrainingPoint);
        hp = Mathf.Min(hp, maxHp);
        return true;
    }

    public void ApplyShopItem(ShopItemData item)
    {
        if (item == null)
            return;

        PlayerAugmentController augmentController = GetComponent<PlayerAugmentController>();
        float itemEffectMultiplier = augmentController != null ? augmentController.GetItemEffectMultiplier() : 1f;
        float attackSpeedMultiplier = 1f + Mathf.Max(0f, item.attackSpeedPercent * itemEffectMultiplier) / 100f;

        attackSpeed = Mathf.Max(0.01f, attackSpeed * attackSpeedMultiplier);
        int attackPowerBonus = Mathf.RoundToInt(item.attackPowerBonus * itemEffectMultiplier);
        int maxHpBonus = Mathf.RoundToInt(item.maxHpBonus * itemEffectMultiplier);
        attackPower = Mathf.Max(0, attackPower + attackPowerBonus);
        criticalChance = Mathf.Clamp01(criticalChance + Mathf.Max(0f, item.criticalChancePercent * itemEffectMultiplier) / 100f);
        maxHp = Mathf.Max(1, maxHp + maxHpBonus);
        hp = Mathf.Clamp(hp + maxHpBonus, 0, maxHp);
        skillCooldownReduction = Mathf.Clamp(
            skillCooldownReduction + Mathf.Max(0f, item.skillCooldownReductionPercent * itemEffectMultiplier) / 100f,
            0f,
            0.9f);
    }

    public void RemoveShopItem(ShopItemData item)
    {
        if (item == null)
            return;

        PlayerAugmentController augmentController = GetComponent<PlayerAugmentController>();
        float itemEffectMultiplier = augmentController != null ? augmentController.GetItemEffectMultiplier() : 1f;
        float attackSpeedMultiplier = 1f + Mathf.Max(0f, item.attackSpeedPercent * itemEffectMultiplier) / 100f;

        attackSpeed = Mathf.Max(0.01f, attackSpeed / Mathf.Max(0.01f, attackSpeedMultiplier));
        int attackPowerBonus = Mathf.RoundToInt(item.attackPowerBonus * itemEffectMultiplier);
        int maxHpBonus = Mathf.RoundToInt(item.maxHpBonus * itemEffectMultiplier);
        attackPower = Mathf.Max(0, attackPower - attackPowerBonus);
        criticalChance = Mathf.Clamp01(criticalChance - Mathf.Max(0f, item.criticalChancePercent * itemEffectMultiplier) / 100f);
        maxHp = Mathf.Max(1, maxHp - maxHpBonus);
        hp = Mathf.Clamp(hp - maxHpBonus, 0, maxHp);
        skillCooldownReduction = Mathf.Clamp(
            skillCooldownReduction - Mathf.Max(0f, item.skillCooldownReductionPercent * itemEffectMultiplier) / 100f,
            0f,
            0.9f);
    }

    private void LevelUp()
    {
        level++;
        trainingPoints++;
        augmentPoints++;
        expToNextLevel += 50;

        maxHp += 20;

        attackPower += 3;

        Debug.Log($"레벨업! 현재 레벨: {level}");
    }

    public bool HasAugmentPoint()
    {
        return augmentPoints > 0;
    }

    public bool SpendAugmentPoint()
    {
        if (augmentPoints <= 0)
            return false;

        augmentPoints--;
        return true;
    }

    private void PlayHitFlash()
    {
        if (!useHitFlash || hitFlashDuration <= 0f)
            return;

        EnsureHitFlashRenderers();

        if (hitFlashRenderers == null || hitFlashRenderers.Length == 0)
            return;

        if (hitFlashCoroutine != null)
        {
            StopCoroutine(hitFlashCoroutine);
            RestoreHitFlashRenderers();
        }

        hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private System.Collections.IEnumerator HitFlashRoutine()
    {
        EnsureHitFlashRenderers();

        activeHitFlashRenderers = hitFlashRenderers;
        activeHitFlashOriginalColors = new Color[activeHitFlashRenderers.Length];
        activeHitFlashOriginalMaterials = new Material[activeHitFlashRenderers.Length];
        Material flashMaterial = GetHitFlashMaterial();

        for (int i = 0; i < activeHitFlashRenderers.Length; i++)
        {
            if (activeHitFlashRenderers[i] == null)
                continue;

            activeHitFlashOriginalColors[i] = activeHitFlashRenderers[i].color;
            activeHitFlashOriginalMaterials[i] = activeHitFlashRenderers[i].sharedMaterial;
            if (flashMaterial != null)
                activeHitFlashRenderers[i].sharedMaterial = flashMaterial;
            activeHitFlashRenderers[i].color = hitFlashColor;
        }

        yield return new WaitForSecondsRealtime(hitFlashDuration);

        RestoreHitFlashRenderers();
        hitFlashCoroutine = null;
    }

    private void RestoreHitFlashRenderers()
    {
        if (activeHitFlashRenderers == null)
            return;

        for (int i = 0; i < activeHitFlashRenderers.Length; i++)
        {
            SpriteRenderer target = activeHitFlashRenderers[i];
            if (target == null)
                continue;

            if (activeHitFlashOriginalColors != null && i < activeHitFlashOriginalColors.Length)
                target.color = activeHitFlashOriginalColors[i];

            if (activeHitFlashOriginalMaterials != null && i < activeHitFlashOriginalMaterials.Length)
                target.sharedMaterial = activeHitFlashOriginalMaterials[i];
        }

        activeHitFlashRenderers = null;
        activeHitFlashOriginalColors = null;
        activeHitFlashOriginalMaterials = null;
    }

    private Material GetHitFlashMaterial()
    {
        if (hitFlashMaterial != null)
        {
            if (hitFlashMaterial.HasProperty("_Color"))
                hitFlashMaterial.SetColor("_Color", hitFlashColor);
            return hitFlashMaterial;
        }

        if (runtimeHitFlashMaterial == null)
        {
            Shader shader = Shader.Find("Custom/WhiteFlashSprite");
            if (shader != null)
            {
                runtimeHitFlashMaterial = new Material(shader);
                runtimeHitFlashMaterial.name = "Runtime White Flash Sprite";
            }
        }

        if (runtimeHitFlashMaterial != null && runtimeHitFlashMaterial.HasProperty("_Color"))
            runtimeHitFlashMaterial.SetColor("_Color", hitFlashColor);

        return runtimeHitFlashMaterial;
    }

    private void EnsureHitFlashRenderers()
    {
        if (hitFlashRenderers != null && hitFlashRenderers.Length > 0)
            return;

        if (playerController != null && playerController.playerSpriteRenderer != null)
        {
            hitFlashRenderers = new[] { playerController.playerSpriteRenderer };
            return;
        }

        SpriteRenderer fallbackRenderer = GetComponentInChildren<SpriteRenderer>(true);
        hitFlashRenderers = fallbackRenderer != null ? new[] { fallbackRenderer } : new SpriteRenderer[0];
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        hp = 0;
        ClearDefenseBuffs();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (playerController != null)
        {
            playerController.HandleDeath();
        }

        if (WaveManager.Instance != null && WaveManager.Instance.IsDefensePhase)
        {
            Debug.Log("방어 페이즈 사망: 잠시 후 집에서 부활");
            WaveManager.Instance.HandlePlayerDefenseDeath(this);
            return;
        }

        if (!useDeathAnimationEvent)
            deathSequenceCoroutine = StartCoroutine(ShowGameOverAfterDeathAnimation());

        Debug.Log("플레이어 사망");
    }

    private System.Collections.IEnumerator ShowGameOverAfterDeathAnimation()
    {
        if (deathAnimationDuration > 0f)
            yield return new WaitForSeconds(deathAnimationDuration);

        CompleteDeathSequence();
    }

    // 사망 Animation Clip 마지막 프레임의 Animation Event에서 호출할 수 있습니다.
    public void CompleteDeathSequence()
    {
        if (!isDead)
            return;

        if (deathSequenceCoroutine != null)
        {
            StopCoroutine(deathSequenceCoroutine);
            deathSequenceCoroutine = null;
        }

        FindGameOverPanelIfNeeded();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        else
        {
            Debug.LogWarning("GameOverPanel을 찾지 못해 게임 오버 UI를 표시할 수 없습니다.", this);
        }
    }

    private void FindGameOverPanelIfNeeded()
    {
        if (gameOverPanel != null)
            return;

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();

        foreach (GameObject candidate in allObjects)
        {
            if (candidate == null || candidate.name != "GameOverPanel")
                continue;

            if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
                continue;

            gameOverPanel = candidate;
            return;
        }
    }

    public void RespawnAt(Vector3 position)
    {
        if (deathSequenceCoroutine != null)
        {
            StopCoroutine(deathSequenceCoroutine);
            deathSequenceCoroutine = null;
        }

        transform.position = position;
        hp = maxHp;
        isDead = false;
        ClearDefenseBuffs();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (playerController != null)
        {
            playerController.HandleRespawn();
        }

        Debug.Log("플레이어 부활");
    }
}
