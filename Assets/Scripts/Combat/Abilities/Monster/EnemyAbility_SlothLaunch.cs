using System.Collections;
using UnityEngine;

/// <summary>Sloth mobility: a brief wind-up followed by a ground launch and landing explosion.</summary>
public class EnemyAbility_SlothLaunch : EnemyAbility
{
    public float windupDuration = 0.25f;
    public float launchDistance = 5f;
    public float launchSpeed = 20f;
    public float landingRadius = 2f;
    public float damageMultiplier = 1f;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "弹射起步";
        cooldown = cooldown <= 0f ? 4f : cooldown;
    }

    protected override void OnTrigger()
    {
        StartCoroutine(LaunchRoutine());
    }

    private IEnumerator LaunchRoutine()
    {
        if (owner == null) yield break;
        yield return AbilityWait(windupDuration);
        Vector3 direction = owner.transform.forward;
        if (owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aimDirection)) direction = aimDirection;
        float distance = GetCardParameter("LaunchDistance", launchDistance);
        float moved = 0f;
        while (owner != null && moved < distance)
        {
            float step = Mathf.Min(launchSpeed * AbilityDeltaTime, distance - moved);
            owner.transform.position += direction * step;
            moved += step;
            yield return null;
        }
        if (owner != null) DamageEnemiesInSphere(owner.transform.position, landingRadius, damage * damageMultiplier);
        EndActivationEffect();
    }
}
