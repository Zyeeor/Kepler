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
    [Tooltip("Pride.Pierce: designer VFX spawned once on first pierce hit (or at max range if nothing was hit).")]
    public GameObject pierceVfxPrefab;
    [Tooltip("Auto-destroy duration for pierceVfxPrefab. <=0 keeps it until owner death tracker cleans up.")]
    public float pierceVfxDuration = 1f;
    [Tooltip("World-space position offset for the pierce VFX.")]
    public Vector3 pierceVfxPositionOffset = Vector3.zero;
    [Tooltip("Rotation offset for the pierce VFX relative to the travel direction.")]
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
        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Skill");
        StartCoroutine(SwordQiRoutine());
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

        float effectiveMaxRange = IsUpgradeUnlocked("Pride01") ? pride01MaxRange : maxRange;
        float shotDamage = overrideDamage > 0f ? overrideDamage : GetShotDamage();

        if (IsUpgradeUnlocked("Pride02"))
        {
            StartCoroutine(LaunchProjectile(forward, effectiveMaxRange, shotDamage, spawnPierceVfx: false, ignoreEnemy));
            Vector3 left = Quaternion.Euler(0f, -pride02SpreadAngle, 0f) * forward;
            StartCoroutine(LaunchProjectile(left, effectiveMaxRange, shotDamage, spawnPierceVfx: false, ignoreEnemy));
            Vector3 right = Quaternion.Euler(0f, pride02SpreadAngle, 0f) * forward;
            StartCoroutine(LaunchProjectile(right, effectiveMaxRange, shotDamage, spawnPierceVfx: false, ignoreEnemy));
        }
        else
        {
            StartCoroutine(LaunchProjectile(forward, effectiveMaxRange, shotDamage, spawnPierceVfx: false, ignoreEnemy));
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
        if (IsUpgradeUnlocked("Pride01"))
            effectiveMaxRange = pride01MaxRange;

        bool pride02 = IsUpgradeUnlocked("Pride02");
        float shotDamage = GetShotDamage();
        bool pierce = IsUpgradeUnlocked("Pride.Pierce");

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
    }

    private float GetShotDamage()
    {
        float shotDamage = damage;
        if (IsUpgradeUnlocked("Pride.Pierce"))
            shotDamage *= pierceDamageMultiplier;
        return shotDamage;
    }

    IEnumerator LaunchProjectile(Vector3 forward, float effectiveMaxRange, float shotDamage, bool spawnPierceVfx, Enemy ignoreEnemy = null)
    {
        Vector3 origin = owner.transform.position;
        Vector3 currentPos = origin + forward * 1f;
        float traveled = 0f;
        bool pierce = IsUpgradeUnlocked("Pride.Pierce");
        var hitIds = new HashSet<int>();
        bool spawnedPierceLook = false;
        if (ignoreEnemy != null)
            hitIds.Add(ignoreEnemy.GetInstanceID());

        GameObject projVfx = null;
        if (projectileVfxPrefab != null)
        {
            Vector3 spawnPos = currentPos + owner.transform.TransformDirection(projectileVfxPositionOffset);
            Quaternion projRot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(projectileVfxRotationOffset);
            projVfx = SpawnVfxTracked(projectileVfxPrefab, spawnPos, projRot);
            projVfx.transform.localScale *= projectileVfxScale;
        }

        while (traveled < effectiveMaxRange)
        {
            float step = projectileSpeed * AbilityDeltaTime;
            traveled += step;
            currentPos = origin + forward * Mathf.Min(traveled, effectiveMaxRange);

            if (projVfx != null)
            {
                projVfx.transform.position = currentPos;
                projVfx.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            Vector3 halfExtents = new Vector3(projectileWidth * 0.5f, projectileHeight * 0.5f, step * 0.5f);
            Vector3 checkCenter = currentPos - forward * (step * 0.5f);
            Quaternion checkRot = Quaternion.LookRotation(forward, Vector3.up);
            CombatHitboxDebug.DrawBox(drawHitboxes, checkCenter, halfExtents, checkRot);

            int layerMask = owner.isPossessed ? ~0 : targetMask;
            Collider[] hits = Physics.OverlapBox(checkCenter, halfExtents, checkRot, layerMask, QueryTriggerInteraction.Collide);
            bool hitSomething = false;
            Vector3 hitPos = currentPos;

            foreach (var h in hits)
            {
                var enemy = h.GetComponentInParent<Enemy>();
                if (enemy != null && ignoreEnemy != null && enemy == ignoreEnemy)
                    continue;
                if (owner.CanDamage(enemy))
                {
                    int id = enemy.GetInstanceID();
                    if (!hitIds.Add(id)) continue;
                    DealDamageTo(enemy, shotDamage);
                    hitSomething = true;
                    hitPos = enemy.transform.position;
                }
                var ph = h.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    int id = ph.GetInstanceID();
                    if (!hitIds.Add(id)) continue;
                    DealDamageToPlayer(ph, shotDamage);
                    hitSomething = true;
                    hitPos = ph.transform.position;
                }
            }

            if (hitSomething)
            {
                if (pierce)
                {
                    if (spawnPierceVfx && !spawnedPierceLook)
                    {
                        spawnedPierceLook = true;
                        SpawnPierceVfx(hitPos, forward);
                    }
                    // Keep traveling through targets.
                }
                else
                {
                    if (projVfx != null) Destroy(projVfx);
                    DoExplosion(hitPos, shotDamage, ignoreEnemy);
                    yield break;
                }
            }

            yield return null;
        }

        if (projVfx != null) Destroy(projVfx);
        if (!pierce)
            DoExplosion(currentPos, shotDamage, ignoreEnemy);
        else if (spawnPierceVfx && !spawnedPierceLook)
            SpawnPierceVfx(currentPos, forward);
    }

    private void SpawnPierceVfx(Vector3 origin, Vector3 axis)
    {
        if (pierceVfxPrefab == null) return;

        Vector3 forward = axis;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = owner != null ? owner.transform.forward : Vector3.forward;
        forward.Normalize();

        Vector3 pos = origin + pierceVfxPositionOffset;
        Quaternion rot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(pierceVfxRotationOffset);
        SpawnVfxTracked(pierceVfxPrefab, pos, rot, pierceVfxDuration);
    }

    void DoExplosion(Vector3 center, float shotDamage, Enemy ignoreEnemy = null)
    {
        if (explosionVfxPrefab != null)
        {
            Vector3 expPos = center + explosionVfxPositionOffset;
            Quaternion expRot = Quaternion.Euler(explosionVfxRotationOffset);
            SpawnVfxTracked(explosionVfxPrefab, expPos, expRot, explosionVfxDuration);
        }
        else
        {
            Debug.LogWarning("[SwordQi] explosionVfxPrefab is NULL — assign one in the Inspector");
        }

        int layerMask = owner.isPossessed ? ~0 : targetMask;
        Collider[] hits = Physics.OverlapSphere(center, blastRadius, layerMask, QueryTriggerInteraction.Collide);
        CombatHitboxDebug.DrawSphere(drawHitboxes, center, blastRadius);
        foreach (var h in hits)
        {
            var enemy = h.GetComponentInParent<Enemy>();
            if (enemy != null && ignoreEnemy != null && enemy == ignoreEnemy) continue;
            if (owner.CanDamage(enemy))
            {
                float dmg = shotDamage * blastDamageMultiplier;
                DealDamageTo(enemy, dmg);
            }
            var ph = h.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                DealDamageToPlayer(ph, shotDamage * blastDamageMultiplier);
            }
        }
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
