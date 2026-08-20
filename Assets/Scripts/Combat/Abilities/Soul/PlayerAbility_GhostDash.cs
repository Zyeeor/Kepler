using UnityEngine;

/// <summary>
/// Skill: Ghost Dash. Dash forward and damage enemies at the destination.
/// </summary>
public class PlayerAbility_GhostDash : PlayerAbility
{
    [Header("Dash")]
    public float dashDistance = 4f;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "Ghost Dash";
        cooldown = cooldown <= 0f ? 3f : cooldown;
    }

    protected override void OnTrigger()
    {
        // 冲刺方向统一为鼠标朝向（覆盖灵魂态与附身态）
        Vector3 dashDir = GetMouseAimDirection();

        Vector3 newPos = owner.transform.position + dashDir * dashDistance;
        newPos.y = owner.transform.position.y;
        owner.transform.position = newPos;

        foreach (var enemy in EnemyRegistry.All)
        {
            if (enemy == null) continue;
            float dist = Vector3.Distance(owner.transform.position, enemy.transform.position);
            if (dist > 0.5f) continue;
            DealDamageToEnemy(enemy, damage);
        }
    }
}
