using UnityEngine;
using System.Collections;

/// <summary>
/// Basic Attack: Charge-shot.
/// Hold left mouse to charge. Release fires a scaled shot.
/// Upgrade Sloth01: if charged > 2s, spawns a land mine on blast.
/// Upgrade Sloth02: spawns random scatter bullets from blast point, count = ceil(chargeTime * multiplier).
/// </summary>
public class EnemyAbility_ChargeShot : EnemyAbility
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileWidth = 1.5f;
    public float projectileHeight = 2f;
    public float projectileSpeed = 30f;
    public float maxRange = 15f;

    [Header("Blast VFX")]
    public GameObject blastVfxPrefab;
    public float blastVfxDuration = 1f;

    [Header("Charge")]
    public float maxChargeTime = 2f;
    public float minChargeScale = 1f;
    public float maxChargeScale = 3f;
    public float minBlastRadius = 1.5f;
    public float maxBlastRadius = 4f;
    public float minDamageMultiplier = 1f;
    public float maxDamageMultiplier = 3f;
    public GameObject chargeVfxPrefab;

    [Header("Damage")]
    public float damageMultiplier = 1f;

    [Header("Targeting")]
    public LayerMask targetMask = -1;
    [Tooltip("Possessed owner turn speed before releasing toward the mouse aim.")]
    public float aimTurnSpeed = 720f;

    [Header("Animation")]
    public string animTrigger = "Basic";

    [Header("Upgrade - Sloth01")]
    [Tooltip("Sloth01: spawn a land mine when a charged-shot kill occurs.")]
    public GameObject sloth01MinePrefab;
    [Tooltip("Sloth01: mine damage multiplier.")]
    public float sloth01MineDamageMult = 1f;
    [Tooltip("Sloth01: mine lifetime.")]
    public float sloth01MineLifetime = 10f;
    [Tooltip("Sloth01: delay before mine spawns after kill.")]
    public float sloth01MineDelay = 0.5f;

    [Header("Upgrade - Sloth02")]
    [Tooltip("Sloth02: scatter bullets from blast. Count = ceil(chargeTime * multiplier).")]
    public float sloth02BulletMult = 2f;
    [Tooltip("Sloth02: scale of scatter bullets.")]
    public float sloth02BulletScale = 0.5f;
    [Tooltip("Sloth02: bullet speed.")]
    public float sloth02BulletSpeed = 15f;
    [Tooltip("Sloth02: bullet max range.")]
    public float sloth02BulletRange = 6f;
    [Tooltip("Sloth02: Y-axis height offset for bullet spawn position.")]
    public float sloth02BulletYOffset = 1f;

    // State
    private bool isCharging;
    private bool isFiringRoutineActive;
    private float chargeTimer;
    private float lastChargeTime; // snapshot used by DoBlast (chargeTimer gets reset before blast)
    private GameObject chargeVfxInstance;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "蓄力射击";
        cooldown = cooldown <= 0f ? 0.2f : cooldown;
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        return owner != null && (owner.isPossessed || owner.targetPlayer != null);
    }

    /// <summary>
    /// 蓄力中或出膛协程未走完时视为释放未结束：
    /// 附身代价致死时先把这一发打出去，再结算死亡。
    /// </summary>
    public override bool IsActivationInProgress => isCharging || isFiringRoutineActive;
    protected override bool UsesDeferredEnemyActivation => true;

    void Update()
    {
        base.Update();
        if (owner == null) return;

        bool wantFire = false;
        if (owner.isPossessed)
            wantFire = Input.GetMouseButton(0);
        else
            wantFire = owner.targetPlayer != null;

        if (wantFire && CanTrigger())
        {
            if (!isCharging)
            {
                if (!TryPrepareDeferredEnemyActivation()) return;
                ConsumeDeferredEnemyActivation();
                if (!TryBeginActivationEffect()) return;
                isCharging = true;
                chargeTimer = 0f;
                currentCooldown = 0f;
                owner.PayAbilityHpCost(this);

                if (chargeVfxPrefab != null)
                {
                    chargeVfxInstance = SpawnVfxTracked(chargeVfxPrefab, owner.transform.position, Quaternion.identity);
                    if (chargeVfxInstance != null) chargeVfxInstance.transform.SetParent(owner.transform, true);
                }
            }

            chargeTimer += AbilityDeltaTime;

            if (chargeVfxInstance != null)
            {
                float ct = Mathf.Clamp01(chargeTimer / maxChargeTime);
                chargeVfxInstance.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2f, ct)
                    * OwnerCombatScaleMultiplier;
            }
        }
        else if (isCharging)
        {
            StartCoroutine(FireShotRoutine(chargeTimer));
            StopCharging();
        }
    }

    IEnumerator FireShotRoutine(float chargeTime)
    {
        isFiringRoutineActive = true;
        if (owner != null && owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aimDirection))
            yield return StartCoroutine(RotatePossessedOwnerTowards(aimDirection, aimTurnSpeed));

        FireShot(chargeTime);
        isFiringRoutineActive = false;
    }

    void FireShot(float chargeTime)
    {
        if (projectilePrefab == null || owner == null) return;

        lastChargeTime = chargeTime;
        float t = Mathf.Clamp01(chargeTime / maxChargeTime);
        float scale = Mathf.Lerp(minChargeScale, maxChargeScale, t);
        float radius = Mathf.Lerp(minBlastRadius, maxBlastRadius, t);
        float dmgMult = Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, t);

        Vector3 forward = owner.transform.forward;
        Vector3 origin = owner.transform.position + forward * 1f + Vector3.up * 1f;

        var go = SpawnVfxTracked(projectilePrefab, origin, Quaternion.LookRotation(forward, Vector3.up));
        go.transform.localScale = Vector3.one * scale * OwnerCombatScaleMultiplier;

        StartCoroutine(ProjectileTravel(go, forward, origin, radius, scale, dmgMult));

        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Basic");
    }

    IEnumerator ProjectileTravel(GameObject projectileGo, Vector3 forward, Vector3 origin, float radius, float scale, float dmgMult)
    {
        float traveled = 0f;
        int layerMask = owner.isPossessed ? ~0 : targetMask;

        float effectiveMaxRange = ScaleAbilityRadius(maxRange);
        while (traveled < effectiveMaxRange && projectileGo != null)
        {
            float step = projectileSpeed * AbilityDeltaTime;
            traveled += step;
            Vector3 currentPos = origin + forward * Mathf.Min(traveled, effectiveMaxRange);
            projectileGo.transform.position = currentPos;

            Vector3 halfExtents = new Vector3(projectileWidth * 0.5f * scale * OwnerCombatScaleMultiplier,
                projectileHeight * 0.5f * scale * OwnerCombatScaleMultiplier, step * 0.5f);
            Vector3 checkCenter = currentPos - forward * (step * 0.5f);
            Quaternion checkRot = Quaternion.LookRotation(forward, Vector3.up);
            CombatHitboxDebug.DrawBox(drawHitboxes, checkCenter, halfExtents, checkRot, 0f);

            Collider[] hits = Physics.OverlapBox(checkCenter, halfExtents, checkRot, layerMask, QueryTriggerInteraction.Collide);
            bool hitSomething = false;
            Vector3 hitPos = currentPos;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<Enemy>();
                if (owner.CanDamage(enemy))
                {
                    DealDamageTo(enemy, damage * damageMultiplier * dmgMult);
                    hitSomething = true;
                    hitPos = enemy.transform.position;
                    break;
                }
                var ph = hit.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    DealDamageToPlayer(ph, damage * damageMultiplier * dmgMult);
                    hitSomething = true;
                    hitPos = ph.transform.position;
                    break;
                }
            }

            if (hitSomething)
            {
                DoBlast(hitPos, radius, scale, dmgMult);
                ReleaseVfx(projectileGo);
                yield break;
            }

            yield return null;
        }

        if (projectileGo != null)
        {
            DoBlast(projectileGo.transform.position, radius, scale, dmgMult);
            ReleaseVfx(projectileGo);
        }
    }

    void DoBlast(Vector3 pos, float radius, float scale, float dmgMult)
    {
        if (blastVfxPrefab != null)
        {
            var blast = VfxPool.Instance.Spawn(blastVfxPrefab, pos, Quaternion.identity);
            BulletTimeController.MarkVfxOrigin(blast, IsOwnedByPlayer);
            blast.transform.localScale = Vector3.one * scale * OwnerCombatScaleMultiplier;
            PlayVfx(blast);
            ReleaseVfx(blast, blastVfxDuration);
        }

        var hitEnemies = DamageEnemiesInSphere(pos, radius, damage * damageMultiplier * dmgMult, null);

        // --- Sloth01: spawn mine when a charged-shot enemy is killed ---
        if (IsUpgradeUnlocked("Sloth01") && sloth01MinePrefab != null)
        {
            foreach (var e in hitEnemies)
            {
                if (e != null && e.isDowned)
                    StartCoroutine(SpawnMineDelayed(e.transform.position));
            }
        }

        // --- Sloth02: scatter bullets (skip enemies hit by blast) ---
        if (IsUpgradeUnlocked("Sloth02"))
        {
            int bulletCount = Mathf.CeilToInt(lastChargeTime * sloth02BulletMult);
            Debug.Log($"[Sloth02] lastChargeTime={lastChargeTime}, bulletMult={sloth02BulletMult}, bulletCount={bulletCount}, isPossessed={owner.isPossessed}");
            Vector3 bulletSpawnPos = pos + Vector3.up * sloth02BulletYOffset;
            for (int i = 0; i < bulletCount; i++)
            {
                Vector3 randomDir = owner != null ? owner.AiRandomUnitSphere() : Random.onUnitSphere;
                randomDir.y = Mathf.Abs(randomDir.y);
                randomDir.Normalize();
                var bullet = SpawnVfxTracked(projectilePrefab, bulletSpawnPos, Quaternion.LookRotation(randomDir, Vector3.up));
                bullet.transform.localScale = Vector3.one * sloth02BulletScale * OwnerCombatScaleMultiplier;
                StartCoroutine(ScatterBulletTravel(bullet, bulletSpawnPos, randomDir, sloth02BulletRange, sloth02BulletScale, dmgMult, hitEnemies));
            }
        }
    }

    IEnumerator SpawnMineDelayed(Vector3 pos)
    {
        yield return AbilityWait(sloth01MineDelay);
        var mineGo = Instantiate(sloth01MinePrefab, pos, Quaternion.identity);
        mineGo.transform.localScale *= OwnerCombatScaleMultiplier;
        var mine = mineGo.GetComponent<MineBehaviour>();
        if (mine == null) mine = mineGo.AddComponent<MineBehaviour>();
        mine.lifetime = sloth01MineLifetime;
        mine.triggerRadius = 1.5f * OwnerCombatScaleMultiplier;
        mine.blastRadius = 3f * OwnerCombatScaleMultiplier;
        mine.damage = damage * sloth01MineDamageMult;
        mine.placer = owner;
        mine.blastVfxPrefab = blastVfxPrefab;
        mine.blastVfxDuration = blastVfxDuration;
    }

    IEnumerator ScatterBulletTravel(GameObject bullet, Vector3 origin, Vector3 dir, float range, float scale, float dmgMult, System.Collections.Generic.HashSet<Enemy> excludeEnemies = null)
    {
        float traveled = 0f;
        int layerMask = owner.isPossessed ? ~0 : targetMask;

        while (traveled < range && bullet != null)
        {
            float step = sloth02BulletSpeed * AbilityDeltaTime;
            traveled += step;
            Vector3 currentPos = origin + dir * Mathf.Min(traveled, range);
            bullet.transform.position = currentPos;

            CombatHitboxDebug.DrawSphere(drawHitboxes, currentPos, 0.5f * scale, 0f);
            Collider[] hits = Physics.OverlapSphere(currentPos, 0.5f * scale * OwnerCombatScaleMultiplier, layerMask, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<Enemy>();
                if (owner.CanDamage(enemy) && (excludeEnemies == null || !excludeEnemies.Contains(enemy)))
                {
                    DealDamageTo(enemy, damage * damageMultiplier * dmgMult * sloth02BulletScale);
                    ReleaseVfx(bullet);
                    yield break;
                }
                var ph = hit.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    DealDamageToPlayer(ph, damage * damageMultiplier * dmgMult * sloth02BulletScale);
                    ReleaseVfx(bullet);
                    yield break;
                }
            }
            yield return null;
        }
        if (bullet != null) ReleaseVfx(bullet);
    }

    void StopCharging()
    {
        isCharging = false;
        EndActivationEffect();
        chargeTimer = 0f;
        currentCooldown = EffectiveCooldown;

        if (chargeVfxInstance != null)
        {
            ReleaseVfx(chargeVfxInstance);
            chargeVfxInstance = null;
        }
    }

    protected override void OnTrigger() { }

    protected override void OnDisable()
    {
        if (isCharging) StopCharging();
        isFiringRoutineActive = false;
        base.OnDisable();
    }
}
