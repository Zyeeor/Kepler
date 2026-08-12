using System.Collections;
using UnityEngine;

/// <summary>Wrath mobility: pull the owner forward in the current mouse aim direction.</summary>
public class EnemyAbility_WrathSelfGrapple : EnemyAbility
{
    public float grappleDistance = 6f;
    public float grappleSpeed = 24f;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "钩链位移";
        cooldown = cooldown <= 0f ? 4f : cooldown;
    }

    protected override void OnTrigger()
    {
        StartCoroutine(GrappleRoutine());
    }

    private IEnumerator GrappleRoutine()
    {
        if (owner == null) yield break;
        Vector3 direction = owner.transform.forward;
        if (owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aimDirection)) direction = aimDirection;
        float distance = GetCardParameter("GrappleDistance", grappleDistance);
        float moved = 0f;
        while (owner != null && moved < distance)
        {
            float step = Mathf.Min(grappleSpeed * AbilityDeltaTime, distance - moved);
            owner.transform.position += direction * step;
            moved += step;
            yield return null;
        }
        EndActivationEffect();
    }
}
