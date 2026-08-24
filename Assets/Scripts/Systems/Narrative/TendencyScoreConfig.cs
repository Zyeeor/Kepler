using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>行为风格度量（评分器词库规则命中）。</summary>
public enum BehaviorMetric
{
    VoluntaryReleaseRatio, LowHealthReleaseCount, BulletTimePerMinute,
    ElitePossessCount, DistinctSins, SingleSinDominance,
}

public enum ComparisonOp { Gte = 0, Lte = 1 }

/// <summary>
/// First Clear 评分配置（SO）：权重/阈值/平局/词库/ID 模板全部配置化（契约 §8）。
/// 得分公式结构写死在 RunTendencyScorer（权重在配置），不做医学/人格诊断（词库即约束）。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/Narrative/Tendency Score Config", fileName = "TendencyScoreConfig")]
public class TendencyScoreConfig : ScriptableObject
{
    [Header("Per-Sin 倾向权重（各 Sin 独立累加）")]
    public float wControlSeconds = 1.0f;
    public float wPossession = 15f;
    public float wAbilityUse = 2f;
    public float wCardInvestment = 25f;
    public float wKills = 5f;

    [Header("次倾向入选门槛（主/次分差比例；低于则次倾向=None）")]
    [Range(0f, 1f)] public float secondaryMinRatio = 0.35f;
    [Tooltip("平局处理：同分时按控制时长决胜")]
    public bool tieBreakByControlSeconds = true;

    [Header("行为风格词库")]
    public List<BehaviorRule> behaviorRules = new List<BehaviorRule>();
    public string fallbackBehaviorTextKey = "nar.firstclear.style.balanced";

    [Header("Model / Version / Instance 模板")]
    public string modelIdTemplate = "CARRIER-{SIN}-V{VER}-I{N}";

    [Serializable]
    public class BehaviorRule
    {
        public string ruleId;
        public BehaviorMetric metric;
        public ComparisonOp op;
        public float threshold;
        public string textKey;
        public int priority;
    }
}
