using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Optional designer-facing hitbox component. It can directly reference an Effect asset or resolve one or more
/// Effect Tags through a GameplayTagCatalog, allowing run-time cards to bind Effects without changing this prefab.
/// Damage remains owned by the ability/projectile.
/// </summary>
public class GameplayEffectApplier : MonoBehaviour
{
    [Tooltip("Optional direct Effect asset. Use Effect Tags for data-driven lookup instead.")]
    public GameplayEffectDefinition effect;
    [Tooltip("Optional Effect Tag directory. When empty, CardManager's configured catalog is used.")]
    public GameplayTagCatalog gameplayTagCatalog;
    [Tooltip("Effect Tags resolved and applied on hit. Example: Effect.Control.Stunned.")]
    public List<string> effectTags = new List<string>();
    [Tooltip("Additional source attack tags used by Effect source requirements.")]
    public List<string> sourceTags = new List<string>();
    [Tooltip("Apply once per target while this object exists. Disable for a persistent hazard that refreshes the effect.")]
    public bool applyOncePerTarget = true;
    public bool destroyAfterApply;

    private readonly HashSet<CombatAbilityComponent> appliedTargets = new HashSet<CombatAbilityComponent>();

    public void AddEffectTag(string effectTag)
    {
        string normalized = GameplayTagUtility.Normalize(effectTag);
        if (string.IsNullOrEmpty(normalized) || effectTags.Exists(value => string.Equals(value, normalized, System.StringComparison.OrdinalIgnoreCase))) return;
        effectTags.Add(normalized);
    }

    public bool ApplyTo(Component target)
    {
        if (target == null) return false;

        CombatAbilityComponent combat = target.GetComponentInParent<CombatAbilityComponent>();
        if (combat == null) return false;
        if (applyOncePerTarget && appliedTargets.Contains(combat)) return false;

        CombatAbilityComponent source = GetComponentInParent<CombatAbilityComponent>();
        bool applied = ApplyDefinition(combat, source, effect);
        foreach (string effectTag in effectTags)
        {
            GameplayEffectDefinition definition;
            if (!TryGetEffect(effectTag, out definition)) continue;
            applied |= ApplyDefinition(combat, source, definition);
        }

        if (!applied) return false;

        appliedTargets.Add(combat);
        if (destroyAfterApply) Destroy(gameObject);
        return true;
    }

    private bool ApplyDefinition(CombatAbilityComponent target, CombatAbilityComponent source, GameplayEffectDefinition definition)
    {
        if (definition == null) return false;

        string ignoredReason;
        return target.ApplyEffect(definition, source, sourceTags, out ignoredReason);
    }

    private bool TryGetEffect(string effectTag, out GameplayEffectDefinition definition)
    {
        if (gameplayTagCatalog != null && gameplayTagCatalog.TryGetEffect(effectTag, out definition)) return true;
        if (CardManager.Instance != null && CardManager.Instance.TryGetGameplayEffect(effectTag, out definition)) return true;

        definition = null;
        return false;
    }

    private void OnTriggerEnter(Collider other)
    {
        ApplyTo(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        ApplyTo(collision.collider);
    }
}
