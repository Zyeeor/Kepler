using System.Collections;
using UnityEngine;

/// <summary>
/// Shared possessed-monster mobility: dash in the current movement direction.
/// Added at runtime by MonsterActor so every monster receives the same Space input ability.
/// </summary>
public class EnemyAbility_MobilityDash : EnemyAbility
{
    [Header("Dash")]
    public float dashDistance = 4f;
    public float dashSpeed = 24f;
    public float collisionRadius = 0.4f;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "冲刺";
        cooldown = cooldown <= 0f ? 3f : cooldown;
    }

    protected override void OnTrigger()
    {
        if (owner != null) StartCoroutine(DashRoutine());
    }

    private IEnumerator DashRoutine()
    {
        Vector3 direction = owner.transform.forward;
        // AI 态（未被附身）：朝索敌目标冲刺，避免被玩家输入方向污染；
        // 玩家态：朝当前移动输入方向冲刺。
        if (owner is MonsterActor monster && !monster.IsPlayerControlled && monster.targetPlayer != null)
        {
            Vector3 toTarget = monster.targetPlayer.position - monster.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f) direction = toTarget;
        }
        else if (PlayerController.CurrentMoveDirection.sqrMagnitude > 0.0001f)
        {
            direction = PlayerController.CurrentMoveDirection;
        }
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) yield break;

        direction.Normalize();
        owner.IsAbilityFacingLocked = true;
        owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        float travelled = 0f;
        float effectiveCollisionRadius = ScaleAbilityRadius(collisionRadius);
        int obstacleMask = ~((1 << 8) | (1 << 9));
        while (owner != null && travelled < dashDistance)
        {
            float step = Mathf.Min(dashSpeed * AbilityDeltaTime, dashDistance - travelled);
            Vector3 castOrigin = owner.transform.position + Vector3.up * 0.75f;
            if (Physics.SphereCast(castOrigin, effectiveCollisionRadius, direction, out RaycastHit hit, step, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                step = Mathf.Max(0f, hit.distance - 0.05f);
                if (step <= 0f) break;
            }

            owner.transform.position += direction * step;
            travelled += step;
            yield return null;
        }

        if (owner != null) owner.IsAbilityFacingLocked = false;
    }

    protected override void OnDisable()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        StopAllCoroutines(); // 终止尸体消失/池回收时仍在滑尾的冲刺协程
        base.ResetForOwnerReuse();
    }
}
