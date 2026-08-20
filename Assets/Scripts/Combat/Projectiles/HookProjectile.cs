using System;
using UnityEngine;

/// <summary>
/// Hook projectile: flies forward until max distance, a target, or (AnchorStop) a solid obstacle.
/// PullTargets — used by SweepPull (hit actor → pull callback; miss → miss callback).
/// AnchorStop — used by Wrath self-grapple (stop position → dash landing).
/// </summary>
public class HookProjectile : MonoBehaviour
{
    public enum FlightMode
    {
        PullTargets = 0,
        AnchorStop = 1
    }

    [Header("Movement")]
    public float speed = 25f;
    public float maxLifetime = 3f;
    public float maxTravelDistance = 8f;

    [Header("Hit Detection")]
    public float hitRadius = 0.5f;
    public float hitCheckInterval = 0.03f;
    public LayerMask obstacleMask = ~0;

    [Header("VFX")]
    public GameObject hitVfxPrefab;
    public float hitVfxDuration = 0.5f;

    [Header("Debug")]
    public bool debugLogging = true;

    [HideInInspector] public FlightMode flightMode = FlightMode.PullTargets;
    [HideInInspector] public EnemyAbility_SweepPull ownerAbility;
    [HideInInspector] public Transform ownerTransform;
    [HideInInspector] public bool hitPlayer;
    [HideInInspector] public LayerMask hitMask = -1;
    [HideInInspector] public bool useUnscaledTime;
    [HideInInspector] public Action<Vector3> onAnchorStop;

    private float lifetime;
    private float hitCheckTimer;
    private float traveled;
    private Vector3 lastHitCheckPosition;
    private bool finished;

    void OnEnable()
    {
        ResetForPoolSpawn();
    }

    public void ResetForPoolSpawn()
    {
        lifetime = maxLifetime;
        hitCheckTimer = hitCheckInterval;
        traveled = 0f;
        finished = false;
        hitPlayer = false;
        lastHitCheckPosition = transform.position;
        onAnchorStop = null;
        // flightMode / masks / callbacks are reassigned by the firing ability after reset.
    }

    void Update()
    {
        if (finished) return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (deltaTime <= 0f) return;

        Vector3 from = transform.position;
        float step = speed * deltaTime;
        float remaining = Mathf.Max(0f, maxTravelDistance - traveled);
        bool reachedMax = false;
        if (step >= remaining)
        {
            step = remaining;
            reachedMax = true;
        }

        Vector3 to = from + transform.forward * step;
        transform.position = to;
        traveled += step;

        hitCheckTimer -= deltaTime;
        if (hitCheckTimer <= 0f || reachedMax)
        {
            hitCheckTimer = hitCheckInterval;
            if (TryResolveHitAlongSegment(lastHitCheckPosition, transform.position))
            {
                lastHitCheckPosition = transform.position;
                return;
            }
            lastHitCheckPosition = transform.position;
        }

        if (reachedMax || traveled >= maxTravelDistance - 0.0001f)
        {
            if (flightMode == FlightMode.AnchorStop)
            {
                FinishAnchor(transform.position);
                return;
            }

            // PullTargets: max range counts as a miss.
            if (debugLogging)
                Debug.Log($"[Hook] Missed (max range) owner={ownerTransform?.name ?? "none"} position={transform.position:F2}");
            NotifyPullMissAndRelease();
            return;
        }

        lifetime -= deltaTime;
        if (lifetime <= 0f)
        {
            if (flightMode == FlightMode.AnchorStop)
            {
                FinishAnchor(transform.position);
                return;
            }

            if (debugLogging)
                Debug.Log($"[Hook] Missed owner={ownerTransform?.name ?? "none"} position={transform.position:F2} forward={transform.forward:F2}");
            NotifyPullMissAndRelease();
        }
    }

    private bool TryResolveHitAlongSegment(Vector3 from, Vector3 to)
    {
        Vector3 displacement = to - from;
        float distance = displacement.magnitude;
        CombatHitboxDebug.DrawCapsule(true, from, to, hitRadius, 0f);

        if (distance > 0.0001f)
        {
            Vector3 dir = displacement / distance;
            RaycastHit[] sweptHits = Physics.SphereCastAll(from, hitRadius, dir, distance, hitMask, QueryTriggerInteraction.Collide);
            Array.Sort(sweptHits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (RaycastHit hit in sweptHits)
            {
                if (TryHandleHit(hit.collider, hit.point, "sweep"))
                    return true;
            }
        }

        Collider[] overlaps = Physics.OverlapSphere(to, hitRadius, hitMask, QueryTriggerInteraction.Collide);
        foreach (Collider col in overlaps)
        {
            if (TryHandleHit(col, to, "overlap"))
                return true;
        }

        return false;
    }

    private bool TryHandleHit(Collider collider, Vector3 hitPoint, string source)
    {
        if (collider == null) return false;
        if (ownerTransform != null && collider.transform.IsChildOf(ownerTransform)) return false;
        if (collider.gameObject == gameObject) return false;

        PlayerHealth player = collider.GetComponentInParent<PlayerHealth>();
        if (player != null)
        {
            if (debugLogging) Debug.Log($"[Hook] Hit player via {source}: collider={collider.name}");
            if (flightMode == FlightMode.AnchorStop)
            {
                FinishAnchor(FlattenStopPoint(hitPoint));
                return true;
            }

            HitPullTarget(player.transform, true);
            return true;
        }

        Enemy enemy = collider.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            if (enemy.transform == ownerTransform || enemy.isDowned || enemy.isPossessed)
            {
                if (debugLogging)
                    Debug.Log($"[Hook] Ignored enemy via {source}: target={enemy.name} downed={enemy.isDowned} possessed={enemy.isPossessed}");
                return false;
            }

            if (debugLogging) Debug.Log($"[Hook] Hit enemy via {source}: target={enemy.name} collider={collider.name}");
            if (flightMode == FlightMode.AnchorStop)
            {
                FinishAnchor(FlattenStopPoint(hitPoint));
                return true;
            }

            HitPullTarget(enemy.transform, false);
            return true;
        }

        if (flightMode != FlightMode.AnchorStop) return false;
        if (collider.isTrigger) return false;
        if (((1 << collider.gameObject.layer) & obstacleMask) == 0) return false;

        if (debugLogging) Debug.Log($"[Hook] Hit obstacle via {source}: collider={collider.name}");
        // Stop slightly before the surface so the body does not embed.
        Vector3 stop = hitPoint - transform.forward * 0.15f;
        FinishAnchor(FlattenStopPoint(stop));
        return true;
    }

    private Vector3 FlattenStopPoint(Vector3 point)
    {
        if (ownerTransform != null)
            point.y = ownerTransform.position.y;
        return point;
    }

    private void HitPullTarget(Transform target, bool isPlayer)
    {
        if (finished) return;
        finished = true;
        hitPlayer = isPlayer;

        if (hitVfxPrefab != null)
        {
            GameObject vfx = VfxPool.Instance.Spawn(hitVfxPrefab, target.position, Quaternion.identity);
            foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>())
                ps.Play(true);
            VfxPool.ReleaseOrDestroy(vfx, hitVfxDuration);
        }

        if (ownerAbility != null)
            ownerAbility.OnHookHitTarget(target, isPlayer);

        ownerAbility = null;
        ownerTransform = null;
        onAnchorStop = null;
        VfxPool.ReleaseOrDestroy(gameObject);
    }

    private void FinishAnchor(Vector3 position)
    {
        if (finished) return;
        finished = true;

        Action<Vector3> callback = onAnchorStop;
        onAnchorStop = null;
        ownerAbility = null;
        ownerTransform = null;
        callback?.Invoke(position);
        VfxPool.ReleaseOrDestroy(gameObject);
    }

    private void NotifyPullMissAndRelease()
    {
        if (finished) return;
        finished = true;
        if (ownerAbility != null) ownerAbility.OnHookMissed();
        ownerAbility = null;
        ownerTransform = null;
        onAnchorStop = null;
        VfxPool.ReleaseOrDestroy(gameObject);
    }
}
