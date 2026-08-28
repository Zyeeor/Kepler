using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 荣誉殿堂条目（Canonical Meta_Progression §5.2–5.4）：
/// 记录身份 = (playerId, runId, sin)；每个 Run 中每个「携带 Card 数量 ≥ 1」的 Sin 形成一条记录（§5.2）。
/// 原始 Run 表现与异步传播战绩分开展示，不混用同一字段名（§5.4）。
/// </summary>
[Serializable]
public class HallOfFameEntry
{
    // ── 记录身份（§5.2）──
    public string playerId;
    public string runId;
    /// <summary>Sin wire 名（如 "lust"，与精英快照 upsert 键同源）。</summary>
    public string sin;

    // ── 原始 Run 表现（§5.4）──
    /// <summary>保存时间（最近一次更新 / 终态冻结时刻，Unix 秒 UTC）。</summary>
    public long savedAtUnix;
    /// <summary>最近一次构筑更新阶段（"wave" / "final"；终态冻结后为 endPhase）。</summary>
    public string stage;
    /// <summary>来源 Run 的结束阶段（Result / Failed / Aborted / NewRunInterrupt；空 = 对局中或异常未冻结）。</summary>
    public string endPhase;
    /// <summary>来源 Run 到达波次（1-based；0 = 未完成任何波）。</summary>
    public int reachedWave;
    /// <summary>该 Sin 的 BD 深度（携带 Card 数量）。</summary>
    public int bdCount;
    /// <summary>携带的 Card ID 清单（当前卡系统无叠层，stack 恒 1，仅存 ID；§5.4）。</summary>
    public List<string> cardIds = new List<string>();
    /// <summary>本局控制该 Sin 怪物的总时间（秒，终态冻结自 RunStats）。</summary>
    public float controlSeconds;
    /// <summary>本局该 Sin 身体的击杀数（终态冻结自 RunStats；§5.5「该怪物在本 Run 来源局的击杀数」）。</summary>
    public int kills;

    // ── 异步传播战绩缓存（§5.4/§5.7：来自他人游戏，联网刷新覆盖；与原始表现字段严格分开）──
    /// <summary>被投放次数。</summary>
    public int deployed;
    /// <summary>被其他玩家击杀次数。</summary>
    public int fatal;
    /// <summary>被其他玩家 Possess 次数。</summary>
    public int possessed;
    /// <summary>造成 Body Fatal 次数。</summary>
    public int bodyFatal;
    /// <summary>直接导致 Run Fail 次数。</summary>
    public int runFail;
    /// <summary>战绩缓存刷新时间（Unix 秒；0 = 从未拉取过，UI 显示"未同步"）。</summary>
    public long statsUpdatedAtUnix;

    // ── 称号词缀缓存（词缀命名系统，方案 §7：生命周期 C 混合冻结）──
    /// <summary>
    /// 冻结的中性词序列（如 "远诱|阔口|贪噬|终食"；空 = 未生成）。
    /// 存**中性词序列**而非成品字符串：切 Mythic/System 显示线时仍需重新套模板，
    /// 冻结成品会锁死显示线。
    /// </summary>
    public string epithetCache;
    /// <summary>生成时的词表版本号；0 = 未生成。词表改版时用当前词表重算并写入。</summary>
    public int epithetRev;
}

/// <summary>荣誉殿堂持久化数据（独立长期存储，§5.3：永久保留，不受 Elite 候选库 FIFO 淘汰影响）。</summary>
[Serializable]
public class HallOfFameData
{
    public int schemaVersion = 1;
    public List<HallOfFameEntry> entries = new List<HallOfFameEntry>();
}

/// <summary>
/// 荣誉殿堂存储（纯静态 JSON IO，模式同 TutorialProfileStore）。
/// 独立文件 possess_hall_of_fame.json，不进 SaveMigrator 链；读写均带 try/catch，
/// 损坏重置为空（仅丢展示性记录，不影响对局与 Run 档）。
/// </summary>
public static class HallOfFameStore
{
    static readonly string FilePath =
        Path.Combine(Application.persistentDataPath, "possess_hall_of_fame.json");

    // v1 → v2：追加 epithetCache / epithetRev（称号词缀缓存，方案 §7）。
    // 均为可空 / 有缺省值字段，旧档载入后字段为 null/0，首次展示时用当前词表补生成，
    // 因此**无需显式迁移代码**（Load 已有前向兼容：版本不符仍保留条目）。
    const int CurrentSchemaVersion = 2;

    static HallOfFameData cached;
    static bool loaded;

    /// <summary>当前数据（懒加载；无文件/损坏时返回全新默认）。</summary>
    public static HallOfFameData Data
    {
        get
        {
            if (!loaded) Load();
            return cached;
        }
    }

    // ── 写入链 ──

    /// <summary>
    /// 对局内滚动更新（§5.2「对局内持续更新该 Sin 快照」）：
    /// 选卡完成 / Final 时由 EliteBuildDirector 在组装上传快照后调用（同一数据源，双写本地与服务器）。
    /// 本地无条件写入（不依赖网络），保证异常终止也有最后一波间存档点的构筑记录。
    /// </summary>
    /// <param name="runId">本局 Run ID。</param>
    /// <param name="reachedWave">刚完成的波次（1-based 真实值；荣誉殿堂「到达第 N 波」展示口径，与上传 wire 的选卡计数 sourceWave 语义分离）。</param>
    /// <param name="stage">阶段标记（"wave" / "final"）。</param>
    /// <param name="snapshots">本局全部 bdCount>=1 的 Sin 快照（EliteBuildDirector.BuildSnapshots 产物）。</param>
    public static void UpsertFromSnapshots(string runId, int reachedWave, string stage, List<SnapshotEntry> snapshots)
    {
        if (string.IsNullOrEmpty(runId) || snapshots == null || snapshots.Count == 0) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool changed = false;
        foreach (var snap in snapshots)
        {
            if (snap == null || snap.bdCount < 1 || string.IsNullOrEmpty(snap.sin)) continue;
            var entry = FindOrCreate(runId, snap.sin);
            entry.playerId = DeviceIdentity.Id;
            entry.savedAtUnix = now;
            entry.stage = stage;
            entry.reachedWave = Mathf.Max(entry.reachedWave, reachedWave);
            entry.bdCount = snap.bdCount;
            entry.cardIds = ExtractCardIds(snap.bdData);
            // 称号词缀：与上传端共用 CardEpithetGenerator（验收项 14：不得各写一份）。
            // 每次滚动更新都重算——构筑变了名字就该变；epithetRev 记录所用词表版本。
            RefreshEpithet(entry);
            changed = true;
        }
        if (changed) Save();
    }

    /// <summary>
    /// Run 结束冻结（§5.2「Run 结束后冻结为荣誉记录」/ §5.7「Run 结束立即写入」；失败局同样冻结 §5.10）：
    /// 由 RunStatsCollector.FinalizeRun 落盘后调用，覆盖全部终态（Result / Failed / Aborted / NewRunInterrupt）。
    /// </summary>
    public static void FinalizeRun(RunStatsData data)
    {
        if (data == null || string.IsNullOrEmpty(data.runId)) return;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool changed = false;
        foreach (var ps in data.perSin)
        {
            if (ps == null || ps.sin == SinType.None || ps.cardInvestmentCount < 1) continue; // 未携带 Card 的 Sin 不形成记录（§5.2）
            var entry = Find(data.runId, RunStatsUtil.WireName(ps.sin));
            if (entry == null) continue; // 无构筑清单（从未触发滚动更新，如 W1 内即败）——不创建半条记录
            entry.savedAtUnix = now;
            entry.endPhase = data.endPhase;
            entry.stage = data.endPhase;
            entry.reachedWave = data.reachedWaveIndex + 1;
            entry.controlSeconds = ps.controlSeconds;
            entry.kills = ps.kills;
            changed = true;
        }
        if (changed) Save();
    }

    /// <summary>
    /// 异步战绩缓存更新（§5.7 联网刷新）：按 (runId, sin) 匹配覆盖五计数器。返回更新条数。
    /// 服务器有、本地无的条目（换设备/清档）忽略——不虚构原始表现。
    /// </summary>
    public static int ApplyStats(List<EliteStatsItem> stats)
    {
        if (stats == null || stats.Count == 0) return 0;
        int applied = 0;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var st in stats)
        {
            if (st == null || string.IsNullOrEmpty(st.ownerRunId) || string.IsNullOrEmpty(st.sin)) continue;
            var entry = Find(st.ownerRunId, st.sin);
            if (entry == null)
            {
                Debug.Log($"[HallOfFame] 战绩条目无本地记录，跳过：runId={st.ownerRunId} sin={st.sin}（服务器聚合存在、本地荣誉记录缺失，不虚构原始表现）。");
                continue;
            }
            entry.deployed = st.deployed;
            entry.fatal = st.fatal;
            entry.possessed = st.possessed;
            entry.bodyFatal = st.bodyFatal;
            entry.runFail = st.runFail;
            entry.statsUpdatedAtUnix = now;
            applied++;
        }
        if (applied > 0) Save();
        return applied;
    }

    // ── 称号词缀（方案 §7：生命周期 C 混合冻结）──

    /// <summary>
    /// 按当前卡清单与词表重算并冻结称号词序列。
    /// 目录缺失 / 卡池无词时写入空串（UI 走兜底命名，不虚构词缀，方案 §7.3）。
    /// </summary>
    public static void RefreshEpithet(HallOfFameEntry entry)
    {
        if (entry == null) return;
        var catalog = CardEpithetCatalog.Instance;
        if (catalog == null || entry.cardIds == null || entry.cardIds.Count == 0)
        {
            entry.epithetCache = "";
            entry.epithetRev = 0;
            return;
        }
        var words = CardEpithetGenerator.Generate(entry.cardIds, catalog);
        entry.epithetCache = CardEpithetGenerator.EncodeCache(words);
        entry.epithetRev = catalog.epithetRev;
    }

    /// <summary>
    /// 老档补生成：epithetCache 为空（旧档 / 词表改版）时用当前词表生成一次并写入。
    /// 返回是否有变更（有则调用方需落盘）。
    /// 名字已冻结的条目**不会**被覆写（方案 §5.9：历史语义不静默改写）。
    /// </summary>
    public static bool BackfillMissingEpithets()
    {
        var catalog = CardEpithetCatalog.Instance;
        if (catalog == null) return false;

        bool changed = false;
        foreach (var e in Data.entries)
        {
            if (e == null) continue;
            // 已是当前词表版本 → 保持不动（名字已冻结，不静默改写）
            if (!string.IsNullOrEmpty(e.epithetCache) && e.epithetRev == catalog.epithetRev) continue;

            // 两种情况重算：
            //   1) 老档 / 从未生成过（epithetCache 空）
            //   2) 词表已改版（bump 过 epithetRev）—— 按新词表重算，便于修错别字
            RefreshEpithet(e);
            changed = true;
        }
        if (changed) Save();
        return changed;
    }

    // ── 查询 ──

    /// <summary>全部荣誉记录（按保存时间倒序副本，§5.6 默认排序）。</summary>
    public static List<HallOfFameEntry> EntriesBySavedTimeDesc()
    {
        var list = new List<HallOfFameEntry>(Data.entries);
        list.RemoveAll(e => e == null);
        list.Sort((a, b) => b.savedAtUnix.CompareTo(a.savedAtUnix));
        return list;
    }

    // ── 内部 ──

    static HallOfFameEntry Find(string runId, string sin)
    {
        foreach (var e in Data.entries)
            if (e != null && e.runId == runId && e.sin == sin) return e;
        return null;
    }

    static HallOfFameEntry FindOrCreate(string runId, string sin)
    {
        var found = Find(runId, sin);
        if (found != null) return found;
        var created = new HallOfFameEntry { runId = runId, sin = sin };
        Data.entries.Add(created);
        return created;
    }

    static List<string> ExtractCardIds(List<BdCardEntry> bdData)
    {
        var ids = new List<string>();
        if (bdData == null) return ids;
        foreach (var c in bdData)
            if (c != null && !string.IsNullOrEmpty(c.cardId)) ids.Add(c.cardId);
        return ids;
    }

    static void Load()
    {
        loaded = true;
        cached = new HallOfFameData();
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonUtility.FromJson<HallOfFameData>(File.ReadAllText(FilePath));
                if (data != null && data.entries != null)
                {
                    // 前向兼容（§3 内容版本兼容）：schema 为可加性（仅新增字段）。
                    // 旧版本文件直接复用条目；更高版本（未来字段）也保留可读字段，不整体清库。
                    if (data.schemaVersion != CurrentSchemaVersion)
                        Debug.Log($"[HallOfFame] 荣誉记录 schemaVersion={data.schemaVersion}，当前={CurrentSchemaVersion}，按前向兼容载入（保留条目）。");
                    cached = data;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[HallOfFame] 荣誉记录读取失败，重置为空（{e.Message}）。");
            cached = new HallOfFameData();
        }
    }

    static void Save()
    {
        try
        {
            Data.schemaVersion = CurrentSchemaVersion;
            string dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonUtility.ToJson(Data, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[HallOfFame] 荣誉记录写入失败（{e.Message}）。");
        }
    }
}
