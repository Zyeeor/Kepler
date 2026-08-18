using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Sloth basic: hold to charge, release a green blast that scales radius and damage.
/// Card Sloth.Scatter: on enemy hit, scatter fragments that ignore the first target.
/// </summary>
public class EnemyAbility_SlothChargeShot : EnemyAbility
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileWidth = 1.5f;
    public float projectileHeight = 2f;
    public float projectileSpeed = 30f;
    public float maxRange = 15f;

    [Header("Charge")]
    public float maxChargeTime = 2f;
    public float minChargeScale = 1f;
    public float maxChargeScale = 3f;
    public float minBlastRadius = 1.5f;
    public float maxBlastRadius = 4f;
    public float minDamage = 2f;
    public float maxDamage = 100f;
    public GameObject chargeVfxPrefab;

    [Header("Targeting")]
    public LayerMask targetMask = -1;
    public float aimTurnSpeed = 720f;

    [Header("Upgrade - Sloth.Scatter")]
    public float scatterBulletMult = 2f;
    public float scatterBulletScale = 0.5f;
    public float scatterBulletSpeed = 15f;
    public float scatterBulletRange = 6f;
    public float scatterBulletYOffset = 1f;

    private bool isCharging;
    private float chargeTimer;
    private float lastChargeTime;
    private GameObject chargeVfxInstance;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "地爆天星";
        cooldown = 0f;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Sloth.ChargeShot", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Sloth.ChargeShot");
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        if (owner != null && owner.isPossessed) return false;
        return owner != null && owner.targetPlayer != null;
    }

    void Update()
    {
        base.Update();
        if (owner == null) return;

        bool wantFire = false;
        if (owner.isPossessed)
            wantFire = Input.GetMouseButton(0);
        else
            wantFire = owner.targetPlayer != null;

        bool canStart = currentCooldown <= 0f && !owner.isDowned;
        string reason;
        if (owner.Combat != null && !owner.Combat.CanActivate(this, requiredTags, out reason))
            canStart = false;

        if (wantFire && (isCharging || canStart))
        {
            if (!isCharging)
            {
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
                float ct = Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, maxChargeTime));
                chargeVfxInstance.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2f, ct);
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
        if (owner != null && owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aimDirection))
            yield return StartCoroutine(RotatePossessedOwnerTowards(aimDirection, aimTurnSpeed));

        FireShot(chargeTime);
    }

    void FireShot(float chargeTime)
    {
        if (projectilePrefab == null || owner == null) return;

        lastChargeTime = chargeTime;
        float t = Mathf.Clamp01(chargeTime / Mathf.Max(0.01f, maxChargeTime));
        float scale = Mathf.Lerp(minChargeScale, maxChargeScale, t);
        float radius = Mathf.Lerp(minBlastRadius, maxBlastRadius, t);
        float shotDamage = Mathf.Lerp(minDamage, maxDamage, t);
        if (damage > 0f) shotDamage = Mathf.Max(shotDamage, damage * Mathf.Lerp(1f, maxDamage / Mathf.Max(1f, minDamage), t));

        Vector3 forward = owner.transform.forward;
        Vector3 origin = owner.transform.position + forward * 1f + Vector3.up * 1f;

        var go = SpawnVfxTracked(projectilePrefab, origin, Quaternion.LookRotation(forward, Vector3.up));
        go.transform.localScale = Vector3.one * scale;
        StartCoroutine(ProjectileTravel(go, forward, origin, radius, scale, shotDamage));

        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Basic");
    }

    IEnumerator ProjectileTravel(GameObject projectileGo, Vector3 forward, Vector3 origin, float radius, float scale, float shotDamage)
    {
        float traveled = 0f;
        int layerMask = owner.isPossessed ? ~0 : targetMask;

        while (traveled < maxRange && projectileGo != null)
        {
            float step = projectileSpeed * AbilityDeltaTime;
            traveled += step;
            Vector3 currentPos = origin + forward * Mathf.Min(traveled, maxRange);
            projectileGo.transform.position = currentPos;

            Vector3 halfExtents = new Vector3(projectileWidth * 0.5f * scale, projectileHeight * 0.5f * scale, step * 0.5f);
            Vector3 checkCenter = currentPos - forward * (step * 0.5f);
            Quaternion checkRot = Quaternion.LookRotation(forward, Vector3.up);
            CombatHitboxDebug.DrawBox(drawHitboxes, checkCenter, halfExtents, checkRot);

            Collider[] hits = Physics.OverlapBox(checkCenter, halfExtents, checkRot, layerMask, QueryTriggerInteraction.Collide);
            bool hitSomething = false;
            Enemy primaryHit = null;
            Vector3 hitPos = currentPos;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<Enemy>();
                if (owner.CanDamage(enemy))
                {
                    hitSomething = true;
                    primaryHit = enemy;
                    hitPos = enemy.transform.position;
                    break;
                }
                var ph = hit.GetComponentInParent<PlayerHealth>();
                if (ph != null && owner.CanDamageSoul())
                {
                    hitSomething = true;
                    hitPos = ph.transform.position;
                    break;
                }
            }

            if (hitSomething)
            {
                DoBlast(hitPos, radius, scale, shotDamage, primaryHit);
                Destroy(projectileGo);
                yield break;
            }

            yield return null;
        }

        if (projectileGo != null)
        {
            DoBlast(projectileGo.transform.position, radius, scale, shotDamage, null);
            Destroy(projectileGo);
        }
    }

    void DoBlast(Vector3 pos, float radius, float scale, float shotDamage, Enemy scatterIgnore)
    {
        DamageEnemiesInSphere(pos, radius, shotDamage, null);
        TryDamagePlayerInRadius(pos, radius, shotDamage);

        if (scatterIgnore == null || !IsUpgradeUnlocked("Sloth.Scatter") || projectilePrefab == null)
            return;

        var exclude = new HashSet<Enemy> { scatterIgnore };
        int bulletCount = Mathf.CeilToInt(lastChargeTime * scatterBulletMult);
        Vector3 bulletSpawnPos = pos + Vector3.up * scatterBulletYOffset;
        for (int i = 0; i < bulletCount; i++)
        {
            Vector3 randomDir = Random.onUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y);
            randomDir.Normalize();
            var bullet = SpawnVfxTracked(projectilePrefab, bulletSpawnPos, Quaternion.LookRotation(randomDir, Vector3.up));
            bullet.transform.localScale = Vector3.one * scatterBulletScale;
            StartCoroutine(ScatterBulletTravel(bullet, bulletSpawnPos, randomDir, scatterBulletRange, scatterBulletScale, shotDamage, exclude));
        }
    }

    IEnumerator ScatterBulletTravel(GameObject bullet, Vector3 origin, Vector3 dir, float range, float scale, float shotDamage, HashSet<Enemy> excludeEnemies)
    {
        float traveled = 0f;
        int layerMask = owner.isPossessed ? ~0 : targetMask;
        float fragmentDamage = shotDamage * scatterBulletScale;

        while (traveled < range && bullet != null)
        {
            float step = scatterBulletSpeed * AbilityDeltaTime;
            traveled += step;
            Vector3 currentPos = origin + dir * Mathf.Min(traveled, range);
            bullet.transform.position = currentPos;

            Collider[] hits = Physics.OverlapSphere(currentPos, 0.5f * scale, layerMask, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<Enemy>();
                if (owner.CanDamage(enemy) && (excludeEnemies == null || !excludeEnemies.Contains(enemy)))
                {
                    SettleHit(enemy, fragmentDamage);
                    Destroy(bullet);
                    yield break;
                }
                var ph = hit.GetComponentInParent<PlayerHealth>();
                if (ph != null && owner.CanDamageSoul())
                {
                    SettleHit(ph, fragmentDamage);
                    Destroy(bullet);
                    yield break;
                }
            }
            yield return null;
        }
        if (bullet != null) Destroy(bullet);
    }

    void StopCharging()
    {
        isCharging = false;
        EndActivationEffect();
        chargeTimer = 0f;
        currentCooldown = EffectiveCooldown;

        if (chargeVfxInstance != null)
        {
            Destroy(chargeVfxInstance);
            chargeVfxInstance = null;
        }
    }

    protected override void OnTrigger() { }

    protected override void OnDisable()
    {
        if (isCharging) StopCharging();
        base.OnDisable();
    }
}
