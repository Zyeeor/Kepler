using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chunk 视觉渲染层（视觉渲染 v2：Tile 级方块地形方案）：监听 MapStreamingSystem 状态切换事件，
/// 在进 B（Prepared→Dormant）时把 Chunk 的逻辑 Tile 网格逐格铺成方块实例，离开 B/D 时整块销毁。
/// 与流送系统解耦（System 不直接引用本类，同 MonsterSpawner 的订阅方式）。
///
/// 方案要点（无"墙"概念，用户明确要求当前不考虑墙壁）：
///   1. 每 Tile 一类地形（TerrainKind：Normal/Lava/Spike/Blocker，生成期快照进 TileData.kind）；
///      TileDef.visualPrefab 即「一格大小」的方块 prefab（用户自行调整），代码按 tileSize/bounds 比例缩放适配铺满一格。
///   2. 每个 Tile 独立实例、1×1 原始尺寸，三轴等比缩放适配一格（不做横向合并拉长，保证纹理逐格正确）。
///      16×16=256 格 → 最多 256 实例/Chunk（对象池化是未来优化方向，见 PlaceBlock TODO）。
///   3. Normal 且无 visualPrefab 的 Tile 无视觉（生成器保证 normalTiles 池非空，极端空槽跳过）。
///   4. 物理阻挡完全由 prefab 自带 Collider 决定（装饰物须自带 solid Collider；生成器不兜底补碰撞）。
///   5. Chunk 聚合根（ChunkVisual_(x,y)）+ 模块实例化：整块开关/销毁。
///
/// 缩放适配：实例化后实测 prefab 世界 bounds（Renderer 优先，Collider 兜底）→ 逐轴/统一缩放 →
///   解析式 pivot 校正（包围盒中心 XZ 对齐格中心、底面对齐 y=0 行走面基准）。用户改 prefab 尺寸无需改代码。
///
/// 确定性：视觉完全由 TileData（kind/def 快照）驱动，TileData 由 chunk.Seed 确定性生成，
///   同一 Chunk 反复进出 B 重建的视觉完全一致（本层不再消耗随机数）。
///
/// TODO(玩法): 岩浆/地刺伤害区逻辑（当前纯视觉，isWalkable=true 可走；接入战斗系统时按 TileData.kind 判定，本层不动）。
/// TODO(性能): 对象池化——同 prefab 跨 Chunk 分池复用；或 StaticBatchingUtility/GPU Instancing 合批（先 profile 再优化）。
/// v2 明确不做：墙壁、NavMesh 局部烘焙、Tilemap/合并网格（远期方向，见 MapStreamingSystem.Instantiate TODO）。
/// </summary>
public class ChunkVisualizer : MonoBehaviour
{
    [Header("调试")]
    [Tooltip("Scene 视图绘制非 Normal Tile 线框（Lava 橙 / Spike 黄 / Blocker 红）。")]
    public bool showDebug = true;
    [Tooltip("chunk 视觉建成/销毁日志（移动时高频刷屏，默认关闭）。")]
    public bool logBuilds = false;

    /// <summary>视觉根注册表：coord → 聚合根 GameObject（重复进出 B 正确重建/销毁的凭据）。</summary>
    readonly Dictionary<ChunkCoord, GameObject> visualRoots = new Dictionary<ChunkCoord, GameObject>();
    /// <summary>配置告警去重（同一配置问题只告警一次，避免每 Chunk 刷屏）。</summary>
    readonly HashSet<string> configWarnings = new HashSet<string>();

    Transform container;

    void Start()
    {
        var system = MapStreamingSystem.Instance;
        if (system == null)
        {
            Debug.LogWarning("[ChunkVisualizer] 场景中无 MapStreamingSystem，视觉渲染不启动（本组件空转）。");
            return;
        }
        system.OnChunkStateChanged += HandleChunkStateChanged;

        // 订阅前已就位的 Chunk 补齐视觉（出生区同步实例化可能早于本组件 Start）：
        // Dormant/Active 都说明已经过 Instantiate（进 B），直接补建。
        foreach (var kv in system.Registry)
        {
            var state = kv.Value.State;
            if (state == ChunkStreamState.Dormant || state == ChunkStreamState.Active)
                BuildVisual(kv.Key);
        }
    }

    void OnDestroy()
    {
        if (MapStreamingSystem.Instance != null)
            MapStreamingSystem.Instance.OnChunkStateChanged -= HandleChunkStateChanged;
    }

    // ── 状态切换响应 ──

    void HandleChunkStateChanged(ChunkCoord coord, ChunkStreamState oldState, ChunkStreamState newState)
    {
        switch (newState)
        {
            case ChunkStreamState.Dormant:
                // Instantiate 完成（进 B）：建视觉。Active→Dormant（Pause）不重建（视觉仍在）。
                if (oldState == ChunkStreamState.Prepared)
                    BuildVisual(coord);
                break;
            case ChunkStreamState.Prepared:
                // UnloadScene（离开 B）：销毁视觉，逻辑数据保留。
                if (oldState == ChunkStreamState.Dormant)
                    DestroyVisual(coord, "离开 B");
                break;
            case ChunkStreamState.Unloaded:
                // UnloadFull（离开 D）兜底：Dormant 直跳 Unloaded 时也要销毁（正常此时视觉已随离开 B 销毁，幂等 no-op）。
                DestroyVisual(coord, "离开 D");
                break;
        }
    }

    // ── 建视觉（进 B） ──

    void BuildVisual(ChunkCoord coord)
    {
        var system = MapStreamingSystem.Instance;
        if (system == null) return;
        if (visualRoots.TryGetValue(coord, out var existing))
        {
            if (existing != null) return; // 已建（如重复事件），幂等跳过
            visualRoots.Remove(coord);    // 外部已销毁的死引用，摘除后重建
        }
        if (!system.Registry.TryGetValue(coord, out var chunk) || chunk == null || chunk.Tiles == null)
        {
            Debug.LogWarning($"[ChunkVisualizer] {coord} 建视觉失败：Chunk 不存在或 Tile 数据未就绪。");
            return;
        }

        // 聚合根：世界位置/朝向跟随系统 transform
        var root = new GameObject($"ChunkVisual_{coord}");
        root.transform.SetParent(GetContainer(), false);
        root.transform.SetPositionAndRotation(system.ChunkToWorldOrigin(coord), system.transform.rotation);
        visualRoots.Add(coord, root);

        int instanceCount = 0, coveredTiles = 0;
        PlaceTileBlocks(root, chunk, ref instanceCount, ref coveredTiles);

        if (logBuilds)
            Debug.Log($"[ChunkVisualizer] {coord} 视觉建成：Tile 方块实例 {instanceCount}（覆盖 {coveredTiles} 格）。");
    }

    // ── 销毁视觉（离开 B/D） ──

    void DestroyVisual(ChunkCoord coord, string reason)
    {
        if (!visualRoots.TryGetValue(coord, out var root)) return; // 未建过：幂等 no-op
        visualRoots.Remove(coord);

        // 状态异常防御：Pin 只阻止离开 D，不阻止离开 B——故"离开 D"时被 Pin 属异常（ stale 校验应已拦截），告警但照销（防泄漏优先）
        var system = MapStreamingSystem.Instance;
        if (system != null && reason == "离开 D" && system.Pins.IsPinned(coord))
            Debug.LogWarning($"[ChunkVisualizer] {coord} 被 Pin 却收到离开 D 销毁（状态异常），仍销毁防视觉泄漏。");

        if (root == null) return; // 已被外部销毁（Unity 假空）
        Destroy(root);
        if (logBuilds) Debug.Log($"[ChunkVisualizer] {coord} 视觉销毁（{reason}）。");
    }

    // ── Tile 方块铺设 ──

    /// <summary>
    /// 按 Tile 网格逐格铺方块（v2 核心）：每个 Tile 独立实例、1×1 原始尺寸，
    /// 不做横向合并拉伸——保证每格 prefab 的纹理/贴图逐格正确（拉伸会致纹理变形）。
    /// 统计 instanceCount（实例数）与 coveredTiles（覆盖格数）。
    /// </summary>
    void PlaceTileBlocks(GameObject root, ChunkRuntime chunk, ref int instanceCount, ref int coveredTiles)
    {
        var system = MapStreamingSystem.Instance;
        Vector3 origin = Vector3.zero; // 聚合根局部空间即 Chunk 原点（旋转支持：统一局部坐标）
        float ts = system.tileSize;
        var tiles = chunk.Tiles;
        int nx = tiles.GetLength(0), ny = tiles.GetLength(1);

        for (int y = 0; y < ny; y++)
        for (int x = 0; x < nx; x++)
        {
            var t = tiles[x, y];

            // 底层：恒非装饰地砖（Normal/Trigger）。保留原始尺寸仅对手摆整格 Decoration（程序生成底层不会是 Decoration）
            if (t.prefab != null)
            {
                Vector3 target = origin + new Vector3((x + 0.5f) * ts, 0f, (y + 0.5f) * ts);
                bool baseKeepOriginal = (t.kind == TerrainKind.Decoration);
                PlaceBlock(t.prefab, root, target, ts, ts, baseKeepOriginal, $"Base_{t.kind}_{x}_{y}");
                instanceCount++;
                coveredTiles++;
            }
            else if (t.kind != TerrainKind.Normal)
            {
                // 底层无 prefab：生成器应保证底层非空，理论不可达（双保险告警）
                WarnOnce($"tile-null-{t.kind}", $"[ChunkVisualizer] {t.kind} 底层 Tile 无 prefab 且未回退 Normal（配置异常），跳过视觉。");
            }

            // 旧式无 owner 的叠加层仍按单格兼容；新式多格 placement 在循环结束后只实例化一次。
            if (t.overlayPrefab != null && t.overlayPlacementId <= 0)
            {
                Vector3 target = origin + new Vector3((x + 0.5f) * ts, 0f, (y + 0.5f) * ts);
                PlaceBlock(t.overlayPrefab, root, target, ts, ts, true, $"Overlay_{t.overlayKind}_{x}_{y}");
                instanceCount++;
            }
        }

        // 多格装饰物按逻辑 placement 一次生成，避免同一个 prefab 在每个占用格重复 Instantiate。
        if (chunk.DecorationPlacements != null)
        {
            foreach (var placement in chunk.DecorationPlacements)
            {
                if (placement == null || placement.prefab == null) continue;
                var size = placement.SafeFootprintSize;
                Vector3 target = origin + new Vector3(
                    (placement.anchor.x + size.x * 0.5f) * ts,
                    0f,
                    (placement.anchor.y + size.y * 0.5f) * ts);
                PlaceBlock(placement.prefab, root, target, size.x * ts, size.y * ts, true,
                    $"Decoration_{placement.id}_{placement.prefab.name}");
                instanceCount++;
            }
        }
    }

    // ── 方块实例化（缩放适配 + bounds 校正） ──

    /// <summary>
    /// 实例化 Tile 方块并缩放适配：
    ///   · 普通/触发地块——三轴统一等比缩放 s = min(targetX/sizeX, targetZ/sizeZ)，保持模型原始比例、
    ///     XZ 内切一格（prefab 非正方形时短轴留边，不溢出邻格），对规整 1×1 地砖 s≈1 纹理逐格正确。
    ///   · 装饰地块（keepOriginalSize=true）——保留 prefab 原始 localScale（不覆盖），允许模型自然超出单格边界
    ///     （如柱子跨格、1×2 长装饰等），仅做 pivot 校正（中心 XZ 对齐格中心、底面对齐 y=0）。
    ///     调整 prefab 根物体的 Scale 会在生成中如实体现。
    /// 包围盒中心 XZ 对齐格中心、底面对齐 y=0（行走面基准，消除美术 pivot 偏移）。
    /// 解析式校正（轴对齐缩放、无旋转，AABB 精确，无需二次重测）：
    ///   缩放关于 pivot p0：newCenter = p0 + (b.center - p0)⊙s，newMinY = p0.y + (b.min.y - p0.y)·s。
    /// 注意：假设聚合根父链 localScale = 1（ChunkVisuals 容器与本组件所在物体不做缩放，与 v1 同假设）。
    /// TODO(性能): 对象池化方向——按 prefab 分池，此处改为取池/重置变换。
    /// </summary>
    void PlaceBlock(GameObject prefab, GameObject root, Vector3 targetCenterXZ, float targetSizeX, float targetSizeZ, bool keepOriginalSize, string name)
    {
        var go = Instantiate(prefab, root.transform);
        go.name = name;
        Vector3 p0 = Vector3.zero; // 局部空间 pivot（聚合根局部空间 = 地图本地空间，旋转支持）
        go.transform.localPosition = p0;
        if (!TryMeasureBounds(go, out Bounds b))
        {
            WarnOnce($"bounds-{prefab.name}", $"[ChunkVisualizer] Tile 方块 prefab '{prefab.name}' 无 Renderer/Collider，无法测量 bounds，已按格中心原样摆放（无缩放适配）。");
            go.transform.localPosition = targetCenterXZ;
            return;
        }

        // 装饰地块：保留 prefab 原始 localScale（b 已含原始 scale，直接按实际 bounds 对齐）；
        // 其余地块：三轴统一等比内切一格（s 覆盖 localScale，最终 bounds = b⊙s）
        float s = 1f;
        if (!keepOriginalSize)
        {
            s = Mathf.Min(SafeRatio(targetSizeX, b.size.x), SafeRatio(targetSizeZ, b.size.z));
            go.transform.localScale = new Vector3(s, s, s);
        }
        // pivot 校正：p0=0，b 是缩放前测得的 bounds（含 prefab 原始 scale）。
        //   普通地块已把 localScale 覆盖为 (s,s,s)，最终中心 = b.center⊙s、底面 = b.minY·s；
        //   装饰地块 localScale 保持原始值，b 即最终实际 bounds，s=1 时 b⊙s = b 直接对齐。
        Vector3 scaledCenter = p0 + Vector3.Scale(b.center - p0, new Vector3(s, s, s));
        float scaledMinY = p0.y + (b.min.y - p0.y) * s;
        go.transform.localPosition = p0 + new Vector3(targetCenterXZ.x - scaledCenter.x, 0f - scaledMinY, targetCenterXZ.z - scaledCenter.z);
        // 物理阻挡完全由 prefab 自带 Collider 决定（装饰物自带 solid Collider 精确挡路，玩家可贴边绕行）
    }

    /// <summary>
    /// 合并模型空间（本地）包围盒，供聚合根局部空间解析式校正使用：
    /// Renderer 用 sharedMesh.bounds 经其 localRotation/localScale 变换（2026-08-14 修复：
    /// 美术 fbx 常用旋转/缩放把竖直面转成水平地面——如 Room001_Floor 砖块 45° 旋转、
    /// 直接读 sharedMesh.bounds 会拿到竖面顶点空间，导致方块抬高悬空）；
    /// 多 Renderer 按各自相对根物体的局部平移偏移合并（Encapsulate 对旋转子物体取 AABB，地形 prefab 结构简单可接受）；
    /// 无 MeshFilter 退用 BoxCollider 局部 size/center 拼合；皆无返回 false。
    /// </summary>
    static bool TryMeasureBounds(GameObject go, out Bounds bounds)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(false);
        bool first = true;
        bounds = default;
        foreach (var r in renderers)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var b = TransformBounds(mf.sharedMesh.bounds, r.transform.localRotation, r.transform.localScale);
            b.center += r.transform.localPosition - go.transform.localPosition;
            if (first) { bounds = b; first = false; }
            else bounds.Encapsulate(b);
        }
        if (!first) return true;

        var colliders = go.GetComponentsInChildren<Collider>(false);
        first = true;
        foreach (var c in colliders)
        {
            var bc = c as BoxCollider;
            if (bc == null) continue;
            var b = new Bounds(bc.center + (c.transform.localPosition - go.transform.localPosition), bc.size);
            if (first) { bounds = b; first = false; }
            else bounds.Encapsulate(b);
        }
        return !first;
    }

    /// <summary>
    /// 模型空间 bounds 经局部旋转/缩放变换的 AABB：8 角点逐点变换取 min/max。
    /// 美术 fbx 中地砖/地面常以旋转或缩放摆出水平面，直接读 sharedMesh.bounds 会拿到原始竖面顶点空间。
    /// </summary>
    static Bounds TransformBounds(Bounds b, Quaternion rot, Vector3 scale)
    {
        Vector3 c = b.center, e = b.extents;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = c + new Vector3((i & 1) != 0 ? e.x : -e.x,
                (i & 2) != 0 ? e.y : -e.y,
                (i & 4) != 0 ? e.z : -e.z);
            p = Vector3.Scale(p, scale);
            p = rot * p;
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        var result = new Bounds { center = (min + max) * 0.5f, size = max - min };
        return result;
    }

    /// <summary>安全比例：分母 ≈0（退化 prefab）时返回 1（不缩放），防除零/NaN。</summary>
    static float SafeRatio(float numerator, float denominator)
    {
        return denominator > 0.0001f ? numerator / denominator : 1f;
    }

    Transform GetContainer()
    {
        if (container == null)
        {
            var go = new GameObject("ChunkVisuals");
            go.transform.SetParent(transform, false);
            container = go.transform;
        }
        return container;
    }

    void WarnOnce(string key, string message)
    {
        if (configWarnings.Add(key)) Debug.LogWarning(message);
    }

    // ── 调试可视化 ──

    void OnDrawGizmos()
    {
        if (!showDebug || !Application.isPlaying) return;
        var system = MapStreamingSystem.Instance;
        if (system == null) return;
        // 按地图本地坐标绘制，经 Gizmos.matrix 跟随系统 transform 旋转（2026-08-14 旋转支持）
        Gizmos.matrix = system.transform.localToWorldMatrix;
        float ts = system.tileSize;
        float chunkWorld = system.chunkSize * system.tileSize;
        foreach (var kv in system.Registry)
        {
            var chunk = kv.Value;
            if (chunk.Tiles == null) continue;
            if (chunk.State != ChunkStreamState.Dormant && chunk.State != ChunkStreamState.Active) continue;
            Vector3 origin = new Vector3(kv.Key.x * chunkWorld, 0f, kv.Key.y * chunkWorld); // 本地原点
            int nx = chunk.Tiles.GetLength(0), ny = chunk.Tiles.GetLength(1);
            for (int x = 0; x < nx; x++)
            for (int y = 0; y < ny; y++)
            {
                var t = chunk.Tiles[x, y];

                // 底层 Trigger（橙）
                if (t.kind == TerrainKind.Trigger)
                {
                    Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.85f);
                    var c1 = origin + new Vector3((x + 0.5f) * ts, 0.5f, (y + 0.5f) * ts);
                    Gizmos.DrawWireCube(c1, new Vector3(ts, 1f, ts));
                }
                // 无 owner 的旧式叠加层 / 手摆整格 Decoration（红）；多格 placement 在循环后画整块。
                bool drawDeco = t.overlayPlacementId <= 0
                                && ((t.overlayPrefab != null && t.overlayKind == TerrainKind.Decoration)
                                || (t.overlayPrefab == null && t.kind == TerrainKind.Decoration));
                if (drawDeco)
                {
                    Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.85f);
                    var c2 = origin + new Vector3((x + 0.5f) * ts, 0.5f, (y + 0.5f) * ts);
                    Gizmos.DrawWireCube(c2, new Vector3(ts, 1f, ts));
                }
            }

            if (chunk.DecorationPlacements != null)
            {
                Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.85f);
                foreach (var placement in chunk.DecorationPlacements)
                {
                    if (placement == null || placement.prefab == null) continue;
                    var size = placement.SafeFootprintSize;
                    var c = origin + new Vector3(
                        (placement.anchor.x + size.x * 0.5f) * ts,
                        0.5f,
                        (placement.anchor.y + size.y * 0.5f) * ts);
                    Gizmos.DrawWireCube(c, new Vector3(size.x * ts, 1f, size.y * ts));
                }
            }
        }
        Gizmos.matrix = Matrix4x4.identity;
    }
}
