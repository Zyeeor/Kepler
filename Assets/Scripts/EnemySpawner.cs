using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns enemies from a prefab list. Each prefab is self-contained (stats + abilities configured on the prefab).
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    [Header("Enemy Prefabs (drag enemy prefabs here)")]
    [Tooltip("List of enemy prefabs the spawner can pick from. Each prefab has its own stats/abilities configured in the Inspector.")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    public float spawnRadius = 8f;
    public float spawnInterval = 2f;
    public int maxEnemies = 20;
    public int enemiesPerWave = 3;
    [Tooltip("两个敌人之间最小间距。两个 Kinematic Rigidbody 之间 Unity 不互相物理阻挡(引擎硬性限制),所以靠 spawn 时强制避开来避免出生重叠。")]
    public float minSeparation = 2.5f;
    [Tooltip("生成避障:出生点周围这个半径内不能有任何静态碰撞器,避免敌人卡在墙/柱/掩体里。")]
    public float spawnClearRadius = 0.6f;
    [Tooltip("避障/spawn 重试次数。")]
    public int spawnMaxAttempts = 12;

    [Header("Difficulty Curve")]
    public float difficultyRampRate = 0.1f;
    [Tooltip("Multiply spawned enemy HP by currentDifficulty.")]
    public bool scaleHealthWithDifficulty = true;

    private float spawnTimer;
    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private float currentDifficulty = 1f;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 诊断日志：检查配置是否正确
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogError("[EnemySpawner] No enemy prefabs assigned! Open GameManager in Inspector and drag enemy prefabs into the EnemySpawner list.");
        }
        else
        {
            int validCount = enemyPrefabs.FindAll(p => p != null).Count;
            Debug.Log($"[EnemySpawner] Ready: {validCount}/{enemyPrefabs.Count} valid prefabs, radius={spawnRadius}, spawnerPos={transform.position}");
        }
        SpawnWave();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver) return;
        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0 && activeEnemies.Count < maxEnemies)
        {
            spawnTimer = Mathf.Max(spawnInterval - currentDifficulty * 0.1f, 0.5f);
            SpawnWave();
        }
        currentDifficulty = 1f + Time.time * difficultyRampRate;
        activeEnemies.RemoveAll(e => e == null);
    }

    void SpawnWave()
    {
        int count = Mathf.Min(enemiesPerWave + Mathf.FloorToInt(currentDifficulty * 0.5f), maxEnemies - activeEnemies.Count);
        for (int i = 0; i < count; i++) SpawnEnemy();
    }

    public GameObject SpawnEnemy()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogWarning("EnemySpawner: no enemy prefabs in list!");
            return null;
        }
        // Filter null entries
        var valid = enemyPrefabs.FindAll(p => p != null);
        if (valid.Count == 0) return null;

        var prefab = valid[Random.Range(0, valid.Count)];

        // 找一个不重叠且不卡在静态碰撞器内部的生成点
        // 即使所有尝试失败，仍然使用 spawner 位置（有 Debug.LogWarning 提示）
        TryFindSpawnPoint(out Vector3 spawnPos);

        var go = Instantiate(prefab, spawnPos, Quaternion.identity);
        go.tag = "Enemy";
        var enemy = go.GetComponent<Enemy>();
        if (enemy != null && scaleHealthWithDifficulty)
        {
            enemy.currentHealth = enemy.maxHealth * currentDifficulty;
            enemy.currentTenacity = enemy.maxTenacity * currentDifficulty;
            enemy.UpdateHealthUI();
        }
        activeEnemies.Add(go);
        return go;
    }

    /// <summary>
    /// 在 spawnRadius 范围内找一个合适的生成点:
    /// 1) 距离已生成敌人 ≥ minSeparation
    /// 2) spawnClearRadius 范围内无静态碰撞器(墙/柱/掩体)
    /// </summary>
    bool TryFindSpawnPoint(out Vector3 result)
    {
        // 障碍 mask:不包括敌人自身(Layer 8)、玩家(Layer 9),只关心环境(Default)
        int obstacleMask = ~((1 << 8) | (1 << 9));
        for (int attempt = 0; attempt < spawnMaxAttempts; attempt++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float r = Random.Range(0.3f, 1f) * spawnRadius; // 偏内圈,避免贴边
            Vector3 candidate = transform.position + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

            // 1) 避障:出生点周围不能有静态碰撞器
            if (Physics.CheckSphere(candidate, spawnClearRadius, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                continue; // 卡在墙里/柱子边/掩体里,跳过
            }

            // 2) 距离其他敌人 ≥ minSeparation
            bool tooClose = false;
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                var e = activeEnemies[i];
                if (e == null) { activeEnemies.RemoveAt(i); continue; }
                if (Vector3.Distance(e.transform.position, candidate) < minSeparation)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            // 找到合法位置
            result = candidate;
            return true;
        }
        Debug.LogWarning($"[EnemySpawner] TryFindSpawnPoint: all {spawnMaxAttempts} attempts failed (obstacle avoidance + separation). Falling back to spawner position.");
        result = transform.position;
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
