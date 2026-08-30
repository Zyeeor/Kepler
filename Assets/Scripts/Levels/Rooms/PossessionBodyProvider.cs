using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 附身躯体供给点：玩家（灵魂状态）走近触发范围内时，刷出一具可附身的怪物躯体
/// （只能提供一次）。典型挂载：雕像等装饰物 prefab。
///
/// 触发条件：PlayerController 存在 + 未处于附身状态（PossessionManager.CurrentBody == null）
/// + 距离 <= triggerRadius。
///
/// 躯体来源两种模式：
///   Specific（指定）      → 固定用 bodyPrefab；
///   RandomFromCatalog（随机）→ 从 MonsterCheatCatalog（调试怪物列表，Assets/Configs/）中
///                               随机选一个 prefab 非空的怪物（每局/每次触发随机一次）。
///
/// 提供行为：在 spawnOffset 处刷出躯体（经 MonsterSpawner 配额与追踪），
/// autoPossess=true 时随即自动附身（DebugForcePossess：跳过飞行/倒地窗口，直接接管）。
/// 刷怪失败（配额满）时本次不算消耗，下次继续尝试。
/// </summary>
public class PossessionBodyProvider : MonoBehaviour
{
    static readonly HashSet<PossessionBodyProvider> activeProviders = new HashSet<PossessionBodyProvider>();

    /// <summary>当前场景中激活的神龛供给点，供引导 UI 等外部系统低分配查询。</summary>
    public static void CollectActiveProviders(List<PossessionBodyProvider> buffer)
    {
        if (buffer == null) return;
        buffer.Clear();
        foreach (var provider in activeProviders)
        {
            if (provider == null || !provider.isActiveAndEnabled || !provider.gameObject.activeInHierarchy) continue;
            buffer.Add(provider);
        }
    }

    /// <summary>躯体来源模式。</summary>
    public enum BodyMode
    {
        [Tooltip("固定使用 bodyPrefab 指定的怪物。")]
        Specific = 0,
        [Tooltip("从 MonsterCheatCatalog 中随机选一个怪物。")]
        RandomFromCatalog = 1,
    }

    [Header("触发")]
    [Tooltip("触发半径（米）：玩家灵魂进入此范围即提供躯体。")]
    [Min(0.1f)] public float triggerRadius = 5f;

    [Header("躯体来源")]
    [Tooltip("躯体来源模式：指定 or 从怪物列表随机。")]
    public BodyMode mode = BodyMode.Specific;
    [Tooltip("指定模式：供附身的怪物 prefab（须挂 MonsterActor）。")]
    public GameObject bodyPrefab;
    [Tooltip("随机模式：怪物列表（与 MonsterPossessionCheat 共用 Assets/Configs/MonsterCheatCatalog）。")]
    public MonsterCheatCatalog catalog;

    [Header("提供行为")]
    [Tooltip("躯体刷出位置偏移（雕像本地空间）。")]
    public Vector3 spawnOffset = new Vector3(2f, 0f, 2f);
    [Tooltip("刷出后立即自动附身（跳过飞行/倒地窗口直接接管）。关闭则刷出的怪由 AI 控制（会攻击玩家）。")]
    public bool autoPossess = true;

    [Header("音频")]
    [Tooltip("是否启用神龛接近/提供音效（close 首次进入触发圈、provide 提供躯体时）。")]
    public bool audioEnabled = true;
    [Tooltip("接近提示触发圈的半径（≤0 = 与 triggerRadius 相同）。")]
    public float audioProximityRadius = 3f;

    [Header("状态（调试可重置）")]
    [Tooltip("已提供过（只一次）。调试时可手动取消勾选重置。")]
    public bool used;
    /// <summary>本次运行中由该神龛生成的躯体；用于区分“已生成但玩家尚未使用”和“已被使用”。</summary>
    [System.NonSerialized] MonsterActor providedBody;
    /// <summary>生成的躯体是否已被玩家真正接管过；脱离后仍保持已使用。</summary>
    [System.NonSerialized] bool providedBodyConsumed;
    PossessionManager observedPossessionManager;

    /// <summary>
    /// 是否仍是神龛引导的有效目标：尚未生成躯体，或已生成的躯体仍在场且从未被玩家接管。
    /// HasValidSource 是必要条件，避免把配置失效的装饰物当作神龛目标。
    /// </summary>
    public bool IsValidForGuide
    {
        get
        {
            if (!HasValidSource()) return false;
            if (!used) return true;
            if (providedBody == null || providedBodyConsumed || providedBody.isPossessed) return false;
            if (!providedBody.gameObject.activeInHierarchy) return false;
            return providedBody.Body != MonsterActor.BodyState.Fading
                && providedBody.Body != MonsterActor.BodyState.Despawned;
        }
    }

    [System.NonSerialized] Vector3? cachedGuideAnchor;

    /// <summary>
    /// 引导线应指向的锚点：神龛可见几何（Renderer 包围盒）的底部中心（贴地）。
    /// 不用 root transform.position——TileAsset 类 prefab 的 root 常带较大 localPosition 偏移，
    /// 直接取 root 会让引导线指向雕像旁的空气。神龛为静态装饰，锚点计算一次后缓存。
    /// 无 Renderer 时回退 transform.position。
    /// </summary>
    public Vector3 GuideAnchorPosition
    {
        get
        {
            if (cachedGuideAnchor.HasValue) return cachedGuideAnchor.Value;
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                cachedGuideAnchor = transform.position;
                return cachedGuideAnchor.Value;
            }
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                b.Encapsulate(renderers[i].bounds);
            }
            cachedGuideAnchor = new Vector3(b.center.x, b.min.y, b.center.z);
            return cachedGuideAnchor.Value;
        }
    }

    /// <summary>接近提示音是否已播（每次进入触发圈只播一次，离开后重置）。</summary>
    bool proximitySfxPlayed;

    /// <summary>提示日志限频（配置缺失时）。</summary>
    float lastWarnTime = -999f;

    void OnEnable()
    {
        activeProviders.Add(this);
        TryBindPossessionManager();
    }

    void OnDisable()
    {
        activeProviders.Remove(this);
        UnbindPossessionManager();
    }

    void OnDestroy()
    {
        activeProviders.Remove(this);
        UnbindPossessionManager();
    }

    void TryBindPossessionManager()
    {
        PossessionManager manager = PossessionManager.Instance;
        if (observedPossessionManager == manager) return;
        UnbindPossessionManager();
        observedPossessionManager = manager;
        if (observedPossessionManager != null)
            observedPossessionManager.OnPossessionStarted += HandlePossessionStarted;
    }

    void UnbindPossessionManager()
    {
        if (observedPossessionManager != null)
            observedPossessionManager.OnPossessionStarted -= HandlePossessionStarted;
        observedPossessionManager = null;
    }

    void HandlePossessionStarted(MonsterActor body)
    {
        if (body != null && body == providedBody)
            providedBodyConsumed = true;
    }

    void Update()
    {
        TryBindPossessionManager();
        if (providedBody != null && providedBody.isPossessed)
            providedBodyConsumed = true;
        if (used) return;
        if (!HasValidSource())
        {
            if (Time.time - lastWarnTime > 5f)
            {
                Debug.LogWarning($"[PossessionBodyProvider] {name} 未配置躯体来源" +
                    (mode == BodyMode.Specific ? "（bodyPrefab 为空）" : "（catalog 为空/无有效怪物）") + "，组件不生效。", this);
                lastWarnTime = Time.time;
            }
            return;
        }

        var player = PlayerController.Instance;
        if (player == null) return;
        // 仅灵魂状态触发（附身怪/其他占用时跳过），避免附身怪路过误触发
        var pm = PossessionManager.Instance;
        if (pm != null && pm.CurrentBody != null) return;

        float dist = Vector3.Distance(player.transform.position, transform.position);

        // 神龛接近提示音：首次进入音频提示圈（默认 3m）时播一次；离开后重置可再次触发。
        if (audioEnabled && pm != null && pm.CurrentBody == null)
        {
            float proximityRadius = audioProximityRadius > 0f ? audioProximityRadius : triggerRadius;
            if (dist <= proximityRadius)
            {
                if (!proximitySfxPlayed)
                {
                    proximitySfxPlayed = true;
                    AudioManager.Instance?.Play(SfxId.ShrineProximity, transform.position);
                }
            }
            else
            {
                proximitySfxPlayed = false;
            }
        }

        if (dist <= triggerRadius)
            ProvideBody();
    }

    /// <summary>当前模式下是否有可用躯体来源。</summary>
    public bool HasValidSource()
    {
        if (mode == BodyMode.RandomFromCatalog)
        {
            if (catalog == null || catalog.monsters == null) return false;
            for (int i = 0; i < catalog.monsters.Count; i++)
            {
                var e = catalog.monsters[i];
                if (e != null && e.prefab != null) return true;
            }
            return false;
        }
        return bodyPrefab != null;
    }

    /// <summary>解析本次实际使用的怪物 prefab（随机模式每次调用重新随机）。</summary>
    GameObject ResolveBodyPrefab()
    {
        if (mode == BodyMode.RandomFromCatalog && catalog != null && catalog.monsters != null)
        {
            var valid = new System.Collections.Generic.List<MonsterCheatCatalog.Entry>();
            for (int i = 0; i < catalog.monsters.Count; i++)
            {
                var e = catalog.monsters[i];
                if (e != null && e.prefab != null) valid.Add(e);
            }
            if (valid.Count > 0)
            {
                // 种子确定性：用一次性的 DomainAI 流（salt=固定锚点坐标哈希，同种子下同雕像同结果）
                int salt = Mathf.RoundToInt(transform.position.x * 1000f) * 131
                         + Mathf.RoundToInt(transform.position.y * 1000f) * 17
                         + Mathf.RoundToInt(transform.position.z * 1000f);
                var rng = SeedSystem.CreateFlow(SeedSystem.DomainAI, salt);
                return valid[rng.Next(0, valid.Count)].prefab;
            }
        }
        return bodyPrefab;
    }

    /// <summary>提供躯体（只一次；刷怪失败回滚 used，下次重试）。</summary>
    public void ProvideBody()
    {
        if (used) return;

        // Pass v1 bug fix（§1.1/§1.3）：首次 Possess Pride（CombatStarted）之前，神龛不得提供随机躯体。
        // 否则出生点神龛（RandomFromCatalog）会在玩家灵魂落地后立即刷出非傲慢尸体，干扰「开局固定 Pride Corpse」。
        // 开场固定 Pride corpse 由 TutorialController.OpeningCarrierRoutine 单独负责；Boss 模式无此门。
        var run = RunSession.Instance;
        if (RunSpawnDirector.Instance != null && !RunSpawnDirector.Instance.CombatStarted
            && !(run != null && run.IsBossMode))
            return;

        var prefab = ResolveBodyPrefab();
        if (prefab == null) return;

        var spawner = MonsterSpawner.Instance;
        if (spawner == null) spawner = MonsterSpawner.EnsureInstance();

        Vector3 pos = transform.TransformPoint(spawnOffset);
        var monster = spawner.SpawnWaveMonster(prefab, pos);
        if (monster == null)
        {
            Debug.Log($"[PossessionBodyProvider] {name} 刷怪失败（配额满/prefab 无效），保留提供机会待重试。", this);
            return; // 不消耗
        }

        // 转尸体状态：永久倒地躯体（不自动消散），等待附身；附身后由附身流程管理消散
        monster.SpawnAsPermanentCorpse();

        providedBody = monster;
        providedBodyConsumed = false;
        used = true;
        // 提供躯体音（默认 3D 定位在神龛位置；未配置 clip 静默）
        if (audioEnabled)
            AudioManager.Instance?.Play(SfxId.ShrineProvide, transform.position);
        Debug.Log($"[PossessionBodyProvider] {name} 提供躯体（尸体）{monster.name}（{mode}）@{pos.ToString("F1")}", this);

        PossessionManager manager = PossessionManager.Instance;
        if (autoPossess && manager != null && manager.DebugForcePossess(monster))
            providedBodyConsumed = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = used ? new Color(0.5f, 0.5f, 0.5f, 0.35f) : new Color(0.2f, 0.9f, 0.4f, 0.35f);
        Gizmos.DrawSphere(transform.position, triggerRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.TransformPoint(spawnOffset), 0.4f);
    }
}
