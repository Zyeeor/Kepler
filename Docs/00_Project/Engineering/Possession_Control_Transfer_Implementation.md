# 附身与控制权切换架构技术设计与实现文档

> **文档类型**：技术设计 / 实现说明（Implementation）
>
> **状态**：已完成核心链路，当前实现以单机运行时为目标
>
> **整理日期**：2026-08-28
>
> **归属**：`Docs/00_Project/Engineering/`（工程实现文档，随仓库分发）
>
> **配对设计文档**：`Refactor_Plan_Actor_Controller.md`（设计输出，不含代码实施，位于本地 `.docs` 文档包，未随仓库分发）→ 本文为其实现落地
>
> **事实基线**：本文按当前源码整理，重点描述已经落地的 Actor、Controller、附身状态机、目标校验、变换/碰撞处理、战斗绑定与对象池边界；不把网络同步、ECS 化和完整回放系统等预留方向写成已实现能力。

---

## 1. 设计目标

### 1.1 用户可见需求

玩家在游戏中有两种主要存在形态：

1. **自由灵魂态**：玩家直接控制 `SoulActor`，可以移动、攻击，并寻找可附身的躯体；
2. **附身躯体态**：玩家接管一只 `MonsterActor`，使用该怪物的移动、技能、属性和视觉表现；
3. 玩家可以在合适时机释放当前躯体、切换到另一具躯体，或在当前躯体死亡后返回灵魂态；
4. 附身期间灵魂、身体、相机、HUD、战斗阵营和输入不能出现“双重生效”或状态不同步；
5. 被附身的身体不能在仍被灵魂挂载时被对象池回收，避免灵魂被连带带入错误场景。

### 1.2 设计约束

- 不为“玩家灵魂”“玩家怪物”“AI 怪物”分别复制完整角色类；
- 输入采集与角色执行解耦，AI 和玩家都输出相同的控制意图；
- 附身必须是可逆操作，且切换中间态可被取消；
- 目标在飞行过程中可能被其他逻辑销毁、进入淡出、被其他事务预定，必须二次校验；
- 游戏暂停、子弹时间、GameOver 和场景卸载不能留下残留 Controller、事件订阅或父子层级；
- 高热路径尽量避免临时对象分配，控制命令使用值类型。

### 1.3 核心设计结论

本系统将“玩家是谁”与“谁执行玩家输入”拆开：

```text
PlayerController = 唯一玩家输入源
Actor            = 被控制的执行体
IController      = 输入/AI 意图生产器
PossessionManager= 控制权、状态、资源和表现切换编排器
```

附身的核心不是创建一个新的“玩家怪物”类，而是：

```csharp
monster.SetController(PlayerController.Instance);
```

释放时再把身体 Controller 切回 `NullController`，由身体生命周期决定后续回收；灵魂重新绑定 `PlayerController`。

---

## 2. 系统边界与模块职责

### 2.1 模块关系

```text
                         ┌──────────────────────────┐
                         │       PlayerController   │
                         │  Input → ControlCommand  │
                         └─────────────┬────────────┘
                                       │
                                       │ SetController
                                       ▼
┌──────────────┐            ┌──────────────────────────┐
│  SoulActor   │◄───────────│          Actor           │──────────────┐
│自由灵魂/抑制│            │ Controller + Tick loop  │              │
└──────┬───────┘            └─────────────┬────────────┘              │
       │                                  │                           │
       │ Attach/Detach                    │ default Controller        │
       ▼                                  ▼                           ▼
┌──────────────┐                 ┌───────────────┐           ┌────────────────┐
│ Possession   │                 │ MonsterActor  │           │  AIController   │
│ Manager      │                 │ AI/被附身身体 │           │ AI → Command    │
└──────┬───────┘                 └───────────────┘           └────────────────┘
       │
       ├─ PossessionBehavior：射线目标解析与输入策略
       ├─ CameraDirector：相机目标切换
       ├─ PossessionHUD：当前身体信息表现
       ├─ PlayerHealth：灵魂/当前身体生命绑定
       ├─ GameManager：Soul / Possessed 状态切换
       └─ MonsterPool：身体回收、复用和附身安全检查
```

### 2.2 核心类职责

| 类 | 职责 | 不负责的内容 |
|---|---|---|
| `Actor` | 统一控制入口、命令消费、移动基类、碰撞滑行 | 不决定玩家输入来源、不决定附身业务 |
| `SoulActor` | 灵魂态移动/攻击、附身期间抑制、父子挂载与脱离 | 不直接选择附身目标、不编排全局状态 |
| `MonsterActor` | 怪物状态、AI/玩家控制下的执行、倒地/尸体/淡出生命周期 | 不决定附身事务时序 |
| `PlayerController` | 采集输入并生成 `ControlCommand` | 不执行移动、不直接修改怪物状态 |
| `AIController` | 生成 AI 控制意图 | 不直接接管相机或 HUD |
| `PossessionBehavior` | 射线命中、目标过滤、辅助选尸体 | 不维护附身状态机 |
| `PossessionManager` | 飞行、预定、提交、释放、换身、死亡中继、事件编排 | 不实现具体角色移动算法 |
| `CombatAbilityComponent` | Actor 的能力、Tag、Effect 门禁与状态 | 不决定 Controller 归属 |
| `MonsterPool` | 怪物根对象生成、重置、回收和跨场景池 | 不允许绕过附身状态直接回收 |
| `CameraDirector` | 跟随目标切换 | 不决定目标是否允许附身 |
| `PossessionHUD` / `PlayerHealth` | 当前身体 HUD 与生命数据绑定 | 不决定身体生命周期 |

### 2.3 单一责任的关键边界

最重要的边界是：

- `PossessionBehavior` 解决“玩家这次点击想附身谁”；
- `PossessionManager` 解决“这次附身事务能否开始、如何过渡、如何提交/回滚”；
- `MonsterActor` 解决“身体在 AI、附身、倒地、淡出状态下如何执行”；
- `Actor` 解决“当前 Controller 产生的命令如何进入角色执行层”。

这样可以避免把射线、状态切换、身体动画、相机和输入全部堆在一个 MonoBehaviour 中。

---

## 3. Actor 与 Controller 抽象

### 3.1 `Actor`：执行层基类

文件：

```text
Assets/Scripts/Core/Actors/Actor.cs
```

`Actor` 持有当前 Controller：

```csharp
public IController Controller { get; private set; }

public virtual void SetController(IController next)
{
    if (next == Controller) return;
    Controller?.OnDetached();
    Controller = next ?? NullController.Instance;
    Controller.OnAttached(this);
    OnControllerChanged?.Invoke(this);
}
```

`SetController` 是整个架构的控制权切换单入口，具备三个作用：

1. 先让旧控制器执行 `OnDetached`，清理目标、计时器和残留决策；
2. 将空引用归一化为 `NullController`，避免 Actor 在过渡期间出现 null 分支；
3. 新控制器执行 `OnAttached`，重新绑定宿主并初始化控制器状态。

业务系统不应该直接修改 `Actor.Controller`，也不应该在角色内部复制一套“当前是不是玩家”的独立切换协议。

### 3.2 `IController`：意图生产器

文件：

```text
Assets/Scripts/Core/Control/IController.cs
```

接口只有三个生命周期入口：

```csharp
void OnAttached(Actor owner);
void Tick(in ActorContext ctx, ref ControlCommand cmd);
void OnDetached();
```

实现包括：

- `PlayerController`：读取玩家输入；
- `AIController`：读取行为树/黑板/寻路结果；
- `NullController`：无操作控制器，用于灵魂被抑制、尸体、池内对象和过渡态。

Controller 只产出控制意图，不直接执行 Transform、相机、HUD 或伤害。

### 3.3 `ControlCommand`：统一命令协议

文件：

```text
Assets/Scripts/Core/Control/ControlCommand.cs
```

命令是 `struct`，包含：

```csharp
public Vector3 MoveDirection;
public Vector3 AimPoint;
public bool HasMove;
public bool HasAim;
public CommandButtons Pressed;
```

按钮使用 `[Flags] enum CommandButtons`：

```text
Basic / Skill1 / Skill2 / Skill3 / Interact / Possess / Release / Mobility
```

实际玩家绑定由 `GameInputBindings` 读取；命令中的 `Pressed` 是当前帧的边沿触发，不是持续状态。

使用值类型的原因：

- 每帧命令不需要 `new`；
- Controller 通过 `ref` 写入命令；
- Actor 拿到的是值语义，不会被其他 Controller 事后篡改；
- 玩家与 AI 可以复用完全相同的执行层。

### 3.4 Actor 的统一帧循环

```text
Actor.Update
  ├─ Controller.Tick(ctx, ref pendingCmd)
  ├─ ExecuteButtons(pendingCmd)
  └─ 若当前是 PlayerController → ExecuteMovement(pendingCmd)

Actor.FixedUpdate
  └─ 若当前不是 PlayerController → ExecuteMovement(pendingCmd)
```

这么分配的原因：

- 玩家移动在 `Update` 消费，能够在子弹时间或暂停相关表现中使用非缩放时间；
- AI 移动在 `FixedUpdate` 消费，保留与物理步进一致的行为；
- `ExecuteButtons` 由 `SoulActor` 和 `MonsterActor` 各自实现，技能入口可以不同，但输入协议一致。

`ActorContext` 提供当前宿主、目标和时间信息，`MonsterActor` 缓存追击目标，避免每帧通过 Tag 查找玩家。

---

## 4. 三层状态模型

附身系统不是一个 bool，而是三个相互正交的状态层。

### 4.1 `PossessionManager.SwitchState`

文件：

```text
Assets/Scripts/Combat/Possession/PossessionManager.cs
```

```text
Idle        当前为自由灵魂态，可以请求附身
Flying      灵魂正在飞向已预定目标，尚未提交控制权
Possessing  玩家已控制身体
Releasing   正在清理身体/灵魂绑定，完成后回到 Idle
```

它表达的是**全局事务阶段**。

### 4.2 `MonsterActor.BodyState`

```text
Active      AI 或玩家控制中的正常身体
Hit         短暂受击表现状态
Downed      倒地，可在窗口内附身
Fading      释放/死亡后的淡出阶段
Despawned   已完成生命周期，等待/完成回池
```

它表达的是**身体生命周期**。

### 4.3 `MonsterActor.ControlState`

```text
AI          当前 Controller 不是 PlayerController
Possessed   当前由玩家控制
```

它表达的是**当前控制权**。

### 4.4 可附身条件

`MonsterActor.CanBePossessed` 的最终判断包含：

```text
isPossessable
&& Body == Downed
&& !isPossessed
&& !isPossessionReserved
&& 非受限 Boss 身体条件
&& Time.time < possessionWindowEndsAt
```

飞行提交阶段使用另一个条件：

```text
CanCompleteReservedPossession
= Body == Downed
  && !isPossessed
  && isPossessionReserved
```

也就是说，目标一旦被当前附身事务预定，就不会因为普通附身窗口的时间检查导致飞行中途失效；但身体仍必须保持倒地且未被其他流程接管。

---

## 5. 玩家输入与角色执行

### 5.1 `PlayerController` 的职责

文件：

```text
Assets/Scripts/Core/Control/PlayerController.cs
```

`PlayerController` 是全局唯一输入源：

- `Tick` 读取 WASD，生成世界空间 XZ 移动方向；
- `GameInputBindings.GetDown` 读取按键边沿；
- 通过当前控制目标的 Y 平面计算鼠标瞄准点；
- 通过 `OnCommandProduced` 广播带按钮的命令，供教学/遥测等系统订阅；
- `SetGameplayInputBlocked` 为选卡、教学、系统过渡提供全局输入门。

它不关心当前是灵魂还是怪物：

```text
当前控制 SoulActor  → SoulActor.ExecuteMovement / ExecuteButtons
当前控制 MonsterActor→ MonsterActor.ExecuteMovement / ExecuteButtons
```

### 5.2 灵魂态执行

`SoulActor` 的默认 Controller 是 `PlayerController.Instance`。

灵魂态：

- 移动使用加速度/减速度平滑；
- 静止时面向鼠标；
- 通过 `SlideMove` 做 SphereCast 预检测和沿墙滑行；
- 左键触发灵魂基础攻击；
- 附身请求走 `PossessionManager.TryRequestPossessFromInput`；
- 灵魂 Y 位置由 `hoverHeight` 维持，避免贴地。

### 5.3 身体态执行

`MonsterActor` 默认创建/获取 `AIController`。

- AI 态：`AIController` 产出追击、攻击、走位和技能命令；
- 被附身后：Controller 变为 `PlayerController`，同一 `MonsterActor` 改走玩家执行分支；
- `MonsterActor` 仍保留自己的能力、双属性和身体生命周期；
- 释放后 Controller 变为 `NullController`，再由 `OnUnpossessed` 和 `BeginDisappearing` 决定清理/回收。

这是“同一身体、不同控制源”，而不是创建一个新的玩家版身体对象。

---

## 6. 目标解析与附身请求

### 6.1 请求入口

```text
SoulActor.ExecuteButtons
  → PossessionManager.TryRequestPossessFromInput
  → PossessionManager.TryRequestPossess
  → PossessionBehavior.TryBegin
```

`TryRequestPossessFromInput` 使用 `lastPossessionInputFrame` 防止同一帧重复处理输入。

### 6.2 `PossessionBehavior.TryBegin`

文件：

```text
Assets/Scripts/Combat/Possession/PossessionBehavior.cs
```

目标解析顺序：

1. 调用 `PossessionManager.CanStartPossession`；
2. 检查 GameOver、Flying/Releasing、冷却；
3. 从鼠标射线 `Physics.RaycastAll` 获取命中；
4. 按命中距离排序；
5. 从 Collider 父级查找 `MonsterActor`；
6. 调用 `ValidatePossessionTarget`；
7. 第一个有效尸体开始飞行；
8. 如果射线没有直接命中，执行半径 `1.5m` 的 ray assist，从射线附近寻找可附身尸体。

射线解析层只返回目标，不修改目标状态。这样可以把“瞄准策略”与“事务提交”分离。

### 6.3 目标校验

`PossessionManager.ValidatePossessionTarget` 统一校验 `MonsterActor.CanBePossessed`，避免不同入口产生不同规则：

- 鼠标射线；
- ray assist；
- 调试/教程直接赋值；
- Boss 特殊切换入口。

校验失败会输出带身体状态的调试信息，包含：

```text
Body / downed / possessed / reserved / possession window / active
```

---

## 7. 附身事务完整流程

### 7.1 阶段 A：预检查与预定

```text
Idle
  │
  ├─ CanStartPossession
  ├─ ValidatePossessionTarget
  ├─ SoulActor 是否存在
  └─ target.TryReserveForPossession
          │
          ▼
       reservedBody = target
       State = Flying
```

`TryReserveForPossession` 是并发保护点：

```csharp
if (!CanBePossessed) return false;
isPossessionReserved = true;
return true;
```

预定成功后，其他附身请求看到 `isPossessionReserved` 会失败，不会出现两个灵魂同时飞向同一尸体。

### 7.2 阶段 B：灵魂飞行

`BeginPossessionFlight`：

1. 若当前已经附身，先停止子弹时间；
2. 通过 `DetachCurrentBodyForSwitch` 清理旧身体；
3. 将全局状态切换为 `Flying`；
4. `SoulActor.SetPossessionFlight(true)`，禁用灵魂控制/碰撞；
5. 启动 `FlyAndCommitRoutine`。

飞行使用：

```csharp
Vector3.MoveTowards(..., flySpeed * Time.unscaledDeltaTime)
```

因此飞行表现不依赖 `Time.timeScale`，与附身时的慢动作/子弹时间相容。

飞行循环每帧继续检查 `target.CanCompleteReservedPossession`。若目标已销毁、状态已改变或预定失效：

```text
取消预定
清理 reservedBody
CancelFlightToSoul
恢复灵魂控制
保持 State = Idle
```

### 7.3 阶段 C：提交控制权

`CommitPossession` 是唯一的控制权提交点，顺序为：

```text
1. reservedBody 清空，CurrentBody = target
2. State = Possessing，记录 possessStartTime
3. SoulActor.SetPossessionFlight(false)
4. SoulActor.SetSuppressed(true)
5. 灵魂挂到目标 soulAnchorPoint（若存在）
6. 灵魂战斗 Tag 加入 State.Possession.Active / State.Soul.Suppressed
7. 目标战斗 Tag 加入 State.Possession.Active / State.Possession.Controlled
8. target.OnPossessed()
9. target.SetController(PlayerController.Instance)
10. CameraDirector.Target = target.transform
11. PossessionHUD.Show(target)
12. PlayerHealth.BindActor(target)
13. GameManager 切换到 Possessed
14. 触发 OnPossessionStarted
15. 触发 PossessionCommitted（带 reason + transactionId）
16. 触发 BulletTime
```

其中第 8、9 步是“身体表现/属性初始化”和“控制权转移”的核心；第 10～15 步是跨系统同步。

### 7.4 事务幂等性

`PossessionCommitted` 附带递增 `possessionTransactionId`：

```csharp
public event Action<MonsterActor, PossessionGrantReason, long> PossessionCommitted;
```

Run-level 系统可以把 transaction id 作为幂等键，避免同一次附身重复发放构筑/印记/奖励。

---

## 8. 释放、换身与身体死亡

### 8.1 主动释放

`RequestRelease` 只有在 `State == Possessing` 时生效，普通模式还会检查：

- `minPossessTime`：最短附身时间；
- `possessCooldown`：释放后再次附身的冷却。

通过后进入 `CommitRelease`：

```text
State = Releasing
停止 BulletTime
移除双方附身 Tag
oldBody.SetController(NullController)
oldBody.OnUnpossessed()
oldBody.BeginDisappearing()
SoulActor.DetachFromPossessionAnchor()
SoulActor.PlaceInFreeSoulForm(oldBody.position)
SoulActor.SetPossessionFlight(false)
SoulActor.SetSuppressed(false)
CameraDirector.Target = soul
PossessionHUD.Hide
PlayerHealth.UnbindActor
GameManager → Soul
State = Idle
设置冷却
触发 OnPossessionEnded / OnPossessionEndedEx(VoluntaryRelease)
```

### 8.2 附身换身

换身路径不是“保持旧身体不变，再瞬间切新身体”，而是：

```text
旧身体解绑与进入淡出
  → 灵魂暂时处于 Flying
  → 新目标提交
```

`DetachCurrentBodyForSwitch` 会：

- 清理旧身体的 Combat Tag；
- 设为 `NullController`；
- 调用 `OnUnpossessed`；
- Boss 预留身体回到特殊预留状态；
- 普通身体进入 `BeginDisappearing`；
- 灵魂解除旧锚点并恢复自由态；
- HUD、Health、Camera 和 GameManager 回到 Soul；
- 新的飞行事务继续执行。

### 8.3 身体死亡

`NotifyBodyDied` 只在当前状态为 `Possessing` 且有 `CurrentBody` 时处理：

1. 记录死亡身体；
2. 设置下一次附身原因为 `DeathRelay`；
3. 使用 `CommitRelease(..., PossessionEndReason.BodyDied)`；
4. 触发 `OnBodyDiedWhilePossessing`。

身体死亡与主动释放共用清理骨架，但通过 `PossessionEndReason` 区分教学、统计和后续逻辑。

### 8.4 GameOver 与组件禁用

`OnGameOver`：

- 停止飞行协程；
- 停止子弹时间；
- Possessing 状态走系统重置释放；
- Flying 状态取消预定并恢复灵魂；
- 标记 `handlingGameOver`，避免状态回调再次启动常规流程。

`OnDisable` 额外处理 Flying 状态：如果管理器在飞行中被禁用，必须取消协程、释放目标预定、恢复灵魂控制并回到 Idle。

---

## 9. 灵魂挂载、Scale 与 Collider 一致性

### 9.1 自由态与抑制态

`SoulActor.SetSuppressed(true)` 做的不是简单隐藏：

- Controller 切换到 `NullController`；
- Tag 从 `Player` 改为 `Soul`；
- 清空旧的 `pendingCmd`；
- 清零移动惯性；
- 关闭所有子层级 Collider；
- 保持 Renderer 可见，使灵魂可以作为附身表现跟随身体。

恢复时：

- Tag 恢复 `Player`；
- Controller 绑定 `PlayerController.Instance`；
- 所有灵魂 Collider 恢复；
- 重新施加 `hoverHeight`。

这样可避免附身期间灵魂仍被 AoE 命中、仍消费旧移动指令，或与身体同时被识别成 Player。

### 9.2 锚点挂载

`AttachToPossessionAnchor` 使用：

```csharp
transform.SetParent(anchor, true);
transform.localPosition = Vector3.zero;
transform.localRotation = Quaternion.identity;
```

`worldPositionStays=true` 的目的是保持世界 Scale。附身前记录 `worldScaleBeforePossession`，附身期间每帧按父链 `lossyScale` 反算 `localScale`，避免怪物缩放变化传递给灵魂。

### 9.3 脱离顺序

`DetachFromPossessionAnchor` 有一个重要的层级顺序：

```text
先 SetParent(null, true)
再恢复世界 Scale
再视情况 MoveGameObjectToScene
最后恢复原父级
```

不能在灵魂仍是锚点子物体时直接调用 `MoveGameObjectToScene`，因为 Unity 要求移动的 GameObject 是场景根节点。这个顺序还处理了“身体被错误回池后灵魂留在 DontDestroyOnLoad”的兜底情况。

### 9.4 物理移动

`Actor.SlideMove` 使用固定容量的 `Collider[32]` 缓冲：

1. 起点 `OverlapSphereNonAlloc` 脱困；
2. 高速位移按 `maxStep` 分段；
3. 每段 `SphereCast` 预检测；
4. 命中后沿法线投影剩余位移，实现沿墙滑行；
5. 最终 `CheckSphere` 防止滑入墙角；
6. XZ 位移交给子类，Y 高度由 Soul/Monster 自己维护。

这套逻辑被灵魂和身体的移动实现复用，但速度、朝向和时间来源由子类决定。

---

## 10. 战斗、属性与 Tag 接入

### 10.1 双属性块

`MonsterActor` 同时持有：

```text
enemyStats      AI 控制时的属性
possessedStats  被玩家接管时的属性
```

`OnPossessed` 负责把身体切换到被附身语义，`OnUnpossessed` 负责清理附身状态并恢复 AI/尸体生命周期。

### 10.2 CombatAbilityComponent

`Actor.Awake` 自动确保宿主拥有 `CombatAbilityComponent`，并加入基础 `Actor` Tag。

附身过程中由 `PossessionManager` 添加临时 Tag：

```text
灵魂：State.Possession.Active / State.Soul.Suppressed
身体：State.Possession.Active / State.Possession.Controlled
```

能力系统可以据此控制：

- 能力是否可触发；
- 是否允许移动；
- 阵营/命中判定；
- 其他系统是否将当前对象视为玩家控制体。

### 10.3 附身技能烧血与延迟死亡

被附身身体使用技能时，`MonsterActor` 可能支付 HP 代价。若代价将耐久扣到 0，不立即中断当前技能，而是进入：

```text
IsAbilityCostDeathPending = true
```

宽限窗口用于覆盖延迟命中、蓄力/持续技能的最后判定：

- 当前技能继续完成一次判断；
- 后续技能不能重新触发；
- 被动附身衰减不会抢先结束身体；
- 窗口结束后统一进入死亡结算。

这是身体生命周期与能力执行时序的一个典型交叉点。

### 10.4 PlayerHealth 与 HUD

提交附身后：

```text
PlayerHealth.BindActor(target)
PossessionHUD.Show(target)
CameraDirector.Target = target.transform
GameManager.State = Possessed
```

释放或身体死亡后反向解绑，生命显示恢复为灵魂生命池。这样生命条和当前控制体不是通过查找 Tag 临时推断，而是通过明确的绑定关系同步。

---

## 11. 对象池与生命周期安全

文件：

```text
Assets/Scripts/Levels/Spawning/MonsterPool.cs
Assets/Scripts/Combat/Runtime/PooledObject.cs
```

### 11.1 生成

`MonsterPool.Spawn`：

1. 按 prefab 查找可用队列；
2. 没有可用对象时 Instantiate；
3. 保持对象 inactive，先完成 `ResetForSpawn`；
4. 设置世界位置和旋转；
5. 激活对象；
6. 将当前 CardManager 的解锁能力补写到新/复用对象。

### 11.2 回收

`MonsterPool.Return` 的安全门：

- 未击败的 Boss 不回收；
- `monster.isPossessed` 为 true 时拒绝回收；
- 先执行 `ResetForPool`；
- 挂到池根节点并设 inactive；
- 入对应 prefab 队列。

拒绝直接回收被附身身体是关键保护：灵魂可能仍作为其子节点挂在 `soulAnchorPoint` 下，直接回池会把灵魂一起带到池场景/常驻场景，导致脱离时场景归属和相机目标异常。

### 11.3 重置协议

`MonsterActor.ResetForSpawn` / `ResetForPool` 负责清理：

- `isDowned`、`isPossessed`、`isWeakened`；
- Controller；
- Ability runtime state / telegraph / movement lock；
- 协程与尸体生命周期；
- 速度与残留 `ControlCommand`；
- Boss 预留态；
- Collider、Animator、Health UI、VFX 状态；
- 附身烧血延迟死亡状态。

对象池的核心不是 Queue，而是“归还前必须完成的状态重置协议”。

---

## 12. 事件、可观测性与跨系统一致性

### 12.1 `PossessionManager` 事件

```csharp
OnPossessionStarted(MonsterActor body)
PossessionCommitted(MonsterActor body, PossessionGrantReason reason, long transactionId)
OnPossessionEnded()
OnPossessionEndedEx(PossessionEndReason reason)
OnBodyDiedWhilePossessing(MonsterActor body)
```

建议订阅关系：

| 事件 | 典型订阅者 |
|---|---|
| `OnPossessionStarted` | HUD、引导、神龛使用状态、教学 |
| `PossessionCommitted` | 对局统计、印记/构筑、奖励、幂等消费系统 |
| `OnPossessionEndedEx` | 教学、统计、死亡中继、引导状态 |
| `OnBodyDiedWhilePossessing` | 死亡 relay、战斗结算、提示系统 |

### 12.2 日志分层

当前代码使用以下日志前缀定位事务：

```text
[PossessionInput]       输入和目标请求
[PossessionTargeting]   射线命中与目标筛选
[Possession]            状态机、飞行、提交、释放
[MonsterState]          身体倒地、附身、淡出、回池生命周期
[PlayerInput]           全局输入门
[HpCost]                附身技能烧血与延迟死亡
[MonsterPool]           池安全检查和回收拒绝
```

通过目标状态字符串和事务日志，可以定位“输入没有到达”“目标不合法”“飞行被取消”“提交后表现不同步”等不同类别问题。

---

## 13. 失败、取消与重入矩阵

| 场景 | 所在阶段 | 处理 | 结果 |
|---|---|---|---|
| 没有 `PossessionManager` | 输入 | 记录警告并拒绝 | 当前控制不变 |
| 没有 `SoulActor` | 开始飞行前 | 拒绝 | 不预定目标 |
| 目标不是倒地可附身体 | 目标解析 | `ValidatePossessionTarget` 拒绝 | 不启动飞行 |
| 目标已被其他事务预定 | 目标解析 | `TryReserveForPossession` 失败 | 不启动飞行 |
| 同帧重复输入 | 输入 | `lastPossessionInputFrame` 去重 | 只处理一次 |
| 飞行中目标失效 | Flying | 取消预定并 `CancelFlightToSoul` | 恢复灵魂 Idle |
| Flying 中管理器禁用 | Flying | `OnDisable` 停止协程、恢复控制 | 不留下半附身 |
| 当前已附身又选择新目标 | Possessing | 先解绑旧身体，再飞向新目标 | 目标切换可回收 |
| 普通模式未到最短附身时间 | Possessing | `RequestRelease` 拒绝 | 身体继续受控 |
| GameOver 中请求附身 | 任意 | `CanStartPossession` 拒绝 | 不创建新事务 |
| 当前身体死亡 | Possessing | `NotifyBodyDied` + `CommitRelease(BodyDied)` | 回到灵魂并触发死亡 relay |
| 直接回收被附身身体 | Pool | `MonsterPool.Return` 拒绝 | 防灵魂连带回池 |
| 场景缩放非 1 | 地图/表现 | 初始化告警 | 不自动修复，避免视觉 bounds 失真 |

---

## 14. 性能设计

### 14.1 控制热路径

- `ControlCommand` 使用 struct；
- `Actor` 只在 `Awake` 建立默认 Controller；
- `PlayerController` 是全局单例，不为每个 Actor 读取输入；
- `MonsterActor` 缓存目标引用；
- `SlideMove` 使用固定容量 `Collider[32]`，避免热路径扩容；
- 附身目标射线只在输入边沿执行，不在每帧扫描全部怪物。

### 14.2 状态切换成本集中化

将相机、HUD、PlayerHealth、GameManager、Tag、父子关系的修改集中到 `PossessionManager`，避免各组件在每帧猜测当前是否附身。这样虽然提交时有多个跨系统调用，但运行时状态读取更简单，且回滚路径明确。

### 14.3 非缩放时间的使用

附身飞行、最短附身计时、冷却和身体衰减使用 `Time.unscaledTime` / `Time.unscaledDeltaTime` 的场景，确保子弹时间不会意外改变事务时序。

---

## 15. 验收与测试矩阵

| 编号 | 测试 | 操作 | 预期 |
|---|---|---|---|
| POS-01 | 灵魂输入 | 自由灵魂态移动、瞄准、攻击 | SoulActor 消费 PlayerController 命令 |
| POS-02 | 射线目标 | 点击可附身倒地怪 | 目标进入 Flying，灵魂沿路径移动 |
| POS-03 | 无效目标 | 点击活怪、已附身体、淡出体 | 请求拒绝，日志给出明确原因 |
| POS-04 | 目标争抢 | 两个附身请求同时指向同一尸体 | 只有一个事务能成功预定 |
| POS-05 | 飞行取消 | 飞行中让目标进入 Fading/Destroy | 预定释放，灵魂恢复 Idle |
| POS-06 | 提交 | 飞行到达尸体 | Target 变为 PlayerController，Camera/HUD/Health/GameManager 同步 |
| POS-07 | 灵魂抑制 | 附身期间观察灵魂 | 不消费输入、不移动、不参与碰撞，仍可显示跟随 |
| POS-08 | Scale | 附身身体缩放变化 | 灵魂世界 Scale 保持稳定 |
| POS-09 | 主动释放 | 达到最短时间后释放 | 身体淡出/回收，灵魂回到原位置和 hoverHeight |
| POS-10 | 最短时间 | 附身后立即释放 | 请求被拒绝 |
| POS-11 | 身体死亡 | 当前身体 HP 归零 | 走 BodyDied 释放原因，死亡 relay 只触发一次 |
| POS-12 | 技能烧血 | 技能代价将身体 HP 扣到 0 | 当前技能完成判定后再死亡，不被被动衰减抢断 |
| POS-13 | 对象池 | 附身结束后回收，再 Spawn 同 prefab | Controller、Collider、VFX、Tag、Ability 状态全部恢复 |
| POS-14 | GameOver | Flying/Possessing 时进入 GameOver | 协程停止、绑定清理、无残留控制权 |
| POS-15 | 场景切换 | 附身目标/灵魂跨场景过渡 | 不产生 DontDestroyOnLoad 灵魂残留 |
| POS-16 | 性能 | 连续刷怪、频繁附身、VFX | 无每帧输入对象分配，池回收无状态泄漏 |

### 15.1 诊断建议

优先按日志前缀排查：

```text
输入不响应       → [PossessionInput]
射线选错目标     → [PossessionTargeting]
飞行中断         → [Possession] + GetPossessionDebugState
附身后双 Player  → SoulActor.SetSuppressed + MonsterActor.OnPossessed
释放后幽灵/消失   → SoulActor.DetachFromPossessionAnchor + MonsterPool.Return
技能代价立即死亡  → [HpCost] + IsAbilityCostDeathPending
```

---

## 16. 已实现边界与后续扩展

### 16.1 当前已实现

- Actor/Controller 解耦；
- PlayerController 与 AIController 共用 `ControlCommand`；
- Soul ↔ Monster 控制权切换；
- Flying 预定与目标失效回滚；
- 可逆释放、换身、身体死亡和 GameOver 清理；
- 灵魂抑制、Collider 管理、Tag 切换、相机/HUD/Health 绑定；
- 附身身体与 AI/技能/双属性/池生命周期协作；
- 对象池防附身回收；
- 事务事件和 transaction id。

### 16.2 当前未实现或不属于本架构范围

1. 网络多人控制权同步；
2. 输入回放/确定性重演；
3. ECS 化 Actor；
4. 完整的跨场景 Actor 序列化恢复；
5. 多灵魂同时争抢同一身体的网络级锁；
6. 把所有 VFX、相机过渡和 UI 动画统一成可配置状态机。

这些可以沿用当前状态机和事件接口扩展，但不应把当前单机事件系统表述为网络权威事务系统。

---

## 17. 源码索引

### 核心控制层

```text
Assets/Scripts/Core/Actors/Actor.cs
Assets/Scripts/Core/Control/IController.cs
Assets/Scripts/Core/Control/ControlCommand.cs
Assets/Scripts/Core/Control/PlayerController.cs
Assets/Scripts/Core/Control/AIController.cs
```

`NullController` 定义在 `Assets/Scripts/Core/Control/IController.cs` 中。

### 角色与附身

```text
Assets/Scripts/Combat/Actors/SoulActor.cs
Assets/Scripts/Combat/Actors/MonsterActor.cs
Assets/Scripts/Combat/Possession/PossessionManager.cs
Assets/Scripts/Combat/Possession/PossessionBehavior.cs
```

### 战斗与表现接入

```text
Assets/Scripts/Combat/Abilities/Core/EnemyAbility.cs
Assets/Scripts/Combat/Abilities/Core/PlayerAbility.cs
Assets/Scripts/Combat/Tags/GameplayTagContainer.cs
Assets/Scripts/Combat/Effects/GameplayEffectDefinition.cs
Assets/Scripts/UI/HUD/PossessionHUD.cs
Assets/Scripts/Presentation/Camera/CameraDirector.cs
```

### 生命周期与池

```text
Assets/Scripts/Levels/Spawning/MonsterPool.cs
Assets/Scripts/Combat/Runtime/PooledObject.cs
Assets/Scripts/Combat/Runtime/DestroyOnOwnerDeath.cs
```

---

## 18. 总结

本架构将附身定义为一个可回滚、可观测、可复用的控制权事务：

```text
输入命令
  → 目标解析
  → 目标预定
  → 灵魂飞行
  → CommitPossession
  → Controller 转移
  → 战斗/相机/HUD/生命同步
  → 释放、死亡或 GameOver 清理
```

其技术价值不在于“按键换了一个角色”，而在于通过 `Actor + IController + ControlCommand` 将输入源、执行体和附身编排分层，再用 `PossessionManager` 把父子变换、碰撞、Tag、能力、HUD、相机、生命绑定和对象池安全收束到可验证的生命周期中。
