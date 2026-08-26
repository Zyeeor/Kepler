using UnityEngine;
using System.Collections;

/// <summary>
/// Sloth Q: throw a wood-spirit drone into the air. The summon follows the player
/// (possessed body, or soul while AI) and auto-fires bullets for a short lifetime.
/// Cards: Sloth.DroneEconomy (HP cost -50%, lifetime +10%), Sloth.DroneDeathBlast.
/// </summary>
public class EnemyAbility_SlothDrone : EnemyAbility
{
    public GameObject dronePrefab;
    public float droneLifetime = 4f;
    public float tossHeight = 4f;
    public float tossDuration = 0.35f;
    public float deathBlastDamage = 20f;
    [Tooltip("Start the explosion when closer than this to the nearest enemy.")]
    public float deathBlastTriggerDistance = 1.5f;
    [Tooltip("Damage radius of the death explosion.")]
    public float deathBlastRadius = 3f;
    public float deathBlastDiveSpeed = 14f;
    public GameObject deathBlastVfx;
    [Tooltip("Death explosion VFX lifetime before it is destroyed.")]
    public float deathBlastVfxDuration = 1f;

    [Header("Canonical Sloth Cards")]
    public int baseDroneCount = 1;
    public int bonusDroneCount = 2;
    public int maxActiveDrones = 3;
    public float pursuitAttackIntervalMultiplier = 0.6f;
    [Tooltip("Canonical attack range of each summoned drone.")]
    public float droneAttackRange = 30f;

    private readonly System.Collections.Generic.List<SummonActor> activeDrones = new System.Collections.Generic.List<SummonActor>();
    public bool HasActiveDrone
    {
        get
        {
            PruneDrones();
            return activeDrones.Count > 0;
        }
    }

    int GetBossDroneLimit()
    {
        BossSevenfoldActor boss = owner as BossSevenfoldActor;
        return boss != null ? boss.CombatPhase : Mathf.Max(1, maxActiveDrones);
    }

    private void OnEnable()

    {
        type = AbilityType.Skill;
        abilityName = "木灵";
        cooldown = owner is BossSevenfoldActor ? 15f : (cooldown <= 0f ? 5f : cooldown);
        if (abilityTags == null) abilityTags = new System.Collections.Generic.List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Sloth.Drone", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Sloth.Drone");
        EnsureUpgrade("SL-S01");
        EnsureUpgrade("SL-S03");

    }

    public override float GetHpCostMultiplier()
    {
        return 1f;
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        PruneDrones();
        return !(owner is BossSevenfoldActor) || activeDrones.Count < GetBossDroneLimit();
    }


    protected override void OnTrigger()
    {
        StartCoroutine(TossRoutine());
    }

    private IEnumerator TossRoutine()
    {
        if (owner == null || dronePrefab == null)
        {
            EndActivationEffect();
            yield break;
        }

        bool isBossDrone = owner is BossSevenfoldActor;
        int spawnCount = isBossDrone
            ? Mathf.Max(0, GetBossDroneLimit() - activeDrones.Count)
            : baseDroneCount + (IsUpgradeUnlocked("SL-S01") ? bonusDroneCount : 0);
        for (int i = 0; i < spawnCount; i++)
        {
            PruneDrones();
            while (activeDrones.Count >= (isBossDrone ? GetBossDroneLimit() : Mathf.Max(1, maxActiveDrones)))
            {
                SummonActor oldest = activeDrones[0];
                activeDrones.RemoveAt(0);
                if (oldest != null) Destroy(oldest.gameObject);
            }

            float ownerScale = OwnerCombatScaleMultiplier;
            Vector3 start = owner.transform.position + Vector3.up * 1.2f * ownerScale;
            Vector3 apex = start + Vector3.up * tossHeight * ownerScale;
            GameObject go = Instantiate(dronePrefab, start, Quaternion.identity);
            go.transform.localScale *= OwnerCombatScaleMultiplier;
            SummonActor summon = go.GetComponent<SummonActor>();
            if (summon == null) summon = go.AddComponent<SummonActor>();
            summon.Bind(owner, droneLifetime, false, deathBlastDamage,
                ScaleAbilityRadius(deathBlastTriggerDistance), ScaleAbilityRadius(deathBlastRadius),
                deathBlastDiveSpeed, deathBlastVfx, deathBlastVfxDuration);
            summon.ConfigurePursuit(
                IsUpgradeUnlocked("SL-S03"),
                IsUpgradeUnlocked("SL-S03")
                    ? GetCardParameter("PursuitAttackIntervalMultiplier", pursuitAttackIntervalMultiplier)
                    : 1f);

            // Keep spawned drones aligned with the monster contract even when an older
            // drone prefab still carries the previous 10 m serialized value.
            EnemyAbility_SummonBolt bolt = go.GetComponentInChildren<EnemyAbility_SummonBolt>(true);
            if (bolt != null)
                bolt.searchRange = Mathf.Max(0.1f, droneAttackRange);

            activeDrones.Add(summon);
            StartCoroutine(TossDrone(go, start, apex));
        }

        yield return null;


        EndActivationEffect();
    }

    private IEnumerator TossDrone(GameObject drone, Vector3 start, Vector3 apex)
    {
        float elapsed = 0f;
        while (drone != null && elapsed < tossDuration)
        {
            elapsed += AbilityDeltaTime;
            drone.transform.position = Vector3.Lerp(start, apex, Mathf.Clamp01(elapsed / tossDuration));
            yield return null;
        }
    }

    private void PruneDrones()
    {
        for (int i = activeDrones.Count - 1; i >= 0; i--)
            if (activeDrones[i] == null) activeDrones.RemoveAt(i);
    }

    private void EnsureUpgrade(string effectId)
    {
        if (upgrades == null) upgrades = new System.Collections.Generic.List<UpgradeSlot>();
        if (upgrades.Exists(slot => slot != null && string.Equals(slot.effectId, effectId, System.StringComparison.OrdinalIgnoreCase))) return;
        upgrades.Add(new UpgradeSlot { effectId = effectId, unlocked = false });
    }
}
