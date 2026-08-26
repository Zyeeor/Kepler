# Tutorial 系统实现方案 v1

> **项目**：Kepler（Possession，七宗罪主题附身战斗）
> **日期**：2026-08-21
> **状态**：方案待评审（未实施；本文档不含任何仓库代码/资产改动）
> **需求真源**：`.vibe/doc/Canonical/Content/Tutorial_Delivery_Baseline_v1.0.md`（下称 Baseline）+ `01_DESIGN_CANONICAL.md` §26 + `02_CONTENT_CANONICAL.md` §12
> **分工约束**：战斗核心逻辑（技能/怪物/Death Relay/Soul Shrine 玩法）由另一位程序负责。本方案 M1/M2 完全独立于战斗逻辑改动；M3 只标注依赖接口，不设计战斗玩法。

---

## 0. 调研结论速览（证据）

| 调研项 | 结论 | 证据 |
|---|---|---|
| Tutorial 实现量 | **零实现**，`RunPhase.Tutorial` 占位直通 | `WaveManager.AutoStartRoutine` 137-143 行：`Opening→Tutorial→Waves` 空转推进 |
| 主战场流程模式 | 正式主战场为**无房间模式**（WaveManager autoStart + MapStreamingSystem 流送） | 战斗场景内由 WaveManager 与 MapStreamingSystem 组件共同驱动 |
| 附身事件 | `OnPossessionStarted(MonsterActor)` / `OnPossessionEnded()` / `OnBodyDiedWhilePossessing(MonsterActor)` 已齐 | `PossessionManager.cs:27-29` |
| 主动换身语义 | **不可区分**：主动脱离（`RequestRelease→CommitRelease`）与附身死亡（`NotifyBodyDied→CommitRelease`）都触发同一个 `OnPossessionEnded`；直接转移（Possessing 中 `BeginPossessionFlight`）只发 `OnPossessionStarted`、不发任何 End 事件 | `PossessionManager.cs:227-237, 389-412, 502-510` |
| 尸体生命周期 | `Die()` → Downed（窗口 `corpsePossessionWindow`=5s）→ `CorpseLifecycleRoutine` 等待 → `BeginDisappearing` → 淡出 3s（`corpseFadeDuration`）→ `MonsterPool.Return`；`SpawnAsPermanentCorpse` 已有"窗口=+∞"先例 | `MonsterActor.cs:1085-1186` |
| 尸体保护扩展点 | `possessionWindowEndsAt` 为 private 字段，`CorpseLifecycleRoutine` 每帧检查 `Time.time < possessionWindowEndsAt`——**延长该字段即自动延后消散、到期自动恢复**，无需改协程 | `MonsterActor.cs:225, 1168-1171` |
| 池回收兜底 | `MonsterPool.Return` 已拒绝回收 `isPossessed` 怪；波次怪不随 Chunk 回收（MonsterSpawner 标记） | `MonsterPool.cs:142-146`；`WaveManager.cs` 头注释 |
| 输入 | 旧 Input Manager 硬编码：LMB=Basic / RMB=Skill1(附身) / Q=Skill2 / Space=Mobility / E=Skill3(子弹时间) / F=Release；**无键位映射层、无运行时改键**；`PlayerController.SetGameplayInputBlocked` 静态输入屏蔽已存在 | `PlayerController.cs:121-168` |
| 输入消费链 | `PlayerController.Tick` 产 `ControlCommand` → `SoulActor.ExecuteButtons` / `MonsterActor.ExecuteButtons`（`PlayerTriggerBasicAttack/Skill/Mobility`、`TryRequestPossessFromInput`、`RequestRelease`） | `SoulActor.cs:225-245`；`MonsterActor.cs:1528-1561` |
| 存档 | 仅 Run 级：`SaveCoordinator`（静态，JSON `possess_run_save.json`，schemaVersion=2 + `SaveMigrator` 链式迁移）；**无 Profile 级持久化** | `SaveCoordinator.cs`、`SaveData.cs` |
| 设置持久化先例 | `AudioSettingsManager` / `DeviceIdentity` 用 PlayerPrefs（设备级、跨 Run） | `AudioSettingsManager.cs:67-108`；`DeviceIdentity.cs` |
| UI 基建 | `UIManager`（SceneSingleton）按钮/文本直连，**无 Toast/Banner/cue 通用件**；`SettingsPanel` 为主菜单与局内**共用组件**（教学开关挂一处两入口兼得）；`PossessionHUD` 有硬编码键位文案（"左键 - 普攻 / Q - 技能"），是键位技术债实证 | `UIManager.cs`、`SettingsPanel.cs`、`PossessionHUD.cs:54-58` |
| Death Relay / Soul Shrine | **完全未实现**（全仓 grep 零命中） | — |
| 初始 Pride Carrier 分配 | 未实现（Opening 占位）；玩家开局为场景内 SoulActor | `02_CONTENT_CANONICAL.md` §14 |
| 调试面板先例 | `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` + OnGUI + F 键 + `GameManager.IsFormalFlow` 屏蔽 | `WaveFlowDebug.cs`、`MonsterPossessionCheat.cs` |
| SO 配置先例 | `[CreateAssetMenu(menuName="Possession/...")]`，场景组件 Inspector 引用（CardManager→CardLibrary 模式），不走 Resources.Load | `CardLibrary.cs`、`CardManager.cs:21-23` |

---

## 1. 现状与差距清单（对齐 Baseline 硬指标）

| Baseline 硬指标 | 现状 | 差距 / 阻塞 |
|---|---|---|
| TUT-01/02 短准备段，可短暂阻塞 Wave，超时放行 | WaveManager 有 gracePeriod + WaveRoutine 协程，**无 Wave 启动门概念**；Phase 直通 | 缺：Wave 启动门（TutorialGate）、准备段驱动 |
| TUT-01 前置"初始 Pride 取得控制" | 未实现，开局为裸 SoulActor | 缺：Opening Carrier 分配驱动（M2，最小接线方案见 §3.1） |
| TUT-02 三槽合法尝试（可追溯提前操作） | 无输入事实采集；`ControlCommand` 是 Tick 局部 ref 参数，外部观察不到 | 缺：输入事实探针（需 PlayerController 加 1 个静态事件） |
| TUT-03 Kill→Corpse（正式规则） | `WaveManager.OnWaveEnemyKilled(MonsterActor)` 已广播（含 isDowned） | **已有事实源**，接 Probe 即可 |
| TUT-04 首个合法 Corpse + 保护期不被清理 | 尸体 5s 窗口后自动消散；保护机制缺，但扩展点天然存在（延 `possessionWindowEndsAt`） | 缺：MonsterActor 扩展 API（约 5 行，加法）+ 教学 Corpse 守护 |
| TUT-05 主动 Leave / 主动转移；无第二 Body 不提示 | 主动/死亡脱离事件不可区分；合法性查询可做（轮询 `CanBePossessed`） | 缺：PossessionManager 结束原因事件（约 10 行，加法）或控制侧关联判定 |
| TUT-06 首次真实 Death Relay；TUT-07 首次 Soul/Shrine | **玩法零实现** | **外部依赖阻塞（M3）**，只定义事实接口契约 |
| TUT-MONSTER-* 首次 Possess 微教学，每 Profile 一次 | 无 | 缺：Monster 微教学框架 + Profile 记录；prefabId 反查有 `MonsterPool.GetPrefabOf` 现成 |
| 幂等完成 / 追溯判定（§6） | 无 Run 级 Gameplay Facts 存储 | 缺：TutorialFactBus（本 Run 事实日志） |
| 动态按键图标读实际 Binding（§2/§4） | 键位硬编码在 `PlayerController.Tick`；无映射层、无运行时改键 | 缺：键位单源化（GameInputBindings）+ GlyphProvider；**真·改键需新 Input System，列为技术债** |
| 教学关闭：隐藏 UI + 立即放行所有阻塞（§4） | 无教学系统，无从关闭 | 缺：全局开关 + 放行语义（开关存 Profile） |
| 跨 Run 持久化（§11） | 仅 Run 档；PlayerPrefs 有先例 | 缺：TutorialProfileStore（独立文件，**不进 SaveMigrator 链**） |
| 读档/场景恢复跳过教学准备段（§10） | `LoadFromSave` 已直接置 Phase=Waves/Choice，**结构性满足**；延迟教学在读档后仍需工作 | 已有 + 注意点：TutorialController 须贯穿整个 Run（含读档后） |
| 提醒间隔 / 超时策略 / 目标失效重获取（§4/§10） | 无 | 缺：Step 运行时状态机 |
| UI/Voice 优先级：Card/Pause 时隐藏恢复（§9） | `CoreChoiceUI.IsDrafting` 可轮询；暂停无事件（`UIManager.isPaused` 私有），可用 `timeScale==0` 兜底判定 | 缺：TutorialUI 冲突隐藏/恢复逻辑（轮询方案，零他方改动） |
| Debug/Authoring（§13） | 调试面板模式成熟可复用 | 缺：TutorialDebugPanel；Death Relay/Shrine 只能做"事实注入"模拟 |
| Telemetry（§12） | 无 | 缺：最小埋点（show/complete/耗时/关闭） |
| Settings 开关 + 重看入口（§11） | SettingsPanel 两处复用 | 缺：Toggle + 重看按钮（加法字段） |

---

## 2. 总体架构

五层划分，依赖方向单向：配置层 ← 控制层 ← 事实检测层；控制层 → 表现层 / 持久化层。新增代码全部放 `Assets/Scripts/Tutorial/`（对齐现有顶层分组惯例），对他方文件只做**加法改动**。

```
┌─────────────────────────────────────────────────────────────┐
│ 表现层   TutorialUI（Banner/微教学面板）  InputGlyphProvider │
├─────────────────────────────────────────────────────────────┤
│ 控制层   TutorialController（SceneSingleton，Step 状态机、  │
│          Gate、提醒/超时/失效重获取、幂等结算、Debug API）  │
├─────────────────────────────────────────────────────────────┤
│ 事实层   TutorialFactBus（Run 级事实日志） + 4 个 Probe：   │
│          Input / Possession / Wave / CorpseTracker          │
├─────────────────────────────────────────────────────────────┤
│ 持久化   TutorialProfileStore（静态类，独立 JSON 文件）     │
├─────────────────────────────────────────────────────────────┤
│ 配置层   TutorialConfig（SO：Step 列表 + 全局参数）         │
└─────────────────────────────────────────────────────────────┘
```

### 2.1 配置层

**`TutorialConfig.cs`**（`[CreateAssetMenu(menuName = "Possession/Tutorial/Tutorial Config")]`）
场景引用方式同 CardManager→CardLibrary：挂在 TutorialController 的 Inspector 字段上。

```csharp
public class TutorialConfig : ScriptableObject
{
    [Header("全局")]
    public bool masterEnabled = true;              // 资产级总开关（Profile 开关叠加）
    public float factPollInterval = 0.2f;          // CorpseTracker 等低频轮询间隔
    public float corpseProtectionSeconds = 30f;    // 教学尸体保护时长（Production Open 可调）
    public OpeningCarrierConfig openingCarrier;    // 初始 Pride Carrier（prefab + 刷出偏移）

    [Header("Steps（按链序）")]
    public List<TutorialStepConfig> steps;

    [Header("Monster 微教学")]
    public List<MonsterMicroEntry> monsterMicros;  // prefabId → 名称/一句核心玩法/关键资源
}
```

### 2.2 控制层

**`TutorialController.cs`** — `SceneSingleton<TutorialController>`，挂战斗场景。

职责：
- 启动判定：新 Run（Phase=Opening 且 `RunSession.HasActiveRun`）→ 进入教学编排；读档恢复（Phase=Waves/Choice）→ 跳过准备段、仅激活未完成 Step 的后台监听；教学关闭（Profile.hintsEnabled=false 或 config 关）→ 全放行、全隐藏。
- Step 状态机：`Inactive → Waiting(startCondition) → Active(提示中) → Completed / Released(超时放行)`；每个 Step 一个 `TutorialStepRuntime`。
- **WaveGate**：`public static bool WaveStartGateOpen { get; }`（默认 true；仅 TUT-01/02 的 `BlockWaveStart` 且未超时/未关闭时 false）。超时由 Controller 侧计时放行——WaveManager 只读门、不背计时。
- **幂等结算**：`CompleteStep(stepId)` 入口先查 Profile（已完成→跳过反馈）再查运行时状态；事件重复/重复订阅/读档重复均不产生二次完成。
- **追溯判定**：Step 进入 Waiting 时先查 FactBus 历史（玩家手快已完成→直接结算）。
- Debug API：`DebugForceStart(stepId)` / `DebugForceComplete(stepId)` / `DebugAdvance()` / `DebugResetRun()` / `GetStepDiagnostics()`（未开始原因、当前目标、保障状态）。

关键接口（伪代码级）：

```csharp
public class TutorialController : SceneSingleton<TutorialController>
{
    public static bool WaveStartGateOpen => Instance == null || Instance.gateOpen;
    public event Action<TutorialStepConfig> OnStepShown;
    public event Action<TutorialStepConfig> OnStepCompleted;
    public event Action<MonsterMicroEntry> OnMonsterMicroShown;

    void BeginRun();                       // 新 Run 编排入口
    void ResumeMidRun();                   // 读档恢复入口
    void SetHintsEnabled(bool enabled);    // 关=隐藏+放行；开=按当前进度恢复
    void ReplayBasics();                   // 重看（不覆盖 Profile 完成记录）
}
```

### 2.3 事实检测层

**`TutorialFactBus.cs`**（静态）：本 Run 事实日志 + 广播。Run 级生命周期，换 Run 清空（见 §3.6 新 Run 感知）。

```csharp
public static class TutorialFactBus
{
    public static event Action<TutorialFact> OnFact;
    public static void Record(TutorialFact f);           // 幂等去重由消费侧做；计数类事实累加
    public static bool Has(TutorialFact f);
    public static int  Count(TutorialFact f);
    public static float FirstTime(TutorialFact f);       // 首次记录时刻（Telemetry 用）
    public static void ResetRun();
}
```

**事实枚举**（声明式谓词的词汇表）：

```csharp
public enum TutorialFact
{
    MovedWithAimChange,        // TUT-01：有效移动且 Aim 方向变化
    BasicAttempted,            // TUT-02 三槽：攻击尝试
    SkillAttempted,            //       （Q，附身怪技能）
    MobilityAttempted,         //       （Space，位移槽）
    EnemyCorpseProduced,       // TUT-03：Enemy Fatal 且产生合法 Corpse
    FirstPossessionDone,       // TUT-04：Possession 完成
    VoluntaryBodySwitch,       // TUT-05：主动 Leave 或主动转移
    SecondBodyAvailable,       // TUT-05 启动前提：存在下一合法 Body 机会
    DeathRelaySucceeded,       // TUT-06（M3 占位，战斗侧接入）
    SoulShrineRestored,        // TUT-07（M3 占位，战斗侧接入）
    PossessedMonsterPrefix,    // TUT-MONSTER-*：按 prefabId 参数化（见下）
}
```

**Probe 组**（各 1 个小 MonoBehaviour 或 Controller 内模块，统一在 Awake 订阅 / OnDestroy 退订，SceneSingleton 语义）：

| Probe | 事实源 | 说明 |
|---|---|---|
| `TutorialInputProbe` | `PlayerController.OnCommandProduced`（新增静态事件，见 §3.5） | 移动/Aim 变化/三槽 Pressed；只在"未被屏蔽且 timeScale>0"时产事实，与 Tick 自身守卫一致 |
| `TutorialPossessionProbe` | PossessionManager 三事件 + 新增结束原因事件（§3.3） | FirstPossessionDone / VoluntaryBodySwitch / Monster 微教学触发（prefabId 经 `MonsterPool.GetPrefabOf` 反查） |
| `TutorialWaveProbe` | `WaveManager.OnWaveEnemyKilled` / `OnWaveStarted` | EnemyCorpseProduced（仅正式击杀，DebugSkipWave 路径同事件但 IsFormalFlow 下不可达，可接受） |
| `TutorialCorpseTracker` | 低频轮询 `FindObjectsOfType<MonsterActor>`（0.2s，与 RunSession.SampleBodies 同模式） | SecondBodyAvailable；首个教学 Corpse 守护（延窗口/失效重武装）；目标失效检测 |

### 2.4 表现层

**`TutorialUI.cs`**：挂战斗场景 Canvas（与 UIManager 平级独立组件，UIManager 零改动）。
- Banner 构件：标题 + 一句说明 + 动态键位 Glyph + 可选轻量进度 + 完成反馈（短闪）+ 自动收起。默认无全屏弹窗/遮罩/立绘（Baseline §8.1）。
- Monster 微教学面板：Monster 名 + 一句核心玩法 + 最关键状态/资源；三槽详情"可展开"为后续增强，v1 不做展开。
- **冲突隐藏/恢复**（轮询，零他方改动）：`CoreChoiceUI.Instance != null && IsDrafting` → 隐藏；`Time.timeScale == 0f`（Pause/GameOver 冻结）→ 隐藏；恢复条件消失且 Step 仍 Active → 重新显示。满足 §9"Card/Pause 打开时暂时隐藏，关闭后恢复"。
- 音频：轻量提示音走 AudioManager 现有通道（可选字段 audioId，缺失不阻塞 Step，§8.3）；Voice 为 Production Open，v1 不接。

**`InputGlyphProvider.cs`**（静态）：

```csharp
public static class InputGlyphProvider
{
    public static string GetText(InputActionId action);   // "Q" / "Space" / "鼠标左键"
    public static Sprite GetIcon(InputActionId action);   // v1 返回 null（无图标资产），UI 回退文本
}
public enum InputActionId { Move, Basic, Possess, Skill, Mobility, BulletTime, Release }
```

数据源 = 新增 `GameInputBindings`（§3.5），**单一事实源**：PlayerController 读它执行，GlyphProvider 读它显示。"改键后提示显示实际绑定"在单源层面成立；真·运行时改键属 Input System 迁移技术债（§6）。

### 2.5 持久化层

**`TutorialProfileStore.cs`**（静态类，风格对齐 SaveCoordinator，但**边界独立**）：

```csharp
public static class TutorialProfileStore
{
    // 文件：persistentDataPath/possess_profile.json（与 Run 档分离）
    // 自带 profileVersion；版本不符→重置默认（Profile 数据非关键，不进 SaveMigrator 版本链）
    public static bool HintsEnabled { get; }                       // 教学提示开关（Settings 读写）
    public static bool IsStepCompleted(string stepId);
    public static bool HasPossessedMonster(string prefabId);
    public static void MarkStepCompleted(string stepId);           // 幂等；完成立即落盘（§10）
    public static void MarkMonsterPossessed(string prefabId);
    public static void SetHintsEnabled(bool enabled);
    public static void ResetTutorialRecords();                     // 只清教学记录，不动其他 Profile 进度（§11）
    public static event Action OnChanged;
}
```

**边界纪律**：Profile 不进 `SaveData`、不进 `SaveMigrator` 链、不被 `SaveCoordinator.DeleteSave` 删除（新游戏清 Run 档不清 Profile——跨 Run 持久化正是 Baseline 要求）；`RunSession.EndRun` 同样不碰。`MainMenuController` 的"开始新游戏"确认文案无需变化。

---

## 3. 与现有架构的接缝（文件级嵌入点）

> 原则：全部**加法改动**；不改既有方法签名；不改 `.vibe/doc`；场景/prefab 改动走 Editor 手工或后续实施任务，不在本方案执行。

### 3.1 WaveManager.cs — 阶段推进 + Wave 启动门

**改动 A：替换占位直通（137-143 行）**

```csharp
// 现状：
if (session.CurrentPhase == RunPhase.Opening)
{
    session.TransitionTo(RunPhase.Tutorial);
    session.TransitionTo(RunPhase.Waves);
}
// 改为：
if (session.CurrentPhase == RunPhase.Opening)
{
    session.TransitionTo(RunPhase.Tutorial);
    // 教学系统接管 Tutorial→Waves 的推进（准备段完成/超时/关闭后放行）；
    // 无 TutorialController 或教学关闭时保持原直通语义（零行为变化兜底）。
    if (TutorialController.Instance == null || !TutorialController.Instance.WillDrivePhase)
        session.TransitionTo(RunPhase.Waves);
}
```

**改动 B：WaveRoutine 首波前插门**（`WaveRoutine` grace 段之后、for 循环之前，约 6 行）：

```csharp
// 短阻塞 Wave 启动（TUT-01/02 BlockWaveStart）：门默认开；超时/关闭教学由 Controller 放行。
while (!TutorialController.WaveStartGateOpen) yield return null;
```

- 正式主战场使用 `WaveRoutine`，**一处改动直接作用于连续战场**。
- 门只对**新 Run 首波**关闭：`CompletedWaveIndex >= 0`（读档/后续波次）时 Controller 不关门。
- **风险**：低。门默认开，无教学场景零影响；`DebugSkipWave`/读档路径不经过门。需防死锁：Controller 侧超时强制放行 + 教学关闭立即放行（§4 硬指标）。

### 3.2 RunSession.cs — 阶段机与新 Run 感知

- `PhaseTransitions` 已有 `Opening→Tutorial→Waves` 合法边，**无需改表**。
- `LoadFromSave` 直置 Waves/Choice（230 行）——读档跳过准备段**结构性成立**，无需改动。
- **可选加法**：`BeginNewRun` 末尾发 `public static event Action OnRunBegin`（FactBus 清 Run、Controller 重新编排用）。**零改动备选**：Controller 每帧比对 `RunSession.RunId` 变化（BeginNewRun 必换新 runId）。推荐加事件（1 行声明 + 1 行触发），备选方案记录在案。

### 3.3 PossessionManager.cs — 主动换身语义（TUT-05）

**问题**（§0 证据）：主动脱离与附身死亡共用 `OnPossessionEnded`；直接转移无 End 事件。

**改动（加法，约 12 行）**：

```csharp
public enum PossessionEndReason { Voluntary, BodyDied, GameOver, Switch }
public event Action<MonsterActor, PossessionEndReason> OnPossessionEndedEx;
// CommitRelease(recycleBody, startCooldown, reason=Voluntary) 内补发；
// NotifyBodyDied 路径传 BodyDied；DetachCurrentBodyForSwitch 传 Switch（转移前旧体）。
```

- 旧事件保留不动，全项目零破坏；Probe 只订新事件。
- **TUT-05 判定**：`Voluntary`（主动 Leave）或"先 Switch 后 OnPossessionStarted 新体"（主动转移）→ `VoluntaryBodySwitch`。
- **风险**：中低——文件与战斗程序共享，改动面极小且纯加法；需提交时说明并在合并窗口协调。

### 3.4 MonsterActor.cs — 教学尸体保护

**改动（加法，约 5 行）**：

```csharp
/// <summary>延长附身窗口（教学尸体保护）：窗口延展期间不消散；到期后现有 CorpseLifecycleRoutine 自动恢复正常淡出。</summary>
public void ExtendPossessionWindow(float secondsFromNow)
{
    if (Body != BodyState.Downed || isPossessed) return;
    possessionWindowEndsAt = Mathf.Max(possessionWindowEndsAt, Time.time + secondsFromNow);
}
```

**兼容性论证**：
- `CorpseLifecycleRoutine` 的等待条件本就每帧检查 `possessionWindowEndsAt`——延长即保护，**到期自动恢复**（无永久安全区，满足 §7"保障失效"语义）。
- 被附身时 `OnPossessed` 停 corpseRoutine，保护自然终结；`MonsterPool.Return` 的 isPossessed 兜底不变；波次怪不随 Chunk 回收，保护期尸体不会被流送误收。
- **风险**：低。不改任何现有路径；房间模式静态怪已移除，教学 Corpse 必为波次怪。

### 3.5 PlayerController.cs — 键位单源化 + 命令流事件

**改动 A（新增文件 `Assets/Scripts/Core/Control/GameInputBindings.cs`）**：

```csharp
public static class GameInputBindings
{
    public const int BasicMouseButton = 0;
    public const int PossessMouseButton = 1;
    public const KeyCode Skill = KeyCode.Q;
    public const KeyCode Mobility = KeyCode.Space;
    public const KeyCode BulletTime = KeyCode.E;
    public const KeyCode Release = KeyCode.F;
    public const string MoveHorizontalAxis = "Horizontal";
    public const string MoveVerticalAxis = "Vertical";
}
```

**改动 B（Tick 内替换字面量为常量 + 尾部加事件，各 1-2 行）**：

```csharp
public static event Action<ControlCommand> OnCommandProduced;  // 输入屏蔽/timeScale 守卫之后触发
// Tick 末尾：OnCommandProduced?.Invoke(cmd);
```

- 行为零变化（同值替换）；事件在现有守卫之后发，Probe 天然只收"合法尝试"。
- **风险**：低——但属输入关键路径文件，改动需单独提交、单独验证（编译 + 手动跑一局确认操作无回归）。
- `PossessionHUD` 的硬编码键位文案**不在本期改**（最小改动纪律），记为技术债，待 GlyphProvider 落地后由其消费方逐步收敛。

### 3.6 SaveCoordinator / SaveData / SaveMigrator — 零改动

Profile 独立文件与版本号（§2.5）。**显式决策**：教学完成状态不进 Run 档——Run 档是"波间安全点"语义，教学完成是设备级跨 Run 记录，混入会污染 SaveMigrator 链式迁移纪律（SaveData 类头注释明确要求字段变更必须升版本）。读档后延迟教学照常工作：Controller 读 Profile 决定哪些 Step 仍需监听。

### 3.7 UIManager / SettingsPanel — 教学开关与重看入口

- `SettingsPanel` 加法字段：`Toggle tutorialHintsToggle` + `Button replayTutorialButton`；Show 时从 `TutorialProfileStore` 读初值，变更写回。主菜单与局内**共用同一组件**，一处改动两个入口（§11"Settings 中的教学提示开关"达成）。
- "重看基础操作"：`TutorialController.ReplayBasics()`——重置 TUT-01/02 的运行时状态重新显示，**不覆盖 Profile 完成记录**（§11）。
- UIManager 本体零改动。TutorialUI 独立挂 Canvas。
- **风险**：低；SettingsPanel 是 Shared UI 资产（prefab），改动需检查主菜单/局内两处实例的字段接线。

### 3.8 GameManager / 调试入口

- 无 GameManager 改动。`TutorialDebugPanel` 遵循既有先例：`#if UNITY_EDITOR || DEVELOPMENT_BUILD` + OnGUI + `GameManager.IsFormalFlow` 屏蔽 + F 键开关（建议 F8，避开 F5 跳波）。
- 面板能力（对齐 §13）：强制开始/完成指定 Step、跳下一 Step、重置 Run/Profile 教学记录、模拟首个 Corpse（`MonsterPool.Spawn` + 击杀或 `SpawnAsPermanentCorpse`）与 Possession（`PossessionManager.DebugForcePossess` 现成）、Death Relay / Shrine 的**事实注入**（`TutorialFactBus.Record(DeathRelaySucceeded)`，M3 验证管道用）、查看 Step 未开始/未完成原因与当前目标/保障状态、切换提示开关、显示 GlyphProvider 当前输出（验证实际绑定显示）。
- BootStrapper：ProfileStore 为静态懒加载，**可不进 Boot 序**；若评审要求显式初始化序，加一行 `TutorialProfileStore.EnsureLoaded()` 即可（加法）。

---

## 4. 数据驱动设计（TutorialConfig Schema）

### 4.1 Step 配置（对齐 Baseline §4 Step Data Contract）

```csharp
[Serializable]
public class TutorialStepConfig
{
    [Header("身份")]
    public string stepId;                     // "TUT-01"…"TUT-07" / "TUT-MONSTER-<prefabId>"
    public string textKey;                    // 文案 key（v1 内联 title/body 字段兜底，见 §6 文案开放项）
    public string title;
    [TextArea] public string body;            // 一句中性操作说明；{action:Skill} 占位符由 Glyph 替换
    public InputActionId primaryAction;       // 动态图标/当前绑定键来源
    public string audioId;                    // 可选轻量提示音

    [Header("条件（声明式事实谓词）")]
    public RunPhaseGate phaseGate;            // PrepOnly(准备段) / AnyPhase(贯穿 Run)
    public string prerequisiteStepId;         // 链式前置（可空）
    public FactPredicate startPredicate;      // 开始条件
    public FactPredicate completionPredicate; // 完成条件

    [Header("阻塞与节奏")]
    public BlockingMode blockingMode;         // NonBlocking / BlockTutorialChain / BlockWaveStart
    public float reminderInterval = 8f;       // 提醒（重显）间隔
    public TimeoutPolicy timeoutPolicy;       // KeepWaiting / ReleaseGate / Defer
    public float timeoutSeconds = 20f;        // 超时阈值（TUNABLE，Production Open）
    public InvalidTargetPolicy invalidTargetPolicy; // ReacquireNext / Defer（无第二 Body 时延后）

    [Header("恢复与持久化")]
    public PersistenceScope persistenceScope; // Profile（跨 Run）/ RunOnly
    public string nextStepId;                 // 显式后继（链序冗余校验用）
}

[Serializable]
public struct FactPredicate
{
    public TutorialFact[] requiredFacts;      // AND 语义；空=无条件
    public int requiredCount;                 // 计数阈值（默认 1）
}

public enum BlockingMode { NonBlocking, BlockTutorialChain, BlockWaveStart }
public enum TimeoutPolicy { KeepWaiting, ReleaseGate, Defer }
public enum InvalidTargetPolicy { ReacquireNext, Defer }
public enum RunPhaseGate { PrepOnly, AnyPhase }
public enum PersistenceScope { Profile, RunOnly }
```

### 4.2 谓词如何避免硬编码

- 完成条件 = `requiredFacts` 的 AND 集合，**事实由 Probe 产生、Step 只声明消费**。例：
  - TUT-01：`{ MovedWithAimChange }`，BlockWaveStart，timeout=ReleaseGate
  - TUT-02：`{ BasicAttempted, SkillAttempted, MobilityAttempted }`（三槽 AND），BlockWaveStart，timeout=ReleaseGate；"已提前使用可追溯"由 FactBus 历史查询天然满足
  - TUT-03：`{ EnemyCorpseProduced }`，NonBlocking
  - TUT-04：`{ FirstPossessionDone }`，BlockTutorialChain；startPredicate=`{ EnemyCorpseProduced }`
  - TUT-05：`{ VoluntaryBodySwitch }`，NonBlocking；startPredicate=`{ FirstPossessionDone, SecondBodyAvailable }`（无第二 Body 时 `SecondBodyAvailable` 缺位→延后，不发无效指令）
  - TUT-06/07：M3 事实占位，NonBlocking/Defer
  - TUT-MONSTER-*：参数化 Step——`monsterMicros` 列表驱动，完成即"提示展示完成"，Profile 记 prefabId
- **新增 Step 不改 Gameplay 代码**（验收项）：新事实=Probe 里 1 行 Record；新 Step=配置资产加一行。
- 故意不做：通用表达式 DSL/可视化节点（rules.md §4.2 不引入额外抽象；当前 7+1 步枚举谓词足够）。

### 4.3 失败/恢复语义到配置的映射（§10）

| Baseline 要求 | 机制 |
|---|---|
| 提醒间隔 | `reminderInterval`：Active 未完成时每 N 秒重显 Banner |
| 超时后继续等待/放行/延后 | `timeoutPolicy` 三态；TUT-01/02=ReleaseGate（放 Wave，Step 后台继续） |
| 目标失效重获取 | `invalidTargetPolicy=ReacquireNext`：CorpseTracker 监测守护目标失效→解除武装→等下一合法 Corpse |
| 玩家死亡后继续/延后/重启 | v1：玩家死亡=Run 终结（失败不续关），Step 不存半步，下 Run 重来——与现有 GameOver 语义一致 |
| 退出/读档恢复 | Phase=Waves/Choice → 准备段 Step 转后台 NonBlocking 继续；FactBus 重置 |
| UI 冲突恢复 | §2.4 轮询隐藏/恢复 |
| 教学关闭 | SetHintsEnabled(false)：隐藏 UI + Gate 立即放行 + 所有 BlockTutorialChain 视为通过 |

---

## 5. 分阶段实施计划

### M1 — 基建（独立于战斗逻辑，约 5.5 人日）

| # | 任务 | 产出/接缝 | 估时 |
|---|---|---|---|
| 1 | `GameInputBindings` + PlayerController 常量替换 + `OnCommandProduced` 事件 + `InputGlyphProvider` | §3.5 | 1.0d |
| 2 | `TutorialProfileStore`（JSON、独立版本、幂等写、ResetTutorialRecords）+ SettingsPanel 开关/重看按钮 | §2.5/§3.7 | 1.0d |
| 3 | `TutorialFactBus` + 4 Probe（Input/Possession/Wave/CorpseTracker，不含保护逻辑） | §2.3 | 1.5d |
| 4 | `TutorialConfig`/`TutorialStepConfig` Schema + `TutorialController` Step 状态机 + 幂等结算 + 追溯判定 + 提醒/超时 | §2.1/§2.2/§4 | 1.0d |
| 5 | `TutorialUI` Banner（显隐/自动收起/Card-Pause 冲突恢复/Glyph 文本） | §2.4 | 0.5d |
| 6 | `TutorialDebugPanel` 骨架（Step 列表/强制完成/开关/诊断） | §3.8 | 0.5d |

**M1 出口标准**：空 Step 配置下全量回归无影响（门默认开、UI 无显示、Profile 文件可读写）；Debug 面板可注入事实并看到 Step 状态迁移。

### M2 — TUT-01~05 + 框架（依赖 M1；约 5 人日）

| # | 任务 | 产出/接缝 | 估时 |
|---|---|---|---|
| 1 | WaveManager 接缝：直通替换 + WaveGate（含无教学兜底） | §3.1 | 0.5d |
| 2 | Opening 最小 Carrier 分配驱动：新 Run → 刷配置 Pride prefab（pride_new）→ `PossessionManager.BeginPossessionFlight(target)`（现成 public API，走完整校验 + 灵魂飞向目标 + 自动提交附身的正常附身流程——即玩家要求的"主角本身未附身，触发正常附身位移动画"）→ 玩家取得控制 → TUT-01 开始。**不**做 CG/演出（Production Open） | §3.1 + `openingCarrier` 配置 | 1.0d |
| 3 | MonsterActor `ExtendPossessionWindow` + CorpseTracker 保护/失效重武装 | §3.4 | 0.5d |
| 4 | PossessionManager 结束原因事件（`OnPossessionEndedEx`）+ TUT-05 判定 | §3.3 | 0.5d |
| 5 | TUT-01/02 Step 配置 + 门协同 + 超时放行 + 追溯验收 | §4.2 | 0.75d |
| 6 | TUT-03/04/05 Step 配置 + 尸体保护联动 + 无第二 Body 延后验收 | §4.2 | 0.75d |
| 7 | TUT-MONSTER-* 框架 + 1 个示例怪（微教学面板 + Profile 记录） | §2.2/§2.4 | 0.5d |
| 8 | Telemetry 最小埋点（show/complete/耗时/关闭 → Debug.Log + 可选 jsonl） + Debug 面板补全 + Baseline §14 验收走查 | §3.8 | 0.5d |

**M2 出口标准**（对齐 §14）：TUT-01/02 完成或超时正常进 Wave；提前操作不重复要求；教学 Corpse 提示期不消散；无第二 Body 时 TUT-05 不提示；关闭教学立即放行；改 GameInputBindings 常量后 Glyph 显示同步；Card/Pause 冲突后恢复；完成状态跨 Run 保持、读档跳过准备段但延迟教学仍工作；新增 Step 纯配置。

**分工隔离声明**：M1/M2 不触碰任何技能数值、怪物行为、死亡规则；对共享文件（PlayerController/PossessionManager/MonsterActor/WaveManager/SettingsPanel）的改动全部为加法、各自独立小提交，便于与战斗程序并行。

### M3 — TUT-06/07（被战斗玩法阻塞；本方案只交付接口契约，约 0.5d 现在 + 1.5d 解锁后）

**对战斗程序的接口依赖（契约请求，非设计其玩法）**：

| 需要的事实 | 建议来源（由战斗程序定夺） | Tutorial 侧消费 |
|---|---|---|
| 首次 Body Fatal 且存在合法 Corpse | 已有：`OnBodyDiedWhilePossessing` + CorpseTracker 查合法尸体 | TUT-06 startPredicate |
| Death Relay 成功（死亡后接力附身完成） | 战斗侧事件或状态（如 relay 完成时通知）；若复用"死亡后短时窗内 OnPossessionStarted"，Tutorial 可按"OnBodyDiedWhilePossessing 后 T 秒内 FirstPossessionDone"关联判定——**时机语义需战斗侧确认** | `DeathRelaySucceeded` |
| 进入 Soul（无合法 Corpse 的 Body Fatal） | 已有事件组合可判（BodyDied + CorpseTracker 无合法尸体） | TUT-07 startPredicate |
| Shrine 恢复 Body 完成 | 战斗侧 Shrine 交互完成事件（Shrine 玩法零实现） | `SoulShrineRestored` |

M3 解锁后工作：2 个 Step 配置 + Probe 接线 + Debug 面板真实模拟按钮 + 跨 Run 等待验收（TUT-06/07 允许跨多 Run，§10）。**现在交付**：FactBus 占位枚举 + Debug 事实注入入口 + 本契约表。

---

## 6. 风险与开放问题

### 三大风险

1. **WaveGate 与流程协同（BlockingMode 落地风险）**：门插在 `WaveRoutine` grace 后、首波前；若地图完成前 Carrier 分配未完成，战斗流程会在 Combat 态等待——需验收"门持 ≤ 超时上限（默认 20s 内强制放行）"且只对新 Run 首波生效。缓解：门默认开 + Controller 侧双保险（超时/关闭教学立即放行）+ 读档路径不经过。
2. **共享战斗文件的加法改动合并风险**：`PossessionManager`（结束原因事件）、`MonsterActor`（延窗 API）、`PlayerController`（键位单源+事件）与战斗程序并行修改同一批文件。缓解：改动面各 ≤12 行、纯加法、独立提交、先于战斗大改动合入；若战斗程序反对改 PossessionManager，备选为控制侧同帧关联判定（`OnPossessionEnded` 后同帧无 `OnBodyDiedWhilePossessing` 即主动）——可行但脆弱，仅作 fallback。
3. **旧 Input Manager 技术债 vs "改键后显示实际绑定"验收项**：当前无运行时改键能力，Glyph 单源化只能保证"显示=实际硬编码值"；若验收要求真·改键（新 Input System 迁移），工作量与风险均出本方案范围。建议：本期以单源化收口验收语义，同时在 Backlog 立 Input System 迁移项（GlyphProvider 的 `InputActionId` 抽象已为其预留接缝）。

### 其他风险

- **Profile 边界**：新 JSON 文件版本不符即重置（不进 SaveMigrator 链）——需在文档与代码注释显式声明，防后人把 Profile 字段塞进 SaveData。PlayerPrefs 备选（AudioSettingsManager 先例）被否：教学记录含列表结构且需 Debug 可读，JSON 文件更合适。
- **UI 冲突轮询方案的边角**：Card/Pause 无事件可订（CoreChoiceUI/UIManager 均无私有状态外露），轮询可能在 1 帧内出现显隐闪烁；可接受，若实测可见再请 UI 侧加事件。
- **教学尸体保护与资源滥用**：保护=延长窗口而非永久（§7"不得创建永久安全区"），到期自动恢复正常消散；保护时长为 Production Open 调参。
- **FindObjectsOfType 轮询开销**：CorpseTracker 0.2s 一次全场扫描，与 `RunSession.SampleBodies` 同量级；场上怪数十只规模可接受（rules.md §4.6：不在 Update 每帧做）。

### 开放问题（需 Owner/策划/战斗程序确认）

1. ~~**初始 Pride Carrier 资产**~~ **【已确认 2026-08-21】**：`openingCarrier.prefab` = `pride_new`；动画要求=主角开局不附身，用 `PossessionManager.BeginPossessionFlight` 触发正常附身飞行位移动画（非 DebugForcePossess 瞬移）。CG/演出不在本期。
2. ~~**三槽语义映射**~~ **【已确认 2026-08-21】**：TUT-02 按 `Mobility(Space) / Basic(LMB) / Skill(Q)` 映射（E=子弹时间、F=脱离不计入三槽）。
3. ~~**文案/本地化**~~ **【已确认 2026-08-21】**：不考虑多语言，v1 文案直接内联 TutorialConfig（textKey 留位不启用）。
4. **Death Relay / Soul Shrine 接口**：§5-M3 契约表需与战斗程序对齐事件名与时机语义（尤其"Relay 成功"的判定窗口）。**【已确认 2026-08-21】**：契约表需在实施时整理成正式交付物给战斗程序。
5. ~~**房间模式的去留**~~ **【已确认 2026-08-21】**：无房间模式是正式游戏玩法；Room 管线已退役，WaveGate 验收以连续战场为准。
6. ~~**Telemetry 形态**~~ **【已确认 2026-08-21】**：v1 用 Debug.Log + 本地 jsonl（不接后端）。

---

## 附：改动文件清单（实施时据此控范围）

**新增（全部在 `Assets/Scripts/Tutorial/`，除注明外）**：
`TutorialConfig.cs`、`TutorialStepConfig.cs`、`TutorialFact.cs`、`TutorialFactBus.cs`、`TutorialController.cs`、`Probes/TutorialInputProbe.cs`、`Probes/TutorialPossessionProbe.cs`、`Probes/TutorialWaveProbe.cs`、`Probes/TutorialCorpseTracker.cs`、`UI/TutorialUI.cs`、`UI/InputGlyphProvider.cs`、`TutorialProfileStore.cs`、`TutorialTelemetry.cs`、`Debug/TutorialDebugPanel.cs`、`Core/Control/GameInputBindings.cs`（放 Core/Control，与 PlayerController 同目录）。

**加法修改（不动既有签名）**：`PlayerController.cs`（常量替换+静态事件）、`WaveManager.cs`（直通替换+门等待）、`MonsterActor.cs`（ExtendPossessionWindow）、`PossessionManager.cs`（OnPossessionEndedEx）、`SettingsPanel.cs`（Toggle+重看按钮）、`RunSession.cs`（可选 OnRunBegin 事件）。

**零改动**：`SaveCoordinator.cs` / `SaveData.cs` / `SaveMigrator.cs` / `UIManager.cs` / `GameManager.cs` / `MonsterPool.cs` / `.vibe/**`。
