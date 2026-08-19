/// <summary>
/// 运行时 Tile 数据：Chunk 内一格（局部坐标 + 视觉 prefab 引用 + 通行性快照 + 地形类型快照）。
/// isWalkable / kind 为生成期快照，查询时不必追引用（prefab 后续被改配置不影响已生成 Chunk）。
/// 玩法语义经 TileSemantics 从 prefab 自带组件推导。
/// </summary>
public struct TileData
{
    /// <summary>Chunk 内局部坐标 x（[0, chunkSize-1]）。</summary>
    public int localX;
    /// <summary>Chunk 内局部坐标 y（[0, chunkSize-1]，对应世界 z 方向）。</summary>
    public int localY;
    /// <summary>视觉 prefab 引用（可为 null：Normal 由 ChunkVisualizer 默认地面板兜底）。</summary>
    public UnityEngine.GameObject prefab;
    /// <summary>逻辑通行性快照（生成期确定：prefab 无 solid Collider 即为可通行——刷怪选取/开放边判定用；物理阻挡由模型 Collider 精确决定）。</summary>
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

    public override string ToString()
    {
        return $"Tile({localX}, {localY}) kind={kind} walkable={isWalkable} prefab={(prefab != null ? prefab.name : "null")}";
    }
}
