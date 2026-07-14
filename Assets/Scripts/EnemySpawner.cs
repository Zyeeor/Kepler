using UnityEngine;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    [Header("Spawn Settings")]
    public GameObject enemyPrefab;
    public EnemyConfig[] enemyConfigs;
    public float spawnRadius = 8f;
    public float spawnInterval = 2f;
    public int maxEnemies = 20;
    public int enemiesPerWave = 3;

    [Header("Difficulty Curve")]
    public float difficultyRampRate = 0.1f;

    private float spawnTimer;
    private List<GameObject> activeEnemies = new List<GameObject>();
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
        for (int i = 0; i < count; i++)
            SpawnEnemy();
    }

    void SpawnEnemy()
    {
        if (enemyPrefab == null) { Debug.LogWarning("Enemy prefab not assigned!"); return; }
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 spawnPos = transform.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * spawnRadius;
        var enemyGO = Object.Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        enemyGO.tag = "Enemy";
        var enemy = enemyGO.GetComponent<Enemy>();
        if (enemy != null)
        {
            // Assign random config from available configs
            if (enemyConfigs != null && enemyConfigs.Length > 0)
            {
                enemy.config = enemyConfigs[Random.Range(0, enemyConfigs.Length)];
                enemy.ApplyConfig();
                // Difficulty scaling on stats
                if (enemy.config != null)
                {
                    enemy.currentHealth = enemy.config.maxHealth * currentDifficulty;
                    enemy.currentTenacity = enemy.config.maxHealth * currentDifficulty;
                }
            }
        }
        activeEnemies.Add(enemyGO);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
