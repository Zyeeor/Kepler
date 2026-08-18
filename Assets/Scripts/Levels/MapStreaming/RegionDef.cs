using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Region 类型：对齐"主路径、分支、守卫、Boss"的玩法分区。
/// 这是"玩家路径必须经过/可达"的 Chunk 集合标签，不是位置。
/// </summary>
public enum RegionType
{
    /// <summary>普通区：玩家出生与主要探索路径。</summary>
    Normal = 0,
    /// <summary>分支：玩家可达的支线（奖励/可选战斗）。</summary>
    Branch = 1,
    /// <summary>守卫：必经之路的强制战斗节点。</summary>
    Guard = 2,
    /// <summary>Boss：终局目标区域（Boss 战期间强制保持活跃）。</summary>
    Boss = 3,
}

/// <summary>
/// Region 配置（SO）：描述普通/分支/守卫/Boss 区域的用途与主题。
/// 预留：宏观区域（WorldPlan）排布尚未接入，当前字段未被运行时引用。
/// </summary>
[CreateAssetMenu(fileName = "RegionDef", menuName = "Kepler/Map/Region Def")]
public class RegionDef : ScriptableObject
{
    [Tooltip("配置唯一 id（WorldPlan 引用用）。")]
    public string id;

    [Tooltip("区域用途类型。")]
    public RegionType type = RegionType.Normal;

    [Header("主题（占位）")]
    [Tooltip("主题/风格标识（美术与氛围配置占位）。")]
    public string theme;

    [Tooltip("主题映射中心值（0~1，WorldPlan 阈值查表用）：噪声值与各 RegionDef 本值最近邻决定归属。")]
    [Range(0f, 1f)] public float themeCenter = 0.5f;

    [Header("内容池")]
    [Tooltip("该区域可使用的 Chunk 模板池（WorldPlan 排布时抽取）。")]
    public List<ChunkDef> chunkPool = new List<ChunkDef>();
}
