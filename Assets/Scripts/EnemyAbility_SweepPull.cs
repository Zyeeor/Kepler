using UnityEngine;
using System.Collections;

/// <summary>
/// Skill: 横扫链勾 - Sweep Pull. Fires a hook projectile forward. On hit, spawns hit VFX,
/// then pulls the target back to the owner with a return trail VFX.
/// </summary>
public class EnemyAbility_SweepPull : EnemyAbility
{
    [Header("Hook Projectile")]
    public GameObject hookPrefab;           // prefab with HookProjectile component + VFX + Collider
    public float hookSpeed = 25f;
    public float hookMaxRange = 8f;

    [Header("Hit VFX")]
    public GameObject hitVfxPrefab;
    public float hitVfxDuration = 0.5f;

    [Header("Return VFX")]
    public GameObject returnVfxPrefab;      // VFX that follows the target back during pull (chain trail)
    public float returnVfxDuration = 2f;

    [Header("Pull")]
    public float pullSpeed = 15f;
    public float pullStopDistance = 1.5f;

    [Header("Damage")]
    public float damageMultiplier = 1.5f;

    [Header("Targeting")]
    [Tooltip("Who gets hit when AI-controlled. When possessed, always hits everything.")]
    public LayerMask targetMask = -1;

    [Header("Animation")]
    public string animTrigger = "SweepPull";

    // Set by HookProjectile callback
    private Transform pullTarget;
    private bool isPullingPlayer;
    private bool hookHit;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "横扫链勾";
        cooldown = cooldown <= 0f ? 8f : cooldown;
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
        hookHit = false;
        pullTarget = null;
        isPullingPlayer = false;
        StartCoroutine(SweepPullRoutine());
    }

    IEnumerator SweepPullRoutine()
    {
        // 1) Fire hook projectile forward
        Vector3 origin = owner.transform.position;
        Vector3 forward = owner.transform.forward;

        GameObject hookObj = null;
        HookProjectile hookProj = null;

        if (hookPrefab != null)
        {
            hookObj = Instantiate(hookPrefab, origin, Quaternion.LookRotation(forward, Vector3.up));
            hookProj = hookObj.GetComponent<HookProjectile>();
            if (hookProj != null)
            {
                hookProj.speed = hookSpeed;
                hookProj.maxLifetime = hookMaxRange / hookSpeed;
                hookProj.hitVfxPrefab = hitVfxPrefab;
                hookProj.hitVfxDuration = hitVfxDuration;
                hookProj.ownerAbility = this;
                hookProj.ownerTransform = owner.transform;
                hookProj.hitMask = owner.isPossessed ? ~0 : targetMask;
            }
            PlayVfx(hookObj);
        }
        else
        {
            // No prefab — create a simple invisible projectile
            hookObj = new GameObject("HookProj");
            hookObj.transform.position = origin;
            hookObj.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            hookProj = hookObj.AddComponent<HookProjectile>();
            hookProj.speed = hookSpeed;
            hookProj.maxLifetime = hookMaxRange / hookSpeed;
            hookProj.hitVfxPrefab = hitVfxPrefab;
            hookProj.hitVfxDuration = hitVfxDuration;
            hookProj.ownerAbility = this;
            hookProj.ownerTransform = owner.transform;
            hookProj.hitMask = owner.isPossessed ? ~0 : targetMask;
        }

        // 2) Wait for hook to hit or miss
        float timeout = hookMaxRange / hookSpeed + 0.5f;
        float elapsed = 0f;
        while (!hookHit && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3) If hook hit something, pull it
        if (hookHit && pullTarget != null)
        {
            // Damage target
            if (isPullingPlayer)
            {
                var ph = pullTarget.GetComponent<PlayerHealth>();
                if (ph != null) DealDamageToPlayer(ph, damage);
            }
            else
            {
                var enemy = pullTarget.GetComponent<Enemy>();
                if (enemy != null) DealDamageTo(enemy, damage * damageMultiplier);
            }

            // Return trail VFX (follows target)
            GameObject returnVfx = null;
            if (returnVfxPrefab != null)
            {
                returnVfx = Instantiate(returnVfxPrefab, pullTarget.position, Quaternion.identity);
                PlayVfx(returnVfx);
            }

            // Pull target
            yield return StartCoroutine(PullTarget(pullTarget));

            if (returnVfx != null)
                Destroy(returnVfx, returnVfxDuration);
        }

        // 4) Reset state for next use
        hookHit = false;
        pullTarget = null;
        isPullingPlayer = false;
    }

    /// <summary>Called by HookProjectile when it hits a valid target.</summary>
    public void OnHookHitTarget(Transform target, bool isPlayer)
    {
        hookHit = true;
        pullTarget = target;
        isPullingPlayer = isPlayer;
    }

    /// <summary>Called by HookProjectile when it times out without hitting anything.</summary>
    public void OnHookMissed()
    {
        hookHit = false;
        pullTarget = null;
    }

    IEnumerator PullTarget(Transform target)
    {
        if (target == null || owner == null) yield break;

        var trb = target.GetComponent<Rigidbody>();
        bool wasKinematic = false;
        if (trb != null) { wasKinematic = trb.isKinematic; trb.isKinematic = true; }

        while (target != null && owner != null)
        {
            Vector3 ownerPos = owner.transform.position;
            float dist = Vector3.Distance(target.position, ownerPos);
            if (dist <= pullStopDistance) break;

            target.position = Vector3.MoveTowards(target.position, ownerPos, pullSpeed * Time.deltaTime);
            yield return null;
        }

        if (trb != null) trb.isKinematic = wasKinematic;
    }
}
