using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 精英怪 BD 快照服务器 HTTP 客户端（Server/docs/api-guide.md §4）。
/// 仅封装精英怪两个接口：POST /api/bd-snapshots（滚动上传 upsert）与 POST /api/elite/pick（请求投放）。
/// 调用模式与 ServerApiCheat 一致：UnityWebRequest + TaskCompletionSource 桥接 + JsonUtility DTO。
/// </summary>
public class EliteNetClient
{
    readonly string serverUrl;
    readonly int timeoutSeconds;
    readonly bool logRawResponses;

    public EliteNetClient(string serverUrl, int timeoutSeconds, bool logRawResponses = false)
    {
        this.serverUrl = serverUrl;
        this.timeoutSeconds = Mathf.Max(1, timeoutSeconds);
        this.logRawResponses = logRawResponses;
    }

    /// <summary>健康检查（启动探活 + 网络状态检测）。成功返回 true，超时/失败返回 false。</summary>
    public async Task<bool> Ping()
    {
        try
        {
            using (var req = UnityWebRequest.Get(serverUrl + "/api/health"))
            {
                req.timeout = timeoutSeconds;
                await SendAsync(req);
                return req.result == UnityWebRequest.Result.Success;
            }
        }
        catch { return false; }
    }

    /// <summary>每波选卡后批量上传 BD 快照（同 (playerId, runId, sin) upsert 覆盖）。</summary>
    public Task<UploadSnapshotsResp> UploadSnapshots(UploadSnapshotsReq body)
        => PostJson<UploadSnapshotsResp>("/api/bd-snapshots", body);

    /// <summary>第 N 波请求精英怪投放（四步筛选 + 三级兜底；snapshot=null 为正常"不投放"分支）。</summary>
    public Task<ElitePickResp> Pick(string playerId, int wave, int waveGap)
        => PostJson<ElitePickResp>("/api/elite/pick", new ElitePickReq { playerId = playerId, wave = wave, waveGap = waveGap });

    /// <summary>战果回传（Meta §6.5）：精英在他人游戏中的战果事件批量上报，按构筑主人聚合（荣誉殿堂异步战绩数据源）。</summary>
    public Task<ReportEventsResp> ReportEvents(ReportEventsReq body)
        => PostJson<ReportEventsResp>("/api/elite/events", body);

    /// <summary>查询异步战绩聚合（荣誉殿堂 §5.7 联网刷新；playerId = 本机玩家 = 构筑主人）。</summary>
    public Task<EliteStatsResp> FetchStats(string playerId)
        => GetJson<EliteStatsResp>("/api/elite/stats?playerId=" + Uri.EscapeDataString(playerId));

    // ── HTTP 基础设施（同 ServerApiCheat 模式）──

    static Task SendAsync(UnityWebRequest req)
    {
        UnityWebRequestAsyncOperation op = req.SendWebRequest();
        var tcs = new TaskCompletionSource<bool>();
        op.completed += _ => tcs.SetResult(true);
        return tcs.Task;
    }

    async Task<T> PostJson<T>(string path, object body)
    {
        string label = "POST " + path;
        string json = JsonUtility.ToJson(body);
        using (var req = new UnityWebRequest(serverUrl + path, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = timeoutSeconds;
            await SendAsync(req);

            string text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"{label} failed: {req.error} (HTTP {req.responseCode}) body: {text}");
            if (logRawResponses)
                Debug.Log($"[EliteNetClient] <- {label}: {text}");
            return JsonUtility.FromJson<T>(text);
        }
    }

    async Task<T> GetJson<T>(string path)
    {
        string label = "GET " + path;
        using (var req = UnityWebRequest.Get(serverUrl + path))
        {
            req.timeout = timeoutSeconds;
            await SendAsync(req);

            string text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"{label} failed: {req.error} (HTTP {req.responseCode}) body: {text}");
            if (logRawResponses)
                Debug.Log($"[EliteNetClient] <- {label}: {text}");
            return JsonUtility.FromJson<T>(text);
        }
    }
}

// ── Wire DTO（与 Server/internal 的 JSON 字段一一对应，camelCase；JsonUtility 字段即键名）──

/// <summary>BD 卡条目：卡 ID + 层数（策划案 §8.6；当前卡系统无叠层，stack 恒 1，字段向前兼容）。</summary>
[Serializable]
public class BdCardEntry
{
    public string cardId;
    public int stack;
}

/// <summary>上传用单条快照（bdData 为不透明 JSON，服务器只透传存储）。</summary>
[Serializable]
public class SnapshotEntry
{
    public string sin;
    public string monsterType;
    public int bdCount;
    public List<BdCardEntry> bdData;
    public int sourceWave;
    /// <summary>
    /// 独立阶段标记：普通波内选卡 = "wave"，Final = "final"（不借用虚构编号）。
    /// sourceWave = 本局第几次选卡（1-based 选卡会话计数，含精英奖励选卡；Owner 2026-08-26
    /// 决策，与投放序号同量纲）；服务器按数值透传比较（sourceWave >= wave + waveGap）。
    /// </summary>
    public string stage;
    public long gameTime;
}

[Serializable]
public class UploadSnapshotsReq
{
    public string playerId;
    public string runId;
    public List<SnapshotEntry> snapshots;
}

[Serializable]
public class UploadSnapshotsResp
{
    public bool ok;
    public int accepted;
}

[Serializable]
public class ElitePickReq
{
    public string playerId;
    public int wave;
    public int waveGap;
}

/// <summary>服务器返回的投放快照（bdData/gameTime 为上传时原文透传）。</summary>
[Serializable]
public class EliteSnapshotItem
{
    public long snapshotId;
    public string sourcePlayerId;
    public string runId;
    public string sin;
    public string monsterType;
    public List<BdCardEntry> bdData;
    public int bdCount;
    public int sourceWave;
    public long gameTime;
}

[Serializable]
public class ElitePickResp
{
    public EliteSnapshotItem snapshot;
    public bool relaxed;

    /// <summary>是否命中投放。JsonUtility 无法区分 "snapshot":null 与默认实例，双重判断（同 ServerApiCheat.LogPick）。</summary>
    public bool HasSnapshot => snapshot != null
        && !(snapshot.snapshotId == 0 && string.IsNullOrEmpty(snapshot.sin));
}

// ── 战果回传 Wire DTO（Meta §6.5）──

/// <summary>单条战果事件（客户端埋点）。owner 三键 = 快照来源玩家身份，服务端按其聚合。</summary>
[Serializable]
public class EliteEventEntry
{
    public long snapshotId;
    public string ownerPlayerId;
    public string ownerRunId;
    public string sin;
    /// <summary>事件类型（§6.5 五类）：spawned / fatal / possessed / bodyFatal / runFail。</summary>
    public string type;
    public int wave;
    public long gameTime;
    /// <summary>客户端生成的唯一事件 ID：上报失败重发同一批事件时，服务端按 (playerId, eventId) 幂等去重（P1 防重试重放刷计数）。</summary>
    public string eventId;
}

[Serializable]
public class ReportEventsReq
{
    /// <summary>回报玩家（观测用，不参与聚合键）。</summary>
    public string playerId;
    public List<EliteEventEntry> events;
}

[Serializable]
public class ReportEventsResp
{
    public bool ok;
    public int accepted;
}

/// <summary>异步战绩聚合条目（GET /api/elite/stats 返回项；荣誉殿堂 §5.4 异步字段，字段名与原始表现分开）。</summary>
[Serializable]
public class EliteStatsItem
{
    public string ownerPlayerId;
    public string ownerRunId;
    public string sin;
    public int deployed;    // 被投放次数
    public int fatal;       // 被其他玩家击杀次数
    public int possessed;   // 被其他玩家 Possess 次数
    public int bodyFatal;   // 造成 Body Fatal 次数
    public int runFail;     // 直接导致 Run Fail 次数
    public long updatedAt;
}

[Serializable]
public class EliteStatsResp
{
    public string playerId;
    public List<EliteStatsItem> stats;
}
