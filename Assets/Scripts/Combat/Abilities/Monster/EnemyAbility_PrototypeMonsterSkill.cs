using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configurable prototypes for new CSV monster mechanics. Attach one instance per skill and select a mode.
/// VFX and Gameplay Effects are assigned in the Inspector; no scene or prefab changes are required here.
/// </summary>
public class EnemyAbility_PrototypeMonsterSkill : EnemyAbility
{
    public enum PrototypeMode { ChargeStrike, BlinkChain, Drone, Transform, ExecuteArc, Flight, Grapple, Summon }

    [Header("Prototype")]
    public PrototypeMode mode;
    public float range = 6f;
    public float radius = 1.5f;
    public float duration = 3f;
    public float speed = 20f;
    public int count = 3;
    public float damageMultiplier = 1f;
    public GameObject spawnedPrefab;
    public GameplayEffectDefinition selfEffect;
    public GameplayEffectDefinition targetEffect;

    [Header("Blink Chain")]
    public float blinkInterval = 0.25f;
    public bool becomeUntargetableDuringBlink = true;

    [Header("Drone / Summon")]
    public float summonAttackInterval = 0.5f;
    public float summonAttackRange = 8f;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        cooldown = cooldown <= 0f ? 5f : cooldown;
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;
        StartCoroutine(ExecuteRoutine());
    }

    private IEnumerator ExecuteRoutine()
    {
        switch (mode)
        {
            case PrototypeMode.ChargeStrike: yield return StartCoroutine(ChargeStrike()); break;
            case PrototypeMode.BlinkChain: yield return StartCoroutine(BlinkChain()); break;
            case PrototypeMode.Drone: yield return StartCoroutine(SummonRoutine(true)); break;
            case PrototypeMode.Transform: yield return StartCoroutine(TransformRoutine()); break;
            case PrototypeMode.ExecuteArc: ExecuteArc(); break;
            case PrototypeMode.Flight: yield return StartCoroutine(FlightRoutine()); break;
            case PrototypeMode.Grapple: yield return StartCoroutine(GrappleRoutine()); break;
            case PrototypeMode.Summon: yield return StartCoroutine(SummonRoutine(false)); break;
        }
        EndActivationEffect();
    }

    private IEnumerator ChargeStrike()
    {
        Vector3 direction = owner.transform.forward;
        if (owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aim)) direction = aim;
        Vector3 start = owner.transform.position;
        Vector3 end = start + direction.normalized * GetCardParameter("ChargeDistance", range);
        float travelled = 0f;
        while (owner != null && travelled < range)
        {
            float step = Mathf.Min(speed * AbilityDeltaTime, range - travelled);
            Vector3 next = owner.transform.position + direction.normalized * step;
            DamageEnemiesAlongPath(owner.transform.position, next, radius, damage * damageMultiplier);
            owner.transform.position = next;
            owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            travelled += step;
            yield return null;
        }
        DamageEnemiesInSphere(end, radius, damage * damageMultiplier);
    }

    private IEnumerator BlinkChain()
    {
        List<Enemy> targets = FindEnemiesInArc(owner.transform.position, owner.transform.forward, range, 360f);
        if (targets.Count == 0) yield break;
        int strikes = Mathf.RoundToInt(GetCardParameter("BlinkCount", count));
        if (becomeUntargetableDuringBlink && selfEffect != null)
            owner.Combat.ApplyEffect(selfEffect, owner.Combat, abilityTags, out _);
        for (int i = 0; i < strikes && owner != null; i++)
        {
            Enemy target = targets[i % targets.Count];
            if (target == null || target.isDowned) continue;
            Vector3 from = owner.transform.position;
            Vector3 to = target.transform.position;
            owner.transform.position = to - (to - from).normalized * 0.6f;
            owner.transform.rotation = Quaternion.LookRotation((to - from).normalized, Vector3.up);
            DealDamageTo(target, damage * damageMultiplier);
            yield return AbilityWait(blinkInterval);
        }
        if (selfEffect != null) owner.Combat.RemoveEffect(selfEffect);
    }

    private void ExecuteArc()
    {
        foreach (Enemy enemy in FindEnemiesInArc(owner.transform.position, owner.transform.forward, range, 100f))
        {
            if (enemy.currentHealth <= enemy.maxHealth * GetCardParameter("ExecuteThreshold", 0.2f))
                DealDamageTo(enemy, enemy.currentHealth);
            else
                DealDamageTo(enemy, damage * damageMultiplier);
            ApplyTargetEffect(enemy);
        }
    }

    private IEnumerator TransformRoutine()
    {
        if (selfEffect != null) owner.Combat.ApplyEffect(selfEffect, owner.Combat, abilityTags, out _);
        yield return AbilityWait(GetCardParameter("TransformDuration", duration));
        if (selfEffect != null) owner.Combat.RemoveEffect(selfEffect);
    }

    private IEnumerator FlightRoutine()
    {
        if (selfEffect != null) owner.Combat.ApplyEffect(selfEffect, owner.Combat, abilityTags, out _);
        yield return AbilityWait(GetCardParameter("FlightDuration", duration));
        if (selfEffect != null) owner.Combat.RemoveEffect(selfEffect);
    }

    private IEnumerator GrappleRoutine()
    {
        Vector3 direction = owner.transform.forward;
        if (owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 aim)) direction = aim;
        Vector3 start = owner.transform.position;
        Vector3 end = start + direction.normalized * range;
        float elapsed = 0f;
        while (owner != null && elapsed < range / speed)
        {
            elapsed += AbilityDeltaTime;
            owner.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed * speed / range));
            yield return null;
        }
    }

    private IEnumerator SummonRoutine(bool attacks)
    {
        int summons = Mathf.RoundToInt(GetCardParameter("SummonCount", count));
        for (int i = 0; i < summons; i++)
        {
            if (spawnedPrefab != null)
                SpawnVfxTracked(spawnedPrefab, owner.transform.position + Random.insideUnitSphere * radius, Quaternion.identity, duration);
        }
        if (!attacks) yield break;
        float elapsed = 0f;
        while (elapsed < duration && owner != null)
        {
            elapsed += AbilityDeltaTime;
            Enemy target = FindNearestEnemy();
            if (target != null)
            {
                DealDamageTo(target, damage * damageMultiplier);
                ApplyTargetEffect(target);
            }
            yield return AbilityWait(summonAttackInterval);
        }
    }

    private Enemy FindNearestEnemy()
    {
        Enemy result = null;
        float bestDistance = summonAttackRange;
        foreach (Enemy enemy in FindObjectsOfType<Enemy>())
        {
            if (owner == null || !owner.CanDamage(enemy)) continue;
            float distance = Vector3.Distance(owner.transform.position, enemy.transform.position);
            if (distance < bestDistance) { bestDistance = distance; result = enemy; }
        }
        return result;
    }

    private void ApplyTargetEffect(Enemy target)
    {
        if (targetEffect != null && target != null)
            target.Combat.ApplyEffect(targetEffect, owner.Combat, abilityTags, out _);
    }
}
