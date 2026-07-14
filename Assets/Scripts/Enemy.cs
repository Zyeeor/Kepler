using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Self-contained enemy. All stats and abilities are configured directly on the prefab
/// (no more EnemyConfig ScriptableObject). Attach EnemyAbility components for behaviors.
/// </summary>
public class Enemy : MonoBehaviour
{
    [Header("Identity")]
    public string displayName = "Enemy";

    [Header("Stats (configured on prefab)")]
    public float maxHealth = 200f;
    public float maxTenacity = 200f;
    public float moveSpeed = 50f;
    public float damage = 30f;
    public float attackRange = 1.5f;
    public float detectionRadius = 8f;

    [Header("Visual")]
    public Color bodyColor = Color.red;
    public Color weakenedColor = new Color(1f, 0.5f, 0f);
    public Color downedColor = new Color(0.3f, 0.3f, 0.3f);
    public Color flashColor = Color.red;
    public float flashDuration = 0.1f;

    [Header("State")]
    public bool isWeakened = false;
    public bool isDowned = false;
    public bool isPossessed = false;
    public bool playerDetected = false;

    [Header("UI")]
    public Slider healthSlider;
    public Canvas healthCanvas;
    [Tooltip("Show health bar above this enemy. Can be toggled in Inspector.")]
    public bool showHealthBar = true;
    public static bool ShowHealthBars = true;

    [Header("Abilities (auto-discovered)")]
    public List<EnemyAbility> abilities = new List<EnemyAbility>();
    public List<EnemyAbility> basicAbilities = new List<EnemyAbility>();
    public List<EnemyAbility> skillAbilities = new List<EnemyAbility>();

    public float currentHealth;
    public float currentTenacity;

    [Header("AI Targets (read-only)")]
    public Transform targetPlayer;
    public Enemy targetEnemy;

    [Header("Body Type")]
    public BodyType bodyType = BodyType.HugeMuscular;
    public string weaponDescription = "双手锁链";
    public float attackDowntime = 10f;

    public enum BodyType { Slim, Medium, Large, HugeMuscular, Boss }

    public Renderer meshRenderer;
    public Rigidbody rb;
    private Color originalColor;
    private float aiTimer;
    private float skillTimer;

    void Awake()
    {
        meshRenderer = GetComponent<Renderer>();
        rb = GetComponent<Rigidbody>();
        currentHealth = maxHealth;
        currentTenacity = maxTenacity;
        originalColor = bodyColor;
        if (meshRenderer != null) meshRenderer.material.color = originalColor;

        var found = GetComponentsInChildren<EnemyAbility>(true);
        abilities.Clear(); basicAbilities.Clear(); skillAbilities.Clear();
        foreach (var a in found)
        {
            if (!abilities.Contains(a)) abilities.Add(a);
            if (a.type == EnemyAbility.AbilityType.BasicAttack && !basicAbilities.Contains(a)) basicAbilities.Add(a);
            else if (a.type == EnemyAbility.AbilityType.Skill && !skillAbilities.Contains(a)) skillAbilities.Add(a);
        }
    }

    public void RegisterAbility(EnemyAbility a)
    {
        if (a == null) return;
        if (!abilities.Contains(a)) abilities.Add(a);
        if (a.type == EnemyAbility.AbilityType.BasicAttack && !basicAbilities.Contains(a)) basicAbilities.Add(a);
        else if (a.type == EnemyAbility.AbilityType.Skill && !skillAbilities.Contains(a)) skillAbilities.Add(a);
    }

    void Start()
    {
        gameObject.layer = 8;
        gameObject.tag = "Enemy";
        BoxCollider col = GetComponent<BoxCollider>();
        if (col == null) col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = false; col.enabled = true;
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false; rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        var p = GameObject.FindGameObjectWithTag("Player");
        targetPlayer = p != null ? p.transform : null;
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(ShowHealthBars);
        UpdateHealthUI();
    }

    void Update()
    {
        if (isDowned || isWeakened || isPossessed) return;
        if (targetPlayer == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            targetPlayer = p != null ? p.transform : null;
        }
        if (targetPlayer != null)
        {
            Vector3 dir = targetPlayer.position - transform.position;
            dir.y = 0;
            float dist = dir.magnitude;
            playerDetected = dist <= detectionRadius;
            if (playerDetected && dist > 0.01f)
            {
                dir.Normalize();
                if (rb != null)
                {
                    Vector3 targetPos = rb.position + dir * moveSpeed * Time.deltaTime;
                    targetPos.y = rb.position.y;
                    rb.MovePosition(targetPos);
                }
                else
                {
                    Vector3 newPos = transform.position + dir * moveSpeed * Time.deltaTime;
                    newPos.y = transform.position.y;
                    transform.position = newPos;
                }
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
                if (dist <= attackRange)
                {
                    aiTimer -= Time.deltaTime;
                    if (aiTimer <= 0f) { TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.BasicAttack); aiTimer = 0.5f; }
                }
                skillTimer -= Time.deltaTime;
                if (skillTimer <= 0f) { if (TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.Skill)) skillTimer = 10f; }
            }
        }
    }

    bool TryTriggerAbilitiesOfType(EnemyAbility.AbilityType t)
    {
        bool any = false;
        var list = t == EnemyAbility.AbilityType.BasicAttack ? basicAbilities : skillAbilities;
        foreach (var a in list) { if (a != null && a.CanTrigger()) { a.Trigger(); any = true; } }
        return any;
    }

    void LateUpdate()
    {
        if (healthCanvas != null)
        {
            bool shouldShow = showHealthBar && ShowHealthBars && !isPossessed;
            if (healthCanvas.gameObject.activeSelf != shouldShow) healthCanvas.gameObject.SetActive(shouldShow);
        }
        if (healthCanvas != null && healthCanvas.gameObject.activeSelf && Camera.main != null)
        {
            var cam = Camera.main;
            healthCanvas.transform.rotation = Quaternion.LookRotation(cam.transform.position - healthCanvas.transform.position, Vector3.up);
            Vector3 vp = cam.WorldToViewportPoint(transform.position + Vector3.up * 2f);
            if (vp.z < 0f) { healthCanvas.gameObject.SetActive(false); return; }
            float yClampMin = 0.05f, yClampMax = 0.95f;
            float yOffset = 0f;
            if (vp.y > yClampMax) yOffset = -(vp.y - yClampMax) * cam.orthographicSize * 2f;
            else if (vp.y < yClampMin) yOffset = (yClampMin - vp.y) * cam.orthographicSize * 2f;
            if (yOffset != 0f) { var p = healthCanvas.transform.position; healthCanvas.transform.position = new Vector3(p.x, p.y + yOffset, p.z); }
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDowned || isPossessed) return;
        currentHealth -= amount;
        currentTenacity -= amount;
        FlashDamage();
        UpdateHealthUI();
        if (currentTenacity <= 0) { currentTenacity = 0; BecomeWeakened(); }
        if (currentHealth <= 0) { currentHealth = 0; Die(); }
    }

    public void OnDealtDamage(float amount)
    {
        foreach (var a in abilities) { if (a is EnemyAbility_Lifesteal ls) ls.OnOwnerDealtDamage(amount); }
    }

    public void ApplyOffensiveDamage(Enemy target, float amount)
    {
        if (target == null || amount <= 0f) return;
        target.TakeDamage(amount);
        OnDealtDamage(amount);
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealthUI();
    }

    void BecomeWeakened()
    {
        isWeakened = true;
        if (meshRenderer != null) meshRenderer.material.color = weakenedColor;
        Debug.Log(name + " is weakened!");
    }

    public void OnPossessed()
    {
        isPossessed = true;
        isDowned = false;
        if (meshRenderer != null) meshRenderer.enabled = false;
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(false);
        Debug.Log(name + " has been possessed!");
    }

    public void Execute()
    {
        var ph = PlayerHealth.Instance;
        if (ph != null) ph.PossessEnemy(this);
        else Debug.LogWarning("PlayerHealth.Instance is null");
    }

    void Die()
    {
        isDowned = true;
        if (meshRenderer != null) meshRenderer.material.color = downedColor;
        if (rb != null) { rb.velocity = Vector3.zero; rb.isKinematic = true; }
        transform.rotation = Quaternion.Euler(90, transform.rotation.eulerAngles.y, 0);
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(true);
        UpdateHealthUI();
        Debug.Log(name + " is downed! Press R to possess.");
    }

    void FlashDamage()
    {
        if (meshRenderer != null) StartCoroutine(FlashRoutine());
    }

    System.Collections.IEnumerator FlashRoutine()
    {
        Color orig = meshRenderer.material.color;
        meshRenderer.material.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        if (meshRenderer == null) yield break;
        if (isDowned) meshRenderer.material.color = downedColor;
        else if (isWeakened) meshRenderer.material.color = weakenedColor;
        else meshRenderer.material.color = bodyColor;
    }

    public void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isDowned || isPossessed) return;
        if (collision.gameObject.CompareTag("Player"))
        {
            var ph = collision.gameObject.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(damage);
            Debug.Log("Enemy " + name + " hit player!");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
