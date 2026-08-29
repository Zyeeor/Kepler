using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Envy Attack: continuous laser that writes target Marks.
/// Possessed auto-aims at the nearest legal enemy; Enemy AI locks the current player.
/// Ported beam VFX/tick loop from legacy EnemyAbility_Laser; Mark/cashout rules are Canonical.
/// </summary>
public class EnemyAbility_EnvyLaser : EnemyAbility
{
    public const string AbilityTag = "Ability.Monster.Envy.Laser";
    public const string GuardBlockTag = "State.Defense.GreedGuard";

    [Header("Laser")]
    public float maxRange = 15f;
    [Tooltip("Baseline DPS before EN-A05 ramp (Canonical 2/sec).")]
    public float damagePerSecond = 2f;
    public float tickInterval = 0.25f;
    [Tooltip("Baseline Mark storage cap before EN-R01 (TUNABLE).")]
    public float markStorageCap = 100f;
    [Tooltip("Baseline write ratio of effective damage into Mark (Canonical 20%).")]
    public float markWriteRatio = 0.2f;
    [Tooltip("EN-R04 grace seconds after disconnect. 0 = clear immediately.")]
    public float markGraceDuration = 0f;
    [Tooltip("Baseline max continuous connect window. EN-A04 raises via ConnectDuration.")]
    public float maxConnectDuration = 1f;

    [Header("Enemy Tracking (Pass v1 §13.3)")]
    [Tooltip("Enemy 版激光有限追踪的转向速度（°/s）。Beam 不再无限瞬时锁头；玩家可横移/绕侧甩掉，丢失目标后本次 Cast 结束。")]
    [Min(1f)] public float enemyTrackingTurnSpeed = 100f;
    [Tooltip("前摇期间动态 Lock 警示线的最细宽度倍数（progress=0 时），到 progress=1 时线性过渡到 1（真实 Beam 宽度）。")]
    [Range(0f, 1f)] public float lockLineMinWidthMultiplier = 0.35f;
    [Tooltip("前摇期间 Lock 警示线的颜色（偏弱，可用透明度区分强弱；为空则沿用 beamMaterial 默认）。")]
    public Material lockLineMaterial;

    [Header("Enemy Lock Range Cap (Pass v1.1 §3)")]
    [Tooltip("Enemy/Elite 激光锁定距离安全上限（米）。最终锁定 Range = min(当前 EffectiveRange, 此值)，0 表示不封顶。仅作用于非 Boss、非附身的 Enemy；Player 版与 Boss 版不受影响。")]
    [Min(0f)] public float enemyLaserTargetRangeCap = 15f;
    [Tooltip("Beam 期间玩家超出此距离（米）即断束。建议 16–17m（略高于锁定上限，留出走位余量）。0 表示不断束。仅作用于非 Boss、非附身的 Enemy。")]
    [Min(0f)] public float enemyLaserBreakRange = 17f;

    public GameplayEffectDefinition markEffect;
    public GameplayEffectDefinition laserHitEffect;

    [Header("EN-A05 Ramp")]
    public float rampMaxDamagePerSecond = 50f;
    public float rampTimeToMax = 8f;
    [Tooltip("EN-A05 外显：连续连接伤害爬升时整条激光宽度的最大放大倍数。")]
    public float rampWidthMultiplier = 2.5f;

    [Header("EN-A01 Multi Eye")]
    public float multiEyeInterval = 3f;
    public float multiEyeWindow = 0.6f;
    public int multiEyeTargetCount = 4;

    [Header("EN-A03 Pierce")]
    public float piercePhaseInterval = 4f;
    public float piercePhaseDuration = 1f;

    [Header("Beam VFX")]
    public GameObject beamPrefab;
    public Material beamMaterial;
    public Vector3 beamPositionOffset = new Vector3(0f, 0.3f, 0f);
    public Vector3 beamRotationOffset = Vector3.zero;

    [Header("Hit VFX")]
    public GameObject hitImpactPrefab;
    public float hitImpactDuration = 0.3f;

    private bool _isFiring;
    private CombatAudioManager.SfxLoopHandle _castLoop;
    private float _damageTimer;
    private float _fireDuration;
    private float _hpCostTimer;
    private float _rampTimer;
    private GameObject _hitVfx;
    private Vector3 _enemyBeamDirection;
    private readonly HashSet<Enemy> _connectedThisBurst = new HashSet<Enemy>();
    private readonly List<Enemy> _lastMarked = new List<Enemy>();

    // Pass v1 §13.3：前摇动态 Lock 警示线（持续对象，前摇期间每帧更新位置/宽度，开火/取消时释放）。
    private GameObject _lockLineVfx;
    private float _lockLineBaseScaleZ = 1f;

    private void OnEnable()
    {
        type = AbilityType.BasicAttack;
        abilityName = "激光";
        cooldown = cooldown < 0f ? 0f : cooldown;
        if (abilityTags == null) abilityTags = new List<string>();
        if (!abilityTags.Exists(t => string.Equals(t, AbilityTag, System.StringComparison.OrdinalIgnoreCase)))
            abilityTags.Add(AbilityTag);
    }

    protected override void Update()
    {
        base.Update();
        if (owner == null) return;
        BossSevenfoldActor boss = owner as BossSevenfoldActor;
        if (boss != null && boss.IsAbilitySequenceLocked)
        {
            if (_isFiring) StopLaser();
            SetAnimBoolCached(owner.GetActiveAnimator(), "IsFiring", false);
            return;
        }

        bool wantFire;
        if (owner.isPossessed)
            wantFire = Input.GetMouseButton(0) && !PlayerController.IsGameplayInputBlocked && Time.timeScale > 0f;
        else
            wantFire = owner.targetPlayer != null
                       && Vector3.Distance(owner.transform.position, owner.targetPlayer.position) <= GetEffectiveRange();

        if (wantFire && CanTrigger())
        {
            if (!_isFiring)
            {
                if (!TryPrepareDeferredEnemyActivation()) return;
                ConsumeDeferredEnemyActivation();
                if (!TryBeginActivationEffect()) return;
                _isFiring = true;
                _enemyBeamDirection = Vector3.zero;   // Pass v1 §13.3：本次 Cast 首帧指向玩家，同一 Cast 不立即重新锁回
                _damageTimer = 0f;
                _fireDuration = 0f;
                _hpCostTimer = 0f;
                _rampTimer = 0f;
                _connectedThisBurst.Clear();
                currentCooldown = 0f;

                Animator[] animators = owner.GetComponentsInChildren<Animator>(false);
                for (int i = 0; i < animators.Length; i++)
                    animators[i].SetTrigger("Basic");

                // 持续激光循环音：开火 Start（音频配置中心 Envy→普攻 条目 loop=true 时生效，否则静默）
                _castLoop = CombatAudioManager.StartCastLoop(owner, type, owner.transform.position);
            }

            UpdateLaser();
        }
        else if (_isFiring)
        {
            StopLaser();
        }

        // Animator（参数存在性缓存：避免每帧遍历 anim.parameters 分配新数组）
        SetAnimBoolCached(owner.GetActiveAnimator(), "IsFiring", _isFiring);
    }

    public override bool CanTrigger()
    {
        if (!base.CanTrigger()) return false;
        return owner != null && (owner.isPossessed || owner.targetPlayer != null);
    }

    // ── Pass v1 §13.3：前摇动态 Lock 警示线（由弱到强，持续跟随玩家，开火时进入真实 Beam）──

    protected override void OnEnemyTelegraphBegin()
    {
        // 前摇开始：从发射点连到玩家，初始最弱（细）。
        RefreshLockLine(0f);
    }

    protected override void OnEnemyTelegraphTick(float progress)
    {
        // 前摇期间每帧跟随玩家并线性增强宽度，表现"锁定由弱到强"。
        RefreshLockLine(progress);
    }

    protected override void OnEnemyTelegraphEnd()
    {
        ReleaseLockLine();
    }

    /// <summary>
    /// Pass v1.1 §3：前摇期间玩家离开合法锁定范围（= min(EffectiveRange, cap)）即取消本次施放。
    /// 仅作用于非 Boss、非附身的 Enemy；Player 版不进入前摇，Boss 版不受上限约束。
    /// </summary>
    protected override bool ShouldCancelEnemyTelegraph()
    {
        if (owner == null || owner.isPossessed || owner is BossSevenfoldActor || owner.targetPlayer == null)
            return false;
        float dist = Vector3.Distance(owner.transform.position, owner.targetPlayer.position);
        return dist > GetEffectiveRange();
    }

    /// <summary>生成本次 Cast 的 Lock 警示线，或更新其位置/朝向/宽度（复用 Beam 的 LineRenderer 承载）。</summary>
    void RefreshLockLine(float progress)
    {
        if (beamPrefab == null || owner == null || owner.targetPlayer == null) return;

        Vector3 origin = GetBeamOrigin();
        Vector3 target = owner.targetPlayer.position + Vector3.up;
        Vector3 dir = target - origin;
        // 距离 = 本体→目标的实际距离，夹到有效射程：目标移动时长度动态变化，且绝不越界。
        float dist = Mathf.Min(dir.magnitude, GetEffectiveRange());
        if (dist < 0.01f) return;

        Quaternion rot = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(beamRotationOffset);

        if (_lockLineVfx == null)
        {
            _lockLineVfx = SpawnVfxTracked(beamPrefab, origin, rot, -1f);
            if (_lockLineVfx == null) return;
            // 基准 z 取 prefab 原始缩放（不含 ScaleAbilityObject 施加的 OwnerCombatScaleMultiplier）：
            // 后续每帧用「基准 z × 长度比」赋值，既避免持续对象反复累乘，也保证长度不随体型放大。
            _lockLineBaseScaleZ = beamPrefab.transform.localScale.z;
        }
        else
        {
            _lockLineVfx.transform.SetPositionAndRotation(origin, rot);
        }

        // 长度精确落在玩家位置（复用真实 Beam 的归一化逻辑）。
        float authoredLength = ResolveBeamAuthoredLength(_lockLineVfx);
        Vector3 scale = _lockLineVfx.transform.localScale;
        scale.z = _lockLineBaseScaleZ * dist / Mathf.Max(0.01f, authoredLength);
        _lockLineVfx.transform.localScale = scale;

        // 由弱到强：宽度从 lockLineMinWidthMultiplier 线性过渡到 1（真实 Beam 宽度）。
        float widthMult = Mathf.Lerp(lockLineMinWidthMultiplier, 1f, Mathf.Clamp01(progress));
        ApplyBeamWidth(_lockLineVfx, widthMult);

        if (lockLineMaterial != null)
        {
            foreach (ParticleSystem ps in _lockLineVfx.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.material = lockLineMaterial;
            }
        }
    }

    void ReleaseLockLine()
    {
        if (_lockLineVfx != null)
        {
            ReleaseVfx(_lockLineVfx, hitImpactDuration);
            _lockLineVfx = null;
            _lockLineBaseScaleZ = 1f;
        }
    }

    /// <summary>持续开火中视为释放未结束：附身代价致死时等这束激光熄火后再死。</summary>
    public override bool IsActivationInProgress => _isFiring;
    protected override bool UsesDeferredEnemyActivation => true;

    protected override void OnTrigger() { }

    public void ApplyMarkTo(Enemy target, bool dealDamage)
    {
        if (target == null || owner == null || !owner.CanDamage(target)) return;
        EnvyMarkTarget mark = EnvyMarkTarget.EnsureOn(target);
        if (mark == null) return;

        float cap = markStorageCap;
        if (IsUpgradeUnlocked("EN-R01"))
            cap *= 1f + GetCardParameter("MarkStorageBonus", 0.5f);

        float ratio = GetCardParameter("MarkWriteRatio", markWriteRatio);
        if (IsUpgradeUnlocked("EN-R02"))
            ratio = GetCardParameter("MarkWriteRatio", Mathf.Min(1f, markWriteRatio + 0.15f));

        float grace = markGraceDuration;
        if (IsUpgradeUnlocked("EN-R04"))
            grace = GetCardParameter("MarkGraceDuration", Mathf.Max(1.5f, markGraceDuration));

        mark.ApplyOrRefresh(owner, cap, ratio, grace, markEffect);
        mark.CancelGraceKeepMark();
        if (!_lastMarked.Contains(target)) _lastMarked.Add(target);
    }

    private void UpdateLaser()
    {
        float connectCap = GetCardParameter("ConnectDuration", maxConnectDuration);
        if (IsUpgradeUnlocked("EN-A04"))
            connectCap = GetCardParameter("ConnectDuration", maxConnectDuration + 4f);

        _fireDuration += AbilityDeltaTime;
        _rampTimer += AbilityDeltaTime;
        if (_fireDuration > connectCap)
        {
            StopLaser();
            currentCooldown = EffectiveCooldown;
            return;
        }

        _hpCostTimer += AbilityDeltaTime;
        if (_hpCostTimer >= 1f)
        {
            owner.PayAbilityHpCost(this);
            _hpCostTimer -= 1f;
        }

        Vector3 origin = GetBeamOrigin();
        Vector3 aimPoint = owner.isPossessed ? GetAimPoint(origin) : GetEnemyTrackedAimPoint(origin);
        if (!owner.isPossessed && (IsEnemyTargetLost(origin) || IsEnemyOutOfBreakRange(origin)))
        {
            // Pass v1 §13.3：玩家甩掉激光（绕侧/横移超速）→ 本次 Cast 结束，下次 Cast 重新锁定。
            // Pass v1.1 §3：Beam 期间玩家超出 Break Range（断束）同样结束本次 Cast。
            StopLaser();
            currentCooldown = EffectiveCooldown;
            return;
        }
        bool pierceActive = IsPierceActive();
        float tickDamage = GetTickDamage();

        _damageTimer += AbilityDeltaTime;
        if (_damageTimer < tickInterval) return;
        _damageTimer -= tickInterval;

        List<Enemy> hitTargets = new List<Enemy>();
        Vector3 beamEnd = aimPoint;
        bool blocked = ResolveBeamHits(origin, aimPoint, pierceActive, hitTargets, out beamEnd);

        SpawnBeamVfx(origin, beamEnd);

        bool anyLegalHit = false;
        for (int i = 0; i < hitTargets.Count; i++)
        {
            Enemy target = hitTargets[i];
            if (target == null) continue;
            anyLegalHit = true;
            DealDamageTo(target, tickDamage);
            ApplyMarkTo(target, dealDamage: true);
            _connectedThisBurst.Add(target);
            if (laserHitEffect != null && target.Combat != null)
                target.Combat.ApplyEffect(laserHitEffect, owner.Combat, abilityTags, out _);
        }

        if (!owner.isPossessed && owner.targetPlayer != null && hitTargets.Count == 0 && !blocked)
        {
            // AI beam aimed at player may miss colliders; still settle soul damage on line-of-sight range.
            float dist = Vector3.Distance(origin, owner.targetPlayer.position);
            if (dist <= GetEffectiveRange())
            {
                PlayerHealth ph = owner.targetPlayer.GetComponent<PlayerHealth>();
                if (ph != null) DealDamageToPlayer(ph, tickDamage);
            }
        }

        if (!anyLegalHit)
        {
            // Empty fire: keep beam, but do not write Mark / ramp EN-A05.
            _connectedThisBurst.Clear();
            _fireDuration = 0f;
            _rampTimer = 0f; // 断链：EN-A05 宽度爬升归零，下次连接重新 ramp up
        }

        if (IsMultiEyeActive())
            FireMultiEye(origin, tickDamage, hitTargets);

        UpdateHitVfx(beamEnd);
    }

    private bool IsPierceActive()
    {
        if (!IsUpgradeUnlocked("EN-A03")) return false;
        float window = GetCardParameter("PierceDuration", piercePhaseDuration);
        float interval = Mathf.Max(window + 0.1f, GetCardParameter("PierceInterval", piercePhaseInterval));
        float cycle = AbilityTime % interval;
        return cycle <= window;
    }

    private bool IsMultiEyeActive()
    {
        if (!IsUpgradeUnlocked("EN-A01")) return false;
        float window = GetCardParameter("MultiEyeWindow", multiEyeWindow);
        float interval = Mathf.Max(window + 0.1f, GetCardParameter("MultiEyeInterval", multiEyeInterval));
        float cycle = AbilityTime % interval;
        return cycle <= window;
    }

    private void FireMultiEye(Vector3 origin, float tickDamage, List<Enemy> primaryHits)
    {
        List<Enemy> candidates = new List<Enemy>();
        foreach (var e in EnemyRegistry.All)
        {
            if (e == null || !owner.CanDamage(e)) continue;
            if (Vector3.Distance(origin, e.transform.position) > GetEffectiveRange()) continue;
            candidates.Add(e);
        }

        candidates.Sort((a, b) =>
            Vector3.Distance(origin, a.transform.position).CompareTo(Vector3.Distance(origin, b.transform.position)));

        // 万眼同视：链接 Beam VFX 到所有敌人。主光束已覆盖 primary 命中，其余每个敌人各接一束。
        foreach (Enemy target in candidates)
        {
            if (primaryHits.Contains(target)) continue;
            Vector3 end = target.transform.position + Vector3.up;
            SpawnBeamVfx(origin, end);
            DealDamageTo(target, tickDamage);
            ApplyMarkTo(target, dealDamage: true);
        }
    }

    private bool ResolveBeamHits(Vector3 origin, Vector3 aimPoint, bool pierce, List<Enemy> hits, out Vector3 beamEnd)
    {
        hits.Clear();
        Vector3 dir = aimPoint - origin;
        float maxDist = Mathf.Min(dir.magnitude, GetEffectiveRange());
        if (maxDist < 0.01f)
        {
            beamEnd = origin + owner.transform.forward * 0.1f;
            return false;
        }

        dir.Normalize();
        CombatHitboxDebug.DrawRay(drawHitboxes, origin, dir, maxDist, 0f);
        RaycastHit[] results = Physics.RaycastAll(origin, dir, maxDist, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(results, (a, b) => a.distance.CompareTo(b.distance));

        bool blocked = false;
        beamEnd = origin + dir * maxDist;
        for (int i = 0; i < results.Length; i++)
        {
            RaycastHit hit = results[i];
            CombatAbilityComponent combat = hit.collider.GetComponentInParent<CombatAbilityComponent>();
            if (combat != null && combat.Tags != null && combat.Tags.HasTag(GuardBlockTag))
            {
                // Guard truncates; truncated segment does not write Mark.
                beamEnd = hit.point;
                blocked = true;
                break;
            }

            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (enemy == null || !owner.CanDamage(enemy)) continue;
            if (!hits.Contains(enemy)) hits.Add(enemy);
            beamEnd = hit.point;
            if (!pierce) break;
        }

        return blocked;
    }

    private float GetRampFactor()
    {
        if (!IsUpgradeUnlocked("EN-A05") || _connectedThisBurst.Count <= 0) return 0f;
        return Mathf.Clamp01(_rampTimer / Mathf.Max(0.01f, GetCardParameter("RampTime", rampTimeToMax)));
    }

    private float GetTickDamage()
    {
        float dps = damagePerSecond;
        float ramp = GetRampFactor();
        if (ramp > 0f)
        {
            float maxDps = GetCardParameter("RampMaxDps", rampMaxDamagePerSecond);
            dps = Mathf.Lerp(damagePerSecond, maxDps, ramp);
        }

        return dps * tickInterval;
    }

    private float GetEffectiveRange()
    {
        float range = maxRange;
        if (IsUpgradeUnlocked("EN-TG01"))
            range += GetCardParameter("AttackRangeBonus", 4f);
        // Boss visual scale must not turn Envy's beam into a longer threat.
        float effective = owner is BossSevenfoldActor ? range : ScaleAbilityRadius(range);
        // Pass v1.1 §3：Enemy/Elite 视觉缩放会把 15m 放大到 ~30m（叠加 EN-TG01 快照可达 ~38m），
        // 造成全图锁人。此处对非 Boss、非附身的 Enemy 锁定距离封顶；Player 版与 Boss 版不受影响。
        if (owner != null && !owner.isPossessed && !(owner is BossSevenfoldActor) && enemyLaserTargetRangeCap > 0f)
            effective = Mathf.Min(effective, enemyLaserTargetRangeCap);
        return effective;
    }

    private Vector3 GetBeamOrigin()
    {
        return owner.transform.position + Vector3.up + beamPositionOffset;
    }

    private Vector3 GetAimPoint(Vector3 origin)
    {
        float range = GetEffectiveRange();
        if (owner.isPossessed)
        {
            Enemy nearest = FindNearestEnemy(origin, range);
            if (nearest != null)
            {
                Vector3 target = nearest.transform.position + Vector3.up;
                Vector3 delta = target - origin;
                if (delta.magnitude > range) delta = delta.normalized * range;
                return origin + delta;
            }

            return origin + owner.transform.forward * range;
        }

        if (owner.targetPlayer != null)
        {
            Vector3 target = owner.targetPlayer.position + Vector3.up;
            Vector3 delta = target - origin;
            if (delta.magnitude > range) delta = delta.normalized * range;
            return origin + delta;
        }

        return origin + owner.transform.forward * range;
    }

    /// <summary>
    /// Pass v1 §13.3：Enemy 版有限追踪瞄准。Beam 以 enemyTrackingTurnSpeed 转向玩家，不再无限瞬时锁头；
    /// 首次开火初始化指向玩家，之后每帧 RotateTowards 平滑追踪，玩家横移/绕侧可甩掉。
    /// </summary>
    private Vector3 GetEnemyTrackedAimPoint(Vector3 origin)
    {
        float range = GetEffectiveRange();
        if (owner.targetPlayer == null)
            return origin + _enemyBeamDirection * range;

        Vector3 desired = owner.targetPlayer.position + Vector3.up - origin;
        desired.y = 0f;
        if (desired.sqrMagnitude < 0.0001f)
            return origin + _enemyBeamDirection * range;
        desired.Normalize();

        if (_enemyBeamDirection.sqrMagnitude < 0.0001f)
            _enemyBeamDirection = desired;

        float maxAngle = Mathf.Max(0f, enemyTrackingTurnSpeed) * AbilityDeltaTime;
        _enemyBeamDirection = Vector3.RotateTowards(_enemyBeamDirection, desired, maxAngle * Mathf.Deg2Rad, 0f);
        if (_enemyBeamDirection.sqrMagnitude < 0.0001f) _enemyBeamDirection = desired;
        return origin + _enemyBeamDirection.normalized * range;
    }

    /// <summary>玩家已甩掉激光：beam 方向与玩家方向夹角超过 90°（玩家绕到侧后方）。</summary>
    private bool IsEnemyTargetLost(Vector3 origin)
    {
        if (owner == null || owner.targetPlayer == null) return owner != null && owner.targetPlayer == null;
        Vector3 toPlayer = owner.targetPlayer.position + Vector3.up - origin;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return false;
        return Vector3.Angle(_enemyBeamDirection, toPlayer.normalized) > 90f;
    }

    /// <summary>
    /// Pass v1.1 §3：Beam 期间玩家超出 Break Range（建议 16–17m）即断束。
    /// 仅作用于非 Boss、非附身的 Enemy；Player 版与 Boss 版不受影响。
    /// </summary>
    private bool IsEnemyOutOfBreakRange(Vector3 origin)
    {
        if (owner == null || owner.isPossessed || owner is BossSevenfoldActor || owner.targetPlayer == null)
            return false;
        if (enemyLaserBreakRange <= 0f) return false;
        float dist = Vector3.Distance(origin, owner.targetPlayer.position);
        return dist > enemyLaserBreakRange;
    }

    private Enemy FindNearestEnemy(Vector3 origin, float range)
    {
        Enemy nearest = null;
        float nearestSqrDistance = float.MaxValue;
        float maxSqrDistance = range * range;

        foreach (Enemy enemy in EnemyRegistry.All)
        {
            if (enemy == null || !owner.CanDamage(enemy)) continue;

            Vector3 offset = enemy.transform.position - origin;
            float sqrDistance = offset.sqrMagnitude;
            if (sqrDistance <= maxSqrDistance && sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private void SpawnBeamVfx(Vector3 origin, Vector3 targetPos)
    {
        if (beamPrefab == null) return;
        Vector3 dir = targetPos - origin;
        Quaternion rot = (dir.sqrMagnitude > 0.01f
            ? Quaternion.LookRotation(dir.normalized, Vector3.up)
            : Quaternion.identity) * Quaternion.Euler(beamRotationOffset);
        GameObject vfx = SpawnVfxTracked(beamPrefab, origin, rot, tickInterval);
        if (vfx == null) return;

        // 光束由本地空间 LineRenderer 承载（蓝-激光 等，本地 0→authoredLength）。
        // 距离 = 本体→目标的实际距离，夹到有效射程：目标移动时长度动态变化，且绝不越界超过 max range。
        float dist = Mathf.Min(dir.magnitude, GetEffectiveRange());
        float authoredLength = ResolveBeamAuthoredLength(vfx);
        // 长度用「prefab 原始 z × 长度比」绝对值赋值：SpawnVfxTracked 的 ScaleAbilityObject
        // 已把整条 scale 乘过 OwnerCombatScaleMultiplier，若继续累乘会让光束长度随怪物体型放大、
        // 超过目标距离与 max range。x/y 仍保留体型缩放（只影响粗细），z 精确落在 dist 处。
        float baseZ = beamPrefab.transform.localScale.z;
        Vector3 scale = vfx.transform.localScale;
        scale.z = baseZ * (dist / Mathf.Max(0.01f, authoredLength));
        vfx.transform.localScale = scale;

        // EN-A05 外显：连续连接伤害爬升时按比例加宽整条激光，封顶到 rampWidthMultiplier。
        // 用「创作宽度 × widthMult」直接赋值而非累乘：对象池复用的实例不会残留上次宽度，
        // 到上限（ramp=1 → widthMult=rampWidthMultiplier）后不再继续变宽；断链 ramp=0 回基准。
        // 主光束与 EN-A01 散射激光共用本路径，因此上限同样约束所有散射激光。
        float ramp = GetRampFactor();
        float widthMult = Mathf.Lerp(1f, GetCardParameter("RampWidthMult", rampWidthMultiplier), ramp);
        ApplyBeamWidth(vfx, widthMult);

        if (beamMaterial != null)
        {
            foreach (ParticleSystem ps in vfx.GetComponentsInChildren<ParticleSystem>())
            {
                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.material = beamMaterial;
            }
        }
    }

    private static float ResolveBeamAuthoredLength(GameObject vfx)
    {
        if (vfx == null) return 1f;
        LineRenderer[] renderers = vfx.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            LineRenderer lr = renderers[i];
            if (lr == null || lr.positionCount < 2) continue;
            float len = (lr.GetPosition(lr.positionCount - 1) - lr.GetPosition(0)).magnitude;
            if (len > 0.001f) return len;
        }
        return 1f;
    }

    /// <summary>
    /// 按 beamPrefab 的创作宽度 × widthMult 直接赋值（不累乘）。
    /// 对象池复用实例不会保留上一次 ramp 后的 widthMultiplier，保证宽度严格封顶在
    /// authoredWidth × rampWidthMultiplier，到上限后不再继续变宽。
    /// </summary>
    private void ApplyBeamWidth(GameObject vfx, float widthMult)
    {
        if (vfx == null || beamPrefab == null) return;
        LineRenderer[] prefabLrs = beamPrefab.GetComponentsInChildren<LineRenderer>(true);
        LineRenderer[] instanceLrs = vfx.GetComponentsInChildren<LineRenderer>(true);
        int count = Mathf.Min(prefabLrs.Length, instanceLrs.Length);
        for (int i = 0; i < count; i++)
        {
            if (prefabLrs[i] == null || instanceLrs[i] == null) continue;
            instanceLrs[i].widthMultiplier = prefabLrs[i].widthMultiplier * widthMult;
        }
    }

    private void UpdateHitVfx(Vector3 hitPos)
    {
        if (hitImpactPrefab == null) return;
        if (_hitVfx == null) _hitVfx = SpawnVfxTracked(hitImpactPrefab, hitPos, Quaternion.identity);
        else _hitVfx.transform.position = hitPos;
    }

    private void StopLaser()
    {
        _isFiring = false;
        _enemyBeamDirection = Vector3.zero;   // Pass v1 §13.3：下次 Cast 重新锁定
        ReleaseLockLine();                     // Pass v1 §13.3：兜底清理 Lock 线（开火中断等非 TelegraphEnd 路径）
        CombatAudioManager.StopCastLoop(_castLoop);
        _castLoop = default;
        EndActivationEffect();

        float grace = markGraceDuration;
        if (IsUpgradeUnlocked("EN-R04"))
            grace = GetCardParameter("MarkGraceDuration", Mathf.Max(1.5f, markGraceDuration));

        for (int i = 0; i < _lastMarked.Count; i++)
        {
            Enemy e = _lastMarked[i];
            if (e == null) continue;
            EnvyMarkTarget mark = e.GetComponent<EnvyMarkTarget>();
            if (mark == null || mark.Source != owner) continue;
            if (grace > 0f) mark.BeginGrace();
            else mark.Clear();
        }

        _lastMarked.Clear();
        _connectedThisBurst.Clear();
        if (_hitVfx != null)
        {
            ReleaseVfx(_hitVfx, hitImpactDuration);
            _hitVfx = null;
        }
    }

    protected override void OnDisable()
    {
        if (_isFiring) StopLaser();
        ReleaseLockLine();
        if (owner != null)
            EnvyMarkTarget.ClearMarksFromSource(owner);
        base.OnDisable();
    }
}
