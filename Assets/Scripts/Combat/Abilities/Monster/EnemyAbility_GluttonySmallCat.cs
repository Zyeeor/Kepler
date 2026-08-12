using System.Collections;
using UnityEngine;

/// <summary>Gluttony mobility: enter the small-cat form for a configurable duration.</summary>
public class EnemyAbility_GluttonySmallCat : EnemyAbility
{
    public float formDuration = 3f;
    public GameplayEffectDefinition smallCatEffect;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "小猫形态";
        cooldown = cooldown <= 0f ? 6f : cooldown;
    }

    protected override void OnTrigger()
    {
        StartCoroutine(FormRoutine());
    }

    private IEnumerator FormRoutine()
    {
        if (owner == null) yield break;
        if (smallCatEffect != null) owner.Combat.ApplyEffect(smallCatEffect, owner.Combat, abilityTags, out _);
        yield return AbilityWait(GetCardParameter("TransformDuration", formDuration));
        if (owner != null && smallCatEffect != null) owner.Combat.RemoveEffect(smallCatEffect);
        EndActivationEffect();
    }
}
