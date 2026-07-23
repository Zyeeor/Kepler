using UnityEngine;

/// <summary>
/// Skill: Soul Bullet. Fires a projectile toward the mouse cursor that damages the first enemy hit.
/// </summary>
public class PlayerAbility_SoulBullet : PlayerAbility
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 12f;
    public float projectileLifetime = 5f;

    [Header("Hit Effect")]
    public GameObject hitEffectPrefab;
    public float hitEffectDuration = 0.5f;

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "Soul Bullet";
        cooldown = cooldown <= 0f ? 0.5f : cooldown;
    }

    protected override void OnTrigger()
    {
        Vector3 aimDir = GetMouseAimDirection();
        Quaternion aimRot = Quaternion.LookRotation(aimDir, Vector3.up);
        Vector3 spawnPos = owner.transform.position + aimDir * 1.5f + Vector3.up * 0.5f;

        Debug.Log("[SoulBullet] Triggered! aimDir=" + aimDir + " spawnPos=" + spawnPos + " hasPrefab=" + (projectilePrefab != null));

        GameObject bullet;
        if (projectilePrefab != null)
        {
            bullet = Instantiate(projectilePrefab, spawnPos, aimRot);
            Debug.Log("[SoulBullet] Instantiated prefab: " + projectilePrefab.name);
            PlayVfx(bullet);
        }
        else
        {
            Debug.LogWarning("[SoulBullet] No prefab assigned — bullet will be invisible. Assign a Projectile Prefab in the Inspector.");
            bullet = CreateDefaultBullet(spawnPos, aimRot);
        }

        var projectile = bullet.GetComponent<Projectile>();
        if (projectile == null) projectile = bullet.AddComponent<Projectile>();
        projectile.speed = projectileSpeed;
        projectile.damage = damage;
        projectile.maxLifetime = projectileLifetime;
        projectile.isPlayerProjectile = true;
        projectile.hitEffectPrefab = hitEffectPrefab;
        projectile.hitEffectDuration = hitEffectDuration;
        projectile.ownerEnemy = PlayerHealth.Instance != null ? PlayerHealth.Instance.possessedEnemy : null;

        Debug.Log("[SoulBullet] Bullet created: " + bullet.name + " at " + bullet.transform.position + " rot=" + bullet.transform.rotation.eulerAngles);
    }

    GameObject CreateDefaultBullet(Vector3 position, Quaternion rotation)
    {
        var bullet = new GameObject("SoulBullet");
        bullet.transform.position = position;
        bullet.transform.rotation = rotation;
        var col = bullet.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;
        var rb = bullet.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        return bullet;
    }
}
