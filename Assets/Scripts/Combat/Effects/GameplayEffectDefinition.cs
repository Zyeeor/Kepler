using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameplayEffectStackPolicy
{
    RefreshDuration,
    AddStack,
    Replace
}

public enum GameplayEffectModifierType
{
    MoveSpeedMultiplier,
    OutgoingDamageMultiplier,
    IncomingDamageMultiplier
}

[Serializable]
public class GameplayEffectModifier
{
    public GameplayEffectModifierType type;
    [Tooltip("Multiplier per stack. 0.5 means half value; 1.2 means 20% more value.")]
    [Min(0f)] public float multiplier = 1f;
}

/// <summary>
/// Lightweight, data-driven combat effect. Effects grant Tags and optional scalar modifiers.
/// Ability gating remains tag-driven: configure an ability's required/blocked tags to react to this effect.
/// </summary>
[CreateAssetMenu(fileName = "GameplayEffect", menuName = "Possession/Combat/Gameplay Effect")]
public class GameplayEffectDefinition : ScriptableObject
{
    [Header("Identity")]
    public string effectName = "New Effect";

    [Header("Lifetime")]
    [Tooltip("Duration in seconds. Values <= 0 are permanent until removed explicitly.")]
    public float duration = 1f;
    [Min(1)] public int maxStacks = 1;
    public GameplayEffectStackPolicy stackPolicy = GameplayEffectStackPolicy.RefreshDuration;

    [Header("Granted Tags")]
    [Tooltip("Example: State.Control.Stunned, State.Control.Rooted, State.Debuff.Burning.")]
    public List<string> grantedTags = new List<string>();

    [Header("Optional Numeric Modifiers")]
    public List<GameplayEffectModifier> modifiers = new List<GameplayEffectModifier>();

    [Header("Optional VFX")]
    [Tooltip("Spawned once while this effect is active. Leave empty when the effect has no persistent visual.")]
    public GameObject activeVfxPrefab;
    public bool parentVfxToTarget = true;

    private void OnValidate()
    {
        maxStacks = Mathf.Max(1, maxStacks);
        for (int i = grantedTags.Count - 1; i >= 0; i--)
        {
            string normalized = GameplayTagUtility.Normalize(grantedTags[i]);
            if (string.IsNullOrEmpty(normalized)) grantedTags.RemoveAt(i);
            else grantedTags[i] = normalized;
        }
    }
}
