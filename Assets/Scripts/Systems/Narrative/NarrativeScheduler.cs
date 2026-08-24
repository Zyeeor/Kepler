using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>决策类型（契约 §7"可调试原因"）。</summary>
public enum CueDecision { Played, Queued, Deferred, Dropped, Rejected, Completed, Cancelled }

/// <summary>决策记录（原因环形缓冲）。</summary>
public struct CueDecisionRecord
{
    public float unscaledTime;
    public string cueId;
    public CueDecision decision;
    public string reason;
    public override string ToString() => $"[{unscaledTime:F1}] {cueId} {decision}（{reason}）";
}

/// <summary>
/// 叙事调度核心（常驻单例）：Cue 匹配 → 决策（播放/排队/延后/放弃）→ 播放状态机 → 幂等完成。
/// 契约 §7 全部最低调度合同：同时只播一条、优先级、高压门、有限等待队列+过期丢弃、Pause 同步、可调试原因。
/// </summary>
public class NarrativeScheduler : MonoBehaviour
{
    public static NarrativeScheduler Instance { get; private set; }

    [Tooltip("调度参数（空则 Resources/Narrative/NarrativeSchedulerConfig 兜底）")]
    public NarrativeSchedulerConfig config;

    public NarrativeAccessController Access { get; } = new NarrativeAccessController();

    readonly List<NarrativeCue> _pendingQueue = new List<NarrativeCue>();
    readonly List<float> _pendingEnqueueTime = new List<float>();
    readonly List<int> _pendingResumeLine = new List<int>(); // 与队列同索引：断点行（-1=从 0 开始）
    readonly List<CueDecisionRecord> _decisionLog = new List<CueDecisionRecord>(); // 环形 64
    readonly List<string> _playedThisRun = new List<string>();
    readonly Dictionary<string, float> _cueLastPlayedAt = new Dictionary<string, float>();

    NarrativeCue _currentCue;
    int _currentLineIndex;
    float _stateTimer;
    bool _isDelaying;
    bool _gapWaiting;
    float _subtitleTimer;
    float _subtitleDuration;
    bool _duckPushed;
    bool _wasPaused;

    enum PlayState { Idle, Delaying, PlayingLine, Gap }
    PlayState _state = PlayState.Idle;

    public static NarrativeScheduler EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("NarrativeScheduler");
        return go.AddComponent<NarrativeScheduler>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (config == null)
            config = Resources.Load<NarrativeSchedulerConfig>("Narrative/NarrativeSchedulerConfig");
        if (config == null)
            config = ScriptableObject.CreateInstance<NarrativeSchedulerConfig>();
    }

    void OnEnable() => NarrativeEventBus.OnNarrativeEvent += OnNarrativeEvent;
    void OnDisable() => NarrativeEventBus.OnNarrativeEvent -= OnNarrativeEvent;

    void Start()
    {
        DontDestroyOnLoad(gameObject);
        // Access 变化 → Display 刷新（文本线切换传播）
        Access.OnAccessChanged += (prev, next) => NarrativeDisplay.NotifyAccessChanged();
        ResetForNewRun(); // 常驻实例启动即初始化（含 RunStarted 边沿）
    }

    void OnDestroy()
    {
        CancelCurrent("scheduler-destroyed");
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Pause 边沿（任意域冻结）
        bool paused = Time.timeScale == 0f;
        if (paused && !_wasPaused)
        {
            AudioManager.Instance?.PauseVoice();
            _wasPaused = true;
        }
        else if (!paused && _wasPaused)
        {
            AudioManager.Instance?.ResumeVoice();
            _wasPaused = false;
        }

        TickStateMachine();
    }

    // ── 事件入口 ──

    void OnNarrativeEvent(NarrativeTriggerEvent evt, string qualifier)
    {
        // 先评 Access 推进规则（推进判定与 Cue 判定同帧一致）
        EvaluateAccessRules(evt, qualifier);

        // First Clear 触发：RunWon + 未首通 → 启动八步序列（挂起普通 Cue 调度）
        if (evt == NarrativeTriggerEvent.RunWon && !NarrativeProfileStore.FirstClearCompleted)
        {
            StartFirstClear();
            return; // 首通序列期间不再评普通 Cue
        }

        var set = NarrativeCueSet.Instance;
        if (set == null || set.cues == null) return;

        for (int i = 0; i < set.cues.Count; i++)
        {
            var cue = set.cues[i];
            if (cue == null || cue.cueId == _currentCue?.cueId) continue;
            if (MatchesTrigger(cue, evt, qualifier) && MatchesConditions(cue))
                EvaluateCue(cue, "event:" + evt);
        }
    }

    FirstClearConfig _firstClearConfig;
    bool _firstClearActive;

    void StartFirstClear()
    {
        if (_firstClearActive) return;
        _firstClearActive = true;
        CancelCurrent("首通序列接管"); // 停掉进行中的旁白
        UIManager.ResultSuspendCount++; // 挂起结算面板

        if (_firstClearConfig == null)
            _firstClearConfig = Resources.Load<FirstClearConfig>("Narrative/FirstClearConfig");
        if (_firstClearConfig == null)
            _firstClearConfig = ScriptableObject.CreateInstance<FirstClearConfig>();

        var tendency = ComputeTendency();
        var fc = FirstClearController.EnsureInstance(_firstClearConfig);
        fc.Begin(tendency, () =>
        {
            UIManager.ResultSuspendCount--;
            _firstClearActive = false;
        });
    }

    RunTendencyResult ComputeTendency()
    {
        var config = Resources.Load<TendencyScoreConfig>("Narrative/TendencyScoreConfig");
        var runId = RunSession.Instance != null ? RunSession.Instance.RunId : null;
        var data = RunStatsStore.LoadRunStats(runId);
        return RunTendencyScorer.Score(data, config);
    }

    void EvaluateAccessRules(NarrativeTriggerEvent evt, string qualifier)
    {
        var profile = NarrativeAccessProfile.Instance;
        if (profile == null || profile.rules == null) return;
        for (int i = 0; i < profile.rules.Count; i++)
        {
            var rule = profile.rules[i];
            if (rule == null || rule.trigger == null) continue;
            if (rule.trigger.eventType != evt) continue;
            if (rule.fireOnce && Access.Current >= rule.targetAccess) continue; // 已推进过
            var bus = NarrativeEventBus.Instance;
            int count = bus != null ? bus.GetCount(evt, qualifier) : 0;
            bool nthOk = rule.trigger.nthMode == NthMode.AtLeast ? count >= rule.trigger.nth : count == rule.trigger.nth;
            if (!nthOk) continue;
            if (!TriggerConditionsMet(rule.trigger)) continue;
            Access.RequestAdvance(rule.targetAccess, $"规则 {rule.ruleId}（{evt}）");
        }
    }

    bool TriggerConditionsMet(NarrativeTrigger t)
    {
        if (t.conditions == null || t.conditions.Count == 0) return true;
        bool group = t.join == ConditionJoin.And;
        bool result = group;
        foreach (var c in t.conditions)
        {
            if (c == null) continue;
            bool r = EvalCondition(c);
            result = group ? (result && r) : (result || r);
        }
        return result;
    }

    // ── 触发匹配 ──

    bool MatchesTrigger(NarrativeCue cue, NarrativeTriggerEvent evt, string qualifier)
    {
        foreach (var t in cue.triggers)
        {
            if (t == null || t.eventType != evt) continue;
            var bus = NarrativeEventBus.Instance;
            int count = bus != null ? bus.GetCount(evt, qualifier) : 0;
            bool nthOk = t.nthMode == NthMode.AtLeast ? count >= t.nth : count == t.nth;
            if (!nthOk) continue;
            if (evt == NarrativeTriggerEvent.RunPhaseChanged)
            {
                // qualifier 需匹配 param 中的 RunPhaseIs 条件（简化：直接比较 phase 名）
                bool phaseMatch = false;
                foreach (var c in t.conditions)
                    if (c != null && c.type == NarrativeConditionType.RunPhaseIs && c.param == qualifier) { phaseMatch = true; break; }
                if (!phaseMatch && HasRunPhaseCondition(t)) continue;
            }
            return true;
        }
        return false;
    }

    static bool HasRunPhaseCondition(NarrativeTrigger t)
    {
        foreach (var c in t.conditions)
            if (c != null && c.type == NarrativeConditionType.RunPhaseIs) return true;
        return false;
    }

    bool MatchesConditions(NarrativeCue cue)
    {
        foreach (var t in cue.triggers)
        {
            if (t == null || t.conditions == null || t.conditions.Count == 0) return true;
            bool group = t.join == ConditionJoin.And;
            bool result = group;
            foreach (var c in t.conditions)
            {
                if (c == null) continue;
                bool r = EvalCondition(c);
                result = group ? (result && r) : (result || r);
            }
            if (result) return true;
        }
        return false;
    }

    bool EvalCondition(NarrativeCondition c)
    {
        switch (c.type)
        {
            case NarrativeConditionType.RunPhaseIs:
                return RunSession.Instance != null && RunSession.Instance.CurrentPhase.ToString() == c.param;
            case NarrativeConditionType.AccessAtLeast:
                if (int.TryParse(c.param, out int a)) return (int)Access.Current >= a;
                return false;
            case NarrativeConditionType.WaveIndexAtLeast:
                if (int.TryParse(c.param, out int w))
                    return RunSession.Instance != null && RunSession.Instance.CompletedWaveIndex + 1 >= w;
                return false;
            case NarrativeConditionType.ProfileFirstCleared:
                return NarrativeProfileStore.FirstClearCompleted;
            case NarrativeConditionType.ProfileNotFirstCleared:
                return !NarrativeProfileStore.FirstClearCompleted;
            case NarrativeConditionType.PressureFree:
                return !IsUnderPressure();
            default:
                return false;
        }
    }

    // ── 决策 ──

    void EvaluateCue(NarrativeCue cue, string source)
    {
        // 唯一性 + 间隔 + Access 门槛
        if (cue.requiredAccess > Access.Current)
        {
            Log(cue.cueId, CueDecision.Rejected, $"Access {Access.Current} < 要求 {cue.requiredAccess}");
            return;
        }
        if (!CanRepeatNow(cue))
        {
            Log(cue.cueId, CueDecision.Rejected, $"RepeatScope={cue.repeatScope} 已播/间隔未到");
            return;
        }
        if (cue.lines == null || cue.lines.Count == 0)
        {
            Log(cue.cueId, CueDecision.Rejected, "无内容行");
            return;
        }

        // 高压延后（Normal/Low 且 deferUnderPressure）
        if (cue.deferUnderPressure && cue.priority <= CuePriority.Normal && IsUnderPressure())
        {
            if (TryEnqueue(cue))
                Log(cue.cueId, CueDecision.Deferred, "高压延后入等待队列");
            else
                Log(cue.cueId, CueDecision.Dropped, "等待队列已满");
            return;
        }

        // 忙判定
        if (_state != PlayState.Idle || _pendingQueue.Count > 0)
        {
            switch (cue.busyPolicy)
            {
                case CueBusyPolicy.Interrupt:
                    if (cue.priority > CurrentPriority())
                    {
                        CancelCurrent($"被更高优 {cue.cueId} 打断", resumeAfterInterruption: true);
                        StartPlayback(cue);
                    }
                    else
                    {
                        Log(cue.cueId, CueDecision.Dropped, "Interrupt 但优先级不高于当前");
                    }
                    return;
                case CueBusyPolicy.DropIfBusy:
                    Log(cue.cueId, CueDecision.Dropped, "DropIfBusy 忙");
                    return;
                default: // Queue
                    if (TryEnqueue(cue))
                        Log(cue.cueId, CueDecision.Queued, "入等待队列");
                    else
                        Log(cue.cueId, CueDecision.Dropped, "等待队列已满");
                    return;
            }
        }

        StartPlayback(cue);
    }

    CuePriority CurrentPriority() => _currentCue != null ? _currentCue.priority : CuePriority.Low;

    bool CanRepeatNow(NarrativeCue cue)
    {
        if (cue.repeatScope == RepeatScope.OncePerRun && _playedThisRun.Contains(cue.cueId)) return false;
        if (cue.repeatScope == RepeatScope.OncePerProfile && NarrativeProfileStore.HasPlayedCue(cue.cueId)) return false;
        if (cue.minimumIntervalSeconds > 0f && _cueLastPlayedAt.TryGetValue(cue.cueId, out float last)
            && Time.unscaledTime - last < cue.minimumIntervalSeconds) return false;
        return true;
    }

    bool TryEnqueue(NarrativeCue cue, int resumeLine = -1)
    {
        if (_pendingQueue.Count >= config.maxPendingCues)
        {
            // 挤掉最低优先级项（挤不掉则失败）
            int lowest = -1;
            for (int i = 0; i < _pendingQueue.Count; i++)
                if (lowest == -1 || _pendingQueue[i].priority < _pendingQueue[lowest].priority) lowest = i;
            if (lowest >= 0 && _pendingQueue[lowest].priority < cue.priority)
            {
                _pendingQueue.RemoveAt(lowest);
                _pendingEnqueueTime.RemoveAt(lowest);
                _pendingResumeLine.RemoveAt(lowest);
            }
            else return false;
        }
        _pendingQueue.Add(cue);
        _pendingEnqueueTime.Add(Time.unscaledTime);
        _pendingResumeLine.Add(resumeLine);
        return true;
    }

    // ── 播放状态机 ──

    void StartPlayback(NarrativeCue cue, int resumeLine = -1)
    {
        _currentCue = cue;
        _currentLineIndex = resumeLine >= 0 ? resumeLine : 0; // 断点续播（-1=从 0 开始）
        _duckPushed = false;
        _state = cue.delaySeconds > 0f ? PlayState.Delaying : PlayState.PlayingLine;
        _stateTimer = cue.delaySeconds > 0f ? cue.delaySeconds : 0f;
        Log(cue.cueId, CueDecision.Played, $"开始播放（{cue.lines.Count} 行，从 {_currentLineIndex} 起）");
        if (cue.delaySeconds <= 0f) BeginLine(_currentLineIndex);
    }

    void BeginLine(int lineIndex)
    {
        var cue = _currentCue;
        var line = cue.lines[lineIndex];
        var am = AudioManager.Instance;

        if (cue.bgmDuck && !_duckPushed && am != null)
        {
            am.PushBgmDuck(am.voiceBgmDuckFactor);
            _duckPushed = true;
        }

        // 音频行
        bool hasAudio = !string.IsNullOrEmpty(line.audioId) && am != null;
        float duration = 0f;
        if (hasAudio)
        {
            bool played = am.PlayVoice(line.audioId, line.speaker, duckBgm: false); // Duck 由调度器自管
            if (played) duration = am.VoiceClipLength;
        }
        // 无音频/缺失：按字数估算
        if (duration <= 0f)
        {
            string text = ResolveLineText(line);
            duration = Mathf.Clamp((text != null ? text.Length : 0) / config.charsPerSecond,
                config.subtitleClamp.x, config.subtitleClamp.y);
        }

        // 字幕
        if (cue.subtitleMode != SubtitleMode.None)
        {
            var sub = NarrativeSubtitleUI.EnsureInstance();
            if (sub != null)
            {
                bool forced = cue.subtitleMode == SubtitleMode.Forced
                    || (cue.subtitleMode == SubtitleMode.Optional && NarrativeProfileStore.SubtitlesEnabled);
                if (forced) sub.ShowLine(ResolveLineText(line), duration);
            }
        }

        _subtitleTimer = 0f;
        _subtitleDuration = duration;
    }

    string ResolveLineText(NarrativeCueLine line)
    {
        if (string.IsNullOrEmpty(line.textKey)) return "";
        // 以 Subtitle 载体身份解析（旁白字幕线偏好，而非条目自身载体；无命中回退 key）
        var tc = TextCatalog.Instance;
        return tc != null ? tc.Get(line.textKey, NarrativeCarrier.Subtitle) : line.textKey;
    }

    void TickStateMachine()
    {
        if (_state == PlayState.Idle) return;
        var cue = _currentCue;
        if (cue == null) { _state = PlayState.Idle; return; }

        _stateTimer -= Time.deltaTime;

        switch (_state)
        {
            case PlayState.Delaying:
                // 断点续播：Delay 结束从 _currentLineIndex（可能为断点行）开始，勿硬编码 0
                if (_stateTimer <= 0f) { _state = PlayState.PlayingLine; BeginLine(_currentLineIndex); }
                break;

            case PlayState.PlayingLine:
                if (LineDone())
                {
                    var line = cue.lines[_currentLineIndex];
                    if (_currentLineIndex + 1 < cue.lines.Count)
                    {
                        _currentLineIndex++;
                        _state = line.gapAfter > 0f ? PlayState.Gap : PlayState.PlayingLine;
                        _stateTimer = line.gapAfter;
                        if (_state == PlayState.PlayingLine) BeginLine(_currentLineIndex);
                        else NarrativeSubtitleUI.Instance?.Hide(); // 进入 Gap 收起本行字幕
                    }
                    else
                    {
                        CompleteCurrent();
                    }
                }
                break;

            case PlayState.Gap:
                if (_stateTimer <= 0f) { _state = PlayState.PlayingLine; BeginLine(_currentLineIndex); }
                break;
        }
    }

    bool LineDone()
    {
        var am = AudioManager.Instance;
        if (am != null && am.IsVoicePlaying) return false;
        // 无音频行：字幕计时
        _subtitleTimer += Time.deltaTime;
        return _subtitleTimer >= _subtitleDuration;
    }

    void CompleteCurrent()
    {
        var cue = _currentCue;
        FinishCleanup();
        if (cue == null) return; // 状态机 Idle 阻断 + 此处双重保险

        _playedThisRun.Add(cue.cueId);
        _cueLastPlayedAt[cue.cueId] = Time.unscaledTime;
        if (cue.repeatScope == RepeatScope.OncePerProfile) NarrativeProfileStore.MarkCuePlayed(cue.cueId);
        if (cue.advanceAccessOnComplete) Access.RequestAdvance(cue.accessResult, $"Cue {cue.cueId} 完成");
        if (cue.applyDisplayModeResult) NarrativeDisplay.SetCarrierOverride(cue.displayCarrier, cue.displayModeResult);
        Log(cue.cueId, CueDecision.Completed, "完成");

        ProcessPendingQueue();
    }

    void CancelCurrent(string reason, bool resumeAfterInterruption = false)
    {
        if (_currentCue == null) { _state = PlayState.Idle; return; }
        var cue = _currentCue;
        int resumeLine = -1;
        if (resumeAfterInterruption && cue.interruptPolicy == CueInterruptPolicy.ResumeAfterInterruption)
            resumeLine = _currentLineIndex; // 断点行（下次从此行续播）
        FinishCleanup();
        Log(cue.cueId, CueDecision.Cancelled, reason);

        if (resumeLine >= 0)
        {
            // 断点续播：插回等待队列首位（断点行随队列项存储，多断点互不覆盖）
            _pendingQueue.Insert(0, cue);
            _pendingEnqueueTime.Insert(0, Time.unscaledTime);
            _pendingResumeLine.Insert(0, resumeLine);
        }
    }

    void FinishCleanup()
    {
        if (_duckPushed) { AudioManager.Instance?.PopBgmDuck(); _duckPushed = false; }
        AudioManager.Instance?.StopVoice();
        NarrativeSubtitleUI.Instance?.Hide();
        _currentCue = null;
        _state = PlayState.Idle;
        _currentLineIndex = 0;
    }

    void ProcessPendingQueue()
    {
        // 过期丢弃，否则取最高优先级（FIFO 稳定）播放；播放前复查高压门（Deferred 项仍延后）
        if (_pendingQueue.Count == 0) return;
        bool pressure = IsUnderPressure();
        int best = -1;
        for (int i = 0; i < _pendingQueue.Count; i++)
        {
            if (Time.unscaledTime - _pendingEnqueueTime[i] > config.pendingExpireSeconds)
            {
                Log(_pendingQueue[i].cueId, CueDecision.Dropped, "等待超时过期");
                _pendingQueue.RemoveAt(i); _pendingEnqueueTime.RemoveAt(i); _pendingResumeLine.RemoveAt(i); i--;
                continue;
            }
            // 高压未解除：Normal/Low 且 deferUnderPressure 的项继续等待（不丢队列）
            if (pressure && _pendingQueue[i].deferUnderPressure && _pendingQueue[i].priority <= CuePriority.Normal)
                continue;
            if (best == -1 || _pendingQueue[i].priority > _pendingQueue[best].priority) best = i;
        }
        if (best < 0) return;
        var next = _pendingQueue[best];
        int resumeLine = _pendingResumeLine[best];
        _pendingQueue.RemoveAt(best); _pendingEnqueueTime.RemoveAt(best); _pendingResumeLine.RemoveAt(best);
        StartPlayback(next, resumeLine);
    }

    // ── 高压门 ──

    public bool IsUnderPressure()
    {
        var cfg = config;
        if (cfg.pressureInFinal && RunSession.Instance != null && RunSession.Instance.CurrentPhase == RunPhase.Final) return true;
        var pm = PossessionManager.Instance;
        if (cfg.pressureDuringTransfer && pm != null && pm.State == PossessionManager.SwitchState.Flying) return true;
        if (pm != null && pm.CurrentBody != null && pm.CurrentBody.maxHealth > 0f
            && pm.CurrentBody.currentHealth / pm.CurrentBody.maxHealth < cfg.lowBodyHealthThreshold) return true;
        if (cfg.pressureDuringBlockingUi
            && (TimeScaleManager.IsDomainActive(TimeDomain.Pause)
                || TimeScaleManager.IsDomainActive(TimeDomain.GameOver)
                || TutorialController.HasActivePrompt)) return true;
        var bus = NarrativeEventBus.Instance;
        if (bus != null && Time.unscaledTime - bus.LastEliteSpawnedAt < cfg.eliteNoDisturbSeconds) return true;
        return false;
    }

    // ── 快照 / 重置 / Debug ──

    public NarrativeRunSave CaptureSnapshot()
    {
        return new NarrativeRunSave
        {
            access = (int)Access.Current,
            playedCueIds = new List<string>(_playedThisRun),
            triggerCounters = NarrativeEventBus.Instance != null ? NarrativeEventBus.Instance.SnapshotCounters() : new List<TriggerCounterEntry>(),
            cueLastPlayed = SnapshotCueTimestamps(),
        };
    }

    List<CueTimestampEntry> SnapshotCueTimestamps()
    {
        var list = new List<CueTimestampEntry>();
        foreach (var kv in _cueLastPlayedAt) list.Add(new CueTimestampEntry { cueId = kv.Key, unscaledAt = kv.Value });
        return list;
    }

    public void RestoreSnapshot(NarrativeRunSave save)
    {
        if (save == null) { ResetForNewRun(); return; }
        Access.Restore((NarrativeAccess)Mathf.Clamp(save.access, 0, 4));
        _playedThisRun.Clear();
        if (save.playedCueIds != null) _playedThisRun.AddRange(save.playedCueIds);
        NarrativeEventBus.Instance?.RestoreCounters(save.triggerCounters);
        _cueLastPlayedAt.Clear();
        if (save.cueLastPlayed != null)
            foreach (var e in save.cueLastPlayed) if (e != null && !string.IsNullOrEmpty(e.cueId)) _cueLastPlayedAt[e.cueId] = e.unscaledAt;
    }

    public void ResetForNewRun()
    {
        CancelCurrent("新局重置");
        Access.ResetForNewRun();
        _playedThisRun.Clear();
        _cueLastPlayedAt.Clear();
        _pendingQueue.Clear();
        _pendingEnqueueTime.Clear();
        _pendingResumeLine.Clear();
        _firstClearActive = false;               // 首通序列标记随新局复位（防中途新局后 RunWon 不再触发八步）
        NarrativeDisplay.ClearRuntimeOverrides(); // 上局 Cue 的 applyDisplayModeResult 载体覆盖不带入下局
        NarrativeEventBus.Instance?.ResetForNewRun();
    }

    public void ForceTriggerCue(string cueId)
    {
        var cue = NarrativeCueSet.Instance != null ? NarrativeCueSet.Instance.Find(cueId) : null;
        if (cue == null) { Log(cueId, CueDecision.Rejected, "Cue 不存在"); return; }
        EvaluateCue(cue, "debug-force");
    }

    public IReadOnlyList<CueDecisionRecord> GetRecentDecisions() => _decisionLog;

    void Log(string cueId, CueDecision decision, string reason)
    {
        _decisionLog.Add(new CueDecisionRecord { unscaledTime = Time.unscaledTime, cueId = cueId, decision = decision, reason = reason });
        if (_decisionLog.Count > 64) _decisionLog.RemoveAt(0);
        Debug.Log($"[Narrative] {cueId} → {decision}（{reason}）");
    }
}
