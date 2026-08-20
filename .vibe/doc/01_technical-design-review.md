# 《Kepler》技术设计审查报告

> 审查日期：2026-08-10 | 审查人：Eli（程序）| 项目阶段：Stage1 原型开发
> 基准版本：`da2d9dc` (8.3 — 贪婪本体技能+关卡生成bug+特效bug解决)

---

## 一、Git 状态

| 项目 | 状态 |
|------|------|
| 分支 | `main` |
| 与 `origin/main` 同步 | ✅ up-to-date |
| 未暂存修改 | `Custom Render Texture.asset`、`probuilder Settings.json`（2 个资产文件，非脚本） |
| 未跟踪文件 | `.codebuddy/`（IDE 目录，无需关注） |

> **结论：当前是最新版本，且无关键代码变更未提交。可以安全审查。**

---

## 二、架构全景

```
Assets/Scripts/
├── GameManager.cs          — 中心协调器（单例，游戏状态/玩家/敌人引用）
├── PlayerHealth.cs         — 生命值 + 附身/灵魂状态机（⚠️ 职责过重）
├── PlayerCombat.cs         — 玩家攻击调用
├── PlayerInputController.cs— 输入 + 动画状态切换
├── PlayerPassiveManager.cs — 被动效果管理器
├── Enemy.cs                — 敌人 AI 状态机（Patrol/Chase/Attack/Stun/Death）
├── EnemyConfig.cs          — 敌人 ScriptableObject 配置
├── EnemySpawner.cs         — 敌人实例化
├── EnemyAbility.cs         — 敌人能力基类
├── PlayerAbility.cs        — 玩家能力基类
├── PossessionHUD.cs        — 附身 HUD 显示
├── Camera/CameraDirector.cs— Cinemachine 相机（运行时自建 Rig）
├── Room/
│   ├── RoomManager.cs      — 房间布局/池管理/Boss 房间
│   ├── RoomGenerator.cs    — BSP/网格房间生成
│   ├── RoomTemplate.cs     — 房间配置 ScriptableObject
│   ├── RoomInstance.cs     — 房间 Prefab 运行时根
│   ├── RoomFlowController.cs— 单房间流程（Loading→Combat→Cleared→Exit）
│   ├── WaveManager.cs      — 波次管理
│   ├── WaveConfig.cs       — 单波配置 ScriptableObject
│   ├── RoomCore.cs         — 核心拾取物
│   ├── RoomLoader.cs       — 房间加载器
│   ├── RoomExit.cs         — 出口交互
│   ├── CardData.cs         — 卡牌数据定义
│   ├── CardManager.cs      — 卡牌选择管理
│   ├── CoreChoiceUI.cs     — 核心选择 UI
│   └── ChoiceCard.cs/Manager.cs — 选择卡牌
```

---

## 三、逐系统分析

### 3.1 GameManager — 中心协调器

**当前职责**（共 8 项，严重过载）：
- 游戏状态机（MainMenu/Combat/Pause/GameOver/Victory）
- 玩家引用管理
- 敌人引用与计数（`EnemyList` + `EnemyAliveCount`）
- 敌人死亡/移除处理
- 玩家死亡/重生流程
- 房间计数追踪（`RoomCount`）
- 玩家瞬移
- 全场景 `Find` 查询

**问题**：
1. `Update()` 中每帧 `FindGameObjectsWithTag("Enemy")` — 这是性能热点，应该用事件驱动更新计数
2. 状态切换散落在各处（`PlayerHealth.Die()` 直接调 `GameManager.GameOver()`）
3. 职责模糊：既是状态机又是数据仓库又是工具函数集

**建议**：拆分为 `GameStateMachine`（纯状态）+ `GameContext`（共享数据）+ `PlayerManager`（玩家生命周期）

---

### 3.2 PlayerHealth — 附身系统耦合

**现状**：PlayerHealth 同时管理：
- HP/受伤/死亡
- 附身目标选择（`currentPossessionTarget`）
- 灵魂出窍/入体动画
- 敌人身体状态的 isDead 标记
- 灵魂状态（`isGhost`）下的移动和碰撞

**问题**：
1. **附身逻辑和生命值管理是两个正交概念**，不应在同一类中
2. `TryPossess()` 直接操作 `Enemy` 组件状态（`enemy.isDead`、`enemy.state`），跨模块侵入
3. 灵魂状态下的行为切换（禁用碰撞、改变移动速度）散落在多处

**建议**：
- 创建独立的 `PossessionController` 类，专管附身逻辑
- PlayerHealth 仅管理 HP，通过事件通知 PossessionController
- Enemy 的死亡/可附身状态应有清晰的接口

---

### 3.3 Enemy — AI 状态机

**当前设计**：
- 字符串驱动状态切换（`"patrol"`、`"chase"`、`"attack"`、`"stun"`、`"death"`）
- NavMeshAgent 移动
- Config-driven 属性（`EnemyConfig` ScriptableObject）

**问题**：
1. **字符串状态名不安全** — 拼写错误在编译期无法发现
2. `Update()` 中每帧调用 `FindObjectOfType<GameManager>()` 和 `FindObjectOfType<PlayerHealth>()`
3. `Die()` 方法中 `Destroy(gameObject)` 直接销毁 — 没有对象池

**建议**：
- 将状态改为 `enum EnemyState`
- 通过 `GameManager.Instance`（现有模式）或依赖注入获取引用，避免每帧 Find
- 引入简单对象池减少 GC 压力

---

### 3.4 房间系统 — 设计亮点

房间模块是目前架构最清晰的部分：

| 组件 | 职责 | 评价 |
|------|------|------|
| `RoomManager` | 布局、池管理、Boss | ✅ 清晰 |
| `RoomTemplate` | SO 配置（位置/旋转/敌人池/波次/核心/奖励） | ✅ 数据驱动 |
| `RoomInstance` | Prefab 运行时根（生成点/出口/相机边界） | ✅ 简洁 |
| `RoomFlowController` | 单房间状态机 | ✅ 职责单一 |
| `WaveManager` | 波次调度 | ✅ 事件驱动 |
| `RoomGenerator` | BSP/网格生成 | ✅ 算法隔离 |

**小问题**：
- `RoomFlowController.SpawnCore()` 中的坐标计算逻辑耦合了 `RoomTemplate.transform` 细节，建议封装到 `RoomTemplate.GetCoreWorldPose()`

---

### 3.5 CameraDirector — 技术亮点

**优点**：
- 运行时自建 Cinemachine 管线（Brain + VirtualCamera + Impulse），零手动布线
- Hit-stop（顿帧）实现正确保留了恢复逻辑
- 支持透视/正交双模式

**隐患**：
- `Time.timeScale` 控制顿帧 — 如果有暂停菜单或子弹时间功能，会产生冲突。建议用 `Time.unscaledDeltaTime` 的替代方案或在 HitStop 开始前检查当前 timeScale 来源

---

### 3.6 BurnEffect.cs — ⚠️ 缺失文件

`PlayerCombat.cs` 中引用了 `BurnEffect` 组件处理燃烧状态，但在 `Assets/Scripts/` 下**找不到 `BurnEffect.cs`**。

**影响**：如果 Prefab/场景中依赖这个组件序列化数据，运行时可能静默失败或 null 引用。

**建议**：立即确认是否存在或需创建。

---

### 3.7 卡牌/选择系统

`CardData` / `CardManager` / `CoreChoiceUI` 结构合理，卡牌数据通过 SO 配置。但：
- `CardManager` 与 `CoreChoiceUI` 的交互方式需要进一步确认是否有循环依赖
- 卡牌效果的运行时解析逻辑未在审查文件中看到完整流程

---

## 四、架构健康度评估

| 维度 | 评分 | 说明 |
|------|------|------|
| 模块划分 | 🟡 中等 | 房间系统好，核心 GameManager 过载 |
| 数据驱动 | 🟢 良好 | ScriptableObject 驱动配置，方向正确 |
| 事件通信 | 🔴 薄弱 | 大量直接调用，缺少事件总线 |
| 性能意识 | 🔴 薄弱 | Update() 中的 Find 查询、无对象池、无 LOD/剔除 |
| 状态管理 | 🟡 中等 | 房间流程用 enum，但 Enemy 用 string |
| 可测试性 | 🔴 薄弱 | 单例 + 紧耦合，几乎无法单元测试 |
| 文档覆盖 | 🔴 缺失 | `.vibe/doc/` 目录不存在，无任何技术文档 |
| 代码规范 | 🟡 中等 | 命名基本一致，但缺少常量和 enum 管理 |

---

## 五、优先级整改建议

### 🔴 P0 — 立即处理

| 序号 | 问题 | 影响 |
|------|------|------|
| 1 | **缺失 `BurnEffect.cs`** | 运行时潜在崩溃 |
| 2 | **更新中的 Find 查询** (Enemy.cs, PlayerHealth.cs) | 每帧 GC 分配 + O(n²) 搜索 |
| 3 | **无对象池** | Instantiate/Destroy 频繁触发 GC |

### 🟡 P1 — 本阶段内处理

| 序号 | 问题 | 方案 |
|------|------|------|
| 4 | **PlayerHealth 职责过重** | 拆分 PossessionController |
| 5 | **Enemy 状态改用 enum** | 编译期安全 + 可读性 |
| 6 | **创建 `.vibe/doc/` 技术文档** | 架构图、模块接口说明 |
| 7 | **引入事件总线** | `EventManager` 解耦系统间调用 |

### 🟢 P2 — 后续阶段

| 序号 | 问题 | 方案 |
|------|------|------|
| 8 | **GameManager 拆分** | GameStateMachine + GameContext + PlayerManager |
| 9 | **Time.timeScale 顿帧** | 替代方案（unscaled 时间线） |
| 10 | **单元测试框架** | Test Runner + 接口化 |

---

## 六、与 Stage1 技术规范的对照

根据 `Technical_Numerical_Spec.md` 的 P0 要求对照：

| Stage1 P0 需求 | 当前状态 | 备注 |
|---------------|---------|------|
| 附身/换身系统 | ✅ 已实现 | 耦合在 PlayerHealth 中 |
| 三槽输入框架 | ✅ 已实现 | PlayerInputController + PlayerCombat |
| 3-5 只怪物 | ✅ 已实现 | EnemyConfig 数据驱动 |
| 卡牌+反向 BD | 🟡 基础实现 | CardManager+CardData 存在，逻辑待确认 |
| 普通 Wave 清场 | ✅ 已实现 | WaveManager + RoomFlow 完整 |
| 终局导演 | ❓ 待确认 | RoomManager 有 Boss 相关，未深入 |
| 结算+快速重开 | 🟡 部分 | GameManager 有状态切换，流程待完善 |
| 新档教学 | ❓ 待确认 | 未在代码中看到教学系统 |
| 评审防阻塞 | ❓ 待确认 | 无相关逻辑 |

---

## 七、总体结论

项目处于 **原型开发中期**，核心玩法循环（附身→战斗→清场→选择）已可运行。架构上最大风险是 **GameManager 的单体膨胀** 和 **跨模块紧耦合**，这会导致后续功能添加成本指数增长。

**最优先行动项**：
1. 补全缺失的 `BurnEffect.cs`
2. 建立 `.vibe/doc/` 下的技术文档
3. 引入 EventManager 事件总线下解耦 PlayerHealth↔GameManager↔Enemy 的三角依赖
4. 对 EnemySpawner 引入对象池
