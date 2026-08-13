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

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "木灵";
        cooldown = cooldown <= 0f ? 5f : cooldown;
        if (abilityTags == null) abilityTags = new System.Collections.Generic.List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Sloth.Drone", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Sloth.Drone");
    }

    public override float GetHpCostMultiplier()
    {
        return IsUpgradeUnlocked("Sloth.DroneEconomy") ? 0.5f : 1f;
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

        Vector3 start = owner.transform.position + Vector3.up * 1.2f;
        Vector3 apex = start + Vector3.up * tossHeight;
        GameObject go = Instantiate(dronePrefab, start, Quaternion.identity);
        SummonActor summon = go.GetComponent<SummonActor>();
        if (summon == null) summon = go.AddComponent<SummonActor>();

        float life = droneLifetime;
        if (IsUpgradeUnlocked("Sloth.DroneEconomy"))
            life *= 1.1f;

        summon.Bind(
            owner,
            life,
            IsUpgradeUnlocked("Sloth.DroneDeathBlast"),
            deathBlastDamage,
            deathBlastTriggerDistance,
            deathBlastRadius,
            deathBlastDiveSpeed,
            deathBlastVfx,
            deathBlastVfxDuration);

        float elapsed = 0f;
        while (go != null && elapsed < tossDuration)
        {
            elapsed += AbilityDeltaTime;
            float t = Mathf.Clamp01(elapsed / tossDuration);
            go.transform.position = Vector3.Lerp(start, apex, t);
            yield return null;
        }

        EndActivationEffect();
    }
}
