using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Access 推进规则表（SO）：A1-A4 推进规则资产化（"由哪个事件推进"=策划配置，非硬编码）。
/// 与 Cue 共用 Trigger 评估模型（事件+nth+条件组 → 目标 Access）。
/// Resources/Narrative/NarrativeAccessProfile 懒加载；缺失 → Access 停 A0（Display 全 Mythic，系统仍可玩）。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/Narrative/Access Profile", fileName = "NarrativeAccessProfile")]
public class NarrativeAccessProfile : ScriptableObject
{
    [Serializable]
    public class AccessRule
    {
        [Tooltip("规则 ID（Debug 原因展示用）")]
        public string ruleId;
        [Tooltip("触发定义（与 Cue 完全同构：事件+nth+条件组）")]
        public NarrativeTrigger trigger;
        [Tooltip("命中 → RequestAdvance(target)")]
        public NarrativeAccess targetAccess;
        [Tooltip("只推进一次（命中后失效；Access 单调使重复推进无副作用，此为性能优化）")]
        public bool fireOnce = true;
    }

    public List<AccessRule> rules = new List<AccessRule>();

    static NarrativeAccessProfile _instance;
    public static NarrativeAccessProfile Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<NarrativeAccessProfile>("Narrative/NarrativeAccessProfile");
            return _instance;
        }
    }

    public void InvalidateCache() => _instance = null;
}
