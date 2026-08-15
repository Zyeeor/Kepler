# 00_CANONICAL_INDEX — Repository-Ready v1.1

**Project:** Possession<br>
**Date:** 2026-08-15<br>
**Package Status:** `REPOSITORY-READY DESIGN CANONICAL v1.1`<br>
**Repository Fact Snapshot:** `EXTERNAL / REFRESH SEPARATELY`<br>
**Core boundary:** `Repository Fact ≠ Design Truth`

---

# 1. 本包是什么

本包是当前《Possession》中心策划案的**仓库设计真源**。

它用于：

- 给团队成员和 Agent 提供统一设计读取入口；
- 作为后续 Design Intake / Legacy Audit 的设计侧输入；
- 防止旧 Demo、旧 Excel、旧对话、旧模块文档或当前仓库实现反向覆盖已确认设计；
- 把仍需实机验证、仍未拍板和长期延期内容明确保留为 `PLAYABLE / TUNABLE / OPEN / DEFERRED`。

本包**不绑定某一个 Repository Snapshot SHA**。团队可在导入前或导入后重新做一次当前资产 / 工程盘点；盘点结果只更新“现在有什么”，不自动修改本设计真源。

---

# 2. Authority Order

出现冲突时，按以下顺序处理：

1. 最新明确的 Owner / 人类设计决策；
2. `01_DESIGN_CANONICAL.md`；
3. `02_CONTENT_CANONICAL.md`；
4. `03_PRESENTATION_CANONICAL.md`；
5. `Content/Card_System_Current_Truth_v1.1.md` 中未被 `02` 摘要覆盖的逐卡详细字段；
6. 明确的后续 Decision Log；
7. Repository implementation fact；
8. R03.1 / Monster Production Baseline / 资产盘点等已整合来源；
9. Legacy 文档、旧 Demo 行为、旧 Card / Monster / Room 内容、早期提案。

Repository 当前实现只能回答：

> **“现在有什么。”**

不能自动回答：

> **“最终应该是什么。”**

旧设计可以保留以便追溯和查询，但发生冲突时不得与当前 Canonical 并列作为设计 Authority。

---

# 3. 当前正式文件

## `01_DESIGN_CANONICAL.md`

`CANONICAL / RULES CLOSED`

负责核心Gameplay规则：高频换身、Body/Soul/Corpse、Combat、Bullet Time、Run/Wave/Final、Reverse-BD、Card系统合同、Infinite Terrain、Encounter、Elite、Tutorial、HUD与Result。

## `02_CONTENT_CANONICAL.md`

`CANONICAL / CURRENT CONTENT BASELINE`

负责七宗罪Roster、21个基础技能、82张Card注册表、Card作用域、Type Growth / Global Slot、固定起始傲慢Body、Encounter / Spawn、Elite、Terrain、Tutorial等Content基线。

## `03_PRESENTATION_CANONICAL.md`

`CANONICAL / CURRENT PRESENTATION BASELINE`

负责R03.1世界观中心整合、Mythic/System双线、User Distillation、A0–A4 Reveal、开场Carrier分配、First Clear、Seven Sins语法、UI/VFX/Audio/Telegraph与双线文本资产需求。

## `Content/Card_System_Current_Truth_v1.1.md`

`OWNER REVIEW PASSED / CURRENT CARD CONTENT TRUTH`

负责82张Card完整逐卡字段、明暗文案、机制、Stack、人工删除历史，以及当前Basic / Global / Type Growth规则。

## `Content/Encounter_CardOffer_Baseline_v1.0.md`

`BASELINE / TUNABLE / NOT PLAYABLE-VALIDATED`

负责当前首版Wave/Encounter节奏算法、投资权重、Soft Pity、Elite注入与9次Card Offer算法。用于程序第一版对接，不代表数值冻结。

## `Content/Dual_Line_Text_Requirements_v1.0.md`

`PRESENTATION PRODUCTION REQUIREMENT`

负责全项目需要Mythic/System双版本的文本资产清单、显示原则与A0–A4映射。

## `04_FINAL_CLOSURE_AUDIT.md`

记录本轮最终Owner Decision、五轴Closure与入仓Gate；它是审计证据，不高于01–03的设计真源。

## `REPOSITORY_IMPORT_MANIFEST.md`

提供部署侧建议目录、读取顺序与旧文档保留规则。

---

# 4. 当前完成状态

| 层 | 当前状态 | 判断 |
|---|---|---|
| Rules | `CLOSED` | 无核心规则阻塞 |
| Monster Core | `CARD-READY / CLOSED` | 七只Roster与三槽稳定 |
| Starting Carrier | `CONFIRMED` | 开场Soul被分配进傲慢Carrier；玩家首个可操作Body固定傲慢 |
| Card Content | `OWNER REVIEW PASSED` | 82张当前真源完成 |
| Card Timing | `CONFIRMED` | 删除Opening Card；W1–7后各1次，W8后连续2次，共9次 |
| Card Scope | `CONFIRMED` | Basic / Global作用普通Enemy + Possessed；Elite完全排除当前Run Card层 |
| World / Narrative | `R03.1 INTEGRATED` | 核心主题、Reveal、First Clear已收口；深层Ontology故意Open |
| Presentation Contracts | `BASELINE CLOSED` | 双线文本资产需求已扩大到全项目 |
| Encounter / Spawn | `BASELINE + TUNABLE` | 首版算法已给出，需接当前Spawner并Playable验证 |
| Elite Content | `CORE CONTRACT CLOSED / PRESET CONTENT OPEN` | Historical Build Snapshot已定；具体Fake Profile可后续补 |
| Exact Numbers | `TUNABLE / PLAYABLE` | 不阻塞Canonical |
| Boss / long-term Meta | `DEFERRED / OPEN` | 不阻塞当前Demo Canonical |

---

# 5. 本轮最新Owner Decisions

1. 首个可操作Body固定为**傲慢**；不随机、不选择。
2. 开场叙事上Candidate/Soul先处于无稳定Carrier状态，再通过动画/CG被分配进第一具傲慢尸体/Carrier；玩家获得控制时已经在傲慢Body中。
3. **Opening Card正式删除 / DEPRECATED**；不把它解释为Wave 0奖励。
4. 当前单Run Card选择为**9次**：Wave 1–7后各1次，Wave 8后连续2次。
5. 9张Basic Universal与7张Global Slot Card作用于本Run所有**普通Enemy + 普通Possessed Body**。
6. Elite无论敌人态或被玩家Possess后，都**不读取当前Run Basic / Global / Monster-Type / Type Growth**，只携带External Historical Build Snapshot并执行正常Possession初始化。
7. Type Growth取得后计入对应Sin `Investment +1`。
8. 9张Basic Universal全部`Stack Max = 1`。
9. Final约5分钟继续作为Playable测试Baseline，不冻结最终时长。
10. 全项目叙事型玩家文本需要Mythic/System两套资产；同一Gameplay事实保持“一事两译，机制唯一”，默认不同时并排显示。

---

# 6. Explicit Non-Goals

本包不执行：

- Legacy Audit；
- KEEP / REFACTOR / SALVAGE / RETIRE；
- 架构重构判断；
- 迁移方案；
- 代码复用结论；
- 最终PM；
- Issue / Task拆分；
- 实现完成度承诺；
- 所有Playable数值冻结。

---

# 7. 入仓之后

```text
Canonical Import
↓
Design Intake
↓
Legacy / Implementation Gap Audit
↓
Migration / Architecture Alignment
↓
Production PM / Issues / Tasks
↓
Implementation & Playable Validation
```

新一轮资产盘点可以在导入前完成，也可以作为Design Intake输入；不需要为了等待Repository Fact刷新而重新打开已关闭的中心设计。
