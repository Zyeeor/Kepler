using UnityEngine;
using System.Collections;

/// <summary>
/// A projectile that flies forward, detects the first valid target (player or enemy),
/// triggers a hit VFX, and reports back to the ability that fired it to initiate the pull.
/// </summary>
public class HookProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 25f;
    public float maxLifetime = 3f;

    [Header("Hit Detection")]
    public float hitRadius = 0.5f;
    public float hitCheckInterval = 0.03f;

    [Header("VFX")]
    public GameObject hitVfxPrefab;
    public float hitVfxDuration = 0.5f;

    // Set by the ability that fires this projectile
    [HideInInspector] public EnemyAbility_SweepPull ownerAbility;
    [HideInInspector] public Transform ownerTransform;   // the enemy that fired this hook
    [HideInInspector] public bool hitPlayer = false;
    [HideInInspector] public LayerMask hitMask = -1;     // who to hit (~0 when possessed, targetMask when AI)

    private float lifetime;
    private float hitCheckTimer;

    void Start()
    {
        lifetime = maxLifetime;
        hitCheckTimer = hitCheckInterval;
    }

    void Update()
    {
        // Fly forward
        transform.position += transform.forward * speed * Time.deltaTime;

        // Hit detection
        hitCheckTimer -= Time.deltaTime;
        if (hitCheckTimer <= 0)
        {
            hitCheckTimer = hitCheckInterval;
            CheckHit();
        }

        // Timeout
        lifetime -= Time.deltaTime;
        if (lifetime <= 0)
        {
            // Timeout — notify ability that nothing was hit
            if (ownerAbility != null) ownerAbility.OnHookMissed();
            Destroy(gameObject);
        }
    }

    void CheckHit()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius, hitMask, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            // Don't hit the owner
            if (ownerTransform != null && h.transform.IsChildOf(ownerTransform))
                continue;

            // Don't hit self
            if (h.gameObject == gameObject) continue;

            // Check player
            var ph = h.GetComponentInParent<PlayerHealth>();
            if (ph != null)
            {
                HitTarget(ph.transform, true);
                return;
            }

            // Check other enemies
            var enemy = h.GetComponentInParent<Enemy>();
            if (enemy != null && enemy.transform != ownerTransform && !enemy.isDowned && !enemy.isPossessed)
            {
                HitTarget(enemy.transform, false);
                return;
            }
        }
    }

    void HitTarget(Transform target, bool isPlayer)
    {
        hitPlayer = isPlayer;

        // Hit VFX
        if (hitVfxPrefab != null)
        {
            var vfx = Instantiate(hitVfxPrefab, target.position, Quaternion.identity);
            Destroy(vfx, hitVfxDuration);
        }

        // Notify ability to start pull
        if (ownerAbility != null)
            ownerAbility.OnHookHitTarget(target, isPlayer);

        Destroy(gameObject);
    }
}
