using UnityEngine;

/// <summary>Chooses a clear ground point on a ring around the current player body.</summary>
public sealed class BossTeleportPlanner : MonoBehaviour
{
    [Tooltip("Fail-safe minimum separation from the player. Keep this high enough to avoid body overlap.")]
    public float minPlayerDistance = 5f;
    [Tooltip("Default void-walk landing separation. The Boss Actor overrides this at runtime from its Void Walk Landing section.")]
    public float preferredDistance = 5f;
    [Tooltip("Farthest allowed void-walk landing separation. The Boss Actor overrides this at runtime from its Void Walk Landing section.")]
    public float maxPlayerDistance = 6f;
    public int candidateCount = 10;
    public float clearanceRadius = 1.2f;
    public float clearanceHeight = 3.5f;

    public bool TryPlanAroundTarget(BossSevenfoldActor boss, Vector3 playerPosition, out Vector3 result)
    {
        result = boss.transform.position;
        int mask = boss.groundLayer.value != 0 ? boss.groundLayer.value : ~0;
        for (int i = 0; i < candidateCount; i++)
        {
            float angle = (i / (float)candidateCount + boss.AiRandomValue()) * Mathf.PI * 2f;
            float radius = boss.AiRandomRange(Mathf.Max(minPlayerDistance, preferredDistance - 2f),
                Mathf.Max(preferredDistance, maxPlayerDistance));
            Vector3 probe = playerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            if (!Physics.Raycast(probe + Vector3.up * 12f, Vector3.down, out RaycastHit groundHit, 30f, mask,
                    QueryTriggerInteraction.Ignore))
                continue;
            Vector3 candidate = groundHit.point;
            if (!TryPlan(boss.transform.position, playerPosition, candidate, out candidate)) continue;
            Vector3 bottom = candidate + Vector3.up * 0.35f;
            Vector3 top = bottom + Vector3.up * clearanceHeight;
            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, clearanceRadius, ~0, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            for (int j = 0; j < overlaps.Length; j++)
            {
                Collider overlap = overlaps[j];
                if (overlap != null && !overlap.transform.IsChildOf(boss.transform) && overlap.transform != groundHit.transform)
                {
                    blocked = true;
                    break;
                }
            }
            if (blocked) continue;
            result = candidate;
            return true;
        }
        return false;
    }
    public bool TryPlan(Vector3 bossPosition, Vector3 playerPosition, Vector3 candidate, out Vector3 result)
    {
        result = candidate;
        Vector3 fromPlayer = result - playerPosition;
        fromPlayer.y = 0f;
        if (fromPlayer.sqrMagnitude < minPlayerDistance * minPlayerDistance) return false;
        return true;
    }
}
