using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 地图流送调试 HUD（OnGUI，风格与 MonsterSpawner 调试面板一致）：
/// 玩家坐标 / 所在 Chunk、A/B/C/D 各范围 Chunk 数、任务队列长度、本帧已执行 Job 数、
/// 时间片使用率、Chunk 总数与状态分布（Active/Dormant/Prepared/Unloaded）、Pin 数、ChunkState 快照数。
/// 默认关闭（showHud = false）：Inspector 勾选或快捷键（默认 F2）切换。
/// 运行时若场景中无实例会自动创建（挂在名为 MapDebugHUD 的独立物体上），无需手动摆放。
/// 统计低频刷新（refreshInterval），避免每帧遍历 Chunk 注册表造成调试开销。
/// </summary>
public class MapDebugHUD : MonoBehaviour
{
    [Tooltip("是否显示调试 HUD（快捷键切换）。")]
    public bool showHud = false;
    [Tooltip("HUD 显示切换快捷键。")]
    public KeyCode toggleKey = KeyCode.F2;
    [Tooltip("统计刷新间隔（秒）。")]
    [Min(0.05f)] public float refreshInterval = 0.25f;

    [Header("Chunk 边界轮廓")]
    [Tooltip("HUD 开启时同时绘制每个 Chunk 的边界线（Game 与 Scene 视图均可见，按状态着色）。")]
    public bool showChunkBorders = true;
    [Tooltip("边界线世界高度（略高于地面，避免与地面 z-fighting）。")]
    public float borderHeight = 0.05f;

    static MapDebugHUD instance;

    // ── 低频缓存的统计数据（OnGUI 每帧可能多次调用，不直接遍历注册表） ──
    Vector3 playerPos;
    ChunkCoord playerChunk;
    int countA, countB, countC, countD;
    int queuedJobs, jobsExecuted;
    float queueMs, timeBudget;
    bool timeSliceExceeded;
    int chunkTotal, stateActive, stateDormant, statePrepared, stateUnloaded, stateNone;
    int pinCount, snapshotCount;
    bool systemReady;
    float nextRefreshTime;

    /// <summary>运行时自动创建：场景无实例时补一个（默认关闭，不影响正式场景）。</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureInstance()
    {
        if (instance == null)
            new GameObject(nameof(MapDebugHUD)).AddComponent<MapDebugHUD>();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[MapDebugHUD] 重复实例，销毁后者。");
            Destroy(this);
            return;
        }
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void OnEnable()
    {
        // URP 项目：Camera.onPostRender 在 SRP 下不触发，必须用 endCameraRendering（Game 与 Scene 视图均回调）
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
    }

    /// <summary>
    /// Chunk 边界轮廓绘制（GL 线，Play 模式 Game/Scene 视图均可见）：
    /// 相机渲染完成后按 Chunk 状态着色画 4 条边界线，高度 borderHeight 防 z-fighting。
    /// 顶点用地图本地坐标，经 M·V·P 矩阵一次变换（跟随系统 transform 旋转 45° 等摆放）。
    /// 每帧重画即时模式线段（Registry 通常 < 100 Chunk，开销可忽略）。
    /// </summary>
    void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (!showHud || !showChunkBorders || !Application.isPlaying) return;
        var system = MapStreamingSystem.Instance;
        if (system == null || system.Registry == null || system.Registry.Count == 0) return;
        if (borderLineMat == null) CreateBorderLineMat();
        if (borderLineMat == null) return;

        float worldSize = system.chunkSize * system.tileSize;
        float y = borderHeight;

        GL.PushMatrix();
        // 关键：投影矩阵必须走 GL.LoadProjectionMatrix（处理 GL -1..1 深度约定），
        // 直接 GL.MultMatrix(projectionMatrix) 会致顶点深度全在裁剪范围外 → 只剩残片（"一条线"症状）。
        // 栈依次右乘：LoadP → ×V → ×M，顶点 = P·V·M·v（本地 → 世界 → 相机 → 裁剪）。
        GL.LoadProjectionMatrix(cam.projectionMatrix);           // P
        GL.MultMatrix(cam.worldToCameraMatrix);                  // V·P
        GL.MultMatrix(system.transform.localToWorldMatrix);      // M·V·P（地图本地 → 世界 → 相机）
        borderLineMat.SetPass(0);
        GL.Begin(GL.LINES);

        foreach (var kv in system.Registry)
        {
            // 顶点为地图本地坐标（M 矩阵负责平移/旋转）
            float x0 = kv.Key.x * worldSize, z0 = kv.Key.y * worldSize;
            float x1 = x0 + worldSize, z1 = z0 + worldSize;
            Color c;
            switch (kv.Value.State)
            {
                case ChunkStreamState.Active: c = Color.green; break;
                case ChunkStreamState.Dormant: c = new Color(0.3f, 0.6f, 1f); break;
                case ChunkStreamState.Prepared: c = Color.yellow; break;
                case ChunkStreamState.Unloaded: c = new Color(0.6f, 0.6f, 0.6f); break;
                default: c = Color.white; break;
            }
            GL.Color(c);
            GL.Vertex3(x0, y, z0); GL.Vertex3(x1, y, z0);
            GL.Vertex3(x1, y, z0); GL.Vertex3(x1, y, z1);
            GL.Vertex3(x1, y, z1); GL.Vertex3(x0, y, z1);
            GL.Vertex3(x0, y, z1); GL.Vertex3(x0, y, z0);
        }

        GL.End();
        GL.PopMatrix();
    }

    static Material borderLineMat;

    /// <summary>GL 线材质（Hidden/Internal-Colored 使用顶点色，GL.Color 即可着色）。</summary>
    static void CreateBorderLineMat()
    {
        var shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null) return;
        borderLineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            showHud = !showHud;
            showChunkBorders = showHud; // F2 同时开关边界轮廓，与 HUD 保持一致
        }
        if (!showHud || !Application.isPlaying) return;
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + refreshInterval;
            RefreshStats();
        }
    }

    /// <summary>低频采集：从 MapStreamingSystem / MonsterSpawner 公开只读属性取数。</summary>
    void RefreshStats()
    {
        var system = MapStreamingSystem.Instance;
        systemReady = system != null;
        if (!systemReady) return;

        playerPos = system.PlayerWorldPosition;
        playerChunk = system.WorldToChunk(playerPos);
        countA = system.RangeACount;
        countB = system.RangeBCount;
        countC = system.RangeCCount;
        countD = system.RangeDCount;
        queuedJobs = system.QueuedJobCount;
        jobsExecuted = system.JobsExecutedThisFrame;
        queueMs = system.LastFrameQueueMs;
        timeBudget = MapStreamingSystem.TimeBudgetMs;
        timeSliceExceeded = system.TimeSliceExceeded;
        pinCount = system.Pins.PinnedChunkCount;
        snapshotCount = system.States.Count;

        chunkTotal = stateActive = stateDormant = statePrepared = stateUnloaded = stateNone = 0;
        foreach (var kv in system.Registry)
        {
            chunkTotal++;
            switch (kv.Value.State)
            {
                case ChunkStreamState.Active: stateActive++; break;
                case ChunkStreamState.Dormant: stateDormant++; break;
                case ChunkStreamState.Prepared: statePrepared++; break;
                case ChunkStreamState.Unloaded: stateUnloaded++; break;
                default: stateNone++; break;
            }
        }
    }

    void OnGUI()
    {
        if (!showHud || !Application.isPlaying) return;

        const float w = 264f;
        const float lineH = 18f;
        float x = 10f, y = 10f;
        GUI.Box(new Rect(x, y, w, systemReady ? 11 * lineH + 14f : 2 * lineH + 14f), "MapStreaming Debug (F2)");
        y += lineH + 4f;

        if (!systemReady)
        {
            GUI.Label(new Rect(x + 8f, y, w - 16f, lineH), "MapStreamingSystem 未就绪");
            return;
        }

        Label(x, ref y, w, lineH, $"玩家 ({playerPos.x:0.0}, {playerPos.z:0.0})  Chunk ({playerChunk.x}, {playerChunk.y})");
        Label(x, ref y, w, lineH, $"范围 A {countA} / B {countB} / C {countC} / D {countD}");
        Label(x, ref y, w, lineH, $"队列 {queuedJobs}  本帧执行 {jobsExecuted}");
        string slice = timeBudget > 0f
            ? $"时间片 {queueMs:0.00}/{timeBudget:0.0}ms（{queueMs / timeBudget * 100f:0}%）{(timeSliceExceeded ? " 超时!" : "")}"
            : $"时间片 关闭（本帧 {queueMs:0.00}ms 未计时）";
        Label(x, ref y, w, lineH, slice);
        Label(x, ref y, w, lineH, $"Chunk 总数 {chunkTotal}");
        Label(x, ref y, w, lineH, $"Active {stateActive} / Dormant {stateDormant}");
        Label(x, ref y, w, lineH, $"Prepared {statePrepared} / Unloaded {stateUnloaded}");
        if (stateNone > 0) Label(x, ref y, w, lineH, $"None {stateNone}");
        Label(x, ref y, w, lineH, $"Pin {pinCount}  快照 {snapshotCount}");

        var spawner = MonsterSpawner.Instance;
        if (spawner != null)
            Label(x, ref y, w, lineH, $"在场怪 {spawner.TrackedMonsterCount}  战斗 {spawner.CombatMonsterCount}  视野外 {spawner.OffscreenCombatMonsters.Count}");
    }

    static void Label(float x, ref float y, float w, float h, string text)
    {
        GUI.Label(new Rect(x + 8f, y, w - 16f, h), text);
        y += h;
    }
}
