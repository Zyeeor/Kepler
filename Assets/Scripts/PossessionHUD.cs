using UnityEngine;
using UnityEngine.UI;

public class PossessionHUD : MonoBehaviour
{
    public static PossessionHUD Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject panelRoot;
    public Text enemyNameText;
    public Slider healthSlider;
    public Image sliderFill;
    public Text abilityQText;
    public Text abilityWText;
    public Text abilityRText;
    public Gradient healthGradient;

    private Enemy trackedEnemy;

    void Awake()
    {
        Instance = this;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void Update()
    {
        if (trackedEnemy == null) return;
        if (healthSlider != null)
        {
            healthSlider.value = trackedEnemy.currentHealth;
            if (sliderFill != null && healthGradient != null)
            {
                float ratio = trackedEnemy.maxHealth > 0 ? trackedEnemy.currentHealth / trackedEnemy.maxHealth : 0;
                sliderFill.color = healthGradient.Evaluate(ratio);
            }
        }
    }

    public void Show(Enemy enemy)
    {
        if (enemy == null) return;
        trackedEnemy = enemy;
        if (panelRoot != null) panelRoot.SetActive(true);
        if (enemyNameText != null) enemyNameText.text = enemy.displayName;
        if (healthSlider != null) { healthSlider.maxValue = enemy.maxHealth; healthSlider.value = enemy.currentHealth; }
        string basicName = "普攻";
        string skillName = "技能";
        if (enemy.basicAbilities.Count > 0 && enemy.basicAbilities[0] != null) basicName = enemy.basicAbilities[0].abilityName;
        if (enemy.skillAbilities.Count > 0 && enemy.skillAbilities[0] != null) skillName = enemy.skillAbilities[0].abilityName;
        if (abilityQText != null) abilityQText.text = "左键 - " + basicName;
        if (abilityWText != null) abilityWText.text = "右键 - " + skillName;
        if (abilityRText != null) abilityRText.text = "R - 脱离附身";
    }

    public void Hide()
    {
        trackedEnemy = null;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}