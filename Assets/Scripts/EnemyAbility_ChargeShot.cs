using UnityEngine;
using System.Collections;

/// <summary>
/// Basic Attack: Charge-shot.
/// Hold left mouse to charge. Every shot fired during charge scales with current charge time.
/// Release fires a final scaled shot.
/// Projectile scale, blast VFX scale, blast radius, and damage all grow with charge time.
/// Uses owner.transform.forward as aim direction.
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
    [Tooltip("How long (seconds) to hold for full charge.")]
    public float maxChargeTime = 2f;
    [Tooltip("Projectile / blast scale at min / max charge.")]
    public float minChargeScale = 1f;
    public float maxChargeScale = 3f;
    [Tooltip("Blast radius at min / max charge.")]
    public float minBlastRadius = 1.5f;
    public float maxBlastRadius = 4f;
    [Tooltip("Damage multiplier at min / max charge.")]
    public float minDamageMultiplier = 1f;
    public float maxDamageMultiplier = 3f;
    [Tooltip("Charge VFX prefab (scales up while charging, parented to owner).")]
    public GameObject chargeVfxPrefab;

    [Header("Damage")]
    public float damageMultiplier = 1f;

    [Header("Targeting")]
    public LayerMask targetMask = -1;

    [Header("Animation")]
    public string animTrigger = "Basic";

    // State
    private bool isCharging;
    private float chargeTimer;
    private GameObject chargeVfxInstance;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "蓄力射击";
        cooldown = cooldown <= 0f ? 0.2f : cooldown;
    }

    public override bool CanTrigger()
    {
        if (owner.isPossessed)
            return owner != null && !owner.isDowned;
        return base.CanTrigger() && owner != null && owner.targetPlayer != null;
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

        if (wantFire && CanTrigger())
        {
            if (!isCharging)
            {
                isCharging = true;
                chargeTimer = 0f;
                currentCooldown = 0f;

                if (chargeVfxPrefab != null)
                {
                    chargeVfxInstance = Instantiate(chargeVfxPrefab, owner.transform.position, Quaternion.identity, owner.transform);
                    PlayVfx(chargeVfxInstance);
                }
            }

            chargeTimer += Time.deltaTime;

            // Scale charge VFX while holding
            if (chargeVfxInstance != null)
            {
                float ct = Mathf.Clamp01(chargeTimer / maxChargeTime);
                chargeVfxInstance.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2f, ct);
            }
        }
        else if (isCharging)
        {
            // Release: fire charged shot
            FireShot();
            StopCharging();
        }
    }

    void FireShot()
    {
        if (projectilePrefab == null || owner == null) return;

        float t = Mathf.Clamp01(chargeTimer / maxChargeTime);
        float scale = Mathf.Lerp(minChargeScale, maxChargeScale, t);
        float radius = Mathf.Lerp(minBlastRadius, maxBlastRadius, t);
        float dmgMult = Mathf.Lerp(minDamageMultiplier, maxDamageMultiplier, t);

        Vector3 forward = owner.transform.forward;
        Vector3 origin = owner.transform.position + forward * 1f + Vector3.up * 1f;

        var go = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(forward, Vector3.up));
        go.transform.localScale = Vector3.one * scale;
        PlayVfx(go);

        StartCoroutine(ProjectileTravel(go, forward, origin, radius, scale, dmgMult));

        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Basic");
    }

    IEnumerator ProjectileTravel(GameObject projectileGo, Vector3 forward, Vector3 origin, float radius, float scale, float dmgMult)
    {
        float traveled = 0f;
        int layerMask = owner.isPossessed ? ~0 : targetMask;

        while (traveled < maxRange && projectileGo != null)
        {
            float step = projectileSpeed * Time.deltaTime;
            traveled += step;
            Vector3 currentPos = origin + forward * Mathf.Min(traveled, maxRange);
            projectileGo.transform.position = currentPos;

            Vector3 halfExtents = new Vector3(projectileWidth * 0.5f * scale, projectileHeight * 0.5f * scale, step * 0.5f);
            Vector3 checkCenter = currentPos - forward * (step * 0.5f);
            Quaternion checkRot = Quaternion.LookRotation(forward, Vector3.up);

            Collider[] hits = Physics.OverlapBox(checkCenter, halfExtents, checkRot, layerMask, QueryTriggerInteraction.Collide);
            bool hitSomething = false;
            Vector3 hitPos = currentPos;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<Enemy>();
                if (enemy != null && enemy != owner && !enemy.isDowned)
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
                DoBlast(hitPos, radius, scale);
                Destroy(projectileGo);
                yield break;
            }

            yield return null;
        }

        if (projectileGo != null)
        {
            DoBlast(projectileGo.transform.position, radius, scale);
            Destroy(projectileGo);
        }
    }

    void DoBlast(Vector3 pos, float radius, float scale)
    {
        if (blastVfxPrefab != null)
        {
            var blast = Instantiate(blastVfxPrefab, pos, Quaternion.identity);
            blast.transform.localScale = Vector3.one * scale;
            Destroy(blast, blastVfxDuration);
        }

        DamageEnemiesInSphere(pos, radius, damage * damageMultiplier, null);
    }

    void StopCharging()
    {
        isCharging = false;
        chargeTimer = 0f;
        currentCooldown = EffectiveCooldown;

        if (chargeVfxInstance != null)
        {
            Destroy(chargeVfxInstance);
            chargeVfxInstance = null;
        }
    }

    protected override void OnTrigger() { }
}
