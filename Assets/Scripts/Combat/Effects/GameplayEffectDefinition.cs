using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum GameplayEffectStackPolicy
{
    RefreshDuration,
    AddStack,
    Replace
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
/// Combat settlement payload applied on hit (melee or projectile).
/// Identity Tag + duration + granted Tags + target VFX. Damage stays on the Ability.
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

    [Header("Granted Tags")]
    [Tooltip("Example: State.Control.Stunned, State.Defense.Untargetable. The Effect identity Tag is also active while applied.")]
    public List<string> grantedTags = new List<string>();

    [Header("Optional Periodic Trigger")]
    [Tooltip("Seconds between periodic callbacks. Values <= 0 disable periodic behavior.")]
    public float periodicInterval;

    [Header("Target VFX")]
    [Tooltip("Spawned on the target while this effect is active. Used for persistent visuals such as afterimage.")]
    public GameObject activeVfxPrefab;
    public bool parentVfxToTarget = true;
    [Tooltip("Played on the target when this Effect is applied by a hit.")]
    public GameObject hitVfxPrefab;
    [FormerlySerializedAs("attackVfxDuration")]
    [Min(0f)] public float hitVfxDuration = 1f;

    private void OnValidate()
    {
        maxStacks = Mathf.Max(1, maxStacks);
        effectTag = GameplayTagUtility.Normalize(effectTag);
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
