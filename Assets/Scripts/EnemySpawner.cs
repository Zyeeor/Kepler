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
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 spawnPos = transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * spawnRadius;
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

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
