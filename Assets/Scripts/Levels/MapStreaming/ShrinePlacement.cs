using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 神龛 Chunk 规划器（纯逻辑、确定性、生成顺序无关）。
///
/// 规则：
///   1. 第 1 个神龛必定落在玩家出生点最近的 N 个 Chunk 之一（默认 4 = 四邻接）；
///   2. 任意两个神龛所在 Chunk 互不相邻（四邻接互斥），避免连续刷出；
///   3. 全图神龛总数可配置（默认 3）。
///
/// 确定性：随机源仅 System.Random(世界种子 + 固定盐)，与 Chunk 生成顺序无关，
///   同一 (worldSeed, 出生 Chunk, 边界) 永远得到同一组神龛坐标，重生成一致。
///
/// 规划时机：MapStreamingSystem.Awake（边界初始化后）一次性算出，缓存供 Tile 生成查询。
/// </summary>
public static class ShrinePlacement
{
    /// <summary>
    /// 规划全图神龛 Chunk 集合。
    /// </summary>
    /// <param name="worldSeed">世界种子。</param>
    /// <param name="spawnChunk">玩家出生点所在 Chunk。</param>
    /// <param name="min">地图边界最小 Chunk（含）。</param>
    /// <param name="max">地图边界最大 Chunk（含）。</param>
    /// <param name="count">神龛总数（≤0 = 不生成）。</param>
    /// <param name="nearbyCount">出生点最近 Chunk 保底数（≥1）。</param>
    /// <returns>神龛 Chunk 集合（可能少于 count，取决于边界内可放置量）。</returns>
    public static HashSet<ChunkCoord> Plan(uint worldSeed, ChunkCoord spawnChunk, ChunkCoord min, ChunkCoord max, int count, int nearbyCount)
    {
        var result = new HashSet<ChunkCoord>();
        if (count <= 0) return result;
        if (nearbyCount < 1) nearbyCount = 1;

        var rng = new System.Random(unchecked((int)worldSeed) ^ 0x5EED);
        var used = new HashSet<ChunkCoord>();      // 已选神龛
        var blocked = new HashSet<ChunkCoord>();   // 被相邻互斥占用的坐标（不能放神龛）

        // ① 第一个神龛：出生点最近 nearbyCount 个 Chunk（四邻接 + 自身依次外扩）中确定性选一个
        var nearby = CollectNearby(spawnChunk, min, max, nearbyCount);
        if (nearby.Count > 0)
        {
            var first = nearby[rng.Next(nearby.Count)];
            Place(result, used, blocked, first);
        }

        // ② 其余神龛：在地图边界内随机撒点（确定性洗牌），跳过 blocked，直到凑满 count 或候选耗尽
        int placed = result.Count;
        int width = max.x - min.x + 1;
        int height = max.y - min.y + 1;
        int total = width * height;
        if (placed < count && total > 0)
        {
            // 确定性候选遍历：从随机起点按固定步长环状扫过全部格，保证同种子同顺序、且能尝试所有格
            int start = rng.Next(total);
            int step = PickStep(rng, total);
            for (int k = 0; k < total && placed < count; k++)
            {
                int idx = (start + k * step) % total;
                var c = new ChunkCoord(min.x + idx % width, min.y + idx / width);
                if (blocked.Contains(c) || used.Contains(c)) continue;
                // 跳过"出生点保底区已占位"附近仍可能造成的重复——blocked 已覆盖
                Place(result, used, blocked, c);
                placed = result.Count;
            }
        }

        return result;
    }

    /// <summary>把某 Chunk 标记为神龛，并封锁其四邻接（互斥）。</summary>
    static void Place(HashSet<ChunkCoord> result, HashSet<ChunkCoord> used, HashSet<ChunkCoord> blocked, ChunkCoord c)
    {
        result.Add(c);
        used.Add(c);
        blocked.Add(c);
        foreach (var dir in ChunkCoord.AllDirections)
            blocked.Add(c.Neighbor(dir));
    }

    /// <summary>
    /// 收集出生点"最近 nearbyCount 个 Chunk"候选：
    /// 优先严格四邻接（东/南/西/北，即最近的 4 个 Chunk），若被边界裁剪不足 nearbyCount，
    /// 再按切比雪夫距离逐圈外扩补足。返回确定性排序列表（同种子下稳定）。
    /// </summary>
    static List<ChunkCoord> CollectNearby(ChunkCoord spawn, ChunkCoord min, ChunkCoord max, int nearbyCount)
    {
        var list = new List<ChunkCoord>();
        var seen = new HashSet<ChunkCoord>();

        // ① 严格四邻接优先（需求：出生点最近的四个 chunk）
        foreach (var dir in ChunkCoord.AllDirections)
        {
            var c = spawn.Neighbor(dir);
            if (c.x < min.x || c.x > max.x || c.y < min.y || c.y > max.y) continue;
            if (!seen.Add(c)) continue;
            list.Add(c);
            if (list.Count >= nearbyCount) return list;
        }

        // ② 不足时按切比雪夫距离逐圈外扩补足（含对角），直到凑满或耗尽边界
        for (int ring = 2; ring <= 64 && list.Count < nearbyCount; ring++)
        {
            var ringCoords = new List<ChunkCoord>();
            for (int dx = -ring; dx <= ring; dx++)
            for (int dy = -ring; dy <= ring; dy++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue; // 只取本圈
                var c = new ChunkCoord(spawn.x + dx, spawn.y + dy);
                if (c.x < min.x || c.x > max.x || c.y < min.y || c.y > max.y) continue;
                if (!seen.Add(c)) continue;
                ringCoords.Add(c);
            }
            ringCoords.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            foreach (var c in ringCoords)
            {
                list.Add(c);
                if (list.Count >= nearbyCount) break;
            }
        }
        return list;
    }

    /// <summary>选一个与 total 互质的步长（保证环状遍历能覆盖全部格）。</summary>
    static int PickStep(System.Random rng, int total)
    {
        // 优先取一个与 total 互质的奇数步长；简单起见取 total/2 附近向上/下找互质，找不到用 1
        for (int s = Mathf.Max(1, total / 2); s >= 1; s--)
        {
            if (Gcd(s, total) == 1) return s;
        }
        return 1;
    }

    static int Gcd(int a, int b)
    {
        while (b != 0) { int t = a % b; a = b; b = t; }
        return a;
    }
}
