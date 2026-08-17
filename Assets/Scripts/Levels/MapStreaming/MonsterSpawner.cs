using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物刷怪调度：监听 MapStreamingSystem 的 Chunk 状态切换事件，
/// 驱动刷怪 / AI 激活休眠 / 回收，与流送系统解耦（System 不直接引用本类）。
///
/// 状态切换响应：
///   Prepared→Dormant（进 B）：无 ChunkState → 按 ChunkDef.waveTable 权重抽波次刷怪（AI 休眠）；
///                             有 ChunkState（摇过波次）→ 按 MonsterSnapshot 恢复，不重摇
///   Dormant→Active（进 A）：该 Chunk 怪物 AI 激活（MonsterActor.aiActiveOverride = true）
///   Active→Dormant（离开 A）：AI 休眠（附身中的怪跳过——永不休眠）
///   Dormant→Prepared / →Unloaded（离开 B/D）：先快照进 ChunkState再回收 MonsterPool
///                             （附身 / 玩家贴脸的怪跳过回收——仍在场，不写快照）
///
/// 另职责：
///   - 全场配额：Active + Dormant 总数 ≤ maxCombatMonsters，满时不刷、已刷不回收
///   - 视野外战斗怪列表：低频维护，供 EdgeIndicatorUI 画屏幕边缘方向提示
///   - spawnedWaveIds 重入去重：抽中波次即登记，重进 Chunk 不重摇
///   - bodySupplyConsumed：附身消耗计数（订阅 PossessionManager.OnPossessionStarted）
///   - 脱战远距离回收：脱战且距玩家 > B 半径且视野外的怪，快照写回归属 Chunk 后回池
///
/// Phase 3 边界（明确不做，见各处 TODO）：
///   尸体/奖励/事件恢复、脱战怪"传送回 Chunk + 视线遮挡"实体化、
///   落盘序列化、
///   statMult 属性倍率应用（需 prefab 基值快照机制，否则池复用倍率累积）。
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
    /// <summary>全局唯一实例。</summary>
    public static MonsterSpawner Instance { get; private set; }

    [Header("全场配额")]
    [Tooltip("在场怪物上限（Active + Dormant）。满时不再刷怪，已刷的不回收（等自然脱战/击杀）。")]
    [Min(1)] public int maxCombatMonsters = 30;

    [Header("波次模式")]
    [Tooltip("地图静态怪：false = 波次玩法，Chunk 进 B 不再按 waveTable 刷静态怪，场上所有怪物由 WaveManager 波次逻辑驱动（波次怪永不休眠、不随 Chunk 回收/写快照）。")]
    public bool enableChunkStaticSpawns = false;

    [Header("刷怪")]
    [Tooltip("单个 Chunk 刷怪波次上限；无 MarkerLayer 刷怪 Tile 时的占位刷怪点数也取此值。")]
    [Min(1)] public int maxWavesPerChunk = 2;
    [Tooltip("波内怪物散布半径（米）：围绕刷怪点随机取可走 Tile，取不到则落回刷怪点本身。")]
    [Min(0f)] public float scatterRadius = 3f;
    [Tooltip("刷新点到玩家的最小距离（米）——别在玩家面前刷。默认对齐 A 半径。")]
    [Min(0f)] public float minSpawnDistanceToPlayer = 20f;
    [Tooltip("刷新点到玩家的最大距离（米）——须在 B 缓冲带内。默认对齐 B 半径。")]
    [Min(0f)] public float maxSpawnDistanceToPlayer = 50f;

    [Header("视野外提示")]
    [Tooltip("战斗怪 ≤ 该数量才产生边缘提示（满屏遭遇时提示无意义）。")]
    [Min(1)] public int edgeIndicatorMaxCombat = 10;
    [Tooltip("持续视野外超过该时长（秒）才纳入提示列表。")]
    [Min(0f)] public float edgeOutOfViewSeconds = 2f;

    [Header("节奏")]
    [Tooltip("低频维护间隔（秒）：追踪列表修剪 / 视野外列表刷新 / 孤儿怪兜底激活。")]
    [Min(0.05f)] public float upkeepInterval = 0.25f;

    [Header("调试")]
    [Tooltip("Scene 视图绘制每只已刷怪的位置圆点（绿=激活，蓝=休眠）+ 每 Chunk 计数标签。")]
    public bool showGizmos = true;
    [Tooltip("刷怪 / 回收 / 校验拒绝时输出 Debug.Log。")]
    public bool logSpawns = true;
    [Tooltip("屏幕右上角显示配额与战斗怪计数面板。")]
    public bool showDebugHud = true;

    /// <summary>事件：怪物刷出（monster, chunk）。</summary>
    public event Action<MonsterActor, ChunkCoord> OnMonsterSpawned;
    /// <summary>事件：怪物回收（monster, chunk）。</summary>
    public event Action<MonsterActor, ChunkCoord> OnMonsterRecycled;

    /// <summary>视野外战斗怪列表：EdgeIndicatorUI 数据源，upkeepInterval 频率刷新。</summary>
    public IReadOnlyList<MonsterActor> OffscreenCombatMonsters => offscreenCombat;
    /// <summary>当前战斗怪总数（激活 AI + 已索敌玩家； 阈值判定用）。</summary>
    public int CombatMonsterCount { get; private set; }
    /// <summary>当前在场怪物总数（Active + Dormant，含未回收的倒地尸体）。</summary>
    public int TrackedMonsterCount { get; private set; }

    readonly Dictionary<ChunkCoord, List<MonsterActor>> trackedByChunk = new Dictionary<ChunkCoord, List<MonsterActor>>();
    readonly List<MonsterActor> offscreenCombat = new List<MonsterActor>();
    readonly Dictionary<MonsterActor, float> outOfViewSince = new Dictionary<MonsterActor, float>();

    /// <summary>追踪信息：归属 Chunk + 来源 prefab + 是否波次怪。</summary>
    struct TrackedInfo
    {
        public ChunkCoord homeChunk;
        public GameObject prefab;
        /// <summary>波次怪：永不随 Chunk 休眠/回收/写快照，退场由 WaveManager 波次系统裁决。</summary>
        public bool isWaveMonster;
    }
    readonly Dictionary<MonsterActor, TrackedInfo> trackInfoByMonster = new Dictionary<MonsterActor, TrackedInfo>();

    // 低频维护复用缓冲（避免每 0.25s 分配）
    readonly List<MonsterActor> seenBuffer = new List<MonsterActor>();
    readonly List<MonsterActor> keyPruneBuffer = new List<MonsterActor>();
    readonly List<ChunkCoord> chunkPruneBuffer = new List<ChunkCoord>();
    readonly List<MonsterActor> disengageBuffer = new List<MonsterActor>();

    float nextUpkeepTime;
    Camera mainCamera;
    Transform playerFallback;
    PossessionManager subscribedPossessionManager;

    void Awake(){
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MonsterSpawner] 重复实例，销毁后者。");
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start(){
        var system = MapStreamingSystem.Instance;
        if (system == null)
        {
            Debug.LogWarning("[MonsterSpawner] 场景中无 MapStreamingSystem，刷怪调度不启动（本组件空转）。");
            return;
        }
        system.OnChunkStateChanged += HandleChunkStateChanged;

        // 身体消耗计数：附身成功即计入怪物归属 Chunk 的快照
        subscribedPossessionManager = PossessionManager.Instance;
        if (subscribedPossessionManager != null)
            subscribedPossessionManager.OnPossessionStarted += HandlePossessionStarted;

        // 订阅前已就位的 Chunk 补齐处理（出生区同步准备可能早于本组件 Start）：
        // Dormant → 补刷怪/恢复；Active → 补刷怪/恢复 + 激活。
        foreach (var kv in system.Registry)
        {
            if (kv.Value.State == ChunkStreamState.Dormant)
            {
                SpawnOrRestoreChunkMonsters(kv.Value);
            }
            else if (kv.Value.State == ChunkStreamState.Active)
            {
                SpawnOrRestoreChunkMonsters(kv.Value);
                SetChunkAIActive(kv.Key, true);
            }
        }
    }

    void OnDestroy(){
        if (Instance == this) Instance = null;
        if (MapStreamingSystem.Instance != null)
            MapStreamingSystem.Instance.OnChunkStateChanged -= HandleChunkStateChanged;
        if (subscribedPossessionManager != null)
            subscribedPossessionManager.OnPossessionStarted -= HandlePossessionStarted;
    }

    void Update(){
        if (Time.unscaledTime < nextUpkeepTime) return;
        nextUpkeepTime = Time.unscaledTime + upkeepInterval;

        PruneTracked();
        RecycleDisengagedDistantMonsters(); //  脱战远距离回收（轻量版）
        RefreshOffscreenCombat();
        ReactivateOrphansNearPlayer();
    }

    // ── Chunk 状态切换响应 ──

    void HandleChunkStateChanged(ChunkCoord coord, ChunkStreamState oldState, ChunkStreamState newState)
    {
        var system = MapStreamingSystem.Instance;
        if (system == null) return;

        switch (newState)
        {
            case ChunkStreamState.Dormant:
                if (oldState == ChunkStreamState.Prepared)
                {
                    if (system.Registry.TryGetValue(coord, out var chunk))
                        SpawnOrRestoreChunkMonsters(chunk); // 有快照恢复，无快照首刷
                }
                else if (oldState == ChunkStreamState.Active)
                {
                    SetChunkAIActive(coord, false);
                }
                break;
            case ChunkStreamState.Active:
                if (oldState == ChunkStreamState.Dormant)
                    SetChunkAIActive(coord, true);
                break;
            case ChunkStreamState.Prepared:
                if (oldState == ChunkStreamState.Dormant)
                    RecycleChunkMonsters(coord);
                break;
            case ChunkStreamState.Unloaded:
                RecycleChunkMonsters(coord); // 兜底：Dormant 直跳 Unloaded 时也要回收
                break;
        }
    }

    // ── 刷怪与恢复 ──

    /// <summary>
    /// 进 B 分流：该 Chunk 已有 ChunkState（摇过波次，spawnedWaveIds 非空）→ 按快照恢复，不重摇（约束 3）；
    /// 首次进入（无 ChunkState）→ 正常抽波刷怪并登记 spawnedWaveIds。
    /// </summary>
    void SpawnOrRestoreChunkMonsters(ChunkRuntime chunk)
    {
        if (!enableChunkStaticSpawns) return; // 波次模式：地图不刷静态怪，场上所有怪物由 WaveManager 波次逻辑驱动
        var system = MapStreamingSystem.Instance;
        if (system != null && system.States.TryGet(chunk.Coord, out var state) && state.spawnedWaveIds.Count > 0)
        {
            RestoreChunkMonsters(chunk, state);
            return;
        }
        SpawnChunkMonsters(chunk);
    }

    /// <summary>
    /// 首刷（首次进 B）：收集刷怪点 → 逐点 4 项校验 → 权重抽波 → MonsterPool 刷出（AI 休眠）。
    /// 抽中波次即登记 ChunkState.spawnedWaveIds（重入不重摇，约束 3）——
    /// 含配额中断只刷出一半的波次：已摇即视为已确定，剩余名额本局不再补（保守解读，见交付决策）。
    /// </summary>
    void SpawnChunkMonsters(ChunkRuntime chunk)
    {
        if (chunk == null || chunk.Tiles == null) return;
        var def = chunk.Def;
        if (def == null || def.waveTable == null || def.waveTable.Count == 0) return; // 无刷怪表：该 Chunk 不刷怪
        // 初始扫描/事件重入防重：该 Chunk 已有在追踪的怪则不重复刷
        if (trackedByChunk.TryGetValue(chunk.Coord, out var existing) && existing.Count > 0) return;
        // 防御：摇过波次的 Chunk 应走恢复路径（SpawnOrRestore 已分流），此处兜底防重摇
        var streamingSystem = MapStreamingSystem.Instance;
        if (streamingSystem != null && streamingSystem.States.TryGet(chunk.Coord, out var st) && st.spawnedWaveIds.Count > 0) return;

        Vector3 player = GetPlayerPosition();
        var points = CollectSpawnPoints(chunk);
        int spawnedWaves = 0;
        for (int i = 0; i < points.Count && spawnedWaves < maxWavesPerChunk; i++)
        {
            if (TrackedMonsterCount >= maxCombatMonsters)
            {
                if (logSpawns) Debug.Log($"[MonsterSpawner] {chunk.Coord} 全场怪物达上限（{maxCombatMonsters}），剩余刷怪点跳过。");
                break;
            }
            if (!ValidateSpawnPoint(chunk, points[i], player)) continue;
            var wave = PickWave(def.waveTable);
            if (wave == null) continue;
            spawnedWaves++;
            RecordSpawnedWave(chunk.Coord, wave);
            SpawnWave(chunk, wave, points[i]);
        }
    }

    /// <summary>抽中即登记波次 id（ChunkState.spawnedWaveIds 只增不减）。去重键：MonsterWaveDef.id，空则回退资产名。</summary>
    void RecordSpawnedWave(ChunkCoord coord, MonsterWaveDef wave)
    {
        var system = MapStreamingSystem.Instance;
        if (system == null) return;
        var state = system.States.GetOrCreate(coord);
        string key = WaveKey(wave);
        if (!state.spawnedWaveIds.Contains(key)) state.spawnedWaveIds.Add(key);
    }

    static string WaveKey(MonsterWaveDef wave)
    {
        return !string.IsNullOrEmpty(wave.id) ? wave.id : wave.name;
    }

    /// <summary>
    /// 恢复：按 MonsterSnapshot 逐只从 MonsterPool 刷出，
    /// 覆盖血量/虚弱/倒地状态（MonsterActor.ApplyStreamSnapshot），AI 休眠待 Chunk 进 A 激活。
    /// 已恢复项从 state.monsters 移除（其语义 = 在池待恢复集合）；配额中断时剩余项保留，下次进 B 续恢复。
    /// TODO(Phase 4+): 尸体（state.corpses）/ 奖励（state.loots）/ 事件（state.events）恢复挂点。
    /// </summary>
    void RestoreChunkMonsters(ChunkRuntime chunk, ChunkState state)
    {
        if (chunk == null || state == null) return;
        int restored = 0;
        for (int i = state.monsters.Count - 1; i >= 0; i--)
        {
            if (TrackedMonsterCount >= maxCombatMonsters)
            {
                if (logSpawns) Debug.Log($"[MonsterSpawner] {chunk.Coord} 恢复达配额上限（{maxCombatMonsters}），剩余 {i + 1} 只保留快照待下次进 B 续恢复。");
                break;
            }
            var snap = state.monsters[i];
            var prefab = snap.prefabRef != null ? snap.prefabRef : ResolvePrefabById(chunk, snap.prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[MonsterSpawner] {chunk.Coord} 恢复失败：prefab '{snap.prefabId}' 无法解析，丢弃该快照。");
                state.monsters.RemoveAt(i);
                continue;
            }
            GameObject go = MonsterPool.Instance.Spawn(prefab, snap.position, Quaternion.identity);
            if (go == null)
            {
                state.monsters.RemoveAt(i);
                continue;
            }
            var monster = go.GetComponentInChildren<MonsterActor>(true);
            if (monster == null)
            {
                // 无 Actor 无法走 MonsterPool.Return 追踪回收，直接销毁防泄漏
                Debug.LogWarning($"[MonsterSpawner] prefab '{prefab.name}' 无 MonsterActor，已销毁跳过。");
                Destroy(go);
                state.monsters.RemoveAt(i);
                continue;
            }
            monster.ApplyStreamSnapshot(snap.currentHealth, snap.isWeakened, snap.isDowned);
            monster.playerDetected = snap.playerDetected;
            monster.aiActiveOverride = false;
            Track(chunk.Coord, monster, prefab);
            state.monsters.RemoveAt(i);
            restored++;
            OnMonsterSpawned?.Invoke(monster, chunk.Coord);
        }
        if (logSpawns && restored > 0)
            Debug.Log($"[MonsterSpawner] {chunk.Coord} 从快照恢复 {restored} 只怪（在场 {TrackedMonsterCount}/{maxCombatMonsters}，已刷波次不重摇）。");
    }

    /// <summary>按 prefabId 在该 Chunk 刷怪表内解析 prefab（prefabRef 内存引用丢失时的回退，如未来落盘读档）。</summary>
    static GameObject ResolvePrefabById(ChunkRuntime chunk, string prefabId)
    {
        var table = chunk.Def != null ? chunk.Def.waveTable : null;
        if (table == null || string.IsNullOrEmpty(prefabId)) return null;
        for (int w = 0; w < table.Count; w++)
        {
            var wave = table[w];
            if (wave == null) continue;
            for (int e = 0; e < wave.monsters.Count; e++)
            {
                var entry = wave.monsters[e];
                if (entry != null && entry.prefab != null && entry.prefab.name == prefabId)
                    return entry.prefab;
            }
        }
        return null;
    }

    /// <summary>
    /// 收集该 Chunk 的候选刷怪点（世界坐标）：
    /// 优先 TileData 网格中 MarkerLayer 标记为 SpawnPoint 的可走 Tile（玩法标记经 prefab 的 TileVisual 读取）；
    /// 没有则占位——Chunk 中心附近随机可走 Tile（种子由 chunk.Seed 决定，同 Chunk 结果稳定）。
    /// TODO: ChunkRuntime.spawnPoints（MarkerPoint 列表）接入后优先使用。
    /// TODO(策划): 给刷怪 Tile prefab 的 TileVisual 标 markerType = SpawnPoint，占位分支不再命中。
    /// </summary>
    List<Vector3> CollectSpawnPoints(ChunkRuntime chunk)
    {
        var points = new List<Vector3>();
        var tiles = chunk.Tiles;
        int n = tiles.GetLength(0);
        var system = MapStreamingSystem.Instance;

        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
        {
            var t = tiles[x, y];
            var visual = t.Visual;
            if (t.isWalkable && visual != null && visual.markerType == TileMarkerType.SpawnPoint)
                points.Add(system.TileCenterWorld(chunk.Coord, x, y));
        }
        if (points.Count > 0) return points;

        var rng = new System.Random((int)chunk.Seed);
        int center = n / 2;
        int range = Mathf.Max(1, n / 4);
        const int maxTries = 32;
        for (int tries = 0; tries < maxTries && points.Count < maxWavesPerChunk; tries++)
        {
            int x = Mathf.Clamp(center + rng.Next(-range, range + 1), 0, n - 1);
            int y = Mathf.Clamp(center + rng.Next(-range, range + 1), 0, n - 1);
            if (tiles[x, y].isWalkable)
                points.Add(system.TileCenterWorld(chunk.Coord, x, y));
        }
        return points;
    }

    /// <summary>4 项进场校验：距离 / 视野 / 地形 / 连通（占位）。</summary>
    bool ValidateSpawnPoint(ChunkRuntime chunk, Vector3 point, Vector3 player)
    {
        // 1. 距离：不在玩家面前刷（≥ A 半径），且在 B 缓冲带内
        float dist = Vector3.Distance(point, player);
        if (dist < minSpawnDistanceToPlayer || dist > maxSpawnDistanceToPlayer)
        {
            if (logSpawns) Debug.Log($"[MonsterSpawner] {chunk.Coord} 刷怪点距离校验失败：dist={dist:F1}，要求 [{minSpawnDistanceToPlayer}, {maxSpawnDistanceToPlayer}]。");
            return false;
        }

        // 2. 视野：不在玩家相机可见范围（viewport 内即视为可见；暂不做遮挡判定）
        var cam = GetMainCamera();
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(point);
            if (vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
            {
                if (logSpawns) Debug.Log($"[MonsterSpawner] {chunk.Coord} 刷怪点在玩家视野内，跳过。");
                return false;
            }
        }
        // else TODO: 无主相机（Camera.main 为空）时跳过视野校验——测试场景无 MainCamera tag 时放行

        // 3. 地形：Tile 可站
        if (!IsWalkable(chunk, point))
        {
            if (logSpawns) Debug.Log($"[MonsterSpawner] {chunk.Coord} 刷怪点地形不可走，跳过。");
            return false;
        }

        // 4. 连通（v1 弱化版）：当前仅复用上面的 isWalkable 校验。
        // TODO(Phase 4): NavMesh 采样校验（单 NavMeshSurface 增量烘焙接入后用 NavMesh.SamplePosition）。
        return true;
    }

    /// <summary>波次选择：按 spawnWeight 权重随机。权重和为 0 返回 null。</summary>
    static MonsterWaveDef PickWave(List<MonsterWaveDef> table)
    {
        float total = 0f;
        for (int i = 0; i < table.Count; i++)
            if (table[i] != null) total += Mathf.Max(0f, table[i].spawnWeight);
        if (total <= 0f) return null;

        float roll = UnityEngine.Random.Range(0f, total);
        for (int i = 0; i < table.Count; i++)
        {
            if (table[i] == null) continue;
            roll -= Mathf.Max(0f, table[i].spawnWeight);
            if (roll <= 0f) return table[i];
        }
        return table[table.Count - 1];
    }

    /// <summary>
    /// 按编队组成刷怪：monsters 列表逐条目刷 count 只，散布在刷怪点周边可走 Tile。
    /// 刷出即 Dormant（aiActiveOverride = false），待 Chunk 进 A 时激活。
    /// TODO(数值): wave.statMult 暂不应用——需先快照 prefab 基值（maxHealth/moveSpeed/collisionDamage），
    ///             否则池复用时倍率逐次累积污染；接入时需 MonsterActor 侧基值恢复机制。
    /// </summary>
    void SpawnWave(ChunkRuntime chunk, MonsterWaveDef wave, Vector3 point)
    {
        int spawned = 0;
        for (int e = 0; e < wave.monsters.Count; e++)
        {
            var entry = wave.monsters[e];
            if (entry == null || entry.prefab == null) continue;
            for (int i = 0; i < entry.count; i++)
            {
                if (TrackedMonsterCount >= maxCombatMonsters)
                {
                    if (logSpawns) Debug.Log($"[MonsterSpawner] {chunk.Coord} 波次 '{wave.id}' 刷至一半达配额上限，中断（已刷 {spawned}）。");
                    return;
                }
                Vector3 pos = FindScatterPosition(chunk, point);
                GameObject go = MonsterPool.Instance.Spawn(entry.prefab, pos, Quaternion.identity);
                if (go == null) continue;
                var monster = go.GetComponentInChildren<MonsterActor>(true);
                if (monster == null)
                {
                    // 无 Actor 无法走 MonsterPool.Return 追踪回收，直接销毁防泄漏
                    Debug.LogWarning($"[MonsterSpawner] prefab '{entry.prefab.name}' 无 MonsterActor，已销毁跳过。");
                    Destroy(go);
                    continue;
                }
                monster.aiActiveOverride = false;
                Track(chunk.Coord, monster, entry.prefab);
                spawned++;
                OnMonsterSpawned?.Invoke(monster, chunk.Coord);
            }
        }
        if (spawned == 0)
        {
            Debug.LogWarning($"[MonsterSpawner] {chunk.Coord} 波次 '{wave.id}' 无有效怪物条目（prefab 全为空或配额已满）。");
            return;
        }
        if (logSpawns)
            Debug.Log($"[MonsterSpawner] {chunk.Coord} 刷出波次 '{wave.id}' ×{spawned}（在场 {TrackedMonsterCount}/{maxCombatMonsters}）。");
    }

    /// <summary>刷怪点周边 scatterRadius 内随机取可走位置；多次失败落回刷怪点本身（已通过校验）。</summary>
    Vector3 FindScatterPosition(ChunkRuntime chunk, Vector3 center)
    {
        const int maxTries = 8;
        for (int i = 0; i < maxTries; i++)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle * scatterRadius;
            Vector3 candidate = center + new Vector3(offset.x, 0f, offset.y);
            if (IsWalkable(chunk, candidate)) return candidate;
        }
        return center;
    }

    // ── 波次玩法刷怪 API（WaveManager 驱动；与地图静态怪共用追踪/配额/回收体系） ──

    /// <summary>
    /// 波次刷怪：在玩家周围 B 带内寻找合法位置（距离/视野/地形校验）并刷出 1 只怪。
    /// AI 直接激活（不等 Chunk 进 A）；计入全场配额与追踪，死亡/回收由框架统一处理。
    /// </summary>
    /// <param name="prefab">怪物 prefab（须挂 MonsterActor）。</param>
    /// <param name="pos">刷怪世界坐标（由 TryGetWaveSpawnPosition 提供，或调用方自行保证合法）。</param>
    /// <returns>刷出的 MonsterActor；配额满 / prefab 无效 / 无 Actor 时返回 null。</returns>
    public MonsterActor SpawnWaveMonster(GameObject prefab, Vector3 pos)
    {
        if (prefab == null || TrackedMonsterCount >= maxCombatMonsters) return null;
        var system = MapStreamingSystem.Instance;
        ChunkCoord home = system != null ? system.WorldToChunk(pos) : default;

        GameObject go = MonsterPool.Instance.Spawn(prefab, pos, Quaternion.identity);
        if (go == null) return null;
        var monster = go.GetComponentInChildren<MonsterActor>(true);
        if (monster == null)
        {
            Debug.LogWarning($"[MonsterSpawner] 波次刷怪 prefab '{prefab.name}' 无 MonsterActor，已销毁跳过。");
            Destroy(go);
            return null;
        }
        monster.aiActiveOverride = true; // 波次怪直接激活索敌，且永不休眠（SetChunkAIActive 跳过波次怪）
        Track(home, monster, prefab, isWaveMonster: true); // 不随 Chunk 回收/写快照，退场由波次系统裁决
        OnMonsterSpawned?.Invoke(monster, home);
        if (logSpawns)
            Debug.Log($"[MonsterSpawner] 波次刷怪 '{prefab.name}' @ {home}（在场 {TrackedMonsterCount}/{maxCombatMonsters}）。");
        return monster;
    }

    /// <summary>
    /// 波次刷怪点查找：玩家周围 [minSpawnDistanceToPlayer, maxSpawnDistanceToPlayer] 环形带内
    /// 随机采样，要求落在已加载 Chunk（Registry 有条目、Tiles 就绪）的可走 Tile 上且不在相机视野内。
    /// </summary>
    public bool TryGetWaveSpawnPosition(out Vector3 pos)
    {
        pos = default;
        var system = MapStreamingSystem.Instance;
        if (system == null) return false;
        Vector3 player = GetPlayerPosition();
        var cam = GetMainCamera();
        for (int i = 0; i < 16; i++)
        {
            float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float r = UnityEngine.Random.Range(minSpawnDistanceToPlayer, maxSpawnDistanceToPlayer);
            Vector3 c = player + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            if (!system.Registry.TryGetValue(system.WorldToChunk(c), out var chunk) || chunk == null || chunk.Tiles == null)
                continue; // 该点归属 Chunk 未加载：跳过（地图边界外同样落此分支）
            if (cam != null)
            {
                Vector3 vp = cam.WorldToViewportPoint(c);
                if (vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f)
                    continue; // 视野内：不在玩家面前刷
            }
            if (!IsWalkable(chunk, c)) continue;
            pos = c;
            return true;
        }
        return false;
    }

    /// <summary>
    /// 回收单只波次怪（时间波结算清场用）：摘除追踪/配额计数后回池，不写 Chunk 快照
    /// （波次怪与地图静态怪分离，清场即永久退场，不会在快照恢复时重现）。
    /// </summary>
    public void RecycleWaveMonster(MonsterActor monster)
    {
        if (monster == null) return;
        ChunkCoord home = default;
        if (trackInfoByMonster.TryGetValue(monster, out var info))
        {
            home = info.homeChunk;
            if (trackedByChunk.TryGetValue(home, out var list)) list.Remove(monster);
            Untrack(monster);
        }
        MonsterPool.Instance.Return(monster);
        OnMonsterRecycled?.Invoke(monster, home);
    }

    /// <summary>世界坐标是否落在该 Chunk 的可走 Tile 上（经系统坐标换算，支持地图旋转）。</summary>
    bool IsWalkable(ChunkRuntime chunk, Vector3 worldPos)
    {
        var system = MapStreamingSystem.Instance;
        var tiles = chunk.Tiles;
        if (!system.WorldToTileLocal(worldPos, chunk.Coord, out int lx, out int ly)) return false;
        return tiles[lx, ly].isWalkable;
    }

    // ── AI 激活 / 休眠 ──

    void SetChunkAIActive(ChunkCoord coord, bool active)
    {
        if (!trackedByChunk.TryGetValue(coord, out var list)) return;
        int changed = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (!active && m.isPossessed) continue; // 附身中的怪永不休眠
            if (trackInfoByMonster.TryGetValue(m, out var ti) && ti.isWaveMonster) continue; // 波次怪永不休眠（波次系统固定激活）
            if (m.aiActiveOverride == active) continue;
            m.aiActiveOverride = active;
            changed++;
        }
        if (logSpawns && changed > 0)
            Debug.Log($"[MonsterSpawner] {coord} {(active ? "激活" : "休眠")} {changed} 只怪 AI。");
    }

    /// <summary>
    /// 兜底激活：追击跨 Chunk / 附身释放后的怪可能处于"近玩家但 AI 休眠"状态（归属 Chunk 在 A 外），
    /// 距玩家 &lt; minSpawnDistanceToPlayer 时强制激活，避免贴脸木桩。
    /// 与  脱战回收互补：近玩家激活、远玩家（脱战且视野外）回收写回。
    /// </summary>
    void ReactivateOrphansNearPlayer(){
        Vector3 player = GetPlayerPosition();
        foreach (var kv in trackedByChunk)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null || m.aiActiveOverride || m.isPossessed || m.isDowned) continue;
                if (Vector3.Distance(m.transform.position, player) < minSpawnDistanceToPlayer)
                    m.aiActiveOverride = true;
            }
        }
    }

    // ── 回收与快照 ──

    /// <summary>
    /// 离开 B/D 回收：先快照进 ChunkState，再把实例回 MonsterPool。
    /// 快照写入（内存快照，不落盘，）：存活怪 → state.monsters（追加语义，保留  脱战写回项）；
    /// 倒地/淡出尸体 → state.corpses（本批第一具尸体写入前重建列表）。
    /// 跳过并继续追踪（不写快照——它们仍在场）：附身中的怪、玩家贴脸的怪（防在眼前消失）。
    /// </summary>
    void RecycleChunkMonsters(ChunkCoord coord)
    {
        if (!trackedByChunk.TryGetValue(coord, out var list)) return;
        Vector3 player = GetPlayerPosition();
        var system = MapStreamingSystem.Instance;
        // 回收即快照：追踪中的怪必然来自本系统刷出/恢复，GetOrCreate 防御性保证快照不丢
        ChunkState state = system != null ? system.States.GetOrCreate(coord) : null;
        bool corpsesReset = false;
        int recycled = 0;
        var survivors = new List<MonsterActor>();
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null || !m.gameObject.activeInHierarchy)
            {
                // 已自行回池（尸体 fade）或销毁：随列表替换摘除，顺手保持配额计数一致（Phase 2 此处只摘不减，会泄漏计数）
                if (m != null) Untrack(m);
                else TrackedMonsterCount--;
                continue;
            }
            bool isWave = trackInfoByMonster.TryGetValue(m, out var info) && info.isWaveMonster;
            if (m.isPossessed || isWave)
            {
                survivors.Add(m); // 附身中的怪 / 波次怪：跳过并继续追踪（波次怪不随 Chunk 回收与写快照，退场由波次系统裁决）
                continue;
            }
            if (Vector3.Distance(m.transform.position, player) < minSpawnDistanceToPlayer)
            {
                survivors.Add(m);
                continue;
            }

            if (state != null)
            {
                if (m.isDowned || m.Body == MonsterActor.BodyState.Fading)
                {
                    // 尸体快照：本批重建（仅记录，恢复留 TODO）
                    if (!corpsesReset) { state.corpses.Clear(); corpsesReset = true; }
                    state.corpses.Add(new CorpseSnapshot
                    {
                        prefabId = info.prefab != null ? info.prefab.name : m.displayName,
                        position = ClampToHomeChunk(system, coord, m.transform.position),
                        looted = false,         // TODO(Phase 4+): 搜刮系统接入后写真实状态
                        consumedAsBody = false, // 附身消耗由 bodySupplyConsumed 计数兜底
                    });
                }
                else
                {
                    CaptureLiveMonster(system, coord, state, m, info.prefab);
                }
            }
            MonsterPool.Instance.Return(m);
            Untrack(m);
            recycled++;
            OnMonsterRecycled?.Invoke(m, coord);
        }
        if (survivors.Count > 0) trackedByChunk[coord] = survivors;
        else trackedByChunk.Remove(coord);

        if (logSpawns && recycled > 0)
            Debug.Log($"[MonsterSpawner] {coord} 快照并回收 {recycled} 只怪（在场 {TrackedMonsterCount}/{maxCombatMonsters}）。");
    }

    /// <summary>存活怪 → MonsterSnapshot 追加进 state.monsters（追加语义：保留  脱战写回与恢复配额剩余项）。</summary>
    void CaptureLiveMonster(MapStreamingSystem system, ChunkCoord home, ChunkState state, MonsterActor m, GameObject prefab)
    {
        state.monsters.Add(new MonsterSnapshot
        {
            prefabId = prefab != null ? prefab.name : m.displayName,
            prefabRef = prefab,
            position = ClampToHomeChunk(system, home, m.transform.position),
            currentHealth = m.currentHealth,
            maxHealth = m.maxHealth,
            isWeakened = m.isWeakened,
            isDowned = false,
            playerDetected = m.playerDetected,
        });
    }

    /// <summary>
    /// 快照位置钳制：跨 Chunk 追击的怪可能已跑出归属 Chunk，位置落回归属 Chunk 内稳定刷怪点
    ///。
    /// </summary>
    Vector3 ClampToHomeChunk(MapStreamingSystem system, ChunkCoord home, Vector3 pos)
    {
        if (system.WorldToChunk(pos) == home) return pos;
        if (system.Registry.TryGetValue(home, out var chunk) && chunk.Tiles != null)
        {
            var points = CollectSpawnPoints(chunk);
            if (points.Count > 0) return points[0];
        }
        return system.ChunkCenter(home); // 极端兜底（无 Tiles）：Chunk 中心
    }

    // ── 脱战远距离回收与身体消耗计数 ──

    /// <summary>
    ///  轻量版：脱战（AI 休眠或未索敌）且距玩家 > B 半径、不在相机视野内的怪，
    /// 快照写回归属 Chunk 的 ChunkState 后回收进池——从"当前战斗"配额释放（对齐策划："不占战斗上限"）。
    /// TODO(Phase 4): 归属 Chunk 仍在载时的"传送回 Chunk + 视线遮挡校验"实体化
    /// （当前以快照形式回家，该 Chunk 下次离开 B 再进 B 时恢复）。
    /// </summary>
    void RecycleDisengagedDistantMonsters(){
        var system = MapStreamingSystem.Instance;
        if (system == null || trackInfoByMonster.Count == 0) return;
        Vector3 player = GetPlayerPosition();
        float maxDist = system.radiusB;
        var cam = GetMainCamera();

        disengageBuffer.Clear();
        foreach (var kv in trackInfoByMonster)
        {
            var m = kv.Key;
            if (m == null || !m.gameObject.activeInHierarchy) continue;
            if (kv.Value.isWaveMonster) continue; // 波次怪不脱战回收（退场由波次系统裁决，避免跑路清空数量波）
            if (m.isPossessed || m.isDowned) continue; // 附身身体 Pin 保护；倒地尸体走 Chunk 回收路径
            if (m.Body == MonsterActor.BodyState.Fading || m.Body == MonsterActor.BodyState.Despawned) continue;
            if (m.aiActiveOverride && m.playerDetected) continue; // 仍在交战（激活且已索敌）
            if (Vector3.Distance(m.transform.position, player) <= maxDist) continue;
            if (IsInCameraView(cam, m.transform.position)) continue; // 玩家视野内禁止消失
            disengageBuffer.Add(m);
        }

        for (int i = 0; i < disengageBuffer.Count; i++)
        {
            var m = disengageBuffer[i];
            var info = trackInfoByMonster[m];
            var state = system.States.GetOrCreate(info.homeChunk);
            CaptureLiveMonster(system, info.homeChunk, state, m, info.prefab);
            if (trackedByChunk.TryGetValue(info.homeChunk, out var list)) list.Remove(m);
            MonsterPool.Instance.Return(m);
            Untrack(m);
            OnMonsterRecycled?.Invoke(m, info.homeChunk);
            if (logSpawns) Debug.Log($"[MonsterSpawner] 脱战远距离怪 '{m.displayName}' 写回 {info.homeChunk} 快照并回收。");
        }
    }

    /// <summary>相机视野判定（viewport 内即视为可见，不做遮挡；与刷怪视野校验同款近似）。</summary>
    static bool IsInCameraView(Camera cam, Vector3 worldPos)
    {
        if (cam == null) return false;
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        return vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
    }

    /// <summary>附身成功 → 归属 Chunk 的 bodySupplyConsumed +1（重入不重复发放身体）。非流送怪（调试刷出）不计。</summary>
    void HandlePossessionStarted(MonsterActor body)
    {
        if (body == null) return;
        if (!trackInfoByMonster.TryGetValue(body, out var info)) return;
        if (info.isWaveMonster) return; // 波次怪的身体不属于 Chunk 静态供给，不计数
        var system = MapStreamingSystem.Instance;
        if (system == null) return;
        int consumed = ++system.States.GetOrCreate(info.homeChunk).bodySupplyConsumed;
        if (logSpawns) Debug.Log($"[MonsterSpawner] {info.homeChunk} 身体消耗 +1（bodySupplyConsumed={consumed}，）。");
    }

    // ── 追踪与配额 ──

    void Track(ChunkCoord coord, MonsterActor monster, GameObject prefab)
        => Track(coord, monster, prefab, isWaveMonster: false);

    void Track(ChunkCoord coord, MonsterActor monster, GameObject prefab, bool isWaveMonster)
    {
        if (!trackedByChunk.TryGetValue(coord, out var list))
        {
            list = new List<MonsterActor>();
            trackedByChunk.Add(coord, list);
        }
        list.Add(monster);
        trackInfoByMonster[monster] = new TrackedInfo { homeChunk = coord, prefab = prefab, isWaveMonster = isWaveMonster };
        TrackedMonsterCount++;
    }

    /// <summary>摘除单个追踪项（配额计数 + trackInfo 清理）。trackedByChunk 列表项由调用方移除。</summary>
    void Untrack(MonsterActor monster)
    {
        trackInfoByMonster.Remove(monster);
        TrackedMonsterCount--;
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
                    if (m != null) Untrack(m);
                    else TrackedMonsterCount--; // 已销毁（Unity 假空）：trackInfo 查询无意义，仅减计数
                }
            }
            if (list.Count == 0) chunkPruneBuffer.Add(kv.Key);
        }
        for (int i = 0; i < chunkPruneBuffer.Count; i++)
            trackedByChunk.Remove(chunkPruneBuffer[i]);
    }

    // ── 视野外战斗怪列表 ──

    /// <summary>
    /// 低频刷新：统计战斗怪（激活 AI + 未附身 + 未倒地 + 已索敌玩家），
    /// 其中持续视野外 ≥ edgeOutOfViewSeconds 的进入提示列表；战斗怪 &gt; edgeIndicatorMaxCombat 时清空（满屏遭遇不提示）。
    /// </summary>
    void RefreshOffscreenCombat(){
        offscreenCombat.Clear();
        seenBuffer.Clear();
        var cam = GetMainCamera();
        int combat = 0;

        foreach (var kv in trackedByChunk)
        {
            var list = kv.Value;
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null || !m.gameObject.activeInHierarchy) continue;
                if (!m.aiActiveOverride || m.isPossessed || m.isDowned || !m.playerDetected) continue;
                combat++;
                if (cam == null) continue;
                Vector3 vp = cam.WorldToViewportPoint(m.transform.position);
                bool visible = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                if (!visible) seenBuffer.Add(m);
            }
        }
        CombatMonsterCount = combat;

        // 计时对账：本轮不再是"战斗怪且视野外"的怪，移除其累计时长
        keyPruneBuffer.Clear();
        foreach (var kv in outOfViewSince)
            if (!seenBuffer.Contains(kv.Key)) keyPruneBuffer.Add(kv.Key);
        for (int i = 0; i < keyPruneBuffer.Count; i++)
            outOfViewSince.Remove(keyPruneBuffer[i]);

        if (combat > edgeIndicatorMaxCombat)
        {
            outOfViewSince.Clear(); // 清掉累计，避免回落 ≤ 阈值时旧时长残留
            return;
        }

        float now = Time.time;
        for (int i = 0; i < seenBuffer.Count; i++)
        {
            var m = seenBuffer[i];
            if (outOfViewSince.TryGetValue(m, out float since))
            {
                if (now - since >= edgeOutOfViewSeconds) offscreenCombat.Add(m);
            }
            else
            {
                outOfViewSince.Add(m, now); // 首次出视野，开始计时
            }
        }
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
        if (!showDebugHud || !Application.isPlaying) return;

        var system = MapStreamingSystem.Instance;
        GUI.Box(new Rect(Screen.width - 250f, 10f, 240f, 94f), "MonsterSpawner");
        GUI.Label(new Rect(Screen.width - 242f, 32f, 224f, 18f), $"在场 {TrackedMonsterCount}/{maxCombatMonsters}（Active+Dormant）");
        GUI.Label(new Rect(Screen.width - 242f, 50f, 224f, 18f), $"战斗怪 {CombatMonsterCount}（提示阈值 ≤{edgeIndicatorMaxCombat}）");
        GUI.Label(new Rect(Screen.width - 242f, 68f, 224f, 18f), $"视野外提示 {offscreenCombat.Count} 只");
        GUI.Label(new Rect(Screen.width - 242f, 86f, 224f, 18f), $"快照 {(system != null ? system.States.Count : 0)} Chunk / Pin {(system != null ? system.Pins.PinnedChunkCount : 0)}");
    }
}
