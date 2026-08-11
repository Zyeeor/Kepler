using UnityEngine;

/// <summary>
/// 灵魂执行体：灵魂形态的统一 Actor（玩家主控对象）。
/// 组合现有组件：Stats = PlayerHealth（灵魂 HP/衰减/死亡）、Combat = PlayerCombat（普攻/技能触发）。
/// 默认 Controller = PlayerController.Instance（玩家输入）；附身期间由 PossessionManager 抑制并跟随被附身怪。
/// </summary>
public class SoulActor : Actor
{
    [Header("Composition (auto-wired)")]
    public PlayerHealth stats;      // Stats 组件（灵魂 HP/衰减/死亡）
    public PlayerCombat combat;     // Combat 组件（普攻/技能触发）

    [Header("Possession Suppression（附身期间由 PossessionManager 置位）")]
    public float possessYOffset = 0.5f;

    /// <summary>附身期间 = true（PossessionManager.SetSuppressed 控制）。</summary>
    public bool IsSuppressed { get; private set; }
    public bool IsInPossessionFlight { get; private set; }

    public override bool IsDowned => false; // 灵魂无倒地
    public override string DisplayName => "Soul";

    // ---- IActor 数据转发：灵魂真实 HP 在 PlayerHealth（stats），基类字段不同步，
    //      此处转发避免 IActor 视图读到默认 0（HUD/状态面板统一走 IActor 读取） ----
    public override float CurrentHealth => stats != null ? stats.currentHealth : 0f;
    public override float MaxHealth => stats != null ? stats.maxHealth : 0f;

    private Vector3 currentVelocity;          // 加速度平滑（移动手感）

    /// <summary>移动速度：SoulActor 自身配置优先，否则读 PlayerPassiveManager 当前移速（含被动加成）。</summary>
    private float EffectiveMoveSpeed
    {
        get
        {
            float speed;
            if (moveSpeed > 0f) speed = moveSpeed;
            else
            {
                var pm = PlayerPassiveManager.Instance;
                speed = pm != null && pm.CurrentMoveSpeed > 0f ? pm.CurrentMoveSpeed : 5f;
            }
            return Combat != null ? Combat.ModifyMoveSpeed(speed) : speed;
        }
    }

    protected override IController CreateDefaultController()
    {
        return PlayerController.Instance;
    }

    protected override void Awake()
    {
        if (stats == null) stats = GetComponent<PlayerHealth>();
        if (combat == null) combat = GetComponent<PlayerCombat>();
        base.Awake(); // 挂载默认 Controller（PlayerController.Instance）
        if (Combat != null) Combat.AddLooseTags(this, new[] { "Actor.Soul" });
    }

    /// <summary>
    /// Start 兜底重绑：同一物体组件 Awake 顺序不保证，PlayerController.Awake 可能晚于本组件执行，
    /// 导致 Awake 时 PlayerController.Instance 为 null 而绑成 NullController。
    /// 所有组件 Awake 完成后重绑玩家控制器（SetController 自带幂等保护）。
    /// </summary>
    void Start()
    {
        if (Controller == NullController.Instance && PlayerController.Instance != null)
            SetController(PlayerController.Instance);
    }

    /// <summary>
    /// 附身抑制（PossessionManager 调用）：
    /// true  = 停用控制并关闭 Collider；false = 恢复玩家控制和 Collider。
    /// 灵魂 renderer 保持可见（附身时灵魂跟随被附身怪头顶）。
    /// </summary>
    public void SetSuppressed(bool suppressed)
    {
        if (IsSuppressed == suppressed) return;
        IsSuppressed = suppressed;

        if (suppressed)
        {
            SetController(NullController.Instance);
            // 附身期间灵魂不再是 "Player"——玩家身份由被附身身体承接（MonsterActor.OnPossessed 已设 tag=Player）。
            // 消除双 Player tag 二义性：期间 FindGameObjectWithTag("Player") 唯一命中身体（怪物转火/相机跟随/AoE 判定）。
            gameObject.tag = "Soul";
            // 清空 stale 指令与惯性：抑制期 Update 不再刷新 pendingCmd，
            // 若不清理，FixedUpdate 会持续用旧指令驱动常规移动并与 FollowBody 直写互相拉扯。
            pendingCmd = new ControlCommand();
            currentVelocity = Vector3.zero;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

        }
        else
        {
            gameObject.tag = "Player";
            SetController(PlayerController.Instance);
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = true;
        }
    }

    public void SetPossessionFlight(bool inFlight)
    {
        IsInPossessionFlight = inFlight;
        Collider collider = GetComponent<Collider>();
        if (inFlight)
        {
            pendingCmd = new ControlCommand();
            currentVelocity = Vector3.zero;
            SetController(NullController.Instance);
            gameObject.tag = "Soul";
            if (collider != null) collider.enabled = false;
        }
        else if (!IsSuppressed)
        {
            gameObject.tag = "Player";
            SetController(PlayerController.Instance);
            if (collider != null) collider.enabled = true;
        }
    }

    public void SetPossessionPosition(Vector3 bodyPosition, float yOffset)
    {
        transform.position = bodyPosition + Vector3.up * yOffset;
    }

    /// <summary>
    /// 附身跟随模式：灵魂吸附到被附身怪头顶上方 + possessYOffset。
    /// 由 Update 在 IsSuppressed 时调用（本类 Update 覆写中）。
    /// </summary>
    public void FollowBody(Transform body)
    {
        if (body == null) return;
        Vector3 pos = body.position + Vector3.up * possessYOffset;
        transform.position = pos;
    }

    protected override void Update()
    {
        if (IsInPossessionFlight) return;

        // 抑制期：不消费输入，仅跟随被附身怪头顶（目标由 PossessionManager.CurrentBody 提供）
        if (IsSuppressed)
        {
            var pm = PossessionManager.Instance;
            if (pm != null) FollowBody(pm.CurrentBody != null ? pm.CurrentBody.transform : null);
            return;
        }
        base.Update();
    }

    /// <summary>
    /// Soul controls only its left-click BasicAbility. Right-click is exclusively reserved for corpse possession.
    /// </summary>
    protected override void ExecuteButtons(in ControlCommand cmd)
    {
        if ((cmd.Pressed & CommandButtons.Basic) != 0 && combat != null)
            combat.PlayerTriggerBasicAttack();

        if ((cmd.Pressed & CommandButtons.Skill1) == 0) return;

        if (PossessionManager.Instance == null)
        {
            Debug.LogWarning("[PossessionInput] Ignored right-click: PossessionManager is missing.");
            return;
        }

        if (PlayerController.Instance == null)
        {
            Debug.LogWarning("[PossessionInput] Ignored right-click: PlayerController is missing.");
            return;
        }

        PossessionManager.Instance.TryRequestPossessFromInput(PlayerController.Instance.GetMouseRay(), "SoulActor");
    }

    /// <summary>
    /// 灵魂移动 + 朝向 + 位移：
    /// 移动时朝移动方向平滑转向；静止时面向鼠标；位移 = 加速度平滑 + SphereCast 预检测。
    /// </summary>
    protected override void ExecuteMovement(in ControlCommand cmd)
    {
        if (IsMovementBlocked)
        {
            currentVelocity = Vector3.zero;
            return;
        }

        bool hasMove = cmd.HasMove && cmd.MoveDirection.sqrMagnitude >= 0.0001f;
        float movementDeltaTime = Time.timeScale < 1f ? Time.unscaledDeltaTime : Time.deltaTime;
        Vector3 move = cmd.MoveDirection;
        move.y = 0f;

        if (hasMove)
        {
            // 移动：平滑转向移动方向
            Quaternion targetRot = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, movementDeltaTime * 12f);
        }
        else
        {
            // Idle: face mouse
            Vector3 aim;
            if (PlayerController.Instance != null && PlayerController.Instance.TryGetAimPoint(out aim))
            {
                Vector3 dir = aim - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }

        // 位移：加速度平滑（加速 30 / 减速 25，手感调参）
        float speed = EffectiveMoveSpeed;
        Vector3 desired = hasMove ? move * speed : Vector3.zero;
        float accel = hasMove ? (acceleration > 0f ? acceleration : 30f) : (deceleration > 0f ? deceleration : 25f);
        currentVelocity = Vector3.MoveTowards(currentVelocity, desired, accel * movementDeltaTime);

        if (currentVelocity.sqrMagnitude <= 0.01f) return;
        Vector3 targetPos = ApplySpherecast(transform.position, currentVelocity.normalized, currentVelocity.magnitude * movementDeltaTime);
        targetPos.y = transform.position.y;
        transform.position = targetPos;
    }

    /// <summary>spherecast 预检测：撞墙缩短步长防穿墙。</summary>
    private Vector3 ApplySpherecast(Vector3 origin, Vector3 dir, float stepDist)
    {
        if (stepDist <= 0f) return origin;
        Vector3 capsuleCenter = origin + Vector3.up * 0.9f; // capsule center y=0.9
        float capsuleRadius = 0.4f;
        // 不与 Layer 8(Enemy)、Layer 9(Player) 自身检测，只检测环境（Layer 0=Default）
        int obstacleMask = ~((1 << 8) | (1 << 9));
        if (Physics.SphereCast(capsuleCenter, capsuleRadius, dir, out RaycastHit hit, stepDist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            stepDist = Mathf.Max(0f, hit.distance - 0.05f);
        }
        return origin + dir * stepDist;
    }

    public override void FillAbilitySlots(System.Collections.Generic.List<AbilitySlotInfo> buffer)
    {
        if (buffer == null || combat == null) return;
        buffer.Clear();
        foreach (var a in combat.basicAbilities)
        {
            if (a == null) continue;
            buffer.Add(new AbilitySlotInfo { Name = a.abilityName, CooldownRemaining = 0f, CooldownTotal = 0f, HpCost = 0f });
        }

    }
}
