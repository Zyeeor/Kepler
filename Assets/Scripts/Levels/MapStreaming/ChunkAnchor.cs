using System;
using UnityEngine;

/// <summary>
/// 固定 Chunk 锚点：指定某个 Chunk 坐标固定生成指定模板，不受世界种子/WorldPlan 影响。
/// 两种模式（二选一）：
///   1. layout 非空 → 整块 Chunk 完全按该手摆布局生成（逐格 prefab，完全固定）；
///   2. layout 空且 chunkDef 非空 → 用该 ChunkDef 的程序生成 + fixedSeed 固定种子
///      （同锚点配置下每次生成内容一致；不参与全局 ChunkTemplateAllocator 约束）。
/// 两者皆空：该锚点无效（回退正常随机生成 + Warning）。
/// 配置位置：MapStreamingSystem 组件「固定 Chunk 锚点」列表（Inspector 可编辑）。
/// </summary>
[Serializable]
public class ChunkAnchor
{
    [Tooltip("固定位置（Chunk 坐标，世界换算经 MapStreamingSystem）。")]
    public ChunkCoord coord;

    [Tooltip("固定手摆布局（优先）：非空则整块 Chunk 完全按此布局逐格生成。")]
    public FixedChunkLayout layout;

    [Tooltip("固定程序模板（layout 为空时生效）：该 ChunkDef 的程序生成（池/图案权重）。")]
    public ChunkDef chunkDef;

    [Tooltip("固定生成种子：锚点 Chunk 内部随机细节的种子。改此值可切换固定内容变体；与世界种子无关。")]
    public uint fixedSeed = 0;

    /// <summary>锚点是否有效（至少有 layout 或 chunkDef）。</summary>
    public bool IsValid => layout != null || chunkDef != null;
}
