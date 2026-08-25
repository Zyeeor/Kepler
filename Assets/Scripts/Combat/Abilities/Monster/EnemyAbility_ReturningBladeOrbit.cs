using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Greed prototype: orbiting blades damage repeatedly, then return to the owner and can split after kills.</summary>
public class EnemyAbility_ReturningBladeOrbit : EnemyAbility
{
    public GameObject bladePrefab;
    public int bladeCount = 3;
    public float orbitRadius = 2f;
    public float orbitDuration = 5f;
    public float orbitSpeed = 240f;
    public float hitRadius = 0.5f;
    public float hitInterval = 0.25f;
    public int maxSplitCount = 12;
    public float returnSpeed = 18f;

    private readonly List<GameObject> blades = new List<GameObject>();

    private void OnEnable()
    {
        type = AbilityType.Skill;
        cooldown = cooldown <= 0f ? 6f : cooldown;
    }

    protected override void OnTrigger()
    {
        StartCoroutine(BladeRoutine());
    }

    private IEnumerator BladeRoutine()
    {
        int initialCount = Mathf.RoundToInt(GetCardParameter("BladeCount", bladeCount));
        for (int i = 0; i < initialCount; i++)
            CreateBlade(i);

        float elapsed = 0f;
        float nextHit = 0f;
        while (owner != null && elapsed < orbitDuration)
        {
            elapsed += AbilityDeltaTime;
            for (int i = blades.Count - 1; i >= 0; i--)
            {
                if (blades[i] == null) { blades.RemoveAt(i); continue; }
                float angle = elapsed * orbitSpeed + i * 360f / Mathf.Max(1, blades.Count);
                float scale = OwnerCombatScaleMultiplier;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * orbitRadius * scale;
                blades[i].transform.position = owner.transform.position + Vector3.up * scale + offset;
            }
            if (elapsed >= nextHit)
            {
                nextHit = elapsed + hitInterval;
                foreach (GameObject blade in blades)
                {
                    if (blade == null) continue;
                    float effectiveHitRadius = ScaleAbilityRadius(hitRadius);
                    CombatHitboxDebug.DrawSphere(drawHitboxes, blade.transform.position, effectiveHitRadius, 0f);
                    Collider[] hits = Physics.OverlapSphere(blade.transform.position, effectiveHitRadius, ~0, QueryTriggerInteraction.Collide);
                    foreach (Collider hit in hits)
                    {
                        Enemy enemy = hit.GetComponentInParent<Enemy>();
                        if (owner.CanDamage(enemy)) DealDamageTo(enemy, damage);
                    }
                }
            }
            yield return null;
        }
        foreach (GameObject blade in blades)
            if (blade != null) StartCoroutine(ReturnBlade(blade));
        blades.Clear();
        EndActivationEffect();
    }

    private void CreateBlade(int index)
    {
        if (bladePrefab == null || blades.Count >= maxSplitCount) return;
        GameObject blade = SpawnVfxTracked(bladePrefab, owner.transform.position + Vector3.up, Quaternion.identity);
        blades.Add(blade);
    }

    private IEnumerator ReturnBlade(GameObject blade)
    {
        while (blade != null && owner != null && Vector3.Distance(blade.transform.position, owner.transform.position) > 0.25f)
        {
            blade.transform.position = Vector3.MoveTowards(blade.transform.position, owner.transform.position, returnSpeed * AbilityDeltaTime);
            yield return null;
        }
        if (blade != null) Destroy(blade);
    }
}
