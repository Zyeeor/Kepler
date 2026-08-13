using UnityEngine;

/// <summary>
/// 怪物行为树黑板：节点间共享的决策数据与指令输出缓冲。
/// ControlCommand 是 struct（ref 参数）无法存引用，动作节点先写入本黑板，
/// 由 AIController.Tick 结束时一次性写回 ControlCommand。
/// </summary>
public class BTBlackboard
{
    public MonsterActor Host;
    public ActorContext Ctx;

    // ── 输入快照（AIController.Tick 每次刷新） ──
    public float DistToPlayer;
    public Vector3 TowardPlayerDir;
    public bool PlayerInDetectRange;
    /// <summary>玩家在任一攻击范围内（普攻或技能），用于攻击分支守卫。</summary>
    public bool PlayerInAttackRange;
    /// <summary>玩家在普攻范围内（basicAttackRange）。</summary>
    public bool PlayerInBasicRange;
    /// <summary>玩家在技能范围内（skillAttackRange）。</summary>
    public bool PlayerInSkillRange;
    public bool BasicReady;
    public bool SkillReady;
    /// <summary>位移技能冷却就绪（mobilityAbilities 中至少一个 CanTrigger）。</summary>
    public bool MobilityReady;

    // ── 输出（动作节点写入） ──
    /// <summary>本决策节拍是否要移动。</summary>
    public bool WantMove;
    /// <summary>移动方向（已含随机变速乘数，非归一化）。</summary>
    public Vector3 MoveDir;
    /// <summary>攻击脉冲位（Tick 结束写回 cmd 后清空，避免下一帧重复触发）。</summary>
    public CommandButtons Pressed;

    // ── 追击走位状态（BTAction_MoveToPlayer 内部使用） ──
    /// <summary>侧移方向：-1 / 0 / +1（0=直线追击）。</summary>
    public float StrafeDir;
    /// <summary>追击速度随机乘数。</summary>
    public float SpeedMul = 1f;
    /// <summary>走位刷新倒计时。</summary>
    public float StrafeTimer;

    // ── 追击时长 / 对峙状态 ──
    /// <summary>本次连续追击已累计时长（秒）。进入攻击范围或脱离索敌时由 AIController 清零。</summary>
    public float ChaseElapsed;
    /// <summary>当前移动模式是否为对峙（追击超时后的随机角度游走）。每决策节拍由动作节点重新声明。</summary>
    public bool StandoffMove;
    /// <summary>对峙移动方向刷新倒计时。</summary>
    public float StandoffTimer;

    public BTBlackboard(MonsterActor host)
    {
        Host = host;
    }
}
