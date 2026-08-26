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
    // Fixed-capacity (64 colliders), instance-local so nested mine callbacks cannot overwrite
    // another query's results. Returned slots only are processed.
    private readonly Collider[] overlapBuffer = new Collider[64];

    void Update()
    {
        CombatHitboxDebug.DrawSphere(drawHitboxes, transform.position, triggerRadius, 0f);
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position, triggerRadius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null) continue;
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

    void Explode(Vector3 ignoredTargetPos)
    {
        Vector3 blastPos = transform.position;
        CombatHitboxDebug.DrawSphere(drawHitboxes, blastPos, blastRadius, blastVfxDuration);
        if (blastVfxPrefab != null)
        {
            var blast = VfxPool.Instance.Spawn(blastVfxPrefab, blastPos, Quaternion.identity);
            BulletTimeController.MarkVfxOrigin(blast, placer != null && placer.IsPlayerControlled);
            foreach (var ps in blast.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (main.loop) main.loop = false;
                ps.Play(true);
            }
            VfxPool.ReleaseOrDestroy(blast, Mathf.Max(0.01f, blastVfxDuration));
        }

        int hitCount = Physics.OverlapSphereNonAlloc(
            blastPos, blastRadius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null) continue;
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
