using UnityEngine;

/// <summary>叙事调度参数（ScriptableObject，TUNABLE 集中；Resources/Narrative/NarrativeSchedulerConfig 兜底）。</summary>
[CreateAssetMenu(menuName = "Kepler/Narrative/Scheduler Config", fileName = "NarrativeSchedulerConfig")]
public class NarrativeSchedulerConfig : ScriptableObject
{
    [Header("等待队列")]
    [Min(1)] public int maxPendingCues = 3;
    [Min(1f)] public float pendingExpireSeconds = 20f;

    [Header("高压门")]
    [Range(0f, 1f)] public float lowBodyHealthThreshold = 0.3f;
    [Min(0f)] public float eliteNoDisturbSeconds = 4f;
    public bool pressureInFinal = true;
    public bool pressureDuringTransfer = true;
    public bool pressureDuringBlockingUi = true;

    [Header("字幕时长（无音频行）")]
    [Min(1f)] public float charsPerSecond = 12f;
    public Vector2 subtitleClamp = new Vector2(1.5f, 6f);
}
