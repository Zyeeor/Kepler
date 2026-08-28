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
    /// <summary>True while this monster is a downed, fading, or already despawned corpse.</summary>
    public bool IsCorpse => isDowned || Body == BodyState.Downed || Body == BodyState.Fading || Body == BodyState.Despawned;
    public bool CanBePossessed => isPossessable && Body == BodyState.Downed && !isPossessed && !isPossessionReserved
        && (!IsBossBattleReserveBody || currentHealth > 0f) && Time.time < possessionWindowEndsAt;
    public bool CanCompleteReservedPossession => Body == BodyState.Downed && !isPossessed && isPossessionReserved;
    public float PossessionWindowRemaining => Body == BodyState.Downed && !isPossessionReserved ? Mathf.Max(0f, possessionWindowEndsAt - Time.time) : 0f;

    /// <summary>当前控制状态（是否被玩家控制）。</summary>
    public ControlState Control => IsPlayerControlled ? ControlState.Possessed : ControlState.AI;

    /// <summary>
    /// Raised right after an ability HP cost is actually deducted from a possessed body.
    /// Args: the body that paid, and the deducted amount.
    /// Presentation listeners (health bar burn flash) use this instead of watching the
    /// slider, because the slider cannot tell a paid cost apart from incoming damage.
    /// </summary>
    public static event Action<MonsterActor, float> AbilityHpCostPaid;

    /// <summary>
    /// Raised at the lethal damage settlement point, before the body enters its corpse
    /// lifecycle. Player damage is explicitly marked so Elite rewards do not wait for
    /// corpse fade/despawn and do not trigger for environmental damage.
    /// </summary>
    public static event Action<MonsterActor> OnMonsterKilled;

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
    [Tooltip("Run-level sin identity used by possession imprints and Boss visual composition.")]
    public SinType sinType = SinType.None;
    [Tooltip("Bosses and other special actors cannot be possessed.")]
    public bool isPossessable = true;

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
        AIController ai = GetCachedAiController();
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
    [AiConfigId]
    [Tooltip("在配置库中按 id 查找 AI 配置条目。为空或未命中时使用默认值（Inspector 提供下拉）。")]
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
    /// <summary>生效普攻范围（AI 配置 + 已解锁覆盖，实时取 max）。</summary>
    public float basicAttackRange => AiConfig.EffectiveBasicAttackRange();
    /// <summary>生效技能范围（AI 配置 + 已解锁覆盖，实时取 max）。</summary>
    public float skillAttackRange => AiConfig.EffectiveSkillAttackRange();
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
    /// <summary>怪物间软分离半径（AI 配置，米）。</summary>
    public float separationRadius => AiConfig.separationRadius;
    /// <summary>怪物间软分离强度（AI 配置，0~2）。</summary>
    public float separationStrength => AiConfig.separationStrength;
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
    [Tooltip("Optional visual-only root. Imprint size growth never scales the Actor root, colliders or navigation; ability hitboxes use the same multiplier explicitly.")]
    public Transform visualScaleRoot;
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
    /// <summary>Runtime-only body supplied by the Boss fight. It keeps an infinite possession window.</summary>
    public bool IsBossBattleReserveBody { get; private set; }
    /// <summary>Current possessed-body scale used by ability visuals and hitboxes.</summary>
    public float PossessionCombatScaleMultiplier { get; private set; } = 1f;
    /// <summary>
    /// Effective combat scale shared by elite bodies and Gluttony-imprinted possessed bodies.
    /// Visual scaling remains limited to the visual roots; abilities use this value explicitly
    /// for hit volumes, projectile visuals, summon sizes and related effect offsets.
    /// </summary>
    public float CombatScaleMultiplier => Mathf.Max(1f, PossessionCombatScaleMultiplier)
        * Mathf.Max(1f, eliteVisualScaleMultiplier);
    [Tooltip("流送 AI 激活开关：false 时 AI 完全休眠（不产出指令，仅 0.5s 低频维持索敌目标缓存）。由 MonsterSpawner 按 Chunk 状态机驱动；附身中的怪永不休眠。默认 true，非流送场景（调试刷怪等）行为不变。")]
    public bool aiActiveOverride = true;

    [Header("Vertical Placement")]
    [Tooltip("怪物存活状态使用的世界 Y。生成和附身完成后直接采用此值，不进行 Collider 贴地计算。")]
    public float aliveY = 0f;
    [Tooltip("怪物尸体状态使用的世界 Y。死亡转为尸体后直接采用此值，不进行 Collider 贴地计算。")]
    public float corpseY = 0f;

    [Header("Corpse Lifecycle")]
    [Min(0f)] public float corpsePossessionWindow = 5f;
    [Min(0f)] public float corpseFadeDuration = 3f;
    [Min(0f)] public float hitStateDuration = 0.12f;

    [Header("Ability HP Cost Death")]
    [Tooltip("附身技能烧血把耐久扣到 0 时，延迟死亡结算的最长宽限秒数。窗口内让该次技能跑完伤害判定（如砸地的延迟命中），随后立即死亡。充能/持续类技能会等到本次释放结束。")]
    [Min(0f)] public float abilityCostDeathGrace = 0.6f;
    [Tooltip("充能/持续类技能在濒死宽限期内可额外续命的上限秒数（防止一直按住不放无限续命）。超时强制结算死亡。")]
    [Min(0f)] public float abilityCostDeathHoldCap = 3f;

    /// <summary>
    /// 附身技能 HP 代价已把耐久扣到 0，死亡结算被推迟到该次技能判定完成之后。
    /// 宽限期内 Body 仍可正常结算伤害（阵营/命中判定不变），但不得再触发新技能，
    /// 且后续任何代价都不再扣血（耐久已为 0）。
    /// </summary>
    public bool IsAbilityCostDeathPending { get; private set; }
    private Coroutine abilityCostDeathRoutine;
    private EnemyAbility abilityCostDeathSource;
    private bool payingAbilityCost;
    private EnemyAbility abilityCostPaymentSource;
    private bool playerDamageContext;

    private float possessionWindowEndsAt;
    private float hitStateEndsAt;
    private bool isPossessionReserved;
    private bool bossDamageContext;
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
    private AIController aiController;
    private GluttonyBodyState gluttonyBodyState;
    private BossReserveCorpseVisualFx reserveCorpseVisual;
    private Animator bodyAnimator;
    private AnimatorUpdateMode originalAnimatorUpdateMode;
    private Animator[] cachedAnimators;
    private bool animatorCacheInitialized;
    private Camera billboardCamera;
    private readonly Dictionary<Animator, AnimatorUpdateMode> possessedAnimatorUpdateModes = new Dictionary<Animator, AnimatorUpdateMode>();
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
    [Tooltip("Skill abilities = right-click when possessing this enemy")]

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
    private Vector3 authoredVisualScale = Vector3.one;
    private bool authoredVisualScaleCaptured;
    private readonly List<Transform> fallbackVisualScaleRoots = new List<Transform>();
    private readonly List<Vector3> fallbackAuthoredVisualScales = new List<Vector3>();
    private float authoredPossessedMaxHealth;
    private Vector3 aiVelocity; // AI 态加速度平滑
    private float aiCurrentTurnSpeed; // AI 态角速度平滑
    // 软分离降频采样缓存（P3）：O(n²) 遍历从每帧降为每 SeparationSampleInterval 秒一次，其余帧复用上次结果。
    private const float SeparationSampleInterval = 0.1f;
    private float separationNextSampleTime = -999f;
    private Vector3 cachedSeparationVelocity = Vector3.zero;
    // 软分离共享空间桶（P5）：采样时按分离半径分桶，每怪只查 9 邻桶内邻居，
    // 把全量 O(n²) 降为 O(n×平均邻居数)（分散场景约 6 倍，100 怪场景 ~12 倍）。
    // 桶表在同一 0.1s 窗口内全局共享（首只采样怪重建，其余复用），避免重复构建。
    static float sharedBucketStamp = -999f;
    static float sharedBucketSize = 0f;
    static Dictionary<Vector2Int, List<Enemy>> sharedBuckets;
    [Header("Spawn Snapshot")]
    public SpawnOrigin spawnOrigin = SpawnOrigin.PeriodicPressure;
    public int spawnDifficultyTier;
    public float baseSpawnMaxHealth;
    public float spawnHealthMultiplier = 1f;
    public float spawnDamageMultiplier = 1f;
    private float eliteHealthMultiplier = 1f;
    private float eliteAttackDamageMultiplier = 1f;
    private float eliteVisualScaleMultiplier = 1f;
    private bool eliteRuntimeApplied;
    private Vector3 healthBarBaseWorldPosition;
    private Vector3 healthBarBaseWorldScale = Vector3.one;
    private Vector3 healthBarBaseLocalPosition;
    private Vector3 healthBarBaseLocalScale = Vector3.one;
    private Vector3 healthBarBaseCenterOffset;
    private float healthBarBaseHeightOffset;
    private bool healthBarLayoutCaptured;
    private float lastHealthBarLayoutScale = -1f;
    [NonSerialized] public MonsterActor lastDamageSource;
    [NonSerialized] public bool wasKilledByPlayer;
    public bool IsAbilityFacingLocked { get; set; }
    /// <summary>When true, ExecuteMovement skips locomotion so ability-driven dashes keep ownership of position.</summary>
    public bool IsAbilityLocomotionLocked { get; set; }
    private EnemyAbility activeAbilityTelegraph;
    private bool locomotionLockBeforeTelegraph;

    /// <summary>Ability currently reserving this monster during an AI cast wind-up.</summary>
    public EnemyAbility ActiveAbilityTelegraph => activeAbilityTelegraph;
    public bool IsAbilityTelegraphing => activeAbilityTelegraph != null;

    /// <summary>
    /// Reserves the actor for one AI cast. This is deliberately actor-local instead of a
    /// gameplay tag: it blocks every other ability without changing the authored tag graph.
    /// </summary>
    public bool TryBeginAbilityTelegraph(EnemyAbility ability)
    {
        if (ability == null || isPossessed || isDowned || Body != BodyState.Active)
            return false;
        if (activeAbilityTelegraph != null && activeAbilityTelegraph != ability)
            return false;

        if (activeAbilityTelegraph == null)
        {
            activeAbilityTelegraph = ability;
            locomotionLockBeforeTelegraph = IsAbilityLocomotionLocked;
        }
        IsAbilityLocomotionLocked = true;
        return true;
    }

    public void EndAbilityTelegraph(EnemyAbility ability)
    {
        if (activeAbilityTelegraph != ability) return;
        activeAbilityTelegraph = null;
        IsAbilityLocomotionLocked = locomotionLockBeforeTelegraph;
        locomotionLockBeforeTelegraph = false;
    }

    /// <summary>True after the Elite runtime profile is applied or while its build carrier exists.</summary>
    public bool IsElite => eliteRuntimeApplied || EliteBuildCarrier.Get(this) != null;

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
        ResolveSinIdentityIfUnset();
        if (!(this is BossSevenfoldActor) && GetComponent<MonsterPathfinder>() == null)
            gameObject.AddComponent<MonsterPathfinder>();
        base.Awake(); // Actor：挂载默认 Controller
        if (Combat != null) Combat.AddLooseTags(this, new[] { "Actor.Monster" });

        aiController = GetComponent<AIController>();
        if (sinType == SinType.Gluttony)
            gluttonyBodyState = GetComponent<GluttonyBodyState>();
        reserveCorpseVisual = GetComponent<BossReserveCorpseVisualFx>();
        meshRenderer = GetComponent<Renderer>();
        if (visualScaleRoot != null)
        {
            authoredVisualScale = visualScaleRoot.localScale;
            authoredVisualScaleCaptured = true;
        }
        else
        {
            CaptureFallbackVisualScaleRoots();
        }
        bodyAnimator = GetComponent<Animator>();
        if (bodyAnimator != null) originalAnimatorUpdateMode = bodyAnimator.updateMode;
        CacheAnimators();
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        corpseFadeBlock = new MaterialPropertyBlock();
        EnsureDualStatsMigrated();
        ApplyStatBlock(enemyStats, refillVitals: true);
        originalColor = bodyColor;
        bodyRenderers = GetComponentsInChildren<Renderer>(true);
        CaptureHealthBarLayout();
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

    void ResolveSinIdentityIfUnset()
    {
        if (sinType != SinType.None) return;
        string id = transform.root != null ? transform.root.name.ToLowerInvariant() : name.ToLowerInvariant();
        if (id.Contains("pride")) sinType = SinType.Pride;
        else if (id.Contains("wrath")) sinType = SinType.Wrath;
        else if (id.Contains("gluttony")) sinType = SinType.Gluttony;
        else if (id.Contains("greed")) sinType = SinType.Greed;
        else if (id.Contains("envy")) sinType = SinType.Envy;
        else if (id.Contains("lust")) sinType = SinType.Lust;
        else if (id.Contains("sloth")) sinType = SinType.Sloth;
    }

    /// <summary>
    /// Assigns the run-level sin without editing shared source prefabs. The hint is an
    /// authoritative spawn identity, so it intentionally repairs a stale serialized value
    /// left on a pooled/runtime instance instead of only filling None.
    /// </summary>
    public void ResolveSinIdentityFromHint(string hint)
    {
        if (string.IsNullOrEmpty(hint)) return;
        string id = hint.ToLowerInvariant();
        if (id.Contains("pride") || id.Contains("傲慢")) sinType = SinType.Pride;
        else if (id.Contains("wrath") || id.Contains("愤怒")) sinType = SinType.Wrath;
        else if (id.Contains("gluttony") || id.Contains("暴食")) sinType = SinType.Gluttony;
        else if (id.Contains("greed") || id.Contains("贪婪")) sinType = SinType.Greed;
        else if (id.Contains("envy") || id.Contains("嫉妒")) sinType = SinType.Envy;
        else if (id.Contains("lust") || id.Contains("色欲")) sinType = SinType.Lust;
        else if (id.Contains("sloth") || id.Contains("怠惰")) sinType = SinType.Sloth;
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
        CaptureHealthBarLayout();
        RefreshHealthBarLayout(force: true);
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

    /// <summary>让击杀回声怪在刷出后的第一帧直接进入追击，不等待 AI 随机决策节拍。</summary>
    public void BeginImmediateChase()
    {
        RefreshPlayerTarget();
        AIController ai = GetCachedAiController();
        if (ai != null) ai.BeginImmediateChase();
    }

    private AIController GetCachedAiController()
    {
        if (aiController == null) aiController = GetComponent<AIController>();
        return aiController;
    }

    private BossReserveCorpseVisualFx GetCachedReserveCorpseVisual()
    {
        if (reserveCorpseVisual == null) reserveCorpseVisual = GetComponent<BossReserveCorpseVisualFx>();
        return reserveCorpseVisual;
    }

    private GluttonyBodyState GetCachedGluttonyBodyState()
    {
        // Gluttony abilities can add this component lazily from their first activation. Keep
        // the legacy per-frame lookup until it appears, then retain the component permanently.
        if (gluttonyBodyState == null && sinType == SinType.Gluttony)
            gluttonyBodyState = GetComponent<GluttonyBodyState>();
        return gluttonyBodyState;
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
    /// 与 Update 的尸体门对齐：消散中的身体不得再消费移动指令。
    /// 基类 FixedUpdate 按 !IsPlayerControlled 分派移动，而脱离附身会把 Controller 置为
    /// NullController，若不在此拦住，尸体会继续走 AI 移动分支（详见 ExecuteMovement 的尸体门）。
    /// </summary>
    protected override void FixedUpdate()
    {
        if (Body == BodyState.Fading || Body == BodyState.Despawned) return;
        base.FixedUpdate();
    }

    /// <summary>
    /// 移动段：AI 态（AIController）匀速移动、朝移动方向；玩家附身态（PlayerController）
    /// 加速度平滑 + 静止朝鼠标。两者共用SphereCast预检测和Transform位移。
    /// </summary>
    protected override void ExecuteMovement(in ControlCommand cmd)
    {
        // 尸体/消散中的身体不移动：脱离附身后 Controller 变为 NullController，
        // 基类会把移动分派到 AI 分支，这里兜住残留指令带来的滑行。
        if (isDowned || Body == BodyState.Downed || Body == BodyState.Fading || Body == BodyState.Despawned)
        {
            possessVelocity = Vector3.zero;
            aiVelocity = Vector3.zero;
            aiCurrentTurnSpeed = 0f;
            return;
        }

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
                    GluttonyBodyState gluttonyState = GetCachedGluttonyBodyState();
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

        // 怪物间软分离：叠加远离邻近活怪的排斥速度，防止多怪追击时重叠堆积。
        // 与 aiVelocity 独立（停步/对峙时仍生效），不参与平滑，仅作最终位移修正。
        Vector3 separation = ComputeSeparationVelocity();
        // P1 防超速：分离速度直接叠加会让合成速度超过 moveSpeed（默认最高约 1.8×），
        // 这里对「追击 + 分离」合成速度做 clamp，保证横散/贴脸分离时移动不超标。
        Vector3 totalVelocity = aiVelocity + separation;
        float maxSpeed = Combat != null ? Combat.ModifyMoveSpeed(moveSpeed) : moveSpeed;
        if (totalVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            totalVelocity = totalVelocity.normalized * maxSpeed;
        MoveWithSpherecast(totalVelocity * Time.deltaTime);
    }

    /// <summary>
    /// 计算怪物间软分离速度（防重叠堆积）：采样时经共享空间桶查询分离半径内邻居，
    /// 按「远离方向 × 距离衰减权重」累加，输出量纲为速度。
    /// 仅 AI 态调用（玩家附身的身体不参与），且跳过倒地/附身/消散中的邻居。
    /// </summary>
    Vector3 ComputeSeparationVelocity()
    {
        // P3 降频采样：分离速度随距离平滑变化，无需每帧重算。
        // 间隔内复用上次结果；P5 空间桶把全量遍历降为 9 邻桶近邻查询。
        float now = Time.time;
        if (now < separationNextSampleTime) return cachedSeparationVelocity;

        float radius = separationRadius;
        float strength = separationStrength;
        Vector3 result = Vector3.zero;
        if (radius > 0f && strength > 0f && EnemyRegistry.All.Count > 1)
        {
            EnsureSharedSeparationBuckets(now, radius);

            Vector3 self = transform.position;
            Vector3 accumulated = Vector3.zero;
            float sqrRadius = radius * radius;
            int bRange = Mathf.Max(1, Mathf.CeilToInt(radius / sharedBucketSize));
            int bx = Mathf.FloorToInt(self.x / sharedBucketSize);
            int bz = Mathf.FloorToInt(self.z / sharedBucketSize);
            for (int dx = -bRange; dx <= bRange; dx++)
            for (int dz = -bRange; dz <= bRange; dz++)
            {
                if (!sharedBuckets.TryGetValue(new Vector2Int(bx + dx, bz + dz), out var list)) continue;
                for (int j = 0; j < list.Count; j++)
                {
                    var other = list[j];
                    if (other == null || ReferenceEquals(other, this)) continue;

                    Vector3 delta = self - other.transform.position;
                    delta.y = 0f;
                    float sqrDist = delta.sqrMagnitude;
                    if (sqrDist >= sqrRadius) continue;

                    if (sqrDist < 0.0001f)
                    {
                        // P4 完全重叠兜底：同帧生成在同一格/极端堆叠时，按桶内序给确定性方向分离，避免永久卡死。
                        accumulated += new Vector3((j & 1) == 0 ? 1f : -1f, 0f, (j & 2) == 0 ? 0f : 1f);
                        continue;
                    }

                    float dist = Mathf.Sqrt(sqrDist);
                    float weight = 1f - dist / radius; // 距离越近排斥越强，贴脸时权重最大
                    accumulated += (delta / dist) * weight;
                }
            }

            if (accumulated.sqrMagnitude >= 0.0001f)
            {
                float speed = Combat != null ? Combat.ModifyMoveSpeed(moveSpeed) : moveSpeed;
                result = accumulated.normalized * strength * speed;
            }
        }

        cachedSeparationVelocity = result;
        separationNextSampleTime = now + SeparationSampleInterval;
        return result;
    }

    /// <summary>
    /// 确保共享分离桶新鲜：同一 SeparationSampleInterval 窗口内全局复用一份桶表（首只采样怪重建）。
    /// 桶边长取出现过的最大 separationRadius（所有怪同配置时等于 radius），
    /// 查询侧按 bRange = ceil(radius / bucketSize) 扩邻，保证不同半径怪也能覆盖其分离半径内邻居。
    /// </summary>
    void EnsureSharedSeparationBuckets(float now, float radius)
    {
        if (radius > sharedBucketSize)
        {
            sharedBucketSize = radius;
            sharedBucketStamp = -999f; // 桶尺寸变化需强制重建
        }
        if (sharedBuckets != null && now < sharedBucketStamp + SeparationSampleInterval) return;

        sharedBucketStamp = now;
        if (sharedBucketSize <= 0f) sharedBucketSize = 1f;
        if (sharedBuckets == null) sharedBuckets = new Dictionary<Vector2Int, List<Enemy>>(64);
        else sharedBuckets.Clear();

        var enemies = EnemyRegistry.All;
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null) continue;
            if (e.isDowned || e.isPossessed
                || e.Body == BodyState.Fading || e.Body == BodyState.Despawned) continue;
            Vector3 p = e.transform.position;
            var key = new Vector2Int(Mathf.FloorToInt(p.x / sharedBucketSize), Mathf.FloorToInt(p.z / sharedBucketSize));
            if (!sharedBuckets.TryGetValue(key, out var list))
                sharedBuckets[key] = list = new List<Enemy>(4);
            list.Add(e);
        }
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
        if (rangeRingMaterial == null)
        {
            rangeRingMaterial = GameManager.SharedMaterialOptimizationEnabled
                ? RendererShadowVisibility.GetSharedTransientLineMaterial()
                : new Material(Shader.Find("Sprites/Default"));
            if (rangeRingMaterial == null)
                rangeRingMaterial = new Material(Shader.Find("Sprites/Default"));
        }
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
        // Preserve the legacy root Animator contract: callers may drive it even when the
        // component/object is disabled. Child-model swaps use the active hierarchy fallback.
        if (bodyAnimator != null)
            return bodyAnimator;

        Animator[] animators = GetCachedAnimators();
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator != null && animator.gameObject.activeInHierarchy)
                return animator;
        }
        return null;
    }

    private void CacheAnimators()
    {
        cachedAnimators = GetComponentsInChildren<Animator>(true);
        animatorCacheInitialized = true;
    }

    private Animator[] GetCachedAnimators()
    {
        if (!animatorCacheInitialized || cachedAnimators == null)
        {
            CacheAnimators();
            return cachedAnimators;
        }

        for (int i = 0; i < cachedAnimators.Length; i++)
        {
            if (cachedAnimators[i] == null)
            {
                CacheAnimators();
                break;
            }
        }
        return cachedAnimators;
    }

    private Camera GetBillboardCamera()
    {
        if (billboardCamera == null || !billboardCamera.isActiveAndEnabled
            || !billboardCamera.CompareTag("MainCamera"))
            billboardCamera = Camera.main;
        return billboardCamera;
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
                    SettleAbilityHpCost(entry.ability, entry.hpCost, "Basic");
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
                    SettleAbilityHpCost(entry.ability, entry.hpCost, "Skill");
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
                    SettleAbilityHpCost(entry.ability, entry.hpCost, "Mobility");
                    any = true;
                }
            }
        }
        return any;
    }

    /// <summary>
    /// 结算一次附身技能的 HP 代价（三槽触发共用）。
    /// 代价扣血在 payingAbilityCost 上下文里进行，TakeDamage 因此可以把"代价致死"
    /// 与"敌人伤害致死"区分开：前者延迟死亡结算，保证该次技能判定跑完。
    /// </summary>
    private void SettleAbilityHpCost(EnemyAbility ability, float baseCost, string label)
    {
        if (ability == null) return;
        float cost = baseCost * ability.GetHpCostMultiplier();
        if (isPossessed && PossessionImprintManager.Instance != null)
            cost *= PossessionImprintManager.Instance.GetPossessionDrainMultiplier(this);
        if (!isPossessed || suppressPossessionDrain || cost <= 0f) return;

        Debug.Log($"[HpCost] {label} {ability.abilityName}: cost={cost}, hp before={currentHealth}");
        PayAbilityHpCostAmount(ability, cost);
    }

    /// <summary>
    /// 实际扣除代价：进入 payingAbilityCost 上下文后走标准 TakeDamage，
    /// 使代价致死能被识别并转为"延迟死亡"。
    /// </summary>
    private void PayAbilityHpCostAmount(EnemyAbility ability, float cost)
    {
        // 濒死宽限期内耐久已为 0：不再重复扣血、不再重置宽限窗口。
        if (IsAbilityCostDeathPending) return;

        bool previousPaying = payingAbilityCost;
        EnemyAbility previousSource = abilityCostPaymentSource;
        payingAbilityCost = true;
        abilityCostPaymentSource = ability;
        try
        {
            TakeDamage(cost, allowGreedGuardAbsorb: false);
        }
        finally
        {
            payingAbilityCost = previousPaying;
            abilityCostPaymentSource = previousSource;
        }
        if (AbilityHpCostPaid != null) AbilityHpCostPaid(this, cost);
    }

    /// <summary>
    /// AI 攻击前面向索敌目标：把 transform.forward 转正到玩家方向（仅水平面）。
    /// 用于修正 AI 追击走位（侧移/对峙 ±90°）导致的攻击方向偏移。
    /// </summary>
    void FaceAttackTarget(){
        Transform target = CharmController.IsCharmedMonster(this) && targetEnemy != null
            ? targetEnemy.transform
            : targetPlayer;
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
        if (PossessionImprintManager.Instance != null)
            cost *= PossessionImprintManager.Instance.GetPossessionDrainMultiplier(this);
        if (cost > 0f)
        {
            Debug.Log($"[HpCost] Continuous {a.abilityName}: cost={cost}, hp before={currentHealth}");
            PayAbilityHpCostAmount(a, cost);
        }
    }

    /// <summary>
    /// 进入"代价致死待结算"状态：耐久已归零（血条显示 0），但 Body 保持可结算，
    /// 让触发这次代价的技能把伤害判定跑完后再死亡。
    /// </summary>
    private void BeginAbilityCostDeath(EnemyAbility source)
    {
        if (IsAbilityCostDeathPending) return;
        IsAbilityCostDeathPending = true;
        abilityCostDeathSource = source;
        Debug.Log($"[HpCost] Durability spent by '{(source != null ? source.abilityName : "ability")}'. Death deferred until its judgement finishes.");
        if (abilityCostDeathRoutine != null) StopCoroutine(abilityCostDeathRoutine);
        abilityCostDeathRoutine = StartCoroutine(AbilityCostDeathRoutine());
    }

    /// <summary>
    /// 宽限窗口：先等固定宽限秒数覆盖一次性 / 延迟命中类技能（如砸地 firstHitDelay），
    /// 再等充能 / 持续类技能（IsActivationInProgress）本次释放结束，最后结算死亡。
    /// hold 上限防止一直按住不放无限续命。
    /// </summary>
    private System.Collections.IEnumerator AbilityCostDeathRoutine()
    {
        float grace = Mathf.Max(0f, abilityCostDeathGrace);
        float elapsed = 0f;
        while (elapsed < grace)
        {
            if (!isPossessed || Body == BodyState.Fading || Body == BodyState.Despawned) break;
            elapsed += IsPlayerControlled ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        float held = 0f;
        float holdCap = Mathf.Max(0f, abilityCostDeathHoldCap);
        while (isPossessed
               && Body != BodyState.Fading
               && Body != BodyState.Despawned
               && abilityCostDeathSource != null
               && abilityCostDeathSource.IsActivationInProgress
               && held < holdCap)
        {
            held += IsPlayerControlled ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        abilityCostDeathRoutine = null;
        abilityCostDeathSource = null;
        if (!isPossessed || Body == BodyState.Fading || Body == BodyState.Despawned)
        {
            IsAbilityCostDeathPending = false;
            yield break;
        }

        currentHealth = 0f;
        UpdateHealthUI();
        // 先清标记再判死：NotifyBodyDied 可能同步回收 Body（协程随之中止），
        // 标记必须在那之前落到 false，避免残留到下一次复用。
        IsAbilityCostDeathPending = false;
        PossessionManager pm = PossessionManager.Instance;
        if (pm != null && pm.CurrentBody == this) pm.NotifyBodyDied();
    }

    /// <summary>清理代价致死待结算状态（脱离附身 / 池回收 / 复用时调用）。</summary>
    private void ClearAbilityCostDeathState()
    {
        if (abilityCostDeathRoutine != null)
        {
            StopCoroutine(abilityCostDeathRoutine);
            abilityCostDeathRoutine = null;
        }
        abilityCostDeathSource = null;
        IsAbilityCostDeathPending = false;
        payingAbilityCost = false;
        abilityCostPaymentSource = null;
    }

    void LateUpdate(){
        UpdateDebugRanges();
        // Animator speed always updates (even when downed/possessed)
        UpdateAnimatorSpeed();

        if (healthCanvas != null)
        {
            RefreshHealthBarLayout();
            bool shouldShow = showHealthBar && ShowHealthBars && !isPossessed;
            if (healthCanvas.gameObject.activeSelf != shouldShow) healthCanvas.gameObject.SetActive(shouldShow);

            // Always face the camera (billboard)
            if (healthCanvas.gameObject.activeSelf)
            {
                Camera camera = GetBillboardCamera();
                if (camera != null)
                    healthCanvas.transform.LookAt(healthCanvas.transform.position + camera.transform.forward, camera.transform.up);
            }
        }
    }

    /// <summary>Settles damage from the Sevenfold boss against its reserved possessed body.</summary>
    public void TakeBossDamage(float amount)
    {
        if (!IsBossBattleReserveBody)
        {
            TakeDamage(amount);
            return;
        }

        bossDamageContext = true;
        try
        {
            TakeDamage(amount);
        }
        finally
        {
            bossDamageContext = false;
        }
    }

    public virtual void TakeDamage(float amount)
    {
        TakeDamage(amount, allowGreedGuardAbsorb: true);
    }

    /// <summary>Damage dealt directly by the player's Soul form.</summary>
    public void TakePlayerDamage(float amount)
    {
        bool previous = playerDamageContext;
        playerDamageContext = true;
        try
        {
            TakeDamage(amount);
        }
        finally
        {
            playerDamageContext = previous;
        }
    }

    public virtual void TakeDamage(float amount, bool allowGreedGuardAbsorb)
    {
        bool tracingBoss = this is BossSevenfoldActor;
        float incomingAmount = amount;
        if (tracingBoss)
            Debug.Log($"[BossDamage] Base settlement received: incoming={incomingAmount:F2}, hp={currentHealth:F1}/{maxHealth:F1}, downed={isDowned}, body={Body}, possessed={isPossessed}, reserve={IsBossBattleReserveBody}, immune={IsDamageImmune(this)}, untargetable={IsUntargetable(this)}", this);

        if (IsBossBattleReserveBody && !bossDamageContext)
        {
            if (tracingBoss) Debug.LogWarning("[BossDamage] Blocked in base settlement: reason=BossReserveBodyOutsideBossContext", this);
            return;
        }
        if (IsCorpse)
        {
            if (tracingBoss) Debug.LogWarning($"[BossDamage] Blocked in base settlement: reason=InvalidBodyState, downed={isDowned}, body={Body}", this);
            return;
        }
        if (IsUntargetable(this) || IsDamageImmune(this))
        {
            if (tracingBoss) Debug.LogWarning($"[BossDamage] Blocked in base settlement: reason=DefenseState, immune={IsDamageImmune(this)}, untargetable={IsUntargetable(this)}", this);
            return;
        }
        if (Combat != null) amount = Combat.ModifyIncomingDamage(amount);
        if (amount <= 0f)
        {
            if (tracingBoss) Debug.LogWarning($"[BossDamage] Blocked in base settlement: reason=NonPositiveAfterIncomingModifier, incoming={incomingAmount:F2}, modified={amount:F2}", this);
            return;
        }
        if (allowGreedGuardAbsorb && TryGreedGuardAbsorb(amount, environmental: false))
        {
            if (tracingBoss) Debug.LogWarning($"[BossDamage] Blocked in base settlement: reason=GreedGuard, incoming={incomingAmount:F2}, modified={amount:F2}", this);
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
                // 技能烧血把耐久扣到 0：先让该次技能完整释放并跑完伤害判定，再结算死亡。
                if (payingAbilityCost)
                {
                    BeginAbilityCostDeath(abilityCostPaymentSource);
                    return;
                }
                PossessionManager pm = PossessionManager.Instance;
                if (pm != null && pm.CurrentBody == this) pm.NotifyBodyDied();
            }
            return;
        }

        currentHealth -= amount;
        currentTenacity -= amount;
        FlashDamage();
        UpdateHealthUI();
        if (tracingBoss)
            Debug.Log($"[BossDamage] Applied: incoming={incomingAmount:F2}, modified={amount:F2}, hp={currentHealth:F1}/{maxHealth:F1}, tenacity={currentTenacity:F1}/{maxTenacity:F1}", this);
        EnvyMarkTarget.NotifyDamageTaken(this as Enemy, amount);
        if (currentTenacity <= 0)
        {
            currentTenacity = 0;
            BecomeWeakened();
        }
        if (currentHealth <= 0)
        {
            currentHealth = 0;
            wasKilledByPlayer = playerDamageContext || (allowGreedGuardAbsorb
                && lastDamageSource != null && lastDamageSource.isPossessed);
            if (wasKilledByPlayer) OnMonsterKilled?.Invoke(this);
            Die();
        }
    }

    /// <summary>
    /// Environmental / tile hazard damage. Corpse bodies are fully immune.
    /// </summary>
    public void TakeEnvironmentalDamage(float amount)
    {
        if (IsCorpse) return;
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

        TakeDamage(amount, allowGreedGuardAbsorb: false);
    }

    private bool TryGreedGuardAbsorb(float amount, bool environmental)
    {
        EnemyAbility_GreedGuard guard = GetComponentInChildren<EnemyAbility_GreedGuard>(true);
        return guard != null && guard.TryAbsorb(amount, environmental, out _);
    }

    public void OnDealtDamage(float amount)
    {
        if (amount <= 0f) return;
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

        if (isPossessed && PossessionImprintManager.Instance != null)
            PossessionImprintManager.Instance.ApplyLustLifesteal(this, amount);
    }

    /// <summary>
    /// Returns whether this monster may damage the specified monster under the current faction rule.
    /// AI monsters share one faction; a possessed monster belongs to the player faction.
    /// </summary>
    public bool CanDamage(MonsterActor target)
    {
        bool selfPlayerFaction = isPossessed || CharmController.IsCharmedMonster(this);
        bool targetPlayerFaction = target != null && (target.isPossessed || CharmController.IsCharmedMonster(target));
        return target != null && target != this && !target.IsCorpse &&
               selfPlayerFaction != targetPlayerFaction &&
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
        if (isPossessed || CharmController.IsCharmedMonster(this)
            || Body == BodyState.Fading || Body == BodyState.Despawned) return false;
        // Soul is invulnerable while a possession body is active (even if a leftover collider overlaps).
        var pm = PossessionManager.Instance;
        if (pm != null && pm.State == PossessionManager.SwitchState.Possessing) return false;
        return true;
    }

    public void ApplyOffensiveDamage(MonsterActor target, float amount)
    {
        bool targetsBoss = target is BossSevenfoldActor;
        float authoredAmount = amount;
        if (!CanDamage(target) || amount <= 0f)
        {
            if (targetsBoss)
                Debug.LogWarning($"[BossDamage] Attack rejected before settlement: source={name}({GetType().Name}), authored={authoredAmount:F2}, canDamage={CanDamage(target)}, sourcePossessed={isPossessed}, targetDowned={target.isDowned}, targetBody={target.Body}, targetUntargetable={IsUntargetable(target)}", this);
            return;
        }
        // Lust LU-S06: pulled sources cannot damage the player's Possessed Body during pull + grace.
        if (LustPullDamageGate.ShouldBlock(this, target))
        {
            if (targetsBoss) Debug.LogWarning($"[BossDamage] Attack rejected before settlement: reason=LustPullDamageGate, source={name}, authored={authoredAmount:F2}", this);
            return;
        }
        if (Combat != null) amount = Combat.ModifyOutgoingDamage(amount);
        float afterOutgoingModifier = amount;
        if (isPossessed && PossessionImprintManager.Instance != null)
            amount *= PossessionImprintManager.Instance.GetOutgoingDamageMultiplier(this);
        else if (!isPossessed)
            amount *= Mathf.Max(0f, spawnDamageMultiplier);
        if (targetsBoss)
            Debug.Log($"[BossDamage] Attack accepted: source={name}({GetType().Name}), authored={authoredAmount:F2}, afterOutgoing={afterOutgoingModifier:F2}, final={amount:F2}, sourcePossessed={isPossessed}, targetHp={target.currentHealth:F1}/{target.maxHealth:F1}", this);
        target.lastDamageSource = this;
        float healthBefore = target.currentHealth;
        if (target.IsBossBattleReserveBody && target.isPossessed)
        {
            if (!(this is BossSevenfoldActor)) return;
            target.TakeBossDamage(amount);
        }
        else
        {
            target.TakeDamage(amount);
        }
        float actualDamage = Mathf.Clamp(healthBefore - target.currentHealth, 0f, amount);
        if (targetsBoss)
            Debug.Log($"[BossDamage] Attack result: source={name}, target={target.name}, final={amount:F2}, actual={actualDamage:F2}, hp={healthBefore:F1}->{target.currentHealth:F1}/{target.maxHealth:F1}", this);
        OnDealtDamage(actualDamage);

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
        if (activeAbilityTelegraph != null)
            activeAbilityTelegraph.CancelEnemyTelegraph();
        float reservedHealth = currentHealth;
        float reservedMaxHealth = maxHealth;
        float reservedHealthRatio = reservedMaxHealth > 0f
            ? Mathf.Clamp01(reservedHealth / reservedMaxHealth)
            : 1f;
        bool preserveEliteHealth = IsElite && reservedHealth > 0f;
        if (corpseRoutine != null)
        {
            StopCoroutine(corpseRoutine);
            corpseRoutine = null;
        }

        isPossessed = true;
        isDowned = false;
        isWeakened = false;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        // 恢复能力组件：SpawnAsPermanentCorpse（开场载体/刷尸体）与 BeginDisappearing 会禁用 EnemyAbility 组件，

        // 若附身时不恢复，EnemyAbility.Update 不执行 → currentCooldown 不递减 → 攻击一次后永久卡 CD 无法再攻击。
        SetAbilityComponentsEnabled(true);
        // Boss reserve bodies are disabled while waiting as corpses. Their ability
        // components can rebuild tags/slots during OnEnable, so synchronize the
        // Boss-mode run build after re-enabling them and before player input starts.
        if (RunSession.Instance != null && RunSession.Instance.IsBossMode && CardManager.Instance != null)
            CardManager.Instance.ApplyAllUnlocksTo(gameObject);
        Body = BodyState.Active;
        gameObject.tag = "Player";
        SetPossessedAnimatorsUnscaled(true);
        isPossessionReserved = false;
        EnsureDualStatsMigrated();
        ApplyStatBlock(possessedStats.HasConfiguredHealth ? possessedStats : enemyStats,
            refillVitals: !IsBossBattleReserveBody && !preserveEliteHealth);
        if (IsBossBattleReserveBody)
        {
            // ApplyStatBlock may temporarily clamp to the authored base max. Restore the
            // reserve's absolute HP before the global imprint multiplier is applied.
            currentHealth = Mathf.Max(1f, reservedHealth);
            currentTenacity = maxTenacity;
        }
        else if (preserveEliteHealth)
        {
            // A voluntarily detached Elite keeps its current/max ratio when possessed
            // again, even if the possessed stat block has a different absolute max HP.
            currentHealth = maxHealth * reservedHealthRatio;
        }
        authoredPossessedMaxHealth = maxHealth;
        if (PossessionImprintManager.Instance != null)
            PossessionImprintManager.Instance.ApplyBodyEffects(this);
        SetRendererFade(1f);
        if (visualFx != null)
        {
            visualFx.SetDissolve(1f);
            visualFx.SetCorpseHighlight(false);
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

        Transform activeRoot = transform.root != null ? transform.root : transform;
        activeRoot.position = new Vector3(activeRoot.position.x, aliveY, activeRoot.position.z);

        if (healthCanvas != null) healthCanvas.gameObject.SetActive(false);
        UpdateHealthUI();

        Animator animator = GetActiveAnimator();
        if (animator != null) animator.SetBool("IsDowned", false);
        EnvyMarkTarget.ClearMarksFromSource(this as Enemy);
    }

    public void OnUnpossessed(){
        isPossessed = false;
        gameObject.tag = "Enemy";
        ClearAbilityCostDeathState();
        // 清空 stale 指令与惯性：脱离后 IsPlayerControlled 变 false，基类 FixedUpdate 会开始
        // 消费 pendingCmd 驱动 AI 移动分支；若不清理，玩家最后一帧的移动指令会让身体继续滑行。
        pendingCmd = new ControlCommand();
        possessVelocity = Vector3.zero;
        aiVelocity = Vector3.zero;
        aiCurrentTurnSpeed = 0f;
        SetPossessedAnimatorsUnscaled(false);
        if (visualFx != null)
        {
            visualFx.SetPossessionHighlight(false);
            visualFx.SetCorpseHighlight(false);
        }
        // Cheat immortality must not linger on bodies left as normal enemies.
        ClearCheatDefenseEffects();
        EnvyMarkTarget.ClearMarksFromSource(this as Enemy);
        ResetPossessionVisualScale();
    }

    /// <summary>
    /// Gluttony and several other monsters keep their visible model Animator on child
    /// objects. Switching only the root Animator leaves those child models slowed by
    /// bullet time, so possession changes every Animator in the body hierarchy together.
    /// </summary>
    private void SetPossessedAnimatorsUnscaled(bool possessed)
    {
        if (possessed)
        {
            possessedAnimatorUpdateModes.Clear();
            Animator[] animators = GetCachedAnimators();
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null) continue;
                possessedAnimatorUpdateModes[animator] = animator.updateMode;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            return;
        }

        foreach (KeyValuePair<Animator, AnimatorUpdateMode> pair in possessedAnimatorUpdateModes)
        {
            if (pair.Key != null) pair.Key.updateMode = pair.Value;
        }
        possessedAnimatorUpdateModes.Clear();
    }

    private void EnterHitState(){
        if (isDowned || Body == BodyState.Fading || Body == BodyState.Despawned) return;
        Body = BodyState.Hit;
        hitStateEndsAt = Time.time + hitStateDuration;
    }

    protected virtual void Die(){
        if (!isPossessable)
        {
            isDowned = true;
            Body = BodyState.Downed;
            Transform nonPossessableRoot = transform.root != null ? transform.root : transform;
            nonPossessableRoot.position = new Vector3(nonPossessableRoot.position.x, corpseY, nonPossessableRoot.position.z);
            BeginDisappearing();
            return;
        }
        isDowned = true;
        isPossessed = false;
        Body = BodyState.Downed;
        CancelAbilityRuntimeState();
        SetAbilityComponentsEnabled(false);
        Combat?.ClearEffectsForCorpse();
        possessionWindowEndsAt = Time.time + corpsePossessionWindow;
        isPossessionReserved = false;
        transform.rotation = Quaternion.Euler(90f, transform.rotation.eulerAngles.y, 0f);
        Transform corpseRoot = transform.root != null ? transform.root : transform;
        corpseRoot.position = new Vector3(corpseRoot.position.x, corpseY, corpseRoot.position.z);
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            if (IsUnderForeignSoul(collider.transform)) continue;
            collider.enabled = true;
        }
        EnableCorpsePossessionCollider();
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = true;
        // Preserve the authored look in runtime FX material instances; dissolve FX handles fading later.
        if (visualFx != null) visualFx.SetCorpseHighlight(true);
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(true);
        UpdateHealthUI();

        Animator animator = GetActiveAnimator();
        if (animator != null) animator.SetBool("IsDowned", true);

        if (corpseRoutine != null) StopCoroutine(corpseRoutine);
        corpseRoutine = StartCoroutine(CorpseLifecycleRoutine());
        Debug.Log($"[MonsterState] '{displayName}' downed. Possess window={corpsePossessionWindow:F1}s.");
    }

    /// <summary>
    /// 教学保护：延长尸体附身窗口（教学 TUT-04 用——玩家读提示/走位时不因 5s 窗口消散）。
    /// 仅 Downed 态生效；永久尸体（窗口无限）无需延长；不影响后续生命周期。
    /// </summary>
    public void ExtendPossessionWindow(float extraSeconds)
    {
        if (Body != BodyState.Downed || extraSeconds <= 0f) return;
        if (float.IsPositiveInfinity(possessionWindowEndsAt)) return;
        possessionWindowEndsAt = Mathf.Max(possessionWindowEndsAt, Time.time + extraSeconds);
    }

    /// <summary>
    /// 以"附身等待尸体"状态出场（供 PossessionBodyProvider 等直接刷尸体的场景）：
    /// 直接进入 Downed 尸体态，血量清零、AI 休眠、可被附身；
    /// 尸体**永不自动消散**（附身窗口无限），只有被附身后按正常流程消散（附身死亡/退出时）。
    /// </summary>
    public void SpawnAsPermanentCorpse()
    {
        if (Body == BodyState.Fading || Body == BodyState.Despawned) return;

        IsBossBattleReserveBody = false;
        currentHealth = 0f;
        isDowned = true;
        isPossessed = false;
        Body = BodyState.Downed;
        possessionWindowEndsAt = float.PositiveInfinity; // 永久等待附身，不自动消散
        isPossessionReserved = false;
        transform.rotation = Quaternion.Euler(90f, transform.rotation.eulerAngles.y, 0f);
        Transform corpseRoot = transform.root != null ? transform.root : transform;
        corpseRoot.position = new Vector3(corpseRoot.position.x, corpseY, corpseRoot.position.z);
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = true;
        EnableCorpsePossessionCollider();
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = true;
        if (visualFx != null) visualFx.SetCorpseHighlight(true);
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(true);
        UpdateHealthUI();

        Animator animator = GetActiveAnimator();
        if (animator != null) animator.SetBool("IsDowned", true);

        SetController(NullController.Instance); // 尸体 AI 完全休眠
        CancelAbilityRuntimeState();
        SetAbilityComponentsEnabled(false);
        Combat?.ClearEffectsForCorpse();

        if (corpseRoutine != null) StopCoroutine(corpseRoutine);
        corpseRoutine = StartCoroutine(CorpseLifecycleRoutine());
        Debug.Log($"[MonsterState] '{displayName}' spawned as permanent possession corpse.");
    }

    /// <summary>
    /// Boss-only reserve body: it is a permanent corpse while unused, but keeps a real
    /// possessed-body HP pool so Boss damage can consume the slot instead of time decay.
    /// </summary>
    public void SpawnAsBossBattleReserveCorpse()
    {
        if (Body == BodyState.Fading || Body == BodyState.Despawned) return;
        EnsureDualStatsMigrated();
        ApplyStatBlock(possessedStats.HasConfiguredHealth ? possessedStats : enemyStats, refillVitals: true);
        IsBossBattleReserveBody = true;
        SpawnAsPermanentCorpse();
        IsBossBattleReserveBody = true;
        currentHealth = maxHealth;
        UpdateHealthUI();
        BossReserveCorpseVisualFx.EnsureFor(this);
        Debug.Log($"[MonsterState] '{displayName}' registered as Boss battle reserve body ({sinType}).");
    }

    /// <summary>Returns a living Boss reserve body to its permanent corpse state without restoring HP.</summary>
    public void ReturnToBossBattleReserve()
    {
        if (!IsBossBattleReserveBody || currentHealth <= 0f) return;
        float preservedHealth = currentHealth;
        SpawnAsPermanentCorpse();
        IsBossBattleReserveBody = true;
        currentHealth = Mathf.Clamp(preservedHealth, 1f, maxHealth);
        UpdateHealthUI();
        BossReserveCorpseVisualFx.EnsureFor(this);
    }

    public virtual void BeginDisappearing(){
        if (Body == BodyState.Fading || Body == BodyState.Despawned) return;
        if (corpseRoutine != null) StopCoroutine(corpseRoutine);
        isPossessionReserved = false;
        if (visualFx != null)
        {
            visualFx.SetPossessionHighlight(false);
            visualFx.SetCorpseHighlight(false);
        }
        if (corpsePossessionCollider != null) corpsePossessionCollider.enabled = false;
        CancelAbilityRuntimeState();
        SetAbilityComponentsEnabled(false);

        BossReserveCorpseVisualFx reserveVisual = GetCachedReserveCorpseVisual();
        if (reserveVisual != null) reserveVisual.Deactivate();
        IsBossBattleReserveBody = false;
        isPossessed = false;
        isDowned = true;
        Body = BodyState.Fading;
        Combat?.ClearEffectsForCorpse();
        possessionWindowEndsAt = 0f;
        // 停下所有残留惯性与指令，尸体在淡出期间必须原地不动。
        pendingCmd = new ControlCommand();
        possessVelocity = Vector3.zero;
        aiVelocity = Vector3.zero;
        aiCurrentTurnSpeed = 0f;
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
        SetPossessedAnimatorsUnscaled(false);
        if (corpseRoutine != null)
        {
            StopCoroutine(corpseRoutine);
            corpseRoutine = null;
        }

        isWeakened = false;
        isDowned = false;
        isPossessed = false;
        ResetEliteRuntimeState();
        BossReserveCorpseVisualFx reserveVisual = GetCachedReserveCorpseVisual();
        if (reserveVisual != null) reserveVisual.Deactivate();
        IsBossBattleReserveBody = false;
        isPossessable = true;
        lastDamageSource = null;
        wasKilledByPlayer = false;
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
        bossDamageContext = false;
        playerDamageContext = false;
        ClearAbilityCostDeathState();
        IsAbilityFacingLocked = false;
        IsAbilityLocomotionLocked = false;
        activeAbilityTelegraph = null;
        locomotionLockBeforeTelegraph = false;
        SetAbilityComponentsEnabled(true);
        CancelAbilityRuntimeState();
        ApplyStatBlock(enemyStats, refillVitals: true);
        ResetPossessionVisualScale();
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
            visualFx.SetCorpseHighlight(false);
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
        SetController(GetCachedAiController());
        UpdateHealthUI();
        OnResetForSpawn();
    }

    /// <summary>Hook for specialized actors that need to reset additional runtime state when pooled.</summary>
    protected virtual void OnResetForSpawn() { }

    public void ResetForPool(){
        SetPossessedAnimatorsUnscaled(false);
        SetController(NullController.Instance);
        CancelAbilityRuntimeState();
        activeAbilityTelegraph = null;
        locomotionLockBeforeTelegraph = false;
        BossReserveCorpseVisualFx reserveVisual = GetCachedReserveCorpseVisual();
        if (reserveVisual != null) reserveVisual.Deactivate();
        IsBossBattleReserveBody = false;
        bossDamageContext = false;
        playerDamageContext = false;
        ClearAbilityCostDeathState();
        possessVelocity = Vector3.zero;
        aiVelocity = Vector3.zero;
        aiCurrentTurnSpeed = 0f;
        IsAbilityFacingLocked = false;
        IsAbilityLocomotionLocked = false;
        wasKilledByPlayer = false;
        ResetEliteRuntimeState();
        if (corpseRoutine != null)
        {
            StopCoroutine(corpseRoutine);
            corpseRoutine = null;
        }
    }

    /// <summary>Applies a spawn-time curve snapshot while preserving current health ratio.</summary>
    public void ApplySpawnDifficultySnapshot(SpawnOrigin origin,
        float healthMultiplier, float damageMultiplier)
    {
        float previousMaxHealth = maxHealth;
        float previousHealthRatio = previousMaxHealth > 0f
            ? Mathf.Clamp01(currentHealth / previousMaxHealth)
            : 1f;
        spawnOrigin = origin;
        baseSpawnMaxHealth = enemyStats.HasConfiguredHealth ? enemyStats.maxHealth : maxHealth;
        spawnHealthMultiplier = Mathf.Max(0.01f, healthMultiplier) * eliteHealthMultiplier;
        spawnDamageMultiplier = Mathf.Max(0.01f, damageMultiplier) * eliteAttackDamageMultiplier;
        maxHealth = baseSpawnMaxHealth * spawnHealthMultiplier;
        currentHealth = maxHealth * previousHealthRatio;
        currentTenacity = maxTenacity;
        if (!isPossessed && enemyStats.HasConfiguredHealth)
            collisionDamage = enemyStats.collisionDamage * spawnDamageMultiplier;
    }

    /// <summary>
    /// Applies the Elite multipliers on top of the current wave difficulty snapshot.
    /// Health is rescaled by the existing current/max ratio instead of being refilled.
    /// </summary>
    public void ApplyEliteRuntimeModifiers(float healthMultiplier, float attackDamageMultiplier, float visualScaleMultiplier)
    {
        EnsureDualStatsMigrated();
        float previousMaxHealth = maxHealth;
        float previousHealthRatio = previousMaxHealth > 0f
            ? Mathf.Clamp01(currentHealth / previousMaxHealth)
            : 1f;

        float baseHealthMultiplier = spawnHealthMultiplier / Mathf.Max(0.01f, eliteHealthMultiplier);
        float baseDamageMultiplier = spawnDamageMultiplier / Mathf.Max(0.01f, eliteAttackDamageMultiplier);
        eliteHealthMultiplier = Mathf.Max(0.01f, healthMultiplier);
        eliteAttackDamageMultiplier = Mathf.Max(0.01f, attackDamageMultiplier);
        eliteVisualScaleMultiplier = Mathf.Max(1f, visualScaleMultiplier);
        eliteRuntimeApplied = true;

        if (baseSpawnMaxHealth <= 0f)
            baseSpawnMaxHealth = enemyStats.HasConfiguredHealth ? enemyStats.maxHealth : maxHealth;
        spawnHealthMultiplier = baseHealthMultiplier * eliteHealthMultiplier;
        spawnDamageMultiplier = baseDamageMultiplier * eliteAttackDamageMultiplier;
        maxHealth = baseSpawnMaxHealth * spawnHealthMultiplier;
        currentHealth = maxHealth * previousHealthRatio;
        if (!isPossessed && enemyStats.HasConfiguredHealth)
            collisionDamage = enemyStats.collisionDamage * spawnDamageMultiplier;

        ApplyVisualScale();
        if (visualFx != null)
        {
            visualFx.ConfigureEliteStyle(sinType);
            visualFx.SetEliteHighlight(true);
        }
        UpdateHealthUI();
    }

    /// <summary>Called by the imprint manager after the possessed stat block is applied.</summary>
    public void ApplyPossessionImprintStats(float healthMultiplier)
    {
        if (!isPossessed || healthMultiplier <= 0f) return;
        float preservedHealth = currentHealth;
        float oldMax = Mathf.Max(0.0001f, maxHealth);
        float healthRatio = Mathf.Clamp01(currentHealth / oldMax);
        float baseMax = authoredPossessedMaxHealth > 0f ? authoredPossessedMaxHealth : oldMax;
        maxHealth = baseMax * healthMultiplier;
        currentHealth = IsBossBattleReserveBody
            ? Mathf.Clamp(preservedHealth, 1f, maxHealth)
            : maxHealth * healthRatio;
        currentTenacity = Mathf.Min(currentTenacity, maxTenacity);
        UpdateHealthUI();
    }

    public void ApplyPossessionVisualScale(float multiplier)
    {
        PossessionCombatScaleMultiplier = Mathf.Max(1f, multiplier);
        ApplyVisualScale();
    }

    public void ResetPossessionVisualScale()
    {
        PossessionCombatScaleMultiplier = 1f;
        ApplyVisualScale();
    }

    private void ResetEliteRuntimeState()
    {
        eliteHealthMultiplier = 1f;
        eliteAttackDamageMultiplier = 1f;
        eliteVisualScaleMultiplier = 1f;
        eliteRuntimeApplied = false;
        if (visualFx != null) visualFx.SetEliteHighlight(false);
        ApplyVisualScale();
    }

    private void ApplyVisualScale()
    {
        float multiplier = CombatScaleMultiplier;
        if (visualScaleRoot != null)
        {
            if (!authoredVisualScaleCaptured)
            {
                authoredVisualScale = visualScaleRoot.localScale;
                authoredVisualScaleCaptured = true;
            }
            visualScaleRoot.localScale = authoredVisualScale * multiplier;
            RefreshHealthBarLayout(force: true);
            return;
        }

        for (int i = 0; i < fallbackVisualScaleRoots.Count; i++)
        {
            Transform root = fallbackVisualScaleRoots[i];
            if (root != null) root.localScale = fallbackAuthoredVisualScales[i] * multiplier;
        }
        RefreshHealthBarLayout(force: true);
    }

    private void CaptureHealthBarLayout()
    {
        if (healthCanvas == null || healthBarLayoutCaptured) return;

        healthBarBaseWorldPosition = healthCanvas.transform.position;
        healthBarBaseWorldScale = healthCanvas.transform.lossyScale;
        healthBarBaseLocalPosition = healthCanvas.transform.localPosition;
        healthBarBaseLocalScale = healthCanvas.transform.localScale;
        if (TryGetBodyVisualBounds(out Bounds bounds))
        {
            healthBarBaseCenterOffset = healthBarBaseWorldPosition - bounds.center;
            healthBarBaseHeightOffset = healthBarBaseWorldPosition.y - bounds.max.y;
        }
        else
        {
            healthBarBaseCenterOffset = Vector3.zero;
            healthBarBaseHeightOffset = 0f;
        }
        healthBarLayoutCaptured = true;
    }

    private void RefreshHealthBarLayout(bool force = false)
    {
        if (healthCanvas == null) return;
        CaptureHealthBarLayout();
        if (!healthBarLayoutCaptured) return;

        float scale = CombatScaleMultiplier;
        if (!force && Mathf.Abs(lastHealthBarLayoutScale - scale) < 0.001f) return;

        if (scale <= 1.0001f)
        {
            // Pool reset moves the actor root after this method runs. Restore the
            // authored local layout so ordinary health bars continue following the
            // newly spawned actor instead of being pinned to the old world position.
            healthCanvas.transform.localPosition = healthBarBaseLocalPosition;
            healthCanvas.transform.localScale = healthBarBaseLocalScale;
            lastHealthBarLayoutScale = scale;
            return;
        }

        if (TryGetBodyVisualBounds(out Bounds bounds))
        {
            // Keep authored X/Z offsets, but always lift an enlarged bar above the
            // current animated/model bounds. Some historical prefabs authored the
            // canvas inside the body, so a negative source offset is not trusted.
            float minimumClearance = Mathf.Max(0.2f, Mathf.Abs(healthBarBaseHeightOffset));
            healthCanvas.transform.position = new Vector3(
                bounds.center.x + healthBarBaseCenterOffset.x,
                bounds.max.y + minimumClearance,
                bounds.center.z + healthBarBaseCenterOffset.z);
        }
        else
        {
            Vector3 authoredPosition = healthCanvas.transform.parent != null
                ? healthCanvas.transform.parent.TransformPoint(healthBarBaseLocalPosition)
                : healthBarBaseWorldPosition;
            healthCanvas.transform.position = authoredPosition + Vector3.up * Mathf.Max(0.2f, scale - 1f);
        }

        SetHealthBarWorldScale(healthBarBaseWorldScale * scale);
        lastHealthBarLayoutScale = scale;
    }

    private void SetHealthBarWorldScale(Vector3 worldScale)
    {
        Transform parent = healthCanvas.transform.parent;
        Vector3 parentScale = parent != null ? parent.lossyScale : Vector3.one;
        healthCanvas.transform.localScale = new Vector3(
            worldScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            worldScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
            worldScale.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
    }

    private bool TryGetBodyVisualBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        if (bodyRenderers == null || bodyRenderers.Length == 0)
            bodyRenderers = GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            Renderer renderer = bodyRenderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                || IsUnderForeignSoul(renderer.transform)
                || !IsEliteVisualRenderer(renderer)) continue;
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else bounds.Encapsulate(renderer.bounds);
        }
        return hasBounds;
    }

    private void CaptureFallbackVisualScaleRoots()
    {
        fallbackVisualScaleRoots.Clear();
        fallbackAuthoredVisualScales.Clear();
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsEliteVisualRenderer(renderer)) continue;

            Transform root = renderer.transform;
            while (root.parent != null && root.parent != transform)
                root = root.parent;
            if (root == transform || fallbackVisualScaleRoots.Contains(root)) continue;

            fallbackVisualScaleRoots.Add(root);
            fallbackAuthoredVisualScales.Add(root.localScale);
        }
    }

    private bool IsEliteVisualRenderer(Renderer renderer)
    {
        if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            return false;

        Transform current = renderer.transform;
        while (current != null)
        {
            if (current.GetComponent<SoulActor>() != null || current.GetComponent<EnemyAbility>() != null
                || current.GetComponent<Canvas>() != null || current.GetComponent<Light>() != null)
                return false;

            string objectName = current.name;
            if (objectName.IndexOf("VFX", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Trail", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Headfire", StringComparison.OrdinalIgnoreCase) >= 0
                || objectName.IndexOf("Health", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (current == transform) break;
            current = current.parent;
        }
        return true;
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
            if (ability != null && ShouldManageAbilityComponent(ability)) ability.ResetForOwnerReuse();
    }

    private void SetAbilityComponentsEnabled(bool enabled)
    {
        foreach (EnemyAbility ability in GetComponentsInChildren<EnemyAbility>(true))
            if (ability != null && ShouldManageAbilityComponent(ability)) ability.enabled = enabled;
    }

    /// <summary>
    /// Allows composite actors to keep EnemyAbility components embedded in visual source
    /// prefabs from being treated as live abilities of the host actor.
    /// </summary>
    protected virtual bool ShouldManageAbilityComponent(EnemyAbility ability)
    {
        return true;
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
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                || renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;

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

        // AI 配置引用防御：prefab 填了配置库却引用了不存在的 id → 静默落默认，行为异常难查。
        if (aiConfig != null && !string.IsNullOrEmpty(aiConfigId) && aiConfig.Get(aiConfigId) == null)
        {
            Debug.LogWarning($"[MonsterActor] aiConfigId '{aiConfigId}' 未命中 {aiConfig.name} 中的条目，将使用默认配置（行为可能不符预期）。", this);
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
    /// AI keeps Basic/Skill1 semantics. A possessed monster uses left-click Basic, right-click Skill,
    /// Space mobility, middle-click corpse switching, and F release. Bullet Time starts automatically after possession.
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

            if ((cmd.Pressed & CommandButtons.Skill1) != 0)
                TryTriggerAbilitiesOfType(EnemyAbility.AbilityType.Skill);
            if ((cmd.Pressed & CommandButtons.Mobility) != 0) PlayerTriggerMobility();
            return;
        }

        PossessionManager manager = PossessionManager.Instance;
        if ((cmd.Pressed & CommandButtons.Skill1) != 0)
        {
            if (manager == null)
                Debug.LogWarning("[PossessionInput] Ignored body-switch middle-click: PossessionManager is missing.");

            else if (PlayerController.Instance == null)
                Debug.LogWarning("[PossessionInput] Ignored body-switch middle-click: PlayerController is missing.");

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
