using System.Collections.Generic;
using UnityEngine;

/// <summary>Single-ability, range/LOS/state/history aware Boss decision loop.</summary>
public sealed class BossCombatBrain : MonoBehaviour
{
    public float decisionInterval = 0.30f;
    public List<BossAbilityProfile> profiles = new List<BossAbilityProfile>();

    BossSevenfoldActor owner;
    readonly List<EnemyAbility> recentAbilities = new List<EnemyAbility>(2);
    readonly List<string> recentFamilies = new List<string>(2);
    float nextDecisionTime;
    int decisionIndex;
    int failedDecisionCount;

    public IReadOnlyList<EnemyAbility> RecentAbilities => recentAbilities;

    public void BuildDefaultProfiles(EnemyAbility[] abilities)
    {
        profiles.Clear();
        if (abilities == null) return;
        float rangeScale = owner != null ? owner.BossCombatScaleMultiplier : 1f;
        for (int i = 0; i < abilities.Length; i++)
        {
            EnemyAbility ability = abilities[i];
            if (ability == null || (ability.type != EnemyAbility.AbilityType.BasicAttack && ability.type != EnemyAbility.AbilityType.Skill)) continue;
            BossAbilityProfile profile = new BossAbilityProfile
            {
                ability = ability,
                family = ability.abilityName,
                minRange = ability.type == EnemyAbility.AbilityType.BasicAttack ? 0f : 4f,
                maxRange = ability.type == EnemyAbility.AbilityType.BasicAttack ? 8f : 30f,
                preferredRange = ability.type == EnemyAbility.AbilityType.BasicAttack ? 4f : 14f,
                requiresLineOfSight = true,
                baseWeight = 1f,
            };
            ConfigureRange(profile, ability, rangeScale);
            profiles.Add(profile);
        }
    }

    static void ConfigureRange(BossAbilityProfile profile, EnemyAbility ability, float scale)
    {
        if (ability is EnemyAbility_PrideBlinkChain blink)
        {
            profile.minRange = 0f;
            profile.maxRange = blink.searchRange;
            profile.preferredRange = blink.searchRange * 0.65f;
        }
        else if (ability is EnemyAbility_SwordQi swordQi)
        {
            profile.minRange = 0f;
            profile.maxRange = swordQi.maxRange;
            profile.preferredRange = swordQi.maxRange * 0.6f;
        }
        else if (ability is EnemyAbility_EnvyLaser laser)
        {
            profile.minRange = 0f;
            profile.maxRange = laser.maxRange;
            profile.preferredRange = laser.maxRange * 0.7f;
        }
        else if (ability is EnemyAbility_SlothChargeShot chargeShot)
        {
            profile.minRange = 0f;
            profile.maxRange = chargeShot.maxRange;
            profile.preferredRange = chargeShot.maxRange * 0.7f;
        }
        else if (ability is EnemyAbility_GreedHands greedHands)
        {
            profile.minRange = 0f;
            profile.maxRange = greedHands.detectRange;
            profile.preferredRange = greedHands.detectRange * 0.65f;
        }
        else if (ability is EnemyAbility_WrathChainStorm chainStorm)
        {
            profile.minRange = 0f;
            profile.maxRange = chainStorm.pullRadius;
            profile.preferredRange = chainStorm.pullRadius * 0.7f;
        }
        else if (ability is EnemyAbility_LustSoulPull soulPull)
        {
            profile.minRange = 0f;
            profile.maxRange = soulPull.pullMaxDistance;
            profile.preferredRange = soulPull.pullMaxDistance * 0.7f;
        }
        else if (ability is EnemyAbility_GluttonyDevour devour)
        {
            profile.minRange = 0f;
            profile.maxRange = devour.range;
            profile.preferredRange = devour.range * 0.7f;
        }
        else if (ability is EnemyAbility_GluttonyAbyssMaw abyssMaw)
        {
            profile.minRange = 0f;
            profile.maxRange = abyssMaw.maxAimDistance;
            profile.preferredRange = abyssMaw.maxAimDistance * 0.6f;
        }
        else if (ability is EnemyAbility_WrathSlam wrathSlam)
        {
            profile.minRange = 0f;
            profile.maxRange = wrathSlam.radius;
            profile.preferredRange = wrathSlam.radius * 0.7f;
        }
        else if (ability is EnemyAbility_SlothDrone)
        {
            profile.minRange = 0f;
            profile.maxRange = float.MaxValue;
            profile.preferredRange = 0f;
            profile.requiresLineOfSight = false;
        }
        else if (ability is EnemyAbility_LustRoundTrip roundTrip)
        {
            profile.minRange = 0f;
            profile.maxRange = roundTrip.mistRange;
            profile.preferredRange = roundTrip.mistRange * 0.65f;
        }
        else
        {
            string typeName = ability.GetType().Name;
            if (typeName.Contains("GreedGuard"))
            {
                profile.minRange = 0f;
                profile.maxRange = 12f;
                profile.preferredRange = 7f;
            }
            else if (ability.type == EnemyAbility.AbilityType.Skill)
            {
                profile.minRange = 2f;
                profile.maxRange = 20f;
                profile.preferredRange = 9f;
            }
        }

        float effectiveScale = ability is EnemyAbility_EnvyLaser ? 1f : Mathf.Max(1f, scale);
        profile.minRange *= effectiveScale;
        profile.maxRange *= effectiveScale;
        profile.preferredRange *= effectiveScale;
    }


    void Awake()
    {
        owner = GetComponent<BossSevenfoldActor>();
        nextDecisionTime = Time.unscaledTime + decisionInterval;
    }

    void Update()
    {
        if (owner == null || owner.IsDefeated || !owner.CanAct || owner.IsAbilitySequenceLocked) return;
        if (Time.unscaledTime < nextDecisionTime) return;
        nextDecisionTime = Time.unscaledTime + decisionInterval;
        Vector3 targetPosition = owner.GetBossTargetPosition();
        EnemyAbility choice = ChooseAbility(targetPosition);
        float distance = Vector3.Distance(owner.transform.position, targetPosition);
        if (owner.TryRequestTacticalTeleport(targetPosition, distance, choice != null, failedDecisionCount))
        {
            failedDecisionCount = 0;
            return;
        }
        if (choice == null)
        {
            failedDecisionCount++;
            if (failedDecisionCount >= 3 && owner.TryTeleportTowardsTarget(targetPosition))
                failedDecisionCount = 0;
            return;
        }
        failedDecisionCount = 0;
        owner.FaceBossTarget(targetPosition);
        choice.Trigger();
        owner.CompleteVoidWalkFollowUp(choice);
        Record(choice, FindFamily(choice));
        decisionIndex++;
    }

    public EnemyAbility ChooseAbility(Vector3 targetPosition)
    {
        if (owner.HasVoidWalkFollowUp)
        {
            owner.TryGetVoidWalkFollowUp(targetPosition, out EnemyAbility followUp);
            return followUp;
        }

        for (int i = 0; i < profiles.Count; i++)
        {
            EnemyAbility_SlothDrone drone = profiles[i] != null ? profiles[i].ability as EnemyAbility_SlothDrone : null;
            if (drone != null && !drone.HasActiveDrone && drone.CanTrigger()) return drone;
        }

        float distance = Vector3.Distance(owner.transform.position, targetPosition);
        BossAbilityProfile best = null;
        float bestScore = 0f;
        for (int i = 0; i < profiles.Count; i++)
        {
            BossAbilityProfile profile = profiles[i];
            if (!CanUse(profile, distance, targetPosition)) continue;
            float score = Mathf.Max(0f, profile.baseWeight) * DistanceFit(profile, distance);
            score *= owner.AiRandomRange(0.85f, 1.15f);
            if (score > bestScore)
            {
                bestScore = score;
                best = profile;
            }
        }
        return best != null ? best.ability : null;
    }

    public bool CanUse(BossAbilityProfile profile, float distance, Vector3 targetPosition)
    {
        if (profile == null || profile.ability == null || !profile.ability.CanTrigger()) return false;
        if (distance < profile.minRange || distance > profile.maxRange) return false;
        if (profile.requiresLineOfSight && !HasLineOfSight(targetPosition)) return false;
        if (profile.requiresEnvyMark && !owner.TargetHasEnvyMark()) return false;
        if (profile.requiresLustAnchor && !owner.HasTeleportAnchor) return false;
        if (recentAbilities.Count >= 1 && recentAbilities[recentAbilities.Count - 1] == profile.ability) return false;
        if (recentAbilities.Count >= 2 && recentAbilities[recentAbilities.Count - 2] == profile.ability) return false;
        if (recentFamilies.Count >= 1 && recentFamilies[recentFamilies.Count - 1] == profile.family) return false;
        return true;
    }

    bool HasLineOfSight(Vector3 targetPosition)
    {
        if (!Physics.Linecast(owner.transform.position + Vector3.up, targetPosition + Vector3.up,
                out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)) return true;
        MonsterActor body = PossessionManager.Instance != null ? PossessionManager.Instance.CurrentBody : null;
        if (body != null && hit.collider.GetComponentInParent<MonsterActor>() == body) return true;
        PlayerHealth soul = hit.collider.GetComponentInParent<PlayerHealth>();
        return soul != null && soul == PlayerHealth.Instance;
    }

    static float DistanceFit(BossAbilityProfile profile, float distance)
    {
        if (profile.preferredRange <= 0f) return 1f;
        return Mathf.Clamp01(1f - Mathf.Abs(distance - profile.preferredRange) / Mathf.Max(1f, profile.maxRange));
    }

    void Record(EnemyAbility ability, string family)
    {
        recentAbilities.Add(ability);
        recentFamilies.Add(family ?? string.Empty);
        while (recentAbilities.Count > 2) recentAbilities.RemoveAt(0);
        while (recentFamilies.Count > 2) recentFamilies.RemoveAt(0);
    }

    string FindFamily(EnemyAbility ability)
    {
        for (int i = 0; i < profiles.Count; i++)
            if (profiles[i] != null && profiles[i].ability == ability) return profiles[i].family;
        return string.Empty;
    }
}
