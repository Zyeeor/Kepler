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
    private float decayTimer;
    private PlayerCombat combat;
    private Rigidbody rb;
    private MonoBehaviour[] soulComponents;
    private Renderer[] soulRenderers;
    private Collider[] soulColliders;
    private CameraFollow cameraFollow;
    private CameraTarget cameraTarget;

    void Awake()
    {
        Instance = this;
        combat = GetComponent<PlayerCombat>();
        rb = GetComponent<Rigidbody>();
        soulComponents = GetComponents<MonoBehaviour>();
        soulRenderers = GetComponentsInChildren<Renderer>(true);
        soulColliders = GetComponentsInChildren<Collider>(true);
        cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
        cameraTarget = FindObjectOfType<CameraTarget>();
    }

    void Start()
    {
        currentHealth = soulMaxHealth;
        maxHealth = soulMaxHealth;
        UpdateHealthUI();
    }

    void Update()
    {
        // 灵魂自然衰减；附身/飞行期间暂停（PossessionManager 状态非 Idle 时不衰减，
        // 避免附身中灵魂被误判死亡）
        var pm = PossessionManager.Instance;
        if (pm != null && pm.State != PossessionManager.SwitchState.Idle) return;
        decayTimer += Time.deltaTime;
        if (decayTimer >= decayInterval)
        {
            decayTimer -= decayInterval;
            float decayAmount = soulMaxHealth * healthDecayPercent;
            TakeDamage(decayAmount);
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
        if (rb != null) { if (!active) { rb.velocity = Vector3.zero; rb.isKinematic = true; } else { rb.isKinematic = false; } }
    }

    // ── 附身 HUD 已迁至 PossessionManager（Show/Hide 统一走 PossessionHUD.Instance） ──

    public void TakeDamage(float amount)
    {
        var combatState = GetComponent<CombatAbilityComponent>();
        if (combatState != null) amount = combatState.ModifyIncomingDamage(amount);
        currentHealth -= amount;
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
        float ratio = maxHealth > 0 ? currentHealth / maxHealth : 0;

        if (healthSlider != null) { healthSlider.maxValue = maxHealth; healthSlider.value = currentHealth; }
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
