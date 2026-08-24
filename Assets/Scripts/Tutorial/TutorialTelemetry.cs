using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 教学埋点（用户决策：v1 = Debug.Log + 本地 jsonl，不接后端）。
/// 事件：事实报告 / Step 激活 / Step 完成 / Step 超时 / 教学关闭。
/// 文件：persistentDataPath/tutorial_telemetry.jsonl（每事件一行 JSON，跨 Run 追加）。
/// 后续接后端时仅需替换 Flush 目标，事件收集接口不变。
/// </summary>
public static class TutorialTelemetry
{
    static readonly string TelemetryPath =
        Path.Combine(Application.persistentDataPath, "tutorial_telemetry.jsonl");

    public static void LogEvent(string eventName, string detail)
    {
        string line =
            "{\"ts\":\"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\"," +
            "\"event\":\"" + eventName + "\"," +
            "\"detail\":\"" + (detail ?? "").Replace("\"", "'") + "\"}";

        Debug.Log("[TutorialTelemetry] " + line);

        try
        {
            File.AppendAllText(TelemetryPath, line + Environment.NewLine);
        }
        catch (Exception e)
        {
            Debug.LogError($"[TutorialTelemetry] 写入失败：{e.Message}");
        }
    }

    public static void FactReported(TutorialFact fact) => LogEvent("fact", fact.ToString());
    public static void StepActivated(string stepId, TutorialBlockingMode blocking) => LogEvent("step_activated", stepId + "|" + blocking);
    public static void StepCompleted(string stepId, string reason) => LogEvent("step_completed", stepId + "|" + reason);
    public static void TutorialDisabled() => LogEvent("tutorial_disabled", "");
    public static void ProfileReset() => LogEvent("profile_reset", "");
}
