---
name: debug
description: >-
  Diagnose and fix unresolved bugs recorded in the project's Debug.md, using the
  current Assets/Scripts architecture and Unity validation workflow. Use when the
  user asks to solve bugs, says “帮我解决Bug”, or requests work from the Bug List.
---

# Debug — 项目 Bug 处理

本 skill 是当前 Kepler / Possession 项目的 Bug List 执行入口。它把 `Debug.md`
作为待办来源，把下面的代码架构作为定位索引；架构说明是当前实现事实，不是设计真源。

## 每次激活的固定流程

严格按以下顺序执行：

1. 重新读取项目根目录的 `AGENTS.md` 和 `.vibe/rules.md`，并检查 `git status`。
   保留工作区已有改动，不覆盖、不回滚、不顺手清理用户文件。
2. 读取项目根目录的 `Debug.md`。只识别活动文本中的顶层条目 `- [ ] ...`；注释
   内的示例不算 Bug。`- [x] ...` 是已完成项，不再处理。
3. 若没有未解决条目，报告“Bug List 当前没有未解决 Bug”，停止本次执行。
4. 按 `Debug.md` 中的出现顺序取第一个未解决 Bug。除非用户明确要求改变顺序，不能
   跳过它去处理后面的条目。
5. 根据本文件的架构索引和 Bug 条目中的现象、复现步骤、相关文件，读取最小但完整的
   代码上下文：入口、直接调用链、状态/数据来源、所有重要消费者。使用 `rg` 搜索
   调用方和引用关系；不要只凭一个方法名或单个脚本猜根因。
6. 先诊断并给出根因，再实施最小必要修改。Bug 需要场景、Prefab、ScriptableObject
   或配置参与时，同时检查对应资源引用和运行时入口；不要把代码问题误判成资源问题。
7. 完成验证后，才把该条目的开头从 `- [ ]` 改为 `- [x]`。保留 Bug ID、描述、顺序
   和用户填写的内容；默认只切换复选框，不替用户编写新的 Bug 条目。
8. 重新读取 `Debug.md` 并继续处理下一个未解决项。每个 Bug 都要独立完成“定位 →
   修改 → 验证 → 标记”，不能把一批未经验证的修改最后统一标记。
9. 如果当前 Bug 缺少复现信息、需要设计裁决、需要高风险授权、或无法验证，保持
   `[ ]`，不要跳过，停止并向用户报告阻塞原因与需要的输入。

## Bug List 约定

- 只有 `Debug.md` 中活动文本的顶层 `- [ ]` / `- [x]` 行是 Bug 条目。
- `[ ]` 是未解决；`[x]` 只允许在修复和验证均完成后使用；不引入第三种状态。
- Bug ID 应由用户维护并保持稳定，例如 `BUG-001`。不要因为修复而改 ID、重排条目
  或合并条目。
- 不要主动扫描代码并把可疑点写入 `Debug.md`；发现的额外风险只能在最终报告中说明，
  等用户手动登记后再进入处理队列。
- 代码修复的记录放在最终回复中。只有用户明确要求，才在 Bug 条目下追加修复记录。

## 项目规则与设计边界

- `.vibe/rules.md` 是最高优先级规则。不得编辑 `Library/`、`Temp/`、`Logs/`、
  `obj/`、`Build/`、`UserSettings/` 等自动生成目录；不得手改 `.meta` 的系统字段；
  不得自动解决 Unity YAML 冲突。
- 修改 `Packages/` 或 `ProjectSettings/` 前必须先列出变更并获得用户确认。新增依赖、
  大规模重构、删除资源、修改 Shared Original、或修改场景/Prefab/ScriptableObject
  的高风险操作也必须先停下确认。
- 代码应遵循现有全局命名空间、命名和缩进风格，最小化改动，不添加无意义抽象或无意义
  `try/catch`。默认不提交、不推送、不创建分支。
- 预期行为不清楚时，先读取 `.vibe/doc/Canonical/00_CANONICAL_INDEX.md`，再按范围读取
  `Canonical/01_DESIGN_CANONICAL.md`、`02_CONTENT_CANONICAL.md`、
  `03_PRESENTATION_CANONICAL.md` 及相关 Content 文档，并读取 `Docs/02_Open_Decisions/`
  中对应条目。Canonical / Owner 决策优先于实现；Open / Tunable 决策不能被静默拍板。
  若 Bug 修复实际会改变设计行为，停止并请求 Owner 方向。
- `.vibe/doc/整体流程与架构.md` 是实现层速查；它与本文件都不能替代当前代码读取。
  文档、代码和 Canonical 冲突时，要报告冲突，不把实现事实当成设计要求。

## 运行时总流程

```text
BootStrapper
  → GameManager（DDOL，全局状态 / 时间 / 场景级协调）
  → MainMenuController（新局 / 继续 / Boss 入口）
  → RunSession（DDOL，对局阶段与内存快照）
  → 对局场景 ENGPOSS001SceneInstaller
       → MapStreamingSystem（Chunk 地图）
       → WaveManager（波次或连续刷怪流程）
            → RunSpawnDirector → MonsterSpawner → MonsterPool
       → CardManager → CoreChoiceUI → BuildView / HUD
       → PossessionManager（Soul ↔ Monster Controller 切换）
       → Combat Ability / Effect / Projectile / Presentation
  → SaveCoordinator（Run 存档）与 MetaProfileStore（局外存档）
```

关键状态边界：

- `GameManager.GameState` 是战斗级状态：`Soul`、`Possessed`、`BulletTime`、`GameOver`。
- `RunSession.RunPhase` 是整局阶段：`Opening`、`Tutorial`、`Waves`、`Choice`、`Final`、
  `Result`、`Failed`。不要把两套状态混用。
- `TimeScaleManager` 通过按域 Push/Pop 管理暂停、子弹时间、GameOver；检查时间相关
  Bug 时同时确认 `Time.deltaTime`、`Time.unscaledDeltaTime` 和当前时间域请求。
- `Actor.SetController(IController)` 是附身的核心切换入口；不要在局部逻辑中另造一套
  “附身状态控制器”。
- `IActor` 是 UI / 系统读取 Actor 的只读视图；优先通过它取名称、HP、倒地状态、控制状态
  和技能槽快照，避免 UI 直接耦合具体 Actor。

## `Assets/Scripts` 架构索引

项目脚本目前位于全局命名空间；目录是主要的职责边界。定位 Bug 时先选模块，再读取
入口与调用链。

| 目录 | 模块职责 | 关键文件 / 类型 |
|---|---|---|
| `Core/Actors` | Actor 执行层基类与 UI 只读接口 | `Actor.cs`、`IActor.cs` |
| `Core/Control` | 玩家 / AI 控制协议与输入指令 | `IController.cs`、`ControlCommand.cs`、`PlayerController.cs`、`AIController.cs` |
| `AI/` | 怪物 AI 配置、行为树、运行时寻路 | `MonsterAIConfig.cs`、`BehaviorTree/BTNode.cs`、`BTComposite.cs`、`MonsterBTNodes.cs`、`MonsterPathfinder.cs` |
| `AI/Boss` | Boss 决策与安全传送规划 | `BossCombatBrain.cs`、`BossTeleportPlanner.cs` |
| `Combat/Actors` | 玩家、灵魂、普通怪、精英/召唤物的战斗状态 | `MonsterActor.cs`、`Enemy.cs`、`SoulActor.cs`、`PlayerHealth.cs`、`PlayerCombat.cs`、`SummonActor.cs`、`EnemyRegistry.cs` |
| `Combat/Abilities` | Ability 生命周期、槽位、耗血、升级、攻击实现 | `CombatAbilityComponent.cs`、`Core/EnemyAbility.cs`、`Core/PlayerAbility.cs`、`Monster/EnemyAbility_*.cs`、`Soul/PlayerAbility_*.cs` |
| `Combat/Effects` 与 `Tags` | Gameplay Effect、Tag、能力门禁与效果应用 | `GameplayEffectDefinition.cs`、`GameplayEffectApplier.cs`、`GameplayTagCatalog.cs`、`GameplayTagContainer.cs` |
| `Combat/Projectiles` 与 `Runtime` | 弹道、钩索、地雷、对象回收与 VFX 池 | `Projectile.cs`、`HookProjectile.cs`、`MineBehaviour.cs`、`PooledObject.cs`、`VfxPool.cs` |
| `Combat/Possession` | 附身目标解析、飞行、换身、释放、身体死亡与子弹时间 | `PossessionManager.cs`、`PossessionBehavior.cs`、`BulletTimeController.cs` |
| `Combat/Boss` | 七宗罪 Boss、词条快照、Boss 表现辅助 | `BossSevenfoldActor.cs`、`BossAbilityProfile.cs`、`BossAffixAssimilator.cs`、`BossSpatialDistortionController.cs` |
| `Levels/MapStreaming` | 无限地图、Chunk 状态、双层 Tile、地形效果和场景刷怪 | `MapStreamingSystem.cs`、`ChunkRuntime.cs`、`ChunkTileGenerator.cs`、`ChunkStateStore.cs`、`TileData.cs`、`MonsterSpawner.cs`、`TerrainEffectTile.cs`、`TerrainSpikeHazard.cs` |
| `Levels/Spawning` | 场景安装、普通/精英/Boss 刷怪、池和预加载 | `ENGPOSS001SceneInstaller.cs`、`RunSpawnDirector.cs`、`EnemySpawner.cs`、`MonsterPool.cs`、`MonsterPreloadService.cs`、`SpawnRequest.cs` |
| `Levels/Waves` | 波次状态机、连续刷怪接入、选卡时机、Boss 入口 | `WaveManager.cs`、`WaveDefinitions.cs`、`ContinuousSpawnEntry.cs`、`WaveTimerUI.cs` |
| `Levels/Rooms` | 当前仅保留卡牌与选卡，不是旧房间系统 | `CardData.cs`、`CardLibrary.cs`、`CardManager.cs`、`CoreChoiceUI.cs`、`CoreChoiceCard.cs`、`ChoiceCard.cs` |
| `Systems/GameFlow` | 启动、全局游戏状态、开场流程、时间缩放 | `BootStrapper.cs`、`GameManager.cs`、`OpeningLandingSequence.cs`、`TimeScaleManager.cs` |
| `Save/` | Run 内存快照、JSON IO、迁移 | `RunSession.cs`、`SaveData.cs`、`SaveCoordinator.cs`、`SaveMigrator.cs` |
| `Systems/Audio` | BGM、SFX、UI 音、旁白、阶段映射与音量 | `AudioManager.cs`、`AudioEventBinder.cs`、`Controllers/*`、`SfxBank.cs`、`StageBgmMap.cs`、`VoiceClipSet.cs`、`MonsterSkillAudioConfig.cs` |
| `Systems/Elite` | Elite 构筑快照、网络状态、荣誉殿堂本地/在线链路 | `EliteBuildDirector.cs`、`EliteBuildCarrier.cs`、`EliteNetClient.cs`、`EliteMonsterCatalog.cs`、`HallOfFameStore.cs` |
| `Systems/Meta` | 图鉴、卡牌称号、局外档案与局外持久化 | `CardArchiveStore.cs`、`CardEpithet*.cs`、`MetaProfileStore.cs` |
| `Systems/Narrative` | 旁白 Access、Cue、事件总线、调度、运行统计倾向 | `NarrativeAccess*.cs`、`NarrativeCue*.cs`、`NarrativeEventBus.cs`、`NarrativeScheduler.cs`、`RunTendencyScorer.cs` |
| `Systems/Progression` | 七罪印记、玩家被动、敌方被动和数值计算 | `Passives/PossessionImprintManager.cs`、`PlayerPassiveManager.cs`、`EnemyPassiveBuff.cs`、`PossessionImprintMath.cs`、`SinType.cs` |
| `Systems/Seeding` 与 `Settings` | 确定性随机流和音频设置 | `SeedSystem.cs`、`AudioSettingsManager.cs` |
| `Presentation/` | 摄像机、战斗特效、预警、阴影与渲染优化 | `Camera/CameraDirector.cs`、`Combat/ActorVisualFx.cs`、`Combat/CombatEffectManager.cs`、`Combat/MonsterAbilityTelegraph.cs`、`Rendering/*` |
| `Tutorial/` | 教学配置、步骤、事实总线、探针、遥测与 UI | `TutorialController.cs`、`TutorialConfig.cs`、`TutorialFactBus.cs`、`TutorialProbes.cs`、`TutorialUI.cs` |
| `Localization/` | 字体注册/注入与文本目录 | `FontRegistry.cs`、`FontApplier.cs`、`TextCatalog.cs` |
| `UI/` | UI 总控、构筑、HUD、主菜单、图鉴、荣誉殿堂、旁白字幕、设置 | `Core/UIManager.cs`、`Gameplay/BuildView.cs`、`HUD/*`、`MainMenu/*`、`CardArchive/*`、`HallOfFame/*`、`Narrative/*`、`Settings/*` |
| `Debug/` | 运行时调试面板、作弊、卡面/波次/音频/旁白检查 | `CardFaceBrowser.cs`、`CardProgressPanel.cs`、`EnemyAIAttackTestSpawner.cs`、`MonsterPossessionCheat.cs`、`WaveFlowDebug.cs`、`AudioDebugPanel.cs`、`NarrativeDebugPanel.cs`、`ServerApiCheat.cs` |
| `Editor/` | Inspector 绘制器、音频/波次/Chunk 编辑器 | `AiConfigIdDrawer.cs`、`WaveConfigDrawer.cs`、`AudioHubWindow.cs`、`ChunkLayoutEditorWindow.cs`、各配置 Editor |

## 按症状加载代码上下文

每类 Bug 至少读取“入口 + 状态所有者 + 直接消费者”；如涉及跨模块，沿调用链扩展。

| Bug 症状 | 首批上下文 | 继续追踪 |
|---|---|---|
| 编译错误、运行时异常、NullReference | 报错堆栈指定脚本与方法；对应调用方 | `console-get-logs`，组件获取、Prefab 引用、场景安装顺序 |
| 玩家不动、怪物不追击/不攻击、寻路错误 | `Actor.cs`、`ControlCommand.cs`、`PlayerController.cs` 或 `AIController.cs`、`MonsterAIConfig.cs`、行为树节点 | `MonsterActor.cs`、`MonsterPathfinder.cs`、`MapStreamingSystem.cs`、输入/碰撞层 |
| 技能不放、CD/耗血/伤害/命中错误 | `CombatAbilityComponent.cs`、`EnemyAbility.cs` / `PlayerAbility.cs`、具体 `EnemyAbility_*.cs` | `GameplayEffect*`、`GameplayTag*`、目标 Actor、Projectile、Telegraph、CombatAudio |
| 附身、换身、释放、身体死亡、子弹时间错误 | `PossessionManager.cs`、`PossessionBehavior.cs`、`Actor.cs`、`SoulActor.cs`、`MonsterActor.cs` | `PlayerController.cs`、`PlayerHealth.cs`、`CameraDirector.cs`、`BuildView.cs`、HUD、`TimeScaleManager.cs`、`BulletTimeController.cs` |
| 怪物刷出位置/数量/池化/预加载错误 | `ENGPOSS001SceneInstaller.cs`、`RunSpawnDirector.cs`、`MonsterSpawner.cs`、`MonsterPool.cs` | `WaveManager.cs`、`SpawnRequest.cs`、`MapStreamingSystem.cs`、`MonsterActor` 生命周期 |
| 波次不推进、选卡时机/暂停/恢复错误 | `WaveManager.cs`、`WaveDefinitions.cs`、`CoreChoiceUI.cs`、`CardManager.cs` | `RunSession.cs`、`GameManager.cs`、`TimeScaleManager.cs`、`RunSpawnDirector.cs` |
| 地图 Chunk、边界、地形伤害、流送状态错误 | `MapStreamingSystem.cs`、`ChunkRuntime.cs`、`ChunkStateStore.cs`、`ChunkTileGenerator.cs`、`TileData.cs` | `FixedChunkLayout.cs`、`RegionDef.cs`、`TerrainEffectTile.cs`、`TerrainSpikeHazard.cs`、`MonsterPathfinder.cs` |
| 卡牌抽取、解锁、卡效、卡面、重抽错误 | `CardData.cs`、`CardLibrary.cs`、`CardManager.cs`、`CoreChoiceUI.cs`、`CoreChoiceCard.cs` | `EnemyAbility.cs`、具体能力、`BuildView.cs`、`CardArchiveStore.cs`、`CardArchivePanel.cs`、`SeedSystem.cs` |
| 存档、继续、重开、跨场景状态丢失 | `RunSession.cs`、`SaveData.cs`、`SaveCoordinator.cs`、`SaveMigrator.cs` | `MainMenuController.cs`、`GameManager.cs`、`WaveManager.cs`、`CardManager.cs`、`MapStreamingSystem.cs`、`PossessionManager.cs` |
| BGM、SFX、旁白、音量或重复 AudioListener | `AudioManager.cs`、对应 `Controllers/*`、`AudioEventBinder.cs`、`AudioSettingsManager.cs` | `SceneBgm.cs`、`StageBgmMap.cs`、`VoiceClipSet.cs`、`MonsterSkillAudioConfig.cs`、`GameManager.cs` |
| 旁白、字幕、教学步骤或本地化错误 | `NarrativeScheduler.cs`、`NarrativeAccess*.cs`、`NarrativeCue*.cs`、`NarrativeSubtitleUI.cs` | `NarrativeEventBus.cs`、`TutorialController.cs`、`TutorialFactBus.cs`、`TextCatalog.cs`、`FontApplier.cs` |
| 图鉴、荣誉殿堂、Elite 上传/回退错误 | `CardArchiveStore.cs`、`CardArchivePanel.cs`、`MetaProfileStore.cs`、`HallOfFameStore.cs` | `EliteBuildDirector.cs`、`EliteNetClient.cs`、`EliteBuildCarrier.cs`、`HallOfFamePanel.cs`、`ServerApiCheat.cs` |
| UI 不显示、按钮无效、缩放/字体/构筑/HUD 错误 | `UIManager.cs`、具体 UI 脚本、`BuildView.cs` 或对应 HUD | 场景挂载、Canvas/GraphicRaycaster、`IActor`、`PossessionManager`、`FontRegistry` |
| 性能、阴影、Trail、Animator、VFX 或池化问题 | `GameManager.cs`、`RenderingOptimizationState.cs`、`RendererShadowVisibility.cs`、`MonsterPool.cs`、`VfxPool.cs` | 具体生成/回收路径、Profiler、`TimeScaleManager.cs` |
| 调试按键/作弊/调试显示错误 | `Assets/Scripts/Debug/*` 中对应脚本 | `GameManager.IsFormalFlow`、对应业务系统和 `RuntimeInitializeOnLoadMethod` 生命周期 |

## 关键依赖关系

- Game flow：`BootStrapper` / `GameManager` 建立常驻协调器；`MainMenuController` 调用
  `RunSession.BeginNewRun`、`BeginBossRun` 或 `SaveCoordinator.RequestResume`，再加载对局场景。
- Scene install：`ENGPOSS001SceneInstaller` 确保刷怪基础设施；对局场景中的
  `WaveManager`、`MapStreamingSystem`、`CardManager`、`PossessionManager` 和 UI 组件按自身
  生命周期接入。
- Wave/spawn：`WaveManager` 推进波次和选卡；普通/连续刷怪经 `RunSpawnDirector` 调度，
  `MonsterSpawner` 负责实例登记和生成，`MonsterPool` 负责池化；`MapStreamingSystem` 影响
  可行走空间与 Chunk 生命周期。
- Control/combat：Controller 产生 `ControlCommand`；`Actor` 执行移动与按钮；具体 Ability
  通过 `CombatAbilityComponent` 的 Tag/Effect 门禁，执行伤害、弹道和表现；AI 由 `AIController`
  的节拍式行为树驱动。
- Possession：`PossessionBehavior` 解析目标后交给 `PossessionManager`；管理器负责飞行、
  当前 Body、Controller 切换、灵魂抑制、相机目标、HUD 和结束原因；不要把这些责任复制到
  `PlayerHealth`、`MonsterActor` 或 UI 中。
- Card/progression：`CardManager` 从 `CardLibrary` 提供候选并解锁效果，能力脚本按
  `effectId` / Tag / 参数读取；`CardManager` 同步 `RunSession` 并广播事件给图鉴等消费者。
- Persistence：`RunSession` 采样场景运行时状态，`SaveCoordinator` 只负责 Run JSON IO 和迁移；
  `MetaProfileStore` / `CardArchiveStore` 负责局外数据。不要让 UI 或场景对象直接写存档文件。
- Presentation/UI：`UIManager` 聚合 UI，不负责替代业务状态机；`BuildView`、HUD、图鉴与
  荣誉殿堂应读取系统公开状态或事件。`Presentation` 负责摄像机、战斗表现、预警和渲染优化，
  不应成为 Gameplay 真相来源。

## Unity 验证流程

每次 Bug 修改后按风险选择验证层级，至少完成编译检查：

1. 先用 `editor-application-get-state` 检查当前 Unity Editor 是否存在、是否正在编译、
   是否有 Play Mode 或未保存场景。不得自行启动、重启或关闭 Unity Editor。
2. 用 `console-get-logs` 读取错误和异常；必要时带 stack trace，并确认没有新增编译错误。
3. 若项目已有相关 EditMode / PlayMode 测试，且所有打开场景已保存，再用 `tests-run`。
   当前代码扫描未发现专用测试程序集；若后续出现测试，优先运行与 Bug 相关的最小过滤集。
4. 脚本文件可先用 `script-read` 读取；通过 Unity 能力写 C# 时使用
   `script-update-or-create`，并遵守其编译回报。外部文件修改后如需刷新，先告知用户，
   再使用 `assets-refresh`；禁止未经确认执行 Reimport All 或 Clear Cache。
5. 场景/Prefab/UI/视觉 Bug 需要实机确认时，使用现有打开的场景和合适的截图/日志能力；
   不要为了验证而自动保存用户未保存的场景或修改资源引用。
6. 若 Unity 不在当前会话中打开，明确报告“无法按当前会话规则刷新验证”，不得自行启动。

验证不足时不得把 Bug 标记为 `[x]`。最终报告应包含：处理的 Bug ID、根因、修改文件、
验证方式与结果；未完成或阻塞项保持 `[ ]` 并说明原因。

## 维护边界

- 当 `Assets/Scripts` 结构发生变化，先更新本 skill 的架构索引，再继续依赖该索引处理后续
  Bug；更新时以当前代码为准，不凭旧文档补全。
- 本 skill 不负责建立设计决策、不替代 Issue / Task 流程、不执行提交/推送，也不把发现的
  潜在问题自动写进 `Debug.md`。
