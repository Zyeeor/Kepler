/// <summary>
/// 运行时 Tile 数据：Chunk 内一格（局部坐标 + 视觉 prefab 引用 + 通行性快照 + 地形类型快照）。
/// 双层模型（对齐 MapStreaming_Design §2.2 BaseLayer + StructureLayer）：
///   · 底层（prefab / kind / isWalkable）——恒为非装饰地砖（Normal 或 Trigger），isWalkable 为合并后逻辑可走性；
///   · 叠加层（overlayPrefab / overlayKind / overlayWalkable）——可选 StructureLayer（柱/树/装饰/神龛），立在底层之上，不替换底层。
/// isWalkable / kind 为生成期快照，查询时不必追引用（prefab 后续被改配置不影响已生成 Chunk）。
/// 玩法语义经 TileSemantics 从 prefab 自带组件推导。
/// </summary>
public struct TileData
{
    /// <summary>Chunk 内局部坐标 x（[0, chunkSize-1]）。</summary>
    public int localX;
    /// <summary>Chunk 内局部坐标 y（[0, chunkSize-1]，对应世界 z 方向）。</summary>
    public int localY;
    /// <summary>底层地砖 prefab（恒非装饰；可为 null：Normal 由 ChunkVisualizer 默认地面板兜底）。</summary>
    public UnityEngine.GameObject prefab;
    /// <summary>合并后逻辑通行性快照（底层可走 && 叠加物无 solid Collider，对齐 MapStreaming_Design §10.1）。</summary>
    public bool isWalkable;
    /// <summary>底层地形类型快照（Normal / Trigger）。</summary>
    public TerrainKind kind;

    /// <summary>叠加层 prefab（StructureLayer：装饰物/神龛等），可空（无叠加）。</summary>
    public UnityEngine.GameObject overlayPrefab;
    /// <summary>叠加层地形类型快照（Decoration 等），无叠加时为 Normal。</summary>
    public TerrainKind overlayKind;
    /// <summary>叠加物自身逻辑可走性（= 无 solid Collider），不含底层。</summary>
    public bool overlayWalkable;

    public TileData(int localX, int localY, UnityEngine.GameObject prefab, bool isWalkable, TerrainKind kind,
                    UnityEngine.GameObject overlayPrefab = null, TerrainKind overlayKind = TerrainKind.Normal, bool overlayWalkable = true)
    {
        this.localX = localX;
        this.localY = localY;
        this.prefab = prefab;
        this.isWalkable = isWalkable;
        this.kind = kind;
        this.overlayPrefab = overlayPrefab;
        this.overlayKind = overlayKind;
        this.overlayWalkable = overlayWalkable;
    }

    public override string ToString()
    {
        var s = $"Tile({localX}, {localY}) base={kind} walkable={isWalkable} prefab={(prefab != null ? prefab.name : "null")}";
        if (overlayPrefab != null)
            s += $" overlay={overlayPrefab.name}({overlayKind},walkable={overlayWalkable})";
        return s;
    }
}
