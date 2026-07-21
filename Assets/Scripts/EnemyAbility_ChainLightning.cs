using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Skill: Chain Lightning. Auto-aims: first bolt from owner to nearest enemy,
/// then chains to nearby enemies. Uses VFX prefab for trajectory.
/// </summary>
public class EnemyAbility_ChainLightning : EnemyAbility
{
    [Header("Chain Lightning")]
    public int chainCount = 4;
    public float chainRange = 10f;
    public float chainDamage = 50f;
    public float chainDelay = 0.15f;
    public float searchRadius = 15f;

    [Header("Trajectory Visual")]
    [Tooltip("VFX prefab spawned at 'from' position, oriented from→to, for each chain link. Required.")]
    public GameObject boltVfxPrefab;
    public Vector3 boltVfxPositionOffset = Vector3.zero;
    public Vector3 boltVfxRotationOffset = Vector3.zero;

    [Header("Hit VFX")]
    public GameObject hitEffectPrefab;
    public float hitEffectDuration = 0.5f;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "Chain Lightning";
    }

    public override bool CanTrigger()
    {
        if (owner.isPossessed) return base.CanTrigger();
        return base.CanTrigger() && owner != null && owner.targetPlayer != null;
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;
        StartCoroutine(ChainLightningRoutine());
    }

    IEnumerator ChainLightningRoutine()
    {
        Vector3 origin = owner.transform.position + Vector3.up * 1f;

        if (owner.isPossessed)
        {
            var allEnemies = new List<Enemy>();
            foreach (var obj in GameObject.FindGameObjectsWithTag("Enemy"))
            {
                var e = obj.GetComponent<Enemy>();
                if (e != null && e != owner && !e.isDowned && !e.isPossessed)
                    allEnemies.Add(e);
            }

            var hitSet = new HashSet<Enemy>();
            Enemy currentTarget = null;
            float nearestDist = float.MaxValue;
            foreach (var e in allEnemies)
            {
                float d = Vector3.Distance(origin, e.transform.position);
                if (d <= searchRadius && d < nearestDist) { nearestDist = d; currentTarget = e; }
            }

            Vector3 lastPos = origin;
            int chainIdx = 0;

            while (chainIdx < chainCount && currentTarget != null)
            {
                hitSet.Add(currentTarget);
                DealDamageTo(currentTarget, chainDamage);

                Vector3 hitPos = currentTarget.transform.position + Vector3.up * 1f;
                Debug.Log($"[ChainLightning] Chain {chainIdx}: {lastPos} -> {hitPos} (target: {currentTarget.name})");
                SpawnHitEffect(hitPos);
                SpawnTrajectory(lastPos, hitPos, chainIdx);

                Enemy nextTarget = null;
                float minDist = float.MaxValue;
                foreach (var e in allEnemies)
                {
                    if (hitSet.Contains(e)) continue;
                    float d = Vector3.Distance(currentTarget.transform.position, e.transform.position);
                    if (d <= chainRange && d < minDist) { minDist = d; nextTarget = e; }
                }

                lastPos = hitPos;
                currentTarget = nextTarget;
                chainIdx++;

                if (currentTarget != null)
                {
                    Debug.Log($"[ChainLightning] Next chain to: {currentTarget.name} at distance {minDist}");
                    yield return new WaitForSeconds(chainDelay);
                }
                else
                {
                    Debug.Log($"[ChainLightning] No more targets in chainRange={chainRange}");
                }
            }
            Debug.Log($"[ChainLightning] Total chains: {chainIdx}");
        }
        else
        {
            if (owner.targetPlayer == null) yield break;
            var ph = owner.targetPlayer.GetComponent<PlayerHealth>();
            if (ph == null) yield break;

            DealDamageToPlayer(ph, chainDamage);
            Vector3 hitPos = owner.targetPlayer.position + Vector3.up * 1f;
            SpawnHitEffect(hitPos);
            SpawnTrajectory(origin, hitPos, 0);
        }
    }

    void SpawnTrajectory(Vector3 from, Vector3 to, int index)
    {
        if (boltVfxPrefab == null) return;

        Vector3 dir = to - from;
        float distance = dir.magnitude;

        // Spawn at receiver (to), looking back toward sender (from)
        Vector3 pos = to + boltVfxPositionOffset;
        Quaternion rot = (dir.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(dir, Vector3.up)
            : Quaternion.identity) * Quaternion.Euler(boltVfxRotationOffset);

        GameObject vfx = Instantiate(boltVfxPrefab, pos, rot);
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.loop = false;
            main.startLifetime = main.startLifetime.constant * distance;
            ps.Play();
        }
        // Destroy after the longest particle lifetime, capped at chainDelay so it cleans up before next chain
        Destroy(vfx, chainDelay + 0.1f);
    }

    void SpawnHitEffect(Vector3 position)
    {
        if (hitEffectPrefab == null) return;
        GameObject effect = Instantiate(hitEffectPrefab, position, Quaternion.identity);
        PlayVfx(effect);
        Destroy(effect, hitEffectDuration);
    }
}
