using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 怪物生命周期基础设施（波次玩法驱动）：
///   - 波次怪刷出 / 取点 / 回收（API 由 WaveManager 调用）
///   - 全场配额（maxCombatMonsters）：波次怪与未来其他来源的怪共享
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
    [Tooltip("在场怪物上限（波次怪与未来其他来源的怪共享）。满时不再刷怪。")]
    [Min(1)] public int maxCombatMonsters = 30;

    [Header("波次取点")]
    [Tooltip("波次刷怪点到玩家的最小距离（米）——别在玩家面前刷。")]
    [Min(0f)] public float minSpawnDistanceToPlayer = 20f;
    [Tooltip("波次刷怪点到玩家的最大距离（米）——须在 B 缓冲带内。")]
    [Min(0f)] public float maxSpawnDistanceToPlayer = 50f;

    [Header("节奏")]
    [Tooltip("低频维护间隔（秒）：追踪列表修剪（尸体 fade 自行回池的怪摘除）。")]
    [Min(0.05f)] public float upkeepInterval = 0.25f;

    [Header("调试")]
    [Tooltip("Scene 视图绘制每只已刷怪的位置圆点（绿=激活，蓝=休眠）。")]
    public bool showGizmos = true;
    [Tooltip("刷怪 / 回收时输出 Debug.Log。")]
    public bool logSpawns = true;
    [Tooltip("屏幕右上角显示在场怪计数面板。")]
    public bool showDebugHud = true;

    /// <summary>当前在场怪物总数（含未回收的倒地尸体）。</summary>
    public int TrackedMonsterCount { get; private set; }

    /// <summary>
    /// 波次刷怪随机流（种子确定性）：由 WaveManager 每波设置（WorldSeed 派生），
    /// 取点/群系散射等全部随机走此流——同种子下怪物种类与位置可复现。
    /// 未设置时（直接调用方）回退全局 Random 语义。
    /// </summary>
    public System.Random WaveRandom { get; set; }

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

    readonly Dictionary<ChunkCoord, List<MonsterActor>> trackedByChunk = new Dictionary<ChunkCoord, List<MonsterActor>>();

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
    readonly List<ChunkCoord> chunkPruneBuffer = new List<ChunkCoord>();

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
    }

    // ── 波次玩法刷怪 API（WaveManager 驱动） ──

    /// <summary>
    /// 波次刷怪：在指定世界坐标刷出 1 只怪并计入全场配额与追踪。
    /// AI 直接激活索敌；不随 Chunk 休眠/回收/写快照，退场由波次系统裁决。
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
        monster.aiActiveOverride = true; // 波次怪直接激活索敌
        Track(home, monster, prefab, isWaveMonster: true); // 不随 Chunk 回收/写快照，退场由波次系统裁决
        if (logSpawns)
            Debug.Log($"[MonsterSpawner] 波次刷怪 '{prefab.name}' @ {home}（在场 {TrackedMonsterCount}/{maxCombatMonsters}）。");
        return monster;
    }

    /// <summary>
    /// 波次刷怪点查找：玩家周围 [minSpawnDistanceToPlayer, maxSpawnDistanceToPlayer] 环形带内
    /// 随机采样，要求落在已加载 Chunk（Registry 有条目、Tiles 就绪）的可走 Tile 上，
    /// 且不在玩家前方 60° 扇形内（侧面/后方允许，防止贴脸）。
    /// </summary>
    public bool TryGetWaveSpawnPosition(out Vector3 pos)
    {
        pos = default;
        var system = MapStreamingSystem.Instance;
        if (system == null) return false;
        Vector3 player = GetPlayerPosition();
        var cam = GetMainCamera();
        var rng = WaveRandom; // 种子随机流（WaveManager 注入）；null 时回退全局 Random
        for (int i = 0; i < 16; i++)
        {
            float angle = (rng != null ? rng.Next(0, 360) : UnityEngine.Random.Range(0, 360)) * Mathf.Deg2Rad;
            float r = rng != null
                ? minSpawnDistanceToPlayer + (float)rng.NextDouble() * (maxSpawnDistanceToPlayer - minSpawnDistanceToPlayer)
                : UnityEngine.Random.Range(minSpawnDistanceToPlayer, maxSpawnDistanceToPlayer);
            Vector3 c = player + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
            if (!system.Registry.TryGetValue(system.WorldToChunk(c), out var chunk) || chunk == null || chunk.Tiles == null)
                continue; // 该点归属 Chunk 未加载：跳过（地图边界外同样落此分支）
            if (cam != null)
            {
                // 只排除玩家前方 60° 扇形（别在正前方贴脸刷），侧面/后方允许——
                // 不能用整个视锥排除：小刷怪距离（如 5-15m）时采样点几乎全在屏幕内会被全部跳过。
                Vector3 toPoint = c - player;
                Vector3 fwd = cam.transform.forward;
                toPoint.y = 0f; fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.0001f && toPoint.sqrMagnitude > 0.0001f
                    && Vector3.Angle(fwd, toPoint) < 60f)
                    continue;
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
    }

    /// <summary>世界坐标是否落在该 Chunk 的可走 Tile 上（经系统坐标换算，支持地图旋转）。</summary>
    bool IsWalkable(ChunkRuntime chunk, Vector3 worldPos)
    {
        var system = MapStreamingSystem.Instance;
        var tiles = chunk.Tiles;
        if (!system.WorldToTileLocal(worldPos, chunk.Coord, out int lx, out int ly)) return false;
        return tiles[lx, ly].isWalkable;
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
        if (!showDebugHud || !Application.isPlaying || GameManager.IsFormalFlow) return; // 正式流程屏蔽刷怪面板

        GUI.Box(new Rect(Screen.width - 250f, 10f, 240f, 40f), "MonsterSpawner");
        GUI.Label(new Rect(Screen.width - 242f, 32f, 224f, 18f), $"剩怪 {TrackedMonsterCount} 只（在场，含休眠）");
    }
}
