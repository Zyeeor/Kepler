using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 精英怪投放总控（Canonical：01_DESIGN_CANONICAL §23 / Meta_Progression_Systems_Baseline §6；
/// 服务器数据来源见 Server/）。
///
/// 上传（Meta §6.1 快照格式；sourceWave 语义 = 第几次选卡，Owner 2026-08-26 决策）：
///   - 每次选卡完成后（波次选卡 RunFlow Choice→Waves + 精英击杀奖励选卡）上传本局所有
///     bdCount>=1 的 Sin 快照（只含 MonsterType / TypeGrowth 卡，不含 Basic/Global；stack 恒 1；
///     0 投资的 Sin 不上行）；
///   - sourceWave = 本局第几次选卡（1-based 选卡会话计数，含奖励选卡）——与投放序号
///     （pick wave = 第几次投放）同量纲计数，服务器按 sourceWave >= wave + waveGap 越级筛选；
///     读档恢复用已完成波数下限近似（历史奖励选卡次数不存档）；
///   - Final 上传使用独立阶段标记 stage="final"；上传失败静默跳过（不影响对局；
///     崩溃/Fail 无需补传，上一次选卡上传已在库）。
///
/// 投放（前台定时投放模型 + Meta §6.3 来源优先级）：
///   - 节奏：新连续刷怪逻辑下由 WaveManager.SpawnContinuousElites 定时投放——每 60s 周期第 40s
///     投 1 只（eliteCountPerCycle），Boss 前 nonBossDurationSeconds 为止；「波次」字段语义统一为
///     第几次投放精英怪（投放序号，1-based = cycleIndex + 1，与后台 Server/ 语义一致）；
///     旧波表回调路径（HandleWaveStarted，W3 起每 2 波）保留但连续逻辑下不触发；
///   - 来源：在线真实快照优先（RequestScheduledElite → POST /api/elite/pick，wave = 投放序号）；
///     无网（探活/请求失败）或服务器空候选库时本地兜底：Preset（Catalog.presetSnapshots，
///     OD-CAN-001 内容 OPEN）→ 空快照注入（TryInjectScheduledElite，保证定时节奏必出精英）；
///   - 命中快照 → 按 sin 从 Catalog 解析 prefab → 刷出 → 挂 EliteBuildCarrier 还原历史 BD
///     → 计入本波清点（精英未死亡/未被附身前，本波不算清场）；
///   - 响应到达时周期已推进（异步往返期间跨周期）→ 丢弃本次投放。
///
/// 战果回传（Meta §6.5）：精英生成 / Fatal / 被 Possess / 造成 Body Fatal / 直接导致 Run Fail
/// 五类事件入队批量上报（POST /api/elite/events，按构筑主人聚合）——荣誉殿堂「异步战绩」的数据源。
/// 仅服务器来源快照回报（本地 Preset 无真实主人）；Body Fatal / Run Fail 按归因窗口判定
/// （EnemyAbility 伤害结算点记录最近精英伤害来源）；失败静默保留队列重试，离线不发注定失败的请求。
///
/// 装配：WaveManager.Start 拉起（EnsureInstance + AttachToWaveManager），常驻跨场景。
/// 也可直接在场景挂载以在 Inspector 配置 serverUrl / catalog。
/// </summary>
public class EliteBuildDirector : MonoBehaviour
{
    public static EliteBuildDirector Instance { get; private set; }

    /// <summary>精英投放成功事件（AudioEventBinder 等订阅；本 Meta 系统保持音频无感知）。</summary>
    public event System.Action<MonsterActor> OnEliteSpawned;

    [Header("服务器")]
    [Tooltip("内容服务器 Base URL（Server/README.md；局域网填服务器内网 IP）。")]
    public string serverUrl = "http://127.0.0.1:8080";
    [Tooltip("单请求超时（秒）。保持较短，服务器不可达时快速失败回退普通波次。")]
    [Min(1)] public int timeoutSeconds = 5;
    [Tooltip("精英系统总开关：关闭后不上传也不请求投放（回退纯单机行为）。")]
    public bool eliteEnabled = true;
    [Tooltip("打印请求/响应原始 JSON 到 Console（联调用）。")]
    public bool logRawResponses = false;

    [Header("怪物目录")]
    [Tooltip("Sin → 怪物 prefab / 显示名映射。未指定时尝试 Resources.Load(\"EliteMonsterCatalog\")。")]
    public EliteMonsterCatalog catalog;

    [Header("投放节奏（Encounter §7，1-based 波次）")]
    [Tooltip("从第几波开始投放精英怪（Encounter §7：W1–W2 不注入，W3 起 Eligible）。")]
    [Min(1)] public int eliteStartWave = 3;
    [Tooltip("投放节奏间隔（Encounter §7 推荐节奏点 W3/W5/W7：3+2 间隔；1=每波投放）。")]
    [Min(1)] public int eliteEveryNWaves = 2;
    [Tooltip("最后一波（如 W8）的投放概率（Encounter §7：高概率，但仍占 Budget，非硬保证）。")]
    [Range(0f, 1f)] public float finalWaveEliteChance = 0.8f;

    [Header("投放难度")]
    [Tooltip("投放序号差：请求第 N 次投放的精英时，筛选 sourceWave >= N + waveGap 的快照（sourceWave = 上传者第几次选卡，与投放序号同量纲）。1=别人多选一次卡时点的构筑，0=同进度，2=越两级。")]
    [Min(0)] public int waveGap = 1;

    [Header("精英强化参数")]
    [Tooltip("旧波次模式与调试精英使用的生命值倍率；持续刷怪模式由 WaveManager 的条目独立配置。")]
    [Min(1f)] public float eliteHealthMultiplier = 2f;
    [Tooltip("旧波次模式与调试精英使用的攻击力倍率；持续刷怪模式由 WaveManager 的条目独立配置。")]
    [Min(1f)] public float eliteAttackDamageMultiplier = 2f;
    [Tooltip("精英视觉尺寸相对普通怪的倍率。只缩放视觉节点，不缩放 Actor 根节点、碰撞体或导航。")]
    [Min(1f)] public float eliteVisualScaleMultiplier = 2f;

    [Header("网络状态")]
    [Tooltip("连续失败多少次后显示网络异常 UI 提示。")]
    [Min(1)] public int offlineThreshold = 2;

    WaveManager boundWaveManager;
    RunSession boundRunSession;
    PossessionManager boundPossessionManager;
    RunPhase lastPhase = RunPhase.Opening;
    int consecutiveFailures;
    /// <summary>离线状态（探活 / 请求失败置 true，任一成功复位）：离线时跳过网络请求，直接本地 Preset 兜底。</summary>
    bool offlineDetected;

    [Header("战果回传（Meta §6.5）")]
    [Tooltip("致命事件归因窗口（秒）：玩家 Body Fatal / Soul Death 发生前，该窗口内有精英能力命中玩家才归因该精英。TUNABLE。")]
    [Min(0f)] public float fatalAttributionWindow = 4f;

    /// <summary>待上报战果事件（失败保留，下次事件 / 阶段切换重试）。</summary>
    readonly List<EliteEventEntry> pendingEvents = new List<EliteEventEntry>();
    bool eventFlushInFlight;
    EliteBuildCarrier lastEliteDamager;
    float lastEliteDamageTime = float.NegativeInfinity;

    const string DebugEliteRewardRunId = "__debug_elite_run__";
    string eliteRewardRunId;
    int eliteKillRewardCount;

    /// <summary>确保实例存在（场景挂载优先，否则创建常驻对象）。</summary>
    public static EliteBuildDirector EnsureInstance()
    {
        if (Instance != null) return Instance;
        var existing = FindObjectOfType<EliteBuildDirector>();
        if (existing != null) return existing; // Awake 已注册 Instance
        var go = new GameObject("[EliteBuildDirector]");
        DontDestroyOnLoad(go);
        return go.AddComponent<EliteBuildDirector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        MonsterActor.OnMonsterKilled += HandleMonsterKilled;
        if (catalog == null)
            catalog = Resources.Load<EliteMonsterCatalog>("EliteMonsterCatalog");
        if (EliteNetworkStatusUI.Instance == null)
            gameObject.AddComponent<EliteNetworkStatusUI>();
        if (eliteEnabled)
            ProbeServer();
    }

    void OnDestroy()
    {
        MonsterActor.OnMonsterKilled -= HandleMonsterKilled;
        if (Instance == this) Instance = null;
        Attach(null);
        if (boundRunSession != null)
        {
            boundRunSession.OnPhaseChanged -= HandlePhaseChanged;
            boundRunSession = null;
        }
        if (boundPossessionManager != null)
        {
            boundPossessionManager.OnPossessionStarted -= HandlePossessionStarted;
            boundPossessionManager.OnBodyDiedWhilePossessing -= HandleBodyDied;
            boundPossessionManager = null;
        }
    }

    // ── 网络状态 ──

    /// <summary>启动时探活：检测服务器是否可达，不可达则提前标记离线并显示 UI 提示。</summary>
    async void ProbeServer()
    {
        bool ok = await Client().Ping();
        if (ok)
        {
            Debug.Log("[EliteBuildDirector] 服务器探活成功，精英系统就绪。");
            OnNetworkSuccess();
        }
        else
        {
            Debug.LogWarning("[EliteBuildDirector] 服务器探活失败，精英系统进入离线模式（对局不受影响）。");
            OnNetworkFailure();
        }
    }

    void OnNetworkFailure()
    {
        offlineDetected = true; // Meta §6.3：无网 → 后续波次直接本地 Preset 兜底，不发注定失败的请求
        consecutiveFailures++;
        if (consecutiveFailures >= offlineThreshold)
        {
            var ui = EliteNetworkStatusUI.Instance;
            if (ui != null) ui.Show();
        }
    }

    void OnNetworkSuccess()
    {
        offlineDetected = false; // 恢复在线：后续波次回到"在线真实快照优先"
        if (consecutiveFailures >= offlineThreshold)
        {
            var ui = EliteNetworkStatusUI.Instance;
            if (ui != null) ui.Hide();
        }
        consecutiveFailures = 0;
    }

    /// <summary>绑定当前场景的 WaveManager（场景重载后重新绑定）与常驻 RunSession。幂等。</summary>
    public void AttachToWaveManager(WaveManager wm)
    {
        if (boundWaveManager != wm)
        {
            Attach(wm);
        }
        var run = RunSession.EnsureInstance();
        if (boundRunSession != run)
        {
            if (boundRunSession != null) boundRunSession.OnPhaseChanged -= HandlePhaseChanged;
            boundRunSession = run;
            boundRunSession.OnPhaseChanged += HandlePhaseChanged;
        }
    }

    void Attach(WaveManager wm)
    {
        if (boundWaveManager != null)
        {
            boundWaveManager.OnWaveStarted -= HandleWaveStarted;
        }
        boundWaveManager = wm;
        if (boundWaveManager != null)
        {
            boundWaveManager.OnWaveStarted += HandleWaveStarted;
        }
    }

    void Update()
    {
        EnsureEliteRewardRun();
        // PossessionManager 为场景级实例（场景重载后重建），轮询重绑（同 RunStatsCollector 模式）
        var pm = PossessionManager.Instance;
        if (pm == boundPossessionManager) return;
        if (boundPossessionManager != null)
        {
            boundPossessionManager.OnPossessionStarted -= HandlePossessionStarted;
            boundPossessionManager.OnBodyDiedWhilePossessing -= HandleBodyDied;
        }
        boundPossessionManager = pm;
        if (pm != null)
        {
            pm.OnPossessionStarted += HandlePossessionStarted;        // 战果回传：精英被 Possess（Meta §6.5）
            pm.OnBodyDiedWhilePossessing += HandleBodyDied;           // 战果回传：精英造成 Body Fatal（归因）
        }
    }

    EliteNetClient Client() => new EliteNetClient(serverUrl, timeoutSeconds, logRawResponses);

    // ── 投放（Encounter §7 节奏 + Meta §6.3 来源）──

    void HandleWaveStarted(int waveIndex, WaveConfig wave)
    {
        if (!eliteEnabled) return;
        int waveNumber = waveIndex + 1;
        if (!ShouldSpawnEliteAt(waveNumber)) return;

        if (offlineDetected)
        {
            // Meta §6.3：无网 → 本地 Preset 兜底（跳过注定失败的请求）
            InjectPreset(waveIndex, waveNumber, "离线");
            return;
        }
        RequestElite(waveIndex, waveNumber);
    }

    /// <summary>
    /// Encounter §7 节奏判定：W1–W2 不注入（eliteStartWave=3）；eliteStartWave 起按
    /// eliteEveryNWaves 间隔投放（默认 3/2 → W3/W5/W7 推荐节奏点，非硬保证）；
    /// 最后一波按 finalWaveEliteChance 高概率请求（非硬保证）。
    /// </summary>
    bool ShouldSpawnEliteAt(int waveNumber)
    {
        if (waveNumber < eliteStartWave) return false;

        int total = boundWaveManager != null ? boundWaveManager.TotalWaveCount : 0;
        if (total > 0 && waveNumber == total)
            return UnityEngine.Random.value <= finalWaveEliteChance; // 最后一波：高概率非硬保证

        return (waveNumber - eliteStartWave) % eliteEveryNWaves == 0; // 节奏点投放
    }

    /// <summary>异步往返期间波次可能已清场/推进；投放执行前确认波次仍为发起波。</summary>
    bool IsWaveStillCurrent(int waveIndex)
    {
        var wm = boundWaveManager;
        return wm != null && wm.IsWaveActive && wm.CurrentWaveIndex == waveIndex;
    }

    async void RequestElite(int waveIndex, int waveNumber)
    {
        ElitePickResp resp;
        try
        {
            resp = await Client().Pick(DeviceIdentity.Id, waveNumber, waveGap);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EliteBuildDirector] W{waveNumber} 精英请求失败（{e.Message}），改用本地 Preset 兜底。");
            OnNetworkFailure();
            InjectPreset(waveIndex, waveNumber, "网络失败");
            return;
        }
        OnNetworkSuccess();

        if (resp == null || !resp.HasSnapshot)
        {
            // Meta §6.3：服务器候选库为空 → 本地 Preset 兜底
            Debug.Log($"[EliteBuildDirector] W{waveNumber} 服务器无精英候选（snapshot=null），改用本地 Preset 兜底。");
            InjectPreset(waveIndex, waveNumber, "空候选库");
            return;
        }

        // 异步往返期间波次可能已清场/推进，过期投放丢弃
        if (!IsWaveStillCurrent(waveIndex))
        {
            Debug.Log($"[EliteBuildDirector] W{waveNumber} 精英响应到达时波次已推进，丢弃本次投放。");
            return;
        }

        InjectElite(boundWaveManager, resp.snapshot, waveNumber, resp.relaxed);
    }

    /// <summary>
    /// 本地 Preset 兜底（Meta §6.3：无网 / 空候选库；兜底不改变 Run 流程、不产生 Gameplay buff）。
    /// Catalog 未配置 presetSnapshots（OD-CAN-001 内容 OPEN）时本波不投放，回退普通波次。
    /// </summary>
    void InjectPreset(int waveIndex, int waveNumber, string reason)
    {
        if (!IsWaveStillCurrent(waveIndex))
        {
            Debug.Log($"[EliteBuildDirector] W{waveNumber} Preset 兜底注入时波次已推进，丢弃。");
            return;
        }
        var snapshot = catalog != null ? catalog.PickPresetSnapshot() : null;
        if (snapshot == null)
        {
            Debug.Log($"[EliteBuildDirector] W{waveNumber} {reason}且本地 Preset 池为空，本波不投放，回退普通波次（Preset 内容待策划配置：OD-CAN-001）。");
            return;
        }
        InjectElite(boundWaveManager, snapshot, waveNumber, false);
    }

    /// <summary>
    /// 配置精英的联网入口：WaveManager 的每条连续投放表调用。
    /// 精英的时间、罪印、生命和攻击力由该条配置决定；投放序号仅用于快照请求与战果归因。
    /// </summary>
    /// <returns>同步兜底路径返回是否已注入；在线路径返回请求是否已受理（注入结果异步完成）。</returns>
    public bool RequestScheduledElite(SinType sin, int scheduleIndex,
        float healthMultiplier, float attackMultiplier)
    {
        // 进度信号在守卫前推进：投放槽位由 WaveManager 按时间消费，注入成败与否时间深度均已到达该序号。
        var run = RunSession.Instance;
        if (run != null && run.HasActiveRun)
        {
            if (deployProgressRunId != run.RunId) { deployProgressRunId = run.RunId; reachedDeployCount = 0; }
            if (scheduleIndex + 1 > reachedDeployCount) reachedDeployCount = scheduleIndex + 1;
        }
        if (run != null && run.IsBossMode) return false;
        if (!eliteEnabled)
        {
            Debug.LogWarning("[EliteBuildDirector] 精英系统总开关 eliteEnabled=false，跳过定时投放。");
            return false;
        }
        if (catalog == null)
            catalog = Resources.Load<EliteMonsterCatalog>("EliteMonsterCatalog");
        if (catalog == null || catalog.Find(sin) == null || catalog.Find(sin).prefab == null)
        {
            Debug.LogWarning($"[EliteBuildDirector] 定时投放缺少 Catalog 条目：{sin}。");
            return false;
        }

        WaveManager wm = boundWaveManager != null ? boundWaveManager : FindObjectOfType<WaveManager>();
        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (wm == null || spawner == null || spawner.TrackedMonsterCount >= spawner.maxCombatMonsters)
        {
            Debug.LogWarning($"[EliteBuildDirector] 定时投放被拦截：wm={(wm != null)}, spawner={(spawner != null)}, 在场={(spawner != null ? spawner.TrackedMonsterCount : -1)}/上限={(spawner != null ? spawner.maxCombatMonsters : -1)}。");
            return false;
        }

        if (offlineDetected)
        {
            // Meta §6.3：无网 → 不发注定失败的请求，直接本地兜底（Preset → 空快照）
            return InjectScheduledFallback(sin, scheduleIndex, healthMultiplier, attackMultiplier, "离线");
        }

        RequestScheduledEliteAsync(sin, scheduleIndex, healthMultiplier, attackMultiplier);
        return true; // 请求已受理；注入在异步回调完成后（WaveManager 日志计数按受理口径）
    }

    async void RequestScheduledEliteAsync(SinType sin, int scheduleIndex,
        float healthMultiplier, float attackMultiplier)
    {
        ElitePickResp resp;
        try
        {
            resp = await Client().Pick(DeviceIdentity.Id, scheduleIndex + 1, waveGap);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EliteBuildDirector] 第 {scheduleIndex + 1} 次投放请求失败（{e.Message}），改用本地兜底。");
            OnNetworkFailure();
            InjectScheduledFallback(sin, scheduleIndex, healthMultiplier, attackMultiplier, "网络失败");
            return;
        }
        OnNetworkSuccess();

        if (resp == null || !resp.HasSnapshot)
        {
            // Meta §6.3：服务器候选库为空 → 本地兜底
            Debug.Log($"[EliteBuildDirector] 第 {scheduleIndex + 1} 次投放：服务器无精英候选（snapshot=null），改用本地兜底。");
            InjectScheduledFallback(sin, scheduleIndex, healthMultiplier, attackMultiplier, "空候选库");
            return;
        }

        if (!IsScheduledSpawnStillActive())
        {
            Debug.Log($"[EliteBuildDirector] 第 {scheduleIndex + 1} 次投放响应到达时非 Boss 阶段已结束，丢弃本次投放。");
            return;
        }

        InjectElite(boundWaveManager, resp.snapshot, scheduleIndex + 1, resp.relaxed,
            healthMultiplier, attackMultiplier);
    }

    /// <summary>
    /// 定时投放兜底链：本地 Preset（Catalog.presetSnapshots，OD-CAN-001 内容 OPEN）→
    /// 空快照注入（TryInjectScheduledElite，保持定时节奏必出精英）。
    /// </summary>
    bool InjectScheduledFallback(SinType sin, int scheduleIndex,
        float healthMultiplier, float attackMultiplier, string reason)
    {
        if (!IsScheduledSpawnStillActive())
        {
            Debug.Log($"[EliteBuildDirector] 第 {scheduleIndex + 1} 次投放{reason}，兜底注入时非 Boss 阶段已结束，丢弃。");
            return false;
        }
        var snapshot = catalog != null ? catalog.PickPresetSnapshot() : null;
        if (snapshot != null)
        {
            InjectElite(boundWaveManager, snapshot, scheduleIndex + 1, false,
                healthMultiplier, attackMultiplier);
            return true;
        }
        // Preset 池空（内容待策划配置：OD-CAN-001）：空快照注入保底——定时节奏必出精英（无他人构筑、仅数值强化）
        return TryInjectScheduledElite(sin, scheduleIndex, healthMultiplier, attackMultiplier);
    }

    /// <summary>
    /// 定时投放的本地空快照注入（无网络依赖，联网路径 RequestScheduledElite 的最终兜底）：
    /// 按请求 Sin 构造空 BD 快照（bdCount=0，来源 "scheduled"）直接注入精英。
    /// The catalog still owns the actual Elite prefab mapping and runtime modifiers.
    /// </summary>
    public bool TryInjectScheduledElite(SinType sin, int scheduleIndex,
        float healthMultiplier, float attackMultiplier)
    {
        if (!eliteEnabled) return false;
        if (catalog == null)
            catalog = Resources.Load<EliteMonsterCatalog>("EliteMonsterCatalog");
        if (catalog == null || catalog.Find(sin) == null || catalog.Find(sin).prefab == null)
        {
            Debug.LogWarning($"[EliteBuildDirector] 新逻辑精英缺少 Catalog 条目：{sin}。");
            return false;
        }

        WaveManager wm = boundWaveManager != null ? boundWaveManager : FindObjectOfType<WaveManager>();
        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (wm == null || spawner == null)
            return false;

        EliteMonsterCatalog.Entry entry = catalog.Find(sin);
        EliteSnapshotItem snapshot = new EliteSnapshotItem
        {
            snapshotId = 0,
            sourcePlayerId = "scheduled",
            runId = RunSession.Instance != null ? RunSession.Instance.RunId : "scheduled",
            sin = EliteMonsterCatalog.WireName(sin),
            monsterType = entry.displayName,
            bdData = new List<BdCardEntry>(),
            bdCount = 0,
            sourceWave = scheduleIndex + 1,
            gameTime = (long)(RunSpawnDirector.Instance != null ? RunSpawnDirector.Instance.ActiveCombatSeconds : 0f),
        };

        return InjectElite(wm, snapshot, scheduleIndex + 1, false,
            healthMultiplier, attackMultiplier);
    }

    /// <summary>F9 注入：解析快照 → 刷出 → 挂载体还原历史 BD → 计入本波清点。</summary>
    bool InjectElite(WaveManager wm, EliteSnapshotItem snapshot, int waveNumber, bool relaxed,
        float healthMultiplier = -1f, float attackMultiplier = -1f)
    {
        if (RunSession.Instance != null && RunSession.Instance.IsBossMode) return false;
        if (catalog == null)
        {
            Debug.LogWarning("[EliteBuildDirector] 未配置 EliteMonsterCatalog，无法注入精英（Resources/EliteMonsterCatalog.asset 或场景挂载指定）。");
            return false;
        }
        var entry = catalog.FindByWireName(snapshot.sin);
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning($"[EliteBuildDirector] Catalog 未配置 sin='{snapshot.sin}' 的 prefab，本波不投放。");
            return false;
        }
        var spawner = MonsterSpawner.Instance;
        if (spawner == null)
        {
            Debug.LogWarning("[EliteBuildDirector] 场景中无 MonsterSpawner，本波不投放。");
            return false;
        }
        if (!spawner.TryGetEliteSpawnPosition(out Vector3 pos))
        {
            Debug.LogWarning("[EliteBuildDirector] 屏幕内无合法精英刷怪点，本波不投放。");
            return false;
        }

        var monster = spawner.SpawnEliteMonster(entry.prefab, pos);
        if (monster == null)
        {
            Debug.Log("[EliteBuildDirector] 精英 prefab 生成失败（对象池或资源无效）。");
            return false;
        }

        // 遭遇端命名（方案 Phase 4 / ADR-3）：优先用快照自带的词缀名。
        // monsterType 格式为 "sin|w1|w2|…"；旧格式（无 '|'）或解析失败 → 回退 catalog 名（验收项 15）。
        // 用上传端冻结的词序列而非本地重算：否则同一只怪会因遭遇者卡池不同而改名。
        string epithetName = ResolveEncounterName(snapshot, entry);

        var carrier = monster.gameObject.AddComponent<EliteBuildCarrier>();
        carrier.Init(snapshot, epithetName);
        ApplyEliteRuntimeSettings(monster, healthMultiplier, attackMultiplier);
        AnnounceEliteSpawn(monster, epithetName);
        wm.RegisterExternalWaveMonster(monster);
        EnqueueEliteEvent("spawned", carrier, waveNumber); // 战果回传：精英成功生成（Meta §6.5）

        OnEliteSpawned?.Invoke(monster); // 广播投放成功（音频等外部系统订阅，本系统不感知具体订阅方）

        Debug.Log($"[EliteBuildDirector] W{waveNumber} 投放精英 '{monster.displayName}'（sin={snapshot.sin}, bdCount={snapshot.bdCount}, sourceWave={snapshot.sourceWave}, from={snapshot.sourcePlayerId}, relaxed={relaxed}, cards=[{CardIdList(snapshot)}]）。");
        return true;
    }

    /// <summary>投放日志用：快照卡 ID 逗号列表（空快照/无卡返回空串）。</summary>
    static string CardIdList(EliteSnapshotItem snapshot)
    {
        if (snapshot == null || snapshot.bdData == null || snapshot.bdData.Count == 0) return "";
        var ids = new List<string>(snapshot.bdData.Count);
        foreach (var c in snapshot.bdData)
            if (c != null && !string.IsNullOrEmpty(c.cardId)) ids.Add(c.cardId);
        return string.Join(",", ids);
    }

    /// <summary>
    /// Applies the shared Elite runtime presentation and combat settings. The debug key [9]
    /// uses this same entry point so testing and wave injection cannot drift apart.
    /// </summary>
    public void ApplyEliteRuntimeSettings(MonsterActor monster,
        float healthMultiplier = -1f, float attackMultiplier = -1f)
    {
        if (monster == null) return;
        monster.ApplyEliteRuntimeModifiers(
            healthMultiplier > 0f ? healthMultiplier : eliteHealthMultiplier,
            attackMultiplier > 0f ? attackMultiplier : eliteAttackDamageMultiplier,
            eliteVisualScaleMultiplier);
    }

    bool IsScheduledSpawnStillActive()
    {
        WaveManager wm = boundWaveManager != null ? boundWaveManager : FindObjectOfType<WaveManager>();
        return wm != null && wm.IsUsingNewSpawnLogic && wm.IsWaveActive
            && (RunSpawnDirector.Instance == null || !RunSpawnDirector.Instance.NonBossTimeReached);
    }

    /// <summary>Shows the Catalog-designed monster name when an Elite appears.</summary>
    public void AnnounceEliteSpawn(MonsterActor monster, string designedName = null)
    {
        string name = !string.IsNullOrWhiteSpace(designedName)
            ? designedName.Trim()
            : (monster != null ? monster.displayName : string.Empty);
        const string elitePrefix = "精英·";
        if (name.StartsWith(elitePrefix, StringComparison.Ordinal))
            name = name.Substring(elitePrefix.Length);
        EliteAnnouncementUI.ShowElite(name);
    }

    // ── 上传（Meta §6.1；sourceWave = 第几次选卡，Owner 2026-08-26 决策）──

    // 本局选卡会话计数（波次选卡 + 精英击杀奖励选卡）：sourceWave = 第几次选卡。
    // runId 变化时重置；读档恢复用已完成波数作下限（历史奖励选卡次数不存档，波数近似）。
    int pickSessionCount;
    string pickSessionRunId;

    // 连续刷怪模式进度信号（波次数据缺口方案 B，Owner 2026-08-28 拍板）：
    // reachedDeployCount = 本局已触发的定时投放序号（1-based，时间驱动；投放槽位由 WaveManager
    // 按时间消费，注入成败不影响深度语义）。runId 变化时重置；波次模式不走定时投放入口，恒 0，
    // 不影响 OnWaveStarted 既有口径。消费方：RunStatsCollector（到达深度合并）、
    // 荣誉殿堂滚动写入与战果事件 wave 字段（第 N 阶段口径）。
    /// <summary>本局已触发的定时投放序号（1-based；连续模式进度信号，波次模式恒 0）。</summary>
    public int ReachedDeployCount => reachedDeployCount;
    int reachedDeployCount;
    string deployProgressRunId;

    void HandlePhaseChanged(RunPhase next)
    {
        var prev = lastPhase;
        lastPhase = next;
        if (!eliteEnabled) return;
        var run = RunSession.Instance;
        if (run == null || !run.HasActiveRun) return;

        // 战果回传（Meta §6.5）：Run Fail 归因上报 + 终局兜底 flush（失败保留队列待下局在线重试）
        if (next == RunPhase.Failed && HasRecentEliteDamage)
            EnqueueEliteEvent("runFail", lastEliteDamager);
        if (next == RunPhase.Failed || next == RunPhase.Result)
            TryFlushEliteEvents();

        if (prev == RunPhase.Choice && next == RunPhase.Waves)
            UploadBuildSnapshots(AdvancePickSessionCount(run), "wave"); // 波次选卡完成：sourceWave = 第几次选卡
        else if (next == RunPhase.Final)
        {
            // Final 上传使用独立阶段标记 stage="final"；sourceWave = 当前选卡计数——
            // Boss 可能提前于最后一波刷出（RunSpawnDirector 按时长/难度触发），不虚构补满计数；
            // 服务器 schema 支持 stage 前透传忽略。
            UploadBuildSnapshots(CurrentPickSessionCount(run), "final");
        }
    }

    /// <summary>
    /// 选卡会话计数 +1（波次选卡 / 精英奖励选卡共用），返回新计数作为上传 sourceWave。
    /// runId 变化（新局）时重置；读档恢复用已完成波数下限抬底——已完成波数 = 已完成波次
    /// 选卡数，为可靠下限（读档前的奖励选卡次数不存档，从当前起算）。
    /// </summary>
    int AdvancePickSessionCount(RunSession run)
    {
        ResetPickSessionCountIfNewRun(run);
        pickSessionCount++;
        int waveFloor = run.CompletedWaveIndex + 1; // 已完成波次选卡数（1-based）
        if (pickSessionCount < waveFloor) pickSessionCount = waveFloor;
        return pickSessionCount;
    }

    /// <summary>当前选卡计数（不推进；Final 上传用），读档下限口径同上。</summary>
    int CurrentPickSessionCount(RunSession run)
    {
        ResetPickSessionCountIfNewRun(run);
        int waveFloor = run.CompletedWaveIndex + 1;
        return Mathf.Max(pickSessionCount, waveFloor);
    }

    void ResetPickSessionCountIfNewRun(RunSession run)
    {
        if (pickSessionRunId == run.RunId) return;
        pickSessionRunId = run.RunId;
        pickSessionCount = 0;
    }

    async void UploadBuildSnapshots(int sourceWave, string stage)
    {
        var snapshots = BuildSnapshots(sourceWave, stage);
        if (snapshots.Count == 0) return; // 0 投资的 Sin 不上行（§6.1）
        var run = RunSession.Instance;
        if (run == null) return;

        // 荣誉殿堂 §5.2：对局内持续更新构筑快照——与上传同源双写，本地无条件落盘（离线/上传失败不影响冻结源）。
        // reachedWave = max(已完成波次, 已触发投放序号)（「第 N 阶段」展示口径，方案 B）；
        // wire 的 sourceWave = 第几次选卡，两语义分离。
        HallOfFameStore.UpsertFromSnapshots(run.RunId,
            Mathf.Max(run.CompletedWaveIndex + 1, reachedDeployCount), stage, snapshots);

        try
        {
            var resp = await Client().UploadSnapshots(new UploadSnapshotsReq
            {
                playerId = DeviceIdentity.Id,
                runId = run.RunId,
                snapshots = snapshots,
            });
            Debug.Log($"[EliteBuildDirector] BD 快照上传完成：accepted={resp.accepted}/{snapshots.Count}（sourceWave={sourceWave}, stage={stage}, runId={run.RunId}）。");
            OnNetworkSuccess();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EliteBuildDirector] BD 快照上传失败（静默跳过，不影响对局）：{e.Message}");
            OnNetworkFailure();
        }
    }

    /// <summary>
    /// 组装本局当前所有 bdCount>=1 的 Sin 快照（Meta §6.1）：
    /// 只含 MonsterType / TypeGrowth 卡（不含 Basic/Global）；bdData 为 Card ID 清单（stack 恒 1）；
    /// stage 为独立阶段标记（"wave" / "final"，§6.7）。
    /// </summary>
    List<SnapshotEntry> BuildSnapshots(int sourceWave, string stage)
    {
        var result = new List<SnapshotEntry>();
        var cm = CardManager.Instance;
        if (cm == null) return result;

        var bySin = new Dictionary<SinType, List<BdCardEntry>>();
        foreach (var id in cm.UnlockedEffects)
        {
            var card = cm.FindCard(id);
            if (card == null) continue;
            if (card.category != CardCategory.MonsterType && card.category != CardCategory.TypeGrowth) continue;
            if (card.monsterType == SinType.None) continue;
            if (!bySin.TryGetValue(card.monsterType, out var list))
            {
                list = new List<BdCardEntry>();
                bySin.Add(card.monsterType, list);
            }
            list.Add(new BdCardEntry { cardId = id, stack = 1 });
        }

        long gameTime = (long)(GameManager.Instance != null ? GameManager.Instance.gameTimer : 0f);
        foreach (var kv in bySin)
        {
            string sinWire = EliteMonsterCatalog.WireName(kv.Key);
            result.Add(new SnapshotEntry
            {
                sin = sinWire,
                // 词缀命名（方案 Phase 4 / ADR-3）：monsterType 载"sin|中性词序列"，
                // 由**上传端**生成，遭遇端套本机模板 —— 保证同一只怪在任何人屏幕上同名。
                // 服务器只校验非空、不解析内容，故改格式无需改服务器、无需数据迁移。
                // 生成失败（目录缺失/无词）时回退为纯 catalog 名（旧格式，遭遇端自动降级）。
                monsterType = ResolveMonsterType(kv.Key, kv.Value),
                bdCount = kv.Value.Count,
                bdData = kv.Value,
                sourceWave = sourceWave,
                stage = stage,
                gameTime = gameTime,
            });
        }
        return result;
    }

    /// <summary>
    /// 编码 monsterType 载荷："sin|w1|w2|…"。
    /// 无词缀时返回纯 catalog 名（旧格式），遭遇端按"无词缀"降级（验收项 15）。
    /// </summary>
    string ResolveMonsterType(SinType sin, List<BdCardEntry> bdData)
    {
        string sinWire = EliteMonsterCatalog.WireName(sin);
        var catalog = CardEpithetCatalog.Instance;
        if (catalog == null || bdData == null || bdData.Count == 0)
            return ResolveDisplayName(sin);

        var ids = new List<string>(bdData.Count);
        foreach (var c in bdData)
            if (c != null && !string.IsNullOrEmpty(c.cardId)) ids.Add(c.cardId);

        var words = CardEpithetGenerator.Generate(ids, catalog);
        return CardEpithetGenerator.EncodeForWire(sinWire, words);
    }

    /// <summary>
    /// 遭遇端解析精英名字（方案 Phase 4 / ADR-3）。
    /// 快照带词缀序列时用当前显示线套模板；否则回退 catalog 名（旧格式快照 / 定时投放 / 无词缀）。
    /// 本机词表未知的中性词会**原样显示**（中性词本身是可读中文），不丢字不报错（验收项 16）。
    /// </summary>
    static string ResolveEncounterName(EliteSnapshotItem snapshot, EliteMonsterCatalog.Entry entry)
    {
        string fallback = entry != null && !string.IsNullOrEmpty(entry.displayName)
            ? entry.displayName
            : (snapshot != null ? snapshot.sin : "");
        if (snapshot == null || string.IsNullOrEmpty(snapshot.monsterType)) return fallback;

        // 旧格式：纯 catalog 名，无分隔符 → 无词缀
        if (!CardEpithetGenerator.TryDecodeFromWire(snapshot.monsterType, out string sinWire, out string[] words))
            return fallback;

        if (words == null || words.Length == 0) return fallback;   // 无词缀（bdCount=0 的定时投放精英）

        string name = CardEpithetGenerator.Format(sinWire, words, words.Length, CardEpithetCatalog.Instance);
        return string.IsNullOrEmpty(name) ? fallback : name;
    }

    string ResolveDisplayName(SinType sin)
    {
        var entry = catalog != null ? catalog.Find(sin) : null;
        return entry != null && !string.IsNullOrEmpty(entry.displayName)
            ? entry.displayName
            : EliteMonsterCatalog.WireName(sin);
    }

    // ── 战果回传（Meta §6.5：荣誉殿堂「异步战绩」的唯一数据源）──

    /// <summary>
    /// EnemyAbility 归因钩子：精英能力命中玩家（魂或当前附身身体）时记录来源与时间。
    /// Body Fatal / Soul Death 发生在归因窗口内才归因该精英（环境伤害 / 自然衰减不误归因）。
    /// </summary>
    public static void NoteEliteDamagedPlayer(EliteBuildCarrier elite)
    {
        var d = Instance;
        if (d == null || elite == null) return;
        d.lastEliteDamager = elite;
        d.lastEliteDamageTime = Time.unscaledTime;
    }

    /// <summary>归因窗口内是否有精英伤害命中玩家。</summary>
    bool HasRecentEliteDamage =>
        lastEliteDamager != null && Time.unscaledTime - lastEliteDamageTime <= fatalAttributionWindow;

    void HandleMonsterKilled(MonsterActor monster)
    {
        if (monster == null || !monster.wasKilledByPlayer)
            return;
        EliteBuildCarrier carrier = EliteBuildCarrier.Get(monster);
        if (carrier == null) return;

        // 精英 Fatal（§6.5）：在致死伤害结算时计入；脱离附身时的消散和调试清场不计入。
        EnqueueEliteEvent("fatal", carrier);
        QueueEliteCardReward(monster);
    }

    void EnsureEliteRewardRun()
    {
        string currentRunId = RunSession.Instance != null && RunSession.Instance.HasActiveRun
            ? RunSession.Instance.RunId
            : DebugEliteRewardRunId;
        if (string.IsNullOrEmpty(currentRunId))
            currentRunId = DebugEliteRewardRunId;
        if (eliteRewardRunId == currentRunId) return;

        eliteRewardRunId = currentRunId;
        eliteKillRewardCount = 0;
    }

    void QueueEliteCardReward(MonsterActor monster)
    {
        EnsureEliteRewardRun();
        CardManager cardManager = CardManager.Instance;
        if (string.IsNullOrEmpty(eliteRewardRunId) || monster == null || cardManager == null)
            return;
        if (cardManager.eliteKillCardRewardLimit > 0
            && eliteKillRewardCount >= cardManager.eliteKillCardRewardLimit)
            return;

        Vector3 deathPosition = monster.transform.position;
        int waveIndex = boundWaveManager != null ? boundWaveManager.CurrentWaveIndex : -1;

        // Pass v1 §2.3：击杀奖励次数绑定 Elite Schedule Entry（按 Sin）；
        // rewardPickCount >= 2 时每颗宝石复用 doublePick 机制（1 Gem → 连续 2 次选卡）。
        int rewardPickCount = boundWaveManager != null
            ? boundWaveManager.GetEliteRewardPickCount(monster.sinType)
            : 1;
        bool doublePick = rewardPickCount >= 2;

        int eliteGemCount = cardManager.ResolveEliteGemDropCount();
        int spawned = cardManager.SpawnCardOfferGemScatter(
            deathPosition,
            eliteGemCount,
            doublePick,
            false,
            waveIndex,
            CardOfferGemSource.Elite,
            OnEliteCardGemCompleted);
        if (spawned == 0) return;

        eliteKillRewardCount++;
        string rewardLimitLabel = cardManager.eliteKillCardRewardLimit > 0
            ? cardManager.eliteKillCardRewardLimit.ToString()
            : "不限";
        Debug.Log($"[EliteBuildDirector] 精英击杀奖励：第 {eliteKillRewardCount}/{rewardLimitLabel} 只，"
            + $"在 {deathPosition} 掉落 {spawned} 颗选卡宝石（每颗拾取后各结算一次选卡）。", this);
    }

    void OnEliteCardGemCompleted()
    {
        // 宝石拾取后选卡完成：BD 可能变化，与波次选卡同口径上传。
        var run = RunSession.Instance;
        if (run != null && run.HasActiveRun && eliteEnabled)
            UploadBuildSnapshots(AdvancePickSessionCount(run), "wave");
    }

    /// <summary>
    /// 离开战斗场景时清理场上尚未拾取的选卡宝石，避免跨场景残留。
    /// 正在展示的选卡由 UIManager 先保存为 Choice 快照，再随场景销毁。
    /// </summary>
    public void CancelPendingCardRewards()
    {
        if (CardManager.Instance != null)
            CardManager.Instance.ClearCardOfferGems();
    }

    void HandlePossessionStarted(MonsterActor body)
    {
        // 精英被 Possess（§6.5）
        EnqueueEliteEvent("possessed", EliteBuildCarrier.Get(body));
    }

    void HandleBodyDied(MonsterActor dead)
    {
        // 精英造成 Body Fatal（§6.5）：死亡的身体本身是精英时不算（那是 fatal/possessed 语义）
        if (EliteBuildCarrier.Get(dead) != null) return;
        if (!HasRecentEliteDamage) return;
        EnqueueEliteEvent("bodyFatal", lastEliteDamager);
    }

    /// <summary>
    /// 事件入队并立即尝试上报。仅服务器来源快照回报（本地 Preset 兜底无真实主人，回报无意义）。
    /// </summary>
    /// <param name="waveOverride">事件进度（投放事件用注入投放序号；-1 = 取当前进度：波次模式 = 当前波，连续模式 = 已触发投放序号）。</param>
    void EnqueueEliteEvent(string type, EliteBuildCarrier carrier, int waveOverride = -1)
    {
        if (carrier == null || !eliteEnabled) return;
        if (carrier.SnapshotId <= 0 || carrier.SourcePlayerId == "local-preset") return;
        pendingEvents.Add(new EliteEventEntry
        {
            snapshotId = carrier.SnapshotId,
            ownerPlayerId = carrier.SourcePlayerId,
            ownerRunId = carrier.RunId,
            sin = carrier.Sin,
            type = type,
            eventId = System.Guid.NewGuid().ToString("N"), // 幂等去重键：上报失败重发同一批事件时，服务端按此跳过重复计数
            wave = waveOverride > 0 ? waveOverride
                : Mathf.Max(boundWaveManager != null ? boundWaveManager.CurrentWaveIndex + 1 : 0, reachedDeployCount),
            gameTime = (long)(GameManager.Instance != null ? GameManager.Instance.gameTimer : 0f),
        });
        TryFlushEliteEvents();
    }

    /// <summary>批量上报待发事件；失败静默保留队列（下次事件 / 阶段切换重试），不影响对局。</summary>
    void TryFlushEliteEvents()
    {
        if (eventFlushInFlight || pendingEvents.Count == 0) return;
        if (offlineDetected) return; // Meta §6.3：离线不发注定失败的请求，队列保留待在线后重试
        eventFlushInFlight = true;
        int count = pendingEvents.Count;
        var batch = new List<EliteEventEntry>(pendingEvents);
        _ = FlushEliteEventsAsync(batch, count);
    }

    async Task FlushEliteEventsAsync(List<EliteEventEntry> batch, int count)
    {
        try
        {
            var resp = await Client().ReportEvents(new ReportEventsReq
            {
                playerId = DeviceIdentity.Id,
                events = batch,
            });
            if (resp != null && resp.ok)
            {
                pendingEvents.RemoveRange(0, count); // 仅移除本批（上报期间新入队的事件保留）
                OnNetworkSuccess();
                Debug.Log($"[EliteBuildDirector] 战果回传成功：{count} 条（accepted={resp.accepted}）。");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EliteBuildDirector] 战果回传失败（保留 {pendingEvents.Count} 条待重试）：{e.Message}");
            OnNetworkFailure();
        }
        finally
        {
            eventFlushInFlight = false;
        }
    }
}
