using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gluttony skill: forward heavy bite. On unit hit → Overfed (Q4: hit only).
/// Cancels SmallCat on start. GL-S01 first-hit heal, GL-S02 execute, GL-S03 swallow small projectiles.
/// </summary>
public class EnemyAbility_GluttonyDevour : EnemyAbility
{
    public float range = 3f;
    public float angle = 100f;
    public float damageAmount = 40f;
    [Range(0f, 1f)] public float executeHealthFraction = 0.2f;
    public float firstDevourHeal = 20f;
    public float projectileSwallowRadius = 2.5f;
    public float maxSwallowProjectileDamage = 25f;

    private GluttonyBodyState _state;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "吞噬";
        cooldown = cooldown <= 0f ? 1f : cooldown;
        if (damage <= 0f) damage = damageAmount;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Gluttony.Devour", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Gluttony.Devour");
    }

    protected override void OnTrigger()
    {
        if (owner == null)
        {
            EndActivationEffect();
            return;
        }

        CacheOwnerState();
        _state?.CancelSmallCat();

        float dmg = damage > 0f ? damage : damageAmount;
        bool hitUnit = false;

        foreach (Enemy enemy in FindEnemiesInArc(owner.transform.position, owner.transform.forward, range, angle))
        {
            hitUnit = true;
            float amount = dmg;
            if (IsUpgradeUnlocked("GL-S02"))
            {
                float threshold = GetCardParameter("ExecuteThreshold", executeHealthFraction);
                if (enemy.maxHealth > 0f && enemy.currentHealth / enemy.maxHealth <= threshold)
                    amount = enemy.currentHealth;
            }
            DealDamageTo(enemy, amount);
        }

        if (owner.CanDamageSoul())
        {
            foreach (var ph in Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
            {
                if (ph == null) continue;
                Vector3 to = ph.transform.position - owner.transform.position;
                to.y = 0f;
                if (to.magnitude > range) continue;
                if (Vector3.Angle(owner.transform.forward, to) > angle * 0.5f) continue;
                hitUnit = true;
                DealDamageToPlayer(ph, dmg);
            }
        }

        bool swallowedProjectile = false;
        if (IsUpgradeUnlocked("GL-S03"))
            swallowedProjectile = TrySwallowProjectile();

        if (hitUnit || swallowedProjectile)
        {
            _state?.GrantOverfed();
            if (hitUnit && IsUpgradeUnlocked("GL-S01") && _state != null && !_state.FirstDevourHealUsed)
            {
                float heal = GetCardParameter("FirstDevourHeal", firstDevourHeal);
                owner.Heal(heal);
                _state.MarkFirstDevourHealUsed();
            }
        }

        EndActivationEffect();
    }

    private bool TrySwallowProjectile()
    {
        Collider[] hits = Physics.OverlapSphere(owner.transform.position + owner.transform.forward * (range * 0.5f),
            projectileSwallowRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            var projectile = hit.GetComponentInParent<Projectile>();
            if (projectile == null) continue;
            // Base forbids flight swallow; card allows small frontal projectiles only.
            if (projectile.isPlayerProjectile == owner.isPossessed) continue;
            if (projectile.damage > maxSwallowProjectileDamage) continue;

            Vector3 to = projectile.transform.position - owner.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f && Vector3.Angle(owner.transform.forward, to) > angle * 0.5f)
                continue;

            Destroy(projectile.gameObject);
            return true;
        }
        return false;
    }

    private void CacheOwnerState()
    {
        if (owner == null) return;
        _state = owner.GetComponent<GluttonyBodyState>();
        if (_state == null) _state = owner.gameObject.AddComponent<GluttonyBodyState>();
    }
}
