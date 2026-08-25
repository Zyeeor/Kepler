using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Run 级流程阶段（整局状态链）：与战斗级状态（GameManager.GameState）分离。
/// Opening/Tutorial/Final 当前为结构占位（默认直通下一阶段，内容后续填充）。
/// </summary>
public enum RunPhase
{
    Opening,   // 开场（占位：默认直通）
    Tutorial,  // 教学（占位：默认直通）
    Waves,     // 波次（WaveManager 驱动）
    Choice,    // 选卡（与存档点绑定）
    Final,     // 最终阶段（占位：默认直通）
    Result,    // 结算（终态，胜利 VICTORY）
    Failed,    // 失败打断（终态，GAME OVER）
}

/// <summary>
/// 对局会话（Run）：一局完整对局的生命周期状态，常驻（DontDestroyOnLoad）。
///
/// 三层架构的"会话级"：跨场景持有对局进度（地图种子/已选卡/波次/灵魂状态），
/// 场景对象（MapStreamingSystem/WaveManager/CardManager）Awake 时向本会话查询并重建，
/// 不直接持有跨场景状态；持久化统一经 SaveCoordinator（纯 IO 层）。
///
/// 同时承担 Run 级流程总控（RunFlow）：整局由 RunPhase 状态链管理，
/// 阶段切换经 TransitionTo 集中校验并广播 OnPhaseChanged（UI/子系统订阅，事件驱动）。
///
/// 流转：
///   - 主菜单[新游戏] → BeginNewRun：随机种子 + 清状态 + 清旧存档（阶段=Opening）
///   - 主菜单[继续]   → LoadFromSave：读档填充内存态（阶段=Waves 或 Choice，跳过开场/教学）
///   - 对局中波间存档  → SaveProgress：更新内存态 + 落盘
///   - 返回主菜单     → 会话保留（内存态=最近波间，再进入零读盘恢复）
///   - 重开/胜利/失败 → EndRun：清内存态 + 清存档
/// </summary>
public class RunSession : MonoBehaviour
{
    public static RunSession Instance { get; private set; }

    /// <summary>是否有进行中的对局（BeginNewRun/LoadFromSave 后 true，EndRun 后 false）。</summary>
    public bool HasActiveRun { get; private set; }

    /// <summary>
    /// 本局是否由主菜单"新游戏"开始（仅 BeginNewRun 置 true）。
    /// 新人引导唯一合法触发入口：读档/直接 Play/重开路径均为 false，防止"其他形式进入对局"误触发引导。
    /// </summary>
    public bool StartedFromMainMenu { get; private set; }

    /// <summary>当前 Run 级流程阶段（整局状态链，总控）。</summary>
    public RunPhase CurrentPhase { get; private set; }

    /// <summary>阶段切换事件（UI/子系统订阅，事件驱动——不直接跨系统调用）。</summary>
    public event Action<RunPhase> OnPhaseChanged;

    /// <summary>
    /// 阶段转移（集中校验合法边 + 日志 + 广播）。非法转移仅警告不执行。
    /// 合法边：Opening→Tutorial→Waves↔Choice→Final→Result；Tutorial→Choice（教学横跨首波，清场后正常进选卡）；Waves/Choice/Final→Failed（打断边）。
    /// </summary>
    public void TransitionTo(RunPhase next)
    {
        if (CurrentPhase == next) return; // 幂等：已在目标阶段（多路径重复触发无副作用）
        if (!IsValidTransition(CurrentPhase, next))
        {
            Debug.LogWarning($"[RunFlow] 非法阶段转移：{CurrentPhase} → {next}（忽略）。");
            return;
        }
        var prev = CurrentPhase;
        CurrentPhase = next;
        Debug.Log($"[RunFlow] {prev} → {next}");
        OnPhaseChanged?.Invoke(next);
    }

    /// <summary>阶段合法转移表（null 行=非法）。终态 Result/Failed 不可再转移。</summary>
    static readonly Dictionary<RunPhase, RunPhase[]> PhaseTransitions = new Dictionary<RunPhase, RunPhase[]>
    {
        // Failed 为全局打断边：任意非终态（含直接 Play 场景时的 Opening）均可失败——
        // 玩家可能在开局/教学/波次任意时刻死亡，阶段不可阻塞失败结算。
        { RunPhase.Opening,  new[] { RunPhase.Tutorial, RunPhase.Failed } },
        // 教学横跨 Wave 0：清场选卡需 Tutorial→Choice（缺此边则首波选卡走出 Tutorial→Waves，
        // 无 Choice→Waves 边，精英 BD 快照上传触发器永不命中首波）。
        { RunPhase.Tutorial, new[] { RunPhase.Choice, RunPhase.Waves, RunPhase.Failed } },
        { RunPhase.Waves,    new[] { RunPhase.Choice, RunPhase.Final, RunPhase.Failed } },
        { RunPhase.Choice,   new[] { RunPhase.Waves, RunPhase.Failed } },
        { RunPhase.Final,    new[] { RunPhase.Result, RunPhase.Failed } },
        { RunPhase.Result,   Array.Empty<RunPhase>() }, // 终态
        { RunPhase.Failed,   Array.Empty<RunPhase>() }, // 终态
    };

    static bool IsValidTransition(RunPhase from, RunPhase to)
    {
        if (!PhaseTransitions.TryGetValue(from, out var allowed)) return false;
        for (int i = 0; i < allowed.Length; i++)
            if (allowed[i] == to) return true;
        return false;
    }

    /// <summary>地图种子：对局期间锁定，恢复时注入 MapStreamingSystem（地图确定性重建）。</summary>
    public uint WorldSeed { get; private set; }

    /// <summary>
    /// 本局 runId（精英 BD 快照 upsert 唯一键 (playerId, runId, sin) 的组成，策划案 §8.1/F6）：
    /// BeginNewRun 生成，随存档落盘，读档恢复后延续同一 runId。
    /// </summary>
    public string RunId { get; private set; }

    static string NewRunId() => "run-" + Guid.NewGuid().ToString("N");

    /// <summary>已完成波次索引（-1 = 尚未完成任何波），恢复从下一波开始。</summary>
    public int CompletedWaveIndex { get; private set; } = -1;

    /// <summary>选卡未完成标记：为 true 时恢复需先补弹选卡（在选卡界面退出后继续，不跳过本波选卡）。</summary>
    public bool PendingChoice { get; private set; }

    /// <summary>选卡界面退出时的候选卡 effectId 快照（恢复补弹时直接还原，保证与退出时一致）。</summary>
    public readonly List<string> ChoicePicks = new List<string>();

    /// <summary>本局已解锁卡牌效果（选卡会话结算后由场景对象同步进来）。</summary>
    public readonly List<string> UnlockedEffects = new List<string>();

    /// <summary>
    /// Global 卡软保底 streak（Encounter_CardOffer_Baseline §11）：
    /// 连续多少次 Offer 三张都没有 Global 卡；出现 Global 后重置。
    /// CardManager 每次 Offer 后同步进来（与 UnlockedEffects 同模式），存档落盘。
    /// </summary>
    public int GlobalMissStreak { get; set; }

    /// <summary>灵魂位置（最近一次波间存档点的玩家位置 = 下一波起点）。</summary>
    public Vector3 SoulPosition { get; private set; }

    /// <summary>灵魂 HP（存档点采样）。</summary>
    public float SoulHealth { get; private set; }

    /// <summary>灵魂时间（存档点采样）。</summary>
    public float SoulTime { get; private set; }

    /// <summary>玩家当前附身的怪（存档点采样；null = 灵魂态）。</summary>
    public SaveData.MonsterBodySave PossessedBody { get; private set; }

    /// <summary>场上可附身尸体（存档点采样，downed 且窗口内）。</summary>
    public readonly List<SaveData.MonsterBodySave> Corpses = new List<SaveData.MonsterBodySave>();

    public float ActiveCombatSeconds { get; private set; }
    public bool BossSpawned { get; private set; }
    public bool BossDefeated { get; private set; }

    /// <summary>
    /// 确保会话实例存在（主菜单/对局场景均可调用）。
    /// 自动创建常驻对象，无需在场景中挂载。
    /// </summary>
    public static RunSession EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[RunSession]");
        DontDestroyOnLoad(go);
        return go.AddComponent<RunSession>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        // 场景挂载路径警告（Kimi 评审整改）：RunSession 应只经 EnsureInstance 创建（DDOL 常驻）。
        // 场景内挂载的实例不做 DDOL，会随场景卸载销毁、内存态全丢。
        if (!Application.isPlaying && transform.parent == null)
            Debug.LogWarning($"[RunSession] 检测到场景内挂载 RunSession：请移除场景实例（BootStrapper 已在启动时 EnsureInstance 创建常驻实例）。", this);
        Instance = this;
    }

    /// <summary>
    /// 仅初始化世界种子（直接 Play 场景兜底用）：与 BeginNewRun 同款种子逻辑（useFixedSeed 生效），
    /// 但不清进度、不清存档、不置 HasActiveRun——确保"直接 Play"与"主菜单新游戏"的卡牌/刷怪序列一致。
    /// </summary>
    public void InitWorldSeed()
    {
        var gm = GameManager.Instance;
        WorldSeed = (gm != null && gm.useFixedSeed) ? gm.fixedSeed
                                                    : (uint)UnityEngine.Random.Range(1, int.MaxValue);
        if (string.IsNullOrEmpty(RunId)) RunId = NewRunId(); // 直接 Play 路径也保证有 runId
        StartedFromMainMenu = false; // 直接 Play/重开路径：不触发新人引导
        // Run Analytics：直接 Play 也启动采集（幂等；主菜单新局路径由 BeginNewRun 负责）
        RunStatsCollector.EnsureInstance().StartNewRun(RunId);
        CurrentPhase = RunPhase.Opening;
        Debug.Log($"[RunSession] 直接 Play 种子初始化：worldSeed={WorldSeed}（useFixedSeed={gm != null && gm.useFixedSeed}）。");
    }

    /// <summary>开始新对局：随机地图种子（或编辑器配置的固定种子）、清空进度、清除旧存档。</summary>
    public void BeginNewRun()
    {
        // 固定种子调试能力：GameManager 配置 useFixedSeed 时复用固定种子（便于复现同一张地图）
        var gm = GameManager.Instance;
        WorldSeed = (gm != null && gm.useFixedSeed) ? gm.fixedSeed
                                                    : (uint)UnityEngine.Random.Range(1, int.MaxValue);
        CompletedWaveIndex = -1;
        PendingChoice = false;
        ChoicePicks.Clear();
        UnlockedEffects.Clear();
        GlobalMissStreak = 0;
        SoulPosition = Vector3.zero;
        SoulHealth = 0f;
        SoulTime = 0f;
        PossessedBody = null;
        Corpses.Clear();
        // DDOL 灵魂跨局复用：上局 0 HP 死亡残留会让新局开局即死，新局必须回满（读档路径由 RestorePlayerRuntime 恢复，不经此）。
        if (PlayerHealth.Instance != null) PlayerHealth.Instance.ResetHealth();
        PossessionImprintManager.EnsureInstance().BeginNewRun();
        RunSpawnDirector.EnsureInstance().RestoreRuntime(0f, false, false);
        ActiveCombatSeconds = 0f;
        BossSpawned = false;
        BossDefeated = false;
        HasActiveRun = true;
        RunId = NewRunId();
        StartedFromMainMenu = true; // 主菜单"新游戏"：新人引导唯一合法触发入口
        SaveCoordinator.DeleteSave();
        // Run Analytics：新局启动采集器并重置统计（常驻单例自动创建）
        RunStatsCollector.EnsureInstance().StartNewRun(RunId);
        CurrentPhase = RunPhase.Opening; // 新局从开场开始（Opening 占位直通，见 RunFlow）
        Debug.Log($"[RunSession] 新对局开始：worldSeed={WorldSeed}");
    }

    /// <summary>
    /// 从存档恢复对局（主菜单"继续"）。成功返回 true；无有效存档返回 false（不开启会话）。
    /// </summary>
    public bool LoadFromSave()
    {
        SaveCoordinator.RequestResume();
        var data = SaveCoordinator.ResumeData;
        if (data == null)
        {
            Debug.LogWarning("[RunSession] 无有效存档，无法继续。");
            HasActiveRun = false;
            return false;
        }
        WorldSeed = data.worldSeed;
        CompletedWaveIndex = data.completedWaveIndex;
        PendingChoice = data.pendingChoice;
        ChoicePicks.Clear();
        if (data.choicePicks != null) ChoicePicks.AddRange(data.choicePicks);
        UnlockedEffects.Clear();
        if (data.unlockedEffects != null) UnlockedEffects.AddRange(data.unlockedEffects);
        GlobalMissStreak = data.globalMissStreak;
        SoulPosition = data.soulPosition;
        SoulHealth = data.soulHealth;
        SoulTime = data.soulTime;
        PossessedBody = data.possessedBody;
        Corpses.Clear();
        if (data.corpses != null) Corpses.AddRange(data.corpses);
        ActiveCombatSeconds = data.activeCombatSeconds;
        BossSpawned = data.bossSpawned;
        BossDefeated = data.bossDefeated;
        PossessionImprintManager.EnsureInstance().LoadFromSave(data.possessionImprints, data.greedBonusProgress, data.lustHealProgress);
        RunSpawnDirector.EnsureInstance().RestoreRuntime(ActiveCombatSeconds, BossSpawned, BossDefeated);
        // 读档延续同一 runId（老档/缺失字段时补生成，保证精英快照 upsert 键可用）
        RunId = !string.IsNullOrEmpty(data.runId) ? data.runId : NewRunId();
        HasActiveRun = true;
        StartedFromMainMenu = false; // 读档路径：不触发新人引导（阶段直接为 Waves/Choice）
        // 读档不经过开场/教学：回到波次或选卡补弹（pendingChoice=true → Choice）
        CurrentPhase = PendingChoice ? RunPhase.Choice : RunPhase.Waves;
        // 叙事调度 Run-local 状态恢复（旧档 narrative=null → 按新局初始化）
        NarrativeScheduler.Instance?.RestoreSnapshot(data.narrative);
        Debug.Log($"[RunSession] 读档恢复对局：已完成波 {CompletedWaveIndex + 1}，worldSeed={WorldSeed}，解锁卡 {UnlockedEffects.Count} 张（阶段={CurrentPhase}）。");
        return true;
    }

    /// <summary>
    /// 波间存档点：采样当前玩家状态 → 更新会话内存态 → 落盘。
    /// 由 WaveManager 在"波清场后"与"选完卡后"两个时间点调用。
    /// </summary>
    /// <param name="completedWaveIndex">刚完成的波次索引（恢复从下一波开始）。</param>
    /// <param name="pendingChoice">选卡是否未完成（true = 选卡界面退出，恢复时需补弹选卡）。</param>
    public void SaveProgress(int completedWaveIndex, bool pendingChoice = false)
    {
        // 采样场景运行时状态（波间时刻：SoulActor/PlayerHealth/GameManager 均在场景中）
        var soul = FindObjectOfType<SoulActor>();
        SoulPosition = soul != null ? soul.transform.position : Vector3.zero;
        SoulHealth = PlayerHealth.Instance != null ? PlayerHealth.Instance.currentHealth : 0f;
        SoulTime = GameManager.Instance != null ? GameManager.Instance.soulTime : 0f;
        CompletedWaveIndex = completedWaveIndex;
        PendingChoice = pendingChoice;
        SampleBodies();
        ActiveCombatSeconds = RunSpawnDirector.Instance != null ? RunSpawnDirector.Instance.ActiveCombatSeconds : ActiveCombatSeconds;
        BossSpawned = RunSpawnDirector.Instance != null && RunSpawnDirector.Instance.BossSpawned;
        BossDefeated = RunSpawnDirector.Instance != null && RunSpawnDirector.Instance.BossDefeated;
        // 候选卡不在此采样：CardManager 每次抽卡/重抽/恢复后已实时同步 ChoicePicks
        // （弹卡后任何时刻退出，快照都是玩家最后看到的候选，含双选第二轮/重抽结果）。

        SaveCoordinator.SaveSnapshot(completedWaveIndex, WorldSeed, UnlockedEffects,
            SoulPosition, SoulHealth, SoulTime, PossessedBody, Corpses, pendingChoice, ChoicePicks, GlobalMissStreak, RunId,
            ActiveCombatSeconds, PossessionImprintManager.EnsureInstance().CaptureStates(),
            PossessionImprintManager.EnsureInstance().GreedBonusProgress,
            PossessionImprintManager.EnsureInstance().LustHealProgress, BossSpawned, BossDefeated,
            NarrativeScheduler.Instance?.CaptureSnapshot());
        Debug.Log($"[RunSession] 波 {completedWaveIndex} 存档完成：位置={SoulPosition} HP={SoulHealth} 时间={SoulTime} 附身={(PossessedBody != null ? PossessedBody.prefabId : "无")} 尸体={Corpses.Count}");
    }

    /// <summary>采样附身怪与可附身尸体（波间时刻：玩家身体 + 场上待附身尸体）。</summary>
    void SampleBodies()
    {
        PossessedBody = null;
        Corpses.Clear();

        // 玩家当前附身怪
        var poss = PossessionManager.Instance;
        if (poss != null && poss.CurrentBody != null && poss.CurrentBody.isPossessed)
        {
            var body = poss.CurrentBody;
            PossessedBody = new SaveData.MonsterBodySave
            {
                prefabId = ResolvePrefabId(body.gameObject),
                position = body.transform.position,
                health = body.currentHealth,
            };
        }

        // 场上可附身尸体（downed 且窗口内、未附身未保留）
        var all = FindObjectsOfType<MonsterActor>(true);
        foreach (var m in all)
        {
            if (m == null || !m.CanBePossessed) continue;
            Corpses.Add(new SaveData.MonsterBodySave
            {
                prefabId = ResolvePrefabId(m.gameObject),
                position = m.transform.position,
                health = 0f,
            });
        }
    }

    /// <summary>
    /// 解析 prefabId：优先取 MonsterPool 反查的真实 prefab 资产名（恢复时与波表 prefab.name 匹配）；
    /// 非池实例（如场景静态怪）回退去 "(Clone)" 的实例名。
    /// </summary>
    static string ResolvePrefabId(GameObject instance)
    {
        if (instance == null) return null;
        var prefab = MonsterPool.Instance != null ? MonsterPool.Instance.GetPrefabOf(instance) : null;
        if (prefab != null) return prefab.name;
        string n = instance.name;
        return n != null ? n.Replace("(Clone)", "") : null;
    }

    /// <summary>
    /// 结束对局（重开/胜利/失败）：清内存态 + 清存档，回到无会话状态。
    /// </summary>
    public void EndRun()
    {
        // Run Analytics：中途结束（返回主菜单/重开/直接 Play 退出）时兜底结算未终结的对局
        if (RunStatsCollector.Instance != null)
            RunStatsCollector.Instance.EndRunEarly();
        HasActiveRun = false;
        StartedFromMainMenu = false;
        CurrentPhase = RunPhase.Opening; // 回到初始（无会话语义）
        CompletedWaveIndex = -1;
        UnlockedEffects.Clear();
        RunId = null;
        PossessionImprintManager.EnsureInstance().EndRun();
        RunSpawnDirector.EnsureInstance().RestoreRuntime(0f, false, false);
        ActiveCombatSeconds = 0f;
        BossSpawned = false;
        BossDefeated = false;
        SaveCoordinator.DeleteSave();
        Debug.Log("[RunSession] 对局结束，进度已清除。");
    }
}
