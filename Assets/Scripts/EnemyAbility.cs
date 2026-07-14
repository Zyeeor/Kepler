using UnityEngine;

/// <summary>
/// Base class for all enemy abilities (passive / basic attack / skill).
/// Attach one or more of these to an enemy prefab. They self-register with the parent Enemy on Awake.
/// </summary>
public abstract class EnemyAbility : MonoBehaviour
{
    public enum AbilityType { Passive, BasicAttack, Skill }

    [Header("Identity")]
    public string abilityName = "Ability";
    public AbilityType type = AbilityType.Passive;

    [Header("VFX")]
    public GameObject vfxPrefab;       // VFX prefab spawned when the ability triggers
    public Transform vfxSpawnPoint;    // optional spawn anchor (defaults to enemy root)

    [Header("Damage (if applicable)")]
    public float damage = 0f;

    /// <summary>Cooldown in seconds. 0 = no cooldown. Only meaningful for BasicAttack / Skill.</summary>
    public float cooldown = 0f;

    protected Enemy owner;
    protected float currentCooldown;
    protected GameObject activeVfx;

    protected virtual void Awake()
    {
        owner = GetComponentInParent<Enemy>();
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
        return currentCooldown <= 0f && owner != null && !owner.isDowned && !owner.isPossessed;
    }

    /// <summary>Trigger the ability. Called by Enemy AI / Player when possessing.</summary>
    public virtual void Trigger()
    {
        currentCooldown = cooldown;
        SpawnVfx();
        OnTrigger();
    }

    /// <summary>Override to implement ability behavior. Called by Trigger().</summary>
    protected abstract void OnTrigger();

    /// <summary>Spawn the assigned VFX prefab at the spawn point (or enemy root).</summary>
    protected virtual GameObject SpawnVfx()
    {
        if (vfxPrefab == null) return null;
        Transform anchor = vfxSpawnPoint != null ? vfxSpawnPoint : (owner != null ? owner.transform : transform);
        activeVfx = Instantiate(vfxPrefab, anchor.position, anchor.rotation);
        return activeVfx;
    }

    /// <summary>Helper: deal damage to a target via Enemy.ApplyDamageTo so it respects lifesteal etc.</summary>
    protected void DealDamageTo(Enemy target, float amount)
    {
        if (target == null || owner == null) return;
        // Pass damage to owner's damage pipeline so passives (e.g. lifesteal) can react
        owner.ApplyOffensiveDamage(target, amount);
    }

    protected void DealDamageToPlayer(PlayerHealth player, float amount)
    {
        if (player == null) return;
        player.TakeDamage(amount);
        // Trigger lifesteal etc.
        if (owner != null) owner.OnDealtDamage(amount);
    }
}
