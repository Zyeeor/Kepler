using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 整局运行数据（Run Analytics）本地持久化层 + 上传出口（纯 IO + 静默遥测，与 SaveCoordinator 同层职责）。
///
/// 策略：
///   - 每局结束（RunPhase.Result / Failed）时落盘一个 JSON 文件：run_stats/run_&lt;runId&gt;.json；
///   - 文件按 runId 命名，天然幂等（同 runId 覆盖重写）；
///   - 落盘后经 UploadRunData 上传后端（POST {serverUrl}/api/runs，RunStats_后端对接文档.md §4）：
///     服务器按 (playerId, runId) 幂等 upsert → 客户端可安全重试；
///   - 上传状态机（uploadStatus）：0=未上传 → 1=上传中 → 2=已上传 / -1=失败待重试；
///     失败本地保留，由 RetryPendingUploads 在采集器首次拉起（新局开始）时批量重试（§5-3）；
///   - 遥测定位：上传全程静默（仅日志），不弹 UI、不阻塞对局/结算；
///   - 不做自动清理（保留审计）。
///
/// 与对局存档（SaveCoordinator，possess_run_save.json）严格分离：本层只存"局后遥测"，不参与恢复。
/// </summary>
public static class RunStatsStore
{
    /// <summary>运行数据文件扩展名。</summary>
    public const string FileExt = ".json";

    /// <summary>上传开关（TUNABLE；关闭后纯本地记录，不出网）。</summary>
    public static bool uploadEnabled = true;

    /// <summary>单次批量重试的历史未上传条数上限（TUNABLE，防积压轰炸；串行重传自然限速）。</summary>
    public const int MaxRetryPerBoot = 20;

    /// <summary>默认服务器地址（对局中自动取 EliteBuildDirector 配置，与其余联网系统一致）。</summary>
    const string DefaultServerUrl = "http://127.0.0.1:8080";
    const int TimeoutSeconds = 5;

    /// <summary>运行数据根目录（persistentDataPath 下，独立于对局存档）。</summary>
    public static string RunStatsDir
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, "run_stats");
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunStatsStore] 创建目录失败：{e.Message}");
            }
            return dir;
        }
    }

    /// <summary>某局数据文件完整路径。</summary>
    public static string FilePathFor(string runId)
    {
        string safeId = string.IsNullOrEmpty(runId) ? "run-unknown" : runId;
        return Path.Combine(RunStatsDir, safeId + FileExt);
    }

    /// <summary>
    /// 将一局完整数据写入本地（JSON pretty）。失败仅警告不抛出（遥测不阻塞对局/结算）。
    /// </summary>
    public static void SaveRunStats(RunStatsData data)
    {
        if (data == null || string.IsNullOrEmpty(data.runId))
        {
            Debug.LogWarning("[RunStatsStore] 保存失败：data 为空或 runId 缺失。");
            return;
        }
        try
        {
            string path = FilePathFor(data.runId);
            string json = JsonUtility.ToJson(data, prettyPrint: true);
            File.WriteAllText(path, json);
            Debug.Log($"[RunStatsStore] 本局运行数据已落盘 → {path}（{new FileInfo(path).Length}B）");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RunStatsStore] 运行数据写入失败：{e.Message}");
        }
    }

    /// <summary>读取某局运行数据（损坏返回 null）。</summary>
    public static RunStatsData LoadRunStats(string runId)
    {
        if (string.IsNullOrEmpty(runId)) return null;
        string path = FilePathFor(runId);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonUtility.FromJson<RunStatsData>(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RunStatsStore] 读取 {path} 失败：{e.Message}");
            return null;
        }
    }

    // ── 上传出口（RunStats_后端对接文档.md §5-2）──

    /// <summary>
    /// 上传一局运行数据：读本地 → 转 wire payload（sin 枚举 → wire 名）→ POST /api/runs
    /// （幂等，可重试）→ 更新 uploadStatus / lastUploadAttemptUnix 并写回本地。
    /// 异步静默（fire-and-forget）：失败标记 -1 保留本地，由 RetryPendingUploads 重试。
    /// </summary>
    public static void UploadRunData(RunStatsData data)
    {
        if (data == null || string.IsNullOrEmpty(data.runId)) return;
        if (!uploadEnabled) return;
        if (data.uploadStatus == 2) return; // 已上传（幂等）

        // 同步转换 payload：调用方（FinalizeRun）随后即置空 Current，避免异步读对象竞态
        var payload = RunStatsUploadPayload.From(data);
        if (payload == null) return;
        _ = UploadAsync(data.runId, payload);
    }

    /// <summary>
    /// 批量重试历史未上传记录（对接文档 §5-3：断网/失败 → 下次启动/新局重试）。
    /// 扫描 run_stats 目录，uploadStatus != 2 的记录逐条串行重传（上限 MaxRetryPerBoot）。
    /// 由 RunStatsCollector 首次拉起触发；全程静默。
    /// </summary>
    public static void RetryPendingUploads()
    {
        if (!uploadEnabled) return;
        _ = RetryPendingUploadsAsync();
    }

    static async Task RetryPendingUploadsAsync()
    {
        string[] files;
        try
        {
            files = Directory.GetFiles(RunStatsDir, "*" + FileExt);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RunStatsStore] 重试扫描失败：{e.Message}");
            return;
        }

        int retried = 0;
        foreach (string file in files)
        {
            if (retried >= MaxRetryPerBoot) break;
            string runId = Path.GetFileNameWithoutExtension(file);
            var data = LoadRunStats(runId);
            if (data == null || data.uploadStatus == 2) continue;

            var payload = RunStatsUploadPayload.From(data);
            if (payload == null) continue;
            await UploadAsync(runId, payload);
            retried++;
        }
        if (retried > 0)
            Debug.Log($"[RunStatsStore] 批量重试完成：处理 {retried} 条历史记录。");
    }

    static async Task UploadAsync(string runId, RunStatsUploadPayload payload)
    {
        TryMarkStatus(runId, 1); // 上传中（崩溃残留由启动重试收敛）
        try
        {
            var resp = await PostJson<RunStatsUploadResp>("/api/runs", payload);
            if (resp == null || !resp.ok)
                throw new Exception("server rejected (ok != true)");
            TryMarkStatus(runId, 2);
            Debug.Log($"[RunStatsStore] 上传成功：runId={runId}（kills={payload.totalKills} perSin={payload.perSin.Count}）");
        }
        catch (Exception e)
        {
            TryMarkStatus(runId, -1);
            Debug.LogWarning($"[RunStatsStore] 上传失败（本地保留待重试）：runId={runId} → {e.Message}");
        }
    }

    /// <summary>读-改-写本地文件的上传状态（1=上传中 / 2=已上传 / -1=失败待重试），同步刷新 lastUploadAttemptUnix。</summary>
    static void TryMarkStatus(string runId, int status)
    {
        var data = LoadRunStats(runId);
        if (data == null) return; // 文件缺失/损坏：放弃状态标记，不阻断上传流程
        data.uploadStatus = status;
        data.lastUploadAttemptUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SaveRunStats(data);
    }

    // ── HTTP 基础设施（同 EliteNetClient 模式：UnityWebRequest + TaskCompletionSource 桥接）──

    /// <summary>服务器地址：对局中优先取 EliteBuildDirector 配置（与其余联网系统共用一处配置）。</summary>
    static string ResolveServerUrl()
    {
        var director = EliteBuildDirector.Instance;
        return director != null && !string.IsNullOrEmpty(director.serverUrl)
            ? director.serverUrl
            : DefaultServerUrl;
    }

    static Task SendAsync(UnityWebRequest req)
    {
        UnityWebRequestAsyncOperation op = req.SendWebRequest();
        var tcs = new TaskCompletionSource<bool>();
        op.completed += _ => tcs.SetResult(true);
        return tcs.Task;
    }

    static async Task<T> PostJson<T>(string path, object body)
    {
        string url = ResolveServerUrl();
        string json = JsonUtility.ToJson(body);
        using (var req = new UnityWebRequest(url + path, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = TimeoutSeconds;
            await SendAsync(req);

            string text = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"POST {path} failed: {req.error} (HTTP {req.responseCode})");
            return JsonUtility.FromJson<T>(text);
        }
    }
}
