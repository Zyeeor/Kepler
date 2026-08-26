using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chunk Tile 生成器（纯数据层）：按 ChunkDef 生成一块 Chunk 的完整 Tile 网格。
/// 唯一生成模式 = 随机生成与预设模板融合：按 ChunkDef.templateWeight 比重决定走模板（从模板库
/// 按权重抽取并逐格映射）还是纯随机（边沿铺普通地面 → 特殊地形图案 → 道路带/花纹区块/填充）。
/// 模板分配由 ChunkTemplateAllocator 全局协调（mustGenerate 保证出现、maxCount 上限、weight 抽取），
/// 分配结果在 Chunk 首次生成时确定并复用，保证确定性 + 重生成一致。
/// 确定性：同 seed 必得同结果，随机源仅 System.Random(Hash(coord, seed))，禁用 UnityEngine.Random/时间。
/// 输出：TileData.prefab / isWalkable / kind（视觉层与刷怪层的全部依赖）。
/// 纯数据（不创建 GameObject、不触碰世界坐标），生成的坐标换算由调用方负责。
/// </summary>
public static class ChunkTileGenerator
{
    /// <summary>单个图案的放置尝试上限：随机锚点+方向尝试这么多次仍放不下就放弃该图案。</summary>
    const int MaxPlaceTries = 32;
    /// <summary>图案数从 min 递增至 max 的固定概率（min 必放，之后每次以该概率 +1，直到 max）。</summary>
    const float PatternContinueChance = 0.5f;
    /// <summary>无系统实例时的 Chunk 边长兜底（编辑模式验证用）。</summary>
    const int FallbackChunkSize = 8;

    // 四邻接方向偏移（固定顺序，确定性遍历用）
    static readonly int[] DX = { 1, 0, -1, 0 };
    static readonly int[] DY = { 0, 1, 0, -1 };

    // 形状偏移表（[形状][朝向] → 相对锚点的格子偏移；朝向随机抽取，Single/Square2 仅 1 朝向）
    static readonly Vector2Int[][] shapeSingle = { new[] { new Vector2Int(0, 0) } };
    static readonly Vector2Int[][] shapeLine2 =
    {
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) }, // 横向
        new[] { new Vector2Int(0, 0), new Vector2Int(0, 1) }, // 纵向
    };
    static readonly Vector2Int[][] shapeLine3 =
    {
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) },
        new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2) },
    };
    static readonly Vector2Int[][] shapeSquare2 =
    {
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) },
    };
    // L 形 = 2×2 包围盒缺一角，4 朝向（缺的角分别为 右上/右下/左上/左下）
    static readonly Vector2Int[][] shapeL =
    {
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) }, // ┗（缺右上）
        new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) }, // ┏（缺右下）
        new[] { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) }, // ┓（缺左上）
        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(1, 1) }, // ┛（缺左下）
    };

    /// <summary>图案抽取缓冲（主线程专用，避免每次抽取分配）。</summary>
    struct PatternCandidate
    {
        public TerrainKind kind;
        public PatternShape shape;
        public float weight;
    }
    static readonly List<PatternCandidate> pickBuffer = new List<PatternCandidate>(12);
    /// <summary>地面生长的继承源缓冲（主线程专用，prefab 即图案标识）。</summary>
    static readonly List<GameObject> donorBuffer = new List<GameObject>(4);

    /// <summary>
    /// 生成入口（Prepare 阶段唯一调用点）。
    /// 单一混合模式：先经 allocator 决定本 Chunk 走模板还是随机——命中模板则逐格映射该布局，
    /// 否则走纯随机生成。allocator 可为 null（编辑模式验证），此时退化为纯随机（模板不参与）。
    /// </summary>
    public static void Generate(ChunkRuntime chunk, ChunkDef def, uint seed, ChunkTemplateAllocator allocator = null)
    {
        if (chunk == null) return;
        int n = ResolveChunkSize();

        // 模板分配：由全局分配器决定（确定性 + 全局约束复用）
        var picked = allocator != null ? allocator.Resolve(chunk.Coord, def, seed) : null;
        if (picked != null)
        {
            GenerateFromLayout(chunk, picked, n);
            return;
        }

        GenerateProcedural(chunk, def, seed, n);
    }

    /// <summary>Chunk 边长解析：运行时以系统配置为准；编辑模式（无系统实例）兜底 16。</summary>
    static int ResolveChunkSize()
    {
        var sys = MapStreamingSystem.Instance;
        return sys != null ? sys.chunkSize : FallbackChunkSize;
    }

    /// <summary>
    /// 固定锚点生成入口（MapStreamingSystem 固定 Chunk 锚点专用）：
    ///   layout 非空 → 完全按手摆布局逐格生成（内容与种子无关）；
    ///   否则 chunkDef 非空 → 该 def 程序生成 + 固定 seed（确定性），
    ///   且不参与全局 ChunkTemplateAllocator（模板约束/计数与锚点隔离）。
    /// </summary>
    public static void GenerateFixed(ChunkRuntime chunk, FixedChunkLayout layout, ChunkDef def, uint seed)
    {
        if (chunk == null) return;
        int n = ResolveChunkSize();
        if (layout != null)
        {
            GenerateFromLayout(chunk, layout, n);
            return;
        }
        if (def != null)
            Generate(chunk, def, seed, null); // allocator=null：纯程序生成，模板分配不参与
    }

    /// <summary>
    /// 从指定布局逐格映射（模板抽取共用）。
    /// 空格（null）视为可走普通地面；开放边按实际可走性计算（边沿连通由策划保证）。
    /// </summary>
    static void GenerateFromLayout(ChunkRuntime chunk, FixedChunkLayout layout, int n)
    {
        if (n != layout.size)
            Debug.LogWarning($"[ChunkTileGenerator] {chunk.Coord} 系统 chunkSize({n}) 与布局尺寸({layout.size}) 不一致：越界格按空处理（尺寸自适应未实现）。", layout);

        var tiles = new TileData[n, n];
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
        {
            var prefab = layout.GetTile(x, y);
            if (prefab != null)
            {
                // 玩法语义：逻辑通行性=无 solid Collider；分类由 prefab 语义自动推导（ResolveKind）
                var kind = TileSemantics.ResolveKind(prefab);
                var walkable = WalkableOf(prefab);
                tiles[x, y] = new TileData(x, y, prefab, walkable, kind);
            }
            else
                tiles[x, y] = new TileData(x, y, null, true, TerrainKind.Normal);
        }

        var openEdges = ComputeOpenEdges(tiles, n);
        if (openEdges.Count < 2)
            Debug.LogWarning($"[ChunkTileGenerator] {chunk.Coord} 布局 '{layout.name}' 开放边 {openEdges.Count} < 2：Chunk 间连通由策划负责，该 Chunk 将被出入口校验安全兜底（全 Normal）替换。", layout);
        chunk.SetTiles(tiles, openEdges);
    }

    /// <summary>按实际边沿可走性计算开放边：某边存在 ≥1 可走 Tile 即视为该边开放。</summary>
    static List<ChunkDirection> ComputeOpenEdges(TileData[,] tiles, int n)
    {
        int max = n - 1;
        bool east = false, south = false, west = false, north = false;
        for (int i = 0; i < n; i++)
        {
            if (tiles[max, i].isWalkable) east = true;
            if (tiles[i, max].isWalkable) north = true;
            if (tiles[0, i].isWalkable) west = true;
            if (tiles[i, 0].isWalkable) south = true;
        }
        var open = new List<ChunkDirection>(4);
        if (east) open.Add(ChunkDirection.East);
        if (south) open.Add(ChunkDirection.South);
        if (west) open.Add(ChunkDirection.West);
        if (north) open.Add(ChunkDirection.North);
        return open;
    }

    // ── Procedural 模式 ──

    /// <summary>
    /// 程序生成（纯随机路径）：边沿铺普通地面保连通 → 特殊地形图案放置 →
    /// 道路带/花纹区块/填充结构化 → 兜底非空。四边恒开。
    /// 模板抽取不在此处——由 ChunkTemplateAllocator 在 Generate 入口决定。
    /// </summary>
    static void GenerateProcedural(ChunkRuntime chunk, ChunkDef def, uint seed, int n)
    {
        var rng = new System.Random((int)seed);

        var tiles = new TileData[n, n];
        var assigned = new bool[n, n];
        int max = n - 1;

        // 边沿一圈：随机取 normalTiles（Normal 可走），保证 Chunk 间连通
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
        {
            if (x != 0 && y != 0 && x != max && y != max) continue;
            var prefab = PickFromPool(def != null ? def.normalTiles : null, rng);
            tiles[x, y] = new TileData(x, y, prefab, WalkableOf(prefab), TerrainKind.Normal);
            assigned[x, y] = true;
        }

        // 特殊地形图案（伤害区/阻挡物，内部区域，防重叠）
        PlacePatterns(def, rng, tiles, assigned, n);

        // 地面结构化（道路带 → 花纹区块 → 填充，全部剩余格 Normal）
        PlaceStructuredGround(def, rng, tiles, assigned, n);

        // 兜底：仍为空格随机取 normalTiles（每 Tile 非空是硬要求）
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
        {
            if (assigned[x, y]) continue;
            var prefab = PickFromPool(def != null ? def.normalTiles : null, rng);
            tiles[x, y] = new TileData(x, y, prefab, WalkableOf(prefab), TerrainKind.Normal);
            assigned[x, y] = true;
        }

        // 边沿全 Normal 可走 → 四边恒开
        chunk.SetTiles(tiles, new List<ChunkDirection>(ChunkCoord.AllDirections));
    }

    /// <summary>
    /// 图案放置：目标数 = min 必放，之后以固定概率递增至 max；每次从各类 patterns 合并加权抽取
    /// (类别, 形状)；单图案尝试 MaxPlaceTries 次随机锚点+方向，覆盖格须全部未分配且不在边沿；
    /// 放不下就放弃（不硬凑）。防重叠：assigned 占用标记，先放先占，逐格严格检查。
    /// </summary>
    static void PlacePatterns(ChunkDef def, System.Random rng, TileData[,] tiles, bool[,] assigned, int n)
    {
        if (def == null) return;
        int target = Mathf.Max(0, def.minPatternsPerChunk);
        int maxCount = Mathf.Max(target, def.maxPatternsPerChunk);
        while (target < maxCount && rng.NextDouble() < PatternContinueChance) target++;

        for (int p = 0; p < target; p++)
        {
            if (!TryPickPattern(def, rng, out TerrainKind poolKind, out PatternShape shape)) break;
            var prefab = PickFromPool(PoolOf(def, poolKind), rng);
            if (prefab == null) continue;
            // kind 由 prefab 语义自动推导（触发逻辑/碰撞体），不依赖手配字段；池类别仅决定抽取来源
            var kind = TileSemantics.ResolveKind(prefab);
            TryPlaceShape(shape, rng, tiles, assigned, n, prefab, kind);
        }
    }

    /// <summary>各类 patterns 拍平合并加权抽取（权重 ≤0 或对应池为空的条目不参与）。</summary>
    static bool TryPickPattern(ChunkDef def, System.Random rng, out TerrainKind kind, out PatternShape shape)
    {
        kind = TerrainKind.Normal;
        shape = PatternShape.Single;

        pickBuffer.Clear();
        CollectPatterns(def.triggerPatterns, def.triggerTiles, TerrainKind.Trigger);
        CollectPatterns(def.decorationPatterns, def.decorationTiles, TerrainKind.Decoration);
        if (pickBuffer.Count == 0) return false;

        float total = 0f;
        for (int i = 0; i < pickBuffer.Count; i++) total += pickBuffer[i].weight;
        if (total <= 0f) return false;

        double roll = rng.NextDouble() * total;
        for (int i = 0; i < pickBuffer.Count; i++)
        {
            roll -= pickBuffer[i].weight;
            if (roll >= 0) continue;
            kind = pickBuffer[i].kind;
            shape = pickBuffer[i].shape;
            return true;
        }
        kind = pickBuffer[pickBuffer.Count - 1].kind;
        shape = pickBuffer[pickBuffer.Count - 1].shape;
        return true;
    }

    /// <summary>收集某类别的有效图案候选（patterns 非空且对应池非空才参与）。</summary>
    static void CollectPatterns(List<TerrainPattern> patterns, List<GameObject> pool, TerrainKind kind)
    {
        if (patterns == null || patterns.Count == 0) return;
        if (pool == null || pool.Count == 0) return;
        for (int i = 0; i < patterns.Count; i++)
        {
            var p = patterns[i];
            if (p == null || p.weight <= 0f) continue;
            pickBuffer.Add(new PatternCandidate { kind = kind, shape = p.shape, weight = p.weight });
        }
    }

    /// <summary>类别对应的 prefab 池。</summary>
    static List<GameObject> PoolOf(ChunkDef def, TerrainKind kind)
    {
        switch (kind)
        {
            case TerrainKind.Trigger: return def.triggerTiles;
            case TerrainKind.Decoration: return def.decorationTiles;
            default: return def.normalTiles;
        }
    }

    /// <summary>
    /// 尝试整块放置一个形状：随机内部锚点 + 随机朝向，覆盖格须全部在内部且未分配；
    /// 成功则整块分配同一 prefab；MaxPlaceTries 次失败返回 false（调用方放弃）。
    /// </summary>
    static bool TryPlaceShape(PatternShape shape, System.Random rng, TileData[,] tiles, bool[,] assigned, int n, GameObject prefab, TerrainKind kind)
    {
        var rotations = ShapeRotations(shape);
        for (int t = 0; t < MaxPlaceTries; t++)
        {
            var offsets = rotations[rng.Next(rotations.Length)];
            int ax = rng.Next(1, n - 1);
            int ay = rng.Next(1, n - 1);

            bool fits = true;
            for (int i = 0; i < offsets.Length; i++)
            {
                int x = ax + offsets[i].x, y = ay + offsets[i].y;
                if (x < 1 || y < 1 || x >= n - 1 || y >= n - 1 || assigned[x, y])
                {
                    fits = false;
                    break;
                }
            }
            if (!fits) continue;

            bool walkable = WalkableOf(prefab);
            for (int i = 0; i < offsets.Length; i++)
            {
                int x = ax + offsets[i].x, y = ay + offsets[i].y;
                tiles[x, y] = new TileData(x, y, prefab, walkable, kind);
                assigned[x, y] = true;
            }
            return true;
        }
        return false;
    }

    /// <summary>形状朝向表（Single/Square2 仅 1 朝向，Line2/Line3 横纵 2 朝向，LShape 4 朝向）。</summary>
    static Vector2Int[][] ShapeRotations(PatternShape shape)
    {
        switch (shape)
        {
            case PatternShape.Line2: return shapeLine2;
            case PatternShape.Line3: return shapeLine3;
            case PatternShape.Square2: return shapeSquare2;
            case PatternShape.LShape: return shapeL;
            default: return shapeSingle;
        }
    }

    // ── 地面结构化（道路带 → 花纹区块 → 填充） ──

    /// <summary>
    /// 地面结构化：把剩余未分配格（全部 Normal）排布为
    /// 可选道路带（一条横/竖，宽 1）+ 花纹区块（plazaCount 个 4×4 循环图案）+ 填充（fillTiles）。
    /// 不覆盖已分配的特殊地形与边沿。
    /// </summary>
    static void PlaceStructuredGround(ChunkDef def, System.Random rng, TileData[,] tiles, bool[,] assigned, int n)
    {
        if (def == null) return;

        // 道路带：一条横或竖，宽 1，避开边沿 1 格
        if (def.roadTiles != null && def.roadTiles.Count > 0 && rng.NextDouble() < Mathf.Clamp01(def.roadChance))
        {
            bool horizontal = rng.Next(2) == 0;
            int lane = rng.Next(1, n - 1);
            var roadPrefab = PickFromPool(def.roadTiles, rng);
            for (int i = 0; i < n; i++)
            {
                int x = horizontal ? i : lane;
                int y = horizontal ? lane : i;
                if (!assigned[x, y])
                {
                    tiles[x, y] = new TileData(x, y, roadPrefab, WalkableOf(roadPrefab), TerrainKind.Normal);
                    assigned[x, y] = true;
                }
            }
        }

        // 花纹区块：plazaCount 个 4×4，整块 plazaTiles 循环图案
        if (def.plazaTiles != null && def.plazaTiles.Count > 0)
        {
            int size = 4;
            int count = Mathf.Max(0, def.plazaCount);
            for (int p = 0; p < count; p++)
            {
                bool placed = false;
                for (int t = 0; t < MaxPlaceTries && !placed; t++)
                {
                    int px = rng.Next(1, n - 1 - size);
                    int py = rng.Next(1, n - 1 - size);
                    if (!PlazaFits(tiles, assigned, px, py, size, n)) continue;
                    PlacePlaza(tiles, assigned, def, px, py, size);
                    placed = true;
                }
            }
        }

        // 填充：剩余未分配格全 Normal，fillTiles 轻继承
        FillGround(def, rng, tiles, assigned, n);
    }

    /// <summary>花纹区块是否可放置：覆盖格全部在内部且未分配。</summary>
    static bool PlazaFits(TileData[,] tiles, bool[,] assigned, int px, int py, int size, int n)
    {
        for (int x = px; x < px + size; x++)
        for (int y = py; y < py + size; y++)
        {
            if (x < 1 || y < 1 || x >= n - 1 || y >= n - 1 || assigned[x, y]) return false;
        }
        return true;
    }

    /// <summary>整块填 plazaTiles 循环图案：按 (x - px + y - py) % count 确定格图案（对角花纹）。</summary>
    static void PlacePlaza(TileData[,] tiles, bool[,] assigned, ChunkDef def, int px, int py, int size)
    {
        int count = def.plazaTiles.Count;
        for (int x = px; x < px + size; x++)
        for (int y = py; y < py + size; y++)
        {
            var prefab = def.plazaTiles[(x - px + y - py) % count];
            tiles[x, y] = new TileData(x, y, prefab, WalkableOf(prefab), TerrainKind.Normal);
            assigned[x, y] = true;
        }
    }

    /// <summary>
    /// 填充（剩余未分配格全 Normal）：撒种子 + 波前扩散继承。
    /// 未分配格洗牌（Fisher-Yates，确定性）后取前 4 个作种子，每种子独立取 fillTiles 图案；
    /// 多轮生长：每个已分配地面格的未分配邻居按 groundSpreadChance 概率继承相邻图案，否则随机取；
    /// 轮数上限防死循环；仍空者由调用方兜底。fillTiles 为空时回退 normalTiles。
    /// </summary>
    static void FillGround(ChunkDef def, System.Random rng, TileData[,] tiles, bool[,] assigned, int n)
    {
        var fillPool = def != null && def.fillTiles != null && def.fillTiles.Count > 0 ? def.fillTiles
                     : (def != null ? def.normalTiles : null);
        if (fillPool == null || fillPool.Count == 0)
            Debug.LogWarning("[ChunkTileGenerator] fillTiles 与 normalTiles 均为空：地面将全部写空 prefab（视觉走默认地面板）。请配置 fillTiles（或复用 normalTiles）。");

        // 撒种子
        var free = new List<Vector2Int>();
        for (int x = 0; x < n; x++)
        for (int y = 0; y < n; y++)
            if (!assigned[x, y]) free.Add(new Vector2Int(x, y));
        for (int i = free.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (free[i], free[j]) = (free[j], free[i]);
        }
        int seedCount = Mathf.Min(4, free.Count);
        var frontier = new List<Vector2Int>(free.Count);
        for (int i = 0; i < seedCount; i++)
        {
            var c = free[i];
            AssignGround(tiles, assigned, c.x, c.y, PickFromPool(fillPool, rng));
            frontier.Add(c);
        }

        // 多轮生长（波前扩散）
        float spread = def != null ? Mathf.Clamp01(def.groundSpreadChance) : 0.92f;
        int maxRounds = n * n + 16;
        for (int round = 0; round < maxRounds && frontier.Count > 0; round++)
        {
            var next = new List<Vector2Int>();
            for (int i = 0; i < frontier.Count; i++)
            {
                var cell = frontier[i];
                for (int d = 0; d < 4; d++)
                {
                    int x = cell.x + DX[d], y = cell.y + DY[d];
                    if (x < 0 || y < 0 || x >= n || y >= n || assigned[x, y]) continue;

                    donorBuffer.Clear();
                    for (int e = 0; e < 4; e++)
                    {
                        int nx = x + DX[e], ny = y + DY[e];
                        if (nx < 0 || ny < 0 || nx >= n || ny >= n || !assigned[nx, ny]) continue;
                        if (tiles[nx, ny].kind != TerrainKind.Normal) continue;
                        donorBuffer.Add(tiles[nx, ny].prefab);
                    }

                    GameObject pick;
                    if (donorBuffer.Count > 0 && rng.NextDouble() < spread)
                        pick = donorBuffer[rng.Next(donorBuffer.Count)];
                    else
                        pick = PickFromPool(fillPool, rng);
                    AssignGround(tiles, assigned, x, y, pick);
                    next.Add(new Vector2Int(x, y));
                }
            }
            frontier = next;
        }
    }

    /// <summary>写入一格 Normal 地面（可走性 = 无 solid Collider）。</summary>
    static void AssignGround(TileData[,] tiles, bool[,] assigned, int x, int y, GameObject prefab)
    {
        tiles[x, y] = new TileData(x, y, prefab, WalkableOf(prefab), TerrainKind.Normal);
        assigned[x, y] = true;
    }

    // ── 可走性（prefab 自带 solid Collider 即阻挡） ──

    /// <summary>
    /// 逻辑通行性快照：prefab 无 solid Collider 即可通行（= 无物理阻挡物）。
    /// 物理阻挡由模型 Collider 精确决定（SphereCast），此快照仅用于刷怪选取/开放边等逻辑判定。
    /// </summary>
    public static bool WalkableOf(GameObject prefab)
    {
        return !TileSemantics.HasSolidCollider(prefab);
    }

    /// <summary>
    /// 从池随机取一个非空 prefab；池空或全空槽返回 null（调用方负责防御）。
    /// 随机起点 + 线性扫描首个非空槽：rng 消耗固定 1 次/调用（不破坏确定性），容忍池内 null 槽。
    /// </summary>
    public static GameObject PickFromPool(List<GameObject> pool, System.Random rng)
    {
        if (pool == null || pool.Count == 0) return null;
        int start = rng.Next(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            var t = pool[(start + i) % pool.Count];
            if (t != null) return t;
        }
        return null;
    }

    // ── 神龛放置（出生点 Chunk Tile） ──

    /// <summary>
    /// 在指定 Chunk 内放置神龛 Tile（kind=Decoration，走 Tile 可视化与碰撞链路）。
    /// preferred：玩家出生点 tile 下标（可选）——在该格 3×3 邻域内按确定性顺序
    /// （中心→上→下→左→右→四对角）取第一个 Normal 可走格放置，保证神龛 tile 贴近出生点；
    /// 邻域无可用格时回退旧逻辑（内部区域随机 Normal 格，确定性种子）。
    /// </summary>
    public static void PlaceShrine(ChunkRuntime chunk, GameObject shrinePrefab, uint seed, Vector2Int? preferred = null)
    {
        if (chunk == null || chunk.Tiles == null || shrinePrefab == null) return;
        var tiles = chunk.Tiles;
        int n = tiles.GetLength(0);
        var rng = new System.Random(unchecked((int)seed) ^ 0x5E11);
        var walkable = WalkableOf(shrinePrefab);
        var kind = TileSemantics.ResolveKind(shrinePrefab);

        // ① preferred 3×3 邻域优先（确定性顺序，同 seed 稳定）
        if (preferred.HasValue)
        {
            var p = preferred.Value;
            var offsets = new Vector2Int[]
            {
                Vector2Int.zero, Vector2Int.up, Vector2Int.down,
                Vector2Int.left, Vector2Int.right,
                new Vector2Int(1, 1), new Vector2Int(1, -1),
                new Vector2Int(-1, 1), new Vector2Int(-1, -1),
            };
            foreach (var off in offsets)
            {
                int x = p.x + off.x, y = p.y + off.y;
                if (x < 1 || x >= n - 1 || y < 1 || y >= n - 1) continue;
                var t = tiles[x, y];
                if (t.kind == TerrainKind.Normal && t.isWalkable)
                {
                    tiles[x, y] = new TileData(x, y, shrinePrefab, walkable, kind);
                    return;
                }
            }
        }

        // ② 回退：内部区域随机 Normal 格
        var normalCandidates = new List<Vector2Int>();
        var anyInternal = new List<Vector2Int>();
        for (int x = 1; x < n - 1; x++)
        for (int y = 1; y < n - 1; y++)
        {
            var t = tiles[x, y];
            anyInternal.Add(new Vector2Int(x, y));
            if (t.kind == TerrainKind.Normal && t.isWalkable)
                normalCandidates.Add(new Vector2Int(x, y));
        }

        var pool = normalCandidates.Count > 0 ? normalCandidates : anyInternal;
        if (pool.Count == 0) return;

        int idx = rng.Next(pool.Count);
        var cell = pool[idx];
        tiles[cell.x, cell.y] = new TileData(cell.x, cell.y, shrinePrefab, walkable, kind);
    }

}

/// <summary>
/// 全局模板分配器（单局唯一实例，由 MapStreamingSystem 持有）：
/// 协调「随机生成 ↔ 预设模板」的融合与模板的全局约束，保证确定性 + 重生成一致。
///
/// 语义（全局级，整张地图共享）：
///   · templateWeight —— 每个 Chunk 走模板（而非随机）的比重；
///   · mustGenerate —— 该模板整张地图至少出现一次；
///   · maxCount —— 该模板地图内总数上限（≤0 不限制）；
///   · weight —— 模板抽取权重（≤0 且非 mustGenerate 不参与）。
///
/// 确定性关键：分配在 Chunk 首次遇到时确定并记入 assigned（coord → layout），之后重生成直接复用，
/// 不重复计数——规避「Chunk 离开范围回收、重新进入重生成」与全局计数冲突的问题。
/// 未分配的 coord 若被重生成访问，因随机源 Hash(coord, seed) 纯函数，只要计数状态一致结果即一致；
/// 但计数会随生成顺序漂移，故强制缓存 assigned 锁死首定结果。
/// </summary>
public class ChunkTemplateAllocator
{
    /// <summary>coord → 已分配的模板（null 槽 = 走随机）。首次确定后锁死。</summary>
    readonly Dictionary<ChunkCoord, FixedChunkLayout> assigned = new Dictionary<ChunkCoord, FixedChunkLayout>();
    /// <summary>模板 → 已分配次数（全局计数，maxCount 上限依据）。</summary>
    readonly Dictionary<FixedChunkLayout, int> counts = new Dictionary<FixedChunkLayout, int>();

    /// <summary>
    /// 解析某 Chunk 应采用的模板布局；null = 走纯随机生成。
    /// 首次遇到该 coord 时：按 templateWeight 决定是否走模板，走则从有效模板池加权抽取并计数；
    /// 结果记入 assigned，重生成复用。
    /// </summary>
    public FixedChunkLayout Resolve(ChunkCoord coord, ChunkDef def, uint seed)
    {
        if (assigned.TryGetValue(coord, out var cached))
            return cached; // 重生成：复用首定结果（不重复计数）

        FixedChunkLayout result = null;
        if (def != null && def.templateEntries != null && def.templateEntries.Count > 0)
        {
            // seed 已是 ChunkSeed(coord, salt)（含 coord 信息），直接用即可；不可再经 HashCoord 二次
            // 哈希（HashCoord 与 ChunkSeed 同构，二次异或会把 coord 项抵消，导致所有 Chunk 同种子）。
            var rng = new System.Random((int)seed);
            if (rng.NextDouble() < Mathf.Clamp01(def.templateWeight))
            {
                var entry = PickEntry(def, rng);
                if (entry != null && entry.layout != null)
                {
                    result = entry.layout;
                    counts.TryGetValue(result, out int c);
                    counts[result] = c + 1;
                }
            }
        }

        assigned[coord] = result;
        return result;
    }

    /// <summary>已分配的模板数（调试/统计用）。</summary>
    public int AssignedCount => assigned.Count;

    /// <summary>某模板已分配次数（调试/统计用）。</summary>
    public int CountOf(FixedChunkLayout layout)
    {
        return layout != null && counts.TryGetValue(layout, out var c) ? c : 0;
    }

    /// <summary>
    /// 加权抽取一个模板条目：
    ///   · 优先保证 mustGenerate 且尚未出现（count==0）的模板（给足权重，确保首抽必中一个）；
    ///   · 已达 maxCount 上限的模板从池中移除；
    ///   · weight ≤0 且非 mustGenerate 的模板不参与；
    ///   · 池空返回 null（走随机）。
    /// </summary>
    ChunkTemplateEntry PickEntry(ChunkDef def, System.Random rng)
    {
        var entries = def.templateEntries;

        // ① 收集有效候选：未达上限、weight>0 或 mustGenerate
        var pool = new List<ChunkTemplateEntry>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || e.layout == null) continue;
            counts.TryGetValue(e.layout, out int c);
            if (e.maxCount > 0 && c >= e.maxCount) continue; // 达上限移除
            pool.Add(e);
        }
        if (pool.Count == 0) return null;

        // ② mustGenerate 且尚未出现的模板优先：若存在则只在其间抽取（保证至少出现一次）
        bool anyMust = false;
        for (int i = 0; i < pool.Count; i++)
            if (pool[i].mustGenerate) { anyMust = true; break; }
        if (anyMust)
        {
            // 只保留 mustGenerate 且 count==0 的（未满足的必生成模板）
            var mustPool = new List<ChunkTemplateEntry>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].mustGenerate) continue;
                counts.TryGetValue(pool[i].layout, out int c);
                if (c == 0) mustPool.Add(pool[i]);
            }
            if (mustPool.Count > 0) pool = mustPool; // 优先满足未出现的必生成模板
            // 否则（mustGenerate 均已满足）退化为普通加权抽取，pool 保持不变
        }

        // ③ 加权抽取：weight ≤0（且非必生成）已在上一步天然排除（mustGenerate 无 weight 要求）
        float total = 0f;
        for (int i = 0; i < pool.Count; i++) total += Mathf.Max(0f, pool[i].weight);
        if (total <= 0f)
        {
            // 全 0 权重（mustGenerate 可能 weight=0）：等概率兜底
            return pool[rng.Next(pool.Count)];
        }
        float roll = (float)rng.NextDouble() * total;
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= Mathf.Max(0f, pool[i].weight);
            if (roll <= 0f) return pool[i];
        }
        return pool[pool.Count - 1];
    }
}
