using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small actor-local combat coordinator inspired by GAS, intentionally limited for this project:
/// owns Tags, grants/removes timed Effects, gates ability activation, and exposes common combat modifiers.
/// It does not own input, animation graphs, replication, prediction, or a generic attribute framework.
/// </summary>
[DisallowMultipleComponent]
public class CombatAbilityComponent : MonoBehaviour
{
    [Header("Global Ability Gate")]
    [Tooltip("All abilities on this actor are denied while any of these tags is active. Ability-specific blocked tags are configured on each ability.")]
    public List<string> globalAbilityBlockedTags = new List<string> { "State.Control.Stunned" };

    [Header("Movement Gate")]
    [Tooltip("Movement is denied while any of these tags is active. Add State.Control.Rooted or State.Control.Stunned through an effect to immobilize an actor.")]
    public List<string> movementBlockedTags = new List<string>
    {
        "State.Control.Rooted",
        "State.Control.Stunned",
        "State.Action.Movement.Blocked"
    };

    public event Action<string> OnAbilityRejected;
    public event Action<GameplayEffectDefinition, int> OnEffectApplied;
    public event Action<GameplayEffectDefinition> OnEffectExpired;

    private readonly GameplayTagContainer tags = new GameplayTagContainer();
    private readonly List<ActiveAbility> activeAbilities = new List<ActiveAbility>();
    private readonly List<ActiveEffect> activeEffects = new List<ActiveEffect>();

    public GameplayTagContainer Tags
    {
        get { return tags; }
    }

    public bool CanMove
    {
        get { return !tags.HasAny(movementBlockedTags); }
    }

    private class ActiveAbility
    {
        public Component source;
        public Coroutine endRoutine;
    }

    private class ActiveEffect
    {
        public GameplayEffectDefinition definition;
        public int stacks;
        public float expiresAt;
        public GameObject vfxInstance;
    }

    public void AddLooseTags(object source, IEnumerable<string> grantedTags)
    {
        tags.AddTags(source, grantedTags);
    }

    public void RemoveLooseTags(object source)
    {
        tags.RemoveTags(source);
    }

    public bool CanActivate(Component ability, IList<string> requiredTags, IList<string> blockedTags, out string reason)
    {
        reason = string.Empty;
        if (ability != null && FindActiveAbility(ability) != null) return true;

        if (!tags.HasAll(requiredTags))
        {
            reason = "Missing required Gameplay Tag";
            return false;
        }

        if (tags.HasAny(globalAbilityBlockedTags))
        {
            reason = "Blocked by actor Gameplay Tag";
            return false;
        }

        if (tags.HasAny(blockedTags))
        {
            reason = "Blocked by ability Gameplay Tag";
            return false;
        }

        return true;
    }

    public bool TryBeginAbility(Component ability, IList<string> requiredTags, IList<string> blockedTags, IList<string> grantedTags, float activeDuration)
    {
        if (ability == null) return false;

        string reason;
        if (!CanActivate(ability, requiredTags, blockedTags, out reason))
        {
            OnAbilityRejected?.Invoke(reason);
            return false;
        }

        if (FindActiveAbility(ability) != null) return true;

        var entry = new ActiveAbility { source = ability };
        activeAbilities.Add(entry);
        tags.AddTags(entry, grantedTags);

        if (activeDuration > 0f)
            entry.endRoutine = StartCoroutine(EndAbilityAfterDuration(entry, activeDuration));

        return true;
    }

    public void EndAbility(Component ability)
    {
        ActiveAbility entry = FindActiveAbility(ability);
        if (entry == null) return;

        if (entry.endRoutine != null) StopCoroutine(entry.endRoutine);
        tags.RemoveTags(entry);
        activeAbilities.Remove(entry);
    }

    public void ApplyEffect(GameplayEffectDefinition definition)
    {
        if (definition == null) return;

        ActiveEffect active = FindActiveEffect(definition);
        if (active == null)
        {
            active = new ActiveEffect
            {
                definition = definition,
                stacks = 1,
                expiresAt = GetExpiry(definition),
                vfxInstance = SpawnEffectVfx(definition)
            };
            activeEffects.Add(active);
            tags.AddTags(active, definition.grantedTags);
        }
        else
        {
            if (definition.stackPolicy == GameplayEffectStackPolicy.Replace)
            {
                tags.RemoveTags(active);
                if (active.vfxInstance != null) Destroy(active.vfxInstance);
                activeEffects.Remove(active);
                active = new ActiveEffect
                {
                    definition = definition,
                    stacks = 1,
                    expiresAt = GetExpiry(definition),
                    vfxInstance = SpawnEffectVfx(definition)
                };
                activeEffects.Add(active);
                tags.AddTags(active, definition.grantedTags);
            }
            else
            {
                if (definition.stackPolicy == GameplayEffectStackPolicy.AddStack)
                    active.stacks = Mathf.Min(active.stacks + 1, Mathf.Max(1, definition.maxStacks));

                active.expiresAt = GetExpiry(definition);
            }
        }

        OnEffectApplied?.Invoke(definition, active.stacks);
    }

    public void RemoveEffect(GameplayEffectDefinition definition)
    {
        ActiveEffect active = FindActiveEffect(definition);
        if (active == null) return;

        tags.RemoveTags(active);
        if (active.vfxInstance != null) Destroy(active.vfxInstance);
        activeEffects.Remove(active);
        OnEffectExpired?.Invoke(definition);
    }

    public float ModifyMoveSpeed(float value)
    {
        return value * GetModifier(GameplayEffectModifierType.MoveSpeedMultiplier);
    }

    public float ModifyOutgoingDamage(float value)
    {
        return value * GetModifier(GameplayEffectModifierType.OutgoingDamageMultiplier);
    }

    public float ModifyIncomingDamage(float value)
    {
        return value * GetModifier(GameplayEffectModifierType.IncomingDamageMultiplier);
    }

    private void Update()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = activeEffects[i];
            if (effect.expiresAt < 0f || Time.time < effect.expiresAt) continue;

            tags.RemoveTags(effect);
            if (effect.vfxInstance != null) Destroy(effect.vfxInstance);
            activeEffects.RemoveAt(i);
            OnEffectExpired?.Invoke(effect.definition);
        }
    }

    private IEnumerator EndAbilityAfterDuration(ActiveAbility entry, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (entry != null && entry.source != null) EndAbility(entry.source);
    }

    private GameObject SpawnEffectVfx(GameplayEffectDefinition definition)
    {
        if (definition == null || definition.activeVfxPrefab == null) return null;

        GameObject instance = Instantiate(definition.activeVfxPrefab, transform.position, transform.rotation);
        if (definition.parentVfxToTarget) instance.transform.SetParent(transform, true);
        return instance;
    }

    private ActiveAbility FindActiveAbility(Component source)
    {
        for (int i = 0; i < activeAbilities.Count; i++)
        {
            if (activeAbilities[i].source == source) return activeAbilities[i];
        }
        return null;
    }

    private ActiveEffect FindActiveEffect(GameplayEffectDefinition definition)
    {
        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (activeEffects[i].definition == definition) return activeEffects[i];
        }
        return null;
    }

    private float GetExpiry(GameplayEffectDefinition definition)
    {
        return definition.duration > 0f ? Time.time + definition.duration : -1f;
    }

    private float GetModifier(GameplayEffectModifierType type)
    {
        float result = 1f;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            ActiveEffect effect = activeEffects[i];
            if (effect.definition == null || effect.definition.modifiers == null) continue;

            foreach (GameplayEffectModifier modifier in effect.definition.modifiers)
            {
                if (modifier == null || modifier.type != type) continue;
                result *= Mathf.Pow(Mathf.Max(0f, modifier.multiplier), effect.stacks);
            }
        }
        return result;
    }
}
