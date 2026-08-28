using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Envy Special: cash out all target Marks as chain lightning strikes.
/// No legal marked target => no start / no HP cost / no reload.
/// EN-S01 schedules delayed follow-up strikes at the same positions.
/// </summary>
public class EnemyAbility_EnvyThunderstorm : EnemyAbility
{
    public const string AbilityTag = "Ability.Monster.Envy.Thunderstorm";

    [Header("Thunderstorm")]
    public float baseThunderDamage = 10f;
    public float searchRadius = 30f;
    public float telegraphDuration = 0.6f;
    public float chainDelay = 0.08f;

    [Header("EN-S01 Follow-up")]
    public float followUpDelay = 1f;
    public float followUpDamageMult = 1f;

    [Header("Trajectory Visual")]
    public GameObject boltVfxPrefab;
    public Vector3 boltVfxPositionOffset = Vector3.zero;
    public Vector3 boltVfxRotationOffset = new Vector3(0f, -90f, 90f);
    public Vector3 boltVfxScale = Vector3.one;
    public float boltVfxDuration = 0.45f;
    public GameObject hitEffectPrefab;

    public float hitEffectDuration = 0.5f;
    public GameObject telegraphPrefab;

    private bool _gluttonyCopyMode;
    private float _gluttonyCopyRadius;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "雷暴兑现";
        cooldown = cooldown <= 0f ? 1f : cooldown;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, AbilityTag, System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add(AbilityTag);
    }

    public bool HasLegalMarkedTargets => CountLegalMarkedTargets() > 0;

    /// <summary>Configures the copied payload to strike nearby legal enemies without Marks.</summary>
    public void ConfigureForGluttonyCopy(float radius)
    {
        _gluttonyCopyMode = true;
        _gluttonyCopyRadius = Mathf.Max(0f, radius);
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        return _gluttonyCopyMode ? CountGluttonyCopyTargets() > 0 : HasLegalMarkedTargets;
    }

    protected override void OnTrigger()
    {
        bool hasTargets = _gluttonyCopyMode ? CountGluttonyCopyTargets() > 0 : CountLegalMarkedTargets() > 0;
        if (owner == null || !hasTargets)
        {
            EndActivationEffect();
            return;
        }

        Animator[] animators = owner.GetComponentsInChildren<Animator>(false);
        for (int i = 0; i < animators.Length; i++)
            animators[i].SetTrigger("Skill");
        StartCoroutine(ThunderstormRoutine());
    }

    private IEnumerator ThunderstormRoutine()
    {
        List<MarkedStrike> strikes = CollectStrikes();
        if (strikes.Count == 0)
        {
            EndActivationEffect();
            yield break;
        }

        if (telegraphDuration > 0f)
        {
            for (int i = 0; i < strikes.Count; i++)
                SpawnTelegraph(strikes[i].position);
            yield return AbilityWait(telegraphDuration);
        }

        for (int i = 0; i < strikes.Count; i++)
        {
            SettleStrike(strikes[i], isFollowUp: false);

            if (i + 1 < strikes.Count)
                yield return AbilityWait(chainDelay);
        }

        EndActivationEffect();

        if (IsUpgradeUnlocked("EN-S01"))
            StartCoroutine(FollowUpRoutine(strikes));
    }

    private IEnumerator FollowUpRoutine(List<MarkedStrike> original)
    {
        // Stack Max=1 merges to two follow-ups (strike 2 and 3) at the same positions.
        int extraWaves = Mathf.Max(1, Mathf.RoundToInt(GetCardParameter("FollowUpWaves", 2f)));
        for (int wave = 0; wave < extraWaves; wave++)
        {
            yield return AbilityWait(Mathf.Max(0.05f, GetCardParameter("FollowUpDelay", followUpDelay)));
            if (owner == null) yield break;

            for (int i = 0; i < original.Count; i++)
                SpawnTelegraph(original[i].position);
            yield return AbilityWait(telegraphDuration);

            for (int i = 0; i < original.Count; i++)
            {
                MarkedStrike follow = original[i];
                follow.storedDamage = 0f; // follow-ups do not re-consume Mark
                follow.baseDamage = baseThunderDamage * followUpDamageMult;
                SettleStrike(follow, isFollowUp: true);

                if (i + 1 < original.Count)
                    yield return AbilityWait(chainDelay);
            }
        }
    }

    private List<MarkedStrike> CollectStrikes()
    {
        if (_gluttonyCopyMode)
            return CollectGluttonyCopyStrikes();

        List<MarkedStrike> list = new List<MarkedStrike>();
        float range = GetEffectiveRange();
        IReadOnlyList<EnvyMarkTarget> marks = EnvyMarkTarget.AllActive;
        for (int i = 0; i < marks.Count; i++)
        {
            EnvyMarkTarget mark = marks[i];
            if (mark == null || !mark.IsActive || mark.Host == null) continue;
            if (mark.Source != null && mark.Source != owner) continue;
            if (!owner.CanDamage(mark.Host)) continue;
            float dist = Vector3.Distance(owner.transform.position, mark.Host.transform.position);
            if (dist > range) continue;

            float stored = mark.ConsumeStoredDamage();
            list.Add(new MarkedStrike
            {
                target = mark.Host,
                position = mark.Host.transform.position + Vector3.up,
                storedDamage = stored,
                baseDamage = baseThunderDamage
            });
        }

        return list;
    }

    private List<MarkedStrike> CollectGluttonyCopyStrikes()
    {
        List<MarkedStrike> list = new List<MarkedStrike>();
        if (owner == null) return list;

        float radius = Mathf.Max(0f, _gluttonyCopyRadius);
        float radiusSqr = radius * radius;
        Vector3 origin = owner.transform.position;
        CombatHitboxDebug.DrawSphere(drawHitboxes, origin, radius, -1f);
        IReadOnlyList<Enemy> enemies = EnemyRegistry.All;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy candidate = enemies[i];
            if (candidate == null || candidate == owner || !owner.CanDamage(candidate)) continue;
            Vector3 offset = candidate.transform.position - origin;
            offset.y = 0f;
            if (offset.sqrMagnitude > radiusSqr) continue;
            list.Add(new MarkedStrike
            {
                target = candidate,
                position = candidate.transform.position + Vector3.up,
                storedDamage = 0f,
                baseDamage = baseThunderDamage
            });
        }
        return list;
    }

    private void SettleStrike(MarkedStrike strike, bool isFollowUp)
    {
        float damage = strike.baseDamage + (isFollowUp ? 0f : strike.storedDamage);
        SpawnBolt(strike.target, strike.position);
        SpawnHitEffect(strike.position);


        if (strike.target != null && owner != null && owner.CanDamage(strike.target))
            DealDamageTo(strike.target, damage);
        else
        {
            // Follow-up may land after target died; still show VFX at snapshot position.
            float hitRadius = ScaleAbilityRadius(0.75f);
            CombatHitboxDebug.DrawSphere(drawHitboxes, strike.position, hitRadius, hitEffectDuration);
            Collider[] cols = Physics.OverlapSphere(strike.position, hitRadius);
            for (int i = 0; i < cols.Length; i++)
            {
                Enemy enemy = cols[i].GetComponentInParent<Enemy>();
                if (enemy != null && owner != null && owner.CanDamage(enemy))
                {
                    DealDamageTo(enemy, damage);
                    break;
                }
            }
        }
    }

    private int CountLegalMarkedTargets()
    {
        if (owner == null) return 0;
        float range = GetEffectiveRange();
        int count = 0;
        IReadOnlyList<EnvyMarkTarget> marks = EnvyMarkTarget.AllActive;
        for (int i = 0; i < marks.Count; i++)
        {
            EnvyMarkTarget mark = marks[i];
            if (mark == null || !mark.IsActive || mark.Host == null) continue;
            if (mark.Source != null && mark.Source != owner) continue;
            if (!owner.CanDamage(mark.Host)) continue;
            if (Vector3.Distance(owner.transform.position, mark.Host.transform.position) > range) continue;
            count++;
        }

        return count;
    }

    private int CountGluttonyCopyTargets()
    {
        if (owner == null) return 0;
        float radiusSqr = _gluttonyCopyRadius * _gluttonyCopyRadius;
        Vector3 origin = owner.transform.position;
        IReadOnlyList<Enemy> enemies = EnemyRegistry.All;
        int count = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            Enemy candidate = enemies[i];
            if (candidate == null || candidate == owner || !owner.CanDamage(candidate)) continue;
            Vector3 offset = candidate.transform.position - origin;
            offset.y = 0f;
            if (offset.sqrMagnitude <= radiusSqr) count++;
        }
        return count;
    }

    private float GetEffectiveRange()
    {
        float range = searchRadius;
        if (IsUpgradeUnlocked("EN-TG01"))
            range += GetCardParameter("SpecialRangeBonus", 4f);
        return ScaleAbilityRadius(range);
    }

    private void SpawnTelegraph(Vector3 pos)
    {
        if (telegraphPrefab == null) return;
        SpawnVfxTracked(telegraphPrefab, pos, Quaternion.identity, telegraphDuration + 0.05f);
    }

    private void SpawnHitEffect(Vector3 pos)
    {
        if (hitEffectPrefab == null) return;
        SpawnVfxTracked(hitEffectPrefab, pos, Quaternion.identity, hitEffectDuration);
    }

    private void SpawnBolt(Enemy target, Vector3 fallbackPosition)
    {
        if (boltVfxPrefab == null) return;
        Vector3 position = target != null ? target.transform.position : fallbackPosition;
        GameObject bolt = SpawnVfxTracked(
            boltVfxPrefab,
            position + boltVfxPositionOffset,
            Quaternion.Euler(boltVfxRotationOffset),
            Mathf.Max(0.05f, boltVfxDuration));
        if (bolt != null)
            bolt.transform.localScale = Vector3.Scale(bolt.transform.localScale, boltVfxScale);

    }


    private struct MarkedStrike
    {
        public Enemy target;
        public Vector3 position;
        public float storedDamage;
        public float baseDamage;
    }
}
