using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局音效 ID（统一通道，显式编号：成员追加只加在尾部，禁止插入/复用已删除编号——序列化安全）。
/// 按三个类别分组（SfxCategory，SfxBank 编辑器分区显示的划分依据）：
///   GameEvent 游戏事件：波次/附身/系统侧事件（AudioEventBinder 订阅），策划只配 clip，挂点已接线；
///   UI 界面：点击/选卡，走 UI 独立通道；
///   Combat 战斗：受击/击杀/移动等（战斗负责人直调，挂点见交接清单）。
/// 怪物技能施放音不在本枚举（独立资产 MonsterSkillAudioConfig，七罪×技能类别）。
/// 触发点：AudioManager.Instance?.Play(SfxId.X, pos)。
/// 新增音效：在对应类别块尾部加成员（编号从 31 起）+ SfxBank 资产拖 clip + 触发点一行 Play——AudioManager 零改动。
/// </summary>
public enum SfxId
{
    None = 0,

    // ── GameEvent 游戏事件（波次/附身）──
    WaveStart = 1,
    WaveClear = 2,
    AllWavesComplete = 3,
    PossessionStart = 4,

    // ── UI 界面音效（独立 UI 通道）──
    UiClick = 5,
    CardOpen = 6,
    CardSelect = 7,
    CardReroll = 8,

    // ── GameEvent 游戏事件（系统侧，AudioEventBinder 订阅）──
    PossessionEnd = 9,
    PossessBodyDied = 10,
    CorpseWindow = 11,
    SoulEnter = 12,
    SoulDeath = 13,
    BulletTimeStart = 14,
    BulletTimeEnd = 15,
    FinalBegin = 16,
    FinalClear = 17,
    FinalPhaseChange = 18,   // 占位：Final 玩法未实现，接口预留
    ShrineProximity = 27,    // 神龛接近提示（PossessionBodyProvider 提供）
    ShrineProvide = 28,      // 神龛提供躯体

    // ── Combat 战斗音效（战斗负责人直调；挂点见交接清单）──
    BodyHit = 19,
    BodyDurabilityLow = 20,
    EnemyFatal = 21,
    CorpseAvailable = 22,
    TargetLock = 23,         // 锁定机制未实现，先占位
    MovementLoop = 24,       // 循环音：StartSfxLoop/StopSfxLoop
    EliteSpawn = 25,
    Hazard = 26,

    // 怪物技能施放音已拆出（MonsterSkillAudioConfig，七罪×技能类别），不在此枚举内。
    // 编号 29/30 已随技能音效拆分弃用（按约定不复用），新成员从 31 起追加。
}

/// <summary>音效类别（SfxBank 编辑器分区显示 + 策划配置导航用；不影响运行时行为）。</summary>
public enum SfxCategory
{
    [Tooltip("游戏事件：波次开始/结束、附身、子弹时间、结算等系统事件音效。")]
    GameEvent = 0,
    [Tooltip("UI 界面：按钮点击、选卡等界面音效（独立 UI 通道）。")]
    UI = 1,
    [Tooltip("战斗音效：受击、击杀、移动、场景危害等局内战斗音效。")]
    Combat = 2,
}

/// <summary>标记 string 字段为 SfxId 名称引用：Inspector 显示为 SfxId 下拉（策划免背枚举名）。</summary>
public class SfxIdNameAttribute : PropertyAttribute { }

/// <summary>
/// 音效映射表（ScriptableObject，策划编辑）：SfxId → clip/音量/节流/通道/pitch。
/// 加载：AudioManager.Awake 时 sfxBank 字段为空 → Resources.Load&lt;SfxBank&gt;("Audio/SfxBank") 兜底。
/// 语义：id 未配置或 clip 为空 → 静默跳过（属设计行为，Debug 面板可查缺失清单）。
/// 新增一类音效：SfxId 尾部加成员 + 本资产加条目拖 clip + 触发点一行 Play——AudioManager 零改动。
/// </summary>
[CreateAssetMenu(menuName = "Kepler/Audio/SfxBank", fileName = "SfxBank")]
public class SfxBank : ScriptableObject
{
    public enum Channel
    {
        [Tooltip("战斗/世界音效：SFX 池播放（传入 worldPos 且 prefer3D 时 3D 定位）。")]
        World = 0,
        [Tooltip("UI 音效：独立 UI 源一路（不受 SFX 池抢占）。")]
        Ui = 1,
    }

    [Serializable]
    public class Entry
    {
        [Tooltip("音效 ID（对应 SfxId 枚举成员）。")]
        public SfxId id;
        [Tooltip("音频剪辑；留空 = 该音效静默（设计行为，不报错）。")]
        public AudioClip clip;
        [Tooltip("基础音量倍率（0~2，乘全局 SFX/UI 音量）。")]
        [Range(0f, 2f)] public float volumeScale = 1f;
        [Tooltip("重播最小间隔覆盖（秒）；<=0 用 CombatAudioManager.sfxMinInterval 全局值。")]
        [Min(0f)] public float minInterval = 0f;
        [Tooltip("音高（0.5~1.5）。")]
        [Range(0.5f, 1.5f)] public float pitch = 1f;
        [Tooltip("播放通道：World=SFX 池 / Ui=UI 独立源。")]
        public Channel channel = Channel.World;
        [Tooltip("World 通道下：true=调用方传 worldPos 时用 3D 空间定位（随距离衰减）。")]
        public bool prefer3D = true;
    }

    [Tooltip("音效条目列表（编辑器按类别分区显示：游戏事件/UI/战斗/技能）。")]
    public List<Entry> entries = new List<Entry>();

    /// <summary>SfxId → 类别（SfxBank 编辑器分区显示依据；新增 SfxId 成员须在此补映射）。</summary>
    public static SfxCategory GetCategory(SfxId id)
    {
        switch (id)
        {
            // GameEvent 游戏事件
            case SfxId.WaveStart:
            case SfxId.WaveClear:
            case SfxId.AllWavesComplete:
            case SfxId.PossessionStart:
            case SfxId.PossessionEnd:
            case SfxId.PossessBodyDied:
            case SfxId.CorpseWindow:
            case SfxId.SoulEnter:
            case SfxId.SoulDeath:
            case SfxId.BulletTimeStart:
            case SfxId.BulletTimeEnd:
            case SfxId.FinalBegin:
            case SfxId.FinalClear:
            case SfxId.FinalPhaseChange:
            case SfxId.ShrineProximity:
            case SfxId.ShrineProvide:
                return SfxCategory.GameEvent;

            // UI 界面
            case SfxId.UiClick:
            case SfxId.CardOpen:
            case SfxId.CardSelect:
            case SfxId.CardReroll:
                return SfxCategory.UI;

            // Combat 战斗
            case SfxId.BodyHit:
            case SfxId.BodyDurabilityLow:
            case SfxId.EnemyFatal:
            case SfxId.CorpseAvailable:
            case SfxId.TargetLock:
            case SfxId.MovementLoop:
            case SfxId.EliteSpawn:
            case SfxId.Hazard:
                return SfxCategory.Combat;

            default:
                return SfxCategory.GameEvent;
        }
    }

    Dictionary<SfxId, Entry> _cache;

    /// <summary>查询条目（懒构建缓存；重复 id 取首个并 LogWarning）。</summary>
    public bool TryGet(SfxId id, out Entry entry)
    {
        if (_cache == null) BuildCache();
        return _cache.TryGetValue(id, out entry);
    }

    /// <summary>缓存失效（编辑期改表后由 OnValidate 调用；运行时资产不可变无需调用）。</summary>
    public void InvalidateCache() => _cache = null;

    void BuildCache()
    {
        _cache = new Dictionary<SfxId, Entry>();
        if (entries == null) return;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            if (e.id == SfxId.None) continue;
            if (!_cache.ContainsKey(e.id))
            {
                _cache[e.id] = e;
            }
            else
            {
                Debug.LogWarning($"[SfxBank] 重复 id {e.id}（取首个条目），请清理资产。", this);
            }
        }
    }

    void OnValidate()
    {
        _cache = null;
        // 编辑期即时提示重复 id（策划拖重立刻可见，不等运行时首次查询）
        var seen = new HashSet<SfxId>();
        if (entries == null) return;
        foreach (var e in entries)
        {
            if (e == null || e.id == SfxId.None) continue;
            if (!seen.Add(e.id))
                Debug.LogWarning($"[SfxBank] 重复 id {e.id}，运行时取首个条目，请清理资产。", this);
        }
    }
}
