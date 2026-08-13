using System.Collections;
using UnityEngine;

/// <summary>Pride mobility: charge along the current move direction and damage enemies along the path.
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
        if (owner == null)
        {
            CleanupChargeVfx();
            yield break;
        }

        // Charge along current move input (same as shared MobilityDash); fall back to facing.
        Vector3 direction = owner.transform.forward;
        if (owner.isPossessed && PlayerController.CurrentMoveDirection.sqrMagnitude > 0.0001f)
            direction = PlayerController.CurrentMoveDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = owner.transform.forward;
        direction.Normalize();

        // Keep slash/trail VFX glued to the body so it does not linger in world space.
        if (activeVfx != null)
            activeVfx.transform.SetParent(owner.transform, true);

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

        CleanupChargeVfx();
        EndActivationEffect();
    }

    private void CleanupChargeVfx()
    {
        if (activeVfx == null) return;
        Destroy(activeVfx);
        activeVfx = null;
    }

    protected override void OnDisable()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        CleanupChargeVfx();
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        CleanupChargeVfx();
        base.ResetForOwnerReuse();
    }
}
