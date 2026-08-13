using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Actor-local combat coordinator: owns Tags, grants/removes timed Effects, and gates ability activation.
/// Damage stays on Abilities. Effects carry Tags, duration, stacks, and target VFX.
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

    [Header("Effect Immunity")]
    [Tooltip("Reject an incoming Effect when this actor owns a rule's required target tags and the Effect matches one of its blocked Effect tags.")]
    public List<GameplayEffectBlockRule> effectApplicationBlockRules = new List<GameplayEffectBlockRule>
    {
        new GameplayEffectBlockRule
        {
            requiredTargetTags = new List<string> { "State.Defense.SuperArmor" },
            blockedEffectTags = new List<string> { "Effect.Control" }
        }
    };

    public event Action<string> OnAbilityRejected;
    public event Action<GameplayEffectDefinition, int> OnEffectApplied;
    public event Action<GameplayEffectDefinition, string> OnEffectRejected;
    public event Action<GameplayEffectDefinition> OnEffectExpired;
    public event Action<GameplayEffectDefinition, int> OnEffectPeriodic;

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
        public ActiveEffect activationEffect;
    }

    private class ActiveEffect
    {
        public GameplayEffectDefinition definition;
        public Component ownerAbility;
        public int stacks;
        public float expiresAt;
        public float nextPeriodicAt;
        public GameObject vfxInstance;
        public List<GameObject> attachedVfx = new List<GameObject>();
    }

    public void AddLooseTags(object source, IEnumerable<string> grantedTags)
    {
        tags.AddTags(source, grantedTags);
    }

    public void RemoveLooseTags(object source)
    {
        tags.RemoveTags(source);
    }

    public bool CanActivate(Component ability, IList<string> requiredTags, out string reason)
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

        return true;
    }

    public bool TryBeginAbility(Component ability, IList<string> requiredTags, GameplayEffectDefinition activationEffect, IEnumerable<string> sourceTags)
    {
        if (ability == null) return false;

        string reason;
        if (!CanActivate(ability, requiredTags, out reason))
        {
            OnAbilityRejected?.Invoke(reason);
            return false;
        }

        if (FindActiveAbility(ability) != null) return true;
        if (activationEffect == null) return true;

        if (!CanApplyEffect(activationEffect, out reason))
        {
            OnAbilityRejected?.Invoke(reason);
            return false;
        }

        var entry = new ActiveAbility
        {
            source = ability,
            activationEffect = CreateEffectInstance(activationEffect, ability)
        };
        activeAbilities.Add(entry);
        return true;
    }

    public void EndAbility(Component ability)
    {
        ActiveAbility entry = FindActiveAbility(ability);
        if (entry == null) return;

        tags.RemoveTags(entry);
        if (entry.activationEffect != null) RemoveEffectInstance(entry.activationEffect);
        activeAbilities.Remove(entry);
    }

    public bool ApplyEffect(GameplayEffectDefinition definition)
    {
        string ignoredReason;
        return ApplyEffect(definition, null, null, out ignoredReason);
    }

    public bool ApplyEffectByTag(string effectTag, GameplayTagCatalog catalog, CombatAbilityComponent source, IEnumerable<string> sourceTags, out string reason)
    {
        if (catalog == null || !catalog.TryGetEffect(effectTag, out GameplayEffectDefinition definition))
        {
            reason = "Gameplay Effect Tag is not registered in the supplied catalog";
            return false;
        }

        return ApplyEffect(definition, source, sourceTags, out reason);
    }

    public bool ApplyEffect(GameplayEffectDefinition definition, CombatAbilityComponent source, IEnumerable<string> sourceTags, out string reason)
    {
        if (!CanApplyEffect(definition, out reason))
        {
            if (definition != null) OnEffectRejected?.Invoke(definition, reason);
            return false;
        }

        ActiveEffect active = FindActiveEffect(definition);
        if (active == null)
        {
            active = CreateEffectInstance(definition, null);
        }
        else
        {
            if (definition.stackPolicy == GameplayEffectStackPolicy.Replace)
            {
                RemoveEffectInstance(active);
                active = CreateEffectInstance(definition, null);
            }
            else
            {
                if (definition.stackPolicy == GameplayEffectStackPolicy.AddStack)
                    active.stacks = Mathf.Min(active.stacks + 1, Mathf.Max(1, definition.maxStacks));

                active.expiresAt = GetExpiry(definition);
            }
        }

        OnEffectApplied?.Invoke(definition, active.stacks);
        reason = string.Empty;
        return true;
    }

    public bool CanApplyEffect(GameplayEffectDefinition definition, out string reason)
    {
        reason = string.Empty;
        if (definition == null)
        {
            reason = "Missing Gameplay Effect Definition";
            return false;
        }

        if (IsEffectBlockedByTargetRule(definition))
        {
            reason = "Target immunity Gameplay Tag blocks this Effect";
            return false;
        }

        return true;
    }

    public void RemoveEffect(GameplayEffectDefinition definition)
    {
        ActiveEffect active = FindActiveEffect(definition);
        if (active == null) return;

        RemoveEffectInstance(active);
    }

    public void RegisterEffectVfx(GameplayEffectDefinition definition, GameObject instance)
    {
        ActiveEffect effect = FindActiveEffect(definition);
        if (effect != null && instance != null) effect.attachedVfx.Add(instance);
    }

    public float ModifyMoveSpeed(float value)
    {
        return value;
    }

    public float ModifyOutgoingDamage(float value)
    {
        return value;
    }

    public float ModifyIncomingDamage(float value)
    {
        return value;
    }

    private void Update()
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = activeEffects[i];
            if (effect.expiresAt < 0f || Time.time < effect.expiresAt) continue;

            if (effect.ownerAbility != null) EndAbility(effect.ownerAbility);
            else RemoveEffectInstance(effect);
        }
        for (int i = 0; i < activeEffects.Count; i++)
        {
            ActiveEffect effect = activeEffects[i];
            if (effect.definition == null || effect.definition.periodicInterval <= 0f || Time.time < effect.nextPeriodicAt) continue;
            effect.nextPeriodicAt = Time.time + effect.definition.periodicInterval;
            OnEffectPeriodic?.Invoke(effect.definition, effect.stacks);
        }
    }

    private ActiveEffect CreateEffectInstance(GameplayEffectDefinition definition, Component ownerAbility)
    {
        var effect = new ActiveEffect
        {
            definition = definition,
            ownerAbility = ownerAbility,
            stacks = 1,
            expiresAt = GetExpiry(definition),
            nextPeriodicAt = definition.periodicInterval > 0f ? Time.time + definition.periodicInterval : -1f,
            vfxInstance = SpawnEffectVfx(definition)
        };
        activeEffects.Add(effect);
        AddEffectTags(effect, definition);
        return effect;
    }

    private void RemoveEffectInstance(ActiveEffect effect)
    {
        if (effect == null) return;

        tags.RemoveTags(effect);
        if (effect.vfxInstance != null) Destroy(effect.vfxInstance);
        foreach (GameObject instance in effect.attachedVfx)
            if (instance != null) Destroy(instance);
        activeEffects.Remove(effect);
        OnEffectExpired?.Invoke(effect.definition);
    }

    private void AddEffectTags(ActiveEffect active, GameplayEffectDefinition definition)
    {
        if (active == null || definition == null) return;
        if (!string.IsNullOrEmpty(definition.effectTag)) tags.AddTags(active, new[] { definition.effectTag });
        tags.AddTags(active, definition.grantedTags);
    }

    private bool IsEffectBlockedByTargetRule(GameplayEffectDefinition definition)
    {
        if (definition == null || string.IsNullOrEmpty(definition.effectTag)) return false;

        foreach (GameplayEffectBlockRule rule in effectApplicationBlockRules)
        {
            if (rule == null || !tags.HasAll(rule.requiredTargetTags)) continue;
            if (GameplayTagUtility.HasAny(new[] { definition.effectTag }, rule.blockedEffectTags)) return true;
        }
        return false;
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
            ActiveEffect effect = activeEffects[i];
            if (effect.definition == definition && effect.ownerAbility == null) return effect;
        }
        return null;
    }

    private float GetExpiry(GameplayEffectDefinition definition)
    {
        return definition.duration > 0f ? Time.time + definition.duration : -1f;
    }
}
