using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class EnemyPrefabEntry
{
    [Tooltip("Enemy prefab to spawn")]
    public GameObject prefab;
    [Tooltip("How many of this prefab to spawn.")]
    public int count;
}

public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    [Header("Enemy Prefabs")]
    public List<EnemyPrefabEntry> enemyPrefabs = new List<EnemyPrefabEntry>();

    [Header("Spawn Settings")]
    public float spawnRadius = 8f;
    [Tooltip("Spawn clear radius: no existing enemy within this radius of a spawn point.")]
    public float spawnClearRadius = 2.5f;
    public int spawnMaxAttempts = 12;

    private readonly List<GameObject> activeEnemies = new List<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogError("[EnemySpawner] No enemy prefabs assigned!");
            return;
        }
        SpawnAll();
    }

    void Update()
    {
        activeEnemies.RemoveAll(e => e == null);
    }

    void SpawnAll()
    {
        foreach (var entry in enemyPrefabs)
        {
            if (entry.prefab == null) continue;
            for (int i = 0; i < entry.count; i++)
                SpawnEnemy(entry.prefab);
        }
    }

    public GameObject SpawnEnemy(GameObject prefab)
    {
        TryFindSpawnPoint(out Vector3 spawnPos);

        var go = Instantiate(prefab, spawnPos, Quaternion.identity);
        go.tag = "Enemy";
        activeEnemies.Add(go);
        return go;
    }

    bool TryFindSpawnPoint(out Vector3 result)
    {
        for (int attempt = 0; attempt < spawnMaxAttempts; attempt++)
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float r = UnityEngine.Random.Range(spawnClearRadius, spawnRadius);
            Vector3 candidate = transform.position + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);

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

        // Fallback: pick farthest from any enemy
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
        return false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, spawnClearRadius);
    }
}
