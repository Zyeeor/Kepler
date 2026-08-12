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
/// A target-side immunity rule. When the target has every required target tag,
/// an incoming Effect with any matching blocked effect tag is rejected.
/// </summary>
[Serializable]
public class GameplayEffectBlockRule
{
    [Tooltip("The target must own all of these tags for this rule to apply. Example: State.Defense.SuperArmor.")]
    public List<string> requiredTargetTags = new List<string>();
    [Tooltip("Effect identity tags blocked by this rule. A parent tag such as Effect.Control blocks all control effects.")]
    public List<string> blockedEffectTags = new List<string>();
}

/// <summary>
/// Lightweight, data-driven combat effect. Effects have a stable identity Tag, grant state Tags,
/// optionally modify scalar values, and may be rejected by source/target Tag requirements.
/// </summary>
[CreateAssetMenu(fileName = "GameplayEffect", menuName = "Possession/Combat/Gameplay Effect")]
public class GameplayEffectDefinition : ScriptableObject
{
    [Header("Identity")]
    public string effectName = "New Effect";
    [Tooltip("Stable unique Effect Tag used for lookup and runtime binding. Example: Effect.Control.Stunned.")]
    public string effectTag;

    [Header("Lifetime")]
    [Tooltip("Duration in seconds. Values <= 0 are permanent until removed explicitly.")]
    public float duration = 1f;
    [Min(1)] public int maxStacks = 1;
    public GameplayEffectStackPolicy stackPolicy = GameplayEffectStackPolicy.RefreshDuration;

    [Header("Application Requirements")]
    [Tooltip("The target must own all listed tags before this Effect can apply. Empty means any target.")]
    public List<string> requiredTargetTags = new List<string>();
    [Tooltip("The Effect is rejected when the target owns any matching tag. Example: State.Immunity.Burning.")]
    public List<string> blockedTargetTags = new List<string>();
    [Tooltip("The source attack must own all listed tags before this Effect can apply. Empty means any source.")]
    public List<string> requiredSourceTags = new List<string>();
    [Tooltip("The Effect is rejected when the source attack owns any matching tag.")]
    public List<string> blockedSourceTags = new List<string>();

    [Header("Granted Tags")]
    [Tooltip("Example: State.Control.Stunned, State.Control.Rooted, State.Debuff.Burning. The Effect identity Tag is also active while applied.")]
    public List<string> grantedTags = new List<string>();

    [Header("Optional Numeric Modifiers")]
    public List<GameplayEffectModifier> modifiers = new List<GameplayEffectModifier>();

    [Header("Optional Periodic Trigger")]
    [Tooltip("Seconds between periodic callbacks. Values <= 0 disable periodic behavior.")]
    public float periodicInterval;

    [Header("Optional VFX")]
    [Tooltip("Spawned once while this effect is active. Leave empty when the effect has no persistent visual.")]
    public GameObject activeVfxPrefab;
    public bool parentVfxToTarget = true;
    [Tooltip("Played once when the Effect is successfully applied.")]
    public GameObject applyVfxPrefab;
    [Tooltip("Played once when the Effect expires or is removed.")]
    public GameObject expireVfxPrefab;
    [Min(0f)] public float oneShotVfxDuration = 2f;

    private void OnValidate()
    {
        maxStacks = Mathf.Max(1, maxStacks);
        effectTag = GameplayTagUtility.Normalize(effectTag);
        NormalizeTags(requiredTargetTags);
        NormalizeTags(blockedTargetTags);
        NormalizeTags(requiredSourceTags);
        NormalizeTags(blockedSourceTags);
        NormalizeTags(grantedTags);
    }

    private static void NormalizeTags(List<string> tags)
    {
        if (tags == null) return;

        for (int i = tags.Count - 1; i >= 0; i--)
        {
            string normalized = GameplayTagUtility.Normalize(tags[i]);
            if (string.IsNullOrEmpty(normalized)) tags.RemoveAt(i);
            else tags[i] = normalized;
        }
    }
}
