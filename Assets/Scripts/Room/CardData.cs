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
    [Tooltip("The EnemyAbility prefab that contains the matching AbilityUpgrade. Used to find & unlock the effect.")]
    public EnemyAbility abilityPrefab;
}
