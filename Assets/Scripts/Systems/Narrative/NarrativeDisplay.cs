using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Display 静态门面：TextCatalog 的 TODO(display-profile) 挂钩实现 + 载体查询。
/// 无 Mono、零场景依赖。未就绪（未播放/资产缺失）时一切解析回退 neutral——零回归。
/// </summary>
public static class NarrativeDisplay
{
    static NarrativeDisplayProfile _profile;
    static readonly Dictionary<NarrativeCarrier, TextLinePreference> _carrierOverride = new Dictionary<NarrativeCarrier, TextLinePreference>();

    /// <summary>门面是否就绪。</summary>
    public static bool IsReady => _profile != null;

    /// <summary>已获得 Card 是否随 Access 切换即时刷新（契约 §4 配置项；CoreChoiceUI 订阅刷新用）。</summary>
    public static bool SyncOwnedCardsOnAccessChange
    {
        get
        {
            var profile = Profile;
            return profile != null && profile.syncOwnedCardsOnAccessChange;
        }
    }

    /// <summary>当前生效偏好变化事件（Card UI 等即时换皮载体订阅）。</summary>
    public static event Action OnDisplayPreferenceChanged;

    static NarrativeDisplayProfile Profile
    {
        get
        {
            if (_profile == null)
                _profile = Resources.Load<NarrativeDisplayProfile>("Narrative/NarrativeDisplayProfile");
            return _profile;
        }
    }

    /// <summary>按载体取偏好（覆盖 > 全局默认；FollowAccess → accessLineMap[当前 Access]）。</summary>
    public static TextLinePreference EffectiveLine(NarrativeCarrier carrier)
    {
        var profile = Profile;
        if (profile == null) return TextLinePreference.Neutral;

        if (_carrierOverride.TryGetValue(carrier, out var runtimePref))
            return ResolveFollow(runtimePref, profile);

        foreach (var o in profile.carrierOverrides)
            if (o != null && o.carrier == carrier)
                return ResolveFollow(o.preference, profile);

        return ResolveFollow(profile.globalDefault, profile);
    }

    static TextLinePreference ResolveFollow(TextLinePreference pref, NarrativeDisplayProfile profile)
    {
        if (pref != TextLinePreference.FollowAccess) return pref;
        var access = NarrativeScheduler.Instance != null ? NarrativeScheduler.Instance.Access.Current : NarrativeAccess.A0;
        foreach (var rule in profile.accessLineMap)
            if (rule != null && rule.access == access) return rule.line;
        return TextLinePreference.Mythic; // 默认 A 线
    }

    /// <summary>Cue.DisplayModeResult 写入：运行期覆盖指定载体偏好（Run-local，不持久化）。</summary>
    public static void SetCarrierOverride(NarrativeCarrier carrier, TextLinePreference pref)
    {
        _carrierOverride[carrier] = pref;
        OnDisplayPreferenceChanged?.Invoke();
    }

    /// <summary>Access 变化时广播刷新（NarrativeScheduler.Access.OnAccessChanged 订阅）。</summary>
    public static void NotifyAccessChanged() => OnDisplayPreferenceChanged?.Invoke();

    /// <summary>Debug/测试用：清运行期载体覆盖。</summary>
    public static void ClearRuntimeOverrides()
    {
        _carrierOverride.Clear();
        OnDisplayPreferenceChanged?.Invoke();
    }
}
