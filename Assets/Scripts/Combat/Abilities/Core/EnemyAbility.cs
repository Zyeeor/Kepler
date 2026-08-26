using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// Base class for all enemy abilities (passive / basic attack / skill).
/// Attach one or more of these to an enemy prefab. They self-register with the parent Enemy on Awake.
/// </summary>
public abstract class EnemyAbility : MonoBehaviour
{
    public enum AbilityType { Passive, BasicAttack, Skill, Mobility }

    [Serializable]
    public class UpgradeSlot
    {
        [Tooltip("Unique effect ID matching a CardData.effectId.")]
        public string effectId;
        [Tooltip("Is this upgrade permanently unlocked for the current run? Set by CardManager.")]
        public bool unlocked;
    }

    [Header("Identity")]
    public string abilityName = "Ability";
    public AbilityType type = AbilityType.Passive;

    [Header("Upgrades")]
    [Tooltip("Each upgrade slot: effectId + unlocked checkbox. Set by CardManager.")]
    public List<UpgradeSlot> upgrades = new List<UpgradeSlot>();

    /// <summary>Check if a specific upgrade is unlocked (case-insensitive).</summary>
    public bool IsUpgradeUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id) || upgrades == null) return false;
        foreach (var u in upgrades)
            if (u != null && !string.IsNullOrEmpty(u.effectId) && u.effectId.Equals(id, StringComparison.OrdinalIgnoreCase) && u.unlocked)
                return true;
        return false;
    }

    /// <summary>Multiplier applied to the possessed HP cost when this ability is paid.</summary>
    public virtual float GetHpCostMultiplier()
    {
        return 1f;
    }

    protected float GetCardParameter(string key, float defaultValue)
    {
        // 精英怪（携带 EliteBuildCarrier）：只从自身历史 BD 快照解析参数，
        // 不读取当前 Run 的 Card 层（Canonical §23）。
        var carrier = EliteBuildCarrier.Get(this);
        if (carrier != null)
            return carrier.TryGetCardParameter(this, key, out float eliteValue) ? eliteValue : defaultValue;
        return CardManager.Instance != null && CardManager.Instance.TryGetUnlockedAbilityParameter(this, key, out float value)
            ? value
            : defaultValue;
    }

    public bool HasAllAbilityTags(IEnumerable<string> queryTags)
    {
        return GameplayTagUtility.HasAll(abilityTags, queryTags);
    }

    public void AddAppliedEffectTags(IEnumerable<string> effectTags)
    {
        if (effectTags == null) return;
        foreach (string rawTag in effectTags)
        {
            string effectTag = GameplayTagUtility.Normalize(rawTag);
            if (string.IsNullOrEmpty(effectTag) || appliedEffectTags.Exists(value => string.Equals(value, effectTag, StringComparison.OrdinalIgnoreCase))) continue;
            appliedEffectTags.Add(effectTag);
        }
    }

    [Header("VFX")]
    [Tooltip("Body VFX spawned when the ability triggers.")]
    public GameObject vfxPrefab;
    public Transform vfxSpawnPoint;
    [Tooltip("Weapon VFX spawned when the ability triggers. Bound to this Ability, not to Effects.")]
    public GameObject weaponVfxPrefab;
    [Tooltip("Optional weapon anchor. Falls back to VFX Spawn Point, then owner root.")]
    public Transform weaponVfxSpawnPoint;
    [Tooltip("Local position offset added to the spawn point (relative to anchor's transform).")]
    public Vector3 vfxPositionOffset = Vector3.zero;
    [Tooltip("Delay in seconds before the VFX spawns. 0 = instant.")]
    public float vfxDelay = 0f;
    [Tooltip("Rotation offset for the VFX (e.g. (-90,0,0) if your VFX faces Y-up but you need Z-forward)")]
    public Vector3 vfxRotationOffset = Vector3.zero;

    [Header("Damage (if applicable)")]
    public float damage = 0f;
    [Header("Hitbox Debug")]
    [Tooltip("Legacy per-ability flag (unused for gating). Global toggle lives on GameManager → CombatHitboxDebugSettings.")]
    public bool drawHitboxes;

    /// <summary>Cooldown in seconds. 0 = no cooldown. Only meaningful for BasicAttack / Skill / Mobility.</summary>
    public float cooldown = 0f;
    [Header("Cooldown Debug")]
    [Tooltip("Log cooldown trigger/blocked events for this ability (for verifying AI attack cadence).")]
    public bool debugLogCooldown = false;

    [Header("Attack Behavior Tags")]
    [Tooltip("Stable identity tags for this attack behavior. Cards use these tags to target an ability without depending on its display name.")]
    public List<string> abilityTags = new List<string>();
    [Tooltip("Effect Tags applied to targets hit through this ability's shared damage helpers. Cards may add to this list at runtime.")]
    public List<string> appliedEffectTags = new List<string>();

    [Header("Activation Requirements")]
    [Tooltip("All listed tags must be active on the owner to activate this ability. Empty means no requirement.")]
    public List<string> requiredTags = new List<string>();
    [Tooltip("Effect applied to this ability owner when activation starts. Its granted Tags and duration define casting control, such as State.Control.Stunned.")]
    public GameplayEffectDefinition activationEffect;

    [Header("Audio (Combat Audio Manager)")]
    [Tooltip("施放音（SfxId 下拉选择，clip 在 SfxBank 资产配置）。空 = 静默。")]
    [SfxIdName]
    public string castAudioName;
    [Tooltip("首次命中音（SfxId 下拉选择，clip 在 SfxBank 资产配置）。空 = 静默。")]
    [SfxIdName]
    public string hitAudioName;

    [Header("Hit Feedback (Combat Effect Manager)")]
    [Tooltip("Post-process / shake / hit-stop on hit. Fires for possessed (player-controlled) attacks only, once per Trigger.")]
    public HitFeedbackParams hitFeedback = new HitFeedbackParams
    {
        shakeOnHit = true,
        hitStopOnHit = true,
        postProcessOnHit = false
    };

    /// <summary>Actual cooldown after attack speed modifier is applied.</summary>
    public float EffectiveCooldown
    {
        get
        {
            float spd = owner != null ? owner.attackSpeed : 1f;
            if (spd <= 0f) spd = 0.01f;
            float value = cooldown / spd;
            if (PossessionImprintManager.Instance != null)
                value *= PossessionImprintManager.Instance.GetCooldownMultiplier(owner);
            return value;
        }
    }

    protected Enemy owner;
    protected float currentCooldown;
    public float CurrentCooldown { get { return currentCooldown; } }

    /// <summary>
    /// 本次释放是否仍在进行中（蓄力中、持续开火中、冲刺中等"判定尚未结算完"的阶段）。
    /// 附身 HP 代价致死时，MonsterActor 用它把死亡结算推迟到本次释放判定完成之后，
    /// 避免"血量刚好不足一次技能"时技能被打断而白扣血。
    /// 默认 false：一次性 / 短延迟命中类技能由 MonsterActor 的代价死亡宽限窗口覆盖，
    /// 只有充能 / 持续类能力需要覆写此属性。
    /// </summary>
    public virtual bool IsActivationInProgress { get { return false; } }

    /// <summary>能力归属的怪物（Run Analytics 采集用：判断是否当前玩家控制的身体触发）。</summary>
    public MonsterActor OwnerMonster => owner;

    /// <summary>Scale used by possessed-body ability visuals and hitboxes.</summary>
    protected float OwnerCombatScaleMultiplier
    {
        get
        {
            BossSevenfoldActor boss = owner as BossSevenfoldActor;
            if (boss != null) return boss.BossCombatScaleMultiplier;
            MonsterActor monster = owner as MonsterActor;
            return monster != null ? monster.CombatScaleMultiplier : 1f;
        }
    }

    protected float ScaleAbilityRadius(float value)
    {
        return Mathf.Max(0f, value) * OwnerCombatScaleMultiplier;
    }

    protected Vector3 ScaleAbilitySize(Vector3 value)
    {
        return value * OwnerCombatScaleMultiplier;
    }

    protected void ScaleAbilityObject(GameObject instance)
    {
        if (instance == null) return;
        instance.transform.localScale *= OwnerCombatScaleMultiplier;
    }

    // ── Animator 参数缓存（Kimi 评审整改：anim.parameters 每次访问分配新数组，高频路径每帧调用造成 GC）──
    Animator cachedBoolAnimator;
    readonly Dictionary<string, bool> cachedAnimParamExists = new Dictionary<string, bool>();

    /// <summary>
    /// 设置 Animator Bool（仅参数存在时）——参数存在性按 animator 实例缓存：
    /// 首次对某 animator 查询后记住结果，后续调用不再遍历 anim.parameters（消除每帧 GC 分配）。
    /// owner 换身/换 animator 时自动重查。
    /// </summary>
    protected void SetAnimBoolCached(Animator anim, string paramName, bool value)
    {
        if (anim == null) return;
        if (cachedBoolAnimator != anim)
        {
            cachedBoolAnimator = anim;
            cachedAnimParamExists.Clear();
        }
        bool exists;
        if (!cachedAnimParamExists.TryGetValue(paramName, out exists))
        {
            exists = false;
            foreach (var p in anim.parameters)
                if (p.name == paramName) { exists = true; break; }
            cachedAnimParamExists[paramName] = exists;
        }
        if (exists) anim.SetBool(paramName, value);
    }
    protected GameObject activeVfx;

    /// <summary>Raised after this ability has successfully started its activation behavior.</summary>
    public event Action<EnemyAbility> Activated;

    /// <summary>
    /// 全局触发广播（Run Analytics 采集用）：任何能力 Trigger 成功时触发（含 AI 与玩家控制）。
    /// 采集器内部按 IsOwnedByPlayer 过滤玩家控制期间的能力使用。
    /// </summary>
    public static event Action<EnemyAbility> OnAnyTriggered;

    /// <summary>Ensures screen shake / hit-stop / post-FX fire at most once per Trigger.</summary>
    private bool _hitFeedbackFiredThisAttack;
    private bool _hitAudioFiredThisAttack;
    protected virtual void Awake()
    {
        owner = GetComponentInParent<Enemy>();
        currentCooldown = 0f;
        if (owner != null) owner.RegisterAbility(this);
    }

    public bool IsOwnedByPlayer => owner != null && owner.IsPlayerControlled;
    protected float AbilityDeltaTime => IsOwnedByPlayer ? Time.unscaledDeltaTime : Time.deltaTime;
    protected float AbilityTime => IsOwnedByPlayer ? Time.unscaledTime : Time.time;
    protected object AbilityWait(float seconds) => owner != null && owner.IsPlayerControlled
        ? (object)new WaitForSecondsRealtime(seconds)
        : new WaitForSeconds(seconds);

    protected bool TryGetPossessedMouseDirection(out Vector3 direction)
    {
        direction = Vector3.zero;
        if (owner == null || !owner.isPossessed || PlayerController.Instance == null) return false;
        if (!PlayerController.Instance.TryGetAimPoint(out Vector3 aimPoint)) return false;

        direction = aimPoint - owner.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.zero;
            return false;
        }

        direction.Normalize();
        return true;
    }

    protected IEnumerator RotatePossessedOwnerTowards(Vector3 direction, float turnSpeed)
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

    protected virtual void Update()
    {
        if (currentCooldown > 0f) currentCooldown -= AbilityDeltaTime;
    }

    protected virtual void OnDisable()
    {
        EndActivationEffect();
        CancelInvoke();
    }

    public virtual void ResetForOwnerReuse()
    {
        EndActivationEffect();
        CancelInvoke();
        currentCooldown = 0f;
        activeVfx = null;
    }

    /// <summary>Returns true if this ability can be triggered right now.</summary>
    /// <summary>
    /// 激活条件查询。【性能规则】CanTrigger 被 AIController 每帧×每怪轮询（AIController.cs 决策循环
    /// 与 MonsterActor 输入轮询），必须保持 O(1) 纯查询：不得在此调用 FindObjectsOfType /
    /// FindGameObjectsWithTag 等场景扫描；目标检索一律用 EnemyRegistry（内存注册表）或移入触发/协程路径。
    /// </summary>
    public virtual bool CanTrigger()
    {
        if (currentCooldown > 0f || owner == null || owner.isDowned)
            return false;
        // 附身 HP 代价致死宽限期：只允许把已经开始的那次释放跑完，不得再起新技能。
        if (owner.IsAbilityCostDeathPending && !IsActivationInProgress)
            return false;

        CombatAbilityComponent combat = owner.Combat;
        string reason = string.Empty;
        return combat == null || combat.CanActivate(this, requiredTags, out reason);
    }

    /// <summary>Trigger the ability. Called by Enemy AI / Player when possessing.</summary>
    public virtual void Trigger()
    {
        if (!TryBeginActivationEffect()) return;

        currentCooldown = EffectiveCooldown;
        _hitFeedbackFiredThisAttack = false;
        _hitAudioFiredThisAttack = false;
        PlayCastSound();
        if (debugLogCooldown)
            Debug.Log($"[Cooldown] {abilityName} triggered @ {Time.time:F2}s | cooldown={cooldown}s effective={EffectiveCooldown:F2}s (attackSpeed={(owner != null ? owner.attackSpeed : 1f)})");
        if (vfxDelay <= 0f)
            SpawnVfx();
        else
            Invoke(nameof(SpawnVfx), vfxDelay);
        OnTrigger();
        Activated?.Invoke(this);
        OnAnyTriggered?.Invoke(this);   // Run Analytics：全局触发广播（采集器内部过滤玩家控制）
    }

    /// <summary>
    /// 播放本能力的施放音：优先能力自身 castAudioName（SfxBank 覆盖，走音效表「3D 定位」），
    /// 否则按 owner.sinType + 技能类别查 MonsterSkillAudioConfig（七罪 × 位移/普攻/技能）。
    /// 抽成受保护方法，供绕过 Trigger 的自驱动能力（如蓄力位移）在真正释放时补播施放音。
    /// </summary>
    protected void PlayCastSound()
    {
        if (!string.IsNullOrWhiteSpace(castAudioName))
            CombatAudioManager.Play(castAudioName, owner != null ? owner.transform.position : transform.position);
        else
            CombatAudioManager.PlayCastAudio(owner, type, owner != null ? owner.transform.position : transform.position);
    }

    /// <summary>Begins this ability's configured Activation Effect. Effect duration controls the state lifetime.</summary>
    protected bool TryBeginActivationEffect()
    {
        CombatAbilityComponent combat = owner != null ? owner.Combat : null;
        return combat == null || combat.TryBeginAbility(this, requiredTags, activationEffect, abilityTags);
    }

    /// <summary>Ends this ability and removes only the Activation Effect instance it created.</summary>
    protected void EndActivationEffect()
    {
        if (owner != null && owner.Combat != null) owner.Combat.EndAbility(this);
    }

    /// <summary>Override to implement ability behavior. Called by Trigger().</summary>
    protected abstract void OnTrigger();

    /// <summary>Spawn the assigned VFX prefab at the spawn point (or enemy root).</summary>
    protected virtual GameObject SpawnVfx()
    {
        SpawnWeaponVfx();
        if (vfxPrefab == null) return null;
        Transform anchor = vfxSpawnPoint != null ? vfxSpawnPoint : (owner != null ? owner.transform : transform);
        Vector3 pos = anchor.position + anchor.TransformDirection(vfxPositionOffset);
        Quaternion rot = anchor.rotation * Quaternion.Euler(vfxRotationOffset);
        activeVfx = VfxPool.Instance.Spawn(vfxPrefab, pos, rot);
        BulletTimeController.MarkVfxOrigin(activeVfx, IsOwnedByPlayer);
        ScaleAbilityObject(activeVfx);
        PlayVfx(activeVfx);
        float duration = ResolveVfxPlayDuration(activeVfx);
        VfxPool.ReleaseOrDestroy(activeVfx, duration);
        return activeVfx;
    }

    protected GameObject SpawnWeaponVfx()
    {
        if (weaponVfxPrefab == null) return null;
        Transform anchor = weaponVfxSpawnPoint != null
            ? weaponVfxSpawnPoint
            : (vfxSpawnPoint != null ? vfxSpawnPoint : (owner != null ? owner.transform : transform));
        Vector3 pos = anchor.position + anchor.TransformDirection(vfxPositionOffset);
        Quaternion rot = anchor.rotation * Quaternion.Euler(vfxRotationOffset);
        GameObject instance = VfxPool.Instance.Spawn(weaponVfxPrefab, pos, rot);
        BulletTimeController.MarkVfxOrigin(instance, IsOwnedByPlayer);
        ScaleAbilityObject(instance);
        PlayVfx(instance);
        float duration = ResolveVfxPlayDuration(instance);
        VfxPool.ReleaseOrDestroy(instance, duration);
        return instance;
    }

    protected Projectile SpawnAbilityProjectile(GameObject prefab, Vector3 position, Quaternion rotation, float shotDamage, float speed, float lifetime)
    {
        if (prefab == null) return null;
        GameObject go = VfxPool.Instance.Spawn(prefab, position, rotation);
        BulletTimeController.MarkVfxOrigin(go, owner != null && owner.isPossessed);
        ScaleAbilityObject(go);
        PlayVfx(go);
        Projectile projectile = go.GetComponent<Projectile>();
        if (projectile == null) projectile = go.AddComponent<Projectile>();
        projectile.sourceAbility = this;
        projectile.ownerEnemy = owner;
        projectile.damage = shotDamage;
        projectile.speed = speed;
        projectile.maxLifetime = lifetime;
        projectile.isPlayerProjectile = owner != null && owner.isPossessed;
        BulletTimeController.MarkVfxOrigin(go, projectile.isPlayerProjectile);
        // After field writes: OnEnable may have reset against prefab maxLifetime.
        projectile.ResetForPoolSpawn();
        return projectile;
    }

    /// <summary>Return a pooled VFX/projectile, or Destroy if it was never pooled.</summary>
    protected static void ReleaseVfx(GameObject vfx)
    {
        VfxPool.ReleaseOrDestroy(vfx);
    }

    /// <summary>Delayed pool return (or Destroy).</summary>
    protected static void ReleaseVfx(GameObject vfx, float delay)
    {
        VfxPool.ReleaseOrDestroy(vfx, delay);
    }

    /// <summary>Play all ParticleSystems on a VFX GameObject.</summary>
    protected void PlayVfx(GameObject vfx)
    {
        if (vfx == null) return;
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>())
            ps.Play(true);
    }

    /// <summary>
    /// One ParticleSystem cycle if readable; otherwise fallback (default 1s).
    /// Looping systems on this instance are switched to play-once so they do not linger.
    /// </summary>
    protected static float ResolveVfxPlayDuration(GameObject vfx, float fallback = 1f)
    {
        float resolved = 0f;
        if (vfx != null)
        {
            foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (main.loop) main.loop = false;
                if (main.duration > resolved) resolved = main.duration;
            }
        }
        if (resolved > 0.01f) return resolved;
        return Mathf.Max(0.01f, fallback);
    }

    protected static void StopVfxLooping(GameObject vfx)
    {
        if (vfx == null) return;
        foreach (var ps in vfx.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            if (main.loop) main.loop = false;
        }
    }

    /// <summary>Spawn a pooled VFX that returns when the owner dies/pools away, or after autoDestroyTime.</summary>
    protected GameObject SpawnVfxTracked(GameObject prefab, Vector3 pos, Quaternion rot, float autoDestroyTime = -1f)
    {
        if (prefab == null || owner == null) return null;
        GameObject go = VfxPool.Instance.Spawn(prefab, pos, rot);
        BulletTimeController.MarkVfxOrigin(go, IsOwnedByPlayer);
        ScaleAbilityObject(go);
        PlayVfx(go);
        DestroyOnOwnerDeath tracker = go.GetComponent<DestroyOnOwnerDeath>();
        if (tracker == null) tracker = go.AddComponent<DestroyOnOwnerDeath>();
        tracker.owner = owner.gameObject;
        if (autoDestroyTime > 0f) ReleaseVfx(go, autoDestroyTime);
        return go;
    }

    /// <summary>Public hit settlement used by projectiles and melee helpers.</summary>
    public void SettleHit(Enemy target, float amount)
    {
        DealDamageTo(target, amount);
    }

    public void SettleHit(PlayerHealth player, float amount)
    {
        DealDamageToPlayer(player, amount);
    }

    /// <summary>Helper: deal damage to a target via Enemy.ApplyDamageTo so it respects lifesteal etc.</summary>
    protected void DealDamageTo(Enemy target, float amount)
    {
        if (target == null || owner == null) return;
        // 战果回传归因（Meta §6.5）：精英能力命中玩家当前附身身体 → 先记录伤害来源再结算
        // （伤害可能同步致命，Body Fatal 事件处理时归因窗口内必须已有记录）
        if (PossessionManager.Instance != null && PossessionManager.Instance.CurrentBody == target as MonsterActor)
        {
            var eliteSource = EliteBuildCarrier.Get(owner);
            if (eliteSource != null) EliteBuildDirector.NoteEliteDamagedPlayer(eliteSource);
        }
        // Pass damage to owner's damage pipeline so passives (e.g. lifesteal) can react
        owner.ApplyOffensiveDamage(target, amount);
        TryPlayHitFeedback(target != null ? target.transform : null);
        ApplyConfiguredEffectsTo(target.Combat);
    }

    protected void DealDamageToPlayer(PlayerHealth player, float amount)
    {
        if (player == null || owner == null || !owner.CanDamageSoul()) return;
        // 战果回传归因（Meta §6.5）：精英能力命中魂 → 先记录伤害来源再结算
        // （TakeDamage 可能同步触发 Soul Death → Run Fail，事件处理时归因窗口内必须已有记录）
        var eliteSource = EliteBuildCarrier.Get(owner);
        if (eliteSource != null) EliteBuildDirector.NoteEliteDamagedPlayer(eliteSource);
        if (!owner.isPossessed) amount *= Mathf.Max(0f, owner.spawnDamageMultiplier);
        player.TakeDamage(amount);
        TryPlayHitFeedback(player != null ? player.transform : null);
        ApplyConfiguredEffectsTo(player.GetComponent<CombatAbilityComponent>());
        // Also trigger lifesteal for the owner enemy
        owner.OnDealtDamage(amount);
    }

    /// <summary>
    /// Plays configured hit feedback once per Trigger while the owner is player-possessed.
    /// AI-controlled enemies skip this to avoid camera spam.
    /// </summary>
    protected void TryPlayHitFeedback(Transform victim)
    {
        TryPlayHitAudio(victim);
        if (_hitFeedbackFiredThisAttack || hitFeedback == null || !hitFeedback.HasAnyEnabled)
            return;
        if (owner == null || !owner.isPossessed)
            return;

        CombatEffectManager.PlayHitFeedback(hitFeedback, owner.transform, victim);
        _hitFeedbackFiredThisAttack = true;
    }

    protected void TryPlayHitAudio(Transform victim)
    {
        if (_hitAudioFiredThisAttack || string.IsNullOrWhiteSpace(hitAudioName))
            return;
        // Play for possessed body skills and soul-routed hits; skip AI spam.
        if (owner == null || !owner.isPossessed)
            return;
        Vector3 pos = victim != null ? victim.position : owner.transform.position;
        CombatAudioManager.Play(hitAudioName, pos);
        _hitAudioFiredThisAttack = true;
    }

    protected void ApplyConfiguredEffectsTo(CombatAbilityComponent target)
    {
        if (target == null || appliedEffectTags == null || appliedEffectTags.Count == 0) return;
        CardManager manager = CardManager.Instance;
        if (manager == null) return;

        foreach (string effectTag in appliedEffectTags)
        {
            GameplayEffectDefinition definition;
            if (!manager.TryGetGameplayEffect(effectTag, out definition)) continue;

            string ignoredReason;
            if (!target.ApplyEffect(definition, owner != null ? owner.Combat : null, abilityTags, out ignoredReason)) continue;
            SpawnAttackEffectVfx(definition, target, target.transform.position);
        }
    }

    private void SpawnAttackEffectVfx(GameplayEffectDefinition definition, CombatAbilityComponent target, Vector3 hitPosition)
    {
        if (definition == null || definition.hitVfxPrefab == null) return;
        GameObject instance = VfxPool.Instance.Spawn(definition.hitVfxPrefab, hitPosition, Quaternion.identity);
        BulletTimeController.MarkVfxOrigin(instance, IsOwnedByPlayer);
        PlayVfx(instance);
        if (definition.hitVfxDuration > 0f)
            ReleaseVfx(instance, definition.hitVfxDuration);
        else if (target != null)
            target.RegisterEffectVfx(definition, instance);
    }

    protected List<Enemy> FindEnemiesInArc(Vector3 origin, Vector3 forward, float range, float angle, int layerMask = ~0, float hitboxDebugDuration = -1f)
    {
        List<Enemy> results = new List<Enemy>();
        range = ScaleAbilityRadius(range);
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return results;
        forward.Normalize();
        CombatHitboxDebug.DrawArc(drawHitboxes, origin, forward, range, angle, hitboxDebugDuration);

        Collider[] hits = Physics.OverlapSphere(origin, range, layerMask, QueryTriggerInteraction.Collide);
        HashSet<Enemy> unique = new HashSet<Enemy>();
        foreach (Collider hit in hits)
        {
            Enemy enemy = hit.GetComponentInParent<Enemy>();
            if (enemy == null || !unique.Add(enemy)) continue;
            Vector3 toTarget = enemy.transform.position - origin;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f && Vector3.Angle(forward, toTarget) <= angle * 0.5f)
                results.Add(enemy);
        }
        return results;
    }

    protected HashSet<Enemy> DamageEnemiesAlongPath(Vector3 start, Vector3 end, float radius, float amount, float hitboxDebugDuration = -1f)
    {
        HashSet<Enemy> results = new HashSet<Enemy>();
        radius = ScaleAbilityRadius(radius);
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        if (distance < 0.0001f) return DamageEnemiesInSphere(start, radius, amount, null, hitboxDebugDuration);
        direction /= distance;
        CombatHitboxDebug.DrawCapsule(drawHitboxes, start, end, radius, hitboxDebugDuration);

        RaycastHit[] hits = Physics.SphereCastAll(start, radius, direction, distance, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            Enemy enemy = hit.collider.GetComponentInParent<Enemy>();
            if (owner != null && owner.CanDamage(enemy) && results.Add(enemy))
                DealDamageTo(enemy, amount);
        }
        return results;
    }

    /// <summary>
    /// Try to find and damage the Player if they are within the given radius from a point.
    /// Returns true if the player was hit. Does NOT depend on targetMask — uses tag lookup.
    /// </summary>
    protected bool TryDamagePlayerInRadius(Vector3 center, float radius, float amount, float hitboxDebugDuration = -1f)
    {
        radius = ScaleAbilityRadius(radius);
        CombatHitboxDebug.DrawSphere(drawHitboxes, center, radius, hitboxDebugDuration);
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return false;
        float dist = Vector3.Distance(center, playerObj.transform.position);
        if (dist <= radius)
        {
            var ph = playerObj.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                DealDamageToPlayer(ph, amount);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Damage all valid enemy targets within an OverlapBox, ignoring targetMask.
    /// This is used when the player possesses an enemy and needs to hit other enemies.
    /// Falls back to tag-based detection so it works regardless of LayerMask config.
    /// </summary>
    protected void DamageEnemiesInBox(Vector3 center, Vector3 halfExtents, Quaternion orientation, float amount, System.Action<Enemy, Vector3> onHit = null, float hitboxDebugDuration = -1f)
    {
        halfExtents = ScaleAbilitySize(halfExtents);
        CombatHitboxDebug.DrawBox(drawHitboxes, center, halfExtents, orientation, hitboxDebugDuration);
        // Use All layers (~0) so we don't miss enemies due to targetMask misconfiguration
        Collider[] hits = Physics.OverlapBox(center, halfExtents, orientation, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var enemy = h.GetComponentInParent<Enemy>();
            if (owner != null && owner.CanDamage(enemy))
            {
                DealDamageTo(enemy, amount);
                onHit?.Invoke(enemy, enemy.transform.position);
            }
        }
    }

    /// <summary>
    /// Damage all valid enemy targets within an OverlapSphere, ignoring targetMask.
    /// Returns the set of enemies that were hit.
    /// </summary>
    protected HashSet<Enemy> DamageEnemiesInSphere(Vector3 center, float radius, float amount, System.Action<Enemy, Vector3> onHit = null, float hitboxDebugDuration = -1f)
    {
        var hitEnemies = new HashSet<Enemy>();
        radius = ScaleAbilityRadius(radius);
        CombatHitboxDebug.DrawSphere(drawHitboxes, center, radius, hitboxDebugDuration);
        Collider[] hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var enemy = h.GetComponentInParent<Enemy>();
            if (owner != null && owner.CanDamage(enemy))
            {
                DealDamageTo(enemy, amount);
                hitEnemies.Add(enemy);
                onHit?.Invoke(enemy, enemy.transform.position);
            }
        }
        return hitEnemies;
    }
}
