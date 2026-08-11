using System;
using UnityEngine;

[Serializable]
public class AugmentData
{
    public string id;
    public string augmentName;
    [TextArea] public string description;
    public Sprite icon;
    public AugmentCardUI cardPrefab;
    public bool keepPrefabVisuals;

    [Header("Effects")]
    public int attackPowerBonus;
    [Tooltip("15 means attack speed increases by 15%.")]
    public float attackSpeedPercent;
    [Tooltip("10 means critical chance increases by 10 percentage points.")]
    public float criticalChancePercent;
    [Tooltip("20 means critical damage multiplier increases by 20%.")]
    public float criticalDamagePercent;
    public int maxHpBonus;
    [Tooltip("10 means skill cooldown is reduced by 10%.")]
    public float skillCooldownReductionPercent;
    public float moveSpeedBonus;

    public void Apply(PlayerStats stats)
    {
        if (stats == null)
            return;

        PlayerAugmentController augmentController = stats.GetComponent<PlayerAugmentController>();
        if (augmentController == null)
            augmentController = stats.gameObject.AddComponent<PlayerAugmentController>();

        augmentController.RegisterAugment(this);

        stats.attackPower = Mathf.Max(0, stats.attackPower + attackPowerBonus);

        float attackSpeedMultiplier = 1f + Mathf.Max(0f, attackSpeedPercent) / 100f;
        stats.attackSpeed = Mathf.Max(0.01f, stats.attackSpeed * attackSpeedMultiplier);

        stats.criticalChance = Mathf.Clamp01(stats.criticalChance + Mathf.Max(0f, criticalChancePercent) / 100f);
        stats.criticalDamage = Mathf.Max(1f, stats.criticalDamage + Mathf.Max(0f, criticalDamagePercent) / 100f);

        if (maxHpBonus != 0)
        {
            stats.maxHp = Mathf.Max(1, stats.maxHp + maxHpBonus);
            stats.hp = Mathf.Clamp(stats.hp + maxHpBonus, 0, stats.maxHp);
        }

        stats.skillCooldownReduction = Mathf.Clamp(
            stats.skillCooldownReduction + Mathf.Max(0f, skillCooldownReductionPercent) / 100f,
            0f,
            0.9f);

        stats.moveSpeed = Mathf.Max(0.01f, stats.moveSpeed + moveSpeedBonus);
    }

    public string GetEffectText()
    {
        string text = "";
        Append(ref text, attackPowerBonus != 0, $"공격력 +{attackPowerBonus}");
        Append(ref text, !Mathf.Approximately(attackSpeedPercent, 0f), $"공격속도 +{attackSpeedPercent:0.#}%");
        Append(ref text, !Mathf.Approximately(criticalChancePercent, 0f), $"치명타 확률 +{criticalChancePercent:0.#}%");
        Append(ref text, !Mathf.Approximately(criticalDamagePercent, 0f), $"치명타 피해 +{criticalDamagePercent:0.#}%");
        Append(ref text, maxHpBonus != 0, $"최대 체력 +{maxHpBonus}");
        Append(ref text, !Mathf.Approximately(skillCooldownReductionPercent, 0f), $"스킬 쿨타임 -{skillCooldownReductionPercent:0.#}%");
        Append(ref text, !Mathf.Approximately(moveSpeedBonus, 0f), $"이동속도 +{moveSpeedBonus:0.##}");
        return string.IsNullOrWhiteSpace(text) ? "효과 없음" : text;
    }

    private static void Append(ref string text, bool shouldAppend, string line)
    {
        if (!shouldAppend)
            return;

        if (!string.IsNullOrEmpty(text))
            text += "\n";

        text += line;
    }
}
