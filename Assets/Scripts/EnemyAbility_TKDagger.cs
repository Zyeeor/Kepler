using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Skill: 念力飞刀 - Telekinetic Daggers.
/// Summons homing daggers that orbit behind the owner. Up to maxCount.
/// When possessed, daggers home toward nearest enemy.
/// When not possessed (AI), daggers home toward the player.
/// </summary>
public class EnemyAbility_TKDagger : EnemyAbility
{
    [Header("Dagger")]
    public GameObject daggerPrefab;
    public int maxDaggers = 5;
    public float orbitRadius = 1.5f;
    public float orbitSpeed = 120f;
    public float heightOffset = 1f;

    [Header("Homing")]
    public float detectRange = 8f;
    public float homingSpeed = 20f;
    public float launchInterval = 0.3f;
    [Tooltip("How fast the dagger rotates toward the target (degrees/sec).")]
    public float homingTurnRate = 360f;
    [Tooltip("Curve strength: how aggressively it curves toward target. 0=linear, 1=max curve.")]
    [Range(0f, 1f)] public float homingCurveStrength = 0.3f;

    [Header("Damage")]
    public float damageMultiplier = 1f;

    [Header("Impact VFX")]
    public GameObject impactVfxPrefab;
    public float impactVfxDuration = 1f;

    [Header("Animation")]
    public string animTrigger = "Skill";

    private List<GameObject> daggers = new List<GameObject>();
    private float launchTimer;
    private float orbitAngleOffset;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "念力飞刀";
        cooldown = cooldown <= 0f ? 1f : cooldown;
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

        while (daggers.Count >= maxDaggers)
        {
            var old = daggers[0];
            daggers.RemoveAt(0);
            if (old != null) Destroy(old);
        }

        if (daggerPrefab != null)
        {
            var d = SpawnVfxTracked(daggerPrefab, GetOrbitPos(orbitAngleOffset), Quaternion.identity);
            daggers.Add(d);
            orbitAngleOffset += 360f / maxDaggers;
        }
    }

    void Update()
    {
        base.Update();
        if (owner == null) return;

        // Orbit existing daggers
        for (int i = daggers.Count - 1; i >= 0; i--)
        {
            if (daggers[i] == null) { daggers.RemoveAt(i); continue; }
            float angle = Time.time * orbitSpeed + i * (360f / Mathf.Max(1, daggers.Count));
            daggers[i].transform.position = GetOrbitPos(angle);
        }

        // Launch daggers at target
        launchTimer += Time.deltaTime;
        if (launchTimer >= launchInterval && daggers.Count > 0)
        {
            launchTimer -= launchInterval;
            Transform target = GetHomingTarget();
            if (target != null)
            {
                var d = daggers[0];
                daggers.RemoveAt(0);
                if (d != null) StartCoroutine(HomingRoutine(d, target));
            }
        }
    }

    Transform GetHomingTarget()
    {
        if (owner.isPossessed)
        {
            // Target nearest enemy
            Enemy best = null;
            float bestDist = float.MaxValue;
            foreach (var e in FindObjectsOfType<Enemy>())
            {
                if (e == owner || e.isDowned || e.isPossessed) continue;
                float d = Vector3.Distance(owner.transform.position, e.transform.position);
                if (d <= detectRange && d < bestDist) { bestDist = d; best = e; }
            }
            return best != null ? best.transform : null;
        }
        else
        {
            // Target player
            if (owner.targetPlayer != null)
                return owner.targetPlayer;
            return null;
        }
    }

    IEnumerator HomingRoutine(GameObject dagger, Transform target)
    {
        while (dagger != null && target != null)
        {
            Vector3 toTarget = (target.position - dagger.transform.position).normalized;
            Vector3 currentForward = dagger.transform.forward;

            // Curve: blend current forward with target direction for curved trajectory
            Vector3 desiredDir = Vector3.Slerp(currentForward, toTarget, homingCurveStrength).normalized;

            // Smoothly rotate toward desired direction
            dagger.transform.forward = Vector3.RotateTowards(currentForward, desiredDir, homingTurnRate * Mathf.Deg2Rad * Time.deltaTime, 1f);
            dagger.transform.position += dagger.transform.forward * homingSpeed * Time.deltaTime;

            if (Vector3.Distance(dagger.transform.position, target.position) < 0.8f)
            {
                var enemy = target.GetComponent<Enemy>();
                if (enemy != null && !enemy.isDowned)
                    DealDamageTo(enemy, damage * damageMultiplier);
                var ph = target.GetComponent<PlayerHealth>();
                if (ph != null)
                    DealDamageToPlayer(ph, damage * damageMultiplier);

                SpawnImpactVfx(dagger.transform.position);
                Destroy(dagger);
                yield break;
            }
            yield return new WaitForEndOfFrame();
        }
        if (dagger != null) Destroy(dagger);
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
}
