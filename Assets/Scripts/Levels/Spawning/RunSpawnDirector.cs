using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Run-level orchestration above MonsterSpawner. It owns the active-combat clock, pressure
/// cadence and kill-echo window; MonsterSpawner remains the legal spawn/pool executor.
/// </summary>
public sealed class RunSpawnDirector : MonoBehaviour
{
    public static RunSpawnDirector Instance { get; private set; }

    [Header("Pressure")]
    public float pressureInterval = 8f;
    public int earlyTarget = 6;
    public int midTarget = 8;
    public int lateTarget = 11;
    public int earlyPerTick = 2;
    public int midPerTick = 3;
    public int latePerTick = 4;
    [Header("Kill Echo")]
    public int maxEchoesPerWindow = 4;
    public float echoWindowSeconds = 10f;
    public float echoMinDelay = 0.6f;
    public float echoMaxDelay = 1.1f;
    public float echoExpirySeconds = 2f;
    [Header("Boss")]
    public float bossCombatTime = 480f;
    public GameObject bossPrefab;
    public List<GameObject> normalPrefabs = new List<GameObject>();

    readonly List<float> successfulEchoTimes = new List<float>(8);
    readonly List<PendingEcho> pendingEchoes = new List<PendingEcho>(8);
    float nextPressureTime;
    int spawnedBossCount;

    struct PendingEcho
    {
        public SpawnRequest request;
        public GameObject prefab;
        public float readyAt;
    }

    public float ActiveCombatSeconds { get; private set; }
    public int CurrentTier => MonsterSpawnDifficulty.TierAt(ActiveCombatSeconds);
    public bool BossSpawned => spawnedBossCount > 0;
    public bool BossDefeated { get; private set; }
    public int EchoesInWindow => successfulEchoTimes.Count;

    public static RunSpawnDirector EnsureInstance()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("[RunSpawnDirector]");
        DontDestroyOnLoad(go);
        return go.AddComponent<RunSpawnDirector>();
    }

    public void SetNormalPrefabs(IEnumerable<GameObject> prefabs)
    {
        normalPrefabs.Clear();
        if (prefabs == null) return;
        foreach (GameObject prefab in prefabs)
            if (prefab != null && !normalPrefabs.Contains(prefab)) normalPrefabs.Add(prefab);
    }

    public void ConfigureBoss(GameObject prefab)
    {
        if (prefab != null) bossPrefab = prefab;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        nextPressureTime = pressureInterval;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!IsActiveCombat()) return;
        ActiveCombatSeconds += Time.unscaledDeltaTime;
        PruneEchoWindow(Time.unscaledTime);
        bool spawnedEchoThisFrame = false;
        while (!spawnedEchoThisFrame && pendingEchoes.Count > 0 && pendingEchoes[0].readyAt <= Time.unscaledTime)
        {
            PendingEcho echo = pendingEchoes[0];
            pendingEchoes.RemoveAt(0);
            if (Time.unscaledTime <= echo.request.expiryTime)
                spawnedEchoThisFrame = TrySpawn(echo.prefab, echo.request);
        }
        if (!BossSpawned && ActiveCombatSeconds >= bossCombatTime)
            DebugSpawnBossNow();
        if (!BossSpawned && ActiveCombatSeconds >= nextPressureTime)
        {
            nextPressureTime += pressureInterval;
            SpawnPressure();
        }
    }

    bool IsActiveCombat()
    {
        RunSession run = RunSession.Instance;
        if (run != null && run.CurrentPhase != RunPhase.Waves && run.CurrentPhase != RunPhase.Final) return false;
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver) return false;
        return Time.timeScale > 0.0001f;
    }

    void SpawnPressure()
    {
        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (spawner == null || normalPrefabs == null || normalPrefabs.Count == 0) return;
        int target = ActiveCombatSeconds < 120f ? earlyTarget : (ActiveCombatSeconds < 300f ? midTarget : lateTarget);
        int perTick = ActiveCombatSeconds < 120f ? earlyPerTick : (ActiveCombatSeconds < 300f ? midPerTick : latePerTick);
        int desired = Mathf.Min(perTick, Mathf.Max(0, target - spawner.TrackedMonsterCount));
        for (int i = 0; i < desired; i++)
        {
            if (!spawner.TryGetWaveSpawnPosition(out Vector3 position)) break;
            GameObject prefab = normalPrefabs[(i + CurrentTier) % normalPrefabs.Count];
            TrySpawn(prefab, new SpawnRequest(SpawnOrigin.PeriodicPressure, CurrentTier, Vector3.zero, 0f,
                Time.unscaledTime + 1f), position);
        }
    }

    public bool RecordFatal(MonsterFatalEvent fatal)
    {
        if (fatal.actor == null || fatal.cause != FatalCause.PlayerDamage || fatal.killer == null || !fatal.killer.isPossessed)
            return false;
        if (fatal.actor.isPossessed || fatal.actor is BossSevenfoldActor) return false;
        PruneEchoWindow(Time.unscaledTime);
        if (successfulEchoTimes.Count >= maxEchoesPerWindow) return false;
        if (normalPrefabs == null || normalPrefabs.Count == 0) return false;
        successfulEchoTimes.Add(Time.unscaledTime);
        float delay = Mathf.Lerp(echoMinDelay, echoMaxDelay, fatal.killer.AiRandomValue());
        SpawnRequest request = new SpawnRequest(SpawnOrigin.KillEcho, CurrentTier, fatal.actor.transform.position, 15f,
            Time.unscaledTime + echoExpirySeconds);
        PendingEcho pending = new PendingEcho { prefab = normalPrefabs[0], request = request, readyAt = Time.unscaledTime + delay };
        int insertAt = pendingEchoes.Count;
        while (insertAt > 0 && pendingEchoes[insertAt - 1].readyAt > pending.readyAt) insertAt--;
        pendingEchoes.Insert(insertAt, pending);
        return true;
    }

    void PruneEchoWindow(float now)
    {
        for (int i = successfulEchoTimes.Count - 1; i >= 0; i--)
            if (now - successfulEchoTimes[i] >= echoWindowSeconds) successfulEchoTimes.RemoveAt(i);
    }

    bool TrySpawn(GameObject prefab, SpawnRequest request, Vector3 explicitPosition = default(Vector3))
    {
        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (spawner == null || prefab == null || spawner.TrackedMonsterCount >= spawner.maxCombatMonsters) return false;
        Vector3 position = explicitPosition;
        if (position == default(Vector3) && !spawner.TryGetWaveSpawnPosition(out position)) return false;
        if (request.minDistanceFromAvoid > 0f && (position - request.avoidPosition).sqrMagnitude < request.minDistanceFromAvoid * request.minDistanceFromAvoid)
            return false;
        MonsterActor monster = spawner.SpawnWaveMonster(prefab, position);
        if (monster == null) return false;
        monster.ApplySpawnDifficultySnapshot(request.origin, request.difficultyTier);
        return true;
    }

    public bool DebugSpawnBossNow()
    {
        if (BossSpawned || bossPrefab == null || MonsterSpawner.Instance == null) return false;
        if (!MonsterSpawner.Instance.TryGetWaveSpawnPosition(out Vector3 position)) return false;
        MonsterActor actor = MonsterSpawner.Instance.SpawnWaveMonster(bossPrefab, position);
        if (actor == null) return false;
        actor.ApplySpawnDifficultySnapshot(SpawnOrigin.Boss, CurrentTier);
        if (actor is BossSevenfoldActor boss) boss.BeginTakeover();
        spawnedBossCount++;
        return true;
    }

    public int SpawnBossMinions(int count)
    {
        if (BossDefeated || count <= 0 || normalPrefabs == null || normalPrefabs.Count == 0) return 0;
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            MonsterSpawner spawner = MonsterSpawner.Instance;
            if (spawner == null || !spawner.TryGetWaveSpawnPosition(out Vector3 position)) break;
            GameObject prefab = normalPrefabs[(CurrentTier + i) % normalPrefabs.Count];
            if (TrySpawn(prefab, new SpawnRequest(SpawnOrigin.BossMinion, CurrentTier, Vector3.zero, 0f,
                Time.unscaledTime + 1f), position)) spawned++;
        }
        return spawned;
    }

    public void MarkBossDefeated()
    {
        BossDefeated = true;
    }

    public void RestoreRuntime(float activeSeconds, bool bossSpawned, bool bossDefeated)
    {
        ActiveCombatSeconds = Mathf.Max(0f, activeSeconds);
        spawnedBossCount = bossSpawned ? 1 : 0;
        BossDefeated = bossDefeated;
        nextPressureTime = Mathf.Ceil(ActiveCombatSeconds / pressureInterval) * pressureInterval;
    }
}
