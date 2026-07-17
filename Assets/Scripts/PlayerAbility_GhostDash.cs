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
        Vector3 dashDir = GetMouseAimDirection();
        // If there's a PlayerInputController with a current move direction, prefer that
        var input = owner.GetComponent<PlayerInputController>();
        if (input != null && input.moveDirection != Vector3.zero)
            dashDir = input.moveDirection;

        Vector3 newPos = owner.transform.position + dashDir * dashDistance;
        newPos.y = owner.transform.position.y;
        owner.transform.position = newPos;

        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemyObj in enemies)
        {
            float dist = Vector3.Distance(owner.transform.position, enemyObj.transform.position);
            if (dist > 0.5f) continue;
            var enemy = enemyObj.GetComponent<Enemy>();
            if (enemy != null)
                DealDamageToEnemy(enemy, damage);
        }
    }
}
