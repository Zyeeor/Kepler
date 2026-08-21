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
        [Tooltip("HP cost paid by the possessed enemy when the player triggers this ability. 0 = free.")]
        public float hpCost;
    }


    [Header("Identity")]
    public string displayName = "Enemy";

    /// <summary>
    /// 本怪 AI/技能随机子流（种子确定性）：由 MonsterSpawner 刷出时按刷怪序号分配
    /// （SeedSystem.CreateFlow(DomainAI, spawnSequence)），同世界种子下同怪行为可复现。
    /// null 兜底（非波次刷出路径，如场景预摆怪）时回退 UnityEngine.Random 语义。
    /// </summary>
    public System.Random AiRng { get; private set; }

    /// <summary>
    /// 分配 AI 子流（刷出时由刷怪器调用；salt 用全局递增刷怪序号保证可复现）。
    /// 分配后重算 AI 决策相位：Spawn 链路里 OnAttached 的首次相位计算发生在 InitAiRng 之前
    /// （当时 AiRng 为 null 回退全局随机），此处用就绪的流重新随机化，保证首个决策相位可复现。
    /// </summary>
    public void InitAiRng(int salt)
    {
        AiRng = SeedSystem.CreateFlow(SeedSystem.DomainAI, salt);
        var ai = GetComponent<AIController>();
        if (ai != null) ai.ResetDecisionPhase();
    }

    /// <summary>AI 随机（Range 语义）：流为 null 时回退 UnityEngine.Random。</summary>
    public float AiRandomRange(float min, float max)
    {
        return AiRng != null ? AiRng.NextFloat(min, max) : UnityEngine.Random.Range(min, max);
    }

    /// <summary>AI 随机（value 语义）：流为 null 时回退 UnityEngine.Random。</summary>
    public float AiRandomValue()
    {
        return AiRng != null ? AiRng.NextFloat() : UnityEngine.Random.value;
    }

    /// <summary>AI 单位球面方向（流为 null 回退 UnityEngine.Random）。</summary>
    public Vector3 AiRandomUnitSphere()
    {
        return AiRng != null ? AiRng.NextUnitSphere() : UnityEngine.Random.onUnitSphere;
    }

    /// <summary>AI 单位圆盘点（流为 null 回退 UnityEngine.Random）。</summary>
    public Vector2 AiRandomInsideUnitCircle()
    {
        return AiRng != null ? AiRng.NextInsideUnitCircle() : UnityEngine.Random.insideUnitCircle;
    }

    /// <summary>AI 整数随机（流为 null 回退 UnityEngine.Random）。</summary>
    public int AiRandomInt(int min, int maxExclusive)
    {
        return AiRng != null ? AiRng.Next(min, maxExclusive) : UnityEngine.Random.Range(min, maxExclusive);
    }

    [Header("Dual Stats (Enemy vs Possessed)")]
    [Tooltip("Stats while AI-controlled (enemy). Runtime Actor fields are filled from the active block.")]
    public MonsterStatBlock enemyStats;
    [Tooltip("Stats while player-possessed. Applied in OnPossessed().")]
    public MonsterStatBlock possessedStats;

    [Header("Runtime Stats (driven by active Dual Stat block)")]
    public float maxTenacity = 200f;
    // 注：moveSpeed / maxHealth / currentHealth 由 Actor 基类提供（同名同类型，prefab 序列化值按字段名映射到基类字段，无损）
    [Tooltip("Base collision damage when touching the player. Individual ability damage is configured on each EnemyAbility.")]
    public float collisionDamage = 30f;
    [Tooltip("Attack speed multiplier. 1.0 = normal speed. Higher = faster attack cooldown. Also used by possessed player combat.")]
    public float attackSpeed = 1.0f;

    [Header("AI Config (unified library)")]
    [Tooltip("AI 配置库资产（单文件，同 CardLibrary 模式，位于 Assets/Configs/）。")]
    public MonsterAIConfig aiConfig;
    [Tooltip("在配置库中按 id 查找 AI 配置条目。为空或未命中时使用默认值。")]
    public string aiConfigId;

    /// <summary>当前生效的 AI 配置条目（库未命中/未配置时回退共享默认条目）。</summary>
    public MonsterAIConfigEntry AiConfig
    {
        get
        {
            if (aiConfig != null && !string.IsNullOrEmpty(aiConfigId))
            {
                var entry = aiConfig.Get(aiConfigId);
                if (entry != null) return entry;
            }
            return MonsterAIConfig.DefaultEntry;
        }
    }

    // 以下 AI 参数已统一收口到 MonsterAIConfig（Assets/Configs/AI/），只读属性转发便于代码访问。
    /// <summary>索敌半径（AI 配置）。</summary>
    public float detectionRadius => AiConfig.detectionRadius;
    /// <summary>普攻范围（AI 配置，与技能范围相互独立）。</summary>
    public float basicAttackRange => AiConfig.basicAttackRange;
    /// <summary>技能范围（AI 配置，与普攻范围相互独立）。</summary>
    public float skillAttackRange => AiConfig.skillAttackRange;
    /// <summary>AI 停步距离（AI 配置）。</summary>
    public float aiMinRange => AiConfig.aiMinRange;
    /// <summary>攻击迟疑度（AI 配置，0~1）。</summary>
    public float attackEagerness => AiConfig.attackEagerness;
    /// <summary>决策节拍最小间隔（AI 配置）。</summary>
    public float decisionIntervalMin => AiConfig.decisionIntervalMin;
    /// <summary>决策节拍最大间隔（AI 配置）。</summary>
    public float decisionIntervalMax => AiConfig.decisionIntervalMax;
    /// <summary>攻击范围内技能优先概率（AI 配置）。</summary>
    public float skillPriority => AiConfig.skillPriority;
    /// <summary>追击时触发位移技能的概率（AI 配置）。</summary>
    public float aiMobilityChance => AiConfig.aiMobilityChance;
    /// <summary>追击随机走位概率（AI 配置）。</summary>
    public float strafeChance => AiConfig.strafeChance;
    /// <summary>走位刷新间隔下限（AI 配置）。</summary>
    public float strafeIntervalMin => AiConfig.strafeIntervalMin;
    /// <summary>走位刷新间隔上限（AI 配置）。</summary>
    public float strafeIntervalMax => AiConfig.strafeIntervalMax;
    /// <summary>侧移分量强度（AI 配置）。</summary>
    public float strafeStrength => AiConfig.strafeStrength;
    /// <summary>追击速度抖动下限（AI 配置）。</summary>
    public float moveSpeedJitterMin => AiConfig.moveSpeedJitterMin;
    /// <summary>追击速度抖动上限（AI 配置）。</summary>
    public float moveSpeedJitterMax => AiConfig.moveSpeedJitterMax;
    /// <summary>AI 移动加速度（AI 配置）。</summary>
    public float aiMoveAcceleration => AiConfig.moveAcceleration;
    /// <summary>AI 移动减速度（AI 配置）。</summary>
    public float aiMoveDeceleration => AiConfig.moveDeceleration;
    /// <summary>AI 最大转向速度（AI 配置）。</summary>
    public float aiTurnSpeed => AiConfig.turnSpeed;
    /// <summary>AI 转向加速度（AI 配置）。</summary>
    public float aiTurnAcceleration => AiConfig.turnAcceleration;
    /// <summary>调试圆环开关（AI 配置，是否可视化索敌/普攻/技能范围）。</summary>
    public bool showDebugRanges => forceDebugRanges || AiConfig.showDebugRanges;

    [Header("Debug Ranges (visible in Game view)")]
    [Tooltip("运行时强制开启调试圆环（调试脚本刷怪时置 true，不污染配置资产）。")]
    public bool forceDebugRanges = false;
    [Tooltip("索敌范围圆环颜色。")]
    public Color detectRangeColor = new Color(1f, 0.75f, 0.1f, 0.9f);
    [Tooltip("普攻范围圆环颜色。")]
    public Color basicRangeColor = new Color(1f, 0.15f, 0.15f, 0.9f);
    [Tooltip("技能范围圆环颜色。")]
    public Color skillRangeColor = new Color(0.15f, 0.4f, 1f, 0.9f);
    [Range(16, 128)] public int rangeCircleSegments = 64;

    [Header("Ability HP Costs (consumed when possessed player uses)")]
    // HP cost is set on each Basic / Skill / Mobility ability entry below.

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
    [Tooltip("流送 AI 激活开关：false 时 AI 完全休眠（不产出指令，仅 0.5s 低频维持索敌目标缓存）。由 MonsterSpawner 按 Chunk 状态机驱动；附身中的怪永不休眠。默认 true，非流送场景（调试刷怪等）行为不变。")]
    public bool aiActiveOverride = true;

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
    private ActorVisualFx visualFx;
    private const string CorpseColliderObjectName = "__PossessionCorpseCollider";
    private const float CorpseColliderPadding = 0.35f;
    private const float MinimumCorpseColliderSize = 1.25f;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Animator bodyAnimator;
    private AnimatorUpdateMode originalAnimatorUpdateMode;
    private LineRenderer detectRangeRing;
    private LineRenderer basicRangeRing;
    private LineRenderer skillRangeRing;
    private static Material rangeRingMaterial; // 所有怪共享一个圆环材质
    private const string DetectRingObjectName = "__DebugDetectRing";
    private const string BasicRingObjectName = "__DebugBasicRing";
    private const string SkillRingObjectName = "__DebugSkillRing";
    private float lastDetectRingRadius = -1f;
    private float lastBasicRingRadius = -1f;
    private float lastSkillRingRadius = -1f;
    private int lastRingSegments = -1;

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
    private Vector3 aiVelocity; // AI 态加速度平滑
    private float aiCurrentTurnSpeed; // AI 态角速度平滑
    public bool IsAbilityFacingLocked { get; set; }
    /// <summary>When true, ExecuteMovement skips locomotion so ability-driven dashes keep ownership of position.</summary>
    public bool IsAbilityLocomotionLocked { get; set; }

    /// <summary>追击目标（Actor.Update 填充 ActorContext.PlayerTarget；AIController 使用）。</summary>
    protected override Transform PlayerTarget => targetPlayer;

    /// <summary>默认 Controller = AIController（同物体挂载；未挂则运行时自动添加）。</summary>
    protected override IController CreateDefaultController()
    {
        var ai = GetComponent<AIController>();
        if (ai == null) ai = gameObject.AddComponent<AIController>();
        return ai;
    }

    protected override void Awake(){
        base.Awake(); // Actor：挂载默认 Controller
        if (Combat != null) Combat.AddLooseTags(this, new[] { "Actor.Monster" });

        meshRenderer = GetComponent<Renderer>();
        bodyAnimator = GetComponent<Animator>();
        if (bodyAnimator != null) originalAnimatorUpdateMode = bodyAnimator.updateMode;
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        corpseFadeBlock = new MaterialPropertyBlock();
        EnsureDualStatsMigrated();
        ApplyStatBlock(enemyStats, refillVitals: true);
        originalColor = bodyColor;
        bodyRenderers = GetComponentsInChildren<Renderer>(true);
        visualFx = GetComponent<ActorVisualFx>();
        if (visualFx == null) visualFx = gameObject.AddComponent<ActorVisualFx>();
        // Keep exactly one ActorVisualFx on the Enemy host. Extra copies on wrappers/children
        // share the same renderers and make Inspector tweaks appear to do nothing.
        var childFx = GetComponentsInChildren<ActorVisualFx>(true);
        for (int i = 0; i < childFx.Length; i++)
        {
            if (childFx[i] != null && childFx[i] != visualFx)
                Destroy(childFx[i]);
        }
        Transform ancestor = transform.parent;
        while (ancestor != null)
        {
            if (ancestor.GetComponent<MonsterActor>() != null)
                break;
            var parentFx = ancestor.GetComponent<ActorVisualFx>();
            if (parentFx != null)
                Destroy(parentFx);
            ancestor = ancestor.parent;
        }
        visualFx.RefreshRenderers();
        visualFx.SetDissolve(1f);
        visualFx.SetPossessionHighlight(false);

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

    /// <summary>Swaps one Skill-slot ability at runtime while preserving its configured HP cost.</summary>
    public void ReplaceSkillAbility(EnemyAbility original, EnemyAbility replacement)
    {
        if (original == null || replacement == null) return;
        float hpCost = 0f;
        for (int i = skillAbilities.Count - 1; i >= 0; i--)
        {
            SkillAbilityEntry entry = skillAbilities[i];
            if (entry == null || entry.ability == original)
            {
                if (entry != null && entry.ability == original) hpCost = entry.hpCost;
                skillAbilities.RemoveAt(i);
            }
            else if (entry.ability == replacement)
            {
                skillAbilities.RemoveAt(i);
            }
        }
        skillAbilities.Add(new SkillAbilityEntry { ability = replacement, hpCost = hpCost });
    }

    /// <summary>Restores the original Skill-slot ability after a temporary runtime replacement.</summary>
    public void RestoreSkillAbility(EnemyAbility original, EnemyAbility replacement)
    {
        if (original == null) return;
        float hpCost = 0f;
        for (int i = skillAbilities.Count - 1; i >= 0; i--)
        {
            SkillAbilityEntry entry = skillAbilities[i];
            if (entry != null && entry.ability == replacement) hpCost = entry.hpCost;
            if (entry == null || entry.ability == replacement || entry.ability == original)
                skillAbilities.RemoveAt(i);
        }
        skillAbilities.Add(new SkillAbilityEntry { ability = original, hpCost = hpCost });
    }

    protected virtual void Start(){
        // After child OnEnable has stamped AbilityType, only inject shared dash when no custom Mobility exists.
        if (!HasCustomMobilityAbility()&& GetComponent<EnemyAbility_MobilityDash>()== null)
            gameObject.AddComponent<EnemyAbility_MobilityDash>();

        gameObject.layer = 8;
        gameObject.tag = "Enemy";

        Physics.IgnoreLayerCollision(8, 8, true);
        Physics.IgnoreLayerCollision(8, 9, true);

        RefreshPlayerTarget();
        if (healthCanvas == null)
        {
            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name.IndexOf("Health", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    healthCanvas = canvases[i];
                    break;
                }
            }
        }
        if (healthSlider == null && healthCanvas != null)
            healthSlider = healthCanvas.GetComponentInChildren<Slider>(true);
        if (healthSlider == null)
            healthSlider = GetComponentInChildren<Slider>(true);
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(showHealthBar && ShowHealthBars);
        UpdateHealthUI();
    }

    /// <summary>
    /// 统一索敌目标查找：Start / ResetForSpawn / AIController 共用（避免多处散落的 FindGameObjectWithTag）。
    /// </summary>
    public void RefreshPlayerTarget(){
        var p = GameObject.FindGameObjectWithTag("Player");
        targetPlayer = p != null ? p.transform : null;
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
    protected override void Update(){
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
        if (IsMovementBlocked || IsAbilityLocomotionLocked)
        {
            possessVelocity = Vector3.zero;
            aiVelocity = Vector3.zero;
            aiCurrentTurnSpeed = 0f;
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
                    float turnRate = 12f;
                    GluttonyBodyState gluttonyState = GetComponent<GluttonyBodyState>();
                    if (gluttonyState != null && gluttonyState.SmallCatTurnMult > 0.01f)
                        turnRate *= gluttonyState.SmallCatTurnMult;
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, movementDeltaTime * turnRate);
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

        // AI 态：以速度和角速度加速/减速，避免决策刷新时抽动。
        Vector3 aiDir = cmd.HasMove ? cmd.MoveDirection : Vector3.zero;
        aiDir.y = 0f;
        float directionMagnitude = Mathf.Clamp01(aiDir.magnitude);
        Vector3 targetVelocity = Vector3.zero;

        if (directionMagnitude > 0.0001f)
        {
            Vector3 targetDirection = aiDir.normalized;
            float aiMoveSpeed = Combat != null ? Combat.ModifyMoveSpeed(moveSpeed) : moveSpeed;
            targetVelocity = targetDirection * aiMoveSpeed * directionMagnitude;

            if (!IsAbilityFacingLocked)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
                aiCurrentTurnSpeed = Mathf.MoveTowards(aiCurrentTurnSpeed, aiTurnSpeed, aiTurnAcceleration * Time.deltaTime);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, aiCurrentTurnSpeed * Time.deltaTime);
            }
        }
        else
        {
            aiCurrentTurnSpeed = Mathf.MoveTowards(aiCurrentTurnSpeed, 0f, aiTurnAcceleration * Time.deltaTime);
        }

        float velocityChange = targetVelocity.sqrMagnitude > aiVelocity.sqrMagnitude ? aiMoveAcceleration : aiMoveDeceleration;
        aiVelocity = Vector3.MoveTowards(aiVelocity, targetVelocity, velocityChange * Time.deltaTime);
        MoveWithSpherecast(aiVelocity * Time.deltaTime);
    }

    /// <summary>碰撞移动（AI与玩家共用）：CollideAndSlide 滑动，撞墙沿墙切向滑行。</summary>
    private void MoveWithSpherecast(Vector3 displacement)
    {
        if (displacement.sqrMagnitude < 0.0001f) return;
        int obstacleMask = ~((1 << 8) | (1 << 9));
        Vector3 targetPos = SlideMove(transform.position, 0.75f, 0.4f, displacement, obstacleMask);
        targetPos.y = transform.position.y;
        transform.position = targetPos;
    }

    /// <summary>
    /// 游戏视图调试：用 LineRenderer 圆环可视化索敌/攻击距离（随怪移动）。
    /// 显示条件：Inspector 勾选 showDebugRanges，且怪处于可作战的 AI 态（未附身/未倒地/未消失）。
    /// 池化复用：Return 时整物体 SetActive(false) 自动隐藏，Spawn 后本方法按勾选状态自动恢复。
    /// </summary>
    void UpdateDebugRanges(){
        bool show = showDebugRanges && !isPossessed && !isDowned
                    && Body != BodyState.Fading && Body != BodyState.Despawned;
        if (!show)
        {
            if (detectRangeRing != null) detectRangeRing.gameObject.SetActive(false);
            if (basicRangeRing != null) basicRangeRing.gameObject.SetActive(false);
            if (skillRangeRing != null) skillRangeRing.gameObject.SetActive(false);
            return;
        }
        RefreshRangeRing(ref detectRangeRing, DetectRingObjectName, detectionRadius, detectRangeColor, ref lastDetectRingRadius);
        RefreshRangeRing(ref basicRangeRing, BasicRingObjectName, basicAttackRange, basicRangeColor, ref lastBasicRingRadius);
        RefreshRangeRing(ref skillRangeRing, SkillRingObjectName, skillAttackRange, skillRangeColor, ref lastSkillRingRadius);
    }

    /// <summary>
    /// 刷新单个圆环：半径 ≤ 0 时隐藏；懒创建子物体 LineRenderer。
    /// 顶点用本地坐标（随父物体移动），半径/段数不变时零每帧开销；颜色变化才重写。
    /// </summary>
    void RefreshRangeRing(ref LineRenderer ring, string objectName, float radius, Color color, ref float lastRadius)
    {
        if (radius <= 0.01f)
        {
            if (ring != null) ring.gameObject.SetActive(false);
            return;
        }
        if (ring == null) ring = CreateRangeRing(objectName, color);
        ring.gameObject.SetActive(true);

        if (Mathf.Abs(lastRadius - radius) > 0.001f || lastRingSegments != rangeCircleSegments)
        {
            // 本地坐标 + 父链缩放补偿：顶点最终会乘 lossyScale 到世界空间，
            // 怪物 prefab 根物体 scale 可能 ≠1（如 pride=3.5），不补偿则圆环显示被放大。
            Vector3 lossy = transform.lossyScale;
            float sx = Mathf.Max(0.0001f, lossy.x);
            float sy = Mathf.Max(0.0001f, lossy.y);
            float sz = Mathf.Max(0.0001f, lossy.z);
            ring.positionCount = rangeCircleSegments + 1;
            // 本地坐标：以 (0, 0.05, 0) 为圆心（抬高避免与地面 z-fighting），随父物体移动
            for (int i = 0; i <= rangeCircleSegments; i++)
            {
                float angle = i * (360f / rangeCircleSegments) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(angle), 0.05f, Mathf.Sin(angle)) * radius;
                p.x /= sx; p.y /= sy; p.z /= sz;
                ring.SetPosition(i, p);
            }
            lastRadius = radius;
            lastRingSegments = rangeCircleSegments;
        }
        if (ring.startColor != color)
        {
            ring.startColor = color;
            ring.endColor = color;
        }
    }

    /// <summary>懒创建（或复用已存在子物体上的）LineRenderer 圆环。</summary>
    LineRenderer CreateRangeRing(string objectName, Color color)
    {
        Transform ringTransform = transform.Find(objectName);
        GameObject ringObject = ringTransform != null ? ringTransform.gameObject : new GameObject(objectName);
        if (ringTransform == null) ringObject.transform.SetParent(transform, false);

        LineRenderer line = ringObject.GetComponent<LineRenderer>();
        if (line == null) line = ringObject.AddComponent<LineRenderer>();
        if (rangeRingMaterial == null) rangeRingMaterial = new Material(Shader.Find("Sprites/Default"));
        line.sharedMaterial = rangeRingMaterial;
        line.useWorldSpace = false; // 本地坐标：随父物体移动，无需每帧重写顶点
        line.loop = false;
        line.positionCount = rangeCircleSegments + 1;
        // 线宽同样受父链缩放影响，除以 lossyScale 使世界空间显示恒为 0.06
        float scaleComp = Mathf.Max(0.0001f, transform.lossyScale.x);
        line.startWidth = 0.06f / scaleComp;
        line.endWidth = 0.06f / scaleComp;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    void UpdateAnimatorSpeed(){
        var anim = GetActiveAnimator();
        if (anim == null) return;
        float speed = Vector3.Distance(transform.position, lastFramePosition) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastFramePosition = transform.position;
        anim.SetFloat("Speed", speed);
    }

    /// <summary>
    /// Resolves the Animator that gameplay code (attack triggers, IsDowned, Speed, etc.) should drive.
    /// Most monsters carry a single Animator on the root (cached as bodyAnimator in Awake).
    /// Some monsters instead swap between multiple body-model children, each with its own
    /// Animator + Controller (different skeleton/avatar per model) toggled active/inactive
    /// — e.g. Gluttony's normal vs small-cat form. This falls back to the currently active
    /// child's Animator so trigger code stays uniform ("Basic"/"Skill"/"IsDowned"/"Speed")
    /// across every monster prefab regardless of where the Animator physically lives.
    /// </summary>
    public Animator GetActiveAnimator()
    {
        if (bodyAnimator != null) return bodyAnimator;
        return GetComponentInChildren<Animator>(false);
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
                    float basicCost = entry.hpCost * entry.ability.GetHpCostMultiplier();
                    if (isPossessed && !suppressPossessionDrain && basicCost > 0f)
                    {
                        Debug.Log($"[HpCost] Basic {entry.ability.abilityName}: cost={basicCost}, hp before={currentHealth}");
                        TakeDamage(basicCost, allowGreedGuardAbsorb: false);
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
                    float skillCost = entry.hpCost * entry.ability.GetHpCostMultiplier();
                    if (isPossessed && !suppressPossessionDrain && skillCost > 0f)
                    {
                        Debug.Log($"[HpCost] Skill {entry.ability.abilityName}: cost={skillCost}, hp before={currentHealth}");
                        TakeDamage(skillCost, allowGreedGuardAbsorb: false);
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
                    float mobilityCost = entry.hpCost * entry.ability.GetHpCostMultiplier();
                    if (isPossessed && !suppressPossessionDrain && mobilityCost > 0f)
                    {
                        Debug.Log($"[HpCost] Mobility {entry.ability.abilityName}: cost={mobilityCost}, hp before={currentHealth}");
                        TakeDamage(mobilityCost, allowGreedGuardAbsorb: false);
                    }
                    any = true;
                }
            }
        }
        return any;
    }

    /// <summary>
    /// AI 攻击前面向索敌目标：把 transform.forward 转正到玩家方向（仅水平面）。
    /// 用于修正 AI 追击走位（侧移/对峙 ±90°）导致的攻击方向偏移。
    /// </summary>
    void FaceAttackTarget(){
        Transform target = targetPlayer;
        if (target == null) return;
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(toTarget, Vector3.up);
    }

    public void PlayerTriggerBasicAttack(){
        TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.BasicAttack);
    }

    public void PlayerTriggerSkill(){
        TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.Skill);
    }

    public void PlayerTriggerMobility(){
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
        if (cost <= 0f)
        {
            foreach (var entry in mobilityAbilities)
            {
                if (entry != null && entry.ability == a) { cost = entry.hpCost; break; }
            }
        }
        cost *= a.GetHpCostMultiplier();
        if (cost > 0f)
        {
            Debug.Log($"[HpCost] Continuous {a.abilityName}: cost={cost}, hp before={currentHealth}");
            TakeDamage(cost, allowGreedGuardAbsorb: false);
        }
    }

    void LateUpdate(){
        UpdateDebugRanges();
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

    public virtual void TakeDamage(float amount)
    {
        TakeDamage(amount, allowGreedGuardAbsorb: true);
    }

    public virtual void TakeDamage(float amount, bool allowGreedGuardAbsorb)
    {
        if (isDowned || Body == BodyState.Fading || Body == BodyState.Despawned) return;
        if (IsUntargetable(this) || IsDamageImmune(this)) return;
        if (Combat != null) amount = Combat.ModifyIncomingDamage(amount);
        if (amount <= 0f) return;
        if (allowGreedGuardAbsorb && TryGreedGuardAbsorb(amount, environmental: false))
        {
            FlashDamage();
            return;
        }

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
        EnvyMarkTarget.NotifyDamageTaken(this as Enemy, amount);
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

    /// <summary>
    /// Environmental / tile hazard damage. Works on Active and Downed bodies (spike enter on corpses).
    /// Does not apply tenacity / weaken flow for downed corpses.
    /// </summary>
    public void TakeEnvironmentalDamage(float amount)
    {
        if (Body == BodyState.Fading || Body == BodyState.Despawned) return;
        if (IsDamageImmune(this)) return;
        // GR-M02: own normal black oil ignores terrain damage (no Guard convert credit).
        EnemyAbility_GreedBlackOil blackOil = GetComponentInChildren<EnemyAbility_GreedBlackOil>(true);
        if (blackOil != null && blackOil.ShouldIgnoreTerrainDamage())
            return;
        if (Combat != null) amount = Combat.ModifyIncomingDamage(amount);
        if (amount <= 0f) return;
        if (TryGreedGuardAbsorb(amount, environmental: true))
        {
            FlashDamage();
            return;
        }

        if (isDowned || Body == BodyState.Downed)
        {
            // Corpse: edge-trigger feedback only (HP already 0).
            FlashDamage();
            return;
        }

        TakeDamage(amount, allowGreedGuardAbsorb: false);
    }

    private bool TryGreedGuardAbsorb(float amount, bool environmental)
    {
        EnemyAbility_GreedGuard guard = GetComponentInChildren<EnemyAbility_GreedGuard>(true);
        return guard != null && guard.TryAbsorb(amount, environmental, out _);
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

    private bool HasCustomMobilityAbility(){
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
    public bool CanDamageSoul(){
        if (isPossessed || Body == BodyState.Fading || Body == BodyState.Despawned) return false;
        // Soul is invulnerable while a possession body is active (even if a leftover collider overlaps).
        var pm = PossessionManager.Instance;
        if (pm != null && pm.State == PossessionManager.SwitchState.Possessing) return false;
        return true;
    }

    public void ApplyOffensiveDamage(MonsterActor target, float amount)
    {
        if (!CanDamage(target) || amount <= 0f) return;
        // Lust LU-S06: pulled sources cannot damage the player's Possessed Body during pull + grace.
        if (LustPullDamageGate.ShouldBlock(this, target)) return;
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
            if (enemyTarget != null && totalBurnPercent > 0f && enemyTarget.GetComponent<BurnEffect>()== null)
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

    void BecomeWeakened(){
        isWeakened = true;
        // Keep authored materials; do not recolor the whole mesh.
    }

    public void OnPossessed(){
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
        EnsureDualStatsMigrated();
        ApplyStatBlock(possessedStats.HasConfiguredHealth ? possessedStats : enemyStats, refillVitals: true);
        SetRendererFade(1f);
        if (visualFx != null)
        {
            visualFx.SetDissolve(1f);
            visualFx.SetPossessionHighlight(true);
        }
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (IsUnderForeignSoul(renderer.transform)) continue;
            renderer.enabled = true;
        }
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            if (IsUnderForeignSoul(collider.transform)) continue;
            collider.enabled = true;
        }
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = false;
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(false);
        UpdateHealthUI();

        Animator animator = GetActiveAnimator();
        if (animator != null) animator.SetBool("IsDowned", false);
        EnvyMarkTarget.ClearMarksFromSource(this as Enemy);
    }

    public void OnUnpossessed(){
        isPossessed = false;
        gameObject.tag = "Enemy";
        if (bodyAnimator != null) bodyAnimator.updateMode = originalAnimatorUpdateMode;
        if (visualFx != null) visualFx.SetPossessionHighlight(false);
        // Cheat immortality must not linger on bodies left as normal enemies.
        ClearCheatDefenseEffects();
        EnvyMarkTarget.ClearMarksFromSource(this as Enemy);
    }

    private void EnterHitState(){
        if (isDowned || Body == BodyState.Fading || Body == BodyState.Despawned) return;
        Body = BodyState.Hit;
        hitStateEndsAt = Time.time + hitStateDuration;
    }

    protected virtual void Die(){
        isDowned = true;
        isPossessed = false;
        Body = BodyState.Downed;
        possessionWindowEndsAt = Time.time + corpsePossessionWindow;
        isPossessionReserved = false;
        transform.rotation = Quaternion.Euler(90f, transform.rotation.eulerAngles.y, 0f);
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            if (IsUnderForeignSoul(collider.transform)) continue;
            collider.enabled = true;
        }
        EnableCorpsePossessionCollider();
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = true;
        // Keep authored materials on corpse; dissolve FX handles fading later.
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(true);
        UpdateHealthUI();

        Animator animator = GetActiveAnimator();
        if (animator != null) animator.SetBool("IsDowned", true);

        if (corpseRoutine != null) StopCoroutine(corpseRoutine);
        corpseRoutine = StartCoroutine(CorpseLifecycleRoutine());
        Debug.Log($"[MonsterState] '{displayName}' downed. Possess window={corpsePossessionWindow:F1}s.");
    }

    /// <summary>
    /// 以"附身等待尸体"状态出场（供 PossessionBodyProvider 等直接刷尸体的场景）：
    /// 直接进入 Downed 尸体态，血量清零、AI 休眠、可被附身；
    /// 尸体**永不自动消散**（附身窗口无限），只有被附身后按正常流程消散（附身死亡/退出时）。
    /// </summary>
    public void SpawnAsPermanentCorpse()
    {
        if (Body == BodyState.Fading || Body == BodyState.Despawned) return;

        currentHealth = 0f;
        isDowned = true;
        isPossessed = false;
        Body = BodyState.Downed;
        possessionWindowEndsAt = float.PositiveInfinity; // 永久等待附身，不自动消散
        isPossessionReserved = false;
        transform.rotation = Quaternion.Euler(90f, transform.rotation.eulerAngles.y, 0f);
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = true;
        EnableCorpsePossessionCollider();
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = true;
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(true);
        UpdateHealthUI();

        Animator animator = GetActiveAnimator();
        if (animator != null) animator.SetBool("IsDowned", true);

        SetController(NullController.Instance); // 尸体 AI 完全休眠
        CancelAbilityRuntimeState();
        SetAbilityComponentsEnabled(false);

        if (corpseRoutine != null) StopCoroutine(corpseRoutine);
        corpseRoutine = StartCoroutine(CorpseLifecycleRoutine());
        Debug.Log($"[MonsterState] '{displayName}' spawned as permanent possession corpse.");
    }

    public void BeginDisappearing(){
        if (Body == BodyState.Fading || Body == BodyState.Despawned) return;
        if (corpseRoutine != null) StopCoroutine(corpseRoutine);
        isPossessionReserved = false;
        if (visualFx != null) visualFx.SetPossessionHighlight(false);
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = false;
        CancelAbilityRuntimeState();
        SetAbilityComponentsEnabled(false);

        isPossessed = false;
        isDowned = true;
        Body = BodyState.Fading;
        possessionWindowEndsAt = 0f;
        SetController(NullController.Instance);
        foreach (Collider collider in GetComponentsInChildren<Collider>())
        {
            if (IsUnderForeignSoul(collider.transform)) continue;
            collider.enabled = false;
        }
        corpseRoutine = StartCoroutine(FadeAndReturnRoutine());
        Debug.Log($"[MonsterState] '{displayName}' entered fading state.");
    }

    private System.Collections.IEnumerator CorpseLifecycleRoutine(){
        while (Body == BodyState.Downed && (isPossessionReserved || Time.time < possessionWindowEndsAt)) yield return null;
        if (Body == BodyState.Downed) BeginDisappearing();
    }

    private System.Collections.IEnumerator FadeAndReturnRoutine(){
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

    public void ResetForSpawn(){
        if (corpseRoutine != null)
        {
            StopCoroutine(corpseRoutine);
            corpseRoutine = null;
        }

        isWeakened = false;
        isDowned = false;
        isPossessed = false;
        ClearCheatDefenseEffects();
        isPossessionReserved = false;
        playerDetected = false;
        aiActiveOverride = true; // 池复用默认激活；流送场景由 MonsterSpawner 刷出后按 Chunk 状态改写
        Body = BodyState.Active;
        possessionWindowEndsAt = 0f;
        hitStateEndsAt = 0f;
        currentHealth = maxHealth;
        currentTenacity = maxTenacity;
        possessVelocity = Vector3.zero;
        aiVelocity = Vector3.zero;
        aiCurrentTurnSpeed = 0f;
        IsAbilityFacingLocked = false;
        IsAbilityLocomotionLocked = false;
        SetAbilityComponentsEnabled(true);
        CancelAbilityRuntimeState();
        gameObject.tag = "Enemy";
        SetRendererFade(1f);
        // Root world pose is owned by MonsterPool.Spawn (applied after this reset).
        // Only restore local offset when this actor is nested under a pooled root.
        if (transform.parent != null)
        {
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
        }
        if (visualFx != null)
        {
            visualFx.SetDissolve(1f);
            visualFx.SetPossessionHighlight(false);
        }
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (IsUnderForeignSoul(renderer.transform)) continue;
            renderer.enabled = true;
        }
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            if (IsUnderForeignSoul(collider.transform)) continue;
            collider.enabled = true;
        }
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = false;
        Animator animator = GetActiveAnimator();
        if (animator != null) animator.SetBool("IsDowned", false);
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(showHealthBar && ShowHealthBars);
        RefreshPlayerTarget();
        SetController(GetComponent<AIController>());
        UpdateHealthUI();
    }

    public void ResetForPool(){
        SetController(NullController.Instance);
        CancelAbilityRuntimeState();
        possessVelocity = Vector3.zero;
        aiVelocity = Vector3.zero;
        aiCurrentTurnSpeed = 0f;
        if (corpseRoutine != null)
        {
            StopCoroutine(corpseRoutine);
            corpseRoutine = null;
        }
    }

    /// <summary>
    /// 流送恢复（Phase 3）：MonsterPool.Spawn 复位（ResetForSpawn）后立即调用，把快照状态覆盖回实例。
    /// health 下限钳 1：防止非倒地恢复后 Update 中 currentHealth &lt;= 0 被自动判死。
    /// downed=true 复用 Die 的尸体姿态/碰撞/动画/淡出协程——附身窗口重新计时（近似行为，
    /// 精确剩余窗口恢复留 TODO；当前快照构建已把倒地怪分流到 CorpseSnapshot，正常不走此分支）。
    /// </summary>
    public void ApplyStreamSnapshot(float health, bool weakened, bool downed)
    {
        if (downed)
        {
            currentHealth = 0f;
            Die();
            return;
        }
        currentHealth = Mathf.Clamp(health, 1f, maxHealth);
        if (weakened)
        {
            currentTenacity = 0f;
            BecomeWeakened();
        }
        UpdateHealthUI();
    }

    private void CancelAbilityRuntimeState(){
        foreach (EnemyAbility ability in GetComponentsInChildren<EnemyAbility>(true))
            if (ability != null) ability.ResetForOwnerReuse();
    }

    private void SetAbilityComponentsEnabled(bool enabled)
    {
        foreach (EnemyAbility ability in GetComponentsInChildren<EnemyAbility>(true))
            if (ability != null) ability.enabled = enabled;
    }

    public bool TryReserveForPossession(){
        if (!CanBePossessed) return false;
        isPossessionReserved = true;
        Debug.Log($"[MonsterState] '{displayName}' reserved for possession.");
        return true;
    }

    public void CancelPossessionReservation(){
        if (!isPossessionReserved) return;
        isPossessionReserved = false;
        Debug.Log($"[MonsterState] '{displayName}' possession reservation cancelled.");
    }

    public string GetPossessionDebugState(){
        return $"body={Body}, downed={isDowned}, possessed={isPossessed}, reserved={isPossessionReserved}, remaining={PossessionWindowRemaining:F2}s, active={gameObject.activeInHierarchy}";
    }

    private void EnableCorpsePossessionCollider(){
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
        corpsePossessionCollider.size = Vector3.Max(localBounds.size + Vector3.one * CorpseColliderPadding * 2f,
            Vector3.one * MinimumCorpseColliderSize);
        corpsePossessionCollider.isTrigger = true;
        corpsePossessionCollider.enabled = true;
        Debug.Log($"[CorpseCollider] '{displayName}' center={corpsePossessionCollider.bounds.center:F2}, size={corpsePossessionCollider.bounds.size:F2}, enabled={corpsePossessionCollider.enabled}");
    }

    private void SetRendererFade(float fade)
    {
        if (visualFx != null)
        {
            visualFx.SetDissolve(fade);
            return;
        }

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
                if (material == null) continue;
                renderer.GetPropertyBlock(corpseFadeBlock, materialIndex);
                if (material.HasProperty("_CorpseFade"))
                    corpseFadeBlock.SetFloat("_CorpseFade", fade);
                if (material.HasProperty("_DissolveAmount"))
                    corpseFadeBlock.SetFloat("_DissolveAmount", 1f - fade);
                if (material.HasProperty("_BaseColor"))
                {
                    Color c = material.GetColor("_BaseColor");
                    c.a = fade;
                    corpseFadeBlock.SetColor("_BaseColor", c);
                }
                else if (material.HasProperty("_Color"))
                {
                    Color c = material.GetColor("_Color");
                    c.a = fade;
                    corpseFadeBlock.SetColor("_Color", c);
                }
                renderer.SetPropertyBlock(corpseFadeBlock, materialIndex);
            }
        }
    }

    protected void FlashDamage()
    {
        if (visualFx != null)
        {
            visualFx.hitFlashColor = flashColor;
            visualFx.hitFlashDuration = flashDuration;
            visualFx.PlayHitFlash();
            return;
        }
        if (meshRenderer != null) StartCoroutine(FlashRoutine());
    }

    System.Collections.IEnumerator FlashRoutine(){
        Color orig = meshRenderer.material.color;
        meshRenderer.material.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        if (meshRenderer == null) yield break;
        if (isDowned) meshRenderer.material.color = downedColor;
        else if (isWeakened) meshRenderer.material.color = weakenedColor;
        else if (isPossessed) meshRenderer.material.color = possessedColor;
        else meshRenderer.material.color = bodyColor;
    }

    private void ClearCheatDefenseEffects()
    {
        suppressPossessionDrain = false;
        if (Combat != null)
            Combat.RemoveEffectsWithTag("Effect.Defense.DamageImmune");
    }

    private void EnsureDualStatsMigrated()
    {
        if (enemyStats.HasConfiguredHealth) return;
        MonsterStatBlock captured = MonsterStatBlock.FromRuntime(
            maxHealth, moveSpeed, acceleration, deceleration, maxTenacity, collisionDamage, attackSpeed);
        enemyStats = captured;
        if (!possessedStats.HasConfiguredHealth)
            possessedStats = captured;
    }

    private void ApplyStatBlock(MonsterStatBlock block, bool refillVitals)
    {
        if (!block.HasConfiguredHealth) return;
        maxHealth = block.maxHealth;
        moveSpeed = block.moveSpeed;
        acceleration = block.acceleration;
        deceleration = block.deceleration;
        maxTenacity = block.maxTenacity;
        collisionDamage = block.collisionDamage;
        attackSpeed = block.attackSpeed > 0f ? block.attackSpeed : 1f;
        if (refillVitals)
        {
            currentHealth = maxHealth;
            currentTenacity = maxTenacity;
        }
        else
        {
            currentHealth = Mathf.Min(currentHealth, maxHealth);
            currentTenacity = Mathf.Min(currentTenacity, maxTenacity);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Keep dual blocks in sync when designers still tweak legacy runtime fields in prefabs
        // that have never authored dual blocks.
        if (!Application.isPlaying && !enemyStats.HasConfiguredHealth && maxHealth > 0f)
        {
            enemyStats = MonsterStatBlock.FromRuntime(
                maxHealth, moveSpeed, acceleration, deceleration, maxTenacity, collisionDamage, attackSpeed);
            if (!possessedStats.HasConfiguredHealth)
                possessedStats = enemyStats;
        }
    }
#endif

    /// <summary>
    /// SoulActor is parented under the body while possessed. Body-wide collider/renderer
    /// toggles must not touch that foreign hierarchy.
    /// </summary>
    private static bool IsUnderForeignSoul(Transform t)
    {
        while (t != null)
        {
            if (t.GetComponent<SoulActor>() != null) return true;
            t = t.parent;
        }
        return false;
    }

    public void UpdateHealthUI(){
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

    void OnDrawGizmosSelected(){
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }

    // ── IActor 实现（Actor 抽象成员） ──

    public override bool IsDowned => isDowned;
    public override string DisplayName => displayName;

    /// <summary>
    /// AI keeps Basic/Skill1 semantics. A possessed monster uses left-click Basic, Q Skill,
    /// Space mobility, right-click corpse switching, and F release. Bullet Time starts automatically after possession.
    /// </summary>
    protected override void ExecuteButtons(in ControlCommand cmd)
    {
        if ((cmd.Pressed & CommandButtons.Basic) != 0) PlayerTriggerBasicAttack();

        if (!IsPlayerControlled)
        {
            // AI 态：攻击方向依赖 transform.forward，但 AI 追击走位（侧移/对峙 ±90°）会让 forward 偏离玩家。
            // 触发攻击前先面向索敌目标，保证剑气/冲锋/斩击沿玩家方向打出，不受走位朝向污染。
            if ((cmd.Pressed & (CommandButtons.Basic | CommandButtons.Skill1 | CommandButtons.Mobility)) != 0)
                FaceAttackTarget();

            if ((cmd.Pressed & CommandButtons.Skill1) != 0 && TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.Skill))
            {
                AIController ai = Controller as AIController;
                if (ai != null) ai.NotifySkillTriggered();
            }
            if ((cmd.Pressed & CommandButtons.Mobility) != 0) PlayerTriggerMobility();
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
                HpCost = e.hpCost,
            });
        }
    }
}
