using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Envy Mobility: short flight boost (+200% move speed for 1s). No real air layer / base i-frames.
/// EN-M01 writes one Mark per Enemy crossed by the flight path segment.
/// </summary>
public class EnemyAbility_EnvyFlight : EnemyAbility
{
    public const string AbilityTag = "Ability.Monster.Envy.Flight";

    [Header("Flight")]
    public float flightDuration = 1f;
    [Tooltip("Move speed multiplier while flying. Canonical +200% => 3x.")]
    public float flightSpeedMultiplier = 3f;
    public GameplayEffectDefinition flightEffect;
    public float pathMarkSampleRadius = 0.6f;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "飞行加速";
        cooldown = cooldown < 0f ? 0f : cooldown;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, AbilityTag, System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add(AbilityTag);
    }

    protected override void OnTrigger()
    {
        StartCoroutine(FlightRoutine());
    }

    private IEnumerator FlightRoutine()
    {
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        float duration = Mathf.Max(0.05f, GetCardParameter("FlightDuration", flightDuration));
        Vector3 lastPos = owner.transform.position;
        HashSet<Enemy> markedThisFlight = IsUpgradeUnlocked("EN-M01") ? new HashSet<Enemy>() : null;

        if (flightEffect != null)
            owner.Combat.ApplyEffect(flightEffect, owner.Combat, abilityTags, out _);
        else if (owner.Combat != null)
            owner.Combat.AddMoveSpeedMultiplier(this, flightSpeedMultiplier);

        float elapsed = 0f;
        while (owner != null && elapsed < duration)
        {
            elapsed += AbilityDeltaTime;
            if (markedThisFlight != null)
            {
                Vector3 now = owner.transform.position;
                ApplyPathMarks(lastPos, now, markedThisFlight);
                lastPos = now;
            }

            yield return null;
        }

        if (owner != null)
        {
            if (flightEffect != null)
                owner.Combat.RemoveEffect(flightEffect);
            else if (owner.Combat != null)
                owner.Combat.RemoveMoveSpeedMultiplier(this);
        }

        EndActivationEffect();
    }

    private void ApplyPathMarks(Vector3 from, Vector3 to, HashSet<Enemy> already)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.01f) return;

        Vector3 dir = delta / length;
        float sampleRadius = ScaleAbilityRadius(pathMarkSampleRadius);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / Mathf.Max(0.2f, sampleRadius)));
        EnemyAbility_EnvyLaser laser = owner.GetComponentInChildren<EnemyAbility_EnvyLaser>(true);

        for (int i = 0; i <= steps; i++)
        {
            Vector3 sample = from + dir * (length * i / steps);
            foreach (Collider col in Physics.OverlapSphere(sample, sampleRadius))
            {
                if (col == null) continue;
                Enemy enemy = col.GetComponentInParent<Enemy>();
                if (enemy == null || !owner.CanDamage(enemy) || already.Contains(enemy)) continue;
                already.Add(enemy);
                laser?.ApplyMarkTo(enemy, dealDamage: false);
            }
        }
    }

    protected override void OnDisable()
    {
        if (owner != null && owner.Combat != null)
        {
            if (flightEffect != null) owner.Combat.RemoveEffect(flightEffect);
            owner.Combat.RemoveMoveSpeedMultiplier(this);
        }

        base.OnDisable();
    }
}
