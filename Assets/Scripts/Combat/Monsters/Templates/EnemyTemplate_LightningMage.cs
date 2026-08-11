using UnityEngine;

/// <summary>
/// Applies the Lightning Mage template to an Enemy at runtime.
/// Basic: Laser Beam — continuous beam auto-aimed at nearest enemy (uses VFX prefab)
/// Skill: Chain Lightning — chains between enemies (uses VFX prefab)
/// </summary>
[RequireComponent(typeof(Enemy))]
public class EnemyTemplate_LightningMage : MonoBehaviour
{
    [Header("Template")]
    public string templateName = "Lightning Mage";

    [Header("Stats")]
    public float templateMaxHealth = 150f;
    public float templateMoveSpeed = 40f;
    public float templateDetectionRadius = 10f;

    [Header("Body")]
    public BodyTypeEnum templateBodyType = BodyTypeEnum.Medium;
    public string templateWeaponDescription = "Lightning Staff";
    public float templateKnockdownWindow = 8f;

    void Awake() { ApplyTemplate(); }

    public void ApplyTemplate()
    {
        var e = GetComponent<Enemy>();
        if (e == null) return;

        e.displayName = templateName;
        e.maxHealth = templateMaxHealth;
        e.maxTenacity = templateMaxHealth;
        e.moveSpeed = templateMoveSpeed;
        e.collisionDamage = 25f;
        e.aiAttackRange = 5f;
        e.aiMinRange = 2f;
        e.detectionRadius = templateDetectionRadius;
        e.attackSpeed = 1.0f;
        e.bodyType = (Enemy.BodyType)templateBodyType;
        e.weaponDescription = templateWeaponDescription;
        e.attackDowntime = templateKnockdownWindow;
        e.currentHealth = templateMaxHealth;
        e.currentTenacity = templateMaxHealth;
        e.bodyColor = new Color(0.2f, 0.4f, 0.9f);
        e.weakenedColor = new Color(0.4f, 0.6f, 1f);
        e.downedColor = new Color(0.2f, 0.2f, 0.3f);
        e.flashColor = Color.cyan;
        e.flashDuration = 0.1f;

        if (e.meshRenderer == null) e.meshRenderer = e.GetComponent<Renderer>();
        if (e.meshRenderer != null) e.meshRenderer.material.color = e.bodyColor;

        var laser = GetComponent<EnemyAbility_Laser>();
        if (laser == null) laser = gameObject.AddComponent<EnemyAbility_Laser>();
        laser.damagePerTick = 10f;
        laser.tickInterval = 0.25f;
        laser.maxRange = 15f;

        var chain = GetComponent<EnemyAbility_ChainLightning>();
        if (chain == null) chain = gameObject.AddComponent<EnemyAbility_ChainLightning>();
        chain.chainCount = 4;
        chain.chainRange = 10f;
        chain.chainDamage = 50f;
        chain.cooldown = 8f;

        e.UpdateHealthUI();
        Debug.Log("[LightningMage template] applied to " + e.name);
    }
}
