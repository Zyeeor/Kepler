using UnityEngine;

/// <summary>
/// Basic Attack: Laser Beam. Auto-aims at nearest enemy (possessed) or player (AI).
/// Spawns a VFX prefab each tick (0.25s), oriented from owner to target.
/// </summary>
public class EnemyAbility_Laser : EnemyAbility
{
    [Header("Laser")]
    public float maxRange = 15f;
    public float damagePerTick = 10f;
    public float tickInterval = 0.25f;

    [Header("Upgrade - Envy01")]
    [Tooltip("Envy01: extra damage added per tick per second of continuous fire.")]
    public float envy01DamageRampPerSec = 5f;
    [Tooltip("Envy01: max extra damage from ramp.")]
    public float envy01DamageRampMax = 50f;
    [Tooltip("Envy01: time (seconds) to reach max ramp damage.")]
    public float envy01MaxChargeTime = 10f;

    [Header("Upgrade - Envy02")]
    [Tooltip("Envy02: fire a second laser at the second-nearest enemy.")]
    public float envy02SecondBeamDamageMult = 0.6f;

    [Header("Beam VFX")]
    [Tooltip("VFX prefab spawned each tick, oriented from owner to target.")]
    public GameObject beamPrefab;
    public Material beamMaterial;
    public Vector3 beamPositionOffset = Vector3.zero;
    public Vector3 beamRotationOffset = Vector3.zero;

    [Header("Hit VFX")]
    public GameObject hitImpactPrefab;
    public float hitImpactDuration = 0.3f;

    private bool isFiring;
    private Enemy currentTarget;
    private float damageTimer;
    private float fireDuration; // how long laser has been continuously firing
    private GameObject hitVfx;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "Laser Beam";
    }

    protected override void Update()
    {
        base.Update();
        if (owner == null) return;

        bool wantFire;
        if (owner.isPossessed)
            wantFire = Input.GetMouseButton(0);
        else
            wantFire = owner.targetPlayer != null && Vector3.Distance(owner.transform.position, owner.targetPlayer.position) <= maxRange;

        if (wantFire && CanTrigger())
        {
            if (!isFiring)
            {
                if (!TryBeginActivationEffect()) return;
                isFiring = true;
                damageTimer = 0f;
                fireDuration = 0f;
                currentCooldown = 0f;
                owner.PayAbilityHpCost(this);
            }
            UpdateLaser();
        }
        else if (isFiring)
            StopLaser();

        // Animator（参数存在性缓存：避免每帧遍历 anim.parameters 分配新数组）
        SetAnimBoolCached(owner.GetComponent<Animator>(), "IsFiring", isFiring);
    }

    void UpdateLaser()
    {
        Vector3 origin = owner.transform.position + Vector3.up * 1f;

        if (owner.isPossessed)
        {
            // Primary target = nearest enemy
            var primary = FindNearestEnemy(origin);
            if (primary == null || primary.isDowned) { StopLaser(); return; }

            damageTimer += AbilityDeltaTime;
            fireDuration += AbilityDeltaTime;
            if (damageTimer >= tickInterval)
            {
                float tickDamage = GetTickDamage();
                FireBeamAt(origin, primary.transform.position + Vector3.up * 1f, tickDamage, primary);

                // Envy02: second beam at second-nearest enemy
                if (IsUpgradeUnlocked("Envy02"))
                {
                    var second = FindSecondNearestEnemy(origin, primary);
                    if (second != null && !second.isDowned)
                        FireBeamAt(origin, second.transform.position + Vector3.up * 1f, tickDamage * envy02SecondBeamDamageMult, second);
                }

                damageTimer -= tickInterval;
            }
        }
        else
        {
            if (owner.targetPlayer == null || Vector3.Distance(origin, owner.targetPlayer.position) > maxRange)
            { StopLaser(); return; }

            Vector3 targetPos = owner.targetPlayer.position + Vector3.up * 1f;
            damageTimer += AbilityDeltaTime;
            fireDuration += AbilityDeltaTime;
            if (damageTimer >= tickInterval)
            {
                float tickDamage = GetTickDamage();
                FireBeamAt(origin, targetPos, tickDamage, null);
                damageTimer -= tickInterval;
            }
        }

        // Hit VFX follows primary target
        if (hitImpactPrefab != null)
        {
            Vector3 hitPos = currentTarget != null ? currentTarget.transform.position + Vector3.up * 1f : (owner.targetPlayer != null ? owner.targetPlayer.position + Vector3.up * 1f : origin);
            if (hitVfx == null) { hitVfx = SpawnVfxTracked(hitImpactPrefab, hitPos, Quaternion.identity); }
            else hitVfx.transform.position = hitPos;
        }
    }

    float GetTickDamage()
    {
        float rampBonus = 0f;
        if (IsUpgradeUnlocked("Envy01"))
        {
            float t = Mathf.Clamp01(fireDuration / envy01MaxChargeTime);
            rampBonus = Mathf.Lerp(0f, envy01DamageRampMax, t);
        }
        return damagePerTick + rampBonus;
    }

    void FireBeamAt(Vector3 origin, Vector3 targetPos, float damage, Enemy target)
    {
        Vector3 dir = targetPos - origin;
        Vector3 pos = origin + beamPositionOffset;
        Quaternion rot = (dir.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(dir.normalized, Vector3.up)
            : Quaternion.identity) * Quaternion.Euler(beamRotationOffset);

        float distance = dir.magnitude;
        CombatHitboxDebug.DrawRay(drawHitboxes, origin, dir.sqrMagnitude > 0.01f ? dir.normalized : owner.transform.forward, Mathf.Max(0.1f, distance), tickInterval);
        GameObject vfx = SpawnVfxTracked(beamPrefab, pos, rot, tickInterval);
        Vector3 scale = vfx.transform.localScale;
        scale.z *= distance;
        vfx.transform.localScale = scale;

        if (beamMaterial != null)
        {
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
            {
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.material = beamMaterial;
            }
        }

        if (owner.isPossessed && target != null)
            DealDamageTo(target, damage);
        else if (!owner.isPossessed)
        {
            var ph = owner.targetPlayer.GetComponent<PlayerHealth>();
            if (ph != null) DealDamageToPlayer(ph, damage);
        }
    }

    Enemy FindSecondNearestEnemy(Vector3 origin, Enemy exclude)
    {
        Enemy best = null;
        float bestDist = float.MaxValue;
        foreach (var e in EnemyRegistry.All)   // 注册表（O(n) 内存遍历，替代 FindObjectsOfType 全场景扫描）
        {
            if (e == exclude || e == owner || e.isDowned || e.isPossessed) continue;
            float d = Vector3.Distance(origin, e.transform.position);
            if (d <= maxRange && d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    Enemy FindNearestEnemy(Vector3 origin)
    {
        Enemy nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var e in EnemyRegistry.All)
        {
            if (owner.CanDamage(e))
            {
                float d = Vector3.Distance(origin, e.transform.position);
                if (d <= maxRange && d < nearestDist) { nearestDist = d; nearest = e; }
            }
        }
        return nearest;
    }

    void StopLaser()
    {
        isFiring = false;
        EndActivationEffect();
        currentTarget = null;
        if (hitVfx != null) { ReleaseVfx(hitVfx, hitImpactDuration); hitVfx = null; }
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        return owner != null && (owner.isPossessed || owner.targetPlayer != null);
    }

    protected override void OnTrigger() { }

    protected override void OnDisable()
    {
        if (isFiring) StopLaser();
        base.OnDisable();
    }
}
