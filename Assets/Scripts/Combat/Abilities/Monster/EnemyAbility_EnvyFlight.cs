using System.Collections;
using UnityEngine;

/// <summary>Envy mobility: grants the configured flying/untargetable Effect temporarily.</summary>
public class EnemyAbility_EnvyFlight : EnemyAbility
{
    public float flightDuration = 3f;
    public GameplayEffectDefinition flightEffect;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "飞行";
        cooldown = cooldown <= 0f ? 6f : cooldown;
    }

    protected override void OnTrigger()
    {
        StartCoroutine(FlightRoutine());
    }

    private IEnumerator FlightRoutine()
    {
        if (owner == null) yield break;
        if (flightEffect != null) owner.Combat.ApplyEffect(flightEffect, owner.Combat, abilityTags, out _);
        yield return AbilityWait(GetCardParameter("FlightDuration", flightDuration));
        if (owner != null && flightEffect != null) owner.Combat.RemoveEffect(flightEffect);
        EndActivationEffect();
    }
}
