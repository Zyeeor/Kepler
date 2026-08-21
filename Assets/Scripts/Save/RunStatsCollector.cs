using System;
using UnityEngine;

/// <summary>
/// 整局运行数据采集器（Run Analytics Collector）。
///
/// 职责：一局（Run）内订阅各系统事件，把"原始统计"累计进 RunStatsData，
/// 在整局终态（RunPhase.Result / Failed）时经 RunStatsStore 落盘并触发上传预留。
///
/// 采集点（对齐 Canonical §8 / Narrative §8.1-8.2）：
///   - RunSession.OnPhaseChanged            → 终态结算 / Final 到达完成 / run 时长
///   - PossessionManager.OnPossessionStarted → 附身计数、Per-Sin、控制时长起点、Elite 附身
///   - PossessionManager.OnPossessionEndedEx → 控制时长结算、主动离身、低耐久离身、灵魂回自由态
///   - PossessionManager.OnBodyDiedWhilePossessing → 附身中身体死亡（非主动离身）
///   - WaveManager.OnWaveStarted            → 到达波次
///   - WaveManager.OnWaveEnemyKilled        → 击杀计数（附身中击杀归 Per-Sin）
///   - GameManager.OnStateChanged           → Bullet Time 次数/时长
///   - EnemyAbility 静态事件                → Per-Sin Movement/Attack/Special（玩家控制期间）
///   - CardManager 静态事件                 → Per-Sin 卡牌投资
///
/// 生命周期：与 RunSession 同构，EnsureInstance 常驻（DontDestroyOnLoad），无需场景挂载。
/// </summary>
public class RunStatsCollector : MonoBehaviour
{
    public static RunStatsCollector Instance { get; private set; }

    /// <summary>低耐久主动离身阈值：离身瞬间身体 HP% 低于该值记一次（TUNABLE）。</summary>
    [Tooltip("低耐久主动离身判定阈值（身体 HP 百分比，0-1）。")]
    [Range(0f, 1f)] public float lowHealthReleaseThreshold = 0.3f;

    /// <summary>当前整局累计数据（null = 无进行中对局）。</summary>
    public RunStatsData Current { get; private set; }

    // ── 内部累计态 ──
    float runStartedAt;                 // unscaled realtime（run 开始时刻）
    MonsterActor currentBody;           // 当前附身身体（控制时长累计用）
    float bodyControlStartedAt;         // 本次附身开始时刻（unscaled realtime）
    float bulletTimeStartedAt;          // 本次子弹时间开始时刻（unscaled realtime）
    bool inBulletTime;
    bool subsEnabled;                   // 事件订阅是否已建立（防重复）
    bool possessionSubscribed, waveSubscribed, gameSubscribed, cardSubscribed; // 场景单例订阅标志
    // 已订阅的场景单例实例（场景重载时旧实例销毁、新实例出现——实例引用变化必须退订旧并重订阅）
    PossessionManager boundPossession;
    WaveManager boundWave;
    GameManager boundGame;
    CardManager boundCard;

    // ── 单例 ──
    public static RunStatsCollector EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[RunStatsCollector]");
        DontDestroyOnLoad(go);
        return go.AddComponent<RunStatsCollector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Unsubscribe();
    }

    void OnEnable() => Subscribe();
    void OnDisable() => Unsubscribe();

    void Update()
    {
        // 场景单例轮询补订阅：采集器常驻 DDOL，场景加载后实例才出现；
        // 事件订阅必须在实例存在后建立，否则整场对局事件全部丢失。轮询开销 = 每帧 3 次 null 检查，可忽略。
        // 场景重载时单例实例会重建：实例引用变化 → 先退订旧实例再订阅新实例（防悬空委托 + 新实例收不到事件）。
        if (!subsEnabled) return;

        var poss = PossessionManager.Instance;
        if (poss != null && (poss != boundPossession || !possessionSubscribed))
        {
            if (boundPossession != null) DetachPossession(boundPossession);
            AttachPossession(poss);
            boundPossession = poss;
            possessionSubscribed = true;
        }

        var wm = WaveManager.Instance;
        if (wm != null && (wm != boundWave || !waveSubscribed))
        {
            if (boundWave != null) DetachWave(boundWave);
            AttachWave(wm);
            boundWave = wm;
            waveSubscribed = true;
        }

        var gm = GameManager.Instance;
        if (gm != null && (gm != boundGame || !gameSubscribed))
        {
            if (boundGame != null) GameManager.OnStateChanged -= HandleGameStateChanged;
            GameManager.OnStateChanged += HandleGameStateChanged;
            boundGame = gm;
            gameSubscribed = true;
        }

        var cm = CardManager.Instance;
        if (cm != null && (cm != boundCard || !cardSubscribed))
        {
            if (boundCard != null) CardManager.OnEffectUnlocked -= HandleCardUnlocked;
            CardManager.OnEffectUnlocked += HandleCardUnlocked;
            boundCard = cm;
            cardSubscribed = true;
        }
    }

    void Subscribe()
    {
        if (subsEnabled) return;
        subsEnabled = true;
        possessionSubscribed = waveSubscribed = gameSubscribed = cardSubscribed = false;

        var session = RunSession.Instance;
        if (session != null) session.OnPhaseChanged += HandlePhaseChanged;
        EnemyAbility.OnAnyTriggered += HandleAbilityTriggered;
    }

    void Unsubscribe()
    {
        if (!subsEnabled) return;
        subsEnabled = false;
        possessionSubscribed = waveSubscribed = gameSubscribed = cardSubscribed = false;

        if (RunSession.Instance != null) RunSession.Instance.OnPhaseChanged -= HandlePhaseChanged;
        if (boundPossession != null) DetachPossession(boundPossession);
        if (boundWave != null) DetachWave(boundWave);
        if (boundGame != null) GameManager.OnStateChanged -= HandleGameStateChanged;
        if (boundCard != null) CardManager.OnEffectUnlocked -= HandleCardUnlocked;
        boundPossession = null;
        boundWave = null;
        boundGame = null;
        boundCard = null;
        EnemyAbility.OnAnyTriggered -= HandleAbilityTriggered;
    }

    void AttachPossession(PossessionManager pm)
    {
        pm.OnPossessionStarted += HandlePossessionStarted;
        pm.OnPossessionEndedEx += HandlePossessionEnded;
        pm.OnBodyDiedWhilePossessing += HandleBodyDiedWhilePossessing;
    }

    void DetachPossession(PossessionManager pm)
    {
        pm.OnPossessionStarted -= HandlePossessionStarted;
        pm.OnPossessionEndedEx -= HandlePossessionEnded;
        pm.OnBodyDiedWhilePossessing -= HandleBodyDiedWhilePossessing;
    }

    void AttachWave(WaveManager wm)
    {
        wm.OnWaveStarted += HandleWaveStarted;
        wm.OnWaveEnemyKilled += HandleEnemyKilled;
    }

    void DetachWave(WaveManager wm)
    {
        wm.OnWaveStarted -= HandleWaveStarted;
        wm.OnWaveEnemyKilled -= HandleEnemyKilled;
    }

    // ── Run 生命周期 ──

    /// <summary>新局开始：由 RunSession.BeginNewRun 调用（或采集器在阶段变化时兜底检测）。</summary>
    public void StartNewRun(string runId)
    {
        if (Current != null && Current.runId == runId) return; // 幂等：同一 run 不重复重置
        // 若上一局未结算（异常退出/直接 Play），先结算旧局再开新局
        if (Current != null) FinalizeRun(false, "NewRunInterrupt");

        Current = new RunStatsData
        {
            runId = runId,
            playerId = DeviceIdentity.Id,
            startedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        };
        runStartedAt = Time.unscaledTime;
        currentBody = null;
        Debug.Log($"[RunStats] 新局采集开始：runId={runId}");
    }

    /// <summary>整局结束（终态）：累计 run 时长 → 刷新派生字段 → 落盘 → 上传预留。</summary>
    void FinalizeRun(bool won, string endPhase)
    {
        if (Current == null) return;
        if (Current.runDurationSeconds <= 0f)
            Current.runDurationSeconds = Mathf.Max(0f, Time.unscaledTime - runStartedAt);
        Current.endedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Current.won = won;
        Current.endPhase = endPhase;

        // 若终态时仍附身（如 GameOver 打断），结算未闭合的控制时长
        CloseBodyControl();

        Current.RefreshDistinctSinsUsed();
        RunStatsStore.SaveRunStats(Current);
        RunStatsStore.UploadRunData(Current);   // 预留出口（当前空实现）
        Debug.Log($"[RunStats] 本局采集完成：runId={Current.runId}, 时长={Current.runDurationSeconds:F1}s, 附身={Current.totalPossessions}, 到达波={Current.reachedWaveIndex}, 胜利={won}");
        Current = null;
        currentBody = null;
    }

    // ── 阶段变化（RunSession 事件）──

    void HandlePhaseChanged(RunPhase phase)
    {
        // 新局检测：HasActiveRun 且 Current 为空（BeginNewRun 未被调用，如直接 Play 路径）
        var run = RunSession.Instance;
        if (run != null && run.HasActiveRun && Current == null)
            StartNewRun(run.RunId);

        if (phase == RunPhase.Final)
        {
            if (Current != null) Current.finalReached = true;
        }
        else if (phase == RunPhase.Result)
        {
            if (Current != null) Current.finalCompleted = true;
            FinalizeRun(true, "Result");
        }
        else if (phase == RunPhase.Failed)
        {
            FinalizeRun(false, "Failed");
        }
    }

    // ── 附身（PossessionManager 事件）──

    void HandlePossessionStarted(MonsterActor body)
    {
        if (Current == null || body == null) return;
        Current.totalPossessions++;

        SinType sin = ResolveSin(body);
        var ps = Current.FindOrCreateSin(sin);
        if (ps != null) ps.possessionCount++;

        // Elite 附身计数
        if (IsElite(body)) Current.elitePossessionCount++;

        // 若上一段控制未闭合（异常路径），先结算再开新段
        CloseBodyControl();

        currentBody = body;
        bodyControlStartedAt = Time.unscaledTime;
    }

    void HandlePossessionEnded(PossessionManager.PossessionEndReason reason)
    {
        // 先缓存即将被 CloseBodyControl 置空的当前身体（低耐久判断需要它）
        var releasedBody = currentBody;
        CloseBodyControl();   // 结算控制时长归入该身体所属 Sin

        if (Current == null) return;
        // 主动离身细分（低耐久）：VoluntaryRelease 且离身瞬间身体 HP% 低于阈值
        if (reason == PossessionManager.PossessionEndReason.VoluntaryRelease)
        {
            Current.voluntaryReleases++;
            if (releasedBody != null && releasedBody.maxHealth > 0f &&
                releasedBody.currentHealth / releasedBody.maxHealth < lowHealthReleaseThreshold)
                Current.lowHealthReleases++;
        }

        // 灵魂回自由形态（Soul Enter）
        Current.soulEnters++;
    }

    void HandleBodyDiedWhilePossessing(MonsterActor body)
    {
        // 附身中身体死亡：不属于主动离身（计数已在 reason=BodyDied 时处理），无额外字段
    }

    /// <summary>结算当前附身段的控制时长并归入对应 Sin；置空当前身体。</summary>
    void CloseBodyControl()
    {
        if (currentBody == null || Current == null) return;
        var ps = Current.FindOrCreateSin(ResolveSin(currentBody));
        if (ps != null)
            ps.controlSeconds += Mathf.Max(0f, Time.unscaledTime - bodyControlStartedAt);
        currentBody = null;
    }

    // ── 波次 / 击杀（WaveManager 事件）──

    void HandleWaveStarted(int waveIndex, WaveConfig config)
    {
        if (Current == null) return;
        if (waveIndex > Current.reachedWaveIndex) Current.reachedWaveIndex = waveIndex;
    }

    void HandleEnemyKilled(MonsterActor monster)
    {
        if (Current == null || monster == null) return;
        Current.totalKills++;
        if (IsElite(monster)) Current.eliteFatalCount++;

        // 附身期间击杀：归入当前身体所属 Sin（玩家控制的怪杀死其它怪）
        if (currentBody != null && monster != currentBody)
        {
            var ps = Current.FindOrCreateSin(ResolveSin(currentBody));
            if (ps != null) ps.kills++;
        }
    }

    // ── 子弹时间（GameManager 状态事件）──

    void HandleGameStateChanged(GameManager.GameState state)
    {
        if (Current == null) return;
        if (state == GameManager.GameState.BulletTime && !inBulletTime)
        {
            inBulletTime = true;
            bulletTimeStartedAt = Time.unscaledTime;
            Current.bulletTimeCount++;
        }
        else if (state != GameManager.GameState.BulletTime && inBulletTime)
        {
            inBulletTime = false;
            Current.bulletTimeTotalSeconds += Mathf.Max(0f, Time.unscaledTime - bulletTimeStartedAt);
        }
    }

    // ── 卡牌投资（CardManager 静态事件）──

    void HandleCardUnlocked(CardData card)
    {
        if (Current == null || card == null) return;
        if (card.category != CardCategory.MonsterType && card.category != CardCategory.TypeGrowth) return;
        if (card.monsterType == SinType.None) return;
        var ps = Current.FindOrCreateSin(card.monsterType);
        if (ps != null) ps.cardInvestmentCount++;
    }

    // ── 能力使用（EnemyAbility 静态事件：玩家控制期间）──

    void HandleAbilityTriggered(EnemyAbility ability)
    {
        if (Current == null || ability == null) return;
        // 仅统计玩家控制期间的能力使用（附身身体触发）
        if (currentBody == null) return;
        if (ability.OwnerMonster != currentBody) return;

        var ps = Current.FindOrCreateSin(ResolveSin(currentBody));
        if (ps == null) return;
        switch (ability.type)
        {
            case EnemyAbility.AbilityType.BasicAttack: ps.attackCount++; break;
            case EnemyAbility.AbilityType.Skill:       ps.specialCount++; break;
            case EnemyAbility.AbilityType.Mobility:    ps.movementCount++; break;
        }
    }

    /// <summary>
    /// 中途结束兜底（RunSession.EndRun 调用）：未走终态（Result/Failed）的对局强制结算并落盘，
    /// 避免"返回主菜单/重开"丢失整局统计。
    /// </summary>
    public void EndRunEarly()
    {
        if (Current == null) return;
        FinalizeRun(false, "Aborted");
    }

    // ── 工具 ──

    /// <summary>解析怪物所属 Sin：优先 prefab 名（monster 根名前缀，如 gluttony_new），失败回退 None。</summary>
    static SinType ResolveSin(MonsterActor body)
    {
        if (body == null) return SinType.None;
        string name = body.gameObject != null ? body.gameObject.name : null;
        if (string.IsNullOrEmpty(name)) return SinType.None;
        // 池化实例名 = "prefabName(Clone)"；场景怪直接 prefab 名
        return RunStatsUtil.SinFromPrefabName(name.Replace("(Clone)", ""));
    }

    /// <summary>是否精英怪（挂载 EliteBuildCarrier）。</summary>
    static bool IsElite(MonsterActor body)
    {
        if (body == null) return false;
        return EliteBuildCarrier.Get(body) != null;
    }
}
