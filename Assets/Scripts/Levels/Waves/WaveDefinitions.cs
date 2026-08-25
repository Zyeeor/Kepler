using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 波次模式：
/// CountKill = 数量波：刷满 totalCount 只后不再补，玩家清完本波触发选卡；
/// Timed = 时间波：持续 duration 秒，时间到即结算并触发选卡。
/// </summary>
public enum WaveMode
{
    CountKill,
    Timed,
}

/// <summary>
/// 波内编队条目：引用一个 MonsterWaveDef，并配置其在本波内的抽取权重。
/// </summary>
[Serializable]
public class WaveDefEntry
{
    [Tooltip("怪物编队（MonsterWaveDef 资产：本组刷哪些怪、组内数量）。")]
    public MonsterWaveDef def;

    [Tooltip("本波内该编队的抽取权重（占比）：值越大越常出。仅本波生效。")]
    [Min(0f)] public float weight = 1f;
}

/// <summary>
/// 单个波次的正式运行配置。由 WaveManager.waves 持有，不依赖旧房间系统。
/// </summary>
[Serializable]
public class WaveConfig
{
    [Tooltip("怪物编队表：本波按条目 weight 抽取刷怪（每波独立占比）。")]
    public List<WaveDefEntry> weightedTable = new List<WaveDefEntry>();

    [Tooltip("数量波：本波刷怪总数。仅 CountKill 模式生效。")]
    [Min(1)] public int totalCount = 20;

    [Tooltip("时间波：本波时长（秒）。仅 Timed 模式生效。")]
    [Min(1f)] public float duration = 60f;

    [Tooltip("时间波：本波累计刷怪总数上限。0 = 不限制。仅 Timed 模式生效。")]
    [Min(0)] public int maxSpawnCount = 0;

    [Tooltip("本波完成后选卡：true=双选，false=单选。")]
    public bool doublePick = false;
}
