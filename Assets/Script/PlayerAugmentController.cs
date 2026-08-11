using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerDamageSource
{
    NormalAttack,
    Skill,
    Projectile,
    Reflected
}

public class PlayerAugmentController : MonoBehaviour
{
    [Header("Selected Augments")]
    public List<string> selectedAugmentNames = new List<string>();

    [Header("Blood Oath")]
    [Min(0f)] public float bloodOathDuration = 5f;
    [Min(0f)] public float bloodOathAttackSpeedPercentPerStack = 10f;
    [Min(1)] public int bloodOathMaxStacks = 5;

    [Header("Indomitable")]
    [Min(0f)] public float indomitableCooldown = 60f;
    [Range(0.01f, 1f)] public float indomitableSurviveHpPercent = 0.1f;

    [Header("Executioner")]
    [Range(0f, 1f)] public float executionerHealthThreshold = 0.4f;
    [Min(0f)] public float executionerDamageBonusPercent = 15f;

    [Header("Sword Aura")]
    public GameObject swordWaveProjectilePrefab;
    [Min(0f)] public float swordWaveDamageMultiplier = 0.3f;
    [Min(0f)] public float swordWaveSpeed = 9f;
    [Min(0f)] public float swordWaveRange = 5f;

    [Header("Thorn Armor")]
    [Min(0f)] public float thornArmorReflectPercent = 150f;

    [Header("Iron Charge")]
    [Min(0f)] public float ironChargeDamageMultiplier = 0.5f;

    [Header("Relentless Chase")]
    [Range(0f, 1f)] public float relentlessChaseDashCooldownRefundPercent = 0.5f;

    [Header("Overwhelm")]
    [Range(0f, 1f)] public float overwhelmEnemyAttackReductionPercent = 0.2f;
    [Range(0f, 1f)] public float overwhelmPlayerAttackBonusPercent = 0.2f;
    public Color overwhelmAuraColor = new Color(0.55f, 0.8f, 1f, 0.22f);
    [Min(8)] public int overwhelmAuraSegments = 96;
    [Min(0.001f)] public float overwhelmAuraLineWidth = 0.035f;

    [Header("Artisan")]
    [Min(0f)] public float artisanItemEffectBonusPercent = 30f;

    [Header("Ricochet Shot")]
    [Min(0f)] public float ricochetDamageMultiplier = 0.5f;
    [Min(0)] public int ricochetMaxBounces = 1;

    [Header("Explosive Arrow")]
    public GameObject explosiveArrowVfxPrefab;
    [Min(0f)] public float explosiveArrowDamageMultiplier = 0.3f;
    [Min(0f)] public float explosiveArrowRadius = 1.2f;

    [Header("Hunter Focus")]
    [Min(0f)] public float hunterFocusStandStillTime = 2f;
    [Min(0f)] public float hunterFocusDamageBonusPercent = 50f;
    [Min(0f)] public float movementThreshold = 0.01f;

    [Header("Frost Arrow")]
    public GameObject frostArrowVfxPrefab;
    [Range(0f, 1f)] public float frostArrowSlowPercent = 0.3f;
    [Min(0f)] public float frostArrowDuration = 2f;

    [Header("Multishot")]
    [Range(0f, 1f)] public float multishotDamageMultiplier = 0.7f;
    [Min(0f)] public float multishotSpreadAngle = 8f;

    private PlayerStats stats;
    private PlayerController playerController;
    private readonly Dictionary<string, string> selectedAugmentLookup = new Dictionary<string, string>();
    private int bloodOathStacks;
    private Coroutine bloodOathCoroutine;
    private float lastIndomitableTime = -999f;
    private float lastMoveTime;
    private LineRenderer overwhelmAuraRenderer;

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        playerController = GetComponent<PlayerController>();
        RebuildLookup();
        lastMoveTime = Time.time;
        UpdateOverwhelmAura();
    }

    private void Update()
    {
        UpdateOverwhelmAura();
    }

    public bool HasAugment(string augmentName)
    {
        string key = NormalizeAugmentName(augmentName);
        return !string.IsNullOrWhiteSpace(key) && selectedAugmentLookup.ContainsKey(key);
    }

    public void RestoreSelectedAugments(IList<string> augmentNames)
    {
        selectedAugmentNames.Clear();

        if (augmentNames != null)
        {
            for (int i = 0; i < augmentNames.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(augmentNames[i]) && !selectedAugmentNames.Contains(augmentNames[i]))
                    selectedAugmentNames.Add(augmentNames[i]);
            }
        }

        RebuildLookup();
        UpdateOverwhelmAura();
    }

    public bool RegisterAugment(AugmentData augment)
    {
        if (augment == null)
            return false;

        string displayName = !string.IsNullOrWhiteSpace(augment.augmentName) ? augment.augmentName : augment.id;
        string key = NormalizeAugmentName(displayName);
        if (string.IsNullOrWhiteSpace(key) || selectedAugmentLookup.ContainsKey(key))
            return false;

        selectedAugmentLookup[key] = displayName;
        selectedAugmentNames.Add(displayName);
        UpdateOverwhelmAura();
        return true;
    }

    public void NotifyMovement(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude > movementThreshold * movementThreshold)
            lastMoveTime = Time.time;
    }

    public int ModifyOutgoingDamage(EnemyHealth target, int damage, PlayerDamageSource source)
    {
        float multiplier = 1f;

        if (HasAugment("처형자") && target != null && target.maxHp > 0 && (float)target.hp / target.maxHp <= executionerHealthThreshold)
            multiplier += executionerDamageBonusPercent / 100f;

        if (HasAugment("사냥꾼의 집중") && Time.time >= lastMoveTime + hunterFocusStandStillTime)
            multiplier += hunterFocusDamageBonusPercent / 100f;

        if (HasAugment("압도") && IsInOverwhelmRange(target))
            multiplier += overwhelmPlayerAttackBonusPercent;

        return Mathf.Max(0, Mathf.RoundToInt(damage * multiplier));
    }

    public void OnHitEnemy(EnemyHealth target, int finalDamage, PlayerDamageSource source)
    {
        if (target == null)
            return;

        if (HasAugment("검술의 달인"))
            ReduceSkillCooldownByPercent(0.1f);

        if (HasAugment("서리화살") || HasAugment("서리 화살"))
            ApplySlow(target.gameObject, frostArrowSlowPercent, frostArrowDuration, frostArrowVfxPrefab);

        if (HasAugment("폭발화살") || HasAugment("폭발 화살"))
            ExplodeAt(target.transform.position, Mathf.RoundToInt(Mathf.Max(1, stats.attackPower * explosiveArrowDamageMultiplier)), explosiveArrowRadius);
    }

    public void OnEnemyKilled()
    {
        if (HasAugment("피의 맹세"))
            AddBloodOathStack();

        if (HasAugment("맹렬한 추격") && playerController != null)
            playerController.ReduceDodgeCooldownRemaining(relentlessChaseDashCooldownRefundPercent);
    }

    public bool TryIndomitable(ref int incomingDamage)
    {
        if (!HasAugment("불굴") || stats == null)
            return false;

        if (Time.time < lastIndomitableTime + indomitableCooldown)
            return false;

        if (stats.hp - incomingDamage > 0)
            return false;

        lastIndomitableTime = Time.time;
        incomingDamage = Mathf.Max(0, stats.hp - Mathf.Max(1, Mathf.RoundToInt(stats.maxHp * indomitableSurviveHpPercent)));
        return true;
    }

    public void ReflectMeleeDamage(EnemyHealth attacker, int rawDamage)
    {
        if (!HasAugment("가시 갑옷") || attacker == null || rawDamage <= 0)
            return;

        int reflectedDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * thornArmorReflectPercent / 100f));
        attacker.TakeDamage(reflectedDamage);
    }

    public int ModifyIncomingEnemyDamage(int rawDamage, Transform enemyTransform)
    {
        if (HasAugment("압도") && IsTransformInOverwhelmRange(enemyTransform))
            rawDamage = Mathf.RoundToInt(rawDamage * (1f - overwhelmEnemyAttackReductionPercent));

        return Mathf.Max(0, rawDamage);
    }

    public float GetItemEffectMultiplier()
    {
        return HasAugment("장인") ? 1f + artisanItemEffectBonusPercent / 100f : 1f;
    }

    public bool HasMultishot()
    {
        return HasAugment("멀티 샷") || HasAugment("멀티샷");
    }

    public bool HasSwordWave()
    {
        return HasAugment("검기 방출") || HasAugment("검기방출");
    }

    public bool HasIronCharge()
    {
        return HasAugment("철갑 돌진") || HasAugment("철갑돌진");
    }

    public int GetIronChargeDamage()
    {
        return stats != null ? Mathf.Max(1, Mathf.RoundToInt(stats.attackPower * ironChargeDamageMultiplier)) : 1;
    }

    public float GetOverwhelmRadius(float fallbackAttackRange)
    {
        return Mathf.Max(0.01f, fallbackAttackRange * 2f);
    }

    private void AddBloodOathStack()
    {
        int previousStacks = bloodOathStacks;
        bloodOathStacks = Mathf.Clamp(bloodOathStacks + 1, 1, bloodOathMaxStacks);

        if (stats != null && bloodOathStacks > previousStacks)
        {
            float multiplier = 1f + bloodOathAttackSpeedPercentPerStack / 100f;
            stats.attackSpeed = Mathf.Max(0.01f, stats.attackSpeed * multiplier);
        }

        if (bloodOathCoroutine != null)
            StopCoroutine(bloodOathCoroutine);

        bloodOathCoroutine = StartCoroutine(ClearBloodOathAfterDelay());
    }

    private IEnumerator ClearBloodOathAfterDelay()
    {
        yield return new WaitForSeconds(bloodOathDuration);

        if (stats != null && bloodOathStacks > 0)
        {
            float multiplier = Mathf.Pow(1f + bloodOathAttackSpeedPercentPerStack / 100f, bloodOathStacks);
            stats.attackSpeed = Mathf.Max(0.01f, stats.attackSpeed / Mathf.Max(0.01f, multiplier));
        }

        bloodOathStacks = 0;
        bloodOathCoroutine = null;
    }

    private void ReduceSkillCooldownByPercent(float percent)
    {
        if (playerController == null)
            return;

        playerController.ReduceSkillCooldownRemaining(percent);
    }

    private void ExplodeAt(Vector3 position, int damage, float radius)
    {
        if (explosiveArrowVfxPrefab != null)
            Destroy(Instantiate(explosiveArrowVfxPrefab, position, Quaternion.identity), 1.5f);

        Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius, playerController != null ? playerController.enemyLayer : default);
        for (int i = 0; i < hits.Length; i++)
        {
            EnemyHealth enemy = hits[i].GetComponent<EnemyHealth>();
            if (enemy == null)
                enemy = hits[i].GetComponentInParent<EnemyHealth>();
            if (enemy != null)
                enemy.TakeDamage(damage);
        }
    }

    private void ApplySlow(GameObject target, float slowPercent, float duration, GameObject vfxPrefab)
    {
        EnemyController enemyController = target.GetComponent<EnemyController>();
        if (enemyController == null)
            enemyController = target.GetComponentInParent<EnemyController>();

        if (enemyController != null)
            enemyController.ApplyTemporaryMoveSpeedMultiplier(1f - slowPercent, duration);

        if (vfxPrefab != null)
            Destroy(Instantiate(vfxPrefab, target.transform.position, Quaternion.identity, target.transform), duration);
    }

    private bool IsInOverwhelmRange(EnemyHealth enemy)
    {
        return enemy != null && IsTransformInOverwhelmRange(enemy.transform);
    }

    private bool IsTransformInOverwhelmRange(Transform target)
    {
        if (target == null || playerController == null)
            return false;

        return Vector2.Distance(transform.position, target.position) <= GetOverwhelmRadius(playerController.tankRange);
    }

    private void UpdateOverwhelmAura()
    {
        bool shouldShow = HasAugment("압도") && playerController != null && playerController.CurrentRole == PlayerRole.Tank;

        if (!shouldShow)
        {
            if (overwhelmAuraRenderer != null)
                overwhelmAuraRenderer.enabled = false;
            return;
        }

        EnsureOverwhelmAuraRenderer();
        DrawOverwhelmAura(GetOverwhelmRadius(playerController.tankRange));
    }

    private void EnsureOverwhelmAuraRenderer()
    {
        if (overwhelmAuraRenderer != null)
            return;

        GameObject auraObject = new GameObject("OverwhelmAura");
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = Vector3.zero;
        overwhelmAuraRenderer = auraObject.AddComponent<LineRenderer>();
        overwhelmAuraRenderer.useWorldSpace = false;
        overwhelmAuraRenderer.loop = true;
        overwhelmAuraRenderer.material = new Material(Shader.Find("Sprites/Default"));
        overwhelmAuraRenderer.sortingOrder = 8;
    }

    private void DrawOverwhelmAura(float radius)
    {
        int segments = Mathf.Max(8, overwhelmAuraSegments);
        overwhelmAuraRenderer.enabled = true;
        overwhelmAuraRenderer.positionCount = segments;
        overwhelmAuraRenderer.startWidth = overwhelmAuraLineWidth;
        overwhelmAuraRenderer.endWidth = overwhelmAuraLineWidth;
        overwhelmAuraRenderer.startColor = overwhelmAuraColor;
        overwhelmAuraRenderer.endColor = overwhelmAuraColor;

        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            overwhelmAuraRenderer.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
    }

    private void RebuildLookup()
    {
        selectedAugmentLookup.Clear();
        for (int i = 0; i < selectedAugmentNames.Count; i++)
        {
            string key = NormalizeAugmentName(selectedAugmentNames[i]);
            if (!string.IsNullOrWhiteSpace(key) && !selectedAugmentLookup.ContainsKey(key))
                selectedAugmentLookup.Add(key, selectedAugmentNames[i]);
        }
    }

    private static string NormalizeAugmentName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace(" ", string.Empty).Trim();
    }
}
