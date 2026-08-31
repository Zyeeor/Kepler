using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages upgrade cards. Picks random cards from the CardLibrary pool, and permanently
/// unlocks the chosen effect. 重构（S1/S2，见 Upgrade_Choice_System_Refactor.md）：
///   - 卡池从场景内联 allCards 抽离为 CardLibrary SO 资产
///   - 抽卡去重（EnumeratePool 按 effectId）+ 会话排除（shownThisSession）
///
/// 抽卡算法（需求来源：.vibe/doc/Canonical/Content/Encounter_CardOffer_Baseline_v1.0.md）：
///   - 三槽结构（§9）：Slot A=Horizontal（BasicUniversal+GlobalSlot）/ Slot B=Monster Type
///     （Known Type 限定的 MonsterType+TypeGrowth，按 Investment 轻度加权）/ Slot C=Flex（全合法池，排除本次已出现 ID）
///   - 过滤与 Fallback（§10）：已取得的卡 / 同 Offer 同 ID 不重复；A 空→Flex 池，B 空→放宽 Known Type→Flex，
///     C 空→任一未用池；不足 3 个唯一合法 ID 时允许少于 3 个（不造假选项）
///   - Global 软保底（§11）：连续 2 次 Offer 无 Global 后其候选权重提高，每继续 Miss 再提高，出现即重置
///   - Known Type Set（§2）：Pride 起始即进入；其余按波次解锁表（§3）推导——由 CompletedWaveIndex 纯函数恢复，无需额外存档
/// </summary>
public class CardManager : SceneSingleton<CardManager>
{
    [Header("Card Pool")]
    [Tooltip("卡池资产（SO）。运行时从此抽卡。")]
    public CardLibrary cardLibrary;
    [Tooltip("Effect Tag directory used to resolve CardData.grantedEffectTags at runtime.")]
    public GameplayTagCatalog gameplayTagCatalog;

    [Header("Reroll Limit")]
    [Tooltip("每张卡（按槽位记）最多可刷新的次数：0 = 不限，1 = 每卡 1 次，N = 每卡 N 次。")]
    public int maxRerollsPerCard = 1;

    [Header("Focus Assist")]
    [Tooltip("Focus Assist 概率：已有明确最高 Investment 的 Sin 时，Monster-Type 槽约以此概率直接给该 Sin 的一张合法未获得卡；无 Investment 时优先当前 Possessed Body 对应 Sin。")]
    [Range(0f, 1f)] public float focusAssistProbability = 0.65f;

    [Header("Card Offer Gems")]
    [Tooltip("卡牌选卡宝石 prefab（默认使用 Assets/Prefabs/Room/GEM.prefab）。所有正式选卡均先生成宝石，玩家拾取后才打开弹窗。")]
    public GameObject cardOfferGemPrefab;
    [Tooltip("新局出生点附近生成的宝石数量。")]
    [Min(0)] public int openingGemCount = 2;
    [Tooltip("开局宝石生成延迟（秒）：战斗就绪后延迟该秒数，再在玩家脚下生成开局宝石。0 = 立即生成。")]
    [Min(0f)] public float openingGemSpawnDelay = 0f;
    [Tooltip("Starter Gem 生成时刻（战斗开始后的秒数）。Pass v1：删除开局 2 Gem 后，战斗计时达到该值时生成 1 颗 Starter Gem（单次选卡）。")]
    [Min(0f)] public float starterGemTime = 30f;
    [Tooltip("Starter Gem 相对玩家锚点的生成偏移（前方，避免落在玩家身后永久丢失）。")]
    public Vector3 starterGemOffset = new Vector3(0f, 0f, 2.75f);
    [Tooltip("开局宝石是否每颗触发一次单选。关闭时每颗宝石触发双选。")]
    public bool openingGemDoublePick = false;
    [Tooltip("精英掉落的每颗宝石是否触发双选。默认 false：每颗只触发一次单选，奖励总量由上面随机掉落的颗数决定。改 true 则每颗都是双选（总量翻倍）。")]
    public bool eliteGemDoublePick = true;
    [Tooltip("精英击杀掉落的宝石数量下限（含）。多颗时在死亡位置周围散落。与上限相等即固定数量。")]
    [Min(1)] public int eliteGemCountMin = 2;
    [Tooltip("精英击杀掉落的宝石数量上限（含）。等于下限则固定掉落该数量；大于下限则在区间内随机。若误配成小于下限，运行时自动回退为下限。")]
    [Min(1)] public int eliteGemCountMax = 2;
    [Tooltip("每局精英击杀奖励的最大结算次数；0 = 不限。该次数按精英击杀奖励事件计，不按单颗宝石计。")]
    [Min(0)] public int eliteKillCardRewardLimit = 6;
    [Tooltip("玩家进入该半径后自动拾取宝石；角色移动不依赖 Rigidbody，因此由宝石轮询距离。")]
    [Min(0.25f)] public float cardOfferGemPickupRadius = 1.25f;

    [Header("Card Offer Gem 投放开关（各来源独立）")]
    [Tooltip("开局在出生点附近投放宝石（openingGemCount 颗）。关闭后开局不再生成宝石。")]
    public bool enableOpeningGems = true;
    [Tooltip("战斗计时达到 starterGemTime 时投放 Starter Gem（1 颗单选）。")]
    public bool enableStarterGem = true;
    [Tooltip("每波清场后投放波次选卡宝石。")]
    public bool enableWaveGems = true;
    [Tooltip("精英击杀后投放击杀奖励宝石。关闭后精英不再掉落宝石（也不消耗击杀奖励次数）。")]
    public bool enableEliteGems = true;
    [Tooltip("调试来源宝石（debugDoublePickOnStart 等）。")]
    public bool enableDebugGem = true;

    [Header("Card Offer Gem Attract")]
    [Tooltip("进入拾取半径后，宝石飘向玩家的动画时长（秒）。动画播完才打开选卡界面并暂停游戏。")]
    [Min(0f)] public float cardOfferGemAttractSeconds = 0.35f;
    [Tooltip("吸附终点相对玩家锚点的高度偏移（让宝石飘向胸口而不是脚底）。")]
    public float cardOfferGemAttractHeight = 0.8f;
    [Tooltip("吸附动画结束时的缩放系数：0 = 完全缩小消失。")]
    [Min(0f)] public float cardOfferGemAttractEndScale = 0f;

    [Header("Card Offer Gem Drop")]
    [Tooltip("掉落散落半径：多颗宝石沿掉落点周围环形散开，落点在该半径附近随机抖动。")]
    [Min(0f)] public float cardOfferGemScatterRadius = 1.2f;
    [Tooltip("掉落动画的水平初速（米/秒）。")]
    [Min(0.5f)] public float cardOfferGemDropForwardSpeed = 3.5f;
    [Tooltip("掉落动画的上抛初速（米/秒）；调大抛得更高更远。")]
    [Min(0f)] public float cardOfferGemDropUpSpeed = 4f;
    [Tooltip("掉落动画的重力加速度（米/秒²）；调大落得更快更干脆。")]
    [Min(1f)] public float cardOfferGemDropGravity = 18f;
    [Tooltip("掉落动画播完才可被拾取。关闭则宝石在空中就能被吸走。")]
    public bool gemPickupRequiresDropLanded = true;
    [Tooltip("宝石最终落点到玩家的最低水平距离；运行时还会与拾取半径叠加安全余量取较大值。")]
    [Min(0.25f)] public float gemPlayerSafeDistance = 2.25f;
    [Tooltip("宝石落点相对拾取半径额外留出的水平安全余量。")]
    [Min(0f)] public float gemPlayerClearance = 0.5f;
    [Tooltip("宝石被玩家位置校正后优先落在该距离；仅当原始预期落点过近时生效。")]
    [Min(0.25f)] public float gemPlayerPreferredDistance = 2.75f;
    [Tooltip("掉落落地后保持可见、暂不允许拾取的时间（秒）。")]
    [Min(0f)] public float gemPickupArmingDelay = 0.5f;

    [Tooltip("开局宝石相对出生点的散落位置；数量不足时使用默认左右偏移。")]
    public Vector3[] openingGemOffsets =
    {
        new Vector3(-2f, 0f, 1.25f),
        new Vector3(2f, 0f, 1.25f),
    };

    [Header("Debug")]
    [Tooltip("调试：开局生成一颗双选宝石，不再直接打开选卡弹窗。")]
    public bool debugDoublePickOnStart = false;

    [Header("Current Picks (read-only)")]
    public CardData[] currentPicks = new CardData[3];

    readonly List<CardChoiceGemPickup> activeOfferGems = new List<CardChoiceGemPickup>();
    bool openingGemRoutineStarted;
    string openingGemRunId;
    bool starterGemSpawned;
    bool gemFirstSpawnFactReported;

    /// <summary>首次正式选卡已触发「罪印双刃」提示（Pass v1 §2.6，仅一次）。</summary>
    bool reverseBDHintShown;

    /// <summary>
    /// 拾取流程互斥：当前正在"飘向玩家"或"选卡中"的宝石。
    /// 非 null 时其余宝石一律不进入拾取流程，保证附近有多颗宝石时同时只会触发一颗，
    /// 且必须选完卡（弹窗关闭 → 宝石销毁）后才轮到下一颗。
    /// </summary>
    CardChoiceGemPickup busyOfferGem;

    /// <summary>抽卡候选落定广播（叙事事件总线订阅：Offer=候选生成，含重抽外的每次呈现）。</summary>
    public static event System.Action OnCardOffered;
    public static event System.Action OnCardRerolled;

    /// <summary>
    /// 卡牌解锁广播（Run Analytics 采集用）：UnlockEffect 成功后触发（含选卡与调试解锁）。
    /// </summary>
    public static event System.Action<CardData> OnEffectUnlocked;

    // Track which effects have been permanently unlocked this run（已取得的卡；本局不再出现——最新需求：所有卡都只会出现一次）
    private HashSet<string> unlockedEffects = new HashSet<string>();
    // Boss mode can enter the scene while the persistent RunSession is still being
    // resolved by another scene object. Keep the initialization idempotent so every
    // later spawn/possession path can safely repair that ordering without repeating
    // the full-card scan every frame.
    private bool bossModeBuildsInitialized;
    // Known Type Set（§2）：本 Run 已合法遭遇的 Sin 类型（Pride 起始即进入，其余按波次解锁表推导）
    private readonly HashSet<SinType> knownTypes = new HashSet<SinType>();
    // 各 Sin 的 Investment（§6）：取得该 Sin 的 Monster-Type / Type Growth 卡数量（Slot B 加权用；可从已解锁卡推导，无需存档）
    private readonly Dictionary<SinType, int> investments = new Dictionary<SinType, int>();
    // Global 软保底 streak（§11）：连续多少次 Offer 无 Global 卡；出现即重置（存档落盘，见 RunSession.GlobalMissStreak）
    private int globalMissStreak;
    // Session exclusion: every card shown during the current popup session
    // (including ones rerolled away) never reappears until the next DrawCards.
    private readonly HashSet<string> shownThisSession = new HashSet<string>();
    private readonly Dictionary<int, int> rerollCounts = new Dictionary<int, int>(); // 槽位 → 已刷新次数（上限 maxRerollsPerCard）

    // ── 卡牌随机流（种子确定性，经 SeedSystem 统一派生）──
    // 本局卡牌（初始三张 + 每次刷新/重抽）由会话种子派生，与全局 UnityEngine.Random 隔离：
    // 同一种子下（读档恢复同 worldSeed），整局卡牌序列完全可复现。
    // 每波用独立子种子：SeedSystem.CreateFlow(DomainCard, waveIndex)。
    private int currentWaveCardSeed = -1;
    // 会话级随机流：DrawCards 时创建一次，本次会话内持续消费
    // （刷新/重抽复用同一流——正确范式，避免每次新建流取首值的反模式）。
    private System.Random sessionCardRng;

    // ── Known Type 波次解锁表（文档 §3 W1-W8 首版解锁结构，TUNABLE）──
    // W1 Pride+Gluttony → W2 +Wrath → W3 +Sloth → W4 +Greed → W5 +Lust → W6 +Envy；W7/W8 无新增。
    // Pride 作为固定 Starting Carrier 起始即进入 Known Type Set（§2），无需经解锁表。
    // 后续如需策划可调，可将此表迁移为 CardManager 序列化字段。
    static readonly SinType[][] WaveTypeUnlocks =
    {
        new[] { SinType.Pride, SinType.Gluttony }, // W1
        new[] { SinType.Wrath },                   // W2
        new[] { SinType.Sloth },                   // W3
        new[] { SinType.Greed },                   // W4
        new[] { SinType.Lust },                    // W5
        new[] { SinType.Envy },                    // W6
    };

    // ── 可调数值（文档 TUNABLE，首版取建议初值）──
    // §6：每 1 Investment +0.30 乘数增量，上限 +1.50（=5 层）
    const float kInvestmentStep = 0.30f;
    const int kInvestmentCap = 5;
    // §11：连续 2 次 Offer 无 Global 后开始加权，每继续 Miss 一次 +0.25（具体权重 Playable 调节）
    const int kGlobalPityStart = 2;
    const float kGlobalPityStep = 0.25f;

    /// <summary>局部随机流：每波独立子种子（SeedSystem 统一入口，质数混合防跨域/跨波关联）。</summary>
    System.Random CardRandom()
    {
        return SeedSystem.CreateFlow(SeedSystem.DomainCard, currentWaveCardSeed);
    }

    /// <summary>新一波选卡会话开始时固定子种子（由 CardManager 外部在弹卡前调用）。</summary>
    public void PrepareCardSession(int waveIndex)
    {
        currentWaveCardSeed = waveIndex;
    }

    /// <summary>本局已解锁效果（存档采集用，只读）。</summary>
    public IReadOnlyCollection<string> UnlockedEffects => unlockedEffects;

    protected override void Awake()
    {
        base.Awake();   // 防重复注册（场景二次加载时新实例销毁，防止 Instance 被覆盖成随后销毁的对象）
        if (Instance != this) return;
        // 恢复已解锁卡：优先从对局会话（RunSession，主菜单[继续]已填充；新局为空集）；
        // 兼容旧路径：无会话时回退读档（EnumeratePool 自动排除已取得的卡）。
        int completedWaves = -1;
        var run = RunSession.Instance;
        if (run != null && run.HasActiveRun)
        {
            foreach (var id in run.UnlockedEffects)
                if (!string.IsNullOrEmpty(id)) unlockedEffects.Add(id);
            completedWaves = run.CompletedWaveIndex;
            globalMissStreak = run.GlobalMissStreak;
            if (unlockedEffects.Count > 0)
                Debug.Log($"[CardManager] 会话恢复已解锁效果 {unlockedEffects.Count} 个。");
        }
        else
        {
            var resume = SaveCoordinator.ResumeData;
            if (resume != null)
            {
                if (resume.unlockedEffects != null)
                {
                    foreach (var id in resume.unlockedEffects)
                        if (!string.IsNullOrEmpty(id)) unlockedEffects.Add(id);
                    if (unlockedEffects.Count > 0)
                        Debug.Log($"[CardManager] 读档恢复已解锁效果 {unlockedEffects.Count} 个。");
                }
                completedWaves = resume.completedWaveIndex;
                globalMissStreak = resume.globalMissStreak;
            }
        }

        // Investment/Known Type 均由已解锁卡 + 波次进度纯推导（无需额外存档字段）
        RebuildInvestments();
        RefreshKnownTypes(completedWaves);   // Pride 起始常驻 + 按已完成波次累加解锁表（幂等）

        // 接好 AI 攻击/技能范围的解锁钩子：rangeUnlocks[].unlockId 视为卡牌 effectId。
        MonsterAIConfig.IsUnlocked = id => unlockedEffects.Contains(id);

        // Boss 模式不弹选卡，直接把卡库中所有启用的构筑效果视为已解锁。
        if (run != null && run.IsBossMode)
            UnlockAllEffectsForBossMode();
    }

    /// <summary>
    /// Rebuild all currently loaded monsters from the authoritative current-run card set.
    /// Static scene bodies and direct-instantiated tutorial bodies do not always pass through
    /// MonsterPool, so they need the same synchronization path once CardManager is ready.
    /// </summary>
    void ReapplyCurrentRunToExistingMonsters()
    {
        var monsters = FindObjectsOfType<MonsterActor>(true);
        foreach (var monster in monsters)
        {
            if (monster == null) continue;
            ResetAbilityUnlockState(monster.gameObject);
            ApplyAllUnlocksTo(monster.gameObject);
        }
    }

    void Start()
    {
        // Awake ordering is not guaranteed across the persistent RunSession and the
        // scene CardManager. Retry once after all scene Awake calls have completed.
        if (RunSession.Instance != null && RunSession.Instance.IsBossMode)
            UnlockAllEffectsForBossMode();

        // CardManager 的解锁集合此时已从 RunSession/存档恢复；统一重建场景里
        // 已存在的怪物，避免能力组件与 BuildView 卡牌状态因初始化顺序分叉。
        ReapplyCurrentRunToExistingMonsters();

        var run = RunSession.Instance;
        bool isResumeRun = run != null && run.HasActiveRun && !run.StartedFromMainMenu;
        if (debugDoublePickOnStart
            && !(run != null && run.IsBossMode)
            && !isResumeRun)
            StartCoroutine(DebugTriggerDoublePickOnStart());
    }

    void Update()
    {
        // 直接 Play 时 RunSession 可能由 WaveManager.Start 稍后创建；在状态进入 Opening/Tutorial/Waves
        // 后再启动一次性协程，避免 Start 顺序竞争导致开局宝石漏生成。
        // CardManager 挂在 DDOL 常驻对象上跨场景存活：启动进主菜单阶段会短暂进入 Tutorial，
        // 提前把 openingGemRoutineStarted 置 true，随后场景切到 MainMenu、找不到 SoulActor 而跳过。
        // 若不在新局（RunId 变化）时重置，从主菜单再次进入战场将永远不再生成开局宝石。
        var session = RunSession.Instance;
        if (openingGemRoutineStarted && session != null && !string.Equals(openingGemRunId, session.RunId))
            openingGemRoutineStarted = false;

        if (!debugDoublePickOnStart && !openingGemRoutineStarted && ShouldSpawnOpeningGems())
        {
            openingGemRoutineStarted = true;
            openingGemRunId = session != null ? session.RunId : null;
            Debug.Log("[CardManager] 开局宝石闸门开启，开始生成流程。" + DescribeOpeningGemGate());
            StartCoroutine(SpawnOpeningCardGemsWhenReady());
        }

        // Starter Gem（Pass v1）：战斗计时达到 starterGemTime 时生成 1 颗单选宝石。
        // 依赖 RunSpawnDirector.CombatStarted（首次 Possess 后才开始计时），自然满足 Pre-Combat 门。
        if (!starterGemSpawned && enableStarterGem && starterGemTime > 0f
            && RunSpawnDirector.Instance != null && RunSpawnDirector.Instance.CombatStarted
            && RunSpawnDirector.Instance.ActiveCombatSeconds >= starterGemTime
            && !(session != null && session.IsBossMode))
        {
            starterGemSpawned = true;
            SpawnStarterGem();
        }
    }

    /// <summary>
    /// 生成 Starter Gem（Pass v1）：在玩家前方生成 1 颗单选宝石，拾取后正常进行 1 次 Card Pick。
    /// 复用现有 Gem Pickup / Attract 系统，不新增硬编码生成逻辑。
    /// </summary>
    void SpawnStarterGem()
    {
        if (RunSession.Instance != null && RunSession.Instance.IsBossMode) return;
        if (!TryGetPlayerAnchorPosition(out Vector3 anchor)) return;

        Vector3 forward = Vector3.forward;
        PossessionManager pm = PossessionManager.Instance;
        if (pm != null && pm.CurrentBody != null)
            forward = pm.CurrentBody.transform.forward;
        else
        {
            SoulActor soul = FindObjectOfType<SoulActor>();
            if (soul != null) forward = soul.transform.forward;
        }
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        Vector3 gemPosition = anchor + forward * starterGemOffset.z + Vector3.up * starterGemOffset.y;
        gemPosition = EnsureGemLandingDistanceFromPlayer(gemPosition, anchor);
        CardChoiceGemPickup gem = SpawnCardOfferGem(gemPosition, doublePick: false, keepPicks: false,
            waveIndex: -1, source: CardOfferGemSource.Starter);
        if (gem == null)
            Debug.LogWarning("[CardManager] Starter Gem 生成失败。");
        else
        {
            // Pass v1 体验修复：直接 SpawnCardOfferGem 的宝石无掉落动画，玩家在战斗中难察觉，
            // 且 1.5m 距离很快被吸附。改为从上方 1.5m 掉落到落点，给玩家可见的掉落提示。
            if (gemPickupRequiresDropLanded)
                gem.StartDrop(gemPosition + Vector3.up * 1.5f, gemPosition, gemPosition.y, null);

            float combatTime = RunSpawnDirector.Instance != null ? RunSpawnDirector.Instance.ActiveCombatSeconds : 0f;
            Debug.Log($"[CardManager] Starter Gem 已生成（战斗 {combatTime:F1}s）。");
        }
    }

    /// <summary>Boss 模式解锁卡库中所有启用效果，供新生成怪物的构筑应用路径复用。</summary>
    public void UnlockAllEffectsForBossMode()
    {
        if (RunSession.Instance == null || !RunSession.Instance.IsBossMode) return;
        if (bossModeBuildsInitialized) return;
        if (cardLibrary == null || cardLibrary.cards == null)
        {
            Debug.LogWarning("[CardManager] Boss 模式无法全解锁：CardLibrary 未配置。");
            return;
        }

        bossModeBuildsInitialized = true;
        var unlockedIds = new HashSet<string>();
        foreach (var card in cardLibrary.cards)
        {
            if (card == null || string.IsNullOrEmpty(card.effectId)
                || !cardLibrary.IsEffectEnabled(card.effectId)
                || !unlockedIds.Add(card.effectId)) continue;
            UnlockEffect(card.effectId);
        }
        Debug.Log($"[CardManager] Boss 模式构筑全解锁：{unlockedIds.Count} 个效果。");
    }

    /// <summary>
    /// 调试：开局生成一颗双选宝石，不再直接打开选卡弹窗；玩家拾取后才开始 Offer。
    /// </summary>
    IEnumerator DebugTriggerDoublePickOnStart()
    {
        var run = RunSession.Instance;
        if (run != null && run.HasActiveRun && !run.StartedFromMainMenu)
        {
            Debug.Log("[CardManager] debugDoublePickOnStart：继续读档，跳过开局调试双选。");
            yield break;
        }
        float deadline = Time.realtimeSinceStartup + 10f;
        SoulActor soul = FindObjectOfType<SoulActor>();
        while (soul == null && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
            soul = FindObjectOfType<SoulActor>();
        }
        if (soul == null)
        {
            Debug.LogWarning("[CardManager] debugDoublePickOnStart：找不到灵魂，跳过调试宝石。");
            yield break;
        }
        CardChoiceGemPickup gem = SpawnCardOfferGem(
            soul.transform.position + Vector3.forward * 1.5f,
            true, false, 0, CardOfferGemSource.Debug);
        if (gem == null)
            Debug.LogWarning("[CardManager] debugDoublePickOnStart：宝石生成失败。");
        else
            Debug.Log("[CardManager] debugDoublePickOnStart：已生成双选宝石，拾取后触发选卡。");
    }

    bool ShouldSpawnOpeningGems()
    {
        RunSession run = RunSession.Instance;
        if (run == null || run.IsBossMode || run.OpeningCardGemsSpawned || debugDoublePickOnStart) return false;
        if (run.CompletedWaveIndex >= 0 || run.PendingChoice) return false;
        if (run.StartedFromMainMenu
            || run.CurrentPhase == RunPhase.Opening
            || run.CurrentPhase == RunPhase.Tutorial)
            return true;

        // 直接 Play 路径不会把 HasActiveRun 置为 true，但仍应拥有开局两颗宝石；
        // 只接受“尚未完成任何波次、当前已进入 Waves”的一次性初始态，避免读档/中途场景重载误生成。
        return !run.HasActiveRun && run.CurrentPhase == RunPhase.Waves;
    }

    /// <summary>开局宝石闸门诊断串（排查"宝石没生成"时定位到具体字段）。</summary>
    string DescribeOpeningGemGate()
    {
        RunSession run = RunSession.Instance;
        if (run == null) return " [run=null]";
        return $" [phase={run.CurrentPhase}, hasActiveRun={run.HasActiveRun}, fromMainMenu={run.StartedFromMainMenu}"
            + $", bossMode={run.IsBossMode}, completedWave={run.CompletedWaveIndex}, pendingChoice={run.PendingChoice}"
            + $", openingGemsSpawned={run.OpeningCardGemsSpawned}, debugDoublePickOnStart={debugDoublePickOnStart}]";
    }

    IEnumerator SpawnOpeningCardGemsWhenReady()
    {
        // 每个等待阶段使用独立超时预算：共用一个累加器会让前一步耗时挤掉后一步的等待窗口，
        // 表现为"SoulActor 明明会出现却被判定找不到"。
        float wait = 0f;
        while (RunSession.Instance == null && wait < 10f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }
        if (RunSession.Instance == null)
        {
            Debug.LogWarning("[CardManager] 开局宝石：等待 RunSession 超时，跳过生成。");
            yield break;
        }

        wait = 0f;
        SoulActor soul = FindBattleSoul();
        while (soul == null && wait < 10f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
            soul = FindBattleSoul();
        }
        if (soul == null)
        {
            Debug.LogWarning("[CardManager] 开局宝石：找不到战斗灵魂（SoulActor），跳过生成。");
            yield break;
        }

        // 等待本场景所有 Start 执行完，确保 OpeningLandingSequence 已有机会把门置为 false；
        // 否则 Start 顺序竞争时可能在降落演出开始前就把宝石放到空中/初始位置。
        yield return null;
        wait = 0f;
        while (!OpeningLandingSequence.LandingComplete && wait < 10f)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        RunSession run = RunSession.Instance;
        if (run == null || !ShouldSpawnOpeningGems())
        {
            // 等待期间状态发生变化（进入选卡/已完成波次/开局宝石已由别处生成等）。
            // 这里必须留日志：否则开局宝石会"静默不生成"，无法与 prefab 配置错误区分。
            Debug.LogWarning("[CardManager] 开局宝石：等待就绪后闸门已关闭，跳过生成。" + DescribeOpeningGemGate());
            yield break;
        }

        // 场景切换（启动进主菜单 → 进入战场）可能使上面捕获的 soul 引用失效：
        // 生成前重新获取真实战斗灵魂，避免把宝石生成到主菜单展示灵魂处，或对已销毁引用取位置抛空引用。
        soul = FindBattleSoul();
        if (soul == null)
        {
            Debug.LogWarning("[CardManager] 开局宝石：生成前未找到战斗灵魂，跳过生成。");
            yield break;
        }

        // 开局宝石生成延迟：延迟结束后在玩家脚下生成。
        if (openingGemSpawnDelay > 0f)
        {
            float delayWait = 0f;
            while (delayWait < openingGemSpawnDelay)
            {
                delayWait += Time.unscaledDeltaTime;
                yield return null;
            }
            // 延迟期间玩家可能移动，重新取真实战斗灵魂位置。
            soul = FindBattleSoul();
            if (soul == null)
            {
                Debug.LogWarning("[CardManager] 开局宝石：延迟后未找到战斗灵魂，跳过生成。");
                yield break;
            }
        }

        int desired = Mathf.Max(0, openingGemCount);
        if (desired == 0)
        {
            run.MarkOpeningCardGemsSpawned();
            yield break;
        }

        int spawned = 0;
        for (int i = 0; i < desired; i++)
        {
            Vector3 offset = GetOpeningGemOffset(i, desired);
            CardChoiceGemPickup gem = SpawnCardOfferGem(
                soul.transform.position + offset,
                openingGemDoublePick,
                false,
                0,
                CardOfferGemSource.Opening);
            if (gem != null) spawned++;
        }

        if (spawned == desired)
        {
            run.MarkOpeningCardGemsSpawned();
            Debug.Log($"[CardManager] 开局宝石已生成：{spawned} 颗（每颗拾取后打开选卡）。");
        }
        else
        {
            Debug.LogWarning($"[CardManager] 开局宝石生成不完整：{spawned}/{desired}，请检查 cardOfferGemPrefab 配置。");
        }
    }

    /// <summary>
    /// 获取战斗场景中的真实灵魂（排除主菜单展示灵魂 SoulMenuShowcase）。
    /// 启动进主菜单阶段主菜单里只有展示灵魂，直接 FindObjectOfType 会误取到它，
    /// 导致开局宝石生成到主菜单、或随场景卸载后引用失效抛空引用。
    /// </summary>
    static SoulActor FindBattleSoul()
    {
        SoulActor[] souls = FindObjectsOfType<SoulActor>();
        for (int i = 0; i < souls.Length; i++)
        {
            if (souls[i] == null) continue;
            if (souls[i].GetComponent<SoulMenuShowcase>() != null) continue;
            return souls[i];
        }
        return null;
    }

    Vector3 GetOpeningGemOffset(int index, int total)
    {
        if (openingGemOffsets != null && index >= 0 && index < openingGemOffsets.Length)
            return openingGemOffsets[index];
        if (total == 2)
            return index == 0 ? new Vector3(-2f, 0f, 1.25f) : new Vector3(2f, 0f, 1.25f);

        float angle = (index / (float)Mathf.Max(1, total)) * Mathf.PI * 2f;
        return new Vector3(Mathf.Cos(angle) * 2f, 0f, Mathf.Sin(angle) * 2f);
    }

    public bool TryGetPlayerAnchorPosition(out Vector3 position)
    {
        PossessionManager possession = PossessionManager.Instance;
        if (possession != null && possession.CurrentBody != null
            && possession.CurrentBody.gameObject.activeInHierarchy)
        {
            position = possession.CurrentBody.transform.position;
            return true;
        }

        SoulActor soul = FindObjectOfType<SoulActor>();
        if (soul != null && soul.gameObject.activeInHierarchy)
        {
            position = soul.transform.position;
            return true;
        }

        position = Vector3.zero;
        return false;
    }

    public bool IsPlayerWithinPickupRadius(Vector3 gemPosition, float radius)
    {
        if (!TryGetPlayerAnchorPosition(out Vector3 playerPosition)) return false;
        Vector3 delta = playerPosition - gemPosition;
        delta.y = 0f;
        float maxDistance = Mathf.Max(0.25f, radius);
        return delta.sqrMagnitude <= maxDistance * maxDistance;
    }

    /// <summary>
    /// 将过于靠近玩家的预期落点推到玩家可感知、需要主动走过去的位置。
    /// 只校正水平 XZ 距离，保留调用方计算出的地面高度。
    /// </summary>
    Vector3 EnsureGemLandingDistanceFromPlayer(Vector3 candidate, Vector3 origin)
    {
        if (!TryGetPlayerAnchorPosition(out Vector3 playerPosition)) return candidate;

        Vector3 fromPlayer = candidate - playerPosition;
        fromPlayer.y = 0f;
        float currentDistance = fromPlayer.magnitude;
        float safeDistance = Mathf.Max(
            Mathf.Max(0.25f, gemPlayerSafeDistance),
            Mathf.Max(0.25f, cardOfferGemPickupRadius) + Mathf.Max(0f, gemPlayerClearance));
        if (currentDistance >= safeDistance) return candidate;

        float preferredDistance = Mathf.Max(safeDistance, Mathf.Max(0.25f, gemPlayerPreferredDistance));
        Vector3 direction = currentDistance > 0.0001f ? fromPlayer / currentDistance : Vector3.zero;

        // 精英刚好死在玩家脚下时，候选落点方向可能也接近零：优先使用死亡点相对玩家的方向，
        // 再退回当前玩家朝向，保证宝石不会原地生成并立即被拾取。
        if (direction.sqrMagnitude < 0.0001f)
        {
            Vector3 fromOrigin = origin - playerPosition;
            fromOrigin.y = 0f;
            if (fromOrigin.sqrMagnitude > 0.0001f)
                direction = fromOrigin.normalized;
        }
        if (direction.sqrMagnitude < 0.0001f)
        {
            PossessionManager possession = PossessionManager.Instance;
            if (possession != null && possession.CurrentBody != null)
            {
                direction = possession.CurrentBody.transform.forward;
                direction.y = 0f;
            }
            else
            {
                SoulActor soul = FindObjectOfType<SoulActor>();
                if (soul != null)
                {
                    direction = soul.transform.forward;
                    direction.y = 0f;
                }
            }
        }
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
        direction.Normalize();

        Vector3 adjusted = playerPosition + direction * preferredDistance;
        adjusted.y = candidate.y;
        return adjusted;
    }

    /// <summary>各来源宝石投放开关：关闭时该来源不生成宝石（由唯一入口 SpawnCardOfferGem 统一拦截）。</summary>
    public bool IsGemSourceEnabled(CardOfferGemSource source)
    {
        switch (source)
        {
            case CardOfferGemSource.Opening: return enableOpeningGems;
            case CardOfferGemSource.Starter: return enableStarterGem;
            case CardOfferGemSource.Wave: return enableWaveGems;
            case CardOfferGemSource.Elite: return enableEliteGems;
            case CardOfferGemSource.Debug: return enableDebugGem;
            default: return true;
        }
    }

    /// <summary>生成一颗选卡宝石；正式选卡的唯一生成入口。</summary>
    public CardChoiceGemPickup SpawnCardOfferGem(Vector3 position, bool doublePick, bool keepPicks,
        int waveIndex, CardOfferGemSource source, Action onChoiceCompleted = null)
    {
        RunSession run = RunSession.Instance;
        if (run != null && run.IsBossMode)
        {
            Debug.Log("[CardManager] Boss 模式不生成选卡宝石。");
            return null;
        }
        // 投放开关：开局/Starter/波次/精英/调试各自独立，全部经本入口生成，故在此一处拦截即可。
        if (!IsGemSourceEnabled(source))
        {
            Debug.Log($"[CardManager] 宝石投放已关闭：source={source}，跳过生成（可在 CardManager 的 "
                + "Card Offer Gem 投放开关 中开启）。");
            return null;
        }
        if (cardOfferGemPrefab == null)
        {
            Debug.LogError("[CardManager] cardOfferGemPrefab 未配置，无法生成选卡宝石。", this);
            return null;
        }
        // 场景 YAML 若把引用写成 prefab 资产对象（fileID 100100000）而不是 prefab 根 GameObject 的
        // fileID，字段会解析成"非 null 但无效"的幽灵引用（name 为空串）→ Instantiate 静默失败。
        // 这里显式拦截并给出可操作提示，避免再次出现"宝石一颗都不生成且无任何报错"。
        if (string.IsNullOrEmpty(cardOfferGemPrefab.name))
        {
            Debug.LogError("[CardManager] cardOfferGemPrefab 引用无效（未解析到 prefab 根 GameObject），"
                + "请在 Inspector 重新拖入 Assets/Prefabs/Room/GEM.prefab。", this);
            return null;
        }

        GameObject instance = Instantiate(cardOfferGemPrefab, position, Quaternion.identity);
        if (instance == null)
        {
            Debug.LogError($"[CardManager] 选卡宝石实例化失败：prefab={cardOfferGemPrefab.name}, source={source}。", this);
            return null;
        }
        instance.name = $"CardOfferGem_{source}";
        CardChoiceGemPickup pickup = instance.GetComponent<CardChoiceGemPickup>();
        if (pickup == null) pickup = instance.AddComponent<CardChoiceGemPickup>();
        pickup.Initialize(this, doublePick, keepPicks, waveIndex, source,
            cardOfferGemPickupRadius, onChoiceCompleted);
        activeOfferGems.Add(pickup);

        // GEM 选卡宝石第一次出现：报告教学事实（只报一次，供教程 startFacts 使用）。
        if (!gemFirstSpawnFactReported)
        {
            gemFirstSpawnFactReported = true;
            TutorialFactBus.Report(TutorialFact.GemFirstSpawned);
        }
        return pickup;
    }

    /// <summary>
    /// 解析本次精英掉落的数量：下限/上限相等即固定数量，否则在区间内随机。
    /// 集中在这里是为了让调用方与日志用同一套口径，并兜住两类配置错误：
    /// 上限被误配成小于下限、上限/下限被序列化成 0（新增字段的老场景常见）。
    /// </summary>
    public int ResolveEliteGemDropCount()
    {
        int min = Mathf.Max(1, eliteGemCountMin);
        int max = Mathf.Max(1, eliteGemCountMax);
        if (max < min) max = min;          // 上限误配小于下限 → 按固定数量处理
        return max > min ? UnityEngine.Random.Range(min, max + 1) : min;
    }

    /// <summary>
    /// 在掉落点周围散落生成多颗选卡宝石，每颗播"弹射散落"动画，落地后才可拾取。
    /// 用于精英击杀等"一次掉多颗、随机位置"的场景。每颗宝石独立结算一次选卡，
    /// 由拾取互斥闸门保证同时只触发一颗、选完卡才轮到下一颗。
    /// </summary>
    /// <param name="origin">掉落点（如精英怪死亡位置）。</param>
    /// <param name="count">掉落数量；小于等于 1 时原地掉落不散开。</param>
    /// <returns>实际生成的宝石数量。</returns>
    public int SpawnCardOfferGemScatter(Vector3 origin, int count, bool doublePick, bool keepPicks,
        int waveIndex, CardOfferGemSource source, Action onChoiceCompleted = null)
    {
        int desired = Mathf.Max(0, count);
        if (desired == 0) return 0;

        float scatter = Mathf.Max(0f, cardOfferGemScatterRadius);
        // 单颗也散开（Pass v1 bug fix）：精英击杀点就在玩家脚下，单颗若原地落下会立即进入拾取半径
        // 被吸附 → 玩家看不到掉落过程、直接选卡。保留小散开让弹射动画可见、宝石落在玩家可拾取范围。

        // 环形均分 + 随机抖动：既保证多颗散得开，又不会每次都一样。
        float baseAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        int spawned = 0;
        for (int i = 0; i < desired; i++)
        {
            Vector3 landing = origin;
            if (scatter > 0.001f)
            {
                float angle = baseAngle + (Mathf.PI * 2f * i) / desired + UnityEngine.Random.Range(-0.35f, 0.35f);
                float radius = scatter * UnityEngine.Random.Range(0.7f, 1f);
                landing = origin + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            // 精英死亡点靠近玩家时，不让随机落点直接落入拾取范围；改为推到玩家前方的可感知距离。
            landing = EnsureGemLandingDistanceFromPlayer(landing, origin);

            CardChoiceGemPickup gem = SpawnCardOfferGem(landing, doublePick, keepPicks, waveIndex,
                source, onChoiceCompleted);
            if (gem == null) continue;
            spawned++;

            if (gemPickupRequiresDropLanded)
                gem.StartDrop(origin, landing, origin.y, null);
        }
        return spawned;
    }

    /// <summary>
    /// 当前是否已有宝石处于拾取流程中（正在飞向玩家，或选卡弹窗进行中）。
    /// 非 null 时其余宝石一律不触发拾取：附近有多颗宝石时同时只会触发一颗。
    /// </summary>
    public bool IsCardOfferGemBusy()
    {
        CardChoiceGemPickup busy = busyOfferGem;
        if (busy == null) return false;
        // 兜底：宝石已被销毁却没走完释放流程时不要永久卡死闸门。
        if (busy.IsChoiceCompleted || !activeOfferGems.Contains(busy))
        {
            busyOfferGem = null;
            return false;
        }
        return true;
    }

    /// <summary>宝石进入玩家拾取半径后开始吸附动画；动画播完才打开选卡会话。</summary>
    public bool TryCollectCardOfferGem(CardChoiceGemPickup gem)
    {
        if (gem == null || !activeOfferGems.Contains(gem) || gem.IsCollected || gem.IsChoiceCompleted)
            return false;
        if (CoreChoiceUI.Instance == null || CoreChoiceUI.Instance.IsDrafting)
            return false;
        RunSession run = RunSession.Instance;
        if (run != null && run.IsBossMode) return false;
        // 已有别的宝石在飞/在选卡 → 本颗原地等待，等上一颗选完卡释放闸门后才可拾取。
        // 正在飞行中的那颗自己也会回到这里，此处直接短路，避免被自己的闸门拒掉。
        if (busyOfferGem != null && busyOfferGem != gem) return false;
        if (busyOfferGem == null && IsCardOfferGemBusy()) return false;

        busyOfferGem = gem;
        // 先播"飘向玩家 + 缩小消失"，动画结束（OnCardOfferGemAttracted）才弹窗并暂停游戏。
        gem.StartAttract(OnCardOfferGemAttracted);
        return true;
    }

    /// <summary>吸附动画结束：此时宝石已缩小消失，才打开选卡弹窗。</summary>
    void OnCardOfferGemAttracted()
    {
        CardChoiceGemPickup gem = busyOfferGem;
        if (gem == null || gem.IsChoiceCompleted) return;
        if (CoreChoiceUI.Instance == null || CoreChoiceUI.Instance.IsDrafting)
        {
            busyOfferGem = null;
            gem.CancelCollection();
            return;
        }
        RunSession run = RunSession.Instance;
        if (run != null && run.IsBossMode)
        {
            busyOfferGem = null;
            gem.CancelCollection();
            return;
        }

        gem.MarkCollected();
        CoreChoiceUI.Instance.Show(
            onClosed: () => CompleteCardOfferGem(gem),
            doublePick: gem.DoublePick,
            keepPicks: gem.KeepPicks,
            waveIndex: gem.WaveIndex);

        // Pass v1 §2.6：首次正式选卡打开时，仅显示一次「罪印双刃」提示（复用 Tutorial 队列）。
        if (!reverseBDHintShown)
        {
            reverseBDHintShown = true;
            if (TutorialController.Instance != null)
                TutorialController.Instance.ShowPrompt("TUT-REVERSE-BD");
        }

        if (!CoreChoiceUI.Instance.IsDrafting)
        {
            // UI 未能打开（无候选卡等）：释放闸门并让宝石回到场上，不影响后续流程。
            busyOfferGem = null;
            gem.CancelCollection();
            return;
        }
        Debug.Log($"[CardManager] 拾取选卡宝石：source={gem.Source}, doublePick={gem.DoublePick}, wave={gem.WaveIndex}。");
    }

    void CompleteCardOfferGem(CardChoiceGemPickup gem)
    {
        activeOfferGems.Remove(gem);
        // 选完卡才释放互斥闸门，让下一颗宝石可以被拾取。
        if (busyOfferGem == gem) busyOfferGem = null;
        if (gem != null)
        {
            gem.CompleteChoice();
            Debug.Log("[CardManager] 选卡完成，拾取闸门已释放；剩余宝石=" + activeOfferGems.Count + "。");
        }
    }

    /// <summary>
    /// 清理场上尚未拾取的选卡宝石（如离开战斗场景时），避免跨场景残留。
    /// 正在展示选卡的那颗保持原样，其生命周期由选卡流程与场景销毁接管。
    /// </summary>
    public void ClearCardOfferGems()
    {
        if (activeOfferGems.Count == 0)
        {
            if (busyOfferGem != null && !busyOfferGem.IsCollected) busyOfferGem = null;
            return;
        }

        int removed = 0;
        for (int i = activeOfferGems.Count - 1; i >= 0; i--)
        {
            CardChoiceGemPickup gem = activeOfferGems[i];
            if (gem == null) { activeOfferGems.RemoveAt(i); continue; }
            if (gem.IsCollected) continue;      // 已进入选卡流程，交给流程自身收尾
            Destroy(gem.gameObject);
            activeOfferGems.RemoveAt(i);
            removed++;
        }

        if (busyOfferGem != null && !busyOfferGem.IsCollected) busyOfferGem = null;
        Debug.Log($"[CardManager] 已清理未拾取的选卡宝石 {removed} 颗；剩余=" + activeOfferGems.Count + "。");
    }

    public void ClearChoicePicksForPendingOffer()
    {
        currentPicks = new CardData[3];
        shownThisSession.Clear();
        rerollCounts.Clear();
        RunSession.Instance?.ChoicePicks.Clear();
    }

    /// <summary>从已解锁卡重建各 Sin Investment（§6：取得该 Sin 类型卡数量）。</summary>
    void RebuildInvestments()
    {
        investments.Clear();
        foreach (var id in unlockedEffects)
        {
            var data = FindCard(id);
            if (data == null || data.monsterType == SinType.None) continue;
            investments.TryGetValue(data.monsterType, out int v);
            investments[data.monsterType] = v + 1;
        }
    }

    /// <summary>
    /// 刷新 Known Type Set（§2/§3）：Pride 起始即进入（Starting Carrier）；
    /// 其余按波次解锁表累加至 completedWaveIndex（幂等，可重复调用）。
    /// </summary>
    public void RefreshKnownTypes(int completedWaveIndex)
    {
        knownTypes.Add(SinType.Pride);   // 固定 Starting Carrier（§2）
        for (int w = 0; w < WaveTypeUnlocks.Length && w <= completedWaveIndex; w++)
        {
            var unlocks = WaveTypeUnlocks[w];
            for (int i = 0; i < unlocks.Length; i++)
                knownTypes.Add(unlocks[i]);
        }
    }

    public bool TryGetGameplayEffect(string effectTag, out GameplayEffectDefinition definition)
    {
        if (gameplayTagCatalog != null && gameplayTagCatalog.TryGetEffect(effectTag, out definition)) return true;
        definition = null;
        return false;
    }

    /// <summary>
    /// 池枚举：跳过 null/无 effectId/已取得（所有卡只出现一次），按 effectId 去重（首个生效）。
    /// 抽卡/解锁查找/重roll 统一走这里。
    /// </summary>
    private List<CardData> EnumeratePool()
    {
        var result = new List<CardData>();
        var seen = new HashSet<string>();
        if (cardLibrary == null || cardLibrary.cards == null) return result;
        foreach (var card in cardLibrary.cards)
        {
            if (card == null || !cardLibrary.IsEffectEnabled(card.effectId)) continue;
            if (unlockedEffects.Contains(card.effectId)) continue;   // 已取得即剔除（所有卡只出现一次）
            if (!seen.Add(card.effectId)) continue;                  // pool-level dedupe
            result.Add(card);
        }
        return result;
    }

    /// <summary>
    /// 抽取候选卡（三槽结构，需求：Encounter_CardOffer_Baseline §9/§10/§11）：
    ///   Slot A=Horizontal（BasicUniversal+GlobalSlot，Global 软保底加权）→ 空则 Flex 池补
    ///   Slot B=Monster Type（Known Type 限定的 MonsterType+TypeGrowth，Investment 加权）→ 空则放宽 Known Type → 仍空 Flex
    ///   Slot C=Flex（全合法池，排除本次已出现 ID）→ 空则任一未用池补
    ///   三槽结果互不重复（§10.5）；最终不足 count 个唯一合法 ID 时允许少于 count（不造假选项）。
    /// </summary>
    /// <param name="count">候选数量（对应三槽：A/B/C）。</param>
    /// <param name="keepSession">true=保留本次弹窗会话已出现记录（双选第二轮用，避免重现第一轮候选）；false=新会话清空。</param>
    public void DrawCards(int count = 3, bool keepSession = false)
    {
        rerollCounts.Clear();                                // 新会话每张卡重置刷新次数
        if (!keepSession) shownThisSession.Clear();          // 新会话才清空；双选第二轮保留

        // Known Type Set 随波次刷新（幂等；补弹路径走 RestoreChoicePicks 不经过这里，由 Awake 恢复推导保证一致）
        if (currentWaveCardSeed >= 0) RefreshKnownTypes(currentWaveCardSeed);

        // 会话级流：新会话才重建（双选第二轮 keepSession=true 沿用第一轮已推进的流，保持序列连续）
        if (!keepSession || sessionCardRng == null)
            sessionCardRng = CardRandom();
        var rng = sessionCardRng;
        var offered = new HashSet<string>();                 // 本次 Offer 已占用的 effectId（§10.5 去重）

        // ── 槽位合法池（已排除已取得的卡）──
        var poolA = new List<CardData>();                    // Slot A：Horizontal
        var poolB = new List<CardData>();                    // Slot B：Monster Type（Known Type 限定）
        var poolC = new List<CardData>();                    // Slot C：Flex（全合法）
        foreach (var card in EnumeratePool())
        {
            if (card.category == CardCategory.BasicUniversal || card.category == CardCategory.GlobalSlot)
                poolA.Add(card);
            if ((card.category == CardCategory.MonsterType || card.category == CardCategory.TypeGrowth)
                && knownTypes.Contains(card.monsterType))
                poolB.Add(card);
            poolC.Add(card);
        }

        // ── Slot A：Global 软保底加权（§11）──
        CardData pickA = WeightedPick(poolA, rng, card =>
            card.category == CardCategory.GlobalSlot ? GlobalPityWeight() : 1f, offered);

        // ── Slot B：Focus Assist（§2.5）优先，否则 Investment 轻度加权（§9/§6）──
        CardData pickB = null;
        SinType focusSin = ResolveFocusAssistSin();
        if (focusSin != SinType.None && focusAssistProbability > 0f && rng.NextDouble() < focusAssistProbability)
            pickB = WeightedPick(poolB, rng, card => card != null && card.monsterType == focusSin ? 1f : 0f, offered);
        if (pickB == null)
            pickB = WeightedPick(poolB, rng, InvestmentWeight, offered);

        // ── Slot C：Flex（排除本次已出现 ID，§9）──
        CardData pickC = WeightedPick(poolC, rng, card => 1f, offered);

        // ── Fallback（§10）──
        if (pickA == null) pickA = WeightedPick(poolC, rng, card => 1f, offered);   // Horizontal 空→Flex 池
        if (pickB == null)
        {
            // Monster-Type 空→先放宽 Known Type 限制（其他合法 Type 卡）→ 仍空则 Flex 池
            var relaxedB = new List<CardData>();
            foreach (var card in EnumeratePool())
                if (card.category == CardCategory.MonsterType || card.category == CardCategory.TypeGrowth)
                    relaxedB.Add(card);
            pickB = WeightedPick(relaxedB, rng, InvestmentWeight, offered)
                    ?? WeightedPick(poolC, rng, card => 1f, offered);
        }
        if (pickC == null) pickC = WeightedPick(poolA, rng, card => 1f, offered)      // Flex 空→任一未用池
                                   ?? WeightedPick(poolB, rng, card => 1f, offered);

        currentPicks = new CardData[count];
        currentPicks[0] = pickA;
        currentPicks[1] = pickB;
        currentPicks[2] = pickC;
        for (int i = 0; i < currentPicks.Length; i++)
            if (currentPicks[i] != null) shownThisSession.Add(currentPicks[i].effectId);

        // ── Global 软保底更新（§11）：本次 Offer 出现 Global 即重置，否则 streak+1 ──
        bool anyGlobal = (pickA != null && pickA.category == CardCategory.GlobalSlot)
                      || (pickB != null && pickB.category == CardCategory.GlobalSlot)
                      || (pickC != null && pickC.category == CardCategory.GlobalSlot);
        globalMissStreak = anyGlobal ? 0 : globalMissStreak + 1;
        if (RunSession.Instance != null)
            RunSession.Instance.GlobalMissStreak = globalMissStreak;   // 同步会话（存档点落盘）

        SyncChoicePicksToSession(); // 候选变化即同步会话（选卡界面任意时刻退出，补弹候选都一致）
        OnCardOffered?.Invoke();    // 叙事事件：候选落定广播（§3.2 CardOffered）
    }

    /// <summary>Global 软保底权重（§11）：连续 kGlobalPityStart 次 Miss 后，每次 +kGlobalPityStep。</summary>
    float GlobalPityWeight()
    {
        if (globalMissStreak < kGlobalPityStart) return 1f;
        return 1f + kGlobalPityStep * (globalMissStreak - kGlobalPityStart + 1);
    }

    /// <summary>Slot B 权重（§6）：每 1 Investment +0.30 乘数，上限 +1.50（kInvestmentCap 层）。</summary>
    float InvestmentWeight(CardData card)
    {
        if (card == null || card.monsterType == SinType.None) return 1f;
        investments.TryGetValue(card.monsterType, out int inv);
        return 1f + kInvestmentStep * Mathf.Min(inv, kInvestmentCap);
    }

    /// <summary>
    /// Focus Assist（§2.5）：已有明确最高 Investment 的 Sin 时返回该 Sin；
    /// 否则返回当前 Possessed Body 对应 Sin（无附身则 SinType.None）。不写死 Pride。
    /// </summary>
    SinType ResolveFocusAssistSin()
    {
        SinType best = SinType.None;
        int bestInv = 0;
        foreach (var kv in investments)
        {
            if (kv.Value > bestInv) { bestInv = kv.Value; best = kv.Key; }
        }
        if (best != SinType.None) return best;

        PossessionManager pm = PossessionManager.Instance;
        if (pm != null && pm.CurrentBody != null && pm.CurrentBody.sinType != SinType.None)
            return pm.CurrentBody.sinType;
        return SinType.None;
    }

    /// <summary>
    /// 加权抽取（种子流）：weight(card) 为权重（返回 0 的卡不参与）；
    /// offered 中的 effectId 被排除（§10.5 同 Offer 不重复）。池空/总权重<=0 返回 null。
    /// </summary>
    CardData WeightedPick(List<CardData> pool, System.Random rng, System.Func<CardData, float> weight, HashSet<string> offered)
    {
        float total = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            var card = pool[i];
            if (card == null || (offered != null && offered.Contains(card.effectId))) continue;
            total += Mathf.Max(0f, weight(card));
        }
        if (total <= 0f) return null;

        float roll = (float)rng.NextDouble() * total;
        for (int i = 0; i < pool.Count; i++)
        {
            var card = pool[i];
            if (card == null || (offered != null && offered.Contains(card.effectId))) continue;
            roll -= Mathf.Max(0f, weight(card));
            if (roll <= 0f)
            {
                offered.Add(card.effectId);   // 占用本次 Offer 的 ID
                return card;
            }
        }
        return null;   // 浮点误差兜底
    }

    /// <summary>
    /// 候选卡同步到对局会话（RunSession.ChoicePicks）：
    /// 每次抽卡/重抽/恢复后调用，保证"选卡界面退出 → 继续"补弹的候选与退出时一致。
    /// 存档（SaveProgress）读取的是会话快照，而非采样瞬间的 currentPicks。
    /// </summary>
    void SyncChoicePicksToSession()
    {
        if (RunSession.Instance == null) return;
        RunSession.Instance.ChoicePicks.Clear();
        if (currentPicks == null) return;
        foreach (var c in currentPicks)
            if (c != null && !string.IsNullOrEmpty(c.effectId)) RunSession.Instance.ChoicePicks.Add(c.effectId);
    }

    /// <summary>
    /// 恢复选卡候选（读档补弹用）：直接把存档的候选卡还原到 currentPicks，
    /// 保证"选卡界面退出 → 继续"后候选与退出时一致（随机由存档决定，不重新抽）。
    /// </summary>
    public void RestoreChoicePicks(List<string> effectIds)
    {
        if (effectIds == null || effectIds.Count == 0) return;
        rerollCounts.Clear();                                // 读档补弹：每张卡重置刷新次数
        shownThisSession.Clear();
        var pool = new List<CardData>();
        foreach (var c in EnumeratePool())
            pool.Add(c);

        currentPicks = new CardData[Mathf.Max(effectIds.Count, currentPicks != null ? currentPicks.Length : 3)];
        for (int i = 0; i < effectIds.Count; i++)
        {
            var id = effectIds[i];
            var found = pool.Find(c => c != null && c.effectId == id);
            currentPicks[i] = found;
            if (found != null) shownThisSession.Add(found.effectId);
        }
        SyncChoicePicksToSession();
        Debug.Log($"[CardManager] 恢复选卡候选 {effectIds.Count} 张（与退出时一致）。");
    }

    /// <summary>
    /// 刷新候选池：EnumeratePool 排除本会话已出现的卡。
    /// 供 HasRerollCandidates（前置检查）与 DrawOneReroll（实际抽取）共用。
    /// </summary>
    private List<CardData> GetRerollCandidates()
    {
        var available = new List<CardData>();
        foreach (var card in EnumeratePool())
            if (!shownThisSession.Contains(card.effectId))   // session exclusion
                available.Add(card);
        return available;
    }

    /// <summary>该槽位刷新次数是否未达上限（maxRerollsPerCard <= 0 视为不限）。</summary>
    bool CanRerollSlot(int slotIndex)
    {
        if (maxRerollsPerCard <= 0) return true;             // 0 = 不限次数
        int used = rerollCounts.TryGetValue(slotIndex, out var c) ? c : 0;
        return used < maxRerollsPerCard;
    }

    /// <summary>该槽位是否还可刷新（刷新次数未达 maxRerollsPerCard 且有候选；0 = 不限次数）。</summary>
    public bool HasRerollCandidates(int slotIndex = -1)
    {
        if (slotIndex >= 0 && !CanRerollSlot(slotIndex)) return false;
        return GetRerollCandidates().Count > 0;
    }

    /// <summary>
    /// Reroll slotIndex 槽位：返回新卡并更新 currentPicks[slotIndex]（会话排除已出现卡）。
    /// </summary>
    public CardData DrawOneReroll(int slotIndex)
    {
        // 刷新次数限制：该槽位已刷满 maxRerollsPerCard 次则拒绝（0 = 不限）
        if (slotIndex >= 0 && !CanRerollSlot(slotIndex)) return null;

        var available = GetRerollCandidates();
        if (available.Count == 0) return null;               // 无候选不扣次数

        if (slotIndex >= 0)
        {
            rerollCounts.TryGetValue(slotIndex, out int used);
            rerollCounts[slotIndex] = used + 1;
        }

        // 复用会话流持续消费（勿新建流：同 salt 新建流会回到序列首值，同 Count 槽位必取同索引）
        var rng = sessionCardRng ?? CardRandom();
        sessionCardRng = rng;
        var picked = available[rng.Next(0, available.Count)];
        shownThisSession.Add(picked.effectId);
        if (currentPicks != null && slotIndex >= 0 && slotIndex < currentPicks.Length)
            currentPicks[slotIndex] = picked;
        SyncChoicePicksToSession(); // 重抽后同步（退出时补弹候选含重抽结果）
        OnCardRerolled?.Invoke();    // 图鉴采集：重抽后候选变化，标记已知
        return picked;
    }

    /// <summary>Apply all previously unlocked effects to a newly spawned GameObject.</summary>
    public void ApplyAllUnlocksTo(GameObject go)
    {
        if (go == null) return;
        // This is also the repair path for objects spawned before CardManager.Start.
        // It makes Boss mode independent of Unity's cross-object initialization order.
        if (RunSession.Instance != null && RunSession.Instance.IsBossMode)
            UnlockAllEffectsForBossMode();

        if (unlockedEffects.Count == 0) return;
        var abilities = go.GetComponentsInChildren<EnemyAbility>(true);
        int applied = 0;
        foreach (var a in abilities)
        {
            if (a == null) continue;
            foreach (var effectId in unlockedEffects)
            {
                CardData data = FindCard(effectId);
                if (data == null) continue;
                if (!DoesCardTargetAbility(data, a)) continue;

                UnlockOnAbility(a, effectId);
                ApplyCardEffectTags(a, data);
                applied++;
            }
        }
        if (applied > 0)
            Debug.Log($"[CardManager] ApplyAllUnlocksTo {go.name}: applied {applied} unlocks, unlockedEffects={unlockedEffects.Count}");
    }

    /// <summary>Select a card by index (0-2). Unlocks its effect permanently this run.</summary>
    public void SelectCard(int index)
    {
        if (index < 0 || index >= currentPicks.Length) return;
        var card = currentPicks[index];
        if (card == null) return;

        Debug.Log($"[CardManager] Selected: {card.cardName} (effectId={card.effectId})");
        UnlockEffect(card.effectId);
    }

    /// <summary>
    /// Debug：把指定槽位候选替换为卡库中任意一张卡（卡面浏览器用，想看哪张卡直接看哪张）。
    /// 同步会话排除与 ChoicePicks（读档补弹候选一致）；不触发解锁（选择仍走 SelectCard）。
    /// </summary>
    public void DebugReplacePick(int slotIndex, CardData card)
    {
        if (card == null || currentPicks == null || slotIndex < 0 || slotIndex >= currentPicks.Length) return;
        currentPicks[slotIndex] = card;
        if (!string.IsNullOrEmpty(card.effectId)) shownThisSession.Add(card.effectId);
        SyncChoicePicksToSession();
        Debug.Log($"[CardManager] Debug 替换候选槽位 {slotIndex} → {card.cardName} ({card.effectId})");
    }

    /// <summary>
    /// Permanently unlock an effect for this run (same path as selecting a card).
    /// 所有卡都只会出现一次：取得后从池中剔除（EnumeratePool 排除）。
    /// </summary>
    public void UnlockEffect(string effectId)
    {
        if (string.IsNullOrEmpty(effectId) || unlockedEffects.Contains(effectId)) return;
        CardData data = FindCard(effectId);
        if (data == null) return;

        unlockedEffects.Add(effectId);
        // 同步到对局会话：选卡后存档点（SaveProgress）会把新卡一并落盘
        if (RunSession.Instance != null && !RunSession.Instance.UnlockedEffects.Contains(effectId))
            RunSession.Instance.UnlockedEffects.Add(effectId);
        RecordInvestment(data);

        // Unlock on all existing instances NOW
        var allAbilities = FindObjectsOfType<EnemyAbility>(true);
        int count = 0;
        foreach (var a in allAbilities)
        {
            if (!DoesCardTargetAbility(data, a)) continue;

            UnlockOnAbility(a, effectId);
            ApplyCardEffectTags(a, data);
            count++;
        }
        Debug.Log($"[CardManager] Unlock '{effectId}': {count} existing instances");
        OnEffectUnlocked?.Invoke(data);   // Run Analytics：解锁广播（采集器统计 Card 投资）
    }

    /// <summary>
    /// 清空一个怪物实例上的运行时卡牌解锁状态。
    /// 只复位 upgrade.unlocked，不删除能力槽位，避免破坏各技能 OnEnable 注册的基础槽位。
    /// </summary>
    public int ResetAbilityUnlockState(GameObject go)
    {
        if (go == null) return 0;
        int cleared = 0;
        var abilities = go.GetComponentsInChildren<EnemyAbility>(true);
        foreach (var ability in abilities)
        {
            if (ability == null) continue;
            ability.ResetRuntimeCardEffects();
            if (ability.upgrades == null) continue;
            foreach (var slot in ability.upgrades)
            {
                if (slot != null && slot.unlocked)
                {
                    slot.unlocked = false;
                    cleared++;
                }
            }
        }
        return cleared;
    }

    /// <summary>
    /// 清空本局所有卡牌解锁，并把场上所有怪物技能的每一个 upgrade 全部取消勾选。
    /// 供结束对局/退出游戏时调用，确保下一局从零开始（卡牌与技能构筑不跨局残留）。
    /// </summary>
    public void ResetAllUnlocks()
    {
        unlockedEffects.Clear();
        investments.Clear();
        knownTypes.Clear();
        globalMissStreak = 0;
        bossModeBuildsInitialized = false;
        currentPicks = new CardData[3];
        shownThisSession.Clear();
        rerollCounts.Clear();
        sessionCardRng = null;
        currentWaveCardSeed = -1;
        reverseBDHintShown = false;
        starterGemSpawned = false;

        var abilities = FindObjectsOfType<EnemyAbility>(true);
        int cleared = 0;
        foreach (var ability in abilities)
            cleared += ResetAbilityUnlockState(ability != null ? ability.gameObject : null);

        Debug.Log($"[CardManager] 已重置本局卡牌解锁与 {cleared} 个怪物技能 upgrade。");
    }

    /// <summary>Investment 累计（§6）：取得该 Sin 的 Monster-Type / Type Growth 卡时 +1。</summary>
    void RecordInvestment(CardData data)
    {
        if (data == null || data.monsterType == SinType.None) return;
        investments.TryGetValue(data.monsterType, out int v);
        investments[data.monsterType] = v + 1;
    }

    public CardData FindCard(string effectId)
    {
        if (cardLibrary == null || cardLibrary.cards == null) return null;
        foreach (var card in cardLibrary.cards)
            if (card != null && card.effectId == effectId) return card;
        return null;
    }

    /// <summary>
    /// CardLibrary order preserved. Returns cards that target any of the given abilities.
    /// Used by debug cheats to map number keys onto build entries.
    /// </summary>
    public List<CardData> GetCardsTargetingAbilities(IEnumerable<EnemyAbility> abilities)
    {
        var result = new List<CardData>();
        if (abilities == null || cardLibrary == null || cardLibrary.cards == null) return result;

        var abilityList = new List<EnemyAbility>();
        foreach (EnemyAbility ability in abilities)
            if (ability != null) abilityList.Add(ability);
        if (abilityList.Count == 0) return result;

        var seen = new HashSet<string>();
        foreach (CardData card in cardLibrary.cards)
        {
            if (card == null || string.IsNullOrEmpty(card.effectId) || !seen.Add(card.effectId)) continue;
            for (int i = 0; i < abilityList.Count; i++)
            {
                if (!DoesCardTargetAbility(card, abilityList[i])) continue;
                result.Add(card);
                break;
            }
        }
        return result;
    }

    public static bool DoesCardTargetAbility(CardData data, EnemyAbility ability)
    {
        if (data == null || ability == null) return false;
        if (data.targetAbilityTags != null && data.targetAbilityTags.Count > 0)
            return ability.HasAllAbilityTags(data.targetAbilityTags);

        // 用能力类型匹配（可靠）：abilityName 会在子类 OnEnable 被覆盖（如 '剑气'），
        // 而 data.abilityPrefab 是 asset（abilityName 为序列化默认 'Ability'），
        // 字符串比较在运行时必然失败。类型匹配不受此影响，且精确区分怪物能力。
        return data.abilityPrefab != null && ability.GetType() == data.abilityPrefab.GetType();
    }

    private void ApplyCardEffectTags(EnemyAbility ability, CardData data)
    {
        if (ability == null || data == null) return;
        ability.AddAppliedEffectTags(data.grantedEffectTags);
    }

    void UnlockOnAbility(EnemyAbility a, string effectId)
    {
        if (a.upgrades == null) a.upgrades = new List<EnemyAbility.UpgradeSlot>();
        string runtimeEffectId = ResolveLegacyUpgradeId(effectId);
        foreach (var slot in a.upgrades)
        {
            if (slot != null && !string.IsNullOrEmpty(slot.effectId) && slot.effectId.Equals(runtimeEffectId, System.StringComparison.OrdinalIgnoreCase))
            {
                slot.unlocked = true;
                Debug.Log($"[CardManager] UnlockOnAbility: set existing slot '{runtimeEffectId}' for card '{effectId}' on {a.name}, upgrades count={a.upgrades.Count}");
                return;
            }
        }
        a.upgrades.Add(new EnemyAbility.UpgradeSlot { effectId = runtimeEffectId, unlocked = true });
        Debug.Log($"[CardManager] UnlockOnAbility: added new slot '{runtimeEffectId}' for card '{effectId}' on {a.name}, upgrades count={a.upgrades.Count}");
    }

    // Ability slots are stored with Canonical Card IDs. Keeping the value unchanged
    // ensures save data, CardLibrary, prefabs, normal monsters, and Elite snapshots
    // all resolve the same upgrade identifier.
    static string ResolveLegacyUpgradeId(string effectId)
    {
        return effectId;
    }


    /// <summary>Check if an effect has been unlocked.</summary>
    public bool IsEffectUnlocked(string effectId)
    {
        if (RunSession.Instance != null && RunSession.Instance.IsBossMode)
            UnlockAllEffectsForBossMode();
        return !string.IsNullOrEmpty(effectId) && unlockedEffects.Contains(effectId);
    }

    public bool TryGetUnlockedAbilityParameter(EnemyAbility ability, string key, out float value)
    {
        value = 0f;
        if (ability == null || string.IsNullOrWhiteSpace(key) || cardLibrary == null || cardLibrary.cards == null) return false;
        foreach (CardData card in cardLibrary.cards)
        {
            if (card == null || !cardLibrary.IsEffectEnabled(card.effectId) || !IsEffectUnlocked(card.effectId) || !DoesCardTargetAbility(card, ability) || card.abilityParameters == null) continue;
            foreach (CardAbilityParameter parameter in card.abilityParameters)
            {
                if (parameter != null && string.Equals(parameter.key, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    value = parameter.value;
                    return true;
                }
            }
        }
        return false;
    }
}
