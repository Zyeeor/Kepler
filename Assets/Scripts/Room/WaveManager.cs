using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 管理房间内所有波次的生命周期。
/// </summary>
public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    /// <summary>当前正在运行的波次索引。</summary>
    public int CurrentWaveIndex { get; private set; } = -1;
    /// <summary>当前存活的敌人数量。</summary>
    public int EnemiesAlive { get; private set; }
    /// <summary>是否所有波次已完成。</summary>
    public bool AllWavesComplete { get; private set; }
    /// <summary>当前是否在战斗波次中。</summary>
    public bool IsWaveActive { get; private set; }

    /// <summary>事件：波次开始。</summary>
    public event Action<int, WaveConfig> OnWaveStarted;
    /// <summary>事件：波次完成（该波次所有敌人被消灭）。</summary>
    public event Action<int> OnWaveCompleted;
    /// <summary>事件：所有波次完成。</summary>
    public event Action OnAllWavesComplete;
    /// <summary>事件：一个敌人被消灭。</summary>
    public event Action<Enemy> OnEnemyKilled;

    private RoomTemplate currentTemplate;
    private RoomInstance currentRoom;
    private Coroutine waveRoutine;
    private readonly List<Enemy> spawnedEnemies = new List<Enemy>();
    private bool isRunning;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>初始化波次管理器。</summary>
    public void Initialize(RoomTemplate template, RoomInstance room)
    {
        currentTemplate = template;
        currentRoom = room;
        CurrentWaveIndex = -1;
        EnemiesAlive = 0;
        AllWavesComplete = false;
        isRunning = true;
        spawnedEnemies.Clear();
    }

    /// <summary>开始波次流程。</summary>
    public void StartWaves()
    {
        if (!isRunning || currentTemplate == null) return;
        waveRoutine = StartCoroutine(WaveRoutine());
    }

    /// <summary>停止波次。</summary>
    public void StopWaves()
    {
        isRunning = false;
        if (waveRoutine != null) StopCoroutine(waveRoutine);
        CleanupEnemies();
    }

    IEnumerator WaveRoutine()
    {
        // Grace period
        if (currentTemplate.gracePeriod > 0f)
            yield return new WaitForSeconds(currentTemplate.gracePeriod);

        for (int i = 0; i < currentTemplate.waves.Count; i++)
        {
            if (!isRunning) yield break;

            var wave = currentTemplate.waves[i];
            CurrentWaveIndex = i;
            if (currentRoom != null && currentRoom.context != null)
                currentRoom.context.CurrentWaveIndex = i;

            // 等待该波次的开始时间
            if (wave.startTime > 0f)
                yield return new WaitForSeconds(wave.startTime);

            IsWaveActive = true;
            OnWaveStarted?.Invoke(i, wave);

            // 生成该波次的所有敌人
            SpawnWaveEnemies(wave);

            // 等待该波次所有敌人被消灭（轮询 isDowned）
            while (EnemiesAlive > 0 && isRunning)
            {
                for (int j = spawnedEnemies.Count - 1; j >= 0; j--)
                {
                    var e = spawnedEnemies[j];
                    if (e == null || e.isDowned)
                    {
                        if (e != null) OnEnemyKilled?.Invoke(e);
                        spawnedEnemies.RemoveAt(j);
                        EnemiesAlive--;
                    }
                }
                yield return null;
            }

            IsWaveActive = false;
            OnWaveCompleted?.Invoke(i);
        }

        AllWavesComplete = true;
        if (currentRoom != null && currentRoom.context != null)
            currentRoom.context.State = RoomState.Cleared;
        OnAllWavesComplete?.Invoke();
    }

    void SpawnWaveEnemies(WaveConfig wave)
    {
        var spawnPoints = GetSpawnPoints(wave.spawnPointGroup);

        foreach (var entry in wave.enemies)
        {
            if (entry.enemyPrefab == null) continue;
            for (int j = 0; j < entry.count; j++)
            {
                StartCoroutine(SpawnEnemyDelayed(entry.enemyPrefab, entry.delay + j * 0.3f, spawnPoints));
            }
        }
    }

    IEnumerator SpawnEnemyDelayed(GameObject prefab, float delay, List<Vector3> points)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        Vector3 pos;
        if (points != null && points.Count > 0)
            pos = points[UnityEngine.Random.Range(0, points.Count)];
        else
            pos = GetRandomSpawnPos();

        var go = Instantiate(prefab, pos, Quaternion.identity);
        go.tag = "Enemy";
        var enemy = go.GetComponent<Enemy>();
        if (enemy != null)
        {
            spawnedEnemies.Add(enemy);
            EnemiesAlive++;
        }
    }

    List<Vector3> GetSpawnPoints(string groupName)
    {
        var result = new List<Vector3>();
        if (currentRoom == null) return result;

        if (!string.IsNullOrEmpty(groupName))
        {
            var group = currentRoom.GetSpawnPointGroup(groupName);
            if (group != null)
            {
                foreach (var p in group.points)
                    if (p != null) result.Add(p.position);
            }
        }

        if (result.Count == 0 && currentRoom.playerSpawnPoint != null)
        {
            // Default: around player spawn with offset
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                result.Add(currentRoom.playerSpawnPoint.position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 6f);
            }
        }

        return result;
    }

    Vector3 GetRandomSpawnPos()
    {
        if (currentTemplate != null)
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float r = UnityEngine.Random.Range(currentTemplate.spawnClearRadius, currentTemplate.spawnRadius);
            return new Vector3(Mathf.Cos(angle) * r, 0, Mathf.Sin(angle) * r);
        }
        return Vector3.zero;
    }

    void CleanupEnemies()
    {
        foreach (var e in spawnedEnemies)
        {
            if (e != null) Destroy(e.gameObject);
        }
        spawnedEnemies.Clear();
        EnemiesAlive = 0;
    }
}
