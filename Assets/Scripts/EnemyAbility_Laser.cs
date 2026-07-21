using UnityEngine;

/// <summary>
/// Basic Attack: Laser Beam. Auto-aims at nearest enemy (possessed) or player (AI).
/// Spawns a VFX prefab each tick (0.25s), oriented from owner to target.
/// </summary>
public class EnemyAbility_Laser : EnemyAbility
{
    [Header("Laser")]
    public float maxRange = 15f;
    public float damagePerTick = 10f;
    public float tickInterval = 0.25f;

    [Header("Beam VFX")]
    [Tooltip("VFX prefab spawned each tick, oriented from owner to target.")]
    public GameObject beamPrefab;
    public Material beamMaterial;
    public Vector3 beamPositionOffset = Vector3.zero;
    public Vector3 beamRotationOffset = Vector3.zero;

    [Header("Hit VFX")]
    public GameObject hitImpactPrefab;
    public float hitImpactDuration = 0.3f;

    private bool isFiring;
    private Enemy currentTarget;
    private float damageTimer;
    private GameObject hitVfx;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "Laser Beam";
    }

    protected override void Update()
    {
        base.Update();
        if (owner == null) return;

        bool wantFire;
        if (owner.isPossessed)
            wantFire = Input.GetMouseButton(0);
        else
            wantFire = owner.targetPlayer != null && Vector3.Distance(owner.transform.position, owner.targetPlayer.position) <= maxRange;

        if (wantFire && CanTrigger())
        {
            if (!isFiring) { isFiring = true; damageTimer = 0f; currentCooldown = 0f; }
            UpdateLaser();
        }
        else if (isFiring)
            StopLaser();
    }

    void UpdateLaser()
    {
        Vector3 origin = owner.transform.position + Vector3.up * 1f;
        Vector3 targetPos;

        if (owner.isPossessed)
        {
            currentTarget = FindNearestEnemy(origin);
            if (currentTarget == null || currentTarget.isDowned) { StopLaser(); return; }
            targetPos = currentTarget.transform.position + Vector3.up * 1f;
        }
        else
        {
            if (owner.targetPlayer == null || Vector3.Distance(origin, owner.targetPlayer.position) > maxRange)
            { StopLaser(); return; }
            currentTarget = null;
            targetPos = owner.targetPlayer.position + Vector3.up * 1f;
        }

        damageTimer += Time.deltaTime;
        if (damageTimer >= tickInterval)
        {
            // Spawn beam VFX from origin to target
            Vector3 dir = targetPos - origin;
            Vector3 pos = origin + beamPositionOffset;
            Quaternion rot = (dir.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(dir.normalized, Vector3.up)
                : Quaternion.identity) * Quaternion.Euler(beamRotationOffset);

            float distance = dir.magnitude;
            GameObject vfx = Instantiate(beamPrefab, pos, rot);
            // Scale Z by distance so the beam VFX reaches the target
            Vector3 scale = vfx.transform.localScale;
            scale.z *= distance;
            vfx.transform.localScale = scale;

            if (beamMaterial != null)
            {
                foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
                {
                    var renderer = ps.GetComponent<ParticleSystemRenderer>();
                    if (renderer != null) renderer.material = beamMaterial;
                }
            }
            PlayVfx(vfx);
            Destroy(vfx, tickInterval);

            // Damage
            if (owner.isPossessed)
                DealDamageTo(currentTarget, damagePerTick);
            else
            {
                var ph = owner.targetPlayer.GetComponent<PlayerHealth>();
                if (ph != null) DealDamageToPlayer(ph, damagePerTick);
            }
            damageTimer -= tickInterval;

            // Hit VFX
            if (hitImpactPrefab != null)
            {
                if (hitVfx == null) { hitVfx = Instantiate(hitImpactPrefab, targetPos, Quaternion.identity); PlayVfx(hitVfx); }
                else hitVfx.transform.position = targetPos;
            }
        }
    }

    Enemy FindNearestEnemy(Vector3 origin)
    {
        Enemy nearest = null;
        float nearestDist = float.MaxValue;
        foreach (var obj in GameObject.FindGameObjectsWithTag("Enemy"))
        {
            var e = obj.GetComponent<Enemy>();
            if (e != null && e != owner && !e.isDowned && !e.isPossessed)
            {
                float d = Vector3.Distance(origin, e.transform.position);
                if (d <= maxRange && d < nearestDist) { nearestDist = d; nearest = e; }
            }
        }
        return nearest;
    }

    void StopLaser()
    {
        isFiring = false;
        currentTarget = null;
        if (hitVfx != null) { Destroy(hitVfx, hitImpactDuration); hitVfx = null; }
    }

    public override bool CanTrigger()
    {
        if (owner != null && owner.isPossessed) return owner != null && !owner.isDowned;
        return base.CanTrigger() && owner != null && owner.targetPlayer != null;
    }

    protected override void OnTrigger() { }
    void OnDisable() { if (isFiring) StopLaser(); }
}
