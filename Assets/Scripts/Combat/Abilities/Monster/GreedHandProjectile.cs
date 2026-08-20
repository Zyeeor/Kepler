using UnityEngine;

/// <summary>
/// Fired Greed hand. Survives owner body switch; tracks a legal Enemy and settles damage via source ability helpers when available.
/// </summary>
public class GreedHandProjectile : MonoBehaviour
{
    public EnemyAbility_GreedHands sourceAbility;
    public Enemy ownerAtFire;
    public Enemy target;
    public float damage = 15f;
    public float moveSpeed = 14f;
    public float hitRadius = 0.45f;
    public float lifetime = 6f;
    public bool allowRetargetOnce;
    public bool canSpawnOnKill;
    public int spawnOnKillMin = 1;
    public int spawnOnKillMax = 2;
    public bool flankArc;
    public bool flankLeft;
    public float flankArcDuration = 0.35f;
    public float flankSideDistance = 2.2f;
    public GameObject hitVfxPrefab;
    public float hitVfxDuration = 0.8f;
    public bool isDerived;

    private float _expiresAt;
    private float _spawnTime;
    private Vector3 _spawnPos;
    private Vector3 _flankWaypoint;
    private bool _reachedFlank;
    private bool _retargetUsed;
    private bool _settled;

    public void Launch(
        EnemyAbility_GreedHands ability,
        Enemy firedBy,
        Enemy initialTarget,
        float handDamage,
        bool retarget,
        bool spawnOnKill,
        bool useFlank,
        bool leftFlank,
        bool derived,
        GameObject hitVfx)
    {
        sourceAbility = ability;
        ownerAtFire = firedBy;
        target = initialTarget;
        damage = handDamage;
        allowRetargetOnce = retarget;
        canSpawnOnKill = spawnOnKill && !derived;
        flankArc = useFlank;
        flankLeft = leftFlank;
        isDerived = derived;
        hitVfxPrefab = hitVfx;
        _settled = false;
        _retargetUsed = false;
        _reachedFlank = false;
        _spawnTime = Time.time;
        _expiresAt = Time.time + lifetime;
        _spawnPos = transform.position;
        if (flankArc && target != null)
        {
            Vector3 toTarget = target.transform.position - _spawnPos;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) toTarget = firedBy != null ? firedBy.transform.forward : Vector3.forward;
            toTarget.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toTarget) * (flankLeft ? -1f : 1f);
            _flankWaypoint = _spawnPos + side * flankSideDistance + toTarget * (flankSideDistance * 0.35f);
            _flankWaypoint.y = _spawnPos.y;
        }
        else
        {
            _reachedFlank = true;
        }
    }

    private void Update()
    {
        if (_settled) return;
        if (Time.time >= _expiresAt || target == null || !IsLegalTarget(target))
        {
            VfxPool.ReleaseOrDestroy(gameObject);
            return;
        }

        Vector3 goal;
        if (flankArc && !_reachedFlank)
        {
            goal = _flankWaypoint;
            if ((transform.position - _flankWaypoint).sqrMagnitude <= 0.08f * 0.08f
                || Time.time - _spawnTime >= flankArcDuration)
                _reachedFlank = true;
        }
        else
        {
            goal = target.transform.position + Vector3.up * 0.6f;
        }

        transform.position = Vector3.MoveTowards(transform.position, goal, moveSpeed * Time.deltaTime);
        Vector3 look = goal - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

        if (_reachedFlank && Vector3.Distance(transform.position, target.transform.position) <= hitRadius + 0.35f)
        {
            CombatHitboxDebug.DrawSphere(true, transform.position, hitRadius + 0.35f, 0f);
            SettleHit();
        }
    }

    private void SettleHit()
    {
        if (_settled || target == null) return;
        _settled = true;

        Enemy hitTarget = target;
        Vector3 hitPos = hitTarget.transform.position + Vector3.up * 0.5f;
        if (sourceAbility != null)
            sourceAbility.SettleHandHit(hitTarget, damage);
        else
            hitTarget.TakeDamage(damage);

        bool killed = hitTarget == null
            || hitTarget.isDowned
            || hitTarget.currentHealth <= 0f
            || hitTarget.Body == MonsterActor.BodyState.Downed
            || hitTarget.Body == MonsterActor.BodyState.Fading
            || hitTarget.Body == MonsterActor.BodyState.Despawned;

        if (hitVfxPrefab != null)
        {
            GameObject vfx = VfxPool.Instance.Spawn(hitVfxPrefab, hitPos, Quaternion.identity);
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
                ps.Play(true);
            VfxPool.ReleaseOrDestroy(vfx, Mathf.Max(0.05f, hitVfxDuration));
        }

        if (killed)
        {
            if (allowRetargetOnce && !_retargetUsed && sourceAbility != null)
            {
                Enemy next = sourceAbility.FindNearestLegalTarget(transform.position, hitTarget);
                if (next != null)
                {
                    _retargetUsed = true;
                    _settled = false;
                    target = next;
                    return;
                }
            }

            if (canSpawnOnKill && sourceAbility != null)
                sourceAbility.SpawnDerivedHandsFromKill(transform.position, this);
        }

        VfxPool.ReleaseOrDestroy(gameObject);
    }

    private static bool IsLegalTarget(Enemy enemy)
    {
        return enemy != null
            && !enemy.isDowned
            && enemy.Body != MonsterActor.BodyState.Fading
            && enemy.Body != MonsterActor.BodyState.Despawned
            && enemy.currentHealth > 0f;
    }
}
