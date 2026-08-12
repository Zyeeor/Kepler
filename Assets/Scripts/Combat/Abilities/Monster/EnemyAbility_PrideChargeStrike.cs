using System.Collections;
using UnityEngine;

/// <summary>Pride mobility: charge forward and damage enemies along the travelled path.
/// Build Pride.ChargeEmpowered increases ChargeDistance; damage scales 0~100% with the distance bonus.
/// </summary>
public class EnemyAbility_PrideChargeStrike : EnemyAbility
{
    public float chargeDistance = 5f;
    public float chargeSpeed = 24f;
    public float hitRadius = 0.8f;
    public float landingRadius = 1.5f;
    public float damageMultiplier = 1f;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "一刀斩";
        cooldown = cooldown <= 0f ? 2f : cooldown;
        if (abilityTags == null) abilityTags = new System.Collections.Generic.List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Pride.ChargeStrike", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Pride.ChargeStrike");
    }

    protected override void OnTrigger()
    {
        StartCoroutine(ChargeRoutine());
    }

    private IEnumerator ChargeRoutine()
    {
        if (owner == null) yield break;
        Vector3 direction = owner.transform.forward;
        if (owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aimDirection)) direction = aimDirection;

        float baseDistance = Mathf.Max(0.01f, chargeDistance);
        float distance = GetCardParameter("ChargeDistance", chargeDistance);
        float distanceBonus = Mathf.Clamp01((distance - baseDistance) / baseDistance); // 0~100%
        float strikeDamage = damage * damageMultiplier * (1f + distanceBonus);

        float moved = 0f;
        owner.IsAbilityFacingLocked = true;
        owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        while (owner != null && moved < distance)
        {
            float step = Mathf.Min(chargeSpeed * AbilityDeltaTime, distance - moved);
            Vector3 next = owner.transform.position + direction * step;
            DamageEnemiesAlongPath(owner.transform.position, next, hitRadius, strikeDamage);
            owner.transform.position = next;
            moved += step;
            yield return null;
        }
        if (owner != null)
        {
            DamageEnemiesInSphere(owner.transform.position, landingRadius, strikeDamage);
            owner.IsAbilityFacingLocked = false;
        }
        EndActivationEffect();
    }

    protected override void OnDisable()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        base.OnDisable();
    }
}
