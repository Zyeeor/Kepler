using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对局存档数据（轨 B：波次间安全存档点，Wave Clear → 选卡前自动写入）。
///
/// 存档内容设计（最小集）：
///   - worldSeed：地图确定性重建的唯一依赖（ChunkSeed = Hash(coord, worldSeed)），地图块不落盘
///   - completedWaveIndex：已完成的波次索引，恢复后从下一波开始（波间语义：场上怪已清场，无需存怪状态）
///   - unlockedEffects：本局已解锁的卡牌效果（波间选卡已结算，卡池状态只需这一项）
///   - 灵魂态玩家：位置 / 灵魂 HP / 灵魂时间（soulTime）
///
/// 不存（波间语义天然规避）：进行中的波次瞬态、存活怪细节、投射物/脱手效果、协程状态。
/// 附身态与尸体**可恢复**（possessedBody / corpses 字段，WaveManager.RestoreBodies 消费）：
/// 恢复后直接回到附身怪 + 场上尸体照旧。
///
/// 【SchemaVersion 纪律（必须遵守）】任何字段增删/语义变更 → SchemaVersion +1，
/// 并同步在 SaveCoordinator.LoadFromDisk 的 SaveMigrator 挂接对应版本迁移函数（vN→vN+1 链式）。
/// 同版本号下结构漂移 = 校验形同虚设 + 旧档语义未定义。违反此纪律的提交应被拒绝。
/// </summary>
[Serializable]
public class SaveData
{
    /// <summary>存档结构版本：任何字段增删/语义变更时 +1（纪律见类头注释）。读取端经 SaveMigrator 逐版本迁移；无法迁移的旧档返回 null 走新局。v2：新增 runId（精英 BD 快照 upsert 键）。</summary>
    public int schemaVersion = 2;
    /// <summary>写入时间（Unix 秒，仅展示/调试）。</summary>
    public long savedAtUnix;
    /// <summary>本局 runId（精英 BD 快照 upsert 唯一键组成，读档恢复后延续）。</summary>
    public string runId;
    /// <summary>地图种子：恢复时注入 MapStreamingSystem.worldSeed，重建完全一致的地图。</summary>
    public uint worldSeed;
    /// <summary>已完成波次索引（-1 = 尚未完成任何波）。恢复从 completedWaveIndex + 1 开始。</summary>
    public int completedWaveIndex = -1;
    /// <summary>选卡未完成标记：在选卡界面退出时置 true，恢复后先补弹该波选卡再进下一波（避免跳过选卡）。</summary>
    public bool pendingChoice;
    /// <summary>选卡界面退出时的候选卡快照（恢复后补弹用，保证与退出时一致——随机由种子决定而非重新随机）。</summary>
    public List<string> choicePicks = new List<string>();
    /// <summary>本局已解锁的卡牌效果 effectId 列表（至少取得一层的卡）。</summary>
    public List<string> unlockedEffects = new List<string>();
    /// <summary>Global 卡软保底 streak（§11）：连续多少次 Offer 三张都没有 Global 卡。>=2 时 Global 候选权重开始提高。</summary>
    public int globalMissStreak;
    /// <summary>灵魂位置（世界坐标）。</summary>
    public Vector3 soulPosition;
    /// <summary>灵魂当前 HP。</summary>
    public float soulHealth;
    /// <summary>灵魂时间（GameManager.soulTime，玩法资源）。</summary>
    public float soulTime;
    /// <summary>玩家当前附身的怪（null = 灵魂态）。恢复时刷出该怪并直接附身。</summary>
    public MonsterBodySave possessedBody;
    /// <summary>场上可附身尸体（downed 且窗口内），恢复时刷出为尸体状态。</summary>
    public List<MonsterBodySave> corpses = new List<MonsterBodySave>();

    /// <summary>怪物身体快照（附身怪/尸体共用）：prefabId 存 prefab 名，恢复时在波表按名解析。</summary>
    [Serializable]
    public class MonsterBodySave
    {
        /// <summary>怪物 prefab 名（实例名去掉 "(Clone)" 后缀），恢复时在 WaveConfig.weightedTable 按 prefab.name 匹配。</summary>
        public string prefabId;
        /// <summary>世界位置。</summary>
        public Vector3 position;
        /// <summary>当前 HP（附身怪保留血量；尸体为 0）。</summary>
        public float health;
    }
}
