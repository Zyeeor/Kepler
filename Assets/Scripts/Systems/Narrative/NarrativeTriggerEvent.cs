using System;
using System.Collections.Generic;

/// <summary>
/// 归一化触发事件（契约 §3.1 最低集；M3 玩法事件预留占位）。
/// 计数器键 = (eventType, qualifier)，每 Run 重置。
/// </summary>
public enum NarrativeTriggerEvent
{
    None = 0,
    RunStarted,                // RunSession.HasActiveRun 边沿（含直接 Play 兜底）
    InitialCarrierAssigned,    // TutorialFactBus.OpeningCarrierPossessed（开场载体附身）
    RunPhaseChanged,           // qualifier = RunPhase 名
    WaveStarted,               // WaveManager.OnWaveStarted（计数=第 N 波）
    WaveCompleted,             // WaveManager.OnWaveCompleted
    CardOffered,               // CardManager.OnCardOffered（第 N 次 Offer）
    CardConfirmed,             // CardManager.OnEffectUnlocked（第 N 次确认）
    PossessionStarted,         // PossessionManager.OnPossessionStarted（第 N 次）
    VoluntaryRelease,          // OnPossessionEndedEx(VoluntaryRelease)
    DeathRelaySucceeded,       // M3 预留（事件源未实现）
    SoulEntered,               // OnPossessionEndedEx（释放回灵魂态）
    ShrineRecovered,           // M3 预留
    EliteSpawned,              // EliteBuildDirector.OnEliteSpawned（第 N 只）
    EliteFatal,                // OnWaveEnemyKilled + EliteBuildCarrier 判定
    ElitePossessed,            // OnPossessionStarted + EliteBuildCarrier 判定
    FinalPhaseChanged,         // 预留（Final 当前占位直通）
    RunWon,                    // RunPhase.Result
    RunFailed,                 // RunPhase.Failed
    Custom,                    // Report(customEventId)；qualifier = eventId
}

/// <summary>第 N 次 / 至少第 N 次。</summary>
public enum NthMode { Exactly = 0, AtLeast = 1 }

/// <summary>条件组内组合。</summary>
public enum ConditionJoin { And = 0, Or = 1 }

/// <summary>条件类型（Cue Trigger 的附条件）。</summary>
public enum NarrativeConditionType
{
    RunPhaseIs, AccessAtLeast, WaveIndexAtLeast,
    ProfileFirstCleared, ProfileNotFirstCleared,
    PressureFree, CustomFlag,
}

/// <summary>引用"事实"的条件（AND/OR 组成员）。</summary>
[Serializable]
public class NarrativeCondition
{
    public NarrativeConditionType type;
    public string param; // 目标值（phase 名 / flagId）
}

/// <summary>Cue 的触发定义（多条目=OR；条目内 conditions 按 join 组合）。</summary>
[Serializable]
public class NarrativeTrigger
{
    public NarrativeTriggerEvent eventType;
    public int nth = 1;
    public NthMode nthMode = NthMode.Exactly;
    public ConditionJoin join = ConditionJoin.And;
    public List<NarrativeCondition> conditions = new List<NarrativeCondition>();
}

/// <summary>事件计数器快照条目（存档）。</summary>
[Serializable]
public class TriggerCounterEntry
{
    public int eventType;
    public string qualifier;
    public int count;
}
