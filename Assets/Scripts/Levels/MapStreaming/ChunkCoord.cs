using System;
using UnityEngine;

/// <summary>
/// Chunk 四邻接方向（东/南/西/北）。用于出入口校验与邻接偏移。
/// </summary>
public enum ChunkDirection
{
    East = 0,
    South = 1,
    West = 2,
    North = 3,
}

/// <summary>
/// 全局 Chunk 坐标（默认 16×16 Tile 为一个 Chunk，见 MapStreamingSystem.chunkSize）。
/// 纯值类型：可做 Dictionary key；不持有 chunkSize/tileSize 配置，
/// 世界坐标换算统一走 MapStreamingSystem（WorldToChunk / ChunkToWorldOrigin）。
/// </summary>
[Serializable]
public struct ChunkCoord : IEquatable<ChunkCoord>
{
    public int x;
    public int y;

    public ChunkCoord(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    public static readonly ChunkCoord Zero = new ChunkCoord(0, 0);

    /// <summary>四方向数组，校验出入口时遍历用。</summary>
    public static readonly ChunkDirection[] AllDirections =
    {
        ChunkDirection.East, ChunkDirection.South, ChunkDirection.West, ChunkDirection.North,
    };

    /// <summary>某方向上的邻接 Chunk 坐标。</summary>
    public ChunkCoord Neighbor(ChunkDirection dir)
    {
        return this + Offset(dir);
    }

    /// <summary>方向对应的单位偏移（x/z 平面，y 即世界 z 方向）。</summary>
    public static Vector2Int Offset(ChunkDirection dir)
    {
        switch (dir)
        {
            case ChunkDirection.East: return new Vector2Int(1, 0);
            case ChunkDirection.West: return new Vector2Int(-1, 0);
            case ChunkDirection.North: return new Vector2Int(0, 1);
            case ChunkDirection.South: return new Vector2Int(0, -1);
            default: return Vector2Int.zero;
        }
    }

    /// <summary>反方向。</summary>
    public static ChunkDirection Opposite(ChunkDirection dir)
    {
        return (ChunkDirection)(((int)dir + 2) % 4);
    }

    public bool Equals(ChunkCoord other)
    {
        return x == other.x && y == other.y;
    }

    public override bool Equals(object obj)
    {
        return obj is ChunkCoord other && Equals(other);
    }

    public override int GetHashCode()
    {
        // 与 ChunkRuntime 种子散列同源的廉价混合，足够做 key
        unchecked
        {
            return (x * 73856093) ^ (y * 19349663);
        }
    }

    public static bool operator ==(ChunkCoord a, ChunkCoord b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(ChunkCoord a, ChunkCoord b)
    {
        return !a.Equals(b);
    }

    public static ChunkCoord operator +(ChunkCoord a, Vector2Int b)
    {
        return new ChunkCoord(a.x + b.x, a.y + b.y);
    }

    public static ChunkCoord operator -(ChunkCoord a, Vector2Int b)
    {
        return new ChunkCoord(a.x - b.x, a.y - b.y);
    }

    public override string ToString()
    {
        return $"({x}, {y})";
    }
}
