using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wrath Movement: fire HookProjectile toward mouse/facing aim; on stop (max range / monster / obstacle)
/// dash the body to that point. Hook head itself deals no baseline damage.
/// WR-M01 path damage during the body dash; WR-M02 distance ×1.5 + landing impact.
/// </summary>
public class EnemyAbility_WrathSelfGrapple : EnemyAbility
{
    public const string TagGrapple = "Ability.Monster.Wrath.Grapple";
    public const string CardPathDamage = "WR-M01";
    public const string CardRangeImpact = "WR-M02";

    [Header("Grapple")]
    public float grappleDistance = 8f;
    public float rangeMultiplierWithM02 = 1.5f;
    public float hookSpeed = 32f;
    public float grappleSpeed = 36f;
    public LayerMask obstacleMask = ~0;
    public float aimTurnSpeed = 720f;

    [Header("WR-M01 Path Damage")]
    public float pathDamage = 15f;
    public float pathRadius = 0.75f;

    [Header("WR-M02 Landing Impact")]
    public float landingImpactRadius = 2.5f;
    public float landingImpactDamage = 20f;
    public GameObject landingImpactVfxPrefab;
    public float landingImpactVfxDuration = 0.8f;

    [Header("Hook Projectile")]
    public GameObject hookVisualPrefab;
    public GameObject hookHitVfxPrefab;
    public float hookHitVfxDuration = 0.35f;

    private readonly HashSet<int> _pathHitIds = new HashSet<int>();
    private bool _hookResolved;
    private Vector3 _hookStopPoint;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "钩索位移";
        cooldown = cooldown <= 0f ? 3f : cooldown;
        EnsureTag(TagGrapple);
        EnsureUpgrade(CardPathDamage);
        EnsureUpgrade(CardRangeImpact);
        // Prefab previously serialized this as hookVisualPrefab.
        if (obstacleMask.value == 0) obstacleMask = ~0;
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        return TryResolveFireDirection(out _);
    }

    protected override void OnTrigger()
    {
        if (!TryResolveFireDirection(out Vector3 direction))
        {
            EndActivationEffect();
            currentCooldown = 0f;
            return;
        }

        foreach (Animator animator in owner.GetComponentsInChildren<Animator>(false))
            animator.SetTrigger("Mobility");


        StartCoroutine(GrappleRoutine(direction));
    }

    private IEnumerator GrappleRoutine(Vector3 direction)
    {
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            EndActivationEffect();
            currentCooldown = 0f;
            yield break;
        }
        direction.Normalize();

        owner.IsAbilityFacingLocked = true;
        if (owner is MonsterActor monsterFace)
            monsterFace.IsAbilityLocomotionLocked = true;
        owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        float maxDist = GetMaxGrappleDistance();
        float hookY = owner.transform.position.y;
        Vector3 origin = new Vector3(owner.transform.position.x, hookY, owner.transform.position.z);
        _hookResolved = false;
        _hookStopPoint = origin + direction * maxDist;

        HookProjectile hook = FireHook(origin, direction, maxDist);
        float timeout = maxDist / Mathf.Max(0.01f, hookSpeed) + 0.35f;
        float elapsed = 0f;
        while (owner != null && !_hookResolved && elapsed < timeout)
        {
            elapsed += AbilityDeltaTime;
            yield return null;
        }

        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        if (!_hookResolved)
            _hookStopPoint = origin + direction * maxDist;

        // Top-down: hook flies on the XZ plane at the owner's Y; landing keeps that Y.
        _hookStopPoint.y = hookY;
        Vector3 landingPoint = _hookStopPoint;
        Vector3 start = owner.transform.position;
        _pathHitIds.Clear();

        float distance = Vector3.Distance(start, landingPoint);
        float duration = Mathf.Max(0.04f, distance / Mathf.Max(0.01f, grappleSpeed));
        elapsed = 0f;
        bool dealPath = IsUpgradeUnlocked(CardPathDamage);

        while (owner != null && elapsed < duration)
        {
            elapsed += AbilityDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = Vector3.Lerp(start, landingPoint, t);
            pos.y = hookY;
            Vector3 previous = owner.transform.position;
            owner.transform.position = pos;

            if (dealPath)
                DealPathDamageSegment(previous, pos);

            yield return null;
        }

        if (owner != null)
        {
            owner.transform.position = landingPoint;
            ClearLocomotionLock();

            if (IsUpgradeUnlocked(CardRangeImpact))
            {
                PlayLandingImpact(landingPoint);
                DamageEnemiesInSphere(landingPoint, landingImpactRadius, landingImpactDamage, null, landingImpactVfxDuration);
                if (!owner.isPossessed)
                    TryDamagePlayerInRadius(landingPoint, landingImpactRadius, landingImpactDamage, landingImpactVfxDuration);
            }
        }

        EndActivationEffect();
    }

    private HookProjectile FireHook(Vector3 origin, Vector3 direction, float maxDist)
    {
        Quaternion rot = Quaternion.LookRotation(direction, Vector3.up);
        GameObject hookObj;
        HookProjectile hookProj;

        if (hookVisualPrefab != null)
        {
            // Do not auto-release: HookProjectile owns its lifetime.
            hookObj = SpawnVfxTracked(hookVisualPrefab, origin, rot);
            hookProj = hookObj != null ? hookObj.GetComponent<HookProjectile>() : null;
            if (hookObj != null && hookProj == null)
                hookProj = hookObj.AddComponent<HookProjectile>();
        }
        else
        {
            hookObj = new GameObject("WrathGrappleHook");
            hookObj.transform.SetPositionAndRotation(origin, rot);
            hookProj = hookObj.AddComponent<HookProjectile>();
        }

        if (hookProj == null) return null;

        hookProj.speed = hookSpeed;
        hookProj.maxTravelDistance = maxDist;
        hookProj.maxLifetime = maxDist / Mathf.Max(0.01f, hookSpeed) + 0.25f;
        hookProj.hitVfxPrefab = hookHitVfxPrefab;
        hookProj.hitVfxDuration = hookHitVfxDuration;
        hookProj.flightMode = HookProjectile.FlightMode.AnchorStop;
        hookProj.ownerAbility = null;
        hookProj.ownerTransform = owner != null ? owner.transform : null;
        hookProj.hitMask = ~0;
        hookProj.obstacleMask = obstacleMask.value == 0 ? (LayerMask)~0 : obstacleMask;
        hookProj.SetOwnerScaleMultiplier(OwnerCombatScaleMultiplier);
        hookProj.useUnscaledTime = IsOwnedByPlayer;
        hookProj.debugLogging = false;
        hookProj.ResetForPoolSpawn();
        hookProj.onAnchorStop = OnHookAnchored;
        return hookProj;
    }

    private void OnHookAnchored(Vector3 stopPoint)
    {
        _hookStopPoint = stopPoint;
        _hookResolved = true;
    }

    private bool TryResolveFireDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (owner == null) return false;

        if (owner.isPossessed && PlayerController.Instance != null &&
            PlayerController.Instance.TryGetAimPoint(out Vector3 aimPoint))
        {
            direction = aimPoint - owner.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                return true;
            }
        }

        if (!owner.isPossessed && owner.targetPlayer != null)
        {
            direction = owner.targetPlayer.position - owner.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                direction.Normalize();
                return true;
            }
        }

        direction = owner.transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return false;
        direction.Normalize();
        return true;
    }

    private float GetMaxGrappleDistance()
    {
        float distance = Mathf.Max(0.5f, GetCardParameter("GrappleDistance", grappleDistance));
        if (IsUpgradeUnlocked(CardRangeImpact))
            distance *= Mathf.Max(1f, GetCardParameter("GrappleRangeMult", rangeMultiplierWithM02));
        return ScaleAbilityRadius(distance);
    }

    private void DealPathDamageSegment(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float dist = delta.magnitude;
        if (dist < 0.0001f)
        {
            CollectAndDamageSphere(to, pathRadius);
            return;
        }

        Vector3 dir = delta / dist;
        float effectivePathRadius = ScaleAbilityRadius(pathRadius);
        CombatHitboxDebug.DrawCapsule(drawHitboxes, from, to, effectivePathRadius, 0f);
        RaycastHit[] hits = Physics.SphereCastAll(from, effectivePathRadius, dir, dist, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
            TryDamageColliderOnce(hit.collider);

        CollectAndDamageSphere(to, pathRadius);
    }

    private void CollectAndDamageSphere(Vector3 center, float radius)
    {
        float effectiveRadius = ScaleAbilityRadius(radius);
        CombatHitboxDebug.DrawSphere(drawHitboxes, center, effectiveRadius, 0f);
        Collider[] hits = Physics.OverlapSphere(center, effectiveRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
            TryDamageColliderOnce(hit);
    }

    private void TryDamageColliderOnce(Collider hit)
    {
        if (hit == null || owner == null) return;

        Enemy enemy = hit.GetComponentInParent<Enemy>();
        if (enemy != null && owner.CanDamage(enemy) && _pathHitIds.Add(enemy.GetInstanceID()))
        {
            DealDamageTo(enemy, pathDamage);
            return;
        }

        if (!owner.isPossessed)
        {
            PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
            if (player != null && owner.CanDamageSoul() && _pathHitIds.Add(player.GetInstanceID()))
                DealDamageToPlayer(player, pathDamage);
        }
    }

    private void PlayLandingImpact(Vector3 position)
    {
        if (landingImpactVfxPrefab == null) return;
        SpawnVfxTracked(landingImpactVfxPrefab, position, Quaternion.identity, landingImpactVfxDuration);
    }

    private void ClearLocomotionLock()
    {
        if (owner == null) return;
        owner.IsAbilityFacingLocked = false;
        if (owner is MonsterActor monster)
            monster.IsAbilityLocomotionLocked = false;
    }

    protected override void OnDisable()
    {
        ClearLocomotionLock();
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        ClearLocomotionLock();
        _hookResolved = true;
        StopAllCoroutines();
        base.ResetForOwnerReuse();
    }

    private void EnsureTag(string tag)
    {
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, tag, System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add(tag);
    }

    private void EnsureUpgrade(string effectId)
    {
        if (upgrades == null) upgrades = new List<UpgradeSlot>();
        if (upgrades.Exists(u => u != null && string.Equals(u.effectId, effectId, System.StringComparison.OrdinalIgnoreCase)))
            return;
        upgrades.Add(new UpgradeSlot { effectId = effectId, unlocked = false });
    }
}
