using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 管理房间内所有波次的生命周期。
///
/// 波次玩法（2026-08-17 重构）：接入当前地图怪物生成框架（MonsterSpawner / MonsterPool /
/// MonsterWaveDef 权重表），替代旧"直接 MonsterPool.Spawn + Enemy 轮询"逻辑。
/// 两种波次模式（每波在检查器切换 WaveConfig.mode）：
///   CountKill 数量波：按权重表持续补刷，累计刷满 totalCount 只后不再补；
///                     玩家清完场上本波怪 → 触发选卡 → 下一波。
///   Timed 时间波：持续补刷 duration 秒，时间到即结算（可选回收剩余在场怪）→ 选卡 → 下一波。
///
/// 与地图框架的衔接：
///   - 波次怪经 MonsterSpawner.SpawnWaveMonster 刷出（AI 直接激活且永不休眠），计入全场配额/追踪；
///   - 波次怪不随 Chunk 休眠/回收/写快照（MonsterSpawner 波次怪标记），死亡/被附身即从本波清点中退场；
///   - 地图静态怪已禁用（MonsterSpawner.enableChunkStaticSpawns=false），场上所有怪物由本系统驱动；
///   - 时间波结算回收剩余怪（不写快照，永久退场）。
///
/// 选卡衔接：波次完成 → choiceBuffer 缓冲（看清战果）→ 触发 OnWaveCompleted
/// （RoomFlowController 或 autoShowChoiceUI 打开 CoreChoiceUI，timeScale=0）→
/// IsDrafting 轮询等待选卡会话结束 → 下一波自动开始（选卡暂停期间不会刷怪）。
/// </summary>
public class WaveManager : MonoBehaviour
{
    /// <summary>当前正在运行的波次索引。</summary>
    public int CurrentWaveIndex { get; private set; } = -1;
    /// <summary>当前波次在场存活（未死亡/未附身/未被回收）的怪物数量。</summary>
    public int EnemiesAlive { get; private set; }
    /// <summary>是否所有波次已完成。</summary>
    public bool AllWavesComplete { get; private set; }
    /// <summary>当前是否在战斗波次中。</summary>
    public bool IsWaveActive { get; private set; }
    /// <summary>时间波剩余秒数（仅 Timed 波运行中 &gt;0；数量波/未运行 = 0）。供 UI 倒计时显示。</summary>
    public float TimeWaveRemaining { get; private set; }

    /// <summary>全局单例（场景中挂一个 WaveManager）。</summary>
    public static WaveManager Instance { get; private set; }

    /// <summary>事件：波次开始。</summary>
    public event Action<int, WaveConfig> OnWaveStarted;
    /// <summary>事件：波次完成（数量波清场 / 时间波到时）。</summary>
    public event Action<int> OnWaveCompleted;
    /// <summary>事件：所有波次完成。</summary>
    public event Action OnAllWavesComplete;
    /// <summary>事件：本波一只怪被消灭（isDowned）。</summary>
    public event Action<MonsterActor> OnWaveEnemyKilled;

    [Header("波次节奏")]
    [Tooltip("波次补刷间隔（秒）：每隔多久按权重表抽一批怪刷出。")]
    [Min(0.05f)] public float spawnInterval = 0.5f;
    [Tooltip("波次完成 → 下一波/选卡前的缓冲（秒），用于看清战果。选卡期间 timeScale=0 会冻结此等待。")]
    [Min(0f)] public float choiceBuffer = 2f;
    [Tooltip("波次完成时自动打开选卡弹窗（无 RoomFlowController 场景的兜底；有 RoomFlow 时其也会触发，CoreChoiceUI 有防重入）。")]
    public bool autoShowChoiceUI = true;
    [Tooltip("时间波结算时回收本波剩余在场怪（不写快照，永久退场）。")]
    public bool recycleRemainingOnTimeUp = true;

    [Header("独立波次配置（无房间模式）")]
    [Tooltip("无房间模式波次列表：场景没有 RoomTemplate/RoomFlowController（纯流送大地图）时，波次管理器直接使用本配置。有 RoomTemplate 时忽略。")]
    public List<WaveConfig> waves = new List<WaveConfig>();
    [Tooltip("无房间模式：首波开始前的准备时间（秒）。")]
    [Min(0f)] public float gracePeriod = 2f;
    [Tooltip("无房间模式：场景加载后自动启动波次（等待地图流送就绪后开刷）。有 RoomTemplate（房间流程接管）时无效。")]
    public bool autoStart = true;
    [Tooltip("自动启动前等待地图流送就绪的最长时间（秒），超时仍强开（刷怪点会自动重试）。")]
    [Min(1f)] public float autoStartTimeout = 30f;

    private RoomTemplate currentTemplate;
    private RoomInstance currentRoom;
    private Coroutine waveRoutine;
    private readonly List<MonsterActor> waveAlive = new List<MonsterActor>();
    private bool isRunning;
    private bool initialized;

    void Awake()
    {
        Instance = this;
        TimeWaveRemaining = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // 无房间模式：未被 RoomFlowController 初始化时，等待地图就绪后自动启动
        if (!autoStart) return;
        StartCoroutine(AutoStartRoutine());
    }

    IEnumerator AutoStartRoutine()
    {
        float waited = 0f;
        while (waited < autoStartTimeout)
        {
            if (initialized && currentTemplate != null) yield break; // 已被房间流程接管
            var system = MapStreamingSystem.Instance;
            if (system != null && system.Registry != null && system.Registry.Count > 0) break;
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        if (waited >= autoStartTimeout && initialized)
            yield break;
        if (!initialized) Initialize();
        Debug.Log($"[WaveManager] 无房间模式自动启动：地图已就绪（等待 {waited:F1}s），波次 {ActiveWaves.Count} 个。");
        StartWaves();
    }

    /// <summary>房间模式初始化：由 RoomFlowController 调用。</summary>
    public void Initialize(RoomTemplate template, RoomInstance room)
    {
        initialized = true;
        currentTemplate = template;
        currentRoom = room;
        CurrentWaveIndex = -1;
        EnemiesAlive = 0;
        AllWavesComplete = false;
        isRunning = true;
        waveAlive.Clear();
        Debug.Log($"[WaveManager] Initialize: room='{template.roomName}', waves={ActiveWaves.Count}");
        if (MonsterSpawner.Instance == null)
            Debug.LogWarning("[WaveManager] 场景中无 MonsterSpawner，波次无法刷怪（怪物生成框架未接入）。");
    }

    /// <summary>无房间模式初始化：使用自身 waves 配置。</summary>
    public void Initialize()
    {
        initialized = true;
        currentTemplate = null;
        currentRoom = null;
        CurrentWaveIndex = -1;
        EnemiesAlive = 0;
        AllWavesComplete = false;
        isRunning = true;
        waveAlive.Clear();
        Debug.Log($"[WaveManager] Initialize(无房间): waves={ActiveWaves.Count}");
        if (MonsterSpawner.Instance == null)
            Debug.LogWarning("[WaveManager] 场景中无 MonsterSpawner，波次无法刷怪（怪物生成框架未接入）。");
    }

    /// <summary>当前生效的波次配置（房间模板优先，无房间用自身配置）。</summary>
    List<WaveConfig> ActiveWaves => currentTemplate != null ? currentTemplate.waves : waves;

    /// <summary>当前生效的首波前准备时间（房间模板优先，无房间用自身配置）。</summary>
    float ActiveGracePeriod => currentTemplate != null ? currentTemplate.gracePeriod : gracePeriod;

    /// <summary>开始波次流程。</summary>
    public void StartWaves()
    {
        if (!isRunning || ActiveWaves == null || ActiveWaves.Count == 0)
        {
            Debug.LogWarning($"[WaveManager] StartWaves SKIPPED: isRunning={isRunning}, waves={ActiveWaves?.Count}");
            return;
        }
        Debug.Log($"[WaveManager] StartWaves: room='{(currentTemplate != null ? currentTemplate.roomName : "(无房间)")}', waves={ActiveWaves.Count}");
        waveRoutine = StartCoroutine(WaveRoutine());
    }

    /// <summary>停止波次。</summary>
    public void StopWaves()
    {
        isRunning = false;
        if (waveRoutine != null) StopCoroutine(waveRoutine);
        waveRoutine = null;
        waveAlive.Clear();
        EnemiesAlive = 0;
        IsWaveActive = false;
    }

    IEnumerator WaveRoutine()
    {
        Debug.Log($"[WaveManager] WaveRoutine START: room='{(currentTemplate != null ? currentTemplate.roomName : "(无房间)")}', waves={ActiveWaves.Count}, grace={ActiveGracePeriod}");

        // Grace period
        if (ActiveGracePeriod > 0f)
            yield return new WaitForSeconds(ActiveGracePeriod);

        for (int i = 0; i < ActiveWaves.Count; i++)
        {
            if (!isRunning) yield break;

            var wave = ActiveWaves[i];
            CurrentWaveIndex = i;
            if (currentRoom != null && currentRoom.context != null)
                currentRoom.context.CurrentWaveIndex = i;

            // 等待该波次的开始时间
            if (wave.startTime > 0f)
                yield return new WaitForSeconds(wave.startTime);

            IsWaveActive = true;
            waveAlive.Clear();
            OnWaveStarted?.Invoke(i, wave);
            Debug.Log($"[WaveManager] Wave {i} START: mode={wave.mode}, table={wave.weightedTable?.Count}, totalCount={wave.totalCount}, duration={wave.duration}");

            switch (wave.mode)
            {
                case WaveMode.Timed:
                    yield return RunTimedWave(wave);
                    break;
                default:
                    yield return RunCountKillWave(wave);
                    break;
            }

            IsWaveActive = false;
            EnemiesAlive = waveAlive.Count;
            Debug.Log($"[WaveManager] Wave {i} COMPLETED (mode={wave.mode}, remaining={EnemiesAlive})");

            // 选卡前缓冲：先留出时间看清战果（timeScale 正常流逝），再弹选卡。
            // 注意：必须放在弹卡之前——CoreChoiceUI.Show 会置 timeScale=0，
            // WaitForSeconds 在 timeScale=0 时被冻结，放后面永不生效。
            if (choiceBuffer > 0f)
                yield return new WaitForSeconds(choiceBuffer);

            // 弹选卡：房间模式由 RoomFlowController（订阅 OnWaveCompleted）触发；
            // 无房间模式 autoShowChoiceUI 兜底自己弹（CoreChoiceUI 有防重入）。
            OnWaveCompleted?.Invoke(i);
            if (autoShowChoiceUI && CoreChoiceUI.Instance != null)
                CoreChoiceUI.Instance.Show(onClosed: null, doublePick: false);

            // 等待选卡会话结束再进下一波（弹卡后 timeScale=0，怪物不会在暂停期间刷出）。
            // 用 IsDrafting 轮询而非 WaitForSeconds：不依赖 timeScale，
            // 暂停菜单等其它 timeScale=0 场景不受影响；30s 超时兜底防死锁。
            if (CoreChoiceUI.Instance != null)
            {
                float cardWaitStart = Time.realtimeSinceStartup;
                while (CoreChoiceUI.Instance.IsDrafting
                       && Time.realtimeSinceStartup - cardWaitStart < 30f)
                    yield return null;
            }
        }

        AllWavesComplete = true;
        if (currentRoom != null && currentRoom.context != null)
            currentRoom.context.State = RoomState.Cleared;
        Debug.Log($"[WaveManager] ALL WAVES COMPLETE for '{(currentTemplate != null ? currentTemplate.roomName : "(无房间)")}'");
        OnAllWavesComplete?.Invoke();
    }

    // ── 数量波：刷满 totalCount 不再补，清场过波 ──

    IEnumerator RunCountKillWave(WaveConfig wave)
    {
        if (wave.weightedTable == null || wave.weightedTable.Count == 0)
        {
            Debug.LogWarning("[WaveManager] 数量波权重表为空，本波跳过（请在 WaveConfig.weightedTable 配置怪物）。");
            yield break;
        }
        int spawnedTotal = 0;
        float nextSpawnTime = 0f;
        while (isRunning)
        {
            PruneWaveAlive();

            if (spawnedTotal < wave.totalCount)
            {
                // 尚未刷满：按节奏补刷（死亡后配额释放，继续补到累计 totalCount）
                if (Time.time >= nextSpawnTime)
                {
                    int added = SpawnBatch(wave, wave.totalCount - spawnedTotal);
                    if (added > 0)
                    {
                        spawnedTotal += added;
                        EnemiesAlive = waveAlive.Count;
                        Debug.Log($"[WaveManager] CountKill 补刷：本波累计 {spawnedTotal}/{wave.totalCount}，在场 {EnemiesAlive}。");
                    }
                    nextSpawnTime = Time.time + spawnInterval;
                }
                yield return null;
                continue;
            }

            // 已刷满：等待清场（本波怪全灭 / 附身 / 退场）
            if (waveAlive.Count == 0) break;
            yield return null;
        }
        Debug.Log($"[WaveManager] CountKill 波结束：累计刷 {spawnedTotal} 只。");
    }

    // ── 时间波：持续补刷 duration 秒，到时结算 ──

    IEnumerator RunTimedWave(WaveConfig wave)
    {
        float endTime = Time.time + wave.duration;
        TimeWaveRemaining = wave.duration;
        float nextSpawnTime = 0f;
        while (isRunning && Time.time < endTime)
        {
            PruneWaveAlive();
            TimeWaveRemaining = Mathf.Max(0f, endTime - Time.time); // 供 UI 倒计时显示
            if (Time.time >= nextSpawnTime)
            {
                int added = SpawnBatch(wave, int.MaxValue);
                if (added > 0)
                {
                    EnemiesAlive = waveAlive.Count;
                    Debug.Log($"[WaveManager] Timed 补刷 +{added}：在场 {EnemiesAlive}，剩余 {TimeWaveRemaining:F1}s。");
                }
                nextSpawnTime = Time.time + spawnInterval;
            }
            yield return null;
        }
        TimeWaveRemaining = 0f;

        // 结算：回收本波剩余在场怪（清场进入选卡）
        PruneWaveAlive();
        if (recycleRemainingOnTimeUp && waveAlive.Count > 0)
        {
            var spawner = MonsterSpawner.Instance;
            for (int i = waveAlive.Count - 1; i >= 0; i--)
            {
                if (spawner != null) spawner.RecycleWaveMonster(waveAlive[i]);
            }
            Debug.Log($"[WaveManager] Timed 波到时结算：回收剩余在场怪 {waveAlive.Count} 只。");
            waveAlive.Clear();
        }
        EnemiesAlive = waveAlive.Count;
    }

    // ── 刷怪 ──

    /// <summary>
    /// 按权重表抽 1 个 MonsterWaveDef，刷出其一整组怪物（受 quota 与全场配额裁剪）。
    /// 刷怪位置由 MonsterSpawner.TryGetWaveSpawnPosition 提供（B 带、视野外、可走）。
    /// </summary>
    int SpawnBatch(WaveConfig wave, int quota)
    {
        var spawner = MonsterSpawner.Instance;
        if (spawner == null) return 0;
        if (wave.weightedTable == null || wave.weightedTable.Count == 0) return 0;
        var def = PickWeighted(wave.weightedTable);
        if (def == null || def.monsters == null || def.monsters.Count == 0) return 0;

        int spawned = 0;
        for (int e = 0; e < def.monsters.Count && spawned < quota; e++)
        {
            var entry = def.monsters[e];
            if (entry == null || entry.prefab == null) continue;
            for (int i = 0; i < entry.count && spawned < quota; i++)
            {
                if (!spawner.TryGetWaveSpawnPosition(out var pos)) continue; // 无合法刷怪点：本只跳过
                var m = spawner.SpawnWaveMonster(entry.prefab, pos);
                if (m != null)
                {
                    waveAlive.Add(m);
                    spawned++;
                }
            }
        }
        if (spawned > 0)
            Debug.Log($"[WaveManager] 抽中波次 '{def.id}' 刷出 ×{spawned}。");
        return spawned;
    }

    /// <summary>按 spawnWeight 权重随机抽取；权重和 ≤ 0 返回 null。</summary>
    static MonsterWaveDef PickWeighted(List<MonsterWaveDef> table)
    {
        float total = 0f;
        for (int i = 0; i < table.Count; i++)
            if (table[i] != null) total += Mathf.Max(0f, table[i].spawnWeight);
        if (total <= 0f) return null;

        float roll = UnityEngine.Random.Range(0f, total);
        for (int i = 0; i < table.Count; i++)
        {
            if (table[i] == null) continue;
            roll -= Mathf.Max(0f, table[i].spawnWeight);
            if (roll <= 0f) return table[i];
        }
        return table[table.Count - 1];
    }

    // ── 清点 ──

    /// <summary>
    /// 从本波清点中移除已退场的怪：死亡（isDowned）/ 被玩家附身（isPossessed，身体归玩家）。
    /// 波次怪已被 MonsterSpawner 标记为不随 Chunk 回收，此处保留 activeInHierarchy 兜底
    /// （手动回收/异常销毁时仍能正确清点）。
    /// </summary>
    void PruneWaveAlive()
    {
        for (int i = waveAlive.Count - 1; i >= 0; i--)
        {
            var m = waveAlive[i];
            if (m == null || !m.gameObject.activeInHierarchy || m.isDowned || m.isPossessed)
            {
                if (m != null && m.isDowned) OnWaveEnemyKilled?.Invoke(m);
                waveAlive.RemoveAt(i);
            }
        }
        EnemiesAlive = waveAlive.Count;
    }
}
