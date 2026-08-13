using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pride Q: dash between enemies for several strikes (not teleport).
/// Each segment: approach to strike point → settle damage/VFX → short pass-through.
/// Approach + pass-through durations sum to blinkInterval.
/// Owner stays untargetable for the whole chain.
/// </summary>
public class EnemyAbility_PrideBlinkChain : EnemyAbility
{
    public float searchRange = 8f;
    public int blinkCount = 4;
    [Tooltip("Total duration of one strike segment (approach + pass-through).")]
    public float blinkInterval = 0.25f;
    [Tooltip("Stop this far before the target center when settling the slash.")]
    public float arrivalOffset = 0.6f;
    public float damageMultiplier = 1f;

    [Header("Pass-Through")]
    [Tooltip("Extra travel past the strike point along the blink direction.")]
    public float passThroughDistance = 1.2f;
    [Tooltip("Duration of the pass-through phase. Approach uses blinkInterval - this.")]
    public float passThroughDuration = 0.08f;

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

        float passDuration = Mathf.Clamp(passThroughDuration, 0f, Mathf.Max(0f, blinkInterval - 0.01f));
        float approachDuration = Mathf.Max(0.01f, blinkInterval - passDuration);

        int strikes = Mathf.Max(1, Mathf.RoundToInt(GetCardParameter("BlinkCount", blinkCount)));
        for (int i = 0; i < strikes && owner != null; i++)
        {
            if (currentTarget == null || currentTarget.isDowned)
            {
                currentTarget = FindNearestTarget(owner.transform.position, null);
                if (currentTarget == null) break;
            }

            Vector3 from = owner.transform.position;
            Vector3 targetPos = currentTarget.transform.position;
            Vector3 direction = targetPos - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                direction = owner.transform.forward;
            direction.Normalize();

            Vector3 strikePos = targetPos - direction * arrivalOffset;
            strikePos.y = from.y;
            Vector3 passEnd = strikePos + direction * Mathf.Max(0f, passThroughDistance);
            passEnd.y = from.y;

            owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            // (1) Dash into the strike point, then settle damage / hit VFX / optional sword qi.
            yield return MoveOwnerOverTime(from, strikePos, approachDuration);
            if (owner == null) break;

            if (currentTarget != null && !currentTarget.isDowned)
                DealDamageTo(currentTarget, damage * damageMultiplier);

            if (IsUpgradeUnlocked("Pride.BlinkSwordQi"))
            {
                EnemyAbility_SwordQi swordQi = owner.GetComponentInChildren<EnemyAbility_SwordQi>(true);
                // Blade must not be absorbed by the enemy just slashed.
                if (swordQi != null) swordQi.FireDirectedBurst(direction, damage, currentTarget);
            }

            Enemy next = FindNearestTarget(
                currentTarget != null ? currentTarget.transform.position : owner.transform.position,
                currentTarget);
            currentTarget = next != null ? next : currentTarget;

            // (2) Short pass-through to sell moving through the target.
            yield return MoveOwnerOverTime(strikePos, passEnd, passDuration);
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

    private IEnumerator MoveOwnerOverTime(Vector3 start, Vector3 end, float duration)
    {
        if (owner == null) yield break;
        if (duration <= 0.0001f)
        {
            owner.transform.position = end;
            yield break;
        }

        float elapsed = 0f;
        while (owner != null && elapsed < duration)
        {
            elapsed += AbilityDeltaTime;
            float u = Mathf.Clamp01(elapsed / duration);
            owner.transform.position = Vector3.Lerp(start, end, u);
            yield return null;
        }

        if (owner != null) owner.transform.position = end;
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
