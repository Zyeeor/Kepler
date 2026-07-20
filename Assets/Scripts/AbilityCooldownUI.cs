using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays two fixed ability cooldown icons: one for basic attack (left-click) and one for skill (right-click).
/// Each icon shows a grey overlay whose fill amount represents the remaining cooldown.
/// Supports both soul state (player abilities) and possessed state (enemy abilities).
/// </summary>
public class AbilityCooldownUI : MonoBehaviour
{
    [Header("Basic Attack Icon")]
    public RectTransform basicIconRoot;
    public Image basicIconImage;
    public Image basicCooldownOverlay;
    public TMP_Text basicKeyHint;

    [Header("Skill Icon")]
    public RectTransform skillIconRoot;
    public Image skillIconImage;
    public Image skillCooldownOverlay;
    public TMP_Text skillKeyHint;

    [Header("Style")]
    public Color readyColor = Color.white;
    public Color cooldownOverlayColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

    // Runtime data
    private PlayerCombat playerCombat;
    private Enemy currentEnemy;
    private bool trackingPlayer = true;

    // Stored ability references for per-frame cooldown reading
    private PlayerAbility playerBasicAbility;
    private PlayerAbility playerSkillAbility;
    private EnemyAbility enemyBasicAbility;
    private EnemyAbility enemySkillAbility;

    void Awake()
    {
        playerCombat = FindObjectOfType<PlayerCombat>();
    }

    void Start()
    {
        // Auto-setup if not assigned
        SetupIcons();
        RefreshIcons();
    }

    void Update()
    {
        // Track possession state changes
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isPossessing && PlayerHealth.Instance.possessedEnemy != null)
        {
            if (currentEnemy != PlayerHealth.Instance.possessedEnemy)
            {
                currentEnemy = PlayerHealth.Instance.possessedEnemy;
                trackingPlayer = false;
                RefreshIcons();
            }
        }
        else
        {
            if (currentEnemy != null || !trackingPlayer)
            {
                currentEnemy = null;
                trackingPlayer = true;
                RefreshIcons();
            }
        }

        // Update cooldown overlays each frame
        UpdateCooldowns();
    }

    void SetupIcons()
    {
        // Set key hints
        if (basicKeyHint != null) basicKeyHint.text = "左键";
        if (skillKeyHint != null) skillKeyHint.text = "右键";

        // Set overlay colors
        if (basicCooldownOverlay != null)
        {
            basicCooldownOverlay.color = cooldownOverlayColor;
            basicCooldownOverlay.type = Image.Type.Filled;
            basicCooldownOverlay.fillMethod = Image.FillMethod.Vertical;
            basicCooldownOverlay.fillOrigin = 0;
        }
        if (skillCooldownOverlay != null)
        {
            skillCooldownOverlay.color = cooldownOverlayColor;
            skillCooldownOverlay.type = Image.Type.Filled;
            skillCooldownOverlay.fillMethod = Image.FillMethod.Vertical;
            skillCooldownOverlay.fillOrigin = 0;
        }
    }

    void RefreshIcons()
    {
        // Clear references first
        playerBasicAbility = null;
        playerSkillAbility = null;
        enemyBasicAbility = null;
        enemySkillAbility = null;

        if (trackingPlayer && playerCombat != null)
        {
            // Basic attack
            if (playerCombat.basicAbilities.Count > 0)
            {
                playerBasicAbility = playerCombat.basicAbilities[0];
                if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(true);
            }
            else
            {
                if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(false);
            }

            // Skill (first skill ability)
            if (playerCombat.skillAbilities.Count > 0)
            {
                playerSkillAbility = playerCombat.skillAbilities[0];
                if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(true);
            }
            else
            {
                if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(false);
            }
        }
        else if (!trackingPlayer && currentEnemy != null)
        {
            // Basic attack
            if (currentEnemy.basicAbilities.Count > 0)
            {
                enemyBasicAbility = currentEnemy.basicAbilities[0];
                if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(true);
            }
            else
            {
                if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(false);
            }

            // Skill (first skill ability)
            if (currentEnemy.skillAbilities.Count > 0)
            {
                enemySkillAbility = currentEnemy.skillAbilities[0];
                if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(true);
            }
            else
            {
                if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(false);
            }
        }
        else
        {
            // No valid source — hide both
            if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(false);
            if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(false);
        }
    }

    void UpdateCooldowns()
    {
        // Update basic cooldown overlay — read CurrentCooldown every frame
        if (basicCooldownOverlay != null && basicIconRoot != null && basicIconRoot.gameObject.activeSelf)
        {
            float total = 0f;
            float remaining = 0f;
            if (playerBasicAbility != null) { total = playerBasicAbility.EffectiveCooldown; remaining = playerBasicAbility.CurrentCooldown; }
            else if (enemyBasicAbility != null) { total = enemyBasicAbility.EffectiveCooldown; remaining = enemyBasicAbility.CurrentCooldown; }

            if (total > 0f)
                basicCooldownOverlay.fillAmount = Mathf.Clamp01(remaining / total);
            else
                basicCooldownOverlay.fillAmount = 0f;
        }

        // Update skill cooldown overlay
        if (skillCooldownOverlay != null && skillIconRoot != null && skillIconRoot.gameObject.activeSelf)
        {
            float total = 0f;
            float remaining = 0f;
            if (playerSkillAbility != null) { total = playerSkillAbility.EffectiveCooldown; remaining = playerSkillAbility.CurrentCooldown; }
            else if (enemySkillAbility != null) { total = enemySkillAbility.EffectiveCooldown; remaining = enemySkillAbility.CurrentCooldown; }

            if (total > 0f)
                skillCooldownOverlay.fillAmount = Mathf.Clamp01(remaining / total);
            else
                skillCooldownOverlay.fillAmount = 0f;
        }
    }
}
