using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 整局运行数据（Run Analytics）本地持久化层（纯 IO，与 SaveCoordinator 同层职责）。
///
/// 策略：
///   - 每局结束（RunPhase.Result / Failed）时落盘一个 JSON 文件：run_stats/run_&lt;runId&gt;.json；
///   - 文件按 runId 命名，天然幂等（同 runId 覆盖重写）；
///   - 落盘目录集中管理（RunStatsDir），后续上传成功后由 UploadService 移动/标记，不做自动清理（保留审计）；
///   - 预留 UploadRunData()：对接后端接口的出口（当前为空实现 + TODO 标记，不影响本地记录）。
///
/// 与对局存档（SaveCoordinator，possess_run_save.json）严格分离：本层只存"局后遥测"，不参与恢复。
/// </summary>
public static class RunStatsStore
{
    /// <summary>运行数据文件扩展名。</summary>
    public const string FileExt = ".json";

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

    /// <summary>
    /// 上传出口（预留）：后续对接后端接口的入口。
    /// 当前为空实现 —— 本地记录已完成，上传协议（POST /api/runs 等）待后端确定后在此实现。
    /// </summary>
    public static void UploadRunData(RunStatsData data)
    {
        if (data == null) return;
        // TODO(backend): 对接后端对局记录接口（如 POST {serverUrl}/api/runs）。
        //   成功 → data.uploadStatus = 2；失败 → data.uploadStatus = -1（保留本地文件供重试）。
        Debug.Log($"[RunStatsStore] 上传预留：runId={data.runId}（后端接口未实现，跳过）。");
    }
}
