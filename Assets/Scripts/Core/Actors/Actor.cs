using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Abstract execution-layer base for every controllable entity (SoulActor / MonsterActor).
/// Owns the active IController; SetController() is the single entry point for possession
/// (the architectural core: possession = swapping the Controller).
/// Movement/stats fields live on the base so prefab serialization is shared across actors.
/// </summary>
public abstract class Actor : MonoBehaviour, IActor
{
    [Header("Movement")]
    public float moveSpeed;
    public float acceleration, deceleration;
    public LayerMask groundLayer;

    [Header("Stats")]
    public float maxHealth;
    public float currentHealth;

    // ---- Combat state ----
    /// <summary>Actor-local Tag / Effect / ability gate. Added automatically to keep existing prefabs compatible.</summary>
    public CombatAbilityComponent Combat { get; private set; }
    protected bool IsMovementBlocked { get { return Combat != null && !Combat.CanMove; } }

    // ---- Control ----
    public IController Controller { get; private set; } = NullController.Instance;
    protected ControlCommand pendingCmd;    // Collected in Update, consumed by the active movement path
    public event Action<Actor> OnControllerChanged;
    // Fixed-capacity (32 colliders), actor-local buffer: SlideMove only runs synchronous Physics
    // queries on the main thread. If full, returned colliders still provide conservative escape
    // behavior without hot-path growth.
    private readonly Collider[] slideMoveOverlapBuffer = new Collider[32];

    /// <summary>
    /// The single entry point for the possession mechanism.
    /// </summary>
    public virtual void SetController(IController next)
    {
        if (next == Controller) return;
        Controller?.OnDetached();
        Controller = next ?? NullController.Instance;
        Controller.OnAttached(this);
        OnControllerChanged?.Invoke(this);
    }

    /// <summary>Soul → PlayerController, Monster → AIController.</summary>
    protected abstract IController CreateDefaultController();

    // ---- Lifecycle ----
    /// <summary>Cache rb/animator; attach the default controller.</summary>
    protected virtual void Awake()
    {
        Combat = GetComponent<CombatAbilityComponent>();
        if (Combat == null) Combat = gameObject.AddComponent<CombatAbilityComponent>();
        Combat.AddLooseTags(this, new[] { "Actor" });

        if (Controller == NullController.Instance)
            SetController(CreateDefaultController());
    }

    /// <summary>
    /// 统一帧循环：Update 收集指令（Controller.Tick → pendingCmd + ExecuteButtons）。
    /// 玩家控制角色在 Update 消费移动，以便子弹时间使用非缩放时间；
    /// AI 保持在 FixedUpdate 消费移动。
    /// </summary>
    protected virtual void Update()
    {
        if (Controller == null) return;

        var ctx = new ActorContext
        {
            Self = transform,
            PlayerTarget = PlayerTarget, // MonsterActor=targetPlayer；SoulActor=自身
            DeltaTime = IsPlayerControlled && Time.timeScale < 1f
                ? Time.unscaledDeltaTime
                : Time.deltaTime,
        };
        pendingCmd = new ControlCommand();
        Controller.Tick(in ctx, ref pendingCmd);
        ExecuteButtons(in pendingCmd);
        if (IsPlayerControlled) ExecuteMovement(in pendingCmd);
    }

    protected virtual void FixedUpdate()
    {
        if (!IsPlayerControlled) ExecuteMovement(in pendingCmd);
    }

    /// <summary>由子类提供"当前追击/交互目标"（MonsterActor=targetPlayer）。</summary>
    protected virtual Transform PlayerTarget => null;

    // ---- Execution modules (concrete, no interface — rules 4.2) ----

    /// <summary>
    /// Movement + facing + spherecast. Base 为空实现——由子类按各自移动语义 override：
    /// MonsterActor（AI 匀速 / 附身玩家态加速度平滑）、SoulActor（玩家输入加速度平滑）。
    /// </summary>
    protected virtual void ExecuteMovement(in ControlCommand cmd)
    {
    }

    /// <summary>Abstract: Soul / Monster each map buttons to ability triggers.</summary>
    protected abstract void ExecuteButtons(in ControlCommand cmd);

    // ---- Collision-aware movement (CollideAndSlide) ----

    /// <summary>
    /// 滑动移动（CollideAndSlide，PhysX 胶囊控制器同源策略）：
    ///   1. 按 maxStep 把位移分段（高速/低帧率下防穿透薄碰撞体）；
    ///   2. 每段 SphereCast 沿位移方向预检测；
    ///   3. 命中 → 前进到命中点前 skin 处，剩余位移沿 hit.normal 切向投影（沿墙滑动）；
    ///   4. 重复投影方向再 cast（迭代 maxIterations 次），防滑入墙角/双面棱；
    ///   5. 终点 CheckSphere 校验：仍在碰撞体内（凸包棱滑入）则回退到命中点前。
    /// y 恒不改：灵魂飞行/附身怪高度由调用方保持（返回值只含 XZ 位移）。
    /// </summary>
    protected Vector3 SlideMove(Vector3 origin, float capsuleCenterY, float radius,
        Vector3 displacement, int obstacleMask, float skin = 0.05f, int maxIterations = 2, float maxStep = 0.4f)
    {
        Vector3 pos = origin;
        Vector3 totalRemaining = displacement;

        // 脱困：起点已在碰撞体内（如流送地图把装饰物生成在玩家身上/滑入棱角），
        // 沿"最近表面点"方向推出 skin，避免 SphereCast 内部起始行为不可靠 + 终点回退锁死。
        Vector3 capsuleCenter0 = pos + Vector3.up * capsuleCenterY;
        int startHitCount = Physics.OverlapSphereNonAlloc(
            capsuleCenter0, radius, slideMoveOverlapBuffer, obstacleMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < startHitCount; i++)
        {
            var c = slideMoveOverlapBuffer[i];
            if (c == null || c.isTrigger) continue;
            Vector3 closest = Physics.ClosestPoint(capsuleCenter0, c, c.transform.position, c.transform.rotation);
            Vector3 pushDir = capsuleCenter0 - closest;
            float pushDist = pushDir.magnitude;
            if (pushDist < 0.0001f) pushDir = Vector3.up; // 球心重合兜底
            else pushDir /= pushDist;
            pos += pushDir * Mathf.Max(0f, radius - pushDist + skin);
        }

        // 外层：按 maxStep 分段（高速防穿透）
        while (totalRemaining.sqrMagnitude > 0.0001f)
        {
            Vector3 segment = totalRemaining.magnitude > maxStep
                ? totalRemaining.normalized * maxStep
                : totalRemaining;
            totalRemaining -= segment;

            // 内层：该段内的 cast + 滑动投影（迭代防墙角）
            Vector3 segRemaining = segment;
            int iterations = 0;
            Vector3 lastSafe = pos;
            while (segRemaining.sqrMagnitude > 0.0001f && iterations < maxIterations)
            {
                iterations++;
                float stepDist = segRemaining.magnitude;
                Vector3 dir = segRemaining / stepDist;
                Vector3 capsuleCenter = pos + Vector3.up * capsuleCenterY;

                if (Physics.SphereCast(capsuleCenter, radius, dir, out RaycastHit hit, stepDist, obstacleMask, QueryTriggerInteraction.Ignore))
                {
                    // 前进到命中点前 skin 处
                    float moveDist = Mathf.Max(0f, hit.distance - skin);
                    pos += dir * moveDist;
                    lastSafe = pos;
                    float leftoverDist = stepDist - moveDist;
                    if (leftoverDist <= 0.0001f) break;

                    // 剩余位移沿法线切向投影 → 沿墙滑动
                    Vector3 normal = hit.normal.normalized;
                    Vector3 leftover = dir * leftoverDist;
                    Vector3 slide = leftover - normal * Vector3.Dot(leftover, normal);
                    // 投影后与输入方向反向（撞正墙/墙角）→ 停止，防抖动
                    if (Vector3.Dot(slide, displacement) <= 0.0001f) break;
                    segRemaining = slide;
                }
                else
                {
                    pos += segRemaining;
                    segRemaining = Vector3.zero;
                }
            }

            // 终点校验：若滑入碰撞体（凸包棱/双面），回退到最近安全点。
            if (Physics.CheckSphere(pos + Vector3.up * capsuleCenterY, radius, obstacleMask, QueryTriggerInteraction.Ignore))
                pos = lastSafe;
        }
        return pos;
    }

    // ---- IActor implementation ----

    public Transform BodyTransform => transform;

    public bool IsPlayerControlled => Controller is PlayerController;

    public abstract bool IsDowned { get; }
    public abstract string DisplayName { get; }
    public virtual float CurrentHealth => currentHealth;
    public virtual float MaxHealth => maxHealth;

    public abstract void FillAbilitySlots(List<AbilitySlotInfo> buffer);
}
