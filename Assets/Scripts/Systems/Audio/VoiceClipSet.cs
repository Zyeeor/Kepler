using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 旁白音频映射表（ScriptableObject，音频/策划编辑）：audioId → 双通道 clip。
/// 数据契约（Narrative Baseline §6）：未来 Cue SO 的 Audio ID 字段（string）即本表条目 key，
/// 经 TryGet(audioId, channel) 解析后走 AudioManager.PlayVoice。
/// 完整叙事调度系统（队列/优先级/BusyPolicy/字幕联动）另立需求，本期只落接口与数据契约。
/// 加载：AudioManager.PlayVoice 首次调用时 Resources.Load&lt;VoiceClipSet&gt;("Audio/VoiceClipSet")。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/Audio/VoiceClipSet", fileName = "VoiceClipSet")]
public class VoiceClipSet : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("音频 ID（Cue 数据契约的 Audio ID 字段引用此值）。")]
        public string audioId;
        [Tooltip("神谕叙事版（VoiceChannel.Mythic）。")]
        public AudioClip mythic;
        [Tooltip("系统电子版（VoiceChannel.System）。")]
        public AudioClip system;
    }

    [Tooltip("旁白条目列表（本期可为空，等叙事资产）。")]
    public List<Entry> entries = new List<Entry>();

    Dictionary<string, Entry> _cache;

    /// <summary>按 audioId + 通道取 clip。未配置/通道 clip 空返回 false（静默，不阻塞文本与流程）。</summary>
    public bool TryGet(string audioId, VoiceChannel channel, out AudioClip clip)
    {
        clip = null;
        if (string.IsNullOrEmpty(audioId)) return false;
        if (_cache == null)
        {
            _cache = new Dictionary<string, Entry>(StringComparer.Ordinal);
            if (entries != null)
                foreach (var e in entries)
                    if (e != null && !string.IsNullOrEmpty(e.audioId) && !_cache.ContainsKey(e.audioId))
                        _cache[e.audioId] = e;
        }
        if (!_cache.TryGetValue(audioId, out var entry)) return false;
        clip = channel == VoiceChannel.Mythic ? entry.mythic : entry.system;
        return clip != null;
    }

    void OnValidate() => _cache = null;
}
