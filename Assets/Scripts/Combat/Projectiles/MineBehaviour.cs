using UnityEngine;

/// <summary>
/// Standalone mine that persists after its placer dies/unpossesses.
/// Attached to the mine GameObject when placed.
/// </summary>
public class MineBehaviour : MonoBehaviour
{
    public float lifetime = 10f;
    public float triggerRadius = 1.5f;
    public float blastRadius = 3f;
    public float damage;
    public Enemy placer;
    public GameObject blastVfxPrefab;
    public float blastVfxDuration = 1f;
    public System.Action<GameObject> onExplode;
    [Tooltip("Draw trigger and blast ranges when CombatHitboxDebug.Enabled is true.")]
    public bool drawHitboxes;

    void Update()
    {
        CombatHitboxDebug.DrawSphere(drawHitboxes, transform.position, triggerRadius);
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, triggerRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null && enemy != placer && !enemy.isDowned)
            {
                if (placer != null && !placer.CanDamage(enemy)) continue;
                Explode(enemy.transform.position);
                return;
            }
            var ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph != null && (placer == null || placer.CanDamageSoul()))
            {
                Explode(ph.transform.position);
                return;
            }
        }
    }

    void Explode(Vector3 pos)
    {
        CombatHitboxDebug.DrawSphere(drawHitboxes, pos, blastRadius);
        if (blastVfxPrefab != null)
        {
            var blast = Instantiate(blastVfxPrefab, pos, Quaternion.identity);
            foreach (var ps in blast.GetComponentsInChildren<ParticleSystem>()) ps.Play();
            Destroy(blast, blastVfxDuration);
        }

        var allHits = Physics.OverlapSphere(pos, blastRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var hit in allHits)
        {
            var enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null && enemy != placer && !enemy.isDowned)
            {
                if (placer != null && !placer.CanDamage(enemy)) continue;
                if (placer != null)
                    placer.ApplyOffensiveDamage(enemy, damage);
                else
                    enemy.TakeDamage(damage);
            }
            var ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph != null && (placer == null || placer.CanDamageSoul()))
            {
                ph.TakeDamage(damage);
            }
        }

        onExplode?.Invoke(gameObject);
        Destroy(gameObject);
    }
}
