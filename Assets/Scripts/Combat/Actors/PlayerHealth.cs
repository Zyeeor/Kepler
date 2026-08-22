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

    [Header("Burn Effect")]
    [Tooltip("血条末端燃烧特效（附身态显示，灵魂态隐藏）。挂在血条 Canvas 下、带 Image 的节点。\n" +
             "火苗自身的跳动/摇曳/闪烁由其上的 UIFlameFlicker 组件负责（缺失时自动添加）。\n" +
             "位置：你在 RectTransform 上摆的 anchoredPosition 就是火苗的基准位置（满血时所在）；\n" +
             "运行时只把 anchor.x 随血量比例移动，火苗从该位置随血流逝沿血条左移，其余配置不覆盖。")]
    public RectTransform burnEffect;
    [Tooltip("勾选后血量为 0 时也隐藏火苗（血条空了不该还在烧）。")]
    public bool hideBurnEffectWhenEmpty = true;

    public float maxHealth; // 灵魂当前上限（附身切换时由 PossessionManager 同步）
    private float decayTimer;
    private PlayerCombat combat;
    private MonoBehaviour[] soulComponents;
    private Renderer[] soulRenderers;
    private Collider[] soulColliders;
    private ActorVisualFx visualFx;

    // 附身中绑定的 IActor（让 PlayerHealth.healthSlider 切到 Body 池）。
    // null ⇒ Soul 池显示；非 null ⇒ 由 SyncBoundActorToUI 每帧同步 Body 池。
    private IActor _trackedActor;
    private float _lastBurnAnchorX = -1f; // 火苗锚点缓存：血量比例未变时不重复触发布局

    void Awake()
    {
        Instance = this;
        combat = GetComponent<PlayerCombat>();
        soulComponents = GetComponents<MonoBehaviour>();
        soulRenderers = GetComponentsInChildren<Renderer>(true);
        soulColliders = GetComponentsInChildren<Collider>(true);
        visualFx = GetComponent<ActorVisualFx>();
        if (visualFx == null) visualFx = gameObject.AddComponent<ActorVisualFx>();
        visualFx.RefreshRenderers();

        // 火苗跳动交给 UIFlameFlicker；未手动挂时自动补一个（同 ActorVisualFx 的兜底写法）
        if (burnEffect != null && burnEffect.GetComponent<UIFlameFlicker>() == null)
            burnEffect.gameObject.AddComponent<UIFlameFlicker>();
    }

    void Start()
    {
        currentHealth = soulMaxHealth;
        maxHealth = soulMaxHealth;
        UpdateHealthUI();
    }

    void Update()
    {
        // 1) 数据源每帧同步：附身中读 _trackedActor（Body 池），Soul 池时无操作。
        SyncBoundActorToUI();

        // 2) 燃烧特效位置/显隐/呼吸动画（附身态显示，灵魂态隐藏）。
        UpdateBurnEffect();

        // 3) 灵魂自然衰减；附身/飞行期间暂停（PossessionManager 状态非 Idle 时不衰减，
        // 避免附身中灵魂被误判死亡）。
        var pm = PossessionManager.Instance;
        if (pm != null && pm.State != PossessionManager.SwitchState.Idle) return;
        decayTimer += Time.deltaTime;
        if (decayTimer >= decayInterval)
        {
            decayTimer -= decayInterval;
            float decayAmount = soulMaxHealth * healthDecayPercent;
            TakeDamage(decayAmount, playHitFlash: false);
        }
    }

    /// <summary>
    /// 血条末端燃烧特效：附身态显示并跟随当前血量位置，灵魂态隐藏。
    ///
    /// 本方法只负责【位置 + 显隐】。
    ///
    /// 位置用锚点驱动：把火苗 anchor.x 设为血量比例 ratio，火苗正好落在填充条右端
    /// （当前血量的最右侧）——即"正在燃烧"的边缘；随血量下降沿血条向左退，形成烧血感。
    /// 用 anchor 而非像素计算：自适应任意血条宽度 / CanvasScaler 缩放，且与 fillRect
    /// 右缘严格对齐（Slider 正是用 fillRect.anchorMax.x = ratio 驱动填充）。
    ///
    /// 火苗自身的跳动/摇曳/闪烁由 burnEffect 上的 UIFlameFlicker 负责（写 localScale /
    /// localRotation / Image.color），两者操作不同属性，不会互相覆盖。
    /// </summary>
    void UpdateBurnEffect()
    {
        if (burnEffect == null || healthSlider == null) return;

        float ratio = healthSlider.maxValue > 0f ? Mathf.Clamp01(healthSlider.value / healthSlider.maxValue) : 0f;

        // 显隐：附身态显示；血量归零时按开关隐藏（空血条不该还在烧）
        bool shouldShow = _trackedActor != null && (!hideBurnEffectWhenEmpty || ratio > 0.0001f);
        if (burnEffect.gameObject.activeSelf != shouldShow)
            burnEffect.gameObject.SetActive(shouldShow);
        if (!shouldShow) return;

        RectTransform fillRect = healthSlider.fillRect;
        RectTransform fillArea = fillRect != null ? fillRect.parent as RectTransform : null;
        if (fillArea == null) return;

        if (burnEffect.parent != fillArea)
            burnEffect.SetParent(fillArea, false);

        // Slider 反向填充（Right To Left / Top To Bottom）时末端在另一侧
        bool reversed = healthSlider.direction == Slider.Direction.RightToLeft
                     || healthSlider.direction == Slider.Direction.TopToBottom;
        float ax = reversed ? (1f - ratio) : ratio;

        // 只把锚点 x 随血量比例移动（保留你配置的 anchor.y / anchoredPosition / pivot）：
        // 火苗从你在 RectTransform 摆好的基准位置出发，随血流逝沿血条移动。
        if (Mathf.Abs(_lastBurnAnchorX - ax) > 0.0001f)
        {
            _lastBurnAnchorX = ax;
            burnEffect.anchorMin = new Vector2(ax, burnEffect.anchorMin.y);
            burnEffect.anchorMax = new Vector2(ax, burnEffect.anchorMax.y);
        }
        // 不覆盖 anchoredPosition —— 它就是你配置的基准位置。
    }

    /// <summary>
    /// 附身中绑定被附身怪：把 PlayerHealth.healthSlider 数据源切到 Body 池。
    /// 由 PossessionHUD.Show 间接调用（亦可被外部脚本直接调用）。
    /// </summary>
    public void BindActor(IActor actor)
    {
        _trackedActor = actor;
        // Soul 危险面板在附身中与 Body 生存力无关，立即关闭避免误导。
        if (dangerPanel != null && dangerPanel.activeSelf) dangerPanel.SetActive(false);
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
        if (_trackedActor == null || healthSlider == null) return;
        float max = _trackedActor.MaxHealth;
        float cur = _trackedActor.CurrentHealth;
        if (Mathf.Abs(healthSlider.maxValue - max) > 0.001f) healthSlider.maxValue = max;
        if (Mathf.Abs(healthSlider.value - cur) > 0.001f) healthSlider.value = cur;
        if (dangerPanel != null && dangerPanel.activeSelf) dangerPanel.SetActive(false);
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

    public void TakeDamage(float amount, bool playHitFlash = true)
    {
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
        if (playHitFlash && visualFx != null) visualFx.PlayHitFlash();
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
        Debug.Log("Player died!");
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.GameOver);
    }

    /// <summary>刷新玩家血条/危险UI（PossessionManager 附身切换时也调用）。</summary>
    public void UpdateHealthUI()
    {
        if (_trackedActor != null)
        {
            // 附身中：滑块由 SyncBoundActorToUI 每帧写，这里只负责关掉 Soul 危险面板。
            if (dangerPanel != null && dangerPanel.activeSelf) dangerPanel.SetActive(false);
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

        // Danger panel alpha based on health ratio
        if (dangerPanel != null)
        {
            bool show = ratio <= dangerThreshold;
            dangerPanel.SetActive(show);
            if (show && dangerPanelGroup != null)
            {
                float alpha = 1f - (ratio / dangerThreshold);
                dangerPanelGroup.alpha = alpha;
            }
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
