using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wrath Movement: hook self to a forward legal ground point.
/// No legal point → CanTrigger false (no HP cost / no reload).
/// WR-M01 path damage; WR-M02 distance ×1.5 + landing impact.
/// </summary>
public class EnemyAbility_WrathSelfGrapple : EnemyAbility
{
    public const string TagGrapple = "Ability.Monster.Wrath.Grapple";
    public const string CardPathDamage = "WR-M01";
    public const string CardRangeImpact = "WR-M02";

    [Header("Grapple")]
    public float grappleDistance = 8f;
    public float rangeMultiplierWithM02 = 1.5f;
    public float grappleSpeed = 28f;
    public float groundProbeHeight = 2.5f;
    public float groundProbeDown = 5f;
    public float minNormalY = 0.45f;
    public float sampleStep = 0.5f;
    public LayerMask groundMask = ~0;
    public float aimTurnSpeed = 720f;

    [Header("WR-M01 Path Damage")]
    public float pathDamage = 15f;
    public float pathRadius = 0.75f;

    [Header("WR-M02 Landing Impact")]
    public float landingImpactRadius = 2.5f;
    public float landingImpactDamage = 20f;
    public GameObject landingImpactVfxPrefab;
    public float landingImpactVfxDuration = 0.8f;

    [Header("Hook Visual (non-damaging)")]
    public GameObject hookVisualPrefab;
    public float hookVisualLifetime = 0.35f;

    private Vector3 _cachedLandingPoint;
    private bool _hasCachedLanding;
    private readonly HashSet<int> _pathHitIds = new HashSet<int>();

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "钩索位移";
        cooldown = cooldown <= 0f ? 3f : cooldown;
        EnsureTag(TagGrapple);
        EnsureUpgrade(CardPathDamage);
        EnsureUpgrade(CardRangeImpact);
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        _hasCachedLanding = TryFindLandingPoint(out _cachedLandingPoint);
        return _hasCachedLanding;
    }

    protected override void OnTrigger()
    {
        if (!_hasCachedLanding && !TryFindLandingPoint(out _cachedLandingPoint))
        {
            EndActivationEffect();
            currentCooldown = 0f;
            return;
        }

        StartCoroutine(GrappleRoutine(_cachedLandingPoint));
        _hasCachedLanding = false;
    }

    private IEnumerator GrappleRoutine(Vector3 landingPoint)
    {
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        Vector3 start = owner.transform.position;
        Vector3 flatDir = landingPoint - start;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude > 0.0001f)
            yield return RotatePossessedOwnerTowards(flatDir.normalized, aimTurnSpeed);

        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        SpawnHookVisual(start, landingPoint);
        _pathHitIds.Clear();

        float distance = Vector3.Distance(start, landingPoint);
        float duration = Mathf.Max(0.05f, distance / Mathf.Max(0.01f, grappleSpeed));
        float elapsed = 0f;
        owner.IsAbilityFacingLocked = true;
        bool dealPath = IsUpgradeUnlocked(CardPathDamage);

        while (owner != null && elapsed < duration)
        {
            elapsed += AbilityDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = Vector3.Lerp(start, landingPoint, t);
            pos.y = Mathf.Lerp(start.y, landingPoint.y, t);
            Vector3 previous = owner.transform.position;
            owner.transform.position = pos;

            if (dealPath)
                DealPathDamageSegment(previous, pos);

            yield return null;
        }

        if (owner != null)
        {
            owner.transform.position = landingPoint;
            owner.IsAbilityFacingLocked = false;

            if (IsUpgradeUnlocked(CardRangeImpact))
            {
                PlayLandingImpact(landingPoint);
                DamageEnemiesInSphere(landingPoint, landingImpactRadius, landingImpactDamage);
                if (!owner.isPossessed)
                    TryDamagePlayerInRadius(landingPoint, landingImpactRadius, landingImpactDamage);
            }
        }

        EndActivationEffect();
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
        RaycastHit[] hits = Physics.SphereCastAll(from, pathRadius, dir, dist, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
            TryDamageColliderOnce(hit.collider);

        CollectAndDamageSphere(to, pathRadius);
    }

    private void CollectAndDamageSphere(Vector3 center, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
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

    private bool TryFindLandingPoint(out Vector3 landingPoint)
    {
        landingPoint = Vector3.zero;
        if (owner == null) return false;

        Vector3 origin = owner.transform.position;
        Vector3 direction = owner.transform.forward;
        if (owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aim))
            direction = aim;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = owner.transform.forward;
        direction.Normalize();

        float maxDist = GetMaxGrappleDistance();
        LayerMask mask = groundMask.value == 0 ? (LayerMask)~0 : groundMask;
        float step = Mathf.Max(0.25f, sampleStep);

        for (float d = maxDist; d >= step; d -= step)
        {
            Vector3 probe = origin + direction * d + Vector3.up * groundProbeHeight;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, groundProbeDown, mask, QueryTriggerInteraction.Ignore))
                continue;
            if (hit.normal.y < minNormalY) continue;

            landingPoint = hit.point;
            return true;
        }

        return false;
    }

    private float GetMaxGrappleDistance()
    {
        float distance = Mathf.Max(0.5f, GetCardParameter("GrappleDistance", grappleDistance));
        if (IsUpgradeUnlocked(CardRangeImpact))
            distance *= Mathf.Max(1f, GetCardParameter("GrappleRangeMult", rangeMultiplierWithM02));
        return distance;
    }

    private void SpawnHookVisual(Vector3 from, Vector3 to)
    {
        if (hookVisualPrefab == null) return;
        Vector3 mid = (from + to) * 0.5f;
        mid.y += 0.5f;
        Vector3 dir = to - from;
        Quaternion rot = dir.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(dir.normalized, Vector3.up)
            : Quaternion.identity;
        SpawnVfxTracked(hookVisualPrefab, mid, rot, hookVisualLifetime);
    }

    private void PlayLandingImpact(Vector3 position)
    {
        if (landingImpactVfxPrefab == null) return;
        SpawnVfxTracked(landingImpactVfxPrefab, position, Quaternion.identity, landingImpactVfxDuration);
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
