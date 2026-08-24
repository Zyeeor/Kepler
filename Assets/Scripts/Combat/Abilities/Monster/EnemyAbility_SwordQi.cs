using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Skill: 剑气 - Sword Qi. Fires a directed sword-energy projectile that travels forward.
/// Upon hitting an enemy or reaching max range, it explodes in an AoE dealing damage
/// to all enemies within the blast radius.
/// Builds:
/// - Pride01: ranged max range
/// - Pride02: 3-way spread
/// - Pride.Pierce: pierce enemies, double damage; pierce look is a designer VFX slot
/// </summary>
public class EnemyAbility_SwordQi : EnemyAbility
{
    [Header("Projectile")]
    [Tooltip("How far the sword qi travels (meters)")]
    public float maxRange = 12f;
    [Tooltip("How fast the projectile moves (m/s)")]
    public float projectileSpeed = 15f;
    [Tooltip("Delay in seconds before the projectile spawns and starts moving.")]
    public float projectileDelay = 0.3f;
    [Tooltip("Width of the projectile hitbox (radius)")]
    public float projectileWidth = 1.5f;
    [Tooltip("Height of the projectile hitbox")]
    public float projectileHeight = 2f;

    [Header("Explosion")]
    [Tooltip("AoE blast radius on impact")]
    public float blastRadius = 4f;
    [Tooltip("Damage multiplier for enemies hit by the blast (vs direct hit)")]
    public float blastDamageMultiplier = 0.7f;
    public LayerMask targetMask;

    [Header("Animation")]
    public string animTrigger = "SwordQi";
    [Tooltip("Possessed owner turn speed before firing toward the mouse aim.")]
    public float aimTurnSpeed = 720f;

    [Header("Activation Display")]
    [Tooltip("Display object on the enemy body. Shown while Sword Qi is active, then hidden when the cast ends.")]
    public GameObject activationDisplay;


    [Header("VFX - Projectile")]
    public GameObject projectileVfxPrefab;  // the flying sword qi VFX
    public float projectileVfxScale = 1f;
    [Tooltip("Local position offset for the projectile VFX (relative to owner's forward direction).")]
    public Vector3 projectileVfxPositionOffset = Vector3.zero;
    [Tooltip("Rotation offset for the projectile VFX. E.g. (-90,0,0) if VFX faces Y-up but travels Z-forward.")]
    public Vector3 projectileVfxRotationOffset = Vector3.zero;

    [Header("VFX - Explosion")]
    public GameObject explosionVfxPrefab;   // explosion VFX on impact
    public float explosionVfxDuration = 0.5f;
    [Tooltip("World-space position offset for the explosion VFX.")]
    public Vector3 explosionVfxPositionOffset = Vector3.zero;
    [Tooltip("Rotation offset for the explosion VFX.")]
    public Vector3 explosionVfxRotationOffset = Vector3.zero;

    [Header("Upgrade - Pride01")]
    [Tooltip("Pride01: max range when this upgrade is unlocked.")]
    public float pride01MaxRange = 20f;

    [Header("Upgrade - Pride02")]
    [Tooltip("Pride02: fire 3 projectiles in a spread. Angle between each shot.")]
    public float pride02SpreadAngle = 15f;

    [Header("Upgrade - Pride.Pierce")]
    [Tooltip("Pride.Pierce: replacement projectile VFX prefab used while piercing is unlocked.")]
    public GameObject pierceVfxPrefab;
    [Tooltip("Local position offset for the replacement projectile VFX.")]
    public Vector3 pierceVfxPositionOffset = Vector3.zero;
    [Tooltip("Rotation offset for the replacement projectile VFX relative to travel direction.")]
    public Vector3 pierceVfxRotationOffset = Vector3.zero;
    [Tooltip("Pride.Pierce: damage multiplier applied to the main blade.")]
    public float pierceDamageMultiplier = 2f;

    private void OnEnable()
    {
        // Pride maps Sword Qi to left-click basic attack; keep Inspector type if already set.
        if (type != AbilityType.BasicAttack) type = AbilityType.BasicAttack;
        abilityName = "剑气";
        if (abilityTags == null) abilityTags = new System.Collections.Generic.List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Pride.SwordQi", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Pride.SwordQi");
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Pride", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Pride");
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Pride.Cut", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Pride.Cut");
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Pride.ExecutionSpeed", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Pride.ExecutionSpeed");
        EnsureUpgrade("PR-A01");

        EnsureUpgrade("PR-A02");
        EnsureUpgrade("PR-A03");
        EnsureUpgrade("PR-A04");
        EnsureUpgrade("PR-TG01");
        SetActivationDisplay(false);

    }

    public override bool CanTrigger()
    {
        if (owner.isPossessed)
            return base.CanTrigger();
        return base.CanTrigger() && owner != null && owner.targetPlayer != null;
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;
        SetActivationDisplay(true);
        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Skill");
        StartCoroutine(SwordQiRoutine());
    }

    private void SetActivationDisplay(bool visible)
    {
        if (activationDisplay != null) activationDisplay.SetActive(visible);
    }

    /// <summary>Fire an immediate sword-qi burst in a world direction (no windup). Used by BlinkChain build.
    /// Respects Pride02 spread and Pride01 range; pierce X look is not auto-fired from blink bursts.
    /// </summary>
    /// <param name="ignoreEnemy">Optional enemy that must not absorb this blade (e.g. the blink slash target).</param>
    public void FireDirectedBurst(Vector3 worldDirection, float overrideDamage = -1f, Enemy ignoreEnemy = null)
    {
        if (owner == null) return;
        Vector3 forward = worldDirection;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = owner.transform.forward;
        forward.Normalize();

        float effectiveMaxRange = ScaleAbilityRadius(IsUpgradeUnlocked("PR-A04") ? pride01MaxRange : maxRange);

        float shotDamage = overrideDamage > 0f ? overrideDamage : GetShotDamage();

        bool pierce = IsUpgradeUnlocked("PR-A02");
        if (IsUpgradeUnlocked("PR-A01"))


        {
            StartCoroutine(LaunchProjectile(forward, effectiveMaxRange, shotDamage, pierce, ignoreEnemy));
            Vector3 left = Quaternion.Euler(0f, -pride02SpreadAngle, 0f) * forward;
            StartCoroutine(LaunchProjectile(left, effectiveMaxRange, shotDamage, pierce, ignoreEnemy));
            Vector3 right = Quaternion.Euler(0f, pride02SpreadAngle, 0f) * forward;
            StartCoroutine(LaunchProjectile(right, effectiveMaxRange, shotDamage, pierce, ignoreEnemy));
        }
        else
        {
            StartCoroutine(LaunchProjectile(forward, effectiveMaxRange, shotDamage, pierce, ignoreEnemy));
        }
    }

    IEnumerator SwordQiRoutine()
    {
        // Capture mouse aim once for facing + fire center. Do not use transform.forward at fire time:
        // projectileDelay unlocks facing, so the body may already have turned away from the cursor.
        Vector3 fireDirection = owner.transform.forward;
        Vector3 aimDirection = fireDirection;
        bool hadMouseAim = owner.isPossessed && TryGetPossessedMouseDirection(out aimDirection);
        if (hadMouseAim)
        {
            fireDirection = aimDirection;
            yield return StartCoroutine(RotatePossessedOwnerTowards(aimDirection, aimTurnSpeed));
        }

        if (projectileDelay > 0f)
        {
            // Keep facing locked through windup so the body stays on the aim axis.
            if (owner != null) owner.IsAbilityFacingLocked = true;
            yield return AbilityWait(projectileDelay);
            if (owner != null) owner.IsAbilityFacingLocked = false;
        }

        // Prefer a fresh mouse sample at fire time; fall back to the capture from trigger.
        if (owner != null && owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 fireAim))
            fireDirection = fireAim;
        fireDirection.y = 0f;
        if (fireDirection.sqrMagnitude < 0.0001f)
            fireDirection = owner != null ? owner.transform.forward : Vector3.forward;
        fireDirection.Normalize();

        float effectiveMaxRange = maxRange;
        if (IsUpgradeUnlocked("PR-A04"))
            effectiveMaxRange = pride01MaxRange;
        effectiveMaxRange = ScaleAbilityRadius(effectiveMaxRange);

        bool pride02 = IsUpgradeUnlocked("PR-A01");
        float shotDamage = GetShotDamage();
        bool pierce = IsUpgradeUnlocked("PR-A02");



        if (pride02)
        {
            StartCoroutine(LaunchProjectile(fireDirection, effectiveMaxRange, shotDamage, pierce));
            Vector3 left = Quaternion.Euler(0, -pride02SpreadAngle, 0) * fireDirection;
            StartCoroutine(LaunchProjectile(left, effectiveMaxRange, shotDamage, pierce));
            Vector3 right = Quaternion.Euler(0, pride02SpreadAngle, 0) * fireDirection;
            StartCoroutine(LaunchProjectile(right, effectiveMaxRange, shotDamage, pierce));
        }
        else
        {
            StartCoroutine(LaunchProjectile(fireDirection, effectiveMaxRange, shotDamage, pierce));
        }

        SetActivationDisplay(false);
    }

    private float GetShotDamage()
    {
        return IsUpgradeUnlocked("PR-A02") ? damage * pierceDamageMultiplier : damage;
    }


    private void EnsureUpgrade(string effectId)
    {
        if (upgrades == null) upgrades = new List<UpgradeSlot>();
        if (upgrades.Exists(slot => slot != null && string.Equals(slot.effectId, effectId, System.StringComparison.OrdinalIgnoreCase))) return;
        upgrades.Add(new UpgradeSlot { effectId = effectId, unlocked = false });
    }


    IEnumerator LaunchProjectile(Vector3 forward, float effectiveMaxRange, float shotDamage, bool pierce, Enemy ignoreEnemy = null)
    {
        Vector3 origin = owner.transform.position;
        Vector3 currentPos = origin + forward * 1f;
        float traveled = 0f;
        var hitIds = new HashSet<int>();
        if (ignoreEnemy != null)
            hitIds.Add(ignoreEnemy.GetInstanceID());

        GameObject projVfx = null;
        GameObject projectilePrefabToUse = pierce && pierceVfxPrefab != null ? pierceVfxPrefab : projectileVfxPrefab;
        Vector3 projectilePositionOffset = pierce ? pierceVfxPositionOffset : projectileVfxPositionOffset;
        Vector3 projectileRotationOffset = pierce ? pierceVfxRotationOffset : projectileVfxRotationOffset;
        Quaternion projectileFacing = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 visualOffset = projectileFacing * projectilePositionOffset;
        Quaternion visualRotation = projectileFacing * Quaternion.Euler(projectileRotationOffset);
        if (projectilePrefabToUse != null)
        {
            projVfx = SpawnVfxTracked(projectilePrefabToUse, currentPos + visualOffset, visualRotation);
            projVfx.transform.localScale *= projectileVfxScale;
        }


        while (traveled < effectiveMaxRange)
        {
            float speedMultiplier = IsUpgradeUnlocked("PR-TG01")
                ? GetCardParameter("AttackExpandSpeedMultiplier", 1.15f)
                : 1f;
            float step = projectileSpeed * speedMultiplier * AbilityDeltaTime;
            traveled += step;

            currentPos = origin + forward * Mathf.Min(traveled, effectiveMaxRange);

            if (projVfx != null)
            {
                projVfx.transform.position = currentPos + visualOffset;
                projVfx.transform.rotation = visualRotation;
            }


            Vector3 halfExtents = new Vector3(projectileWidth * 0.5f * OwnerCombatScaleMultiplier,
                projectileHeight * 0.5f * OwnerCombatScaleMultiplier, step * 0.5f);
            Vector3 checkCenter = currentPos - forward * (step * 0.5f);
            Quaternion checkRot = Quaternion.LookRotation(forward, Vector3.up);
            CombatHitboxDebug.DrawBox(drawHitboxes, checkCenter, halfExtents, checkRot, 0f);

            int layerMask = owner.isPossessed ? ~0 : targetMask;
            if (IsUpgradeUnlocked("PR-A03"))
                CutIncomingProjectiles(checkCenter, forward, projectileWidth * 0.5f * OwnerCombatScaleMultiplier);

            Collider[] hits = Physics.OverlapBox(checkCenter, halfExtents, checkRot, layerMask, QueryTriggerInteraction.Collide);
            bool hitSomething = false;

            Vector3 hitPos = currentPos;

            foreach (var h in hits)
            {
                if (IsOwnerCollider(h)) continue;

                var enemy = h.GetComponentInParent<Enemy>();
                if (enemy != null && ignoreEnemy != null && enemy == ignoreEnemy)
                    continue;
                if (owner.CanDamage(enemy))
                {
                    int id = enemy.GetInstanceID();
                    if (!hitIds.Add(id)) continue;
                    DealDamageTo(enemy, shotDamage);
                    hitSomething = true;
                    hitPos = h.ClosestPoint(currentPos);
                    if (pierce) SpawnExplosionVfx(hitPos);
                }
                var ph = h.GetComponentInParent<PlayerHealth>();
                if (ph != null && owner.CanDamageSoul())
                {
                    int id = ph.GetInstanceID();
                    if (!hitIds.Add(id)) continue;
                    DealDamageToPlayer(ph, shotDamage);
                    hitSomething = true;
                    hitPos = h.ClosestPoint(currentPos);
                    if (pierce) SpawnExplosionVfx(hitPos);
                }
            }

            if (hitSomething)
            {
                if (pierce)
                {
                    // Keep traveling through targets with the replacement projectile VFX.
                }
                else
                {
                    if (projVfx != null) ReleaseVfx(projVfx);
                    DoExplosion(hitPos, shotDamage, ignoreEnemy);
                    yield break;
                }
            }

            yield return null;
        }

        if (projVfx != null) ReleaseVfx(projVfx);
        if (!pierce)
            DoExplosion(currentPos, shotDamage, ignoreEnemy);
    }

    void DoExplosion(Vector3 center, float shotDamage, Enemy ignoreEnemy = null)
    {
        SpawnExplosionVfx(center);

        int layerMask = owner.isPossessed ? ~0 : targetMask;
        float effectiveBlastRadius = ScaleAbilityRadius(blastRadius);
        Collider[] hits = Physics.OverlapSphere(center, effectiveBlastRadius, layerMask, QueryTriggerInteraction.Collide);
        CombatHitboxDebug.DrawSphere(drawHitboxes, center, effectiveBlastRadius, explosionVfxDuration);
        foreach (var h in hits)
        {
            if (IsOwnerCollider(h)) continue;

            var enemy = h.GetComponentInParent<Enemy>();
            if (enemy != null && ignoreEnemy != null && enemy == ignoreEnemy) continue;
            if (owner.CanDamage(enemy))
            {
                float dmg = shotDamage * blastDamageMultiplier;
                DealDamageTo(enemy, dmg);
            }
            var ph = h.GetComponentInParent<PlayerHealth>();
            if (ph != null && owner.CanDamageSoul())
            {
                DealDamageToPlayer(ph, shotDamage * blastDamageMultiplier);
            }
        }
    }

    private void CutIncomingProjectiles(Vector3 center, Vector3 forward, float radius)
    {
        Collider[] candidates = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < candidates.Length; i++)
        {
            Projectile projectile = candidates[i].GetComponentInParent<Projectile>();
            if (projectile == null || projectile.ownerEnemy == owner) continue;
            Vector3 incoming = projectile.transform.forward;
            incoming.y = 0f;
            if (incoming.sqrMagnitude > 0.0001f && Vector3.Dot(incoming.normalized, forward) >= 0f) continue;
            VfxPool.ReleaseOrDestroy(projectile.gameObject);
        }
    }

    private bool IsOwnerCollider(Collider collider)

    {
        return collider == null || owner == null ||
               collider.transform.IsChildOf(owner.transform) ||
               owner.transform.IsChildOf(collider.transform);
    }

    private void SpawnExplosionVfx(Vector3 center)
    {
        if (explosionVfxPrefab == null)
        {
            Debug.LogWarning("[SwordQi] explosionVfxPrefab is NULL — assign one in the Inspector");
            return;
        }

        Vector3 expPos = center + explosionVfxPositionOffset;
        Quaternion expRot = Quaternion.Euler(explosionVfxRotationOffset);
        SpawnVfxTracked(explosionVfxPrefab, expPos, expRot, explosionVfxDuration);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying && owner != null ? owner.transform.position : transform.position;
        Vector3 forward = Application.isPlaying && owner != null ? owner.transform.forward : transform.forward;

        Gizmos.color = new Color(0.8f, 0.8f, 0.2f, 0.5f);
        Gizmos.DrawLine(origin, origin + forward * maxRange);

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(origin + forward * maxRange, blastRadius);

        Gizmos.color = new Color(0.8f, 0.8f, 0.2f, 0.2f);
        Vector3 right = Quaternion.Euler(0, 90, 0) * forward;
        Vector3 halfW = right * (projectileWidth * 0.5f);
        Gizmos.DrawLine(origin - halfW, origin + halfW);
        Gizmos.DrawLine(origin + forward * maxRange - halfW, origin + forward * maxRange + halfW);
    }
}
