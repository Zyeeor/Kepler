using UnityEngine;
using System;

/// <summary>
/// 卡牌 Offer 分类（需求来源：Encounter_CardOffer_Baseline_v1.0 §9 三槽结构）。
///   BasicUniversal / GlobalSlot → Slot A（Horizontal）
///   MonsterType / TypeGrowth（且类型已进入 Known Type Set）→ Slot B（Monster Type）
///   全部合法卡 → Slot C（Flex）
/// </summary>
public enum CardCategory
{
    [Tooltip("基础通用：作用于本 Run 所有普通 Enemy 与普通 Possessed Body（Slot A）。")]
    BasicUniversal = 0,
    [Tooltip("全局槽位质变：Run 级常驻规则改变（Slot A，受 Global 软保底加权）。")]
    GlobalSlot = 1,
    [Tooltip("怪物类型卡：对应 Sin 普通 Enemy 与对应 Possessed Body（Slot B，Known Type 限定）。")]
    MonsterType = 2,
    [Tooltip("类型成长卡：对应 Sin 整体形态成长（Slot B，Known Type 限定）。")]
    TypeGrowth = 3,
}

/// <summary>
/// 七罪类型（Known Type Set / 怪物类型卡归类 / Investment 统计的统一标识）。
/// </summary>
public enum SinType
{
    None = 0,      // 非怪物类型卡（BasicUniversal / GlobalSlot 等）
    Pride = 1,
    Sloth = 2,
    Gluttony = 3,
    Envy = 4,
    Wrath = 5,
    Greed = 6,
    Lust = 7,
}

/// <summary>
/// Single upgrade card data. Each card corresponds to an upgrade effect on a specific enemy ability.
/// </summary>
[Serializable]
public class CardData
{
    [Tooltip("Display name shown on the card.")]
    public string cardName;
    [Tooltip("Unique effect ID. Matches an AbilityUpgrade on an EnemyAbility prefab.")]
    public string effectId;
    [Tooltip("Card image / icon sprite (shown in CoreChoiceUI).")]
    public Sprite image;
    [Tooltip("Short description of what this upgrade does.")]
    [TextArea(2, 4)]
    public string description;
    [Tooltip("The EnemyAbility prefab that contains the matching AbilityUpgrade. Used as the legacy matching fallback when Target Ability Tags is empty.")]
    public EnemyAbility abilityPrefab;
    [Tooltip("Stable attack behavior Tags this card targets. When populated, they take precedence over abilityPrefab display-name matching.")]
    public System.Collections.Generic.List<string> targetAbilityTags = new System.Collections.Generic.List<string>();
    [Tooltip("Effect Tags dynamically bound to every matching attack for this run. Resolve these through CardManager's Gameplay Tag Catalog.")]
    public System.Collections.Generic.List<string> grantedEffectTags = new System.Collections.Generic.List<string>();
    [Tooltip("Optional numeric overrides read by the targeted ability when this card is unlocked.")]
    public System.Collections.Generic.List<CardAbilityParameter> abilityParameters = new System.Collections.Generic.List<CardAbilityParameter>();

    [Header("Offer 分类（Encounter_CardOffer_Baseline）")]
    [Tooltip("卡牌分类，决定其进入哪个 Offer 槽位（Slot A/B/C）。")]
    public CardCategory category = CardCategory.BasicUniversal;
    [Tooltip("怪物类型卡归属的 Sin（仅 MonsterType / TypeGrowth 有意义；Slot B 要求该类型已进入 Known Type Set）。")]
    public SinType monsterType = SinType.None;

    [Header("Card Layers（UI 多层素材，可扩展为并列多张）")]
    [Tooltip("前景层（foreground）基础素材；null = 使用卡 prefab 默认素材。")]
    public Sprite foregroundSprite;
    [Tooltip("前景层（foreground）额外并列素材（叠在 foregroundSprite 之上，列表索引越大越靠上）。")]
    public System.Collections.Generic.List<Sprite> extraForegroundSprites = new System.Collections.Generic.List<Sprite>();
    [Tooltip("中景层（middleground）基础素材；null = 使用卡 prefab 默认素材。")]
    public Sprite middlegroundSprite;
    [Tooltip("中景层（middleground）额外并列素材（叠在 middlegroundSprite 之上，列表索引越大越靠上）。")]
    public System.Collections.Generic.List<Sprite> extraMiddlegroundSprites = new System.Collections.Generic.List<Sprite>();
    [Tooltip("背景层（background）基础素材；null = 使用卡 prefab 默认素材。")]
    public Sprite backgroundSprite;
    [Tooltip("背景层（background）额外并列素材（叠在 backgroundSprite 之上，列表索引越大越靠上）。")]
    public System.Collections.Generic.List<Sprite> extraBackgroundSprites = new System.Collections.Generic.List<Sprite>();
    [Tooltip("边框层（border）基础素材；null = 使用卡 prefab 默认素材。")]
    public Sprite borderSprite;
    [Tooltip("边框层（border）额外并列素材（叠在 borderSprite 之上，列表索引越大越靠上）。")]
    public System.Collections.Generic.List<Sprite> extraBorderSprites = new System.Collections.Generic.List<Sprite>();
}

[Serializable]
public class CardAbilityParameter
{
    [Tooltip("Stable key understood by the targeted ability, for example ExtraProjectiles.")]
    public string key;
    public float value;
}
