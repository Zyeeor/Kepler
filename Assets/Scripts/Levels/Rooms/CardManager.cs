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

    [Header("Debug")]
    [Tooltip("调试：勾选后开局自动弹出双选测试（等待选卡 UI 就绪后触发，最迟 10 秒）。仅测试用，与正常波次流程互斥——调试弹窗期间游戏暂停，关闭后波次流程继续。")]
    public bool debugDoublePickOnStart = false;

    [Header("Current Picks (read-only)")]
    public CardData[] currentPicks = new CardData[3];

    // Track which effects have been permanently unlocked this run（已取得的卡；本局不再出现——最新需求：所有卡都只会出现一次）
    private HashSet<string> unlockedEffects = new HashSet<string>();
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
    }

    void Start()
    {
        if (debugDoublePickOnStart)
            StartCoroutine(DebugTriggerDoublePickOnStart());
    }

    /// <summary>
    /// 调试：开局自动弹双选。等 CoreChoiceUI 就绪（场景加载后 Awake 注册单例）后触发，
    /// waveIndex=0 固定种子。弹窗期间游戏暂停，关闭后正常波次流程继续（WaveManager 首次弹卡
    /// 会因 CoreChoiceUI._isDrafting 已复位而正常工作）。仅测试用。
    /// </summary>
    IEnumerator DebugTriggerDoublePickOnStart()
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (CoreChoiceUI.Instance == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        if (CoreChoiceUI.Instance == null)
        {
            Debug.LogWarning("[CardManager] debugDoublePickOnStart：等待选卡 UI 超时（10 秒），跳过调试双选。");
            yield break;
        }
        CoreChoiceUI.Instance.Show(onClosed: null, doublePick: true, keepPicks: false, waveIndex: 0);
        Debug.Log("[CardManager] debugDoublePickOnStart：开局双选已触发。");
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

        // ── Slot B：Investment 轻度加权（§9/§6）──
        CardData pickB = WeightedPick(poolB, rng, InvestmentWeight, offered);

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
        return picked;
    }

    /// <summary>Apply all previously unlocked effects to a newly spawned GameObject.</summary>
    public void ApplyAllUnlocksTo(GameObject go)
    {
        if (go == null || unlockedEffects.Count == 0) return;
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
        foreach (var slot in a.upgrades)
        {
            if (slot != null && !string.IsNullOrEmpty(slot.effectId) && slot.effectId.Equals(effectId, System.StringComparison.OrdinalIgnoreCase))
            {
                slot.unlocked = true;
                Debug.Log($"[CardManager] UnlockOnAbility: set existing slot '{effectId}' on {a.name}, upgrades count={a.upgrades.Count}");
                return;
            }
        }
        a.upgrades.Add(new EnemyAbility.UpgradeSlot { effectId = effectId, unlocked = true });
        Debug.Log($"[CardManager] UnlockOnAbility: added new slot '{effectId}' on {a.name}, upgrades count={a.upgrades.Count}");
    }

    /// <summary>Check if an effect has been unlocked.</summary>
    public bool IsEffectUnlocked(string effectId)
    {
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
