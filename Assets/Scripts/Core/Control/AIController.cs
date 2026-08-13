using UnityEngine;

/// <summary>
/// 怪物 AI：行为树驱动（决策节拍 + 随机化）。
/// 每只怪 spawn 时随机化决策相位，决策间隔在 [decisionIntervalMin, Max] 内随机抖动，
/// 从根重新评估行为树，选中行为在下一决策点前保持——大量同类怪的行为天然错开。
/// 树结构（AIController.BuildTree）：
///   Selector
///   ├─ [任一攻击范围内] → WeightedSelector{
///   │      Skill(权重 skillPriority, 带 InSkillRange 守卫),
///   │      Basic(权重 1-skillPriority, 带 InBasicRange 守卫) }  ← 互斥选择，范围不满足自动回退
///   ├─ [索敌范围内] → Selector{
///   │      [追击超时 ChaseExpired] → Standoff(对峙：随机角度游走、不背离玩家),
///   │      否则 → WeightedSelector{ Mobility(冲刺), MoveToPlayer(追击走位) } }
///   └─ Idle（兜底）
/// 攻击范围已拆分：basicAttackRange（普攻/近战）与 skillAttackRange（技能/远程），两者独立。
/// 追击时长限制：连续追击超过 chaseDuration 仍未进入攻击范围 → 转入对峙；0 = 不限时长。
/// 守卫语义：
///   - isPossessed（被玩家附身）→ 不产出任何指令
///   - isDowned / isWeakened → 不产出任何指令
///   - 玩家超出 detectionRadius → 待机（原地停留，不转向）
/// </summary>
public class AIController : MonoBehaviour, IController
{
    private MonsterActor host;
    private BTBlackboard bb;
    private BTNode root;
    private BTAction_MoveToPlayer moveAction;
    private BTAction_Standoff standoffAction;
    private float nextDecisionTime;
    private float nextTargetRetryTime;

    /// <summary>运行时自动挂载（CreateDefaultController 兜底 AddComponent）；Prefab 上无序列化需求。</summary>
    void Awake()
    {
        host = GetComponent<MonsterActor>();
    }

    public void OnAttached(Actor owner)
    {
        host = owner as MonsterActor;
        if (host == null) return;
        // 每次挂载（spawn/复用）重建行为树：让运行中调整的配置（skillPriority 等）即时生效
        BuildTree();
        ResetDecisionState();
    }

    public void OnDetached()
    {
        host = null;
    }

    /// <summary>重建决策状态：决策相位随机化（打散同帧刷怪的行为同步）。攻击就绪态每帧由技能 CD 重算，无需预置。</summary>
    private void ResetDecisionState()
    {
        bb.Pressed = CommandButtons.None;
        bb.WantMove = false;
        bb.MoveDir = Vector3.zero;
        nextDecisionTime = Time.time + Random.Range(0f, host.decisionIntervalMax);
    }

    private void BuildTree()
    {
        bb = new BTBlackboard(host);
        moveAction = new BTAction_MoveToPlayer();
        standoffAction = new BTAction_Standoff();

        // 攻击分支：任一攻击范围内，技能/普攻按权重互斥选择。
        // 技能分支带 InSkillRange 守卫、普攻分支带 InBasicRange 守卫——玩家在技能范围内但普攻范围外时，
        // 普攻分支因范围条件失败自动回退，只能释放技能（若冷却好）；两个分支都失败则整体回退到追击。
        BTNode attackBranch = new BTSequence(
            new BTCondition_InAttackRange(),
            new BTWeightedSelector(
                new BTWeightedSelector.Entry(host.skillPriority,
                    new BTSequence(new BTCondition_InSkillRange(), new BTCondition_SkillReady(), new BTAction_Skill())),
                new BTWeightedSelector.Entry(1f - host.skillPriority,
                    new BTSequence(new BTCondition_InBasicRange(), new BTCondition_BasicReady(), new BTAction_BasicAttack()))
            )
        );

        // 追击分支：攻击范围外、索敌范围内。
        // - 追击超时（ChaseExpired）→ 对峙（随机角度游走，不再直线扑向玩家）
        // - 未超时 → 按权重在「位移冲刺」与「普通追击」间互斥选择；位移冷却未就绪时自动回退普通追击。
        BTNode chaseBranch = new BTSelector(
            new BTSequence(new BTCondition_ChaseExpired(), standoffAction),
            new BTWeightedSelector(
                new BTWeightedSelector.Entry(host.aiMobilityChance,
                    new BTSequence(new BTCondition_MobilityReady(), new BTAction_MobilityDash())),
                new BTWeightedSelector.Entry(1f - host.aiMobilityChance, moveAction)
            )
        );

        // 根选择器：优先级互斥短路（攻击 > 追击/冲刺 > 待机）
        root = new BTSelector(
            attackBranch,
            new BTSequence(new BTCondition_InDetectRange(), chaseBranch),
            new BTAction_Idle()
        );
    }

    public void Tick(in ActorContext ctx, ref ControlCommand cmd)
    {
        cmd = ControlCommand.Empty;
        if (host == null) return;
        if (host.isPossessed) return;
        if (host.isDowned || host.isWeakened) return;
        if (root == null) return;

        // Y2 目标有效性校验：销毁/失活（池回收）/ tag 不再是 Player（附身切换或释放）→ 失效重找
        if (IsTargetInvalid() && Time.time >= nextTargetRetryTime)
        {
            host.RefreshPlayerTarget();
            nextTargetRetryTime = Time.time + 0.5f; // 查找失败重试节流，避免每帧 FindGameObjectWithTag
        }
        if (host.targetPlayer == null) return;

        RefreshBlackboard(ctx);

        // 追击时长累计：进入攻击范围（追上）或脱离索敌（丢失目标）时清零；否则累计连续追击时长。
        // chaseDuration == 0 时 ChaseExpired 恒 false（一直直线追击），此处累计无副作用。
        if (bb.PlayerInAttackRange || !bb.PlayerInDetectRange)
            bb.ChaseElapsed = 0f;
        else
            bb.ChaseElapsed += ctx.DeltaTime;

        // 决策节拍：到点从根重新评估树（间隔随机抖动，避免大量同类怪同拍行动）
        if (Time.time >= nextDecisionTime)
        {
            // 意图声明模型：每拍先清空输出缓冲，各分支重新声明本拍意图。
            // 避免上一拍意图残留（如玩家脱离索敌/进入攻击范围后仍持续移动）。
            bb.WantMove = false;
            bb.MoveDir = Vector3.zero;
            bb.StandoffMove = false; // 移动模式同样每拍重新声明（对峙/追击）
            root.Evaluate(bb);
            nextDecisionTime = Time.time + Random.Range(host.decisionIntervalMin, host.decisionIntervalMax);
        }

        // 移动：决策点之间持续产出（走位节奏独立刷新，行动更自然）；
        // IsAbilityFacingLocked 期间（冲刺中）抑制移动，避免冲刺与追击位移叠加。
        if (bb.WantMove && !host.IsAbilityFacingLocked)
        {
            if (bb.StandoffMove) standoffAction.TickStandoff(bb, ctx.DeltaTime);
            else moveAction.TickStrafe(bb, ctx.DeltaTime);
            cmd.HasMove = true;
            cmd.MoveDirection = bb.MoveDir;
        }

        // 攻击脉冲：写回后清空（避免下一帧重复触发）
        cmd.Pressed = bb.Pressed;
        bb.Pressed = CommandButtons.None;
    }

    /// <summary>
    /// 索敌目标失效检查：目标被销毁 / 失活（池回收）/ tag 不再是 Player（附身切换或释放）。
    /// 附身玩法下 tag="Player" 会跟随「玩家当前控制的实体」（灵魂或被附身怪）转移，
    /// 缓存必须实时校验，否则会追一个不存在的目标（隔空转向 / 追尸体原位）。
    /// Unity 假空：已销毁对象 == null 为 true，短路后不再访问成员，避免 MissingReferenceException。
    /// </summary>
    private bool IsTargetInvalid()
    {
        Transform t = host.targetPlayer;
        if (t == null) return true;
        if (!t.gameObject.activeInHierarchy) return true;
        return !t.gameObject.CompareTag("Player");
    }

    private void RefreshBlackboard(in ActorContext ctx)
    {
        Vector3 dir = host.targetPlayer.position - host.transform.position;
        dir.y = 0f;
        float dist = dir.magnitude;
        host.playerDetected = dist <= host.detectionRadius;

        bb.Ctx = ctx;
        bb.DistToPlayer = dist;
        bb.TowardPlayerDir = dist > 0.01f ? dir / dist : Vector3.zero;
        bb.PlayerInDetectRange = host.playerDetected;
        bb.PlayerInBasicRange = dist <= host.basicAttackRange;
        bb.PlayerInSkillRange = dist <= host.skillAttackRange;
        bb.PlayerInAttackRange = bb.PlayerInBasicRange || bb.PlayerInSkillRange;

        // 攻击节奏统一以技能自身 cooldown 为准（方案 A）：CanTrigger() 即「冷却就绪」。
        // 不再用 basicCastTime/skillCastTime 双层节拍，避免与技能 CD 职责重叠 + attackSpeed 双重加速。
        bb.BasicReady = AnyBasicCanTrigger();
        bb.SkillReady = AnySkillCanTrigger();
        bb.MobilityReady = AnyMobilityCanTrigger();
    }

    bool AnyBasicCanTrigger()
    {
        for (int i = 0; i < host.basicAbilities.Count; i++)
        {
            var e = host.basicAbilities[i];
            if (e != null && e.ability != null && e.ability.CanTrigger()) return true;
        }
        return false;
    }

    bool AnySkillCanTrigger()
    {
        for (int i = 0; i < host.skillAbilities.Count; i++)
        {
            var e = host.skillAbilities[i];
            if (e != null && e.ability != null && e.ability.CanTrigger()) return true;
        }
        return false;
    }

    bool AnyMobilityCanTrigger()
    {
        for (int i = 0; i < host.mobilityAbilities.Count; i++)
        {
            var e = host.mobilityAbilities[i];
            if (e != null && e.ability != null && e.ability.CanTrigger()) return true;
        }
        return false;
    }

    /// <summary>技能触发成功回调（保留：仍用于通知 AI 技能已释放，当前无额外冷却逻辑）。</summary>
    public void NotifySkillTriggered()
    {
        // 方案 A：技能冷却完全由技能自身 cooldown 管理，此处不再维护 SkillReadyAt。
    }
}
