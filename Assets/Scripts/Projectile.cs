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
        else
        {
            CreateDefaultHitEffect();
        }

        Destroy(gameObject);
    }

    void CreateDefaultHitEffect()
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.position = transform.position;
        sphere.transform.localScale = Vector3.one * 0.4f;

        var renderer = sphere.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = new Color(0.3f, 0.7f, 1f, 0.8f);
            mat.SetInt("_Surface", 1);
            mat.SetInt("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            renderer.material = mat;
        }

        var collider = sphere.GetComponent<Collider>();
        if (collider != null) Destroy(collider);

        Destroy(sphere, 0.3f);
    }
}
