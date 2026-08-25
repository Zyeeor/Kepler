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
    private readonly List<SpeedMultiplier> externalSpeedMultipliers = new List<SpeedMultiplier>();
    private Actor actor;

    private float EffectTime
    {
        get { return actor != null && actor.IsPlayerControlled ? Time.unscaledTime : Time.time; }
    }

    private void Awake()
    {
        actor = GetComponent<Actor>();
    }

    private struct SpeedMultiplier
    {
        public object source;
        public float multiplier;
    }

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
        public float damagePerStackOverride = -1f;
        public float periodicIntervalOverride = -1f;
        public GameplayEffectStackPolicy? stackPolicyOverride;
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
        return ApplyEffect(definition, source, sourceTags, out reason, -1f, -1);
    }

    /// <param name="durationOverride">Seconds; &lt;= 0 keeps definition.duration.</param>
    /// <param name="maxStacksOverride">When &gt; 0, clamps stacks using this instead of definition.maxStacks.</param>
    public bool ApplyEffect(
        GameplayEffectDefinition definition,
        CombatAbilityComponent source,
        IEnumerable<string> sourceTags,
        out string reason,
        float durationOverride,
        int maxStacksOverride)
    {
        return ApplyEffect(definition, source, sourceTags, out reason, durationOverride, maxStacksOverride, -1f, -1f, null);
    }

    public bool ApplyEffect(
        GameplayEffectDefinition definition,
        CombatAbilityComponent source,
        IEnumerable<string> sourceTags,
        out string reason,
        float durationOverride,
        int maxStacksOverride,
        float damagePerStackOverride,
        float periodicIntervalOverride,
        GameplayEffectStackPolicy? stackPolicyOverride)
    {
        if (!CanApplyEffect(definition, out reason))
        {
            if (definition != null) OnEffectRejected?.Invoke(definition, reason);
            return false;
        }

        int stackCap = maxStacksOverride > 0 ? maxStacksOverride : Mathf.Max(1, definition.maxStacks);
        GameplayEffectStackPolicy policy = stackPolicyOverride ?? definition.stackPolicy;
        ActiveEffect active = FindActiveEffect(definition);
        if (active == null)
        {
            active = CreateEffectInstance(definition, null, durationOverride, periodicIntervalOverride);
            active.damagePerStackOverride = damagePerStackOverride;
            active.periodicIntervalOverride = periodicIntervalOverride;
            active.stackPolicyOverride = stackPolicyOverride;
        }
        else
        {
            if (policy == GameplayEffectStackPolicy.Replace)
            {
                RemoveEffectInstance(active);
                active = CreateEffectInstance(definition, null, durationOverride, periodicIntervalOverride);
                active.damagePerStackOverride = damagePerStackOverride;
                active.periodicIntervalOverride = periodicIntervalOverride;
                active.stackPolicyOverride = stackPolicyOverride;
            }
            else
            {
                if (policy == GameplayEffectStackPolicy.AddStack)
                    active.stacks = Mathf.Min(active.stacks + 1, stackCap);

                active.expiresAt = GetExpiry(definition, durationOverride);
                if (damagePerStackOverride > 0f) active.damagePerStackOverride = damagePerStackOverride;
                if (periodicIntervalOverride > 0f) active.periodicIntervalOverride = periodicIntervalOverride;
                if (stackPolicyOverride.HasValue) active.stackPolicyOverride = stackPolicyOverride;
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

    /// <summary>Remove all active effects whose identity tag matches (case-insensitive).</summary>
    public void RemoveEffectsWithTag(string effectTag)
    {
        string normalized = GameplayTagUtility.Normalize(effectTag);
        if (string.IsNullOrEmpty(normalized)) return;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = activeEffects[i];
            if (effect == null || effect.definition == null) continue;
            if (!string.Equals(effect.definition.effectTag, normalized, System.StringComparison.OrdinalIgnoreCase))
                continue;
            RemoveEffectInstance(effect);
        }
    }

    public bool TryGetEffectStacks(GameplayEffectDefinition definition, out int stacks)
    {
        ActiveEffect active = FindActiveEffect(definition);
        if (active == null)
        {
            stacks = 0;
            return false;
        }
        stacks = active.stacks;
        return true;
    }

    /// <summary>
    /// Extends an already-active effect's expiry without changing stacks or re-firing apply events.
    /// Returns false if the effect is not currently active.
    /// </summary>
    public bool RefreshEffectDuration(GameplayEffectDefinition definition, float durationOverride = -1f)
    {
        ActiveEffect active = FindActiveEffect(definition);
        if (active == null) return false;
        active.expiresAt = GetExpiry(definition, durationOverride);
        return true;
    }

    public void RegisterEffectVfx(GameplayEffectDefinition definition, GameObject instance)
    {
        ActiveEffect effect = FindActiveEffect(definition);
        if (effect != null && instance != null) effect.attachedVfx.Add(instance);
    }

    public void AddMoveSpeedMultiplier(object source, float multiplier)
    {
        if (source == null) return;
        for (int i = 0; i < externalSpeedMultipliers.Count; i++)
        {
            if (ReferenceEquals(externalSpeedMultipliers[i].source, source))
            {
                var entry = externalSpeedMultipliers[i];
                entry.multiplier = multiplier;
                externalSpeedMultipliers[i] = entry;
                return;
            }
        }
        externalSpeedMultipliers.Add(new SpeedMultiplier { source = source, multiplier = multiplier });
    }

    public void RemoveMoveSpeedMultiplier(object source)
    {
        if (source == null) return;
        for (int i = externalSpeedMultipliers.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(externalSpeedMultipliers[i].source, source))
                externalSpeedMultipliers.RemoveAt(i);
        }
    }

    public float ModifyMoveSpeed(float value)
    {
        float multiplier = 1f;
        for (int i = 0; i < activeEffects.Count; i++)
        {
            GameplayEffectDefinition def = activeEffects[i].definition;
            if (def == null || Mathf.Approximately(def.moveSpeedMultiplier, 1f) || def.moveSpeedMultiplier <= 0f)
                continue;
            multiplier *= def.moveSpeedMultiplier;
        }
        for (int i = 0; i < externalSpeedMultipliers.Count; i++)
            multiplier *= externalSpeedMultipliers[i].multiplier;
        return value * multiplier;
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
        if (actor == null) actor = GetComponent<Actor>();
        float now = EffectTime;
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect effect = activeEffects[i];
            if (effect.expiresAt < 0f || now < effect.expiresAt) continue;

            if (effect.ownerAbility != null) EndAbility(effect.ownerAbility);
            else RemoveEffectInstance(effect);
        }
        for (int i = 0; i < activeEffects.Count; i++)
        {
            ActiveEffect effect = activeEffects[i];
            float interval = effect.periodicIntervalOverride > 0f
                ? effect.periodicIntervalOverride
                : (effect.definition != null ? effect.definition.periodicInterval : 0f);
            if (effect.definition == null || interval <= 0f || now < effect.nextPeriodicAt) continue;
            effect.nextPeriodicAt = now + interval;
            OnEffectPeriodic?.Invoke(effect.definition, effect.stacks);
            ApplyPeriodicDamage(effect);
        }
    }

    private void ApplyPeriodicDamage(ActiveEffect effect)
    {
        if (effect == null || effect.definition == null)
            return;
        float perStack = effect.damagePerStackOverride > 0f ? effect.damagePerStackOverride : effect.definition.damagePerStack;
        if (perStack <= 0f)
            return;
        float amount = perStack * Mathf.Max(1, effect.stacks);
        MonsterActor monster = GetComponent<MonsterActor>();
        if (monster != null)
        {
            monster.TakeEnvironmentalDamage(amount);
            return;
        }
        PlayerHealth soul = GetComponent<PlayerHealth>();
        if (soul != null)
            soul.TakeDamage(amount);
    }

    private ActiveEffect CreateEffectInstance(GameplayEffectDefinition definition, Component ownerAbility, float durationOverride = -1f, float periodicIntervalOverride = -1f)
    {
        float interval = periodicIntervalOverride > 0f ? periodicIntervalOverride : definition.periodicInterval;
        // Terrain/hazard overrides request an immediate first tick; normal effects wait one interval.
        float firstPeriodicAt = -1f;
        if (interval > 0f)
            firstPeriodicAt = periodicIntervalOverride > 0f ? EffectTime : EffectTime + interval;
        var effect = new ActiveEffect
        {
            definition = definition,
            ownerAbility = ownerAbility,
            stacks = 1,
            expiresAt = GetExpiry(definition, durationOverride),
            nextPeriodicAt = firstPeriodicAt,
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
        if (effect.vfxInstance != null) VfxPool.ReleaseOrDestroy(effect.vfxInstance);
        foreach (GameObject instance in effect.attachedVfx)
            if (instance != null) VfxPool.ReleaseOrDestroy(instance);
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

        GameObject instance = VfxPool.Instance.Spawn(definition.activeVfxPrefab, transform.position, transform.rotation);
        if (definition.parentVfxToTarget) instance.transform.SetParent(transform, true);
        foreach (var ps in instance.GetComponentsInChildren<ParticleSystem>())
            ps.Play(true);
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

    private float GetExpiry(GameplayEffectDefinition definition, float durationOverride = -1f)
    {
        float duration = durationOverride > 0f ? durationOverride : definition.duration;
        return duration > 0f ? EffectTime + duration : -1f;
    }
}
