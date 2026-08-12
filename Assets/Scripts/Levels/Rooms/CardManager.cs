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
    [Tooltip("Maximum total rerolls allowed across all cards per CoreChoiceUI session.")]
    public int maxRerolls = 3;

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
    private int rerollsUsed;
    private int selectsUsed;

    void Awake()
    {
        Instance = this;
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
        rerollsUsed = 0;
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

    /// <summary>是否还有可刷新的候选卡（不扣次数、不改状态；供 UI 刷新前检查）。</summary>
    public bool HasRerollCandidates()
    {
        if (rerollsUsed >= maxRerolls) return false;
        return GetRerollCandidates().Count > 0;
    }

    /// <summary>
    /// Reroll slotIndex 槽位：返回新卡并更新 currentPicks[slotIndex]（会话排除已出现卡）。
    /// </summary>
    public CardData DrawOneReroll(int slotIndex)
    {
        if (rerollsUsed >= maxRerolls) return null;

        var available = GetRerollCandidates();
        if (available.Count == 0) return null;               // 无候选不扣次数

        rerollsUsed++;

        var picked = available[UnityEngine.Random.Range(0, available.Count)];
        shownThisSession.Add(picked.effectId);
        if (currentPicks != null && slotIndex >= 0 && slotIndex < currentPicks.Length)
            currentPicks[slotIndex] = picked;
        return picked;
    }

    /// <summary>How many rerolls remain this session.</summary>
    public int RerollsRemaining => Mathf.Max(0, maxRerolls - rerollsUsed);
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
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }
}
