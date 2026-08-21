using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 管理房间内所有波次的生命周期。
///
/// 波次玩法（2026-08-17 重构）：接入当前地图怪物生成框架（MonsterSpawner / MonsterPool /
/// MonsterWaveDef 权重表），替代旧"直接 MonsterPool.Spawn + Enemy 轮询"逻辑。
/// 波次模式为整体配置（WaveManager.waveMode / RoomTemplate.waveMode，全局统一）：
///   CountKill 数量波：按权重表持续补刷，累计刷满 totalCount 只后不再补；
///                     玩家清完场上本波怪 → 触发选卡 → 下一波。
///   Timed 时间波：持续补刷 duration 秒，时间到即结算（可选回收剩余在场怪）→ 选卡 → 下一波。
///
/// 与地图框架的衔接：
///   - 波次怪经 MonsterSpawner.SpawnWaveMonster 刷出（AI 直接激活且永不休眠），计入全场配额/追踪；
///   - 波次怪不随 Chunk 休眠/回收/写快照（MonsterSpawner 波次怪标记），死亡/被附身即从本波清点中退场；
///   - 场上所有怪物由本系统驱动（地图静态怪模式已于 2026-08-18 移除）；
///   - 时间波结算回收剩余怪（不写快照，永久退场）。
///
/// 选卡衔接：波次完成 → choiceBuffer 缓冲（看清战果）→ 触发 OnWaveCompleted
/// （RoomFlowController 或 autoShowChoiceUI 打开 CoreChoiceUI，timeScale=0）→
/// IsDrafting 轮询等待选卡会话结束 → 下一波自动开始（选卡暂停期间不会刷怪）。
/// </summary>
public class WaveManager : SceneSingleton<WaveManager>
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

    /// <summary>事件：波次开始。</summary>
    public event Action<int, WaveConfig> OnWaveStarted;
    /// <summary>事件：波次完成（数量波清场 / 时间波到时）。</summary>
    public event Action<int> OnWaveCompleted;
    /// <summary>事件：所有波次完成。</summary>
    public event Action OnAllWavesComplete;
    /// <summary>事件：本波一只怪被消灭（isDowned）。</summary>
    public event Action<MonsterActor> OnWaveEnemyKilled;

    [Header("波次节奏")]
    [Tooltip("怪物群系：同一编队（WaveDef 一组怪）刷出时围绕同一中心点的散射半径（米），组内怪聚集出现。")]
    [Min(0f)] public float groupScatterRadius = 3f;
    [Tooltip("波次补刷间隔（秒）：每隔多久按权重表抽一批怪刷出。")]
    [Min(0.05f)] public float spawnInterval = 0.5f;
    [Tooltip("在场怪修剪间隔（秒）：死亡/附身/倒地怪的摘除频率。越低清场判定越及时，越高每帧开销越小。")]
    [Min(0.02f)] public float pruneInterval = 0.1f;
    float nextPruneTime;
    [Tooltip("波次完成 → 下一波/选卡前的缓冲（秒），用于看清战果。选卡期间 timeScale=0 会冻结此等待。")]
    [Min(0f)] public float choiceBuffer = 2f;
    [Tooltip("波次完成时自动打开选卡弹窗（无 RoomFlowController 场景的兜底；有 RoomFlow 时其也会触发，CoreChoiceUI 有防重入）。")]
    public bool autoShowChoiceUI = true;
    [Tooltip("时间波结算时回收本波剩余在场怪（不写快照，永久退场）。")]
    public bool recycleRemainingOnTimeUp = true;

    [Header("独立波次配置（无房间模式）")]
    [Tooltip("无房间模式波次模式（整体）：CountKill=全部波为数量波；Timed=全部波为时间波。有 RoomTemplate 时用模板的 waveMode。")]
    public WaveMode waveMode = WaveMode.CountKill;
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
    /// <summary>读档恢复：从该波次索引的下一波开始（-1 = 新局第一波）。</summary>
    private int resumeFromWaveIndex = -1;
    /// <summary>读档恢复：选卡未完成标记（为 true 时先补弹该波选卡再进下一波）。</summary>
    private bool resumePendingChoice;
    /// <summary>调试跳波标志（DebugSkipWave 置位，波循环下一帧检测后视为清场过波）。</summary>
    private bool debugSkipWave;

    protected override void Awake()
    {
        base.Awake();   // 防重复注册（已有实例则销毁本对象）
        if (Instance != this) return;
        TimeWaveRemaining = 0f;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();   // 清 Instance
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 编辑期校验：提示场景需挂刷怪基础设施（运行时自动补齐，此处仅告知配置者）
        if (!Application.isPlaying && FindObjectOfType<MonsterSpawner>() == null)
            Debug.LogWarning("[WaveManager] 场景中无 MonsterSpawner——运行时将由 WaveManager 自动创建，但建议显式挂载以便调整配额等配置。");
    }
#endif

    void Start()
    {
        // 自举装配刷怪基础设施：只挂本组件（未挂 MonsterSpawner）也能刷出怪。
        // 放在 Start 而非 Awake：此时场景所有 Awake 已执行完（已有实例已注册），
        // 确保不会因创建时机抢跑而覆盖场景中已配置的 MonsterSpawner。幂等。
        MonsterSpawner.EnsureInstance();

        // 精英投放总控拉起（幂等）：订阅本 WaveManager 波次事件与 RunSession 阶段事件
        EliteBuildDirector.EnsureInstance().AttachToWaveManager(this);

        // 无房间模式：未被 RoomFlowController 初始化时，等待地图就绪后自动启动
        if (!autoStart) return;
        StartCoroutine(AutoStartRoutine());
    }

    IEnumerator AutoStartRoutine()
    {
        // 兜底创建对局会话：主菜单流程已由 MainMenuController EnsureInstance；
        // 直接 Play 场景（不经主菜单）时若无会话，存档点会被静默跳过（RunSession.Instance==null
        // → SaveProgress 不执行 → 选卡界面退出后无存档可恢复）。此调用幂等，不影响已有会话。
        var session = RunSession.EnsureInstance();

        // 直接 Play（不经主菜单）：会话仅被 EnsureInstance 创建，未走 BeginNewRun——
        // 此时 WorldSeed 仍为 0，且 useFixedSeed 不生效，导致与"主菜单开始新游戏"的
        // 卡牌/刷怪序列不一致。补上与 BeginNewRun 同款的种子初始化（不清进度不清档）。
        if (!session.HasActiveRun)
            session.InitWorldSeed();

        // RunFlow 阶段推进：新局 Opening → Tutorial（Waves 的推进由 WaveRoutine 内的教学波门决定，
        // 教学系统未配置/关闭时波门恒开，行为等价于原直通）。
        // 读档恢复时阶段已为 Waves/Choice（LoadFromSave 设置），不受影响。
        if (session.CurrentPhase == RunPhase.Opening)
        {
            session.TransitionTo(RunPhase.Tutorial);
        }

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

        // 读档恢复：从会话（RunSession，主菜单[继续]时已填充）的已完成波次之后继续（跳过 grace）
        var run = RunSession.Instance;
        if (run != null && run.HasActiveRun && run.CompletedWaveIndex >= 0)
        {
            // 选卡未完成（选卡界面退出）：即使已完成最后一波也要补弹选卡，不能直接判结束
            if (run.CompletedWaveIndex >= ActiveWaves.Count - 1 && !run.PendingChoice)
            {
                Debug.LogWarning($"[WaveManager] 存档波次 {run.CompletedWaveIndex} 已超出配置范围（共 {ActiveWaves.Count} 波），忽略读档，新开一局。");
                run.EndRun();
            }
            else
            {
                resumeFromWaveIndex = run.CompletedWaveIndex;
                resumePendingChoice = run.PendingChoice;
                // 恢复玩家运行时状态（灵魂位置/HP/时间）——由会话提供，不依赖存档文件
                var soul = FindObjectOfType<SoulActor>();
                if (soul != null) soul.transform.position = run.SoulPosition;
                if (PlayerHealth.Instance != null)
                {
                    PlayerHealth.Instance.currentHealth = run.SoulHealth;
                    PlayerHealth.Instance.UpdateHealthUI();
                }
                if (GameManager.Instance != null)
                    GameManager.Instance.soulTime = run.SoulTime;
                RestoreBodies(run);
                Debug.Log($"[WaveManager] 读档恢复：已完成 {resumeFromWaveIndex + 1} 波" + (resumePendingChoice ? "（选卡未完成，将补弹选卡）" : "") + "，继续对局。");
            }
        }

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

    /// <summary>当前生效波次总数（精英投放节奏判断用，1-based 波次的边界）。</summary>
    public int TotalWaveCount => ActiveWaves != null ? ActiveWaves.Count : 0;

    /// <summary>
    /// 外部来源怪（精英投放）计入本波清点：计入后精英未死亡/未被附身，本波不算清场。
    /// 仅战斗波次进行中（IsWaveActive）有效。
    /// </summary>
    public void RegisterExternalWaveMonster(MonsterActor monster)
    {
        if (monster == null || !IsWaveActive) return;
        waveAlive.Add(monster);
        EnemiesAlive = waveAlive.Count;
    }

    /// <summary>当前生效的首波前准备时间（房间模板优先，无房间用自身配置）。</summary>
    float ActiveGracePeriod => currentTemplate != null ? currentTemplate.gracePeriod : gracePeriod;

    /// <summary>查询指定波清场后选卡是否双选（越界返回 false=单选）。</summary>
    public bool GetWaveDoublePick(int waveIndex)
    {
        if (waveIndex < 0 || ActiveWaves == null || waveIndex >= ActiveWaves.Count)
            return false;
        return ActiveWaves[waveIndex].doublePick;
    }

    /// <summary>当前生效的整体波次模式（房间模板优先，无房间用自身配置）。</summary>
    WaveMode ActiveWaveMode => currentTemplate != null ? currentTemplate.waveMode : waveMode;

    /// <summary>
    /// 波次刷怪随机流（种子确定性）：本波开始前由 WaveRandomFor(waveIndex) 设置，
    /// 编队抽取/群系散射/取点全部走此流——同一种子（读档恢复同 worldSeed）下怪物种类与位置可复现。
    /// </summary>
    System.Random WaveRandom { get; set; }

    /// <summary>按波次号派生刷怪子种子（SeedSystem 统一入口，质数混合防跨域/跨波关联）。</summary>
    System.Random WaveRandomFor(int waveIndex)
    {
        return SeedSystem.CreateFlow(SeedSystem.DomainWave, waveIndex);
    }

    /// <summary>进入本波前设置种子随机流（注入 MonsterSpawner，取点/散射共用）。</summary>
    void PrepareWaveRandom(int waveIndex)
    {
        WaveRandom = WaveRandomFor(waveIndex);
        if (MonsterSpawner.Instance != null)
            MonsterSpawner.Instance.WaveRandom = WaveRandom;
    }

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

    /// <summary>
    /// 调试：立即清掉当前波所有在场怪（视为全部击杀）并结束本波 → 进入选卡 → 下一波。
    /// 正式流程（GameManager.IsFormalFlow）下屏蔽。
    /// </summary>
    public void DebugSkipWave()
    {
        if (GameManager.IsFormalFlow) return;
        if (!isRunning || !IsWaveActive)
        {
            Debug.Log("[WaveManager] DebugSkipWave: 当前无进行中的波，忽略。");
            return;
        }
        // 回收本波所有在场怪（视为全部击杀）
        for (int i = waveAlive.Count - 1; i >= 0; i--)
        {
            var m = waveAlive[i];
            if (m != null)
            {
                OnWaveEnemyKilled?.Invoke(m);
                if (MonsterSpawner.Instance != null) MonsterSpawner.Instance.RecycleWaveMonster(m);
            }
        }
        waveAlive.Clear();
        EnemiesAlive = 0;
        debugSkipWave = true; // 波循环下一帧检测后 break（视为清场）
        Debug.Log($"[WaveManager] DebugSkipWave: 清场并跳波（当前 Wave {CurrentWaveIndex}）。");
    }

    IEnumerator WaveRoutine()
    {
        Debug.Log($"[WaveManager] WaveRoutine START: room='{(currentTemplate != null ? currentTemplate.roomName : "(无房间)")}', waves={ActiveWaves.Count}, grace={ActiveGracePeriod}");

        // 教学波门：首波开始前等待教学系统开门（教学未配置/关闭时恒开，无感知）。
        // 兜底超时强制放行，防教学异常卡死开局（读档恢复也走此门，但门默认开 → 无感）。
        float gateWait = 0f;
        while (!TutorialController.WaveStartGateOpen && gateWait < 30f)
        {
            gateWait += Time.unscaledDeltaTime;
            yield return null;
        }
        if (gateWait >= 30f)
            Debug.LogWarning("[WaveManager] 教学波门等待超时（30s），强制放行。");

        // Grace period（读档恢复时跳过：存档点本身就在波间，无需再次等待）
        if (resumeFromWaveIndex < 0 && ActiveGracePeriod > 0f)
            yield return new WaitForSeconds(ActiveGracePeriod);

        // 读档补弹选卡：上一波已完成但选卡未完成（选卡界面退出），先补弹再进下一波
        if (resumePendingChoice)
        {
            // 先恢复退出时的候选卡（保证候选与退出时一致，由存档决定而非重新随机）
            var run = RunSession.Instance;
            if (run != null && run.ChoicePicks.Count > 0 && CardManager.Instance != null)
                CardManager.Instance.RestoreChoicePicks(run.ChoicePicks);
            yield return RunChoiceStage(resumeFromWaveIndex, fireCompletionEvent: false);
            resumePendingChoice = false;
        }

        for (int i = resumeFromWaveIndex + 1; i < ActiveWaves.Count; i++)
        {
            if (!isRunning) yield break;

            var wave = ActiveWaves[i];
            CurrentWaveIndex = i;
            if (currentRoom != null && currentRoom.context != null)
                currentRoom.context.CurrentWaveIndex = i;

            IsWaveActive = true;
            waveAlive.Clear();
            OnWaveStarted?.Invoke(i, wave);
            Debug.Log($"[WaveManager] Wave {i} START: mode={ActiveWaveMode}, table={wave.weightedTable?.Count}, totalCount={wave.totalCount}, duration={wave.duration}");

            PrepareWaveRandom(i); // 种子确定性：每波固定刷怪随机流（种类/位置可复现）

            switch (ActiveWaveMode)
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
            Debug.Log($"[WaveManager] Wave {i} COMPLETED (mode={ActiveWaveMode}, remaining={EnemiesAlive})");

            yield return RunChoiceStage(i);
        }

        AllWavesComplete = true;
        if (currentRoom != null && currentRoom.context != null)
            currentRoom.context.State = RoomState.Cleared;

    /// <summary>
    /// 弹卡阶段（波清场后执行）：
    ///   缓冲 → 存档点①（选卡未完成）→ 弹卡 → 等待选卡会话 → 存档点②（选卡完成，覆盖①）。
    /// 读档补弹（resumePendingChoice）复用本流程：若补弹期间再次退出，
    /// 存档点①仍写"选卡未完成"，继续后依然补弹（自洽）。
    /// </summary>
    /// <param name="waveIndex">刚完成的波次索引（选卡属于该波）。</param>
    /// <param name="fireCompletionEvent">是否触发 OnWaveCompleted（补弹时 false，避免重复通知）。</param>
    IEnumerator RunChoiceStage(int waveIndex, bool fireCompletionEvent = true)
    {
        // 选卡前缓冲：先留出时间看清战果，再弹选卡。
        // 必须放在弹卡之前——CoreChoiceUI.Show 会置 timeScale=0。
        // 用 Realtime 版本：暂停菜单（timeScale=0）不会冻结波次流程（否则缓冲等待永不到点，波不结束）。
        if (choiceBuffer > 0f)
            yield return new WaitForSecondsRealtime(choiceBuffer);

        // 弹选卡：房间模式由 RoomFlowController（订阅 OnWaveCompleted）触发；
        // 无房间模式 autoShowChoiceUI 兜底自己弹（CoreChoiceUI 有防重入）。
        // 读档补弹（fireCompletionEvent=false）时 keepPicks=true：保留已恢复的候选（与退出时一致）。
        if (fireCompletionEvent) OnWaveCompleted?.Invoke(waveIndex);
        if (autoShowChoiceUI && CoreChoiceUI.Instance != null)
            CoreChoiceUI.Instance.Show(onClosed: null, doublePick: GetWaveDoublePick(waveIndex), keepPicks: !fireCompletionEvent, waveIndex: waveIndex);

        // 波次间安全存档点①：弹卡后、等待选卡前写入（选卡未完成标记 + 本次候选快照）。
        // 必须放在 Show 之后——SaveProgress 的 SampleChoicePicks 采样 CardManager.currentPicks，
        // 弹卡前采样到的是上一波遗留候选（Close 不清 currentPicks），恢复补弹时候选与退出时不一致。
        // 玩家在选卡界面退出（ESC→Return to Menu 不重新存档）时，本存档即唯一候选来源。
        if (RunSession.Instance != null)
        {
            RunSession.Instance.SaveProgress(waveIndex, pendingChoice: true);
            RunSession.Instance.TransitionTo(RunPhase.Choice); // RunFlow：波清场 → 选卡阶段
        }

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

        // 存档点②：选完卡之后存档——覆盖波清场存档（补充本次选卡 + 玩家最终位置）。
        // 恢复位置 = 本波起点（选卡结束瞬间玩家所在 = 下一波开始时玩家所在），
        // 保证"波次中间移动位置后退出重进，回到波次开始的地方"。
        if (RunSession.Instance != null)
        {
            RunSession.Instance.SaveProgress(waveIndex, pendingChoice: false);
            RunSession.Instance.TransitionTo(RunPhase.Waves); // RunFlow：选卡完成 → 回波次阶段
        }
    }
        Debug.Log($"[WaveManager] ALL WAVES COMPLETE for '{(currentTemplate != null ? currentTemplate.roomName : "(无房间)")}'");
        OnAllWavesComplete?.Invoke();

        // 整局流程走 Run 级状态链（RunFlow）：
        //   Waves → Final（三阶段压力战未实现，先用一波普通怪占位：清完这波才 → Result）。
        // 房间模式由 RoomFlowController 的 OnAllWavesCompleteHandler 处理（Cleared/出口/Core），不在此推进阶段。
        if (currentTemplate == null)
        {
            var session = RunSession.EnsureInstance();
            session.TransitionTo(RunPhase.Final);

            // Final 占位：复用最后一波的编队配置，打一波普通怪（清完 = 胜利）。
            // 三阶段压力战实现后替换为 RunFinalPressurePhases()。
            var lastWave = ActiveWaves.Count > 0 ? ActiveWaves[ActiveWaves.Count - 1] : null;
            if (lastWave != null && lastWave.weightedTable != null && lastWave.weightedTable.Count > 0)
            {
                yield return RunFinalPlaceholderWave(lastWave);
            }

            session.TransitionTo(RunPhase.Result);
        }
    }

    /// <summary>
    /// Final 占位波：复用传入波配置刷一波普通怪（数量 = 该波 totalCount，模式 = 整体模式），
    /// 清完即返回（视为三阶段压力战占位）。三阶段压力战实现后替换。
    /// </summary>
    IEnumerator RunFinalPlaceholderWave(WaveConfig wave)
    {
        Debug.Log("[WaveManager] FINAL 占位波开始（压力战未实现，先打一波普通怪）");
        IsWaveActive = true;
        int spawnedTotal = 0;
        float nextSpawnTime = 0f;
        while (isRunning)
        {
            PruneWaveAlive();
            if (spawnedTotal < wave.totalCount && Time.time >= nextSpawnTime)
            {
                int added = SpawnBatch(wave, wave.totalCount - spawnedTotal);
                if (added > 0)
                {
                    spawnedTotal += added;
                    EnemiesAlive = waveAlive.Count;
                }
                nextSpawnTime = Time.time + spawnInterval;
            }
            if (spawnedTotal >= wave.totalCount && waveAlive.Count == 0) break; // 清完 = 胜利
            yield return null;
        }
        IsWaveActive = false;
        EnemiesAlive = waveAlive.Count;
        Debug.Log("[WaveManager] FINAL 占位波清场完成 → 进入结算");
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
            if (debugSkipWave) { debugSkipWave = false; break; } // 调试跳波：视为清场，直接过波
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
        int spawnedTotal = 0; // 本波累计刷出数（含已击杀/回收），用于 maxSpawnCount 上限
        while (isRunning && Time.time < endTime)
        {
            if (debugSkipWave) { debugSkipWave = false; break; } // 调试跳波：视为清场，直接过波
            PruneWaveAlive();
            TimeWaveRemaining = Mathf.Max(0f, endTime - Time.time); // 供 UI 倒计时显示
            if (Time.time >= nextSpawnTime)
            {
                // 剩余刷怪配额：maxSpawnCount=0 → 不限制（仅受全场配额约束）
                int quota = wave.maxSpawnCount > 0 ? Mathf.Max(0, wave.maxSpawnCount - spawnedTotal) : int.MaxValue;
                if (quota > 0)
                {
                    int added = SpawnBatch(wave, quota);
                    if (added > 0)
                    {
                        spawnedTotal += added;
                        EnemiesAlive = waveAlive.Count;
                        Debug.Log($"[WaveManager] Timed 补刷 +{added}：本波累计 {spawnedTotal}（上限 {wave.maxSpawnCount}），在场 {EnemiesAlive}，剩余 {TimeWaveRemaining:F1}s。");
                    }
                }
                nextSpawnTime = Time.time + spawnInterval;
            }
            yield return null;
        }
        TimeWaveRemaining = 0f;

        // 结算：回收本波剩余在场怪（清场进入选卡）——force 立即修剪，避免残留已死怪
        PruneWaveAlive(force: true);
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

        // 怪物群系：抽中编队后先取一个组中心（玩家 B 带内合法点），组内所有怪围绕中心小半径散射——
        // 同一编队的怪聚集出现，不再每只独立环形采样散落全图。
        if (!spawner.TryGetWaveSpawnPosition(out var center))
        {
            Debug.LogWarning("[WaveManager] 无合法群系中心点，本组跳过。");
            return 0;
        }

        int spawned = 0;
        for (int e = 0; e < def.monsters.Count && spawned < quota; e++)
        {
            var entry = def.monsters[e];
            if (entry == null || entry.prefab == null) continue;
            for (int i = 0; i < entry.count && spawned < quota; i++)
            {
                Vector2 offset = ScatterOffset(); // 种子流群系散射（y 沿用 center 的统一高度）
                Vector3 pos = center + new Vector3(offset.x, 0f, offset.y);
                var m = spawner.SpawnWaveMonster(entry.prefab, pos);
                if (m != null)
                {
                    waveAlive.Add(m);
                    spawned++;
                }
            }
        }
        if (spawned > 0)
            Debug.Log($"[WaveManager] 抽中波次 '{def.id}' 刷出 ×{spawned}（群系中心 {center}）。");
        return spawned;
    }

    /// <summary>群系散射偏移（种子流，uniform disk）。</summary>
    Vector2 ScatterOffset()
    {
        var rng = WaveRandom;
        if (rng == null)
        {
            Vector2 v = UnityEngine.Random.insideUnitCircle * groupScatterRadius;
            return v;
        }
        double a = rng.NextDouble() * Mathf.PI * 2.0;
        double r = Math.Sqrt(rng.NextDouble()) * groupScatterRadius;
        return new Vector2((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r));
    }

    /// <summary>按条目 weight 权重随机抽取（种子流）；权重和 ≤ 0 返回 null。</summary>
    MonsterWaveDef PickWeighted(List<WaveDefEntry> table)
    {
        float total = 0f;
        for (int i = 0; i < table.Count; i++)
            if (table[i] != null && table[i].def != null) total += Mathf.Max(0f, table[i].weight);
        if (total <= 0f) return null;

        var rng = WaveRandom;
        float roll = rng != null ? (float)rng.NextDouble() * total : UnityEngine.Random.Range(0f, total);
        for (int i = 0; i < table.Count; i++)
        {
            if (table[i] == null || table[i].def == null) continue;
            roll -= Mathf.Max(0f, table[i].weight);
            if (roll <= 0f) return table[i].def;
        }
        return table[table.Count - 1].def;
    }

    // ── 读档恢复 ──

    /// <summary>
    /// 恢复附身怪与可附身尸体（读档）：按存档快照从波表解析 prefab → 直接刷出。
    /// 尸体不经过 spawner 追踪（不算战斗怪，淡出即回池）；附身怪刷出后应用已解锁能力并直接附身。
    /// </summary>
    void RestoreBodies(RunSession run)
    {
        if (run == null) return;

        // 1) 可附身尸体
        foreach (var snap in run.Corpses)
        {
            var prefab = ResolveWavePrefab(snap.prefabId);
            if (prefab == null) continue;
            var go = MonsterPool.Instance.Spawn(prefab, snap.position, Quaternion.identity);
            if (go == null) continue;
            var monster = go.GetComponentInChildren<MonsterActor>(true);
            if (monster != null)
            {
                monster.ApplyStreamSnapshot(0f, false, true); // downed：复用 Die() 重建尸体姿态/窗口
                Debug.Log($"[WaveManager] 恢复尸体 '{snap.prefabId}' @ {snap.position}");
            }
        }

        // 2) 玩家附身的怪（最后刷并附身，确保尸体已就位）
        if (run.PossessedBody != null)
        {
            var prefab = ResolveWavePrefab(run.PossessedBody.prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[WaveManager] 附身怪 prefab '{run.PossessedBody.prefabId}' 无法解析，恢复为灵魂态。");
                return;
            }
            var go = MonsterPool.Instance.Spawn(prefab, run.PossessedBody.position, Quaternion.identity);
            if (go == null) return;
            var monster = go.GetComponentInChildren<MonsterActor>(true);
            if (monster == null) return;

            monster.ApplyStreamSnapshot(run.PossessedBody.health, false, false); // 恢复血量（≥1，非倒地）
            if (CardManager.Instance != null) CardManager.Instance.ApplyAllUnlocksTo(go);
            if (PossessionManager.Instance != null && PossessionManager.Instance.DebugForcePossess(monster))
                Debug.Log($"[WaveManager] 附身恢复：'{run.PossessedBody.prefabId}' @ {run.PossessedBody.position} HP={run.PossessedBody.health}");
            else
                Debug.LogWarning("[WaveManager] 附身恢复失败，已刷出怪但保持灵魂态。");
        }
    }

    /// <summary>按 prefab 名在全部波的权重表内解析怪物 prefab（存档 prefabId 匹配）。</summary>
    GameObject ResolveWavePrefab(string prefabId)
    {
        if (string.IsNullOrEmpty(prefabId)) return null;
        foreach (var wave in ActiveWaves)
        {
            if (wave == null || wave.weightedTable == null) continue;
            foreach (var entry in wave.weightedTable)
            {
                var def = entry != null ? entry.def : null;
                if (def == null || def.monsters == null) continue;
                foreach (var me in def.monsters)
                    if (me != null && me.prefab != null && me.prefab.name == prefabId)
                        return me.prefab;
            }
        }
        return null;
    }

    // ── 清点 ──

    /// <summary>
    /// 从本波清点中移除已退场的怪：死亡（isDowned）/ 被玩家附身（isPossessed，身体归玩家）。
    /// 波次怪已被 MonsterSpawner 标记为不随 Chunk 回收，此处保留 activeInHierarchy 兜底
    /// （手动回收/异常销毁时仍能正确清点）。
    /// </summary>
    /// <summary>
    /// 修剪失效波次怪（死亡/附身/倒地）：按 pruneInterval 低频执行（默认 0.1s），
    /// 避免怪物多时每帧 O(n) 遍历；force=true 时立即执行（波结算前调用）。
    /// </summary>
    void PruneWaveAlive(bool force = false)
    {
        if (!force && Time.unscaledTime < nextPruneTime) return;
        nextPruneTime = Time.unscaledTime + pruneInterval;
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
