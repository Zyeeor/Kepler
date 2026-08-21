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
    [Header("配置")]
    [Tooltip("教学 Step 配置资产（策划编辑；留空 = 教学系统不工作，战斗不受影响）")]
    public TutorialConfig config;

    [Header("开场载体（Opening Carrier）")]
    [Tooltip("新局 Tutorial 阶段刷出的初始 Pride 载体 prefab（pride_new）；灵魂经正常附身飞行动画进入")]
    public GameObject openingCarrierPrefab;
    [Tooltip("载体出生位置（灵魂正前方偏移，世界坐标）")]
    public Vector3 openingCarrierSpawnOffset = new Vector3(0f, 0f, 5f);

    [Header("UI")]
    [Tooltip("教学 Banner UI（场景引用；留空则运行时自举创建最小 Banner）")]
    public TutorialUI ui;
    [Tooltip("教学 Banner 字体（自举 Banner 用；必须含中文，项目默认字体缺中文字形会显示方框）")]
    public TMPro.TMP_FontAsset bannerFont;

    TutorialProbes probes;
    readonly HashSet<TutorialFact> runSeenFacts = new HashSet<TutorialFact>();
    readonly Dictionary<string, TutorialStepConfig> activeSteps = new Dictionary<string, TutorialStepConfig>();
    readonly HashSet<string> runCompletedStepIds = new HashSet<string>();      // 本局已完成 Step（强制模式绕过 Profile 后防轮询重激活风暴）
    readonly HashSet<string> runPossessedMonsterTypes = new HashSet<string>(); // 本局已首次附身怪物类型（防微教学重复弹）

    TutorialStepConfig currentBlockingStep;   // BlockTutorialChain 时唯一激活的 Step
    Coroutine pollRoutine;
    bool started;
    bool openingCarrierStarted;   // 开场载体流程防重入（阶段事件 + Start 补查两条触发路径）

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
    /// 本局新人引导准入判定（唯一触发口径）：
    /// - GameManager.forceTutorial 强制开关 → 恒允许（调试，且同时忽略 Profile 幂等，全量重放）；
    /// - 否则要求：教学总开关开启 + 本局由主菜单"新游戏"开始（RunSession.StartedFromMainMenu）。
    ///   其他路径（读档/直接 Play/重开）一律不触发。
    /// Step 级幂等（Profile 已完成则跳过）在非强制模式下照旧生效。
    /// </summary>
    static bool TutorialAllowedThisRun
    {
        get
        {
            if (GameManager.ForceTutorial) return true;
            if (!TutorialProfileStore.TutorialEnabled) return false;
            var session = RunSession.Instance;
            return session != null && session.StartedFromMainMenu;
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

    /// <summary>怪物类型是否已首次附身过（本局内存记忆 + Profile；强制模式忽略 Profile，重弹微教学但本局不重复）。</summary>
    bool MonsterTypeSeenThisRun(string monsterTypeName)
        => runPossessedMonsterTypes.Contains(monsterTypeName)
           || (!GameManager.ForceTutorial && TutorialProfileStore.HasPossessedMonsterType(monsterTypeName));

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        TutorialFactBus.ClearSubscribers();
        TutorialFactBus.OnFactReported += OnFactReported;

        // 尽早注入中文字体（Awake 阶段）：EliteBuildDirector.Awake（经 WaveManager.Start 触发）
        // 会先于本组件 Start 创建 EliteNetworkStatusUI 动态文本，晚注入则其回退默认字体显示方框。
        if (bannerFont != null) UiFontAssets.Chinese = bannerFont;
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
        // TUT-MONSTER 微教学：首次附身某怪物类型（displayName 为 key）时提示该怪核心机制
        var pm = PossessionManager.Instance;
        if (pm != null) pm.OnPossessionStarted += OnPossessionStartedForMonsterTut;

        started = true;
        pollRoutine = StartCoroutine(PollActiveSteps());

        // 静态态复位（防跨局残留）：波门每局开始复位为开；开场载体 pending 标记清空
        WaveStartGateOpen = true;
        OpeningCarrierPendingUntil = 0f;

        // 开局主动评估：startFacts 为空的 Step（如 TUT-01 移动提示）应在开局立即激活，
        // 不等待首个事实报告（此前靠 OpeningCarrierPossessed 事实触发，附身动画期间无任何提示）。
        if (TutorialAllowedThisRun)
            EvaluateAll();

        Debug.Log($"[TutorialController] 教学系统启动（{config.steps.Count} 个 Step，开关={TutorialProfileStore.TutorialEnabled}，本局允许={TutorialAllowedThisRun}，强制开关={GameManager.ForceTutorial}）。");

        // 补查当前阶段：WaveManager.AutoStartRoutine 的 TransitionTo(Tutorial) 可能早于本组件 Start
        // （场景对象初始化序：协程 resume 先于部分 Start），导致 OnPhaseChanged 订阅错过 Tutorial 事件。
        // 此处主动检查当前阶段，若已处于 Tutorial 且本局准入通过则补启动开场载体流程
        // （防重入由 openingCarrierStarted 保证；准入口径见 TutorialAllowedThisRun）。
        if (session != null && session.CurrentPhase == RunPhase.Tutorial && TutorialAllowedThisRun)
            StartCoroutine(OpeningCarrierRoutine());
    }

    void OnPhaseChanged(RunPhase phase)
    {
        // 阶段变化不做强制行为；Step 激活与否由事实判定驱动（Tutorial 阶段的 Step 用 startFacts 空配置）
        Debug.Log($"[TutorialController] 阶段 → {phase}");

        // 新局进入 Tutorial 阶段：本局准入通过时启动开场载体流程（灵魂经正常附身飞行动画进入初始载体）
        if (phase == RunPhase.Tutorial && TutorialAllowedThisRun && !openingCarrierStarted)
            StartCoroutine(OpeningCarrierRoutine());
    }

    /// <summary>
    /// 开场载体流程（用户确认方案）：主角开局未附身 → 刷出 pride_new 载体 →
    /// PossessionManager.BeginPossessionFlight 触发正常附身飞行位移动画 → 自动提交附身。
    /// 失败重试 1 次，仍失败回退 DebugForcePossess（保底不卡开局）。
    /// 防重入：阶段事件与 Start 补查两条路径都可能触发，openingCarrierStarted 保证只执行一次。
    /// </summary>
    IEnumerator OpeningCarrierRoutine()
    {
        if (openingCarrierStarted) yield break;
        openingCarrierStarted = true;

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

            // 载体以"永久附身等待尸体"出场（AI 休眠 + 附身窗口无限）——
            // BeginPossessionFlight 要求目标处于 Downed 可附身态，且避免活怪 AI 在附身动画期间攻击灵魂。
            actor.SpawnAsPermanentCorpse();
            lastCarrier = actor;

            var pm = PossessionManager.Instance;
            if (pm != null && pm.BeginPossessionFlight(actor))
            {
                OpeningCarrierPendingUntil = Time.unscaledTime + PendingValidSeconds;   // Probe 据此识别开场附身
                Debug.Log("[TutorialController] 开场载体：附身飞行已开始（pride_new 永久尸体态）。");
                yield break;
            }
            Debug.LogWarning($"[TutorialController] 开场载体附身飞行被拒（第 {attempt + 1} 次），清理载体。");
            Destroy(carrier);
            lastCarrier = null;
            yield return new WaitForSeconds(0.5f);
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
            Debug.LogError("[TutorialController] 开场载体保底附身也失败，玩家将保持未附身（教学 TUT-01 无法完成）。");
        }
    }

    void Update()
    {
        // Debug 运行时快捷键宿主（Shift+T 查看 / Shift+Y 重置），依赖方向 Tutorial→Tutorial 无污染
        TutorialDebugPanel.TickRuntimeHotkeys();
    }

    // ---------------- 事实判定 ----------------

    void OnFactReported(TutorialFact fact)
    {
        // 记录一次性事实（本 Run 内存 + Profile 持久化，跨 Run 追溯）
        runSeenFacts.Add(fact);
        TutorialProfileStore.MarkSeenFact(fact);
        TutorialTelemetry.FactReported(fact);

        if (!started || config == null || !TutorialAllowedThisRun) return;

        EvaluateAll();
    }

    /// <summary>命令事件 → 输入事实（TUT-02 三槽认知；WASD 移动不产生 Pressed，走 Update 轮询）。</summary>
    void OnCommandProduced(ControlCommand cmd)
    {
        if ((cmd.Pressed & CommandButtons.Mobility) != 0)
            TutorialFactBus.Report(TutorialFact.InputMobilityPressed);
        if ((cmd.Pressed & CommandButtons.Basic) != 0)
            TutorialFactBus.Report(TutorialFact.InputBasicPressed);
        if ((cmd.Pressed & CommandButtons.Skill2) != 0)
            TutorialFactBus.Report(TutorialFact.InputSkillPressed);
    }

    // WASD 移动边沿检测（TUT-01 完成条件）：纯移动帧不产生 ControlCommand.Pressed，
    // 故由本组件轮询原始输入轴做"静止 → 移动"首帧报告；暂停（选卡 timeScale=0）时不采集。
    bool wasMovingLastFrame;
    void Update()
    {
        if (!started || Time.timeScale == 0f) return;
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        bool moving = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;
        if (moving && !wasMovingLastFrame)
            TutorialFactBus.Report(TutorialFact.InputMovementPressed);
        wasMovingLastFrame = moving;
    }

    /// <summary>
    /// TUT-MONSTER 微教学：首次附身某怪物类型时，查找 TUT-MONSTER-<displayName> Step 并激活。
    /// 类型 key 用 displayName（MonsterActor 尚无 monsterType 字段，战斗程序补齐后替换为枚举）。
    /// </summary>
    void OnPossessionStartedForMonsterTut(MonsterActor body)
    {
        if (body == null || string.IsNullOrEmpty(body.displayName)) return;
        if (MonsterTypeSeenThisRun(body.displayName)) return;
        runPossessedMonsterTypes.Add(body.displayName);
        TutorialProfileStore.MarkPossessedMonsterType(body.displayName);

        // 微教学同样受本局准入约束：非准入路径只记录首次附身事实，不弹教学
        if (!started || config == null || !TutorialAllowedThisRun) return;

        var monsterStep = config.FindStep("TUT-MONSTER-" + body.displayName);
        if (monsterStep != null && !StepCompletedThisRun(monsterStep.id))
            ActivateStep(monsterStep);
    }

    /// <summary>判断某事实当前是否"已满足"（一次性 = 内存或 Profile；状态性 = 实时查询）。</summary>
    bool IsFactSatisfied(TutorialFact fact)
    {
        switch (fact)
        {
            case TutorialFact.CorpseExists:
                return TutorialProbes.QueryCorpseExists();
            default:
                return runSeenFacts.Contains(fact) || FactSeenInProfile(fact);
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
            ui.ShowBanner(step.title, ResolveText(step.text));

        Debug.Log($"[TutorialController] Step 激活：{step.id}（{step.title}），阻断={step.blocking}，超时={step.timeoutSeconds}s，提醒={step.remindInterval}s");
        TutorialTelemetry.StepActivated(step.id, step.blocking);

        if (step.timeoutSeconds > 0f)
            StartCoroutine(TimeoutStep(step.id, step.timeoutSeconds));
        if (step.remindInterval > 0f)
            StartCoroutine(RemindStep(step.id, step.remindInterval));
    }

    void CompleteStep(string stepId)
    {
        if (!activeSteps.TryGetValue(stepId, out var step)) return;

        activeSteps.Remove(stepId);
        runCompletedStepIds.Add(stepId); // 本局内存记忆：强制模式绕过 Profile 后靠它防轮询重激活
        if (currentBlockingStep != null && currentBlockingStep.id == stepId)
            currentBlockingStep = null;

        // 开波门：BriefWaveBlock Step 完成（或超时）即放行波次
        if (step.blocking == TutorialBlockingMode.BriefWaveBlock)
        {
            WaveStartGateOpen = true;
            Debug.Log("[TutorialController] 教学波门打开（Step 完成）。");
        }

        TutorialProfileStore.MarkStepCompleted(stepId);
        // Banner 管理：并行 Step（NonBlocking 模式）下完成一个不隐藏整个 Banner，
        // 而是切换到另一个仍激活的 Step 文案；全部完成才隐藏。
        if (ui != null)
        {
            if (activeSteps.Count > 0)
            {
                TutorialStepConfig next = null;
                foreach (var kv in activeSteps) { next = kv.Value; break; }
                ui.ShowBanner(next.title, ResolveText(next.text));
            }
            else
            {
                ui.Hide();
            }
        }

        Debug.Log($"[TutorialController] Step 完成：{stepId}（{step.title}）→ next={step.nextStepId}");
        TutorialTelemetry.StepCompleted(stepId, "fact");

        // 自动激活 nextStep（其开始条件仍需满足）
        if (!string.IsNullOrEmpty(step.nextStepId))
        {
            var next = config.FindStep(step.nextStepId);
            if (next != null && !StepCompletedThisRun(next.id) && AreStartFactsSatisfied(next))
                ActivateStep(next);
        }
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
            TutorialTelemetry.StepCompleted(stepId, "timeout");
            CompleteStep(stepId);
        }
    }

    IEnumerator RemindStep(string stepId, float interval)
    {
        yield return new WaitForSeconds(interval);
        while (activeSteps.TryGetValue(stepId, out var step))
        {
            if (ui != null) ui.ShowBanner(step.title, ResolveText(step.text));
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

    /// <summary>运行时自举最小 Banner（TMP；优先用 bannerFont，否则退回 TMP 默认字体）。</summary>
    TutorialUI EnsureMinimalUI()
    {
        var go = new GameObject("TutorialBanner_Auto");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        go.AddComponent<UnityEngine.UI.CanvasScaler>();
        go.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var ui = go.AddComponent<TutorialUI>();
        ui.BuildRuntimeLayout(go.transform, bannerFont);
        return ui;
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        TutorialFactBus.OnFactReported -= OnFactReported;
        PlayerController.OnCommandProduced -= OnCommandProduced;
        var pm = PossessionManager.Instance;
        if (pm != null) pm.OnPossessionStarted -= OnPossessionStartedForMonsterTut;
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
}
