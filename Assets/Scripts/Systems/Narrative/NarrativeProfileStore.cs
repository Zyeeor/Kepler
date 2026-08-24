using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>Run-local 叙事存档（进轨 B 存档 SaveData v3；契约 §9）。</summary>
[Serializable]
public class NarrativeRunSave
{
    public int access;
    public List<string> playedCueIds = new List<string>();
    public List<TriggerCounterEntry> triggerCounters = new List<TriggerCounterEntry>();
    public List<CueTimestampEntry> cueLastPlayed = new List<CueTimestampEntry>();
}

/// <summary>Cue 最近播放时间戳条目（Minimum Interval 存档）。</summary>
[Serializable]
public class CueTimestampEntry
{
    public string cueId;
    public float unscaledAt;
}

/// <summary>Profile 层叙事数据（独立 JSON；First Clear/认证计数/每 Profile 已播 Cue/字幕设置）。</summary>
[Serializable]
public class NarrativeProfileData
{
    public int schemaVersion = 1;
    public bool firstClearCompleted;
    public int certificationCount;
    public List<string> playedCueIds = new List<string>();
    public string selectedDeclarationId;
    public bool subtitlesEnabled = true;
}

/// <summary>
/// 叙事 Profile 持久化（范式照抄 TutorialProfileStore：懒加载 + 幂等标记 + try/catch 损坏重置）。
/// Profile 层跨 Run 保留；Run-local 层走 SaveData v3（NarrativeScheduler.CaptureSnapshot）。
/// </summary>
public static class NarrativeProfileStore
{
    static NarrativeProfileData _data;
    static readonly string Path = System.IO.Path.Combine(Application.persistentDataPath, "possess_narrative_profile.json");

    public static NarrativeProfileData Data
    {
        get
        {
            if (_data == null) Load();
            return _data;
        }
    }

    public static bool FirstClearCompleted => Data.firstClearCompleted;

    public static bool HasPlayedCue(string cueId)
        => !string.IsNullOrEmpty(cueId) && Data.playedCueIds.Contains(cueId);

    public static void MarkCuePlayed(string cueId)
    {
        if (string.IsNullOrEmpty(cueId) || HasPlayedCue(cueId)) return;
        Data.playedCueIds.Add(cueId);
        Save();
    }

    public static void MarkFirstClearCompleted()
    {
        if (Data.firstClearCompleted) return;
        Data.firstClearCompleted = true;
        Data.certificationCount = 1;
        Save();
    }

    public static void IncrementCertification()
    {
        if (!Data.firstClearCompleted) { MarkFirstClearCompleted(); return; }
        Data.certificationCount++;
        Save();
    }

    public static bool SubtitlesEnabled
    {
        get => Data.subtitlesEnabled;
        set { if (Data.subtitlesEnabled != value) { Data.subtitlesEnabled = value; Save(); } }
    }

    public static string SelectedDeclarationId
    {
        get => Data.selectedDeclarationId;
        set { Data.selectedDeclarationId = value; Save(); }
    }

    public static void ResetProfile()
    {
        _data = new NarrativeProfileData();
        Save();
    }

    public static void Save()
    {
        try { File.WriteAllText(Path, JsonUtility.ToJson(Data, true)); }
        catch (Exception e) { Debug.LogWarning($"[NarrativeProfileStore] 保存失败：{e.Message}"); }
    }

    static void Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                _data = JsonUtility.FromJson<NarrativeProfileData>(File.ReadAllText(Path));
                if (_data == null) _data = new NarrativeProfileData();
                if (_data.playedCueIds == null) _data.playedCueIds = new List<string>();
            }
            else _data = new NarrativeProfileData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[NarrativeProfileStore] 读取失败，重置：{e.Message}");
            _data = new NarrativeProfileData();
        }
    }

    public static void InvalidateCache() => _data = null;
}
