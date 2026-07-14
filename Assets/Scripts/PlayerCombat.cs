using UnityEngine;
using System.Collections.Generic;

public class PlayerCombat : MonoBehaviour
{
    [Header("Combat Settings")]
    public bool enableAutoAttack = true;
    public float autoAttackRange = 3f;
    public float autoAttackInterval = 0.5f;
    public float autoAttackDamage = 10f;

    [Header("Slash Attack")]
    public bool enableSlash = true;

    [Header("Slash Effect")]
    public GameObject slashEffectPrefab;
    public float slashEffectDuration = 0.3f;
    public int slashEffectCount = 7;
    public float slashEffectRadius = 2f;
    public float slashEffectHeight = 1f;
    public Color slashColor = new Color(0.2f, 0.6f, 1f);
    [ColorUsage(false, true)] public Color slashEmissionColor = new Color(0.2f, 0.6f, 1f);

    [Header("Skills")]
    public SkillData[] skills = new SkillData[4];

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 12f;
    public float projectileLifetime = 5f;
    public GameObject hitEffectPrefab;
    public float hitEffectDuration = 0.5f;

    private float autoAttackTimer;
    private Transform nearestEnemy;
    private PlayerHealth health;

    [System.Serializable]
    public class SkillData
    {
        public string skillName;
        public float cooldown;
        public float damage;
        public float range;
        public SkillType type;
        public float currentCooldown;
    }

    public enum SkillType
    {
        MeleeSlash,
        Projectile,
        AOE,
        Dash
    }

    void Awake()
    {
        health = GetComponent<PlayerHealth>();
        skills = new SkillData[4];
        skills[0] = new SkillData { skillName = "Slash", cooldown = 0.3f, damage = 10f, range = 3.5f, type = SkillType.MeleeSlash };
        skills[1] = new SkillData { skillName = "Soul Bullet", cooldown = 0.5f, damage = 5f, range = 20f, type = SkillType.Projectile };
        skills[2] = new SkillData { skillName = "Slash", cooldown = 0.3f, damage = 10f, range = 3.5f, type = SkillType.MeleeSlash };
        skills[3] = new SkillData { skillName = "Ghost Dash", cooldown = 3f, damage = 10f, range = 4f, type = SkillType.Dash };
    }

    void Update()
    {
        if (health != null && !health.isPossessing)
            AutoAttack();
        UpdateCooldowns();
    }

    void AutoAttack()
    {
        if (!enableAutoAttack) return;

        autoAttackTimer -= Time.deltaTime;
        if (autoAttackTimer > 0) return;
        FindNearestEnemy();
        if (nearestEnemy != null)
        {
            float dist = Vector3.Distance(transform.position, nearestEnemy.position);
            if (dist <= autoAttackRange)
            {
                autoAttackTimer = autoAttackInterval;
                var enemy = nearestEnemy.GetComponent<Enemy>();
                if (enemy != null)
                {
                    enemy.TakeDamage(autoAttackDamage);
                    Debug.Log("Auto-attack hit " + enemy.name + " for " + autoAttackDamage);
                }
            }
        }
    }

    void FindNearestEnemy()
    {
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        float minDist = float.MaxValue;
        nearestEnemy = null;
        foreach (var enemy in enemies)
        {
            if (health != null && health.possessedEnemy != null && 
                enemy == health.possessedEnemy.gameObject) continue;
            
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearestEnemy = enemy.transform;
            }
        }
    }

    void UpdateCooldowns()
    {
        if (skills == null) return;
        foreach (var skill in skills)
            if (skill != null && skill.currentCooldown > 0)
                skill.currentCooldown -= Time.deltaTime;
    }

    public void SlashAttack()
    {
        if (!enableSlash) return;

        Vector3 forward = transform.forward;
        Vector3 slashOrigin = transform.position + forward * 0.5f;
        float slashRange = 3.5f;
        float slashDamage = 10f;

        CreateSlashArc(slashOrigin, forward, slashRange);

        // Use tag-based search instead of Physics.OverlapSphere because enemy colliders are triggers
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        bool hitSomething = false;
        foreach (var enemyObj in enemies)
        {
            // Check distance to slash origin
            float dist = Vector3.Distance(slashOrigin, enemyObj.transform.position);
            if (dist > slashRange) continue;

            // Check if enemy is in front of the player (within 180 degrees)
            Vector3 toEnemy = (enemyObj.transform.position - transform.position).normalized;
            float dot = Vector3.Dot(forward, toEnemy);
            if (dot < 0f) continue;

            var enemy = enemyObj.GetComponent<Enemy>();
            if (enemy != null && !enemy.isDowned && !enemy.isPossessed)
            {
                if (health != null && health.possessedEnemy == enemy) continue;
                enemy.TakeDamage(slashDamage);
                hitSomething = true;
                Debug.Log("Slash hit " + enemy.name + " for " + slashDamage);
            }
        }
        if (!hitSomething)
            Debug.Log("Slash - no enemy in range");
    }

    void CreateSlashArc(Vector3 position, Vector3 direction, float radius)
    {
        float arcAngle = 90f;
        float startAngle = -arcAngle / 2f;
        float angleStep = arcAngle / (slashEffectCount - 1);

        GameObject arcParent = new GameObject("SlashArc");
        arcParent.transform.position = position;
        // Rotate so that the slash arc is horizontal (parallel to XZ plane)
        arcParent.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        // --- 1. Main slash trail: a curved LineRenderer ---
        GameObject trailObj = new GameObject("SlashTrail");
        trailObj.transform.SetParent(arcParent.transform);
        trailObj.transform.localPosition = Vector3.zero;
        trailObj.transform.localRotation = Quaternion.identity;

        var lineRenderer = trailObj.AddComponent<LineRenderer>();
        int lineSegments = slashEffectCount * 2;
        lineRenderer.positionCount = lineSegments;
        lineRenderer.startWidth = 0.15f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.useWorldSpace = false;
        lineRenderer.alignment = LineAlignment.View;

        // Create a glowing material for the line
        Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (lineShader == null) lineShader = Shader.Find("Standard");
        Material lineMat = new Material(lineShader);
        lineMat.color = new Color(slashColor.r, slashColor.g, slashColor.b, 0.9f);
        lineMat.SetInt("_Surface", 1);
        lineMat.SetInt("_Blend", 0);
        lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        lineMat.SetInt("_ZWrite", 0);
        lineMat.SetInt("_Cull", 0);
        lineMat.renderQueue = 3000;
        lineRenderer.material = lineMat;

        for (int i = 0; i < lineSegments; i++)
        {
            float t = (float)i / (lineSegments - 1);
            float angle = startAngle + arcAngle * t;
            float rad = angle * Mathf.Deg2Rad;
            float localX = Mathf.Sin(rad) * slashEffectRadius;
            float localZ = Mathf.Cos(rad) * slashEffectRadius;
            // Slightly offset Y so the trail is at waist height
            lineRenderer.SetPosition(i, new Vector3(localX, slashEffectHeight * 0.5f, localZ));
        }

        // Animate the line: use a gradient to make it fade from center outward
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.5f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        lineRenderer.colorGradient = grad;
        Destroy(trailObj, slashEffectDuration);

        // --- 2. Glowing impact particles (Quads) along the arc ---
        for (int i = 0; i < slashEffectCount; i++)
        {
            float angle = startAngle + angleStep * i;
            float rad = angle * Mathf.Deg2Rad;
            float localX = Mathf.Sin(rad) * slashEffectRadius;
            float localZ = Mathf.Cos(rad) * slashEffectRadius;
            Vector3 localPos = new Vector3(localX, slashEffectHeight * 0.5f, localZ);

            if (slashEffectPrefab != null)
            {
                GameObject obj = Instantiate(slashEffectPrefab, arcParent.transform);
                obj.transform.localPosition = localPos;
                // Face outward perpendicular to the arc
                obj.transform.localRotation = Quaternion.Euler(0, angle, 90);
                obj.transform.localScale = Vector3.one;
                Destroy(obj, slashEffectDuration);
            }
            else
            {
                // Create glowing slash quad
                GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Quad);
                segment.name = "SlashSeg";
                segment.transform.SetParent(arcParent.transform);
                segment.transform.localPosition = localPos;
                // Rotate: face outward along arc, with Y-up for the quad's vertical
                segment.transform.localRotation = Quaternion.Euler(90, 0, angle);
                segment.transform.localScale = new Vector3(0.3f, slashEffectHeight * 0.6f, 1f);

                var renderer = segment.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = new Material(lineShader);
                    mat.color = new Color(slashColor.r, slashColor.g, slashColor.b, 0.6f);
                    mat.SetInt("_Surface", 1);
                    mat.SetInt("_Blend", 0);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    mat.SetInt("_ZWrite", 0);
                    mat.SetInt("_Cull", 0);
                    mat.renderQueue = 3000;
                    renderer.material = mat;
                }

                var collider = segment.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);

                // Stagger the destruction: center first, edges later
                float t = Mathf.Abs((float)i / (slashEffectCount - 1) - 0.5f) * 2f;
                float delay = t * 0.05f;
                Destroy(segment, slashEffectDuration + delay);
            }
        }

        // --- 3. Slash origin burst ---
        GameObject burst = GameObject.CreatePrimitive(PrimitiveType.Quad);
        burst.name = "SlashBurst";
        burst.transform.SetParent(arcParent.transform);
        burst.transform.localPosition = new Vector3(0, slashEffectHeight * 0.5f, 0);
        burst.transform.localRotation = Quaternion.Euler(90, 0, 0);
        burst.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
        var burstRenderer = burst.GetComponent<Renderer>();
        if (burstRenderer != null)
        {
            Material burstMat = new Material(lineShader);
            burstMat.color = new Color(1f, 1f, 1f, 0.8f);
            burstMat.SetInt("_Surface", 1);
            burstMat.SetInt("_Blend", 0);
            burstMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            burstMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            burstMat.SetInt("_ZWrite", 0);
            burstMat.SetInt("_Cull", 0);
            burstMat.renderQueue = 3000;
            burstRenderer.material = burstMat;
        }
        var burstCol = burst.GetComponent<Collider>();
        if (burstCol != null) Object.Destroy(burstCol);
        Destroy(burst, slashEffectDuration * 0.5f);

        Destroy(arcParent, slashEffectDuration + 0.2f);
    }

    public void CastSkill(int index)
    {
        if (skills == null || index >= skills.Length || skills[index] == null) return;
        var skill = skills[index];
        if (skill.currentCooldown > 0)
        {
            Debug.Log(skill.skillName + " on cooldown: " + skill.currentCooldown.ToString("F1") + "s");
            return;
        }
        skill.currentCooldown = skill.cooldown;
        Debug.Log("Cast " + skill.skillName + "!");
        
        if (skill.type == SkillType.MeleeSlash)
        {
            SlashAttack();
            return;
        }
        
        switch (skill.type)
        {
            case SkillType.Projectile: PerformProjectile(skill); break;
            case SkillType.AOE: PerformAOE(skill); break;
            case SkillType.Dash: PerformDash(skill); break;
        }
    }

    void PerformMeleeSlash(SkillData skill)
    {
        Vector3 forward = transform.forward;
        Vector3 slashOrigin = transform.position + forward * 0.5f;
        CreateSlashArc(slashOrigin, forward, skill.range);

        // Use tag-based search instead of Physics.OverlapSphere because enemy colliders are triggers
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemyObj in enemies)
        {
            float dist = Vector3.Distance(slashOrigin, enemyObj.transform.position);
            if (dist > skill.range) continue;

            Vector3 toEnemy = (enemyObj.transform.position - transform.position).normalized;
            float dot = Vector3.Dot(forward, toEnemy);
            if (dot < 0f) continue;

            var enemy = enemyObj.GetComponent<Enemy>();
            if (enemy != null && !enemy.isDowned && !enemy.isPossessed)
            {
                if (health != null && health.possessedEnemy == enemy) continue;
                enemy.TakeDamage(skill.damage);
            }
        }
    }

    void PerformProjectile(SkillData skill)
    {
        // Spawn position: in front of the player
        Vector3 spawnPos = transform.position + transform.forward * 1.5f + Vector3.up * 0.5f;

        GameObject bullet;

        if (projectilePrefab != null)
        {
            bullet = Instantiate(projectilePrefab, spawnPos, transform.rotation);
        }
        else
        {
            // No prefab assigned — create a default glowing sphere bullet
            bullet = CreateDefaultBullet(spawnPos, transform.rotation);
        }

        // Configure the projectile component
        var projectile = bullet.GetComponent<Projectile>();
        if (projectile == null)
            projectile = bullet.AddComponent<Projectile>();

        projectile.speed = projectileSpeed;
        projectile.damage = skill.damage;
        projectile.maxLifetime = projectileLifetime;
        projectile.isPlayerProjectile = true;
        projectile.hitEffectPrefab = hitEffectPrefab;
        projectile.hitEffectDuration = hitEffectDuration;

        Debug.Log("[PlayerCombat] Fired Soul Bullet! Damage: " + skill.damage + " Speed: " + projectileSpeed);
    }

    /// <summary>
    /// Create a default glowing sphere bullet when no prefab is assigned
    /// </summary>
    GameObject CreateDefaultBullet(Vector3 position, Quaternion rotation)
    {
        var bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "SoulBullet";
        bullet.transform.position = position;
        bullet.transform.rotation = rotation;
        bullet.transform.localScale = Vector3.one * 0.3f;

        // Make it glow with a bright blue-white color
        var renderer = bullet.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = new Color(0.3f, 0.7f, 1f, 1f);
            mat.SetInt("_Surface", 1);
            mat.SetInt("_Blend", 0);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            renderer.material = mat;
        }

        // Remove the default sphere collider and add a trigger collider
        var defaultCollider = bullet.GetComponent<SphereCollider>();
        if (defaultCollider != null)
        {
            defaultCollider.isTrigger = true;
            defaultCollider.radius = 0.5f;
        }

        // Ensure Rigidbody for trigger detection
        var rb = bullet.GetComponent<Rigidbody>();
        if (rb == null)
            rb = bullet.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        return bullet;
    }

    void PerformAOE(SkillData skill)
    {
        // Use tag-based search instead of Physics.OverlapSphere because enemy colliders are triggers
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemyObj in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemyObj.transform.position);
            if (dist > skill.range) continue;

            var enemy = enemyObj.GetComponent<Enemy>();
            if (enemy != null) enemy.TakeDamage(skill.damage);
        }
    }

    void PerformDash(SkillData skill)
    {
        var input = GetComponent<PlayerInputController>();
        Vector3 dashDir = input != null && input.moveDirection != Vector3.zero ? input.moveDirection : transform.forward;
        Vector3 newPos = transform.position + dashDir * skill.range;
        newPos.y = transform.position.y;
        transform.position = newPos;

        // Use tag-based search instead of Physics.OverlapSphere because enemy colliders are triggers
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemyObj in enemies)
        {
            float dist = Vector3.Distance(transform.position, enemyObj.transform.position);
            if (dist > 0.5f) continue;

            var enemy = enemyObj.GetComponent<Enemy>();
            if (enemy != null) enemy.TakeDamage(skill.damage);
        }
    }

    public void OnInteract()
    {
        Debug.Log("Leap! (Bullet Time)");
        if (GameManager.Instance != null)
            GameManager.Instance.SwitchState(GameManager.GameState.BulletTime);
    }
}