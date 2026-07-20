using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays cooldown icons: basic attack (left-click), skill (right-click), and possess/space.
/// Each icon shows a grey overlay whose fill amount represents the remaining cooldown.
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

    [Header("Possess Icon")]
    public RectTransform possessIconRoot;
    public Image possessIconImage;
    public Image possessCooldownOverlay;
    public TMP_Text possessKeyHint;

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
        if (basicKeyHint != null) basicKeyHint.text = "左键";
        if (skillKeyHint != null) skillKeyHint.text = "右键";
        if (possessKeyHint != null) possessKeyHint.text = "Space";

        // Set overlay colors
        SetupOverlay(basicCooldownOverlay);
        SetupOverlay(skillCooldownOverlay);
        SetupOverlay(possessCooldownOverlay);
    }

    void SetupOverlay(Image overlay)
    {
        if (overlay == null) return;
        overlay.color = cooldownOverlayColor;
        overlay.type = Image.Type.Filled;
        overlay.fillMethod = Image.FillMethod.Vertical;
        overlay.fillOrigin = 0;
    }

    void RefreshIcons()
    {
        playerBasicAbility = null;
        playerSkillAbility = null;
        enemyBasicAbility = null;
        enemySkillAbility = null;

        if (trackingPlayer && playerCombat != null)
        {
            if (playerCombat.basicAbilities.Count > 0)
            {
                playerBasicAbility = playerCombat.basicAbilities[0];
                if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(true);
            }
            else { if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(false); }

            if (playerCombat.skillAbilities.Count > 0)
            {
                playerSkillAbility = playerCombat.skillAbilities[0];
                if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(true);
            }
            else { if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(false); }
        }
        else if (!trackingPlayer && currentEnemy != null)
        {
            if (currentEnemy.basicAbilities.Count > 0)
            {
                enemyBasicAbility = currentEnemy.basicAbilities[0];
                if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(true);
            }
            else { if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(false); }

            if (currentEnemy.skillAbilities.Count > 0)
            {
                enemySkillAbility = currentEnemy.skillAbilities[0];
                if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(true);
            }
            else { if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(false); }
        }
        else
        {
            if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(false);
            if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(false);
        }

        // Possess icon: always show in soul state, hide while possessing
        bool showPossess = !trackingPlayer || (PlayerHealth.Instance != null && !PlayerHealth.Instance.isPossessing);
        if (possessIconRoot != null) possessIconRoot.gameObject.SetActive(showPossess);
    }

    void UpdateCooldowns()
    {
        // Basic cooldown
        if (basicCooldownOverlay != null && basicIconRoot != null && basicIconRoot.gameObject.activeSelf)
        {
            float total = 0f, remaining = 0f;
            if (playerBasicAbility != null) { total = playerBasicAbility.EffectiveCooldown; remaining = playerBasicAbility.CurrentCooldown; }
            else if (enemyBasicAbility != null) { total = enemyBasicAbility.EffectiveCooldown; remaining = enemyBasicAbility.CurrentCooldown; }
            basicCooldownOverlay.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        }

        // Skill cooldown
        if (skillCooldownOverlay != null && skillIconRoot != null && skillIconRoot.gameObject.activeSelf)
        {
            float total = 0f, remaining = 0f;
            if (playerSkillAbility != null) { total = playerSkillAbility.EffectiveCooldown; remaining = playerSkillAbility.CurrentCooldown; }
            else if (enemySkillAbility != null) { total = enemySkillAbility.EffectiveCooldown; remaining = enemySkillAbility.CurrentCooldown; }
            skillCooldownOverlay.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        }

        // Possess cooldown
        if (possessCooldownOverlay != null && possessIconRoot != null && possessIconRoot.gameObject.activeSelf)
        {
            float total = PlayerHealth.Instance != null ? PlayerHealth.Instance.possessCooldown : 3f;
            float remaining = PlayerHealth.Instance != null ? PlayerHealth.Instance.possessCooldownTimer : 0f;
            possessCooldownOverlay.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        }
    }
}
