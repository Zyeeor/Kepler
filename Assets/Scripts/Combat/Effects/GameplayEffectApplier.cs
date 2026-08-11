using UnityEngine;

/// <summary>
/// Optional designer-facing hitbox component. Attach it to a trigger or projectile to apply a configured
/// Gameplay Effect to the first combat actor it touches. Damage remains owned by the ability/projectile.
/// </summary>
public class GameplayEffectApplier : MonoBehaviour
{
    public GameplayEffectDefinition effect;
    [Tooltip("Apply once per target while this object exists. Disable for a persistent hazard that refreshes the effect.")]
    public bool applyOncePerTarget = true;
    public bool destroyAfterApply;

    private readonly System.Collections.Generic.HashSet<CombatAbilityComponent> appliedTargets = new System.Collections.Generic.HashSet<CombatAbilityComponent>();

    public bool ApplyTo(Component target)
    {
        if (effect == null || target == null) return false;

        CombatAbilityComponent combat = target.GetComponentInParent<CombatAbilityComponent>();
        if (combat == null) return false;
        if (applyOncePerTarget && appliedTargets.Contains(combat)) return false;

        combat.ApplyEffect(effect);
        appliedTargets.Add(combat);
        if (destroyAfterApply) Destroy(gameObject);
        return true;
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
