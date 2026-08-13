# 怪物 AI 配置指南

本文说明如何基于当前 AI 行为树框架（`MonsterAIConfig` 单文件库 + 行为树 + 攻击范围拆分）为怪物配置 AI 行为。  
**所有怪的 AI 参数集中在一个资产 `Assets/Configs/MonsterAIConfig.asset`（同 `CardLibrary` 模式）**，怪物 prefab 只通过 `aiConfigId` 引用对应条目。

需求来源：`项目全量表 - 怪物设计（新）.csv`。  
参考实现：`Assets/Configs/MonsterAIConfig.asset`。  
快速测试：§9（`EnemyAiTest` + 调试圆环）。

---

## 0. 框架速览

```text
AIController（每决策节拍 Evaluate 行为树）
    ↓
行为树：Selector（优先级互斥短路）
  ├─ 攻击分支: Sequence[ InAttackRange → WeightedSelector{ Skill, Basic } ]
  ├─ 追击分支: Sequence[ InDetectRange → Selector{ 对峙, WeightedSelector{ Mobility, MoveToPlayer } } ]
  └─ Idle（兜底）
    ↓
意图声明模型：每拍清空输出缓冲（WantMove / MoveDir / Pressed），各分支重新声明
    ↓
MonsterActor 执行：朝向修正 → 移动 / 触发 EnemyAbility
```

核心类型：

| 类型 | 作用 | 典型路径 |
|---|---|---|
| `MonsterAIConfig` | AI 配置库（SO，单文件） | `Assets/Configs/MonsterAIConfig.asset` |
| `MonsterAIConfigEntry` | 单个怪的 AI 配置条目（内联，非 SO） | 库内 `entries` 列表 |
| `MonsterActor` | 怪物本体：`aiConfig` + `aiConfigId` 引用；只读属性转发 | `Assets/Scripts/Combat/Actors/MonsterActor.cs` |
| `AIController` | 行为树驱动：黑板刷新、决策节拍、意图声明 | `Assets/Scripts/Core/Control/AIController.cs` |
| `MonsterBTNodes` | 行为树条件/动作节点 | `Assets/Scripts/AI/BehaviorTree/MonsterBTNodes.cs` |
| `BTBlackboard` | 运行时状态：输入/输出缓冲/冷却/走位/追击计时 | `Assets/Scripts/AI/BehaviorTree/BTBlackboard.cs` |

---

## 1. 配置原则

1. **单文件库，一条目一怪**：所有怪的 AI 参数都在 `MonsterAIConfig.asset` 的 `entries` 里；每只怪一条 `MonsterAIConfigEntry`，靠 `id` 区分。
2. **prefab 只留一个 `aiConfigId` 字符串**：不在 prefab 上散落 AI 数值；改参数只动这一个资产。
3. **攻击范围独立可配置、无大小关系**：`basicAttackRange`（普攻范围）与 `skillAttackRange`（技能范围）相互独立，代码不做"谁大谁小"假设——远程怪可技能范围大，近战怪可普攻范围大。
4. **范围不可超出索敌半径**：`basicAttackRange` / `skillAttackRange` 应 ≤ `detectionRadius`，否则索敌失效（编辑期 `OnValidate` 会警告）。
5. **`chaseDuration = 0` 表示一直直线追击**（向后兼容旧行为）；配 > 0 才启用"追击超时转对峙"。
6. **`id` 全局唯一**：重复 id 编辑期警告，运行时后者被忽略。

---

## 2. 字段速查（`MonsterAIConfigEntry`）

### 2.1 范围（4 项）

| 字段 | 默认 | 含义 |
|---|---|---|
| `detectionRadius` | 8 | 索敌半径：玩家在此半径内才产生行为（追击/攻击），超出则待机 |
| `basicAttackRange` | 3 | 普攻范围：玩家在此半径内才能尝试普攻 |
| `skillAttackRange` | 6 | 技能范围：玩家在此半径内才能释放技能 |
| `aiMinRange` | 0 | 停步距离：玩家在此距离内不再前进（只侧移） |

### 2.2 攻击节奏（1 项）

| 字段 | 默认 | 含义 |
|---|---|---|
| `attackEagerness` | 0.8 | 攻击迟疑度（0~1）：技能/普攻 CD 就绪后，每决策拍仅有该概率真正出手；否则该拍放弃、等下一决策拍。1 = CD 好了立刻放，0 = 永不出手（仅调试）。用于让 AI 不「CD 一好就无缝放」 |

> **攻击冷却以技能自身 `cooldown` 为准**：AI 不再有独立的普攻/技能节拍（原 `basicCastTime`/`skillCastTime` 已移除）。`attackEagerness` 只控制「CD 好了之后出手的果断程度」，不额外增加冷却时间。

### 2.3 决策节拍（2 项）

| 字段 | 默认 | 含义 |
|---|---|---|
| `decisionIntervalMin` | 0.12 | 决策节拍最小间隔（秒） |
| `decisionIntervalMax` | 0.4 | 决策节拍最大间隔（秒） |

每只怪每次决策在 `[min, max]` 内随机取，间隔抖动 + spawn 相位随机化，避免同类怪同步行动。

### 2.4 行为权重（2 项）

| 字段 | 默认 | 含义 |
|---|---|---|
| `skillPriority` | 0.6 | 攻击范围内技能优先概率（0~1）。技能与普攻都就绪时按该权重随机选中技能 |
| `aiMobilityChance` | 0.3 | 追击时触发位移技能（冲刺）的概率（0~1）；位移冷却未就绪自动回退普通追击 |

### 2.5 追击时长（1 项）

| 字段 | 默认 | 含义 |
|---|---|---|
| `chaseDuration` | 0 | 连续直线追击时长上限（秒）。超过仍未进入攻击范围则转**对峙**（随机角度游走、面向玩家方向随机但不背离）。0 = 一直直线追击 |

### 2.6 追击走位（6 项）

| 字段 | 默认 | 含义 |
|---|---|---|
| `strafeChance` | 0.4 | 追击时随机走位概率（每次走位刷新有该概率侧移，否则直线追击） |
| `strafeIntervalMin` | 0.3 | 走位刷新间隔下限（秒） |
| `strafeIntervalMax` | 0.9 | 走位刷新间隔上限（秒） |
| `strafeStrength` | 0.45 | 侧移分量强度（0~1，1=完全横向） |
| `moveSpeedJitterMin` | 0.7 | 追击速度抖动下限（乘数，作用于 moveSpeed） |
| `moveSpeedJitterMax` | 1.3 | 追击速度抖动上限（乘数） |

### 2.7 调试可视化（1 项）

| 字段 | 默认 | 含义 |
|---|---|---|
| `showDebugRanges` | false | 游戏视图中用圆环可视化索敌/普攻/技能范围（黄/红/蓝三圈） |

---

## 3. 标准配置流程

### Step A — 读需求，定三半径一节奏

从 CSV 拆出：

| 问题 | 对应字段 |
|---|---|
| 怪多远开始注意玩家？ | `detectionRadius` |
| 普攻够得着多远？ | `basicAttackRange` |
| 技能够得着多远？ | `skillAttackRange` |
| 贴多近停步？ | `aiMinRange` |
| 攻击快慢？ | 由技能自身 `cooldown` 决定（`attackSpeed` 加速）；AI 出手果断度看 `attackEagerness` |
| 追多久算追不上？ | `chaseDuration`（0 = 不限） |

### Step B — 在资产里加条目

1. 打开 `Assets/Configs/MonsterAIConfig.asset`
2. `entries` 里新增一条 `MonsterAIConfigEntry`
3. 填 `id`（建议用怪物名，如 `Pride_new`、`Envy`），全局唯一
4. 填各字段；不确定的先保留默认值

### Step C — prefab 挂引用

在怪物的 `MonsterActor` 组件上：

| 字段 | 值 |
|---|---|
| `aiConfig` | 拖入 `Assets/Configs/MonsterAIConfig.asset` |
| `aiConfigId` | 填 Step B 的 `id`（字符串，精确匹配） |

**注意**：`aiConfigId` 为空或未命中时，AI 使用硬编码默认条目（即 §2 各默认值），不会报错但行为可能不符预期。

### Step D — 编辑期自检

`OnValidate` 会在编辑期自动检查并警告：

- `id` 重复
- `basicAttackRange` / `skillAttackRange` > `detectionRadius`
- `decisionIntervalMin` / `strafeIntervalMin` / `moveSpeedJitterMin` > 各自 max

出现警告应立即处理，否则运行时行为异常（如索敌失效、追击反向）。

### Step E — Play Mode 验收

1. 打开 `EnemyAiTest` 刷一只怪
2. 勾选该怪条目的 `showDebugRanges` 查看三圈是否与预期一致
3. 验证 §8 验收清单

---

## 4. 行为树语义详解

### 4.1 三分支与距离判定

| 玩家距离 | 走哪个分支 | 怪的行为 |
|---|---|---|
| > `detectionRadius` | Idle | 原地不动（没发现你） |
| `skillAttackRange` ~ `detectionRadius` | 追击分支 | 直线追击 + 随机走位 / 冲刺突进 |
| `basicAttackRange` ~ `skillAttackRange` | 攻击分支 | 只能放技能（普攻够不到）；技能冷却中回退追击 |
| ≤ `basicAttackRange` | 攻击分支 | 技能/普攻按 `skillPriority` 权重互斥出手 |

### 4.2 攻击范围拆分（普攻 / 技能）

- `InAttackRange = basic || skill`（任一范围内即进攻击分支）
- 攻击分支内部：`Skill` 带 `InSkillRange` 守卫、`Basic` 带 `InBasicRange` 守卫，`WeightedSelector` 按 `skillPriority` 权重互斥选择；范围不满足的分支自动回退到满足的那支
- 技能与普攻各自受冷却约束；同一决策节拍只执行一种攻击

### 4.3 追击时长与对峙

- 连续追击累计 `ChaseElapsed`；进入攻击范围（追上）或脱离索敌（丢目标）时清零
- `ChaseElapsed >= chaseDuration` 时，追击分支优先走**对峙**：不再直线扑向玩家，改为随机角度游走（面向玩家方向随机偏转 ±90°，不背离玩家）
- 速度复用 `moveSpeedJitter`，方向刷新节奏复用 `strafeInterval`
- `chaseDuration = 0` 时该判定恒 false，等价旧行为（一直直线追击）

### 4.4 AI 攻击朝向修正

AI 态下攻击前会先面向索敌目标（`FaceAttackTarget`，水平面 LookRotation），保证剑气/冲锋/斩击沿玩家方向打出，不受追击走位（侧移/对峙偏转）污染。

---

## 5. 当前怪物 AI 配置（已落地）

| id | 索敌 | 普攻 | 技能 | 停步 | 迟疑度 | 备注 |
|---|---:|---:|---:|---:|---:|---|
| `Pride` / `Pride_new` | 15 | 4 | 6 | 3 | 0.6 | 傲慢：远索敌，出手迟疑明显 |
| `Envy` | 15 | 4 | 6 | 3 | 0.9 | 嫉妒：出手几乎不迟疑（激光快攻） |
| `Wrath` | 15 | 3 | 6 | 2 | 0.6 | 愤怒：近战，出手迟疑明显 |
| `Sloth` | 15 | 5 | 6 | 2 | 0.7 | 怠惰 |
| `Greed` | 15 | 5 | 6 | 2 | 0.8 | 贪婪 |
| `SwordShield` | 15 | 3 | 6 | 2 | 0.8 | 剑盾 |

当前 7 个怪的索敌半径 `detectionRadius` 统一为 **15**。其余字段（`skillPriority=0.6`、`aiMobilityChance=0.3`、`chaseDuration=0`、`strafe*`、`moveSpeedJitter*`）目前全部用默认值。攻击冷却一律由各技能自身 `cooldown` 决定。

---

## 6. 常见坑

| 现象 | 原因 | 处理 |
|---|---|---|
| 怪原地不动（技能范围内普攻范围外） | 该怪的 Skill 技能 `CanTrigger()` 对 AI 态返回 false（如 `FindNearestTarget` 只找怪不找玩家），而行为树技能节点无条件成功、不产出移动 | 修正该技能 AI 态目标判定（对 `owner.targetPlayer`），参考 `PrideBlinkChain` 分态实现 |
| 攻击方向打偏 | AI 态攻击方向取 `transform.forward`，而追击走位会偏转 forward | 已由 `FaceAttackTarget` 修正；若仍偏，检查技能是否绕过 `ExecuteButtons` 直接触发 |
| 怪索敌半径外就出手 | `basic/skillAttackRange > detectionRadius` | 调小攻击范围或调大索敌半径；编辑期有警告 |
| 改了 prefab 上数值不生效 | AI 参数已收口到资产，prefab 上无这些字段 | 改 `MonsterAIConfig.asset` 对应条目 |
| `aiConfigId` 写错/漏填 | 未命中条目 → 用默认值 | 检查 id 精确匹配（大小写敏感） |
| 所有怪同步出手 | 决策节拍未随机化 | 确认 `decisionIntervalMin/Max` 有差异；间隔抖动 + 相位随机已内置 |
| 追不上还死磕直线 | `chaseDuration = 0` | 配 > 0 启用对峙 |

---

## 7. 代码接入点（二次开发参考）

| 文件 | 职责 |
|---|---|
| `Assets/Scripts/AI/MonsterAIConfig.cs` | 配置条目 + 库 + `Get(id)` + 默认条目 + `OnValidate` |
| `Assets/Scripts/Core/Control/AIController.cs` | `BuildTree()` 接线、`RefreshBlackboard`、决策节拍、追击计时、意图声明 |
| `Assets/Scripts/AI/BehaviorTree/MonsterBTNodes.cs` | 条件节点（InSkillRange / InBasicRange / InDetectRange / ChaseExpired / SkillReady / BasicReady / MobilityReady）+ 动作节点（Skill / Basic / Mobility / MoveToPlayer / Standoff / Idle） |
| `Assets/Scripts/AI/BehaviorTree/BTBlackboard.cs` | 运行时状态：`ChaseElapsed` / `StandoffMove` / `StandoffTimer` 等 |
| `Assets/Scripts/Combat/Actors/MonsterActor.cs` | `aiConfig` + `aiConfigId` + `AiConfig` 属性 + 只读属性转发 + `FaceAttackTarget` |

---

## 8. 验收清单

- [ ] `entries` 里每个怪有唯一 `id`
- [ ] 每个怪 prefab 的 `aiConfig` 已挂库、`aiConfigId` 精确匹配
- [ ] `basicAttackRange` / `skillAttackRange` ≤ `detectionRadius`（无 OnValidate 警告）
- [ ] `basicAttackRange` 与 `skillAttackRange` 按怪定位独立设置（远程/近战）
- [ ] `attackEagerness` 符合攻击节奏（CD 好后出手的果断度）
- [ ] 各技能自身 `cooldown` 符合攻击节奏（唯一事实源）
- [ ] `skillPriority` / `aiMobilityChance` 符合预期权重
- [ ] `chaseDuration` 按需设置（0 = 一直追，> 0 = 超时对峙）
- [ ] Play 下勾选 `showDebugRanges` 验证三圈
- [ ] 索敌范围外待机、追击、攻击范围独立生效、追击超时对峙

---

## 9. 测试流程

### 9.0 调试按键速查

**EnemyAiTest 场景（AI 行为调试，`EnemyAIAttackTestSpawner` 脚本）**

| 按键 | 功能 |
|---|---|
| `1` ~ `9` | 按数字刷出 catalog 中对应序号的普通敌人（**不附身**），AI 自动索敌/追击/攻击玩家 |
| `0` | 刷一只随机普通敌人 |
| `F1` | 切换屏幕提示面板显隐 |

- 刷出的怪**默认显示调试距离圆环**（黄=索敌 / 红=普攻 / 蓝=技能），无需手动勾选
- 每次刷怪在 Console 打印该怪 AI 配置摘要（索敌/普攻/技能范围/迟疑度/追击时长），方便核对配置是否生效
- 刷怪位置 = 玩家位置 + `spawnOffset`（默认右移 3m）

### 9.1 调试圆环

1. 打开 `Assets/Configs/MonsterAIConfig.asset`
2. 找到目标怪条目，勾选 `showDebugRanges`（或使用 `EnemyAIAttackTestSpawner` 刷怪，自动强制显示）
3. Play Mode 下 Game 视图显示三圈：黄（索敌）/ 红（普攻）/ 蓝（技能），随怪移动

### 9.2 场景准备

- `EnemyAiTest`：专项验证 AI 行为（索敌/追击/攻击范围/对峙），挂 `EnemyAIAttackTestSpawner`，数字键刷普通敌人观察 AI 攻击

### 9.3 推荐测试步骤

1. 打开 `EnemyAiTest` 场景 → Play
2. 按数字键刷一只怪（自动显示调试圆环，Console 打印 AI 配置摘要）
3. 站在索敌范围外 → 怪待机
4. 走进索敌范围、攻击范围外 → 怪追击（观察走位/冲刺）
5. 走到技能范围内普攻范围外 → 怪只放技能（不放普攻、不僵住）
6. 贴脸 → 技能/普攻按权重互斥出手
7. 配 `chaseDuration > 0` 后 → 追满时长转对峙（随机角度游走、不背离玩家）
8. 观察攻击方向是否始终朝向玩家（不受走位偏转影响）
9. 多刷几只不同怪对比节奏差异（`attackEagerness` 迟疑度 / 技能 `cooldown`）

