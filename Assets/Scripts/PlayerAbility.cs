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
    public AbilityType type = AbilityType.Passive;

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

    [Header("Screen Shake")]
    [Tooltip("Trigger a camera shake when this attack deals damage.")]
    public bool shakeOnHit = false;
    [Tooltip("Shake strength passed to the CameraDirector when this attack lands. 1 = default.")]
    public float shakeForce = 1f;

    [Header("Hit Stop (顿帧)")]
    [Tooltip("Briefly freeze time when this attack deals damage, for impact feel.")]
    public bool hitStopOnHit = false;
    [Tooltip("Hit-stop duration in unscaled seconds passed to the CameraDirector.")]
    public float hitStopDuration = 0.07f;

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

    protected virtual void Awake()
    {
        owner = GetComponentInParent<PlayerCombat>();
        currentCooldown = 0f;
        if (owner != null) owner.RegisterAbility(this);
    }

    protected virtual void Update()
    {
        if (currentCooldown > 0f) currentCooldown -= Time.deltaTime;
    }

    /// <summary>Returns true if this ability can be triggered right now.</summary>
    public virtual bool CanTrigger()
    {
        return currentCooldown <= 0f && owner != null;
    }

    /// <summary>Trigger the ability. Called by PlayerCombat when player presses the corresponding key.</summary>
    public virtual void Trigger()
    {
        currentCooldown = EffectiveCooldown;
        _hitFeedbackFiredThisAttack = false;
        if (vfxDelay <= 0f)
            SpawnVfx();
        else
            Invoke(nameof(SpawnVfx), vfxDelay);
        OnTrigger();
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
        activeVfx = Instantiate(vfxPrefab, pos, rot);
        PlayVfx(activeVfx);
        return activeVfx;
    }

    /// <summary>Play all ParticleSystems on a VFX GameObject.</summary>
    protected void PlayVfx(GameObject vfx)
    {
        if (vfx == null) return;
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
            ps.Play(true);
    }

    /// <summary>Helper: deal damage to an enemy (triggers burn passive).</summary>
    protected void DealDamageToEnemy(Enemy target, float amount)
    {
        if (target == null) return;

        // If possessing an enemy, route through its ApplyOffensiveDamage to trigger passives
        var possessed = PlayerHealth.Instance != null ? PlayerHealth.Instance.possessedEnemy : null;
        if (possessed != null)
            possessed.ApplyOffensiveDamage(target, amount);
        else
            ApplySoulBurn(target, amount);

        if (!_hitFeedbackFiredThisAttack && CameraDirector.Instance != null)
        {
            if (shakeOnHit) CameraDirector.Instance.Shake(shakeForce);
            if (hitStopOnHit) CameraDirector.Instance.HitStop(hitStopDuration);
            _hitFeedbackFiredThisAttack = true;
        }
        if (owner != null) owner.OnDealtDamage(amount);
    }

    /// <summary>Apply burn from soul form (no possessed enemy to route through).</summary>
    void ApplySoulBurn(Enemy target, float amount)
    {
        target.TakeDamage(amount);
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
