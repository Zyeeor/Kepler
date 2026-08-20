using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 对局存档协调器（轨 B：波次间安全存档点）。
///
/// 纯静态实现（无场景挂载要求）：
///   - 写：WaveManager 在波次清场后（选卡弹窗前）调用 SaveSnapshot(completedWaveIndex)
///   - 读：主菜单"继续"按钮 RequestResume() → 加载场景后各系统在自身启动点读取 ResumeData
///         （MapStreamingSystem.Awake 应用 worldSeed / CardManager.Awake 恢复已解锁卡 /
///          WaveManager.Start 恢复波次进度 + 玩家运行时状态）
///
/// 跨场景标志说明：静态 resumeRequested 在场景切换间存活；ResumeData 懒加载（首次访问读盘），
/// 保证不依赖各系统 Awake 顺序。新游戏（MainMenu.OnStartGame）必须 DeleteSave() 清档。
/// </summary>
public static class SaveCoordinator
{
    /// <summary>存档结构版本（与 SaveData.schemaVersion 一致）。</summary>
    public const int SchemaVersion = 1;

    static readonly string SavePath = Path.Combine(Application.persistentDataPath, "possess_run_save.json");

    /// <summary>主菜单"继续"请求标记（LoadScene 前置 true，场景内各系统据此恢复）。</summary>
    static bool resumeRequested;

    /// <summary>是否有"继续"请求（供场景启动判断；读取会触发懒加载）。</summary>
    public static bool ResumeRequested => resumeRequested;

    /// <summary>恢复数据（懒加载：首次访问读盘解析；无请求/无文件/损坏返回 null）。</summary>
    public static SaveData ResumeData
    {
        get
        {
            if (!resumeRequested) return null;
            if (resumeData == null) resumeData = LoadFromDisk();
            return resumeData;
        }
    }

    static SaveData resumeData;

    /// <summary>是否存在存档文件（主菜单"继续"按钮可用性判断）。</summary>
    public static bool HasSaveFile => File.Exists(SavePath);

    /// <summary>请求"继续对局"：主菜单按钮调用，随后 LoadScene 战斗场景。</summary>
    public static void RequestResume()
    {
        resumeRequested = true;
        resumeData = null;
    }

    /// <summary>
    /// 写入存档（纯 IO：数据由 RunSession 采集并传入，本层不感知场景对象）。
    /// 由 RunSession.SaveProgress 调用（波间安全窗口：场上怪已清空、选卡未弹）。
    /// </summary>
    /// <param name="completedWaveIndex">刚完成的波次索引（恢复从下一波开始）。</param>
    public static void SaveSnapshot(int completedWaveIndex, uint worldSeed, List<string> unlockedEffects,
        Vector3 soulPosition, float soulHealth, float soulTime,
        SaveData.MonsterBodySave possessedBody = null, List<SaveData.MonsterBodySave> corpses = null,
        bool pendingChoice = false, List<string> choicePicks = null, int globalMissStreak = 0)
    {
        var data = new SaveData
        {
            schemaVersion = SchemaVersion,
            savedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            worldSeed = worldSeed,
            completedWaveIndex = completedWaveIndex,
            pendingChoice = pendingChoice,
            soulPosition = soulPosition,
            soulHealth = soulHealth,
            soulTime = soulTime,
            possessedBody = possessedBody,
            globalMissStreak = globalMissStreak,
        };
        if (unlockedEffects != null)
            data.unlockedEffects.AddRange(unlockedEffects);
        if (corpses != null)
            data.corpses.AddRange(corpses);
        if (choicePicks != null)
            data.choicePicks.AddRange(choicePicks);

        try
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, prettyPrint: true));
            resumeRequested = false;   // 存档后清除继续标记，避免同场景误恢复
            resumeData = data;         // 缓存当前存档（本次会话内可再次查询）
            Debug.Log($"[SaveCoordinator] 波次 {completedWaveIndex} 后已存档 → {SavePath}（{new FileInfo(SavePath).Length}B）");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveCoordinator] 存档写入失败：{e.Message}");
        }
    }

    /// <summary>
    /// 恢复玩家运行时状态（灵魂位置 / 灵魂 HP / 灵魂时间）。
    /// 由 WaveManager 在恢复对局时调用（此时 PlayerHealth/SoulActor/GameManager 已 Awake）。
    /// </summary>
    public static void RestorePlayerRuntime(SaveData data)
    {
        if (data == null) return;

        var soul = UnityEngine.Object.FindObjectOfType<SoulActor>();
        if (soul != null)
            soul.transform.position = data.soulPosition;

        if (PlayerHealth.Instance != null)
        {
            PlayerHealth.Instance.currentHealth = data.soulHealth;
            PlayerHealth.Instance.UpdateHealthUI();
        }

        if (GameManager.Instance != null)
            GameManager.Instance.soulTime = data.soulTime;

        Debug.Log($"[SaveCoordinator] 玩家已恢复到灵魂态：pos={data.soulPosition} hp={data.soulHealth} soulTime={data.soulTime}");
    }

    /// <summary>删除存档（新游戏 / 存档损坏清理）。</summary>
    public static void DeleteSave()
    {
        try
        {
            if (File.Exists(SavePath)) File.Delete(SavePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveCoordinator] 存档删除失败：{e.Message}");
        }
        resumeRequested = false;
        resumeData = null;
    }

    /// <summary>读盘解析：损坏 / 版本不符 / 异常均返回 null（调用方走新局兜底）。</summary>
    static SaveData LoadFromDisk()
    {
        if (!File.Exists(SavePath))
        {
            Debug.LogWarning("[SaveCoordinator] 存档文件不存在，无法继续。");
            return null;
        }

        try
        {
            var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
            if (data == null)
            {
                Debug.LogError("[SaveCoordinator] 存档解析为空，已作废。");
                return null;
            }
            if (data.schemaVersion != SchemaVersion)
            {
                // 版本不符：走迁移链（SaveMigrator 逐版本升级）；缺迁移函数才作废
                int from = data.schemaVersion;
                if (!SaveMigrator.TryMigrate(data, from, out var migrated))
                {
                    Debug.LogError($"[SaveCoordinator] 存档版本 {from} 无法迁移至 {SchemaVersion}（缺迁移函数），已作废。");
                    return null;
                }
                Debug.Log($"[SaveCoordinator] 存档已迁移：v{from} → v{SchemaVersion}。");
                data = migrated;
            }
            return data;
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveCoordinator] 存档读取异常：{e.Message}，已作废。");
            return null;
        }
    }
}
