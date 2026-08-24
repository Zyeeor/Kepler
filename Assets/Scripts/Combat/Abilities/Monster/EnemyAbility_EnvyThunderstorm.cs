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
    public GameObject hitEffectPrefab;
    public float hitEffectDuration = 0.5f;
    public GameObject telegraphPrefab;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "雷暴兑现";
        cooldown = cooldown <= 0f ? 1f : cooldown;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, AbilityTag, System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add(AbilityTag);
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        return CountLegalMarkedTargets() > 0;
    }

    protected override void OnTrigger()
    {
        if (owner == null || CountLegalMarkedTargets() <= 0)
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

        Vector3 origin = owner != null ? owner.transform.position + Vector3.up : Vector3.zero;
        for (int i = 0; i < strikes.Count; i++)
        {
            SettleStrike(origin, strikes[i], isFollowUp: false);
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

            Vector3 origin = owner.transform.position + Vector3.up;
            for (int i = 0; i < original.Count; i++)
            {
                MarkedStrike follow = original[i];
                follow.storedDamage = 0f; // follow-ups do not re-consume Mark
                follow.baseDamage = baseThunderDamage * followUpDamageMult;
                SettleStrike(origin, follow, isFollowUp: true);
                if (i + 1 < original.Count)
                    yield return AbilityWait(chainDelay);
            }
        }
    }

    private List<MarkedStrike> CollectStrikes()
    {
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

    private void SettleStrike(Vector3 origin, MarkedStrike strike, bool isFollowUp)
    {
        float damage = strike.baseDamage + (isFollowUp ? 0f : strike.storedDamage);
        SpawnTrajectory(origin, strike.position);
        SpawnHitEffect(strike.position);

        if (strike.target != null && owner != null && owner.CanDamage(strike.target))
            DealDamageTo(strike.target, damage);
        else
        {
            // Follow-up may land after target died; still show VFX at snapshot position.
            CombatHitboxDebug.DrawSphere(drawHitboxes, strike.position, 0.75f, hitEffectDuration);
            Collider[] cols = Physics.OverlapSphere(strike.position, 0.75f);
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

    private float GetEffectiveRange()
    {
        float range = searchRadius;
        if (IsUpgradeUnlocked("EN-TG01"))
            range += GetCardParameter("SpecialRangeBonus", 4f);
        return range;
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

    private void SpawnTrajectory(Vector3 from, Vector3 to)
    {
        if (boltVfxPrefab == null) return;
        Vector3 dir = to - from;
        Quaternion rot = (dir.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(dir.normalized, Vector3.up)
            : Quaternion.identity) * Quaternion.Euler(boltVfxRotationOffset);
        GameObject vfx = SpawnVfxTracked(boltVfxPrefab, from + boltVfxPositionOffset, rot, 0.35f);
        if (vfx == null) return;
        Vector3 scale = vfx.transform.localScale;
        scale.z *= Mathf.Max(0.1f, dir.magnitude);
        vfx.transform.localScale = scale;
    }

    private struct MarkedStrike
    {
        public Enemy target;
        public Vector3 position;
        public float storedDamage;
        public float baseDamage;
    }
}
