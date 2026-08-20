using UnityEngine;

// ═══════════════════════════ 条件节点 ═══════════════════════════

/// <summary>玩家在索敌半径内。</summary>
public class BTCondition_InDetectRange : BTCondition
{
    protected override bool Check(BTBlackboard bb) => bb.PlayerInDetectRange;
}

/// <summary>玩家在任一攻击范围内（普攻或技能）。</summary>
public class BTCondition_InAttackRange : BTCondition
{
    protected override bool Check(BTBlackboard bb) => bb.PlayerInAttackRange;
}

/// <summary>玩家在普攻范围内。</summary>
public class BTCondition_InBasicRange : BTCondition
{
    protected override bool Check(BTBlackboard bb) => bb.PlayerInBasicRange;
}

/// <summary>玩家在技能范围内。</summary>
public class BTCondition_InSkillRange : BTCondition
{
    protected override bool Check(BTBlackboard bb) => bb.PlayerInSkillRange;
}

/// <summary>普攻冷却就绪。</summary>
public class BTCondition_BasicReady : BTCondition
{
    protected override bool Check(BTBlackboard bb) => bb.BasicReady;
}

/// <summary>技能尝试间隔就绪（触发成功才恢复，见 NotifySkillTriggered）。</summary>
public class BTCondition_SkillReady : BTCondition
{
    protected override bool Check(BTBlackboard bb) => bb.SkillReady;
}

/// <summary>位移技能（冲刺）冷却就绪。</summary>
public class BTCondition_MobilityReady : BTCondition
{
    protected override bool Check(BTBlackboard bb) => bb.MobilityReady;
}

/// <summary>
/// 追击时长已超限：连续直线追击超过 chaseDuration 仍未进入攻击范围（未追上玩家）。
/// chaseDuration = 0 时恒 false（不限时长，一直直线追击）。
/// </summary>
public class BTCondition_ChaseExpired : BTCondition
{
    protected override bool Check(BTBlackboard bb)
    {
        float limit = bb.Host.AiConfig.chaseDuration;
        return limit > 0f && bb.ChaseElapsed >= limit;
    }
}

// ═══════════════════════════ 动作节点 ═══════════════════════════

/// <summary>
/// 普攻：产出 Basic 脉冲。攻击节奏由技能自身 cooldown 决定（守卫已由 BasicReady=CanTrigger 保证），
/// 此处仅按攻击迟疑度 attackEagerness 做概率闸——CD 就绪后每决策拍仅有该概率真正出手；
/// 迟疑时返回 true 但不产出脉冲（攻击分支仍短路成功，怪原地等待而非回退追击），
/// 让 AI 不「CD 一好就无缝放」，下一决策拍重新随机。
/// </summary>
public class BTAction_BasicAttack : BTAction
{
    protected override bool Execute(BTBlackboard bb)
    {
        if (bb.Host.AiRandomValue() < bb.Host.attackEagerness)
            bb.Pressed |= CommandButtons.Basic;
        return true;
    }
}

/// <summary>
/// 技能：产出 Skill1 脉冲。攻击节奏由技能自身 cooldown 决定（守卫由 SkillReady=CanTrigger 保证），
/// 此处仅按攻击迟疑度 attackEagerness 做概率闸——CD 就绪后每决策拍仅有该概率真正出手；
/// 迟疑时返回 true 但不产出脉冲（攻击分支仍短路成功，怪原地等待而非回退追击）。
/// </summary>
public class BTAction_Skill : BTAction
{
    protected override bool Execute(BTBlackboard bb)
    {
        if (bb.Host.AiRandomValue() < bb.Host.attackEagerness)
            bb.Pressed |= CommandButtons.Skill1;
        return true;
    }
}

/// <summary>
/// 追击 + 随机走位：直线追击为主，间隔性随机侧移/变速，避免大量同类怪整齐列队。
/// 已到 aiMinRange 停步距离后只侧移不前进。
/// </summary>
public class BTAction_MoveToPlayer : BTAction
{
    protected override bool Execute(BTBlackboard bb)
    {
        RollStrafe(bb);
        ComputeMoveDir(bb);
        bb.WantMove = bb.MoveDir.sqrMagnitude > 0.0001f;
        return true;
    }

    /// <summary>决策点之间每帧刷新走位（节奏独立于决策节拍，行为更自然）。</summary>
    public void TickStrafe(BTBlackboard bb, float dt)
    {
        if (!bb.WantMove) return;
        bb.StrafeTimer -= dt;
        if (bb.StrafeTimer <= 0f)
        {
            RollStrafe(bb);
            ComputeMoveDir(bb);
            bb.WantMove = bb.MoveDir.sqrMagnitude > 0.0001f;
        }
    }

    /// <summary>随机走位参数：侧移方向（概率 strafeChance）+ 速度乘数 + 下次刷新间隔。</summary>
    void RollStrafe(BTBlackboard bb)
    {
        var host = bb.Host;
        bb.StrafeDir = host.AiRandomValue() < host.strafeChance ? (host.AiRandomValue() < 0.5f ? -1f : 1f) : 0f;
        bb.SpeedMul = host.AiRandomRange(host.moveSpeedJitterMin, host.moveSpeedJitterMax);
        bb.StrafeTimer = host.AiRandomRange(host.strafeIntervalMin, host.strafeIntervalMax);
    }

    /// <summary>由走位参数计算移动方向（含速度乘数，产出非归一化向量供 ExecuteMovement 变速）。</summary>
    void ComputeMoveDir(BTBlackboard bb)
    {
        var host = bb.Host;
        Vector3 toward = bb.TowardPlayerDir;
        Vector3 perp = new Vector3(-toward.z, 0f, toward.x);

        if (bb.DistToPlayer <= host.aiMinRange)
        {
            // 已到停步距离：只允许侧移，不前进
            bb.MoveDir = bb.StrafeDir != 0f
                ? (toward * (1f - host.strafeStrength) + perp * (bb.StrafeDir * host.strafeStrength)).normalized * bb.SpeedMul
                : Vector3.zero;
            return;
        }

        Vector3 dir = toward * (1f - host.strafeStrength * Mathf.Abs(bb.StrafeDir))
                    + perp * (bb.StrafeDir * host.strafeStrength);
        if (dir.sqrMagnitude < 0.0001f)
        {
            bb.MoveDir = Vector3.zero;
            return;
        }
        bb.MoveDir = dir.normalized * bb.SpeedMul;
    }
}

/// <summary>
/// 位移技能（冲刺）：产出 Mobility 脉冲，由 MonsterActor 触发 mobilityAbilities（冷却由能力自身管理）。
/// 普攻范围内（贴身）返回失败 → WeightedSelector 自动回退到普通追击，避免近身缠斗中贴身冲刺。
/// </summary>
public class BTAction_MobilityDash : BTAction
{
    protected override bool Execute(BTBlackboard bb)
    {
        if (bb.DistToPlayer <= bb.Host.basicAttackRange) return false;
        bb.Pressed |= CommandButtons.Mobility;
        return true;
    }
}

/// <summary>待机：不产出任何指令（选择器兜底分支，保证树总有一个成功结果）。</summary>
public class BTAction_Idle : BTAction
{
    protected override bool Execute(BTBlackboard bb) => true;
}

/// <summary>
/// 对峙：追击超时后不再直线扑向玩家，改为随机角度游走。
/// 移动方向 = 朝向玩家方向随机偏转 ±90°（始终保留朝向玩家的分量，不会背离玩家），
/// 速度复用追击速度抖动（moveSpeedJitterMin/Max），刷新节奏复用走位间隔（strafeIntervalMin/Max）。
/// </summary>
public class BTAction_Standoff : BTAction
{
    protected override bool Execute(BTBlackboard bb)
    {
        RollStandoffDir(bb);
        bb.StandoffMove = true;
        bb.WantMove = true;
        return true;
    }

    /// <summary>决策点之间按刷新间隔重随机对峙方向（独立于决策节拍，游走更自然）。</summary>
    public void TickStandoff(BTBlackboard bb, float dt)
    {
        if (!bb.WantMove) return;
        bb.StandoffTimer -= dt;
        if (bb.StandoffTimer <= 0f) RollStandoffDir(bb);
    }

    void RollStandoffDir(BTBlackboard bb)
    {
        var cfg = bb.Host.AiConfig;
        // 朝向玩家方向绕 Y 轴随机偏转 ±90°（不背离玩家）
        float a = bb.Host.AiRandomRange(-90f, 90f) * Mathf.Deg2Rad;
        Vector3 toward = bb.TowardPlayerDir;
        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
        Vector3 dir = new Vector3(toward.x * ca - toward.z * sa, 0f, toward.x * sa + toward.z * ca);

        bb.SpeedMul = bb.Host.AiRandomRange(cfg.moveSpeedJitterMin, cfg.moveSpeedJitterMax);
        bb.StandoffTimer = bb.Host.AiRandomRange(cfg.strafeIntervalMin, cfg.strafeIntervalMax);
        bb.MoveDir = dir * bb.SpeedMul;
    }
}
