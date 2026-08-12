using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

/// <summary>
/// Monster execution body：怪物执行体。
/// 继承自 Actor（moveSpeed / maxHealth / currentHealth 由基类提供），Enemy.cs 保留为壳 `Enemy : MonsterActor {}`
/// 供 prefab 序列化与 GetComponent&lt;Enemy&gt; 调用方使用。
/// BodyState/ControlState 双正交 enum 为只读派生视图（源数据仍是 isDowned/isWeakened/isPossessed bool 字段）。
/// </summary>
public class MonsterActor : Actor
{
    public enum BodyState { Active, Hit, Downed, Fading, Despawned }
    public enum ControlState { AI, Possessed }

    /// <summary>Current combat/lifecycle state. Downed bodies remain possessable only during their configured window.</summary>
    public BodyState Body { get; private set; } = BodyState.Active;
    public bool CanBePossessed => Body == BodyState.Downed && !isPossessed && !isPossessionReserved && Time.time < possessionWindowEndsAt;
    public bool CanCompleteReservedPossession => Body == BodyState.Downed && !isPossessed && isPossessionReserved;
    public float PossessionWindowRemaining => Body == BodyState.Downed && !isPossessionReserved ? Mathf.Max(0f, possessionWindowEndsAt - Time.time) : 0f;

    /// <summary>当前控制状态（是否被玩家控制）。</summary>
    public ControlState Control => IsPlayerControlled ? ControlState.Possessed : ControlState.AI;

    [Serializable]
    public class AbilityHpCost
    {
        [Tooltip("The ability to apply HP cost to.")]
        public EnemyAbility ability;
        [Tooltip("HP cost paid by the possessed enemy when the player triggers this ability.")]
        public float hpCost;
    }

    [Serializable]
    public class BasicAbilityEntry
    {
        public EnemyAbility ability;
        [Tooltip("HP cost paid by the possessed enemy when the player triggers this ability. 0 = free.")]
        public float hpCost;
    }

    [Serializable]
    public class SkillAbilityEntry
    {
        public EnemyAbility ability;
        [Tooltip("HP cost paid by the possessed enemy when the player triggers this ability. 0 = free.")]
        public float hpCost;
    }

    [Serializable]
    public class MobilityAbilityEntry
    {
        public EnemyAbility ability;
    }


    [Header("Identity")]
    public string displayName = "Enemy";

    [Header("Stats (configured on prefab)")]
    public float maxTenacity = 200f;
    // 注：moveSpeed / maxHealth / currentHealth 由 Actor 基类提供（同名同类型，prefab 序列化值按字段名映射到基类字段，无损）
    [Tooltip("Base collision damage when touching the player. Individual ability damage is configured on each EnemyAbility.")]
    public float collisionDamage = 30f;
    public float detectionRadius = 8f;
    [Tooltip("AI will attempt basic attacks when within this range of the player.")]
    public float aiAttackRange = 3f;
    [Tooltip("AI will stop moving closer when within this distance of the player.")]
    public float aiMinRange = 0f;
    [Tooltip("Attack speed multiplier. 1.0 = normal speed. Higher = faster attack cooldown.")]
    public float attackSpeed = 1.0f;
    [Tooltip("AI cast time: seconds between basic attack attempts (before cooldown).")]
    public float basicCastTime = 0.5f;
    [Tooltip("AI cast time: seconds between skill attempts (before cooldown).")]
    public float skillCastTime = 10f;

    [Header("Ability HP Costs (consumed when possessed player uses)")]
    // HP cost is now set directly on each Basic/Skill ability entry below.

    [Header("Visual")]
    public Color bodyColor = Color.red;
    public Color weakenedColor = new Color(1f, 0.5f, 0f);
    public Color downedColor = new Color(0.3f, 0.3f, 0.3f);
    public Color possessedColor = new Color(0.8f, 0.2f, 1f);
    public Color flashColor = Color.red;
    public float flashDuration = 0.1f;

    [Header("State")]
    public bool isWeakened = false;
    public bool isDowned = false;
    [Tooltip("Debug/cheat: skip possession HP decay and ability HP costs while possessed.")]
    public bool suppressPossessionDrain;
    public bool isPossessed = false;
    public bool playerDetected = false;

    [Header("Corpse Lifecycle")]
    [Min(0f)] public float corpsePossessionWindow = 5f;
    [Min(0f)] public float corpseFadeDuration = 3f;
    [Min(0f)] public float hitStateDuration = 0.12f;

    private float possessionWindowEndsAt;
    private float hitStateEndsAt;
    private bool isPossessionReserved;
    private Coroutine corpseRoutine;
    private Renderer[] bodyRenderers;
    private BoxCollider corpsePossessionCollider;
    private MaterialPropertyBlock corpseFadeBlock;
    private const string CorpseColliderObjectName = "__PossessionCorpseCollider";
    private const float CorpseColliderPadding = 0.35f;
    private const float MinimumCorpseColliderSize = 1.25f;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Animator bodyAnimator;
    private AnimatorUpdateMode originalAnimatorUpdateMode;

    [Header("UI")]
    public Slider healthSlider;
    public Canvas healthCanvas;
    [Tooltip("Show health bar above this enemy. Can be toggled in Inspector.")]
    public bool showHealthBar = true;
    public static bool ShowHealthBars = true;

    [Header("Abilities (auto-discovered from children)")]
    [Tooltip("Basic abilities = left-click when possessing this enemy")]
    public List<BasicAbilityEntry> basicAbilities = new List<BasicAbilityEntry>();
    [Tooltip("Skill abilities = Q when possessing this enemy")]
    public List<SkillAbilityEntry> skillAbilities = new List<SkillAbilityEntry>();
    [Tooltip("Mobility abilities = Space when possessing this enemy")]
    public List<MobilityAbilityEntry> mobilityAbilities = new List<MobilityAbilityEntry>();
    [Tooltip("Passive effects (always active, e.g. lifesteal)")]
    public List<EnemyAbility> passiveAbilities = new List<EnemyAbility>();

    public float currentTenacity;

    [Header("AI Targets (read-only)")]
    public Transform targetPlayer;
    public Enemy targetEnemy;

    [Header("Body Type")]
    public BodyType bodyType = BodyType.HugeMuscular;
    public string weaponDescription = "双手锁链";
    public float attackDowntime = 10f;

    public enum BodyType { Slim, Medium, Large, HugeMuscular, Boss }

    public Renderer meshRenderer;
    private Color originalColor;
    private Vector3 lastFramePosition;
    private Vector3 possessVelocity; // 附身玩家态加速度平滑
    public bool IsAbilityFacingLocked { get; set; }

    /// <summary>追击目标（Actor.Update 填充 ActorContext.PlayerTarget；AIController 使用）。</summary>
    protected override Transform PlayerTarget => targetPlayer;

    /// <summary>默认 Controller = AIController（同物体挂载；未挂则运行时自动添加）。</summary>
    protected override IController CreateDefaultController()
    {
        var ai = GetComponent<AIController>();
        if (ai == null) ai = gameObject.AddComponent<AIController>();
        return ai;
    }

    protected override void Awake()
    {
        base.Awake(); // Actor：挂载默认 Controller
        if (Combat != null) Combat.AddLooseTags(this, new[] { "Actor.Monster" });

        meshRenderer = GetComponent<Renderer>();
        bodyAnimator = GetComponent<Animator>();
        if (bodyAnimator != null) originalAnimatorUpdateMode = bodyAnimator.updateMode;
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        corpseFadeBlock = new MaterialPropertyBlock();
        currentHealth = maxHealth;
        currentTenacity = maxTenacity;
        originalColor = bodyColor;
        if (meshRenderer != null) meshRenderer.material.color = originalColor;
        bodyRenderers = GetComponentsInChildren<Renderer>(true);

        var found = GetComponentsInChildren<EnemyAbility>(true);
        passiveAbilities.Clear();
        // Keep existing basic/skill entries (preserves hpCost from Inspector), only add new ones
        for (int i = basicAbilities.Count - 1; i >= 0; i--)
            if (basicAbilities[i] == null || System.Array.IndexOf(found, basicAbilities[i].ability) < 0) basicAbilities.RemoveAt(i);
        for (int i = skillAbilities.Count - 1; i >= 0; i--)
            if (skillAbilities[i] == null || System.Array.IndexOf(found, skillAbilities[i].ability) < 0) skillAbilities.RemoveAt(i);
        for (int i = mobilityAbilities.Count - 1; i >= 0; i--)
            if (mobilityAbilities[i] == null || System.Array.IndexOf(found, mobilityAbilities[i].ability) < 0) mobilityAbilities.RemoveAt(i);

        foreach (var a in found)
        {
            if (a.type == EnemyAbility.AbilityType.BasicAttack && !BasicListContains(a))
                basicAbilities.Add(new BasicAbilityEntry { ability = a });
            else if (a.type == EnemyAbility.AbilityType.Skill && !SkillListContains(a))
                skillAbilities.Add(new SkillAbilityEntry { ability = a });
            else if (a.type == EnemyAbility.AbilityType.Mobility && !MobilityListContains(a))
                mobilityAbilities.Add(new MobilityAbilityEntry { ability = a });
            else if (a.type == EnemyAbility.AbilityType.Passive && !passiveAbilities.Contains(a))
                passiveAbilities.Add(a);
        }
    }

    public void RegisterAbility(EnemyAbility a)
    {
        if (a == null) return;
        if (a.type == EnemyAbility.AbilityType.BasicAttack && !BasicListContains(a))
            basicAbilities.Add(new BasicAbilityEntry { ability = a });
        else if (a.type == EnemyAbility.AbilityType.Skill && !SkillListContains(a))
            skillAbilities.Add(new SkillAbilityEntry { ability = a });
        else if (a.type == EnemyAbility.AbilityType.Mobility && !MobilityListContains(a))
            mobilityAbilities.Add(new MobilityAbilityEntry { ability = a });
        else if (a.type == EnemyAbility.AbilityType.Passive && !passiveAbilities.Contains(a)) passiveAbilities.Add(a);
    }

    void Start()
    {
        // After child OnEnable has stamped AbilityType, only inject shared dash when no custom Mobility exists.
        if (!HasCustomMobilityAbility() && GetComponent<EnemyAbility_MobilityDash>() == null)
            gameObject.AddComponent<EnemyAbility_MobilityDash>();

        gameObject.layer = 8;
        gameObject.tag = "Enemy";

        Physics.IgnoreLayerCollision(8, 8, true);
        Physics.IgnoreLayerCollision(8, 9, true);

        var p = GameObject.FindGameObjectWithTag("Player");
        targetPlayer = p != null ? p.transform : null;
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(ShowHealthBars);
        UpdateHealthUI();
    }

    bool BasicListContains(EnemyAbility a)
    {
        foreach (var e in basicAbilities) if (e != null && e.ability == a) return true;
        return false;
    }

    bool SkillListContains(EnemyAbility a)
    {
        foreach (var e in skillAbilities) if (e != null && e.ability == a) return true;
        return false;
    }

    bool MobilityListContains(EnemyAbility a)
    {
        foreach (var e in mobilityAbilities) if (e != null && e.ability == a) return true;
        return false;
    }

    /// <summary>
    /// AI 逻辑由 AIController.Tick 产出指令，此处走 Actor.Update 统一流程
    /// （Controller.Tick → ExecuteButtons → ExecuteMovement）。
    /// </summary>
    protected override void Update()
    {
        // Some legacy abilities and Inspector debugging can set health directly instead of calling TakeDamage.
        // Normalize that state here so a zero-health monster always enters the corpse possession lifecycle.
        if (!isPossessed && !isDowned && currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }

        if (Body == BodyState.Hit && Time.time >= hitStateEndsAt) Body = BodyState.Active;
        if (Body == BodyState.Fading || Body == BodyState.Despawned) return;
        base.Update();
    }

    /// <summary>
    /// 移动段：AI 态（AIController）匀速移动、朝移动方向；玩家附身态（PlayerController）
    /// 加速度平滑 + 静止朝鼠标。两者共用SphereCast预检测和Transform位移。
    /// </summary>
    protected override void ExecuteMovement(in ControlCommand cmd)
    {
        if (IsMovementBlocked)
        {
            possessVelocity = Vector3.zero;
            return;
        }

        bool playerControlled = IsPlayerControlled;
        float movementDeltaTime = playerControlled && Time.timeScale < 1f ? Time.unscaledDeltaTime : Time.deltaTime;

        if (playerControlled)
        {
            // 玩家附身态：加速度平滑
            Vector3 dir = cmd.HasMove ? cmd.MoveDirection : Vector3.zero;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                // 平滑旋转朝移动方向
                if (!IsAbilityFacingLocked)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, movementDeltaTime * 12f);
                }

                float effectiveMoveSpeed = Combat != null ? Combat.ModifyMoveSpeed(moveSpeed) : moveSpeed;
                Vector3 desired = dir * effectiveMoveSpeed;
                float accel = acceleration > 0f ? acceleration : 30f;
                possessVelocity = Vector3.MoveTowards(possessVelocity, desired, accel * movementDeltaTime);
                MoveWithSpherecast(possessVelocity * movementDeltaTime);
            }
            else
            {
                possessVelocity = Vector3.MoveTowards(possessVelocity, Vector3.zero, (deceleration > 0f ? deceleration : 25f) * movementDeltaTime);
                // 静止：面向鼠标
                if (cmd.HasAim && !IsAbilityFacingLocked)
                {
                    Vector3 aimDir = cmd.AimPoint - transform.position;
                    aimDir.y = 0f;
                    if (aimDir.sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.LookRotation(aimDir, Vector3.up);
                }
            }
            return;
        }

        // AI 态：匀速
        if (!cmd.HasMove || cmd.MoveDirection.sqrMagnitude < 0.0001f) return;
        Vector3 aiDir = cmd.MoveDirection;
        aiDir.y = 0f;
        float aiMoveSpeed = Combat != null ? Combat.ModifyMoveSpeed(moveSpeed) : moveSpeed;
        MoveWithSpherecast(aiDir * aiMoveSpeed * Time.deltaTime);
        transform.rotation = Quaternion.LookRotation(aiDir, Vector3.up);
    }

    /// <summary>SphereCast 预检测后的Transform移动（AI与玩家共用）。</summary>
    private void MoveWithSpherecast(Vector3 displacement)
    {
        float stepDist = displacement.magnitude;
        if (stepDist < 0.0001f) return;
        Vector3 dir = displacement / stepDist;
        Vector3 capsuleCenter = transform.position + Vector3.up * 0.75f;
        const float capsuleRadius = 0.4f;
        int obstacleMask = ~((1 << 8) | (1 << 9));
        if (Physics.SphereCast(capsuleCenter, capsuleRadius, dir, out RaycastHit hit, stepDist, obstacleMask, QueryTriggerInteraction.Ignore))
            stepDist = Mathf.Max(0f, hit.distance - 0.05f);

        Vector3 targetPos = transform.position + dir * stepDist;
        targetPos.y = transform.position.y;
        transform.position = targetPos;
    }

    void UpdateAnimatorSpeed()
    {
        var anim = GetComponent<Animator>();
        if (anim == null) return;
        float speed = Vector3.Distance(transform.position, lastFramePosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastFramePosition = transform.position;
        anim.SetFloat("Speed", speed);
    }

    bool TryTriggerAbilitiesOfType(EnemyAbility.AbilityType t)
    {
        bool any = false;
        if (t == EnemyAbility.AbilityType.BasicAttack)
        {
            foreach (var entry in basicAbilities)
            {
                if (entry != null && entry.ability != null && entry.ability.CanTrigger())
                {
                    entry.ability.Trigger();
                    if (isPossessed && !suppressPossessionDrain && entry.hpCost > 0f)
                    {
                        Debug.Log($"[HpCost] Basic {entry.ability.abilityName}: cost={entry.hpCost}, hp before={currentHealth}");
                        TakeDamage(entry.hpCost);
                    }
                    any = true;
                }
            }
        }
        else if (t == EnemyAbility.AbilityType.Skill)
        {
            foreach (var entry in skillAbilities)
            {
                if (entry != null && entry.ability != null && entry.ability.CanTrigger())
                {
                    entry.ability.Trigger();
                    if (isPossessed && !suppressPossessionDrain && entry.hpCost > 0f)
                    {
                        Debug.Log($"[HpCost] Skill {entry.ability.abilityName}: cost={entry.hpCost}, hp before={currentHealth}");
                        TakeDamage(entry.hpCost);
                    }
                    any = true;
                }
            }
        }
        else if (t == EnemyAbility.AbilityType.Mobility)
        {
            foreach (var entry in mobilityAbilities)
            {
                if (entry != null && entry.ability != null && entry.ability.CanTrigger())
                {
                    entry.ability.Trigger();
                    any = true;
                }
            }
        }
        return any;
    }

    public void PlayerTriggerBasicAttack()
    {
        TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.BasicAttack);
    }

    public void PlayerTriggerSkill()
    {
        TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.Skill);
    }

    public void PlayerTriggerMobility()
    {
        TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.Mobility);
    }

    /// <summary>
    /// Pay HP cost for a specific ability. Called by continuous abilities (Laser, ChargeShot)
    /// that bypass TryTriggerAbilitiesOfType. Only pays once per ability per frame if isPossessed.
    /// </summary>
    public void PayAbilityHpCost(EnemyAbility a)
    {
        if (!isPossessed || suppressPossessionDrain || a == null) return;
        float cost = 0f;
        foreach (var entry in basicAbilities)
        {
            if (entry != null && entry.ability == a) { cost = entry.hpCost; break; }
        }
        if (cost <= 0f)
        {
            foreach (var entry in skillAbilities)
            {
                if (entry != null && entry.ability == a) { cost = entry.hpCost; break; }
            }
        }
        if (cost > 0f)
        {
            Debug.Log($"[HpCost] Continuous {a.abilityName}: cost={cost}, hp before={currentHealth}");
            TakeDamage(cost);
        }
    }

    void LateUpdate()
    {
        // Animator speed always updates (even when downed/possessed)
        UpdateAnimatorSpeed();

        if (healthCanvas != null)
        {
            bool shouldShow = showHealthBar && ShowHealthBars && !isPossessed;
            if (healthCanvas.gameObject.activeSelf != shouldShow) healthCanvas.gameObject.SetActive(shouldShow);

            // Always face the camera (billboard)
            if (healthCanvas.gameObject.activeSelf && Camera.main != null)
                healthCanvas.transform.LookAt(healthCanvas.transform.position + Camera.main.transform.forward, Camera.main.transform.up);
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDowned || Body == BodyState.Fading || Body == BodyState.Despawned) return;
        if (IsUntargetable(this) || IsDamageImmune(this)) return;
        if (Combat != null) amount = Combat.ModifyIncomingDamage(amount);
        if (amount <= 0f) return;

        EnterHitState();
        if (isPossessed)
        {
            currentHealth -= amount;
            FlashDamage();
            UpdateHealthUI();
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                PossessionManager pm = PossessionManager.Instance;
                if (pm != null && pm.CurrentBody == this) pm.NotifyBodyDied();
            }
            return;
        }

        currentHealth -= amount;
        currentTenacity -= amount;
        FlashDamage();
        UpdateHealthUI();
        if (currentTenacity <= 0)
        {
            currentTenacity = 0;
            BecomeWeakened();
        }
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    public void OnDealtDamage(float amount)
    {
        // Enemy-specific lifesteal passive (e.g. from prefab)
        foreach (var a in passiveAbilities) { if (a is EnemyAbility_Lifesteal ls) ls.OnOwnerDealtDamage(amount); }

        // Global player passive lifesteal (accumulated from possessed enemies)
        if (isPossessed && PlayerPassiveManager.Instance != null)
        {
            float lifesteal = PlayerPassiveManager.Instance.GetLifestealMultiplier();
            if (lifesteal > 0f)
            {
                float heal = amount * lifesteal;
                Heal(heal);
            }
        }
    }

    /// <summary>
    /// Returns whether this monster may damage the specified monster under the current faction rule.
    /// AI monsters share one faction; a possessed monster belongs to the player faction.
    /// </summary>
    public bool CanDamage(MonsterActor target)
    {
        return target != null && target != this && !target.isDowned &&
               target.Body != BodyState.Fading && target.Body != BodyState.Despawned &&
               isPossessed != target.isPossessed &&
               !IsUntargetable(target);
    }

    /// <summary>True while the actor owns State.Defense.Untargetable (e.g. Pride blink chain).</summary>
    public static bool IsUntargetable(MonsterActor target)
    {
        return target != null && target.Combat != null && target.Combat.Tags.HasTag("State.Defense.Untargetable");
    }

    /// <summary>True while the actor owns State.Defense.DamageImmune (e.g. cheat immortal body).</summary>
    public static bool IsDamageImmune(MonsterActor target)
    {
        return target != null && target.Combat != null && target.Combat.Tags.HasTag("State.Defense.DamageImmune");
    }

    private bool HasCustomMobilityAbility()
    {
        EnemyAbility[] abilities = GetComponentsInChildren<EnemyAbility>(true);
        for (int i = 0; i < abilities.Length; i++)
        {
            EnemyAbility ability = abilities[i];
            if (ability == null || ability is EnemyAbility_MobilityDash) continue;
            if (ability.type == EnemyAbility.AbilityType.Mobility) return true;
        }
        return false;
    }

    /// <summary>Only AI-controlled monsters may damage the soul player.</summary>
    public bool CanDamageSoul()
    {
        return !isPossessed && Body != BodyState.Fading && Body != BodyState.Despawned;
    }

    public void ApplyOffensiveDamage(MonsterActor target, float amount)
    {
        if (!CanDamage(target) || amount <= 0f) return;
        if (Combat != null) amount = Combat.ModifyOutgoingDamage(amount);
        target.TakeDamage(amount);
        OnDealtDamage(amount);

        float totalBurnPercent = 0f;
        GameObject burnVfx = null;

        if (isPossessed && PlayerPassiveManager.Instance != null)
        {
            totalBurnPercent = PlayerPassiveManager.Instance.GetBurnPercent();
            foreach (var a in passiveAbilities)
            {
                if (a is EnemyPassiveBuff b && b.burnVfxPrefab != null) { burnVfx = b.burnVfxPrefab; break; }
            }
            // Fallback: if possessed enemy doesn't have burn VFX, use player's
            if (burnVfx == null && PlayerPassiveManager.Instance.GetBurnVfxPrefab() != null)
                burnVfx = PlayerPassiveManager.Instance.GetBurnVfxPrefab();

            Debug.Log($"[Burn] Possessed attack: burnPct={totalBurnPercent}, vfx={burnVfx != null}, target={target.name}");
            // BurnEffect.Init 签名为 Enemy（敌人 prefab 经 Enemy 壳类实例化），显式转换安全
            Enemy enemyTarget = target as Enemy;
            if (enemyTarget != null && totalBurnPercent > 0f && enemyTarget.GetComponent<BurnEffect>() == null)
            {
                var burn = enemyTarget.gameObject.AddComponent<BurnEffect>();
                burn.Init(enemyTarget, totalBurnPercent, 3f, 0.5f, burnVfx);
            }
        }
        else
        {
            foreach (var a in passiveAbilities)
            {
                if (a is EnemyPassiveBuff buff)
                {
                    Enemy enemyTarget2 = target as Enemy;
                    if (enemyTarget2 != null)
                        buff.OnOwnerDealtDamage(enemyTarget2, amount, buff.burnBonusPercent / 100f);
                }
            }
        }
    }

    public void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealthUI();
    }

    void BecomeWeakened()
    {
        isWeakened = true;
        if (meshRenderer != null) meshRenderer.material.color = weakenedColor;
    }

    public void OnPossessed()
    {
        if (corpseRoutine != null)
        {
            StopCoroutine(corpseRoutine);
            corpseRoutine = null;
        }

        isPossessed = true;
        isDowned = false;
        isWeakened = false;
        Body = BodyState.Active;
        gameObject.tag = "Player";
        if (bodyAnimator != null) bodyAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        isPossessionReserved = false;
        SetRendererFade(1f);
        if (meshRenderer != null) meshRenderer.material.color = possessedColor;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = true;
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = true;
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = false;
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(false);
        currentHealth = maxHealth;
        currentTenacity = maxTenacity;
        UpdateHealthUI();

        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.SetBool("IsDowned", false);
    }

    public void OnUnpossessed()
    {
        isPossessed = false;
        gameObject.tag = "Enemy";
        if (bodyAnimator != null) bodyAnimator.updateMode = originalAnimatorUpdateMode;
    }

    private void EnterHitState()
    {
        if (isDowned || Body == BodyState.Fading || Body == BodyState.Despawned) return;
        Body = BodyState.Hit;
        hitStateEndsAt = Time.time + hitStateDuration;
    }

    private void Die()
    {
        isDowned = true;
        isPossessed = false;
        Body = BodyState.Downed;
        possessionWindowEndsAt = Time.time + corpsePossessionWindow;
        isPossessionReserved = false;
        transform.rotation = Quaternion.Euler(90f, transform.rotation.eulerAngles.y, 0f);
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = true;
        EnableCorpsePossessionCollider();
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = true;
        if (meshRenderer != null) meshRenderer.material.color = downedColor;
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(true);
        UpdateHealthUI();

        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.SetBool("IsDowned", true);

        if (corpseRoutine != null) StopCoroutine(corpseRoutine);
        corpseRoutine = StartCoroutine(CorpseLifecycleRoutine());
        Debug.Log($"[MonsterState] '{displayName}' downed. Possess window={corpsePossessionWindow:F1}s.");
    }

    public void BeginDisappearing()
    {
        if (Body == BodyState.Fading || Body == BodyState.Despawned) return;
        if (corpseRoutine != null) StopCoroutine(corpseRoutine);
        isPossessionReserved = false;
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = false;
        CancelAbilityRuntimeState();
        SetAbilityComponentsEnabled(false);

        isPossessed = false;
        isDowned = true;
        Body = BodyState.Fading;
        possessionWindowEndsAt = 0f;
        SetController(NullController.Instance);
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        corpseRoutine = StartCoroutine(FadeAndReturnRoutine());
        Debug.Log($"[MonsterState] '{displayName}' entered fading state.");
    }

    private System.Collections.IEnumerator CorpseLifecycleRoutine()
    {
        while (Body == BodyState.Downed && (isPossessionReserved || Time.time < possessionWindowEndsAt)) yield return null;
        if (Body == BodyState.Downed) BeginDisappearing();
    }

    private System.Collections.IEnumerator FadeAndReturnRoutine()
    {
        float elapsed = 0f;
        while (elapsed < corpseFadeDuration)
        {
            elapsed += Time.deltaTime;
            SetRendererFade(1f - Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, corpseFadeDuration)));
            yield return null;
        }

        SetRendererFade(0f);
        Body = BodyState.Despawned;
        corpseRoutine = null;
        MonsterPool.Instance.Return(this);
    }

    public void ResetForSpawn()
    {
        if (corpseRoutine != null)
        {
            StopCoroutine(corpseRoutine);
            corpseRoutine = null;
        }

        isWeakened = false;
        isDowned = false;
        isPossessed = false;
        suppressPossessionDrain = false;
        isPossessionReserved = false;
        playerDetected = false;
        Body = BodyState.Active;
        possessionWindowEndsAt = 0f;
        hitStateEndsAt = 0f;
        currentHealth = maxHealth;
        currentTenacity = maxTenacity;
        possessVelocity = Vector3.zero;
        SetAbilityComponentsEnabled(true);
        CancelAbilityRuntimeState();
        gameObject.tag = "Enemy";
        SetRendererFade(1f);
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        if (meshRenderer != null) meshRenderer.material.color = bodyColor;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = true;
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = true;
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = false;
        Animator animator = GetComponent<Animator>();
        if (animator != null) animator.SetBool("IsDowned", false);
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(showHealthBar && ShowHealthBars);
        targetPlayer = GameObject.FindGameObjectWithTag("Player")?.transform;
        SetController(GetComponent<AIController>());
        UpdateHealthUI();
    }

    public void ResetForPool()
    {
        SetController(NullController.Instance);
        CancelAbilityRuntimeState();
        possessVelocity = Vector3.zero;
        if (corpseRoutine != null)
        {
            StopCoroutine(corpseRoutine);
            corpseRoutine = null;
        }
    }

    private void CancelAbilityRuntimeState()
    {
        foreach (EnemyAbility ability in GetComponentsInChildren<EnemyAbility>(true))
            if (ability != null) ability.ResetForOwnerReuse();
    }

    private void SetAbilityComponentsEnabled(bool enabled)
    {
        foreach (EnemyAbility ability in GetComponentsInChildren<EnemyAbility>(true))
            if (ability != null) ability.enabled = enabled;
    }

    public bool TryReserveForPossession()
    {
        if (!CanBePossessed) return false;
        isPossessionReserved = true;
        Debug.Log($"[MonsterState] '{displayName}' reserved for possession.");
        return true;
    }

    public void CancelPossessionReservation()
    {
        if (!isPossessionReserved) return;
        isPossessionReserved = false;
        Debug.Log($"[MonsterState] '{displayName}' possession reservation cancelled.");
    }

    public string GetPossessionDebugState()
    {
        return $"body={Body}, downed={isDowned}, possessed={isPossessed}, reserved={isPossessionReserved}, remaining={PossessionWindowRemaining:F2}s, active={gameObject.activeInHierarchy}";
    }

    private void EnableCorpsePossessionCollider()
    {
        if (corpsePossessionCollider == null)
        {
            Transform colliderTransform = transform.Find(CorpseColliderObjectName);
            if (colliderTransform == null)
            {
                GameObject colliderObject = new GameObject(CorpseColliderObjectName);
                colliderTransform = colliderObject.transform;
                colliderTransform.SetParent(transform, false);
            }

            SphereCollider legacySphere = colliderTransform.GetComponent<SphereCollider>();
            if (legacySphere != null) legacySphere.enabled = false;
            corpsePossessionCollider = colliderTransform.GetComponent<BoxCollider>();
            if (corpsePossessionCollider == null) corpsePossessionCollider = colliderTransform.gameObject.AddComponent<BoxCollider>();
        }

        bodyRenderers = GetComponentsInChildren<Renderer>(true);
        Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer renderer in bodyRenderers)
        {
            if (renderer == null || !renderer.enabled || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;

            Bounds rendererBounds = renderer.bounds;
            Vector3 extents = rendererBounds.extents;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 worldCorner = rendererBounds.center + Vector3.Scale(extents, new Vector3(x, y, z));
                Vector3 localCorner = transform.InverseTransformPoint(worldCorner);
                if (!hasBounds)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else localBounds.Encapsulate(localCorner);
            }
        }

        if (!hasBounds) localBounds = new Bounds(Vector3.zero, Vector3.one * MinimumCorpseColliderSize);
        corpsePossessionCollider.center = localBounds.center;
        corpsePossessionCollider.size = Vector3.Max(
            localBounds.size + Vector3.one * CorpseColliderPadding * 2f,
            Vector3.one * MinimumCorpseColliderSize);
        corpsePossessionCollider.isTrigger = true;
        corpsePossessionCollider.enabled = true;
        Debug.Log($"[CorpseCollider] '{displayName}' center={corpsePossessionCollider.bounds.center:F2}, size={corpsePossessionCollider.bounds.size:F2}, enabled={corpsePossessionCollider.enabled}");
    }

    private void SetRendererFade(float fade)
    {
        if (bodyRenderers == null || bodyRenderers.Length == 0)
            bodyRenderers = GetComponentsInChildren<Renderer>(true);
        if (corpseFadeBlock == null) corpseFadeBlock = new MaterialPropertyBlock();

        foreach (Renderer renderer in bodyRenderers)
        {
            if (renderer == null) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null || !material.HasProperty("_CorpseFade")) continue;
                renderer.GetPropertyBlock(corpseFadeBlock, materialIndex);
                corpseFadeBlock.SetFloat("_CorpseFade", fade);
                renderer.SetPropertyBlock(corpseFadeBlock, materialIndex);
            }
        }
    }

    void FlashDamage()
    {
        if (meshRenderer != null) StartCoroutine(FlashRoutine());
    }

    System.Collections.IEnumerator FlashRoutine()
    {
        Color orig = meshRenderer.material.color;
        meshRenderer.material.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        if (meshRenderer == null) yield break;
        if (isDowned) meshRenderer.material.color = downedColor;
        else if (isWeakened) meshRenderer.material.color = weakenedColor;
        else if (isPossessed) meshRenderer.material.color = possessedColor;
        else meshRenderer.material.color = bodyColor;
    }

    public void UpdateHealthUI()
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }
        else
        {
            // Auto-find if not assigned
            healthSlider = GetComponentInChildren<Slider>();
            if (healthSlider != null)
            {
                healthSlider.maxValue = maxHealth;
                healthSlider.value = currentHealth;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }

    // ── IActor 实现（Actor 抽象成员） ──

    public override bool IsDowned => isDowned;
    public override string DisplayName => displayName;

    /// <summary>
    /// AI keeps Basic/Skill1 semantics. A possessed monster uses left-click Basic, Q Skill,
    /// Space mobility, right-click corpse switching, E bullet time, and F release.
    /// </summary>
    protected override void ExecuteButtons(in ControlCommand cmd)
    {
        if ((cmd.Pressed & CommandButtons.Basic) != 0) PlayerTriggerBasicAttack();

        if (!IsPlayerControlled)
        {
            if ((cmd.Pressed & CommandButtons.Skill1) != 0 && TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.Skill))
            {
                AIController ai = Controller as AIController;
                if (ai != null) ai.NotifySkillTriggered();
            }
            return;
        }

        PossessionManager manager = PossessionManager.Instance;
        if ((cmd.Pressed & CommandButtons.Skill1) != 0)
        {
            if (manager == null)
                Debug.LogWarning("[PossessionInput] Ignored body-switch right-click: PossessionManager is missing.");
            else if (PlayerController.Instance == null)
                Debug.LogWarning("[PossessionInput] Ignored body-switch right-click: PlayerController is missing.");
            else
            {
                manager.TryRequestPossessFromInput(PlayerController.Instance.GetMouseRay(), "MonsterActor");
            }
        }
        if ((cmd.Pressed & CommandButtons.Skill2) != 0) PlayerTriggerSkill();
        if ((cmd.Pressed & CommandButtons.Mobility) != 0) PlayerTriggerMobility();
        if ((cmd.Pressed & CommandButtons.Skill3) != 0 && manager != null) manager.TriggerBulletTime();
        if ((cmd.Pressed & CommandButtons.Release) != 0 && manager != null && manager.CurrentBody == this)
            manager.RequestRelease(force: false);
    }

    public override void FillAbilitySlots(List<AbilitySlotInfo> buffer)
    {
        if (buffer == null) return;
        buffer.Clear();
        foreach (var e in basicAbilities)
        {
            if (e == null || e.ability == null) continue;
            buffer.Add(new AbilitySlotInfo
            {
                Name = e.ability.abilityName,
                CooldownRemaining = Mathf.Max(0f, e.ability.CurrentCooldown),
                CooldownTotal = e.ability.cooldown,
                HpCost = e.hpCost,
            });
        }
        foreach (var e in skillAbilities)
        {
            if (e == null || e.ability == null) continue;
            buffer.Add(new AbilitySlotInfo
            {
                Name = e.ability.abilityName,
                CooldownRemaining = Mathf.Max(0f, e.ability.CurrentCooldown),
                CooldownTotal = e.ability.cooldown,
                HpCost = e.hpCost,
            });
        }
        foreach (var e in mobilityAbilities)
        {
            if (e == null || e.ability == null) continue;
            buffer.Add(new AbilitySlotInfo
            {
                Name = e.ability.abilityName,
                CooldownRemaining = Mathf.Max(0f, e.ability.CurrentCooldown),
                CooldownTotal = e.ability.cooldown,
                HpCost = 0f,
            });
        }
    }
}
