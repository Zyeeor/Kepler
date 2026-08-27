# 《Kepler》技术设计审查报告

> 审查日期：2026-08-26 | 审查人：Eli（程序） | 项目阶段：原型开发中期
> 基准版本：`6444bab`（feat(build): BuildView 默认参数对齐与附身实时刷新，已同步 origin/main）
> 说明：本次为**重写**——取代 2026-08-10 的旧版审查（旧版大量内容已因结构重构过时，如旧房间系统、Enemy 字符串状态机、PlayerHealth 附身耦合等）。

---

## 一、Git 状态

| 项目 | 状态 |
|------|------|
| 分支 | `main` |
| 与 `origin/main` 同步 | ✅ up-to-date（6444bab 已推送） |
| 未跟踪 | `.docs/`（约定不入库，无需关注） |
| 当日已提交 | 图鉴系统、怪物 AI 范围解锁、调试修复、卡面图标、构筑界面、地图流式、编译修复（共约 8 个 commit） |

> 结论：当前为最新版本，工作区干净，可安全审查。

---

## 二、架构全景（当前实现）

```
Assets/Scripts/
├── GameManager.cs                  — 核心协调单例（DDOL；状态/引用/计数 + 调试组件挂载点）
├── Core/Control/                   — AIController（怪物行为树）、PlayerController
├── Combat/                         — MonsterActor、Abilities/Monster/*（EnemyAbility_*）、Possession/PossessionManager
├── AI/                             — MonsterAIConfig（SO 配置库 + rangeUnlocks）、AiConfigIdAttribute
├── Editor/                         — AiConfigIdDrawer
├── Levels/
│   ├── Arena/ArenaConfig           — SO 竞技场配置
│   ├── Waves/WaveManager           — 波次调度（+ WaveDefinitions/WaveTimerUI）
│   ├── Spawning/                   — RunSpawnDirector（曲线乘数）、MonsterPool、SpawnRequest、
│   │                                 MonsterSpawnDifficulty、ENGPOSS001SceneInstaller（场景安装入口）
│   ├── MapStreaming/               — 地图流式（Chunk 双层 Tile：Base+Structure 层、地形效果、MonsterSpawner）
│   └── Rooms/                      — 【仅剩卡牌】CardData/CardLibrary/CardManager/CoreChoiceUI/CoreChoiceCard
├── UI/
│   ├── Core/UIManager              — UI 总控（EnsureBuildView = FindObjectOfType）
│   ├── Gameplay/BuildView          — 构筑显示（三态，附身实时刷新，场景挂载）
│   ├── MainMenu/ + CardArchive/ + HallOfFame/ + Narrative/ + HUD/
├── Systems/
│   ├── Meta/MetaProfileStore       — 局外 meta 存档（possess_meta_save.json）
│   ├── Meta/CardArchiveStore       — 图鉴数据 + CardArchiveTracker（采集器，挂 GameManager）
│   ├── Elite/HallOfFameStore       — 荣誉殿堂（版本守卫）
│   └── Save/SaveCoordinator        — Run 存档（不触碰 meta）
├── Localization/                   — FontRegistry / FontApplier
└── Debug/                          — CardFaceBrowser 等（多挂 GameManager）
```

**关键结构变化（相对 08-10）**：
- 旧“房间”系统（`RoomFlowController/RoomManager/RoomGenerator/...`）已**整体移除**；`Levels/Rooms/` 现仅保留**卡牌系统**。
- 关卡改为 `Waves` 波次 + `Spawning` 刷怪 + `MapStreaming` 地图流式 + `Arena` 竞技场。
- 怪物 AI 由“字符串状态机”改为**行为树（AIController）+ SO 配置（MonsterAIConfig）**。
- 附身逻辑已从 PlayerHealth 拆出为独立 **PossessionManager**。
- 新增局外系统（图鉴/荣誉殿堂/Meta 存档）与地图流式。

---

## 三、逐系统分析

### 3.1 GameManager — 核心协调器
**当前职责**：游戏状态、玩家/敌人引用、敌人计数、玩家死亡/重生、调试组件统一挂载（`EnsureOnGameManager`）。
**评价**：职责仍偏多（既是状态机又是数据仓库，现在还兼调试挂载点）。
**建议**：拆分为 `GameStateMachine`（状态）+ `GameContext`（共享数据）+ `PlayerLifecycle`；调试挂载逻辑可下沉到独立的 `DebugBootstrap`。

### 3.2 怪物 AI（`AI/` + `Core/Control/AIController.cs`）— 本次重点改善
- `MonsterAIConfig`（SO）：7 只 `_new` 怪配置（范围/节拍/权重/走位），`OnValidate` 校验。
- `AIController`：`BTSelector/BTSequence/BTCondition_*` 组装行为树，替代旧字符串状态机。
- **新增**：`rangeUnlocks` 动态范围（解锁后攻击/技能范围取 max，实时读配置无快照）+ `AiConfigIdDrawer` 编辑器下拉。
**评价**：数据驱动良好，解耦清晰，是本轮架构上的正向演进。

### 3.3 波次与刷怪（`Waves/` + `Spawning/`）
- `WaveManager`：波次调度（读档 resume、`autoShowChoiceUI`、潮汐/精英/Boss）。
- `RunSpawnDirector`（2026-08 重构）：连续刷怪改为 **AnimationCurve 难度乘数**（删“周期压力/击杀回声窗口”）。
**评价**：曲线化更数据驱动；重构曾遗漏调用方（WaveManager），已通过编译修复对齐。建议重构公共 API 时全量搜索调用方。

### 3.4 地图流式（`MapStreaming/`）— 新模块
- 双层 Tile 模型（Base+Structure）、Chunk 运行时、地形效果。
**评价**：模块边界清晰（System/Runtime/Generator/Layout 分层），与设计 §2.2 对齐。

### 3.5 附身系统（`Combat/Possession/`）
- `PossessionManager` 独立类，状态机 + 事件（`OnPossessionStarted/OnPossessionEnded/OnBodyDiedWhilePossessing`）。
**评价**：已从 PlayerHealth 解耦（解决旧版耦合问题）；`BuildView`/`AbilityCooldownUI` 用轮询或事件消费，模式统一。

### 3.6 卡牌系统（`Levels/Rooms/`）— 本次新增事件
- `CardManager`：卡池 + 获得卡 + 候选（currentPicks，活跃运行时数据源）。
- **新增事件**：`OnCardOffered/OnCardRerolled/OnEffectUnlocked`（图鉴采集 + AI 解锁钩子挂点）。
- `CardLibrary`（SO）：49 张 active 卡；卡面 image 已替换为技能图标。
**评价**：事件通信有改善（解耦图鉴/AI），但 currentPicks 与 `RunSession.ChoicePicks` 双份数据靠手动 Sync，可观察。

### 3.7 局外系统（`Systems/Meta/` + `Systems/Elite/`）— 新模块
- `MetaProfileStore`/`CardArchiveStore`/`CardArchiveTracker`/`HallOfFameStore`/`SaveCoordinator`。
**评价**：数据层清晰（Run/meta 分离、版本守卫）。**已知缺口**：图鉴 `CardArchivePanel` 卡片 tile 只渲染文字，**卡面 image 未接入**（待补）。

### 3.8 UI / Gameplay 显示层（BuildView · UIManager · CardArchivePanel）
- `UIManager`：发现并持有 BuildView（不创建）。
- `BuildView`：自包含三态；场景挂载 + 默认值对齐 + 附身实时刷新（2026-08）。
- `CardArchivePanel`：纯代码 UGUI；本轮已补齐 4K 缩放（CanvasScaler）、中文字体、GraphicRaycaster、实例自愈。
**评价**：本轮统一了“动态 UI 的字体/缩放/单例健壮性”模式，可作为后续新面板模板。

---

## 四、架构健康度评估（对比 08-10）

| 维度 | 08-10 | 当前 | 说明 |
|------|-------|------|------|
| 模块划分 | 🟡 | 🟢 | 房间→Waves/Spawning/MapStreaming 拆分，AI/附身解耦；仅 GameManager 仍偏重 |
| 数据驱动 | 🟢 | 🟢 | SO 持续（MonsterAIConfig/WaveDefinitions/CardLibrary/ArenaConfig） |
| 事件通信 | 🔴 | 🟡 | 新增 CardManager 事件 + PossessionManager 事件；仍有较多直接调用，无统一总线 |
| 性能意识 | 🔴 | 🟡 | AI 有 DecisionTick 节流；未见显式对象池（刷怪/飞行物 Instantiate/Destroy 频繁） |
| 状态管理 | 🟡 | 🟢 | Enemy 字符串状态机→行为树；波次/附身为 enum/事件 |
| 可测试性 | 🔴 | 🔴 | 单例+紧耦合仍在，单元测试缺失 |
| 文档覆盖 | 🔴 | 🟢 | 已有 `.vibe/doc/`（本文档、整体流程与架构、Canonical、Modules） |
| 代码规范 | 🟡 | 🟢 | 编辑器校验（OnValidate/AiConfigIdDrawer）、编译修复、调试面板挂点统一 |

---

## 五、优先级整改建议

### 🔴 P0 — 立即处理
| 序号 | 问题 | 影响 |
|------|------|------|
| 1 | **图鉴卡面图未接入**（`CardArchivePanel` tile 仅文字） | 图鉴可用性/辨识度不足；CardLibrary 资产非 Resources，主菜单取图需方案（Resources 化 / entry 持久化 image guid / 场景引用） |
| 2 | **`MapDebugHUD`/`CardProgressPanel` 仍为 RuntimeInitializeOnLoadMethod 自建** | 无域重载下静态 instance 悬空隐患（CardFaceBrowser/CardArchiveTracker 已修），同款需改挂 GameManager |

### 🟡 P1 — 本阶段内处理
| 序号 | 问题 | 方案 |
|------|------|------|
| 3 | **GameManager 职责过重** | 拆 GameStateMachine + GameContext + PlayerLifecycle；调试挂载下沉 DebugBootstrap |
| 4 | **卡池数量差异**：Canonical 65 张 vs 实现 49 张 | 核对是设计滞后还是实现未补齐，对齐 CardLibrary |
| 5 | **对象池缺失**（刷怪/飞行物/尸体 Instantiate/Destroy 频繁） | 引入简单对象池降低 GC |
| 6 | **纯代码 UI 的“字体/缩放/单例”模式需模板化** | 抽公共基类（CanvasScaler 1080p、AddText 应用字体、EnsureInstance 自愈） |

### 🟢 P2 — 后续阶段
| 序号 | 问题 | 方案 |
|------|------|------|
| 7 | **统一事件总线** | EventManager 收敛跨系统直接调用（Card/Possession 已有事件基础） |
| 8 | **单元测试** | Test Runner + 接口化 |
| 9 | **currentPicks 与 ChoicePicks 双份 Sync** | 收敛为单一数据源 + 序列化快照 |

---

## 六、与 Canonical 需求对照

| Canonical 基线 | 实现状态 | 备注 |
|---|---|---|
| 七宗罪 Roster（7 只，起始傲慢） | ✅ 7 只 `_new` 怪 | 一致 |
| 卡池 65 张（2 张 TypeGrowth） | ⚠️ 实现 49 张 active | 见 P1-4 差异核对 |
| 卡面图标稳定 | ✅ 49 张 image → 技能图标 | 已接入 |
| 怪物 AI 三槽/范围 | ✅ SO 配置 + 行为树 + **rangeUnlocks**（实现新增） | 实现超前 |
| Wave/Encounter 节奏 | ✅ WaveManager + RunSpawnDirector（曲线化） | 已落地 |
| Infinite Terrain | ✅ MapStreaming（Chunk 双层） | 已落地 |
| 局外界面（图鉴+荣誉殿堂，Decision 35-38） | ✅（图鉴卡面待补） | 见 P0-1 |
| 高频换身 Body/Soul/Corpse | ✅ PossessionManager | 一致 |
| Tutorial / Narrative Baseline | 🔍 未在本快照深入 | 见对应 Baseline 文档 |

---

## 七、总体结论

项目已完成一次较大的结构演进：旧房间系统移除、关卡重构为 Waves/Spawning/MapStreaming、怪物 AI 配置化（行为树 + SO + 动态范围）、局外系统（图鉴/荣誉殿堂）落地。整体向**数据驱动 + 模块解耦**方向前进，文档体系也已建立。

当前主要风险/欠账：
1. **GameManager 单体膨胀**（含调试挂载点）；
2. **无域重载下的静态实例自建模式**残留（MapDebugHUD/CardProgressPanel）；
3. **图鉴卡面图**这一可用性缺口；
4. **卡池数量与 Canonical 的差异**需核对；
5. 对象池、事件总线、单元测试仍缺。

**最优先行动项**：
1. 补齐图鉴卡面图（P0-1）；
2. 统一改挂剩余自建调试面板（P0-2）；
3. 核对卡池数量与 Canonical 基线（P1-4）。
