using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PossessionHUD : MonoBehaviour
{
    public static PossessionHUD Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject panelRoot;
    public TMP_Text enemyNameText;
    public Slider healthSlider;
    public Image sliderFill;
    public TMP_Text abilityQText;
    public TMP_Text abilityWText;
    public TMP_Text abilityRText;
    public Gradient healthGradient;

    private IActor trackedActor;
    private readonly List<AbilitySlotInfo> abilitySlots = new List<AbilitySlotInfo>();

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void Update()
    {
        if (trackedActor == null) return;
        if (healthSlider != null)
        {
            healthSlider.value = trackedActor.CurrentHealth;
            // 仅在渐变显式配置过（>=3 个颜色键）时才覆盖 Fill 颜色；
            // 默认 gradient（2 个白色键）不覆盖，血条颜色由 Fill 的 Image 自身控制。
            if (sliderFill != null && healthGradient != null && healthGradient.colorKeys.Length >= 3)
            {
                float ratio = trackedActor.MaxHealth > 0 ? trackedActor.CurrentHealth / trackedActor.MaxHealth : 0;
                sliderFill.color = healthGradient.Evaluate(ratio);
            }
        }
    }

    /// <summary>附身 HUD 只读 IActor 视图（不依赖具体 Enemy/PlayerHealth 类型）。</summary>
    public void Show(IActor actor)
    {
        if (actor == null) return;
        trackedActor = actor;
        if (panelRoot != null) panelRoot.SetActive(true);
        if (enemyNameText != null) enemyNameText.text = actor.DisplayName;
        if (healthSlider != null) { healthSlider.maxValue = actor.MaxHealth; healthSlider.value = actor.CurrentHealth; }
        actor.FillAbilitySlots(abilitySlots);
        string basicName = abilitySlots.Count > 0 ? abilitySlots[0].Name : "普攻";
        string skillName = abilitySlots.Count > 1 ? abilitySlots[1].Name : "技能";
        if (abilityQText != null) abilityQText.text = "左键 - " + basicName;
        if (abilityWText != null) abilityWText.text = "右键 - " + skillName;
        if (abilityRText != null) abilityRText.text = "Space/F - 脱离附身";
    }

    public void Hide()
    {
        trackedActor = null;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}