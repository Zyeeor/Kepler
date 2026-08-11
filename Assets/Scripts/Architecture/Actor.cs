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

    // ---- Control ----
    public IController Controller { get; private set; } = NullController.Instance;
    protected ControlCommand pendingCmd;    // Collected in Update, movement segment consumed in FixedUpdate
    public event Action<Actor> OnControllerChanged;

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
        if (Controller == NullController.Instance)
            SetController(CreateDefaultController());
    }

    /// <summary>
    /// 统一帧循环：Update 收集指令（Controller.Tick → pendingCmd + ExecuteButtons），
    /// FixedUpdate 消费移动段（ExecuteMovement）。
    /// 移动必须在物理帧执行——rb.MovePosition 对 dynamic Rigidbody 在渲染帧调用时，
    /// 位移会被下一个物理步进覆盖 → 移动变慢/手感异常。
    /// </summary>
    protected virtual void Update()
    {
        if (Controller == null) return;

        var ctx = new ActorContext
        {
            Self = transform,
            PlayerTarget = PlayerTarget, // MonsterActor=targetPlayer；SoulActor=自身
            DeltaTime = Time.deltaTime,
        };
        pendingCmd = new ControlCommand();
        Controller.Tick(in ctx, ref pendingCmd);
        ExecuteButtons(in pendingCmd);
        // 移动段在 FixedUpdate 消费（见类注释：Collected in Update, movement segment consumed in FixedUpdate）
    }

    protected virtual void FixedUpdate()
    {
        ExecuteMovement(in pendingCmd);
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

    // ---- IActor implementation ----

    public Transform BodyTransform => transform;

    public bool IsPlayerControlled => Controller is PlayerController;

    public abstract bool IsDowned { get; }
    public abstract string DisplayName { get; }
    public virtual float CurrentHealth => currentHealth;
    public virtual float MaxHealth => maxHealth;

    public abstract void FillAbilitySlots(List<AbilitySlotInfo> buffer);
}
