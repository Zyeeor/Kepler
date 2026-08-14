using System.Collections.Generic;

/// <summary>
/// Pin 注册表：被 Pin 的 Chunk 离开 D 不卸载，保持加载等解 Pin。
///
/// 来源计数制：同来源多次 Pin 只计一次（幂等）；该 Chunk 全部来源解除后才回到可卸载。
/// 动态来源（玩家所在 Chunk / 附身目标所在 Chunk）由 MapStreamingSystem 每 Tick 经 SetDynamicPin 刷新；
/// Boss 事件来源预留 PinBoss / UnpinBoss（TODO Phase 5 接入，）。
/// 防泄漏：附身结束 PossessionManager.CurrentBody 置空，下一 Tick 动态 Pin 自动解除；
/// 被 Pin Chunk 由 MapStreamingSystem Gizmos 紫色线框可视化。
/// </summary>
public class PinRegistry
{
    /// <summary>来源 id：玩家所在 Chunk（正常不会离开 D，防御性兜底）。</summary>
    public const string SourcePlayer = "Player";
    /// <summary>来源 id：附身目标（PossessionManager.CurrentBody）所在 Chunk——身体跨 Chunk 移动时原 Chunk 不得卸载。</summary>
    public const string SourcePossession = "Possession";
    /// <summary>来源 id：Boss 战进行中的 Boss Chunk（Phase 5 接入）。</summary>
    public const string SourceBoss = "Boss";

    readonly Dictionary<ChunkCoord, HashSet<string>> sourcesByChunk = new Dictionary<ChunkCoord, HashSet<string>>();
    readonly Dictionary<string, ChunkCoord> dynamicPinBySource = new Dictionary<string, ChunkCoord>();

    /// <summary>当前被 Pin 的 Chunk 数（调试 HUD 用）。</summary>
    public int PinnedChunkCount => sourcesByChunk.Count;

    /// <summary>当前全部被 Pin 的 Chunk（MapStreamingSystem 解 Pin 对比 / Gizmos 可视化用）。</summary>
    public IEnumerable<ChunkCoord> PinnedCoords => sourcesByChunk.Keys;

    /// <summary>该 Chunk 是否被任一来源 Pin。</summary>
    public bool IsPinned(ChunkCoord coord)
    {
        return sourcesByChunk.TryGetValue(coord, out var sources) && sources.Count > 0;
    }

    /// <summary>登记来源 Pin（幂等：同来源重复 Pin 只计一次）。</summary>
    public void Pin(ChunkCoord coord, string source)
    {
        if (!sourcesByChunk.TryGetValue(coord, out var sources))
        {
            sources = new HashSet<string>();
            sourcesByChunk.Add(coord, sources);
        }
        sources.Add(source);
    }

    /// <summary>解除来源 Pin；该 Chunk 来源清空后从注册表移除（回到可卸载）。</summary>
    public void Unpin(ChunkCoord coord, string source)
    {
        if (!sourcesByChunk.TryGetValue(coord, out var sources)) return;
        sources.Remove(source);
        if (sources.Count == 0) sourcesByChunk.Remove(coord);
    }

    /// <summary>
    /// 动态来源刷新：同一来源同一时刻只 Pin 一个 Chunk；coord 传 null 表示该来源当前无 Pin。
    /// 来源目标不变时直通（零开销）。
    /// </summary>
    public void SetDynamicPin(string source, ChunkCoord? coord)
    {
        bool hasOld = dynamicPinBySource.TryGetValue(source, out var old);
        if (hasOld && coord.HasValue && old == coord.Value) return;
        if (hasOld)
        {
            Unpin(old, source);
            dynamicPinBySource.Remove(source);
        }
        if (coord.HasValue)
        {
            Pin(coord.Value, source);
            dynamicPinBySource.Add(source, coord.Value);
        }
    }

    /// <summary>Boss 事件 Pin。</summary>
    public void PinBoss(ChunkCoord coord)
    {
        Pin(coord, SourceBoss);
    }

    /// <summary>解除 Boss 事件 Pin（Boss 战结束必须调用，防泄漏，）。</summary>
    public void UnpinBoss(ChunkCoord coord)
    {
        Unpin(coord, SourceBoss);
    }
}
