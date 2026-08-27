#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// EnemyAiTest 场景专用：AI 攻击行为调试刷怪器。
/// 按数字键刷「普通敌人」（不附身），让 AI 自动索敌/追击/攻击玩家，
/// 用于观察 AI 的攻击行为、攻击范围、攻击 CD 是否符合预期。
///
/// 按键：
///   - 1~9：生成 catalog 中对应序号的普通敌人（不附身）
///   - 0  ：生成随机普通敌人
///   - F1 ：切换屏幕上提示面板
///
/// 与 MonsterPossessionCheat（附身测试）互补，互不修改：本脚本不参与附身逻辑，
/// 只 spawn 普通敌人并可选打印其 AI 配置摘要。
/// </summary>
public class EnemyAIAttackTestSpawner : MonoBehaviour
{
    [Header("Catalog")]
    [Tooltip("刷怪清单（复用 MonsterCheatCatalog 资产）。")]
    public MonsterCheatCatalog catalog;

    [Header("Spawn")]
    [Tooltip("刷怪位置偏移（相对玩家灵魂/身体，XZ 平面）。")]
    public Vector3 spawnOffset = new Vector3(3f, 0f, 0f);
    [Tooltip("刷怪时在 Console 打印该怪的 AI 配置摘要（索敌/普攻/技能范围/攻击迟疑度）。")]
    public bool logAIConfigOnSpawn = true;
    [Tooltip("是否显示屏幕提示面板。")]
    public bool showHint = true;
    [Tooltip("刷出的怪是否显示调试距离圆环（索敌/普攻/技能范围，Game 视图可见）。仅影响本测试刷怪器刷出的怪。")]
    public bool showDebugRanges = true;

    void Update()
    {
        // Debug 大门：正式流程禁用测试刷怪器（防出包后玩家数字键刷怪）
        if (GameManager.IsFormalFlow) return;
        // 玩家输入被 UI/对话框阻塞时不响应（与 MonsterPossessionCheat 一致）
        if (PlayerController.IsGameplayInputBlocked)
            return;

        if (Input.GetKeyDown(KeyCode.F1))
        {
            showHint = !showHint;
            return;
        }

        int number = ReadNumberKeyDown();
        if (number < 0) return;

        // Key 8 is reserved for the Sevenfold Boss summon in the shared combat scenes.
        if (number == 8) return;

        if (number == 0)
            SpawnRandomEnemy();
        else
            SpawnEnemyByNumber(number);
    }

    /// <summary>读取 0-9 数字键；无按键返回 -1。</summary>
    static int ReadNumberKeyDown()
    {
        for (int n = 0; n <= 9; n++)
        {
            if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + n))) return n;
        }
        return -1;
    }

    void SpawnEnemyByNumber(int number)
    {
        if (!TryGetEntry(number, out MonsterCheatCatalog.Entry entry)) return;

        Vector3 pos = ResolveSpawnPosition();
        GameObject go = MonsterPool.Instance.Spawn(entry.prefab, pos, Quaternion.identity);
        if (go == null)
        {
            Debug.LogWarning($"[EnemyAIAttackTest] spawn failed for '{entry.prefab.name}'.");
            return;
        }

        go.tag = "Enemy";
        MonsterActor monster = go.GetComponentInChildren<MonsterActor>(true);
        if (monster == null)
        {
            Debug.LogWarning($"[EnemyAIAttackTest] spawned '{go.name}' has no MonsterActor.");
            return;
        }

        // 调试距离圆环开关（Inspector 可配；force 机制不污染 AI 配置资产）
        monster.forceDebugRanges = showDebugRanges;

        if (CardManager.Instance != null) CardManager.Instance.ApplyAllUnlocksTo(go);

        string label = !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : monster.displayName;
        Debug.Log($"[EnemyAIAttackTest] spawned [{number}] {label} at {pos}");

        if (logAIConfigOnSpawn)
        {
            MonsterAIConfigEntry cfg = monster.AiConfig;
            Debug.Log($"[EnemyAIAttackTest]   AI '{monster.aiConfigId}': det={cfg.detectionRadius} basic={cfg.basicAttackRange} skill={cfg.skillAttackRange} min={cfg.aiMinRange} eagerness={cfg.attackEagerness} chase={cfg.chaseDuration}");
        }
    }

    void SpawnRandomEnemy()
    {
        if (catalog == null || catalog.monsters == null || catalog.monsters.Count == 0)
        {
            Debug.LogWarning("[EnemyAIAttackTest] no catalog assigned.");
            return;
        }

        int index = Random.Range(0, catalog.monsters.Count);
        SpawnEnemyByNumber(index + 1);
    }

    bool TryGetEntry(int number, out MonsterCheatCatalog.Entry entry)
    {
        entry = null;
        if (catalog == null || catalog.monsters == null || catalog.monsters.Count == 0)
        {
            Debug.LogWarning("[EnemyAIAttackTest] no catalog assigned.");
            return false;
        }

        int index = number - 1;
        if (index < 0 || index >= catalog.monsters.Count)
        {
            Debug.LogWarning($"[EnemyAIAttackTest] no monster mapped to key {number}.");
            return false;
        }

        entry = catalog.monsters[index];
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning($"[EnemyAIAttackTest] monster slot {number} has no prefab.");
            return false;
        }
        return true;
    }

    Vector3 ResolveSpawnPosition()
    {
        Vector3 origin = Vector3.zero;
        if (PlayerController.Instance != null && PlayerController.Instance.transform != null)
            origin = PlayerController.Instance.transform.position;
        Vector3 pos = origin + spawnOffset;
        return pos;
    }

    void OnGUI()
    {
        // 正式流程下不绘制调试提示面板
        if (GameManager.IsFormalFlow) return;
        if (!showHint) return;

        float width = 360f;
        float height = 120f + (catalog != null && catalog.monsters != null ? catalog.monsters.Count * 18f : 0f);
        GUI.Box(new Rect(10f, 10f, width, height), "Enemy AI Attack Test");

        int y = 32;
        GUI.Label(new Rect(18f, y, width - 24f, 18f), "0 random | 1-9 spawn enemy | F1 hide");
        y += 20;

        if (catalog != null && catalog.monsters != null)
        {
            for (int i = 0; i < catalog.monsters.Count && i < 9; i++)
            {
                MonsterCheatCatalog.Entry e = catalog.monsters[i];
                string name = e != null && e.prefab != null ? e.prefab.name : "(empty)";
                GUI.Label(new Rect(18f, y, width - 24f, 18f), $"  [{i + 1}] {name}");
                y += 18;
            }
        }
    }
}
#endif
