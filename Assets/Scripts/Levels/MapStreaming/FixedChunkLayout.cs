using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chunk 固定布局资产（Fixed 模式用）：策划手摆的 N×N 网格，支持双层——
/// 底层地砖 tiles[] + 可选叠加装饰物 overlayTiles[]（叠加在底层之上，对齐双层 Tile 模型）。
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

    [Tooltip("底层地砖网格（索引 y*size+x）。每格一个 Tile prefab 引用（Normal/Trigger 地砖）；空 = 可走普通地面。")]
    public GameObject[] tiles = new GameObject[8 * 8];

    [Tooltip("叠加层网格（可选，与 tiles 同索引）。每格一个 StructureLayer prefab（装饰物/神龛），叠加在底层地砖之上。空 = 无叠加。旧布局无此层时留空（自动兼容）。")]
    public GameObject[] overlayTiles;

    [Tooltip("多格装饰物实例列表。每条只生成一个 prefab，并占用 anchor 起始的 footprintSize 矩形区域。旧的 overlayTiles 仍兼容为单格实例。")]
    public List<DecorationPlacement> decorationPlacements = new List<DecorationPlacement>();

    [Tooltip("默认底层地砖：当某格只有叠加装饰物（旧格式把装饰物整格摆在 tiles 字段，或 overlayTiles 有值而 tiles 为空）时，底层用此地砖兜底。留空则回退生成所用 ChunkDef.normalTiles[0]。")]
    public GameObject defaultGround;

    /// <summary>读格（越界返回 null）。</summary>
    public GameObject GetTile(int x, int y)
    {
        if (x < 0 || y < 0 || x >= size || y >= size) return null;
        if (tiles == null || tiles.Length != size * size) return null;
        return tiles[y * size + x];
    }

    /// <summary>写底层格（越界忽略；编辑器工具走 Undo.RecordObject + SetDirty）。</summary>
    public void SetTile(int x, int y, GameObject prefab)
    {
        if (x < 0 || y < 0 || x >= size || y >= size) return;
        EnsureCapacity();
        tiles[y * size + x] = prefab;
    }

    /// <summary>读叠加格（越界返回 null）。</summary>
    public GameObject GetOverlay(int x, int y)
    {
        if (x < 0 || y < 0 || x >= size || y >= size) return null;
        if (overlayTiles == null || overlayTiles.Length != size * size) return null;
        return overlayTiles[y * size + x];
    }

    /// <summary>写叠加格（越界忽略；编辑器工具走 Undo.RecordObject + SetDirty）。</summary>
    public void SetOverlay(int x, int y, GameObject prefab)
    {
        if (x < 0 || y < 0 || x >= size || y >= size) return;
        EnsureCapacity();
        overlayTiles[y * size + x] = prefab;
    }

    /// <summary>容量防御：tiles / overlayTiles 长度 ≠ size² 时重建，保留重合区数据（尺寸变更迁移）。</summary>
    public void EnsureCapacity()
    {
        if (tiles != null && tiles.Length == size * size)
        {
            // tiles 容量正常：仍确保 overlayTiles 同步（旧 SO 无此字段时为 null → 初始化空网格）
        }
        else
        {
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

        if (overlayTiles == null || overlayTiles.Length != size * size)
        {
            var fixedOverlay = new GameObject[size * size];
            if (overlayTiles != null)
            {
                int oldSize = Mathf.CeilToInt(Mathf.Sqrt(overlayTiles.Length));
                for (int y = 0; y < Mathf.Min(oldSize, size); y++)
                for (int x = 0; x < Mathf.Min(oldSize, size); x++)
                    fixedOverlay[y * size + x] = overlayTiles[y * oldSize + x];
            }
            overlayTiles = fixedOverlay;
        }
    }

#if UNITY_EDITOR
    /// <summary>配置防御：Inspector 中数组长度被改坏或 size 变化时自动修正为 size²。</summary>
    void OnValidate()
    {
        EnsureCapacity();
        if (decorationPlacements == null) return;
        foreach (var placement in decorationPlacements)
        {
            if (placement == null) continue;
            placement.footprintSize = new Vector2Int(Mathf.Max(1, placement.footprintSize.x), Mathf.Max(1, placement.footprintSize.y));
        }
    }
#endif
}
