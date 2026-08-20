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

    [Header("Debug")]
    public bool debugLogging = true;

    // Set by the ability that fires this projectile
    [HideInInspector] public EnemyAbility_SweepPull ownerAbility;
    [HideInInspector] public Transform ownerTransform;   // the enemy that fired this hook
    [HideInInspector] public bool hitPlayer = false;
    [HideInInspector] public LayerMask hitMask = -1;     // who to hit (~0 when possessed, targetMask when AI)

    private float lifetime;
    private float hitCheckTimer;
    private Vector3 lastHitCheckPosition;

    void Start()
    {
        lifetime = maxLifetime;
        hitCheckTimer = hitCheckInterval;
        lastHitCheckPosition = transform.position;
        if (debugLogging)
            Debug.Log($"[Hook] Launched owner={ownerTransform?.name ?? "none"} position={transform.position:F2} forward={transform.forward:F2} radius={hitRadius:F2}");
    }

    void Update()
    {
        float deltaTime = ownerAbility != null && ownerAbility.IsOwnedByPlayer ? Time.unscaledDeltaTime : Time.deltaTime;
        // Fly forward
        transform.position += transform.forward * speed * deltaTime;

        // Hit detection
        hitCheckTimer -= deltaTime;
        if (hitCheckTimer <= 0)
        {
            hitCheckTimer = hitCheckInterval;
            CheckHit(lastHitCheckPosition, transform.position);
            lastHitCheckPosition = transform.position;
        }

        // Timeout
        lifetime -= deltaTime;
        if (lifetime <= 0)
        {
            // Timeout — notify ability that nothing was hit
            if (debugLogging)
                Debug.Log($"[Hook] Missed owner={ownerTransform?.name ?? "none"} position={transform.position:F2} forward={transform.forward:F2}");
            if (ownerAbility != null) ownerAbility.OnHookMissed();
            Destroy(gameObject);
        }
    }

    void CheckHit(Vector3 from, Vector3 to)
    {
        Vector3 displacement = to - from;
        float distance = displacement.magnitude;
        CombatHitboxDebug.DrawCapsule(true, from, to, hitRadius, 0f);
        if (distance > 0.0001f)
        {
            RaycastHit[] sweptHits = Physics.SphereCastAll(from, hitRadius, displacement / distance, distance, hitMask, QueryTriggerInteraction.Collide);
            foreach (var hit in sweptHits)
                if (TryHitCollider(hit.collider, "sweep")) return;
        }

        Collider[] hits = Physics.OverlapSphere(to, hitRadius, hitMask, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            if (TryHitCollider(h, "overlap")) return;
        }
    }

    bool TryHitCollider(Collider collider, string source)
    {
        if (collider == null) return false;
        if (ownerTransform != null && collider.transform.IsChildOf(ownerTransform)) return false;
        if (collider.gameObject == gameObject) return false;

        var ph = collider.GetComponentInParent<PlayerHealth>();
        if (ph != null)
        {
            if (debugLogging) Debug.Log($"[Hook] Hit player via {source}: collider={collider.name}");
            HitTarget(ph.transform, true);
            return true;
        }

        var enemy = collider.GetComponentInParent<Enemy>();
        if (enemy == null) return false;
        if (enemy.transform == ownerTransform || enemy.isDowned || enemy.isPossessed)
        {
            if (debugLogging)
                Debug.Log($"[Hook] Ignored enemy via {source}: target={enemy.name} downed={enemy.isDowned} possessed={enemy.isPossessed}");
            return false;
        }

        if (debugLogging) Debug.Log($"[Hook] Hit enemy via {source}: target={enemy.name} collider={collider.name}");
        HitTarget(enemy.transform, false);
        return true;
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
