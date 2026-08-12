using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pride Q: blink between enemies for several strikes.
/// Owner stays untargetable for the whole chain; each strike damages via DealDamageTo
/// (appliedEffectTags drive hit VFX). After each hit, retarget the nearest other enemy
/// near the current target, or keep striking the same target when alone.
/// </summary>
public class EnemyAbility_PrideBlinkChain : EnemyAbility
{
    public float searchRange = 8f;
    public int blinkCount = 4;
    public float blinkInterval = 0.25f;
    public float arrivalOffset = 0.6f;
    public float damageMultiplier = 1f;
    [Tooltip("Self Effect while blinking. Grant State.Defense.Untargetable; put afterimage/trail on activeVfxPrefab.")]
    public GameplayEffectDefinition untargetableEffect;
    [Tooltip("Hide mesh/skinned renderers while blinking so only Effect afterimage VFX remains.")]
    public bool hideOwnerMeshes = true;

    [Header("Activation Display")]
    [Tooltip("First display object on the enemy body. It is shown while this ability is active.")]
    public GameObject activationDisplayA;
    [Tooltip("Second display object on the enemy body. It is shown while this ability is active.")]
    public GameObject activationDisplayB;

    private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "穿梭斩";
        cooldown = cooldown <= 0f ? 1.5f : cooldown;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Pride.BlinkChain", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Pride.BlinkChain");
        SetActivationDisplays(false);
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        return FindNearestTarget(owner.transform.position, null) != null;
    }

    protected override void OnTrigger()
    {
        StartCoroutine(BlinkRoutine());
    }

    private IEnumerator BlinkRoutine()
    {
        if (owner == null || owner.Combat == null)
        {
            EndActivationEffect();
            yield break;
        }

        Enemy currentTarget = FindNearestTarget(owner.transform.position, null);
        if (currentTarget == null)
        {
            EndActivationEffect();
            yield break;
        }

        if (untargetableEffect != null)
            owner.Combat.ApplyEffect(untargetableEffect, owner.Combat, abilityTags, out _);

        SetActivationDisplays(true);
        if (hideOwnerMeshes) HideOwnerMeshes();
        owner.IsAbilityFacingLocked = true;

        int strikes = Mathf.Max(1, Mathf.RoundToInt(GetCardParameter("BlinkCount", blinkCount)));
        for (int i = 0; i < strikes && owner != null; i++)
        {
            if (currentTarget == null || currentTarget.isDowned)
            {
                currentTarget = FindNearestTarget(owner.transform.position, null);
                if (currentTarget == null) break;
            }

            Vector3 from = owner.transform.position;
            Vector3 to = currentTarget.transform.position;
            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = owner.transform.forward;
            direction.Normalize();

            owner.transform.position = to - direction * arrivalOffset;
            owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            DealDamageTo(currentTarget, damage * damageMultiplier);

            if (IsUpgradeUnlocked("Pride.BlinkSwordQi"))
            {
                EnemyAbility_SwordQi swordQi = owner.GetComponentInChildren<EnemyAbility_SwordQi>(true);
                if (swordQi != null) swordQi.FireDirectedBurst(direction, damage);
            }

            Enemy next = FindNearestTarget(currentTarget.transform.position, currentTarget);
            currentTarget = next != null ? next : currentTarget;

            yield return AbilityWait(blinkInterval);
        }

        if (owner != null)
        {
            owner.IsAbilityFacingLocked = false;
            if (untargetableEffect != null) owner.Combat.RemoveEffect(untargetableEffect);
        }

        RestoreOwnerMeshes();
        SetActivationDisplays(false);
        EndActivationEffect();
    }

    private void SetActivationDisplays(bool visible)
    {
        if (activationDisplayA != null) activationDisplayA.SetActive(visible);
        if (activationDisplayB != null) activationDisplayB.SetActive(visible);
    }

    private Enemy FindNearestTarget(Vector3 origin, Enemy exclude)
    {
        Enemy result = null;
        float bestDistance = searchRange;
        Enemy[] candidates = FindObjectsOfType<Enemy>();
        for (int i = 0; i < candidates.Length; i++)
        {
            Enemy candidate = candidates[i];
            if (candidate == null || candidate == exclude || owner == null || !owner.CanDamage(candidate)) continue;
            float distance = Vector3.Distance(origin, candidate.transform.position);
            if (distance > bestDistance) continue;
            bestDistance = distance;
            result = candidate;
        }
        return result;
    }

    private void HideOwnerMeshes()
    {
        _hiddenRenderers.Clear();
        if (owner == null) return;

        Renderer[] renderers = owner.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled) continue;
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            renderer.enabled = false;
            _hiddenRenderers.Add(renderer);
        }
    }

    private void RestoreOwnerMeshes()
    {
        for (int i = 0; i < _hiddenRenderers.Count; i++)
        {
            if (_hiddenRenderers[i] != null) _hiddenRenderers[i].enabled = true;
        }
        _hiddenRenderers.Clear();
    }

    protected override void OnDisable()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        if (owner != null && owner.Combat != null && untargetableEffect != null)
            owner.Combat.RemoveEffect(untargetableEffect);
        RestoreOwnerMeshes();
        SetActivationDisplays(false);
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        if (owner != null) owner.IsAbilityFacingLocked = false;
        if (owner != null && owner.Combat != null && untargetableEffect != null)
            owner.Combat.RemoveEffect(untargetableEffect);
        RestoreOwnerMeshes();
        SetActivationDisplays(false);
        base.ResetForOwnerReuse();
    }
}
