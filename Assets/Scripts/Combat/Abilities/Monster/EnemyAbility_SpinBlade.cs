using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Skill: 飞轮环绕 - Spinning Blades.
/// Summons spinning blades that orbit the owner for bladeLifetime seconds.
/// Also grants move speed bonus for speedBoostDuration seconds.
/// Blades don't disappear on hit. Blades face toward circle center.
/// </summary>
public class EnemyAbility_SpinBlade : EnemyAbility
{
    [Header("Blades")]
    public GameObject bladePrefab;
    public int bladeCount = 3;
    public float bladeLifetime = 5f;
    public float orbitRadius = 2f;
    public float spinSpeed = 360f;
    public float heightOffset = 0.5f;

    [Header("Speed Boost")]
    public float speedBoostMult = 1.5f;
    public float speedBoostDuration = 3f;

    [Header("Damage")]
    public float damageMultiplier = 1f;
    public float hitInterval = 0.15f;
    public float hitRadius = 0.6f;
    public LayerMask targetMask = -1;

    [Header("Impact VFX")]
    public GameObject impactVfxPrefab;
    public float impactVfxDuration = 0.5f;

    [Header("Animation")]
    public string animTrigger = "Skill";

    private List<GameObject> blades = new List<GameObject>();
    private float hitTimer;
    private float originalMoveSpeed;
    private Enemy ownerEnemy;
    private HashSet<Transform> recentHits = new HashSet<Transform>(); // prevent double-hit per tick

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "飞轮环绕";
        cooldown = cooldown <= 0f ? 8f : cooldown;
    }

    public override bool CanTrigger()
    {
        if (owner.isPossessed) return base.CanTrigger();
        return base.CanTrigger() && owner != null && owner.targetPlayer != null;
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;
        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger(animTrigger);

        ownerEnemy = owner;

        foreach (var b in blades) if (b != null) Destroy(b);
        blades.Clear();

        for (int i = 0; i < bladeCount; i++)
        {
            if (bladePrefab == null) continue;
            float angle = i * (360f / bladeCount);
            var b = SpawnVfxTracked(bladePrefab, GetOrbitPos(angle), Quaternion.identity);
            blades.Add(b);
        }

        StartCoroutine(SpeedBoostRoutine());
        StartCoroutine(BladeLifetimeRoutine());
        hitTimer = 0f;
    }

    IEnumerator SpeedBoostRoutine()
    {
        if (ownerEnemy == null) yield break;
        originalMoveSpeed = ownerEnemy.moveSpeed;
        ownerEnemy.moveSpeed *= speedBoostMult;
        yield return AbilityWait(speedBoostDuration);
        if (ownerEnemy != null) ownerEnemy.moveSpeed = originalMoveSpeed;
    }

    IEnumerator BladeLifetimeRoutine()
    {
        yield return AbilityWait(bladeLifetime);
        foreach (var b in blades) if (b != null) Destroy(b);
        blades.Clear();
    }

    void Update()
    {
        base.Update();
        if (owner == null || blades.Count == 0) return;

        Vector3 center = owner.transform.position + Vector3.up * heightOffset;

        // Orbit blades + face toward center
        float angleStep = 360f / blades.Count;
        for (int i = 0; i < blades.Count; i++)
        {
            if (blades[i] == null) continue;
            float angle = AbilityTime * spinSpeed + i * angleStep;
            blades[i].transform.position = GetOrbitPos(angle);
            // Face toward circle center (inward)
            Vector3 toCenter = (center - blades[i].transform.position).normalized;
            if (toCenter.sqrMagnitude > 0.001f)
                blades[i].transform.rotation = Quaternion.LookRotation(toCenter, Vector3.up);
        }

        // Hit detection (pass-through, no destroy)
        hitTimer += AbilityDeltaTime;
        if (hitTimer >= hitInterval)
        {
            hitTimer -= hitInterval;
            recentHits.Clear();
            int layerMask = owner.isPossessed ? ~0 : targetMask;
            foreach (var b in blades)
            {
                if (b == null) continue;
                CombatHitboxDebug.DrawSphere(drawHitboxes, b.transform.position, hitRadius);
                Collider[] hits = Physics.OverlapSphere(b.transform.position, hitRadius, layerMask, QueryTriggerInteraction.Collide);
                foreach (var h in hits)
                {
                    if (recentHits.Contains(h.transform.root)) continue;

                    var enemy = h.GetComponentInParent<Enemy>();
                    if (owner.CanDamage(enemy))
                    {
                        DealDamageTo(enemy, damage * damageMultiplier);
                        recentHits.Add(enemy.transform);
                        SpawnImpactVfx(h.ClosestPoint(b.transform.position));
                    }
                    var ph = h.GetComponentInParent<PlayerHealth>();
                    if (ph != null)
                    {
                        DealDamageToPlayer(ph, damage * damageMultiplier);
                        recentHits.Add(ph.transform);
                        SpawnImpactVfx(h.ClosestPoint(b.transform.position));
                    }
                }
            }
        }
    }

    void SpawnImpactVfx(Vector3 pos)
    {
        if (impactVfxPrefab == null) return;
        SpawnVfxTracked(impactVfxPrefab, pos, Quaternion.identity, impactVfxDuration);
    }

    Vector3 GetOrbitPos(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * orbitRadius;
        return owner.transform.position + Vector3.up * heightOffset + offset;
    }

    void OnDestroy()
    {
        foreach (var b in blades) if (b != null) Destroy(b);
        blades.Clear();
        if (ownerEnemy != null) ownerEnemy.moveSpeed = originalMoveSpeed;
    }
}
