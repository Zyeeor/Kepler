using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 预设模板条目：一个手摆布局 + 全局约束。
/// 由全局模板分配器统一管理：mustGenerate 保证整张地图至少出现一次；maxCount 是地图内该模板
/// 总数上限（抽中达到上限后从池中移除）；weight 是抽取权重（≤0 不参与抽取）。
/// </summary>
[Serializable]
public class ChunkTemplateEntry
{
    [Tooltip("手摆布局资产（每格一个 prefab，玩法经 TileSemantics 推导）。")]
    public FixedChunkLayout layout;
    [Tooltip("是否必须生成：整张地图至少出现一次（全局分配器优先保证）。")]
    public bool mustGenerate = false;
    [Tooltip("最大生成数量：地图内该模板总数上限（≤0 视为不限制）。")]
    [Min(0)] public int maxCount = 1;
    [Tooltip("抽取权重（≤0 不参与抽取）。")]
    [Min(0f)] public float weight = 1f;
}

/// <summary>
/// 装饰地块条目：一个装饰 prefab + 其在单个 Chunk 内的最大生成数。
/// maxPerChunk &lt; 0 表示不限制（默认）；&gt;=0 时该 prefab 在每个 Chunk 内最多放置该数量，
/// 用于避免同种装饰物相邻重复 / 密集堆叠。
/// </summary>
[Serializable]
public class DecorationTileEntry
{
    [Tooltip("装饰地块 prefab（玩法由自带组件推导）。")]
    public GameObject prefab;
    [Tooltip("该装饰物在每个 Chunk 内的最大生成数；-1 = 不限制。")]
    public int maxPerChunk = -1;
}

/// <summary>
/// 地形图案形状（程序生成：整块放置于 Chunk 内部区域，不触碰边沿）。
/// </summary>
public enum PatternShape
{
    /// <summary>单格 1×1。</summary>
    Single = 0,
    /// <summary>直线 1×2（横向/纵向随机）。</summary>
    Line2 = 1,
    /// <summary>直线 1×3（横向/纵向随机）。</summary>
    Line3 = 2,
    /// <summary>方块 2×2。</summary>
    Square2 = 3,
    /// <summary>L 形 3 格直角（2×2 包围盒缺一角，4 朝向随机）。</summary>
    LShape = 4,
}

/// <summary>
/// 地形图案条目（Inspector 友好）：形状 + 抽取权重。
/// 各类别列表（triggerPatterns/decorationPatterns 等）合并后按 weight 加权抽取。
/// </summary>
[Serializable]
public class TerrainPattern
{
    [Tooltip("图案形状。")]
    public PatternShape shape = PatternShape.Single;
    [Tooltip("抽取权重（≤0 不参与抽取）。")]
    [Min(0f)] public float weight = 1f;

    public TerrainPattern() { }

    public TerrainPattern(PatternShape shape, float weight)
    {
        this.shape = shape;
        this.weight = weight;
    }
}

/// <summary>
/// Chunk 模板配置（SO）：一块 Chunk 的 Tile 生成规则 + 刷怪表 + 邻接偏好。
/// 唯一生成模式 = 随机生成与预设模板融合：每个 Chunk 按「随机比重」决定走模板（从模板库按权重抽取
/// 并逐格映射）还是纯随机（边沿铺普通地面 → 特殊地形图案 → 道路带/花纹区块/填充）。
/// 模板带全局约束：mustGenerate（整张地图至少出现一次）、maxCount（地图内该模板总数上限）、
/// weight（抽取权重），由全局模板分配器在 Chunk 首次生成时确定并复用，保证确定性。
/// 池直接存 prefab（GameObject），玩法语义（地形类别/伤害/碰撞）由 prefab 自带组件推导（TileSemantics）。
/// </summary>
[CreateAssetMenu(fileName = "ChunkDef", menuName = "Kepler/Map/Chunk Def")]
public class ChunkDef : ScriptableObject
{
    [Tooltip("配置唯一 id（调试与 WorldPlan 引用用）。")]
    public string id;

    [Header("模板库（预设模板，与随机生成融合）")]
    [Tooltip("候选模板条目列表：{ 布局, 必须生成, 最大数量, 权重 }。按 weight 加权抽取；抽中次数达到 maxCount 后从池中移除；mustGenerate 的模板保证整张地图至少出现一次。")]
    public List<ChunkTemplateEntry> templateEntries = new List<ChunkTemplateEntry>();
    [Tooltip("该 Chunk 走模板（而非随机生成）的比重。0 = 全随机；1 = 全模板。")]
    [Range(0f, 1f)] public float templateWeight = 0f;

    [Header("地形 prefab 池（每项 = 一格大小的 Tile prefab，玩法由自带组件推导）")]
    [Tooltip("普通地面候选（可行走）。程序生成的边沿与地面填充均从此取；为空时告警（每 Tile 非空是硬要求）。")]
    public List<GameObject> normalTiles = new List<GameObject>();
    [Tooltip("可触发地块候选（岩浆/地刺/传送/事件等触发区，prefab 自带 TerrainEffectTile）；为空时 triggerPatterns 不参与抽取（该类不生成）。")]
    [FormerlySerializedAs("lavaTiles")] [FormerlySerializedAs("hazardTiles")] public List<GameObject> triggerTiles = new List<GameObject>();
    [Tooltip("装饰地块候选（柱子/雕塑等视觉装饰）。阻挡由 prefab 自带 solid Collider 决定（物理精确挡路）；为空时 decorationPatterns 不参与抽取（该类不生成）。每条可配 maxPerChunk：该装饰物每个 Chunk 内的最大生成数，-1 不限制。")]
    public List<DecorationTileEntry> decorationTiles = new List<DecorationTileEntry>();

    // 旧版 decorationTiles 是 List<GameObject>（无 maxPerChunk），升级时自动迁移为默认条目（-1 不限制）。
    [SerializeField, HideInInspector, FormerlySerializedAs("decorationTiles")]
    private List<GameObject> decorationTilesLegacy = new List<GameObject>();

    void OnEnable()
    {
        if (decorationTilesLegacy == null || decorationTilesLegacy.Count == 0) return;
        foreach (var g in decorationTilesLegacy)
        {
            if (g == null) continue;
            if (decorationTiles.Exists(e => e != null && e.prefab == g)) continue;
            decorationTiles.Add(new DecorationTileEntry { prefab = g, maxPerChunk = -1 });
        }
        decorationTilesLegacy.Clear();
    }

    [Header("形状图案（程序生成：各类合并按权重抽取，整块放置于内部区域）")]
    [Tooltip("可触发地块图案（岩浆/地刺等触发区）。默认 Line3(1) + Single(0.5)。")]
    [FormerlySerializedAs("lavaPatterns")] [FormerlySerializedAs("hazardPatterns")] public List<TerrainPattern> triggerPatterns = new List<TerrainPattern>
    {
        new TerrainPattern(PatternShape.Line3, 1f),
        new TerrainPattern(PatternShape.Single, 0.5f),
    };
    [Tooltip("装饰地块图案。默认 Single(1)。")]
    [FormerlySerializedAs("blockerPatterns")] public List<TerrainPattern> decorationPatterns = new List<TerrainPattern>
    {
        new TerrainPattern(PatternShape.Single, 1f),
    };

    [Header("图案数量（先按 min 必放，再按固定概率递增至 max；内部放不下则少放，不硬凑）")]
    [Min(0)] public int minPatternsPerChunk = 1;
    [Min(0)] public int maxPatternsPerChunk = 4;

    [Header("地面视觉方案（Normal 的结构化排布，Procedural）")]
    [Tooltip("道路地砖（可空，空 = 本 Chunk 不生成道路带）。")]
    public List<GameObject> roadTiles = new List<GameObject>();
    [Tooltip("花纹区块地砖组：一组 prefab 凑成一块规律区（如 4×4 对角花纹）。")]
    public List<GameObject> plazaTiles = new List<GameObject>();
    [Tooltip("普通填充地砖（默认兜底，建议复用 normalTiles）。为空时回退 normalTiles。")]
    public List<GameObject> fillTiles = new List<GameObject>();
    [Tooltip("本 Chunk 出道路带的概率。")]
    [Range(0f, 1f)] public float roadChance = 0.5f;
    [Tooltip("花纹区块个数（每块按 plazaTiles 循环图案排布）。")]
    [Min(0)] public int plazaCount = 1;
    [Tooltip("填充邻接继承概率：剩余格以该概率继承相邻填充图案，否则从 fillTiles 随机取。")]
    [Range(0f, 1f)] public float groundSpreadChance = 0.92f;

    // 2026-08-18 删除刷怪表（waveTable）：地图静态怪模式已移除，怪物由波次玩法（WaveManager）驱动

    [Header("邻接偏好")]
    [Tooltip("偏好的相邻 Chunk 模板（宏观区域排布时参考，暂未接入）。")]
    public List<ChunkDef> preferredNeighbors = new List<ChunkDef>();

    [Header("兜底")]
    [Tooltip("是否安全预制：出入口校验重摇仍失败时替换使用。普通模板保持 false。")]
    public bool isSafeFallback = false;

#if UNITY_EDITOR
    /// <summary>
    /// 配置防御：normalTiles 空告警（每 Tile 非空是硬要求）；
    /// patterns 权重全 0 / 有图案无池告警；池 prefab 推导 kind 不匹配提示；min > max 提示；
    /// 模板条目 layout 空 / weight≤0 / maxCount<0 提示。
    /// </summary>
    void OnValidate()
    {
        if (normalTiles.Count == 0)
            Debug.LogWarning($"[ChunkDef] {name}.normalTiles 为空：程序生成将无法填充地面（每 Tile 非空是硬要求）。", this);
        CheckTemplateEntries();
        if (minPatternsPerChunk > maxPatternsPerChunk)
            Debug.LogWarning($"[ChunkDef] {name} minPatternsPerChunk({minPatternsPerChunk}) > maxPatternsPerChunk({maxPatternsPerChunk})：将按 max 截断。", this);
        if (fillTiles.Count == 0 && normalTiles.Count > 0)
            Debug.LogWarning($"[ChunkDef] {name}.fillTiles 为空：将回退 normalTiles 作填充（建议直接复用 normalTiles）。", this);
        CheckPool(normalTiles, TerrainKind.Normal, nameof(normalTiles));
        CheckPool(triggerTiles, TerrainKind.Trigger, nameof(triggerTiles));
        CheckPool(decorationTiles, TerrainKind.Decoration, nameof(decorationTiles));
        CheckPool(roadTiles, TerrainKind.Normal, nameof(roadTiles));
        CheckPool(plazaTiles, TerrainKind.Normal, nameof(plazaTiles));
        CheckPool(fillTiles, TerrainKind.Normal, nameof(fillTiles));
        CheckPatterns(triggerPatterns, triggerTiles, nameof(triggerPatterns), nameof(triggerTiles));
        CheckPatterns(decorationPatterns, decorationTiles, nameof(decorationPatterns), nameof(decorationTiles));
    }

    /// <summary>模板条目检查：layout 空 / weight≤0（mustGenerate 例外）/ maxCount<0 提示。</summary>
    void CheckTemplateEntries()
    {
        for (int i = 0; i < templateEntries.Count; i++)
        {
            var e = templateEntries[i];
            if (e == null) { Debug.LogWarning($"[ChunkDef] {name}.templateEntries[{i}] 为 null，请删除或补全。", this); continue; }
            if (e.layout == null)
                Debug.LogWarning($"[ChunkDef] {name}.templateEntries[{i}] 的 layout 为空：该模板不参与生成。", this);
            if (e.maxCount < 0)
                Debug.LogWarning($"[ChunkDef] {name}.templateEntries[{i}] maxCount({e.maxCount}) < 0：将视为不限制（建议用 0 表示不限制）。", this);
            if (e.weight <= 0f && !e.mustGenerate)
                Debug.LogWarning($"[ChunkDef] {name}.templateEntries[{i}] weight({e.weight}) ≤ 0 且非 mustGenerate：该模板永不抽中。", this);
        }
    }

    /// <summary>
    /// 池检查：分类由 TileSemantics.ResolveKind 自动推导
    /// （Trigger=挂 TerrainEffectTile / Decoration=带 solid Collider / Normal=其余），与池类别不符时告警。
    /// </summary>
    void CheckPool(List<GameObject> pool, TerrainKind expect, string poolName)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var prefab = pool[i];
            if (prefab == null) continue;
            var resolved = TileSemantics.ResolveKind(prefab);
            if (resolved != expect)
                Debug.LogWarning($"[ChunkDef] {name}.{poolName}[{i}] = '{prefab.name}' 推导类别 {resolved}，与池类别 {expect} 不匹配（触发地块应挂 TerrainEffectTile；装饰地块应自带 solid Collider——如 Capsule/Box/Mesh，生成器不再兜底补碰撞）。", this);
        }
    }

    /// <summary>池检查（装饰条目版）：逐条目取 prefab 做同样的语义比对。</summary>
    void CheckPool(List<DecorationTileEntry> pool, TerrainKind expect, string poolName)
    {
        if (pool == null) return;
        for (int i = 0; i < pool.Count; i++)
        {
            var prefab = pool[i] != null ? pool[i].prefab : null;
            if (prefab == null) continue;
            var resolved = TileSemantics.ResolveKind(prefab);
            if (resolved != expect)
                Debug.LogWarning($"[ChunkDef] {name}.{poolName}[{i}] = '{prefab.name}' 推导类别 {resolved}，与池类别 {expect} 不匹配（装饰地块应自带 solid Collider——如 Capsule/Box/Mesh，生成器不再兜底补碰撞）。", this);
        }
    }

    /// <summary>图案配置检查：非空但权重和 ≤0 → 永不抽中；非空但池空 → 该类不生成。</summary>
    void CheckPatterns(List<TerrainPattern> patterns, List<GameObject> pool, string patternsName, string poolName)
    {
        if (patterns == null || patterns.Count == 0) return;
        float sum = 0f;
        for (int i = 0; i < patterns.Count; i++)
            if (patterns[i] != null) sum += Mathf.Max(0f, patterns[i].weight);
        if (sum <= 0f)
            Debug.LogWarning($"[ChunkDef] {name}.{patternsName} 权重全为 0：该类别图案永不抽中。", this);
        else if (pool == null || pool.Count == 0)
            Debug.LogWarning($"[ChunkDef] {name}.{patternsName} 已配置但 {poolName} 为空：该类别不生成（池空防御）。", this);
    }

    /// <summary>图案配置检查（装饰条目版）：条目全空（无任何 prefab）视为池空。</summary>
    void CheckPatterns(List<TerrainPattern> patterns, List<DecorationTileEntry> pool, string patternsName, string poolName)
    {
        if (patterns == null || patterns.Count == 0) return;
        float sum = 0f;
        for (int i = 0; i < patterns.Count; i++)
            if (patterns[i] != null) sum += Mathf.Max(0f, patterns[i].weight);
        if (sum <= 0f)
            Debug.LogWarning($"[ChunkDef] {name}.{patternsName} 权重全为 0：该类别图案永不抽中。", this);
        else if (pool == null || pool.Count == 0 || pool.TrueForAll(e => e == null || e.prefab == null))
            Debug.LogWarning($"[ChunkDef] {name}.{patternsName} 已配置但 {poolName} 为空：该类别不生成（池空防御）。", this);
    }
#endif
}
