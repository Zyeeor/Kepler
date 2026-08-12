using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages upgrade cards. Stores a list of all possible cards, picks 3 random ones
/// when CoreChoiceUI triggers, and permanently unlocks the chosen effect.
/// </summary>
public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [Header("Card Pool")]
    [Tooltip("All possible upgrade cards. N are randomly picked each time.")]
    public List<CardData> allCards = new List<CardData>();
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

    /// <summary>Pick N random cards from the pool (no duplicates, excluding already-unlocked effects).</summary>
    public void DrawCards(int count = 3)
    {
        rerollsUsed = 0;
        selectsUsed = 0;
        var available = new List<CardData>();
        foreach (var card in allCards)
        {
            if (card != null && !string.IsNullOrEmpty(card.effectId) && !unlockedEffects.Contains(card.effectId))
                available.Add(card);
        }

        Shuffle(available);
        currentPicks = new CardData[count];
        for (int i = 0; i < count; i++)
        {
            if (i < available.Count)
                currentPicks[i] = available[i];
            else
                currentPicks[i] = null;
        }
    }

    /// <summary>Draw one random card from the remaining pool (excludes current picks and unlocked effects). Returns null if no rerolls left or no cards available.</summary>
    public CardData DrawOneReroll()
    {
        if (rerollsUsed >= maxRerolls) return null;
        rerollsUsed++;

        var available = new List<CardData>();
        foreach (var card in allCards)
        {
            if (card == null || string.IsNullOrEmpty(card.effectId)) continue;
            if (unlockedEffects.Contains(card.effectId)) continue;
            bool alreadyShown = false;
            if (currentPicks != null)
                foreach (var p in currentPicks)
                    if (p != null && p.effectId == card.effectId) { alreadyShown = true; break; }
            if (!alreadyShown) available.Add(card);
        }
        if (available.Count == 0) return null;
        return available[UnityEngine.Random.Range(0, available.Count)];
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
                CardData data = null;
                foreach (var card in allCards)
                    if (card != null && card.effectId == effectId) { data = card; break; }
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

    void UnlockEffect(string effectId)
    {
        if (string.IsNullOrEmpty(effectId) || unlockedEffects.Contains(effectId)) return;
        unlockedEffects.Add(effectId);

        CardData data = null;
        foreach (var card in allCards)
            if (card != null && card.effectId == effectId) { data = card; break; }
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

    private static bool DoesCardTargetAbility(CardData data, EnemyAbility ability)
    {
        if (data == null || ability == null) return false;
        if (data.targetAbilityTags != null && data.targetAbilityTags.Count > 0)
            return ability.HasAllAbilityTags(data.targetAbilityTags);

        return data.abilityPrefab != null && ability.abilityName == data.abilityPrefab.abilityName;
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
        if (ability == null || string.IsNullOrWhiteSpace(key)) return false;
        foreach (CardData card in allCards)
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
