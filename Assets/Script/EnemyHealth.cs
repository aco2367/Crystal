using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public event Action<EnemyHealth> Died;

    public int hp = 30;
    public int maxHp = 30;

    [Header("Debug")]
    public bool logDamage = true;

    [Header("Hit Feedback")]
    public GameObject hitVfxPrefab;
    public Vector3 hitVfxOffset = Vector3.zero;
    [Min(0f)] public float hitInvincibilityDuration = 0.15f;
    [Min(1)] public int hitFlashCount = 2;

    [Header("Death Rewards")]
    [Tooltip("CoinPickup 컴포넌트가 붙은 골드 프리팹입니다.")]
    public GameObject goldPickupPrefab;
    [Min(0)] public int minGoldDrop = 1;
    [Min(0)] public int maxGoldDrop = 3;
    [Tooltip("ExperiencePickup 컴포넌트가 붙은 경험치 프리팹입니다.")]
    public GameObject experiencePickupPrefab;
    [Min(0)] public int minExperienceDrop = 5;
    [Min(0)] public int maxExperienceDrop = 10;
    [Min(0f)] public float rewardSpawnRadius = 0.25f;

    [Header("Death Animation")]
    [Tooltip("비워두면 자신 또는 자식 오브젝트의 Animator를 자동으로 찾습니다.")]
    public Animator animator;
    public string deathTriggerParameter = "Die";
    [Tooltip("Animation Event가 없을 때 이 시간이 지나면 적을 자동으로 제거합니다.")]
    [Min(0f)] public float deathAnimationDuration = 1f;

    private HitShake hitShake;
    private bool isDead;
    private bool rewardsDropped;
    private Coroutine deathCoroutine;
    private Coroutine hitFeedbackCoroutine;
    private SpriteRenderer[] spriteRenderers;
    private Material hitFlashMaterial;
    private MaterialPropertyBlock flashPropertyBlock;
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    private void Awake()
    {
        CopyMissingRewardSettingsFromDuplicate();

        EnemyHealth[] healthComponents = GetComponents<EnemyHealth>();

        if (healthComponents.Length > 1 && healthComponents[0] != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }
        hitShake = GetComponent<HitShake>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        flashPropertyBlock = new MaterialPropertyBlock();

        Shader flashShader = Shader.Find("EmberKeep/EnemyHitFlash");
        if (flashShader != null)
        {
            hitFlashMaterial = new Material(flashShader);
            foreach (SpriteRenderer spriteRenderer in spriteRenderers)
            {
                if (spriteRenderer != null)
                    spriteRenderer.sharedMaterial = hitFlashMaterial;
            }
        }

        hp = Mathf.Clamp(hp, 0, maxHp);
    }

    private void OnDestroy()
    {
        if (hitFlashMaterial != null)
            Destroy(hitFlashMaterial);
    }

    private void CopyMissingRewardSettingsFromDuplicate()
    {
        if (goldPickupPrefab != null && experiencePickupPrefab != null)
            return;

        EnemyHealth[] healthComponents = GetComponents<EnemyHealth>();

        foreach (EnemyHealth other in healthComponents)
        {
            if (other == null || other == this)
                continue;

            if (goldPickupPrefab == null && other.goldPickupPrefab != null)
            {
                goldPickupPrefab = other.goldPickupPrefab;
                minGoldDrop = other.minGoldDrop;
                maxGoldDrop = other.maxGoldDrop;
            }

            if (experiencePickupPrefab == null && other.experiencePickupPrefab != null)
            {
                experiencePickupPrefab = other.experiencePickupPrefab;
                minExperienceDrop = other.minExperienceDrop;
                maxExperienceDrop = other.maxExperienceDrop;
            }

            rewardSpawnRadius = Mathf.Max(rewardSpawnRadius, other.rewardSpawnRadius);
        }
    }

    public void Setup(int newMaxHp)
    {
        maxHp = Mathf.Max(1, newMaxHp);
        hp = maxHp;
        isDead = false;
        rewardsDropped = false;

        if (hitFeedbackCoroutine != null)
        {
            StopCoroutine(hitFeedbackCoroutine);
            hitFeedbackCoroutine = null;
        }

        SetFlashAmount(0f);

        if (deathCoroutine != null)
        {
            StopCoroutine(deathCoroutine);
            deathCoroutine = null;
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead || hitFeedbackCoroutine != null)
            return;

        hp = Mathf.Max(0, hp - Mathf.Max(0, damage));

        if (logDamage)
            Debug.Log($"{gameObject.name} 피해량: {damage}, 남은 HP: {hp}/{maxHp}");

        if (hp <= 0)
        {
            Die();
            return;
        }

        PlayHitFeedback();
        hitFeedbackCoroutine = StartCoroutine(HitFlashAndInvincibilityRoutine());
    }

    private System.Collections.IEnumerator HitFlashAndInvincibilityRoutine()
    {
        int flashCount = Mathf.Max(1, hitFlashCount);
        float phaseDuration = hitInvincibilityDuration / (flashCount * 2f);

        for (int i = 0; i < flashCount; i++)
        {
            SetFlashAmount(1f);
            if (phaseDuration > 0f)
                yield return new WaitForSeconds(phaseDuration);

            SetFlashAmount(0f);
            if (phaseDuration > 0f)
                yield return new WaitForSeconds(phaseDuration);
        }

        SetFlashAmount(0f);
        hitFeedbackCoroutine = null;
    }

    private void SetFlashAmount(float amount)
    {
        if (spriteRenderers == null || flashPropertyBlock == null)
            return;

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null)
                continue;

            spriteRenderer.GetPropertyBlock(flashPropertyBlock);
            flashPropertyBlock.SetFloat(FlashAmountId, amount);
            spriteRenderer.SetPropertyBlock(flashPropertyBlock);
        }
    }

    private void PlayHitFeedback()
    {
        if (hitShake != null)
        {
            hitShake.Shake(0.08f, 0.06f);
        }

        if (hitVfxPrefab != null)
        {
            GameObject vfx = Instantiate(hitVfxPrefab, transform.position + hitVfxOffset, Quaternion.identity);
            Destroy(vfx, 1.5f);
        }
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (hitFeedbackCoroutine != null)
        {
            StopCoroutine(hitFeedbackCoroutine);
            hitFeedbackCoroutine = null;
        }

        SetFlashAmount(0f);
        Died?.Invoke(this);

        StopEnemyBehaviour();
        PlayDeathAnimation();
        deathCoroutine = StartCoroutine(DestroyAfterDeathAnimation());

        Debug.Log($"{gameObject.name} 사망");
    }

    private void DropDeathRewards()
    {
        if (rewardsDropped)
            return;

        rewardsDropped = true;

        int gold = GetRandomInclusive(minGoldDrop, maxGoldDrop);
        int experience = GetRandomExperienceMultipleOfFive(
            minExperienceDrop,
            maxExperienceDrop);

        if (gold > 0 && goldPickupPrefab != null)
        {
            for (int i = 0; i < gold; i++)
            {
                GameObject goldObject = Instantiate(
                    goldPickupPrefab,
                    GetRewardSpawnPosition(),
                    Quaternion.identity);

                CoinPickup pickup = goldObject.GetComponent<CoinPickup>();

                if (pickup == null)
                    pickup = goldObject.GetComponentInChildren<CoinPickup>();

                if (pickup != null)
                    pickup.value = 3;
                else
                    Debug.LogWarning($"{goldPickupPrefab.name}에 CoinPickup 컴포넌트가 없습니다.", goldObject);
            }
        }

        if (experience > 0 && experiencePickupPrefab != null)
        {
            const int experiencePerPickup = 5;
            int remainingExperience = experience;

            while (remainingExperience > 0)
            {
                GameObject experienceObject = Instantiate(
                    experiencePickupPrefab,
                    GetRewardSpawnPosition(),
                    Quaternion.identity);

                ExperiencePickup pickup = experienceObject.GetComponent<ExperiencePickup>();

                if (pickup == null)
                    pickup = experienceObject.GetComponentInChildren<ExperiencePickup>();

                if (pickup != null)
                {
                    pickup.value = Mathf.Min(experiencePerPickup, remainingExperience);
                    remainingExperience -= pickup.value;
                }
                else
                {
                    Debug.LogWarning($"{experiencePickupPrefab.name}에 ExperiencePickup 컴포넌트가 없습니다.", experienceObject);
                    break;
                }
            }
        }
    }

    private Vector3 GetRewardSpawnPosition()
    {
        Vector2 offset = UnityEngine.Random.insideUnitCircle * rewardSpawnRadius;
        return transform.position + new Vector3(offset.x, offset.y, 0f);
    }

    private int GetRandomInclusive(int minimum, int maximum)
    {
        int min = Mathf.Max(0, Mathf.Min(minimum, maximum));
        int max = Mathf.Max(min, Mathf.Max(minimum, maximum));
        return UnityEngine.Random.Range(min, max + 1);
    }

    private int GetRandomExperienceMultipleOfFive(int minimum, int maximum)
    {
        const int experienceUnit = 5;
        int min = Mathf.Max(0, Mathf.Min(minimum, maximum));
        int max = Mathf.Max(min, Mathf.Max(minimum, maximum));
        int minimumUnits = Mathf.CeilToInt(min / (float)experienceUnit);
        int maximumUnits = Mathf.FloorToInt(max / (float)experienceUnit);

        if (minimumUnits > maximumUnits)
            return Mathf.Max(experienceUnit, Mathf.RoundToInt(min / (float)experienceUnit) * experienceUnit);

        return UnityEngine.Random.Range(minimumUnits, maximumUnits + 1) * experienceUnit;
    }

    private void StopEnemyBehaviour()
    {
        EnemyController controller = GetComponent<EnemyController>();

        if (controller == null)
            controller = GetComponentInParent<EnemyController>();

        if (controller != null)
            controller.enabled = false;

        Rigidbody2D body = GetComponent<Rigidbody2D>();

        if (body == null)
            body = GetComponentInParent<Rigidbody2D>();

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D enemyCollider in colliders)
            enemyCollider.enabled = false;
    }

    private void PlayDeathAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(deathTriggerParameter))
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == deathTriggerParameter &&
                parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.ResetTrigger(deathTriggerParameter);
                animator.SetTrigger(deathTriggerParameter);
                return;
            }
        }

        Debug.LogWarning(
            $"{name}: Animator에 Trigger '{deathTriggerParameter}'가 없어 사망 애니메이션을 실행하지 못했습니다.",
            this);
    }

    private System.Collections.IEnumerator DestroyAfterDeathAnimation()
    {
        if (deathAnimationDuration > 0f)
            yield return new WaitForSeconds(deathAnimationDuration);

        CompleteDeathAnimation();
    }

    // 사망 Animation Clip의 마지막 프레임에 Animation Event로 연결할 수 있습니다.
    public void CompleteDeathAnimation()
    {
        if (!isDead)
            return;

        if (deathCoroutine != null)
        {
            StopCoroutine(deathCoroutine);
            deathCoroutine = null;
        }

        DropDeathRewards();
        Destroy(gameObject);
    }
}
