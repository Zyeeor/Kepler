using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对局存档数据（轨 B：安全窗口与离开战斗场景时写入的对局快照）。
///
/// 存档内容设计（最小集）：
///   - worldSeed：地图确定性重建的唯一依赖（ChunkSeed = Hash(coord, worldSeed)），地图块不落盘
///   - completedWaveIndex：旧波次模式的已完成波次索引；连续刷怪模式改用战斗时钟与调度快照
///   - unlockedEffects：本局已解锁的卡牌效果（波间选卡已结算，卡池状态只需这一项）
///   - 灵魂态玩家：位置 / 灵魂 HP / 灵魂时间（soulTime）
///
/// 保存 RunPhase，确保恢复时回到正确的波次/选卡流程阶段。
/// 连续刷怪模式额外保存战斗时钟和调度游标，恢复后从同一调度位置继续。
/// monsterSnapshots 保存离开时仍在场的怪物（位置、生命、状态、刷怪来源与精英构筑元数据）。
/// 不存：投射物/脱手效果、协程状态等无法安全跨场景重建的瞬态。
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
    /// <summary>存档结构版本：任何字段增删/语义变更时 +1（纪律见类头注释）。读取端经 SaveMigrator 逐版本迁移；无法迁移的旧档返回 null 走新局。v4：新增战斗 imprint/贪婪/色欲/boss 字段。v5：新增 narrative（叙事调度 Run-local 状态）。v6：新增连续刷怪调度快照。v7：新增 RunPhase。v8：新增场上怪物快照。</summary>
    public int schemaVersion = 8;
    /// <summary>写入时间（Unix 秒，仅展示/调试）。</summary>
    public long savedAtUnix;
    /// <summary>本局 runId（精英 BD 快照 upsert 唯一键组成，读档恢复后延续）。</summary>
    public string runId;
    /// <summary>地图种子：恢复时注入 MapStreamingSystem.worldSeed，重建完全一致的地图。</summary>
    public uint worldSeed;
    /// <summary>旧波次模式的已完成波次索引（-1 = 尚未完成任何波）；连续刷怪模式固定使用 -1，改由战斗时钟与调度快照恢复。</summary>
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
    /// <summary>离开时场上仍存在的怪物完整快照。新版本优先使用此字段，旧 body 字段保留用于迁移兼容。</summary>
    public List<MonsterSnapshotSave> monsterSnapshots = new List<MonsterSnapshotSave>();
    /// <summary>Effective combat clock used by RunSpawnDirector; pauses and card choice are excluded.</summary>
    public float activeCombatSeconds;
    public List<PossessionImprintState> possessionImprints = new List<PossessionImprintState>();
    public float greedBonusProgress;
    public float lustHealProgress;
    public bool bossSpawned;
    public bool bossDefeated;
    /// <summary>叙事调度 Run-local 状态（Access/已播 Cue/触发计数/最小间隔时间戳）。null = 该档无叙事状态（旧档迁移，恢复按新局初始化）。</summary>
    public NarrativeRunSave narrative;
    /// <summary>连续刷怪普通怪罪印轮换游标。</summary>
    public int continuousNormalOrderIndex;
    /// <summary>连续刷怪下一次普通怪生成时间（RunSpawnDirector.ActiveCombatSeconds 时间轴）。</summary>
    public float continuousNextNormalSpawnTime;
    /// <summary>连续刷怪各配置精英调度是否已消费。</summary>
    public List<bool> continuousEliteSpawned = new List<bool>();
    /// <summary>对局流程阶段（Waves/Choice 等），用于继续时恢复整体流程。</summary>
    public RunPhase runPhase = RunPhase.Waves;

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

    /// <summary>
    /// 场上怪物快照：用于继续游戏时重建普通怪、精英怪、尸体与当前附身身体。
    /// 不记录 AI 协程/投射物等不可安全跨场景恢复的瞬态。
    /// </summary>
    [Serializable]
    public class MonsterSnapshotSave
    {
        public string prefabId;
        public string displayName;
        public SinType sin;
        public Vector3 position;
        public Quaternion rotation;
        public float health;
        public float tenacity;
        public bool isWeakened;
        public bool isDowned;
        public bool isPossessed;
        public bool isContinuousAutomatic;
        public bool countsTowardCombatLimit = true;
        public bool isElite;
        public SpawnOrigin spawnOrigin;

        public bool hasEliteRuntimeModifiers;
        public float eliteHealthMultiplier = 1f;
        public float eliteAttackDamageMultiplier = 1f;
        public float eliteVisualScaleMultiplier = 1f;
        public long eliteSnapshotId;
        public string eliteSourcePlayerId;
        public string eliteRunId;
        public string eliteSin;
        public int eliteSourceWave;
        public List<string> eliteCardIds = new List<string>();
    }
}
