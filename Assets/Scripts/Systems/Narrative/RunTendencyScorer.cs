using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>First Clear 倾向评分结果。</summary>
public class RunTendencyResult
{
    public SinType primary = SinType.None;
    public SinType secondary = SinType.None;
    public string behaviorTextKey;
    public string modelIdText;
}

/// <summary>
/// 纯静态评分函数（无副作用）：消费 RunStatsData + TendencyScoreConfig → Functional Summary。
/// 得分公式写死（结构），权重全在配置（数值）。data/config 任一 null → 返回 null（调用方走兜底）。
/// </summary>
public static class RunTendencyScorer
{
    public static RunTendencyResult Score(RunStatsData data, TendencyScoreConfig config)
    {
        if (data == null || config == null) return null;

        // 1) Per-Sin 得分
        var scores = new Dictionary<SinType, float>();
        var controlSeconds = new Dictionary<SinType, float>();
        foreach (var s in data.perSin)
        {
            if (s == null || s.sin == SinType.None) continue;
            float score = config.wControlSeconds * s.controlSeconds
                        + config.wPossession * s.possessionCount
                        + config.wAbilityUse * (s.movementCount + s.attackCount + s.specialCount)
                        + config.wCardInvestment * s.cardInvestmentCount
                        + config.wKills * s.kills;
            if (score <= 0f) continue;
            scores[s.sin] = score;
            controlSeconds[s.sin] = s.controlSeconds;
        }

        if (scores.Count == 0) return null; // 全 0 → 数据不足，走兜底

        // 2) 主/次倾向（平局按控制时长决胜）
        var sorted = new List<SinType>(scores.Keys);
        sorted.Sort((a, b) =>
        {
            int c = scores[b].CompareTo(scores[a]);
            if (c != 0) return c;
            if (config.tieBreakByControlSeconds)
                return controlSeconds[b].CompareTo(controlSeconds[a]);
            return 0;
        });

        var result = new RunTendencyResult();
        result.primary = sorted[0];
        if (sorted.Count >= 2 && scores[sorted[1]] >= scores[sorted[0]] * config.secondaryMinRatio)
            result.secondary = sorted[1];

        // 3) 行为风格（多规则命中取最高优先级）
        result.behaviorTextKey = PickBehavior(data, config);

        // 4) Model/Version/Instance 模板
        result.modelIdText = BuildModelId(result, config, data);

        return result;
    }

    static string PickBehavior(RunStatsData data, TendencyScoreConfig config)
    {
        string best = null;
        int bestPrio = int.MinValue;
        foreach (var rule in config.behaviorRules)
        {
            if (rule == null) continue;
            if (!EvalMetric(data, rule)) continue;
            if (rule.priority > bestPrio)
            {
                bestPrio = rule.priority;
                best = rule.textKey;
            }
        }
        return string.IsNullOrEmpty(best) ? config.fallbackBehaviorTextKey : best;
    }

    static bool EvalMetric(RunStatsData data, TendencyScoreConfig.BehaviorRule rule)
    {
        float value;
        switch (rule.metric)
        {
            case BehaviorMetric.VoluntaryReleaseRatio:
                value = data.totalPossessions > 0 ? (float)data.voluntaryReleases / data.totalPossessions : 0f;
                break;
            case BehaviorMetric.LowHealthReleaseCount:
                value = data.lowHealthReleases;
                break;
            case BehaviorMetric.BulletTimePerMinute:
                value = data.runDurationSeconds > 0f ? data.bulletTimeCount / (data.runDurationSeconds / 60f) : 0f;
                break;
            case BehaviorMetric.ElitePossessCount:
                value = data.elitePossessionCount;
                break;
            case BehaviorMetric.DistinctSins:
                value = data.distinctSinsUsed;
                break;
            case BehaviorMetric.SingleSinDominance:
                value = ComputeDominance(data);
                break;
            default:
                return false;
        }
        return rule.op == ComparisonOp.Gte ? value >= rule.threshold : value <= rule.threshold;
    }

    static float ComputeDominance(RunStatsData data)
    {
        float total = 0f, max = 0f;
        foreach (var s in data.perSin)
        {
            if (s == null || s.sin == SinType.None) continue;
            total += s.possessionCount;
            if (s.possessionCount > max) max = s.possessionCount;
        }
        return total > 0f ? max / total : 0f;
    }

    static string BuildModelId(RunTendencyResult result, TendencyScoreConfig config, RunStatsData data)
    {
        if (result.primary == SinType.None) return config.modelIdTemplate;
        string sin = RunStatsUtil.WireName(result.primary).ToUpperInvariant();
        int ver = NarrativeProfileStore.Data != null ? NarrativeProfileStore.Data.certificationCount : 1;
        string n = "1"; // 占位序号（Production Open）
        return config.modelIdTemplate
            .Replace("{SIN}", sin)
            .Replace("{VER}", ver.ToString())
            .Replace("{N}", n);
    }
}
