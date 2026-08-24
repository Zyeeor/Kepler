# Possession：七罪罪印、双源生怪与七相缝合 Boss

> 状态：Detailed Contract（设计已决策；实现待工作树隔离）  
> 日期：2026-08-21  
> 适用项目：Kepler / Possession  
> GitHub Issue：https://github.com/Zyeeor/Kepler/issues/7  
> 仓库路径：`Tasks/Active/ENG-POSS-001-seven-imprints-spawn-sevenfold-boss.md`  
> 设计依据优先级：`.vibe/rules.md` → Canonical → 本次 Owner 需求 → 当前实现  
> 实施前置：Issue 与 Contract 已建立；当前 Unity 工作树存在 Owner 的未提交修改，资产写入前必须隔离或恢复干净状态。

---

## 0. 执行摘要

本需求拆为三个相互解耦、由事件连接的系统：

1. **七罪罪印（Run 级）**：真实夺舍每次使对应罪印基础 `+1`；状态跟随一局而不是跟随身体，也不属于 Card 叠层。七种效果作用于当前玩家附身体，灵魂态保持脆弱。
2. **RunSpawnDirector（遭遇级）**：统一管理常压生怪、击杀回响生怪、30 秒强度快照和 8:00 Boss 接管。所有生成仍通过现有 `MonsterSpawner` 与合法落点检查。
3. **Sevenfold Convergence Boss（Actor 级）**：新建独立 Boss Prefab，以七个 `*_new` Prefab 的视觉子部件组合，不修改任何源 Prefab；持有七怪共 14 个非 Movement 槽技能，由距离、命中可行性、前置状态和最近使用记录驱动选技。

核心数据流：

```text
PossessionManager --PossessionCommitted--> PossessionImprintManager
       |                                      |--> 七罪运行时效果
       |                                      |--> HUD / 首次提示
       |
MonsterActor --FatalEvent------------------> RunSpawnDirector
                                               |--> PeriodicPressure
                                               |--> KillEcho window limiter
                                               |--> 30s SpawnTier snapshot
                                               `--> 480s Boss takeover

BossCombatBrain --> Range/LOS/State/History scorer --> one exact ability
Boss Gluttony Devour hit --> BossAffixAssimilator --> mirror player's E affixes
```

---

## 1. 目标、范围与非目标

### 1.1 Goal

- 让高频夺舍在一局内形成可读、可成长、鼓励换体的长期收益。
- 让场上压力同时来自时间和玩家主动击杀，并保证可控、不出现无界瞬时增殖。
- 在 8:00 提供一个能检验七怪知识、视觉上由七怪缝合、AI 能按距离正确选技的 Boss。
- 所有新增状态支持波间存档和继续游戏；对象池复用不造成数值累乘。

### 1.2 In Scope

- 七罪身份、罪印层数、数值公式、运行时应用、存档、首次提示、HUD、Tooltip。
- 常压生成、击杀回响生成、时间强度、生成来源标记、Boss 计时与接管。
- Boss Prefab、Actor、AI、技能适配、词条同化、浮空移动、瞬移与材质表现。
- EditMode/PlayMode 测试、Unity 编译与场景验收。

### 1.3 Out of Scope

- 修改 Canonical 或把 Boss 与七宗罪的世界观关系升格为正式 Canonical。
- 修改七个源 Monster Prefab、共享 Material、Packages、ProjectSettings。
- 为 Boss 制作骨骼动画、全新 3D 模型、全新音乐或剧情演出。
- 重做卡牌系统；罪印不是 Card，不受 Card `stack max = 1` 约束。
- Boss 的玩家夺舍版本。Boss 不可夺舍。
- 七怪 Movement 槽技能；Boss 的系统瞬移不是复用怪物 Movement 技能。

---

## 2. 已决策事项与规则冲突处理

| 编号 | 决策 | 理由 |
|---|---|---|
| D-01 | “提升 CD”解释为**降低技能冷却**，UI 写“冷却缩减” | “提升 CD”字面会变弱，不符合其余六项皆为增益 |
| D-02 | 罪印属于 Run History，不是身体被动拷贝，也不是 Card | 保持 Canonical 的 Carrier-bound 被动规则和 Card 单层规则 |
| D-03 | 每次真实夺舍必得基础 1 层；Load Restore、Debug、重复回调不加层 | 防读档、调试与事件重入刷层 |
| D-04 | 初始分配的傲慢身体算第一次真实获得，给予傲慢 1 层 | 与“第一次夺舍”体验一致 |
| D-05 | 七罪数值作用于当前附身体；灵魂态不吃 HP、攻击、血条消耗减免、体型收益 | 保留 Canonical 的灵魂脆弱性 |
| D-06 | 贪婪采用确定性小数累加，不用随机额外层 | UI 可解释、可测试、无坏运气 |
| D-07 | 击杀回响怪也可继续触发回响，但 10 秒窗口硬限 4 次；超额击杀不延期补发 | 满足“杀一生一”，同时避免积压爆发 |
| D-08 | 生成强度在出生时快照；场上旧怪不随 Tier 跳变 | 避免半分钟节点瞬间改变战斗结果与池化累乘 |
| D-09 | 8:00 使用“有效战斗实时时钟”：暂停、选卡、结算不计；子弹时间仍按真实秒计 | 玩家看到的是精确八分钟战斗时间，慢动作不拖延 Boss |
| D-10 | Boss 出场吞噬所有非玩家 MonsterActor（活怪与尸体），随后 3 秒内补 3 个可夺舍普通小怪 | 满足“吞掉全场”，同时不长期移除夺舍循环 |
| D-11 | Boss 吞噬成功命中当前附身体时，镜像该身体 E/Special 的全部已激活词条到 Boss 自己的同源技能；不复制技能实例 | 精确落实“获得词条而不是获取技能” |
| D-12 | 同化词条在本场 Boss 战中取并集、唯一化、持续到 Boss 死亡 | 让多次吞噬有成长反馈，避免重复叠同一单层 Card |
| D-13 | Boss 世界观暂称“七相收束载体 / Sevenfold Convergence”，不声明它就是七罪本体、终极存在或正式终局 | `OD-CAN-005` 仍是 Open Decision |
| D-14 | 怠惰罪印不再提供承伤减免；改为降低当前附身体的血条消耗，覆盖附身自然消耗与技能/移动技能 HP 消耗 | 与“血条作为附身与技能资源”的实际玩法一致 |

### 2.1 与 Canonical / Open Decision 的显式偏差

- `OD-CAN-002` 当前把 Boss inclusion 标为 Deferred / Milestone-dependent；本次 Owner 需求明确要求实现，本文将其视作**本 Task 的局部 Owner 决策**，不回写 Canonical。
- `OD-CAN-005` 的 Boss 与七罪关系仍开放；表现只使用“系统把七种行动语法缝成测试载体”的临时叙事，不升格世界观真相。
- Canonical 不鼓励以统一 HP/攻击倍率作为主要难度手段；本需求明确要求每 30 秒提升，因此只将倍率作为**次级压力**，不增加移动速度、控制时长或攻击频率，主要压力仍来自组合、数量和技能选择。
- 当前 Run 约五分钟并以八波结构为主；Boss 要求固定 8:00。实现时 Run 在最后普通波完成后不得提前胜利，进入等待/最终压力阶段，8:00 生成 Boss，击杀 Boss 后才 Result。

---

## 3. 七罪罪印系统

### 3.1 领域模型

```csharp
public enum SinType { Pride, Wrath, Gluttony, Greed, Envy, Lust, Sloth }

public enum PossessionGrantReason
{
    InitialAssignment,
    PlayerPossession,
    DeathRelay,
    LoadRestore,
    Debug
}

[Serializable]
public struct PossessionImprintState
{
    public SinType sin;
    public int stacks;
}
```

- `MonsterActor` 增加序列化的 `SinType sinType`，七个新 Prefab 在 Unity 中配置。
- `PossessionManager` 在**事务提交完成之后**只发出一次 `PossessionCommitted(body, reason, transactionId)`。
- `PossessionImprintManager` 保存 7 个整数层数与贪婪小数进度，按 `transactionId` 幂等消费。
- 每局 `BeginNewRun` 清零；`LoadFromSave` 恢复；`EndRun` 清零。
- 旧 `PlayerPassiveManager` 的四项实验性逻辑迁移为兼容外壳后停用，不能与新罪印叠加。

### 3.2 层数获取

真实来源：

- `InitialAssignment`：`+1`
- `PlayerPossession`：`+1`
- `DeathRelay`：若确实完成到另一具身体的自动接管，`+1`
- `LoadRestore`：`+0`
- `Debug`：`+0`

算法：

```text
oldGreed = Greed stacks before this transaction
target stacks += 1
greedProgress += min(oldGreed * 0.05, 1.00)
if greedProgress >= 1:
    target stacks += 1
    greedProgress -= 1
```

- 贪婪取事务开始前的层数，避免夺舍贪婪时立即递归自增。
- 单次事务最多奖励 1 个额外层；每次仍保证基础 `+1`。
- 所有罪印层数统一受可配置的 `MaxStacks` 限制，首版为 `100` 层；达到上限后不再增加该罪印层数，收益仍按各自效果上限计算。

### 3.3 数值公式（首版 TUNABLE）

| 罪印 | 每层 / 公式 | 效果上限 | 作用域与实现钩子 |
|---|---|---|---|
| 傲慢 Pride | 实际冷却倍率 `1 / (1 + 0.05 × N)` | 层数上限 `100`；100 层约 83.3% CDR | 当前附身体全部 Attack/Special/Movement 的 `EnemyAbility.EffectiveCooldown`；不改动画速度 |
| 愤怒 Wrath | 造成伤害 `+6% × N`，加法累计 | `+120%` | 当前附身体所有经 `ApplyOffensiveDamage` 的结算；DoT 按源快照，不重复乘 |
| 暴食 Gluttony | 最大生命 `+5% × N`；视觉体型 `+2.5% × N` | HP `+100%`；体型 `+25%` | 只放大 `visualScaleRoot`，不缩放根 Collider/NavMeshAgent；新层按当前生命比例重算，不白送治疗 |
| 贪婪 Greed | 每次后续真实夺舍增加 `5% × N` 额外层进度 | 每次最多 `100%`，最多奖 1 层 | 见 3.2；HUD Tooltip 同时显示当前进度 |
| 嫉妒 Envy | 子弹时间 `+0.15s × N` | `+3.0s` | 叠加在 `PossessionManager` 的基础持续时间上；手动与现有自动触发共用 |
| 色欲 Lust | 有效攻击命中控制概率 `+2% × N` | `30%` | 每个目标、每次 ability activation 最多判定一次；DoT tick/弹射重复命中不重复抽取 |
| 怠惰 Sloth | 血条消耗倍率 `1 - min(60%, 1 - 1 / (1 + 0.04 × N))` | `60%` 消耗减免 | 当前附身体的附身自然消耗、Attack/Skill/Movement 的 HP 消耗；不作用于承伤、真实处决或规则伤害 |

#### 怠惰血条消耗细则

- 怠惰罪印只降低“玩家当前附身体主动承担的血条消耗”，不改变怪物受到的外部伤害，也不提供传统意义上的减伤。
- 覆盖两类来源：`PossessionManager` 的附身自然衰减，以及 `MonsterActor` 对 Attack、Skill、Movement 和持续型 Ability 的 HP cost。
- 每次消耗先计算 Ability 自身的基础倍率，再乘以 `PossessionImprintMath.SlothDrainMultiplier(N)`；非附身状态、灵魂态和 `suppressPossessionDrain` 路径不受影响。
- 首版公式在 `N=100` 时封顶为 60% 消耗减免（实际消耗倍率 40%）；罪印层数上限仍由统一 `PossessionImprintMath.MaxStacks` 参数控制。

#### 暴食体型细则

- 每个 Monster Prefab 增加可选 `visualScaleRoot`；没有时由配置明确指定视觉节点，禁止直接缩放 Actor 根节点。
- 池化时恢复 Authored Scale；罪印应用使用 `authoredScale × sizeMultiplier`，绝不在上次 Scale 上继续乘。
- 射线、近战距离、碰撞体、导航半径不跟随体型增长，避免攻击范围与卡位的隐式增强。

#### 色欲控制细则

- 有效目标：活着、非玩家身体、非 Boss、非 Elite、非已被控制的普通怪物。
- 触发后进入 `Charmed` 2.5 秒：临时敌对目标改为最近普通敌人，不能伤害玩家，也不算夺舍。
- 结束后恢复原 AI；目标死亡、被夺舍、场景卸载时立即清理。
- 一次多段攻击用 `AbilityActivationId + TargetInstanceId` 去重。
- 不用每帧场景扫描；通过 `EnemyRegistry` 获取候选，并在 AI 决策间隔查询。

### 3.4 首次提示

- “第一次”按 **Profile + SinType** 记录，跨局不重复。
- 与 Canonical 的 `TUT-MONSTER-*` 微教学合并为同一个非暂停提示，不叠两个弹窗。
- 提示触发时机：首次真实夺舍提交、玩家重新获得控制后的下一帧。
- 展示 4 秒；任何输入均可关闭；进入设置可关闭微教学。
- 即使微教学关闭，层数仍正常增加。
- 文案模板：

```text
[罪印图标] 傲慢罪印 · 冷却缩减
每次夺舍傲慢，傲慢罪印 +1。
当前 1 层：技能冷却缩短 4.8%。
```

- Profile 进度独立于 Run Save；用现有 Profile/PlayerPrefs 轻量键保存：`tutorial.sin_imprint.<sin>.seen.v1`。

### 3.5 局内 HUD 与 Tooltip

- 七个图标固定显示，顺序：傲慢、愤怒、暴食、贪婪、嫉妒、色欲、怠惰。
- 位置：沿用当前左下 HUD 的上方横排/两行紧凑布局，不遮挡生命、技能槽与世界画面。
- 0 层为 35% 明度；获得后恢复完整颜色并播放 0.25 秒“烙印压入”脉冲。
- 图标必须同时通过**轮廓/纹样**区分，不能只靠颜色：冠、裂拳、巨口、叠手、瞳眼、心钩、闭眼盾。
- 右下角显示 `×N`；层数变化数字短暂上浮；贪婪图标外圈显示额外层进度。
- 鼠标/手柄焦点进入后 Tooltip 展示：
  - 名称、效果类别；
  - 当前层数；
  - 每层规则；
  - 当前总收益；
  - 下一层预览；
  - 已达效果上限时显示“效果已达上限，罪印层数仍会记录”。
- Tooltip 使用单一 Presenter、跟随锚点并限制在屏幕安全区；离开、场景切换、HUD 禁用时关闭。

---

## 4. 双源生怪与半分钟强度

### 4.1 单一生成入口

新增 `RunSpawnDirector`，但不复制生成基础设施：

```text
WaveManager / periodic tick -----\
                                  > RunSpawnDirector -> MonsterSpawner -> MonsterPool
Monster fatal / kill echo -------/
Boss / boss minion requests -----/
```

每个请求携带：

```csharp
public enum SpawnOrigin
{
    PeriodicPressure,
    KillEcho,
    BossMinion,
    Boss
}

public readonly struct SpawnRequest
{
    public SpawnOrigin origin;
    public int difficultyTier;
    public Vector3 avoidPosition;
    public float minDistanceFromAvoid;
    public float expiryTime;
}
```

所有来源共享：合法地面、屏幕外生成、离玩家安全距离、`maxCombatMonsters`、对象池。

### 4.2 来源 A：常压生成

- 复用现有 `WaveManager` 的波表和权重池，将其标记为 `PeriodicPressure`。
- 每 8 秒做一次压力 Tick，而不是无条件刷满：

| 有效战斗时间 | 目标活怪数 | 单 Tick 最大补怪 |
|---|---:|---:|
| 0:00–1:59 | 6 | 2 |
| 2:00–4:59 | 8 | 3 |
| 5:00–7:59 | 11 | 4 |

- 实际数量为 `min(本档最大补怪, 目标活怪数 - 当前活怪数, 全局容量)`。
- Card Choice、Pause、Result、Failed、Boss 在场时停 Tick。
- 当前 Wave 的合法怪物集合和已有解锁顺序保持不变。

### 4.3 来源 B：击杀回响

- 仅**玩家归因的 Fatal Kill**触发；环境死亡、Boss 吞噬、调试删除、超时回收不触发。
- 每杀 1 只符合条件的普通怪，创建 1 个 `KillEcho` 请求。
- 10 秒滚动窗口最多成功生成 4 只；第 5 次及以后直接丢弃，不跨窗口积压。
- 请求随机延迟 0.6–1.1 秒，落点：
  - 屏幕外合法地面；
  - 离玩家至少 12m；
  - 离被杀位置至少 15m，体现“另一个地方”；
  - 同一帧最多落 1 只。
- `KillEcho` 怪再次被玩家杀死也能触发新的回响，只受同一窗口限制。
- 达到全局活怪上限时，请求最多等待 2 秒，随后过期，避免延迟队列在降压后突然倾泻。
- `KillEcho` 不计入 Wave 的目标配额；Wave 结束/进入 Choice 时全部安全淡出且不掉落、不触发回响，保证选卡安全。

### 4.4 每 30 秒出生快照

```text
tier = floor(activeCombatSeconds / 30)
healthMultiplier = min(1 + 0.10 × tier, 3.00)
damageMultiplier = min(1 + 0.06 × tier, 2.20)
```

示例：

| 时间 | Tier | HP | 攻击 |
|---|---:|---:|---:|
| 0:00 | 0 | 1.00× | 1.00× |
| 2:00 | 4 | 1.40× | 1.24× |
| 4:00 | 8 | 1.80× | 1.48× |
| 6:00 | 12 | 2.20× | 1.72× |
| 8:00 | 16 | 2.60× | 1.96× |

- 在 `MonsterPool.ResetForSpawn()` 完成基础重置后一次性应用。
- `MonsterActor` 记录 `spawnDifficultyTier/baseSpawnMaxHealth/damageMultiplier`。
- 伤害倍率在敌方伤害进入 `PlayerHealth` 前只乘一次；投射物/DoT 在生成时携带来源倍率快照。
- 不修改 Prefab 序列化值，不修改 Ability Asset，不对已存活怪回溯更新。
- Boss 用独立基础数值，但其伤害仍参考 8:00 的 Tier 16。

### 4.5 有效战斗时钟

- `RunSpawnDirector.ActiveCombatSeconds` 为唯一时间源，Run Save 持久化。
- 仅 `RunPhase.Waves` 或 `RunPhase.Final` 且游戏未 Pause/CardChoice/Result/Failed 时推进。
- 使用 `unscaledDeltaTime`，因此 Bullet Time 期间仍按真实秒推进。
- UI 可在调试面板显示 `mm:ss / Tier / EchoWindow 计数`；正式 HUD 无需新增大计时器，若现有计时器存在则复用。

---

## 5. 七相缝合 Boss

### 5.1 身体拼接方案

新 Prefab：`Assets/Prefabs/Monster/Boss_Sevenfold_Convergence_new.prefab`

| 来源 Prefab | 采用部位 / 视觉职责 |
|---|---|
| `gluttony_new.prefab` | 核心躯干、巨口与腹部质量，作为缝合中心 |
| `pride_new.prefab` | 头冠、上胸与 `AoMang-Jian` 武器，形成主轮廓和权威感 |
| `wrath_new.prefab` | 右臂、燃烧脊刺/锁链挂件，承担近战侧 |
| `greed_new.prefab` | 左臂与多手结构，承担护卫、念力手侧 |
| `sloth_new 1.prefab` | 背部机械球/炮体，承担蓄力射击和无人机舱 |
| `envy_new.prefab` | 下身悬浮结构、尾部/能量肢，替代步行腿 |
| `lust_new.prefab` | 背后心钩光环、飘带/锚形装饰，平衡剪影 |

构建约束：

- 七个源 Prefab 均视为 Shared Original：**不修改、不 Apply Override、不改 Material**。
- 新 Boss 是独立 Prefab；视觉组以嵌套只读实例或复制视觉子树组成，源脚本、Animator、Collider、AudioListener 全部在 Boss 视觉组中禁用。
- Boss 只有一套根 Collider/NavMesh/Targetable/Actor 组件，避免七套逻辑重复运行。
- Skinned Mesh 必须连同自身最小骨架子树保留；没有动画时锁定静态 Pose。
- 每个来源形成一个 `VisualPart_<Sin>` 子根，允许单独做材质脉冲和受击反馈。
- 最终高度约普通中型怪 2.4 倍；下沿离地 1.4m，阴影/投影明确表达浮空高度。

### 5.2 Actor 与基础数值

新增 `BossSevenfoldActor : Enemy`：

- `isPossessable = false`；致死直接进入 Boss Death，不生成可夺舍尸体。
- 不进入普通池的随机生怪表；只由 Boss Director 在 480 秒生成。
- 首版数值：
  - Max HP：`max(8000, medianNormalBaseHp × Tier16HP × 24)`；Inspector 最终烘焙为可调值。
  - 伤害：同技能普通 8:00 怪伤害的 `1.65×`。
  - 移速：普通中型怪 `0.85×`，但有浮空追踪和系统瞬移。
  - 受控、魅惑、夺舍免疫；普通硬直抗性 80%，重 Telegraph 打断规则另行配置。
- Boss 在 `EnemyRegistry` 注册，但 `MonsterSpawner` 的普通容量统计单独排除/保留 1 个 Boss 槽。

### 5.3 8:00 出场流程

1. `7:55`：停止新 Periodic/Echo 请求；取消未落地 Echo；屏幕边缘出现七罪纹样收束，系统提示“检测到七相收束”。
2. `8:00.000`：在玩家 24–32m 的合法屏幕外位置创建 Boss 并 Pin 所在地图 Chunk。
3. 0.0–1.0s：Boss 不可伤害；把 `EnemyRegistry` 快照到临时数组，吞噬除玩家当前身体和 Boss 自身之外的全部 MonsterActor，包括尸体。统一走 `ConsumedByBoss` 原因，不发 FatalKill/Echo/奖励。
4. 1.0–2.2s：怪物化作七色但低饱和的粒子/材质流汇入腹部巨口；Boss HP 条出现。
5. 2.2s：Boss 可伤害并启动 AI。
6. 3.0s：在远离玩家的三个方位生成 3 个正常、可击倒、可夺舍小怪；Boss 战中若“活小怪 + 可夺舍尸体 < 3”，每 18 秒补至 3，最多同时 5。
7. Boss 死亡：解除 Chunk Pin，清理临时词条、召唤物和 MPB 状态，进入 Run Result。

### 5.4 技能集合

Boss 持有七怪的 Attack + Special 共 14 个能力，不挂七个 Movement 槽：

| 罪 | 近/中程 | 中/远程或机制 |
|---|---|---|
| 傲慢 | 剑气 `EnemyAbility_SwordQi` | 穿梭斩 `EnemyAbility_PrideBlinkChain` |
| 愤怒 | 双拳砸地 `EnemyAbility_WrathSlam` | 暴怒锁链 `EnemyAbility_WrathChainStorm` |
| 暴食 | 深渊巨口 `EnemyAbility_GluttonyAbyssMaw` | 吞噬 `EnemyAbility_GluttonyDevour` |
| 贪婪 | 念力魔手 `EnemyAbility_GreedHands` | 大手 Guard `EnemyAbility_GreedGuard` |
| 嫉妒 | 激光 `EnemyAbility_EnvyLaser` | 雷暴兑现 `EnemyAbility_EnvyThunderstorm` |
| 色欲 | 迷情往返 `EnemyAbility_LustRoundTrip` | 诱引牵魂 `EnemyAbility_LustSoulPull` |
| 怠惰 | 地爆天星 `EnemyAbility_SlothChargeShot` | 木灵 `EnemyAbility_SlothDrone` |

适配规则：

- 傲慢穿梭斩可使用 Boss 的瞬移材质通道，但仍由原 Special 的伤害/Telegraph 规则结算。
- 嫉妒雷暴只有在玩家身上存在 Envy Mark 时入候选；没有 Mark 时先提高激光权重。
- 色欲 SoulPull 需要 Anchor 时，使用最近一次 Boss 瞬移留下的 6 秒空间残影作为 Anchor；没有残影则不可选。
- 贪婪魔手资源由 Boss 被动每 2 秒生成 1 只，上限使用原能力配置；Guard 在资源不足时不可选。
- 暴食吞噬必须成功命中/抓取玩家当前身体才触发词条镜像；空放不获益。
- 怠惰 Drone 同时存在数量遵循原上限，不因 AI Tick 重复生成。

### 5.5 距离感知与反单一化 AI

不能复用当前“对一个 Slot 发脉冲、同 Slot 全部能力一起触发”的方式。新增 `BossCombatBrain`，每次只触发一个确定能力。

每个能力配置 `BossAbilityProfile`：

```csharp
ability, family, minRange, maxRange, preferredRange,
requiresLineOfSight, requiresState, baseWeight,
minimumRepeatGap, dangerClass, phaseWeights
```

决策间隔：0.30 秒；仅在当前能力完成、没有全局后摇且 Boss 可行动时评估。

硬过滤：

- `ability.CanTrigger == true`
- 玩家处于 `[minRange, maxRange]`
- LOS / 导航 / Anchor / Mark / 资源等前置满足
- 最近 2 次不能是同一 ability；最近 1 次不能是同 family
- 高危险能力后至少留 0.8 秒安全间隔
- 近战能力超出可命中距离绝对不能选择

评分：

```text
score = baseWeight
      × distanceFit(0..1)
      × phaseWeight
      × setupSynergy
      × antiRepeat
      × deterministicJitter(seed, decisionIndex, 0.85..1.15)
```

- 近距 `<6m`：砸地、巨口、Guard 反击、SoulPull 收尾权重高。
- 中距 `6–14m`：剑气、锁链、激光、念力手为主。
- 远距 `>14m`：蓄力射击、Drone、激光；若 2 秒内没有可命中候选则浮空追近或瞬移到 8–12m。
- 连续 3 次决策无候选、导航卡住 1.5 秒、或距离超过 24m 时可申请系统瞬移。
- AI 随机使用 `RunSession.WorldSeed` 派生流，不调用全局随机破坏复现性。

阶段：

| HP | 行为 |
|---|---|
| 100–70% | 单技能探测，重视中远程，瞬移冷却 14s |
| 70–35% | 增加 Setup→Payoff（激光→雷暴、往返→牵魂），瞬移冷却 11s |
| 35–0% | 决策后摇缩短 15%，可进行两技组合但两个高危 Telegraph 不重叠，瞬移冷却 9s |

### 5.6 暴食吞噬：只获得词条

新增 `BossAffixAssimilator`：

1. 只在 Boss 的 `GluttonyDevour` 成功命中玩家当前附身体时运行。
2. 找到该身体的 `skillAbilities`（E/Special 槽）。
3. 从 `CardManager.UnlockedEffects` 和 `EliteBuildCarrier` 收集真正作用于这些 Special 的已激活 effectId。
4. 用 `CardManager.DoesCardTargetAbility` / target tag 规则过滤 Global、Attack、Movement 与无关技能词条。
5. 找到 Boss 已有的同源 Special；把唯一词条通过 Boss 自己的本地升级容器应用到它，禁止把玩家 Ability Component 复制/重挂给 Boss。
6. 已拥有的 effectId 不重复应用；同化集合持续到 Boss 死亡。
7. 如果玩家当前 E 没有词条，Boss 不获得任何东西，只播放“空同化”反馈。

例：玩家附身嫉妒，E=雷暴兑现，拥有“雷暴范围扩大”“雷暴延迟缩短”；Boss 已经有雷暴兑现。吞噬命中后，Boss 的雷暴获得这两个词条，Boss 不新增第二个雷暴，也不获得玩家 Attack/Movement。

### 5.7 浮空、移动与瞬移表现

现有 `Possession/CharacterFX` 支持 Dissolve、Rim、HitFlash，可复用受击/出现通道，但不足以表现空间折射。新增 Boss 专用 Shader/Material，不改共享材质：

`BossSevenfoldDistortion.shader` 属性：

- `_DistortionStrength`
- `_VertexWarp`
- `_FlowDirection`
- `_ChromaticSplit`
- `_DissolveAmount`
- `_RimPulse`
- `_SinChannel`

由 `BossSpatialDistortionController` 使用 `MaterialPropertyBlock` 驱动，禁止 `renderer.material` 产生运行时材质副本。

状态表现：

- Idle：上下 0.18m、1.1Hz 浮动；七个 VisualPart 的 Rim 以错峰低频呼吸。
- Move：沿速度反方向拉伸 UV/顶点 0.12–0.25，身后生成薄层空间残影；速度归零后 0.2 秒衰减。
- Teleport Out（0.18s）：轮廓向核心压缩、色差增大、Dissolve 到 1；原地留 6 秒空间残影供 Lust Anchor。
- Reposition：选择合法地面上方 1.4m，不能进墙、深坑、玩家 5m 内。
- Teleport In（0.22s）：先出现七罪轮廓，再从中心重构；0.15 秒后恢复伤害判定。
- Cast：当前技能所属罪的 VisualPart 和符号升亮；高危技能同时提供地面范围、方向与生效倒计时。

---

## 6. 存档、池化与事件纪律

### 6.1 Save Schema

`SaveData` 新增字段意味着 `SchemaVersion 2 → 3`，必须同时新增 `SaveMigrator v2→v3`：

- `float activeCombatSeconds`
- `List<PossessionImprintState> possessionImprints`
- `float greedBonusProgress`
- `bool bossSpawned`
- `bool bossDefeated`

波间存档不保存进行中 Boss 的瞬态；Boss 战中退出按现有安全存档语义回到最近波间点。若产品要求 Boss 中途续战，另立 Task，不在本范围猜测协程/投射物恢复。

### 6.2 Event / Kill Attribution

新增统一 Fatal 事件：

```csharp
MonsterFatalEvent(actor, killer, cause, spawnOrigin, transactionId)
```

`cause` 至少区分：`PlayerDamage / Environment / BossConsume / WaveCleanup / Debug / Timeout`。只有 `PlayerDamage` 进入 KillEcho。

### 6.3 Pool Reset

每次出池先恢复：

- Authored maxHealth/currentHealth/scale/damage multipliers
- `spawnOrigin`、difficulty tier、控制/魅惑状态
- Boss 同化词条与 MPB（Boss 若不池化仍实现清理）
- Ability cooldown、激活 id 与去重集合

再应用本次请求快照。任何倍率都不允许读取“上次已乘后的当前值”作为 base。

---

## 7. 代码与资产变更计划

> 最终文件列表需在创建 GitHub Issue 后由实施 Agent基于实际 diff 再核对；以下为预期范围。

### 7.1 新增代码

- `Assets/Scripts/Systems/Progression/Passives/SinType.cs`
- `Assets/Scripts/Systems/Progression/Passives/PossessionImprintConfig.cs`
- `Assets/Scripts/Systems/Progression/Passives/PossessionImprintManager.cs`
- `Assets/Scripts/Systems/Progression/Passives/PossessionImprintMath.cs`
- `Assets/Scripts/Combat/Effects/CharmController.cs`
- `Assets/Scripts/UI/HUD/PossessionImprintHUD.cs`
- `Assets/Scripts/UI/HUD/PossessionImprintIcon.cs`
- `Assets/Scripts/UI/HUD/PossessionImprintTooltip.cs`
- `Assets/Scripts/UI/HUD/PossessionImprintTutorialPrompt.cs`
- `Assets/Scripts/Levels/Spawning/RunSpawnDirector.cs`
- `Assets/Scripts/Levels/Spawning/SpawnRequest.cs`
- `Assets/Scripts/Levels/Spawning/MonsterSpawnDifficulty.cs`
- `Assets/Scripts/Combat/Boss/BossSevenfoldActor.cs`
- `Assets/Scripts/Combat/Boss/BossAffixAssimilator.cs`
- `Assets/Scripts/Combat/Boss/BossSpatialDistortionController.cs`
- `Assets/Scripts/AI/Boss/BossCombatBrain.cs`
- `Assets/Scripts/AI/Boss/BossAbilityProfile.cs`
- `Assets/Scripts/AI/Boss/BossTeleportPlanner.cs`

### 7.2 修改代码

- `PossessionManager.cs`：带 reason/transactionId 的单次提交事件；罪印与 Bullet Time 接口。
- `MonsterActor.cs`：SinType、可夺舍开关、视觉根、出生倍率、Fatal attribution、Boss consume 路径。
- `EnemyAbility.cs`：外部冷却倍率、activationId、精确单能力触发入口。
- `CombatAbilityComponent.cs` / 伤害路径：统一玩家出伤、敌人出生伤害、最终入伤修正。
- `PlayerPassiveManager.cs` / `EnemyPassiveBuff.cs`：旧实验系统迁移或停用，防双重加成。
- `MonsterSpawner.cs`：接收 SpawnRequest，返回成功/失败与合法位置。
- `MonsterPool.cs`：严格恢复 Authored base 后应用 Spawn Snapshot。
- `WaveManager.cs`：Periodic origin、Choice 前清理 Echo、最后波等待 8:00 Boss。
- `EnemyAbility_GluttonyDevour.cs`：Boss 分支走词条同化，普通暴食维持现有复制 Skill 行为。
- `EliteBuildCarrier.cs`：提供只读 active effectId 枚举接口。
- `SaveData.cs`、`RunSession.cs`、`SaveCoordinator.cs`、`SaveMigrator.cs`：Schema v3 和新字段。
- `UIManager`/现有 HUD 绑定点：挂接罪印 HUD 与 Boss HP。

### 7.3 新增 Unity 资产（只能通过 Unity Editor/MCP 创建与保存）

- `Assets/Prefabs/Monster/Boss_Sevenfold_Convergence_new.prefab`
- `Assets/Prefabs/UI/HUD/PossessionImprintHUD.prefab`
- `Assets/Prefabs/UI/HUD/PossessionImprintTooltip.prefab`
- `Assets/Prefabs/UI/HUD/PossessionImprintTutorialPrompt.prefab`
- `Assets/Materials/Boss/M_BossSevenfoldDistortion.mat`
- `Assets/Shaders/BossSevenfoldDistortion.shader`
- 七罪罪印图标资源；首版可用程序化符号/现有纹样，禁止用白色占位块交付。
- Boss Ability Profile / Imprint Config ScriptableObject。

### 7.4 Scene / Prefab 修改

- 先用 Unity 依赖查询确认 Combat Scene 与 HUD Prefab。
- 通过 Unity MCP 打开 Prefab Stage、创建/挂组件/保存；禁止文本编辑 `.unity/.prefab/.asset` YAML。
- 只在确定不是 Shared Original 或得到 Owner 确认后修改共享 HUD。优先创建独立 HUD 子 Prefab并在单一场景入口实例化。
- 不触碰 `Packages/manifest.json`、`ProjectSettings/**`，不 Reimport All。

---

## 8. 实施拆解

### Phase A：基础身份、状态与存档

1. 新增 SinType、罪印公式和数据配置。
2. 为七个 `*_new` Prefab 配 SinType 与视觉根。
3. 改造 Possession 事务事件，确保初始/玩家/接力/读档/调试来源明确。
4. 实现 Run 状态、Schema v3 与迁移。
5. 单元测试层数、幂等、贪婪进度与公式。

### Phase B：运行时效果、UI 与提示

1. 接入冷却、出伤、HP/视觉 Scale、Bullet Time、控制、血条消耗减免。
2. 删除/停用旧四项永久被动的重复路径。
3. 创建七图标 HUD、Tooltip、首次提示。
4. 验证每种身体、换体、死亡接力、读档和对象池。

### Phase C：生成 Director

1. 建立 ActiveCombatSeconds 和 SpawnRequest。
2. 接入 PeriodicPressure、目标活怪数与现有 Wave 表。
3. 接入 Fatal attribution、Echo 限流、远处合法落点和过期。
4. 接入 30 秒快照与池化重置。
5. Choice/Wave/Boss 阶段清理与暂停。

### Phase D：Boss 代码

1. Boss Actor、不可夺舍、Boss 出场接管和全场吞噬。
2. 14 技能挂接及前置状态适配。
3. 距离/LOS/状态/历史选技和系统瞬移。
4. Gluttony 词条同化。
5. Boss 小怪供给与胜利流。

### Phase E：Boss 资产与表现

1. Unity MCP 创建独立 Prefab 和七个 VisualPart。
2. 创建专用材质/Shader，MPB 驱动浮空、移动折射和瞬移。
3. 配技能 Telegraph、Boss HP、出场吞噬 VFX。
4. 逐个 Camera/Game View 截图检查剪影、遮挡、材质和 HUD。

### Phase F：验证与交付

1. EditMode + PlayMode 自动测试。
2. Unity 编译、Console 零新增 Error、关键 Warning 说明。
3. 8 分钟完整对局验证与性能采样。
4. `git diff` 核对 Scope；记录 Shared Original 未修改证明。
5. 人工审核 Boss 拼接美术质量、技能可读性、手感和平衡。

---

## 9. 测试计划与验收标准

### 9.1 EditMode

- 七种罪印公式在 0/1/10/20/100/101 层下正确，统一由 `MaxStacks=100` 截断且不越各自效果上限。
- 怠惰在附身自然衰减、Attack/Skill/Movement 与持续型 Ability HP cost 上均使用同一消耗倍率；不改变外部承伤。
- 每个 possession transaction 只结算一次；LoadRestore/Debug 不加层。
- 贪婪进度使用旧层数、满 1 后只奖 1 层并正确保留余数。
- Echo 滚动窗口 10 秒最多 4 个成功生成；超额不积压。
- 30 秒 Tier 边界：29.999/30.000/479.999/480.000。
- Boss 候选过滤：近战在距离外永不入选；缺 Mark/Anchor/资源永不入选。
- 反重复：同技能不能进入最近 2 次，同 family 不能连续。
- Affix filter 只返回当前身体 E/Special 的 active effectId。

### 9.2 PlayMode

- 七个 Prefab 每次真实夺舍对应层 `+1`，起始傲慢正确；死亡接力正确。
- 首次提示 Profile 仅一次；关提示仍涨层。
- HUD 始终有 7 个非白块图标，0 层暗显，数字和 Tooltip 正确。
- 暴食视觉放大不改变 Collider/NavMeshAgent，不在池化后累乘。
- 色欲多段/DoT 一次 activation 对同目标只抽一次，Charm 可正常恢复。
- Periodic 能维持目标压力；Echo 远离死亡点和玩家；Choice 时无残留攻击者。
- 新出生怪在每个 30 秒 Tier 取得正确 HP/伤害；旧怪不跳变。
- Pause/CardChoice 不推进 8 分钟计时；Bullet Time 推进。
- 8:00 只生成一个 Boss；全场怪物被吞噬，玩家身体不被吞，3 秒内补可夺舍小怪。
- Boss 近/中/远距离分别使用能命中的技能，五次施法序列不单一；近战不空放远处玩家。
- Boss 瞬移合法、不会进墙/玩家体内；材质 Out/In 和伤害窗口同步。
- Boss Devour 命中后只给其已有同源 Special 添加玩家词条，不新增技能 Component；重复词条不叠。
- Boss 死亡解除 Chunk Pin、清理小怪/残影/MPB，进入 Result。

### 9.3 性能与稳定性

- Boss AI 不在 Update 做 `FindObjectsOfType`、LINQ 分配或全场物理扫描。
- 候选数组、Registry 快照和 MPB 复用；吞噬时允许一次性受控快照。
- 目标 60 FPS；Boss + 5 小怪下 CPU AI/技能决策无明显尖峰。
- 连续三次 New Run / Continue / Restart 后无重复 Singleton、重复事件订阅或残留层数。

### 9.4 Definition of Done

- 代码编译通过，自动测试通过，无新增 Console Error。
- 七罪罪印、HUD、提示、双源生怪、30 秒成长、8:00 Boss 全部可在 Game View 复现。
- Boss 确实由七个源 Prefab 的可辨识部位组成，并使用全部 14 个非 Movement 槽技能。
- Boss Devour 词条同化符合“只得词条、不复制技能”。
- 源七怪 Prefab 和共享材质 `git diff` 为零。
- Packages、ProjectSettings、`.vibe/rules.md`、`.vibe/doc/**` 无改动。
- 人工审核点已列出，最终 Merge 仍由人类执行。

---

## 10. 风险与人工审核点

| 风险 | 等级 | 缓解 / 审核点 |
|---|---|---|
| 七个 Skinned Mesh 拼接后骨架/材质数量造成 Draw Call 增长 | 高 | 先保留最小骨架；禁用不可见 Renderer；Profiler 对比；美术最终审核 |
| Boss 同时挂 14 能力，而现有 Slot 脉冲会一次触发多个 | 高 | 必须使用精确能力触发接口和独立 BossCombatBrain |
| 旧 PlayerPassiveManager 与新罪印双重加成 | 高 | 迁移后写 PlayMode 断言；旧 Prefab 组件仅兼容身份，不再给数值 |
| Echo 与 Wave Clear/Choice 冲突 | 高 | Echo 独立 origin，不计目标；阶段切换统一消费/淡出 |
| SaveData 字段漂移导致旧档异常 | 高 | Schema v3 + v2→v3 迁移；默认层数/时钟为 0 |
| Boss 吞噬清空尸体使夺舍循环暂时中断 | 中 | 3 秒内补 3 小怪，之后至少维持 3 个身体供给 |
| 色欲 Charm 触发敌我伤害边界错误 | 中 | 明确 faction override、伤害过滤、超时恢复和目标切换测试 |
| 体型增长影响物理与导航 | 中 | 仅缩放视觉根，不缩放 Actor 根 |
| 时间倍率在 8 分钟过强 | 中 | HP/伤害分别封顶 3.0/2.2；视完整跑局调参 |
| Boss 与七罪关系仍是 Open Decision | 中 | 只使用临时系统称谓，不更新 Canonical |

人工必须审核：Boss 拼接审美、Telegraph 可读性、浮空/瞬移眩目程度、整体难度、8:00 节奏、色欲控制是否过强、词条同化的可理解反馈。

---

## 11. Shared Original 影响

- 共享源：七个 `Assets/Prefabs/Monster/*_new*.prefab`、其 Material、Mesh、Texture、Ability 子资产。
- 需求是局部 Boss 组合，不是全局修改。
- 采用独立 Prefab + 只读嵌套/视觉子树，源资产不 Apply Override。
- 实施前需用 Unity Dependency/API 确认源引用；实施后用 `git diff -- <seven prefabs> <shared materials>` 证明无改动。
- 若 Unity MCP 无法在不 Apply Override 的前提下拆取某个部位，降级为嵌套完整视觉组并禁用非选定 Renderer；不得直接改源 Prefab。

---

## 12. 实施启动门槛

当前已完成只读架构研究、GitHub Issue 与 Detailed Contract。启动写入型 SubAgent 前必须同时满足：

1. Open GitHub Issue #7 已建立，包含 Goal、Scope、Acceptance Criteria、Owner、Executor。
2. Issue 已链接本 Detailed Contract 的仓库路径。
3. 仍需建立合规分支 `work/ENG-POSS-001-sevenfold-possession`，不能直接在当前 `dev_randompei` 上实施。
4. 仍需隔离当前已有脏工作树；不得覆盖用户的 `Packages/manifest.json`、材质、插件和其他未提交改动。
5. 实施 Agent 每次启动重新读取 `AGENTS.md` 与 `.vibe/rules.md`，按需加载相关 Canonical 和 shared skills。

门槛未满足时，不应启动一个声称会落地工程的 Agent，因为它第一步就必须按项目规则停止。
