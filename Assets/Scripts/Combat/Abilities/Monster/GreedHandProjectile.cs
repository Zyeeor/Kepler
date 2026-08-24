using UnityEngine;

/// <summary>
/// Fired Greed hand. Survives owner body switch; tracks a legal Enemy and settles damage via source ability helpers when available.
/// </summary>
public class GreedHandProjectile : MonoBehaviour
{
    public EnemyAbility_GreedHands sourceAbility;
    public Enemy ownerAtFire;
    public Transform target;

    public float damage = 15f;
    public float moveSpeed = 20f;
    public float hitRadius = 0.8f;

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
    public float hitVfxDuration = 1f;

    public bool isDerived;

    private const float BaseCurveAngle = 20f;
    private const float FlankTurnSpeed = 90f;
    private const float MinimumFlightDuration = 0.3f;


    private float _expiresAt;
    private float _canHitAt;
    private float _curveBiasEndsAt;
    private float _homingTurnRate;
    private float _homingCurveStrength;

    private Vector3 _travelDirection;

    private bool _retargetUsed;
    private bool _settled;


    public void Launch(
        EnemyAbility_GreedHands ability,
        Enemy firedBy,
        Transform initialTarget,

        float handDamage,
        bool retarget,
        bool spawnOnKill,
        bool useFlank,
        bool leftFlank,
        bool derived,
        GameObject hitVfx,
        float impactDuration,
        float homingMoveSpeed,
        float homingTurnRate,
        float homingCurveStrength)

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
        hitVfxDuration = Mathf.Max(0.05f, impactDuration);
        moveSpeed = Mathf.Max(0f, homingMoveSpeed);
        _homingTurnRate = Mathf.Max(0f, homingTurnRate);
        _homingCurveStrength = Mathf.Clamp01(homingCurveStrength);
        _settled = false;

        _retargetUsed = false;
        _expiresAt = Time.time + lifetime;
        _canHitAt = Time.time + MinimumFlightDuration;
        _curveBiasEndsAt = Time.time + (flankArc ? Mathf.Max(0f, flankArcDuration) : 0f);


        Vector3 toTarget = target != null
            ? target.position + Vector3.up * 0.6f - transform.position

            : (firedBy != null ? firedBy.transform.forward : Vector3.forward);
        if (toTarget.sqrMagnitude < 0.01f)
            toTarget = firedBy != null ? firedBy.transform.forward : Vector3.forward;
        toTarget.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, toTarget);
        if (side.sqrMagnitude < 0.01f) side = Vector3.right;
        if (flankLeft) side = -side;
        float curveAngle = flankArc
            ? Mathf.Clamp(flankSideDistance * 25f, BaseCurveAngle, 75f)
            : BaseCurveAngle;
        _travelDirection = (toTarget + side.normalized * Mathf.Tan(curveAngle * Mathf.Deg2Rad)).normalized;

    }

    private void Update()
    {
        if (_settled) return;
        if (Time.time >= _expiresAt || target == null || !IsLegalTarget(target))
        {
            VfxPool.ReleaseOrDestroy(gameObject);
            return;
        }

        float deltaTime = ownerAtFire != null && ownerAtFire.IsPlayerControlled
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
        Vector3 goal = target.position + Vector3.up * 0.6f;

        Vector3 toGoal = goal - transform.position;
        if (toGoal.sqrMagnitude > 0.001f)
        {
            float turnSpeed = flankArc && Time.time < _curveBiasEndsAt
                ? FlankTurnSpeed
                : _homingTurnRate;
            Vector3 desiredDirection = Vector3.Slerp(
                _travelDirection,
                toGoal.normalized,
                _homingCurveStrength).normalized;

            _travelDirection = Vector3.RotateTowards(
                _travelDirection,
                desiredDirection,
                turnSpeed * Mathf.Deg2Rad * deltaTime,
                1f);
            transform.position += _travelDirection * (moveSpeed * deltaTime);
        }


        Vector3 look = _travelDirection;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

        float effectiveHitRadius = hitRadius;
        if (sourceAbility != null && sourceAbility.OwnerMonster != null)
            effectiveHitRadius *= sourceAbility.OwnerMonster.PossessionCombatScaleMultiplier;
        if (Time.time >= _canHitAt
            && Vector3.Distance(transform.position, target.position) <= effectiveHitRadius)

        {
            CombatHitboxDebug.DrawSphere(true, transform.position, effectiveHitRadius, 0f);

            SettleHit();
        }


    }

    private void SettleHit()
    {
        if (_settled || target == null) return;
        _settled = true;

        Enemy hitEnemy = target.GetComponentInParent<Enemy>();
        PlayerHealth hitPlayer = hitEnemy == null ? target.GetComponentInParent<PlayerHealth>() : null;
        Vector3 hitPos = transform.position;
        bool killed = false;

        if (hitEnemy != null)
        {
            if (sourceAbility != null)
                sourceAbility.SettleHandHit(hitEnemy, damage);
            else
                hitEnemy.TakeDamage(damage);

            killed = hitEnemy.isDowned
                || hitEnemy.currentHealth <= 0f
                || hitEnemy.Body == MonsterActor.BodyState.Downed
                || hitEnemy.Body == MonsterActor.BodyState.Fading
                || hitEnemy.Body == MonsterActor.BodyState.Despawned;
        }
        else if (hitPlayer != null)
        {
            if (sourceAbility != null)
                sourceAbility.SettleHit(hitPlayer, damage);
            else
                hitPlayer.TakeDamage(damage);
        }
        else
        {
            VfxPool.ReleaseOrDestroy(gameObject);
            return;
        }

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
                Enemy next = sourceAbility.FindNearestLegalTarget(transform.position, hitEnemy);
                if (next != null)
                {
                    _retargetUsed = true;
                    _settled = false;
                    target = next.transform;
                    return;
                }
            }

            if (canSpawnOnKill && sourceAbility != null)
                sourceAbility.SpawnDerivedHandsFromKill(transform.position, this);
        }

        VfxPool.ReleaseOrDestroy(gameObject);
    }

    private bool IsLegalTarget(Transform candidate)
    {
        Enemy enemy = candidate != null ? candidate.GetComponentInParent<Enemy>() : null;
        if (enemy != null)
        {
            return ownerAtFire != null
                && ownerAtFire.CanDamage(enemy)
                && !enemy.isDowned
                && enemy.Body != MonsterActor.BodyState.Fading
                && enemy.Body != MonsterActor.BodyState.Despawned
                && enemy.currentHealth > 0f;
        }

        PlayerHealth player = candidate != null ? candidate.GetComponentInParent<PlayerHealth>() : null;
        return player != null && ownerAtFire != null && ownerAtFire.CanDamageSoul();
    }

}
