using UnityEngine;

public enum BodyTypeEnum { Slim, Medium, Large, HugeMuscular, Boss }

/// <summary>
/// Applies the 愤怒-楚狱冥兽 (Fury Beast) template stats to an Enemy at runtime.
/// Attach to an enemy prefab or use the menu to populate values.
/// Values per the design table:
///   HP = 200, Move = 50, ATK = 30
///   Body = HugeMuscular, Weapon = 双手锁链
///   Knockdown window = 10s
///   Passive: 2% lifesteal (red orbiting VFX)
///   Basic: 山崩地裂 (Ground Slam) - delayed first hit + aftershock
///   Skill (10s CD): 暴怒锁链 (Fury Chain) - 30% target HP, fan pull
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyTemplate_FuryBeast : MonoBehaviour
{
    [Header("Template")]
    public string templateName = "愤怒-楚狱冥兽";

    [Header("Stats")]
    public float templateMaxHealth = 200f;
    public float templateMoveSpeed = 50f;
    public float templateDetectionRadius = 10f;

    [Header("Body")]
    public BodyTypeEnum templateBodyType = BodyTypeEnum.HugeMuscular;
    public string templateWeaponDescription = "双手锁链";
    public float templateKnockdownWindow = 10f;

    void Awake()
    {
        ApplyTemplate();
    }

    public void ApplyTemplate()
    {
        var e = GetComponent<Enemy>();
        if (e == null) return;

        e.displayName = templateName;
        e.maxHealth = templateMaxHealth;
        e.maxTenacity = templateMaxHealth; // 1:1
        e.moveSpeed = templateMoveSpeed;
        e.collisionDamage = 30f;
        e.aiAttackRange = 3f;
        e.detectionRadius = templateDetectionRadius;

        e.bodyType = (Enemy.BodyType)templateBodyType;
        e.weaponDescription = templateWeaponDescription;
        e.attackDowntime = templateKnockdownWindow;

        e.currentHealth = templateMaxHealth;
        e.currentTenacity = templateMaxHealth;
        e.bodyColor = new Color(0.7f, 0.1f, 0.1f); // dark red angry
        e.weakenedColor = new Color(1f, 0.5f, 0f);
        e.downedColor = new Color(0.3f, 0.3f, 0.3f);
        e.flashColor = Color.red;
        e.flashDuration = 0.1f;

        if (e.meshRenderer == null) e.meshRenderer = e.GetComponent<Renderer>();
        if (e.meshRenderer != null) e.meshRenderer.material.color = e.bodyColor;

        // Auto-attach Lifesteal passive if not present
        var ls = GetComponent<EnemyAbility_Lifesteal>();
        if (ls == null) ls = gameObject.AddComponent<EnemyAbility_Lifesteal>();
        ls.lifestealPercent = 0.02f;

        // Auto-attach Ground Slam basic attack if not present
        var slam = GetComponent<EnemyAbility_GroundSlam>();
        if (slam == null) slam = gameObject.AddComponent<EnemyAbility_GroundSlam>();
        slam.damage = 30f;       // damage configured directly on ability
        slam.radius = 4f;        // AoE radius configured directly on ability
        slam.cooldown = 1.5f;

        // Auto-attach Chain Pull skill if not present
        var chain = GetComponent<EnemyAbility_ChainPull>();
        if (chain == null) chain = gameObject.AddComponent<EnemyAbility_ChainPull>();
        chain.cooldown = 10f;
        chain.percentOfTargetMaxHp = 0.30f;

        e.UpdateHealthUI();
        Debug.Log("[FuryBeast template] applied to " + e.name);
    }
}
