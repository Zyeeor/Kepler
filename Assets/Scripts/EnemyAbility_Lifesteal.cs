using UnityEngine;

/// <summary>
/// Passive: gain X% lifesteal on every damage dealt. Triggers a red orbiting VFX when active.
/// </summary>
public class EnemyAbility_Lifesteal : EnemyAbility
{
    [Header("Lifesteal")]
    [Range(0f, 1f)] public float lifestealPercent = 0.02f; // 2%

    private void OnEnable()
    {
        type = AbilityType.Passive;
        abilityName = "Lifesteal " + (lifestealPercent * 100f) + "%";
    }

    /// <summary>Called by Enemy when it deals damage to a target. Returns amount healed.</summary>
    public float OnOwnerDealtDamage(float damageDealt)
    {
        if (!enabled) return 0f;
        float heal = damageDealt * lifestealPercent;
        if (owner != null && heal > 0f) owner.Heal(heal);
        return heal;
    }

    protected override void OnTrigger()
    {
        // Passive: no manual trigger
    }

    public override bool CanTrigger() { return false; } // passive cannot be triggered
}
