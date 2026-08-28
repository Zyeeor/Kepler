using UnityEngine;

/// <summary>
/// Base class for all player abilities (passive / basic attack / skill).
/// Same structure as EnemyAbility — attach to player prefab, auto-register with PlayerCombat on Awake.
/// </summary>
public abstract class PlayerAbility : MonoBehaviour
{
    public enum AbilityType { Passive, BasicAttack, Skill }

    [Header("Identity")]
    public string abilityName = "Ability";
    [TextArea(2, 5)]
    [Tooltip("技能在 HUD 悬浮提示中显示的具体效果；留空时使用伤害、冷却等通用字段生成摘要。")]
    public string abilityDescription;
    public AbilityType type = AbilityType.Passive;

    public bool HasAllAbilityTags(System.Collections.Generic.IEnumerable<string> queryTags)
    {
        return GameplayTagUtility.HasAll(abilityTags, queryTags);
    }

    public void AddAppliedEffectTags(System.Collections.Generic.IEnumerable<string> effectTags)
    {
        if (effectTags == null) return;
        foreach (string rawTag in effectTags)
        {
            string effectTag = GameplayTagUtility.Normalize(rawTag);
            if (string.IsNullOrEmpty(effectTag) || appliedEffectTags.Exists(value => string.Equals(value, effectTag, System.StringComparison.OrdinalIgnoreCase))) continue;
            appliedEffectTags.Add(effectTag);
        }
    }

    [Header("VFX")]
    public GameObject vfxPrefab;
    public Transform vfxSpawnPoint;
    public Vector3 vfxPositionOffset = Vector3.zero;
    public float vfxDelay = 0f;
    public Vector3 vfxRotationOffset = Vector3.zero;

    [Header("Damage (if applicable)")]
    public float damage = 0f;

    /// <summary>Cooldown in seconds. 0 = no cooldown.</summary>
    public float cooldown = 0f;

    [Header("Attack Behavior Tags")]
    [Tooltip("Stable identity tags for this attack behavior. Use them to bind run-time Effects without relying on the ability display name.")]
    public System.Collections.Generic.List<string> abilityTags = new System.Collections.Generic.List<string>();
    [Tooltip("Effect Tags applied to targets hit through this ability's shared damage helper.")]
    public System.Collections.Generic.List<string> appliedEffectTags = new System.Collections.Generic.List<string>();

    [Header("Activation Requirements")]
    [Tooltip("All listed tags must be active on the player to activate this ability. Empty means no requirement.")]
    public System.Collections.Generic.List<string> requiredTags = new System.Collections.Generic.List<string>();
    [Tooltip("Effect applied to this ability owner when activation starts. Its granted Tags and duration define casting control, such as State.Control.Stunned.")]
    public GameplayEffectDefinition activationEffect;

    [Header("Audio (Combat Audio Manager)")]
    [Tooltip("施放音（SfxId 下拉选择，clip 在 SfxBank 资产配置）。空 = 静默。")]
    [SfxIdName]
    public string castAudioName;
    [Tooltip("首次命中音（SfxId 下拉选择，clip 在 SfxBank 资产配置）。空 = 静默。")]
    [SfxIdName]
    public string hitAudioName;

    [Header("Hit Feedback (Combat Effect Manager)")]
    [Tooltip("Post-process / shake / hit-stop settings played on first damage of each attack.")]
    public HitFeedbackParams hitFeedback = new HitFeedbackParams
    {
        shakeOnHit = false,
        hitStopOnHit = false,
        postProcessOnHit = false
    };

    /// <summary>Actual cooldown after attack speed modifier is applied.</summary>
    public float EffectiveCooldown
    {
        get
        {
            float spd = owner != null ? owner.attackSpeed : 1f;
            if (spd <= 0f) spd = 0.01f;
            return cooldown / spd;
        }
    }

    protected PlayerCombat owner;
    protected float currentCooldown;
    public float CurrentCooldown { get { return currentCooldown; } }
    protected GameObject activeVfx;

    /// <summary>Ensures screen shake / hit-stop fire at most once per attack, even for multi-hit attacks.</summary>
    private bool _hitFeedbackFiredThisAttack;
    private bool _hitAudioFiredThisAttack;

    protected virtual void Awake()
    {
        owner = GetComponentInParent<PlayerCombat>();
        currentCooldown = 0f;
        if (owner != null) owner.RegisterAbility(this);
    }

    protected virtual void Update()
    {
        if (currentCooldown > 0f) currentCooldown -= Time.unscaledDeltaTime;
    }

    protected virtual void OnDisable()
    {
        EndAbilityEffect();
    }

    /// <summary>Returns true if this ability can be triggered right now.</summary>
    public virtual bool CanTrigger()
    {
        if (currentCooldown > 0f || owner == null) return false;

        CombatAbilityComponent combat = owner.GetComponent<CombatAbilityComponent>();
        string reason;
        return combat == null || combat.CanActivate(this, requiredTags, out reason);
    }

    /// <summary>Trigger the ability. Called by PlayerCombat when player presses the corresponding key.</summary>
    public virtual void Trigger()
    {
        if (!TryBeginAbilityEffect()) return;

        currentCooldown = EffectiveCooldown;
        _hitFeedbackFiredThisAttack = false;
        _hitAudioFiredThisAttack = false;
        if (!string.IsNullOrWhiteSpace(castAudioName))
            // 灵魂/玩家自身施放音：2D/3D 走 SfxBank 条目 prefer3D（音效表「3D 定位」勾选框）
            CombatAudioManager.Play(castAudioName, owner != null ? owner.transform.position : transform.position);
        if (vfxDelay <= 0f)
            SpawnVfx();
        else
            Invoke(nameof(SpawnVfx), vfxDelay);
        OnTrigger();
    }

    /// <summary>Begins this ability's configured Activation Effect. Effect duration controls the state lifetime.</summary>
    protected bool TryBeginAbilityEffect()
    {
        CombatAbilityComponent combat = owner != null ? owner.GetComponent<CombatAbilityComponent>() : null;
        return combat == null || combat.TryBeginAbility(this, requiredTags, activationEffect, abilityTags);
    }

    /// <summary>Ends this ability and removes only the Activation Effect instance it created.</summary>
    protected void EndAbilityEffect()
    {
        CombatAbilityComponent combat = owner != null ? owner.GetComponent<CombatAbilityComponent>() : null;
        if (combat != null) combat.EndAbility(this);
    }

    /// <summary>Override to implement ability behavior.</summary>
    protected abstract void OnTrigger();

    /// <summary>Spawn the assigned VFX prefab at the spawn point (or player root).</summary>
    protected virtual GameObject SpawnVfx()
    {
        if (vfxPrefab == null) return null;
        Transform anchor = vfxSpawnPoint != null ? vfxSpawnPoint : (owner != null ? owner.transform : transform);
        Vector3 pos = anchor.position + anchor.TransformDirection(vfxPositionOffset);
        Quaternion rot = anchor.rotation * Quaternion.Euler(vfxRotationOffset);
        activeVfx = VfxPool.Instance.Spawn(vfxPrefab, pos, rot);
        BulletTimeController.MarkVfxOrigin(activeVfx, true);
        PlayVfx(activeVfx);
        float duration = 1f;
        foreach (var ps in activeVfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            if (main.loop) main.loop = false;
            if (main.duration > duration) duration = main.duration;
        }
        VfxPool.ReleaseOrDestroy(activeVfx, duration);
        return activeVfx;
    }

    /// <summary>Play all ParticleSystems on a VFX GameObject.</summary>
    protected void PlayVfx(GameObject vfx)
    {
        if (vfx == null) return;
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
            ps.Play(true);
    }

    /// <summary>Helper: deal damage to an enemy (triggers burn passive + damage amp).</summary>
    protected void DealDamageToEnemy(Enemy target, float amount)
    {
        if (target == null) return;

        // Apply damage amplification from player passives
        if (PlayerPassiveManager.Instance != null)
            amount *= (1f + PlayerPassiveManager.Instance.GetDamageAmp());

        // If possessing an enemy, route through its ApplyOffensiveDamage to trigger passives
        var possessed = PossessionManager.Instance != null ? PossessionManager.Instance.CurrentBody : null;
        if (possessed != null)
            possessed.ApplyOffensiveDamage(target, amount);
        else
            ApplySoulBurn(target, amount);

        if (!_hitFeedbackFiredThisAttack && hitFeedback != null && hitFeedback.HasAnyEnabled)
        {
            Transform victim = target != null ? target.transform : null;
            CombatEffectManager.PlayHitFeedback(hitFeedback, owner != null ? owner.transform : transform, victim);
            _hitFeedbackFiredThisAttack = true;
        }
        if (!_hitAudioFiredThisAttack && !string.IsNullOrWhiteSpace(hitAudioName))
        {
            CombatAudioManager.Play(hitAudioName, target != null ? target.transform.position : transform.position);
            _hitAudioFiredThisAttack = true;
        }
        ApplyConfiguredEffectsTo(target.Combat);
        if (owner != null) owner.OnDealtDamage(amount);
    }

    protected void ApplyConfiguredEffectsTo(CombatAbilityComponent target)
    {
        if (target == null || appliedEffectTags == null || appliedEffectTags.Count == 0) return;
        CardManager manager = CardManager.Instance;
        if (manager == null) return;

        foreach (string effectTag in appliedEffectTags)
        {
            GameplayEffectDefinition definition;
            if (!manager.TryGetGameplayEffect(effectTag, out definition)) continue;

            string ignoredReason;
            target.ApplyEffect(definition, owner != null ? owner.GetComponent<CombatAbilityComponent>() : null, abilityTags, out ignoredReason);
        }
    }

    /// <summary>Apply burn from soul form (no possessed enemy to route through).</summary>
    void ApplySoulBurn(Enemy target, float amount)
    {
        target.TakePlayerDamage(amount);
        if (PlayerPassiveManager.Instance != null)
        {
            float burnPct = PlayerPassiveManager.Instance.GetBurnPercent();
            if (burnPct > 0f && target.GetComponent<BurnEffect>() == null)
            {
                var burn = target.gameObject.AddComponent<BurnEffect>();
                burn.Init(target, burnPct, 3f, 0.5f, PlayerPassiveManager.Instance.GetBurnVfxPrefab());
            }
        }
    }

    /// <summary>Get the aim direction from player to mouse cursor on ground plane.</summary>
    protected Vector3 GetMouseAimDirection()
    {
        if (owner != null) return owner.GetMouseAimDirection();
        return transform.forward;
    }
}
