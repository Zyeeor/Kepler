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
    // ── 正交状态（只读派生视图，源数据为 bool 字段） ──
    public enum BodyState { Active, Weakened, Downed }
    public enum ControlState { AI, Possessed }

    /// <summary>当前身体状态（派生自 isDowned/isWeakened）。</summary>
    public BodyState Body
    {
        get
        {
            if (isDowned) return BodyState.Downed;
            if (isWeakened) return BodyState.Weakened;
            return BodyState.Active;
        }
    }

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
    public bool isPossessed = false;
    public bool playerDetected = false;

    [Header("UI")]
    public Slider healthSlider;
    public Canvas healthCanvas;
    [Tooltip("Show health bar above this enemy. Can be toggled in Inspector.")]
    public bool showHealthBar = true;
    public static bool ShowHealthBars = true;

    [Header("Abilities (auto-discovered from children)")]
    [Tooltip("Basic abilities = left-click when possessing this enemy")]
    public List<BasicAbilityEntry> basicAbilities = new List<BasicAbilityEntry>();
    [Tooltip("Skill abilities = right-click when possessing this enemy")]
    public List<SkillAbilityEntry> skillAbilities = new List<SkillAbilityEntry>();
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
    public Rigidbody rb;
    private Color originalColor;
    private bool savedKinematic;
    private Vector3 lastFramePosition;
    private Vector3 possessVelocity; // 附身玩家态加速度平滑

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
        rb = GetComponent<Rigidbody>();
        currentHealth = maxHealth;
        currentTenacity = maxTenacity;
        originalColor = bodyColor;
        if (meshRenderer != null) meshRenderer.material.color = originalColor;

        var found = GetComponentsInChildren<EnemyAbility>(true);
        passiveAbilities.Clear();
        // Keep existing basic/skill entries (preserves hpCost from Inspector), only add new ones
        for (int i = basicAbilities.Count - 1; i >= 0; i--)
            if (basicAbilities[i] == null || System.Array.IndexOf(found, basicAbilities[i].ability) < 0) basicAbilities.RemoveAt(i);
        for (int i = skillAbilities.Count - 1; i >= 0; i--)
            if (skillAbilities[i] == null || System.Array.IndexOf(found, skillAbilities[i].ability) < 0) skillAbilities.RemoveAt(i);

        foreach (var a in found)
        {
            if (a.type == EnemyAbility.AbilityType.BasicAttack && !BasicListContains(a))
                basicAbilities.Add(new BasicAbilityEntry { ability = a });
            else if (a.type == EnemyAbility.AbilityType.Skill && !SkillListContains(a))
                skillAbilities.Add(new SkillAbilityEntry { ability = a });
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
        else if (a.type == EnemyAbility.AbilityType.Passive && !passiveAbilities.Contains(a)) passiveAbilities.Add(a);
    }

    void Start()
    {
        gameObject.layer = 8;
        gameObject.tag = "Enemy";

        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

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

    /// <summary>
    /// AI 逻辑由 AIController.Tick 产出指令，此处走 Actor.Update 统一流程
    /// （Controller.Tick → ExecuteButtons → ExecuteMovement）。
    /// </summary>
    protected override void Update()
    {
        base.Update();
    }

    /// <summary>
    /// 移动段：AI 态（AIController）匀速移动、朝移动方向；玩家附身态（PlayerController）
    /// 加速度平滑 + 静止朝鼠标。两者共用 spherecast 预检测 + rb.MovePosition。
    /// </summary>
    protected override void ExecuteMovement(in ControlCommand cmd)
    {
        if (IsMovementBlocked)
        {
            possessVelocity = Vector3.zero;
            return;
        }

        bool playerControlled = IsPlayerControlled;

        if (playerControlled)
        {
            // 玩家附身态：加速度平滑
            Vector3 dir = cmd.HasMove ? cmd.MoveDirection : Vector3.zero;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                // 平滑旋转朝移动方向
                Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 12f);

                float effectiveMoveSpeed = Combat != null ? Combat.ModifyMoveSpeed(moveSpeed) : moveSpeed;
                Vector3 desired = dir * effectiveMoveSpeed;
                float accel = acceleration > 0f ? acceleration : 30f;
                possessVelocity = Vector3.MoveTowards(possessVelocity, desired, accel * Time.deltaTime);
                MoveWithSpherecast(possessVelocity * Time.deltaTime);
            }
            else
            {
                possessVelocity = Vector3.MoveTowards(possessVelocity, Vector3.zero, (deceleration > 0f ? deceleration : 25f) * Time.deltaTime);
                // 静止：面向鼠标
                if (cmd.HasAim)
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

    /// <summary>spherecast 预检测 + rb.MovePosition/transform 移动（AI 与玩家共用）。</summary>
    private void MoveWithSpherecast(Vector3 displacement)
    {
        float stepDist = displacement.magnitude;
        if (stepDist < 0.0001f) return;
        Vector3 dir = displacement / stepDist;

        if (rb != null)
        {
            // 手动 spherecast 预检测:Kinematic Rigidbody + MovePosition 在 Unity 中
            // 不做 CCD sweep,直接 MovePosition 会穿过薄静态碰撞器。
            // 这里模拟 sweep,撞到墙/柱/掩体时把步长缩短到 hit.distance - radius - skin
            Vector3 capsuleCenter = rb.position + Vector3.up * 0.75f; // capsule center y=0.75
            float capsuleRadius = 0.4f;
            // 不和 Layer 8(Enemy)、Layer 9(Player) 自身检测,只检测环境(Layer 0=Default)
            int obstacleMask = ~((1 << 8) | (1 << 9));
            if (Physics.SphereCast(capsuleCenter, capsuleRadius, dir, out RaycastHit hit, stepDist, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                stepDist = Mathf.Max(0f, hit.distance - 0.05f);
            }
            Vector3 targetPos = rb.position + dir * stepDist;
            targetPos.y = rb.position.y;
            rb.MovePosition(targetPos);
        }
        else
        {
            Vector3 capsuleCenter = transform.position + Vector3.up * 0.75f;
            float capsuleRadius = 0.4f;
            int obstacleMask = ~((1 << 8) | (1 << 9));
            if (Physics.SphereCast(capsuleCenter, capsuleRadius, dir, out RaycastHit hit, stepDist, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                stepDist = Mathf.Max(0f, hit.distance - 0.05f);
            }
            Vector3 newPos = transform.position + dir * stepDist;
            newPos.y = transform.position.y;
            transform.position = newPos;
        }
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
                    if (isPossessed && entry.hpCost > 0f)
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
                    if (isPossessed && entry.hpCost > 0f)
                    {
                        Debug.Log($"[HpCost] Skill {entry.ability.abilityName}: cost={entry.hpCost}, hp before={currentHealth}");
                        TakeDamage(entry.hpCost);
                    }
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

    /// <summary>
    /// Pay HP cost for a specific ability. Called by continuous abilities (Laser, ChargeShot)
    /// that bypass TryTriggerAbilitiesOfType. Only pays once per ability per frame if isPossessed.
    /// </summary>
    public void PayAbilityHpCost(EnemyAbility a)
    {
        if (!isPossessed || a == null) return;
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
        if (isDowned) return;
        if (Combat != null) amount = Combat.ModifyIncomingDamage(amount);
        if (isPossessed)
        {
            currentHealth -= amount;
            FlashDamage();
            UpdateHealthUI();
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                // 附身身体死亡 → PossessionManager 编排强制释放
                var pm = PossessionManager.Instance;
                if (pm != null && pm.CurrentBody == this) pm.NotifyBodyDied();
            }
            return;
        }
        currentHealth -= amount;
        currentTenacity -= amount;
        FlashDamage();
        UpdateHealthUI();
        if (currentTenacity <= 0) { currentTenacity = 0; BecomeWeakened(); }
        if (currentHealth <= 0) { currentHealth = 0; Die(); }
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

    public void ApplyOffensiveDamage(MonsterActor target, float amount)
    {
        if (target == null || amount <= 0f) return;
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
        isPossessed = true;
        isDowned = false;
        isWeakened = false;
        gameObject.tag = "Player";
        if (meshRenderer != null) { meshRenderer.enabled = true; meshRenderer.material.color = possessedColor; }
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = true;
        if (rb != null) { savedKinematic = rb.isKinematic; rb.velocity = Vector3.zero; }
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(false);
        currentHealth = maxHealth;
        currentTenacity = maxTenacity;
        UpdateHealthUI();

        // Reset animator from downed state
        var anim = GetComponent<Animator>();
        if (anim != null) anim.SetBool("IsDowned", false);
    }

    public void OnUnpossessed()
    {
        isPossessed = false;
        gameObject.tag = "Enemy";
    }

    void Die()
    {
        isDowned = true;
        if (meshRenderer != null) meshRenderer.material.color = downedColor;
        if (rb != null) { rb.velocity = Vector3.zero; }
        transform.rotation = Quaternion.Euler(90, transform.rotation.eulerAngles.y, 0);
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(true);
        UpdateHealthUI();

        // Trigger downed animation immediately
        var anim = GetComponent<Animator>();
        if (anim != null) anim.SetBool("IsDowned", true);
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

    void OnCollisionEnter(Collision collision)
    {
        if (isDowned || isPossessed) return;
        if (collision.gameObject.CompareTag("Player"))
        {
            var ph = collision.gameObject.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(collisionDamage);
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
    /// 按钮指令执行：Basic→普攻、Skill1→技能；附身玩家态：Interact(Space)/Release(F)→脱离。
    /// 由 Actor.Update 流程调用（Controller.Tick 产出按钮位）。
    /// 技能触发成功后回调 AIController.NotifySkillTriggered（成功才重置 skillTimer）。
    /// </summary>
    protected override void ExecuteButtons(in ControlCommand cmd)
    {
        if ((cmd.Pressed & CommandButtons.Basic) != 0)
            PlayerTriggerBasicAttack();
        if ((cmd.Pressed & CommandButtons.Skill1) != 0)
        {
            if (TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.Skill))
            {
                var ai = Controller as AIController;
                if (ai != null) ai.NotifySkillTriggered();
            }
        }
        // 附身玩家态下 Space/F 脱离（PossessionManager 统一编排；AI 态无该指令）
        if ((cmd.Pressed & (CommandButtons.Interact | CommandButtons.Release)) != 0)
        {
            var pm = PossessionManager.Instance;
            if (pm != null && pm.CurrentBody == this) pm.RequestRelease(force: false);
        }
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
    }
}
