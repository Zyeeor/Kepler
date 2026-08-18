using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 波次内单种怪物的条目：prefab + 数量。
/// </summary>
[Serializable]
public class MonsterEntry
{
    [Tooltip("怪物 prefab（挂 MonsterActor/Enemy）。Phase 2 经 MonsterPool 实例化。")]
    public GameObject prefab;
    [Tooltip("该种怪物数量。")]
    [Min(1)] public int count = 1;
}

/// <summary>
/// 波次属性倍率：作用于波内所有怪物的基础属性（难度调节用）。
/// </summary>
[Serializable]
public class StatMultipliers
{
    [Tooltip("生命值倍率。")]
    public float health = 1f;
    [Tooltip("伤害倍率。")]
    public float damage = 1f;
    [Tooltip("移动速度倍率。")]
    public float moveSpeed = 1f;
}

/// <summary>
/// 怪物编队（Wave）配置：波次玩法的配置单元（"刷哪些怪"）。
///
/// 当前唯一使用场景：
///   - 波次玩法：被 WaveConfig.weightedTable 引用（WaveDefEntry 包装），
///     权重在条目上（每波独立占比），抽中后刷出 monsters 整组
///     （数量由 WaveConfig.totalCount / duration 在 WaveManager 侧控制）。
///
/// 2026-08-18：地图静态怪模式已移除（原 spawnWeight/ChunkDef.waveTable/spawnedWaveIds 链路废弃）。
/// </summary>
[CreateAssetMenu(fileName = "MonsterWaveDef", menuName = "Kepler/Map/Monster Wave")]
public class MonsterWaveDef : ScriptableObject
{
    [Tooltip("配置唯一 id（备用，当前波次模式不参与逻辑）。")]
    public string id;

    [Tooltip("抽取权重（备用，波次模式的占比配置在 WaveConfig.weightedTable 条目上）。")]
    [Min(0f)] public float spawnWeight = 1f;

    [Header("怪物组成")]
    [Tooltip("怪物类型 + 组内数量：抽中本编队时整组刷出。")]
    public List<MonsterEntry> monsters = new List<MonsterEntry>();

    [Header("属性倍率（暂未应用）")]
    [Tooltip("作用于波内所有怪物的属性倍率（难度调节预留）。当前实现不应用——需先快照 prefab 基值，否则池复用倍率累积污染。")]
    public StatMultipliers statMult = new StatMultipliers();
}
