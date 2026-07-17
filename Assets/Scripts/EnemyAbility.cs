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
    [Tooltip("Local position offset added to the spawn point (relative to anchor's transform).")]
    public Vector3 vfxPositionOffset = Vector3.zero;
    [Tooltip("Delay in seconds before the VFX spawns. 0 = instant.")]
    public float vfxDelay = 0f;
    [Tooltip("Rotation offset for the VFX (e.g. (-90,0,0) if your VFX faces Y-up but you need Z-forward)")]
    public Vector3 vfxRotationOffset = Vector3.zero;

    [Header("Damage (if applicable)")]
    public float damage = 0f;

    /// <summary>Cooldown in seconds. 0 = no cooldown. Only meaningful for BasicAttack / Skill.</summary>
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
        return currentCooldown <= 0f && owner != null && !owner.isDowned;
    }

    /// <summary>Trigger the ability. Called by Enemy AI / Player when possessing.</summary>
    public virtual void Trigger()
    {
        currentCooldown = EffectiveCooldown;
        if (vfxDelay <= 0f)
            SpawnVfx();
        else
            Invoke(nameof(SpawnVfx), vfxDelay);
        OnTrigger();
    }

    /// <summary>Override to implement ability behavior. Called by Trigger().</summary>
    protected abstract void OnTrigger();

    /// <summary>Spawn the assigned VFX prefab at the spawn point (or enemy root).</summary>
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

    /// <summary>
    /// Try to find and damage the Player if they are within the given radius from a point.
    /// Returns true if the player was hit. Does NOT depend on targetMask — uses tag lookup.
    /// </summary>
    protected bool TryDamagePlayerInRadius(Vector3 center, float radius, float amount)
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return false;
        float dist = Vector3.Distance(center, playerObj.transform.position);
        if (dist <= radius)
        {
            var ph = playerObj.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                DealDamageToPlayer(ph, amount);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Damage all valid enemy targets within an OverlapBox, ignoring targetMask.
    /// This is used when the player possesses an enemy and needs to hit other enemies.
    /// Falls back to tag-based detection so it works regardless of LayerMask config.
    /// </summary>
    protected void DamageEnemiesInBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, float amount, System.Action<Enemy, Vector3> onHit = null)
    {
        // Use All layers (~0) so we don't miss enemies due to targetMask misconfiguration
        Collider[] hits = Physics.OverlapBox(center, halfExtents, orientation, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var enemy = h.GetComponentInParent<Enemy>();
            if (enemy != null && enemy != owner && !enemy.isDowned && !enemy.isPossessed)
            {
                DealDamageTo(enemy, amount);
                onHit?.Invoke(enemy, enemy.transform.position);
            }
        }
    }

    /// <summary>
    /// Damage all valid enemy targets within an OverlapSphere, ignoring targetMask.
    /// </summary>
    protected void DamageEnemiesInSphere(Vector3 center, float radius, float amount, System.Action<Enemy, Vector3> onHit = null)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var enemy = h.GetComponentInParent<Enemy>();
            if (enemy != null && enemy != owner && !enemy.isDowned && !enemy.isPossessed)
            {
                DealDamageTo(enemy, amount);
                onHit?.Invoke(enemy, enemy.transform.position);
            }
        }
    }
}
