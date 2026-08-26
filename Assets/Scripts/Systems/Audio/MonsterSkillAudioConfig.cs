using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物技能施放音效配置（独立资产，与 SfxBank 解耦）：
///   配置维度 =（七罪类型 × 技能类别），策划从"哪种怪 → 哪种技能（位移/普攻/技能）→ 拖音源"。
///   每个（sin, kind）条目支持：
///     - 随机多音源：每组候选 clip 列表按 pickMode 随机取一条（Random 纯随机 / NoRepeat 不连续重复）；
///     - 敌我分轨：splitSides 开 = 敌方（AI）与附身（玩家控制）各配一组独立音源/音量/音高。
///   触发：EnemyAbility.Trigger 施放成功时，若能力自身未配置 castAudioName（per-ability override），
///   则按 owner.sinType + 技能类别查本表播放（CombatAudioManager.PlayCastAudio）。
/// 资产：Assets/Resources/Audio/MonsterSkillAudioConfig.asset（缺失时全部静默，设计行为）。
/// 只覆盖"技能释放"音效；命中音仍走能力字段 hitAudioName。
/// </summary>
[CreateAssetMenu(fileName = "MonsterSkillAudioConfig", menuName = "Kepler/Audio/Monster Skill Audio Config")]
public class MonsterSkillAudioConfig : ScriptableObject
{
    /// <summary>音源组选取规则：一组候选 clip 里怎么挑一条播。</summary>
    public enum ClipPickMode
    {
        [Tooltip("纯随机：每次等概率随机取一条，可能连续两次同一音。")]
        Random = 0,
        [Tooltip("不连续重复：排除上一次播放的那一条（按列表条目去重；重复放同 clip 可加权）。")]
        NoRepeat = 1,
    }

    /// <summary>空间化模式：音效走 2D（恒定音量，不随距离衰减）还是 3D（随距离衰减）。</summary>
    public enum SpatialMode
    {
        [Tooltip("3D：随距离衰减，空间定位（远处敌方 / 环境音推荐）。")]
        Positional3D = 0,
        [Tooltip("2D：恒定音量，不随距离衰减（玩家自身 / 附身怪音推荐）。")]
        Flat2D = 1,
    }

    /// <summary>音源组：一组候选 clip + 选取规则 + 音量/音高 + 空间化。敌我各挂一组。</summary>
    [Serializable]
    public class ClipSet
    {
        [Tooltip("候选音源列表；留空 = 静默。重复放同一 clip = 提高其被选中权重（NoRepeat 按条目去重，不消除权重）。")]
        public List<AudioClip> clips = new List<AudioClip>();
        [Tooltip("选取规则：Random 纯随机 / NoRepeat 不连续重复（按条目去重）。")]
        public ClipPickMode pickMode = ClipPickMode.NoRepeat;
        [Tooltip("音量倍率（0~1 = 0~100%）。100% 即满音量（AudioSource 物理上限，无法超过）；想更响请从素材响度或全局 SFX 音量入手。")]
        [Range(0f, 1f)] public float volumeScale = 1f;
        [Tooltip("音高（0.5~1.5）。")]
        [Range(0.5f, 1.5f)] public float pitch = 1f;
        [Tooltip("空间化：3D 随距离衰减 / 2D 恒定音量（附身/玩家音推荐 2D）。")]
        public SpatialMode spatialMode = SpatialMode.Positional3D;
    }

    [Serializable]
    public class Entry
    {
        [Tooltip("七罪类型（怪物身份）。")]
        public SinType sin = SinType.None;
        [Tooltip("技能类别：位移 / 普攻 / 技能（Passive 不播放，忽略）。")]
        public EnemyAbility.AbilityType kind = EnemyAbility.AbilityType.BasicAttack;
        [Tooltip("敌我分轨：开 = 敌方/附身各配一组音源；关 = 敌我共用 enemy 组。")]
        public bool splitSides = false;
        [Tooltip("敌方（AI 控制）音源组。")]
        public ClipSet enemy = new ClipSet();
        [Tooltip("附身（玩家控制）音源组；splitSides=false 时忽略。")]
        public ClipSet possessed = new ClipSet();

        public Entry()
        {
            // 默认空间化：敌方（AI）3D 距离衰减，附身（玩家控制）2D 恒定音量；均可在编辑器里单独改
            enemy.spatialMode = SpatialMode.Positional3D;
            possessed.spatialMode = SpatialMode.Flat2D;
        }
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
