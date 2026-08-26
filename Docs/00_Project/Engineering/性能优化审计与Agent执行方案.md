# Possession 性能优化审计与 Agent 执行方案

> 审计日期：2026-08-26
> 审计范围：`Assets/Scripts/**/*.cs`（255 个 C# 文件，约 5.2 万行）
> 工程版本：Unity 2022.3.62f3c1
> 证据等级：当前结论来自静态代码审计；Unity Editor 已打开，但本地 Unity-MCP 未响应，尚无可信的 Play Mode Profiler 基线。文中不会把静态风险冒充实测耗时。

## 1. 结论摘要

当前项目已经具备怪物对象池、VFX 对象池、敌人注册表、AI 决策降频、空间桶分离和分层地图流送，说明性能意识已经进入架构层。现阶段最明确的问题不是“完全没有优化”，而是部分高频路径仍残留分配型 Physics API、层级组件扫描和场景全量扫描，抵消了既有池化收益。

首批建议优先处理五类问题：

1. 所有移动 Actor 都会调用 `Actor.SlideMove`，其中 `Physics.OverlapSphere` 会在移动热路径产生数组分配。
2. 通用 `Projectile` 与 `MineBehaviour` 的周期命中检测使用分配型 Overlap API，弹幕和地雷数量上升时会形成持续 GC 压力。
3. `MonsterPathfinder` 每 0.25 秒通过 `FindObjectsOfType<TerrainEffectTile>` 重建危险区缓存，流送地图越大，扫描成本越高。
4. `MonsterActor` 在帧循环中重复查询 `GluttonyBodyState`、活动 Animator 与 `Camera.main`，怪物数量会线性放大这些调用。
5. `VfxPool` 虽然复用了 GameObject，但每次租借和归还仍执行 `GetComponentsInChildren<ParticleSystem>`，池化对象的层级扫描与数组分配仍然存在。

本轮代码落地只处理上述确定性热点，不改战斗数值、技能时序、Prefab、场景或资源配置。

## 2. 性能风险分级

| 优先级 | 热点 | 静态证据 | 影响模型 | 本轮动作 |
|---|---|---|---|---|
| P0 | Actor 滑动碰撞分配 | `Actor.SlideMove` 起点和每段终点使用 `Physics.OverlapSphere` | 活跃 Actor 数 × 移动帧数 × 分段数 | 改为复用缓冲的 `OverlapSphereNonAlloc`；终点只需布尔结果，改用 `Physics.CheckSphere` |
| P0 | 通用投射物命中分配 | `Projectile.CheckHit` 每 0.05 秒创建 Collider 数组 | 投射物数量 × 20 次/秒 | 使用固定复用缓冲与 `OverlapSphereNonAlloc`，保留命中顺序和首次结算语义 |
| P0 | 地雷检测分配 | `MineBehaviour.Update/Explode` 使用两次 `OverlapSphere` | 地雷数量 × 帧率；爆炸额外一次 | 使用实例复用缓冲与 NonAlloc 查询，不改变逐帧触发时机 |
| P0 | 危险区全场景扫描 | `MonsterPathfinder.RefreshHazardCache` 每 0.25 秒 `FindObjectsOfType` | 活跃地块/场景对象总数；会产生数组 | 由 `TerrainEffectTile` 维护启用实例注册表，Pathfinder 直接遍历 |
| P1 | 怪物帧循环查询 | `MonsterActor.ExecuteMovement/LateUpdate/GetActiveAnimator` 重复 GetComponent、GetComponentInChildren、Camera.main | 活跃怪物数 × 帧率 | Awake 缓存可选组件、Animator 集合与 billboard Camera；丢失时惰性恢复 |
| P1 | VFX 池层级扫描 | `VfxPool.StopAndClearParticles` 每次 Spawn/Release 遍历并分配粒子数组 | VFX 租借/归还频率 × 子层级规模 | `PooledObject` 首次缓存 ParticleSystem 数组，池复用直接读取缓存 |
| P1 | 地块 FixedUpdate 密度 | 每个 `TerrainEffectTile` 每个 FixedUpdate 做 OverlapBox | 活跃触发地块数 × 50 次/秒 | 暂不改变时序；后续按地块分相、10Hz 扫描并用触发事件做快速唤醒 |
| P1 | 技能 Physics API 分散 | 多个技能仍使用 `OverlapSphere/Box`、`SphereCastAll/RaycastAll` | 技能段数、投射物数、范围目标数 | 第二批按能力逐个改 NonAlloc，必须为缓冲溢出定义降级策略 |
| P2 | 寻路 A* 开表 | `openCells.RemoveAt/Contains` 为线性操作，单次最多 768 节点 | 同时重算路径的怪物数 | 后续改二叉堆 + open membership HashSet；先加 ProfilerMarker 验证占比 |
| P2 | UI 每帧同步 | 血条、冷却、方向、结果等多个组件常驻 Update/LateUpdate | UI 元素数 × 帧率 + Canvas rebuild | 改为事件驱动并保留低频兜底；需独立 UI 回归任务 |
| P2 | 调试日志 | 战斗与波次路径包含大量插值日志 | Development Build 中字符串构造与日志 IO | 将高频日志挂到显式 debug 开关或 `Conditional`；不在本轮批量改动 |

## 3. 本轮可执行改动合同

### 3.1 `Actor.SlideMove`：移动碰撞零分配

文件：`Assets/Scripts/Core/Actors/Actor.cs`

执行：

- 增加类级复用 Collider 缓冲，只用于主线程同步 Physics 查询。
- 起点脱困由 `Physics.OverlapSphere` 改为 `Physics.OverlapSphereNonAlloc`。
- 终点校验不读取具体 Collider，只判断是否重叠，改为 `Physics.CheckSphere`。
- 缓冲满时仍以已返回的 Collider 完成脱困，不在热路径扩容；在注释中说明复杂重叠场景的保守行为。

保持不变：分段、SphereCast、skin、滑墙投影、Y 轴约束和碰撞 LayerMask。

验收：`SlideMove` 方法内不再出现分配型 `Physics.OverlapSphere`。

### 3.2 `Projectile` / `MineBehaviour`：命中查询零分配

文件：

- `Assets/Scripts/Combat/Projectiles/Projectile.cs`
- `Assets/Scripts/Combat/Projectiles/MineBehaviour.cs`

执行：

- 为查询增加可复用 Collider 缓冲。
- 全部周期性 `OverlapSphere` 改为 `OverlapSphereNonAlloc`。
- 循环上限严格使用返回的 `hitCount`，不得遍历缓冲残留槽位。
- 不改变 Projectile 的 0.05 秒检查间隔；不改变 Mine 的逐帧触发与爆炸范围。
- 不在本轮把 Mine 改为对象池，避免改变其 `onExplode` 和 Ability 活跃列表生命周期。

验收：上述两个类不再调用分配型 `Physics.OverlapSphere`；伤害仍只结算一次。

### 3.3 `TerrainEffectTile` / `MonsterPathfinder`：危险区注册表

文件：

- `Assets/Scripts/Levels/MapStreaming/TerrainEffectTile.cs`
- `Assets/Scripts/AI/MonsterPathfinder.cs`

执行：

- `TerrainEffectTile` 内维护静态启用实例集合，只读暴露给 Pathfinder。
- `OnEnable` 注册；`OnDisable` 必须先注销，再处理占用者清理，不能被当前早退逻辑跳过。
- `MonsterPathfinder.RefreshHazardCache` 遍历启用集合，删除 `FindObjectsOfType<TerrainEffectTile>`。
- 迭代期间只读，不修改注册表；失效引用直接跳过。
- 复用 `TerrainEffectTile` 的退出 ID 缓冲，消除角色离开地块时临时 `List<int>` 分配。

保持不变：危险类型仅 Lava/Spike、0.25 秒缓存节拍、HazardBucketSize、路径阻挡矩形算法。

验收：`MonsterPathfinder` 不再包含 `FindObjectsOfType<TerrainEffectTile>`；地块禁用后不会继续阻挡寻路。

### 3.4 `MonsterActor`：高频引用缓存

文件：`Assets/Scripts/Combat/Actors/MonsterActor.cs`

执行：

- Awake 缓存 `AIController`、`GluttonyBodyState`、`BossReserveCorpseVisualFx`。
- 缓存 Animator 列表，并在活动 Animator 丢失/模型切换时从缓存中选择第一个 activeInHierarchy 的 Animator；仅在缓存无效时重新扫描。
- 缓存 billboard Camera；只有缓存为空或失活时才回退 `Camera.main`。
- `BeginImmediateChase`、`ResetForSpawn`、`ResetForPool` 优先使用缓存。
- 不缓存会因运行时挂载而变化的 Ability 列表，不改变现有技能注册语义。

保持不变：血条朝向、Animator `Speed` 参数、暴食小猫转向倍率、池化状态重置。

验收：`LateUpdate` 不再同帧访问两次 `Camera.main`；移动循环不再每帧 `GetComponent<GluttonyBodyState>`。

### 3.5 `VfxPool`：粒子层级缓存

文件：

- `Assets/Scripts/Combat/Runtime/PooledObject.cs`
- `Assets/Scripts/Combat/Runtime/VfxPool.cs`

执行：

- `PooledObject` 保存首次扫描得到的 `ParticleSystem[]`，提供惰性只读访问。
- 新实例加入池时初始化缓存；旧池实例在第一次访问时兼容初始化。
- `VfxPool.StopAndClearParticles` 使用缓存，不再每次 `GetComponentsInChildren`。
- 若缓存中元素已销毁，跳过空引用；不得在每次租借时为此重建数组。

边界：运行时在已入池 VFX 下动态新增 ParticleSystem 的对象，需要显式刷新缓存。本项目现有 VFX 生成路径以静态 Prefab 子层级为主；本轮不改变 Ability 自身的播放时长解析。

验收：`VfxPool.StopAndClearParticles` 不再调用 `GetComponentsInChildren<ParticleSystem>`。

## 4. 明确不做

- 不修改任何技能数值、CD、伤害、半径或帧时序。
- 不修改 Prefab、场景、Material、ScriptableObject、Packages 或 ProjectSettings。
- 不重写寻路算法，不引入 Job/Burst/ECS 或第三方库。
- 不批量替换全部技能 Physics 查询；它们的命中顺序、穿透和缓冲溢出语义需逐项回归。
- 不声称 FPS、GC Alloc 或内存已经获得某个百分比提升；必须等可用 Play Mode Profiler 基线后量化。

## 5. 验证计划

### 5.1 静态检查

1. `rg` 确认目标热路径不存在分配型 Overlap/Find 调用。
2. 检查每个 NonAlloc 循环只遍历 `[0, hitCount)`。
3. 检查静态注册表在 Enable/Disable 对称更新。
4. 检查池缓存兼容旧实例、空引用与重复 Release。
5. 检查 `git diff` 只包含本合同文件与两篇用户要求的文档。

### 5.2 Unity 验证

1. 对当前已打开 Editor 执行 `AssetDatabase.Refresh`，等待脚本编译。
2. 查询 Console Error/Exception；不得引入新增严重错误。
3. 若可运行 Play Mode，最低回归：
   - 灵魂与附身怪沿墙移动、墙角滑动、起点卡入障碍时脱困；
   - 玩家/敌方投射物命中、墙体命中、生命周期结束；
   - 地雷触发、爆炸、多目标伤害、超时销毁；
   - Lava/Spike 对寻路仍为阻挡，地块卸载后阻挡消失；
   - 怪物血条持续朝向镜头，暴食形态切换 Animator 正常；
   - VFX 重复租借后粒子不残留、不提前消失。

### 5.3 后续性能基线

在 Unity-MCP 恢复后，使用固定场景与固定 Seed 对优化前后各采 60 秒：

- 25 / 50 / 100 活怪；
- 20 / 50 / 100 同时投射物；
- 0 / 20 / 50 活跃 TerrainEffectTile；
- CPU Main Thread、Scripts、Physics、GC Alloc/frame、GC 次数、Mono Used；
- 记录 P50/P95/P99 Frame Time，不能只报平均 FPS。

## 6. 风险与人工 Review 点

| 风险 | 防护 |
|---|---|
| NonAlloc 缓冲溢出导致少处理 Collider | Movement 使用保守脱困；Projectile/Mine 缓冲取明显高于典型同屏密度的容量，并在代码注释记录上限。后续 Profiler/压力测试发现满缓冲再扩容 |
| 静态注册表残留已销毁对象 | OnEnable/OnDisable 对称维护；遍历时跳过 Unity fake-null |
| 运行时切换模型导致 Animator 缓存过期 | 从缓存选择 active Animator；缓存整体失效时才重新扫描 |
| 动态追加粒子系统未进入缓存 | 明确静态 Prefab 层级合同；动态结构调用刷新入口，不在每次租借重扫 |
| 当前工作区已有用户资产修改 | 本轮不触碰 `pride_new.prefab`、`sloth_new 1.prefab`、`EnemyAiTest.unity`、`BlackFlash.mat` |

## 7. 第二批候选任务

1. 为所有持续范围技能建立统一的 NonAlloc 查询规范：实例缓冲、满缓冲告警采样、目标去重。
2. 将 TerrainEffectTile 轮询降为分相 10Hz，并用玩家/敌人空间桶筛掉远离所有 Actor 的地块。
3. 为 MonsterPathfinder 增加全局重算预算，避免多怪同一帧 A* 峰值；用二叉堆替换线性 open list。
4. 为 PlayerHealth、AbilityCooldownUI、BossHealthBarUI 等改事件驱动，降低 Canvas 脏标记频率。
5. 将 MonsterActor 池化重置中的 Renderer/Collider/Ability 层级扫描改为 Awake 缓存，并对运行时动态能力提供显式注册。
6. 为高频 Debug 日志增加统一编译/运行时门禁。
7. 增加 `ProfilerMarker`：`AI.Decision`、`AI.Path.Rebuild`、`MapStreaming.Tick`、`Combat.PhysicsQuery`、`VfxPool.RentReturn`。

## 8. 本轮执行结果

- 首批合同已由 `GPT-5.6 Luna / xhigh` 实现 SubAgent 落地，并由另一名 `GPT-5.6 Luna / xhigh` Review SubAgent 独立复审、修正。
- `Assets/Scripts` 的改动范围收敛为合同约定的 8 个脚本；`git diff --check` 通过。
- 静态搜索确认目标三个类不再调用分配型 `Physics.OverlapSphere`，Pathfinder 不再使用 `FindObjectsOfType<TerrainEffectTile>`，VFX 归还路径不再执行粒子层级扫描。
- 使用项目生成的 `Assembly-CSharp.csproj` 和 Unity 对应 Mono 工具链完成编译，结果成功；输出仍包含项目原有的程序集版本冲突和未使用字段等 Warning，本轮没有扩改这些无关问题。
- Unity-MCP 当前返回认证/本地服务异常，因此没有执行可信的 Editor Refresh、Play Mode 回归和 Profiler 对照采样。运行时收益与 Collider 缓冲打满情况仍需按 5.3 节补测。

## 9. 完成定义

- 本轮 5 项改动合同全部落地，且没有扩大到第二批候选任务。
- 静态审查通过，目标分配型调用被移除。
- Unity 刷新编译无新增错误；若工具不可用，明确记录未验证原因，不能写“已通过”。
- Review SubAgent 对正确性、池化生命周期、缓冲溢出、注册表对称性和用户脏改保护逐项确认。
- 技术亮点报告只描述实际完成的优化，不把候选方案写成既成成果。
