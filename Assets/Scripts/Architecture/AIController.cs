using UnityEngine;

/// <summary>
/// AI 控制：通过 IController.Tick 产出统一 ControlCommand（MoveDirection + Basic/Skill1 按钮位），
/// 由 MonsterActor 的 Actor.Update 流程消费（Controller.Tick → ExecuteButtons → ExecuteMovement）。
/// 守卫语义：
///   - isPossessed（已被玩家附身）→ 不产出任何指令
///   - isDowned / isWeakened → 不产出任何指令
///   - 玩家超出 detectionRadius → 空指令（原地停留，不转向）
///   - dist ∈ (aiMinRange, detectionRadius] → 朝玩家移动
///   - aiTimer/skillTimer：skill 触发成功才重置（见 NotifySkillTriggered）
/// </summary>
public class AIController : MonoBehaviour, IController
{
    private MonsterActor host;
    private float aiTimer;
    private float skillTimer;

    /// <summary>运行时自动挂载（CreateDefaultController 兜底 AddComponent）；Prefab 上无序列化需求。</summary>
    void Awake()
    {
        host = GetComponent<MonsterActor>();
    }

    public void OnAttached(Actor owner)
    {
        host = owner as MonsterActor;
        aiTimer = 0f;
        skillTimer = 0f;
    }

    public void OnDetached()
    {
        host = null;
    }

    public void Tick(in ActorContext ctx, ref ControlCommand cmd)
    {
        cmd = ControlCommand.Empty;
        if (host == null) return;
        if (host.isPossessed) return;
        if (host.isDowned || host.isWeakened) return;

        if (host.targetPlayer == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            host.targetPlayer = p != null ? p.transform : null;
        }
        if (host.targetPlayer == null) return;

        Vector3 dir = host.targetPlayer.position - host.transform.position;
        dir.y = 0f;
        float dist = dir.magnitude;
        host.playerDetected = dist <= host.detectionRadius;
        if (!host.playerDetected || dist <= 0.01f) return;

        if (dist > host.aiMinRange)
        {
            cmd.HasMove = true;
            cmd.MoveDirection = dir.normalized;
        }

        // 攻击/技能均需在 aiAttackRange 内（超出范围不产出按钮位，timer 挂起等待进入范围）
        aiTimer -= ctx.DeltaTime;
        if (aiTimer <= 0f && dist <= host.aiAttackRange)
        {
            cmd.Pressed |= CommandButtons.Basic;
            aiTimer = host.basicCastTime / host.attackSpeed;
        }

        skillTimer -= ctx.DeltaTime;
        if (skillTimer <= 0f && dist <= host.aiAttackRange)
        {
            cmd.Pressed |= CommandButtons.Skill1;
            // 注意：不在此重置 skillTimer —— 技能触发成功才重置，
            // 由 MonsterActor.ExecuteButtons 在 TryTrigger 成功时回调 NotifySkillTriggered 保持等价
        }
    }

    /// <summary>技能触发成功回调（TryTrigger 成功才重置 skillTimer）。</summary>
    public void NotifySkillTriggered()
    {
        if (host != null) skillTimer = host.skillCastTime / host.attackSpeed;
    }
}
