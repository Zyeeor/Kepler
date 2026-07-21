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
        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 spawnPos = transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * spawnRadius;
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
