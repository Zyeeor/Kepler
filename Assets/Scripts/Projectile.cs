using UnityEngine;

/// <summary>
/// A projectile that flies forward, deals damage to the first enemy it hits, and destroys itself.
/// Uses both OnTriggerEnter AND manual distance check for reliable hit detection.
/// </summary>
public class Projectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 10f;
    public float maxLifetime = 5f;

    [Header("Damage")]
    public float damage = 5f;
    public bool isPlayerProjectile = true;

    [Header("Visual")]
    public GameObject hitEffectPrefab;
    public float hitEffectDuration = 0.5f;

    private float lifetime;
    private float hitCheckInterval = 0.05f;
    private float hitCheckTimer;

    void Start()
    {
        lifetime = maxLifetime;
        hitCheckTimer = hitCheckInterval;
    }

    void Update()
    {
        // Fly forward
        transform.position += transform.forward * speed * Time.deltaTime;

        // Manual distance-based hit detection (works regardless of collider trigger settings)
        hitCheckTimer -= Time.deltaTime;
        if (hitCheckTimer <= 0)
        {
            hitCheckTimer = hitCheckInterval;
            CheckHit();
        }

        // Lifetime timeout
        lifetime -= Time.deltaTime;
        if (lifetime <= 0)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Manual distance check against all enemies — reliable regardless of collider config
    /// </summary>
    void CheckHit()
    {
        float checkRadius = 0.8f;
        var hits = Physics.OverlapSphere(transform.position, checkRadius);

        foreach (var hit in hits)
        {
            if (isPlayerProjectile && hit.CompareTag("Enemy"))
            {
                var enemy = hit.GetComponent<Enemy>();
                if (enemy != null && !enemy.isDowned && !enemy.isPossessed)
                {
                    enemy.TakeDamage(damage);
                    Debug.Log("[Projectile] Hit enemy " + enemy.name + " for " + damage);
                    OnHit();
                    return;
                }
            }

            if (!isPlayerProjectile && hit.CompareTag("Player"))
            {
                var playerHealth = hit.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage);
                    Debug.Log("[Projectile] Hit player for " + damage);
                    OnHit();
                    return;
                }
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Backup trigger detection
        if (isPlayerProjectile && other.CompareTag("Enemy"))
        {
            var enemy = other.GetComponent<Enemy>();
            if (enemy != null && !enemy.isDowned && !enemy.isPossessed)
            {
                enemy.TakeDamage(damage);
                Debug.Log("[Projectile] Hit enemy " + enemy.name + " for " + damage);
                OnHit();
            }
        }
    }

    void OnHit()
    {
        if (hitEffectPrefab != null)
        {
            var effect = Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, hitEffectDuration);
        }
        // No hit effect prefab — skip fallback VFX

        Destroy(gameObject);
    }


}
