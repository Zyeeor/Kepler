using System;
using UnityEngine;

/// <summary>
/// 一个装饰物实例在 Chunk 内的占位记录。
/// anchor 是 footprint 的左下角格；footprintSize 是占用的矩形格数。
/// 运行时 id 用于让多个 TileData 共享同一个装饰实例，视觉层只 Instantiate 一次。
/// </summary>
[Serializable]
public class DecorationPlacement
{
    [HideInInspector] public int id;
    public GameObject prefab;
    public Vector2Int anchor;
    public Vector2Int footprintSize = Vector2Int.one;

    public Vector2Int SafeFootprintSize => new Vector2Int(
        Mathf.Max(1, footprintSize.x),
        Mathf.Max(1, footprintSize.y));

    public int CellCount => SafeFootprintSize.x * SafeFootprintSize.y;

    public bool Contains(int x, int y)
    {
        var size = SafeFootprintSize;
        return x >= anchor.x && x < anchor.x + size.x
            && y >= anchor.y && y < anchor.y + size.y;
    }
}
