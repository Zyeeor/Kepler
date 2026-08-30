using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wrath Special: chain storm tornado — base 2s, tick every 0.5s, weight-scaled pull, no Stun.
/// WR-S01: +300% move speed during storm (moveSpeedMultiplier 4).
/// WR-S03: duration +2s.
/// </summary>
public class EnemyAbility_WrathChainStorm : EnemyAbility
{
    public const string TagChainStorm = "Ability.Monster.Wrath.ChainStorm";
    public const string CardStormSpeed = "WR-S01";
    public const string CardStormDuration = "WR-S03";

    [Header("Storm")]
    public float baseDuration = 2f;
    public float durationBonusS03 = 2f;
    public float tickInterval = 0.5f;
    public float pullRadius = 5f;
    public float tickDamage = 12f;
    [Tooltip("Possessed Player 专属每 Tick 伤害；Enemy 版本仍使用 tickDamage。")]
    public float possessedTickDamageOverride = 15f;
    public float pullStepMax = 1.2f;
    public float lightPullRatio = 1f;
    public float mediumPullRatio = 0.55f;
    public float heavyPullRatio = 0.25f;
    public float aimTurnSpeed = 720f;

    [Header("Effects / VFX")]
    public GameplayEffectDefinition stormSpeedEffect;
    public GameObject stormVfxPrefab;
    public float stormVfxLifetimePadding = 0.25f;

    [Header("Storm Model")]
    public GameObject modelToDisable;
    public GameObject stormModelPrefab;
    public Transform stormModelSpawnPoint;
    public Vector3 stormModelTransformOffset = Vector3.zero;
    public Vector3 stormModelScale = Vector3.one;
    public float stormModelClockwiseSpinSpeed = 360f;


    private Coroutine _stormRoutine;
    private GameObject _stormVfx;
    private GameObject _stormModel;
    private bool _restoreDisabledModel;


    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "暴怒锁链";
        cooldown = cooldown <= 0f ? 8f : cooldown;
        if (damage > 0f) tickDamage = damage;
        EnsureTag(TagChainStorm);
        EnsureUpgrade(CardStormSpeed);
        EnsureUpgrade(CardStormDuration);
    }

    public override bool CanTrigger()
    {
        // Empty storm allowed per subdivision A6.
        return base.CanTrigger();
    }

    protected override bool TryGetEnemyTelegraphGeometryInternal(out Vector3 center, out float telegraphRadius)
    {
        center = owner != null ? owner.transform.position : transform.position;
        telegraphRadius = pullRadius;
        return enemyIndicatorEnabled && telegraphRadius > 0f;
    }

    protected override void OnDisable()
    {
        StopStormInternal();
        base.OnDisable();
    }

    public override void ResetForOwnerReuse()
    {
        StopStormInternal();
        base.ResetForOwnerReuse();
    }

    protected override void OnTrigger()
    {
        if (_stormRoutine != null)
            StopCoroutine(_stormRoutine);
        _stormRoutine = StartCoroutine(StormRoutine());
    }

    private IEnumerator StormRoutine()
    {
        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        Vector3 aim = owner.transform.forward;
        if (owner.isPossessed)
        {
            if (TryGetPossessedMouseDirection(out Vector3 mouseAim))
                aim = mouseAim;
        }
        else if (owner.targetPlayer != null)
        {
            aim = owner.targetPlayer.position - owner.transform.position;
        }

        aim.y = 0f;
        if (aim.sqrMagnitude > 0.0001f)
            yield return RotateOwnerTowards(aim.normalized, aimTurnSpeed);


        if (owner == null)
        {
            EndActivationEffect();
            yield break;
        }

        foreach (Animator animator in owner.GetComponentsInChildren<Animator>(false))
            animator.SetTrigger("Skill");


        float duration = baseDuration;
        if (IsUpgradeUnlocked(CardStormDuration))
            duration += durationBonusS03;

        bool speedBuff = IsUpgradeUnlocked(CardStormSpeed) && stormSpeedEffect != null && owner.Combat != null;
        if (speedBuff)
            owner.Combat.ApplyEffect(stormSpeedEffect, owner.Combat, abilityTags, out _);

        SpawnStormVfx(duration);
        SpawnStormModel();
        float elapsed = 0f;

        float nextTick = 0f;

        while (owner != null && !owner.isDowned && elapsed < duration)
        {
            // Body fatal / swap ends via OnDisable / ResetForOwnerReuse.
            elapsed += AbilityDeltaTime;
            RotateStormModel();
            if (elapsed >= nextTick)
            {
                nextTick += tickInterval;
                TickStorm();
            }
            yield return null;
        }


        if (speedBuff && owner != null && owner.Combat != null)
            owner.Combat.RemoveEffect(stormSpeedEffect);

        CleanupStormVfx();
        CleanupStormModel();
        _stormRoutine = null;

        EndActivationEffect();
    }

    private IEnumerator RotateOwnerTowards(Vector3 direction, float turnSpeed)
    {
        if (owner == null || direction.sqrMagnitude < 0.0001f) yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        owner.IsAbilityFacingLocked = true;
        while (owner != null && Quaternion.Angle(owner.transform.rotation, targetRotation) > 0.1f)
        {
            owner.transform.rotation = Quaternion.RotateTowards(
                owner.transform.rotation,
                targetRotation,
                turnSpeed * AbilityDeltaTime);
            yield return null;
        }

        if (owner != null)
        {
            owner.transform.rotation = targetRotation;
            owner.IsAbilityFacingLocked = false;
        }
    }

    private void TickStorm()

    {
        if (owner == null) return;
        Vector3 center = owner.transform.position;
        float dmg = owner.isPossessed && possessedTickDamageOverride > 0f
            ? possessedTickDamageOverride
            : (tickDamage > 0f ? tickDamage : damage);
        float effectivePullRadius = ScaleAbilityRadius(pullRadius);
        CombatHitboxDebug.DrawSphere(drawHitboxes, center, effectivePullRadius, Mathf.Max(0.08f, tickInterval));

        Collider[] hits = Physics.OverlapSphere(center, effectivePullRadius, ~0, QueryTriggerInteraction.Collide);
        HashSet<int> seen = new HashSet<int>();
        foreach (Collider hit in hits)
        {
            if (hit == null) continue;

            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy != null && owner.CanDamage(enemy) && seen.Add(enemy.GetInstanceID()))
            {
                DealDamageTo(enemy, dmg);
                PullTransform(enemy.transform, center, ResolvePullRatio(enemy));
                continue;
            }

            if (!owner.isPossessed)
            {
                PlayerHealth player = hit.GetComponentInParent<PlayerHealth>();
                if (player != null && owner.CanDamageSoul() && seen.Add(player.GetInstanceID()))
                {
                    DealDamageToPlayer(player, dmg);
                    PullTransform(player.transform, center, lightPullRatio);
                }
            }
        }
    }

    private float ResolvePullRatio(Enemy enemy)
    {
        if (enemy == null) return mediumPullRatio;
        switch (enemy.bodyType)
        {
            case MonsterActor.BodyType.Slim:
                return lightPullRatio;
            case MonsterActor.BodyType.Medium:
                return mediumPullRatio;
            default:
                return heavyPullRatio;
        }
    }

    private void PullTransform(Transform target, Vector3 center, float ratio)
    {
        if (target == null) return;
        Vector3 pos = target.position;
        Vector3 toCenter = center - pos;
        toCenter.y = 0f;
        float dist = toCenter.magnitude;
        if (dist < 0.05f) return;
        float step = Mathf.Min(dist, pullStepMax * Mathf.Clamp01(ratio));
        Vector3 next = pos + toCenter.normalized * step;
        next.y = pos.y;
        // No Stun / root — only displace; player keeps move + Space.
        target.position = next;
    }

    private void SpawnStormVfx(float duration)
    {
        CleanupStormVfx();
        if (stormVfxPrefab == null || owner == null) return;
        _stormVfx = VfxPool.Instance.Spawn(stormVfxPrefab, owner.transform.position, Quaternion.identity, owner.transform);
        BulletTimeController.MarkVfxOrigin(_stormVfx, IsOwnedByPlayer);
        ScaleAbilityObject(_stormVfx);
        PlayVfx(_stormVfx);
        ReleaseVfx(_stormVfx, duration + stormVfxLifetimePadding);
    }

    private void SpawnStormModel()
    {
        CleanupStormModel();
        if (stormModelPrefab == null || owner == null) return;

        Transform anchor = stormModelSpawnPoint != null ? stormModelSpawnPoint : owner.transform;
        if (modelToDisable != null)
        {
            _restoreDisabledModel = modelToDisable.activeSelf;
            modelToDisable.SetActive(false);
        }

        _stormModel = Instantiate(stormModelPrefab, anchor);
        _stormModel.transform.localPosition = stormModelTransformOffset;
        _stormModel.transform.localRotation = Quaternion.identity;
        _stormModel.transform.localScale = Vector3.Scale(_stormModel.transform.localScale, stormModelScale);

    }

    private void RotateStormModel()
    {
        if (_stormModel == null) return;
        _stormModel.transform.Rotate(Vector3.up, -stormModelClockwiseSpinSpeed * AbilityDeltaTime, Space.Self);
    }

    private void CleanupStormModel()
    {
        if (_stormModel != null) Destroy(_stormModel);
        _stormModel = null;
        if (modelToDisable != null && _restoreDisabledModel)
            modelToDisable.SetActive(true);
        _restoreDisabledModel = false;
    }

    private void CleanupStormVfx()

    {
        if (_stormVfx != null)
        {
            ReleaseVfx(_stormVfx);
            _stormVfx = null;
        }
    }

    private void StopStormInternal()
    {
        if (_stormRoutine != null)
        {
            StopCoroutine(_stormRoutine);
            _stormRoutine = null;
        }
        if (owner != null && owner.Combat != null && stormSpeedEffect != null)
            owner.Combat.RemoveEffect(stormSpeedEffect);
        CleanupStormVfx();
        CleanupStormModel();
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
