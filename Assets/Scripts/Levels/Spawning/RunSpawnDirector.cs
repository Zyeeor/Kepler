using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Run-level orchestration above MonsterSpawner. It owns the active-combat clock and Boss
/// takeover timing; WaveManager owns all ordinary and Elite spawn scheduling.
/// </summary>
public sealed class RunSpawnDirector : MonoBehaviour
{
    public static RunSpawnDirector Instance { get; private set; }

    [Header("Boss")]
    [Min(1f)] public float bossCombatTime = 420f;
    [Tooltip("仅供旧 Boss 仆从类型选择使用的成长档位间隔（秒）。")]
    [Min(0.1f)] public float difficultyGrowthIntervalSeconds = 30f;
    [Tooltip("横轴为战斗分钟数，纵轴为怪物基础生命值乘数。由 WaveManager 配置。")]
    public AnimationCurve monsterHealthMultiplierByMinute = AnimationCurve.Linear(0f, 1f, 7f, 2.4f);
    [Tooltip("横轴为战斗分钟数，纵轴为怪物基础攻击力乘数。由 WaveManager 配置。")]
    public AnimationCurve monsterAttackMultiplierByMinute = AnimationCurve.Linear(0f, 1f, 7f, 1.84f);
    public GameObject bossPrefab;
    public List<GameObject> normalPrefabs = new List<GameObject>();

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
    int spawnedBossCount;
    bool bossTimeReached;

    public float ActiveCombatSeconds { get; private set; }
    public int CurrentTier => MonsterSpawnDifficulty.TierAt(ActiveCombatSeconds, difficultyGrowthIntervalSeconds);
    public float CurrentHealthMultiplier => EvaluateMultiplier(monsterHealthMultiplierByMinute, 1f);
    public float CurrentAttackMultiplier => EvaluateMultiplier(monsterAttackMultiplierByMinute, 1f);
    public bool BossSpawned => spawnedBossCount > 0;
    public bool BossDefeated { get; private set; }
    public bool NonBossTimeReached => bossTimeReached || BossSpawned;
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

    public void ConfigureRunTiming(float nonBossSeconds, float growthIntervalSeconds,
        AnimationCurve healthMultiplierByMinute, AnimationCurve attackMultiplierByMinute)
    {
        bossCombatTime = Mathf.Max(1f, nonBossSeconds);
        difficultyGrowthIntervalSeconds = Mathf.Max(0.1f, growthIntervalSeconds);
        if (healthMultiplierByMinute != null && healthMultiplierByMinute.length > 0)
            monsterHealthMultiplierByMinute = healthMultiplierByMinute;
        if (attackMultiplierByMinute != null && attackMultiplierByMinute.length > 0)
            monsterAttackMultiplierByMinute = attackMultiplierByMinute;
        bossTimeReached = BossSpawned || ActiveCombatSeconds >= bossCombatTime;
    }

    float EvaluateMultiplier(AnimationCurve curve, float fallback)
    {
        if (curve == null || curve.length == 0) return fallback;
        return Mathf.Max(0.01f, curve.Evaluate(Mathf.Clamp(ActiveCombatSeconds / 60f, 0f, 7f)));
    }

    /// <summary>
    /// New WaveManager schedule entry point: resolve a normal prefab by Sin and spawn it
    /// at the shared screen-edge legal position logic.
    /// </summary>
    public MonsterActor SpawnScheduledMonster(SinType sin)
    {
        GameObject prefab = FindNormalPrefabForSin(sin);
        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (prefab == null || spawner == null) return null;
        if (!spawner.TryGetWaveSpawnPosition(sin, out Vector3 position)) return null;

        MonsterActor monster = spawner.SpawnContinuousMonster(prefab, position);
        if (monster == null) return null;
        return monster;
    }

    /// <summary>
    /// 玩家按方向移动达到一屏距离后的定向一换一：从不可见的连续自动普通怪中
    /// 随机选一只，在目标罪印扇区重新生成，连续自动怪数量保持不变。
    /// </summary>
    public bool TryReplaceInvisibleContinuousMonster(SinType targetSin)
    {
        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (spawner == null) return false;
        GameObject prefab = FindNormalPrefabForSin(targetSin);
        if (prefab == null) return false;
        if (!spawner.TryGetRandomInvisibleContinuousMonster(targetSin, out MonsterActor victim)) return false;
        if (!spawner.TryGetWaveSpawnPosition(targetSin, out Vector3 position)) return false;

        MonsterActor replacement = spawner.ReplaceContinuousMonster(victim, prefab, position);
        if (replacement == null) return false;
        if (WaveManager.Instance != null)
            WaveManager.Instance.ReplaceContinuousWaveMonster(victim, replacement);
        return true;
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
        bossTimeReached = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!IsActiveCombat()) return;
        ActiveCombatSeconds += Time.unscaledDeltaTime;
        if (!bossTimeReached && ActiveCombatSeconds >= bossCombatTime)
        {
            bossTimeReached = true;
            if (!BossSpawned && !DebugSpawnBossNow())
                Debug.LogWarning($"[RunSpawnDirector] 非 Boss 时长已到 {bossCombatTime:F1}s，但 Boss 未能生成。");
        }
    }

    bool IsActiveCombat()
    {
        RunSession run = RunSession.Instance;
        if (run != null && run.CurrentPhase != RunPhase.Waves && run.CurrentPhase != RunPhase.Final) return false;
        if (GameManager.Instance != null && GameManager.Instance.currentState == GameManager.GameState.GameOver) return false;
        return Time.timeScale > 0.0001f;
    }

    bool TrySpawn(GameObject prefab, SpawnRequest request, Vector3 explicitPosition = default(Vector3))
    {
        MonsterSpawner spawner = MonsterSpawner.Instance;
        if (spawner == null || prefab == null || spawner.TrackedMonsterCount >= spawner.maxCombatMonsters) return false;
        Vector3 position = explicitPosition;
        if (request.origin == SpawnOrigin.KillEcho)
        {
            if (!spawner.TryGetKillEchoSpawnPosition(request.sin, request.avoidPosition, out position)) return false;
        }
        else if (position == default(Vector3) && !spawner.TryGetLegacyWaveSpawnPosition(out position)) return false;
        if (request.minDistanceFromAvoid > 0f && (position - request.avoidPosition).sqrMagnitude < request.minDistanceFromAvoid * request.minDistanceFromAvoid)
            return false;
        MonsterActor monster = request.origin == SpawnOrigin.KillEcho
            ? spawner.SpawnKillEchoMonster(prefab, position, immediateChase: true)
            : spawner.SpawnWaveMonster(prefab, position, immediateChase: false);
        if (monster == null) return false;
        monster.ApplySpawnDifficultySnapshot(
            request.origin,
            request.difficultyTier,
            CurrentHealthMultiplier,
            CurrentAttackMultiplier);
        if (request.origin == SpawnOrigin.KillEcho
            && WaveManager.Instance != null
            && WaveManager.Instance.IsUsingNewSpawnLogic)
            WaveManager.Instance.RegisterExternalWaveMonster(monster);
        return true;
    }

    public bool DebugSpawnBossNow()
    {
        if (BossSpawned || bossPrefab == null || MonsterSpawner.Instance == null) return false;
        if (!MonsterSpawner.Instance.TryGetLegacyWaveSpawnPosition(out Vector3 position)) return false;
        MonsterActor actor = MonsterSpawner.Instance.SpawnWaveMonster(bossPrefab, position);
        if (actor == null) return false;
        actor.ApplySpawnDifficultySnapshot(
            SpawnOrigin.Boss,
            CurrentTier,
            CurrentHealthMultiplier,
            CurrentAttackMultiplier);
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
        bossTimeReached = true;
        return true;
    }

    public int SpawnBossMinions(int count)
    {
        if (BossDefeated || count <= 0 || normalPrefabs == null || normalPrefabs.Count == 0) return 0;
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            MonsterSpawner spawner = MonsterSpawner.Instance;
            if (spawner == null || !spawner.TryGetLegacyWaveSpawnPosition(out Vector3 position)) break;
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
        bossTimeReached = BossSpawned || ActiveCombatSeconds >= bossCombatTime;
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
