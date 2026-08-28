# 地图流送、确定性生成与模板编辑器技术设计与实现文档

> **文档类型**：技术设计 / 实现说明（Implementation）
>
> **状态**：核心链路已完成；部分宏观世界、边界签名、对象池和 NavMesh 能力为后续扩展
>
> **整理日期**：2026-08-28
>
> **归属**：`Docs/00_Project/Engineering/`（工程实现文档，随仓库分发）
>
> **配对设计文档**：`MapStreaming_Design.md`（Design 阶段，位于本地 `.docs` 文档包，未随仓库分发）→ 本文为其实现落地
>
> **事实基线**：本文以当前源码和资产为准，描述已实现的 Chunk 流送、纯数据生成、随机/模板融合、固定布局、多格装饰和 Unity 编辑器链路，并单独列出设计预留，避免将目标架构误写成现状。

---

## 1. 目标与问题定义

### 1.1 需要解决的问题

游戏地图不是一张一次性加载的静态场景，而是由大量 Chunk 组成的可探索空间。系统需要同时处理：

1. 玩家移动时只维持附近 Chunk 的必要运行状态；
2. 远处 Chunk 在逻辑、视觉和动态实体层面分阶段加载/卸载；
3. 相同世界种子和 Chunk 坐标必须得到可复现内容；
4. 程序随机地图要有边沿连通、图案防重叠和地面填充兜底；
5. 关键房间可以通过固定锚点或手工模板精确控制；
6. 模板既要支持普通地砖、触发地块，也要支持装饰物叠加和矩形多格占位；
7. 策划编辑模板时不需要直接操作序列化数组；
8. 无限地图和有限边界应由配置切换，而不是重写生成算法；
9. Chunk 反复离开/进入流送范围时，逻辑和视觉不能随机漂移。

### 1.2 核心设计结论

地图系统采用四层分离：

```text
配置资产
  → MapStreamingSystem（范围/任务/边界/Pin）
  → ChunkTileGenerator（纯数据生成）
  → ChunkRuntime（状态与数据容器）
  → ChunkVisualizer / MonsterSpawner（视觉与动态实体）
```

其中：

- `ChunkTileGenerator` 不创建 GameObject；
- `ChunkRuntime` 不是 MonoBehaviour，只保存 Chunk 逻辑数据和状态；
- `MapStreamingSystem` 只编排状态和任务，不直接铺每一格视觉；
- `ChunkVisualizer` 监听状态变化动态创建/销毁视觉；
- `MonsterSpawner` 监听状态变化处理动态怪物和快照；
- `ChunkLayoutEditorWindow` 直接编辑 `FixedChunkLayout` 资产，运行时仍动态生成，不烘焙成静态场景对象。

---

## 2. 系统架构与职责

### 2.1 模块图

```text
┌────────────────────────────────────────────────────────────┐
│ 配置资产                                                   │
│ MapStreamingSystem / ChunkDef / RegionDef / ChunkAnchor    │
│ FixedChunkLayout / Tile prefab / Decoration prefab         │
└──────────────────────────┬─────────────────────────────────┘
                           ▼
┌────────────────────────────────────────────────────────────┐
│ MapStreamingSystem                                         │
│ 世界坐标、Chunk 范围、边界、任务队列、Pin、状态转换       │
└──────────────────────────┬─────────────────────────────────┘
                           ▼
┌────────────────────────────────────────────────────────────┐
│ ChunkRuntime                                                │
│ Coord / Seed / Def / TileData[,] / Placement / OpenEdges   │
│ ChunkStreamState / OnStateChanged                          │
└───────────────┬───────────────────────────────┬────────────┘
                ▼                               ▼
┌────────────────────────────┐     ┌─────────────────────────┐
│ ChunkTileGenerator          │     │ ChunkStateStore          │
│ Generate / GenerateFixed   │     │ 动态实体内存快照         │
│ System.Random / 模板分配   │     └─────────────────────────┘
└───────────────┬────────────┘
                ▼
       TileData + DecorationPlacement
                │
       ┌────────┴────────┐
       ▼                 ▼
ChunkVisualizer       MonsterSpawner
地砖/装饰视觉          怪物生成/休眠/回收/恢复
```

### 2.2 核心类职责

| 类 | 职责 | 关键边界 |
|---|---|---|
| `MapStreamingSystem` | 计算 A/B/C/D 集合、Diff、排队、预算、边界、Pin、状态转换 | 不直接逐格 Instantiate |
| `ChunkRuntime` | 保存一个 Chunk 的纯运行时数据和状态机 | 不持有视觉 GameObject |
| `ChunkTileGenerator` | 纯数据生成，随机/模板/固定锚点 | 不触碰世界坐标 |
| `ChunkDef` | 生成池、图案、模板条目、神龛等配置 | ScriptableObject 数据源 |
| `FixedChunkLayout` | 手工模板双层网格和 placement 列表 | 不直接生成场景对象 |
| `DecorationPlacement` | 一个逻辑装饰实例的 prefab、anchor、footprint、owner id | 当前仅矩形 footprint |
| `ChunkVisualizer` | 按 Chunk 状态创建/销毁地砖和装饰 | 当前逐对象 Instantiate/Destroy |
| `MonsterSpawner` | 与 Chunk 状态绑定的动态实体生命周期 | 动态实体快照由其写入 |
| `WorldPlan` | `regionTable` 启用时按区域解析 ChunkDef | 不是完整宏观关卡图 |
| `ChunkLayoutEditorWindow` | Unity 内网格绘制、预览、擦除、Undo、资产刷新 | Editor-only |

---

## 3. 坐标与运行时配置

### 3.1 Chunk 坐标

`ChunkCoord` 是全局二维 Chunk 索引，局部网格使用 `(x, y)`，其中 `y` 对应地图世界 Z 轴。

```text
世界/地图局部坐标
  x → East
  z → North

Chunk 原点 = ChunkCoord(x,y) × (chunkSize × tileSize)
局部格中心 = ((cellX + 0.5) × tileSize,
              0,
              (cellY + 0.5) × tileSize)
```

`MapStreamingSystem` 支持通过自身 Transform 做整体平移/旋转；当前不支持非均匀缩放，因为 `ChunkVisualizer` 的 bounds 校正假设父链 scale 为 1。

### 3.2 当前默认场景参数

测试场景当前使用：

```text
chunkSize = 8 tiles
 tileSize = 2m
Chunk world size = 16m
radiusA = 25m
radiusB = 50m
radiusC = 60m
radiusD = 80m
tickInterval = 0.2s
boundaryChunkExtent = 0（无限）
```

`D > C` 是流送滞后区的硬约束，避免玩家在 C/D 临界点往返时频繁回收和重建。

### 3.3 世界种子来源

`MapStreamingSystem.Awake` 优先从 `RunSession.Instance` 读取活动对局的 `WorldSeed`：

```text
有活动 RunSession → 使用对局种子
无活动 RunSession → 使用 Inspector 的 worldSeed
```

这样新局随机种子和继续游戏读取的种子共享同一套 Chunk 生成入口。

### 3.4 地图边界

```text
boundaryChunkExtent <= 0 → boundaryEnabled = false，所有 Chunk 均可进入范围
boundaryChunkExtent >  0 → 以出生点/覆盖 Transform 所在 Chunk 为中心的矩形边界
```

边界过滤集中在 `IsInsideBoundary` 和 `ChunksInRadius`：

- A/B/C/D 所有集合都经过边界检查；
- 玩家所在 Chunk 与邻接 Chunk 的 A 保底也经过边界检查；
- 边界外不会 Prepare、Instantiate 或 Activate；
- 无限模式无需维护边界矩形。

---

## 4. A/B/C/D 流送状态机

### 4.1 范围语义

| 范围 | 语义 | 主要工作 |
|---|---|---|
| A | 完整模拟范围 | AI、战斗、事件激活 |
| B | 场景缓冲范围 | 视觉和动态实体已就位，AI 待机 |
| C | 逻辑预生成范围 | TileData、模板、开放边计算 |
| D | 卸载缓冲范围 | 离开后快照、回收；Pin 时保留 |

A 当前采用半径近似，并额外加入玩家所在 Chunk 和四邻接 Chunk；真实相机视锥投影仍是后续优化点。

### 4.2 Tick 与集合 Diff

`MapStreamingSystem.Update`：

```text
每 tickInterval 秒：
  player = GetPlayerPosition()
  RefreshPins(player)
  newA = ComputeASet(player)
  newB = ChunksInRadius(player, radiusB)
  newC = ChunksInRadius(player, radiusC)
  newD = ChunksInRadius(player, radiusD)

  对 new/current 做 Diff，生成 Job
  记录新集合

每帧：
  ProcessJobQueue()
```

四个集合都不变时直接短路。Pin 变化单独处理，即使玩家不移动，解 Pin 且 Chunk 仍在 D 外也会补发 Serialize/UnloadFull。

### 4.3 JobKind 优先级

枚举声明顺序就是排序优先级：

```text
Activate     进入 A，最高优先级
Pause        离开 A
Instantiate  进入 B
Prepare      进入 C
UnloadScene  离开 B
Serialize    离开 D 的内存快照确认
UnloadFull   离开 D，最低优先级
```

同一优先级内按入队时距离排序，近处 Chunk 优先。

入队阶段按 `kind + coord` 去重；执行阶段重新判断任务是否过期，避免玩家快速掉头后仍执行旧方向的任务。

### 4.4 每帧预算

当前代码常量：

| Job | 每帧上限 |
|---|---:|
| `Activate` | 4 |
| `Pause` | 6 |
| `Instantiate` | 4 |
| `Prepare` | 8 |
| `UnloadScene` | 4 |
| `Serialize` | 4 |
| `UnloadFull` | 6 |
| 总时间片 | 8ms |

数量预算控制任务“数量”，时间片预算控制单个复杂 Chunk 的“单价失控”。二者任一先达到就停止处理，剩余任务压缩保留到下一帧。

`MapDebugHUD` 可读取：

```text
QueuedJobCount
JobsExecutedThisFrame
LastFrameQueueMs
TimeSliceExceeded
RangeACount / RangeBCount / RangeCCount / RangeDCount
```

### 4.5 `ChunkRuntime` 状态

文件：

```text
Assets/Scripts/Levels/MapStreaming/ChunkRuntime.cs
```

```text
None
  └─ Prepare → Prepared
                  ├─ Instantiate → Dormant
                  │                    └─ Activate → Active
                  │                                      └─ Pause → Dormant
                  ├─ UnloadScene ← Dormant
                  └─ UnloadFull → Unloaded

Unloaded ── Prepare → Prepared
```

状态含义：

- `None`：没有逻辑网格；
- `Prepared`：TileData、装饰 placement、OpenEdges 已生成，未保留视觉；
- `Dormant`：视觉/动态实体已经实例化，但 AI 处于待机；
- `Active`：完整模拟；
- `Unloaded`：运行时状态已进入可回收阶段，重新进入时重新 Prepare。

`TransitionTo` 校验合法转换、支持幂等目标状态，并通过 `OnStateChanged` 通知订阅者。

---

## 5. Prepare 阶段与固定锚点

### 5.1 Chunk 选择优先级

`GetOrCreateChunk` 的选择顺序：

```text
1. fixedAnchors 命中 → 使用 ChunkAnchor
2. regionTable 非空 → 懒构造 WorldPlan，解析 ChunkDef
3. 否则 → defaultChunkDef
```

`ChunkAnchor` 支持：

```text
layout 非空
  → 完全使用 FixedChunkLayout，内容与 worldSeed 无关

layout 为空、chunkDef 非空
  → 使用指定 ChunkDef + fixedSeed 的固定程序生成

两者都为空
  → 锚点无效，告警并回退正常生成
```

固定锚点不参与全局模板分配器计数，适合出生点、Boss 房、教程房和关键剧情房。

### 5.2 Prepare 的开放边校验

`Prepare` 最多使用 `attempt = 0..3` 生成：

```text
生成 TileData
  → 读取 OpenEdges
  → openEdges.Count >= 2：Prepared
  → 随机路径失败：换 salt 重试
  → 模板/手摆路径失败：不重试，不覆盖作者布局，告警
  → 随机路径重试耗尽：ForceFallbackOpenings
```

当前程序随机路径边沿一圈强制使用普通地砖，因此四边恒开；模板路径按照实际边沿 Tile 的 `isWalkable` 计算开放边，开放边少于 2 时保留作者布局并告警。

当前未实现“相邻 Chunk 共享边界签名”：模板边沿是否匹配由策划保证，程序随机路径使用四边开放简化。

### 5.3 神龛是地图生成的一类特殊 Decoration

`MapStreamingSystem.PlanShrines` 在 Awake 阶段规划神龛所在 Chunk：

- 第一个神龛放在出生点 Chunk；
- 其余神龛按 ring 外扩；
- 神龛 Chunk 之间要求 Chebyshev 距离至少 2；
- 出生点 Chunk 可使用教程专用 `firstShrinePrefab`；
- 神龛最终通过 `ChunkTileGenerator.PlaceShrine` 进入普通 Decoration/ChunkVisual 链路。

神龛不是单独的静态场景系统，仍然遵循 Chunk 的逻辑生成、视觉实例化和流送生命周期。

---

## 6. 确定性生成设计

### 6.1 ChunkSeed

`MapStreamingSystem.ChunkSeed` 使用世界种子、Chunk 坐标和 salt 派生：

```csharp
worldSeed
  ^ (uint)(coord.x * 73856093)
  ^ (uint)(coord.y * 19349663)
  ^ (uint)(salt * 83492791)
```

这组 hash 的作用是让：

- 不同 Chunk 使用不同随机流；
- 同一 Chunk 的重试使用不同 salt；
- 生成顺序不影响基础 Chunk seed；
- 玩家离开再返回时可以重新得到同一逻辑内容。

程序生成的随机源是 `System.Random`，不是 `UnityEngine.Random`，且不使用时间作为生成输入。

### 6.2 生成入口

文件：

```text
Assets/Scripts/Levels/MapStreaming/ChunkTileGenerator.cs
```

```csharp
ChunkTileGenerator.Generate(chunk, def, seed, templateAllocator)
```

流程：

```text
allocator.Resolve(coord, def, seed)
  ├─ 返回 FixedChunkLayout → GenerateFromLayout
  └─ 返回 null             → GenerateProcedural
```

固定锚点使用：

```csharp
ChunkTileGenerator.GenerateFixed(chunk, layout, def, fixedSeed)
```

其中特殊点是：固定锚点传入 `allocator = null`，不会改变全局模板计数。

### 6.3 `GenerateProcedural` 顺序

程序随机 Chunk 的顺序是固定的：

```text
1. 创建 TileData[n,n]、assigned[n,n]、placements
2. 铺 Chunk 外圈 normalTiles，确保边沿连通
3. PlacePatterns：Trigger / Decoration 图案
4. PlaceStructuredGround：道路、花纹区块、填充
5. 其余空格用 normalTiles 兜底
6. 写入 ChunkRuntime
```

每格 `TileData` 保存：

```text
prefab
kind
isWalkable
overlayPrefab
overlayKind
overlayWalkable
overlayPlacementId
```

可走性由底层和叠加层 Collider 共同决定，不额外复制一份装饰物碰撞配置。

### 6.4 图案抽取

`triggerPatterns` 与 `decorationPatterns` 会拍平后合并成候选池，按：

```text
(TerrainKind, PatternShape, weight)
```

统一加权抽取。

每个 Chunk：

- 至少尝试 `minPatternsPerChunk` 个图案；
- 之后以 `PatternContinueChance = 0.5` 递增；
- 不超过 `maxPatternsPerChunk`；
- 每个图案最多 `MaxPlaceTries = 32` 次随机锚点/方向；
- `assigned[,]` 保证先放先占，不能覆盖已有内容；
- 程序随机图案不能占用边沿；
- 无法放置时放弃该图案，而不是强行覆盖。

支持的图案包括：

```text
Single / Line2 / Line3 / Square2 / LShape
SShape / ZShape / TShape / Cross
```

### 6.5 多格装饰

`DecorationTileEntry`：

```csharp
GameObject prefab;
int maxPerChunk;
Vector2Int footprintSize;
```

多格装饰的关键语义：

- footprint 是逻辑占用矩形；
- 一个 placement 只计一个逻辑实例；
- `maxPerChunk` 限制 prefab 的逻辑实例数量，不是格子数量；
- 覆盖格共享 `overlayPlacementId`；
- `ChunkVisualizer` 最终只 Instantiate 一次 prefab。

程序随机路径：

```text
allowBoundary = false
anchor >= 1
anchor + size <= n - 1
```

即程序随机多格装饰不能占边沿，避免随机障碍破坏当前四边开放策略。

固定布局路径：

```text
allowBoundary = true
anchor >= 0
anchor + size <= n
```

固定模板可以让多格装饰触碰 Chunk 边沿，但不能越出当前 Chunk；跨 Chunk ownership 尚未实现。

### 6.6 地面结构化

剩余地面处理顺序：

1. `roadChance` 决定横向/纵向道路带；
2. `plazaCount` 生成 4×4 花纹区块；
3. `fillTiles` 填充剩余空格；
4. `groundSpreadChance` 控制填充图案继承；
5. 最后用普通地砖池兜底。

这样特殊图案和道路不会因为填充阶段被覆盖，每格最终都有底层地砖或明确的空/告警语义。

---

## 7. 模板分配器

### 7.1 `ChunkTemplateAllocator`

位置：

```text
Assets/Scripts/Levels/MapStreaming/ChunkTileGenerator.cs
```

它由 `MapStreamingSystem` 持有单局唯一实例，内部保存：

```text
assigned: coord → FixedChunkLayout
counts:   layout → 已分配数量
```

### 7.2 分配算法

`Resolve(coord, def, seed)`：

```text
如果 assigned 已有 coord
  → 直接复用缓存结果

否则：
  以 seed 初始化 System.Random
  roll < def.templateWeight
    → 进入模板候选池
  否则
    → assigned[coord] = null，走随机
```

候选池规则：

1. `layout == null` 排除；
2. `maxCount > 0 && count >= maxCount` 排除；
3. 存在尚未满足的 `mustGenerate` 时，仅在这些模板中选择；
4. 普通候选按 `weight` 加权；
5. 全部权重为 0 时等概率兜底；
6. 结果写入 `assigned`，之后不再重新抽取。

### 7.3 为什么必须缓存坐标分配

模板有全局计数约束，单纯依赖坐标 seed 仍然不够：

```text
玩家向东探索 → 模板计数顺序 A
玩家向西探索 → 模板计数顺序 B
```

若不缓存，`mustGenerate` 和 `maxCount` 会使同一个坐标可能因为访问顺序不同得到不同模板。`assigned` 将“首次访问时的选择”锁定，避免 Chunk 离开/返回后内容漂移。

这是一种“确定性基础随机 + 有状态的全局分配缓存”的混合设计。

### 7.4 与固定锚点的关系

固定锚点优先于模板分配：

```text
fixedAnchors → GenerateFixed
普通 Chunk   → WorldPlan/defaultDef → allocator.Resolve
```

因此出生点、Boss 房和教程房不会消耗随机模板配额，也不会因为世界探索顺序变化。

---

## 8. FixedChunkLayout 数据模型

### 8.1 资产字段

文件：

```text
Assets/Scripts/Levels/MapStreaming/FixedChunkLayout.cs
```

核心字段：

```csharp
int size;
GameObject[] tiles;
GameObject[] overlayTiles;
List<DecorationPlacement> decorationPlacements;
GameObject defaultGround;
```

数组索引：

```text
y * size + x
```

运行时使用 `[x,y]` 读取，编辑器显示时把较大的 y 放在上方。

### 8.2 双层语义

#### `tiles[]` 底层

- 普通地砖；
- Trigger；
- 兼容旧格式直接放在底层的装饰物。

#### `overlayTiles[]` 旧式单格叠加

- 每格一个装饰物；
- 继续兼容历史布局；
- 运行时转换为 `1×1 DecorationPlacement`。

#### `decorationPlacements[]` 新式多格叠加

```csharp
int id;
GameObject prefab;
Vector2Int anchor;
Vector2Int footprintSize;
```

一个 placement 表示一个逻辑 GameObject。`Contains(x,y)` 供编辑器擦除与运行时占用判断。

#### `defaultGround`

模板中空格或只有叠加层时使用：

```text
FixedChunkLayout.defaultGround
  → ChunkDef.normalTiles[0]
  → 允许空地/告警兜底
```

### 8.3 旧格式兼容

`GenerateFromLayout` 逐格读取底层和叠加层：

- 底层 prefab 推导为 Decoration 时，转成默认地面 + 单格 overlay；
- 仅有叠加层时，底层使用 `defaultGround`；
- 旧式 overlay 注册独立 placement id；
- 新式 `decorationPlacements` 在后续阶段应用；
- 重叠/越界时告警并跳过新 placement，不静默破坏旧数据。

### 8.4 模板生成流程

```text
创建 n×n TileData
  → 逐格解析 tiles[] / overlayTiles[]
  → 默认地面兜底
  → 计算底层/叠加层可走性
  → 注册旧式 1×1 placement
  → 应用新式多格 placement
  → 计算 OpenEdges
  → chunk.SetTiles(...)
```

如果 `MapStreamingSystem.chunkSize != layout.size`，当前实现会告警，越界格按空处理；尚未实现自动尺寸适配/重采样。

---

## 9. ChunkVisualizer：从逻辑数据到动态视觉

文件：

```text
Assets/Scripts/Levels/MapStreaming/ChunkVisualizer.cs
```

### 9.1 订阅式视觉生命周期

`ChunkVisualizer.Start` 订阅：

```csharp
MapStreamingSystem.OnChunkStateChanged
```

状态响应：

```text
Prepared → Dormant  : BuildVisual
Dormant  → Prepared : DestroyVisual
任意 → Unloaded     : DestroyVisual 兜底
```

如果 Visualizer 启动晚于出生区同步生成，会遍历 `Registry`，对已经处于 Dormant/Active 的 Chunk 补建视觉。

### 9.2 视觉聚合根

每个 Chunk 一个聚合根：

```text
ChunkVisual_(x,y)
  ├─ Base_*_x_y
  ├─ Overlay_*_x_y
  └─ Decoration_<placementId>_<prefab>
```

离开 B 时销毁整个聚合根，避免逐对象在各系统散落清理。

### 9.3 Tile 实例化

每格地砖独立实例化，不做横向合并：

- 普通/Trigger 地砖按 `tileSize` 等比内切；
- Renderer bounds 优先，Collider bounds 兜底；
- 解析式校正模型中心 XZ 和底面 y；
- prefab 自带 Collider 决定物理阻挡；
- 装饰物保留原始 localScale，允许模型视觉跨格。

### 9.4 多格装饰实例化

循环 TileData 时：

- 旧式无 owner 的 overlay 逐格创建；
- 新式 `overlayPlacementId > 0` 不在每个格重复创建；
- 遍历 `chunk.DecorationPlacements`，按 anchor + footprint 计算目标中心；
- 每个 placement 只调用一次 `Instantiate`；
- 装饰 prefab 的 Collider 仍由 prefab 自身提供。

视觉层不消费随机数，因此同一个 `ChunkRuntime` 重建视觉不会改变内容。

### 9.5 当前性能边界

当前 Chunk 地砖视觉仍是逐对象 `Instantiate/Destroy`：

```text
8×8 Chunk → 最多 64 个底层地砖实例 + 叠加物/装饰物
```

对象池、Static Batching、GPU Instancing 和合并网格均是后续方向，需要用 Profiler 数据证明瓶颈后再选择。

---

## 10. 地图模板编辑器

### 10.1 入口与编辑目标

文件：

```text
Assets/Scripts/Editor/ChunkLayoutEditorWindow.cs
```

菜单：

```text
Kepler/Map/Chunk Layout Editor
```

编辑器直接修改 `FixedChunkLayout` 资产；运行时不会使用一个提前烘焙的场景卡片，而是根据资产在 Prepare 阶段生成逻辑数据，在 B 阶段动态实例化。

### 10.2 面板结构

```text
顶部：布局资产、创建、刷新、网格缩放
选项：底层/叠加层、手动多格、缩略图、默认地块
左侧：TileAsset prefab 刷子面板
右侧：size×size 网格、坐标轴、悬停和 footprint 预览
底部：格子信息、kind、Collider、anchor、footprint、清空
```

刷子来源：

```text
Assets/Prefabs/Room/RoomObjects/TileAsset
```

类别通过 `TileSemantics.ResolveKind` 推导：

```text
TerrainEffectTile → Trigger
非 Trigger 实心 Collider → Decoration
其他 → Normal
```

### 10.3 自动归层

左键选择刷子后，不要求策划手动切换普通地砖/装饰写入层：

| 刷子类别 | 写入 |
|---|---|
| `Normal` | `tiles[]` |
| `Trigger` | `tiles[]` |
| `Decoration` | `overlayTiles[]` 或 `decorationPlacements[]` |

`editLayer` 主要决定橡皮擦的当前层；涂刷逻辑根据 prefab 语义自动归层。

### 10.4 footprint 自动读取

`RefreshPalette` 之后调用 `RefreshDecorationFootprints`：

```text
扫描全部 ChunkDef
  → 读取 decorationTiles
  → prefab → footprintSize
  → 同一 prefab 多处配置取各轴最大值
  → 刷子标签/悬停预览/自动放置共用
```

刷子标签示例：

```text
Gate1 [Decoration, 有碰撞] 3×2格
```

这样 footprint 的权威配置仍在 `ChunkDef.decorationTiles`，编辑器不再维护第二份尺寸表。

### 10.5 自动多格放置

点击已配置 footprint 的装饰刷子：

1. 读取自动 footprint；
2. 检查 `x >= 0、y >= 0`；
3. 检查 `x + width <= size、y + height <= size`；
4. 检查每格没有 `overlayTiles` 或其他 placement；
5. 添加一个 `DecorationPlacement`；
6. `Undo.RecordObject`；
7. `EditorUtility.SetDirty`。

### 10.6 手动多格模式

没有在 `ChunkDef` 中配置 footprint 的装饰，可以打开“多格放置”，手工输入：

```text
width × height
```

手动模式和自动模式共用 `PaintMultiCellDecoration`，最终数据结构一致。

### 10.7 Chunk 边沿规则

当前固定布局编辑器允许多格装饰触碰边沿：

```text
合法：anchor >= 0
合法：anchor + footprint <= size
非法：footprint 越出布局网格
```

这解决了“装饰物必须完整位于内部区域”的过度限制。编辑器不支持跨 Chunk placement；跨 Chunk 需要新的 ownership、加载 Pin、坐标拆分和回收协议。

### 10.8 预览

编辑器支持：

- AssetPreview 缩略图；
- 预览未就绪时下一帧重绘；
- 默认地块兜底遮罩；
- 叠加层右上角缩略图与描边；
- 多格 placement 整体边框和尺寸标签；
- 悬停时黄色半透明 footprint 预览；
- 顶行 y 翻转显示，北方在上；
- 关闭缩略图时使用颜色块/缩写。

### 10.9 擦除与 Undo

左键橡皮擦：

```text
底层模式     → 清除 tiles[]
叠加层模式   → 先查找包含当前格的 placement，命中则整组删除
              → 否则清除 overlayTiles[]
```

右键：

```text
命中 placement → 删除整个 placement
否则           → 清空当前格底层和叠加层
```

所有资产修改使用：

```csharp
Undo.RecordObject(target, "操作名称");
EditorUtility.SetDirty(target);
```

因此多格装饰不会出现“视觉删除一个格，但资产仍保留整组”的残留问题。

### 10.10 资产自动刷新

`ChunkLayoutEditorPostprocessor` 监听：

```text
Assets/Prefabs/Room/RoomObjects/TileAsset/**/*.prefab
Assets/Settings/MapStreaming/**/*.asset
```

导入、删除或移动相关资产后：

```text
Resources.FindObjectsOfTypeAll<ChunkLayoutEditorWindow>
  → RefreshPalette()
  → RefreshDecorationFootprints()
  → Repaint()
```

这保证新增装饰 prefab 或修改 `ChunkDef` footprint 后，已经打开的编辑器可以重新读取刷子和尺寸映射。

---

## 11. WorldPlan 与模板的宏观选择

### 11.1 `WorldPlan`

`regionTable` 非空时，`MapStreamingSystem` 懒构造 `WorldPlan`：

- 使用世界种子和 Chunk 坐标计算连续主题噪声；
- 按 `RegionDef.themeCenter` 选择区域；
- 从区域 `chunkPool` 加权选 `ChunkDef`；
- 已生成邻居命中 `preferredNeighbors` 时增加权重。

当 `regionTable` 为空时，默认所有 Chunk 使用 `defaultChunkDef`。

### 11.2 已实现边界

当前 `WorldPlan` 是区域/ChunkDef 解析器，不是完整关卡图：

- 没有把主路径、分支、守卫、Boss 目标编排成可验证图；
- 没有完整的宏观可达性证明；
- 默认测试场景未必启用区域表；
- 固定锚点仍是关键房间的更强覆盖机制。

---

## 12. 状态、Pin 与动态实体

### 12.1 PinRegistry

Pin 来源采用集合而不是单一 bool：

```text
Player       玩家所在 Chunk
Possession   当前附身身体所在 Chunk
Boss         预留扩展来源
```

`RefreshPins` 每次 Tick 刷新玩家和当前身体所在 Chunk。离开 D 时，只要仍有来源 Pin，就跳过 UnloadFull；解除 Pin 且仍在 D 外时，补发 Serialize/UnloadFull。

### 12.2 ChunkStateStore

`ChunkStateStore` 保存单局内存中的动态快照，主要由 `MonsterSpawner` 在回收 Chunk 动态实体时写入：

```text
怪物快照
尸体快照
```

TileData 本身由 seed 重新生成，不需要为纯静态地形重复存盘。

当前完整的奖励、尸体搜刮、所有动态事件和任意时刻磁盘存档恢复仍不是本功能的完整闭环。

### 12.3 MonsterSpawner 协作

`MapStreamingSystem` 只发状态事件，`MonsterSpawner` 根据状态处理：

```text
进入 Dormant → 生成/恢复动态实体
进入 Active  → 激活 AI
离开 A       → AI 休眠
离开 B       → 回收动态实体并写快照
```

这种订阅方式避免 `MapStreamingSystem` 直接依赖怪物具体实现。

---

## 13. 配置指南

### 13.1 新增普通地砖

1. 创建 prefab；
2. 按玩法决定是否带实心 Collider；
3. 加入 `ChunkDef.normalTiles`；
4. 确认 `TileSemantics.ResolveKind` 为 Normal；
5. 刷新模板编辑器后即可作为底层刷子。

### 13.2 新增 Trigger

1. prefab 挂 `TerrainEffectTile`；
2. 加入 `ChunkDef.triggerTiles`；
3. 配置 `triggerPatterns` 的形状与权重；
4. 通过 `minPatternsPerChunk/maxPatternsPerChunk` 控制总图案槽位。

### 13.3 新增多格装饰

```text
1. 创建带正确视觉和 Collider 的 prefab
2. 加入 ChunkDef.decorationTiles
3. 配置 footprintSize = width × height
4. 配置 maxPerChunk
5. 配置 decorationPatterns 权重
6. 打开 Chunk Layout Editor
7. 刷新 Tile 列表
8. 悬停确认 footprint，左键放置
```

### 13.4 创建模板

```text
1. Create → Kepler → Map → Fixed Chunk Layout
2. 选择资产
3. 设置 size 与 defaultGround
4. 使用地砖/Trigger 刷子绘制 tiles[]
5. 使用装饰刷子绘制 overlay/placement
6. 校验边沿至少有两个可走方向
7. 在 ChunkDef.templateEntries 或 ChunkAnchor 中引用
```

### 13.5 模板出现频率

```text
def.templateWeight       每个普通 Chunk 进入模板路径的概率
entry.weight             模板候选池相对权重
entry.mustGenerate       保证至少出现一次
entry.maxCount           单局该模板的最大分配数
fixedAnchors             直接覆盖随机/模板分配
```

### 13.6 无限/有限地图

```text
无限：boundaryChunkExtent = 0
有限：boundaryChunkExtent = 正整数
```

有限边界中心可以使用 `boundaryCenterOverride`；为空时使用初始化时玩家位置。

---

## 14. 技术验收矩阵

| 编号 | 验收目标 | 操作 | 预期 |
|---|---|---|---|
| MAP-01 | Chunk 确定性 | 固定 RunSession.WorldSeed，重复进入同一坐标 | Tile、图案、装饰、模板分配一致 |
| MAP-02 | 顺序无关基础生成 | 先访问东侧再访问西侧，再反向测试 | 基础 seed 内容不因访问顺序改变 |
| MAP-03 | 模板首定缓存 | 让 Chunk 离开 D 后重新进入 | `assigned[coord]` 复用同一 layout |
| MAP-04 | 模板上限 | 配置 `maxCount` 并扩大探索范围 | 达到上限后不再抽取该模板 |
| MAP-05 | mustGenerate | 配置未出现的 `mustGenerate` 模板 | 候选池优先完成必生成模板 |
| MAP-06 | 无限边界 | `boundaryChunkExtent=0` 向任意方向移动 | Chunk 持续进入 C/B/A，不被矩形拒绝 |
| MAP-07 | 有限边界 | 设置正数并走到边界外 | 边界外不进入任何范围集合 |
| MAP-08 | 任务预算 | 高速跨多个 Chunk | 单帧任务受数量和 8ms 时间片限制，队列顺延 |
| MAP-09 | 任务过期 | 快速掉头 | 旧方向任务被过期检查丢弃 |
| MAP-10 | Pin | 附身身体移动到 D 外 | 身体所在 Chunk 不被完整卸载 |
| MAP-11 | 固定锚点 | 配置 layout/fixedSeed | 指定坐标不受 worldSeed/allocator 影响 |
| MAP-12 | 模板双层 | 底层+叠加层同时配置 | 运行时两层都存在，底层不被装饰替换 |
| MAP-13 | 多格放置 | 放置 2×2、1×3、3×1 装饰 | 一个 placement、一组 owner id、一个视觉实例 |
| MAP-14 | 边沿放置 | 从 `(0,0)` 或边界起点放置 | 只要不越出网格即可成功 |
| MAP-15 | 重叠保护 | 在已有 placement 上放置 | 编辑器拒绝并保留原数据 |
| MAP-16 | 整组擦除 | 橡皮擦点击 footprint 内任意格 | 整个 placement 删除，可 Undo |
| MAP-17 | 资产刷新 | 修改 TileAsset 或 ChunkDef | 编辑器刷新刷子和 footprint 映射 |
| MAP-18 | 视觉重建 | 离开 B 再返回 B | 视觉重新创建且不消耗新随机数 |
| MAP-19 | 尺寸不一致 | layout.size 与 chunkSize 不同 | 告警，越界格按空处理，不静默重采样 |
| MAP-20 | 连通性告警 | 模板边沿大面积阻挡 | OpenEdges<2 告警，保留作者布局 |

---

## 15. 已实现边界与后续设计

### 15.1 已实现

- A/B/C/D Chunk 流送集合和状态机；
- JobKind 优先级、去重、距离排序、过期检查；
- 每帧数量预算与 8ms 时间片；
- 无限/有限矩形边界；
- 世界种子 + Chunk 坐标 + salt 的确定性随机；
- 随机地形、Trigger、装饰、道路、花纹和填充；
- 模板权重、mustGenerate、maxCount 和坐标缓存；
- 固定锚点；
- FixedChunkLayout 双层数据与旧格式兼容；
- DecorationPlacement 矩形多格占位；
- ChunkVisualizer 动态视觉实例化；
- ChunkLayoutEditorWindow 多格预览、边沿放置、整组擦除、Undo、资产自动刷新；
- Pin 和动态怪物快照接入。

### 15.2 尚未完整实现

1. **共享边界签名**：相邻 Chunk 尚不能自动逐格匹配开口；
2. **跨 Chunk 多格装饰**：缺少跨 Chunk owner、Pin、拆分渲染和回收协议；
3. **真实相机视锥 A 集合**：当前使用半径近似 + 玩家/邻接保底；
4. **Chunk 地形对象池**：当前逐对象 Instantiate/Destroy；
5. **NavMesh 增量烘焙**：当前没有由 Chunk 状态驱动的局部 NavMesh 更新；
6. **完整 WorldPlan 图**：当前是区域/ChunkDef 解析，不是完整主路径/分支/Boss 图；
7. **完整动态状态落盘**：ChunkState 有内存快照接口，奖励、尸体搜刮、事件和所有动态实体的完整跨存档恢复需要单独验收；
8. **自动布局尺寸适配**：layout.size 与系统 chunkSize 不一致时仅告警，不做重采样；
9. **非矩形 footprint**：当前只支持矩形 `Vector2Int`，不支持 L 形 mask、旋转 mask。

### 15.3 推荐扩展顺序

```text
1. 共享边界签名
2. 多格 footprint mask / 旋转
3. Chunk 地形对象池或合批
4. 完整 WorldPlan 可达图
5. 完整 ChunkState / 动态实体存档恢复
6. Profile 证明必要后接入 NavMesh 增量烘焙
```

---

## 16. 源码与资产索引

### 运行时地图

```text
Assets/Scripts/Levels/MapStreaming/MapStreamingSystem.cs
Assets/Scripts/Levels/MapStreaming/ChunkTileGenerator.cs
Assets/Scripts/Levels/MapStreaming/ChunkRuntime.cs
Assets/Scripts/Levels/MapStreaming/ChunkVisualizer.cs
Assets/Scripts/Levels/MapStreaming/ChunkDef.cs
Assets/Scripts/Levels/MapStreaming/FixedChunkLayout.cs
Assets/Scripts/Levels/MapStreaming/DecorationPlacement.cs
Assets/Scripts/Levels/MapStreaming/TileData.cs
Assets/Scripts/Levels/MapStreaming/TileEnums.cs
Assets/Scripts/Levels/MapStreaming/ChunkCoord.cs
Assets/Scripts/Levels/MapStreaming/ChunkAnchor.cs
Assets/Scripts/Levels/MapStreaming/WorldPlan.cs
Assets/Scripts/Levels/MapStreaming/RegionDef.cs
Assets/Scripts/Levels/MapStreaming/ChunkStateStore.cs
Assets/Scripts/Levels/MapStreaming/PinRegistry.cs
```

### 编辑器

```text
Assets/Scripts/Editor/ChunkLayoutEditorWindow.cs
```

### 配置资产

```text
Assets/Settings/MapStreaming/ChunkDef.asset
Assets/Settings/MapStreaming/Templates/Template1.asset
Assets/Settings/MapStreaming/Templates/Template2.asset
Assets/Settings/MapStreaming/Templates/Template3.asset
Assets/Scenes/EnemyAiTest.unity
Assets/Scenes/CombatTest.unity
```

### 相关文档

```text
MapStreaming_Design.md                      （设计阶段，本文为其实现落地；位于本地 .docs 文档包，未随仓库分发）
统一文本与字体管理方案.md                   （同目录：工程实现文档）
性能优化审计与Agent执行方案.md              （同目录：工程实现文档）
```

---

## 17. 总结

当前地图系统形成了如下技术闭环：

```text
RunSession.WorldSeed / Inspector 配置
  → Chunk 范围 Diff
  → 任务预算调度
  → ChunkRuntime 状态机
  → ChunkSeed + 模板分配
  → ChunkTileGenerator 纯数据生成
  → ChunkVisualizer 动态实例化
  → MonsterSpawner / ChunkState 动态实体生命周期
  → 离开范围时降级、快照、回收
```

模板编辑器则把生产工具链补齐：

```text
prefab 语义
  → 刷子自动分类
  → ChunkDef footprint 自动读取
  → 双层网格绘制
  → 多格 placement 预览/放置
  → 边沿合法性与重叠校验
  → 整组擦除 + Undo
  → AssetPostprocessor 自动刷新
  → 运行时 GenerateFromLayout 动态还原
```

该设计的技术价值在于同时解决了三类问题：

1. **规模问题**：用 A/B/C/D、任务队列、Pin 和预算把无限探索拆成可控生命周期；
2. **确定性问题**：用世界种子、Chunk 坐标、固定随机流和模板首定缓存保证重建一致；
3. **生产问题**：用 FixedChunkLayout、双层数据和编辑器工具让策划可以精确制作多格内容，并确保编辑结果与运行时实例化规则一致。
