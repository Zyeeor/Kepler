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
    [Tooltip("未附身（自由灵魂态）时的世界 Y 高度，避免贴地。")]
    public float hoverHeight = 1f;

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
    private Transform possessionAnchor;
    private Transform parentBeforePossession;
    // 附身前灵魂的世界 scale（自由态通常 =1）。附身期间以它为基准每帧修正 localScale，
    // 防止锚点父链 scale 或怪 scale 变化（如暴食小猫化 0.5↔1）连带缩放灵魂并在释放时固化。
    private Vector3 worldScaleBeforePossession = Vector3.one;


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
        if (hoverHeight <= 0f) hoverHeight = 1f;
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
        if (!IsSuppressed && !IsInPossessionFlight)
            EnforceHoverHeight();
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
            // Disable ALL colliders (not just root): soul is parented under the body while
            // possessed; any leftover child collider would still receive combat Overlaps and
            // flash/damage the soul when the body is hit.
            SetSoulCollidersEnabled(false);
        }
        else
        {
            gameObject.tag = "Player";
            SetController(PlayerController.Instance);
            SetSoulCollidersEnabled(true);
            EnforceHoverHeight();
        }
    }

    public void SetPossessionFlight(bool inFlight)
    {
        IsInPossessionFlight = inFlight;
        if (inFlight)
        {
            pendingCmd = new ControlCommand();
            currentVelocity = Vector3.zero;
            SetController(NullController.Instance);
            gameObject.tag = "Soul";
            SetSoulCollidersEnabled(false);
        }
        else if (!IsSuppressed)
        {
            gameObject.tag = "Player";
            SetController(PlayerController.Instance);
            SetSoulCollidersEnabled(true);
            EnforceHoverHeight();
        }
    }

    private void SetSoulCollidersEnabled(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null) colliders[i].enabled = enabled;
        }
    }

    public void SetPossessionPosition(Vector3 bodyPosition, float yOffset)
    {
        transform.position = bodyPosition + Vector3.up * yOffset;
    }

    /// <summary>回到自由灵魂态：保留 XZ，锁定到 <see cref="hoverHeight"/>。</summary>
    public void PlaceInFreeSoulForm(Vector3 referencePosition)
    {
        transform.position = new Vector3(referencePosition.x, hoverHeight, referencePosition.z);
    }

    /// <summary>未附身时强制 Y = hoverHeight（默认 1），避免贴地。</summary>
    public void EnforceHoverHeight()
    {
        Vector3 p = transform.position;
        if (Mathf.Abs(p.y - hoverHeight) <= 0.0001f) return;
        p.y = hoverHeight;
        transform.position = p;
    }

    public void AttachToPossessionAnchor(Transform anchor)
    {
        if (anchor == null) return;
        if (possessionAnchor == anchor) return;

        DetachFromPossessionAnchor();
        parentBeforePossession = transform.parent;
        worldScaleBeforePossession = transform.lossyScale;
        possessionAnchor = anchor;
        // SetParent(worldPositionStays=true) 会按父链 lossyScale 自动反算 localScale，保持灵魂世界 scale 不变。
        // 注意：此处【不能】强制设置 localScale —— anchor 父链（怪模型 FBX 内部节点）scale 往往 ≠ 1，
        // 强制 localScale=1 会使灵魂世界 scale 变成 anchor.lossyScale 倍，导致附身任意怪都变大。
        transform.SetParent(anchor, true);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void DetachFromPossessionAnchor()
    {
        if (possessionAnchor == null) return;

        Transform restoreParent = parentBeforePossession;
        bool restoreParentAlive = restoreParent != null;

        // 先解除父子关系：无论当前是锚点子物体还是场景根，SetParent(null) 后本对象
        // 必为所在场景的根节点，之后 MoveGameObjectToScene 才允许调用（其入参必须是场景根）。
        // 若先调 MoveGameObjectToScene 再 SetParent(null)，灵魂仍是锚点子物体时会抛
        // "Gameobject is not a root in a scene"，导致后续恢复逻辑全部中断（玩家消失 bug）。
        transform.SetParent(null, true);
        // 灵魂现为场景根，localScale == 世界 scale。直接按附身前世界 scale 恢复，
        // 确保自由灵魂态回到附身前大小（若附身期间怪 scale 变化已被 Update 修正，此处即为附身前值）。
        transform.localScale = worldScaleBeforePossession;

        // 兜底（主界面幽灵 bug 根因③）：若锚点怪曾被意外回池，灵魂随其进入 DDOL 场景，
        // 此时灵魂会留在 DDOL 根跨场景存活。先移回活动场景再恢复父级。
        if (!restoreParentAlive && gameObject.scene.name == "DontDestroyOnLoad")
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.IsValid() && active.name != "DontDestroyOnLoad")
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, active);
        }

        if (restoreParentAlive)
        {
            // 恢复到原父级：SetParent(worldPositionStays=true) 保持当前世界 scale，并按新父链自动反算 localScale。
            transform.SetParent(restoreParent, true);
        }

        possessionAnchor = null;
        parentBeforePossession = null;
        worldScaleBeforePossession = Vector3.one;
    }

    /// <summary>
    /// 附身跟随模式：若已配置锚点则由 Transform 父子关系跟随；否则吸附到被附身怪头顶上方。
    /// 由 Update 在 IsSuppressed 时调用（本类 Update 覆写中）。
    /// </summary>
    public void FollowBody(Transform body)
    {
        if (possessionAnchor != null || body == null) return;
        Vector3 pos = body.position + Vector3.up * possessYOffset;
        transform.position = pos;
    }

    protected override void Update()
    {
        if (IsInPossessionFlight) return;

        // 抑制期：不消费输入，仅跟随被附身怪头顶（目标由 PossessionManager.CurrentBody 提供）
        if (IsSuppressed)
        {
            // 附身期间锚点父链 scale 可能变化（如暴食小猫化 0.5↔1），灵魂作为子物体会被连带缩放。
            // 每帧按附身前世界 scale 反算 localScale，保证灵魂世界 scale 恒定（不受怪缩放污染）。
            if (transform.parent != null)
            {
                Vector3 parentLossy = transform.parent.lossyScale;
                transform.localScale = new Vector3(
                    worldScaleBeforePossession.x / parentLossy.x,
                    worldScaleBeforePossession.y / parentLossy.y,
                    worldScaleBeforePossession.z / parentLossy.z);
            }

            var pm = PossessionManager.Instance;
            if (pm != null) FollowBody(pm.CurrentBody != null ? pm.CurrentBody.transform : null);
            return;
        }

        EnforceHoverHeight();
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
        targetPos.y = hoverHeight;
        transform.position = targetPos;
    }

    /// <summary>碰撞移动：CollideAndSlide 滑动（撞墙沿墙切向滑行，不再整段截断）。</summary>
    private Vector3 ApplySpherecast(Vector3 origin, Vector3 dir, float stepDist)
    {
        if (stepDist <= 0f) return origin;
        // 不与 Layer 8(Enemy)、Layer 9(Player) 自身检测，只检测环境（Layer 0=Default）
        int obstacleMask = ~((1 << 8) | (1 << 9));
        return SlideMove(origin, 0.9f, 0.4f, dir * stepDist, obstacleMask);
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
