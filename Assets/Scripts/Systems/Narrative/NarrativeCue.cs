using System;
using System.Collections.Generic;
using UnityEngine;

public enum CuePriority { Low = 0, Normal = 10, High = 20, Critical = 30 }
public enum RepeatScope { Repeatable = 0, OncePerRun = 1, OncePerProfile = 2 }
public enum SubtitleMode { None = 0, Optional = 1, Forced = 2 }
public enum CueBusyPolicy { Queue = 0, DropIfBusy = 1, Interrupt = 2 }
public enum CueInterruptPolicy { AbandonSequence = 0, ResumeAfterInterruption = 1 }

/// <summary>文本线偏好（Display Profile）。</summary>
public enum TextLinePreference { Neutral = 0, Mythic = 1, System = 2, FollowAccess = 3 }

/// <summary>文本载体分类（载体级覆盖用）。</summary>
public enum NarrativeCarrier { General = 0, Card = 1, WaveTitle = 2, Result = 3, Subtitle = 4, MonsterIntro = 5, TutorialFlavor = 6 }

/// <summary>
/// 旁白 Cue（ScriptableObject，策划编辑）：契约 §6 全部字段 + §5 多句顺序组合。
/// 增删 Cue = 增删 Assets/Resources/Narrative/Cues/ 下资产，零代码零 Prefab。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/Narrative/Cue", fileName = "Cue_")]
public class NarrativeCue : ScriptableObject
{
    [Header("① Cue ID（唯一；Repeat/Interval/存档键）")]
    public string cueId;

    [Header("② Trigger 与条件（多条目=OR；条目内 conditions 按 join 组合）")]
    public List<NarrativeTrigger> triggers = new List<NarrativeTrigger>();

    [Header("③ Access Requirement（低于该 Access 不触发）")]
    public NarrativeAccess requiredAccess = NarrativeAccess.A0;

    [Header("④ 可选 Access Result（播完请求推进；False=不推进）")]
    public bool advanceAccessOnComplete = false;
    public NarrativeAccess accessResult = NarrativeAccess.A0;

    [Header("⑤⑥⑦ 内容行（单句=1 行；多句顺序=多行；组内 Speaker/间隔逐行）")]
    public List<NarrativeCueLine> lines = new List<NarrativeCueLine>();

    [Header("⑧ Subtitle Mode")]
    public SubtitleMode subtitleMode = SubtitleMode.Optional;

    [Header("⑨ Delay（触发后延迟秒；scaled，Pause 冻结）")]
    [Min(0f)] public float delaySeconds = 0f;

    [Header("⑩ Priority")]
    public CuePriority priority = CuePriority.Normal;

    [Header("⑪ Repeat Scope")]
    public RepeatScope repeatScope = RepeatScope.Repeatable;

    [Header("⑫ Minimum Interval（同 Cue 重播最小间隔秒；0=不限）")]
    [Min(0f)] public float minimumIntervalSeconds = 0f;

    [Header("⑬ Busy Policy（已有 Voice 在播/排队时）")]
    public CueBusyPolicy busyPolicy = CueBusyPolicy.Queue;

    [Header("⑭ BGM Duck（播放期间压低 BGM；调度器自管 Push/Pop 精确配对）")]
    public bool bgmDuck = true;

    [Header("⑮ 可选 Display Mode Result（播完切换指定载体显示模式）")]
    public bool applyDisplayModeResult = false;
    public NarrativeCarrier displayCarrier = NarrativeCarrier.General;
    public TextLinePreference displayModeResult = TextLinePreference.FollowAccess;

    [Header("多句组中断策略（被更高优 Cue 打断后）")]
    public CueInterruptPolicy interruptPolicy = CueInterruptPolicy.AbandonSequence;

    [Header("高压行为（Normal/Low 默认可延后；High/Critical 不延后）")]
    public bool deferUnderPressure = true;
}

/// <summary>内容行（契约 §5：组内不同 Speaker、间隔；TextKey/AudioID 逐行）。</summary>
[Serializable]
public class NarrativeCueLine
{
    public VoiceChannel speaker = VoiceChannel.Mythic;
    public string textKey;
    public string audioId;
    [Min(0f)] public float gapAfter = 0.3f;
}
