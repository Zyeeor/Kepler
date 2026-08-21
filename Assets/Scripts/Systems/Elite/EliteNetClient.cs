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
    /// 独立阶段标记（Meta §6.7）：普通波 = "wave"，Final = "final"（不借用虚构 Wave 编号）。
    /// sourceWave 始终记真实已完成波数；服务器当前忽略此未知字段，schema 支持后消费。
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
