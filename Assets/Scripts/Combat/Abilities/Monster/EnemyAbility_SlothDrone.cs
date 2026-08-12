using System.Collections;
using UnityEngine;

/// <summary>Sloth skill: summon timed wood spirits that periodically strike nearby enemies.</summary>
public class EnemyAbility_SlothDrone : EnemyAbility
{
    public GameObject dronePrefab;
    public int droneCount = 1;
    public float droneLifetime = 4f;
    public float attackInterval = 0.5f;
    public float attackRange = 8f;
    public float damageMultiplier = 1f;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "木灵";
        cooldown = cooldown <= 0f ? 5f : cooldown;
    }

    protected override void OnTrigger()
    {
        StartCoroutine(DroneRoutine());
    }

    private IEnumerator DroneRoutine()
    {
        if (owner == null) yield break;
        int count = Mathf.RoundToInt(GetCardParameter("SummonCount", droneCount));
        for (int i = 0; i < count; i++)
        {
            if (dronePrefab != null)
                SpawnVfxTracked(dronePrefab, owner.transform.position + Random.insideUnitSphere, Quaternion.identity, droneLifetime);
        }

        float elapsed = 0f;
        while (owner != null && elapsed < droneLifetime)
        {
            elapsed += AbilityDeltaTime;
            Enemy target = FindNearestTarget();
            if (target != null) DealDamageTo(target, damage * damageMultiplier);
            yield return AbilityWait(attackInterval);
        }
        EndActivationEffect();
    }

    private Enemy FindNearestTarget()
    {
        Enemy result = null;
        float nearest = attackRange;
        foreach (Enemy enemy in FindObjectsOfType<Enemy>())
        {
            if (owner == null || !owner.CanDamage(enemy)) continue;
            float distance = Vector3.Distance(owner.transform.position, enemy.transform.position);
            if (distance < nearest) { nearest = distance; result = enemy; }
        }
        return result;
    }
}
