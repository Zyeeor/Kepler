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
    [Tooltip("Possessed owner turn speed before launching a dagger toward the mouse aim.")]
    public float aimTurnSpeed = 720f;
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
    private bool aimingForLaunch;
    private Vector3 nextLaunchDirection;

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

            if (owner.isPossessed && !aimingForLaunch)
                StartCoroutine(AimForDaggerLaunch());
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
            float angle = AbilityTime * orbitSpeed + i * (360f / Mathf.Max(1, daggers.Count));
            daggers[i].transform.position = GetOrbitPos(angle);
        }

        // Launch daggers at target
        launchTimer += AbilityDeltaTime;
        if (!aimingForLaunch && launchTimer >= launchInterval && daggers.Count > 0)
        {
            launchTimer -= launchInterval;
            Transform target = GetHomingTarget();
            if (target != null)
            {
                var d = daggers[0];
                daggers.RemoveAt(0);
                if (d != null)
                {
                    Vector3 launchDirection = nextLaunchDirection.sqrMagnitude > 0.0001f
                        ? nextLaunchDirection
                        : target.position - d.transform.position;
                    launchDirection.y = 0f;
                    if (launchDirection.sqrMagnitude > 0.0001f)
                        d.transform.forward = launchDirection.normalized;
                    nextLaunchDirection = Vector3.zero;
                    StartCoroutine(HomingRoutine(d, target));
                }
            }
        }
    }

    IEnumerator AimForDaggerLaunch()
    {
        aimingForLaunch = true;
        if (TryGetPossessedMouseDirection(out Vector3 aimDirection))
        {
            nextLaunchDirection = aimDirection;
            yield return StartCoroutine(RotatePossessedOwnerTowards(aimDirection, aimTurnSpeed));
        }
        else if (owner != null)
        {
            nextLaunchDirection = owner.transform.forward;
        }
        aimingForLaunch = false;
    }

    Transform GetHomingTarget()
    {
        if (owner.isPossessed)
        {
            Enemy best = null;
            float bestDist = float.MaxValue;
            Vector3 ownerPos = owner.transform.position;
            foreach (var e in EnemyRegistry.All)
            {
                if (e == null || !owner.CanDamage(e)) continue;
                float d = Vector3.Distance(ownerPos, e.transform.position);
                float effectiveDetectRange = ScaleAbilityRadius(detectRange);
                if (d <= effectiveDetectRange && d < bestDist) { bestDist = d; best = e; }
            }
            return best != null ? best.transform : null;
        }
        else
        {
            if (owner.targetPlayer != null)
            {
                float d = Vector3.Distance(owner.transform.position, owner.targetPlayer.position);
                if (d <= detectRange) return owner.targetPlayer;
            }
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
            dagger.transform.forward = Vector3.RotateTowards(currentForward, desiredDir, homingTurnRate * Mathf.Deg2Rad * AbilityDeltaTime, 1f);
            dagger.transform.position += dagger.transform.forward * homingSpeed * AbilityDeltaTime;

            if (Vector3.Distance(dagger.transform.position, target.position) < 0.8f)
            {
                CombatHitboxDebug.DrawSphere(drawHitboxes, dagger.transform.position, 0.8f, 0f);
                var enemy = target.GetComponent<Enemy>();
                if (owner.CanDamage(enemy))
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
        float scale = OwnerCombatScaleMultiplier;
        Vector3 offset = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad)) * orbitRadius * scale;
        return owner.transform.position + Vector3.up * heightOffset * scale + offset;
    }

    protected override void OnDisable()
    {
        aimingForLaunch = false;
        nextLaunchDirection = Vector3.zero;
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        aimingForLaunch = false;
        nextLaunchDirection = Vector3.zero;
        base.ResetForOwnerReuse();
    }
}
