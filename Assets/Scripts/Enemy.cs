using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    [Header("Config")]
    public EnemyConfig config;

    [Header("Detection")]
    public float detectionRadius = 8f;

    [Header("Runtime Stats")]
    public float currentHealth;
    public float currentTenacity;

    [Header("State")]
    public bool isWeakened = false;
    public bool isDowned = false;
    public bool isPossessed = false;
    public bool playerDetected = false;

    [Header("UI")]
    public Slider healthSlider;
    public Canvas healthCanvas;

    private Transform player;
    private Renderer meshRenderer;
    private Rigidbody rb;
    private Color originalColor;
    private Color downedColor = new Color(0.3f, 0.3f, 0.3f);

    void Awake()
    {
        meshRenderer = GetComponent<Renderer>();
        rb = GetComponent<Rigidbody>();
        ApplyConfig();
    }

    void Start()
    {
        // Set layer and tag for collision control
        gameObject.layer = 8;  // Enemy layer
        gameObject.tag = "Enemy";

        // Ensure BoxCollider (non-trigger) for physical collision between enemies
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null)
            col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = false;
        col.enabled = true;

        // Ensure Rigidbody for physics collision
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        UpdateHealthUI();
    }

    void Update()
    {
        if (isDowned || isWeakened || isPossessed) return;

        if (player != null)
        {
            Vector3 dir = player.position - transform.position;
            dir.y = 0;
            float dist = dir.magnitude;

            // Check if player entered detection range
            if (dist <= detectionRadius)
            {
                playerDetected = true;
            }
            else
            {
                playerDetected = false;
            }

            // Only chase when player is detected
            if (playerDetected && dist > 0.01f)
            {
                dir.Normalize();
                float speed = config != null ? config.moveSpeed : 1.5f;

                if (rb != null)
                {
                    Vector3 targetPos = rb.position + dir * speed * Time.deltaTime;
                    targetPos.y = rb.position.y;
                    rb.MovePosition(targetPos);
                }
                else
                {
                    Vector3 newPos = transform.position + dir * speed * Time.deltaTime;
                    newPos.y = transform.position.y;
                    transform.position = newPos;
                }

                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
        }

        // Billboard health bar to face camera
        if (healthCanvas != null && Camera.main != null)
            healthCanvas.transform.LookAt(healthCanvas.transform.position + Camera.main.transform.forward);
    }

    public void ApplyConfig()
    {
        if (config != null)
        {
            currentHealth = config.maxHealth;
            currentTenacity = config.maxHealth;
            originalColor = config.enemyColor;
            if (meshRenderer != null)
                meshRenderer.material.color = originalColor;
        }
        else
        {
            currentHealth = 100f;
            currentTenacity = 100f;
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDowned || isPossessed) return;
        currentHealth -= amount;
        currentTenacity -= amount;
        FlashDamage();
        UpdateHealthUI();

        if (currentTenacity <= 0)
        {
            currentTenacity = 0;
            BecomeWeakened();
        }

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    void BecomeWeakened()
    {
        isWeakened = true;
        if (meshRenderer != null && config != null)
            meshRenderer.material.color = config.weakenedColor;
        Debug.Log(name + " is weakened!");
    }

    public void OnPossessed()
    {
        isPossessed = true;
        isDowned = false;
        // Player has copied the enemy's model, so hide enemy visuals
        if (meshRenderer != null)
            meshRenderer.enabled = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.enabled = false;
        // Disable colliders so player doesn't collide with possessed body
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;
        if (healthCanvas != null)
            healthCanvas.gameObject.SetActive(false);
        Debug.Log(name + " has been possessed!");
    }

    public void Execute()
    {
        var playerHealth = PlayerHealth.Instance;
        if (playerHealth != null)
            playerHealth.PossessEnemy(this);
        else
            Debug.LogWarning("PlayerHealth.Instance is null");
    }

    void Die()
    {
        isDowned = true;
        if (meshRenderer != null)
            meshRenderer.material.color = downedColor;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
        }

        transform.rotation = Quaternion.Euler(90, transform.rotation.eulerAngles.y, 0);

        if (healthCanvas != null)
            healthCanvas.gameObject.SetActive(true);
        UpdateHealthUI();

        Debug.Log(name + " is downed! Press R to possess.");
    }

    void FlashDamage()
    {
        if (meshRenderer != null)
            StartCoroutine(FlashRoutine());
    }

    System.Collections.IEnumerator FlashRoutine()
    {
        meshRenderer.material.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        if (!isWeakened && !isPossessed && !isDowned)
            meshRenderer.material.color = config != null ? config.enemyColor : originalColor;
        else if (isDowned)
            meshRenderer.material.color = downedColor;
        else if (isWeakened)
            meshRenderer.material.color = config != null ? config.weakenedColor : originalColor;
    }

    public void UpdateHealthUI()
    {
        if (healthSlider != null && config != null)
        {
            healthSlider.maxValue = config.maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isDowned || isPossessed) return;
        if (collision.gameObject.CompareTag("Player"))
        {
            var ph = collision.gameObject.GetComponent<PlayerHealth>();
            if (ph != null && config != null)
                ph.TakeDamage(config.damage * 0.5f);
            Debug.Log("Enemy " + name + " hit player!");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}