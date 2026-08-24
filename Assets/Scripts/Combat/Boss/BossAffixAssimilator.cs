using System.Collections.Generic;
using UnityEngine;

/// <summary>Mirrors the current body's active Special affixes onto an existing Boss Special.</summary>
public sealed class BossAffixAssimilator : MonoBehaviour
{
    readonly HashSet<string> assimilatedEffectIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    BossSevenfoldActor boss;

    public IReadOnlyCollection<string> AssimilatedEffectIds => assimilatedEffectIds;

    void Awake() { boss = GetComponent<BossSevenfoldActor>(); }

    public int Assimilate(MonsterActor playerBody, EnemyAbility sourceSpecial = null)
    {
        if (boss == null || playerBody == null) return 0;
        if (sourceSpecial == null && playerBody.skillAbilities != null)
        {
            for (int i = 0; i < playerBody.skillAbilities.Count; i++)
            {
                if (playerBody.skillAbilities[i] != null && playerBody.skillAbilities[i].ability != null)
                {
                    sourceSpecial = playerBody.skillAbilities[i].ability;
                    break;
                }
            }
        }
        if (sourceSpecial == null) return 0;
        EnemyAbility destination = FindMatchingSpecial(sourceSpecial);
        if (destination == null) return 0;
        int added = 0;

        if (sourceSpecial.upgrades != null)
        {
            for (int i = 0; i < sourceSpecial.upgrades.Count; i++)
            {
                EnemyAbility.UpgradeSlot sourceSlot = sourceSpecial.upgrades[i];
                if (sourceSlot == null || !sourceSlot.unlocked || string.IsNullOrEmpty(sourceSlot.effectId)) continue;
                if (UnlockDestinationSlot(destination, sourceSlot.effectId)) added++;
            }
        }

        if (sourceSpecial.appliedEffectTags != null)
        {
            for (int i = 0; i < sourceSpecial.appliedEffectTags.Count; i++)
            {
                string effectTag = sourceSpecial.appliedEffectTags[i];
                if (string.IsNullOrEmpty(effectTag)) continue;
                destination.AddAppliedEffectTags(new[] { effectTag });
            }
        }

        CardManager cards = CardManager.Instance;
        if (cards != null)
        {
            foreach (string effectId in cards.UnlockedEffects)
            {
                CardData card = cards.FindCard(effectId);
                if (card == null || !CardManager.DoesCardTargetAbility(card, sourceSpecial)) continue;
                if (assimilatedEffectIds.Add(effectId)) added++;
                destination.AddAppliedEffectTags(card.grantedEffectTags);
            }
        }
        return added;
    }

    bool UnlockDestinationSlot(EnemyAbility destination, string effectId)
    {
        if (destination.upgrades == null) destination.upgrades = new List<EnemyAbility.UpgradeSlot>();
        for (int i = 0; i < destination.upgrades.Count; i++)
        {
            EnemyAbility.UpgradeSlot slot = destination.upgrades[i];
            if (slot == null || !string.Equals(slot.effectId, effectId, System.StringComparison.OrdinalIgnoreCase)) continue;
            slot.unlocked = true;
            return assimilatedEffectIds.Add(effectId);
        }
        destination.upgrades.Add(new EnemyAbility.UpgradeSlot { effectId = effectId, unlocked = true });
        return assimilatedEffectIds.Add(effectId);
    }

    EnemyAbility FindMatchingSpecial(EnemyAbility source)
    {
        EnemyAbility[] abilities = boss.GetComponentsInChildren<EnemyAbility>(true);
        for (int i = 0; i < abilities.Length; i++)
        {
            EnemyAbility candidate = abilities[i];
            if (candidate == null || candidate.type != EnemyAbility.AbilityType.Skill) continue;
            if (candidate.GetType() == source.GetType() || candidate.abilityName == source.abilityName) return candidate;
        }
        return null;
    }

    public void ClearAssimilation() { assimilatedEffectIds.Clear(); }
}
