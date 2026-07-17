using UnityEngine;
using UnityEngine.UI;
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

    [Header("Possession Decay")]
    public float possessionDecayPercent = 0.05f;
    private float possessionDecayTimer;

    [Header("Possession")]
    public Enemy possessedEnemy;
    public bool isPossessing = false;
    public bool isFlyingToPossess = false;
    public float possessFlySpeedMultiplier = 3f;

    [Header("UI")]
    public Slider healthSlider;
    public Image sliderFillImage;
    public Gradient healthGradient;

    [Header("Possession UI (Fallback)")]
    public GameObject possessionHUDPanel;
    public Slider possessionHealthSlider;
    public Image possessionSliderFill;
    public Text possessionEnemyNameText;
    public Text possessionAbilityQText;
    public Text possessionAbilityWText;
    public Text possessionAbilityRText;

    private float maxHealth;
    private float decayTimer;
    private PlayerInputController input;
    private PlayerCombat combat;
    private Rigidbody rb;
    private float savedDecayTimer;
    private MonoBehaviour[] soulComponents;
    private Renderer[] soulRenderers;
    private Collider[] soulColliders;
    private GameObject dynamicHUD; // dynamically created possession HUD
    private CameraFollow cameraFollow;

    void Awake()
    {
        Instance = this;
        input = GetComponent<PlayerInputController>();
        combat = GetComponent<PlayerCombat>();
        rb = GetComponent<Rigidbody>();
        soulComponents = GetComponents<MonoBehaviour>();
        soulRenderers = GetComponentsInChildren<Renderer>(true);
        soulColliders = GetComponentsInChildren<Collider>(true);
        cameraFollow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
    }

    void Start()
    {
        currentHealth = soulMaxHealth;
        maxHealth = soulMaxHealth;
        isPossessing = false;
        isFlyingToPossess = false;
        HidePossessionHUD();
        UpdateHealthUI();
    }

    void Update()
    {
        if (!isFlyingToPossess && !isPossessing)
        {
            decayTimer += Time.deltaTime;
            if (decayTimer >= decayInterval)
            {
                decayTimer -= decayInterval;
                float decayAmount = soulMaxHealth * healthDecayPercent;
                TakeDamage(decayAmount);
            }
        }
        if (isPossessing)
        {
            if (possessedEnemy == null) { Unpossess(); return; }
            // Soul follows the possessed enemy
            transform.position = possessedEnemy.transform.position + Vector3.up * 1.5f;
            possessionDecayTimer += Time.deltaTime;
            if (possessionDecayTimer >= decayInterval)
            {
                possessionDecayTimer -= decayInterval;
                float decayAmount = possessedEnemy.maxHealth * possessionDecayPercent;
                possessedEnemy.currentHealth -= decayAmount;
                if (possessedEnemy.currentHealth <= 0)
                {
                    possessedEnemy.currentHealth = 0;
                    OnPossessedEnemyDied();
                }
            }
            UpdatePossessionHUD();
        }
    }

    public void FlyAndPossess(Enemy enemy)
    {
        if (enemy == null || flyRoutine != null) return;
        flyRoutine = StartCoroutine(FlyAndPossessRoutine(enemy));
    }

    IEnumerator FlyAndPossessRoutine(Enemy enemy)
    {
        isFlyingToPossess = true;
        Vector3 targetPos = enemy.transform.position;
        targetPos.y = transform.position.y;
        float flySpeed = SoulMoveSpeed * possessFlySpeedMultiplier;
        while (Vector3.Distance(transform.position, targetPos) > 0.3f)
        {
            if (enemy == null) { isFlyingToPossess = false; flyRoutine = null; yield break; }
            targetPos = enemy.transform.position; targetPos.y = transform.position.y;
            Vector3 dir = (targetPos - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, flySpeed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            yield return null;
        }
        isFlyingToPossess = false;
        PossessEnemy(enemy);
        flyRoutine = null;
    }

    public void PossessEnemy(Enemy enemy)
    {
        if (enemy == null) return;
        if (!enemy.isWeakened && !enemy.isDowned) return;
        if (isPossessing && possessedEnemy != null) Unpossess();
        possessedEnemy = enemy;
        isPossessing = true;
        // Restore soul health to full when possessing
        currentHealth = soulMaxHealth;
        maxHealth = soulMaxHealth;
        savedDecayTimer = decayTimer;
        possessionDecayTimer = 0f;
        SetSoulActive(false);
        // Keep soul renderer visible so it follows the possessed enemy
        foreach (var r in soulRenderers) if (r != null) r.enabled = true;
        if (cameraFollow != null) cameraFollow.target = enemy.transform;
        enemy.OnPossessed();
        if (input != null) input.OnPossessionStarted(enemy);
        if (combat != null) combat.OnPossessionStarted(enemy);
        ShowPossessionHUD(enemy);
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Possessed);
        Debug.Log("[PlayerHealth] POSSESSED " + enemy.displayName);
    }

    public void OnPossessedEnemyDied()
    {
        Debug.Log("[PlayerHealth] Possessed enemy died - returning to soul form");
        Unpossess();
    }

    public void Unpossess()
    {
        if (!isPossessing) return;
        Enemy oldEnemy = possessedEnemy;
        if (input != null) input.OnPossessionEnded();
        if (combat != null) combat.OnPossessionEnded();
        if (oldEnemy != null) { oldEnemy.OnUnpossessed(); Destroy(oldEnemy.gameObject); possessedEnemy = null; }
        isPossessing = false;
        if (cameraFollow != null) cameraFollow.target = transform;
        SetSoulActive(true);
        maxHealth = soulMaxHealth;
        decayTimer = savedDecayTimer;
        HidePossessionHUD();
        if (GameManager.Instance != null) GameManager.Instance.SwitchState(GameManager.GameState.Soul);
        Debug.Log("[PlayerHealth] Unpossessed - soul form restored. HP: " + currentHealth);
        UpdateHealthUI();
    }

    void SetSoulActive(bool active)
    {
        foreach (var r in soulRenderers) if (r != null) r.enabled = active;
        foreach (var c in soulColliders) if (c != null) c.enabled = active;
        foreach (var comp in soulComponents)
        {
            if (comp == null || comp == this) continue;
            // Keep PlayerInputController enabled while possessing so player can still drive the enemy
            if (!active && comp is PlayerInputController) continue;
            comp.enabled = active;
        }
        if (rb != null) { if (!active) { rb.velocity = Vector3.zero; rb.isKinematic = true; } else { rb.isKinematic = false; } }
    }

    void ShowPossessionHUD(Enemy enemy)
    {
        if (PossessionHUD.Instance != null) { PossessionHUD.Instance.Show(enemy); return; }
        if (possessionHUDPanel != null)
        {
            possessionHUDPanel.SetActive(true);
            if (possessionEnemyNameText != null) possessionEnemyNameText.text = enemy.displayName;
            string basicName = "普攻";
            string skillName = "技能";
            if (enemy.basicAbilities.Count > 0 && enemy.basicAbilities[0] != null) basicName = enemy.basicAbilities[0].abilityName;
            if (enemy.skillAbilities.Count > 0 && enemy.skillAbilities[0] != null) skillName = enemy.skillAbilities[0].abilityName;
            if (possessionAbilityQText != null) possessionAbilityQText.text = "左键 - " + basicName;
            if (possessionAbilityWText != null) possessionAbilityWText.text = "右键 - " + skillName;
            if (possessionAbilityRText != null) possessionAbilityRText.text = "R - 脱离附身";
        }
        if (possessionHealthSlider != null) { possessionHealthSlider.maxValue = enemy.maxHealth; possessionHealthSlider.value = enemy.currentHealth; }
        // If no PossessionHUD and no fallback panel, create one dynamically
        if (PossessionHUD.Instance == null && possessionHUDPanel == null && dynamicHUD == null)
        {
            CreateDynamicHUD(enemy);
        }
        UpdatePossessionHUD();
    }

    void UpdatePossessionHUD()
    {
        if (PossessionHUD.Instance != null) return;
        Slider slider = possessionHealthSlider;
        Image fill = possessionSliderFill;
        if (slider == null && dynamicHUD != null)
        {
            slider = dynamicHUD.GetComponentInChildren<Slider>();
        }
        if (slider != null && possessedEnemy != null)
        {
            slider.value = possessedEnemy.currentHealth;
            if (fill != null && healthGradient != null)
            {
                float ratio = possessedEnemy.maxHealth > 0 ? possessedEnemy.currentHealth / possessedEnemy.maxHealth : 0;
                fill.color = healthGradient.Evaluate(ratio);
            }
        }
    }

    void HidePossessionHUD()
    {
        if (PossessionHUD.Instance != null) { PossessionHUD.Instance.Hide(); return; }
        if (possessionHUDPanel != null) possessionHUDPanel.SetActive(false);
        if (dynamicHUD != null) { Destroy(dynamicHUD); dynamicHUD = null; }
    }

    void CreateDynamicHUD(Enemy enemy)
    {
        // Find or create a Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("DynamicCanvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        dynamicHUD = new GameObject("DynamicPossessionHUD");
        dynamicHUD.transform.SetParent(canvas.transform, false);
        var hudRect = dynamicHUD.AddComponent<RectTransform>();
        hudRect.anchorMin = new Vector2(0.5f, 0f);
        hudRect.anchorMax = new Vector2(0.5f, 0f);
        hudRect.pivot = new Vector2(0.5f, 0f);
        hudRect.anchoredPosition = new Vector2(0, 20);
        hudRect.sizeDelta = new Vector2(400, 100);

        // Background
        var bg = dynamicHUD.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.6f);

        // Enemy name text
        var nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(dynamicHUD.transform, false);
        var nameRect = nameGO.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.6f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(10, 0);
        nameRect.offsetMax = new Vector2(-10, 0);
        var nameText = nameGO.AddComponent<Text>();
        nameText.text = enemy.displayName;
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 20;
        nameText.color = Color.white;
        nameText.alignment = TextAnchor.MiddleCenter;

        // Health bar background
        var hpBarBg = new GameObject("HPBarBg");
        hpBarBg.transform.SetParent(dynamicHUD.transform, false);
        var hpBgRect = hpBarBg.AddComponent<RectTransform>();
        hpBgRect.anchorMin = new Vector2(0.05f, 0.4f);
        hpBgRect.anchorMax = new Vector2(0.95f, 0.55f);
        hpBgRect.offsetMin = Vector2.zero;
        hpBgRect.offsetMax = Vector2.zero;
        var hpBgImg = hpBarBg.AddComponent<Image>();
        hpBgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Health bar fill
        var hpFillGO = new GameObject("HPFill");
        hpFillGO.transform.SetParent(hpBarBg.transform, false);
        var hpFillRect = hpFillGO.AddComponent<RectTransform>();
        hpFillRect.anchorMin = Vector2.zero;
        hpFillRect.anchorMax = Vector2.one;
        hpFillRect.offsetMin = Vector2.zero;
        hpFillRect.offsetMax = Vector2.zero;
        var hpFillImg = hpFillGO.AddComponent<Image>();
        hpFillImg.color = Color.red;

        // Slider component
        var slider = hpBarBg.AddComponent<Slider>();
        slider.minValue = 0;
        slider.maxValue = enemy.maxHealth;
        slider.value = enemy.currentHealth;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        slider.fillRect = hpFillRect;
        slider.targetGraphic = hpFillImg;

        // Ability texts
        string basicName = "普攻";
        string skillName = "技能";
        if (enemy.basicAbilities.Count > 0 && enemy.basicAbilities[0] != null) basicName = enemy.basicAbilities[0].abilityName;
        if (enemy.skillAbilities.Count > 0 && enemy.skillAbilities[0] != null) skillName = enemy.skillAbilities[0].abilityName;

        var abTextGO = new GameObject("AbilityText");
        abTextGO.transform.SetParent(dynamicHUD.transform, false);
        var abTextRect = abTextGO.AddComponent<RectTransform>();
        abTextRect.anchorMin = new Vector2(0, 0);
        abTextRect.anchorMax = new Vector2(1, 0.35f);
        abTextRect.offsetMin = new Vector2(10, 0);
        abTextRect.offsetMax = new Vector2(-10, 0);
        var abText = abTextGO.AddComponent<Text>();
        abText.text = "左键 - " + basicName + "  |  右键 - " + skillName + "  |  R - 脱离附身";
        abText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        abText.fontSize = 14;
        abText.color = new Color(0.8f, 0.8f, 0.8f);
        abText.alignment = TextAnchor.MiddleCenter;
    }

    public void TakeDamage(float amount)
    {
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

    void UpdateHealthUI()
    {
        if (healthSlider != null) { healthSlider.maxValue = maxHealth; healthSlider.value = currentHealth; }
        if (sliderFillImage != null && healthGradient != null)
        {
            float ratio = maxHealth > 0 ? currentHealth / maxHealth : 0;
            sliderFillImage.color = healthGradient.Evaluate(ratio);
        }
    }

    private float SoulMoveSpeed
    {
        get { if (input != null) return input.moveSpeed; return 5f; }
    }

    private Coroutine flyRoutine;
}