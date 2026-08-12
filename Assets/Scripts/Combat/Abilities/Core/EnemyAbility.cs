using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// Base class for all enemy abilities (passive / basic attack / skill).
/// Attach one or more of these to an enemy prefab. They self-register with the parent Enemy on Awake.
/// </summary>
public abstract class EnemyAbility : MonoBehaviour
{
    public enum AbilityType { Passive, BasicAttack, Skill, Mobility }

    [Serializable]
    public class UpgradeSlot
    {
        [Tooltip("Unique effect ID matching a CardData.effectId.")]
        public string effectId;
        [Tooltip("Is this upgrade permanently unlocked for the current run? Set by CardManager.")]
        public bool unlocked;
    }

    [Header("Identity")]
    public string abilityName = "Ability";
    public AbilityType type = AbilityType.Passive;

    [Header("Upgrades")]
    [Tooltip("Each upgrade slot: effectId + unlocked checkbox. Set by CardManager.")]
    public List<UpgradeSlot> upgrades = new List<UpgradeSlot>();

    /// <summary>Check if a specific upgrade is unlocked (case-insensitive).</summary>
    public bool IsUpgradeUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id) || upgrades == null) return false;
        foreach (var u in upgrades)
            if (u != null && !string.IsNullOrEmpty(u.effectId) && u.effectId.Equals(id, StringComparison.OrdinalIgnoreCase) && u.unlocked)
                return true;
        return false;
    }

    protected float GetCardParameter(string key, float defaultValue)
    {
        return CardManager.Instance != null && CardManager.Instance.TryGetUnlockedAbilityParameter(this, key, out float value)
            ? value
            : defaultValue;
    }

    public bool HasAllAbilityTags(IEnumerable<string> queryTags)
    {
        return GameplayTagUtility.HasAll(abilityTags, queryTags);
    }

    public void AddAppliedEffectTags(IEnumerable<string> effectTags)
    {
        if (effectTags == null) return;
        foreach (string rawTag in effectTags)
        {
            string effectTag = GameplayTagUtility.Normalize(rawTag);
            if (string.IsNullOrEmpty(effectTag) || appliedEffectTags.Exists(value => string.Equals(value, effectTag, StringComparison.OrdinalIgnoreCase))) continue;
            appliedEffectTags.Add(effectTag);
        }
    }

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
    [Header("Hitbox Debug")]
    [Tooltip("Draw this ability's runtime physics queries when CombatHitboxDebug.Enabled is true.")]
    public bool drawHitboxes;

    /// <summary>Cooldown in seconds. 0 = no cooldown. Only meaningful for BasicAttack / Skill / Mobility.</summary>
    public float cooldown = 0f;

    [Header("Attack Behavior Tags")]
    [Tooltip("Stable identity tags for this attack behavior. Cards use these tags to target an ability without depending on its display name.")]
    public List<string> abilityTags = new List<string>();
    [Tooltip("Effect Tags applied to targets hit through this ability's shared damage helpers. Cards may add to this list at runtime.")]
    public List<string> appliedEffectTags = new List<string>();

    [Header("Activation Requirements")]
    [Tooltip("All listed tags must be active on the owner to activate this ability. Empty means no requirement.")]
    public List<string> requiredTags = new List<string>();
    [Tooltip("Effect applied to this ability owner when activation starts. Its granted Tags and duration define casting control, such as State.Control.Stunned.")]
    public GameplayEffectDefinition activationEffect;

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
    public float CurrentCooldown { get { return currentCooldown; } }
    protected GameObject activeVfx;

    protected virtual void Awake()
    {
        owner = GetComponentInParent<Enemy>();
        currentCooldown = 0f;
        if (owner != null) owner.RegisterAbility(this);
    }

    public bool IsOwnedByPlayer => owner != null && owner.IsPlayerControlled;
    protected float AbilityDeltaTime => IsOwnedByPlayer ? Time.unscaledDeltaTime : Time.deltaTime;
    protected float AbilityTime => IsOwnedByPlayer ? Time.unscaledTime : Time.time;
    protected object AbilityWait(float seconds) => owner != null && owner.IsPlayerControlled
        ? (object)new WaitForSecondsRealtime(seconds)
        : new WaitForSeconds(seconds);

    protected bool TryGetPossessedMouseDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (owner == null || !owner.isPossessed || PlayerController.Instance == null) return false;
        if (!PlayerController.Instance.TryGetAimPoint(out Vector3 aimPoint)) return false;

        direction = aimPoint - owner.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.zero;
            return false;
        }

        direction.Normalize();
        return true;
    }

    protected IEnumerator RotatePossessedOwnerTowards(Vector3 direction, float turnSpeed)
    {
        if (owner == null || direction.sqrMagnitude < 0.0001f) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        owner.IsAbilityFacingLocked = true;
        while (owner != null && Quaternion.Angle(owner.transform.rotation, targetRotation) > 0.1f)
        {
            owner.transform.rotation = Quaternion.RotateTowards(
                owner.transform.rotation,
                targetRotation,
                turnSpeed * AbilityDeltaTime);
            yield return null;
        }

        if (owner != null)
        {
            owner.transform.rotation = targetRotation;
            owner.IsAbilityFacingLocked = false;
        }
    }

    protected virtual void Update()
    {
        if (currentCooldown > 0f) currentCooldown -= AbilityDeltaTime;
    }

    protected virtual void OnDisable()
    {
        EndActivationEffect();
        CancelInvoke();
    }

    public virtual void ResetForOwnerReuse()
    {
        EndActivationEffect();
        CancelInvoke();
        currentCooldown = 0f;
        activeVfx = null;
    }

    /// <summary>Returns true if this ability can be triggered right now.</summary>
    public virtual bool CanTrigger()
    {
        if (currentCooldown > 0f || owner == null || owner.isDowned) return false;

        CombatAbilityComponent combat = owner.Combat;
        string reason;
        return combat == null || combat.CanActivate(this, requiredTags, out reason);
    }

    /// <summary>Trigger the ability. Called by Enemy AI / Player when possessing.</summary>
    public virtual void Trigger()
    {
        if (!TryBeginActivationEffect()) return;

        currentCooldown = EffectiveCooldown;
        if (vfxDelay <= 0f)
            SpawnVfx();
        else
            Invoke(nameof(SpawnVfx), vfxDelay);
        OnTrigger();
    }

    /// <summary>Begins this ability's configured Activation Effect. Effect duration controls the state lifetime.</summary>
    protected bool TryBeginActivationEffect()
    {
        CombatAbilityComponent combat = owner != null ? owner.Combat : null;
        return combat == null || combat.TryBeginAbility(this, requiredTags, activationEffect, abilityTags);
    }

    /// <summary>Ends this ability and removes only the Activation Effect instance it created.</summary>
    protected void EndActivationEffect()
    {
        if (owner != null && owner.Combat != null) owner.Combat.EndAbility(this);
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

    /// <summary>Spawn a VFX that auto-destroys when the owner dies.</summary>
    protected GameObject SpawnVfxTracked(GameObject prefab, Vector3 pos, Quaternion rot, float autoDestroyTime = -1f)
    {
        if (prefab == null || owner == null) return null;
        var go = Instantiate(prefab, pos, rot);
        PlayVfx(go);
        // Track owner death
        var tracker = go.AddComponent<DestroyOnOwnerDeath>();
        tracker.owner = owner.gameObject;
        if (autoDestroyTime > 0f) Destroy(go, autoDestroyTime);
        return go;
    }

    /// <summary>Helper: deal damage to a target via Enemy.ApplyDamageTo so it respects lifesteal etc.</summary>
    protected void DealDamageTo(Enemy target, float amount)
    {
        if (target == null || owner == null) return;
        // Pass damage to owner's damage pipeline so passives (e.g. lifesteal) can react
        owner.ApplyOffensiveDamage(target, amount);
        ApplyConfiguredEffectsTo(target.Combat);
    }

    protected void DealDamageToPlayer(PlayerHealth player, float amount)
    {
        if (player == null || owner == null || !owner.CanDamageSoul()) return;
        player.TakeDamage(amount);
        ApplyConfiguredEffectsTo(player.GetComponent<CombatAbilityComponent>());
        // Also trigger lifesteal for the owner enemy
        owner.OnDealtDamage(amount);
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
            target.ApplyEffect(definition, owner != null ? owner.Combat : null, abilityTags, out ignoredReason);
        }
    }

    protected List<Enemy> FindEnemiesInArc(Vector3 origin, Vector3 forward, float range, float angle, int layerMask = ~0)
    {
        List<Enemy> results = new List<Enemy>();
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return results;
        forward.Normalize();
        CombatHitboxDebug.DrawArc(drawHitboxes, origin, forward, range, angle);

        Collider[] hits = Physics.OverlapSphere(origin, range, layerMask, QueryTriggerInteraction.Collide);
        HashSet<Enemy> unique = new HashSet<Enemy>();
        foreach (Collider hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null || !unique.Add(enemy)) continue;
            Vector3 toTarget = enemy.transform.position - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f && Vector3.Angle(forward, toTarget) <= angle * 0.5f)
                results.Add(enemy);
        }
        return results;
    }

    protected HashSet<Enemy> DamageEnemiesAlongPath(Vector3 start, Vector3 end, float radius, float amount)
    {
        HashSet<Enemy> results = new HashSet<Enemy>();
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        if (distance < 0.0001f) return DamageEnemiesInSphere(start, radius, amount);
        direction /= distance;
        CombatHitboxDebug.DrawCapsule(drawHitboxes, start, end, radius);

        RaycastHit[] hits = Physics.SphereCastAll(start, radius, direction, distance, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (owner != null && owner.CanDamage(enemy) && results.Add(enemy))
                DealDamageTo(enemy, amount);
        }
        return results;
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
        CombatHitboxDebug.DrawBox(drawHitboxes, center, halfExtents, orientation);
        // Use All layers (~0) so we don't miss enemies due to targetMask misconfiguration
        Collider[] hits = Physics.OverlapBox(center, halfExtents, orientation, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var enemy = h.GetComponentInParent<Enemy>();
            if (owner != null && owner.CanDamage(enemy))
            {
                DealDamageTo(enemy, amount);
                onHit?.Invoke(enemy, enemy.transform.position);
            }
        }
    }

    /// <summary>
    /// Damage all valid enemy targets within an OverlapSphere, ignoring targetMask.
    /// Returns the set of enemies that were hit.
    /// </summary>
    protected HashSet<Enemy> DamageEnemiesInSphere(Vector3 center, float radius, float amount, System.Action<Enemy, Vector3> onHit = null)
    {
        var hitEnemies = new HashSet<Enemy>();
        CombatHitboxDebug.DrawSphere(drawHitboxes, center, radius);
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var enemy = h.GetComponentInParent<Enemy>();
            if (owner != null && owner.CanDamage(enemy))
            {
                DealDamageTo(enemy, amount);
                hitEnemies.Add(enemy);
                onHit?.Invoke(enemy, enemy.transform.position);
            }
        }
        return hitEnemies;
    }
}
