using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 解析式宏观区域图。
/// 任意 (coord, worldSeed) 直接解析出 RegionDef / ChunkDef，与生成顺序无关：
///   RegionOf   —— 低分辨率格点 hash 噪声 + 双线性插值 → 连续 theme 值 → 按 themeCenter 最近邻查表（纯函数）；
///   ChunkDefOf —— region.chunkPool 加权抽取，已生成邻居的 preferredNeighbors 提供权重加成（锦上添花，缺失不阻塞）。
/// 确定性铁律：随机源仅 System.Random(Hash(coord, seed))，噪声格点 hash 纯整数运算，无时间依赖。
/// </summary>
public class WorldPlan
{
    readonly uint seed;
    /// <summary>主题映射表：按 themeCenter 升序（构造时排序），噪声值最近邻查表。</summary>
    readonly List<RegionDef> themeTable;
    /// <summary>出生点主题（themeTable 中 type == Normal 的第一个）；null 表示不强制出生点。</summary>
    readonly RegionDef normalRegion;
    /// <summary>区域粒度：每个噪声采样点覆盖的 Chunk 边长（建议 ≥4；2 在连续噪声下同主题邻接率仅 ~50%，棋盘格化）。</summary>
    readonly int regionCellSize;

    /// <summary>邻接权重加成：候选模板的 preferredNeighbors 每命中一个已生成邻居 Def，权重 += 本值。</summary>
    public float neighborWeightBonus = 2f;

    /// <summary>regions 内 null 元素会被过滤；为空/全 null 时 RegionOf 恒 null（调用方走 defaultChunkDef 兜底）。</summary>
    public WorldPlan(uint seed, IReadOnlyList<RegionDef> regions, int regionCellSize)
    {
        this.seed = seed;
        this.regionCellSize = Mathf.Max(1, regionCellSize);
        themeTable = new List<RegionDef>();
        if (regions != null)
        {
            for (int i = 0; i < regions.Count; i++)
                if (regions[i] != null) themeTable.Add(regions[i]);
            themeTable.Sort((a, b) => a.themeCenter.CompareTo(b.themeCenter));
        }
        for (int i = 0; i < themeTable.Count; i++)
            if (themeTable[i].type == RegionType.Normal) { normalRegion = themeTable[i]; break; }
    }

    /// <summary>
    /// 区域解析（纯函数）：连续噪声值 → themeCenter 最近邻 RegionDef。
    /// 出生点强制 Normal：曼哈顿距离 |x|+|y| ≤ regionCellSize 时直接返回 normalRegion。
    /// </summary>
    public RegionDef RegionOf(ChunkCoord c)
    {
        if (themeTable.Count == 0) return null;
        if (normalRegion != null && Mathf.Abs(c.x) + Mathf.Abs(c.y) <= regionCellSize)
            return normalRegion;
        float t = ContinuousNoise((float)c.x / regionCellSize, (float)c.y / regionCellSize, seed);
        return themeTable[ThemeIndexOf(t)];
    }

    /// <summary>
    /// ChunkDef 选择：region.chunkPool 加权抽取（基础权重 1）；
    /// 每个已生成邻居的 Def 命中候选 preferredNeighbors → 权重 += neighborWeightBonus。
    /// 邻居未生成跳过（顺序无关容错）；region 无池 / 权重和 ≤0 → 返回 null（调用方走 defaultChunkDef）。
    /// </summary>
    public ChunkDef ChunkDefOf(ChunkCoord c, IReadOnlyDictionary<ChunkCoord, ChunkRuntime> registry)
    {
        var region = RegionOf(c);
        if (region == null || region.chunkPool == null || region.chunkPool.Count == 0)
            return null;

        // 已生成邻居的 Def（≤4；未生成/无 Def 的跳过——顺序无关容错，与  "Unknown 按潜在可通行计" 同款）
        List<ChunkDef> neighborDefs = null;
        if (registry != null)
        {
            foreach (var dir in ChunkCoord.AllDirections)
            {
                if (registry.TryGetValue(c.Neighbor(dir), out var nb) && nb != null && nb.Def != null)
                {
                    if (neighborDefs == null) neighborDefs = new List<ChunkDef>(4);
                    neighborDefs.Add(nb.Def);
                }
            }
        }

        var pool = region.chunkPool;
        var weights = new float[pool.Count];
        float total = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            var cand = pool[i];
            if (cand == null) continue;
            float w = 1f;
            if (neighborDefs != null && cand.preferredNeighbors != null)
                for (int n = 0; n < neighborDefs.Count; n++)
                    if (cand.preferredNeighbors.Contains(neighborDefs[n]))
                        w += neighborWeightBonus;
            weights[i] = w;
            total += w;
        }
        if (total <= 0f) return null;

        // 加权抽取：随机源仅此处，roll 一次 + 固定扫描顺序 → 确定性
        var rng = new System.Random((int)HashCoord(c, seed));
        float roll = (float)rng.NextDouble() * total;
        ChunkDef last = null;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] == null) continue;
            last = pool[i];
            roll -= weights[i];
            if (roll <= 0f) return pool[i];
        }
        return last; // 浮点误差兜底
    }

    /// <summary>连续 theme 值 [0,1)：格点 hash + 双线性插值 + smoothstep。</summary>
    static float ContinuousNoise(float fx, float fy, uint seed)
    {
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        // smoothstep t*t*(3-2t)：格点处一阶连续，跨格无折痕
        float sx = tx * tx * (3f - 2f * tx);
        float sy = ty * ty * (3f - 2f * ty);
        float v00 = HashInt(x0, y0, seed);     float v10 = HashInt(x0 + 1, y0, seed);
        float v01 = HashInt(x0, y0 + 1, seed); float v11 = HashInt(x0 + 1, y0 + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(v00, v10, sx), Mathf.Lerp(v01, v11, sx), sy);
    }

    /// <summary>theme 值 → themeTable 索引：themeCenter 最近邻（MC 参数空间最近邻同款，）。</summary>
    int ThemeIndexOf(float t)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < themeTable.Count; i++)
        {
            float d = Mathf.Abs(t - themeTable[i].themeCenter);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    /// <summary>格点 hash → [0,1)：纯整数混合（负坐标经补码强转确定性一致），与 ChunkSeed 同族。</summary>
    static float HashInt(int x, int y, uint seed)
    {
        unchecked
        {
            uint h = seed ^ (uint)(x * 73856093) ^ (uint)(y * 19349663);
            h *= 2654435761u; // Knuth 乘法扩散
            h ^= h >> 16;     // 高低位折叠
            return (h & 0xFFFFFF) * (1f / 16777216f);
        }
    }

    /// <summary>Chunk 抽取种子：与 MapStreamingSystem.ChunkSeed(coord, 0) 同构。</summary>
    static uint HashCoord(ChunkCoord c, uint seed)
    {
        unchecked
        {
            return seed ^ (uint)(c.x * 73856093) ^ (uint)(c.y * 19349663);
        }
    }
}
