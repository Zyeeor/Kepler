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

    /// <summary>Helper: deal damage to an enemy.</summary>
    protected void DealDamageToEnemy(Enemy target, float amount)
    {
        if (target == null) return;
        target.TakeDamage(amount);
        if (owner != null) owner.OnDealtDamage(amount);
    }

    /// <summary>Get the aim direction from player to mouse cursor on ground plane.</summary>
    protected Vector3 GetMouseAimDirection()
    {
        if (owner != null) return owner.GetMouseAimDirection();
        return transform.forward;
    }
}
