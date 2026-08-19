using UnityEngine;

/// <summary>
/// Chunk 固定布局资产（Fixed 模式用）：策划手摆的 N×N 网格，每格直接存 prefab 引用——
/// 玩法语义（可行走/地形类别等）由 TileSemantics 从 prefab 自带组件推导，不重复存储。
/// 空格（null）生成时视为可走普通地面。
/// 索引约定：tiles[y * size + x]，(x, y) 为 Chunk 内局部坐标（y 对应世界 z 方向）。
/// 手摆工具见 Editor/ChunkLayoutEditorWindow（菜单 Kepler/Map/Chunk Layout Editor）。
/// 注意：Fixed 布局的 Chunk 间边沿连通由策划负责，开放边不足时生成会告警并可能被安全兜底替换。
/// 布局边长应与 MapStreamingSystem.chunkSize 一致（不一致时生成器告警，越界格按空处理）。
/// </summary>
[CreateAssetMenu(fileName = "FixedChunkLayout", menuName = "Kepler/Map/Fixed Chunk Layout")]
public class FixedChunkLayout : ScriptableObject
{
    /// <summary>布局边长（默认 8×8；与 MapStreamingSystem.chunkSize 不一致时生成器告警）。</summary>
    [Tooltip("布局边长（Tile 数）。修改后按新尺寸重建网格，旧数据保留在重合区。")]
    [Min(1)]
    public int size = 8;

    [Tooltip("N×N 网格（索引 y*size+x）。每格一个 Tile prefab 引用；空 = 可走普通地面。")]
    public GameObject[] tiles = new GameObject[8 * 8];

    /// <summary>读格（越界返回 null）。</summary>
    public GameObject GetTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= size || y >= size) return null;
        if (tiles == null || tiles.Length != size * size) return null;
        return tiles[y * size + x];
    }

    /// <summary>写格（越界忽略；编辑器工具走 Undo.RecordObject + SetDirty）。</summary>
    public void SetTile(int x, int y, GameObject prefab)
    {
        if (x < 0 || y < 0 || x >= size || y >= size) return;
        EnsureCapacity();
        tiles[y * size + x] = prefab;
    }

    /// <summary>容量防御：数组长度 ≠ size² 时重建，保留重合区数据（尺寸变更迁移）。</summary>
    public void EnsureCapacity()
    {
        if (tiles != null && tiles.Length == size * size) return;
        var fixed_ = new GameObject[size * size];
        if (tiles != null)
        {
            int oldSize = Mathf.CeilToInt(Mathf.Sqrt(tiles.Length));
            for (int y = 0; y < Mathf.Min(oldSize, size); y++)
            for (int x = 0; x < Mathf.Min(oldSize, size); x++)
                fixed_[y * size + x] = tiles[y * oldSize + x];
        }
        tiles = fixed_;
    }

#if UNITY_EDITOR
    /// <summary>配置防御：Inspector 中数组长度被改坏或 size 变化时自动修正为 size²。</summary>
    void OnValidate()
    {
        EnsureCapacity();
    }
#endif
}
