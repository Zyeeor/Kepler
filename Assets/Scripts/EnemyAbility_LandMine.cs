using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Skill: Land Mine. Places a mine at the owner's feet that persists for a duration.
/// When an enemy walks over it, it explodes dealing AoE damage.
/// </summary>
public class EnemyAbility_LandMine : EnemyAbility
{
    [Header("Mine")]
    [Tooltip("Mine prefab to spawn at feet.")]
    public GameObject minePrefab;
    [Tooltip("How long the mine persists before self-destructing.")]
    public float mineDuration = 10f;
    [Tooltip("How many mines can be placed at once (oldest removed if exceeded).")]
    public int maxMines = 3;

    [Header("Explosion")]
    [Tooltip("Blast radius.")]
    public float blastRadius = 3f;
    [Tooltip("Blast VFX prefab (spawned on explosion).")]
    public GameObject blastVfxPrefab;
    [Tooltip("How long the blast VFX lasts.")]
    public float blastVfxDuration = 1f;

    [Header("Damage")]
    public float damageMultiplier = 1.5f;

    [Header("Targeting")]
    [Tooltip("Who triggers the mine. When possessed, hits everything.")]
    public LayerMask targetMask = -1;

    [Header("Animation")]
    public string animTrigger = "Skill";

    private List<GameObject> activeMines = new List<GameObject>();

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "地雷";
        cooldown = cooldown <= 0f ? 5f : cooldown;
    }

    public override bool CanTrigger()
    {
        if (owner.isPossessed)
            return base.CanTrigger();
        return base.CanTrigger() && owner != null && owner.targetPlayer != null;
    }

    protected override void OnTrigger()
    {
        if (owner == null) return;

        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger(animTrigger);

        StartCoroutine(PlaceMineRoutine());
    }

    IEnumerator PlaceMineRoutine()
    {
        // Remove oldest mine if at max
        while (activeMines.Count >= maxMines)
        {
            var oldest = activeMines[0];
            activeMines.RemoveAt(0);
            if (oldest != null) Destroy(oldest);
        }

        // Place mine at owner's feet
        Vector3 pos = owner.transform.position;
        GameObject mine = null;
        if (minePrefab != null)
        {
            mine = Instantiate(minePrefab, pos, Quaternion.identity);
            PlayVfx(mine);
        }
        else
        {
            mine = new GameObject("Mine");
            mine.transform.position = pos;
        }

        activeMines.Add(mine);

        // Start monitoring coroutine
        StartCoroutine(MineLifetime(mine));

        yield return null;
    }

    IEnumerator MineLifetime(GameObject mine)
    {
        float timer = mineDuration;
        int layerMask = owner.isPossessed ? ~0 : targetMask;

        while (timer > 0f && mine != null)
        {
            timer -= Time.deltaTime;

            // Check for enemies in blast radius
            Collider[] hits = Physics.OverlapSphere(mine.transform.position, blastRadius * 0.5f, layerMask, QueryTriggerInteraction.Collide);
            bool triggered = false;
            Vector3 triggerPos = mine.transform.position;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<Enemy>();
                if (enemy != null && enemy != owner && !enemy.isDowned && !enemy.isPossessed)
                {
                    triggered = true;
                    triggerPos = enemy.transform.position;
                    break;
                }
                var ph = hit.GetComponentInParent<PlayerHealth>();
                if (ph != null)
                {
                    triggered = true;
                    triggerPos = ph.transform.position;
                    break;
                }
            }

            if (triggered)
            {
                Explode(mine, triggerPos);
                yield break;
            }

            yield return null;
        }

        // Timeout — just remove silently
        if (mine != null)
        {
            activeMines.Remove(mine);
            Destroy(mine);
        }
    }

    void Explode(GameObject mine, Vector3 pos)
    {
        activeMines.Remove(mine);
        Destroy(mine);

        // Blast VFX
        if (blastVfxPrefab != null)
        {
            var blast = Instantiate(blastVfxPrefab, pos, Quaternion.identity);
            PlayVfx(blast);
            Destroy(blast, blastVfxDuration);
        }

        // AoE damage
        DamageEnemiesInSphere(pos, blastRadius, damage * damageMultiplier, null);

        // Also damage player if in range
        TryDamagePlayerInRadius(pos, blastRadius, damage * damageMultiplier);
    }
}
