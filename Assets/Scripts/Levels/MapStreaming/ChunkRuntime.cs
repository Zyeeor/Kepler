using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chunk 流送状态机枚举。
/// 命名说明：持久化快照类 ChunkState 已占用同名，故枚举用 ChunkStreamState。
/// </summary>
public enum ChunkStreamState
{
    /// <summary>不存在（未生成逻辑数据）。</summary>
    None = 0,
    /// <summary>逻辑数据（TileData + 出入口校验）就绪，资源异步预取中。</summary>
    Prepared = 1,
    /// <summary>场景对象已实例化，AI 待机（动画/粒子/音效暂停）。</summary>
    Dormant = 2,
    /// <summary>完整模拟（AI/事件/战斗）。</summary>
    Active = 3,
    /// <summary>已回收，快照已保存（内存）。</summary>
    Unloaded = 4,
}

/// <summary>
/// Chunk 运行时实例：流送/状态机/刷怪的核心单元。
/// 纯 C# 对象（非 MonoBehaviour）：场景对象生命周期由 MapStreamingSystem 的任务队列与视觉层管理。
/// 状态机转换合法性由 TransitionTo 校验，非法转换拒绝并告警。
/// </summary>
public class ChunkRuntime
{
    /// <summary>全局 Chunk 坐标。</summary>
    public ChunkCoord Coord { get; private set; }
    /// <summary>生成种子（由坐标 + 世界种子派生，生成顺序无关）。</summary>
    public uint Seed { get; private set; }
    /// <summary>模板（含刷怪表），由 MapStreamingSystem.defaultChunkDef 兜底。</summary>
    public ChunkDef Def { get; private set; }
    /// <summary>完整 Tile 网格 [chunkSize, chunkSize]，Prepared 阶段生成。</summary>
    public TileData[,] Tiles { get; private set; }
    /// <summary>可通行的邻接边（自身边沿有开口的方向）。</summary>
    public List<ChunkDirection> OpenEdges { get; private set; } = new List<ChunkDirection>();
    /// <summary>状态机当前位置。</summary>
    public ChunkStreamState State { get; private set; } = ChunkStreamState.None;

    /// <summary>事件：状态切换（oldState, newState）。</summary>
    public event Action<ChunkRuntime, ChunkStreamState, ChunkStreamState> OnStateChanged;

    public ChunkRuntime(ChunkCoord coord, ChunkDef def, uint seed)
    {
        Coord = coord;
        Def = def;
        Seed = seed;
    }

    /// <summary>写入生成完成的 Tile 网格与出入口。</summary>
    public void SetTiles(TileData[,] tiles, List<ChunkDirection> openEdges)
    {
        Tiles = tiles;
        OpenEdges = openEdges ?? new List<ChunkDirection>();
    }

    /// <summary>读取边沿第 i 个 Tile（出入口配对校验用）。</summary>
    public TileData GetEdgeTile(ChunkDirection dir, int i)
    {
        int max = Tiles.GetLength(0) - 1;
        switch (dir)
        {
            case ChunkDirection.East: return Tiles[max, i];
            case ChunkDirection.West: return Tiles[0, i];
            case ChunkDirection.North: return Tiles[i, max];
            case ChunkDirection.South: return Tiles[i, 0];
            default: return Tiles[0, 0];
        }
    }

    /// <summary>
    /// 状态转换。非法转换拒绝并告警，返回 false。
    /// 幂等：目标 == 当前状态时直接返回 true，不重复触发事件。
    /// </summary>
    public bool TransitionTo(ChunkStreamState target)
    {
        if (target == State) return true;
        if (!IsTransitionAllowed(State, target))
        {
            Debug.LogWarning($"[ChunkRuntime] {Coord} 非法状态转换 {State} → {target}，已拒绝。");
            return false;
        }
        var old = State;
        State = target;
        OnStateChanged?.Invoke(this, old, target);
        return true;
    }

    /// <summary>转换合法性表。</summary>
    static bool IsTransitionAllowed(ChunkStreamState from, ChunkStreamState to)
    {
        switch (from)
        {
            case ChunkStreamState.None:
                return to == ChunkStreamState.Prepared;
            case ChunkStreamState.Prepared:
                return to == ChunkStreamState.Dormant || to == ChunkStreamState.Unloaded;
            case ChunkStreamState.Dormant:
                return to == ChunkStreamState.Active || to == ChunkStreamState.Prepared || to == ChunkStreamState.Unloaded;
            case ChunkStreamState.Active:
                return to == ChunkStreamState.Dormant;
            case ChunkStreamState.Unloaded:
                // 重新进入：从 Unloaded 再 Prepare（快照恢复在实例化时做）
                return to == ChunkStreamState.Prepared;
            default:
                return false;
        }
    }
}
