using UnityEngine;
using System.Collections;

/// <summary>
/// Basic Attack: 一刀斩 - Flash Slash. Dashes forward a distance, damaging all enemies
/// along the dash path. A slash VFX trail is spawned during the dash.
/// </summary>
public class EnemyAbility_FlashSlash : EnemyAbility
{
    [Header("Dash")]
    [Tooltip("How far the owner dashes forward (meters)")]
    public float dashDistance = 6f;
    [Tooltip("How long the dash takes (seconds)")]
    public float dashDuration = 0.15f;

    [Header("Slash Hitbox")]
    [Tooltip("Width of the slash hitbox along the dash path")]
    public float slashWidth = 2f;
    [Tooltip("Height of the slash hitbox")]
    public float slashHeight = 2f;
    public LayerMask targetMask;

    [Header("Animation")]
    public string animTrigger = "FlashSlash";

    [Header("VFX - Dash Trail")]
    public GameObject dashTrailPrefab;      // trail VFX spawned at start and follows owner during dash
    public float dashTrailDuration = 0.5f;
    [Tooltip("Local position offset for the trail VFX (relative to owner's transform).")]
    public Vector3 dashTrailPositionOffset = Vector3.zero;
    [Tooltip("Delay in seconds before the trail VFX spawns. 0 = instant.")]
    public float dashTrailDelay = 0f;
    [Tooltip("Rotation offset for the trail VFX. E.g. (-90,0,0) if VFX faces Y-up but dash is Z-forward.")]
    public Vector3 dashTrailRotationOffset = Vector3.zero;

    [Header("VFX - Hit Impact")]
    public GameObject hitImpactPrefab;      // spawned at each hit enemy's position
    public float hitImpactDuration = 0.3f;
    [Tooltip("Local position offset for the hit impact VFX (world-space offset from hit point).")]
    public Vector3 hitImpactPositionOffset = Vector3.zero;
    [Tooltip("Delay in seconds before the hit impact VFX spawns. 0 = instant.")]
    public float hitImpactDelay = 0f;
    [Tooltip("Rotation offset for the hit impact VFX.")]
    public Vector3 hitImpactRotationOffset = Vector3.zero;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "一刀斩";
    }

    public override bool CanTrigger()
    {
        if (owner.isPossessed)
            return base.CanTrigger();
        return base.CanTrigger() && owner != null && owner.targetPlayer != null;
    }

    protected override void OnTrigger()
    {
        if (owner == null)
        {
            Debug.LogWarning("[FlashSlash] OnTrigger: owner is null, aborting");
            return;
        }
        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Basic");
        StartCoroutine(FlashSlashRoutine());
    }

    IEnumerator FlashSlashRoutine()
    {
        Vector3 startPos = owner.transform.position;
        Vector3 forward = owner.transform.forward;
        Vector3 endPos = startPos + forward * dashDistance;
        float halfDash = dashDuration * 0.5f;

        // Spawn dash trail VFX (with optional delay)
        GameObject trail = null;
        if (dashTrailPrefab != null)
        {
            if (dashTrailDelay > 0f)
                yield return new WaitForSeconds(dashTrailDelay);
            Vector3 trailPos = owner.transform.position + owner.transform.TransformDirection(dashTrailPositionOffset);
            Quaternion trailRot = owner.transform.rotation * Quaternion.Euler(dashTrailRotationOffset);
            trail = Instantiate(dashTrailPrefab, trailPos, trailRot, owner.transform);
            PlayVfx(trail);
            Debug.Log("[FlashSlash] Dash trail spawned: " + dashTrailPrefab.name + " at " + trailPos);
        }
        else
        {
            Debug.LogWarning("[FlashSlash] dashTrailPrefab is NULL - no trail VFX will be shown");
        }

        // Phase 1: dash forward (first half — wind-up into strike)
        float t = 0f;
        while (t < halfDash)
        {
            t += Time.deltaTime;
            float k = t / halfDash;
            // Ease-out: fast start, slow into position
            k = 1f - (1f - k) * (1f - k);
            owner.transform.position = Vector3.Lerp(startPos, endPos, k);
            yield return null;
        }

        // At midpoint: deal damage to all enemies along the path so far
        PerformSlashHit(startPos, owner.transform.position);

        // Phase 2: continue dash (second half)
        Vector3 midPos = owner.transform.position;
        t = 0f;
        while (t < halfDash)
        {
            t += Time.deltaTime;
            float k = t / halfDash;
            // Ease-in: slow out of strike
            k = k * k;
            owner.transform.position = Vector3.Lerp(midPos, endPos, k);
            yield return null;
        }

        // Final hit check for second half of the path
        PerformSlashHit(midPos, endPos);

        // Clean up: ensure final position
        owner.transform.position = endPos;

        // Destroy trail after duration
        if (trail != null)
        {
            trail.transform.SetParent(null);
            Destroy(trail, dashTrailDuration);
        }
    }

    /// <summary>
    /// Perform a box-shaped hit check along the line from start to end.
    /// Damages all enemies whose colliders intersect the box.
    /// </summary>
    void PerformSlashHit(Vector3 pathStart, Vector3 pathEnd)
    {
        Vector3 mid = (pathStart + pathEnd) * 0.5f;
        Vector3 direction = pathEnd - pathStart;
        float length = direction.magnitude;
        direction.y = 0f;
        direction.Normalize();

        // Use OverlapBox along the path center
        Vector3 halfExtents = new Vector3(slashWidth * 0.5f, slashHeight * 0.5f, length * 0.5f);
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

        // When possessed, hit all layers (to damage other enemies).
        // When AI-controlled, only hit targetMask (to avoid friendly fire).
        int layerMask = owner.isPossessed ? ~0 : targetMask;
        Collider[] hits = Physics.OverlapBox(mid, halfExtents, rotation, layerMask, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var ph = h.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                DealDamageToPlayer(ph, damage);
                SpawnHitImpact(h.transform.position);
            }
            var enemy = h.GetComponentInParent<Enemy>();
            if (enemy != null && enemy != owner && !enemy.isDowned && !enemy.isPossessed)
            {
                DealDamageTo(enemy, damage);
                SpawnHitImpact(enemy.transform.position);
            }
        }
    }

    void SpawnHitImpact(Vector3 position)
    {
        if (hitImpactPrefab == null) return;
        Vector3 impactPos = position + hitImpactPositionOffset;
        Quaternion impactRot = Quaternion.Euler(hitImpactRotationOffset);
        GameObject impact = Instantiate(hitImpactPrefab, impactPos, impactRot);
        PlayVfx(impact);
        Destroy(impact, hitImpactDuration);
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying && owner != null ? owner.transform.position : transform.position;
        Vector3 forward = Application.isPlaying && owner != null ? owner.transform.forward : transform.forward;
        Vector3 end = origin + forward * dashDistance;

        // Draw dash path
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        Gizmos.DrawLine(origin, end);

        // Draw slash width box outline
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        Vector3 mid = (origin + end) * 0.5f;
        Vector3 right = Quaternion.Euler(0, 90, 0) * forward;
        Vector3 halfW = right * (slashWidth * 0.5f);
        Vector3 halfL = forward * (dashDistance * 0.5f);
        Gizmos.DrawLine(mid - halfW - halfL, mid + halfW - halfL);
        Gizmos.DrawLine(mid + halfW - halfL, mid + halfW + halfL);
        Gizmos.DrawLine(mid + halfW + halfL, mid - halfW + halfL);
        Gizmos.DrawLine(mid - halfW + halfL, mid - halfW - halfL);

        // Draw start/end spheres
        Gizmos.DrawWireSphere(origin, slashWidth * 0.5f);
        Gizmos.DrawWireSphere(end, slashWidth * 0.5f);
    }
}
