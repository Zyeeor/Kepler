using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>为技能图标和已拥有卡片提供统一的鼠标悬浮效果提示。</summary>
public sealed class GameplayTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    PossessionImprintTooltip tooltip;
    EnemyAbility enemyAbility;
    PlayerAbility playerAbility;
    CardData card;
    string monsterName;
    string monsterDescription;

    void Awake()
    {
        // 需要一个 raycast target 接收 IPointerEnter/Exit：复用已有 Image（不破坏其 color，仅打开 raycastTarget），
        // 没有 Image 时才新增并用 Color.clear。仅当 GameObject 上没有 Image 时才清色，避免覆盖主图标颜色。
        Image hitArea = GetComponent<Image>();
        if (hitArea == null)
        {
            hitArea = gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
        }
        hitArea.raycastTarget = true;
    }

    public void SetTooltip(PossessionImprintTooltip value)
    {
        tooltip = value;
    }

    public void BindAbility(EnemyAbility value)
    {
        enemyAbility = value;
        playerAbility = null;
        card = null;
    }

    public void BindAbility(PlayerAbility value)
    {
        playerAbility = value;
        enemyAbility = null;
        card = null;
    }

    public void BindCard(CardData value)
    {
        card = value;
        enemyAbility = null;
        playerAbility = null;
        monsterName = null;
        monsterDescription = null;
    }

    /// <summary>绑定怪物身份（enemy icon 用）：悬浮时显示怪物显示名与描述。</summary>
    public void BindMonster(string name, string description)
    {
        monsterName = name;
        monsterDescription = description;
        enemyAbility = null;
        playerAbility = null;
        card = null;
    }

    public void ClearBinding()
    {
        enemyAbility = null;
        playerAbility = null;
        card = null;
        monsterName = null;
        monsterDescription = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        PossessionImprintTooltip resolvedTooltip = ResolveTooltip();
        if (resolvedTooltip == null) return;

        if (card != null)
        {
            resolvedTooltip.Show(card.ResolveCardName(), card.ResolveDescription() ?? string.Empty);
            return;
        }

        if (enemyAbility != null)
        {
            resolvedTooltip.Show(enemyAbility.abilityName, BuildAbilityDescription(enemyAbility));
            return;
        }

        if (playerAbility != null)
            resolvedTooltip.Show(playerAbility.abilityName, BuildAbilityDescription(playerAbility));

        if (!string.IsNullOrEmpty(monsterName))
            resolvedTooltip.Show(monsterName, monsterDescription ?? string.Empty);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        PossessionImprintTooltip resolvedTooltip = ResolveTooltip();
        if (resolvedTooltip != null) resolvedTooltip.Hide();
    }

    PossessionImprintTooltip ResolveTooltip()
    {
        if (tooltip == null) tooltip = FindObjectOfType<PossessionImprintTooltip>(true);
        return tooltip;
    }

    static string BuildAbilityDescription(EnemyAbility ability)
    {
        if (!string.IsNullOrWhiteSpace(ability.abilityDescription))
            return AppendUnlockedCards(ability.abilityDescription, ability);

        var lines = new List<string>();
        if (ability.damage > 0f)
            lines.Add("伤害：" + FormatNumber(ability.damage));
        if (ability.cooldown > 0f)
            lines.Add("冷却：" + FormatNumber(ability.cooldown) + " 秒");
        if (ability.requiredTags != null && ability.requiredTags.Count > 0)
            lines.Add("需要状态：" + string.Join("、", ability.requiredTags));

        if (lines.Count == 0)
            lines.Add("施放该技能以触发其战斗效果。");
        return AppendUnlockedCards(string.Join("\n", lines), ability);
    }

    static string BuildAbilityDescription(PlayerAbility ability)
    {
        if (!string.IsNullOrWhiteSpace(ability.abilityDescription))
            return ability.abilityDescription;

        var lines = new List<string>();
        if (ability.damage > 0f)
            lines.Add("伤害：" + FormatNumber(ability.damage));
        if (ability.cooldown > 0f)
            lines.Add("冷却：" + FormatNumber(ability.cooldown) + " 秒");
        if (lines.Count == 0)
            lines.Add("施放该技能以触发其战斗效果。");
        return string.Join("\n", lines);
    }

    static string AppendUnlockedCards(string description, EnemyAbility ability)
    {
        if (CardManager.Instance == null || ability.upgrades == null) return description;

        var upgrades = new List<string>();
        foreach (EnemyAbility.UpgradeSlot slot in ability.upgrades)
        {
            if (slot == null || !slot.unlocked || string.IsNullOrEmpty(slot.effectId)) continue;
            CardData data = CardManager.Instance.FindCard(slot.effectId);
            if (data == null) continue;
            string cardDescription = data.ResolveDescription();
            if (!string.IsNullOrWhiteSpace(cardDescription)) upgrades.Add(cardDescription);
        }

        if (upgrades.Count == 0) return description;
        return description + "\n\n已拥有强化：\n" + string.Join("\n", upgrades);
    }

    static string FormatNumber(float value)
    {
        return value.ToString("0.##");
    }
}
