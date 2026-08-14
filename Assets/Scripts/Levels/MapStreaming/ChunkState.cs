using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 单个怪物的内存快照：prefab 标识 + 位置 + 血量 + 虚弱/倒地/交战状态。
/// [Serializable] 普通类：JsonUtility 可直接序列化（Phase 4 落盘复用， 两级语义）；
/// prefabRef 为内存快捷引用（[NonSerialized]，JsonUtility 跳过），丢失时按 prefabId 回退解析。
/// </summary>
[Serializable]
public class MonsterSnapshot
{
    /// <summary>prefab 标识（prefab.name；落盘读档时的解析键）。</summary>
    public string prefabId;
    /// <summary>prefab 内存引用（恢复快照优先使用；不序列化）。</summary>
    [NonSerialized] public GameObject prefabRef;
    /// <summary>回收时世界位置（已钳制回归属 Chunk 内：跨 Chunk 追击怪落回归属 Chunk 刷怪点，）。</summary>
    public Vector3 position;
    /// <summary>回收时当前血量。</summary>
    public float currentHealth;
    /// <summary>回收时血量上限（statMult 属性倍率接入后用于还原上限，当前仅记录）。</summary>
    public float maxHealth;
    /// <summary>是否虚弱（tenacity 被打空）。</summary>
    public bool isWeakened;
    /// <summary>是否倒地。当前快照构建把倒地怪分流到 CorpseSnapshot，本字段实践中恒 false；恢复侧仍兼容 true。</summary>
    public bool isDowned;
    /// <summary>回收时是否已索敌玩家（遭遇进度，"离开 A 保留遭遇进度"）。</summary>
    public bool playerDetected;
}

/// <summary>
/// 尸体快照：尸体是否被搜刮 / 是否已被用作附身材料。
/// Phase 3 仅记录（回收倒地怪时写入），恢复留 TODO（MonsterSpawner.RestoreChunkMonsters）。
/// </summary>
[Serializable]
public class CorpseSnapshot
{
    /// <summary>prefab 标识（prefab.name）。</summary>
    public string prefabId;
    /// <summary>尸体世界位置（已钳制回归属 Chunk 内）。</summary>
    public Vector3 position;
    /// <summary>是否已被搜刮（搜刮系统接入后写真实值，当前恒 false）。</summary>
    public bool looted;
    /// <summary>是否已被用作附身材料（当前由 ChunkState.bodySupplyConsumed 计数兜底，本字段占位）。</summary>
    public bool consumedAsBody;
}

/// <summary>
/// 奖励快照：奖励是否已被拾取。Phase 3 仅占位结构（奖励系统未接入）。
/// </summary>
[Serializable]
public class LootSnapshot
{
    /// <summary>奖励标识。</summary>
    public string lootId;
    /// <summary>世界位置。</summary>
    public Vector3 position;
    /// <summary>是否已被拾取。</summary>
    public bool picked;
}

/// <summary>
/// 事件标志：守卫击败、机关激活等事件进度。Phase 3 仅占位结构（事件系统未接入）。
/// </summary>
[Serializable]
public class EventFlag
{
    /// <summary>事件标识（如 "guard_defeated"）。</summary>
    public string id;
    /// <summary>是否已触发。</summary>
    public bool triggered;
}

/// <summary>
/// Chunk 玩家干预痕迹快照：单局内持久保留，不随 Chunk 卸载丢失；
/// 落盘仅发生在显式存档（Phase 4+， 两级语义）。
///
/// monsters 生命周期不变量——语义为"当前在池、待恢复的怪物集合"：
///   恢复时逐只移除（配额中断保留剩余项，下次进 B 续恢复）；
///   离开 B/D 回收时追加新鲜快照； 脱战远距离怪写回时追加。
/// spawnedWaveIds 只增不减——重入判重键是"摇过波次"而非"还有怪"：
///   怪物全灭的 Chunk 重入后保持空（约束 3：不得重新随机敌人/奖励/尸体状态）。
/// </summary>
[Serializable]
public class ChunkState
{
    /// <summary>在池待恢复的怪物快照（存活怪：血量/位置/状态）。</summary>
    public List<MonsterSnapshot> monsters = new List<MonsterSnapshot>();
    /// <summary>已刷出的波次 id 列表——重入时跳过，禁止按权重重摇（约束 3）。抽中即登记（含配额中断的波次）。</summary>
    public List<string> spawnedWaveIds = new List<string>();
    /// <summary>已被附身消耗的身体数——重入不重复发放。</summary>
    public int bodySupplyConsumed;
    /// <summary>尸体记录（是否被搜刮/附身消耗）。Phase 3 仅记录不恢复。</summary>
    public List<CorpseSnapshot> corpses = new List<CorpseSnapshot>();
    /// <summary>奖励拾取记录。占位（奖励系统未接入）。</summary>
    public List<LootSnapshot> loots = new List<LootSnapshot>();
    /// <summary>事件进度（守卫击败、机关激活等）。占位（事件系统未接入）。</summary>
    public List<EventFlag> events = new List<EventFlag>();
    /// <summary>氛围种子：维持"相同 Chunk 不同时间进入"的世界感（占位，恢复侧未消费）。</summary>
    public float ambientSeed;
}
