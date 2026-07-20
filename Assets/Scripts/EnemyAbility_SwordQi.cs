using UnityEngine;
using System.Collections;

/// <summary>
/// Skill: 剑气 - Sword Qi. Fires a directed sword-energy projectile that travels forward.
/// Upon hitting an enemy or reaching max range, it explodes in an AoE dealing damage
/// to all enemies within the blast radius.
/// </summary>
public class EnemyAbility_SwordQi : EnemyAbility
{
    [Header("Projectile")]
    [Tooltip("How far the sword qi travels (meters)")]
    public float maxRange = 12f;
    [Tooltip("How fast the projectile moves (m/s)")]
    public float projectileSpeed = 15f;
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

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "剑气";
        cooldown = cooldown <= 0f ? 8f : cooldown; // default 8s CD
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
        StartCoroutine(SwordQiRoutine());
    }

    IEnumerator SwordQiRoutine()
    {
        Vector3 origin = owner.transform.position;
        Vector3 forward = owner.transform.forward;
        Vector3 currentPos = origin + forward * 1f; // start slightly in front of owner
        float traveled = 0f;

        // Spawn projectile VFX
        GameObject projVfx = null;
        if (projectileVfxPrefab != null)
        {
            Vector3 spawnPos = currentPos + owner.transform.TransformDirection(projectileVfxPositionOffset);
            Quaternion projRot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(projectileVfxRotationOffset);
            projVfx = Instantiate(projectileVfxPrefab, spawnPos, projRot);
            PlayVfx(projVfx);
            projVfx.transform.localScale *= projectileVfxScale;
        }

        // --- Travel loop ---
        while (traveled < maxRange)
        {
            float step = projectileSpeed * Time.deltaTime;
            traveled += step;
            currentPos = origin + forward * Mathf.Min(traveled, maxRange);

            // Move VFX
            if (projVfx != null)
            {
                projVfx.transform.position = currentPos;
                projVfx.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            // Check for hits along the way
            Vector3 halfExtents = new Vector3(projectileWidth * 0.5f, projectileHeight * 0.5f, step * 0.5f);
            Vector3 checkCenter = currentPos - forward * (step * 0.5f);
            Quaternion checkRot = Quaternion.LookRotation(forward, Vector3.up);

            int layerMask = owner.isPossessed ? ~0 : targetMask;
            Collider[] hits = Physics.OverlapBox(checkCenter, halfExtents, checkRot, layerMask, QueryTriggerInteraction.Collide);
            bool hitSomething = false;
            Vector3 hitPos = currentPos;

            foreach (var h in hits)
            {
                var enemy = h.GetComponentInParent<Enemy>();
                if (enemy != null && enemy != owner && !enemy.isDowned && !enemy.isPossessed)
                {
                    // Direct hit: full damage
                    DealDamageTo(enemy, damage);
                    hitSomething = true;
                    hitPos = enemy.transform.position;
                }
                var ph = h.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    DealDamageToPlayer(ph, damage);
                    hitSomething = true;
                    hitPos = ph.transform.position;
                }
            }

            if (hitSomething)
            {
                if (projVfx != null) Destroy(projVfx);
                DoExplosion(hitPos);
                yield break;
            }

            yield return null;
        }

        // Reached max range without hitting anything — explode at final position
        if (projVfx != null) Destroy(projVfx);
        DoExplosion(currentPos);
    }

    /// <summary>
    /// AoE explosion at the given position. Damages all enemies within blastRadius.
    /// </summary>
    void DoExplosion(Vector3 center)
    {
        // Spawn explosion VFX
        if (explosionVfxPrefab != null)
        {
            Vector3 expPos = center + explosionVfxPositionOffset;
            Quaternion expRot = Quaternion.Euler(explosionVfxRotationOffset);
            GameObject explosion = Instantiate(explosionVfxPrefab, expPos, expRot);
            PlayVfx(explosion);
            Destroy(explosion, explosionVfxDuration);
        }

        // AoE damage — when possessed, hit all layers (to damage other enemies)
        int layerMask = owner.isPossessed ? ~0 : targetMask;
        Collider[] hits = Physics.OverlapSphere(center, blastRadius, layerMask, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var enemy = h.GetComponentInParent<Enemy>();
            if (enemy != null && enemy != owner && !enemy.isDowned && !enemy.isPossessed)
            {
                // Blast damage: reduced multiplier (enemies already hit directly get less from blast)
                float dmg = damage * blastDamageMultiplier;
                DealDamageTo(enemy, dmg);
            }
            var ph = h.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                DealDamageToPlayer(ph, damage * blastDamageMultiplier);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying && owner != null ? owner.transform.position : transform.position;
        Vector3 forward = Application.isPlaying && owner != null ? owner.transform.forward : transform.forward;

        // Draw projectile path
        Gizmos.color = new Color(0.8f, 0.8f, 0.2f, 0.5f);
        Gizmos.DrawLine(origin, origin + forward * maxRange);

        // Draw blast radius at max range
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.3f);
        Gizmos.DrawWireSphere(origin + forward * maxRange, blastRadius);

        // Draw projectile width
        Gizmos.color = new Color(0.8f, 0.8f, 0.2f, 0.2f);
        Vector3 right = Quaternion.Euler(0, 90, 0) * forward;
        Vector3 halfW = right * (projectileWidth * 0.5f);
        Gizmos.DrawLine(origin - halfW, origin + halfW);
        Gizmos.DrawLine(origin + forward * maxRange - halfW, origin + forward * maxRange + halfW);
    }
}
