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
/// 怪物编队（Wave）配置：刷怪系统的配置单元。
/// ChunkDef 持有刷怪表（List&lt;MonsterWaveDef&gt;），MonsterSpawner 按 spawnWeight 抽取；
/// 已刷出的波次 ID 记入 ChunkState.spawnedWaveIds，重入不重摇。
/// </summary>
[CreateAssetMenu(fileName = "MonsterWaveDef", menuName = "Kepler/Map/Monster Wave")]
public class MonsterWaveDef : ScriptableObject
{
    [Tooltip("配置唯一 id。记入 ChunkState.spawnedWaveIds，用于重入去重。")]
    public string id;

    [Header("规模")]
    [Tooltip("基础数量：编队规模基准。")]
    [Min(1)] public int baseCount = 4;
    [Tooltip("威胁值：影响全场总量上限的权重。")]
    public float threatValue = 1f;
    [Tooltip("身体供应：每波提供多少可附身身体（附身玩法供给，消耗计入 ChunkState.bodySupplyConsumed）。")]
    [Min(0)] public int bodySupply = 1;

    [Header("刷新")]
    [Tooltip("刷新权重：MonsterSpawner 从 ChunkDef 刷怪表抽取时的相对权重。")]
    [Min(0f)] public float spawnWeight = 1f;

    [Header("怪物组成")]
    [Tooltip("怪物类型 + 数量。")]
    public List<MonsterEntry> monsters = new List<MonsterEntry>();

    [Header("属性倍率")]
    [Tooltip("作用于波内所有怪物的属性倍率。")]
    public StatMultipliers statMult = new StatMultipliers();
}
