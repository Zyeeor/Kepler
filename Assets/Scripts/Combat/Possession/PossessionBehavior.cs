using System;
using UnityEngine;

/// <summary>
/// Resolves middle-click possession targets and delegates the state transition to PossessionManager.

/// This keeps input diagnostics and raycast policy separate from possession state changes.
/// </summary>
public class PossessionBehavior : MonoBehaviour
{
    [Header("Targeting")]
    [Tooltip("Maximum middle-click possession targeting distance.")]

    public float maxTargetDistance = 100f;
    [Tooltip("Nearest valid corpse fallback range when mouse ray and aim assist find no target.")]
    [Min(0f)] public float nearestCorpseFallbackRange = 15f;
    [Tooltip("Log every middle-click target resolution attempt for possession debugging.")]

    public bool enableDebugLogs = true;

    private PossessionManager manager;

    public void Initialize(PossessionManager possessionManager)
    {
        manager = possessionManager;
    }

    public bool TryBegin(Ray aimRay)
    {
        if (manager == null)
        {
            Log("Rejected: PossessionManager is missing.");
            return false;
        }

        if (!manager.CanStartPossession(out string stateReason))
        {
            Log("Rejected: " + stateReason);
            return false;
        }

        RaycastHit[] hits = Physics.RaycastAll(aimRay, maxTargetDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        Log($"Ray mouse={Input.mousePosition:F0}, origin={aimRay.origin:F2}, direction={aimRay.direction:F2}, hits={hits.Length}, state={manager.State}");

        if (hits.Length == 0)
            Log("Ray hit no collider; continuing to aim assist and nearest-corpse fallback.");

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            MonsterActor target = hit.collider.GetComponentInParent<MonsterActor>();
            if (target == null)
            {
                Log($"Hit[{i}] collider='{hit.collider.name}', distance={hit.distance:F2}, no MonsterActor.");
                continue;
            }

            if (!manager.ValidatePossessionTarget(target, out string reason))
            {
                Log($"Hit[{i}] monster='{target.displayName}', distance={hit.distance:F2}, rejected: {reason}. {target.GetPossessionDebugState()}");
                continue;
            }

            Log($"Selected monster='{target.displayName}', collider='{hit.collider.name}', distance={hit.distance:F2}. {target.GetPossessionDebugState()}");
            return manager.BeginPossessionFlight(target);
        }

        if (TryFindRayAssistedCorpse(aimRay, out MonsterActor assistedTarget, out float missDistance))
        {
            Log($"Ray assist selected monster='{assistedTarget.displayName}', rayMiss={missDistance:F2}m. {assistedTarget.GetPossessionDebugState()}");
            return manager.BeginPossessionFlight(assistedTarget);
        }

        if (TryFindNearestCorpseFallback(out MonsterActor fallbackTarget, out float fallbackDistance))
        {
            Log($"Nearest-corpse fallback selected monster='{fallbackTarget.displayName}', distance={fallbackDistance:F2}m. {fallbackTarget.GetPossessionDebugState()}");
            return manager.BeginPossessionFlight(fallbackTarget);
        }

        Log("Rejected: no valid corpse was found by mouse ray, aim assist, or nearest-corpse fallback.");
        return false;
    }

    private bool TryFindRayAssistedCorpse(Ray aimRay, out MonsterActor selected, out float selectedMissDistance)
    {
        selected = null;
        selectedMissDistance = float.MaxValue;
        const float assistRadius = 1.5f;

        foreach (MonsterActor candidate in FindObjectsOfType<MonsterActor>(true))
        {
            if (!manager.ValidatePossessionTarget(candidate, out _)) continue;

            Vector3 toCandidate = candidate.transform.position - aimRay.origin;
            float projectedDistance = Vector3.Dot(toCandidate, aimRay.direction);
            if (projectedDistance < 0f || projectedDistance > maxTargetDistance) continue;

            Vector3 closestPoint = aimRay.origin + aimRay.direction * projectedDistance;
            float missDistance = Vector3.Distance(candidate.transform.position, closestPoint);
            if (missDistance > assistRadius || missDistance >= selectedMissDistance) continue;

            selected = candidate;
            selectedMissDistance = missDistance;
        }

        return selected != null;
    }

    private bool TryFindNearestCorpseFallback(out MonsterActor selected, out float selectedDistance)
    {
        selected = null;
        selectedDistance = float.MaxValue;

        Vector3 playerPosition = manager.CurrentBody != null
            ? manager.CurrentBody.transform.position
            : (PlayerController.Instance != null ? PlayerController.Instance.transform.position : Vector3.zero);
        float fallbackRange = Mathf.Max(0f, nearestCorpseFallbackRange);
        float fallbackRangeSqr = fallbackRange * fallbackRange;

        foreach (MonsterActor candidate in FindObjectsOfType<MonsterActor>(true))
        {
            if (!manager.ValidatePossessionTarget(candidate, out _)) continue;

            float distanceSqr = (candidate.transform.position - playerPosition).sqrMagnitude;
            if (distanceSqr > fallbackRangeSqr || distanceSqr >= selectedDistance * selectedDistance) continue;

            selected = candidate;
            selectedDistance = Mathf.Sqrt(distanceSqr);
        }

        return selected != null;
    }

    private void Log(string message)
    {
        if (enableDebugLogs) Debug.Log("[PossessionTargeting] " + message);
    }
}
