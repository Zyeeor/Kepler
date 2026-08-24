using System;
using System.Collections.Generic;

/// <summary>
/// 整局运行数据（Run Analytics）—— 数据模型层（纯数据，无逻辑）。
///
/// 字段对齐设计真源：
///   - `.vibe/doc/Canonical/Content/Narrative_Voice_Delivery_Baseline_v1.0.md` §8（First Clear Runtime Data）
///   - `.vibe/doc/Canonical/01_DESIGN_CANONICAL.md` §28（Result 最低原始统计）
///
/// 约定：
///   - 本类只记录"原始值"，主/次倾向评分由配置化评分器生成，不冻结在 Gameplay 规则中（Canonical §28）。
///   - JsonUtility 序列化：Per-Sin 用 List（不支持 Dictionary）。
///   - 预留后端对接字段（playerId / uploadStatus 等）以兼容后续上传。
/// </summary>
[Serializable]
public class RunStatsData
{
    public const int SchemaVersion = 1;

    // ── 身份 / 时间 ──
    /// <summary>数据模型版本（与本地文件 / 上传 schema 对齐）。</summary>
    public int schemaVersion = SchemaVersion;
    /// <summary>对局 ID（RunSession.RunId，与精英 BD 快照/荣誉殿堂 upsert 键一致）。</summary>
    public string runId;
    /// <summary>设备身份（DeviceIdentity.Id；上传后端时使用，本地记录也写入以便离线归档）。</summary>
    public string playerId;
    /// <summary>对局开始 Unix 秒（UTC）。</summary>
    public long startedAtUnix;
    /// <summary>对局结束 Unix 秒（UTC）；未结束为 0。</summary>
    public long endedAtUnix;
    /// <summary>Run 总时长（秒，unscaled realtime，不受子弹时间/暂停影响）。</summary>
    public float runDurationSeconds;

    // ── 结果 ──
    /// <summary>是否胜利（RunPhase.Result）；false = 失败/中途退出（RunPhase.Failed）。</summary>
    public bool won;
    /// <summary>结束时的 Run 阶段（Result / Failed），便于区分胜利、失败、强制退出。 </summary>
    public string endPhase;
    /// <summary>到达的最远波次索引（0-based；-1 = 未完成任何波）。</summary>
    public int reachedWaveIndex = -1;
    /// <summary>是否到达 Final 阶段。</summary>
    public bool finalReached;
    /// <summary>是否完成 Final（进入 Result）。</summary>
    public bool finalCompleted;

    // ── Global 计数（§8.2）──
    /// <summary>总附身次数（含换身）。</summary>
    public int totalPossessions;
    /// <summary>主动离身次数（PossessionEndReason.VoluntaryRelease）。</summary>
    public int voluntaryReleases;
    /// <summary>死亡接力成功次数（M3 玩法未实现，预留字段，恒 0）。</summary>
    public int deathRelays;
    /// <summary>灵魂进入自由形态次数（附身释放回灵魂态时 +1）。</summary>
    public int soulEnters;
    /// <summary>神龛恢复次数（M3 玩法未实现，预留字段，恒 0）。</summary>
    public int shrineRecovers;
    /// <summary>低耐久主动离身次数（离身时身体 HP 低于阈值）。</summary>
    public int lowHealthReleases;
    /// <summary>子弹时间触发次数。</summary>
    public int bulletTimeCount;
    /// <summary>子弹时间总时长（秒）。</summary>
    public float bulletTimeTotalSeconds;
    /// <summary>精英怪击杀数（Fatal）。</summary>
    public int eliteFatalCount;
    /// <summary>精英怪附身数（Possession）。</summary>
    public int elitePossessionCount;
    /// <summary>使用过的不同 Sin 数量（附身过几种 Sin 的身体）。</summary>
    public int distinctSinsUsed;
    /// <summary>总击杀数（怪物进入 Downed 尸体态，含附身死亡后淡出）。</summary>
    public int totalKills;

    // ── Per-Sin（§8.1）──
    /// <summary>各 Sin 分项统计（List，JsonUtility 兼容）。</summary>
    public List<PerSinStats> perSin = new List<PerSinStats>();

    // ── 上传预留 ──
    /// <summary>上传状态（0=未上传，1=上传中，2=已上传，-1=失败待重试）。供后续后端对接轮询。 </summary>
    public int uploadStatus;
    /// <summary>上次上传尝试 Unix 秒（UTC）；0 = 未尝试。</summary>
    public long lastUploadAttemptUnix;

    public PerSinStats FindOrCreateSin(SinType sin)
    {
        if (sin == SinType.None) return null;
        foreach (var s in perSin)
            if (s != null && s.sin == sin) return s;
        var created = new PerSinStats { sin = sin };
        perSin.Add(created);
        return created;
    }

    /// <summary>使用过的不同 Sin 数量（由 perSin 计算，调用方在结算时刷新）。</summary>
    public void RefreshDistinctSinsUsed()
    {
        int count = 0;
        foreach (var s in perSin)
            if (s != null && (s.possessionCount > 0 || s.controlSeconds > 0f))
                count++;
        distinctSinsUsed = count;
    }
}

/// <summary>单个 Sin 的分项统计（§8.1 Per-Sin）。</summary>
[Serializable]
public class PerSinStats
{
    /// <summary>Sin 类型（枚举名序列化为 int；显示时映射 wire 名，见 RunStatsUtil.WireName）。</summary>
    public SinType sin;
    /// <summary>有效 Body 控制时长（秒，附身期间累计，unscaled）。</summary>
    public float controlSeconds;
    /// <summary>附身该 Sin 身体的次数。</summary>
    public int possessionCount;
    /// <summary>该 Sin 身体上"移动类"能力（Mobility）使用次数（玩家控制期间）。</summary>
    public int movementCount;
    /// <summary>该 Sin 身体上"攻击类"能力（BasicAttack）使用次数（玩家控制期间）。</summary>
    public int attackCount;
    /// <summary>该 Sin 身体上"技能类"能力（Skill）使用次数（玩家控制期间）。</summary>
    public int specialCount;
    /// <summary>该 Sin 的卡牌投资数量（取得的 MonsterType / TypeGrowth 卡数）。</summary>
    public int cardInvestmentCount;
    /// <summary>该 Sin 身体造成的击杀数（附身期间击杀其它怪）。</summary>
    public int kills;
}

/// <summary>Sin 工具：wire 名 / 显示名（与 EliteMonsterCatalog.WireName 语义一致，独立实现避免耦合）。</summary>
public static class RunStatsUtil
{
    /// <summary>Sin 的 wire 编码（上传/持久化用）：小写枚举名，如 SinType.Lust → "lust"。</summary>
    public static string WireName(SinType sin) => sin.ToString().ToLowerInvariant();

    /// <summary>
    /// 从怪物 prefab 名解析 Sin（池化怪命名约定：前缀为 sin 英文名，如 gluttony_new / pride_new）。
    /// 解析失败返回 None。
    /// </summary>
    public static SinType SinFromPrefabName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return SinType.None;
        string lower = prefabName.ToLowerInvariant();
        foreach (SinType sin in Enum.GetValues(typeof(SinType)))
        {
            if (sin == SinType.None) continue;
            if (lower.StartsWith(sin.ToString().ToLowerInvariant())) return sin;
        }
        return SinType.None;
    }
}
