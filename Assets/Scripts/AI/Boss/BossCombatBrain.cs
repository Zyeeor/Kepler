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
            ConfigureRange(profile, ability);
            profiles.Add(profile);
        }
    }

    static void ConfigureRange(BossAbilityProfile profile, EnemyAbility ability)
    {
        string typeName = ability.GetType().Name;
        if (typeName.Contains("GluttonyDevour") || typeName.Contains("GluttonyAbyssMaw"))
        {
            profile.minRange = 0f;
            profile.maxRange = 4.5f;
            profile.preferredRange = 2.2f;
        }
        else if (typeName.Contains("WrathSlam"))
        {
            profile.minRange = 0f;
            profile.maxRange = 7f;
            profile.preferredRange = 4f;
        }
        else if (typeName.Contains("GreedGuard"))
        {
            profile.minRange = 0f;
            profile.maxRange = 12f;
            profile.preferredRange = 7f;
        }
        else if (typeName.Contains("SwordQi") || typeName.Contains("EnvyLaser")
                 || typeName.Contains("SlothChargeShot") || typeName.Contains("LustRoundTrip"))
        {
            profile.minRange = 4f;
            profile.maxRange = 26f;
            profile.preferredRange = 13f;
        }
        else if (ability.type == EnemyAbility.AbilityType.Skill)
        {
            profile.minRange = 2f;
            profile.maxRange = 20f;
            profile.preferredRange = 9f;
        }
    }


    void Awake()
    {
        owner = GetComponent<BossSevenfoldActor>();
        nextDecisionTime = Time.unscaledTime + decisionInterval;
    }

    void Update()
    {
        if (owner == null || owner.IsDefeated || !owner.CanAct) return;
        if (Time.unscaledTime < nextDecisionTime) return;
        nextDecisionTime = Time.unscaledTime + decisionInterval;
        Vector3 targetPosition = owner.GetBossTargetPosition();
        EnemyAbility choice = ChooseAbility(targetPosition);
        if (choice == null)
        {
            failedDecisionCount++;
            if (failedDecisionCount >= 4 && owner.TryTeleportTowardsTarget(owner.GetBossTargetPosition()))
                failedDecisionCount = 0;
            return;
        }
        failedDecisionCount = 0;
        owner.FaceBossTarget(targetPosition);
        choice.Trigger();
        Record(choice, FindFamily(choice));
        decisionIndex++;
    }

    public EnemyAbility ChooseAbility(Vector3 targetPosition)
    {
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
