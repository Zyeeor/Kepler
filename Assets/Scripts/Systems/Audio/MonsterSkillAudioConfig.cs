using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物技能施放音效配置（独立资产，与 SfxBank 解耦）：
///   配置维度 =（七罪类型 × 技能类别），策划从"哪种怪 → 哪种技能（位移/普攻/技能）→ 拖音源"。
///   触发：EnemyAbility.Trigger 施放成功时，若能力自身未配置 castAudioName（per-ability override），
///   则按 owner.sinType + 技能类别查本表播放（CombatAudioManager.PlayCastAudio）。
/// 资产：Assets/Resources/Audio/MonsterSkillAudioConfig.asset（缺失时全部静默，设计行为）。
/// 只覆盖"技能释放"音效；命中音仍走能力字段 hitAudioName。
/// </summary>
[CreateAssetMenu(fileName = "MonsterSkillAudioConfig", menuName = "Kepler/Audio/Monster Skill Audio Config")]
public class MonsterSkillAudioConfig : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("七罪类型（怪物身份）。")]
        public SinType sin = SinType.None;
        [Tooltip("技能类别：位移 / 普攻 / 技能（Passive 不播放，忽略）。")]
        public EnemyAbility.AbilityType kind = EnemyAbility.AbilityType.BasicAttack;
        [Tooltip("施放音源；空 = 静默。")]
        public AudioClip clip;
        [Tooltip("音量倍率（0~2）。")]
        [Range(0f, 2f)] public float volumeScale = 1f;
        [Tooltip("音高（0.5~1.5）。")]
        [Range(0.5f, 1.5f)] public float pitch = 1f;
    }

    [Tooltip("条目列表（编辑器按七罪分区显示；同（sin, kind）重复时首个生效）。")]
    public List<Entry> entries = new List<Entry>();

    Dictionary<(SinType, EnemyAbility.AbilityType), Entry> _cache;

    /// <summary>查表（构建一次缓存；sin=None / Passive 一律返回 false）。</summary>
    public bool TryGet(SinType sin, EnemyAbility.AbilityType kind, out Entry entry)
    {
        entry = null;
        if (sin == SinType.None || kind == EnemyAbility.AbilityType.Passive) return false;
        if (_cache == null)
        {
            _cache = new Dictionary<(SinType, EnemyAbility.AbilityType), Entry>();
            foreach (var e in entries)
            {
                if (e == null || e.sin == SinType.None || e.kind == EnemyAbility.AbilityType.Passive) continue;
                var key = (e.sin, e.kind);
                if (!_cache.ContainsKey(key)) _cache[key] = e; // 首个生效
            }
        }
        return _cache.TryGetValue((sin, kind), out entry);
    }

    void OnValidate()
    {
        // 重复（sin, kind）告警（不改数据，仅提示策划清理）
        var seen = new HashSet<(SinType, EnemyAbility.AbilityType)>();
        foreach (var e in entries)
        {
            if (e == null || e.sin == SinType.None) continue;
            var key = (e.sin, e.kind);
            if (!seen.Add(key))
                Debug.LogWarning($"[MonsterSkillAudioConfig] 重复条目 sin={e.sin} kind={e.kind}，运行时取首个，请清理资产。");
        }
        _cache = null; // 编辑期数据变更后缓存失效
    }
}
