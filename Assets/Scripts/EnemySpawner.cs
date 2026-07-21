using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Spawns enemies from a prefab list with per-prefab count control.
/// </summary>
[Serializable]
public class EnemyPrefabEntry
{
    [Tooltip("Enemy prefab to spawn")]
    public GameObject prefab;
    [Tooltip("How many of this prefab can exist at once (0 = unlimited)")]
    public int maxCount;
    [Tooltip("Current alive count (read-only)")]
    [HideInInspector] public int currentCount;
}

public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    [Header("Enemy Prefabs")]
    [Tooltip("Each entry defines a prefab and how many can be alive at once.")]
    public List<EnemyPrefabEntry> enemyPrefabs = new List<EnemyPrefabEntry>();

    [Header("Spawn Settings")]
    public float spawnRadius = 8f;
    public float spawnInterval = 2f;
    public int maxEnemies = 20;
    public int enemiesPerWave = 3;
    [Tooltip("生成点周围这个半径内不能有任何已存在的敌人。")]
    public float spawnClearRadius = 2.5f;
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

        // Update per-prefab counts
        foreach (var entry in enemyPrefabs)
        {
            if (entry.prefab == null) continue;
            entry.currentCount = activeEnemies.FindAll(e => e != null && e.name.StartsWith(entry.prefab.name)).Count;
        }
    }

    void SpawnWave()
    {
        int count = Mathf.Min(enemiesPerWave + Mathf.FloorToInt(currentDifficulty * 0.5f), maxEnemies - activeEnemies.Count);
        for (int i = 0; i < count; i++) SpawnEnemy();
    }

    public GameObject SpawnEnemy()
    {
        // Collect valid entries that haven't reached their max count
        var available = new List<EnemyPrefabEntry>();
        foreach (var entry in enemyPrefabs)
        {
            if (entry.prefab == null) continue;
            if (entry.maxCount > 0 && entry.currentCount >= entry.maxCount) continue;
            available.Add(entry);
        }
        if (available.Count == 0) return null;

        var chosen = available[UnityEngine.Random.Range(0, available.Count)];

        // 找一个不重叠且不卡在静态碰撞器内部的生成点
        TryFindSpawnPoint(out Vector3 spawnPos);

        var go = Instantiate(chosen.prefab, spawnPos, Quaternion.identity);
        go.tag = "Enemy";
        var enemy = go.GetComponent<Enemy>();
        if (enemy != null && scaleHealthWithDifficulty)
        {
            enemy.currentHealth = enemy.maxHealth * currentDifficulty;
            enemy.currentTenacity = enemy.maxTenacity * currentDifficulty;
            enemy.UpdateHealthUI();
        }
        activeEnemies.Add(go);
        chosen.currentCount++;
        return go;
    }

    /// <summary>
    /// 在 spawnRadius 范围内找一个生成点，spawnClearRadius 内不能有已存在的敌人。
    /// </summary>
    bool TryFindSpawnPoint(out Vector3 result)
    {
        for (int attempt = 0; attempt < spawnMaxAttempts; attempt++)
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float r = UnityEngine.Random.Range(spawnClearRadius, spawnRadius);
            Vector3 candidate = transform.position + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

            // 检查 spawnClearRadius 内是否有已存在的敌人
            bool tooClose = false;
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                var e = activeEnemies[i];
                if (e == null) { activeEnemies.RemoveAt(i); continue; }
                if (Vector3.Distance(e.transform.position, candidate) < spawnClearRadius)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            result = candidate;
            return true;
        }
        // Fallback: pick the candidate that was farthest from any enemy
        float bestDist = -1f;
        Vector3 bestPos = transform.position + Vector3.right * spawnRadius;
        for (int fb = 0; fb < spawnMaxAttempts * 2; fb++)
        {
            float fa = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float fr = UnityEngine.Random.Range(spawnClearRadius, spawnRadius);
            Vector3 fc = transform.position + new Vector3(Mathf.Cos(fa) * fr, 0f, Mathf.Sin(fa) * fr);
            float minDistToEnemy = float.MaxValue;
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                var e = activeEnemies[i];
                if (e == null) { activeEnemies.RemoveAt(i); continue; }
                float d = Vector3.Distance(e.transform.position, fc);
                if (d < minDistToEnemy) minDistToEnemy = d;
            }
            if (minDistToEnemy > bestDist) { bestDist = minDistToEnemy; bestPos = fc; }
        }
        result = bestPos;
        Debug.LogWarning($"[EnemySpawner] TryFindSpawnPoint: all attempts failed. Best distance to enemy: {bestDist:F1}");
        return false;
    }

    void OnDrawGizmosSelected()
    {
        // Spawn radius
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        // Spawn clear radius (per enemy)
        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, spawnClearRadius);
    }
}
