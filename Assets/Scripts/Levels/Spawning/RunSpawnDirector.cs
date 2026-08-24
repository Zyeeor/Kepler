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
    readonly List<MonsterActor> bossBattleReserveBodies = new List<MonsterActor>(7);
    static readonly SinType[] BossBattleReserveOrder =
    {
        SinType.Pride,
        SinType.Wrath,
        SinType.Gluttony,
        SinType.Greed,
        SinType.Envy,
        SinType.Lust,
        SinType.Sloth,
    };
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
    public int BossBattleReserveBodyCount
    {
        get
        {
            PruneBossBattleReserveBodies();
            return bossBattleReserveBodies.Count;
        }
    }

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

    /// <summary>
    /// Creates one permanent possession corpse for each sin when the boss takeover starts.
    /// These bodies intentionally bypass MonsterSpawner quota tracking: they are encounter
    /// slots, not ordinary wave enemies, and remain available for repeated switching.
    /// </summary>
    public int SpawnBossBattleReserveBodies()
    {
        PruneBossBattleReserveBodies();
        if (MonsterPool.Instance == null) return 0;

        int spawned = 0;
        Vector3 center = GetBossBattleReserveCenter();
        Vector3 forward = GetBossBattleReserveForward();
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        for (int i = 0; i < BossBattleReserveOrder.Length; i++)
        {
            SinType sin = BossBattleReserveOrder[i];
            if (FindBossBattleReserveBody(sin) != null) continue;

            GameObject prefab = FindNormalPrefabForSin(sin);
            if (prefab == null)
            {
                Debug.LogWarning("[RunSpawnDirector] Missing normal prefab for Boss reserve sin=" + sin + ".");
                continue;
            }

            // Keep every fixed reserve body visible and reachable: a compact 4+3 array
            // immediately beside the current player body, in canonical sin order.
            int row = i / 4;
            int column = i % 4;
            int columnsInRow = row == 0 ? 4 : 3;
            float horizontal = (column - (columnsInRow - 1) * 0.5f) * 3f;
            Vector3 position = center + forward * (3.5f + row * 3f) + right * horizontal;
            GameObject instance = MonsterPool.Instance.Spawn(prefab, position, Quaternion.LookRotation(-forward, Vector3.up));
            if (instance == null) continue;

            MonsterActor body = instance.GetComponentInChildren<MonsterActor>(true);
            if (body == null)
            {
                Debug.LogWarning("[RunSpawnDirector] Reserve prefab has no MonsterActor: " + prefab.name);
                instance.SetActive(false);
                continue;
            }

            body.ResolveSinIdentityFromHint(prefab.name);
            if (body.sinType != sin)
            {
                Debug.LogWarning("[RunSpawnDirector] Reserve prefab sin mismatch. expected=" + sin + ", actual=" + body.sinType + ", prefab=" + prefab.name);
                body.BeginDisappearing();
                continue;
            }

            body.SpawnAsBossBattleReserveCorpse();
            bossBattleReserveBodies.Add(body);
            spawned++;
        }

        Debug.Log("[RunSpawnDirector] Boss battle reserve bodies spawned=" + spawned + "/" + BossBattleReserveOrder.Length + ".");
        return spawned;
    }

    /// <summary>Removes reserve corpses after the boss encounter, keeping the active body intact until result flow.</summary>
    public void ClearBossBattleReserveBodies()
    {
        MonsterActor current = PossessionManager.Instance != null ? PossessionManager.Instance.CurrentBody : null;
        for (int i = bossBattleReserveBodies.Count - 1; i >= 0; i--)
        {
            MonsterActor body = bossBattleReserveBodies[i];
            if (body == null || body == current) continue;
            body.BeginDisappearing();
        }
        bossBattleReserveBodies.Clear();
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
        SpawnRequest request = new SpawnRequest(SpawnOrigin.KillEcho, CurrentTier, fatal.actor.transform.position, 0f,
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
        if (request.origin == SpawnOrigin.KillEcho)
        {
            if (!spawner.TryGetKillEchoSpawnPosition(request.avoidPosition, out position)) return false;
        }
        else if (position == default(Vector3) && !spawner.TryGetWaveSpawnPosition(out position)) return false;
        if (request.minDistanceFromAvoid > 0f && (position - request.avoidPosition).sqrMagnitude < request.minDistanceFromAvoid * request.minDistanceFromAvoid)
            return false;
        MonsterActor monster = spawner.SpawnWaveMonster(prefab, position, request.origin == SpawnOrigin.KillEcho);
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
        if (actor is BossSevenfoldActor boss)
        {
            // Debug key [8] is an explicit Boss battle entry, not another wave spawn.
            // Stop the ordinary wave loop and move the run into Final when that edge is legal.
            WaveManager waveManager = FindObjectOfType<WaveManager>();
            if (waveManager != null) waveManager.StopWaves();
            boss.BeginTakeover();
            RunSession session = RunSession.Instance;
            if (session != null && session.CurrentPhase == RunPhase.Waves)
                session.TransitionTo(RunPhase.Final);
        }
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
        bossBattleReserveBodies.Clear();
        ActiveCombatSeconds = Mathf.Max(0f, activeSeconds);
        spawnedBossCount = bossSpawned ? 1 : 0;
        BossDefeated = bossDefeated;
        nextPressureTime = Mathf.Ceil(ActiveCombatSeconds / pressureInterval) * pressureInterval;
    }

    Vector3 GetBossBattleReserveCenter()
    {
        if (PossessionManager.Instance != null && PossessionManager.Instance.CurrentBody != null)
            return PossessionManager.Instance.CurrentBody.transform.position;
        if (PlayerHealth.Instance != null)
            return PlayerHealth.Instance.transform.position;
        return Vector3.zero;
    }

    Vector3 GetBossBattleReserveForward()
    {
        Transform anchor = PossessionManager.Instance != null && PossessionManager.Instance.CurrentBody != null
            ? PossessionManager.Instance.CurrentBody.transform
            : (PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null);
        Vector3 forward = anchor != null ? anchor.forward : Vector3.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    MonsterActor FindBossBattleReserveBody(SinType sin)
    {
        for (int i = 0; i < bossBattleReserveBodies.Count; i++)
        {
            MonsterActor body = bossBattleReserveBodies[i];
            if (body != null && body.IsBossBattleReserveBody && body.sinType == sin && body.Body != MonsterActor.BodyState.Despawned)
                return body;
        }
        return null;
    }

    void PruneBossBattleReserveBodies()
    {
        for (int i = bossBattleReserveBodies.Count - 1; i >= 0; i--)
        {
            MonsterActor body = bossBattleReserveBodies[i];
            if (body == null || body.Body == MonsterActor.BodyState.Despawned || !body.IsBossBattleReserveBody)
                bossBattleReserveBodies.RemoveAt(i);
        }
    }

    GameObject FindNormalPrefabForSin(SinType sin)
    {
        for (int i = 0; i < normalPrefabs.Count; i++)
        {
            GameObject prefab = normalPrefabs[i];
            if (prefab == null) continue;
            MonsterActor actor = prefab.GetComponentInChildren<MonsterActor>(true);
            if ((actor != null && actor.sinType == sin) || PrefabNameMatchesSin(prefab.name, sin)) return prefab;
        }

        // The reserve is a fixed seven-body encounter rule, not a by-product of the
        // current wave table. The production Resources catalog is the stable fallback.
        EliteMonsterCatalog catalog = Resources.Load<EliteMonsterCatalog>("EliteMonsterCatalog");
        EliteMonsterCatalog.Entry entry = catalog != null ? catalog.Find(sin) : null;
        if (entry != null && entry.prefab != null) return entry.prefab;
        return null;
    }

    static bool PrefabNameMatchesSin(string prefabName, SinType sin)
    {
        string id = prefabName != null ? prefabName.ToLowerInvariant() : string.Empty;
        switch (sin)
        {
            case SinType.Pride: return id.Contains("pride") || id.Contains("傲慢");
            case SinType.Wrath: return id.Contains("wrath") || id.Contains("愤怒");
            case SinType.Gluttony: return id.Contains("gluttony") || id.Contains("暴食");
            case SinType.Greed: return id.Contains("greed") || id.Contains("贪婪");
            case SinType.Envy: return id.Contains("envy") || id.Contains("嫉妒");
            case SinType.Lust: return id.Contains("lust") || id.Contains("色欲");
            case SinType.Sloth: return id.Contains("sloth") || id.Contains("怠惰");
            default: return false;
        }
    }
}
