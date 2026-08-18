using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages upgrade cards. Picks random cards from the CardLibrary pool, and permanently
/// unlocks the chosen effect. 重构（S1/S2，见 Upgrade_Choice_System_Refactor.md）：
///   - 卡池从场景内联 allCards 抽离为 CardLibrary SO 资产
///   - 抽卡去重（EnumeratePool 按 effectId）+ 会话排除（shownThisSession）
/// </summary>
public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [Header("Card Pool")]
    [Tooltip("卡池资产（SO）。运行时从此抽卡。")]
    public CardLibrary cardLibrary;
    [Tooltip("Effect Tag directory used to resolve CardData.grantedEffectTags at runtime.")]
    public GameplayTagCatalog gameplayTagCatalog;

    [Header("Reroll Limit")]
    [Tooltip("每张卡最多刷新 1 次（按槽位记：刷新过的卡禁再刷，其他卡不受影响）。")]
    public bool maxRerollsPerCard = true;

    [Header("Select Limit")]
    [Tooltip("Maximum cards that can be selected (confirmed) per CoreChoiceUI session.")]
    public int maxSelects = 1;

    [Header("Current Picks (read-only)")]
    public CardData[] currentPicks = new CardData[3];

    // Track which effects have been permanently unlocked this run
    private HashSet<string> unlockedEffects = new HashSet<string>();
    // Session exclusion: every card shown during the current popup session
    // (including ones rerolled away) never reappears until the next DrawCards.
    private readonly HashSet<string> shownThisSession = new HashSet<string>();
    private readonly HashSet<int> rerolledSlots = new HashSet<int>(); // 已刷新过的槽位（每卡最多 1 次）
    private int selectsUsed;

    // ── 卡牌随机流（种子确定性，经 SeedSystem 统一派生）──
    // 本局卡牌（初始三张 + 每次刷新/重抽）由会话种子派生，与全局 UnityEngine.Random 隔离：
    // 同一种子下（读档恢复同 worldSeed），整局卡牌序列完全可复现。
    // 每波用独立子种子：SeedSystem.CreateFlow(DomainCard, waveIndex)。
    private int currentWaveCardSeed = -1;

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

    void Awake()
    {
        // 单例防覆盖：GameManager 是 DontDestroyOnLoad 常驻对象，场景二次加载时
        // 新实例若先 Awake 会把 Instance 覆盖为"随后被销毁的新对象"（伪 null），
        // 导致选卡系统判定 Instance 为空而显示占位。已有实例时不覆盖。
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // 恢复已解锁卡：优先从对局会话（RunSession，主菜单[继续]已填充；新局为空集）；
        // 兼容旧路径：无会话时回退读档（EnumeratePool 自动排除已解锁）。
        var run = RunSession.Instance;
        if (run != null && run.HasActiveRun)
        {
            foreach (var id in run.UnlockedEffects)
                if (!string.IsNullOrEmpty(id)) unlockedEffects.Add(id);
            if (unlockedEffects.Count > 0)
                Debug.Log($"[CardManager] 会话恢复已解锁效果 {unlockedEffects.Count} 个。");
        }
        else
        {
            var resume = SaveCoordinator.ResumeData;
            if (resume != null && resume.unlockedEffects != null && resume.unlockedEffects.Count > 0)
            {
                foreach (var id in resume.unlockedEffects)
                    if (!string.IsNullOrEmpty(id)) unlockedEffects.Add(id);
                Debug.Log($"[CardManager] 读档恢复已解锁效果 {unlockedEffects.Count} 个。");
            }
        }
    }

    public bool TryGetGameplayEffect(string effectTag, out GameplayEffectDefinition definition)
    {
        if (gameplayTagCatalog != null && gameplayTagCatalog.TryGetEffect(effectTag, out definition)) return true;
        definition = null;
        return false;
    }

    /// <summary>
    /// 池枚举：跳过 null/无 effectId/已解锁，按 effectId 去重（首个生效）。
    /// 抽卡/解锁查找/重roll 统一走这里。
    /// </summary>
    private List<CardData> EnumeratePool()
    {
        var result = new List<CardData>();
        var seen = new HashSet<string>();
        if (cardLibrary == null || cardLibrary.cards == null) return result;
        foreach (var card in cardLibrary.cards)
        {
            if (card == null || string.IsNullOrEmpty(card.effectId)) continue;
            if (unlockedEffects.Contains(card.effectId)) continue;
            if (!seen.Add(card.effectId)) continue;          // pool-level dedupe
            result.Add(card);
        }
        return result;
    }

    /// <summary>
    /// 抽取 N 张候选卡（去重、排除已解锁）。
    /// </summary>
    /// <param name="count">候选数量。</param>
    /// <param name="keepSession">true=保留本次弹窗会话已出现记录（双选第二轮用，避免重现第一轮候选）；false=新会话清空。</param>
    public void DrawCards(int count = 3, bool keepSession = false)
    {
        rerolledSlots.Clear();                               // 新会话每张卡重置为可刷新 1 次
        selectsUsed = 0;
        if (!keepSession) shownThisSession.Clear();          // 新会话才清空；双选第二轮保留

        var available = EnumeratePool();
        Shuffle(available);
        currentPicks = new CardData[count];
        for (int i = 0; i < count; i++)
        {
            currentPicks[i] = i < available.Count ? available[i] : null;
            if (currentPicks[i] != null) shownThisSession.Add(currentPicks[i].effectId);
        }
        SyncChoicePicksToSession(); // 候选变化即同步会话（选卡界面任意时刻退出，补弹候选都一致）
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
        rerolledSlots.Clear();                               // 读档补弹：每张卡重置为可刷新 1 次
        selectsUsed = 0;
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

    /// <summary>该槽位是否还可刷新（每卡最多 1 次：未刷过且有候选）。</summary>
    public bool HasRerollCandidates(int slotIndex = -1)
    {
        if (maxRerollsPerCard && slotIndex >= 0 && rerolledSlots.Contains(slotIndex)) return false;
        return GetRerollCandidates().Count > 0;
    }

    /// <summary>
    /// Reroll slotIndex 槽位：返回新卡并更新 currentPicks[slotIndex]（会话排除已出现卡）。
    /// </summary>
    public CardData DrawOneReroll(int slotIndex)
    {
        // 每卡最多 1 次：该槽位已刷新过则拒绝
        if (maxRerollsPerCard && slotIndex >= 0 && rerolledSlots.Contains(slotIndex)) return null;

        var available = GetRerollCandidates();
        if (available.Count == 0) return null;               // 无候选不扣次数

        if (slotIndex >= 0) rerolledSlots.Add(slotIndex);

        var picked = available[CardRandom().Next(0, available.Count)];
        shownThisSession.Add(picked.effectId);
        if (currentPicks != null && slotIndex >= 0 && slotIndex < currentPicks.Length)
            currentPicks[slotIndex] = picked;
        SyncChoicePicksToSession(); // 重抽后同步（退出时补弹候选含重抽结果）
        return picked;
    }

    /// <summary>How many selects remain this session.</summary>
    public int SelectsRemaining => Mathf.Max(0, maxSelects - selectsUsed);

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
        if (selectsUsed >= maxSelects) return;
        if (index < 0 || index >= currentPicks.Length) return;
        var card = currentPicks[index];
        if (card == null) return;

        selectsUsed++;

        Debug.Log($"[CardManager] Selected: {card.cardName} (effectId={card.effectId})");
        UnlockEffect(card.effectId);
    }

    /// <summary>Permanently unlock an effect for this run (same path as selecting a card).</summary>
    public void UnlockEffect(string effectId)
    {
        if (string.IsNullOrEmpty(effectId) || unlockedEffects.Contains(effectId)) return;
        unlockedEffects.Add(effectId);
        // 同步到对局会话：选卡后存档点（SaveProgress）会把新卡一并落盘
        if (RunSession.Instance != null && !RunSession.Instance.UnlockedEffects.Contains(effectId))
            RunSession.Instance.UnlockedEffects.Add(effectId);

        CardData data = FindCard(effectId);
        if (data == null) return;

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
            if (card == null || !IsEffectUnlocked(card.effectId) || !DoesCardTargetAbility(card, ability) || card.abilityParameters == null) continue;
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

    void Shuffle<T>(List<T> list)
    {
        // 用卡牌局部随机流（种子确定），不用全局 UnityEngine.Random——保证同种子下整局可复现
        var rng = CardRandom();
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(0, i + 1);
            var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }
}
