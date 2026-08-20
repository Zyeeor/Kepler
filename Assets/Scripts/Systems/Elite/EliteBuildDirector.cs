using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 精英怪投放总控（策划案《精英怪筛选-他人BD怪物投放》前台侧，服务器数据来源见 Server/）。
///
/// 上传（§8.1 滚动 upsert）：
///   - 每波选卡完成后（RunFlow Choice→Waves）上传本局所有 bdCount>=1 的 Sin 快照
///     （只含 MonsterType / TypeGrowth 卡，不含 Basic/Global，§8.6；0 投资的 Sin 不上行）；
///   - sourceWave = 刚完成波次（1-based；F1 取"快照拍摄时刻波次"）；
///   - 进入 Final 时再传一次，sourceWave 编码 finalSourceWave（F2：>= 总波次+1，保证 W8 请求主路径可命中）；
///   - 上传失败静默跳过（不影响对局；崩溃/Fail 无需补传，上一波上传已在库）。
///
/// 投放（Encounter §7 节奏点）：
///   - W1–W2 不注入；W3/W5/W7 必请求（guaranteedEliteWaves 可配），最后一波按 finalWaveEliteChance 概率请求；
///   - 命中快照 → 按 sin 从 Catalog 解析 prefab → B 带取点 → 刷出 → 挂 EliteBuildCarrier 还原历史 BD
///     → 计入本波清点（精英不死，本波不算清场）；
///   - snapshot=null / 网络失败 / 离线 → 本波不投放，回退普通波次（§8.5，不做 Preset 兜底）；
///   - 响应到达时波次已推进（异步往返期间清场）→ 丢弃本次投放。
///
/// 装配：WaveManager.Start 拉起（EnsureInstance + AttachToWaveManager），常驻跨场景。
/// 也可直接在场景挂载以在 Inspector 配置 serverUrl / catalog。
/// </summary>
public class EliteBuildDirector : MonoBehaviour
{
    public static EliteBuildDirector Instance { get; private set; }

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
    [Tooltip("从第几波开始投放精英怪（1-based，之前的波次不投放）。")]
    [Min(1)] public int eliteStartWave = 1;
    [Tooltip("每 N 波投放一次精英怪（1=每波都投放，2=隔一波投放一次）。")]
    [Min(1)] public int eliteEveryNWaves = 1;

    [Header("投放难度")]
    [Tooltip("越级波次差：请求第 N 波精英时，筛选 sourceWave >= N + waveGap 的快照。1=别人多打一波的怪，0=同波次，2=越两级。")]
    [Min(0)] public int waveGap = 1;
    [Tooltip("进入 Final 阶段上传时 sourceWave 的编码值（F2：建议 >= 总波次+1，保证 W8 请求主路径 sourceWave >= N+WAVE_GAP 可命中）。")]
    [Min(1)] public int finalSourceWave = 9;

    [Header("网络状态")]
    [Tooltip("连续失败多少次后显示网络异常 UI 提示。")]
    [Min(1)] public int offlineThreshold = 2;

    WaveManager boundWaveManager;
    RunSession boundRunSession;
    RunPhase lastPhase = RunPhase.Opening;
    int consecutiveFailures;

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
        if (catalog == null)
            catalog = Resources.Load<EliteMonsterCatalog>("EliteMonsterCatalog");
        if (EliteNetworkStatusUI.Instance == null)
            gameObject.AddComponent<EliteNetworkStatusUI>();
        if (eliteEnabled)
            ProbeServer();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Attach(null);
        if (boundRunSession != null)
        {
            boundRunSession.OnPhaseChanged -= HandlePhaseChanged;
            boundRunSession = null;
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
        consecutiveFailures++;
        if (consecutiveFailures >= offlineThreshold)
        {
            var ui = EliteNetworkStatusUI.Instance;
            if (ui != null) ui.Show();
        }
    }

    void OnNetworkSuccess()
    {
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
        if (boundWaveManager != null) boundWaveManager.OnWaveStarted -= HandleWaveStarted;
        boundWaveManager = wm;
        if (boundWaveManager != null) boundWaveManager.OnWaveStarted += HandleWaveStarted;
    }

    EliteNetClient Client() => new EliteNetClient(serverUrl, timeoutSeconds, logRawResponses);

    // ── 投放（F8）──

    void HandleWaveStarted(int waveIndex, WaveConfig wave)
    {
        if (!eliteEnabled) return;
        int waveNumber = waveIndex + 1;

        if (waveNumber < eliteStartWave) return;
        if ((waveNumber - eliteStartWave) % eliteEveryNWaves != 0) return;

        RequestElite(waveIndex, waveNumber);
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
            Debug.LogWarning($"[EliteBuildDirector] W{waveNumber} 精英请求失败，本波不投放：{e.Message}");
            OnNetworkFailure();
            return;
        }
        OnNetworkSuccess();

        if (resp == null || !resp.HasSnapshot)
        {
            Debug.Log($"[EliteBuildDirector] W{waveNumber} 无精英候选（snapshot=null），回退普通波次。");
            return;
        }

        // 异步往返期间波次可能已清场/推进，过期投放丢弃
        var wm = boundWaveManager;
        if (wm == null || !wm.IsWaveActive || wm.CurrentWaveIndex != waveIndex)
        {
            Debug.Log($"[EliteBuildDirector] W{waveNumber} 精英响应到达时波次已推进，丢弃本次投放。");
            return;
        }

        InjectElite(wm, resp.snapshot, waveNumber, resp.relaxed);
    }

    /// <summary>F9 注入：解析快照 → 刷出 → 挂载体还原历史 BD → 计入本波清点。</summary>
    void InjectElite(WaveManager wm, EliteSnapshotItem snapshot, int waveNumber, bool relaxed)
    {
        if (catalog == null)
        {
            Debug.LogWarning("[EliteBuildDirector] 未配置 EliteMonsterCatalog，无法注入精英（Resources/EliteMonsterCatalog.asset 或场景挂载指定）。");
            return;
        }
        var entry = catalog.FindByWireName(snapshot.sin);
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning($"[EliteBuildDirector] Catalog 未配置 sin='{snapshot.sin}' 的 prefab，本波不投放。");
            return;
        }
        var spawner = MonsterSpawner.Instance;
        if (spawner == null)
        {
            Debug.LogWarning("[EliteBuildDirector] 场景中无 MonsterSpawner，本波不投放。");
            return;
        }
        if (!spawner.TryGetWaveSpawnPosition(out Vector3 pos))
        {
            Debug.LogWarning("[EliteBuildDirector] 无合法精英刷怪点，本波不投放。");
            return;
        }

        var monster = spawner.SpawnWaveMonster(entry.prefab, pos);
        if (monster == null)
        {
            Debug.Log("[EliteBuildDirector] 全场配额已满，精英未刷出。");
            return;
        }

        var carrier = monster.gameObject.AddComponent<EliteBuildCarrier>();
        carrier.Init(snapshot, entry.displayName);
        wm.RegisterExternalWaveMonster(monster);

        Debug.Log($"[EliteBuildDirector] W{waveNumber} 投放精英 '{monster.displayName}'（sin={snapshot.sin}, bdCount={snapshot.bdCount}, sourceWave={snapshot.sourceWave}, from={snapshot.sourcePlayerId}, relaxed={relaxed}）。");
    }

    // ── 上传（F7 / F2）──

    void HandlePhaseChanged(RunPhase next)
    {
        var prev = lastPhase;
        lastPhase = next;
        if (!eliteEnabled) return;
        var run = RunSession.Instance;
        if (run == null || !run.HasActiveRun) return;

        if (prev == RunPhase.Choice && next == RunPhase.Waves)
            UploadBuildSnapshots(run.CompletedWaveIndex + 1); // 选卡完成：sourceWave = 刚完成波次（1-based）
        else if (next == RunPhase.Final)
            UploadBuildSnapshots(finalSourceWave);          // F2：Final 触发上传并编码
    }

    async void UploadBuildSnapshots(int sourceWave)
    {
        var snapshots = BuildSnapshots(sourceWave);
        if (snapshots.Count == 0) return; // 0 投资的 Sin 不上行（§8.1）
        var run = RunSession.Instance;
        if (run == null) return;

        try
        {
            var resp = await Client().UploadSnapshots(new UploadSnapshotsReq
            {
                playerId = DeviceIdentity.Id,
                runId = run.RunId,
                snapshots = snapshots,
            });
            Debug.Log($"[EliteBuildDirector] BD 快照上传完成：accepted={resp.accepted}/{snapshots.Count}（sourceWave={sourceWave}, runId={run.RunId}）。");
            OnNetworkSuccess();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EliteBuildDirector] BD 快照上传失败（静默跳过，不影响对局）：{e.Message}");
            OnNetworkFailure();
        }
    }

    /// <summary>
    /// 组装本局当前所有 bdCount>=1 的 Sin 快照（§8.1/§8.6）：
    /// 只含 MonsterType / TypeGrowth 卡（不含 Basic/Global）；当前卡系统每卡只出现一次，stack 恒 1。
    /// </summary>
    List<SnapshotEntry> BuildSnapshots(int sourceWave)
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
            result.Add(new SnapshotEntry
            {
                sin = EliteMonsterCatalog.WireName(kv.Key),
                monsterType = ResolveDisplayName(kv.Key),
                bdCount = kv.Value.Count,
                bdData = kv.Value,
                sourceWave = sourceWave,
                gameTime = gameTime,
            });
        }
        return result;
    }

    string ResolveDisplayName(SinType sin)
    {
        var entry = catalog != null ? catalog.Find(sin) : null;
        return entry != null && !string.IsNullOrEmpty(entry.displayName)
            ? entry.displayName
            : EliteMonsterCatalog.WireName(sin);
    }
}
