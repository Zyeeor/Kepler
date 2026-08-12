using UnityEngine;
using System;

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
}

[Serializable]
public class CardAbilityParameter
{
    [Tooltip("Stable key understood by the targeted ability, for example ExtraProjectiles.")]
    public string key;
    public float value;
}
