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
    [Tooltip("When set, hit settlement goes through the Ability (damage + Effects).")]
    public EnemyAbility sourceAbility;

    [Header("Visual")]
    public GameObject hitEffectPrefab;
    public float hitEffectDuration = 0.5f;

    private float lifetime;
    private float hitCheckInterval = 0.05f;
    private float hitCheckTimer;
    private bool settled;

    void OnEnable()
    {
        ResetForPoolSpawn();
    }

    /// <summary>Clear runtime state before / when leaving the pool. Safe to call while inactive.</summary>
    public void ResetForPoolSpawn()
    {
        lifetime = maxLifetime;
        hitCheckTimer = hitCheckInterval;
        settled = false;
    }

    void Update()
    {
        if (settled) return;

        float deltaTime = isPlayerProjectile ? Time.unscaledDeltaTime : Time.deltaTime;
        float stepDist = speed * deltaTime;
        int obstacleMask = ~((1 << 8) | (1 << 9));
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit wallHit, stepDist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            transform.position = wallHit.point;
            SpawnFallbackHitVfx(wallHit.point, Quaternion.LookRotation(wallHit.normal));
            ReleaseSelf();
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
        if (lifetime <= 0) ReleaseSelf();
    }

    void CheckHit()
    {
        MonsterActor ownerMonster = ownerEnemy as MonsterActor;
        float checkRadius = 0.8f * (ownerMonster != null ? ownerMonster.CombatScaleMultiplier : 1f);
        CombatHitboxDebug.DrawSphere(true, transform.position, checkRadius, 0f);
        var hits = Physics.OverlapSphere(transform.position, checkRadius);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponentInParent<Enemy>();
            if (ownerEnemy != null && ownerEnemy.CanDamage(enemy))
            {
                if (sourceAbility != null) sourceAbility.SettleHit(enemy, damage);
                else DealDamage(enemy);
                OnHit();
                return;
            }

            var playerHealth = hit.GetComponentInParent<PlayerHealth>();
            if (playerHealth != null && ownerEnemy != null && ownerEnemy.CanDamageSoul())
            {
                if (sourceAbility != null) sourceAbility.SettleHit(playerHealth, damage);
                else playerHealth.TakeDamage(damage);
                OnHit();
                return;
            }

            if (sourceAbility == null && isPlayerProjectile && hit.CompareTag("Enemy"))
            {
                if (enemy != null && !enemy.isDowned && !enemy.isPossessed)
                {
                    DealDamage(enemy);
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
        if (sourceAbility == null)
            SpawnFallbackHitVfx(transform.position, Quaternion.identity);
        ReleaseSelf();
    }

    void SpawnFallbackHitVfx(Vector3 position, Quaternion rotation)
    {
        if (hitEffectPrefab == null) return;
        var effect = VfxPool.Instance.Spawn(hitEffectPrefab, position, rotation);
        BulletTimeController.MarkVfxOrigin(effect, isPlayerProjectile);
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>())
            ps.Play(true);
        VfxPool.ReleaseOrDestroy(effect, hitEffectDuration);
    }

    void ReleaseSelf()
    {
        if (settled) return;
        settled = true;
        sourceAbility = null;
        ownerEnemy = null;
        VfxPool.ReleaseOrDestroy(gameObject);
    }
}
