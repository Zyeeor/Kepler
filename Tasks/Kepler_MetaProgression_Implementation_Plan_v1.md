# 局外系统（Meta Progression）需求落地方案 v1

> 依据（行为已 CLOSED，实现待落地）：
> - `Docs/03_Decision_Log/DEC-CANON-20260821-006.md`（Approved，局外收敛决策）
> - `.vibe/doc/Canonical/Content/Meta_Progression_Systems_Baseline_v1.0.md`（冻结合同）
> - `.vibe/doc/Canonical/01_DESIGN_CANONICAL.md` §1、§29.1
> - `.vibe/doc/Canonical/00_CANONICAL_INDEX.md` §3 / §4 / §5
>
> 合同边界：行为/数据语义已冻结（CLOSED），但**精确阈值、权重、文案、存储引擎、网络协议、类结构为 OPEN**，须在本方案的 Task Contract / ADR 中自行决定。

---

## 0. 当前实现审计结论（差距总览）

| 需求条款 | 实现状态 | 证据 |
|---|---|---|
| §3 匿名身份（device playerId） | ✅ 已实现 | `Systems/Elite/DeviceIdentity.cs`（PlayerPrefs 设备级） |
| §5 荣誉殿堂（本地落盘 + 冻结 + 异步战绩 + 端到端接入） | ✅ 基本满足 | `HallOfFameStore.cs`（possess_hall_of_fame.json, schemaVersion=1, 身份 playerId+runId+sin, 滚动更新/Finalize/ApplyStats/永久保留）；`HallOfFamePanel.cs`（本地优先+刷新+4 排序+空态+文本目录+陈旧卡标记+F6 调试）；`EliteBuildDirector.cs:627` 调用 `UpsertFromSnapshots`；`RunStatsCollector.cs:210` 调用 `FinalizeRun`；`EliteNetClient.cs` 上传/选怪/回传 |
| §6.5 Elite 数据链路（在线快照优先 / Preset 本地兜底 / 跨 Sin 标准化 / bdData=CardID 列表） | ✅ 较完整 | `EliteNetClient` + `EliteBuildDirector` + `EliteBuildCarrier` + `EliteMonsterCatalog` + 服务端 `Server/internal/elite/*`（store/service/leaderboard/events/upload/pick/seed + sqlite） |
| §4 卡牌图鉴（三态 / 字段 / 分类 / 进度分母 / 主菜单入口 / 持久化） | ❌ 基本未实现 | 仅有 `Debug/CardProgressPanel.cs`（EDITOR/DEV 宏、`#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD`，仅本局已解锁、正式流程屏蔽、无持久化、无三态、无主菜单入口）；**无** `CardArchiveStore`/`ProfileStore` 三态数据 |
| §3 长期 Profile 存档抽象（与 Run 存档分离、统一） | ❌ 缺失 | `Save/SaveCoordinator.cs` 仅 Run 级 `possess_run_save.json`（schemaVersion=5）；Hall of Fame 自管 JSON 未纳入统一 Profile；Card Archive 无任何持久化 |
| §3 通用列表 / 状态组件（复用） | ❌ 缺失 | `HallOfFamePanel` 自建 UI，无共享 `ListPanel`/`StateBadge` 组件；新建图鉴将重复造轮子 |
| §3 内容版本兼容（meta 迁移 / 前向兼容） | ⚠️ 部分 | Hall of Fame 有 `schemaVersion` 但**未接入 SaveMigrator**，版本不符整体清空（非逐字段迁移）；图鉴无 |
| §3 局外入口导航 | ⚠️ 部分 | 仅荣誉殿堂有克隆设置按钮入口（`MainMenuController.EnsureHallOfFameEntry`）；图鉴无入口、无返回态管理复用 |
| §3 统一调试 | ⚠️ 部分 | 荣誉殿堂 F6 调试已存在；图鉴无调试入口 |
| UGC 平台 / Boss·long-term Meta | — 本期不开发 / DEFERRED | 与设计一致，无代码，无需处理 |

**核心差距一句话**：荣誉殿堂与 Elite 链路已端到端可用；**卡牌图鉴完全空白**，且两个系统之间**缺少统一的长期 Profile 抽象、通用 UI 组件与内容版本兼容层**。

---

## 1. 落地原则

1. **不提供任何战斗加成**（§1/§29.1）：局外系统只读展示，不得影响 Run 内数值。
2. **长期 Profile 与 Run 存档物理分离**：图鉴/荣誉殿堂走独立 `possess_meta_save.json`（或分文件），绝不与 `possess_run_save.json` 混用；Run 结束不清空 Profile。
3. **本地优先 + 离线兜底**：图鉴纯本地（无服务端源）；荣誉殿堂/Elite 网络失败降级本地。
4. **复用优先**：先建通用列表/状态/调试组件，荣誉殿堂与图鉴共用，避免重复。
5. **内容前向兼容**：meta 存储带 `schemaVersion`，升级时保留可保留字段（不整体清空），纳入统一迁移或自带迁移。
6. **合同 OPEN 项收敛**：阈值/权重/文案/存储引擎/网络协议/类结构在本方案 Task 内以 ADR 决议，不阻塞 Demo。

---

## 2. 任务拆分

### T0 — 共享基础设施（优先级最高，所有系统依赖）
- **T0.1 长期 Profile 存储抽象**
  - 新增 `Systems/Meta/MetaProfileStore.cs`：统一读写 `possess_meta_save.json`（schemaVersion=1），聚合 `CardArchive`（§4）与 `HallOfFame`（§5）数据，或采用「单 meta 文件 + 分段」模式。
  - 提供 `Load/Save/SafeMutate` API；损坏/版本不符时保留可保留字段（替换缺失段，不整体清库）。
  - 验收：Run 结束 / 新游戏（`SaveCoordinator.DeleteSave`）**不触碰** meta 文件；两系统数据跨 Run 持久。
- **T0.2 匿名身份复用**
  - 确认 `DeviceIdentity.cs` 已是唯一 playerId 源；图鉴/荣誉殿堂统一从此读取，**禁止新增** PlayerPrefs/设备号。
  - 验收：`DeviceIdentity.PlayerId` 单例唯一，重启不变。
- **T0.3 通用列表 / 状态组件**
  - 新增 `UI/Common/MetaListPanel.cs`（虚拟列表 + 排序键 + 空态 + 文本目录）+ `UI/Common/StateBadge.cs`（陈旧/新解锁/未知 角标）。
  - 验收：荣誉殿堂面板改造为复用 `MetaListPanel`（保留现有 4 排序/文本目录/陈旧标记行为），图鉴直接复用，无重复 UI 代码。
- **T0.4 统一调试入口**
  - 新增 `UI/Common/MetaDebug.cs` 或复用现有调试键：图鉴/荣誉殿堂均可注入模拟记录、强制刷新、显示 schemaVersion。
  - 验收：正式流程屏蔽，DEV/EDITOR 可见。

### T1 — 卡牌图鉴（最大缺口，从零建设）
- **T1.1 数据模型 `CardArchiveEntry`**
  - 字段（对齐 §4）：`cardId`、`state ∈ {Unknown, Known, Unlocked}`、`firstSeenAtUnix`、`firstUnlockedAtUnix`、`selectedCount`、`isNewUnread`、`lastSeenRunId`。
  - 验收：三态可独立演进；`Unknown` 仅存剪影、`Known` 显卡面+名+效果、`Unlocked` 加时间戳+次数。
- **T1.2 持久化与采集钩子**
  - 在 `CardManager.OnEffectUnlocked`（首次见卡=Known；确认解锁=Unlocked，`selectedCount++`）与「首次遭遇某卡」事件挂钩，写入 `MetaProfileStore`。
  - 验收：跨 Run 累计；禁用/删除卡不计入分母、不清空历史（§4 进度分母 = 当前有效 Card 总数）。
- **T1.3 分类与进度**
  - 七宗罪 + 通用 独立页签；进度条分母读「有效 Card 总数」（排除 disabled/deleted）。
  - 验收：分母随卡库更新，历史记录不丢。
- **T1.4 UI 面板 `CardArchivePanel`**
  - 复用 `MetaListPanel` + `StateBadge`；`isNewUnread` 进面板显新解锁红点，查看后清。
  - 验收：未知卡仅剪影、不泄露效果；已解锁可查看详情。
- **T1.5 主菜单入口**
  - 在 `MainMenuController` 新增图鉴按钮（同荣誉殿堂克隆风格），打开 `CardArchivePanel`、关闭时恢复主菜单（复用 `subPanelOpened` 机制）。
  - 验收：主菜单可见入口，Esc/关闭回主菜单。

### T2 — 荣誉殿堂收尾（基于已有实现补齐）
- **T2.1 内容版本兼容**：将 Hall of Fame 的 `schemaVersion` 检查改为「保留可保留字段的前向迁移」（与 `MetaProfileStore` T0.1 统一），消除「版本不符整体清空」风险。
- **T2.2 组件复用改造**：`HallOfFamePanel` 改为基于 `MetaListPanel`/`StateBadge`，行为不变（4 排序/空态/文本目录/陈旧标记/F6）。
- **T2.3 双源清晰度核对**：确认本地快照（`UpsertFromSnapshots`）与异步战果（`ApplyStats` 来自 `EliteNetClient`/服务端）写入不同字段、互不覆盖；永久保留且**独立于 Elite 候选库 FIFO**（§5 明确）。
- **T2.4 失败局保留**：验证 `RunStatsCollector.FinalizeRun(false,"Failed")` → `HallOfFameStore.FinalizeRun` 确实写入（§5.10）。

### T3 — Elite 数据链路验收（已较完整，列清单核对）
- **T3.1** 在线实时快照优先、离线/空 `Preset` 本地兜底（`EliteNetClient` + 服务端）端到端验证。
- **T3.2** 跨 Sin 标准化 Build 深度一致；`bdData` = Card ID 列表、`stack` 恒 1（§6.5）。
- **T3.3** 与荣誉殿堂的「他人战果回传 → `ApplyStats`」链路联调。
- 验收：客户端选怪/上传/回传三态在无网、弱网、服务端空库下均不崩、可降级。

### T4 — 局外流与入口导航
- **T4.1** 主菜单两入口（图鉴 + 荣誉殿堂）统一风格、统一返回态。
- **T4.2** 空态文案走 `TextCatalog`（与现有一致）。
- **T4.3** 正式流程下调试面板全屏蔽（沿用 `GameManager.IsFormalFlow`）。

---

## 3. 验收映射表（Baseline → 实现）

| Baseline | 验收点 | 对应任务 |
|---|---|---|
| §3 长期 Profile | 跨 Run 持久、与 Run 分离、损坏可恢复 | T0.1 |
| §3 匿名身份 | 单 device playerId，无重复源 | T0.2 |
| §3 通用列表/状态组件 | 两系统复用同一组件 | T0.3 |
| §3 统一调试 | DEV 可见、正式屏蔽 | T0.4 |
| §4 三态 | Unknown/Known/Unlocked 独立展示 | T1.1–T1.4 |
| §4 字段 | firstSeenAt/firstUnlockedAt/selectedCount/isNewUnread | T1.1–T1.2 |
| §4 分类+分母 | 七宗罪+通用页；分母=有效卡总数 | T1.3 |
| §4 入口 | 主菜单可达 | T1.5 |
| §5.2/§5.7/§5.10 | Run 结束冻结（含失败）、异步战绩应用 | T2.3–T2.4（已有，核对） |
| §5.8/§5.9 | 列表排序/空态/文本/陈旧标记 | T2.2 |
| §3 版本兼容 | meta 升级不丢数据 | T0.1/T2.1 |
| §6.5 | Elite 在线优先+Preset 兜底+标准化 | T3 |

---

## 4. 需在 ADR / Task Contract 中决议的 OPEN 项
- 精确文案（UI 标签、空态、新解锁提示）—— 走 `TextCatalog` 统一管理。
- 阈值/权重（如 `isNewUnread` 保留时长、排序默认键）。
- 存储引擎：单 `possess_meta_save.json` 聚合 vs 图鉴/荣誉分文件（倾向聚合 + 分段）。
- 网络协议：图鉴纯本地（**无服务端**，与 §3 网络要求区分）；Elite 沿用现有 HTTP。
- 类结构：`MetaProfileStore` / `CardArchiveEntry` / `MetaListPanel` / `StateBadge` 命名与目录。

## 5. 风险
- T1 工作量最大（从零），建议优先 T0 抽象 + T1 最小可用（三态+持久+入口），渲染细节后置。
- 避免把图鉴数据误写入 Run 存档（`SaveCoordinator`）——务必走 T0.1 的独立 meta 文件。
- 荣誉殿堂改造（T2.2）需保留现有行为，建议先补测试桩再重构 UI。

---

## 6. 执行情况（2026-08-26）

按"实现 → unity-dev 审查 → 改进 → 下一任务"循环推进，所有任务均经 unity-dev 复审。

| 任务 | 状态 | 说明 |
|---|---|---|
| T0 长期 Profile 抽象 | ✅ 完成 | `MetaProfileStore`（`possess_meta_save.json`，与 Run 存档物理分离、版本不符保留可恢复段）；`DeviceIdentity` 复用；进度分母持久化到 meta |
| T1 卡牌图鉴 | ✅ 完成 | `CardArchiveStore`（三态/字段/分类/分母缓存）+ `CardArchiveTracker`（订阅 CardManager 事件，对局内持续记录）+ `CardArchivePanel`（三态渲染/七宗罪+通用页签/进度/主菜单入口）；未知卡剪影不泄露 |
| T2 荣誉殿堂收尾 | ✅ 完成（T2.2 延后） | T2.1 版本守卫（前向兼容、保留条目）；T2.3 双源（本地快照+异步战果）已分离；T2.4 失败局冻结已满足（无需改代码）。T2.2 通用组件复用延后，避免 destabilize 已工作的荣誉殿堂 |
| T3 Elite 数据链路验收 | ✅ 通过 | §6.5 已完整实现：在线快照优先 + 离线 Preset 兜底 + 空候选空快照注入；bdData=Card ID 列表、stack 恒 1；五类战果回传仅服务器来源回报（源码核实 PASS） |
| T4 入口导航 | ✅ 通过 | 主菜单双入口（图鉴+荣誉殿堂）同风格可返回；调试面板受 `IsFormalFlow` 屏蔽 |

**审查修复记录**：
- 第 1 轮（T0+T1）：修复 H1 viewport 未拉伸（内容不可见）、H2 `_map` 未初始化 NRE、M1 主菜单误显+ESC、M2 分母为 0（持久化到 meta）；均经第 2 轮复审 PASS。
- T2 版本守卫 + onClose 跨场景悬空防御（调用后置空）。
- 终审（T3/T4+集成）：整体可交付，无高严重度；补齐 `CardArchivePanel.EnsureInstance` 单例查重与 Awake 防重（与 HallOfFamePanel 一致）。

**待办（非阻塞）**：T2.2 通用列表/状态组件复用（可选重构）；卡面文本接入 TextCatalog（当前用字面量，低优先级）。
