using UnityEngine;

/// <summary>Gluttony skill: damage enemies in front and execute targets below a health threshold.</summary>
public class EnemyAbility_GluttonyDevour : EnemyAbility
{
    public float range = 3f;
    public float angle = 100f;
    public float damageMultiplier = 1f;
    [Range(0f, 1f)] public float executeHealthFraction = 0.2f;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "吞噬";
        cooldown = cooldown <= 0f ? 4f : cooldown;
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;
        float threshold = GetCardParameter("ExecuteThreshold", executeHealthFraction);
        foreach (Enemy enemy in FindEnemiesInArc(owner.transform.position, owner.transform.forward, range, angle))
        {
            DealDamageTo(enemy, enemy.currentHealth <= enemy.maxHealth * threshold
                ? enemy.currentHealth
                : damage * damageMultiplier);
        }
        EndActivationEffect();
    }
}
