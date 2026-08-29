using System.Collections;
using UnityEngine;

/// <summary>
/// Scene-level possession orchestrator. Possession switches the shared PlayerController
/// from SoulActor to MonsterActor, while PossessionBehavior resolves middle-click targets.

/// </summary>
public class PossessionManager : SceneSingleton<PossessionManager>
{
    [Header("Possession")]
    public float possessFlySpeedMultiplier = 5f;
    public float possessYOffset = 0.5f;
    public float possessCooldown = 3f;
    public float minPossessTime = 1f;
    public float possessionDecayPercent = 0.05f;
    public float decayInterval = 1f;
    [Tooltip("Body 死亡后的附身锁定秒数（Pass v1 §7.4 失败惩罚）。与主动 F 离身（无额外 CD）分离，不共用 possessCooldown。")]
    [Min(0f)] public float bodyDeathPossessionLock = 1.5f;
    [Tooltip("成功附身新 Body 后的伤害免疫秒数（Pass v1 §7.5）。独立于 Bullet Time：不 Slow Motion、不消耗 BT Charge。")]
    [Min(0f)] public float postPossessDamageImmunityDuration = 0.25f;

    public enum SwitchState { Idle, Flying, Possessing, Releasing }
    public SwitchState State { get; private set; }
    public MonsterActor CurrentBody { get; private set; }
    public float CooldownRemaining { get; private set; }
    /// <summary>当前是否正在被动流逝附身 Body 的耐久（表现层读取，如血条燃烧特效）。</summary>
    public bool IsBodyDecaying { get; private set; }

    public event System.Action<MonsterActor> OnPossessionStarted;
    /// <summary>Single post-commit event consumed by run-level systems. Transaction ids are idempotency keys.</summary>
    public event System.Action<MonsterActor, PossessionGrantReason, long> PossessionCommitted;
    public event System.Action OnPossessionEnded;
    public event System.Action<MonsterActor> OnBodyDiedWhilePossessing;
    /// <summary>附身结束（带原因细分；教学 TUT-05 用 VoluntaryRelease 判定"主动脱离"）。</summary>
    public event System.Action<PossessionEndReason> OnPossessionEndedEx;

    /// <summary>附身结束原因（OnPossessionEndedEx 参数）。</summary>
    public enum PossessionEndReason
    {
        /// <summary>玩家主动脱离（RequestRelease / 换身 Detach）。</summary>
        VoluntaryRelease,
        /// <summary>附身中身体死亡（被迫脱离）。</summary>
        BodyDied,
        /// <summary>系统重置（读档/场景切换等非玩家行为）。</summary>
        SystemReset,
    }

    private Coroutine flyRoutine;
    private float possessStartTime;
    private SoulActor soul;
    private PossessionBehavior behavior;
    private MonsterActor reservedBody;
    private bool handlingGameOver;
    private long possessionTransactionId;
    private bool nextPossessionIsDeathRelay;
    private PossessionGrantReason reservedGrantReason = PossessionGrantReason.PlayerPossession;
    private int lastPossessionInputFrame = -1;
    private bool bossBattleSwitchMode;

    /// <summary>Boss 战允许在附身尸体之间立即切换，不受常规冷却和最短附身时间限制。</summary>
    public bool IsBossBattleSwitchMode => bossBattleSwitchMode;

    protected override void Awake()
    {
        base.Awake();   // 防重复注册：已有实例时 Destroy 本对象（Destroy 延迟到帧末，须自行跳过后续初始化）
        if (Instance != this) return;
        State = SwitchState.Idle;
        soul = FindObjectOfType<SoulActor>();
        behavior = GetComponent<PossessionBehavior>();
        if (behavior == null) behavior = gameObject.AddComponent<PossessionBehavior>();
        behavior.Initialize(this);
        PossessionImprintManager.EnsureInstance().Attach(this);
        // 断环（Kimi 评审）：GameOver 通知由 GameManager 状态事件驱动，GameManager 不再认识本类；
        // 本类订阅常驻 GameManager 的静态事件（场景级单例须在 OnDestroy 退订，防悬空委托）。
        GameManager.OnStateChanged += HandleGameManagerStateChanged;
    }

    protected override void OnDestroy()
    {
        GameManager.OnStateChanged -= HandleGameManagerStateChanged;   // 场景卸载退订（GameManager 常驻）
        base.OnDestroy();                                               // 清 Instance
    }

    void HandleGameManagerStateChanged(GameManager.GameState state)
    {
        if (state == GameManager.GameState.GameOver)
            OnGameOver();
    }

    void OnDisable()
    {
        if (State != SwitchState.Flying) return;

        if (flyRoutine != null)
        {
            StopCoroutine(flyRoutine);
            flyRoutine = null;
        }
        if (reservedBody != null) reservedBody.CancelPossessionReservation();
        reservedBody = null;
        if (soul == null) soul = FindObjectOfType<SoulActor>();
        if (soul != null)
        {
            soul.SetPossessionFlight(false);
            soul.SetSuppressed(false);
        }
        State = SwitchState.Idle;
        Debug.LogWarning("[Possession] Flight aborted because PossessionManager was disabled; soul control restored.");
    }

    void Update()
    {
        if (handlingGameOver && (GameManager.Instance == null || GameManager.Instance.currentState != GameManager.GameState.GameOver))
            handlingGameOver = false;

        if (CooldownRemaining > 0f) CooldownRemaining -= Time.unscaledDeltaTime;

        IsBodyDecaying = false;
        if (State != SwitchState.Possessing || CurrentBody == null) return;
        if (TimeScaleManager.IsDomainActive(TimeDomain.Pause)) return;
        if (CurrentBody.suppressPossessionDrain || MonsterActor.IsDamageImmune(CurrentBody)) return;
        // 技能烧血已把耐久扣到 0，死亡结算正等待该次技能判定完成：
        // 被动流逝不得在宽限窗口内抢先判死，否则技能仍会被打断。
        if (CurrentBody.IsAbilityCostDeathPending) return;
        // Pass v1 §9：Elite Possessed Body 不再免疫普通 4% Max HP/s Body Decay。
        // （旧规则：Elite HP 不被被动附身计时消耗，本轮移除。）
        if (CurrentBody.IsBossBattleReserveBody) return;

        if (decayInterval <= 0f) return;
        IsBodyDecaying = true;
        float decayAmount = CurrentBody.maxHealth * possessionDecayPercent / decayInterval * Time.unscaledDeltaTime;
        if (PossessionImprintManager.Instance != null)
            decayAmount *= PossessionImprintManager.Instance.GetPossessionDrainMultiplier(CurrentBody);
        CurrentBody.currentHealth -= decayAmount;
        if (CurrentBody.currentHealth > 0f) return;

        CurrentBody.currentHealth = 0f;
        Debug.Log("[Possession] Current possessed body expired from decay.");
        NotifyBodyDied();
    }

    public bool TryRequestPossessFromInput(Ray aimRay, string source)
    {
        if (lastPossessionInputFrame == Time.frameCount) return false;
        lastPossessionInputFrame = Time.frameCount;
        Debug.Log("[PossessionInput] Middle-click received by " + source + ".");

        return TryRequestPossess(aimRay);
    }

    public bool TryRequestPossess(Ray aimRay)
    {
        if (behavior == null)
        {
            behavior = GetComponent<PossessionBehavior>();
            if (behavior == null) behavior = gameObject.AddComponent<PossessionBehavior>();
            behavior.Initialize(this);
            Debug.LogWarning("[Possession] Recreated missing PossessionBehavior.");
        }

        return behavior.TryBegin(aimRay);
    }

    public void RequestPossess(Ray aimRay)
    {
        TryRequestPossess(aimRay);
    }

    public bool CanStartPossession(out string reason)
    {
        if (handlingGameOver || (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver))
        {
            reason = "game is over";
            return false;
        }

        if (State == SwitchState.Flying || State == SwitchState.Releasing)
        {
            reason = "possession state is busy: " + State;
            return false;
        }

        if (!bossBattleSwitchMode && State == SwitchState.Idle && CooldownRemaining > 0f)
        {
            reason = "possession cooldown remaining=" + CooldownRemaining.ToString("F2");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool ValidatePossessionTarget(MonsterActor target, out string reason)
    {
        if (target == null)
        {
            reason = "target has no MonsterActor";
            return false;
        }

        if (!target.CanBePossessed)
        {
            reason = "target is not in its downed possession window";
            return false;
        }

        if (target.isPossessed)
        {
            reason = "target is already possessed";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool BeginPossessionFlight(MonsterActor target, PossessionGrantReason grantReason = PossessionGrantReason.PlayerPossession)
    {
        if (!CanStartPossession(out string stateReason))
        {
            Debug.Log("[Possession] Flight rejected: " + stateReason);
            return false;
        }

        if (!ValidatePossessionTarget(target, out string targetReason))
        {
            Debug.Log("[Possession] Flight rejected: " + targetReason);
            return false;
        }

        // Pass v1 §7.2：Body→Body 主动换身至少控制当前 Body minPossessTime 秒（覆盖直接 Body→Body 切换，不只 F Release）。
        // Boss 战切换模式豁免。
        if (State == SwitchState.Possessing && !bossBattleSwitchMode
            && Time.unscaledTime - possessStartTime < minPossessTime)
        {
            Debug.Log($"[Possession] Body→Body switch rejected: min possession time remaining={minPossessTime - (Time.unscaledTime - possessStartTime):F2}");
            return false;
        }

        if (soul == null) soul = FindObjectOfType<SoulActor>();
        if (soul == null)
        {
            Debug.LogWarning("[Possession] Flight rejected: SoulActor is missing.");
            return false;
        }

        if (!target.TryReserveForPossession())
        {
            Debug.Log("[Possession] Flight rejected: target reservation failed.");
            return false;
        }
        reservedBody = target;
        reservedGrantReason = grantReason;

        if (State == SwitchState.Possessing)
        {
            StopBulletTime();
            DetachCurrentBodyForSwitch();
        }

        State = SwitchState.Flying;
        soul.SetPossessionFlight(true);
        flyRoutine = StartCoroutine(FlyAndCommitRoutine(target));
        Debug.Log($"[Possession] Flight started: target='{target.displayName}', speedMultiplier={possessFlySpeedMultiplier:F1}");
        return true;
    }

    public void RequestRelease(bool force)
    {
        if (State != SwitchState.Possessing) return;
        if (!bossBattleSwitchMode && !force && Time.unscaledTime - possessStartTime < minPossessTime)
        {
            Debug.Log("[Possession] Release rejected: min possession time remaining=" + (minPossessTime - (Time.unscaledTime - possessStartTime)).ToString("F2"));
            return;
        }

        CommitRelease(recycleBody: true, startCooldown: false);
    }

    /// <summary>
    /// Toggles the boss battle possession rules. The mode is owned by the boss encounter
    /// so normal waves retain their cooldown and minimum-possession-time behavior.
    /// </summary>
    public void SetBossBattleSwitchMode(bool enabled)
    {
        bossBattleSwitchMode = enabled;
        if (enabled) CooldownRemaining = 0f;
    }

    /// <summary>
    /// Debug/test helper: instantly possess a living body, skipping downed-window validation and flight.
    /// </summary>
    public bool DebugForcePossess(MonsterActor target, PossessionGrantReason grantReason = PossessionGrantReason.Debug)
    {
        if (target == null)
        {
            Debug.LogWarning("[Possession] DebugForcePossess rejected: target is null.");
            return false;
        }

        if (handlingGameOver || (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver))
        {
            Debug.Log("[Possession] DebugForcePossess rejected: game is over.");
            return false;
        }

        if (soul == null) soul = FindObjectOfType<SoulActor>();
        if (soul == null)
        {
            Debug.LogWarning("[Possession] DebugForcePossess rejected: SoulActor is missing.");
            return false;
        }

        if (State == SwitchState.Flying)
        {
            if (flyRoutine != null)
            {
                StopCoroutine(flyRoutine);
                flyRoutine = null;
            }
            if (reservedBody != null) reservedBody.CancelPossessionReservation();
            reservedBody = null;
            soul.SetPossessionFlight(false);
            State = SwitchState.Idle;
        }

        if (State == SwitchState.Possessing)
        {
            if (CurrentBody == target) return true;
            StopBulletTime();
            DetachCurrentBodyForSwitch();
        }

        CooldownRemaining = 0f;
        CommitPossession(target, grantReason);
        Debug.Log($"[Possession] DebugForcePossess committed: target='{target.displayName}'");
        return true;
    }

    public void TriggerBulletTime()
    {
        if (State != SwitchState.Possessing || CurrentBody == null) return;
        // Pass v1 §8.2：Charge 检查，0 时同 Body 不可再使用（换 Body 后重新刷新）。
        if (CurrentBody.bulletTimeChargesRemaining <= 0) return;
        CurrentBody.bulletTimeChargesRemaining--;
        BulletTimeController.EnsureInstance().Trigger(CurrentBody);
    }

    private IEnumerator FlyAndCommitRoutine(MonsterActor target)
    {
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.currentHealth = PlayerHealth.Instance.soulMaxHealth;
            PlayerHealth.Instance.UpdateHealthUI();
        }

        float flySpeed = (PlayerHealth.Instance != null ? PlayerHealth.Instance.SoulMoveSpeedForFly : 5f) * possessFlySpeedMultiplier;
        while (target != null && target.CanCompleteReservedPossession)
        {
            Vector3 targetPosition = target.transform.position;
            targetPosition.y = soul.transform.position.y;
            if (Vector3.Distance(soul.transform.position, targetPosition) <= 0.3f) break;

            Vector3 direction = targetPosition - soul.transform.position;
            soul.transform.position = Vector3.MoveTowards(soul.transform.position, targetPosition, flySpeed * Time.unscaledDeltaTime);
            if (direction.sqrMagnitude > 0.0001f) soul.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            yield return null;
        }

        flyRoutine = null;
        if (target == null || !target.CanCompleteReservedPossession)
        {
            if (reservedBody != null) reservedBody.CancelPossessionReservation();
            reservedBody = null;
            Debug.Log("[Possession] Flight cancelled: target was no longer a valid reserved corpse.");
            CancelFlightToSoul();
            yield break;
        }

        PossessionGrantReason reason = nextPossessionIsDeathRelay ? PossessionGrantReason.DeathRelay : reservedGrantReason;
        CommitPossession(target, reason);
    }

    /// <summary>Explicit initial assignment entry point; only the opening flow should call this.</summary>
    public bool CommitInitialAssignment(MonsterActor target)
    {
        if (target == null || State == SwitchState.Possessing) return false;
        if (soul == null) soul = FindObjectOfType<SoulActor>();
        if (soul == null) return false;
        CommitPossession(target, PossessionGrantReason.InitialAssignment);
        return true;
    }

    private void CommitPossession(MonsterActor target, PossessionGrantReason reason)
    {
        if (target == null) return;

        reservedBody = null;
        CurrentBody = target;
        State = SwitchState.Possessing;
        possessStartTime = Time.unscaledTime;

        if (soul != null)
        {
            soul.SetPossessionFlight(false);
            soul.SetSuppressed(true);
            if (target is Enemy enemy && enemy.soulAnchorPoint != null)
                soul.AttachToPossessionAnchor(enemy.soulAnchorPoint);
            if (soul.Combat != null) soul.Combat.AddLooseTags(this, new[] { "State.Possession.Active", "State.Soul.Suppressed" });
        }
        else if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.SetSoulActive(false);
        }

        if (target.Combat != null) target.Combat.AddLooseTags(this, new[] { "State.Possession.Active", "State.Possession.Controlled" });

        // Pre-Combat gate (Pass v1): the first successful Possession of the Opening Carrier
        // starts the run combat clock. Runs through the Opening Carrier flow always satisfy
        // this before any Normal Spawn / Elite Schedule / Starter Gem can fire.
        if (target.IsOpeningCarrier && RunSpawnDirector.Instance != null)
            RunSpawnDirector.Instance.MarkCombatStarted();

        target.OnPossessed();
        target.SetController(PlayerController.Instance);
        SetCameraTarget(target.transform);

        // Pass v1 §7.5：Commit 后短暂免伤（独立于 Bullet Time，不 Slow Motion，不消耗 BT Charge）。
        if (postPossessDamageImmunityDuration > 0f)
            BulletTimeController.EnsureInstance().ApplyDamageImmunityForDuration(target, postPossessDamageImmunityDuration);

        // Pass v1 §8.2：每 Body 默认 1 次 BT Charge，附身刷新（Boss Reserve Body 同样刷新）。
        target.bulletTimeChargesRemaining = BulletTimeController.ConfiguredChargesPerBody;

        if (PossessionHUD.Instance != null) PossessionHUD.Instance.Show(target);
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.BindActor(target);
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Possessed);

        Debug.Log("[Possession] Possessed " + target.displayName);
        OnPossessionStarted?.Invoke(target);
        nextPossessionIsDeathRelay = false;
        PossessionCommitted?.Invoke(target, reason, ++possessionTransactionId);
        // Pass v1 §8.1：移除 CommitPossession 后自动 Trigger Bullet Time（BT 改为 E 手动）。
    }

    private void DetachCurrentBodyForSwitch()
    {
        MonsterActor oldBody = CurrentBody;
        CurrentBody = null;
        if (oldBody != null)
        {
            if (oldBody.Combat != null) oldBody.Combat.RemoveLooseTags(this);
            oldBody.SetController(NullController.Instance);
            oldBody.OnUnpossessed();
            if (oldBody.IsBossBattleReserveBody && oldBody.currentHealth > 0f)
                oldBody.ReturnToBossBattleReserve();
            else
                oldBody.BeginDisappearing();
        }

        if (soul != null)
        {
            soul.DetachFromPossessionAnchor();
            soul.SetSuppressed(false);
            if (oldBody != null) soul.PlaceInFreeSoulForm(oldBody.transform.position);
            if (soul.Combat != null) soul.Combat.RemoveLooseTags(this);
        }

        if (PossessionHUD.Instance != null) PossessionHUD.Instance.Hide();
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.UnbindActor();
        SetCameraTarget(soul != null ? soul.transform : null);
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Soul);
    }

    private void CommitRelease(bool recycleBody, bool startCooldown, PossessionEndReason reason = PossessionEndReason.VoluntaryRelease)
    {
        MonsterActor oldBody = CurrentBody;
        CurrentBody = null;
        State = SwitchState.Releasing;
        StopBulletTime();

        if (soul != null && soul.Combat != null) soul.Combat.RemoveLooseTags(this);
        if (oldBody != null && oldBody.Combat != null) oldBody.Combat.RemoveLooseTags(this);

        if (oldBody != null)
        {
            oldBody.SetController(NullController.Instance);
            oldBody.OnUnpossessed();
            if (recycleBody)
            {
                if (oldBody.IsBossBattleReserveBody && oldBody.currentHealth > 0f)
                    oldBody.ReturnToBossBattleReserve();
                else
                    oldBody.BeginDisappearing();
            }
        }

        if (soul != null)
        {
            soul.DetachFromPossessionAnchor();
            soul.PlaceInFreeSoulForm(oldBody != null ? oldBody.transform.position : soul.transform.position);
            soul.SetPossessionFlight(false);
            soul.SetSuppressed(false);
        }
        else if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.SetSoulActive(true);
        }

        SetCameraTarget(soul != null ? soul.transform : null);
        if (PossessionHUD.Instance != null) PossessionHUD.Instance.Hide();
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.UnbindActor();
            PlayerHealth.Instance.maxHealth = PlayerHealth.Instance.soulMaxHealth;
            PlayerHealth.Instance.UpdateHealthUI();
        }
        if (!handlingGameOver && GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Soul);

        State = SwitchState.Idle;
        if (startCooldown) CooldownRemaining = possessCooldown;
        Debug.Log("[Possession] Returned to soul form.");
        OnPossessionEnded?.Invoke();
        OnPossessionEndedEx?.Invoke(reason);
    }

    private void CancelFlightToSoul()
    {
        if (reservedBody != null) reservedBody.CancelPossessionReservation();
        reservedBody = null;
        State = SwitchState.Idle;
        if (soul != null)
        {
            soul.SetPossessionFlight(false);
            soul.SetSuppressed(false);
        }
        SetCameraTarget(soul != null ? soul.transform : null);
        if (!handlingGameOver && GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Soul);
    }

    private void StopBulletTime()
    {
        BulletTimeController controller = BulletTimeController.Instance;
        if (controller == null) return;

        controller.Stop(State == SwitchState.Possessing
            ? GameManager.GameState.Possessed
            : GameManager.GameState.Soul);
    }

    private static void SetCameraTarget(Transform target)
    {
        if (CameraDirector.Instance != null) CameraDirector.Instance.Target = target;
    }

    public void NotifyBodyDied()
    {
        if (State != SwitchState.Possessing || CurrentBody == null) return;

        MonsterActor dead = CurrentBody;
        nextPossessionIsDeathRelay = true;
        Debug.Log("[Possession] Possessed body died.");
        CommitRelease(recycleBody: true, startCooldown: false, PossessionEndReason.BodyDied);
        // Pass v1 §7.4：Body 死亡作为失败惩罚，锁 bodyDeathPossessionLock 秒（独立于主动 F 离身，后者无额外 CD）。
        CooldownRemaining = bodyDeathPossessionLock;
        OnBodyDiedWhilePossessing?.Invoke(dead);
    }

    public void OnGameOver()
    {
        handlingGameOver = true;
        bossBattleSwitchMode = false;
        if (flyRoutine != null)
        {
            StopCoroutine(flyRoutine);
            flyRoutine = null;
        }
        StopBulletTime();
        if (State == SwitchState.Possessing) CommitRelease(recycleBody: false, startCooldown: false, PossessionEndReason.SystemReset);
        else if (State == SwitchState.Flying) CancelFlightToSoul();
        State = SwitchState.Idle;
    }
}
