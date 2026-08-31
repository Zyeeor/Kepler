using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 教学控制器（场景级单例）：Step 状态机 + 事实判定 + 幂等结算 + 提醒/超时。
///
/// 生命周期贯穿整个 Run（不只 Tutorial 阶段——事实在任意阶段都可能发生，
/// 例如玩家提前击杀怪，KilledFirstMonster 需被追溯判定消费）。
///
/// 判定模型：
///   - 一次性事实（击杀/附身/脱离/接力/神龛）：事件驱动，本 Run 报告 + Profile 追溯；
///   - 状态性事实（场上尸体存在）：激活时查询 + 低频轮询。
///   - Step 完成 = completeFacts 全部满足 → 写 Profile → 激活 nextStep。
///   - 幂等：Step 激活时若 Profile 已标记完成 → 直接跳过；开始条件已满足 → 立即结算。
/// </summary>
public class TutorialController : SceneSingleton<TutorialController>
{
    /// <summary>是否有激活中的教学提示（叙事调度器高压门只读查询：关键教学提示不被旁白遮挡；含队列中待播提示）。</summary>
    public static bool HasActivePrompt => Instance != null && (Instance.activeSteps.Count > 0 || Instance.queueRunning);

    /// <summary>Step 额外展示面板绑定（场景引用）。Step ID 对应 TutorialConfig 里的 step.id。</summary>
    [System.Serializable]
    public class StepExtraPanelBinding
    {
        [Tooltip("Step ID（对应 TutorialConfig 里该 Step 的 id）")]
        public string stepId = "";
        [Tooltip("该 Step 激活时同步显示的面板（场景对象）")]
        public GameObject panel;
    }

    [Header("配置")]
    [Tooltip("教学 Step 配置资产（策划编辑；留空 = 教学系统不工作，战斗不受影响）")]
    public TutorialConfig config;

    [Header("开场载体（Opening Carrier）")]
    [Tooltip("是否在开局自动附身开场载体（pride_new）。默认关闭：新游戏灵魂独立（未附身），由玩家自行寻找尸体附身；开启则恢复旧的自动附身开场流程。")]
    public bool autoPossessOpeningCarrier;
    [Tooltip("新局 Tutorial 阶段刷出的初始 Pride 载体 prefab（pride_new）；仅 autoPossessOpeningCarrier 开启时使用")]
    public GameObject openingCarrierPrefab;
    [Tooltip("载体出生位置（灵魂正前方偏移，世界坐标）")]
    public Vector3 openingCarrierSpawnOffset = new Vector3(0f, 0f, 5f);

    [Header("UI")]
    [Tooltip("教学 Banner UI（场景引用；留空则运行时自举创建最小 Banner）")]
    public TutorialUI ui;
    [Tooltip("旧版教学 Banner 字体字段（兼容已有场景引用）；运行时实际统一使用 FontRegistry.default")]
    public TMPro.TMP_FontAsset bannerFont;

    [Header("额外面板绑定")]
    [Tooltip("Step ID → 额外面板 映射（场景引用）。对应 Step 激活时显示该面板，完成/隐藏时关闭。")]
    public List<StepExtraPanelBinding> extraPanelBindings = new List<StepExtraPanelBinding>();

    [Header("跳过按钮")]
    [Tooltip("跳过教程按钮（Editor 配置；留空则无跳过入口）。点击后结束当前局所有剩余教程。")]
    public UnityEngine.UI.Button skipTutorialButton;
    [Tooltip("重新开始引导按钮（Editor 配置；跳过引导后出现）。点击后从头重放完整引导。")]
    public UnityEngine.UI.Button newTutorialButton;

    TutorialProbes probes;
    readonly HashSet<TutorialFact> runSeenFacts = new HashSet<TutorialFact>();
    readonly Dictionary<string, TutorialStepConfig> activeSteps = new Dictionary<string, TutorialStepConfig>();
    readonly HashSet<string> runCompletedStepIds = new HashSet<string>();      // 本局已完成 Step（强制模式绕过 Profile 后防轮询重激活风暴）
    readonly HashSet<string> runPossessedMonsterTypes = new HashSet<string>(); // 本局已首次附身怪物类型（防微教学重复弹）

    TutorialStepConfig currentBlockingStep;   // BlockTutorialChain 时唯一激活的 Step
    Coroutine pollRoutine;
    bool started;
    bool openingCarrierStarted;   // 开场载体流程防重入（阶段事件 + Start 补查两条触发路径）

    // ---------------- 提示队列（queueDelivery 步骤） ----------------
    // 队列模型：提示按触发顺序排队逐个显示；每条可配 displaySeconds（>0 定时自动消失）
    // 或 completeFacts（显示到完成条件满足）/ timeoutSeconds 兜底。
    // 上一条未结束下一条排队；跨 Run 幂等（persistAcrossRuns / Profile 已完成则跳过）。
    readonly List<TutorialStepConfig> promptQueue = new List<TutorialStepConfig>();
    readonly HashSet<string> queuedPromptIds = new HashSet<string>();   // 队列中或正在显示的 Step ID（防重入）
    bool queueRunning;
    Coroutine queueRoutine;

    bool forceCompleteActiveQueue;       // 玩家主动按 F 脱离 → 结束当前展示中的队列步骤
    TutorialStepConfig activeQueueStep; // 当前正在展示的队列步骤（供"脱离即结束"定位）
    bool _forceReplayArmed;              // 强制模式(forceTutorial)上升沿检测，避免每帧重复重放

    /// <summary>
    /// 教学波门（WaveManager.WaveRoutine 首波前等待）：
    /// - 教学未配置/关闭 → 恒开（战斗无感知，等价原直通）
    /// - BriefWaveBlock Step 激活时关闸，完成/超时开闸
    /// </summary>
    public static bool WaveStartGateOpen { get; private set; } = true;

    /// <summary>
    /// 开场载体飞行附身进行中标记（TutorialProbes.OnPossessionStarted 据此区分开场附身）。
    /// 用时间戳防残留：飞行被取消（死亡/场景切换）时标记不会永久残留——
    /// 超过 pendingValidSeconds 后 Probe 视为失效（下次附身不会误判为开场附身）。
    /// </summary>
    public static float OpeningCarrierPendingUntil { get; set; }
    public const float PendingValidSeconds = 15f;

    /// <summary>
    /// 本局教程准入判定（唯一触发口径）：
    /// - GameManager.forceTutorial 开关打开 → 显示所有教程（全量重放，忽略 Profile 幂等）；
    /// - GameManager.forceTutorial 开关关闭 → 一律不显示任何教程（含开局引导与附身怪微教学）。
    /// 不再依赖教学总开关、是否新手第一局或主菜单"新游戏"入口。
    /// </summary>
    static bool TutorialAllowedThisRun
    {
        get
        {
            if (RunSession.Instance != null && RunSession.Instance.IsBossMode)
                return false;
            // force 开关为唯一口径：打开即全量显示，关闭即全部隐藏。
            return GameManager.ForceTutorial;
        }
    }

    /// <summary>
    /// Step 是否已完成（本局内存记忆 + Profile 持久化）：
    /// forceTutorial 强制模式忽略 Profile（全量重放），但本局内存记忆仍生效——
    /// 防止 EvaluateAll 轮询对已完成 Step 无限重激活（激活即完成 → 每 0.5s 循环风暴）。
    /// </summary>
    bool StepCompletedThisRun(string stepId)
        => runCompletedStepIds.Contains(stepId)
           || (!GameManager.ForceTutorial && TutorialProfileStore.IsStepCompleted(stepId));

    /// <summary>一次性事实是否已在 Profile 发生（forceTutorial 强制模式忽略：跨 Run 追溯失效，本 Run 内存事实由 runSeenFacts 保证）。</summary>
    static bool FactSeenInProfile(TutorialFact fact)
        => !GameManager.ForceTutorial && TutorialProfileStore.HasSeenFact(fact);

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        TutorialFactBus.ClearSubscribers();
        TutorialFactBus.OnFactReported += OnFactReported;

        // 动态文本统一由 FontRegistry 管理；不再从教学字段向全局静态字体入口注入覆盖。
    }

    void Start()
    {
        if (Instance != this) return;
        // 兜底：无配置则教学系统整体静默（战斗零影响）
        if (config == null)
        {
            Debug.Log("[TutorialController] 未配置 TutorialConfig，教学系统关闭。");
            return;
        }

        // UI 自举放 try/catch：字体资产异常（如 atlas 纹理导入期间被销毁）不得中断教学系统启动
        try
        {
            if (ui == null) ui = EnsureMinimalUI();
            if (ui != null) ui.Hide();
        }
        catch (System.Exception e)
        {
            ui = null;
            Debug.LogWarning($"[TutorialController] Banner UI 自举失败（教学逻辑继续，仅无提示显示）：{e.Message}");
        }

        probes = GetComponent<TutorialProbes>();
        if (probes == null) probes = gameObject.AddComponent<TutorialProbes>();
        probes.StartProbing();

        var session = RunSession.Instance;
        if (session != null) session.OnPhaseChanged += OnPhaseChanged;

        // 输入事实转译：玩家按键 → 三槽认知事实（TUT-01/02 完成条件）
        PlayerController.OnCommandProduced += OnCommandProduced;
        // TUT-MONSTER 微教学：首次附身某怪物类型（displayName 为 key）时提示该怪核心机制。
        // 触发源走 TutorialProbes.OnPossessionObserved 转译（TutorialProbes 对 PossessionManager 的
        // 绑定带每帧重试兜底，本组件与 probes 同物体，订阅必然成功——无"晚创建漏订"时序风险）。
        probes.OnPossessionObserved += OnPossessionStartedForMonsterTut;

        started = true;
        pollRoutine = StartCoroutine(PollActiveSteps());

        // 跳过/重看引导按钮：Editor 配置后自动接管点击
        if (skipTutorialButton != null)
        {
            skipTutorialButton.onClick.RemoveAllListeners();
            skipTutorialButton.onClick.AddListener(SkipTutorial);
        }
        if (newTutorialButton != null)
        {
            newTutorialButton.onClick.RemoveAllListeners();
            newTutorialButton.onClick.AddListener(RestartTutorial);
        }
        SwapButtons(showSkip: true);   // 初始：显示跳过按钮，隐藏重看按钮

        // 静态态复位（防跨局残留）：开场载体 pending 标记清空
        OpeningCarrierPendingUntil = 0f;

        // 教学启动（波门 / 首条提示 / 开场载体补查）抽为 BeginTutorial，供 Start 与强制模式重放共用
        BeginTutorial();
        _forceReplayArmed = GameManager.ForceTutorial;
    }

    /// <summary>教学局启动/重放：设置波门、评估并展示首条提示、补查开场载体。Start 与 ForceReplay 共用。</summary>
    void BeginTutorial()
    {
        // 教学波门（WaveManager.WaveRoutine 首波前等待）：教学局先关闸，
        // 防止波次怪在玩家完成首次附身（获得躯体）前刷出秒杀灵魂（灵魂无防护，被秒 → GameOver → 引导中断）。
        // 首次附身事实（PossessedFirstBody / OpeningCarrierPossessed）到达时开闸放行；非教学局恒开等价直通。
        // WaveRoutine 侧已有 60s 兜底超时强制放行（防玩家一直不附身卡死开局）。
        WaveStartGateOpen = !TutorialAllowedThisRun;
        if (!WaveStartGateOpen)
            Debug.Log("[TutorialController] 教学波门关闭：等待玩家完成首次附身后放行波次。");

        // 开局主动评估：startFacts 为空的 Step（如 TUT-01 移动提示）应在开局立即激活，
        // 不等待首个事实报告（此前靠 OpeningCarrierPossessed 事实触发，附身动画期间无任何提示）。
        if (TutorialAllowedThisRun)
        {
            EvaluateAll();
            // 队列模式：开局首条提示（TUT-01 移动）显式入队（startFacts 为空不再被 EvaluateAll 自动入队）
            var firstStep = config.FindStep("TUT-01");
            if (firstStep != null && firstStep.queueDelivery)
                EnqueuePrompt(firstStep);
        }

        Debug.Log($"[TutorialController] 教学系统启动（{config.steps.Count} 个 Step，开关={TutorialProfileStore.TutorialEnabled}，本局允许={TutorialAllowedThisRun}，强制开关={GameManager.ForceTutorial}）。");

        // 补查当前阶段：WaveManager.AutoStartRoutine 的 TransitionTo(Tutorial) 可能早于本组件 Start
        // （场景对象初始化序：协程 resume 先于部分 Start），导致 OnPhaseChanged 订阅错过 Tutorial 事件。
        // 此处主动检查当前阶段，若已处于 Tutorial 则补启动开场载体流程（防重入由 openingCarrierStarted 保证）。
        // Pass v1：开场固定 Pride 载体为正式流程的一部分，不再依赖 autoPossessOpeningCarrier
        // （autoPossessOpeningCarrier 只控制是否自动附身，关闭时刷出尸体由玩家亲手 Possess）。
        var session = RunSession.Instance;
        if (session != null && session.CurrentPhase == RunPhase.Tutorial && !openingCarrierStarted)
            StartCoroutine(OpeningCarrierRoutine());
    }

    /// <summary>强制模式(forceTutorial)上升沿触发：本局视为全新玩家，清空一切完成/激活态后从头重放完整引导。</summary>
    void ForceReplay()
    {
        if (config == null) return;
        // 清空本局所有完成/激活态（内存态，不写 Profile），保证"强制=所有教程都未执行过"
        runCompletedStepIds.Clear();
        runPossessedMonsterTypes.Clear();
        runSeenFacts.Clear();
        promptQueue.Clear();
        queuedPromptIds.Clear();
        activeSteps.Clear();
        currentBlockingStep = null;
        activeQueueStep = null;
        forceCompleteActiveQueue = false;
        if (ui != null) ui.Hide();
        HideAllExtraPanels();
        StopAllCoroutines();
        // StopAllCoroutines 会杀掉正在运行的 PromptQueueRoutine 但不复位其 queueRunning 标记，
        // 必须手动复位，否则下方 EnqueuePrompt 因误判"队列仍在运行"而停摆、首条提示不显示。
        queueRunning = false;
        queueRoutine = null;
        started = true;
        enabled = true;
        pollRoutine = StartCoroutine(PollActiveSteps());
        BeginTutorial();   // 重置波门与首条提示；开场载体若处 Tutorial 阶段且未触发则按防重入补查重放
    }

    void OnPhaseChanged(RunPhase phase)
    {
        // 新手引导完成标记：仅第一局胜利（Result 结算）才视为"打完新手引导"并持久化，
        // 此后不再触发引导；失败（Failed）不算完成——玩家下次新游戏仍触发完整引导。
        // 中途退出/重开/直接关游戏不进入终态、不置位 → 同样下次仍触发。
        if (phase == RunPhase.Result)
            TutorialProfileStore.MarkTutorialCompleted();

        // 阶段变化不做强制行为；Step 激活与否由事实判定驱动（Tutorial 阶段的 Step 用 startFacts 空配置）
        Debug.Log($"[TutorialController] 阶段 → {phase}");

        // 新局进入 Tutorial 阶段：刷出固定 Pride 开场载体（永久尸体）。
        // autoPossessOpeningCarrier 仅控制是否自动附身；关闭时玩家亲手 Possess（Pass v1 正式流程）。
        if (phase == RunPhase.Tutorial && !openingCarrierStarted)
            StartCoroutine(OpeningCarrierRoutine());
    }

    /// <summary>
    /// 开场载体流程（可选，autoPossessOpeningCarrier 开启时启用）：主角开局未附身 → 刷出 pride_new 载体 →
    /// PossessionManager.BeginPossessionFlight 触发正常附身飞行位移动画 → 自动提交附身。
    /// 默认关闭：新游戏灵魂独立（未附身），由玩家自行寻找尸体附身。
    /// 失败重试 1 次，仍失败回退 DebugForcePossess（保底不卡开局）。
    /// 防重入：阶段事件与 Start 补查两条路径都可能触发，openingCarrierStarted 保证只执行一次。
    /// </summary>
    IEnumerator OpeningCarrierRoutine()
    {
        if (openingCarrierStarted) yield break;
        openingCarrierStarted = true;

        // 等开场降落演出（OpeningLandingSequence）完成后再刷载体：
        // 载体在降落途中刷出会触发飞行附身、打断降落演出。未配置/关闭时 LandingComplete 恒 true（无感）。
        float landingWait = 0f;
        while (!OpeningLandingSequence.LandingComplete && landingWait < 10f)
        {
            landingWait += Time.unscaledDeltaTime;
            yield return null;
        }

        if (openingCarrierPrefab == null)
        {
            Debug.LogWarning("[TutorialController] 未配置 openingCarrierPrefab，跳开场载体流程（玩家将保持未附身状态）。");
            yield break;
        }
        // 读档恢复跳过：读档路径阶段直接为 Waves/Choice，不会进入 Tutorial
        if (PossessionManager.Instance != null && PossessionManager.Instance.State == PossessionManager.SwitchState.Possessing)
            yield break; // 已附身（异常容错）

        // 等待灵魂就位（场景初始化序）
        var soul = FindObjectOfType<SoulActor>();
        float wait = 0f;
        while (soul == null && wait < 5f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
            soul = FindObjectOfType<SoulActor>();
        }
        if (soul == null) { Debug.LogWarning("[TutorialController] 开场载体流程：找不到灵魂，跳过。"); yield break; }

        yield return null; // 等一帧，确保所有 Awake 完成

        MonsterActor lastCarrier = null;
        GameObject lastCarrierRoot = null;
        for (int attempt = 0; attempt < 2; attempt++)
        {
            var carrier = Instantiate(openingCarrierPrefab, soul.transform.position + openingCarrierSpawnOffset, Quaternion.identity);
            var actor = carrier.GetComponentInChildren<MonsterActor>(true);
            if (actor == null)
            {
                Debug.LogError("[TutorialController] openingCarrierPrefab 无 MonsterActor 组件，销毁并跳过开场载体。");
                Destroy(carrier);
                yield break;
            }

            // 开场载体绕过 MonsterPool 直接 Instantiate，也必须从当前 Run 卡牌集合重建能力状态。
            if (CardManager.Instance != null)
            {
                CardManager.Instance.ResetAbilityUnlockState(carrier);
                CardManager.Instance.ApplyAllUnlocksTo(carrier);
            }

            // 载体以"永久附身等待尸体"出场（AI 休眠 + 附身窗口无限）——
            // BeginPossessionFlight 要求目标处于 Downed 可附身态，且避免活怪 AI 在附身动画期间攻击灵魂。
            actor.SpawnAsPermanentCorpse();
            actor.MarkAsOpeningCarrier();
            lastCarrier = actor;
            lastCarrierRoot = carrier;

            // Pass v1：autoPossessOpeningCarrier 关闭时，固定 Pride 载体保持为尸体，
            // 由玩家 Soul 落地后亲手 RMB Possess（第一次 Possession 触发 Pre-Combat 门）。
            if (!autoPossessOpeningCarrier)
            {
                Debug.Log("[TutorialController] 开场 Pride 载体已刷出为永久尸体，等待玩家亲手附身。");
                yield break;
            }

            var pm = PossessionManager.Instance;
            if (pm != null && pm.BeginPossessionFlight(actor))
            {
                OpeningCarrierPendingUntil = Time.unscaledTime + PendingValidSeconds;   // Probe 据此识别开场附身
                Debug.Log("[TutorialController] 开场载体：附身飞行已开始（pride_new 永久尸体态）。");
                yield break;
            }
            // 拒因随警告输出（PossessionManager 侧 rejection 日志为 Debug 级易被折叠，此处汇总便于定位）
            string rejectReason = pm == null ? "PossessionManager 未就绪"
                : !pm.CanStartPossession(out var stateReason) ? stateReason
                : !pm.ValidatePossessionTarget(actor, out var targetReason) ? targetReason
                : "灵魂缺失或载体预留失败";
            Debug.LogWarning($"[TutorialController] 开场载体附身飞行被拒（第 {attempt + 1} 次）：{rejectReason}。");
            if (attempt == 0)
            {
                Destroy(carrier);   // 首次被拒：清理后短暂重试
                lastCarrier = null;
                lastCarrierRoot = null;
                yield return new WaitForSeconds(0.5f);
            }
            // 末次被拒：保留载体（不销毁）交给下方保底——此前版本失败即销毁并置空 lastCarrier，
            // 保底分支永远拿不到载体，恒走"保底附身也失败"。
        }

        // 保底：正常飞行路径均被拒 → 对最后一次刷出的载体强制附身（不卡开局；
        // 不 FindObjectOfType 随机找，避免附身到场上无关怪）。
        Debug.LogWarning("[TutorialController] 开场载体飞行均失败，回退 DebugForcePossess 保底附身。");
        var pm2 = PossessionManager.Instance;
        if (lastCarrier != null && pm2 != null && pm2.DebugForcePossess(lastCarrier))
        {
            OpeningCarrierPendingUntil = 0f; // 保底附身走 Debug 路径，Probe 仍会收到 OnPossessionStarted；
                                             // pending 置零则该次被识别为普通附身——此处直接补报开场事实
            TutorialFactBus.Report(TutorialFact.OpeningCarrierPossessed);
        }
        else
        {
            if (lastCarrierRoot != null) Destroy(lastCarrierRoot); // 清理滞留场上的永久尸体载体
            Debug.LogError("[TutorialController] 开场载体保底附身也失败，玩家将保持未附身（教学 TUT-01 无法完成）。");
        }
    }

    void Update()
    {
#if UNITY_EDITOR
        // Debug 运行时快捷键宿主（Shift+T 查看 / Shift+Y 重置），仅编辑器 Play 调试使用，发布版不暴露
        TutorialDebugPanel.TickRuntimeHotkeys();
#endif

        // 强制模式(forceTutorial)上升沿：本局视为全新玩家，清空完成态并重放完整引导。
        // 场景启动时已为 true 的情况由 _forceReplayArmed 初始化跳过，避免每帧重复重放。
        if (GameManager.ForceTutorial)
        {
            if (!_forceReplayArmed) { _forceReplayArmed = true; ForceReplay(); }
        }
        else
        {
            _forceReplayArmed = false;
        }

        PollWasmMovement();
    }

    // WASD 移动边沿检测（TUT-01 完成条件）：纯移动帧不产生 ControlCommand.Pressed，
    // 故由本组件轮询检测。双保险：
    // ① 旧输入轴 Input.GetAxisRaw（旧输入系统可用时；activeInputHandler=2 新输入系统下抛异常 → 首次捕获后禁用）；
    // ② 玩家对象位置采样（不依赖输入系统，任何移动来源/新输入系统都有效）——防止 TUT-01 因检测失效永不完成、队列卡死。
    // 暂停（选卡 timeScale=0）时不采集。
    bool wasMovingLastFrame;
    static bool oldInputAvailable = true;   // 默认尝试旧输入轴一次；新输入系统抛异常后置 false
    float lastSampleTime;
    Vector3 lastSamplePos;

    void PollWasmMovement()
    {
        if (!started || Time.timeScale == 0f) return;

        // ① 旧输入轴（新输入系统下 GetAxisRaw 抛 InvalidOperationException，首次捕获后禁用）
        bool axisMoving = false;
        if (oldInputAvailable)
        {
            try
            {
                float h = Input.GetAxisRaw("Horizontal");
                float v = Input.GetAxisRaw("Vertical");
                axisMoving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
            }
            catch
            {
                oldInputAvailable = false;
            }
        }

        // ② 位置采样（双保险）：每 0.2s 采样玩家控制对象位置，位移 > 0.15m 视为移动
        bool posMoving = false;
        var pc = PlayerController.Instance;
        if (pc != null)
        {
            Vector3 pos = pc.transform.position;
            if (lastSampleTime == 0f)
            {
                lastSamplePos = pos;
                lastSampleTime = Time.unscaledTime;
            }
            else if (Time.unscaledTime - lastSampleTime >= 0.2f)
            {
                if (Vector3.Distance(pos, lastSamplePos) > 0.15f) posMoving = true;
                lastSamplePos = pos;
                lastSampleTime = Time.unscaledTime;
            }
        }

        bool moving = axisMoving || posMoving;
        if (moving && !wasMovingLastFrame)
            TutorialFactBus.Report(TutorialFact.InputMovementPressed);
        wasMovingLastFrame = moving;
    }

    // ---------------- 事实判定 ----------------

    void OnFactReported(TutorialFact fact)
    {
        // 记录一次性事实（本 Run 内存 + Profile 持久化，跨 Run 追溯）
        runSeenFacts.Add(fact);
        TutorialProfileStore.MarkSeenFact(fact);
        TutorialTelemetry.FactReported(fact);

        // 教学波门开闸：玩家完成首次附身（含开场载体附身）即放行波次刷怪。
        // 波门只在教学局关闭，此处对非教学局恒开状态无副作用（置 true 幂等）。
        if (fact == TutorialFact.PossessedFirstBody || fact == TutorialFact.OpeningCarrierPossessed)
            WaveStartGateOpen = true;

        // 玩家主动按 F 脱离：若当前正展示"脱离/换身"教学(TUT-05)，则直接结束该步骤——
        // 按 F 脱离本就是该步骤的核心完成手势（脱离即视为已学会）。
        if (fact == TutorialFact.ReleasedBody && activeQueueStep != null && activeQueueStep.id == "TUT-05")
            forceCompleteActiveQueue = true;

        if (!started || config == null || !TutorialAllowedThisRun) return;

        EvaluateAll();
    }

    /// <summary>命令事件 → 输入事实（TUT-02 三槽认知；WASD 移动不产生 Pressed，走 PollWasmMovement 轮询）。</summary>
    void OnCommandProduced(ControlCommand cmd)
    {
        if ((cmd.Pressed & CommandButtons.Mobility) != 0)
            TutorialFactBus.Report(TutorialFact.InputMobilityPressed);
        if ((cmd.Pressed & CommandButtons.Basic) != 0)
            TutorialFactBus.Report(TutorialFact.InputBasicPressed);
        if ((cmd.Pressed & CommandButtons.Skill2) != 0)
            TutorialFactBus.Report(TutorialFact.InputSkillPressed);
        // 注：TUT-04 击杀引导不再由普攻直接触发——由三段按键提示链 03C 完成后的 nextStepId 触发，
        // 保证队列顺序固定为 普攻→技能→位移→击杀引导。
    }

    /// <summary>
    /// TUT-MONSTER 微教学：首次附身某罪类型怪物时，查找 TUT-MONSTER-<罪中文名> Step 并入队（3 秒特点介绍）。
    /// 类型 key 用 sinType（同罪怪物共享一条特点介绍；按罪去重：每种罪首次附身提示一次）。
    /// </summary>
    void OnPossessionStartedForMonsterTut(MonsterActor body)
    {
        if (body == null) return;
        // sinType 兜底解析（与 PossessionImprintManager 同款）：某些怪 Awake 时根名未含罪关键词，
        // sinType 保持 None；PossessionCommitted（补解析点）晚于本事件，此处不兜底会静默漏掉怪介绍。
        if (body.sinType == SinType.None)
            body.ResolveSinIdentityFromHint(body.name + " " + body.displayName);
        // 仍无法解析时，再尝试父级名称兜底（神龛/载体可能包裹命名不含罪关键词的实例）
        if (body.sinType == SinType.None)
        {
            var p = body.transform.parent;
            if (p != null) body.ResolveSinIdentityFromHint(p.name);
        }

        // 微教学同样受本局准入约束：非准入路径不弹教学（也不写 Profile，避免污染正式局）
        if (!started || config == null || !TutorialAllowedThisRun) return;

        // 怪介绍依赖 sinType；解析失败（如开场载体/神龛躯体在运行实例上未带出罪名）时静默跳过——
        // TUT-02 已通过 nextStepId 直连 TUT-03A，整条教学链不会因此断裂。
        string key = body.sinType != SinType.None ? SinTypeDisplayName(body.sinType) : null;
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning($"[TutorialController] 附身怪 '{body.name}'（displayName='{body.displayName}'）无法解析罪类型，跳过怪介绍微教学（按键教学链由 TUT-02.nextStepId 兜底继续）。");
            return;
        }

        var monsterStep = config.FindStep("TUT-MONSTER-" + key);
        if (monsterStep == null || !monsterStep.queueDelivery) return;

        // 本局已提示过该罪类型 → 不再提示（同局内多次附身同罪怪只提示一次）
        if (runPossessedMonsterTypes.Contains(key)) return;

        // 跨 Run 已提示过该罪类型（persistAcrossRuns）则不再弹，但计入本局避免重复处理
        if (monsterStep.persistAcrossRuns
            && !GameManager.ForceTutorial
            && TutorialProfileStore.HasPossessedMonsterType(key))
        {
            runPossessedMonsterTypes.Add(key);
            return;
        }

        runPossessedMonsterTypes.Add(key);
        if (monsterStep.persistAcrossRuns)
            TutorialProfileStore.MarkPossessedMonsterType(key);

        if (runCompletedStepIds.Contains(monsterStep.id)) return;

        // 顺序保证：附身瞬间若按键提示已入队（TUT-02 的 nextStepId 已入队 TUT-03A），先摘除 ——
        // 队列变为 [怪介绍 → 按键提示]
        if (!string.IsNullOrEmpty(monsterStep.nextStepId))
            RemoveQueuedPrompt(monsterStep.nextStepId);
        Debug.Log($"[TutorialController] 附身观察：'{body.name}' sinType={body.sinType} → 怪介绍入队（{monsterStep.id}）。");
        EnqueuePrompt(monsterStep);
    }

    /// <summary>SinType → 中文罪名字符串（TUT-MONSTER 步骤 key；与 TutorialConfig 步骤 id 对齐）。</summary>
    static string SinTypeDisplayName(SinType sin)
    {
        switch (sin)
        {
            case SinType.Pride: return "傲慢";
            case SinType.Lust: return "色欲";
            case SinType.Wrath: return "愤怒";
            case SinType.Greed: return "贪婪";
            case SinType.Gluttony: return "暴食";
            case SinType.Envy: return "嫉妒";
            case SinType.Sloth: return "怠惰";
            default: return null;
        }
    }

    /// <summary>判断某事实当前是否"已满足"（一次性 = 内存或 Profile；状态性 = 实时查询）。</summary>
    bool IsFactSatisfied(TutorialFact fact)
    {
        switch (fact)
        {
            case TutorialFact.CorpseExists:
                return TutorialProbes.QueryCorpseExists();
            default:
                // 注意：不做 Profile 追溯（FactSeenInProfile）。
                // 跨局"不再提示"语义由 EnqueuePrompt / 队列完成时的 IsStepCompleted(persistAcrossRuns) 负责；
                // 若这里追溯 Profile，历史局按过键的记录会让新局三段按键提示入队即完成、完全不显示。
                return runSeenFacts.Contains(fact);
        }
    }

    // ---------------- Step 状态机 ----------------

    /// <summary>全量评估：激活新 Step + 结算已激活 Step（事件驱动 + 轮询共用入口）。</summary>
    void EvaluateAll()
    {
        // 1) 结算当前激活的 Step
        List<string> toComplete = null;
        foreach (var kv in activeSteps)
        {
            if (AreCompleteFactsSatisfied(kv.Value))
            {
                if (toComplete == null) toComplete = new List<string>();
                toComplete.Add(kv.Key);
            }
        }
        if (toComplete != null)
        {
            foreach (var id in toComplete) CompleteStep(id);
        }

        // 2) 激活可激活的未完成 Step
        if (currentBlockingStep != null) return; // 串行模式：当前 Step 未完不激活后续
        for (int i = 0; i < config.steps.Count; i++)
        {
            var step = config.steps[i];
            if (step == null) continue;
            if (activeSteps.ContainsKey(step.id)) continue;                 // 已激活
            if (StepCompletedThisRun(step.id)) continue;      // 已完成（本局/Profile 幂等跳过）
            if (!AreStartFactsSatisfied(step)) continue;                    // 开始条件未满足

            // 队列交付步骤：仅 startFacts 非空的步骤由事实评估自动入队
            // （三段按键提示/怪介绍/TUT-04 等靠显式触发或 nextStepId 链入队，避免开局顺序错乱）
            if (step.queueDelivery)
            {
                if (step.startFacts.Count > 0)
                    EnqueuePrompt(step);
                continue;
            }

            ActivateStep(step);
            if (step.blocking != TutorialBlockingMode.NonBlocking) break;   // 串行模式只激活一个
        }
    }

    void ActivateStep(TutorialStepConfig step)
    {
        // 防重：同一 Step 可能被多个入口同时激活（EvaluateAll 激活循环 + TUT-MONSTER 附身事件 + DebugStartStep），
        // 已激活则直接返回，避免重复 Banner 刷新与重复超时/提醒协程。
        if (activeSteps.ContainsKey(step.id)) return;

        activeSteps[step.id] = step;

        // 幂等结算：激活瞬间完成条件已满足（玩家提前完成了操作）→ 直接完成，不重复要求
        if (AreCompleteFactsSatisfied(step))
        {
            CompleteStep(step.id);
            return;
        }

        if (step.blocking != TutorialBlockingMode.NonBlocking)
            currentBlockingStep = step;

        // 短阻塞波次：关教学波门（WaveManager 首波前等待；对局中途激活时无实际阻塞效果——波门只在首波前检查）
        if (step.blocking == TutorialBlockingMode.BriefWaveBlock)
        {
            WaveStartGateOpen = false;
            Debug.Log("[TutorialController] 教学波门关闭（BriefWaveBlock）。");
        }

        if (ui != null)
            ui.ShowBanner(step.ResolveTitle(), ResolveText(step.ResolveBody()));
        SetExtraPanel(step, true);

        Debug.Log($"[TutorialController] Step 激活：{step.id}（{step.title}），阻断={step.blocking}，超时={step.timeoutSeconds}s，提醒={step.remindInterval}s");
        TutorialTelemetry.StepActivated(step.id, step.blocking);

        if (step.timeoutSeconds > 0f)
            StartCoroutine(TimeoutStep(step.id, step.timeoutSeconds));
        if (step.remindInterval > 0f)
            StartCoroutine(RemindStep(step.id, step.remindInterval));
        if (step.idleRemindSeconds > 0f)
            StartCoroutine(IdleRemindStep(step.id, step.idleRemindSeconds));
    }

    // ---------------- 提示队列 ----------------

    /// <summary>
    /// 提示入队（queueDelivery 步骤）：已在队列/显示中忽略；队列空闲立即开始显示。
    /// 只显示一次语义（可配置）：persistAcrossRuns=true → 跨 Run 只显示一次（Profile 幂等）；
    /// false → 本局只显示一次，每新 Run 都重新显示。
    /// </summary>
    void EnqueuePrompt(TutorialStepConfig step)
    {
        if (step == null || config == null || !started) return;
        if (!TutorialAllowedThisRun) return;
        if (runCompletedStepIds.Contains(step.id)) return;   // 本局已显示过
        if (step.persistAcrossRuns && !GameManager.ForceTutorial && TutorialProfileStore.IsStepCompleted(step.id))
            return;   // 跨 Run 只显示一次
        if (!queuedPromptIds.Add(step.id)) return;   // 已在队列/显示中（防重入）

        promptQueue.Add(step);
        if (!queueRunning && queueRoutine == null)
            queueRoutine = StartCoroutine(PromptQueueRoutine());
    }

    /// <summary>
    /// 公开一次性提示入口：按 Step ID 入队一条 queueDelivery 提示，供战斗系统触发机制向教学提示
    /// （如 Pass v1 §2.6 首次正式选卡时的「罪印双刃」提示）。门控/幂等/持久化由 EnqueuePrompt 统一保证。
    /// </summary>
    public void ShowPrompt(string stepId)
    {
        if (string.IsNullOrEmpty(stepId)) return;
        EnqueuePrompt(config != null ? config.FindStep(stepId) : null);
    }

    /// <summary>从待播队列中摘除指定 Step（若正在显示则不动）；附身事件用它保证"怪介绍先于按键提示"。</summary>
    void RemoveQueuedPrompt(string stepId)
    {
        if (string.IsNullOrEmpty(stepId)) return;
        for (int i = 0; i < promptQueue.Count; i++)
        {
            if (promptQueue[i].id == stepId)
            {
                promptQueue.RemoveAt(i);
                queuedPromptIds.Remove(stepId);
                return;
            }
        }
    }

    /// <summary>
    /// 队列调度协程：逐个显示提示。
    /// - displaySeconds &gt; 0：显示该时长后自动消失，播放下一条（无完成条件则直接标记完成）；
    /// - displaySeconds == 0：显示到 completeFacts 满足（timeoutSeconds 兜底超时），
    ///   完成后沿 nextStepId 链自动入队下一条。
    /// 上一条未结束下一条排队等待。
    /// </summary>
    IEnumerator PromptQueueRoutine()
    {
        queueRunning = true;
        while (promptQueue.Count > 0)
        {
            var step = promptQueue[0];
            activeQueueStep = step;   // 记录当前展示步骤，供"按 F 脱离即结束"定位
            promptQueue.RemoveAt(0);
            // 出队后仍有幂等防御：显示等待期间可能被外部标记完成（如 Debug 强制）
            if (runCompletedStepIds.Contains(step.id)
                || (step.persistAcrossRuns && !GameManager.ForceTutorial && TutorialProfileStore.IsStepCompleted(step.id)))
            {
                queuedPromptIds.Remove(step.id);
                continue;
            }

            if (ui != null)
                ui.ShowBanner(step.ResolveTitle(), ResolveText(step.ResolveBody()));
            SetExtraPanel(step, true);
            Debug.Log($"[TutorialController] 队列提示开始：{step.id}（{step.title}），显示时长={step.displaySeconds}s");

            if (step.displaySeconds > 0f)
            {
                // 定时模式：显示 N 秒后自动消失
                yield return new WaitForSeconds(step.displaySeconds);

                // 无完成条件的定时提示：显示完即算完成（persistAcrossRuns=true 才写 Profile = 跨 Run 只一次）
                if (step.completeFacts.Count == 0)
                    RecordStepCompletion(step, "displayed");

                if (ui != null) ui.Hide();
                SetExtraPanel(step, false);
                queuedPromptIds.Remove(step.id);

                // 自动入队 nextStepId（怪介绍 → 按键提示链；此前定时分支缺此链，怪介绍后队列断流）
                AdvanceToNextStep(step);
                continue;
            }

            // 完成条件模式：显示到完成条件满足（或超时兜底）；idleRemindSeconds 内未完成 → 重新弹一次提示
            float elapsed = 0f;
            float idleElapsed = 0f;
            bool idleFired = false;
            while (!AreCompleteFactsSatisfied(step) && !forceCompleteActiveQueue)
            {
                if (step.timeoutSeconds > 0f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (elapsed >= step.timeoutSeconds) break;
                }
                if (step.idleRemindSeconds > 0f && !idleFired)
                {
                    idleElapsed += Time.unscaledDeltaTime;
                    if (idleElapsed >= step.idleRemindSeconds)
                    {
                        idleFired = true;
                        if (ui != null)
                            ui.ShowBanner(step.ResolveTitle(), ResolveText(step.ResolveBody()));
                        Debug.Log($"[TutorialController] 队列提示静止提醒：{step.id}（{step.title}）{step.idleRemindSeconds}s 未完成，重新提示。");
                    }
                }
                yield return null;
            }
            forceCompleteActiveQueue = false;   // 消费"按 F 脱离"提前结束标记

            RecordStepCompletion(step, "fact");
            if (ui != null) ui.Hide();
            SetExtraPanel(step, false);
            queuedPromptIds.Remove(step.id);
            Debug.Log($"[TutorialController] 队列提示结束（完成/超时）：{step.id}");

            // 自动入队 nextStepId（如怪介绍 → 按键提示）
            AdvanceToNextStep(step);
        }
        queueRunning = false;
        activeQueueStep = null;
        queueRoutine = null;
    }

    void CompleteStep(string stepId, string reason = "fact")
    {
        if (!activeSteps.TryGetValue(stepId, out var step)) return;

        activeSteps.Remove(stepId);
        if (currentBlockingStep != null && currentBlockingStep.id == stepId)
            currentBlockingStep = null;

        // 开波门：BriefWaveBlock Step 完成（或超时）即放行波次
        if (step.blocking == TutorialBlockingMode.BriefWaveBlock)
        {
            WaveStartGateOpen = true;
            Debug.Log("[TutorialController] 教学波门打开（Step 完成）。");
        }

        // 统一结算：内存记忆 + 持久化（persistAcrossRuns）+ 埋点。
        // 本局内存记忆用于强制模式绕过 Profile 后防轮询重激活。
        RecordStepCompletion(step, reason);
        SetExtraPanel(step, false);   // 完成的 Step 收起其额外面板

        // Banner 管理：并行 Step（NonBlocking 模式）下完成一个不隐藏整个 Banner，
        // 而是切换到另一个仍激活的 Step 文案；全部完成才隐藏。
        if (ui != null)
        {
            if (activeSteps.Count > 0)
            {
                TutorialStepConfig next = null;
                foreach (var kv in activeSteps) { next = kv.Value; break; }
                ui.ShowBanner(next.ResolveTitle(), ResolveText(next.ResolveBody()));
                SetExtraPanel(next, true);   // 切换到下一个激活 Step 时，同步打开其额外面板
            }
            else
            {
                ui.Hide();
            }
        }

        Debug.Log($"[TutorialController] Step 完成：{stepId}（{step.title}）→ next={step.nextStepId}");

        // 自动激活 nextStep（其开始条件仍需满足；队列交付步骤入队由队列调度）
        AdvanceToNextStep(step);
    }

    /// <summary>
    /// 统一的"步骤完成登记"：内存记忆 + 可选 Profile 持久化（persistAcrossRuns）+ 埋点。
    /// 队列交付步骤（PromptQueueRoutine）与非队列激活步骤（CompleteStep）共用，
    /// 消除两套结算逻辑对"完成判定 / persist 策略 / 埋点"的重复与漂移。
    /// </summary>
    void RecordStepCompletion(TutorialStepConfig step, string reason)
    {
        runCompletedStepIds.Add(step.id); // HashSet 幂等
        if (step.persistAcrossRuns)
            TutorialProfileStore.MarkStepCompleted(step.id);
        TutorialTelemetry.StepCompleted(step.id, reason);
    }

    /// <summary>按 Step ID 查找绑定的额外面板（找不到返回 null）。</summary>
    GameObject FindExtraPanel(string stepId)
    {
        if (string.IsNullOrEmpty(stepId) || extraPanelBindings == null) return null;
        for (int i = 0; i < extraPanelBindings.Count; i++)
        {
            var b = extraPanelBindings[i];
            if (b != null && b.stepId == stepId) return b.panel;
        }
        return null;
    }

    /// <summary>统一管理 Step 的额外面板显隐：激活时显示，完成/隐藏时关闭。</summary>
    void SetExtraPanel(TutorialStepConfig step, bool active)
    {
        if (step == null) return;
        GameObject panel = FindExtraPanel(step.id);
        if (panel == null) return;
        if (panel.activeSelf != active)
            panel.SetActive(active);
    }

    /// <summary>关闭所有额外面板（重置/强制重放清理用）。</summary>
    void HideAllExtraPanels()
    {
        if (extraPanelBindings == null) return;
        for (int i = 0; i < extraPanelBindings.Count; i++)
        {
            var b = extraPanelBindings[i];
            if (b != null && b.panel != null && b.panel.activeSelf)
                b.panel.SetActive(false);
        }
    }

    /// <summary>
    /// 跳过本局所有剩余教程：关闭提示与额外面板、清空激活/队列、标记本局所有 Step 完成（防重激活）、
    /// 恢复波门并持久化"用户已跳过"标记。由 skipTutorialButton.onClick 调用。
    /// </summary>
    public void SkipTutorial()
    {
        // 关闭当前显示
        if (ui != null) ui.Hide();
        HideAllExtraPanels();

        // 清空激活态与队列
        activeSteps.Clear();
        promptQueue.Clear();
        queuedPromptIds.Clear();
        currentBlockingStep = null;
        activeQueueStep = null;
        forceCompleteActiveQueue = false;

        // 标记本局所有 Step 完成，防止 EvaluateAll / 事实事件重新激活
        if (config != null)
            for (int i = 0; i < config.steps.Count; i++)
                if (config.steps[i] != null) runCompletedStepIds.Add(config.steps[i].id);

        // 停止所有教学协程（超时/提醒/队列），并复位队列运行标记、重启轮询心跳
        StopAllCoroutines();
        queueRunning = false;
        queueRoutine = null;
        pollRoutine = StartCoroutine(PollActiveSteps());

        // 恢复波门（跳过阻断教学不卡波次）
        WaveStartGateOpen = true;

        // 持久化：用户已主动跳过（跨 Run 标记，供设置面板/后续准入判定使用）
        TutorialProfileStore.MarkSkippedByUser();

        // 跳过按钮消失，换出「重看引导」按钮
        SwapButtons(showSkip: false);

        Debug.Log("[TutorialController] 用户跳过教程：已结束本局所有剩余教学。");
    }

    /// <summary>重新开始引导（new tutorial 按钮）：从头重放完整引导，并把按钮切回跳过态。</summary>
    public void RestartTutorial()
    {
        ForceReplay();
        SwapButtons(showSkip: true);
        Debug.Log("[TutorialController] 重新开始引导。");
    }

    /// <summary>切换跳过/重看按钮显隐：showSkip=true 显示跳过按钮，否则显示「重看引导」按钮。</summary>
    void SwapButtons(bool showSkip)
    {
        if (skipTutorialButton != null) skipTutorialButton.gameObject.SetActive(showSkip);
        if (newTutorialButton != null) newTutorialButton.gameObject.SetActive(!showSkip);
    }

    /// <summary>
    /// 统一的 next 推进：沿 nextStepId 入队（queueDelivery）或激活（非队列），
    /// 需满足"本局未完成 + 开始条件已满足"（当前配置下 next 步骤 startFacts 皆为空，恒推进）。
    /// 队列模式原为无条件入队，此处统一后行为等价且更严谨（EnqueuePrompt 自带幂等防御）。
    /// </summary>
    void AdvanceToNextStep(TutorialStepConfig step)
    {
        if (string.IsNullOrEmpty(step.nextStepId)) return;
        var next = config.FindStep(step.nextStepId);
        if (next == null) return;
        if (StepCompletedThisRun(next.id)) return;
        if (!AreStartFactsSatisfied(next)) return;
        if (next.queueDelivery) EnqueuePrompt(next);
        else ActivateStep(next);
    }

    bool AreStartFactsSatisfied(TutorialStepConfig step)
    {
        for (int i = 0; i < step.startFacts.Count; i++)
        {
            if (!IsFactSatisfied(step.startFacts[i])) return false;
        }
        return true;
    }

    bool AreCompleteFactsSatisfied(TutorialStepConfig step)
    {
        if (step.completeFacts.Count == 0) return false; // 空完成条件 = 永不自动完成（需 Debug/未来指令式完成）
        for (int i = 0; i < step.completeFacts.Count; i++)
        {
            if (!IsFactSatisfied(step.completeFacts[i])) return false;
        }
        return true;
    }

    // ---------------- 协程：轮询 / 超时 / 提醒 ----------------

    /// <summary>低频轮询：状态性事实（尸体存在）变化时重新评估 + 教学尸体保护（TUT-04 激活期间延长尸体窗口）。</summary>
    IEnumerator PollActiveSteps()
    {
        var wait = new WaitForSeconds(0.5f);
        while (true)
        {
            yield return wait;

            // 教学尸体保护：激活中的 Step 声明 protectCorpseDuringStep 时，
            // 对场上所有未附身的 Downed 尸体持续延长附身窗口（教学读提示需要时间）。
            foreach (var kv in activeSteps)
            {
                if (!kv.Value.protectCorpseDuringStep) continue;
                var enemies = EnemyRegistry.All;
                for (int i = 0; i < enemies.Count; i++)
                {
                    var e = enemies[i];
                    if (e != null && e.isDowned && !e.isPossessed)
                        e.ExtendPossessionWindow(2f);
                }
                break;
            }

            if (activeSteps.Count > 0) EvaluateAll();
        }
    }

    IEnumerator TimeoutStep(string stepId, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (activeSteps.ContainsKey(stepId))
        {
            Debug.Log($"[TutorialController] Step 超时自动完成：{stepId}");
            CompleteStep(stepId, "timeout"); // 埋点统一交给 RecordStepCompletion，reason=timeout（不再重复预埋）
        }
    }

    /// <summary>
    /// 静止提醒：激活 N 秒后仍未满足完成条件 → 重新弹一次 Banner（提示玩家操作，如 WASD 移动）。
    /// 仅触发一次；玩家已满足完成条件或 Step 已完成则不打扰。
    /// </summary>
    IEnumerator IdleRemindStep(string stepId, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (activeSteps.TryGetValue(stepId, out var step) && !AreCompleteFactsSatisfied(step))
        {
            if (ui != null) ui.ShowBanner(step.ResolveTitle(), ResolveText(step.ResolveBody()));
            Debug.Log($"[TutorialController] Step 静止提醒：{stepId}（{step.title}）{seconds}s 未完成，重新提示。");
        }
    }

    IEnumerator RemindStep(string stepId, float interval)
    {
        yield return new WaitForSeconds(interval);
        while (activeSteps.TryGetValue(stepId, out var step))
        {
            if (ui != null) ui.ShowBanner(step.ResolveTitle(), ResolveText(step.ResolveBody()));
            yield return new WaitForSeconds(interval);
        }
    }

    // ---------------- 文案与 UI ----------------

    /// <summary>替换 {KEY} 占位符为动态键位显示名（如 {MOBILITY} → 空格）。</summary>
    string ResolveText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        return raw
            .Replace("{MOBILITY}", GameInputBindings.GlyphOf(CommandButtons.Mobility))
            .Replace("{BASIC}", GameInputBindings.GlyphOf(CommandButtons.Basic))
            .Replace("{SKILL}", GameInputBindings.GlyphOf(CommandButtons.Skill2))
            .Replace("{SKILL1}", GameInputBindings.GlyphOf(CommandButtons.Skill1))
            .Replace("{RELEASE}", GameInputBindings.GlyphOf(CommandButtons.Release))
            .Replace("{BULLET}", GameInputBindings.GlyphOf(CommandButtons.Skill3));
    }

    /// <summary>运行时自举最小 Banner；字体统一从 FontRegistry 读取。</summary>
    TutorialUI EnsureMinimalUI()
    {
        var go = new GameObject("TutorialBanner_Auto");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        go.AddComponent<UnityEngine.UI.CanvasScaler>();
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var ui = go.AddComponent<TutorialUI>();
        ui.BuildRuntimeLayout(go.transform);
        return ui;
    }

    void OnDisable()
    {
        // 队列协程中断兜底：父级禁用/StopAllCoroutines 中断 PromptQueueRoutine 时复位，
        // 防止 queueRunning 残留 true 导致 EnqueuePrompt 误判队列仍在运行而停摆。
        queueRunning = false;
        queueRoutine = null;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        TutorialFactBus.OnFactReported -= OnFactReported;
        PlayerController.OnCommandProduced -= OnCommandProduced;
        if (probes != null) probes.OnPossessionObserved -= OnPossessionStartedForMonsterTut;
        var session = RunSession.Instance;
        if (session != null) session.OnPhaseChanged -= OnPhaseChanged;
        if (probes != null) probes.StopProbing();
        // 场景卸载兜底复位：防静态残留影响下一局
        WaveStartGateOpen = true;
        OpeningCarrierPendingUntil = 0f;
    }

    // ---------------- Debug API ----------------

    /// <summary>Debug：强制开始指定 Step（无视开始条件；不覆盖 Profile 完成态）。</summary>
    public void DebugStartStep(string stepId)
    {
        if (config == null) return;
        var step = config.FindStep(stepId);
        if (step == null) { Debug.LogWarning($"[TutorialController] Debug 无此 Step：{stepId}"); return; }
        if (StepCompletedThisRun(stepId))
        {
            Debug.LogWarning($"[TutorialController] Step 已完成（本局/Profile），Debug 启动前请 ResetProfile 或开启 GameManager.forceTutorial 强制模式（本局已完成仍需新开一局）。");
            return;
        }
        ActivateStep(step);
    }

    /// <summary>Debug：强制完成指定 Step。</summary>
    public void DebugForceCompleteStep(string stepId)
    {
        if (activeSteps.ContainsKey(stepId)) { CompleteStep(stepId); return; }
        // 未激活也直接标记完成（后续激活会被幂等跳过）
        // Debug 语义：无条件写 Profile（含 persist=false 步骤），Debug 强制完成即"本局+跨局都算完成"；
        // 与正式路径（RecordStepCompletion 仅 persistAcrossRuns 写）有意不同，勿统一。
        runCompletedStepIds.Add(stepId);
        TutorialProfileStore.MarkStepCompleted(stepId);
        Debug.Log($"[TutorialController] Debug 强制完成：{stepId}");
    }

    /// <summary>Debug：报告当前未完成 Step 及未满足原因。</summary>
    public string DebugDescribeActive()
    {
        if (config == null) return "TutorialConfig 未配置";
        var sb = new System.Text.StringBuilder();
        foreach (var kv in activeSteps)
        {
            var step = kv.Value;
            sb.AppendLine($"  [{step.id}] {step.title}");
            for (int i = 0; i < step.completeFacts.Count; i++)
            {
                var f = step.completeFacts[i];
                sb.AppendLine($"    - {f}: {(IsFactSatisfied(f) ? "已满足" : "未满足")}");
            }
        }
        return sb.Length > 0 ? sb.ToString() : "无激活 Step";
    }

    /// <summary>Debug：打印配置全量总览（含 completeFacts 的语义名与 next 链），供策划/程序直读资产配置。</summary>
    public string DebugDescribeConfig()
    {
        if (config == null) return "TutorialConfig 未配置";
        var sb = new System.Text.StringBuilder();
        sb.Append($"配置 {config.name}（{config.steps.Count} Steps）：\n");
        for (int i = 0; i < config.steps.Count; i++)
        {
            var s = config.steps[i];
            var facts = new System.Text.StringBuilder();
            for (int j = 0; j < s.completeFacts.Count; j++)
            {
                if (j > 0) facts.Append(" & ");
                facts.Append(s.completeFacts[j]);
            }
            if (facts.Length == 0)
                facts.Append(s.displaySeconds > 0f ? "（无，定时显示）" : "（空，需外部/Debug 完成）");
            sb.AppendLine($"  [{s.id}] {s.title}");
            sb.AppendLine($"      queue={s.queueDelivery} display={s.displaySeconds}s persist={s.persistAcrossRuns} next=[{s.nextStepId}]");
            sb.AppendLine($"      完成条件: {facts}");
        }
        return sb.ToString();
    }
}
