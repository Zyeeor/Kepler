using System;
using System.Collections.Generic;

/// <summary>
/// Run Analytics 上传 Wire DTO（POST /api/runs；RunStats_后端对接文档.md §4）。
///
/// 与本地 RunStatsData 的差异：perSin[].sin 由 SinType 枚举（本地 JsonUtility 序列化为 int）
/// 转为 wire 名小写字符串（服务器合法值 = 七宗罪 wire 名）；其余标量字段与本地模型同名
/// （JsonUtility 字段名即 JSON 键，camelCase 天然对齐）。uploadStatus / lastUploadAttemptUnix
/// 为客户端本地状态字段，不上传。
/// </summary>
[Serializable]
public class RunStatsUploadPayload
{
    public int schemaVersion;
    public string runId;
    public string playerId;

    // 身份 / 时间
    public long startedAtUnix;
    public long endedAtUnix;
    public float runDurationSeconds;

    // 结果
    public bool won;
    public string endPhase;
    public int reachedWaveIndex;
    public bool finalReached;
    public bool finalCompleted;

    // Global 计数
    public int totalPossessions;
    public int voluntaryReleases;
    public int deathRelays;
    public int soulEnters;
    public int shrineRecovers;
    public int lowHealthReleases;
    public int bulletTimeCount;
    public float bulletTimeTotalSeconds;
    public int eliteFatalCount;
    public int elitePossessionCount;
    public int distinctSinsUsed;
    public int totalKills;

    // Per-Sin（sin = wire 名字符串）
    public List<RunStatsPerSinUpload> perSin = new List<RunStatsPerSinUpload>();

    /// <summary>本地模型 → 上传 payload（sin 枚举转 wire 名；None 条目跳过——服务器只收七宗罪）。</summary>
    public static RunStatsUploadPayload From(RunStatsData d)
    {
        if (d == null) return null;
        var p = new RunStatsUploadPayload
        {
            schemaVersion = d.schemaVersion,
            runId = d.runId,
            playerId = d.playerId,
            startedAtUnix = d.startedAtUnix,
            endedAtUnix = d.endedAtUnix,
            runDurationSeconds = d.runDurationSeconds,
            won = d.won,
            endPhase = d.endPhase,
            reachedWaveIndex = d.reachedWaveIndex,
            finalReached = d.finalReached,
            finalCompleted = d.finalCompleted,
            totalPossessions = d.totalPossessions,
            voluntaryReleases = d.voluntaryReleases,
            deathRelays = d.deathRelays,
            soulEnters = d.soulEnters,
            shrineRecovers = d.shrineRecovers,
            lowHealthReleases = d.lowHealthReleases,
            bulletTimeCount = d.bulletTimeCount,
            bulletTimeTotalSeconds = d.bulletTimeTotalSeconds,
            eliteFatalCount = d.eliteFatalCount,
            elitePossessionCount = d.elitePossessionCount,
            distinctSinsUsed = d.distinctSinsUsed,
            totalKills = d.totalKills,
        };
        if (d.perSin != null)
        {
            foreach (var ps in d.perSin)
            {
                if (ps == null || ps.sin == SinType.None) continue;
                p.perSin.Add(new RunStatsPerSinUpload
                {
                    sin = RunStatsUtil.WireName(ps.sin),
                    controlSeconds = ps.controlSeconds,
                    possessionCount = ps.possessionCount,
                    movementCount = ps.movementCount,
                    attackCount = ps.attackCount,
                    specialCount = ps.specialCount,
                    cardInvestmentCount = ps.cardInvestmentCount,
                    kills = ps.kills,
                });
            }
        }
        return p;
    }
}

/// <summary>单 Sin 分项统计（上传 wire 格式；sin = wire 名字符串，如 "gluttony"）。</summary>
[Serializable]
public class RunStatsPerSinUpload
{
    public string sin;
    public float controlSeconds;
    public int possessionCount;
    public int movementCount;
    public int attackCount;
    public int specialCount;
    public int cardInvestmentCount;
    public int kills;
}

/// <summary>POST /api/runs 响应（{"ok":true}）。</summary>
[Serializable]
public class RunStatsUploadResp
{
    public bool ok;
}
