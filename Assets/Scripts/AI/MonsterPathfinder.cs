using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime XZ pathfinding for normal monsters. It samples the live streamed world instead
/// of relying on a baked NavMesh, so newly instantiated terrain is immediately respected.
/// Solid colliders and Lava/Spike TerrainEffectTile volumes are treated as blocked.
/// </summary>
[DisallowMultipleComponent]
public sealed class MonsterPathfinder : MonoBehaviour
{
    private const float NearRepathInterval = 0.15f;
    private const float FarRepathInterval = 0.75f;
    private const float MaxSearchDistance = 32f;
    private const int MaxSearchNodes = 768;
    private const float HazardBucketSize = 4f;
    private const float HazardRefreshInterval = 0.25f;

    private struct PathNode
    {
        public float g;
        public Vector2Int parent;
        public bool hasParent;
    }

    private struct HazardRect
    {
        public float minX;
        public float maxX;
        public float minZ;
        public float maxZ;
    }

    private static readonly Dictionary<Vector2Int, List<HazardRect>> HazardBuckets = new Dictionary<Vector2Int, List<HazardRect>>();
    private static float nextHazardRefreshAt;

    private readonly List<Vector2Int> openCells = new List<Vector2Int>(MaxSearchNodes);
    private readonly Dictionary<Vector2Int, PathNode> nodes = new Dictionary<Vector2Int, PathNode>(MaxSearchNodes);
    private readonly HashSet<Vector2Int> closedCells = new HashSet<Vector2Int>();
    private readonly List<Vector2Int> reversePath = new List<Vector2Int>(64);
    private readonly List<Vector3> path = new List<Vector3>(64);
    private readonly Collider[] overlapBuffer = new Collider[48];

    private MonsterActor host;
    private Camera gameplayCamera;
    private int waypointIndex;
    private float nextRepathAt;
    private float cellSize;
    private float agentRadius;
    private float agentCenterY;
    private float agentHalfHeight;

    private void Awake()
    {
        host = GetComponent<MonsterActor>();
        RefreshAgentFootprint();
    }

    private void OnDisable()
    {
        ClearPath();
        nextRepathAt = 0f;
    }

    /// <summary>
    /// Returns a replacement movement direction only when the direct route is unsafe or an
    /// existing route is being followed. Clear routes keep the BT's normal strafe behavior.
    /// </summary>
    public bool TryGetMoveDirection(Vector3 target, Vector3 preferredDirection, float stoppingDistance,
        float distanceToTarget, out Vector3 direction)
    {
        direction = Vector3.zero;
        if (host == null || distanceToTarget <= stoppingDistance) return false;

        bool hasRoute = HasWaypoint(out direction);
        if (Time.time < nextRepathAt) return hasRoute;

        RefreshAgentFootprint();
        RefreshHazardCache();

        Vector3 start = transform.position;
        start.y = 0f;
        target.y = 0f;
        float interval = distanceToTarget >= GetFarDistance() ? FarRepathInterval : NearRepathInterval;
        nextRepathAt = Time.time + interval;

        bool directRouteClear = IsSegmentClear(start, target);
        bool preferredMoveIsSafe = preferredDirection.sqrMagnitude <= 0.0001f
            || !IsBlocked(start + preferredDirection.normalized * Mathf.Max(agentRadius, cellSize));
        if (directRouteClear && preferredMoveIsSafe)
        {
            ClearPath();
            return false;
        }

        Vector3 requestedTarget = target;
        Vector3 toTarget = target - start;
        if (toTarget.sqrMagnitude > MaxSearchDistance * MaxSearchDistance)
            requestedTarget = start + toTarget.normalized * MaxSearchDistance;

        BuildPath(start, requestedTarget);
        return HasWaypoint(out direction);
    }

    private bool HasWaypoint(out Vector3 direction)
    {
        direction = Vector3.zero;
        while (waypointIndex < path.Count)
        {
            Vector3 toWaypoint = path[waypointIndex] - transform.position;
            toWaypoint.y = 0f;
            float arrival = Mathf.Max(0.2f, cellSize * 0.35f);
            if (toWaypoint.sqrMagnitude <= arrival * arrival)
            {
                waypointIndex++;
                continue;
            }

            direction = toWaypoint.normalized;
            return true;
        }

        ClearPath();
        return false;
    }

    private void BuildPath(Vector3 start, Vector3 target)
    {
        ClearPath();
        cellSize = Mathf.Clamp(agentRadius * 2f, 0.75f, 2.5f);

        Vector2Int startCell = ToCell(start);
        Vector2Int goalCell = FindNearestWalkable(ToCell(target));

        openCells.Clear();
        nodes.Clear();
        closedCells.Clear();
        openCells.Add(startCell);
        nodes[startCell] = new PathNode { g = 0f, hasParent = false };

        Vector2Int bestCell = startCell;
        float bestGoalDistance = CellDistance(startCell, goalCell);
        int expanded = 0;

        while (openCells.Count > 0 && expanded < MaxSearchNodes)
        {
            int bestOpenIndex = FindBestOpenIndex(goalCell);
            Vector2Int current = openCells[bestOpenIndex];
            openCells.RemoveAt(bestOpenIndex);
            if (!closedCells.Add(current)) continue;

            float currentGoalDistance = CellDistance(current, goalCell);
            if (currentGoalDistance < bestGoalDistance)
            {
                bestGoalDistance = currentGoalDistance;
                bestCell = current;
            }

            if (current == goalCell)
            {
                bestCell = current;
                break;
            }

            expanded++;
            ExpandNeighbours(current, goalCell);
        }

        ReconstructPath(startCell, bestCell);
    }

    private int FindBestOpenIndex(Vector2Int goalCell)
    {
        int index = 0;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < openCells.Count; i++)
        {
            Vector2Int cell = openCells[i];
            PathNode node = nodes[cell];
            float score = node.g + CellDistance(cell, goalCell);
            if (score >= bestScore) continue;
            bestScore = score;
            index = i;
        }
        return index;
    }

    private void ExpandNeighbours(Vector2Int current, Vector2Int goalCell)
    {
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        {
            if (x == 0 && y == 0) continue;

            Vector2Int next = new Vector2Int(current.x + x, current.y + y);
            if (closedCells.Contains(next) || IsBlocked(CellToWorld(next))) continue;

            if (x != 0 && y != 0)
            {
                if (IsBlocked(CellToWorld(new Vector2Int(current.x + x, current.y)))
                    || IsBlocked(CellToWorld(new Vector2Int(current.x, current.y + y))))
                    continue;
            }

            float stepCost = x != 0 && y != 0 ? 1.4142135f : 1f;
            float candidateG = nodes[current].g + stepCost;
            if (nodes.TryGetValue(next, out PathNode known) && candidateG >= known.g) continue;

            nodes[next] = new PathNode { g = candidateG, parent = current, hasParent = true };
            if (!openCells.Contains(next)) openCells.Add(next);
        }
    }

    private void ReconstructPath(Vector2Int startCell, Vector2Int endCell)
    {
        reversePath.Clear();
        Vector2Int current = endCell;
        while (current != startCell)
        {
            reversePath.Add(current);
            if (!nodes.TryGetValue(current, out PathNode node) || !node.hasParent)
            {
                reversePath.Clear();
                return;
            }
            current = node.parent;
        }

        for (int i = reversePath.Count - 1; i >= 0; i--)
            path.Add(CellToWorld(reversePath[i]));
    }

    private Vector2Int FindNearestWalkable(Vector2Int requested)
    {
        if (!IsBlocked(CellToWorld(requested))) return requested;

        for (int radius = 1; radius <= 3; radius++)
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
        {
            if (Mathf.Abs(x) != radius && Mathf.Abs(y) != radius) continue;
            Vector2Int candidate = new Vector2Int(requested.x + x, requested.y + y);
            if (!IsBlocked(CellToWorld(candidate))) return candidate;
        }
        return requested;
    }

    private bool IsSegmentClear(Vector3 start, Vector3 end)
    {
        float sampleSpacing = Mathf.Clamp(agentRadius, 0.4f, 1.25f);
        float distance = Vector3.Distance(start, end);
        int samples = Mathf.Max(1, Mathf.CeilToInt(distance / sampleSpacing));
        for (int i = 1; i <= samples; i++)
        {
            Vector3 point = Vector3.Lerp(start, end, (float)i / samples);
            if (IsBlocked(point)) return false;
        }
        return true;
    }

    private bool IsBlocked(Vector3 point)
    {
        if (IsHazard(point)) return true;

        Vector3 center = new Vector3(point.x, agentCenterY, point.z);
        Vector3 extents = new Vector3(agentRadius, agentHalfHeight, agentRadius);
        int count = Physics.OverlapBoxNonAlloc(center, extents, overlapBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider collider = overlapBuffer[i];
            if (collider == null || collider.isTrigger) continue;
            if (collider.transform.IsChildOf(transform) || transform.IsChildOf(collider.transform)) continue;
            if (collider.GetComponentInParent<Actor>() != null) continue;

            Bounds bounds = collider.bounds;
            if (bounds.max.y <= transform.position.y + 0.05f) continue;
            bounds.Expand(new Vector3(agentRadius * 2f, 0f, agentRadius * 2f));
            if (point.x >= bounds.min.x && point.x <= bounds.max.x
                && point.z >= bounds.min.z && point.z <= bounds.max.z)
                return true;
        }
        return false;
    }

    private bool IsHazard(Vector3 point)
    {
        int range = Mathf.CeilToInt(agentRadius / HazardBucketSize) + 1;
        Vector2Int cell = ToHazardCell(point);
        for (int x = -range; x <= range; x++)
        for (int y = -range; y <= range; y++)
        {
            if (!HazardBuckets.TryGetValue(new Vector2Int(cell.x + x, cell.y + y), out List<HazardRect> hazards)) continue;
            for (int i = 0; i < hazards.Count; i++)
            {
                HazardRect hazard = hazards[i];
                if (point.x >= hazard.minX - agentRadius && point.x <= hazard.maxX + agentRadius
                    && point.z >= hazard.minZ - agentRadius && point.z <= hazard.maxZ + agentRadius)
                    return true;
            }
        }
        return false;
    }

    private void RefreshAgentFootprint()
    {
        Collider collider = GetComponentInChildren<CapsuleCollider>();
        if (collider == null) collider = GetComponent<Collider>();

        if (collider != null)
        {
            Bounds bounds = collider.bounds;
            agentRadius = Mathf.Max(0.25f, Mathf.Max(bounds.extents.x, bounds.extents.z));
            agentCenterY = bounds.center.y;
            agentHalfHeight = Mathf.Max(0.25f, bounds.extents.y);
        }
        else
        {
            agentRadius = 0.4f;
            agentCenterY = transform.position.y + 0.75f;
            agentHalfHeight = 0.75f;
        }

        cellSize = Mathf.Clamp(agentRadius * 2f, 0.75f, 2.5f);
    }

    private float GetFarDistance()
    {
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (gameplayCamera == null) return 40f;

        Plane plane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
        Ray lowerLeft = gameplayCamera.ViewportPointToRay(Vector3.zero);
        Ray upperRight = gameplayCamera.ViewportPointToRay(Vector3.one);
        if (!plane.Raycast(lowerLeft, out float lowerLeftDistance)
            || !plane.Raycast(upperRight, out float upperRightDistance))
            return 40f;

        float screenDiameter = Vector3.Distance(
            lowerLeft.GetPoint(lowerLeftDistance),
            upperRight.GetPoint(upperRightDistance));
        return Mathf.Max(1f, screenDiameter * 2f);
    }

    private Vector2Int ToCell(Vector3 point)
    {
        return new Vector2Int(Mathf.RoundToInt(point.x / cellSize), Mathf.RoundToInt(point.z / cellSize));
    }

    private Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x * cellSize, transform.position.y, cell.y * cellSize);
    }

    private static float CellDistance(Vector2Int a, Vector2Int b)
    {
        float x = a.x - b.x;
        float y = a.y - b.y;
        return Mathf.Sqrt(x * x + y * y);
    }

    private void ClearPath()
    {
        path.Clear();
        waypointIndex = 0;
    }

    private static void RefreshHazardCache()
    {
        if (Time.time < nextHazardRefreshAt) return;
        nextHazardRefreshAt = Time.time + HazardRefreshInterval;
        HazardBuckets.Clear();

        TerrainEffectTile[] tiles = Object.FindObjectsOfType<TerrainEffectTile>();
        for (int i = 0; i < tiles.Length; i++)
        {
            TerrainEffectTile tile = tiles[i];
            if (tile == null || !tile.isActiveAndEnabled) continue;
            if (tile.kind != TerrainEffectTile.TerrainEffectKind.Lava && tile.kind != TerrainEffectTile.TerrainEffectKind.Spike)
                continue;

            Vector3 center = tile.transform.TransformPoint(tile.detectionCenter);
            Vector3 scale = tile.transform.lossyScale;
            float halfX = Mathf.Abs(tile.detectionSize.x * scale.x) * 0.5f;
            float halfZ = Mathf.Abs(tile.detectionSize.z * scale.z) * 0.5f;
            if (halfX <= 0f || halfZ <= 0f) continue;

            HazardRect rect = new HazardRect
            {
                minX = center.x - halfX,
                maxX = center.x + halfX,
                minZ = center.z - halfZ,
                maxZ = center.z + halfZ,
            };
            AddHazard(rect);
        }
    }

    private static void AddHazard(HazardRect rect)
    {
        int minX = Mathf.FloorToInt(rect.minX / HazardBucketSize);
        int maxX = Mathf.FloorToInt(rect.maxX / HazardBucketSize);
        int minZ = Mathf.FloorToInt(rect.minZ / HazardBucketSize);
        int maxZ = Mathf.FloorToInt(rect.maxZ / HazardBucketSize);
        for (int x = minX; x <= maxX; x++)
        for (int z = minZ; z <= maxZ; z++)
        {
            Vector2Int key = new Vector2Int(x, z);
            if (!HazardBuckets.TryGetValue(key, out List<HazardRect> list))
            {
                list = new List<HazardRect>(2);
                HazardBuckets.Add(key, list);
            }
            list.Add(rect);
        }
    }

    private static Vector2Int ToHazardCell(Vector3 point)
    {
        return new Vector2Int(
            Mathf.FloorToInt(point.x / HazardBucketSize),
            Mathf.FloorToInt(point.z / HazardBucketSize));
    }
}
