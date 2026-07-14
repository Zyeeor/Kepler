using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Soul State")]
    public float soulMaxHealth = 100f;
    public float currentHealth;

    [Header("Health Decay")]
    public float healthDecayPercent = 0.01f;    // 1% per second
    public float decayInterval = 1f;

    [Header("Possession")]
    public Enemy possessedEnemy;
    public bool isPossessing = false;
    public bool isFlyingToPossess = false;
    public float possessFlySpeedMultiplier = 3f;

    [Header("UI")]
    public Slider healthSlider;
    public Image sliderFillImage;
    public Gradient healthGradient;

    private float maxHealth;
    private float decayTimer;
    private PlayerInputController input;
    private PlayerCombat combat;
    private MeshRenderer meshRenderer;
    private Rigidbody rb;
    private Color soulColor = new Color(0.2f, 0.5f, 1f);
    private Color possessedColor = new Color(0.8f, 0.2f, 1f);
    private Coroutine flyRoutine;

    // Model swap data for possession
    private Mesh savedSoulMesh;
    private Material savedSoulMaterial;
    private Vector3 savedSoulScale;
    private MeshFilter playerMeshFilter;
    private Transform playerModelTransform;  // The visual model child (Sphere)

    void Awake()
    {
        Instance = this;
        input = GetComponent<PlayerInputController>();
        combat = GetComponent<PlayerCombat>();
        meshRenderer = GetComponent<MeshRenderer>();
        rb = GetComponent<Rigidbody>();

        // Cache the visual model child (the Sphere with MeshFilter/Renderer)
        playerMeshFilter = GetComponentInChildren<MeshFilter>();
        if (playerMeshFilter != null)
        {
            playerModelTransform = playerMeshFilter.transform;
            savedSoulMesh = playerMeshFilter.sharedMesh;
            savedSoulScale = playerModelTransform.localScale;
            var mr = playerMeshFilter.GetComponent<MeshRenderer>();
            if (mr != null) savedSoulMaterial = mr.sharedMaterial;
        }
    }

    void Start()
    {
        currentHealth = soulMaxHealth;
        maxHealth = soulMaxHealth;
        isPossessing = false;
        isFlyingToPossess = false;
        UpdateHealthUI();
    }

    void Update()
    {
        // Health decay only applies in soul form, not while possessing
        if (!isFlyingToPossess && !isPossessing)
        {
            decayTimer += Time.deltaTime;
            if (decayTimer >= decayInterval)
            {
                decayTimer -= decayInterval;
                float decayAmount = soulMaxHealth * healthDecayPercent;
                TakeDamage(decayAmount);
            }
        }

        if (isPossessing)
        {
            if (possessedEnemy == null || possessedEnemy.currentHealth <= 0)
            {
                Unpossess();
            }
            else
            {
                transform.position = possessedEnemy.transform.position;
                transform.rotation = possessedEnemy.transform.rotation;
            }
        }
    }

    /// <summary>
    /// Fly to downed enemy at 3x speed, then possess
    /// </summary>
    public void FlyAndPossess(Enemy enemy)
    {
        if (enemy == null || flyRoutine != null) return;
        flyRoutine = StartCoroutine(FlyAndPossessRoutine(enemy));
    }

    IEnumerator FlyAndPossessRoutine(Enemy enemy)
    {
        isFlyingToPossess = true;

        // Disable input during flight
        if (input != null) input.enabled = false;

        Vector3 targetPos = enemy.transform.position;
        targetPos.y = transform.position.y;

        float flySpeed = moveSpeed * possessFlySpeedMultiplier;

        // Fly towards the downed enemy
        while (Vector3.Distance(transform.position, targetPos) > 0.3f)
        {
            if (enemy == null)
            {
                isFlyingToPossess = false;
                if (input != null) input.enabled = true;
                flyRoutine = null;
                yield break;
            }

            targetPos = enemy.transform.position;
            targetPos.y = transform.position.y;

            Vector3 dir = (targetPos - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, flySpeed * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            yield return null;
        }

        // Arrived - now possess the enemy
        isFlyingToPossess = false;
        PossessEnemy(enemy);
        flyRoutine = null;
    }

    // Saved player stats for restoring after unpossess
    private float savedMoveSpeed;
    private float savedSlashRange;
    private float savedSlashDamage;
    private float savedAutoAttackRange;
    private float savedAutoAttackDamage;
    private float savedAutoAttackInterval;
    private bool savedSlashEnabled;
    private bool savedAutoAttackEnabled;
    private PlayerCombat.SkillData[] savedSkills;

    /// <summary>
    /// Possess a downed or weakened enemy. Works for both states.
    /// </summary>
    public void PossessEnemy(Enemy enemy)
    {
        if (enemy == null) return;
        if (!enemy.isWeakened && !enemy.isDowned) return;
        if (isPossessing && possessedEnemy != null) Unpossess();
        
        possessedEnemy = enemy;
        isPossessing = true;
        maxHealth = enemy.config != null ? enemy.config.maxHealth : 100f;
        currentHealth = maxHealth; // Full heal on possess
        
        // Save player's original stats before applying enemy config
        SavePlayerStats();
        
        // Apply enemy config to player - inherit enemy form and skills
        ApplyEnemyConfigToPlayer(enemy);
        
        // Keep combat and input enabled
        if (combat != null) combat.enabled = true;
        if (input != null) input.enabled = true;
        if (rb != null) rb.isKinematic = true;
        
        // Swap player model to look exactly like the enemy
        SwapModelToEnemy(enemy);
        
        enemy.OnPossessed();
        
        if (GameManager.Instance != null)
            GameManager.Instance.SwitchState(GameManager.GameState.Possessed);
        Debug.Log("Possessed " + enemy.name + "! HP: " + currentHealth + 
                  " | Speed: " + (input != null ? input.moveSpeed.ToString("F1") : "N/A") +
                  " | Damage: " + (combat != null ? combat.autoAttackDamage.ToString("F0") : "N/A"));
        UpdateHealthUI();
    }

    /// <summary>
    /// Save player's original combat/movement stats so they can be restored after unpossess
    /// </summary>
    void SavePlayerStats()
    {
        if (input != null) savedMoveSpeed = input.moveSpeed;
        if (combat != null)
        {
            savedSlashRange = combat.skills != null && combat.skills.Length > 0 ? combat.skills[0].range : 3.5f;
            savedSlashDamage = combat.skills != null && combat.skills.Length > 0 ? combat.skills[0].damage : 10f;
            savedAutoAttackRange = combat.autoAttackRange;
            savedAutoAttackDamage = combat.autoAttackDamage;
            savedAutoAttackInterval = combat.autoAttackInterval;
            savedSlashEnabled = combat.enableSlash;
            savedAutoAttackEnabled = combat.enableAutoAttack;
            if (combat.skills != null)
            {
                savedSkills = new PlayerCombat.SkillData[combat.skills.Length];
                for (int i = 0; i < combat.skills.Length; i++)
                {
                    if (combat.skills[i] != null)
                    {
                        savedSkills[i] = new PlayerCombat.SkillData
                        {
                            skillName = combat.skills[i].skillName,
                            cooldown = combat.skills[i].cooldown,
                            damage = combat.skills[i].damage,
                            range = combat.skills[i].range,
                            type = combat.skills[i].type,
                            currentCooldown = combat.skills[i].currentCooldown
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// Apply enemy config stats to the player - inherit all enemy form and skills
    /// </summary>
    void ApplyEnemyConfigToPlayer(Enemy enemy)
    {
        var config = enemy.config;
        if (config == null) return;

        // Apply enemy movement speed
        if (input != null) input.moveSpeed = config.moveSpeed;

        // Apply enemy combat stats
        if (combat != null)
        {
            combat.autoAttackRange = config.attackRange;
            combat.autoAttackDamage = config.damage;
            combat.autoAttackInterval = 0.5f;
            combat.enableAutoAttack = true;
            combat.enableSlash = true;

            // Update slash skills to match enemy stats
            if (combat.skills != null)
            {
                for (int i = 0; i < combat.skills.Length; i++)
                {
                    if (combat.skills[i] != null)
                    {
                        if (combat.skills[i].type == PlayerCombat.SkillType.MeleeSlash)
                        {
                            combat.skills[i].damage = config.damage;
                            combat.skills[i].range = config.attackRange + 0.5f;
                        }
                        else if (combat.skills[i].type == PlayerCombat.SkillType.Dash)
                        {
                            combat.skills[i].damage = config.damage * 0.8f;
                            combat.skills[i].range = 4f;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Restore player's original soul-form stats after unpossess
    /// </summary>
    void RestorePlayerStats()
    {
        if (input != null) input.moveSpeed = savedMoveSpeed;
        if (combat != null)
        {
            combat.autoAttackRange = savedAutoAttackRange;
            combat.autoAttackDamage = savedAutoAttackDamage;
            combat.autoAttackInterval = savedAutoAttackInterval;
            combat.enableSlash = savedSlashEnabled;
            combat.enableAutoAttack = savedAutoAttackEnabled;

            if (combat.skills != null && savedSkills != null)
            {
                for (int i = 0; i < Mathf.Min(combat.skills.Length, savedSkills.Length); i++)
                {
                    if (combat.skills[i] != null && savedSkills[i] != null)
                    {
                        combat.skills[i].skillName = savedSkills[i].skillName;
                        combat.skills[i].cooldown = savedSkills[i].cooldown;
                        combat.skills[i].damage = savedSkills[i].damage;
                        combat.skills[i].range = savedSkills[i].range;
                        combat.skills[i].type = savedSkills[i].type;
                        combat.skills[i].currentCooldown = savedSkills[i].currentCooldown;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Replace the player's visual model with the enemy's mesh and material
    /// </summary>
    void SwapModelToEnemy(Enemy enemy)
    {
        if (playerMeshFilter == null) return;

        var enemyMeshFilter = enemy.GetComponent<MeshFilter>();
        var enemyMeshRenderer = enemy.GetComponent<MeshRenderer>();

        if (enemyMeshFilter != null)
        {
            playerMeshFilter.sharedMesh = enemyMeshFilter.sharedMesh;
        }

        if (enemyMeshRenderer != null && playerMeshFilter != null)
        {
            var mr = playerMeshFilter.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = enemyMeshRenderer.sharedMaterial;
            }
        }

        if (playerModelTransform != null)
        {
            playerModelTransform.localScale = enemy.transform.localScale;
        }

        // Hide any collider on the player model (enemy has its own)
        var modelCollider = playerMeshFilter.GetComponent<Collider>();
        if (modelCollider != null) modelCollider.enabled = false;
    }

    /// <summary>
    /// Restore the player's original soul model (sphere)
    /// </summary>
    void RestoreSoulModel()
    {
        if (playerMeshFilter != null && savedSoulMesh != null)
        {
            playerMeshFilter.sharedMesh = savedSoulMesh;
        }

        var mr = playerMeshFilter != null ? playerMeshFilter.GetComponent<MeshRenderer>() : null;
        if (mr != null && savedSoulMaterial != null)
        {
            mr.sharedMaterial = savedSoulMaterial;
            mr.material.color = soulColor;
        }

        if (playerModelTransform != null && savedSoulScale != Vector3.zero)
        {
            playerModelTransform.localScale = savedSoulScale;
        }

        // Re-enable collider
        var modelCollider = playerMeshFilter != null ? playerMeshFilter.GetComponent<Collider>() : null;
        if (modelCollider != null) modelCollider.enabled = true;
    }

    public void Unpossess()
    {
        if (!isPossessing) return;
        if (possessedEnemy != null)
        {
            Object.Destroy(possessedEnemy.gameObject);
            possessedEnemy = null;
        }
        isPossessing = false;
        maxHealth = soulMaxHealth;
        currentHealth = soulMaxHealth;
        if (input != null) input.enabled = true;
        if (combat != null) combat.enabled = true;
        if (rb != null) rb.isKinematic = false;

        // Restore the player's original soul model
        RestoreSoulModel();

        // Restore player's original combat/movement stats
        RestorePlayerStats();

        if (GameManager.Instance != null)
            GameManager.Instance.SwitchState(GameManager.GameState.Soul);
        Debug.Log("Unpossessed - returned to soul form");
        UpdateHealthUI();
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
        UpdateHealthUI();
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealthUI();
    }

    void Die()
    {
        Debug.Log("Player died!");
        if (GameManager.Instance != null)
            GameManager.Instance.SwitchState(GameManager.GameState.GameOver);
    }

    void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        if (sliderFillImage != null && healthGradient != null)
        {
            float ratio = maxHealth > 0 ? currentHealth / maxHealth : 0;
            sliderFillImage.color = healthGradient.Evaluate(ratio);
        }
    }

    private float moveSpeed
    {
        get
        {
            if (input != null) return input.moveSpeed;
            return 5f;
        }
    }
}
