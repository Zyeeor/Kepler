using System.Collections;
using UnityEngine;

/// <summary>
/// 附身用例编排器（场景级服务，随场景重建）：统一承载换身状态机与附身全流程编排。
///
/// 核心架构语义：附身 = Controller 切换 —— body.SetController(PlayerController.Instance) 一行；
/// 灵魂与身体共享 PlayerController 输入，附身通过 Controller 挂接目标实现。
/// State 机：Idle → Flying → Possessing → (Releasing →) Idle
/// 生命周期要点：飞行协程由本服务持有——GameOver 时由 GameManager 显式调用 OnGameOver 终止，
/// 避免 timeScale=0 下协程用 unscaledDeltaTime 继续推进而覆盖 GameOver 状态。
/// </summary>
public class PossessionManager : MonoBehaviour
{
    public static PossessionManager Instance { get; private set; }

    [Header("Tuning（附身飞行/偏移/冷却/衰减参数，Inspector 唯一调参入口）")]
    public float possessFlySpeedMultiplier = 5f;
    public float possessYOffset = 0.5f;
    public float possessCooldown = 3f;
    public float minPossessTime = 1f;
    public float possessionDecayPercent = 0.05f;
    public float decayInterval = 1f;

    // 换身状态机（ENG-SWITCH-001）：
    public enum SwitchState { Idle, Flying, Possessing, Releasing }
    public SwitchState State { get; private set; }
    public MonsterActor CurrentBody { get; private set; }
    public float CooldownRemaining { get; private set; }

    // 事件（风格对齐房间模块 event Action）：
    public event System.Action<MonsterActor> OnPossessionStarted;
    public event System.Action OnPossessionEnded;
    public event System.Action<MonsterActor> OnBodyDiedWhilePossessing;

    private Coroutine flyRoutine;
    private float possessStartTime;
    private float possessionDecayTimer;
    private SoulActor soul;

    // ── 生命周期 ──

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        State = SwitchState.Idle;
        soul = FindObjectOfType<SoulActor>();
    }

    void Update()
    {
        // 冷却计时
        if (CooldownRemaining > 0f)
            CooldownRemaining -= Time.deltaTime;

        // 附身衰减：CurrentBody 持续掉血 → 血尽 force 释放
        if (State == SwitchState.Possessing && CurrentBody != null)
        {
            possessionDecayTimer += Time.deltaTime;
            if (possessionDecayTimer >= decayInterval)
            {
                possessionDecayTimer -= decayInterval;
                float decayAmount = CurrentBody.maxHealth * possessionDecayPercent;
                CurrentBody.currentHealth -= decayAmount;
                if (CurrentBody.currentHealth <= 0)
                {
                    CurrentBody.currentHealth = 0;
                    Debug.Log("[PossessionManager] Possessed body died - force release");
                    // 统一走 NotifyBodyDied：先存 dead 引用 → force release → 事件传 dead
                    // （避免 RequestRelease 已清空 CurrentBody 导致事件参数为 null）
                    NotifyBodyDied();
                }
            }
        }
    }

    // ── 用例入口 ──

    /// <summary>玩家发起附身（SoulActor.ExecuteButtons.Possess → 此处）。</summary>
    public void RequestPossess(Ray aimRay)
    {
        if (State != SwitchState.Idle)
        {
            Debug.Log("[Possess] State busy: " + State);
            return;
        }
        if (CooldownRemaining > 0f)
        {
            Debug.Log("[Possess] Cooldown: " + CooldownRemaining.ToString("F1") + "s remaining");
            return;
        }

        RaycastHit hit;
        if (!Physics.Raycast(aimRay, out hit, 100f)) return;
        var body = hit.collider.GetComponentInParent<MonsterActor>();
        string reason;
        if (!ValidateTarget(body, out reason))
        {
            if (body != null && !string.IsNullOrEmpty(reason)) Debug.Log(reason);
            return;
        }

        State = SwitchState.Flying;
        flyRoutine = StartCoroutine(FlyAndCommitRoutine(body));
    }

    /// <summary>脱离附身（玩家按键 / 身体血尽 force:true）。</summary>
    public void RequestRelease(bool force)
    {
        if (State != SwitchState.Possessing) return;
        if (!force && Time.time - possessStartTime < minPossessTime)
        {
            Debug.Log("[Possess] Cannot unpossess yet — " + (minPossessTime - (Time.time - possessStartTime)).ToString("F1") + "s remaining");
            return;
        }
        CommitRelease(destroyBody: true);
    }

    // ── 内部 ──

    private bool ValidateTarget(MonsterActor m, out string reason)
    {
        reason = null;
        if (m == null) { reason = "Target is not a MonsterActor"; return false; }
        // 合法性：BodyState ∈ {Weakened, Downed}（ENG-POSS-001 合法性扩展点）
        if (!m.isWeakened && !m.isDowned) { reason = "Enemy is not downed yet, keep attacking!"; return false; }
        if (m.isPossessed) { reason = "Enemy already possessed"; return false; }
        return true;
    }

    private IEnumerator FlyAndCommitRoutine(MonsterActor target)
    {
        // 飞行前灵魂回血：接管身体前把灵魂 HP 补满，保证附身切换时的血量语义
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.currentHealth = PlayerHealth.Instance.soulMaxHealth;
            PlayerHealth.Instance.UpdateHealthUI();
        }

        Vector3 targetPos = target.transform.position;
        if (soul != null) targetPos.y = soul.transform.position.y;
        else targetPos.y = 0f;
        float flySpeed = (PlayerHealth.Instance != null ? PlayerHealth.Instance.SoulMoveSpeedForFly : 5f) * possessFlySpeedMultiplier;

        while (soul != null && Vector3.Distance(soul.transform.position, targetPos) > 0.3f)
        {
            if (target == null) { State = SwitchState.Idle; flyRoutine = null; yield break; }
            targetPos = target.transform.position;
            targetPos.y = soul.transform.position.y;
            Vector3 dir = (targetPos - soul.transform.position).normalized;
            soul.transform.position = Vector3.MoveTowards(soul.transform.position, targetPos, flySpeed * Time.unscaledDeltaTime);
            soul.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            yield return null;
        }

        if (target == null) { State = SwitchState.Idle; flyRoutine = null; yield break; }
        State = SwitchState.Idle;
        flyRoutine = null;
        CommitPossession(target);
    }

    /// <summary>接管身体：Controller 切换为附身核心，随后激活身体/切相机/同步全局状态。</summary>
    private void CommitPossession(MonsterActor target)
    {
        if (target == null) return;
        if (State == SwitchState.Possessing && CurrentBody != null) CommitRelease(destroyBody: true);

        CurrentBody = target;
        State = SwitchState.Possessing;
        possessionDecayTimer = 0f;
        possessStartTime = Time.time;

        // ① 灵魂抑制（Controller→Null + collider off + rb kinematic + 跟随模式）
        if (soul != null) soul.SetSuppressed(true);
        else if (PlayerHealth.Instance != null) PlayerHealth.Instance.SetSoulActive(false);

        // ② 身体激活（回血回韧性 / tag→Player / 颜色 / 动画）
        target.OnPossessed();

        // ③ 附身的架构本质：Controller 切换（AI → 玩家输入）
        target.SetController(PlayerController.Instance);

        // ④ 相机切换到身体
        CameraFollow cf = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        if (cf != null) cf.target = target.transform;
        CameraTarget ct = FindObjectOfType<CameraTarget>();
        if (ct != null) ct.player = target.transform;

        // ⑤ 被动积累：MonsterActor 经 Enemy 壳类路由到 PlayerPassiveManager
        if (PlayerPassiveManager.Instance != null && target is Enemy)
            PlayerPassiveManager.Instance.OnEnemyPossessed(target as Enemy);

        // ⑥ HUD / 全局状态（HUD 走 IActor 只读视图，不依赖具体类型）
        if (PossessionHUD.Instance != null) PossessionHUD.Instance.Show(target);
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Possessed);

        Debug.Log("[PossessionManager] POSSESSED " + target.displayName);
        OnPossessionStarted?.Invoke(target);
    }

    /// <summary>归还身体：Controller 切回 + 灵魂恢复；destroyBody=true 时销毁身体（对象池唯一钩子点）。</summary>
    private void CommitRelease(bool destroyBody)
    {
        MonsterActor oldBody = CurrentBody;
        State = SwitchState.Idle;
        CurrentBody = null;

        // ① 身体 Controller → Null（停用输入；若销毁则先解除引用）
        if (oldBody != null)
        {
            oldBody.SetController(NullController.Instance);
            oldBody.OnUnpossessed();
        }

        // ② 灵魂恢复（Controller→PlayerController + collider/rb 恢复）
        if (soul != null) soul.SetSuppressed(false);
        else if (PlayerHealth.Instance != null) PlayerHealth.Instance.SetSoulActive(true);

        // ③ 相机切回灵魂
        CameraFollow cf = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        Transform restore = soul != null ? soul.transform : (PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null);
        if (cf != null && restore != null) cf.target = restore;
        CameraTarget ct = FindObjectOfType<CameraTarget>();
        if (ct != null && restore != null) ct.player = restore;

        // ④ 销毁身体（对象池唯一钩子点，后续可在此换回池实现）
        if (destroyBody && oldBody != null)
        {
            var go = oldBody.gameObject;
            if (go != null) Destroy(go);
        }

        // ⑤ 冷却 / HUD / 全局状态
        CooldownRemaining = possessCooldown;
        if (PossessionHUD.Instance != null) PossessionHUD.Instance.Hide();
        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.maxHealth = PlayerHealth.Instance.soulMaxHealth;
            PlayerHealth.Instance.UpdateHealthUI();
        }
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Soul);

        Debug.Log("[PossessionManager] Unpossessed - soul form restored");
        OnPossessionEnded?.Invoke();
    }

    /// <summary>被附身身体死亡（由 MonsterActor.TakeDamage 附身分支调用）。</summary>
    public void NotifyBodyDied()
    {
        if (State != SwitchState.Possessing || CurrentBody == null) return;
        Debug.Log("[PossessionManager] Possessed body died - returning to soul form");
        MonsterActor dead = CurrentBody;
        RequestRelease(force: true);
        OnBodyDiedWhilePossessing?.Invoke(dead);
    }

    /// <summary>GameOver 防御（GameManager 状态机 GameOver 分支调用）：
    /// 显式停止飞行协程；若正处于附身态，同步恢复灵魂形态，避免 timeScale=0 下面板与附身状态并存。</summary>
    public void OnGameOver()
    {
        if (flyRoutine != null) { StopCoroutine(flyRoutine); flyRoutine = null; }
        if (State == SwitchState.Possessing) CommitRelease(destroyBody: false);
        State = SwitchState.Idle;
    }
}
