using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗音效通道控制器（挂 AudioManager 下，SFX/World 通道）：
///   池（sfxPoolSize 并发上限）+ per-clip / per-id 节流 + 3D 空间参数 + 循环音效租借。
/// 统一播放走 Play(SfxId)（查 Owner.SfxBank 条目；channel=Ui 分流 Owner.UiController）。
/// 怪物技能施放音走 PlayCastAudio（查 Owner.MonsterSkillAudioConfig：七罪 × 技能类别）。
/// 兼容层：能力基类按字符串名调用（castAudioName / hitAudioName = SfxId 成员名，大小写不敏感）
/// → 静态 Play(string, pos) 转发本实例；解析失败 / 未配置均静默并计入缺失名单（AudioDebugPanel 可见）。
/// </summary>
public class CombatAudioManager : AudioChannelController
{
    /// <summary>实例（AudioManager 装配时注册；静态兼容层与 Debug 面板用）。</summary>
    public static CombatAudioManager Instance { get; private set; }

    [Header("SFX 池")]
    [Tooltip("音效池大小：并发音效数上限。")]
    [Min(1)] public int sfxPoolSize = 6;
    [System.NonSerialized] public AudioSource[] sfxPool;   // 运行时自动创建（非序列化，非配置项）
    [Tooltip("同 clip 最小重播间隔（秒）：窗口内重复触发直接丢弃，防高频战斗事件（受击/技能命中）爆音。")]
    [Min(0f)] public float sfxMinInterval = 0.06f;

    [Header("SFX 3D 空间参数")]
    [Tooltip("3D 音效最小距离（衰减起点）。")]
    [Min(0f)] public float sfx3DMinDistance = 2f;
    [Tooltip("3D 音效最大距离（衰减终点）。")]
    [Min(0.1f)] public float sfx3DMaxDistance = 28f;

    /// <summary>SFX 池轮询指针（全忙时均匀覆盖，消灭固定 pool[0] 热点）。</summary>
    int _sfxCursor;
    /// <summary>循环音效句柄表（per-id 单实例；移动音用）。</summary>
    readonly Dictionary<SfxId, AudioSource> _sfxLoops = new Dictionary<SfxId, AudioSource>();
    /// <summary>per-clip 最近播放时间（节流用）。</summary>
    readonly Dictionary<AudioClip, float> _lastSfxTime = new Dictionary<AudioClip, float>();
    /// <summary>per-SfxId 最近播放时间（SfxBank 条目级节流用）。</summary>
    readonly Dictionary<SfxId, float> _lastSfxIdTime = new Dictionary<SfxId, float>();

    /// <summary>未注册名（字符串调用解析失败；name→计数）。</summary>
    static readonly Dictionary<string, int> MissingSfxNames = new Dictionary<string, int>();
    /// <summary>bank 缺失条目（id 未配置或 clip 空；id→计数）。</summary>
    static readonly Dictionary<SfxId, int> MissingSfxIds = new Dictionary<SfxId, int>();
    /// <summary>名称→SfxId 解析缓存（Trim + OrdinalIgnoreCase；失败结果同样缓存，防高频路径 GC/反射开销）。</summary>
    static readonly Dictionary<string, SfxId?> ResolveCache = new Dictionary<string, SfxId?>(StringComparer.OrdinalIgnoreCase);

    public override void Initialize(AudioManager owner)
    {
        base.Initialize(owner);
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    protected override void EnsureSources()
    {
        if (sfxPool == null || sfxPool.Length != sfxPoolSize)
        {
            sfxPool = new AudioSource[sfxPoolSize];
            for (int i = 0; i < sfxPoolSize; i++)
            {
                var go = new GameObject("SFX_" + i);
                go.transform.SetParent(transform);
                sfxPool[i] = go.AddComponent<AudioSource>();
                sfxPool[i].playOnAwake = false;
            }
        }
    }

    // ── 字符串名兼容层（能力基类：castAudioName / hitAudioName）──

    /// <summary>播放命名音效。Instance 缺失 / 名称为空 / 解析失败均静默（设计行为）。</summary>
    public static void Play(string clipName, Vector3? worldPosition = null)
    {
        if (string.IsNullOrWhiteSpace(clipName)) return;
        if (Instance == null) return;
        if (!TryResolve(clipName, out SfxId id))
        {
            RegisterMissingSfxName(clipName);
            return;
        }
        Instance.Play(id, worldPosition);
    }

    static bool TryResolve(string clipName, out SfxId id)
    {
        id = SfxId.None;
        string key = clipName.Trim();
        if (ResolveCache.TryGetValue(key, out SfxId? cached))
        {
            if (cached.HasValue) { id = cached.Value; return true; }
            return false;
        }
        if (System.Enum.TryParse(key, true, out SfxId parsed) && parsed != SfxId.None)
        {
            ResolveCache[key] = parsed;
            id = parsed;
            return true;
        }
        ResolveCache[key] = null;
        return false;
    }

    // ── 怪物技能施放音查表（资产经 Owner 统一配置，与 SfxBank 解耦）──

    /// <summary>
    /// 怪物技能施放音：按 owner.sinType + 技能类别查 MonsterSkillAudioConfig 播放。
    /// 配置经 Owner.monsterSkillAudioConfig 读取（统一配置入口在 AudioManager，无 Resources 旁路）。
    /// 资产缺失 / 无条目 / clip 空 → 静默（设计行为，配置到位即响）。
    /// </summary>
    public static void PlayCastAudio(MonsterActor owner, EnemyAbility.AbilityType kind, Vector3? worldPos = null)
    {
        if (owner == null || kind == EnemyAbility.AbilityType.Passive) return;
        var inst = Instance;
        if (inst == null || inst.Owner == null) return;
        var cfg = inst.Owner.monsterSkillAudioConfig;
        if (cfg == null || !cfg.TryGet(owner.sinType, kind, out var e) || e.clip == null)
            return;
        inst.PlayClip(e.clip, worldPos, e.volumeScale, e.pitch, true);
    }

    /// <summary>记录未注册的音效名（解析失败时调用；Debug 面板可见，不刷屏）。</summary>
    public static void RegisterMissingSfxName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        MissingSfxNames.TryGetValue(name, out int c);
        MissingSfxNames[name] = c + 1;
    }

    /// <summary>读取未注册名清单（AudioDebugPanel 用）。</summary>
    public static IReadOnlyDictionary<string, int> GetMissingSfxNames() => MissingSfxNames;

    /// <summary>读取 bank 缺失条目清单（AudioDebugPanel 用）。</summary>
    public static IReadOnlyDictionary<SfxId, int> GetMissingSfxIds() => MissingSfxIds;

    /// <summary>记录 bank 缺失条目（id 未配置或 clip 空；静默设计行为，Debug 面板可见）。</summary>
    static void RegisterMissingSfxId(SfxId id)
    {
        MissingSfxIds.TryGetValue(id, out int c);
        MissingSfxIds[id] = c + 1;
    }

    // ── 统一播放（SfxId → bank 条目 → 节流 → channel 分流）──

    /// <summary>
    /// 统一播放入口：查 Owner.SfxBank 条目（未配置/clip 空 → 静默 false 并计入缺失名单）。
    /// channel=Ui 分流 Owner.UiController；World 走池（可选 3D）；per-id 节流（条目 minInterval 覆盖全局）。
    /// </summary>
    public bool Play(SfxId id, Vector3? worldPos = null, float volumeScale = 1f)
    {
        if (id == SfxId.None) return false;

        var bank = Owner != null ? Owner.sfxBank : null;
        if (bank == null || !bank.TryGet(id, out var entry) || entry.clip == null)
        {
            RegisterMissingSfxId(id);
            return false;
        }

        // per-id 节流（条目 minInterval 覆盖，否则用全局 sfxMinInterval）
        float interval = entry.minInterval > 0f ? entry.minInterval : sfxMinInterval;
        if (interval > 0f && _lastSfxIdTime.TryGetValue(id, out float last) && Time.unscaledTime - last < interval)
            return false;
        _lastSfxIdTime[id] = Time.unscaledTime;

        if (entry.channel == SfxBank.Channel.Ui)
        {
            if (Owner != null && Owner.uiController != null)
                Owner.uiController.PlayClip(entry.clip, entry.volumeScale * volumeScale);
            return true;
        }
        PlayClip(entry.clip, worldPos, entry.volumeScale * volumeScale, entry.pitch, entry.prefer3D);
        return true;
    }

    /// <summary>播放战斗/世界音效（池选空闲源；可选世界位置；per-clip 节流）。</summary>
    public void PlayClip(AudioClip clip, Vector3? worldPos = null, float volumeScale = 1f, float pitch = 1f, bool prefer3D = true)
    {
        if (clip == null) return;
        if (!CanPlayNow(clip)) return; // 节流：窗口内同 clip 丢弃
        var src = GetFreeSfxSource();
        if (src == null) return;
        if (worldPos.HasValue && prefer3D)
        {
            src.spatialBlend = 1f;
            src.minDistance = sfx3DMinDistance;
            src.maxDistance = sfx3DMaxDistance;
            src.transform.position = worldPos.Value;
        }
        else src.spatialBlend = 0f;
        src.pitch = pitch; // PlayOneShot 应用源 pitch；每次播放前重设（无状态残留）
        // 基础音量存源 volume（RefreshVolume 可统一刷新），scale 走 PlayOneShot 运行时叠加
        src.volume = Perceptual(Owner.SfxVolume);
        src.PlayOneShot(clip, volumeScale);
    }

    // ── 循环音效租借（Movement 用；机制层先行，战斗侧接入见方案 §3.6/§5）──

    /// <summary>循环 SFX 句柄（轻 struct：IsValid=false 时 StopSfxLoop 为 no-op）。</summary>
    public struct SfxLoopHandle
    {
        internal AudioSource source;
        public bool IsValid => source != null;
    }

    /// <summary>
    /// 循环 SFX 租借：在 parent 下建 loop 子源（3D 跟随，不占池）。per-id 单实例（重复 Start 幂等返回已有）。
    /// id 未配置 / clip 空 → 返回无效句柄（静默，设计行为）。移动/悬浮音由调用方按移动状态启停。
    /// </summary>
    public SfxLoopHandle StartSfxLoop(SfxId id, Transform parent)
    {
        var bank = Owner != null ? Owner.sfxBank : null;
        if (bank == null || !bank.TryGet(id, out var entry) || entry.clip == null)
        {
            RegisterMissingSfxId(id);
            return default;
        }
        // 幂等：同 id 已存在且存活则复用（parent 销毁后 source 判空由 Unity fake-null 兜底）
        if (_sfxLoops.TryGetValue(id, out var existing) && existing != null)
            return new SfxLoopHandle { source = existing };

        var go = new GameObject("SfxLoop_" + id);
        if (parent != null) go.transform.SetParent(parent, false);
        var src = go.AddComponent<AudioSource>();
        src.clip = entry.clip;
        src.loop = true;
        src.playOnAwake = false;
        src.volume = Perceptual(Owner.SfxVolume) * entry.volumeScale;
        src.pitch = entry.pitch;
        src.spatialBlend = 1f;
        src.minDistance = sfx3DMinDistance;
        src.maxDistance = sfx3DMaxDistance;
        src.Play();
        _sfxLoops[id] = src;
        return new SfxLoopHandle { source = src };
    }

    /// <summary>停止循环 SFX（幂等；无效句柄 no-op）。</summary>
    public void StopSfxLoop(SfxLoopHandle handle)
    {
        if (handle.source == null) return;
        var go = handle.source.gameObject;
        // 从表移除（值比对；fake-null 条目顺带清理）
        SfxId found = SfxId.None;
        foreach (var kv in _sfxLoops)
            if (kv.Value == handle.source) { found = kv.Key; break; }
        if (found != SfxId.None) _sfxLoops.Remove(found);
        Destroy(go);
    }

    // ── 内部：节流与池调度 ──

    /// <summary>per-clip 最小重播间隔节流（高频事件防爆音，对调用方透明）。</summary>
    bool CanPlayNow(AudioClip clip)
    {
        if (sfxMinInterval <= 0f) return true;
        float now = Time.unscaledTime;
        if (_lastSfxTime.TryGetValue(clip, out float last) && now - last < sfxMinInterval)
            return false;
        _lastSfxTime[clip] = now;
        return true;
    }

    /// <summary>轮询调度取池源：优先空闲；全忙时轮询指针覆盖（非固定 pool[0] 热点）。</summary>
    AudioSource GetFreeSfxSource()
    {
        if (sfxPool == null || sfxPool.Length == 0) return null;
        for (int i = 0; i < sfxPool.Length; i++)
        {
            int idx = (_sfxCursor + i) % sfxPool.Length;
            if (sfxPool[idx] != null && !sfxPool[idx].isPlaying)
            {
                _sfxCursor = (idx + 1) % sfxPool.Length;
                return sfxPool[idx];
            }
        }
        var busy = sfxPool[_sfxCursor];
        _sfxCursor = (_sfxCursor + 1) % sfxPool.Length;
        return busy;
    }

    // ── 统一接口 ──

    public override void RefreshVolume()
    {
        if (sfxPool != null)
            foreach (var s in sfxPool)
                if (s != null && s.isPlaying) s.volume = MixerActive ? 1f : Perceptual(Owner.SfxVolume);
        // 循环音效源（挂外部 parent，不随本对象移动）音量同步刷新
        foreach (var kv in _sfxLoops)
        {
            if (kv.Value == null) continue;
            float scale = 1f;
            var bank = Owner != null ? Owner.sfxBank : null;
            if (bank != null && bank.TryGet(kv.Key, out var entry) && entry != null) scale = entry.volumeScale;
            kv.Value.volume = MixerActive ? 1f : Perceptual(Owner.SfxVolume) * scale;
        }
    }

    public override void PauseAll()
    {
        if (sfxPool != null)
            foreach (var s in sfxPool)
                if (s != null) s.Pause();
    }

    public override void ResumeAll()
    {
        if (sfxPool != null)
            foreach (var s in sfxPool)
                if (s != null) s.UnPause();
    }

    public override void StopAll()
    {
        if (sfxPool != null)
            foreach (var s in sfxPool)
                if (s != null) s.Stop();
    }
}
