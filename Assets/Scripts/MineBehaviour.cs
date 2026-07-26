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

    void Update()
    {
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
                if (placer != null && placer.isPossessed && enemy.isPossessed) continue;
                Explode(enemy.transform.position);
                return;
            }
            var ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph != null && (placer == null || !placer.isPossessed))
            {
                Explode(ph.transform.position);
                return;
            }
        }
    }

    void Explode(Vector3 pos)
    {
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
                if (placer != null && placer.isPossessed && enemy.isPossessed) continue;
                if (placer != null)
                    placer.ApplyOffensiveDamage(enemy, damage);
                else
                    enemy.TakeDamage(damage);
            }
            var ph = hit.GetComponentInParent<PlayerHealth>();
            if (ph != null && (placer == null || !placer.isPossessed))
            {
                ph.TakeDamage(damage);
            }
        }

        onExplode?.Invoke(gameObject);
        Destroy(gameObject);
    }
}
