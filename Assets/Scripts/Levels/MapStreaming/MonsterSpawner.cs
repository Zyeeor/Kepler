using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物生命周期基础设施（波次玩法驱动）：
///   - 波次怪刷出 / 取点 / 回收（API 由 WaveManager 调用）
///   - 全场配额（maxCombatMonsters）：普通战斗来源共享，精英怪独立
///   - 追踪与修剪（trackedByChunk / trackInfoByMonster），供指引 UI 数据源与调试
///
/// 职责边界：本类管"怎么刷出并管理怪"（生成基础设施），
/// WaveManager 管"什么时候刷什么怪"（玩法编排）。只挂 WaveManager 时由 EnsureInstance 自动补齐。
///
/// 2026-08-18 精简：删除地图静态怪模式（enableChunkStaticSpawns 整套）与视野外提示
/// （EdgeIndicatorUI 被 MonsterDirectionUI 取代）。
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    /// <summary>全局唯一实例。</summary>
    public static MonsterSpawner Instance { get; private set; }

    [Header("全场配额")]
    [Tooltip("全局安全上限（普通怪、怪潮、击杀回响等战斗来源共享）。精英怪不占用该上限。")]
    [Min(1)] public int maxCombatMonsters = 30;
    [Tooltip("连续自动生成普通怪的同时存在上限。常规刷怪与怪潮合计；精英怪不占用。达到上限后只有这些怪被击杀/离场才会继续生成。WaveManager 连续逻辑会按时间成长档位更新此值。")]
    [Min(1)] public int continuousSpawnMaxCount = 10;

    [Header("波次取点")]
    [Tooltip("波次刷怪点到玩家的最小距离（米）——别在玩家面前刷。")]
    [Min(0f)] public float minSpawnDistanceToPlayer = 20f;
    [Tooltip("波次刷怪点到玩家的最大距离（米）——须在 B 缓冲带内。")]
    [Min(0f)] public float maxSpawnDistanceToPlayer = 50f;
    [Tooltip("群系中心之间的最小间距（米）：不同批次刷出的怪群出生即保持距离，避免源头重叠堆积。0 = 关闭。")]
    [Min(0f)] public float minSpawnPointSeparation = 6f;

    [Header("精英取点")]
    [Tooltip("精英出生点到玩家的距离，占当前水平屏幕直径的比例。0.25 = 四分之一屏幕直径。")]
    [Range(0.1f, 0.5f)] public float eliteSpawnScreenDiameterFraction = 0.25f;
    [Tooltip("精英出生点在屏幕内目标半径附近的候选采样次数。")]
    [Range(8, 64)] public int eliteSpawnSamples = 32;
    [Tooltip("目标半径附近没有可走点时，允许收缩到目标半径的最低比例。")]
    [Range(0.5f, 1f)] public float eliteSpawnFallbackRadiusFraction = 0.75f;

    [Header("出生点合法性")]
    [Tooltip("初始出生点落入岩浆、地刺或可阻挡碰撞体时，在此半径内寻找可走且合法的位置；0 表示不重定位。")]
    [Min(0f)] public float invalidSpawnRelocationRadius = 6f;
    [Tooltip("非法出生点的周边采样次数。采样失败时取消本次生成，避免怪物出现在禁行区域。")]
    [Range(8, 64)] public int invalidSpawnRelocationSamples = 32;

    [Header("击杀回声取点")]
    [Tooltip("新逻辑所有类型刷怪越过屏幕边界后的基础安全距离（米）。越小越贴近屏幕边缘。")]
    [Min(0f)] public float killEchoScreenPadding = 0.75f;

    [Header("新逻辑：罪印扇区取点")]
    [Tooltip("玩家视角 360° 被等分为 7 个扇区；列表顺序决定七种罪印从镜头正前方顺时针排列的扇区。")]
    public List<SinType> spawnSectorOrder = new List<SinType>
    {
        SinType.Pride,
        SinType.Sloth,
        SinType.Gluttony,
        SinType.Envy,
        SinType.Wrath,
        SinType.Greed,
        SinType.Lust,
    };
    [Tooltip("新逻辑刷怪距离在屏幕外最近边缘基础上可随机增加的最大偏移（米）。")]
    [Min(0f)] public float screenEdgeSpawnOffset = 2f;

    [Header("节奏")]
    [Tooltip("低频维护间隔（秒）：追踪列表修剪（尸体 fade 自行回池的怪摘除）。")]
    [Min(0.05f)] public float upkeepInterval = 0.25f;

    [Header("尸体消散阈值")]
    [Tooltip("场上普通击杀尸体的数量阈值。超过后，最早进入队列的尸体开始现有消散倒计时；倒计时结束后才进入 Fading；0 = 保持原有击杀后自动倒计时。")]
    [Min(0)] public int corpseDissipationThreshold = 5;

    [Header("调试")]
    [Tooltip("Scene 视图绘制每只已刷怪的位置圆点（绿=激活，蓝=休眠）。")]
    public bool showGizmos = true;
    [Tooltip("刷怪 / 回收时输出 Debug.Log。")]
    public bool logSpawns = true;
    [Tooltip("屏幕右上角显示在场怪计数面板。")]
    public bool showDebugHud = true;

    /// <summary>当前计入全局战斗配额的在场怪物数（含未回收的倒地尸体；精英不计入）。</summary>
    public int TrackedMonsterCount { get; private set; }

    /// <summary>
    /// 当前仍占用连续自动刷怪槽位的怪物数。倒地、附身或已离场的身体不再占用槽位，
    /// 但仍可继续由波次清点/尸体生命周期管理。
    /// </summary>
    public int ActiveContinuousMonsterCount
    {
        get
        {
            int count = 0;
            foreach (var pair in trackInfoByMonster)
            {
                MonsterActor monster = pair.Key;
                if (!pair.Value.isContinuousAutomatic || monster == null || !monster.gameObject.activeInHierarchy)
                    continue;
                if (monster.isDowned || monster.isPossessed || monster.Body == MonsterActor.BodyState.Fading
                    || monster.Body == MonsterActor.BodyState.Despawned)
                    continue;
                count++;
            }
            return count;
        }
    }

    /// <summary>设置连续自动刷怪的当前时间档位上限。</summary>
    public void ConfigureContinuousSpawnMaxCount(int maxCount)
    {
        continuousSpawnMaxCount = Mathf.Max(1, maxCount);
    }

    /// <summary>
    /// 全局递增刷怪序号：作为 AI 种子流的 salt（MonsterActor.InitAiRng）。
    /// 刷怪顺序由 DomainWave 种子流决定（同种子同顺序），故序号随顺序可复现。
    /// </summary>
    private int spawnSequence;

    /// <summary>
    /// 波次刷怪随机流（种子确定性）：由 WaveManager 每波设置（WorldSeed 派生），
    /// 取点/群系散射等全部随机走此流——同种子下怪物种类与位置可复现。
    /// 未设置时（直接调用方）回退全局 Random 语义。
    /// </summary>
    public System.Random WaveRandom { get; set; }

    /// <summary>最近刷出的群系中心（滑动窗口），用于源头间距去重。</summary>
    readonly List<Vector3> recentSpawnCenters = new List<Vector3>();
    const int MaxRecentSpawnCenters = 16;

    /// <summary>
    /// 收集当前在场可交互的活怪（未销毁、未附身、未倒地），供指引 UI 等外部系统使用。
    /// </summary>
    public void CollectAliveMonsters(List<MonsterActor> buffer)
    {
        buffer.Clear();
        foreach (var kv in trackedByChunk)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                if (m.isPossessed || m.isDowned) continue; // 附身身体/倒地尸体不算可指引目标
                buffer.Add(m);
            }
        }
    }

    /// <summary>
    /// 收集当前场上真正可以被玩家接管的躯体。
    /// 与 CollectAliveMonsters 分开：这里专门包含 Downed 永久尸体，并以 MonsterActor.CanBePossessed 为最终判定。
    /// </summary>
    public void CollectPossessableMonsters(List<MonsterActor> buffer)
    {
        if (buffer == null) return;
        buffer.Clear();
        foreach (var kv in trackedByChunk)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
            {
                var monster = list[i];
                if (monster == null || !monster.gameObject.activeInHierarchy || !monster.CanBePossessed) continue;
                buffer.Add(monster);
            }
        }
    }

    readonly Dictionary<ChunkCoord, List<MonsterActor>> trackedByChunk = new Dictionary<ChunkCoord, List<MonsterActor>>();

    /// <summary>追踪信息：归属 Chunk + 来源 prefab + 是否波次怪。</summary>
    struct TrackedInfo
    {
        public ChunkCoord homeChunk;
        public GameObject prefab;
        /// <summary>波次怪：永不随 Chunk 休眠/回收/写快照，退场由 WaveManager 波次系统裁决。</summary>
        public bool isWaveMonster;
        /// <summary>新逻辑连续自动生成来源：受 continuousSpawnMaxCount 限制。</summary>
        public bool isContinuousAutomatic;
        /// <summary>是否占用 maxCombatMonsters 全局战斗配额。</summary>
        public bool countsTowardCombatLimit;
    }
    readonly Dictionary<MonsterActor, TrackedInfo> trackInfoByMonster = new Dictionary<MonsterActor, TrackedInfo>();
    readonly Queue<MonsterActor> corpseDissipationQueue = new Queue<MonsterActor>();
    readonly HashSet<MonsterActor> queuedDissipationCorpses = new HashSet<MonsterActor>();

    /// <summary>读取一只已追踪怪物的配额来源标记，供 Run 快照持久化使用。</summary>
    public bool TryGetTrackingInfo(MonsterActor monster, out bool isContinuousAutomatic,
        out bool countsTowardCombatLimit)
    {
        isContinuousAutomatic = false;
        countsTowardCombatLimit = true;
        if (monster == null || !trackInfoByMonster.TryGetValue(monster, out var info)) return false;
        isContinuousAutomatic = info.isContinuousAutomatic;
        countsTowardCombatLimit = info.countsTowardCombatLimit;
        return true;
    }

    // 低频维护复用缓冲（避免每 0.25s 分配）
    readonly List<ChunkCoord> chunkPruneBuffer = new List<ChunkCoord>();
    readonly List<MonsterActor> invisibleReplacementCandidates = new List<MonsterActor>(32);

    float nextUpkeepTime;
    Camera mainCamera;
    Transform playerFallback;

    /// <summary>
    /// 自举装配：确保场景存在一个 MonsterSpawner（缺则挂到场景根自动创建）。
    /// 协作组件（WaveManager 等）启动时调用，避免"只挂主组件缺基础设施"的配置错误。
    /// </summary>
    public static MonsterSpawner EnsureInstance()
    {
        if (Instance != null) return Instance;

        // Scene installers can call this from Awake before the scene component's own
        // Awake has registered Instance. Reuse the configured scene component first;
        // otherwise a default-30 runtime object would win the race and destroy the
        // scene-configured spawner before WaveManager starts.
        MonsterSpawner sceneSpawner = FindObjectOfType<MonsterSpawner>(true);
        if (sceneSpawner != null)
        {
            Instance = sceneSpawner;
            return sceneSpawner;
        }

        var go = new GameObject("MonsterSpawner");
        return go.AddComponent<MonsterSpawner>();
    }

    void Awake(){
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MonsterSpawner] 重复实例，销毁后者。");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy(){
        if (Instance == this) Instance = null;
    }

    void Update(){
        if (Time.unscaledTime < nextUpkeepTime) return;
        nextUpkeepTime = Time.unscaledTime + upkeepInterval;
        PruneTracked();
        PruneCorpseDissipationQueue();
        EnforceCorpseDissipationThreshold();
    }

    // ── 波次玩法刷怪 API（WaveManager 驱动） ──

    /// <summary>
    /// 波次刷怪：在指定世界坐标刷出 1 只怪并计入全场配额与追踪。
    /// AI 直接激活索敌；不随 Chunk 休眠/回收/写快照，退场由波次系统裁决。
    /// </summary>
    /// <param name="prefab">怪物 prefab（须挂 MonsterActor）。</param>
    /// <param name="pos">刷怪世界坐标（由取点方法提供，或调用方自行保证合法）。</param>
    /// <param name="immediateChase">是否在生成后立即向玩家移动（用于击杀回声怪物）。</param>
    /// <returns>刷出的 MonsterActor；配额满 / prefab 无效 / 无 Actor 时返回 null。</returns>
    public MonsterActor SpawnWaveMonster(GameObject prefab, Vector3 pos, bool immediateChase = false)
        => SpawnMonster(prefab, pos, immediateChase, SpawnOrigin.PeriodicPressure, countAsContinuousAutomatic: false);

    /// <summary>
    /// 新逻辑连续自动刷怪：受 continuousSpawnMaxCount 独立限制，供常规刷怪与怪潮使用。
    /// </summary>
    public MonsterActor SpawnContinuousMonster(GameObject prefab, Vector3 pos, bool immediateChase = false)
        => SpawnMonster(prefab, pos, immediateChase, SpawnOrigin.PeriodicPressure, countAsContinuousAutomatic: true);

    /// <summary>从 Run 存档恢复一只怪物，保留原刷怪来源与全局配额语义。</summary>
    public MonsterActor SpawnRestoredMonster(GameObject prefab, Vector3 pos,
        SpawnOrigin origin, bool countAsContinuousAutomatic, bool countsTowardCombatLimit)
        => SpawnMonster(prefab, pos, immediateChase: false, origin: origin,
            countAsContinuousAutomatic: countAsContinuousAutomatic,
            countsTowardCombatLimit: countsTowardCombatLimit);

    /// <summary>
    /// 精英怪刷出：保留生命周期追踪和波次清点，但不占全局战斗配额或连续自动怪上限。
    /// </summary>
    public MonsterActor SpawnEliteMonster(GameObject prefab, Vector3 pos, bool immediateChase = false)
    {
        MonsterActor monster = SpawnMonster(prefab, pos, immediateChase, SpawnOrigin.PeriodicPressure,
            countAsContinuousAutomatic: false, countsTowardCombatLimit: false);
        if (monster == null) return null;

        // Elite arrival is presentation-owned, but starting it here keeps wave, fallback and
        // debug elite injection paths visually identical.
        VoidWalkArrivalVisualFx arrivalFx = monster.GetComponent<VoidWalkArrivalVisualFx>();
        if (arrivalFx == null) arrivalFx = monster.gameObject.AddComponent<VoidWalkArrivalVisualFx>();
        arrivalFx.PlayArrival();
        return monster;
    }

    /// <summary>击杀回响刷怪：不占连续自动刷怪上限。</summary>
    public MonsterActor SpawnKillEchoMonster(GameObject prefab, Vector3 pos, bool immediateChase = true)
        => SpawnMonster(prefab, pos, immediateChase, SpawnOrigin.KillEcho, countAsContinuousAutomatic: false);

    MonsterActor SpawnMonster(GameObject prefab, Vector3 pos, bool immediateChase,
        SpawnOrigin origin, bool countAsContinuousAutomatic, bool countsTowardCombatLimit = true)
    {
        if (prefab == null) return null;
        if (countsTowardCombatLimit && TrackedMonsterCount >= maxCombatMonsters) return null;
        if (countAsContinuousAutomatic && ActiveContinuousMonsterCount >= Mathf.Max(1, continuousSpawnMaxCount))
            return null;
        GameObject go = MonsterPool.Instance.Spawn(prefab, pos, Quaternion.identity);
        if (go == null) return null;
        var monster = go.GetComponentInChildren<MonsterActor>(true);
        if (monster == null)
        {
            Debug.LogWarning($"[MonsterSpawner] 波次刷怪 prefab '{prefab.name}' 无 MonsterActor，已销毁跳过。");
            Destroy(go);
            return null;
        }
        pos.y = monster.aliveY;
        if (!TryResolveSpawnPosition(monster, pos, out Vector3 resolvedPosition))
        {
            Debug.LogWarning($"[MonsterSpawner] 波次刷怪 '{prefab.name}' 未能在 {invalidSpawnRelocationRadius:0.##}m 内找到合法出生点，已取消。", go);
            MonsterPool.Instance.Return(monster);
            return null;
        }
        bool relocated = Mathf.Abs(resolvedPosition.x - pos.x) > 0.0001f
            || Mathf.Abs(resolvedPosition.z - pos.z) > 0.0001f;
        if (relocated)
        {
            resolvedPosition.y = monster.aliveY;
            go.transform.position = resolvedPosition;
            pos = go.transform.position;
        }

        var system = MapStreamingSystem.Instance;
        ChunkCoord home = system != null ? system.WorldToChunk(pos) : default;
        monster.ResolveSinIdentityFromHint(prefab.name + " " + monster.displayName);
        monster.aiActiveOverride = true; // 波次怪直接激活索敌
        // AI 种子流：按全局递增刷怪序号分配（刷怪顺序由 DomainWave 种子流决定，序号随顺序可复现）
        monster.InitAiRng(spawnSequence++);
        RunSpawnDirector director = RunSpawnDirector.Instance;
        monster.ApplySpawnDifficultySnapshot(
            origin,
            director != null ? director.CurrentHealthMultiplier : 1f,
            director != null ? director.CurrentAttackMultiplier : 1f);
        Track(home, monster, prefab, isWaveMonster: true,
            isContinuousAutomatic: countAsContinuousAutomatic,
            countsTowardCombatLimit: countsTowardCombatLimit);
        if (immediateChase)
            monster.BeginImmediateChase();
        if (logSpawns)
            Debug.Log($"[MonsterSpawner] 波次刷怪 '{prefab.name}' @ {home}（配额内 {TrackedMonsterCount}/{maxCombatMonsters}，连续自动 {ActiveContinuousMonsterCount}/{continuousSpawnMaxCount}）。");
        return monster;
    }

    bool TryResolveSpawnPosition(MonsterActor monster, Vector3 requestedPosition, out Vector3 resolvedPosition)
    {
        resolvedPosition = requestedPosition;
        Transform spawnRoot = monster.transform.root;
        if (!MonsterPathfinder.IsSpawnPositionBlocked(monster, spawnRoot, requestedPosition, forceHazardRefresh: true)) return true;

        float searchRadius = Mathf.Max(0f, invalidSpawnRelocationRadius);
        if (searchRadius <= 0f) return false;

        int samples = Mathf.Max(8, invalidSpawnRelocationSamples);
        float startAngle = Random01(WaveRandom) * Mathf.PI * 2f;
        const float goldenAngle = 2.39996323f;
        for (int i = 0; i < samples; i++)
        {
            float distance = searchRadius * Mathf.Sqrt((i + 0.5f) / samples);
            float angle = startAngle + goldenAngle * i;
            Vector3 candidate = requestedPosition + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            if (!IsRelocationTileWalkable(candidate)) continue;
            if (MonsterPathfinder.IsSpawnPositionBlocked(monster, spawnRoot, candidate)) continue;

            resolvedPosition = candidate;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Resolves one Boss reserve corpse position against the streamed world's walkability,
    /// hazards and solid obstacles, while also keeping it away from reserve bodies that were
    /// already placed in this encounter.
    /// </summary>
    public bool TryResolveBossReserveSpawnPosition(MonsterActor monster, Vector3 requestedPosition,
        IList<Vector3> occupiedPositions, float minimumSeparation, out Vector3 resolvedPosition)
    {
        resolvedPosition = requestedPosition;
        if (IsBossReserveCandidateLegal(monster, requestedPosition, occupiedPositions, minimumSeparation,
            forceHazardRefresh: true))
            return true;

        float searchRadius = Mathf.Max(0f, invalidSpawnRelocationRadius);
        if (searchRadius <= 0f) return false;

        int samples = Mathf.Max(8, invalidSpawnRelocationSamples);
        float startAngle = Random01(WaveRandom) * Mathf.PI * 2f;
        const float goldenAngle = 2.39996323f;
        for (int i = 0; i < samples; i++)
        {
            float distance = searchRadius * Mathf.Sqrt((i + 0.5f) / samples);
            float angle = startAngle + goldenAngle * i;
            Vector3 candidate = requestedPosition + new Vector3(Mathf.Cos(angle) * distance, 0f,
                Mathf.Sin(angle) * distance);
            if (!IsBossReserveCandidateLegal(monster, candidate, occupiedPositions, minimumSeparation,
                forceHazardRefresh: false))
                continue;

            resolvedPosition = candidate;
            return true;
        }
        return false;
    }

    bool IsBossReserveCandidateLegal(MonsterActor monster, Vector3 candidate,
        IList<Vector3> occupiedPositions, float minimumSeparation, bool forceHazardRefresh)
    {
        if (!IsRelocationTileWalkable(candidate)) return false;

        Transform spawnRoot = monster != null ? monster.transform.root : null;
        if (MonsterPathfinder.IsSpawnPositionBlocked(monster, spawnRoot, candidate, forceHazardRefresh)) return false;

        float minSqr = Mathf.Max(0f, minimumSeparation) * Mathf.Max(0f, minimumSeparation);
        if (occupiedPositions == null || minSqr <= 0f) return true;
        for (int i = 0; i < occupiedPositions.Count; i++)
        {
            Vector3 delta = candidate - occupiedPositions[i];
            delta.y = 0f;
            if (delta.sqrMagnitude < minSqr) return false;
        }
        return true;
    }

    bool IsRelocationTileWalkable(Vector3 worldPosition)
    {
        var system = MapStreamingSystem.Instance;
        if (system == null) return true;
        if (!system.Registry.TryGetValue(system.WorldToChunk(worldPosition), out var chunk) || chunk == null || chunk.Tiles == null)
            return false;
        return IsWalkable(chunk, worldPosition);
    }

    /// <summary>
    /// 击杀回声刷怪点：从上一只被击杀怪物经过玩家延长射线，取屏幕边界外最近的可走 Tile。
    /// 这样新怪与上一只死去的怪、玩家三点共线，并从玩家视野外直接朝玩家追击。
    /// </summary>
    public bool TryGetKillEchoSpawnPosition(Vector3 lastDeathPosition, out Vector3 pos)
        => TryGetKillEchoSpawnPosition(SinType.None, lastDeathPosition, out pos);

    /// <summary>
    /// 击杀回声取点：按被击杀怪的罪印锁定玩家视角扇区，并在该扇区内随机取角度与屏幕外距离。
    /// </summary>
    public bool TryGetKillEchoSpawnPosition(SinType sin, Vector3 lastDeathPosition, out Vector3 pos)
    {
        pos = default;
        var system = MapStreamingSystem.Instance;
        if (system == null) return false;

        Vector3 player = GetPlayerPosition();
        Vector3 awayFromDeath = player - lastDeathPosition;
        awayFromDeath.y = 0f;
        if (awayFromDeath.sqrMagnitude < 0.0001f)
        {
            Camera cam = GetMainCamera();
            awayFromDeath = cam != null ? cam.transform.forward : Vector3.forward;
            awayFromDeath.y = 0f;
        }
        if (awayFromDeath.sqrMagnitude < 0.0001f) awayFromDeath = Vector3.forward;
        awayFromDeath.Normalize();

        Camera camera = GetMainCamera();
        for (int attempt = 0; attempt < 32; attempt++)
        {
            Vector3 direction = sin == SinType.None
                ? awayFromDeath
                : GetRandomSpawnDirection(sin, camera);
            float boundaryDistance = 0f;
            if (camera != null && TryGetScreenExitDistance(camera, player, direction, out float screenExitDistance))
                boundaryDistance = screenExitDistance;

            float minDistance = Mathf.Max(minSpawnDistanceToPlayer,
                boundaryDistance + Mathf.Max(0f, killEchoScreenPadding));
            float distance = minDistance + Random01() * Mathf.Max(0f, screenEdgeSpawnOffset);
            Vector3 candidate = player + direction * distance;
            if (camera != null && IsOnScreen(camera, candidate)) continue;
            if (!system.Registry.TryGetValue(system.WorldToChunk(candidate), out var chunk) || chunk == null || chunk.Tiles == null)
                continue;
            if (!IsWalkable(chunk, candidate)) continue;
            pos = candidate;
            return true;
        }
        return false;
    }

    bool TryGetScreenExitDistance(Camera camera, Vector3 origin, Vector3 direction, out float distance)
    {
        distance = 0f;
        if (!IsOnScreen(camera, origin)) return true;

        const float sampleStep = 1f;
        float previous = 0f;
        float maxDistance = Mathf.Max(maxSpawnDistanceToPlayer * 2f, 100f);
        for (float current = sampleStep; current <= maxDistance; current += sampleStep)
        {
            if (!IsOnScreen(camera, origin + direction * current))
            {
                float low = previous;
                float high = current;
                for (int i = 0; i < 12; i++)
                {
                    float middle = (low + high) * 0.5f;
                    if (IsOnScreen(camera, origin + direction * middle)) low = middle;
                    else high = middle;
                }
                distance = high;
                return true;
            }
            previous = current;
        }
        return false;
    }

    bool IsOnScreen(Camera camera, Vector3 worldPosition)
    {
        Vector3 viewport = camera.WorldToViewportPoint(worldPosition);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f
            && viewport.y >= 0f && viewport.y <= 1f;
    }

    /// <summary>
    /// 波次刷怪点查找：玩家周围 [minSpawnDistanceToPlayer, maxSpawnDistanceToPlayer] 环形带内
    /// 随机采样，要求落在已加载 Chunk（Registry 有条目、Tiles 就绪）的可走 Tile 上，
    /// 并且在摄像机屏幕之外，保证怪物从玩家视野边缘外进入。
    /// </summary>
    public bool TryGetWaveSpawnPosition(out Vector3 pos)
        => TryGetWaveSpawnPosition(SinType.None, out pos);

    /// <summary>
    /// 新逻辑波次取点：罪印类型锁定玩家视角七等分扇区；角度和屏幕外距离均在扇区/偏移范围内随机。
    /// </summary>
    public bool TryGetWaveSpawnPosition(SinType sin, out Vector3 pos)
    {
        if (TrySampleSpawnPosition(sin, minSpawnPointSeparation, out pos)) return true;
        // P2 兜底：严格间距下采样被 recentSpawnCenters 全部拦截（窗口内的点尚未随怪移动释放），
        // 放宽到一半间距再试一轮，避免本 tick 静默少刷；仍失败才放弃。
        if (minSpawnPointSeparation > 0f && TrySampleSpawnPosition(sin, minSpawnPointSeparation * 0.5f, out pos))
            return true;
        return false;
    }

    /// <summary>
    /// 精英取点：在玩家周围目标屏幕直径比例的环带内取点，并要求点位落在当前镜头内。
    /// 候选点先通过 Chunk 的可走性快照；真正刷出时 SpawnMonster 还会用怪物自身的
    /// Collider 重新执行 MonsterPathfinder 的危险区/实体碰撞/体积检查。
    /// </summary>
    public bool TryGetEliteSpawnPosition(out Vector3 pos)
    {
        pos = default;
        var system = MapStreamingSystem.Instance;
        if (system == null) return false;

        Vector3 player = GetPlayerPosition();
        Camera camera = GetMainCamera();
        float targetRadius = Mathf.Max(0.5f,
            GetScreenDiameterWorldDistance() * Mathf.Clamp(eliteSpawnScreenDiameterFraction, 0.1f, 0.5f));
        float minimumRadius = targetRadius * Mathf.Clamp(eliteSpawnFallbackRadiusFraction, 0.5f, 1f);
        int samples = Mathf.Max(8, eliteSpawnSamples);
        float startAngle = Random01(WaveRandom) * Mathf.PI * 2f;
        const float goldenAngle = 2.39996323f;

        // Try the requested quarter-screen radius first. Only shrink the radius as a
        // fallback when terrain legality leaves no usable point on the exact ring.
        for (int ring = 0; ring < 3; ring++)
        {
            float ringT = ring / 2f;
            float radius = Mathf.Lerp(targetRadius, minimumRadius, ringT);
            for (int i = 0; i < samples; i++)
            {
                float angle = startAngle + goldenAngle * i;
                Vector3 candidate = player + new Vector3(Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius);
                if (camera != null && !IsOnScreen(camera, candidate)) continue;
                if (!system.Registry.TryGetValue(system.WorldToChunk(candidate), out var chunk)
                    || chunk == null || chunk.Tiles == null)
                    continue;
                if (!IsWalkable(chunk, candidate)) continue;

                pos = candidate;
                return true;
            }
        }
        return false;
    }

    /// <summary>在玩家周围环形带内采样一个合法刷怪点（与已刷点保持 separation 间距）。</summary>
    bool TrySampleSpawnPosition(SinType sin, float separation, out Vector3 pos)
    {
        pos = default;
        var system = MapStreamingSystem.Instance;
        if (system == null) return false;
        Vector3 player = GetPlayerPosition();
        var cam = GetMainCamera();
        var rng = WaveRandom; // 种子随机流（WaveManager 注入）；null 时回退全局 Random
        float configuredMin = Mathf.Max(0f, minSpawnDistanceToPlayer);
        float configuredMax = Mathf.Max(configuredMin, maxSpawnDistanceToPlayer);
        for (int i = 0; i < 32; i++)
        {
            Vector3 direction = GetRandomSpawnDirection(sin, cam);
            float edgeDistance = 0f;
            if (cam != null && TryGetScreenExitDistance(cam, player, direction, out float screenExitDistance))
                edgeDistance = screenExitDistance + Mathf.Max(0.5f, killEchoScreenPadding);

            // 屏幕外最近边缘是最小距离，screenEdgeSpawnOffset 控制向外浮动范围。
            float minDistance = Mathf.Max(configuredMin, edgeDistance);
            float maxDistance = Mathf.Max(minDistance, Mathf.Min(configuredMax,
                minDistance + Mathf.Max(0f, screenEdgeSpawnOffset)));
            if (maxDistance < minDistance) maxDistance = minDistance;
            float r = minDistance + Random01(rng) * (maxDistance - minDistance);
            Vector3 c = player + direction * r;
            if (!system.Registry.TryGetValue(system.WorldToChunk(c), out var chunk) || chunk == null || chunk.Tiles == null)
                continue; // 该点归属 Chunk 未加载：跳过（地图边界外同样落此分支）
            if (cam != null && IsOnScreen(cam, c)) continue;
            if (!IsWalkable(chunk, c)) continue;
            if (TooCloseToRecentCenter(c, separation)) continue;
            pos = c;
            RememberSpawnCenter(pos);
            return true;
        }
        return false;
    }

    Vector3 GetRandomSpawnDirection(SinType sin, Camera camera)
    {
        Vector3 forward = camera != null ? camera.transform.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        int sectorIndex = GetSpawnSectorIndex(sin);
        if (sectorIndex < 0)
            return Quaternion.AngleAxis(Random01() * 360f, Vector3.up) * forward;

        const int sectorCount = 7;
        float sectorWidth = 360f / sectorCount;
        float angle = -sectorWidth * 0.5f + (sectorIndex * sectorWidth) + Random01() * sectorWidth;
        return Quaternion.AngleAxis(angle, Vector3.up) * forward;
    }

    /// <summary>根据玩家在世界中的水平移动方向，解析其对应的七等分罪印扇区。</summary>
    public bool TryGetSinForWorldDirection(Vector3 worldDirection, out SinType sin)
    {
        sin = SinType.None;
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude < 0.0001f) return false;

        Camera camera = GetMainCamera();
        Vector3 forward = camera != null ? camera.transform.forward : Vector3.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        worldDirection.Normalize();

        float signedAngle = Vector3.SignedAngle(forward, worldDirection, Vector3.up);
        const int sectorCount = 7;
        float sectorWidth = 360f / sectorCount;
        float normalized = Mathf.Repeat(signedAngle + sectorWidth * 0.5f, 360f);
        int sectorIndex = Mathf.Min(sectorCount - 1, Mathf.FloorToInt(normalized / sectorWidth));
        sin = GetSinForSectorIndex(sectorIndex);
        return sin != SinType.None;
    }

    /// <summary>
    /// 计算玩家所在平面上当前镜头的水平屏幕直径。用于移动达到一屏距离时触发定向替换。
    /// </summary>
    public float GetScreenDiameterWorldDistance()
    {
        Camera camera = GetMainCamera();
        if (camera == null)
            return Mathf.Max(1f, maxSpawnDistanceToPlayer - minSpawnDistanceToPlayer) * 2f;

        Vector3 origin = GetPlayerPosition();
        Vector3 right = camera.transform.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f)
            return Mathf.Max(1f, maxSpawnDistanceToPlayer - minSpawnDistanceToPlayer) * 2f;
        right.Normalize();

        if (TryGetScreenExitDistance(camera, origin, right, out float rightDistance)
            && TryGetScreenExitDistance(camera, origin, -right, out float leftDistance))
            return Mathf.Max(1f, rightDistance + leftDistance);

        if (camera.orthographic)
            return Mathf.Max(1f, camera.orthographicSize * 2f * camera.aspect);
        return Mathf.Max(1f, maxSpawnDistanceToPlayer - minSpawnDistanceToPlayer) * 2f;
    }

    int GetSpawnSectorIndex(SinType sin)
    {
        if (sin == SinType.None) return -1;
        if (spawnSectorOrder != null)
        {
            for (int i = 0; i < spawnSectorOrder.Count && i < 7; i++)
                if (spawnSectorOrder[i] == sin) return i;
        }
        int fallback = (int)sin - 1;
        return fallback >= 0 && fallback < 7 ? fallback : -1;
    }

    SinType GetSinForSectorIndex(int sectorIndex)
    {
        if (sectorIndex < 0 || sectorIndex >= 7) return SinType.None;
        if (spawnSectorOrder != null && sectorIndex < spawnSectorOrder.Count)
            return spawnSectorOrder[sectorIndex];
        return (SinType)(sectorIndex + 1);
    }

    float Random01()
        => Random01(WaveRandom);

    static float Random01(System.Random random)
        => random != null ? (float)random.NextDouble() : UnityEngine.Random.value;

    /// <summary>
    /// 旧波次取点：保留原有环形带 + 前方扇形排除规则，不使用新逻辑的屏幕外边缘约束。
    /// CountKill / Timed 关闭新开关时走此入口，保证旧流程的生成手感不变。
    /// </summary>
    public bool TryGetLegacyWaveSpawnPosition(out Vector3 pos)
    {
        pos = default;
        var system = MapStreamingSystem.Instance;
        if (system == null) return false;
        Vector3 player = GetPlayerPosition();
        var cam = GetMainCamera();
        var rng = WaveRandom;
        for (int i = 0; i < 16; i++)
        {
            float angle = (rng != null ? rng.Next(0, 360) : UnityEngine.Random.Range(0, 360)) * Mathf.Deg2Rad;
            float r = rng != null
                ? minSpawnDistanceToPlayer + (float)rng.NextDouble() * (maxSpawnDistanceToPlayer - minSpawnDistanceToPlayer)
                : UnityEngine.Random.Range(minSpawnDistanceToPlayer, maxSpawnDistanceToPlayer);
            Vector3 c = player + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            if (!system.Registry.TryGetValue(system.WorldToChunk(c), out var chunk) || chunk == null || chunk.Tiles == null)
                continue;
            if (cam != null)
            {
                Vector3 toPoint = c - player;
                Vector3 fwd = cam.transform.forward;
                toPoint.y = 0f;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.0001f && toPoint.sqrMagnitude > 0.0001f
                    && Vector3.Angle(fwd, toPoint) < 60f)
                    continue;
            }
            if (!IsWalkable(chunk, c)) continue;
            if (TooCloseToRecentCenter(c, minSpawnPointSeparation)) continue; // 与已刷点保持最小间距，避免源头重叠
            pos = c;
            RememberSpawnCenter(pos);
            return true;
        }
        return false;
    }

    /// <summary>与最近刷出的点是否过近（源头间距去重，仅水平距离）。separation 可传放大的间距用于二次尝试。</summary>
    bool TooCloseToRecentCenter(Vector3 candidate, float separation)
    {
        if (separation <= 0f || recentSpawnCenters.Count == 0) return false;
        float minSqr = separation * separation;
        for (int i = 0; i < recentSpawnCenters.Count; i++)
        {
            Vector3 delta = candidate - recentSpawnCenters[i];
            delta.y = 0f;
            if (delta.sqrMagnitude < minSqr) return true;
        }
        return false;
    }

    /// <summary>记录一个成功取用的刷怪点（滑动窗口，超出上限移除最旧）。</summary>
    void RememberSpawnCenter(Vector3 center)
    {
        if (minSpawnPointSeparation <= 0f) return;
        recentSpawnCenters.Add(center);
        if (recentSpawnCenters.Count > MaxRecentSpawnCenters)
            recentSpawnCenters.RemoveAt(0);
    }

    /// <summary>
    /// 回收单只波次怪（时间波结算清场用）：摘除追踪/配额计数后回池，不写 Chunk 快照
    /// （波次怪与地图静态怪分离，清场即永久退场，不会在快照恢复时重现）。
    /// </summary>
    public void RecycleWaveMonster(MonsterActor monster)
    {
        if (monster == null) return;
        if (monster is BossSevenfoldActor)
        {
            // Boss owns its own death/fade/pool lifecycle. Wave cleanup must never
            // recycle it while the takeover or encounter is still active.
            return;
        }
        // 时间波清场跳过被附身怪：回收会把灵魂连带带入 DDOL 场景（MonsterPool.Return 也有兜底，
        // 此处提前跳过以保持追踪数据一致——附身结束走正常死亡流程）。
        if (monster.isPossessed) return;
        ChunkCoord home = default;
        if (trackInfoByMonster.TryGetValue(monster, out var info))
        {
            home = info.homeChunk;
            if (trackedByChunk.TryGetValue(home, out var list)) list.Remove(monster);
            Untrack(monster);
        }
        MonsterPool.Instance.Return(monster);
    }

    /// <summary>从连续自动刷怪池中随机挑选一个玩家当前不可见的非目标普通怪。</summary>
    public bool TryGetRandomInvisibleContinuousMonster(SinType targetSin, out MonsterActor victim)
    {
        victim = null;
        Camera camera = GetMainCamera();
        if (camera == null) return false;

        invisibleReplacementCandidates.Clear();
        foreach (var pair in trackInfoByMonster)
        {
            MonsterActor monster = pair.Key;
            if (!pair.Value.isContinuousAutomatic || monster == null || !monster.gameObject.activeInHierarchy)
                continue;
            if (monster.sinType == targetSin || monster.isDowned || monster.isPossessed || monster.IsElite)
                continue;
            if (monster.Body == MonsterActor.BodyState.Fading || monster.Body == MonsterActor.BodyState.Despawned)
                continue;
            if (IsOnScreen(camera, monster.transform.position)) continue;
            invisibleReplacementCandidates.Add(monster);
        }

        if (invisibleReplacementCandidates.Count == 0) return false;
        int index = Mathf.Min(invisibleReplacementCandidates.Count - 1,
            Mathf.FloorToInt(Random01() * invisibleReplacementCandidates.Count));
        victim = invisibleReplacementCandidates[index];
        return victim != null;
    }

    /// <summary>
    /// 一换一替换连续自动刷怪：先释放旧怪的追踪槽位，再在目标罪印的合法屏幕外位置生成新怪。
    /// </summary>
    public MonsterActor ReplaceContinuousMonster(MonsterActor victim, GameObject prefab, Vector3 position)
    {
        if (victim == null || prefab == null || !trackInfoByMonster.TryGetValue(victim, out var info)
            || !info.isContinuousAutomatic || victim.isDowned || victim.isPossessed || victim.IsElite)
            return null;

        if (trackedByChunk.TryGetValue(info.homeChunk, out var list))
            list.Remove(victim);
        Untrack(victim);
        MonsterPool.Instance.Return(victim);
        return SpawnContinuousMonster(prefab, position);
    }

    /// <summary>Releases a permanent corpse from the active combat quota without deactivating it.</summary>
    public void ReleaseTracking(MonsterActor monster)
    {
        if (monster == null || !trackInfoByMonster.TryGetValue(monster, out var info)) return;
        if (trackedByChunk.TryGetValue(info.homeChunk, out var list))
            list.Remove(monster);
        Untrack(monster);
    }

    /// <summary>世界坐标是否落在该 Chunk 的可走 Tile 上（经系统坐标换算，支持地图旋转）。</summary>
    bool IsWalkable(ChunkRuntime chunk, Vector3 worldPos)
    {
        var system = MapStreamingSystem.Instance;
        var tiles = chunk.Tiles;
        if (!system.WorldToTileLocal(worldPos, chunk.Coord, out int lx, out int ly)) return false;
        return tiles[lx, ly].isWalkable;
    }

    // ── 躯体消散队列 ──

    /// <summary>登记一具普通击杀尸体；神龛/Boss 永久尸体不经过此入口。</summary>
    public void RegisterTimedCorpse(MonsterActor corpse)
    {
        if (corpse == null || corpse.Body != MonsterActor.BodyState.Downed || corpse.isPossessed
            || corpse.IsBossBattleReserveBody || !queuedDissipationCorpses.Add(corpse)) return;

        corpseDissipationQueue.Enqueue(corpse);
        if (corpseDissipationThreshold <= 0)
        {
            queuedDissipationCorpses.Remove(corpse);
            corpse.TriggerCorpseDissipation();
            return;
        }
        EnforceCorpseDissipationThreshold();
    }

    /// <summary>尸体进入附身或消散流程后，从 FIFO 的有效集合中摘除。</summary>
    public void UnregisterTimedCorpse(MonsterActor corpse)
    {
        if (corpse != null) queuedDissipationCorpses.Remove(corpse);
    }

    void PruneCorpseDissipationQueue()
    {
        while (corpseDissipationQueue.Count > 0)
        {
            MonsterActor corpse = corpseDissipationQueue.Peek();
            if (corpse != null && queuedDissipationCorpses.Contains(corpse)) break;
            corpseDissipationQueue.Dequeue();
        }
        if (queuedDissipationCorpses.Count == 0) corpseDissipationQueue.Clear();
    }

    void EnforceCorpseDissipationThreshold()
    {
        int threshold = Mathf.Max(0, corpseDissipationThreshold);
        if (threshold <= 0) return;

        while (queuedDissipationCorpses.Count > threshold && corpseDissipationQueue.Count > 0)
        {
            MonsterActor oldest = corpseDissipationQueue.Dequeue();
            if (oldest == null || !queuedDissipationCorpses.Remove(oldest)) continue;
            if (!oldest.gameObject.activeInHierarchy || oldest.Body != MonsterActor.BodyState.Downed
                || oldest.isPossessed || oldest.IsBossBattleReserveBody) continue;

            // 只启动既有 possession countdown；倒计时结束后才进入 Fading / 回池流程。
            oldest.TriggerCorpseDissipation();
        }
    }

    // ── 追踪与配额 ──

    void Track(ChunkCoord coord, MonsterActor monster, GameObject prefab)
        => Track(coord, monster, prefab, isWaveMonster: false, isContinuousAutomatic: false);

    void Track(ChunkCoord coord, MonsterActor monster, GameObject prefab, bool isWaveMonster,
        bool isContinuousAutomatic = false, bool countsTowardCombatLimit = true)
    {
        if (!trackedByChunk.TryGetValue(coord, out var list))
        {
            list = new List<MonsterActor>();
            trackedByChunk.Add(coord, list);
        }
        list.Add(monster);
        trackInfoByMonster[monster] = new TrackedInfo
        {
            homeChunk = coord,
            prefab = prefab,
            isWaveMonster = isWaveMonster,
            isContinuousAutomatic = isContinuousAutomatic,
            countsTowardCombatLimit = countsTowardCombatLimit,
        };
        if (countsTowardCombatLimit)
            TrackedMonsterCount++;
    }

    /// <summary>摘除单个追踪项（配额计数 + trackInfo 清理）。trackedByChunk 列表项由调用方移除。</summary>
    void Untrack(MonsterActor monster)
    {
        bool countsTowardCombatLimit = true;
        if (trackInfoByMonster.TryGetValue(monster, out var info))
            countsTowardCombatLimit = info.countsTowardCombatLimit;
        trackInfoByMonster.Remove(monster);
        if (countsTowardCombatLimit)
            TrackedMonsterCount = Mathf.Max(0, TrackedMonsterCount - 1);
    }

    /// <summary>摘除失效追踪项：怪物可能经尸体 fade 自行回池（FadeAndReturnRoutine → MonsterPool.Return）。</summary>
    void PruneTracked(){
        chunkPruneBuffer.Clear();
        foreach (var kv in trackedByChunk)
        {
            var list = kv.Value;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var m = list[i];
                if (m == null || !m.gameObject.activeInHierarchy || m.Body == MonsterActor.BodyState.Despawned)
                {
                    list.RemoveAt(i);
                    Untrack(m);
                }
            }
            if (list.Count == 0) chunkPruneBuffer.Add(kv.Key);
        }
        for (int i = 0; i < chunkPruneBuffer.Count; i++)
            trackedByChunk.Remove(chunkPruneBuffer[i]);
    }

    // ── 工具 ──

    Camera GetMainCamera()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        return mainCamera;
    }

    Vector3 GetPlayerPosition(){
        if (PlayerController.Instance != null) return PlayerController.Instance.transform.position;
        if (playerFallback == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) playerFallback = go.transform;
        }
        // 场景无玩家时以自身位置为基准（与 MapStreamingSystem 同款兜底）
        return playerFallback != null ? playerFallback.position : transform.position;
    }

    /// <summary>当前玩家位置，供连续刷怪编排读取移动距离。</summary>
    public Vector3 CurrentPlayerPosition => GetPlayerPosition();

    // ── 调试可视化 ──

    void OnDrawGizmos(){
        if (!showGizmos || !Application.isPlaying) return;

        var system = MapStreamingSystem.Instance;
        foreach (var kv in trackedByChunk)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null) continue;
                Gizmos.color = m.aiActiveOverride ? new Color(0.2f, 1f, 0.2f, 0.9f) : new Color(0.2f, 0.6f, 1f, 0.9f);
                Gizmos.DrawSphere(m.transform.position + Vector3.up * 0.5f, 0.35f);
            }
#if UNITY_EDITOR
            if (system != null)
            {
                Vector3 labelPos = system.ChunkCenter(kv.Key) + Vector3.up * 2f;
                UnityEditor.Handles.Label(labelPos, $"{kv.Key} 怪×{list.Count}");
            }
#endif
        }
    }

    void OnGUI(){
        if (!showDebugHud || !Application.isPlaying || GameManager.IsFormalFlow) return; // 正式流程屏蔽刷怪面板

        GUI.Box(new Rect(Screen.width - 250f, 10f, 240f, 40f), "MonsterSpawner");
        GUI.Label(new Rect(Screen.width - 242f, 32f, 224f, 18f), $"剩怪 {TrackedMonsterCount} 只（在场，含休眠）");
    }
}
