using System;
using System.Collections.Generic;
using UnityEngine;

public enum ShopItemTier
{
    Low,
    Mid,
    High
}

[Serializable]
public class ShopItemData
{
    [Serializable]
    public class RecipeMaterial
    {
        public ShopItemData item;
        public int count = 1;
    }

    [Header("Info")]
    public string id;
    public string itemName;
    [TextArea] public string description;
    public ShopItemTier tier;
    public int price = 100;
    public bool canBuyMultiple = true;
    public Sprite iconSprite;
    public Color iconColor = Color.gray;

    [Header("Recipe")]
    public List<RecipeMaterial> recipeMaterials = new List<RecipeMaterial>();
    [TextArea] public string recipeText;

    [Header("Stat Effects")]
    [Tooltip("공격속도 증가율입니다. 15를 입력하면 15% 증가합니다.")]
    [Min(0f)] public float attackSpeedPercent;
    public int attackPowerBonus;
    [Tooltip("치명타 확률 증가율입니다. 10을 입력하면 치명타 확률이 10%p 증가합니다.")]
    [Min(0f)] public float criticalChancePercent;
    public int maxHpBonus;
    [Tooltip("스킬 쿨타임 감소율입니다. 20을 입력하면 스킬 쿨타임이 20% 감소합니다.")]
    [Min(0f)] public float skillCooldownReductionPercent;

    public string GetEffectText()
    {
        string text = "";

        AppendEffect(ref text, !Mathf.Approximately(attackSpeedPercent, 0f), $"Attack Speed +{attackSpeedPercent:0.#}%");
        AppendEffect(ref text, attackPowerBonus != 0, $"Attack +{attackPowerBonus}");
        AppendEffect(ref text, !Mathf.Approximately(criticalChancePercent, 0f), $"Crit Chance +{criticalChancePercent:0.#}%");
        AppendEffect(ref text, maxHpBonus != 0, $"Max HP +{maxHpBonus}");
        AppendEffect(ref text, !Mathf.Approximately(skillCooldownReductionPercent, 0f), $"Skill Cooldown -{skillCooldownReductionPercent:0.#}%");

        return string.IsNullOrWhiteSpace(text) ? "No effect" : text;
    }

    private static void AppendEffect(ref string text, bool shouldAppend, string effect)
    {
        if (!shouldAppend)
            return;

        if (!string.IsNullOrEmpty(text))
            text += "\n";

        text += effect;
    }
}
