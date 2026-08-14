using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 流送任务类型。枚举声明顺序即 JobKind 优先级（Activate 最高，UnloadFull 最低，）。
/// </summary>
public enum JobKind
{
    /// <summary>进 A：激活 AI/事件/战斗（玩家可见，优先级最高）。</summary>
    Activate = 0,
    /// <summary>离开 A：暂停 AI/动画/粒子 → Dormant（廉价）。</summary>
    Pause = 1,
    /// <summary>进 B：实例化场景对象 → Dormant（最贵的 Job）。</summary>
    Instantiate = 2,
    /// <summary>进 C：生成 TileData + 出入口校验 → Prepared（纯逻辑）。</summary>
    Prepare = 3,
    /// <summary>离开 B：卸载场景对象，保留逻辑数据 → Prepared。</summary>
    UnloadScene = 4,
    /// <summary>离开 D：保存 ChunkState 内存快照（Phase 3 落实体）。</summary>
    Serialize = 5,
    /// <summary>离开 D：回收对象池 → Unloaded（优先级最低）。</summary>
    UnloadFull = 6,
}

/// <summary>
/// 流送任务：入队时记录目标 Chunk 与玩家距离（同级内空间排序用，）。
/// </summary>
public struct MapStreamingJob
{
    public JobKind kind;
    public ChunkCoord coord;
    /// <summary>入队时玩家到 Chunk 中心距离（近者优先；前进方向加权留 TODO）。</summary>
    public float distanceToPlayer;
}

/// <summary>
/// 地图流送系统（单例）：四级范围（A/B/C/D）Diff + 任务队列 + 每帧预算驱动 Chunk 状态机。
/// Phase 1 骨架：范围判定/任务调度/状态机闭环可运行；
/// Phase 3 已接入：ChunkState 内存快照库（States，）与 PinRegistry（Pins，）——
/// 被 Pin 的 Chunk 离开 D 不卸载，解 Pin 且仍在 D 外时补发回收；
/// 怪物快照/恢复由 MonsterSpawner 监听状态切换驱动。
/// 真实 Tile 模板生成、场景实例化、NavMesh 为后续 Phase（见各 TODO）。
/// </summary>
public class MapStreamingSystem : MonoBehaviour
{
    /// <summary>全局唯一实例。</summary>
    public static MapStreamingSystem Instance { get; private set; }

    [Header("玩家引用")]
    [Tooltip("可选：手动指定玩家 Transform；为空时自动查找 PlayerController.Instance / tag=Player。")]
    public Transform playerOverride;

    [Header("四级流送范围（米，）——不变量：D > C（卸载滞后区）")]
    [Tooltip("A 可视范围：完整模拟（AI/事件/战斗）。")]
    public float radiusA = 25f;
    [Tooltip("B 缓冲范围：实例化场景对象，AI 待机待命。")]
    public float radiusB = 50f;
    [Tooltip("C 预加载范围：纯逻辑生成（TileData + 校验 + 资源预取）。")]
    public float radiusC = 60f;
    [Tooltip("D 卸载缓冲：C 之外 D 之内保留状态等待；离开 D 保存 → 回收。必须 > C。")]
    public float radiusD = 80f;

    [Header("网格尺寸")]
    [Tooltip("Chunk 边长（Tile 数），默认 8。")]
    [Min(1)] public int chunkSize = 8;
    [Tooltip("每 Tile 世界尺寸（米），默认 2。")]
    [Min(0.01f)] public float tileSize = 2f;

    [Header("世界种子（：Chunk 生成与 WorldPlan 区域解析共用）")]
    [Tooltip("单局种子。ChunkSeed = Hash(coord, worldSeed)，生成顺序无关。")]
    public uint worldSeed = 12345;
    [Tooltip("默认 Chunk 模板：WorldPlan 接入前所有 Chunk 共用；为空时占位生成器仅写 isWalkable 快照。")]
    public ChunkDef defaultChunkDef;

    [Header("WorldPlan（宏观区域；空 = 全部用 defaultChunkDef）")]
    [Tooltip("主题映射表：按 RegionDef.themeCenter 排序后对连续噪声值做最近邻查表。空列表 = 不启用 WorldPlan。")]
    public List<RegionDef> regionTable = new List<RegionDef>();
    [Tooltip("区域粒度：每个噪声采样点覆盖的 Chunk 边长（4 = 4×4 Chunk 一块主题区）。注意：2 在连续噪声下同主题邻接率仅 ~50%（棋盘格化），实测 ≥4 才达到 >60% 连续性验收（Phase A 数据裁决）。")]
    [Min(1)] public int regionCellSize = 4;
    [Tooltip("邻接权重加成：候选模板 preferredNeighbors 每命中一个已生成邻居 Def 的权重加成。")]
    public float regionNeighborWeight = 2f;

    [Header("Tick 节奏（低频轮询兜底）")]
    [Tooltip("范围集合重算间隔（秒）。事件触发（跨 Chunk 边界）留 TODO。")]
    [Min(0.02f)] public float tickInterval = 0.2f;

    [Header("每帧预算：个数上限 + 时间片兜底，两者取先触发者")]
    [Tooltip("Activate 每帧上限。")]
    [Min(1)] public int activatePerFrame = 2;
    [Tooltip("Pause 每帧上限。")]
    [Min(1)] public int pausePerFrame = 5;
    [Tooltip("Instantiate 每帧上限（最贵）。")]
    [Min(1)] public int instantiatePerFrame = 2;
    [Tooltip("Prepare 每帧上限。")]
    [Min(1)] public int preparePerFrame = 4;
    [Tooltip("UnloadScene 每帧上限。")]
    [Min(1)] public int unloadScenePerFrame = 2;
    [Tooltip("Serialize 每帧上限。")]
    [Min(1)] public int serializePerFrame = 2;
    [Tooltip("UnloadFull 每帧上限。")]
    [Min(1)] public int unloadFullPerFrame = 4;
    [Tooltip("流送队列单帧时间片预算（毫秒，）：超时立即停止出队，剩余任务顺延下帧。" +
             "个数预算防\"量\"，时间片防\"单价失控\"（如 Instantiate 遇复杂 Chunk），两者取先触发者。0 = 关闭时间片，仅按个数预算。")]
    [Min(0f)] public float timeBudgetMs = 4f;

    [Header("调试")]
    [Tooltip("Scene 视图绘制 A/B/C/D 范围圆 + Chunk 状态色块。")]
    public bool showGizmos = true;
    [Tooltip("Chunk 状态切换时输出 Debug.Log。")]
    public bool logStateChanges = true;

    /// <summary>事件：Chunk 状态切换（coord, oldState, newState）。</summary>
    public event Action<ChunkCoord, ChunkStreamState, ChunkStreamState> OnChunkStateChanged;
    /// <summary>事件：Chunk 进入 A（Active）。</summary>
    public event Action<ChunkCoord> OnChunkEnteredA;

    /// <summary>Chunk 注册表：coord → 运行时实例（含 Unloaded，供 Phase 3 挂 ChunkState）。</summary>
    public IReadOnlyDictionary<ChunkCoord, ChunkRuntime> Registry => registry;

    /// <summary>ChunkState 内存快照库（单局内持久，落盘仅显式存档）。由 MonsterSpawner 写入怪物快照。</summary>
    public ChunkStateStore States => states;
    /// <summary>Pin 注册表：被 Pin 的 Chunk 离开 D 不卸载。</summary>
    public PinRegistry Pins => pinRegistry;

    // ── 调试统计（MapDebugHUD 数据源，只读、零分配） ──

    /// <summary>任务队列中等待执行的任务数。</summary>
    public int QueuedJobCount => jobQueue.Count;
    /// <summary>本帧已执行的 Job 数（ProcessJobQueue 每帧重置）。</summary>
    public int JobsExecutedThisFrame { get; private set; }
    /// <summary>本帧任务队列总耗时（毫秒，含排序/过期校验；timeBudgetMs = 0 未计时恒为 0）。</summary>
    public float LastFrameQueueMs { get; private set; }
    /// <summary>本帧是否因时间片耗尽提前停止出队（剩余顺延，）。</summary>
    public bool TimeSliceExceeded { get; private set; }
    /// <summary>A/B/C/D 当前集合 Chunk 数。</summary>
    public int RangeACount => currentA.Count;
    /// <summary>A/B/C/D 当前集合 Chunk 数。</summary>
    public int RangeBCount => currentB.Count;
    /// <summary>A/B/C/D 当前集合 Chunk 数。</summary>
    public int RangeCCount => currentC.Count;
    /// <summary>A/B/C/D 当前集合 Chunk 数。</summary>
    public int RangeDCount => currentD.Count;
    /// <summary>玩家世界坐标（与流送判定同源的取值逻辑：override → PlayerController → tag=Player → 自身；内部本地坐标转世界输出）。</summary>
    public Vector3 PlayerWorldPosition => MapLocalToWorld(GetPlayerPosition());

    readonly Dictionary<ChunkCoord, ChunkRuntime> registry = new Dictionary<ChunkCoord, ChunkRuntime>();
    /// <summary>宏观区域图：regionTable 非空时首次 GetOrCreateChunk 懒构造；null = 全图 defaultChunkDef。</summary>
    WorldPlan worldPlan;
    /// <summary>全局模板分配器：协调「随机 ↔ 模板」融合与模板全局约束（mustGenerate/maxCount/weight），单局唯一。</summary>
    readonly ChunkTemplateAllocator templateAllocator = new ChunkTemplateAllocator();
    readonly List<MapStreamingJob> jobQueue = new List<MapStreamingJob>();
    readonly ChunkStateStore states = new ChunkStateStore();
    readonly PinRegistry pinRegistry = new PinRegistry();
    /// <summary>上 Tick 被 Pin 的 Chunk 快照：本 Tick 对比出"解 Pin 且仍在 D 外"的 Chunk 补发回收。</summary>
    readonly HashSet<ChunkCoord> pinnedLastTick = new HashSet<ChunkCoord>();

    HashSet<ChunkCoord> currentA = new HashSet<ChunkCoord>();
    HashSet<ChunkCoord> currentB = new HashSet<ChunkCoord>();
    HashSet<ChunkCoord> currentC = new HashSet<ChunkCoord>();
    HashSet<ChunkCoord> currentD = new HashSet<ChunkCoord>();

    float nextTickTime;
    Transform playerFallback;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[MapStreamingSystem] 重复实例，销毁后者。");
            Destroy(this);
            return;
        }
        Instance = this;
        // 坐标换算经 transform（支持整体平移/旋转），但不支持非均匀缩放：
        // 视觉解析式 bounds 校正假设父链 localScale = 1（ChunkVisualizer.PlaceBlock），缩放会致视觉错位。
        if (transform.lossyScale != Vector3.one)
            Debug.LogWarning($"[MapStreamingSystem] 本物体含缩放 {transform.lossyScale}：地图支持平移/旋转，不支持非均匀缩放（视觉 bounds 校正会失真）。建议缩放保持 1。", this);
    }

    void OnDestroy(){
        if (Instance == this) Instance = null;
    }

    void Update(){
        if (Time.time >= nextTickTime)
        {
            nextTickTime = Time.time + tickInterval;
            Tick();
        }
        ProcessJobQueue();
    }

    // ── 主循环 ──

    /// <summary>
    /// 重算 A/B/C/D 目标集合并与当前集合 Diff；无变化则短路返回（"需要刷新？"菱形）。
    /// </summary>
    void Tick(){
        Vector3 player = GetPlayerPosition();
        RefreshPins(player); // 动态 Pin（玩家/附身目标）先于离开 D 检查刷新

        var newA = ComputeASet(player);
        var newB = ChunksInRadius(player, radiusB);
        var newC = ChunksInRadius(player, radiusC);
        var newD = ChunksInRadius(player, radiusD); // D > C，形成卸载滞后区

        // 解 Pin 释放：
        // 上 Tick 被 Pin、本 Tick 已解 Pin 且仍在 D 外 → 补发回收任务。
        // 先于范围短路判断：Pin 变化本身不引起范围集合 Diff（如静止时结束附身），独立检查。
        foreach (var c in pinnedLastTick)
        {
            if (pinRegistry.IsPinned(c) || newD.Contains(c)) continue;
            Enqueue(JobKind.Serialize, c, player);
            Enqueue(JobKind.UnloadFull, c, player);
        }
        pinnedLastTick.Clear();
        foreach (var c in pinRegistry.PinnedCoords) pinnedLastTick.Add(c);

        if (currentA.SetEquals(newA) && currentB.SetEquals(newB)
            && currentC.SetEquals(newC) && currentD.SetEquals(newD)) return;

        EnqueueDiff(newC, currentC, JobKind.Prepare, player);       // 进 C
        EnqueueDiff(newB, currentB, JobKind.Instantiate, player);   // 进 B
        EnqueueDiff(newA, currentA, JobKind.Activate, player);      // 进 A
        EnqueueDiff(currentA, newA, JobKind.Pause, player);         // 离开 A → Dormant
        EnqueueDiff(currentB, newB, JobKind.UnloadScene, player);   // 离开 B → Prepared
        foreach (var c in Difference(currentD, newD))               // 离开 D
        {
            if (IsPinned(c)) continue;                              // 被 Pin → 保持加载，等解 Pin
            Enqueue(JobKind.Serialize, c, player);                  //   保存 ChunkState（内存快照）
            Enqueue(JobKind.UnloadFull, c, player);                 //   回收 → Unloaded
        }

        currentA = newA;
        currentB = newB;
        currentC = newC;
        currentD = newD;
    }

    /// <summary>
    /// A 集合：视锥投影 ∪ 玩家所在 Chunk 及直接邻接（保底）。
    /// Phase 1 用半径近似视锥，真实视锥投影留 TODO（复用于刷怪视野校验）。
    /// </summary>
    HashSet<ChunkCoord> ComputeASet(Vector3 player)
    {
        // TODO(Phase 1 后续): 相机视锥与地面相交覆盖的 Chunk 集合，替换半径近似
        var set = ChunksInRadius(player, radiusA);
        var playerChunk = ChunkFromLocal(player);
        set.Add(playerChunk);
        foreach (var dir in ChunkCoord.AllDirections)
            set.Add(playerChunk.Neighbor(dir));
        return set;
    }

    /// <summary>玩家到 Chunk 中心距离 ≤ radius 的全部 Chunk（按中心距离判定，整体进出；player 为地图本地坐标）。</summary>
    HashSet<ChunkCoord> ChunksInRadius(Vector3 player, float radius)
    {
        var set = new HashSet<ChunkCoord>();
        float chunkWorld = chunkSize * tileSize;
        int minX = Mathf.FloorToInt((player.x - radius) / chunkWorld);
        int maxX = Mathf.FloorToInt((player.x + radius) / chunkWorld);
        int minY = Mathf.FloorToInt((player.z - radius) / chunkWorld);
        int maxY = Mathf.FloorToInt((player.z + radius) / chunkWorld);
        for (int x = minX; x <= maxX; x++)
        for (int y = minY; y <= maxY; y++)
        {
            var c = new ChunkCoord(x, y);
            if (Vector3.Distance(ChunkLocalCenter(c), player) <= radius) set.Add(c);
        }
        return set;
    }

    // ── 任务队列 ──

    void EnqueueDiff(HashSet<ChunkCoord> added, HashSet<ChunkCoord> removed, JobKind kind, Vector3 player)
    {
        foreach (var c in Difference(added, removed))
            Enqueue(kind, c, player);
    }

    static List<ChunkCoord> Difference(HashSet<ChunkCoord> a, HashSet<ChunkCoord> b)
    {
        var result = new List<ChunkCoord>();
        foreach (var c in a)
            if (!b.Contains(c)) result.Add(c);
        return result;
    }

    void Enqueue(JobKind kind, ChunkCoord coord, Vector3 player)
    {
        // 同类同 Chunk 去重；反向任务由执行前的过期校验兜底
        for (int i = 0; i < jobQueue.Count; i++)
            if (jobQueue[i].kind == kind && jobQueue[i].coord == coord) return;
        jobQueue.Add(new MapStreamingJob
        {
            kind = kind,
            coord = coord,
            // player 为地图本地坐标（Tick 传入），用本地中心算距离
            distanceToPlayer = Vector3.Distance(ChunkLocalCenter(coord), player),
        });
    }

    /// <summary>
    /// 按两级排序（JobKind 优先级 → 距离近优先）执行队列，受每帧个数预算 + 时间片预算约束。
    /// 个数预算防"量"，时间片防"单价失控"（如 Instantiate 遇复杂 Chunk），两者取先触发者；
    /// timeBudgetMs = 0 关闭时间片仅按个数预算。预算耗尽的未执行任务原地压缩顺延下帧。
    /// TODO(Phase 4 后续): 空间优先级加"玩家所在 > 前进方向"权重。
    /// </summary>
    void ProcessJobQueue(){
        JobsExecutedThisFrame = 0;
        LastFrameQueueMs = 0f;
        TimeSliceExceeded = false;
        if (jobQueue.Count == 0) return;

        jobQueue.Sort(CompareJobs);

        // 每帧已执行计数（按 JobKind 索引）；正序遍历保证同 Kind 内距离近者优先，
        // 原地压缩保留"未过期但预算耗尽"的任务顺延到下帧
        Span<int> executed = stackalloc int[7];
        bool timed = timeBudgetMs > 0f;
        // 不用 using System.Diagnostics（避免 Debug 二义性），全限定名引用 Stopwatch
        var stopwatch = timed ? System.Diagnostics.Stopwatch.StartNew() : null;
        int write = 0, read = 0;
        for (; read < jobQueue.Count; read++)
        {
            var job = jobQueue[read];
            if (IsJobStale(job)) continue; // 过期丢弃
            if (!IsJobReady(job))
            {
                // 链式任务前置未就绪（如 Instantiate 等 Prepare、Activate 等 Instantiate）：
                // 保留等待而非丢弃——否则出生首帧同批入队的任务会被排序+过期校验误杀，Chunk 卡死
                jobQueue[write++] = job;
                continue;
            }
            if (executed[(int)job.kind] >= BudgetOf(job.kind))
            {
                jobQueue[write++] = job;
                continue;
            }
            // 时间片兜底：超时立即停止出队，剩余任务（含尚未做过期校验的）整体顺延下帧
            if (timed && stopwatch.Elapsed.TotalMilliseconds >= timeBudgetMs)
            {
                TimeSliceExceeded = true;
                break;
            }
            ExecuteJob(job);
            executed[(int)job.kind]++;
            JobsExecutedThisFrame++;
        }
        // 时间片耗尽提前 break 时：本帧剩余任务原样保留（过期校验下帧统一做）
        for (; read < jobQueue.Count; read++) jobQueue[write++] = jobQueue[read];
        jobQueue.RemoveRange(write, jobQueue.Count - write);
        if (timed)
        {
            stopwatch.Stop();
            LastFrameQueueMs = (float)stopwatch.Elapsed.TotalMilliseconds;
        }
    }

    static int CompareJobs(MapStreamingJob a, MapStreamingJob b)
    {
        int byKind = a.kind.CompareTo(b.kind);
        return byKind != 0 ? byKind : a.distanceToPlayer.CompareTo(b.distanceToPlayer);
    }

    int BudgetOf(JobKind kind)
    {
        switch (kind)
        {
            case JobKind.Activate: return activatePerFrame;
            case JobKind.Pause: return pausePerFrame;
            case JobKind.Instantiate: return instantiatePerFrame;
            case JobKind.Prepare: return preparePerFrame;
            case JobKind.UnloadScene: return unloadScenePerFrame;
            case JobKind.Serialize: return serializePerFrame;
            case JobKind.UnloadFull: return unloadFullPerFrame;
            default: return 1;
        }
    }

    /// <summary>
    /// 过期任务取消：执行前校验 Chunk 当前期望状态，目标已被后续 Tick 改变的直接丢弃。
    /// </summary>
    bool IsJobStale(MapStreamingJob job)
    {
        registry.TryGetValue(job.coord, out var chunk);
        var state = chunk != null ? chunk.State : ChunkStreamState.None;
        switch (job.kind)
        {
            case JobKind.Prepare:
                return !currentC.Contains(job.coord)
                       || (state != ChunkStreamState.None && state != ChunkStreamState.Unloaded);
            case JobKind.Instantiate:
                // 前置（Prepare）未完成的状态（None）不算过期，由 IsJobReady 保留等待
                return !currentB.Contains(job.coord)
                       || state == ChunkStreamState.Dormant || state == ChunkStreamState.Active
                       || state == ChunkStreamState.Unloaded;
            case JobKind.Activate:
                // 前置（Prepare/Instantiate）未完成的状态（None/Prepared）不算过期，保留等待
                return !currentA.Contains(job.coord)
                       || state == ChunkStreamState.Active || state == ChunkStreamState.Unloaded;
            case JobKind.Pause:
                return currentA.Contains(job.coord) || state != ChunkStreamState.Active;
            case JobKind.UnloadScene:
                return currentB.Contains(job.coord) || state != ChunkStreamState.Dormant;
            case JobKind.Serialize:
                // Pin 防御：入队后执行前被 Pin（如附身完成瞬间）→ 丢弃；解 Pin 后由 Tick 的解 Pin 释放补发
                return currentD.Contains(job.coord) || pinRegistry.IsPinned(job.coord)
                       || (state != ChunkStreamState.Dormant && state != ChunkStreamState.Prepared);
            case JobKind.UnloadFull:
                return currentD.Contains(job.coord) || pinRegistry.IsPinned(job.coord)
                       || (state != ChunkStreamState.Dormant && state != ChunkStreamState.Prepared);
            default:
                return true;
        }
    }

    /// <summary>
    /// 链式任务就绪判定：仅 Instantiate/Activate 有前置状态依赖，
    /// 前置未完成时任务保留在队列等待（见 ProcessJobQueue），完成后由后续帧自动执行。
    /// </summary>
    bool IsJobReady(MapStreamingJob job)
    {
        registry.TryGetValue(job.coord, out var chunk);
        var state = chunk != null ? chunk.State : ChunkStreamState.None;
        switch (job.kind)
        {
            case JobKind.Instantiate: return state == ChunkStreamState.Prepared;
            case JobKind.Activate: return state == ChunkStreamState.Dormant;
            default: return true; // 其余任务无前置依赖
        }
    }

    void ExecuteJob(MapStreamingJob job)
    {
        switch (job.kind)
        {
            case JobKind.Prepare: Prepare(job.coord); break;
            case JobKind.Instantiate: Instantiate(job.coord); break;
            case JobKind.Activate: Activate(job.coord); break;
            case JobKind.Pause: Pause(job.coord); break;
            case JobKind.UnloadScene: UnloadScene(job.coord); break;
            case JobKind.Serialize: SerializeChunkState(job.coord); break;
            case JobKind.UnloadFull: UnloadFull(job.coord); break;
        }
    }

    // ── Job 实现（Phase 1 占位；真实行为见各 TODO） ──

    /// <summary>
    /// Prepare（进 C）：生成 TileData 网格 + 出入口校验（≥2 可通行邻接边，）。
    /// 邻接未知视为未校验但暂不报错（邻居后续生成时被边界签名强制约束， 签名本身留 TODO）。
    /// 校验失败重摇最多 3 次，仍失败走全 Normal 安全兜底。
    /// 注：Tile 级方块地形方案下边沿恒 Normal 四边恒开，校验恒一次通过（结构保留给未来真实生成器）。
    /// </summary>
    void Prepare(ChunkCoord coord)
    {
        var chunk = GetOrCreateChunk(coord);

        const int maxRetries = 3; // 
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            uint seed = ChunkSeed(coord, attempt);
            var openEdges = GenerateTilesPlaceholder(chunk, seed);
            if (openEdges.Count >= 2)
            {
                chunk.TransitionTo(ChunkStreamState.Prepared);
                return;
            }
        }

        // 兜底：全部 Tile 强制 Normal（TODO Phase 2+: 替换为策划安全预制 ChunkDef；并上报 telemetry）
        Debug.LogWarning($"[MapStreamingSystem] {coord} 出入口校验重摇 {maxRetries} 次仍失败，全部 Tile 强制 Normal 兜底。");
        ForceFallbackOpenings(chunk);
        chunk.TransitionTo(ChunkStreamState.Prepared);
    }

    /// <summary>
    /// Instantiate（进 B）：实例化场景对象 → Dormant。
    /// 仅切换状态：Tile 方块视觉实例化由 ChunkVisualizer 监听状态切换驱动（视觉渲染 v2，本方法不直接处理）。
    /// </summary>
    void Instantiate(ChunkCoord coord)
    {
        if (!registry.TryGetValue(coord, out var chunk)) return;
        // 怪物刷出 / ChunkState 快照恢复由 MonsterSpawner 监听状态切换驱动（，本方法不直接处理）
        // TODO(Phase 2+ 远期): 视觉层若改共享 Tilemap / 合并静态网格，在此接管实例化
        // TODO(Phase 4): 该 Chunk 包围盒 NavMeshSurface.UpdateNavMesh 异步局部烘焙
        chunk.TransitionTo(ChunkStreamState.Dormant);
    }

    /// <summary>Activate（进 A）：完整模拟。Phase 1 仅状态切换 + 日志。</summary>
    void Activate(ChunkCoord coord)
    {
        if (!registry.TryGetValue(coord, out var chunk)) return;
        // 怪物 AI 激活由 MonsterSpawner 监听状态切换驱动（aiActiveOverride = true，）；
        // 事件 / 动画粒子恢复 TODO(Phase 2 后续)
        if (chunk.TransitionTo(ChunkStreamState.Active))
        {
            if (logStateChanges) Debug.Log($"[MapStreamingSystem] {coord} → Active");
            OnChunkEnteredA?.Invoke(coord);
        }
    }

    /// <summary>Pause（离开 A）：暂停 AI/动画/粒子，保留遭遇进度 → Dormant。</summary>
    void Pause(ChunkCoord coord)
    {
        if (!registry.TryGetValue(coord, out var chunk)) return;
        // AI 降频休眠由 MonsterSpawner 监听状态切换驱动（aiActiveOverride = false，AIController 0.5s/tick，）；
        // 暂停动画/粒子 TODO(Phase 2 后续)
        if (chunk.TransitionTo(ChunkStreamState.Dormant) && logStateChanges)
            Debug.Log($"[MapStreamingSystem] {coord} → Dormant（离开 A）");
    }

    /// <summary>UnloadScene（离开 B）：卸载场景对象，保留逻辑数据 → Prepared。</summary>
    void UnloadScene(ChunkCoord coord)
    {
        if (!registry.TryGetValue(coord, out var chunk)) return;
        // TODO(Phase 2 后续): 卸载场景对象（Tilemap 清除 / 网格回池）
        // 怪物快照进 ChunkState + 实例回 MonsterPool 由 MonsterSpawner 监听状态切换驱动（，Phase 3 已实装）
        // TODO(Phase 4): 同包围盒 NavMeshSurface.UpdateNavMesh 移除局部 NavMesh
        if (chunk.TransitionTo(ChunkStreamState.Prepared) && logStateChanges)
            Debug.Log($"[MapStreamingSystem] {coord} → Prepared（离开 B）");
    }

    /// <summary>
    /// Serialize（离开 D）：ChunkState 内存快照的编排确认。
    /// 怪物/尸体快照已在回收时由 MonsterSpawner 写入（离开 B 即快照，见 MonsterSpawner.RecycleChunkMonsters）；
    /// 本 Job 不再重复采集，仅确认快照就位，并预留尸体搜刮/奖励/事件子系统的写入挂点。
    /// </summary>
    void SerializeChunkState(ChunkCoord coord)
    {
        if (!states.TryGet(coord, out var state))
        {
            // 从未产生动态内容（未刷过怪）的 Chunk 无快照需求：TileData 由种子确定性重生成
            if (logStateChanges) Debug.Log($"[MapStreamingSystem] {coord} 离开 D：无动态状态，无需快照。");
            return;
        }
        // TODO(Phase 4+): 尸体搜刮 / 奖励拾取 / 事件进度子系统接入后在此补拍
        if (logStateChanges)
            Debug.Log($"[MapStreamingSystem] {coord} 离开 D：ChunkState 内存快照已就位（待恢复怪 {state.monsters.Count}，尸体 {state.corpses.Count}，已刷波次 {state.spawnedWaveIds.Count}，身体消耗 {state.bodySupplyConsumed}）。");
    }

    /// <summary>UnloadFull（离开 D）：回收 → Unloaded。</summary>
    void UnloadFull(ChunkCoord coord)
    {
        if (!registry.TryGetValue(coord, out var chunk)) return;
        // TODO(Phase 2): 表现/碰撞/动态对象回收对象池
        if (chunk.TransitionTo(ChunkStreamState.Unloaded) && logStateChanges)
            Debug.Log($"[MapStreamingSystem] {coord} → Unloaded（离开 D）");
    }

    /// <summary>Pin 检查：被 Pin 的 Chunk 离开 D 不卸载。来源注册见 PinRegistry（玩家/附身目标/Boss 事件）。</summary>
    bool IsPinned(ChunkCoord coord)
    {
        return pinRegistry.IsPinned(coord);
    }

    /// <summary>
    /// 动态 Pin 刷新：玩家所在 Chunk（防御性兜底——附身期间锚点是灵魂位置，见 GetPlayerPosition）
    /// + 附身目标（PossessionManager.CurrentBody）所在 Chunk（身体跨 Chunk 移动时原 Chunk 不得卸载）。
    /// TODO(Phase 5): Boss 事件 Chunk 走 PinRegistry.PinBoss / UnpinBoss（Boss 战期间强制 Active）。
    /// </summary>
    void RefreshPins(Vector3 player)
    {
        // player 为地图本地坐标（Tick 传入）；附身目标位置是世界坐标（走 WorldToChunk 世界入口）
        pinRegistry.SetDynamicPin(PinRegistry.SourcePlayer, ChunkFromLocal(player));

        var pm = PossessionManager.Instance;
        if (pm != null && pm.CurrentBody != null)
            pinRegistry.SetDynamicPin(PinRegistry.SourcePossession, WorldToChunk(pm.CurrentBody.transform.position));
        else
            pinRegistry.SetDynamicPin(PinRegistry.SourcePossession, null);
    }

    // ── Prepare 占位生成器 ──

    ChunkRuntime GetOrCreateChunk(ChunkCoord coord)
    {
        if (registry.TryGetValue(coord, out var chunk)) return chunk;
        // WorldPlan 懒构造：regionTable 为空时保持全图 defaultChunkDef 现状
        if (worldPlan == null && regionTable != null && regionTable.Count > 0)
            worldPlan = new WorldPlan(worldSeed, regionTable, regionCellSize) { neighborWeightBonus = regionNeighborWeight };
        var def = worldPlan != null ? worldPlan.ChunkDefOf(coord, registry) ?? defaultChunkDef : defaultChunkDef;
        chunk = new ChunkRuntime(coord, def, ChunkSeed(coord, 0));
        chunk.OnStateChanged += HandleChunkStateChanged;
        registry.Add(coord, chunk);
        return chunk;
    }

    /// <summary>ChunkSeed = Hash(coord, worldSeed)（同一 (coord, seed) 永远生成同一 Record）。</summary>
    uint ChunkSeed(ChunkCoord coord, int salt)
    {
        unchecked
        {
            return worldSeed ^ (uint)(coord.x * 73856093) ^ (uint)(coord.y * 19349663) ^ (uint)(salt * 83492791);
        }
    }

    /// <summary>
    /// Tile 生成薄封装（逻辑迁移至 ChunkTileGenerator，职责分离）：
    /// 单一「随机 + 模板融合」模式，模板分配经全局 ChunkTemplateAllocator 协调（确定性 + 全局约束复用）。
    /// 返回 Chunk 实际开放边（随机路径边沿全 Normal 四边恒开；模板按布局边沿可走性，连通由策划负责）。
    /// TODO(Phase 1 后续): 真实生成器必须满足边界签名（Hash(coord, dir, seed) 决定边沿开口模式）。
    /// </summary>
    List<ChunkDirection> GenerateTilesPlaceholder(ChunkRuntime chunk, uint seed)
    {
        ChunkTileGenerator.Generate(chunk, chunk.Def, seed, templateAllocator);
        return chunk.OpenEdges;
    }

    /// <summary>校验失败兜底：全部 Tile 强制 Normal 且可行走，四边恒开。</summary>
    void ForceFallbackOpenings(ChunkRuntime chunk)
    {
        var rng = new System.Random((int)ChunkSeed(chunk.Coord, 999));
        var def = chunk.Def;
        int n = chunkSize;

        var tiles = new TileData[n, n];
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
        {
            // 安全兜底语义：强制可走（normalTiles 本应是可走地面）；池直接存 prefab
            var prefab = ChunkTileGenerator.PickFromPool(def != null ? def.normalTiles : null, rng);
            tiles[x, y] = new TileData(x, y, prefab, true, TerrainKind.Normal);
        }
        var openEdges = new List<ChunkDirection>(ChunkCoord.AllDirections);
        chunk.SetTiles(tiles, openEdges);
    }

    void HandleChunkStateChanged(ChunkRuntime chunk, ChunkStreamState oldState, ChunkStreamState newState)
    {
        OnChunkStateChanged?.Invoke(chunk.Coord, oldState, newState);
    }

    // ── 坐标换算 ──
    // 空间约定（2026-08-14 旋转支持）：
    //   系统内部一律使用「地图本地坐标」——网格从本地原点 (0,0,0) 铺开；
    //   地图本地 ↔ 世界 的转换经本物体 transform（支持整体平移/旋转 45° 等摆放，不支持非均匀缩放，Awake 有告警）。
    //   对外公开 API 保持世界语义（ChunkToWorldOrigin/ChunkCenter/TileCenterWorld/WorldToChunk），外部无需感知本地系。

    /// <summary>地图本地坐标 → 世界坐标（经本物体 transform， 旋转支持）。</summary>
    public Vector3 MapLocalToWorld(Vector3 local) => transform.TransformPoint(local);

    /// <summary>世界坐标 → 地图本地坐标（经本物体 transform 逆变换）。</summary>
    public Vector3 WorldToMapLocal(Vector3 world) => transform.InverseTransformPoint(world);

    /// <summary>Chunk 本地原点（角点，地图本地坐标）。</summary>
    Vector3 ChunkLocalOrigin(ChunkCoord c)
    {
        float chunkWorld = chunkSize * tileSize;
        return new Vector3(c.x * chunkWorld, 0f, c.y * chunkWorld);
    }

    /// <summary>Chunk 本地中心（地图本地坐标，范围判定用）。</summary>
    Vector3 ChunkLocalCenter(ChunkCoord c)
    {
        float half = chunkSize * tileSize * 0.5f;
        return ChunkLocalOrigin(c) + new Vector3(half, 0f, half);
    }

    /// <summary>地图本地坐标 → Chunk 坐标（内部判定用；外部世界坐标入口见 WorldToChunk）。</summary>
    ChunkCoord ChunkFromLocal(Vector3 local)
    {
        float chunkWorld = chunkSize * tileSize;
        return new ChunkCoord(FloorTolerant(local.x / chunkWorld), FloorTolerant(local.z / chunkWorld));
    }

    /// <summary>
    /// 容差取整：坐标恰在 Chunk/Tile 边界上时，逆变换浮点误差（如 -1e-7）会致 FloorToInt 跨格
    /// （旋转 45° 的三角运算放大误差）。|v - round(v)| < 1e-4 视为整数边界，取 round。
    /// </summary>
    static int FloorTolerant(float v)
    {
        float r = Mathf.Round(v);
        return Mathf.Abs(v - r) < 1e-4f ? (int)r : Mathf.FloorToInt(v);
    }

    /// <summary>世界坐标 → Chunk 坐标（世界入口：先转地图本地再取格）。</summary>
    public ChunkCoord WorldToChunk(Vector3 world) => ChunkFromLocal(WorldToMapLocal(world));

    /// <summary>Chunk 坐标 → 世界原点（角点）。</summary>
    public Vector3 ChunkToWorldOrigin(ChunkCoord c) => MapLocalToWorld(ChunkLocalOrigin(c));

    /// <summary>Chunk 中心世界坐标（MonsterSpawner/ChunkVisualizer 摆放与标签用）。</summary>
    public Vector3 ChunkCenter(ChunkCoord c) => MapLocalToWorld(ChunkLocalCenter(c));

    /// <summary>Tile 中心世界坐标（刷怪点/视觉方块摆放的统一出口，内部处理旋转）。</summary>
    public Vector3 TileCenterWorld(ChunkCoord coord, int x, int y)
    {
        float ts = tileSize;
        return MapLocalToWorld(ChunkLocalOrigin(coord) + new Vector3((x + 0.5f) * ts, 0f, (y + 0.5f) * ts));
    }

    /// <summary>世界坐标 → 该 Chunk 内 Tile 局部下标（越界返回 false，内部处理旋转 + 边界容差）。</summary>
    public bool WorldToTileLocal(Vector3 world, ChunkCoord coord, out int x, out int y)
    {
        Vector3 local = WorldToMapLocal(world);
        Vector3 origin = ChunkLocalOrigin(coord);
        x = FloorTolerant((local.x - origin.x) / tileSize);
        y = FloorTolerant((local.z - origin.z) / tileSize);
        return x >= 0 && x < chunkSize && y >= 0 && y < chunkSize;
    }

    /// <summary>玩家位置（地图本地坐标，内部主循环判定同源；对外世界语义见 PlayerWorldPosition）。</summary>
    Vector3 GetPlayerPosition(){
        Vector3 world;
        if (playerOverride != null) world = playerOverride.position;
        else if (PlayerController.Instance != null) world = PlayerController.Instance.transform.position;
        else
        {
            if (playerFallback == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) playerFallback = go.transform;
            }
            // 场景无玩家时以自身位置为基准（即地图本地原点），保证挂到空场景也能跑状态机
            world = playerFallback != null ? playerFallback.position : transform.position;
        }
        return WorldToMapLocal(world);
    }

#if UNITY_EDITOR
    /// <summary>配置防御：D > C 不变量。</summary>
    void OnValidate(){
        if (radiusD <= radiusC)
            Debug.LogWarning($"[MapStreamingSystem] radiusD({radiusD}) 必须 > radiusC({radiusC})，否则无卸载滞后区。", this);
        if (radiusC < radiusB || radiusB < radiusA)
            Debug.LogWarning($"[MapStreamingSystem] 期望 A ≤ B ≤ C < D（当前 A={radiusA}, B={radiusB}, C={radiusC}, D={radiusD}）。", this);
    }
#endif

    // ── 调试可视化 ──

    void OnDrawGizmos(){
        if (!showGizmos) return;

        // 全部按地图本地坐标绘制，经 Gizmos.matrix 跟随本物体平移/旋转（2026-08-14 旋转支持）
        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 player = GetPlayerPosition();
        DrawRangeCircle(player, radiusA, new Color(0.2f, 1f, 0.2f, 0.9f));   // A 绿
        DrawRangeCircle(player, radiusB, new Color(0.2f, 0.6f, 1f, 0.9f));   // B 蓝
        DrawRangeCircle(player, radiusC, new Color(1f, 0.9f, 0.2f, 0.9f));   // C 黄
        DrawRangeCircle(player, radiusD, new Color(1f, 0.3f, 0.3f, 0.9f));   // D 红

        if (!Application.isPlaying) return;
        foreach (var kv in registry)
        {
            var chunk = kv.Value;
            Gizmos.color = StateColor(chunk.State);
            float size = chunkSize * tileSize;
            var center = ChunkLocalCenter(chunk.Coord);
            Gizmos.DrawCube(center + Vector3.up * 0.05f, new Vector3(size, 0.1f, size));
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(center + Vector3.up * 0.05f, new Vector3(size, 0.1f, size));
        }

        // 被 Pin Chunk 紫色线框（：Pin 泄漏可视化监控）
        foreach (var c in pinRegistry.PinnedCoords)
        {
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.9f);
            float size = chunkSize * tileSize;
            Gizmos.DrawWireCube(ChunkLocalCenter(c) + Vector3.up * 0.3f, new Vector3(size, 0.4f, size));
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    static Color StateColor(ChunkStreamState state)
    {
        switch (state)
        {
            case ChunkStreamState.Prepared: return new Color(1f, 0.9f, 0.2f, 0.25f);
            case ChunkStreamState.Dormant: return new Color(0.2f, 0.6f, 1f, 0.3f);
            case ChunkStreamState.Active: return new Color(0.2f, 1f, 0.2f, 0.35f);
            case ChunkStreamState.Unloaded: return new Color(0.5f, 0.5f, 0.5f, 0.15f);
            default: return new Color(1f, 1f, 1f, 0.1f);
        }
    }

    static void DrawRangeCircle(Vector3 center, float radius, Color color)
    {
        const int segments = 64;
        Gizmos.color = color;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * (360f / segments) * Mathf.Deg2Rad;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(prev + Vector3.up * 0.1f, next + Vector3.up * 0.1f);
            prev = next;
        }
    }
}
