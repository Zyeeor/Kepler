using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BGM 槽位行为（合同 Possession_BGM系统_可扩展最小需求合同 v1.0 §3.1）：
/// 策划显式区分"继承"与"停止"，不能只靠 Clip==null 猜测行为。
/// </summary>
public enum BgmAction
{
    [Tooltip("继承：保持当前音乐不切换（合同 Inherit 语义）。")]
    Inherit = 0,
    [Tooltip("播放：切换到本槽位 Clip（Clip 为空时回退 Inherit 并警告）。")]
    Play = 1,
    [Tooltip("停止：淡出并停止 BGM（用于 Result/Fail 等终态）。")]
    Stop = 2,
}

/// <summary>
/// 阶段 BGM 映射表（ScriptableObject，策划编辑）：RunPhase/状态 → BGM 槽位。
/// 仲裁（集中在 BgmController 单点）：终态（Result/Fail）> Override（soul/elite）> Phase（RunPhase/Wave）> Scene（SceneBgm 兜底）。
/// 加载：AudioManager.Awake 时字段为空 → Resources.Load&lt;StageBgmMap&gt;("Audio/StageBgmMap")。
/// Waves 阶段按波次配置（waveTiers）：某波次匹配到条目则按 action 处理；未匹配 = Inherit（保持当前曲）。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/Audio/StageBgmMap", fileName = "StageBgmMap")]
public class StageBgmMap : ScriptableObject
{
    [Serializable]
    public class Slot
    {
        [Tooltip("行为：Inherit 保持当前 / Play 播放 Clip / Stop 淡出停止。")]
        public BgmAction action = BgmAction.Inherit;
        [Tooltip("BGM 剪辑（action=Play 时使用）。")]
        public AudioClip clip;
        [Tooltip("淡入淡出时长覆盖（秒）；≤0 用 BgmController.bgmFadeDuration 全局值。")]
        [Min(0f)] public float fadeOverride = 0f;
        [Tooltip("该曲目的音量倍率（相对全局 BGM 音量）。1=默认，>1 放大、<1 减小；用于平衡不同素材响度。")]
        [Range(0f, 2f)] public float volumeScale = 1f;
    }

    [Serializable]
    public class WaveTier
    {
        [Tooltip("玩家侧波次编号（从 1 开始）。该波次开始时若匹配则切换到此曲。")]
        [Min(1)] public int waveNumber = 1;
        [Tooltip("该波次的 BGM（留空视为未配置）。")]
        public Slot slot;
    }

    [Header("Waves 阶段（按波次配置，用户逐波逻辑）")]
    [Tooltip("逐波 BGM：某波次匹配到条目 → 切到该曲；未匹配 → 保持当前曲不切。\n列表为空 = 回退旧行为（所有波次共用下方 combat 槽）。")]
    public List<WaveTier> waveTiers = new List<WaveTier>();

    [Header("Phase 层（RunPhase → BGM）")]
    [Tooltip("Waves 波次战斗曲（仅在 waveTiers 为空时作为兜底；使用 waveTiers 后此槽不参与）。")]
    public Slot combat;
    [Tooltip("Choice 波清场·选卡曲。")]
    public Slot choice;
    [Tooltip("Final 最终阶段曲。")]
    public Slot final;
    [Tooltip("Result 结算曲。")]
    public Slot result;
    [Tooltip("Failed 失败曲；留空 = 复用 result 槽。")]
    public Slot fail;

    [Header("Override 层（状态覆盖，压栈优先于 Phase）")]
    [Tooltip("灵魂态曲（离身进入灵魂态 Push，附身 Pop）。")]
    public Slot soul;
    [Tooltip("精英曲（精英投放 Push，本波结束 Pop；语义=精英波）。")]
    public Slot elite;

    /// <summary>
    /// 按玩家侧波次编号（1-based）匹配 waveTier；无匹配（未配置该波）返回 null。
    /// action=Play 要求 clip 非空才视为有效条目；Inherit/Stop 不要求 clip。
    /// </summary>
    public WaveTier TryGetWaveTier(int waveNumber)
    {
        if (waveNumber < 1) return null;
        for (int i = 0; i < waveTiers.Count; i++)
        {
            var t = waveTiers[i];
            if (t == null || t.waveNumber != waveNumber || t.slot == null) continue;
            if (t.slot.action == BgmAction.Play && t.slot.clip == null) continue; // Play 但无 clip：无效条目
            return t;
        }
        return null;
    }

    /// <summary>按 RunPhase 取 Phase 层槽位。Opening/Tutorial 无映射返回 false（=Phase 层清除，回落 Scene 层）。</summary>
    public bool TryGetPhase(RunPhase phase, out Slot slot)
    {
        switch (phase)
        {
            case RunPhase.Waves: slot = combat; return true;   // Waves 优先走 TryGetWaveTier；此兜底仅供 waveTiers 为空时
            case RunPhase.Choice: slot = choice; return true;
            case RunPhase.Final: slot = final; return true;
            case RunPhase.Result: slot = result; return true;
            case RunPhase.Failed:
                slot = fail != null && fail.clip != null ? fail : result; // fail 留空复用 result
                return true;
            default:
                slot = null;
                return false;
        }
    }
}
