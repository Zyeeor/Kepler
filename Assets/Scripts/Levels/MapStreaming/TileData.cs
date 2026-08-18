/// <summary>
/// 运行时 Tile 数据：Chunk 内一格（局部坐标 + 视觉 prefab 引用 + 通行性快照 + 地形类型快照）。
/// isWalkable / kind 为生成期快照，查询时不必追引用（prefab/TileVisual 后续被改配置不影响已生成 Chunk）。
/// 玩法语义经 <see cref="Visual"/> 从 prefab 上的 TileVisual 组件读取。
/// </summary>
public struct TileData
{
    /// <summary>Chunk 内局部坐标 x（[0, chunkSize-1]）。</summary>
    public int localX;
    /// <summary>Chunk 内局部坐标 y（[0, chunkSize-1]，对应世界 z 方向）。</summary>
    public int localY;
    /// <summary>视觉 prefab 引用（可为 null：Normal 由 ChunkVisualizer 默认地面板兜底）。</summary>
    public UnityEngine.GameObject prefab;
    /// <summary>通行性快照（生成期确定：Blocker 恒 false，其余以 TileVisual.isWalkable 为准）。</summary>
    public bool isWalkable;
    /// <summary>地形类型快照（生成期按 ChunkDef 池类别分配）。</summary>
    public TerrainKind kind;

    public TileData(int localX, int localY, UnityEngine.GameObject prefab, bool isWalkable, TerrainKind kind)
    {
        this.localX = localX;
        this.localY = localY;
        this.prefab = prefab;
        this.isWalkable = isWalkable;
        this.kind = kind;
    }

    /// <summary>该格 prefab 上的 TileVisual 组件（null = 未挂组件或 prefab 为空）。</summary>
    public TileVisual Visual => prefab != null ? prefab.GetComponent<TileVisual>() : null;

    public override string ToString()
    {
        return $"Tile({localX}, {localY}) kind={kind} walkable={isWalkable} prefab={(prefab != null ? prefab.name : "null")}";
    }
}
