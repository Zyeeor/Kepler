using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Skill: 横扫链勾 - Sweep Pull. Fires a hook projectile forward.
/// Upgrade Wrath01: dash toward hook direction on release.
/// Upgrade Wrath02: hook hits all enemies in range, VFX scales up.
/// </summary>
public class EnemyAbility_SweepPull : EnemyAbility
{
    [Header("Hook Projectile")]
    public GameObject hookPrefab;
    public float hookSpeed = 25f;
    public float hookMaxRange = 8f;

    [Header("Hit VFX")]
    public GameObject hitVfxPrefab;
    public float hitVfxDuration = 0.5f;

    [Header("Return VFX")]
    public GameObject returnVfxPrefab;
    public float returnVfxDuration = 2f;

    [Header("Pull")]
    public float pullSpeed = 15f;
    public float pullStopDistance = 1.5f;

    [Header("Damage")]
    public float damageMultiplier = 1.5f;

    [Header("Targeting")]
    public LayerMask targetMask = -1;
    [Tooltip("Possessed owner turn speed before firing toward the mouse aim. 720 = one full turn per 0.5 seconds.")]
    public float aimTurnSpeed = 720f;
    public bool debugLogging = true;

    [Header("Animation")]
    public string animTrigger = "SweepPull";

    [Header("Upgrade - Wrath01")]
    [Tooltip("Wrath01: dash toward hook direction after firing.")]
    public float wrath01DashSpeed = 30f;
    [Tooltip("Wrath01: max dash distance.")]
    public float wrath01DashMaxDist = 8f;

    [Header("Upgrade - Wrath02")]
    [Tooltip("Wrath02: hook hits all enemies in range instead of just one.")]
    public float wrath02HookScale = 2f;

    // State
    private Transform pullTarget;
    private bool isPullingPlayer;
    private bool hookHit;
    private List<Transform> multiTargets = new List<Transform>();

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "横扫链勾";
        cooldown = cooldown <= 0f ? 8f : cooldown;
    }

    protected override void OnDisable()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        base.ResetForOwnerReuse();
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
        if (anim != null) anim.SetTrigger("Skill");

        hookHit = false;
        pullTarget = null;
        isPullingPlayer = false;
        multiTargets.Clear();
        StartCoroutine(SweepPullRoutine());
    }

    IEnumerator SweepPullRoutine()
    {
        Vector3 forward = owner.transform.forward;
        if (owner.isPossessed && PlayerController.Instance != null && PlayerController.Instance.TryGetAimPoint(out Vector3 aimPoint))
        {
            Vector3 aimDirection = aimPoint - owner.transform.position;
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
                owner.IsAbilityFacingLocked = true;
                while (owner != null && Quaternion.Angle(owner.transform.rotation, targetRotation) > 0.1f)
                {
                    owner.transform.rotation = Quaternion.RotateTowards(
                        owner.transform.rotation,
                        targetRotation,
                        aimTurnSpeed * AbilityDeltaTime);
                    yield return null;
                }
                if (owner == null) yield break;
                owner.transform.rotation = targetRotation;
                owner.IsAbilityFacingLocked = false;
                forward = aimDirection.normalized;
            }
        }

        Vector3 origin = owner.transform.position;
        if (debugLogging)
            Debug.Log($"[Hook] Fire owner={owner.name} position={origin:F2} forward={forward:F2} possessed={owner.isPossessed}");

        // Wrath01: dash toward hook direction
        if (IsUpgradeUnlocked("Wrath01"))
            StartCoroutine(DashForward(forward));

        // Fire hook
        bool wrath02 = IsUpgradeUnlocked("Wrath02");
        GameObject hookObj = null;
        HookProjectile hookProj = null;

        if (hookPrefab != null)
        {
            hookObj = SpawnVfxTracked(hookPrefab, origin, Quaternion.LookRotation(forward, Vector3.up));
            if (wrath02) hookObj.transform.localScale = Vector3.one * wrath02HookScale;
            hookProj = hookObj.GetComponent<HookProjectile>();
            if (hookProj != null)
                ConfigurePullHook(hookProj);
            if (hookProj != null && hookProj.debugLogging)
                Debug.Log($"[Hook] Launched owner={owner.name} position={origin:F2} forward={forward:F2} radius={hookProj.hitRadius:F2}");
        }
        else
        {
            hookObj = new GameObject("HookProj");
            hookObj.transform.position = origin;
            hookObj.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            if (wrath02) hookObj.transform.localScale = Vector3.one * wrath02HookScale;
            hookProj = hookObj.AddComponent<HookProjectile>();
            ConfigurePullHook(hookProj);
        }

        // Wait for hook to hit or miss
        float timeout = hookMaxRange / hookSpeed + 0.5f;
        float elapsed = 0f;
        while (!hookHit && elapsed < timeout)
        {
            elapsed += AbilityDeltaTime;
            yield return null;
        }

        // Pull logic
        if (hookHit)
        {
            if (wrath02 && multiTargets.Count > 0)
            {
                // Pull all hit targets
                foreach (var t in multiTargets)
                {
                    if (t == null) continue;
                    DamageTarget(t);
                    SpawnReturnVfx(t);
                    StartCoroutine(PullTarget(t));
                }
            }
            else if (pullTarget != null)
            {
                DamageTarget(pullTarget);
                SpawnReturnVfx(pullTarget);
                yield return StartCoroutine(PullTarget(pullTarget));
            }
        }

        hookHit = false;
        pullTarget = null;
        isPullingPlayer = false;
        multiTargets.Clear();
    }

    void DamageTarget(Transform target)
    {
        if (target == null) return;
        if (isPullingPlayer || target.CompareTag("Player"))
        {
            var ph = target.GetComponent<PlayerHealth>();
            if (ph != null) DealDamageToPlayer(ph, damage);
        }
        else
        {
            var enemy = target.GetComponent<Enemy>();
            if (enemy != null) DealDamageTo(enemy, damage * damageMultiplier);
        }
    }

    void SpawnReturnVfx(Transform target)
    {
        if (returnVfxPrefab == null || target == null || owner == null) return;
        Vector3 toOwner = (owner.transform.position - target.position).normalized;
        Quaternion rot = toOwner.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toOwner, Vector3.up) : Quaternion.identity;
        SpawnVfxTracked(returnVfxPrefab, target.position, rot, returnVfxDuration);
    }

    IEnumerator DashForward(Vector3 forward)
    {
        if (owner == null) yield break;
        float dist = 0f;
        while (dist < wrath01DashMaxDist && owner != null)
        {
            float step = wrath01DashSpeed * AbilityDeltaTime;
            dist += step;
            Vector3 targetPos = owner.transform.position + forward * step;
            targetPos.y = owner.transform.position.y;
            owner.transform.position = targetPos;
            yield return null;
        }
    }

    public void OnHookHitTarget(Transform target, bool isPlayer)
    {
        hookHit = true;
        pullTarget = target;
        isPullingPlayer = isPlayer;
        if (debugLogging) Debug.Log($"[Hook] Pull target={target?.name ?? "none"} isPlayer={isPlayer}");
    }

    private void ConfigurePullHook(HookProjectile hookProj)
    {
        if (hookProj == null) return;
        hookProj.flightMode = HookProjectile.FlightMode.PullTargets;
        hookProj.speed = hookSpeed;
        hookProj.maxTravelDistance = hookMaxRange;
        hookProj.maxLifetime = hookMaxRange / Mathf.Max(0.01f, hookSpeed) + 0.25f;
        hookProj.hitVfxPrefab = hitVfxPrefab;
        hookProj.hitVfxDuration = hitVfxDuration;
        hookProj.ownerAbility = this;
        hookProj.ownerTransform = owner != null ? owner.transform : null;
        hookProj.hitMask = owner != null && owner.isPossessed ? ~0 : targetMask;
        hookProj.useUnscaledTime = IsOwnedByPlayer;
        hookProj.onAnchorStop = null;
        hookProj.ResetForPoolSpawn();
    }

    /// <summary>Called by HookProjectile when hitting a target (Wrath02: multiple).</summary>
    public void OnHookHitMultiTarget(Transform target, bool isPlayer)
    {
        hookHit = true;
        if (target != null && !multiTargets.Contains(target))
            multiTargets.Add(target);
        if (pullTarget == null) pullTarget = target;
        isPullingPlayer = isPlayer || target.CompareTag("Player");
    }

    public void OnHookMissed()
    {
        hookHit = false;
        pullTarget = null;
        if (debugLogging) Debug.Log("[Hook] No valid target found before timeout.");
    }

    IEnumerator PullTarget(Transform target)
    {
        if (target == null || owner == null) yield break;

        Actor targetActor = target.GetComponent<Actor>();
        IController previousController = targetActor != null ? targetActor.Controller : null;
        if (targetActor != null) targetActor.SetController(NullController.Instance);

        while (target != null && owner != null)
        {
            Vector3 ownerPos = owner.transform.position;
            float dist = Vector3.Distance(target.position, ownerPos);
            if (dist <= pullStopDistance) break;
            target.position = Vector3.MoveTowards(target.position, ownerPos, pullSpeed * AbilityDeltaTime);
            yield return null;
        }

        if (targetActor != null && previousController != null) targetActor.SetController(previousController);
    }
}
