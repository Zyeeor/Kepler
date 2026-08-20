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

    [Header("状态（调试可重置）")]
    [Tooltip("已提供过（只一次）。调试时可手动取消勾选重置。")]
    public bool used;

    /// <summary>提示日志限频（配置缺失时）。</summary>
    float lastWarnTime = -999f;

    void Update()
    {
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

        used = true;
        Debug.Log($"[PossessionBodyProvider] {name} 提供躯体（尸体）{monster.name}（{mode}）@{pos.ToString("F1")}", this);

        if (autoPossess && PossessionManager.Instance != null)
            PossessionManager.Instance.DebugForcePossess(monster);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = used ? new Color(0.5f, 0.5f, 0.5f, 0.35f) : new Color(0.2f, 0.9f, 0.4f, 0.35f);
        Gizmos.DrawSphere(transform.position, triggerRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.TransformPoint(spawnOffset), 0.4f);
    }
}
