using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Soul State")]
    public float soulMaxHealth = 100f;
    public float currentHealth;

    [Header("Health Decay")]
    public float healthDecayPercent = 0.01f;
    public float decayInterval = 1f;

    [Header("UI")]
    public Slider healthSlider;
    public Image sliderFillImage;
    public Gradient healthGradient;


    [Header("Danger UI")]
    [Tooltip("Panel shown when health is low. Alpha driven by health ratio.")]
    public GameObject dangerPanel;
    [Tooltip("CanvasGroup on the danger panel for alpha control.")]
    public CanvasGroup dangerPanelGroup;
    [Tooltip("Health ratio below which danger panel starts appearing.")]
    [Range(0f, 1f)] public float dangerThreshold = 0.35f;

    public float maxHealth; // 灵魂当前上限（附身切换时由 PossessionManager 同步）
    // 复活加成基准：soulMaxHealth 会被复活倍数改写，且 DDOL 灵魂跨局复用，
    // 必须保留策划配置的原始上限，否则多次复活/多局之间会不断累积污染。
    private float authoredSoulMaxHealth = -1f;
    private float decayTimer;
    private bool isDead; // 死亡幂等：0 HP 后重复伤害不再触发 Die（防主菜单衰减重复 GameOver 污染下一局）
    private PlayerCombat combat;
    private MonoBehaviour[] soulComponents;
    private Renderer[] soulRenderers;
    private Collider[] soulColliders;
    private ActorVisualFx visualFx;

    // 附身中绑定的 IActor（让 PlayerHealth.healthSlider 切到 Body 池）。
    // null ⇒ Soul 池显示；非 null ⇒ 由 SyncBoundActorToUI 每帧同步 Body 池。
    private IActor _trackedActor;
    private float _lastBurnAnchorX = -1f; // 火苗锚点缓存：血量比例未变时不重复触发布局
    private float _nextHealthSliderLookupTime;
    private float _nextHurtAudioTime;

    void Awake()
    {
        Instance = this;
        if (authoredSoulMaxHealth < 0f) authoredSoulMaxHealth = soulMaxHealth;
        combat = GetComponent<PlayerCombat>();
        soulComponents = GetComponents<MonoBehaviour>();
        soulRenderers = GetComponentsInChildren<Renderer>(true);
        soulColliders = GetComponentsInChildren<Collider>(true);
        visualFx = GetComponent<ActorVisualFx>();
        if (visualFx == null) visualFx = gameObject.AddComponent<ActorVisualFx>();
        visualFx.RefreshRenderers();
        ResolveHealthSlider();
    }

    void Start()
    {
        ResolveHealthSlider();
        currentHealth = soulMaxHealth;
        maxHealth = soulMaxHealth;
        UpdateHealthUI();
    }

    void Update()
    {
        // 1) 数据源每帧同步：附身中读 _trackedActor（Body 池），Soul 池时无操作。
        SyncBoundActorToUI();


        // 3) 灵魂自然衰减；附身/飞行期间暂停（PossessionManager 状态非 Idle 时不衰减，
        // 避免附身中灵魂被误判死亡）。
        var pm = PossessionManager.Instance;
        if (pm != null && pm.State != PossessionManager.SwitchState.Idle) return;
        // 灵魂衰减是对局内机制：主菜单/展示态（无进行中对局）或已死亡时不衰减——
        // DDOL 灵魂在主菜单持续掉血至 0 会重复触发 Die→GameOver，污染下一局（开场载体附身被拒）。
        // 注意：编辑器直接 Play 的兜底路径（InitWorldSeed）不置 HasActiveRun，该调试路径下灵魂不再衰减。
        var session = RunSession.Instance;
        if (session == null || !session.HasActiveRun || isDead) return;
        // Pre-Combat gate (Pass v1): Soul HP decay only starts after the first Possession
        // of the Opening Carrier. Boss mode bypasses this gate.
        if (RunSpawnDirector.Instance != null && !RunSpawnDirector.Instance.CombatStarted
            && !session.IsBossMode)
            return;
        decayTimer += Time.deltaTime;
        if (decayTimer >= decayInterval)
        {
            decayTimer -= decayInterval;
            float decayAmount = soulMaxHealth * healthDecayPercent;
            TakeDamage(decayAmount, playHitFlash: false);
        }
    }

    /// <summary>
    /// 附身中绑定被附身怪：把 PlayerHealth.healthSlider 数据源切到 Body 池。
    /// 由 PossessionHUD.Show 间接调用（亦可被外部脚本直接调用）。
    /// </summary>
    public void BindActor(IActor actor)
    {
        _trackedActor = actor;
        UpdateHealthUI();
    }


    /// <summary>解绑，恢复 Soul 池为唯一数据源。由 PossessionHUD.Hide / 切身 / Body Fatal 等调用。</summary>
    public void UnbindActor()
    {
        _trackedActor = null;
        UpdateHealthUI();
    }

    /// <summary>
    /// 附身中每帧把 Slider 同步到 Body 池。Soul 池仍由 UpdateHealthUI 事件驱动。
    /// </summary>
    void SyncBoundActorToUI()
    {
        ResolveHealthSlider();
        if (_trackedActor == null || healthSlider == null) return;
        float max = _trackedActor.MaxHealth;
        float cur = _trackedActor.CurrentHealth;
        if (Mathf.Abs(healthSlider.maxValue - max) > 0.001f) healthSlider.maxValue = max;
        if (Mathf.Abs(healthSlider.value - cur) > 0.001f) healthSlider.value = cur;
        if (sliderFillImage != null && healthGradient != null)
            sliderFillImage.color = healthGradient.Evaluate(max > 0f ? cur / max : 0f);
        UpdateDangerPanel(max > 0f ? cur / max : 0f);
    }


    private void ResolveHealthSlider()
    {
        if (healthSlider != null) return;
        if (Time.unscaledTime < _nextHealthSliderLookupTime) return;
        _nextHealthSliderLookupTime = Time.unscaledTime + 1f;

        Slider[] sliders = FindObjectsOfType<Slider>(true);
        for (int i = 0; i < sliders.Length; i++)
        {
            Slider candidate = sliders[i];
            if (candidate == null || !candidate.isActiveAndEnabled || candidate.gameObject.name != "HealthSlider") continue;

            Canvas canvas = candidate.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) continue;

            healthSlider = candidate;
            if (sliderFillImage == null && healthSlider.fillRect != null)
                sliderFillImage = healthSlider.fillRect.GetComponent<Image>();
            return;
        }
    }


    /// <summary>
    /// 灵魂组件启停（PossessionManager 无 SoulActor 时的兜底路径）。
    /// </summary>
    public void SetSoulActive(bool active)
    {
        foreach (var r in soulRenderers) if (r != null) r.enabled = active;
        foreach (var c in soulColliders) if (c != null) c.enabled = active;
        foreach (var comp in soulComponents)
        {
            if (comp == null || comp == this) continue;
            comp.enabled = active;
        }
    }

    // ── 附身 HUD 已迁至 PossessionHUD（Show/Hide 统一走 PossessionHUD.Instance） ──

    public void TakeDamage(float amount, bool playHitFlash = true, float hurtAudioInterval = 0f)
    {
        // Victory Epilogue uses realtime presentation and must not be interrupted by unscaled combat damage.
        // This only applies while the shared epilogue controller is actively presenting; normal Failure remains unchanged.
        if (VictoryEpilogueController.IsPlaying) return;

        // While possessing a body the soul is suppressed and must not take hit flash/damage;
        // combat targets the possessed MonsterActor instead.
        var soul = GetComponent<SoulActor>();
        if (soul != null && soul.IsSuppressed) return;
        var pm = PossessionManager.Instance;
        if (pm != null && pm.State == PossessionManager.SwitchState.Possessing) return;


        var combatState = GetComponent<CombatAbilityComponent>();
        if (combatState != null) amount = combatState.ModifyIncomingDamage(amount);
        if (amount <= 0f) return;
        currentHealth -= amount;
        if (playHitFlash)
        {
            if (visualFx != null) visualFx.PlayHitFlash();
            if (hurtAudioInterval <= 0f || Time.time >= _nextHurtAudioTime)
            {
                CombatAudioManager.Play(nameof(SfxId.PlayerHurt), transform.position);
                if (hurtAudioInterval > 0f)
                    _nextHurtAudioTime = Time.time + hurtAudioInterval;
            }
        }
        if (currentHealth <= 0) { currentHealth = 0; Die(); }
        UpdateHealthUI();
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealthUI();
    }

    void Die()
    {
        if (isDead) return; // 幂等：重复死亡不重复触发 GameOver（防 TimeScaleManager 重复 Push 冻结时间）
        isDead = true;
        Debug.Log("Player died!");
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.GameOver);
    }

    /// <summary>新局重置：回满 HP 并清死亡标记。DDOL 灵魂跨场景/跨局复用，Start 不会重跑，必须显式复位。</summary>
    public void ResetHealth()
    {
        isDead = false;
        _nextHurtAudioTime = 0f;
        // 上限回到策划配置值：上一局的复活加成不得带入新局
        if (authoredSoulMaxHealth > 0f) soulMaxHealth = authoredSoulMaxHealth;
        currentHealth = soulMaxHealth;
        maxHealth = soulMaxHealth;
        UpdateHealthUI();
    }

    /// <summary>
    /// 复活：按本局累计加成提升灵魂上限并回满，同时清除死亡标记。
    /// totalBonus 为累计倍数（相对策划原始上限），由 GameManager.ReviveHealthBonus 提供。
    /// 注意灵魂衰减是按 soulMaxHealth 的百分比计算，上限提升后每秒衰减量同比例增加（存活时长不变，抗伤能力提升）。
    /// </summary>
    public void ApplyReviveHealthBonus(float totalBonus)
    {
        if (authoredSoulMaxHealth <= 0f) authoredSoulMaxHealth = soulMaxHealth;
        isDead = false;
        _nextHurtAudioTime = 0f;
        soulMaxHealth = authoredSoulMaxHealth * Mathf.Max(0.01f, totalBonus);
        currentHealth = soulMaxHealth;
        maxHealth = soulMaxHealth;
        UpdateHealthUI();
        Debug.Log($"[Revive] 灵魂上限 {authoredSoulMaxHealth:F0} × {totalBonus:F2} = {soulMaxHealth:F0}（已回满）。");
    }

    /// <summary>刷新玩家血条/危险UI（PossessionManager 附身切换时也调用）。</summary>
    public void UpdateHealthUI()
    {
        ResolveHealthSlider();
        if (_trackedActor != null)
        {
            SyncBoundActorToUI();
            return;
        }


        float ratio = maxHealth > 0 ? currentHealth / maxHealth : 0;

        if (healthSlider != null)
        {
            if (Mathf.Abs(healthSlider.maxValue - maxHealth) > 0.001f) healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        if (sliderFillImage != null && healthGradient != null)
            sliderFillImage.color = healthGradient.Evaluate(ratio);

        UpdateDangerPanel(ratio);
    }

    private void UpdateDangerPanel(float ratio)
    {
        if (dangerPanel == null) return;
        bool show = ratio <= dangerThreshold;
        dangerPanel.SetActive(show);
        if (show && dangerPanelGroup != null)
        {
            float alpha = Mathf.Clamp01(1f - (ratio / Mathf.Max(0.0001f, dangerThreshold)));
            dangerPanelGroup.alpha = alpha;
        }
    }


    private float SoulMoveSpeed
    {
        // 移速数据源为 PlayerPassiveManager（含被动加成）
        get { return PlayerPassiveManager.Instance != null ? PlayerPassiveManager.Instance.CurrentMoveSpeed : 5f; }
    }

    /// <summary>飞行速度基准（PossessionManager 读取，语义同 SoulMoveSpeed）。</summary>
    public float SoulMoveSpeedForFly
    {
        get { return SoulMoveSpeed; }
    }
}
