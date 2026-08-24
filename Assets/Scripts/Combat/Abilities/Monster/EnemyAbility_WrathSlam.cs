using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wrath Attack: dual-fist ground slam + Burning field (5 DPS / 3s, refresh duration).
/// WR-B01 scales damage / radius / Attack CD by missing durability (each up to +50%).
/// WR-B02 body burn aura is handled by EnemyAbility_WrathBurnAura.
/// </summary>
public class EnemyAbility_WrathSlam : EnemyAbility
{
    public const string TagSlam = "Ability.Monster.Wrath.Slam";
    public const string CardMartyr = "WR-B01";

    [Header("Slam")]
    public float radius = 3f;
    [Tooltip("Local offset from Wrath's facing direction for the Slam damage area and its impact VFX.")]
    public Vector3 slamOffset = Vector3.zero;
    public float firstHitDelay = 0.15f;
    public float aimTurnSpeed = 720f;
    public GameObject slamImpactVfxPrefab;
    public float slamImpactVfxDuration = 1f;

    [Header("Burning Field")]
    public float burnRadius = 3f;
    public float burnDps = 5f;
    public float burnDuration = 3f;
    public float burnTickInterval = 0.5f;
    [Tooltip("Local offset from Wrath's facing direction for the Burning Field damage area and its VFX.")]
    public Vector3 burnFieldOffset = Vector3.zero;
    public GameObject burnFieldVfxPrefab;
    public GameplayEffectDefinition burnEffect;
    public string burnFieldObjectName = "WrathBurnField";

    [Header("WR-B01 Scaling")]
    [Tooltip("Serialized base cooldown used before missing-HP CD reduction.")]
    public float baseCooldown = 2f;

    private float _configuredDamage = 25f;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "双拳砸地";
        if (baseCooldown <= 0f) baseCooldown = cooldown > 0f ? cooldown : 2f;
        cooldown = baseCooldown;
        if (damage <= 0f) damage = 25f;
        _configuredDamage = damage;
        EnsureTag(TagSlam);
        EnsureUpgrade(CardMartyr);
    }

    private void LateUpdate()
    {
        ApplyMartyrCooldownScaling();
    }

    public override bool CanTrigger()
    {
        if (owner != null && owner.isPossessed)
            return base.CanTrigger();
        return base.CanTrigger() && owner != null && owner.targetPlayer != null;
    }

    protected override void OnTrigger()
    {
        StartCoroutine(SlamRoutine());
    }

    private IEnumerator SlamRoutine()
    {
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        Vector3 aim = owner.transform.forward;
        if (owner.isPossessed && TryGetPossessedMouseDirection(out Vector3 mouseAim))
            aim = mouseAim;
        aim.y = 0f;
        if (aim.sqrMagnitude > 0.0001f)
            yield return RotatePossessedOwnerTowards(aim.normalized, aimTurnSpeed);

        foreach (Animator animator in owner.GetComponentsInChildren<Animator>(false))
            animator.SetTrigger("Basic");


        yield return AbilityWait(firstHitDelay);
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        float missingRatio = GetMissingDurabilityRatio();
        float martyr = IsUpgradeUnlocked(CardMartyr) ? missingRatio : 0f;
        float damageScale = 1f + 0.5f * martyr;
        float radiusScale = 1f + 0.5f * martyr;
        float slamDamage = _configuredDamage * damageScale;
        float slamRadius = radius * radiusScale;
        float fieldRadius = burnRadius * radiusScale;

        Vector3 ownerCenter = owner.transform.position;
        Vector3 slamCenter = ownerCenter + owner.transform.TransformDirection(slamOffset);
        PlaySlamImpact(slamCenter, slamRadius);
        DamageEnemiesInSphere(slamCenter, slamRadius, slamDamage, null, slamImpactVfxDuration);
        if (!owner.isPossessed)
            TryDamagePlayerInRadius(slamCenter, slamRadius, slamDamage, slamImpactVfxDuration);

        Vector3 burnFieldCenter = ownerCenter + owner.transform.TransformDirection(burnFieldOffset);
        SpawnOrRefreshBurnField(burnFieldCenter, fieldRadius);
        EndActivationEffect();
    }

    private void SpawnOrRefreshBurnField(Vector3 center, float fieldRadius)
    {
        WrathBurnField existing = FindNearbyBurnField(center, fieldRadius * 0.5f);
        if (existing != null)
        {
            existing.RefreshDuration(burnDuration);
            existing.radius = fieldRadius;
            existing.dps = burnDps;
            existing.Configure(owner, this, fieldRadius, burnDps, burnDuration, burnFieldVfxPrefab, burnEffect);
            return;
        }

        GameObject go = new GameObject(string.IsNullOrEmpty(burnFieldObjectName) ? "WrathBurnField" : burnFieldObjectName);
        go.transform.position = center;
        WrathBurnField field = go.AddComponent<WrathBurnField>();
        field.tickInterval = burnTickInterval;
        field.Configure(owner, this, fieldRadius, burnDps, burnDuration, burnFieldVfxPrefab, burnEffect);
    }

    private WrathBurnField FindNearbyBurnField(Vector3 center, float searchRadius)
    {
        WrathBurnField[] fields = Object.FindObjectsByType<WrathBurnField>(FindObjectsSortMode.None);
        WrathBurnField best = null;
        float bestDist = float.MaxValue;
        foreach (WrathBurnField field in fields)
        {
            if (field == null || field.owner != owner) continue;
            float dist = Vector3.Distance(field.transform.position, center);
            if (dist <= searchRadius && dist < bestDist)
            {
                best = field;
                bestDist = dist;
            }
        }
        return best;
    }

    private void PlaySlamImpact(Vector3 center, float slamRadius)
    {
        GameObject prefab = slamImpactVfxPrefab != null ? slamImpactVfxPrefab : vfxPrefab;
        if (prefab == null) return;
        GameObject vfx = SpawnVfxTracked(prefab, center, Quaternion.identity, slamImpactVfxDuration);
        if (vfx != null)
        {
            float scale = Mathf.Max(0.1f, slamRadius / Mathf.Max(0.1f, radius));
            vfx.transform.localScale = Vector3.one * scale;
        }
    }

    private void ApplyMartyrCooldownScaling()
    {
        float missingRatio = IsUpgradeUnlocked(CardMartyr) ? GetMissingDurabilityRatio() : 0f;
        float cdMult = 1f - 0.5f * missingRatio;
        cooldown = Mathf.Max(0.05f, baseCooldown * cdMult);
    }

    private float GetMissingDurabilityRatio()
    {
        if (owner == null || owner.maxHealth <= 0f) return 0f;
        return Mathf.Clamp01(1f - owner.currentHealth / owner.maxHealth);
    }

    /// <summary>Debug / UI helper for WR-B01 three readouts (0..0.5 each).</summary>
    public void GetMartyrReadouts(out float damageBonus, out float radiusBonus, out float cdShorten)
    {
        float missing = IsUpgradeUnlocked(CardMartyr) ? GetMissingDurabilityRatio() : 0f;
        float capped = 0.5f * missing;
        damageBonus = capped;
        radiusBonus = capped;
        cdShorten = capped;
    }

    private void EnsureTag(string tag)
    {
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, tag, System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add(tag);
    }

    private void EnsureUpgrade(string effectId)
    {
        if (upgrades == null) upgrades = new List<UpgradeSlot>();
        if (upgrades.Exists(u => u != null && string.Equals(u.effectId, effectId, System.StringComparison.OrdinalIgnoreCase)))
            return;
        upgrades.Add(new UpgradeSlot { effectId = effectId, unlocked = false });
    }
}
