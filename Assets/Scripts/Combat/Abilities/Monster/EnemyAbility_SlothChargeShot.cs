using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Sloth basic: hold to charge, release a green blast that scales radius and damage.
/// Card Sloth.Scatter: on enemy hit, scatter fragments that ignore the first target.
/// </summary>
public class EnemyAbility_SlothChargeShot : EnemyAbility
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    [Tooltip("Optional projectile spawn Transform. Falls back to the owner's forward/up offset when unassigned.")]
    public Transform projectileSpawnPoint;
    public float projectileWidth = 1.5f;
    public float projectileHeight = 2f;
    public float projectileSpeed = 30f;
    public float maxRange = 15f;
    [Tooltip("Boss projectile dimensions and blast radius use this multiplier instead of the generic combat scale.")]
    public float bossProjectileScaleMultiplier = 1.5f;
    [Tooltip("Boss-only cadence for the short-shot then cannon sequence.")]
    public float bossPatternCooldown = 6f;

    [Header("Impact VFX")]
    [Tooltip("Optional VFX spawned on every Enemy damaged by the charged-shot blast.")]
    public GameObject impactVfxPrefab;
    public float impactVfxDuration = 1f;

    [Header("Shoot Feedback (Combat Effect Manager)")]
    [Tooltip("Post-process / shake / hit-stop played when the charged shot is released. Fires for possessed Sloth only.")]
    public HitFeedbackParams shootFeedback = new HitFeedbackParams
    {
        shakeOnHit = false,
        hitStopOnHit = false,
        postProcessOnHit = false
    };

    [Header("Shoot Recoil")]
    [Tooltip("Optional Transform that receives local-position recoil when the charged shot is released.")]
    public Transform recoilTarget;
    [Tooltip("Local displacement at full charge. Use negative Z for backward recoil.")]
    public Vector3 maxRecoilOffset = new Vector3(0f, 0f, -0.2f);
    public float recoilKickDuration = 0.05f;
    public float recoilReturnDuration = 0.15f;

    [Header("Charge")]
    [Tooltip("开启后蓄力期间持续显示红条（实时跟随实际发射方向），发射瞬间消失；关闭则保持旧行为（蓄力前一次性引导红条）。")]
    public bool telegraphDuringCharge = false;
    public float maxChargeTime = 2f;
    public float minChargeScale = 1f;
    public float maxChargeScale = 3f;
    public float minBlastRadius = 1.5f;
    public float maxBlastRadius = 4f;
    public float minDamage = 2f;
    public float maxDamage = 100f;
    public GameObject chargeVfxPrefab;
    [Tooltip("Optional Transform the charge VFX follows. Falls back to the Sloth owner when unassigned.")]
    public Transform chargeVfxSpawnPoint;
    [Tooltip("Local position offset from the Charge VFX Spawn Point.")]
    public Vector3 chargeVfxPositionOffset;

    [Header("Targeting")]
    public LayerMask targetMask = -1;
    [Tooltip("保留以兼容既有 prefab 配置。怠惰朝向已改为始终跟随鼠标（MonsterActor.alwaysFaceAimWhenPossessed），出膛时不再使用该转向速率。")]
    public float aimTurnSpeed = 720f;

    [Header("AI Pacing")]
    [Tooltip("AI 每次开炮后的走位/转向窗口（秒）。引导(站桩)+蓄力+冷却0 会占满整个循环，为 0 时怪物永久站桩且朝向冻结在首次锁定方向（UpdateLocomotion 在 LocomotionLocked 时连转向一并清零）。")]
    [SerializeField, Min(0f)] float aiRecoveryInterval = 0.5f;

    [Header("Upgrade - Sloth.Scatter")]
    public float scatterBulletMult = 2f;
    public float scatterBulletScale = 0.5f;
    public float scatterBulletSpeed = 15f;
    public float scatterBulletRange = 6f;
    public float scatterBulletYOffset = 1f;

    [Header("Canonical Sloth Cards")]
    public int fanProjectileCount = 3;
    public float fanSpreadAngle = 24f;
    public float crushScaleThreshold = 2f;

    private bool isCharging;
    private bool isFiringRoutineActive;
    private Coroutine fireShotRoutine;

    private float chargeTimer;
    /// <summary>AI 怪物本次蓄力的目标时长（0 ~ maxChargeTime 随机），蓄到即自动出手。</summary>
    private float aiChargeTargetTime;
    private float lastChargeTime;
    private GameObject chargeVfxInstance;
    private Coroutine recoilRoutine;
    private Coroutine bossPatternRoutine;
    private Vector3 recoilBasePosition;
    private bool hasRecoilBasePosition;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "地爆天星";
        cooldown = owner is BossSevenfoldActor ? bossPatternCooldown : 0f;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, "Ability.Monster.Sloth.ChargeShot", System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add("Ability.Monster.Sloth.ChargeShot");
        EnsureUpgrade("SL-A03");
        EnsureUpgrade("SL-A04");
        EnsureUpgrade("SL-A05");

    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        if (owner is BossSevenfoldActor)
            return bossPatternRoutine == null;
        if (owner != null && owner.isPossessed) return false;
        return owner != null && owner.targetPlayer != null;
    }

    /// <summary>
    /// 蓄力中 / 出膛协程未走完 / Boss 连射序列进行中视为释放未结束：
    /// 附身代价致死时先把这一发打出去，再结算死亡。
    /// </summary>
    public override bool IsActivationInProgress => isCharging || isFiringRoutineActive || bossPatternRoutine != null;
    protected override bool UsesDeferredEnemyActivation => !(owner is BossSevenfoldActor);

    void Update()
    {
        base.Update();
        if (owner == null) return;
        // 怠惰躯体朝向始终跟随鼠标（走位与射击解耦），出膛前不再做转向。
        if (!owner.alwaysFaceAimWhenPossessed)
            owner.alwaysFaceAimWhenPossessed = true;
        if (owner is BossSevenfoldActor) return;

        bool wantFire = false;
        if (owner.isPossessed)
            wantFire = Input.GetMouseButton(0);
        else
            wantFire = owner.targetPlayer != null;

        bool canStart = currentCooldown <= 0f && !owner.isDowned;
        // 附身代价致死宽限期：耐久已归零，不得再起新一轮蓄力（进行中的这轮不受影响）。
        if (owner.IsAbilityCostDeathPending) canStart = false;
        string reason;
        if (owner.Combat != null && !owner.Combat.CanActivate(this, requiredTags, out reason))
            canStart = false;

        if (wantFire && (isCharging || canStart))
        {
            if (!isCharging)
            {
                // 统一先走基类引导（0.8s 红条），引导完成后再进入蓄力。
                // telegraphDuringCharge 开启时，蓄力期间红条继续贯穿（见 TryBeginCharge / UpdateChargeTelegraph），
                // 因此即使蓄力随机到 0，红条也至少有引导时长兜底。
                if (!TryPrepareDeferredEnemyActivation()) return;
                ConsumeDeferredEnemyActivation();
                if (!TryBeginCharge()) return;
            }

            chargeTimer += AbilityDeltaTime;
            UpdateChargeTelegraph();
            if (chargeVfxInstance != null)
            {
                float ct = Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, maxChargeTime));
                        chargeVfxInstance.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 2f, ct) * OwnerCombatScaleMultiplier;
            }

            // AI 怪物蓄到随机目标时长自动出手（否则会因 targetPlayer 恒存在而无限蓄力不出手）
            if (!owner.isPossessed && chargeTimer >= aiChargeTargetTime)
            {
                StartFireShot(chargeTimer);
                StopCharging();
                return;
            }
        }
        else if (isCharging)
        {
            StartFireShot(chargeTimer);
            StopCharging();
        }
    }

    /// <summary>进入蓄力（公共初始化：activation effect、蓄力计时、HP 代价、蓄力 VFX，以及可选的红条）。</summary>
    private bool TryBeginCharge()
    {
        if (!TryBeginActivationEffect()) return false;
        isCharging = true;
        chargeTimer = 0f;
        // AI 怪物：本次蓄力目标时长在 0 ~ maxChargeTime 之间随机（下限 0，可能立即出手），
        // 蓄到即自动发射；玩家附身仍走按住/松开逻辑，不参与随机。
        if (!owner.isPossessed)
            aiChargeTargetTime = Random.Range(0f, maxChargeTime);
        currentCooldown = 0f;
        owner.PayAbilityHpCost(this);

        if (chargeVfxPrefab != null)
        {
            Transform anchor = chargeVfxSpawnPoint != null ? chargeVfxSpawnPoint : owner.transform;
            chargeVfxInstance = Instantiate(chargeVfxPrefab, anchor);
            if (chargeVfxInstance != null)
            {
                chargeVfxInstance.transform.localPosition = chargeVfxPositionOffset;
                PlayVfx(chargeVfxInstance);
            }
        }

        BeginChargeTelegraph();
        return true;
    }

    /// <summary>开关2：蓄力开始时手动显示红条（仅 AI 非附身）。</summary>
    private void BeginChargeTelegraph()
    {
        if (!telegraphDuringCharge || owner == null || owner.isPossessed || !enemyIndicatorEnabled) return;
        MonsterAbilityTelegraph visual = EnemyTelegraphVisual;
        if (visual == null) return;
        visual.Begin(this, GetEnemyTelegraphGeometry(), true);
    }

    /// <summary>开关2：蓄力期间每帧刷新红条方向（跟随实际发射方向）；中途被附身/击倒则隐藏。</summary>
    private void UpdateChargeTelegraph()
    {
        if (!telegraphDuringCharge || owner == null) return;
        MonsterAbilityTelegraph visual = EnemyTelegraphVisual;
        if (visual == null) return;
        if (owner.isPossessed || owner.isDowned)
        {
            visual.End();
            return;
        }
        visual.RefreshGeometry(GetEnemyTelegraphGeometry());
        visual.SetProgress(Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, maxChargeTime)));
    }

    /// <summary>开关2：蓄力结束（发射/被打断）时隐藏红条。</summary>
    private void EndChargeTelegraph()
    {
        if (!telegraphDuringCharge) return;
        MonsterAbilityTelegraph visual = EnemyTelegraphVisual;
        if (visual != null) visual.End();
    }

    /// <summary>
    /// 出膛：怠惰朝向已始终跟随鼠标（MonsterActor.alwaysFaceAimWhenPossessed），
    /// 出膛前不再需要转向等待，因此直接同步结算，不再起协程。
    /// 这样也从根本上消除了「高频射击时多个转向协程并发拉扯朝向、转向锁永久残留」的隐患。
    /// </summary>
    void StartFireShot(float chargeTime)
    {
        // 兼容旧路径：若仍有残留协程（如从旧版本热重载进来），先停掉并复位转向锁。
        if (fireShotRoutine != null)
        {
            StopCoroutine(fireShotRoutine);
            fireShotRoutine = null;
            if (owner != null) owner.IsAbilityFacingLocked = false;
        }

        // isFiringRoutineActive 维持 IsActivationInProgress 语义：出膛结算期间为真。
        isFiringRoutineActive = true;
        FireShot(chargeTime);
        isFiringRoutineActive = false;
    }

    void FireShot(float chargeTime)
    {
        if (projectilePrefab == null || owner == null) return;

        lastChargeTime = chargeTime;
        float t = Mathf.Clamp01(chargeTime / Mathf.Max(0.01f, maxChargeTime));
        // 发射瞬间按蓄力档位播 Light/Heavy（玩家/AI/Boss 共用）。分档音源与阈值配在
        // 音频配置中心 → 怪物技能音 → Sloth 怠惰 → 普攻（ClipSet 选「蓄力分档」）。
        CombatAudioManager.PlayCastAudio(owner, AbilityType.BasicAttack, owner.transform.position, t);
        float scale = Mathf.Lerp(minChargeScale, maxChargeScale, t);
        float radius = Mathf.Lerp(minBlastRadius, maxBlastRadius, t);
        float shotDamage = Mathf.Lerp(minDamage, maxDamage, t);
        if (damage > 0f) shotDamage = Mathf.Max(shotDamage, damage * Mathf.Lerp(1f, maxDamage / Mathf.Max(1f, minDamage), t));
        float projectileScaleMultiplier = owner is BossSevenfoldActor
            ? bossProjectileScaleMultiplier
            : OwnerCombatScaleMultiplier;

        Vector3 forward = owner.transform.forward;
        // AI 态：出膛瞬间对准玩家，避免蓄力期间目标走位导致弹道打空。
        // 附身态无需处理——MonsterActor.alwaysFaceAimWhenPossessed 已让朝向每帧跟随鼠标。
        // 两个排除条件：
        // - Boss：其出膛路径会先调用 FaceBossTarget(GetBossTargetPosition())，
        //   目标口径为「玩家附身的躯体」，与 targetPlayer（按 tag 查找）并不等价，
        //   在此覆盖会破坏 Boss 刚算好的朝向。
        // - IsAbilityFacingLocked：尊重转向锁，避免打断其它技能正在进行的转向。
        if (!owner.isPossessed && !(owner is BossSevenfoldActor)
            && !owner.IsAbilityFacingLocked && owner.targetPlayer != null)
        {
            Vector3 toTarget = owner.targetPlayer.position - owner.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                forward = toTarget.normalized;
                owner.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }
        Vector3 origin = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : owner.transform.position + forward * 1f + Vector3.up * 1f;

        var go = SpawnVfxTracked(projectilePrefab, origin, Quaternion.LookRotation(forward, Vector3.up));
        go.transform.localScale *= scale * projectileScaleMultiplier;
        foreach (ParticleSystem particleSystem in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        if (IsUpgradeUnlocked("SL-A04"))
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(GetCardParameter("FanProjectileCount", fanProjectileCount)));
            float totalSpread = GetCardParameter("FanSpreadAngle", fanSpreadAngle);
            float perProjectileDamage = Mathf.Max(3f, shotDamage / count);
            ReleaseVfx(go);
            for (int i = 0; i < count; i++)
            {
                float angle = count == 1 ? 0f : Mathf.Lerp(-totalSpread * 0.5f, totalSpread * 0.5f, i / (float)(count - 1));
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * forward;
                GameObject fanProjectile = SpawnVfxTracked(projectilePrefab, origin, Quaternion.LookRotation(direction, Vector3.up));
                fanProjectile.transform.localScale *= scale * scatterBulletScale * projectileScaleMultiplier;
                StartCoroutine(ProjectileTravel(fanProjectile, direction, origin, radius * scatterBulletScale, scale * scatterBulletScale, perProjectileDamage, projectileScaleMultiplier));
            }
        }
        else
        {
            StartCoroutine(ProjectileTravel(go, forward, origin, radius, scale, shotDamage, projectileScaleMultiplier));
        }


        if (owner.isPossessed && shootFeedback != null && shootFeedback.HasAnyEnabled)
            CombatEffectManager.PlayHitFeedback(shootFeedback, owner.transform);

        if (recoilTarget != null)
        {
            if (recoilRoutine != null) StopCoroutine(recoilRoutine);
            if (hasRecoilBasePosition) recoilTarget.transform.localPosition = recoilBasePosition;
            recoilRoutine = StartCoroutine(PlayRecoil(t));
        }

        var anim = owner.GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Basic");
    }

    /// <summary>
    /// 禁用基类单一 cast 音：Sloth 发射音由 FireShot 按蓄力档位（Light/Heavy）播放，
    /// 避免 Boss 经基类 Trigger 时额外播一次旧 castAudioName / 查表音。
    /// </summary>
    protected override void PlayCastSound() { }

    private IEnumerator PlayRecoil(float chargeFraction)
    {
        if (recoilTarget == null) yield break;

        recoilBasePosition = recoilTarget.transform.localPosition;
        hasRecoilBasePosition = true;
        Vector3 basePosition = recoilBasePosition;
        Vector3 recoilPosition = basePosition + maxRecoilOffset * chargeFraction;
        float kickDuration = Mathf.Max(0.01f, recoilKickDuration);
        float returnDuration = Mathf.Max(0.01f, recoilReturnDuration);

        float elapsed = 0f;
        while (elapsed < kickDuration && recoilTarget != null)
        {
            elapsed += AbilityDeltaTime;
            recoilTarget.transform.localPosition = Vector3.Lerp(basePosition, recoilPosition, Mathf.Clamp01(elapsed / kickDuration));
            yield return null;
        }

        elapsed = 0f;
        while (elapsed < returnDuration && recoilTarget != null)
        {
            elapsed += AbilityDeltaTime;
            recoilTarget.transform.localPosition = Vector3.Lerp(recoilPosition, basePosition, Mathf.Clamp01(elapsed / returnDuration));
            yield return null;
        }

        if (recoilTarget != null) recoilTarget.transform.localPosition = basePosition;
        hasRecoilBasePosition = false;
        recoilRoutine = null;
    }

    IEnumerator ProjectileTravel(GameObject projectileGo, Vector3 forward, Vector3 origin, float radius, float scale, float shotDamage, float projectileScaleMultiplier)
    {
        float traveled = 0f;
        int layerMask = owner.isPossessed ? ~0 : targetMask;

        float effectiveMaxRange = ScaleAbilityRadius(maxRange);
        while (traveled < effectiveMaxRange && projectileGo != null)
        {
            float step = projectileSpeed * AbilityDeltaTime;
            traveled += step;
            Vector3 currentPos = origin + forward * Mathf.Min(traveled, effectiveMaxRange);
            projectileGo.transform.position = currentPos;

            Vector3 halfExtents = new Vector3(projectileWidth * 0.5f * scale * projectileScaleMultiplier,
                projectileHeight * 0.5f * scale * projectileScaleMultiplier, step * 0.5f);
            Vector3 checkCenter = currentPos - forward * (step * 0.5f);
            Quaternion checkRot = Quaternion.LookRotation(forward, Vector3.up);
            CombatHitboxDebug.DrawBox(drawHitboxes, checkCenter, halfExtents, checkRot, 0f);

            if (IsUpgradeUnlocked("SL-A05") && scale >= crushScaleThreshold)
                TryCrushIncomingProjectile(checkCenter, forward, scale);

            Collider[] hits = Physics.OverlapBox(checkCenter, halfExtents, checkRot, layerMask, QueryTriggerInteraction.Collide);
            bool hitSomething = false;

            Enemy primaryHit = null;
            Vector3 hitPos = currentPos;

            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<Enemy>();
                if (owner.CanDamage(enemy))
                {
                    hitSomething = true;
                    primaryHit = enemy;
                    hitPos = enemy.transform.position;
                    break;
                }
                var ph = hit.GetComponentInParent<PlayerHealth>();
                if (ph != null && owner.CanDamageSoul())
                {
                    hitSomething = true;
                    hitPos = ph.transform.position;
                    break;
                }
            }

            if (hitSomething)
            {
                DoBlast(hitPos, radius, scale, shotDamage, primaryHit, projectileScaleMultiplier);
                ReleaseVfx(projectileGo);
                yield break;
            }

            yield return null;
        }

        if (projectileGo != null)
        {
            DoBlast(projectileGo.transform.position, radius, scale, shotDamage, null, projectileScaleMultiplier);
            ReleaseVfx(projectileGo);
        }
    }

    void DoBlast(Vector3 pos, float radius, float scale, float shotDamage, Enemy scatterIgnore, float projectileScaleMultiplier)
    {
        float blastRadius = radius * projectileScaleMultiplier / Mathf.Max(0.01f, OwnerCombatScaleMultiplier);
        HashSet<Enemy> hitEnemies = DamageEnemiesInSphere(pos, blastRadius, shotDamage, null);
        foreach (Enemy hitEnemy in hitEnemies)
        {
            if (impactVfxPrefab == null) continue;

            GameObject impact = SpawnVfxTracked(impactVfxPrefab, hitEnemy.transform.position, Quaternion.identity, impactVfxDuration);
            if (impact == null) continue;

            impact.transform.localScale *= scale * projectileScaleMultiplier;
            foreach (ParticleSystem particleSystem in impact.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
        TryDamagePlayerInRadius(pos, blastRadius, shotDamage);

        if (!IsUpgradeUnlocked("SL-A03") || projectilePrefab == null)
            return;

        var exclude = scatterIgnore != null ? new HashSet<Enemy> { scatterIgnore } : null;

        int bulletCount = Mathf.CeilToInt(lastChargeTime * scatterBulletMult);
        Vector3 bulletSpawnPos = pos + Vector3.up * scatterBulletYOffset;
        for (int i = 0; i < bulletCount; i++)
        {
            Vector3 randomDir = owner != null ? owner.AiRandomUnitSphere() : Random.onUnitSphere;
            randomDir.y = Mathf.Abs(randomDir.y);
            randomDir.Normalize();
            var bullet = SpawnVfxTracked(projectilePrefab, bulletSpawnPos, Quaternion.LookRotation(randomDir, Vector3.up));
            bullet.transform.localScale = Vector3.one * scatterBulletScale * OwnerCombatScaleMultiplier;
            StartCoroutine(ScatterBulletTravel(bullet, bulletSpawnPos, randomDir, scatterBulletRange, scatterBulletScale, shotDamage, exclude));
        }
    }

    IEnumerator ScatterBulletTravel(GameObject bullet, Vector3 origin, Vector3 dir, float range, float scale, float shotDamage, HashSet<Enemy> excludeEnemies)
    {
        float traveled = 0f;
        int layerMask = owner.isPossessed ? ~0 : targetMask;
        float fragmentDamage = shotDamage * scatterBulletScale;

        while (traveled < range && bullet != null)
        {
            float step = scatterBulletSpeed * AbilityDeltaTime;
            traveled += step;
            Vector3 currentPos = origin + dir * Mathf.Min(traveled, range);
            bullet.transform.position = currentPos;

            CombatHitboxDebug.DrawSphere(drawHitboxes, currentPos, 0.5f * scale, 0f);
            Collider[] hits = Physics.OverlapSphere(currentPos, 0.5f * scale * OwnerCombatScaleMultiplier, layerMask, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                var enemy = hit.GetComponentInParent<Enemy>();
                if (owner.CanDamage(enemy) && (excludeEnemies == null || !excludeEnemies.Contains(enemy)))
                {
                    SettleHit(enemy, fragmentDamage);
                    ReleaseVfx(bullet);
                    yield break;
                }
                var ph = hit.GetComponentInParent<PlayerHealth>();
                if (ph != null && owner.CanDamageSoul())
                {
                    SettleHit(ph, fragmentDamage);
                    ReleaseVfx(bullet);
                    yield break;
                }
            }
            yield return null;
        }
        if (bullet != null) ReleaseVfx(bullet);
    }

    private void TryCrushIncomingProjectile(Vector3 center, Vector3 forward, float ownScale)
    {
        Collider[] candidates = Physics.OverlapSphere(center, projectileWidth * ownScale * 0.5f * OwnerCombatScaleMultiplier, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < candidates.Length; i++)
        {
            Projectile incoming = candidates[i].GetComponentInParent<Projectile>();
            if (incoming == null || incoming.ownerEnemy == owner) continue;
            Vector3 direction = incoming.transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f && Vector3.Dot(direction.normalized, forward) >= 0f) continue;
            if (incoming.transform.lossyScale.magnitude >= transform.lossyScale.magnitude * ownScale) continue;
            VfxPool.ReleaseOrDestroy(incoming.gameObject);
            return;
        }
    }

    private void EnsureUpgrade(string effectId)
    {
        if (upgrades == null) upgrades = new List<UpgradeSlot>();
        if (upgrades.Exists(slot => slot != null && string.Equals(slot.effectId, effectId, System.StringComparison.OrdinalIgnoreCase))) return;
        upgrades.Add(new UpgradeSlot { effectId = effectId, unlocked = false });
    }

    void StopCharging()

    {
        isCharging = false;
        EndChargeTelegraph();
        EndActivationEffect();
        chargeTimer = 0f;
        // AI 态出手后保留走位/转向窗口：telegraph(站桩读条)+蓄力+冷却0 占满循环时，
        // 怪物将永久站桩且朝向冻结（UpdateLocomotion 在 LocomotionLocked 时连转向一并清零）。
        // 仅 AI 非附身生效；附身与 Boss 节奏不变。
        currentCooldown = owner != null && !owner.isPossessed
            ? Mathf.Max(EffectiveCooldown, aiRecoveryInterval)
            : EffectiveCooldown;

        if (chargeVfxInstance != null)
        {
            ReleaseVfx(chargeVfxInstance);
            chargeVfxInstance = null;
        }
    }

    protected override void OnTrigger()
    {
        BossSevenfoldActor boss = owner as BossSevenfoldActor;
        if (boss == null || bossPatternRoutine != null) return;
        bossPatternRoutine = StartCoroutine(BossPatternRoutine(boss));
    }

    /// <summary>
    /// 地爆天星（蓄力重炮）是直线飞弹：红圈用矩形预警带（长度=maxRange、宽度=projectileWidth、朝向=owner.forward），
    /// 与 FireShot 的实际弹道判定一致（红圈=实际范围）。受 enemyIndicatorEnabled 开关控制。
    /// </summary>
    public override EnemyTelegraphGeometry GetEnemyTelegraphGeometry()
    {
        if (owner == null || !enemyIndicatorEnabled) return default;

        Vector3 forward = owner.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
        forward.Normalize();

        float length = ScaleAbilityRadius(maxRange);
        // 红条宽跟随当前蓄力 scale，与 FireShot / ProjectileTravel 的实际判定盒宽一致（projectileWidth × scale）。
        float chargeScale = Mathf.Lerp(minChargeScale, maxChargeScale, Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, maxChargeTime)));
        float width = ScaleAbilityRadius(projectileWidth * chargeScale);
        return new EnemyTelegraphGeometry
        {
            shape = EnemyIndicatorShape.Rect,
            center = owner.transform.position + forward * (length * 0.5f),
            forward = forward,
            length = length,
            width = width,
            isValid = length > 0f && width > 0f
        };
    }

    private IEnumerator BossPatternRoutine(BossSevenfoldActor boss)
    {
        boss.SetAbilitySequenceLocked(true);
        const float shortFireDuration = 1.5f;
        const float shortFireInterval = 0.3f;
        float elapsed = 0f;
        while (owner != null && elapsed < shortFireDuration)
        {
            boss.FaceBossTarget(boss.GetBossTargetPosition());
            FireShot(0f);

            float intervalElapsed = 0f;
            while (owner != null && intervalElapsed < shortFireInterval)
            {
                boss.FaceBossTarget(boss.GetBossTargetPosition());
                intervalElapsed += AbilityDeltaTime;
                yield return null;
            }
            elapsed += shortFireInterval;
        }

        if (owner != null && chargeVfxPrefab != null)
        {
            Transform anchor = chargeVfxSpawnPoint != null ? chargeVfxSpawnPoint : owner.transform;
            chargeVfxInstance = Instantiate(chargeVfxPrefab, anchor);
            if (chargeVfxInstance != null)
            {
                chargeVfxInstance.transform.localPosition = chargeVfxPositionOffset;
                PlayVfx(chargeVfxInstance);
            }
        }

        elapsed = 0f;
        const float cannonChargeDuration = 1.5f;
        while (owner != null && elapsed < cannonChargeDuration)
        {
            elapsed += AbilityDeltaTime;
            if (chargeVfxInstance != null)
            {
                float charge = Mathf.Clamp01(elapsed / cannonChargeDuration);
                chargeVfxInstance.transform.localScale = Vector3.one
                    * Mathf.Lerp(0.5f, 2f, charge) * OwnerCombatScaleMultiplier;
            }
            yield return null;
        }

        if (owner != null)
        {
            FireShot(maxChargeTime);
        }
        FinishBossPattern(boss);
    }

    private void FinishBossPattern(BossSevenfoldActor boss)
    {
        if (chargeVfxInstance != null)
        {
            ReleaseVfx(chargeVfxInstance);
            chargeVfxInstance = null;
        }
        if (boss != null) boss.SetAbilitySequenceLocked(false);
        bossPatternRoutine = null;
        EndActivationEffect();
    }

    protected override void OnDisable()
    {
        if (bossPatternRoutine != null) StopCoroutine(bossPatternRoutine);
        BossSevenfoldActor boss = owner as BossSevenfoldActor;
        if (boss != null) boss.SetAbilitySequenceLocked(false);
        bossPatternRoutine = null;
        if (isCharging) StopCharging();
        else if (chargeVfxInstance != null)
        {
            ReleaseVfx(chargeVfxInstance);
            chargeVfxInstance = null;
        }
        if (fireShotRoutine != null) StopCoroutine(fireShotRoutine);
        fireShotRoutine = null;
        if (recoilRoutine != null) StopCoroutine(recoilRoutine);
        if (recoilTarget != null && hasRecoilBasePosition) recoilTarget.transform.localPosition = recoilBasePosition;
        recoilRoutine = null;
        hasRecoilBasePosition = false;
        isFiringRoutineActive = false;
        base.OnDisable();
    }
}
