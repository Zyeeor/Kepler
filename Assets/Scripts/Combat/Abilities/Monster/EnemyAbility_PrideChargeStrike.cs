using System.Collections;
using UnityEngine;

/// <summary>
/// Pride mobility: hold Space to charge, release to dash along move direction.
/// Charge ratio (0~1 over maxChargeTime) adds 0~100% distance and damage.
/// Build Pride.ChargeEmpowered raises base distance via ChargeDistance.
/// </summary>
public class EnemyAbility_PrideChargeStrike : EnemyAbility
{
    public float chargeDistance = 5f;
    public float chargeSpeed = 24f;
    public float hitRadius = 0.8f;
    public float landingRadius = 1.5f;
    public float damageMultiplier = 1f;
    [Tooltip("Hold duration that reaches the full +100% distance/damage bonus.")]
    public float maxChargeTime = 2f;

    [Header("VFX - Charge Hold")]
    [Tooltip("Self VFX while charging / dashing. Destroyed when the dash ends (or charge is cancelled).")]
    public GameObject chargeVfxPrefab;
    [Tooltip("Local position offset relative to the owner.")]
    public Vector3 chargeVfxPositionOffset = Vector3.zero;
    [Tooltip("Local euler offset relative to the owner.")]
    public Vector3 chargeVfxRotationOffset = Vector3.zero;

    private bool isCharging;
    private bool isDashing;
    private float chargeTimer;
    private GameObject chargeVfxInstance;

    private void OnEnable()
    {
        type = AbilityType.Mobility;
        abilityName = "一刀斩";
        cooldown = cooldown <= 0f ? 2f : cooldown;
        if (maxChargeTime <= 0f) maxChargeTime = 2f;
        if (abilityTags == null) abilityTags = new System.Collections.Generic.List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Pride.ChargeStrike", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Pride.ChargeStrike");
    }

    /// <summary>Possessed play is hold/release driven in Update; edge Trigger is for AI only.</summary>
    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        if (owner != null && owner.isPossessed) return false;
        return true;
    }

    protected override void OnTrigger()
    {
        // AI / non-possessed: immediate dash with no hold bonus.
        if (isDashing) return;
        StartCoroutine(DashRoutine(0f));
    }

    protected override void Update()
    {
        base.Update();
        if (owner == null || !owner.isPossessed || isDashing) return;
        if (PlayerController.IsGameplayInputBlocked || Time.timeScale == 0f)
        {
            if (isCharging) CancelCharge(applyCooldown: false);
            return;
        }

        bool holding = Input.GetKey(KeyCode.Space);
        if (holding)
        {
            if (!isCharging)
            {
                if (currentCooldown > 0f || owner.isDowned) return;
                if (!TryBeginActivationEffect()) return;
                isCharging = true;
                chargeTimer = 0f;
                owner.PayAbilityHpCost(this);
                SpawnChargeHoldVfx();
            }

            chargeTimer = Mathf.Min(chargeTimer + AbilityDeltaTime, Mathf.Max(0.01f, maxChargeTime));
        }
        else if (isCharging)
        {
            float held = chargeTimer;
            isCharging = false;
            currentCooldown = EffectiveCooldown;
            StartCoroutine(DashRoutine(held));
        }
    }

    private IEnumerator DashRoutine(float chargeTime)
    {
        if (owner == null)
        {
            CleanupDashVfx();
            CleanupChargeHoldVfx();
            EndActivationEffect();
            yield break;
        }

        isDashing = true;

        // AI path has no hold; still show self VFX for the dash duration if configured.
        if (chargeVfxInstance == null)
            SpawnChargeHoldVfx();

        Vector3 direction = owner.transform.forward;
        if (owner.isPossessed && PlayerController.CurrentMoveDirection.sqrMagnitude > 0.0001f)
            direction = PlayerController.CurrentMoveDirection;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = owner.transform.forward;
        direction.Normalize();

        // Slash/trail VFX starts with the dash, not the hold windup.
        SpawnVfx();
        if (activeVfx != null)
            activeVfx.transform.SetParent(owner.transform, true);

        float baseDistance = Mathf.Max(0.01f, GetCardParameter("ChargeDistance", chargeDistance));
        float chargeRatio = Mathf.Clamp01(chargeTime / Mathf.Max(0.01f, maxChargeTime)); // 0~100%
        float distance = baseDistance * (1f + chargeRatio);
        float strikeDamage = damage * damageMultiplier * (1f + chargeRatio);

        float moved = 0f;
        owner.IsAbilityFacingLocked = true;
        owner.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        while (owner != null && moved < distance)
        {
            float step = Mathf.Min(chargeSpeed * AbilityDeltaTime, distance - moved);
            Vector3 next = owner.transform.position + direction * step;
            DamageEnemiesAlongPath(owner.transform.position, next, hitRadius, strikeDamage);
            owner.transform.position = next;
            moved += step;
            yield return null;
        }

        if (owner != null)
        {
            DamageEnemiesInSphere(owner.transform.position, landingRadius, strikeDamage);
            owner.IsAbilityFacingLocked = false;
        }

        CleanupDashVfx();
        CleanupChargeHoldVfx();
        EndActivationEffect();
        isDashing = false;
    }

    private void SpawnChargeHoldVfx()
    {
        if (chargeVfxInstance != null || chargeVfxPrefab == null || owner == null) return;

        Vector3 pos = owner.transform.position + owner.transform.TransformDirection(chargeVfxPositionOffset);
        Quaternion rot = owner.transform.rotation * Quaternion.Euler(chargeVfxRotationOffset);
        chargeVfxInstance = SpawnVfxTracked(chargeVfxPrefab, pos, rot);
        if (chargeVfxInstance != null)
            chargeVfxInstance.transform.SetParent(owner.transform, true);
    }

    private void CancelCharge(bool applyCooldown)
    {
        if (!isCharging) return;
        isCharging = false;
        chargeTimer = 0f;
        if (applyCooldown) currentCooldown = EffectiveCooldown;
        CleanupChargeHoldVfx();
        EndActivationEffect();
    }

    private void CleanupDashVfx()
    {
        if (activeVfx == null) return;
        ReleaseVfx(activeVfx);
        activeVfx = null;
    }

    private void CleanupChargeHoldVfx()
    {
        if (chargeVfxInstance == null) return;
        ReleaseVfx(chargeVfxInstance);
        chargeVfxInstance = null;
    }

    protected override void OnDisable()
    {
        isCharging = false;
        isDashing = false;
        chargeTimer = 0f;
        if (owner != null) owner.IsAbilityFacingLocked = false;
        CleanupDashVfx();
        CleanupChargeHoldVfx();
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        isCharging = false;
        isDashing = false;
        chargeTimer = 0f;
        if (owner != null) owner.IsAbilityFacingLocked = false;
        CleanupDashVfx();
        CleanupChargeHoldVfx();
        base.ResetForOwnerReuse();
    }
}
