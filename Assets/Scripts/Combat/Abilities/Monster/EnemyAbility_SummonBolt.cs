using UnityEngine;

/// <summary>Summon basic attack: fire a bullet at the nearest valid target.</summary>
public class EnemyAbility_SummonBolt : EnemyAbility
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 16f;
    public float projectileLifetime = 3f;
    // Canonical Sloth drone attack range.
    public float searchRange = 30f;
    public Vector3 muzzleOffset = new Vector3(0f, 0f, 0.4f);

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "木灵弹";
        if (cooldown <= 0f) cooldown = 0.5f;
        if (abilityTags == null) abilityTags = new System.Collections.Generic.List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Summon.Bolt", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Summon.Bolt");
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        return FindTargetDirection(out _);
    }

    protected override void OnTrigger()
    {
        if (owner == null || !FindTargetDirection(out Vector3 direction))
        {
            EndActivationEffect();
            return;
        }

        owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        Vector3 origin = owner.transform.position + owner.transform.TransformDirection(muzzleOffset);
        SpawnAbilityProjectile(projectilePrefab, origin, Quaternion.LookRotation(direction, Vector3.up), damage, projectileSpeed, projectileLifetime);
        EndActivationEffect();
    }

    private bool FindTargetDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (owner == null) return false;

        if (owner.isPossessed)
        {
            Enemy nearest = null;
            float best = searchRange;
            // 注册表遍历（替代 FindObjectsOfType 场景扫描；CanTrigger 内仅 O(n) 内存过滤）
            foreach (var candidate in EnemyRegistry.All)
            {
                if (candidate == null || !owner.CanDamage(candidate)) continue;
                float distance = Vector3.Distance(owner.transform.position, candidate.transform.position);
                if (distance >= best) continue;
                best = distance;
                nearest = candidate;
            }
            if (nearest == null) return false;
            direction = nearest.transform.position - owner.transform.position;
        }
        else
        {
            if (owner.targetPlayer == null) owner.RefreshPlayerTarget();
            if (owner.targetPlayer == null || !owner.CanDamageSoul()) return false;
            direction = owner.targetPlayer.position - owner.transform.position;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return false;
        direction.Normalize();
        return true;
    }
}
