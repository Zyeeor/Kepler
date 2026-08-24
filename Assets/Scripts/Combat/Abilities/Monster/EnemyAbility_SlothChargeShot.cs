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
    [Tooltip("Optional projectile spawn Transform. Falls back to the owner's forward/up offset when unassigned.")]
    public Transform projectileSpawnPoint;
    public float projectileWidth = 1.5f;
    public float projectileHeight = 2f;
    public float projectileSpeed = 30f;
    public float maxRange = 15f;

    [Header("Impact VFX")]
    [Tooltip("Optional VFX spawned on every Enemy damaged by the charged-shot blast.")]
    public GameObject impactVfxPrefab;
    public float impactVfxDuration = 1f;

    [Header("Shoot Feedback (Combat Effect Manager)")]
    [Tooltip("Post-process / shake / hit-stop played when the charged shot is released. Fires for possessed Sloth only.")]
    public HitFeedbackParams shootFeedback = new HitFeedbackParams
    {
        shakeOnHit = false,
        hitStopOnHit = false,
        postProcessOnHit = false
    };

    [Header("Shoot Recoil")]
    [Tooltip("Optional Transform that receives local-position recoil when the charged shot is released.")]
    public Transform recoilTarget;
    [Tooltip("Local displacement at full charge. Use negative Z for backward recoil.")]
    public Vector3 maxRecoilOffset = new Vector3(0f, 0f, -0.2f);
    public float recoilKickDuration = 0.05f;
    public float recoilReturnDuration = 0.15f;

    [Header("Charge")]
    public float maxChargeTime = 2f;
    public float minChargeScale = 1f;
    public float maxChargeScale = 3f;
    public float minBlastRadius = 1.5f;
    public float maxBlastRadius = 4f;
    public float minDamage = 2f;
    public float maxDamage = 100f;
    public GameObject chargeVfxPrefab;
    [Tooltip("Optional Transform the charge VFX follows. Falls back to the Sloth owner when unassigned.")]
    public Transform chargeVfxSpawnPoint;
    [Tooltip("Local position offset from the Charge VFX Spawn Point.")]
    public Vector3 chargeVfxPositionOffset;

    [Header("Targeting")]
    public LayerMask targetMask = -1;
    public float aimTurnSpeed = 720f;

    [Header("Upgrade - Sloth.Scatter")]
    public float scatterBulletMult = 2f;
    public float scatterBulletScale = 0.5f;
    public float scatterBulletSpeed = 15f;
    public float scatterBulletRange = 6f;
    public float scatterBulletYOffset = 1f;

    [Header("Canonical Sloth Cards")]
    public int fanProjectileCount = 3;
    public float fanSpreadAngle = 24f;
    public float crushScaleThreshold = 2f;

    private bool isCharging;

    private float chargeTimer;
    private float lastChargeTime;
    private GameObject chargeVfxInstance;
    private Coroutine recoilRoutine;
    private Vector3 recoilBasePosition;
    private bool hasRecoilBasePosition;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "地爆天星";
        cooldown = 0f;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Sloth.ChargeShot", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Sloth.ChargeShot");
        EnsureUpgrade("SL-A03");
        EnsureUpgrade("SL-A04");
        EnsureUpgrade("SL-A05");

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
                    Transform anchor = chargeVfxSpawnPoint != null ? chargeVfxSpawnPoint : owner.transform;
                    chargeVfxInstance = Instantiate(chargeVfxPrefab, anchor);
                    if (chargeVfxInstance != null)
                    {
                        chargeVfxInstance.transform.localPosition = chargeVfxPositionOffset;
                        PlayVfx(chargeVfxInstance);
                    }
                }
            }

            chargeTimer += AbilityDeltaTime;
            if (chargeVfxInstance != null)
            {
                float ct = Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, maxChargeTime));
                        chargeVfxInstance.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2f, ct) * OwnerCombatScaleMultiplier;
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
        Vector3 origin = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : owner.transform.position + forward * 1f + Vector3.up * 1f;

        var go = SpawnVfxTracked(projectilePrefab, origin, Quaternion.LookRotation(forward, Vector3.up));
        go.transform.localScale *= scale;
        foreach (ParticleSystem particleSystem in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        if (IsUpgradeUnlocked("SL-A04"))
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(GetCardParameter("FanProjectileCount", fanProjectileCount)));
            float totalSpread = GetCardParameter("FanSpreadAngle", fanSpreadAngle);
            float perProjectileDamage = Mathf.Max(3f, shotDamage / count);
            ReleaseVfx(go);
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-totalSpread * 0.5f, totalSpread * 0.5f, i / (float)(count - 1));
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * forward;
                GameObject fanProjectile = SpawnVfxTracked(projectilePrefab, origin, Quaternion.LookRotation(direction, Vector3.up));
                fanProjectile.transform.localScale *= scale * scatterBulletScale;
                StartCoroutine(ProjectileTravel(fanProjectile, direction, origin, radius * scatterBulletScale, scale * scatterBulletScale, perProjectileDamage));
            }
        }
        else
        {
            StartCoroutine(ProjectileTravel(go, forward, origin, radius, scale, shotDamage));
        }


        if (owner.isPossessed && shootFeedback != null && shootFeedback.HasAnyEnabled)
            CombatEffectManager.PlayHitFeedback(shootFeedback, owner.transform);

        if (recoilTarget != null)
        {
            if (recoilRoutine != null) StopCoroutine(recoilRoutine);
            if (hasRecoilBasePosition) recoilTarget.transform.localPosition = recoilBasePosition;
            recoilRoutine = StartCoroutine(PlayRecoil(t));
        }

        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Basic");
    }

    private IEnumerator PlayRecoil(float chargeFraction)
    {
        if (recoilTarget == null) yield break;

        recoilBasePosition = recoilTarget.transform.localPosition;
        hasRecoilBasePosition = true;
        Vector3 basePosition = recoilBasePosition;
        Vector3 recoilPosition = basePosition + maxRecoilOffset * chargeFraction;
        float kickDuration = Mathf.Max(0.01f, recoilKickDuration);
        float returnDuration = Mathf.Max(0.01f, recoilReturnDuration);

        float elapsed = 0f;
        while (elapsed < kickDuration && recoilTarget != null)
        {
            elapsed += AbilityDeltaTime;
            recoilTarget.transform.localPosition = Vector3.Lerp(basePosition, recoilPosition, Mathf.Clamp01(elapsed / kickDuration));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < returnDuration && recoilTarget != null)
        {
            elapsed += AbilityDeltaTime;
            recoilTarget.transform.localPosition = Vector3.Lerp(recoilPosition, basePosition, Mathf.Clamp01(elapsed / returnDuration));
            yield return null;
        }

        if (recoilTarget != null) recoilTarget.transform.localPosition = basePosition;
        hasRecoilBasePosition = false;
        recoilRoutine = null;
    }

    IEnumerator ProjectileTravel(GameObject projectileGo, Vector3 forward, Vector3 origin, float radius, float scale, float shotDamage)
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

            if (IsUpgradeUnlocked("SL-A05") && scale >= crushScaleThreshold)
                TryCrushIncomingProjectile(checkCenter, forward, scale);

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
                ReleaseVfx(projectileGo);
                yield break;
            }

            yield return null;
        }

        if (projectileGo != null)
        {
            DoBlast(projectileGo.transform.position, radius, scale, shotDamage, null);
            ReleaseVfx(projectileGo);
        }
    }

    void DoBlast(Vector3 pos, float radius, float scale, float shotDamage, Enemy scatterIgnore)
    {
        HashSet<Enemy> hitEnemies = DamageEnemiesInSphere(pos, radius, shotDamage, null);
        foreach (Enemy hitEnemy in hitEnemies)
        {
            if (impactVfxPrefab == null) continue;

            GameObject impact = SpawnVfxTracked(impactVfxPrefab, hitEnemy.transform.position, Quaternion.identity, impactVfxDuration);
            if (impact == null) continue;

            impact.transform.localScale *= scale;
            foreach (ParticleSystem particleSystem in impact.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
        TryDamagePlayerInRadius(pos, radius, shotDamage);

        if (!IsUpgradeUnlocked("SL-A03") || projectilePrefab == null)
            return;

        var exclude = scatterIgnore != null ? new HashSet<Enemy> { scatterIgnore } : null;

        int bulletCount = Mathf.CeilToInt(lastChargeTime * scatterBulletMult);
        Vector3 bulletSpawnPos = pos + Vector3.up * scatterBulletYOffset;
        for (int i = 0; i < bulletCount; i++)
        {
            Vector3 randomDir = owner != null ? owner.AiRandomUnitSphere() : Random.onUnitSphere;
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

            CombatHitboxDebug.DrawSphere(drawHitboxes, currentPos, 0.5f * scale, 0f);
            Collider[] hits = Physics.OverlapSphere(currentPos, 0.5f * scale * OwnerCombatScaleMultiplier, layerMask, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<Enemy>();
                if (owner.CanDamage(enemy) && (excludeEnemies == null || !excludeEnemies.Contains(enemy)))
                {
                    SettleHit(enemy, fragmentDamage);
                    ReleaseVfx(bullet);
                    yield break;
                }
                var ph = hit.GetComponentInParent<PlayerHealth>();
                if (ph != null && owner.CanDamageSoul())
                {
                    SettleHit(ph, fragmentDamage);
                    ReleaseVfx(bullet);
                    yield break;
                }
            }
            yield return null;
        }
        if (bullet != null) ReleaseVfx(bullet);
    }

    private void TryCrushIncomingProjectile(Vector3 center, Vector3 forward, float ownScale)
    {
        Collider[] candidates = Physics.OverlapSphere(center, projectileWidth * ownScale * 0.5f * OwnerCombatScaleMultiplier, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < candidates.Length; i++)
        {
            Projectile incoming = candidates[i].GetComponentInParent<Projectile>();
            if (incoming == null || incoming.ownerEnemy == owner) continue;
            Vector3 direction = incoming.transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f && Vector3.Dot(direction.normalized, forward) >= 0f) continue;
            if (incoming.transform.lossyScale.magnitude >= transform.lossyScale.magnitude * ownScale) continue;
            VfxPool.ReleaseOrDestroy(incoming.gameObject);
            return;
        }
    }

    private void EnsureUpgrade(string effectId)
    {
        if (upgrades == null) upgrades = new List<UpgradeSlot>();
        if (upgrades.Exists(slot => slot != null && string.Equals(slot.effectId, effectId, System.StringComparison.OrdinalIgnoreCase))) return;
        upgrades.Add(new UpgradeSlot { effectId = effectId, unlocked = false });
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
        if (recoilRoutine != null) StopCoroutine(recoilRoutine);
        if (recoilTarget != null && hasRecoilBasePosition) recoilTarget.transform.localPosition = recoilBasePosition;
        recoilRoutine = null;
        hasRecoilBasePosition = false;
        base.OnDisable();
    }
}
