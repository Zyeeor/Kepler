using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 叙事事件总线（常驻单例）：把现有事件源归一化为 NarrativeTriggerEvent + qualifier + per-Run 计数，
/// 转发给 NarrativeScheduler。战斗侧边界：只订阅，零 handler 逻辑侵入。
/// 订阅时序：OnEnable/Start/sceneLoaded 三处补订阅 + Update 轮询重订阅（实例引用变化先退后订）。
/// </summary>
public class NarrativeEventBus : MonoBehaviour
{
    public static NarrativeEventBus Instance { get; private set; }

    /// <summary>事件出口（Scheduler 订阅）。qualifier：RunPhase 名 / custom eventId / null。</summary>
    public static event Action<NarrativeTriggerEvent, string> OnNarrativeEvent;

    readonly Dictionary<(NarrativeTriggerEvent, string), int> _counters = new Dictionary<(NarrativeTriggerEvent, string), int>();
    bool _runSeen;
    float _lastEliteSpawnedAt = float.NegativeInfinity;

    /// <summary>最近一次精英投放时间（调度器高压门"精英不可打扰窗口"只读）。</summary>
    public float LastEliteSpawnedAt => _lastEliteSpawnedAt;

    // 订阅引用（轮询重订阅比对用）
    object _boundWave, _boundPossession, _boundRun, _boundElite, _boundCard;

    public static NarrativeEventBus EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("NarrativeEventBus");
        return go.AddComponent<NarrativeEventBus>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnEnable()
    {
        SubscribeAll();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start()
    {
        DontDestroyOnLoad(gameObject);
        SubscribeAll();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _runSeen = false;
        SubscribeAll();
    }

    void OnDisable()
    {
        UnsubscribeAll();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        UnsubscribeAll();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // RunStarted 边沿兜底：直接 Play 路径不广播"开始"（参考 RunStatsCollector 兜底检测）
        var run = RunSession.Instance;
        if (run != null && run.HasActiveRun && !_runSeen)
        {
            _runSeen = true;
            Report(NarrativeTriggerEvent.RunStarted, null);
        }
        // 轮询重订阅（场景单例可能重建，实例引用变化先退后订）
        ResubscribeIfChanged();
    }

    // ── 订阅（先退后订幂等）──

    void SubscribeAll()
    {
        var wm = WaveManager.Instance;
        if (wm != null && _boundWave != wm)
        {
            if (_boundWave is WaveManager old) { old.OnWaveStarted -= OnWaveStarted; old.OnWaveCompleted -= OnWaveCompleted; old.OnWaveEnemyKilled -= OnWaveEnemyKilled; }
            wm.OnWaveStarted += OnWaveStarted;
            wm.OnWaveCompleted += OnWaveCompleted;
            wm.OnWaveEnemyKilled += OnWaveEnemyKilled;
            _boundWave = wm;
        }
        var pm = PossessionManager.Instance;
        if (pm != null && _boundPossession != pm)
        {
            if (_boundPossession is PossessionManager old) { old.OnPossessionStarted -= OnPossessionStarted; old.OnPossessionEndedEx -= OnPossessionEndedEx; }
            pm.OnPossessionStarted += OnPossessionStarted;
            pm.OnPossessionEndedEx += OnPossessionEndedEx;
            _boundPossession = pm;
        }
        var run = RunSession.Instance;
        if (run != null && _boundRun != run)
        {
            if (_boundRun is RunSession old) old.OnPhaseChanged -= OnPhaseChanged;
            run.OnPhaseChanged += OnPhaseChanged;
            _boundRun = run;
        }
        var elite = EliteBuildDirector.Instance;
        if (elite != null && _boundElite != elite)
        {
            if (_boundElite is EliteBuildDirector old) old.OnEliteSpawned -= OnEliteSpawned;
            elite.OnEliteSpawned += OnEliteSpawned;
            _boundElite = elite;
        }
        // 静态事件（CardManager/TutorialFactBus）：先退后订幂等
        CardManager.OnEffectUnlocked -= OnCardConfirmed;
        CardManager.OnEffectUnlocked += OnCardConfirmed;
        CardManager.OnCardOffered -= OnCardOffered;
        CardManager.OnCardOffered += OnCardOffered;
        if (!_cardSubscribed)
        {
            TutorialFactBus.OnFactReported += OnTutorialFact;
            _cardSubscribed = true;
        }
    }
    bool _cardSubscribed;

    void UnsubscribeAll()
    {
        if (_boundWave is WaveManager wm) { wm.OnWaveStarted -= OnWaveStarted; wm.OnWaveCompleted -= OnWaveCompleted; wm.OnWaveEnemyKilled -= OnWaveEnemyKilled; _boundWave = null; }
        if (_boundPossession is PossessionManager pm) { pm.OnPossessionStarted -= OnPossessionStarted; pm.OnPossessionEndedEx -= OnPossessionEndedEx; _boundPossession = null; }
        if (_boundRun is RunSession run) { run.OnPhaseChanged -= OnPhaseChanged; _boundRun = null; }
        if (_boundElite is EliteBuildDirector e) { e.OnEliteSpawned -= OnEliteSpawned; _boundElite = null; }
        CardManager.OnEffectUnlocked -= OnCardConfirmed;
        CardManager.OnCardOffered -= OnCardOffered;
        if (_cardSubscribed) { TutorialFactBus.OnFactReported -= OnTutorialFact; _cardSubscribed = false; }
    }

    void ResubscribeIfChanged()
    {
        if (WaveManager.Instance != null && _boundWave != WaveManager.Instance) SubscribeAll();
        else if (PossessionManager.Instance != null && _boundPossession != PossessionManager.Instance) SubscribeAll();
        else if (RunSession.Instance != null && _boundRun != RunSession.Instance) SubscribeAll();
        else if (EliteBuildDirector.Instance != null && _boundElite != EliteBuildDirector.Instance) SubscribeAll();
    }

    // ── 事件源 handler → 归一化 Report ──

    void OnWaveStarted(int index, WaveConfig wave) => Report(NarrativeTriggerEvent.WaveStarted, null);
    void OnWaveCompleted(int index) => Report(NarrativeTriggerEvent.WaveCompleted, null);
    void OnWaveEnemyKilled(MonsterActor m)
    {
        if (m != null && EliteBuildCarrier.Get(m) != null)
            Report(NarrativeTriggerEvent.EliteFatal, null);
    }
    void OnPossessionStarted(MonsterActor body)
    {
        Report(NarrativeTriggerEvent.PossessionStarted, null);
        if (body != null && EliteBuildCarrier.Get(body) != null)
            Report(NarrativeTriggerEvent.ElitePossessed, null);
    }
    void OnPossessionEndedEx(PossessionManager.PossessionEndReason reason)
    {
        if (reason == PossessionManager.PossessionEndReason.VoluntaryRelease)
            Report(NarrativeTriggerEvent.VoluntaryRelease, null);
        else if (reason == PossessionManager.PossessionEndReason.BodyDied)
            Report(NarrativeTriggerEvent.SoulEntered, null); // 身体死亡释放回灵魂态（与 RunStatsCollector.soulEnters 同口径）
    }
    void OnPhaseChanged(RunPhase phase)
    {
        Report(NarrativeTriggerEvent.RunPhaseChanged, phase.ToString());
        if (phase == RunPhase.Result) Report(NarrativeTriggerEvent.RunWon, null);
        else if (phase == RunPhase.Failed) Report(NarrativeTriggerEvent.RunFailed, null);
    }
    void OnEliteSpawned(MonsterActor monster)
    {
        _lastEliteSpawnedAt = Time.unscaledTime;
        Report(NarrativeTriggerEvent.EliteSpawned, null);
    }
    void OnCardConfirmed(CardData data) => Report(NarrativeTriggerEvent.CardConfirmed, null);
    void OnCardOffered() => Report(NarrativeTriggerEvent.CardOffered, null);
    void OnTutorialFact(TutorialFact fact)
    {
        if (fact == TutorialFact.OpeningCarrierPossessed)
            Report(NarrativeTriggerEvent.InitialCarrierAssigned, null);
    }

    // ── 公开入口 ──

    /// <summary>自定义事件入口（策划"自定义 Gameplay Event" + Debug 模拟计数同口）。</summary>
    public static void Report(string customEventId)
    {
        if (string.IsNullOrEmpty(customEventId)) return;
        Report(NarrativeTriggerEvent.Custom, customEventId);
    }

    /// <summary>归一化计数 + 转发（核心单点；Debug 面板模拟事件计数同口）。</summary>
    public static void Report(NarrativeTriggerEvent evt, string qualifier)
    {
        if (evt == NarrativeTriggerEvent.None) return;
        var inst = Instance;
        if (inst == null) return;
        var key = (evt, qualifier ?? "");
        inst._counters.TryGetValue(key, out int c);
        inst._counters[key] = c + 1;
        OnNarrativeEvent?.Invoke(evt, qualifier);
    }

    /// <summary>某事件本 Run 计数（Condition nth 判定与 Debug 查看用）。</summary>
    public int GetCount(NarrativeTriggerEvent evt, string qualifier = null)
    {
        _counters.TryGetValue((evt, qualifier ?? ""), out int c);
        return c;
    }

    public List<TriggerCounterEntry> SnapshotCounters()
    {
        var list = new List<TriggerCounterEntry>();
        foreach (var kv in _counters)
            list.Add(new TriggerCounterEntry { eventType = (int)kv.Key.Item1, qualifier = kv.Key.Item2, count = kv.Value });
        return list;
    }

    public void RestoreCounters(List<TriggerCounterEntry> snapshot)
    {
        _counters.Clear();
        if (snapshot == null) return;
        foreach (var e in snapshot)
            if (e != null && e.eventType >= 0 && e.eventType < Enum.GetValues(typeof(NarrativeTriggerEvent)).Length)
                _counters[((NarrativeTriggerEvent)e.eventType, e.qualifier ?? "")] = e.count;
    }

    public void ResetForNewRun()
    {
        _counters.Clear();
        _runSeen = false;
    }
}
