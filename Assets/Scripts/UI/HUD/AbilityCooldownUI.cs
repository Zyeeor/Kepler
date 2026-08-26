using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays cooldown icons: basic attack (left-click), possessed skill (right-click),
/// and possession (middle-click) or monster mobility (space). Each icon shows a grey overlay.

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

    [Header("Mobility / Possess Icon")]
    public RectTransform possessIconRoot;

    public Image possessIconImage;
    public Image possessCooldownOverlay;
    public TMP_Text possessKeyHint;

    [Header("Icon Configuration")]
    [Tooltip("技能 HUD 图标配置；为空时自动加载 Resources/UI/MonsterSkillIconConfig。")]
    public MonsterSkillIconConfig iconConfig;

    [Header("Style")]
    public Color readyColor = Color.white;
    public Color cooldownOverlayColor = new Color(0.3f, 0.3f, 0.3f, 0.8f);

    // Runtime data
    private PlayerCombat playerCombat;
    private MonsterActor currentEnemy;
    private bool trackingPlayer = true;

    // Stored ability references for per-frame cooldown reading
    private PlayerAbility playerBasicAbility;
    private PlayerAbility playerSkillAbility;
    private EnemyAbility enemyBasicAbility;
    private EnemyAbility enemySkillAbility;
    private EnemyAbility enemyMobilityAbility;
    private SinType enemySkillIconSin = SinType.None;


    // 场景默认图标作为配置缺省值；在玩家/怪物状态切换时避免沿用上一个角色的覆盖图。
    private Sprite defaultBasicIcon;
    private Sprite defaultSkillIcon;
    private Sprite defaultPossessIcon;

    void Awake()
    {
        playerCombat = FindObjectOfType<PlayerCombat>();
        defaultBasicIcon = basicIconImage != null ? basicIconImage.sprite : null;
        defaultSkillIcon = skillIconImage != null ? skillIconImage.sprite : null;
        defaultPossessIcon = possessIconImage != null ? possessIconImage.sprite : null;
    }

    void Start()
    {
        ResolveIconConfig();
        SetupIcons();
        RefreshIcons();
    }

    void ResolveIconConfig()
    {
        if (iconConfig == null)
            iconConfig = Resources.Load<MonsterSkillIconConfig>("UI/MonsterSkillIconConfig");
    }

    void Update()
    {
        // Track possession state changes（读 PossessionManager）
        var pm = PossessionManager.Instance;
        bool possessing = pm != null && pm.State == PossessionManager.SwitchState.Possessing;
        if (possessing && pm.CurrentBody != null)
        {
            if (currentEnemy != pm.CurrentBody)
            {
                currentEnemy = pm.CurrentBody;
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

        if (!trackingPlayer && currentEnemy != null && IsEnemySkillDisplayChanged())
            RefreshIcons();

        // Update cooldown overlays each frame
        UpdateCooldowns();
    }

    void SetupIcons()
    {
        if (basicKeyHint != null) basicKeyHint.text = GameInputBindings.GlyphOf(CommandButtons.Basic);
        if (skillKeyHint != null) skillKeyHint.text = GameInputBindings.GlyphOf(CommandButtons.Skill2);
        if (possessKeyHint != null) possessKeyHint.text = GameInputBindings.GlyphOf(CommandButtons.Skill1);

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
        overlay.fillMethod = Image.FillMethod.Radial360;
        overlay.fillOrigin = (int)Image.Origin360.Top;
        overlay.fillClockwise = false;

    }

    void RefreshIcons()
    {
        playerBasicAbility = null;
        playerSkillAbility = null;
        enemyBasicAbility = null;
        enemySkillAbility = null;
        enemyMobilityAbility = null;
        enemySkillIconSin = SinType.None;


        if (trackingPlayer && playerCombat != null)
        {
            if (playerCombat.basicAbilities.Count > 0)
            {
                playerBasicAbility = playerCombat.basicAbilities[0];
                ApplyPlayerIcon(MonsterSkillIconConfig.PlayerSlot.BasicAttack, basicIconImage);
                if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(true);
            }
            else { if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(false); }

            if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(false);
        }
        else if (!trackingPlayer && currentEnemy != null)
        {
            if (currentEnemy.basicAbilities.Count > 0)
            {
                enemyBasicAbility = currentEnemy.basicAbilities[0].ability;
                ApplyMonsterIcon(currentEnemy.sinType, MonsterSkillIconConfig.MonsterSlot.BasicAttack, basicIconImage);
                if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(true);
            }
            else { if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(false); }

            if (currentEnemy.skillAbilities.Count > 0)
            {
                enemySkillAbility = currentEnemy.skillAbilities[0].ability;
                enemySkillIconSin = ResolveSkillIconSin(currentEnemy);
                ApplyMonsterIcon(enemySkillIconSin, MonsterSkillIconConfig.MonsterSlot.Skill, skillIconImage);
                if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(true);
            }
            else { if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(false); }

            if (currentEnemy.mobilityAbilities.Count > 0)
            {
                enemyMobilityAbility = currentEnemy.mobilityAbilities[0].ability;
                ApplyMonsterIcon(currentEnemy.sinType, MonsterSkillIconConfig.MonsterSlot.Mobility, possessIconImage);
                if (possessIconRoot != null) possessIconRoot.gameObject.SetActive(true);
                if (possessKeyHint != null) possessKeyHint.text = GameInputBindings.GlyphOf(CommandButtons.Mobility);
            }
            else { if (possessIconRoot != null) possessIconRoot.gameObject.SetActive(false); }

        }
        else
        {
            if (basicIconRoot != null) basicIconRoot.gameObject.SetActive(false);
            if (skillIconRoot != null) skillIconRoot.gameObject.SetActive(false);
        }

        // Soul state keeps the possession icon; possessed state already assigns the same slot to mobility.
        if (trackingPlayer)
        {
            ApplyPlayerIcon(MonsterSkillIconConfig.PlayerSlot.Possess, possessIconImage);
            if (possessKeyHint != null) possessKeyHint.text = GameInputBindings.GlyphOf(CommandButtons.Skill1);
            if (possessIconRoot != null) possessIconRoot.gameObject.SetActive(true);
        }

    }

    void ApplyPlayerIcon(MonsterSkillIconConfig.PlayerSlot slot, Image target)
    {
        if (target == null) return;
        Sprite fallback = GetDefaultIcon(target);
        Sprite icon;
        if (iconConfig != null && iconConfig.TryGetPlayerIcon(slot, out icon))
            target.sprite = icon;
        else
            target.sprite = fallback;
        target.color = readyColor;

    }

    void ApplyMonsterIcon(SinType sin, MonsterSkillIconConfig.MonsterSlot slot, Image target)
    {
        if (target == null) return;
        Sprite fallback = GetDefaultIcon(target);
        Sprite icon;
        Color color;
        if (iconConfig != null && iconConfig.TryGetMonsterIcon(sin, slot, out icon, out color))
        {
            target.sprite = icon;
            target.color = color;
        }
        else
        {
            target.sprite = fallback;
            target.color = readyColor;
        }
    }

    bool IsEnemySkillDisplayChanged()
    {
        EnemyAbility currentSkill = null;
        if (currentEnemy.skillAbilities != null && currentEnemy.skillAbilities.Count > 0)
        {
            MonsterActor.SkillAbilityEntry entry = currentEnemy.skillAbilities[0];
            if (entry != null) currentSkill = entry.ability;
        }

        return currentSkill != enemySkillAbility
            || ResolveSkillIconSin(currentEnemy) != enemySkillIconSin;
    }

    SinType ResolveSkillIconSin(MonsterActor monster)
    {
        if (monster == null) return SinType.None;
        if (monster.sinType != SinType.Gluttony) return monster.sinType;

        GluttonyBodyState state = monster.GetComponent<GluttonyBodyState>();
        if (state != null && state.HasCopiedSkill && state.CopiedSkillSourceSin != SinType.None)
            return state.CopiedSkillSourceSin;
        return monster.sinType;
    }

    bool IsEnemySkillUnavailable()
    {
        if (trackingPlayer || currentEnemy == null || currentEnemy.sinType != SinType.Envy)
            return false;

        EnemyAbility_EnvyThunderstorm thunderstorm = enemySkillAbility as EnemyAbility_EnvyThunderstorm;
        return thunderstorm != null && !thunderstorm.HasLegalMarkedTargets;
    }


    Sprite GetDefaultIcon(Image target)
    {
        if (target == basicIconImage) return defaultBasicIcon;
        if (target == skillIconImage) return defaultSkillIcon;
        if (target == possessIconImage) return defaultPossessIcon;
        return null;
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
            skillCooldownOverlay.fillAmount = IsEnemySkillUnavailable()
                ? 1f
                : (total > 0f ? Mathf.Clamp01(remaining / total) : 0f);
        }

        // Soul state reads possession cooldown; possessed state reads mobility cooldown.
        if (possessCooldownOverlay != null && possessIconRoot != null && possessIconRoot.gameObject.activeSelf)
        {
            float total = 0f;
            float remaining = 0f;
            if (trackingPlayer)
            {
                var pm3 = PossessionManager.Instance;
                total = pm3 != null ? pm3.possessCooldown : 3f;
                remaining = pm3 != null ? pm3.CooldownRemaining : 0f;
            }
            else if (enemyMobilityAbility != null)
            {
                total = enemyMobilityAbility.EffectiveCooldown;
                remaining = enemyMobilityAbility.CurrentCooldown;
            }
            possessCooldownOverlay.fillAmount = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
        }

    }
}
