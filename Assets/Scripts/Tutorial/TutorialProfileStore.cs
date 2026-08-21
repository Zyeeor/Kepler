using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 教学 Profile 持久化数据（Player 级，跨 Run）。
/// 与 Run 级存档（SaveCoordinator/SaveData）分离：
///   - 独立文件 possess_tutorial_profile.json，独立 schemaVersion（不进 SaveMigrator 链）；
///   - 内容：教学开关、各 Step 完成态、已首次附身过的 Monster 类型（TUT-MONSTER 用）、
///           一次性事实（如首次击杀）是否已发生（Step 追溯判定用）。
/// </summary>
[Serializable]
public class TutorialProfileData
{
    public int schemaVersion = 1;
    public bool tutorialEnabled = true;
    public bool tutorialSkippedByUser = false;
    [Tooltip("已完成的 Step ID 列表")]
    public List<string> completedStepIds = new List<string>();
    [Tooltip("已首次附身过的 Monster 类型名（TUT-MONSTER 幂等）")]
    public List<string> possessedMonsterTypes = new List<string>();
    [Tooltip("已发生的一次性事实（跨 Run 追溯判定：如 KilledFirstMonster）")]
    public List<string> seenFacts = new List<string>();
}

/// <summary>
/// 教学 Profile 存储（纯静态 JSON IO，模式同 SaveCoordinator）。
/// 读写均带 try/catch，损坏即重置为新 Profile（教学重来，不影响 Run 档）。
/// </summary>
public static class TutorialProfileStore
{
    static readonly string ProfilePath =
        Path.Combine(Application.persistentDataPath, "possess_tutorial_profile.json");

    static TutorialProfileData cached;
    static bool loaded;

    /// <summary>当前 Profile（懒加载；无文件/损坏时返回全新默认）。</summary>
    public static TutorialProfileData Data
    {
        get
        {
            if (!loaded) Load();
            return cached;
        }
    }

    /// <summary>教学总开关。</summary>
    public static bool TutorialEnabled
    {
        get => Data.tutorialEnabled;
        set { Data.tutorialEnabled = value; Save(); }
    }

    /// <summary>某 Step 是否已完成（幂等判定依据）。</summary>
    public static bool IsStepCompleted(string stepId)
    {
        return stepId != null && Data.completedStepIds.Contains(stepId);
    }

    /// <summary>标记 Step 完成并落盘（幂等：已存在不重复写）。</summary>
    public static void MarkStepCompleted(string stepId)
    {
        if (string.IsNullOrEmpty(stepId)) return;
        if (IsStepCompleted(stepId)) return;
        Data.completedStepIds.Add(stepId);
        Save();
    }

    /// <summary>某 Monster 类型是否已首次附身过。</summary>
    public static bool HasPossessedMonsterType(string monsterTypeName)
    {
        return monsterTypeName != null && Data.possessedMonsterTypes.Contains(monsterTypeName);
    }

    /// <summary>标记首次附身某 Monster 类型（幂等）。</summary>
    public static void MarkPossessedMonsterType(string monsterTypeName)
    {
        if (string.IsNullOrEmpty(monsterTypeName)) return;
        if (HasPossessedMonsterType(monsterTypeName)) return;
        Data.possessedMonsterTypes.Add(monsterTypeName);
        Save();
    }

    /// <summary>一次性事实是否已发生（Step 追溯判定：激活时若已发生 → 直接完成）。</summary>
    public static bool HasSeenFact(TutorialFact fact)
    {
        return Data.seenFacts.Contains(fact.ToString());
    }

    /// <summary>记录一次性事实（幂等）。由 TutorialController 在事实报告时调用。</summary>
    public static void MarkSeenFact(TutorialFact fact)
    {
        string key = fact.ToString();
        if (Data.seenFacts.Contains(key)) return;
        Data.seenFacts.Add(key);
        Save();
    }

    /// <summary>重置教学 Profile（Debug/设置面板"重看教学"用；不清 Run 档）。</summary>
    public static void ResetProfile()
    {
        cached = new TutorialProfileData();
        Save();
        Debug.Log("[TutorialProfile] 教学 Profile 已重置（教学将重新出现）。");
    }

    /// <summary>立即落盘（幂等写：内容无变化也允许，调用频率低）。</summary>
    public static void Save()
    {
        try
        {
            File.WriteAllText(ProfilePath, JsonUtility.ToJson(Data, prettyPrint: true));
        }
        catch (Exception e)
        {
            Debug.LogError($"[TutorialProfile] 写入失败：{e.Message}");
        }
    }

    static void Load()
    {
        loaded = true;
        if (!File.Exists(ProfilePath))
        {
            cached = new TutorialProfileData();
            return;
        }
        try
        {
            cached = JsonUtility.FromJson<TutorialProfileData>(File.ReadAllText(ProfilePath));
            if (cached == null) cached = new TutorialProfileData();
            // v1 无历史版本，schemaVersion 不一致仅告警不迁移（未来加版本时在此扩展）
            if (cached.schemaVersion != 1)
                Debug.LogWarning($"[TutorialProfile] Profile schema {cached.schemaVersion} != 1，按默认字段容错读取。");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TutorialProfile] 读取异常（{e.Message}），重置为新 Profile。");
            cached = new TutorialProfileData();
        }
    }
}
