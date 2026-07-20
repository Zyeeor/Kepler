using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Enemy : MonoBehaviour
{
    [Header("Identity")]
    public string displayName = "Enemy";

    [Header("Stats (configured on prefab)")]
    public float maxHealth = 200f;
    public float maxTenacity = 200f;
    public float moveSpeed = 50f;
    [Tooltip("Base collision damage when touching the player. Individual ability damage is configured on each EnemyAbility.")]
    public float collisionDamage = 30f;
    public float detectionRadius = 8f;
    [Tooltip("AI will attempt basic attacks when within this range of the player.")]
    public float aiAttackRange = 3f;
    [Tooltip("AI will stop moving closer when within this distance of the player.")]
    public float aiMinRange = 0f;
    [Tooltip("Attack speed multiplier. 1.0 = normal speed. Higher = faster attack cooldown.")]
    public float attackSpeed = 1.0f;

    [Header("Visual")]
    public Color bodyColor = Color.red;
    public Color weakenedColor = new Color(1f, 0.5f, 0f);
    public Color downedColor = new Color(0.3f, 0.3f, 0.3f);
    public Color possessedColor = new Color(0.8f, 0.2f, 1f);
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

    [Header("Abilities (auto-discovered from children)")]
    [Tooltip("Basic abilities = left-click when possessing this enemy")]
    public List<EnemyAbility> basicAbilities = new List<EnemyAbility>();
    [Tooltip("Skill abilities = right-click when possessing this enemy")]
    public List<EnemyAbility> skillAbilities = new List<EnemyAbility>();
    [Tooltip("Passive effects (always active, e.g. lifesteal)")]
    public List<EnemyAbility> passiveAbilities = new List<EnemyAbility>();

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
    private bool savedKinematic;

    void Awake()
    {
        meshRenderer = GetComponent<Renderer>();
        rb = GetComponent<Rigidbody>();
        currentHealth = maxHealth;
        currentTenacity = maxTenacity;
        originalColor = bodyColor;
        if (meshRenderer != null) meshRenderer.material.color = originalColor;

        var found = GetComponentsInChildren<EnemyAbility>(true);
        passiveAbilities.Clear(); basicAbilities.Clear(); skillAbilities.Clear();
        foreach (var a in found)
        {
            if (a.type == EnemyAbility.AbilityType.BasicAttack && !basicAbilities.Contains(a)) basicAbilities.Add(a);
            else if (a.type == EnemyAbility.AbilityType.Skill && !skillAbilities.Contains(a)) skillAbilities.Add(a);
            else if (a.type == EnemyAbility.AbilityType.Passive && !passiveAbilities.Contains(a)) passiveAbilities.Add(a);
        }
    }

    public void RegisterAbility(EnemyAbility a)
    {
        if (a == null) return;
        if (a.type == EnemyAbility.AbilityType.BasicAttack && !basicAbilities.Contains(a)) basicAbilities.Add(a);
        else if (a.type == EnemyAbility.AbilityType.Skill && !skillAbilities.Contains(a)) skillAbilities.Add(a);
        else if (a.type == EnemyAbility.AbilityType.Passive && !passiveAbilities.Contains(a)) passiveAbilities.Add(a);
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
        if (isPossessed) return;
        if (isDowned || isWeakened) return;

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
                // Stop moving if within minimum range
                if (dist > aiMinRange)
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
                }
                // Attack if within attack range
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

    public void PlayerTriggerBasicAttack()
    {
        TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.BasicAttack);
    }

    public void PlayerTriggerSkill()
    {
        TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.Skill);
    }

    void LateUpdate()
    {
        if (healthCanvas != null)
        {
            bool shouldShow = showHealthBar && ShowHealthBars && !isPossessed;
            if (healthCanvas.gameObject.activeSelf != shouldShow) healthCanvas.gameObject.SetActive(shouldShow);
        }
        if (healthCanvas != null && healthCanvas.gameObject.activeSelf)
        {
            // Keep healthbar rotation fixed in world space (XZ-plane only, always upright)
            healthCanvas.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDowned) return;
        if (isPossessed)
        {
            currentHealth -= amount;
            FlashDamage();
            UpdateHealthUI();
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                var ph = PlayerHealth.Instance;
                if (ph != null && ph.possessedEnemy == this) ph.OnPossessedEnemyDied();
            }
            return;
        }
        currentHealth -= amount;
        currentTenacity -= amount;
        FlashDamage();
        UpdateHealthUI();
        if (currentTenacity <= 0) { currentTenacity = 0; BecomeWeakened(); }
        if (currentHealth <= 0) { currentHealth = 0; Die(); }
    }

    public void OnDealtDamage(float amount)
    {
        // Enemy-specific lifesteal passive (e.g. from prefab)
        foreach (var a in passiveAbilities) { if (a is EnemyAbility_Lifesteal ls) ls.OnOwnerDealtDamage(amount); }

        // Global player passive lifesteal (accumulated from possessed enemies)
        if (isPossessed && PlayerPassiveManager.Instance != null)
        {
            float lifesteal = PlayerPassiveManager.Instance.GetLifestealMultiplier();
            if (lifesteal > 0f)
            {
                float heal = amount * lifesteal;
                Heal(heal);
            }
        }
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
    }

    public void OnPossessed()
    {
        isPossessed = true;
        isDowned = false;
        isWeakened = false;
        gameObject.tag = "Player";
        if (meshRenderer != null) { meshRenderer.enabled = true; meshRenderer.material.color = possessedColor; }
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = true;
        if (rb != null) { savedKinematic = rb.isKinematic; rb.isKinematic = true; rb.velocity = Vector3.zero; rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeRotation; }
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(false);
        currentHealth = maxHealth;
        currentTenacity = maxTenacity;
        UpdateHealthUI();
    }

    public void OnUnpossessed()
    {
        isPossessed = false;
        gameObject.tag = "Enemy";
    }

    public void Execute()
    {
        var ph = PlayerHealth.Instance;
        if (ph != null) ph.PossessEnemy(this);
    }

    void Die()
    {
        isDowned = true;
        if (meshRenderer != null) meshRenderer.material.color = downedColor;
        if (rb != null) { rb.velocity = Vector3.zero; rb.isKinematic = true; }
        transform.rotation = Quaternion.Euler(90, transform.rotation.eulerAngles.y, 0);
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(true);
        UpdateHealthUI();
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
        else if (isPossessed) meshRenderer.material.color = possessedColor;
        else meshRenderer.material.color = bodyColor;
    }

    public void UpdateHealthUI()
    {
        if (healthSlider != null) { healthSlider.maxValue = maxHealth; healthSlider.value = currentHealth; }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isDowned || isPossessed) return;
        if (collision.gameObject.CompareTag("Player"))
        {
            var ph = collision.gameObject.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(collisionDamage);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}