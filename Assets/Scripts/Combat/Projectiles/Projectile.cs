using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 10f;
    public float maxLifetime = 5f;

    [Header("Damage")]
    public float damage = 5f;
    public bool isPlayerProjectile = true;
    [Tooltip("Who fired this projectile (used for burn/lifesteal passives).")]
    public Enemy ownerEnemy;

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
        float deltaTime = ownerEnemy != null && ownerEnemy.IsPlayerControlled ? Time.unscaledDeltaTime : Time.deltaTime;
        float stepDist = speed * deltaTime;
        int obstacleMask = ~((1 << 8) | (1 << 9));
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit wallHit, stepDist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            transform.position = wallHit.point;
            if (hitEffectPrefab != null)
            {
                var effect = Instantiate(hitEffectPrefab, wallHit.point, Quaternion.LookRotation(wallHit.normal));
                Destroy(effect, hitEffectDuration);
            }
            Destroy(gameObject);
            return;
        }

        transform.position += transform.forward * stepDist;

        hitCheckTimer -= deltaTime;
        if (hitCheckTimer <= 0)
        {
            hitCheckTimer = hitCheckInterval;
            CheckHit();
        }

        lifetime -= deltaTime;
        if (lifetime <= 0) Destroy(gameObject);
    }

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
                    DealDamage(enemy);
                    OnHit();
                    return;
                }
            }
            if (!isPlayerProjectile && hit.CompareTag("Player"))
            {
                var playerHealth = hit.GetComponent<PlayerHealth>();
                if (playerHealth != null && (ownerEnemy == null || ownerEnemy.CanDamageSoul()))
                {
                    playerHealth.TakeDamage(damage);
                    OnHit();
                    return;
                }
            }
        }
    }


    void DealDamage(Enemy enemy)
    {
        float dmg = damage;
        if (PlayerPassiveManager.Instance != null)
            dmg *= (1f + PlayerPassiveManager.Instance.GetDamageAmp());

        if (ownerEnemy != null)
        {
            ownerEnemy.ApplyOffensiveDamage(enemy, dmg);
        }
        else
        {
            enemy.TakeDamage(dmg);
            if (PlayerPassiveManager.Instance != null)
            {
                float burnPct = PlayerPassiveManager.Instance.GetBurnPercent();
                if (burnPct > 0f && enemy.GetComponent<BurnEffect>() == null)
                {
                    var burn = enemy.gameObject.AddComponent<BurnEffect>();
                    burn.Init(enemy, burnPct, 3f, 0.5f, PlayerPassiveManager.Instance.GetBurnVfxPrefab());
                }
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
        Destroy(gameObject);
    }
}
