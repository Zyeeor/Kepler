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
        [Tooltip("蓄力分档：按蓄力程度二选一——clips[0]=低蓄力(Light)、clips[1]=高蓄力(Heavy)，阈值 heavyCastThreshold。用于蓄力类普攻（如怠惰蓄力炮）。")]
        ChargeTiered = 2,
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
        [Tooltip("蓄力分档阈值（normalized charge 0~1）：仅 pickMode=ChargeTiered 生效。charge01 >= 阈值 → 播 clips[1]（高蓄力），否则播 clips[0]（低蓄力）。")]
        [Range(0f, 1f)] public float heavyCastThreshold = 0.5f;
        [Tooltip("循环音效：true = 持续技能（如嫉妒激光，按住持续施放）用，由调用方 Start/Stop 控制启停；false = 一次性施放音（默认）。普通一次性技能保持 false。")]
        public bool loop = false;
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
        [Tooltip("回归音（可选）：两段式位移技能的第二段（如色欲魅影换位的瞬移回归）。clips 为空 = 回归段回退到去程音组。")]
        public ClipSet returnSet = new ClipSet();

        public Entry()
        {
            // 默认空间化：敌方（AI）3D 距离衰减，附身（玩家控制）2D 恒定音量；均可在编辑器里单独改
            enemy.spatialMode = SpatialMode.Positional3D;
            possessed.spatialMode = SpatialMode.Flat2D;
            returnSet.spatialMode = SpatialMode.Positional3D;
        }
    }

    /// <summary>
    /// 召唤物（无人机/木灵）攻击音条目：按召唤者七罪类型查表。
    /// 与技能条目同构（敌我分轨 + 空间化 + 音量/音高 + 候选音源列表随机播放），
    /// 但不占用技能类别维度（召唤物不是怪物本体的位移/普攻/技能）。
    /// </summary>
    [Serializable]
    public class DroneEntry
    {
        [Tooltip("召唤者七罪类型（无人机归属哪个怪，如 Sloth 怠惰的木灵）。")]
        public SinType sin = SinType.None;
        [Tooltip("敌我分轨：开 = 敌方/附身各配一组音源；关 = 敌我共用 enemy 组。")]
        public bool splitSides = false;
        [Tooltip("敌方（AI 控制）音源组。")]
        public ClipSet enemy = new ClipSet();
        [Tooltip("附身（玩家控制）音源组；splitSides=false 时忽略。")]
        public ClipSet possessed = new ClipSet();

        public DroneEntry()
        {
            enemy.spatialMode = SpatialMode.Positional3D;
            possessed.spatialMode = SpatialMode.Flat2D;
        }
    }

    [Tooltip("条目列表（编辑器按七罪分区显示；同（sin, kind）重复时首个生效）。")]
    public List<Entry> entries = new List<Entry>();

    [Tooltip("召唤物（无人机/木灵）攻击音条目列表（按召唤者七罪分区显示；同 sin 重复时首个生效）。")]
    public List<DroneEntry> droneEntries = new List<DroneEntry>();

    Dictionary<(SinType, EnemyAbility.AbilityType), Entry> _cache;
    Dictionary<SinType, DroneEntry> _droneCache;

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

    /// <summary>查召唤物（无人机）攻击音：按召唤者七罪类型查表（构建一次缓存；sin=None 返回 false）。</summary>
    public bool TryGetDrone(SinType sin, out DroneEntry entry)
    {
        entry = null;
        if (sin == SinType.None) return false;
        if (_droneCache == null)
        {
            _droneCache = new Dictionary<SinType, DroneEntry>();
            foreach (var e in droneEntries)
            {
                if (e == null || e.sin == SinType.None) continue;
                if (!_droneCache.ContainsKey(e.sin)) _droneCache[e.sin] = e; // 首个生效
            }
        }
        return _droneCache.TryGetValue(sin, out entry);
    }

    void OnEnable()
    {
        // 旧资产数据迁移：returnSet 是后加字段，旧序列化数据里不存在。
        // [Serializable] 类反序列化不调用构造函数/字段初始化器，故旧条目 returnSet 为 null，这里补默认实例。
        // 幂等：补过后磁盘数据可能仍未写回（需用户改动后保存），再次加载会再补一次，无副作用。
        if (entries != null)
        {
            foreach (var e in entries)
            {
                if (e != null && e.returnSet == null)
                    e.returnSet = new ClipSet { spatialMode = SpatialMode.Positional3D };
            }
        }
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
        // 重复无人机（sin）告警
        var droneSeen = new HashSet<SinType>();
        foreach (var e in droneEntries)
        {
            if (e == null || e.sin == SinType.None) continue;
            if (!droneSeen.Add(e.sin))
                Debug.LogWarning($"[MonsterSkillAudioConfig] 重复无人机条目 sin={e.sin}，运行时取首个，请清理资产。");
        }
        _cache = null; // 编辑期数据变更后缓存失效
        _droneCache = null;
    }
}
