using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gluttony's Special: a forward bite that consumes the nearest legal Enemy.
/// A possessed Gluttony temporarily copies that Enemy's Skill ability after a successful bite.
/// </summary>
public class EnemyAbility_GluttonyDevour : EnemyAbility
{
    public float range = 1f;
    public float angle = 100f;
    public float damageAmount = 40f;
    [Tooltip("Delay between the Devour cast and its Enemy damage / consume resolution.")]
    public float hitDelay = 0.5f;
    [Range(0f, 1f)] public float executeHealthFraction = 0.2f;
    public float firstDevourHeal = 50f;
    public float projectileSwallowRadius = 2.5f;
    public float maxSwallowProjectileDamage = 25f;

    [Header("Devour Hit VFX")]
    [Tooltip("Optional burst VFX spawned on the body of the Enemy successfully consumed by Devour.")]
    public GameObject blastVfxPrefab;
    public float blastVfxDuration = 1f;

    [Header("Devour VFX Motion")]
    [Tooltip("Local forward/up displacement from VFX Spawn Point at the apex of the Devour VFX motion.")]
    public Vector3 vfxForwardUpOffset = new Vector3(0f, 0.5f, 1f);
    [Tooltip("Duration of the small-to-large forward/upward phase.")]
    public float vfxExpandDuration = 0.5f;
    [Tooltip("Duration of the large-to-small backward/downward phase.")]
    public float vfxReturnDuration = 0.5f;
    [Tooltip("Scale multiplier at the beginning and end of the Devour VFX motion.")]
    public float vfxStartEndScale = 0.25f;
    [Tooltip("Scale multiplier at the apex of the Devour VFX motion.")]
    public float vfxPeakScale = 1f;

    private GluttonyBodyState _state;
    private Coroutine _vfxMotionRoutine;

    /// <summary>Returns the world-space radius of half the current horizontal screen.</summary>
    public float GetCopiedSkillRadius()
    {
        if (MonsterSpawner.Instance != null)
            return Mathf.Max(1f, MonsterSpawner.Instance.GetScreenDiameterWorldDistance() * 0.5f);

        Camera camera = Camera.main;
        if (camera != null && camera.orthographic)
            return Mathf.Max(1f, camera.orthographicSize * camera.aspect);
        return 12f;
    }

    private void OnEnable()
    {
        type = AbilityType.Skill;
        abilityName = "吞噬";
        cooldown = cooldown <= 0f ? 1f : cooldown;
        if (damage <= 0f) damage = damageAmount;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Gluttony.Devour", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Gluttony.Devour");
    }

    /// <summary>
    /// Pass v1 §13.2：Enemy 使用时的前方扇形 Telegraph——约 100°、范围对应当前真实 Devour 范围。
    /// 玩家版不新增 Enemy 前摇（telegraph 仅 ShouldUseEnemyTelegraph 时启用，即 !isPossessed）。
    /// </summary>
    public override EnemyTelegraphGeometry GetEnemyTelegraphGeometry()
    {
        float radius = ScaleAbilityRadius(range * 2f);
        if (radius <= 0f || angle <= 0f) return default;

        Vector3 forward = ResolveIndicatorForward();
        return new EnemyTelegraphGeometry
        {
            shape = EnemyIndicatorShape.Sector,
            center = owner.transform.position,
            forward = forward,
            length = radius,
            angle = angle,
            isValid = true
        };
    }

    /// <summary>Devour 扇形朝向 = owner.forward（与 FindNearestDevourTarget 的判定一致）。</summary>
    protected override Vector3 ResolveIndicatorForward()
    {
        Vector3 fwd = owner != null ? owner.transform.forward : transform.forward;
        fwd.y = 0f;
        return fwd.sqrMagnitude > 0.0001f ? fwd.normalized : Vector3.forward;
    }

    protected override void OnTrigger()
    {
        if (owner == null)
        {
            EndActivationEffect();
            return;
        }

        CacheOwnerState();
        _state?.CancelSmallCat();

        Animator anim = owner.GetActiveAnimator();
        if (anim != null) anim.SetTrigger("Skill");

        bool hasGluttonyTarget;
        Enemy target = FindNearestDevourTarget(out hasGluttonyTarget);
        if (target != null)
        {
            StartCoroutine(ConsumeEnemyAfterDelay(target));
            return;
        }

        // Gluttony cannot copy another Gluttony's Skill. If that is the only
        // available target, cancel this cast so Devour can be tried again later.
        if (hasGluttonyTarget)
        {
            currentCooldown = 0f;
            EndActivationEffect();
            return;
        }

        // GL-S03 projectile classification is intentionally retained for later work.
        // The current implementation only attempts its existing projectile swallow fallback.
        if (IsUpgradeUnlocked("GL-S03") && TrySwallowProjectile())
            _state?.GrantOverfed();

        EndActivationEffect();
    }

    private IEnumerator ConsumeEnemyAfterDelay(Enemy target)
    {
        yield return AbilityWait(Mathf.Max(0f, hitDelay));
        if (owner != null && IsDevourTarget(target))
            ConsumeEnemy(target);
        EndActivationEffect();
    }

    private Enemy FindNearestDevourTarget(out bool hasGluttonyTarget)
    {
        hasGluttonyTarget = false;
        Enemy nearest = null;
        float nearestSqrDistance = float.MaxValue;
        Vector3 origin = owner.transform.position;
        foreach (Enemy candidate in FindEnemiesInArc(origin, owner.transform.forward, range * 2f, angle))
        {
            if (!IsDevourTarget(candidate)) continue;
            if (candidate.sinType == SinType.Gluttony)
            {
                hasGluttonyTarget = true;
                continue;
            }
            Vector3 offset = candidate.transform.position - origin;
            offset.y = 0f;
            float sqrDistance = offset.sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance) continue;
            nearest = candidate;
            nearestSqrDistance = sqrDistance;
        }
        return nearest;
    }

    private void ConsumeEnemy(Enemy target)
    {
        float damageToDeal = damage > 0f ? damage : damageAmount;
        if (IsUpgradeUnlocked("GL-S02") && target.maxHealth > 0f &&
            target.currentHealth / target.maxHealth < GetCardParameter("ExecuteThreshold", executeHealthFraction))
        {
            damageToDeal = target.currentHealth;
        }

        // A downed body is a valid Devour source, but it is already dead and cannot
        // pass through the normal offensive-damage gate.
        if (!IsCorpseDevourTarget(target))
            DealDamageTo(target, damageToDeal);
        PlayBlastVfx(target.transform.position);
        _state?.GrantOverfed();

        if (IsUpgradeUnlocked("GL-S01") && _state != null && !_state.FirstDevourHealUsed)
        {
            owner.Heal(GetCardParameter("FirstDevourHeal", firstDevourHeal));
            _state.MarkFirstDevourHealUsed();
        }

        if (owner.isPossessed)
            _state?.TryCopySkillFrom(target, this);
        else if (owner is BossSevenfoldActor boss && boss.AffixAssimilator != null
                 && PossessionManager.Instance != null && PossessionManager.Instance.CurrentBody != null)
            boss.AffixAssimilator.Assimilate(PossessionManager.Instance.CurrentBody);
    }

    private bool IsDevourTarget(Enemy candidate)
    {
        if (candidate == null || candidate == owner) return false;
        if (IsCorpseDevourTarget(candidate)) return true;
        return owner != null && owner.CanDamage(candidate);
    }

    private static bool IsCorpseDevourTarget(Enemy candidate)
    {
        return candidate != null && candidate.Body == MonsterActor.BodyState.Downed;
    }

    private void PlayBlastVfx(Vector3 position)
    {
        if (blastVfxPrefab == null) return;
        GameObject blast = VfxPool.Instance.Spawn(blastVfxPrefab, position, Quaternion.identity);
        BulletTimeController.MarkVfxOrigin(blast, IsOwnedByPlayer);
        ScaleAbilityObject(blast);
        PlayVfx(blast);
        StopVfxLooping(blast);
        ReleaseVfx(blast, Mathf.Max(0.01f, blastVfxDuration));
    }

    protected override GameObject SpawnVfx()
    {
        SpawnWeaponVfx();
        if (vfxPrefab == null || owner == null) return null;

        if (_vfxMotionRoutine != null)
        {
            StopCoroutine(_vfxMotionRoutine);
            _vfxMotionRoutine = null;
        }
        if (activeVfx != null) ReleaseVfx(activeVfx);

        Transform anchor = vfxSpawnPoint != null ? vfxSpawnPoint : owner.transform;
        activeVfx = VfxPool.Instance.Spawn(vfxPrefab, anchor.position, anchor.rotation, anchor);
        BulletTimeController.MarkVfxOrigin(activeVfx, IsOwnedByPlayer);
        activeVfx.transform.localPosition = vfxPositionOffset;
        activeVfx.transform.localRotation = Quaternion.Euler(vfxRotationOffset);
        Vector3 authoredScale = activeVfx.transform.localScale * OwnerCombatScaleMultiplier;
        activeVfx.transform.localScale = authoredScale * vfxStartEndScale;
        PlayVfx(activeVfx);
        _vfxMotionRoutine = StartCoroutine(AnimateDevourVfx(activeVfx, authoredScale));
        return activeVfx;
    }

    private IEnumerator AnimateDevourVfx(GameObject vfx, Vector3 authoredScale)
    {
        Transform motion = vfx.transform;
        Vector3 startPosition = vfxPositionOffset;
        Vector3 peakPosition = startPosition + vfxForwardUpOffset;
        float expandDuration = Mathf.Max(0.01f, vfxExpandDuration);
        float returnDuration = Mathf.Max(0.01f, vfxReturnDuration);

        yield return AnimateDevourVfxPhase(motion, startPosition, peakPosition,
            authoredScale * vfxStartEndScale, authoredScale * vfxPeakScale, expandDuration);
        yield return AnimateDevourVfxPhase(motion, peakPosition, startPosition,
            authoredScale * vfxPeakScale, authoredScale * vfxStartEndScale, returnDuration);

        if (vfx != null) ReleaseVfx(vfx);
        if (activeVfx == vfx) activeVfx = null;
        _vfxMotionRoutine = null;
    }

    private IEnumerator AnimateDevourVfxPhase(Transform motion, Vector3 fromPosition,
        Vector3 toPosition, Vector3 fromScale, Vector3 toScale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && motion != null)
        {
            elapsed += AbilityDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            motion.localPosition = Vector3.LerpUnclamped(fromPosition, toPosition, progress);
            motion.localScale = Vector3.LerpUnclamped(fromScale, toScale, progress);
            yield return null;
        }

        if (motion != null)
        {
            motion.localPosition = toPosition;
            motion.localScale = toScale;
        }
    }

    private bool TrySwallowProjectile()
    {
        float effectiveRange = ScaleAbilityRadius(range);
        float effectiveSwallowRadius = ScaleAbilityRadius(projectileSwallowRadius);
        Vector3 center = owner.transform.position + owner.transform.forward * (effectiveRange * 0.5f);
        CombatHitboxDebug.DrawSphere(drawHitboxes, center, effectiveSwallowRadius, -1f);
        CombatHitboxDebug.DrawArc(drawHitboxes, owner.transform.position, owner.transform.forward, effectiveRange, angle, -1f);
        Collider[] hits = Physics.OverlapSphere(center, effectiveSwallowRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (Collider hit in hits)
        {
            Projectile projectile = hit.GetComponentInParent<Projectile>();
            if (projectile == null) continue;
            if (projectile.isPlayerProjectile == owner.isPossessed) continue;
            if (projectile.damage > maxSwallowProjectileDamage) continue;

            Vector3 to = projectile.transform.position - owner.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.0001f && Vector3.Angle(owner.transform.forward, to) > angle * 0.5f)
                continue;

            VfxPool.ReleaseOrDestroy(projectile.gameObject);
            return true;
        }
        return false;
    }

    private void CacheOwnerState()
    {
        if (owner == null) return;
        _state = owner.GetComponent<GluttonyBodyState>();
        if (_state == null) _state = owner.gameObject.AddComponent<GluttonyBodyState>();
    }
}
