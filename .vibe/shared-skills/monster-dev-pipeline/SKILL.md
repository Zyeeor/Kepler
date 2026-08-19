---
name: monster-dev-pipeline
description: >-
  Possession 七宗罪怪物流水线：①Canonical→细分开发需求.md（找茬空隙由 AI 直接写入正文规格，
  §5 仅作供策划审阅的 AI裁定备忘，不再保留开放问题台账）；②按细分需求正文工程落地，
  不强制等策划确认；③用户说「提交xx怪物/所有怪物」时按怪物范围 commit&push 当前分支、
  合入 origin/main、再推 main。不依赖「项目全量表 - 怪物设计（新）.csv」。
---

# 怪物开发流水线（monster-dev-pipeline）

本 skill 是后续怪物开发的**唯一流程入口**。根目录旧的 `怪物技能配置指南.md` 视为历史副产物，**不要依赖它**；测试指南应在落地阶段按本 skill 生成/更新。

## Authority 与输入

本 skill 是 **Owner-approved Monster Development Workflow**，但不是独立的 Design Authority。

| 输入 | 路径 | 定位 |
|---|---|---|
| 项目规则 | `.vibe/rules.md` | 最高操作约束 |
| Canonical 索引 | `.vibe/doc/Canonical/00_CANONICAL_INDEX.md` | 正式 Design Authority 入口 |
| Design Canonical | `.vibe/doc/Canonical/01_DESIGN_CANONICAL.md` | 三槽合同、Possession、跨身生命周期等规则 |
| Content Canonical | `.vibe/doc/Canonical/02_CONTENT_CANONICAL.md` | **怪物策划主源**：Roster、21 基础技能、Card 注册表摘要 |
| Presentation Canonical | `.vibe/doc/Canonical/03_PRESENTATION_CANONICAL.md` | 视觉合同、Telegraph、VFX/表现基线 |
| Card 逐卡真源 | `.vibe/doc/Canonical/Content/Card_System_Current_Truth_v1.1.md` | 该怪 Monster-Type / Type Growth 逐卡字段 |
| 模块总览 | `.vibe/doc/项目设计.md` | Authority 与导航入口 |
| 怪物细分需求 | `.vibe/doc/Modules/Monsters/<中文名>.md` | Implementation Tracking（工程路径 / 资源 / 状态），**不是策划真源** |
| 参考实现（已完成范例） | `pride_new`、`sloth_new` | Repository implementation fact |

**明确排除为设计输入**（除非 Owner 当次明文点名，且仅作 Legacy Evidence）：

- `项目全量表 - 怪物设计（新）.csv`
- 根目录 / 仓库内其它怪物 Excel、旧 Modules 玩法文案、旧 Demo 行为

Authority compatibility：

- **策划需求只读 Canonical**；不得用 CSV / 旧 Module / 旧 Demo 补写或覆盖 Gameplay / Content / Presentation 规格；
- Prefab 路径、Ability 类、Effect ID、VFX、资源 / 开发 / 测试状态、Cheat 与 AI 配置继续写在细分需求.md，作为工程字段；
- 现有细分需求.md / 工程实现与 Canonical 的 Gameplay / Content Design 冲突时，立即 `STOP` 并报告；
- 冲突时 Canonical 胜；旧 CSV / Module / 实现仅保留为 Legacy Evidence，不得当作当前 Requirement。

输出：


| 输出          | 路径                                                  |
| ----------- | --------------------------------------------------- |
| 流程本文件       | `.vibe/shared-skills/monster-dev-pipeline/SKILL.md` |
| 每怪细分需求      | `.vibe/doc/Modules/Monsters/<中文名>.md`               |
| 每怪测试指南（落地后） | `.vibe/doc/Modules/Monsters/<中文名>-技能配置测试指南.md`      |


**禁止**：本 skill 触发的「生成细分需求」阶段不得改代码 / Prefab / `Packages/` / `ProjectSettings/`。  
**工程落地**阶段才允许改工程，且必须先读对应细分需求.md，遵守 `.vibe/rules.md`。

---



## 状态枚举（强制，全文统一）

每一项必须使用且仅使用下列状态（或表格允许的 `—`）：


| 状态        | 含义                                                                |
| --------- | ----------------------------------------------------------------- |
| `策划未明确`   | **仅**用于 Canonical **自相矛盾**，或细分需求 / 工程与 Canonical **硬冲突**且无法在不改 Authority 的前提下选边。此类项 **STOP**，不得落地，须 Owner 改 Canonical 或明确裁决。 |
| `待开发`     | Canonical 已写清，**或**找茬空隙已由 Agent 写入 `[AI裁定]` 可执行答案；工程尚未做 |
| `已开发但没资源` | 逻辑已可玩，**且该项声明了必需的正式美术资源**，但资源仍缺失 / 占位；可暂用占位，必须在资源栏写清占位名与「待替换」 |
| `已完成`     | 该项工程已落地；若声明了必需资源则正式资源也已挂上；**无资源依赖的纯逻辑项，代码就绪即 `已完成`** |
| `—`        | 不适用（参考旧 Prefab、纯文档字段、无工程交付物等） |

### AI裁定与策划审阅（强制；取代旧「开放问题」）

| 标记 | 含义 |
|---|---|
| `AI裁定` | Agent 在 Canonical 空隙处补全的默认可执行规格；**必须先写入细分需求正文**（行为规格 / 数值 / 资源 / Card 关系），阶段 B **只读正文** |
| `Owner确认` | Owner / 策划已追认正文中的某条 AI裁定；可择机回写 Canonical |
| `Canonical` | 该点已由 Canonical 唯一写清（或 Owner 已回写 Canonical）；备忘可标关闭 |

**正文优先原则（强制）**：

1. **禁止**再设「开放问题 / 待策划填写」台账；规格空隙不是问题清单，而是要被收口进正文的交付规格。
2. 找茬发现空隙 → Agent **立刻决定** → 把结论写进对应技能 / 卡 / 资源的**正文条款**（读者应能不看 §5 就落地）。
3. 同一结论再记入 **§5 AI裁定备忘**（供策划审阅）：写清「改了哪一项、AI 决定了什么、依据哪条宏观设计 / 世界观 / Canonical」。
4. AI 自行决定时必须参考：`01` 核心规则与跨身生命周期、`02` 该怪与 Roster 岗位、`03` Seven Sins / Mythic·System / Telegraph 表现合同、以及同类已落地怪先例；**不得**凭空玩法或回读 CSV。
5. **不**因策划未审阅 §5 而阻塞阶段 B。仅 `策划未明确`（硬冲突）阻塞落地。
6. `[AI裁定]` **不是** Design Authority；事后 Owner 可改 Canonical 覆盖正文；Agent 不得把裁定静默写进 Canonical。


### 资源依赖门禁（强制，防滥用 `已开发但没资源`）

`已开发但没资源` **只**用于「该项显式需要美术交付物」的情况。判定前先问：

> 没有正式模型 / VFX / 动画 / UI 资产，该项是否仍算设计与工程验收通过？

- **是（无资源依赖）** → 代码就绪即标 `已完成`。典型：英文 id、Cheat 条目、移速/血量数值、Possession 初始化、纯参数 Card（距离↑、移速↑、回血、斩杀阈值、Stack 行为分支）、无独立特效诉求的词条。
- **否（有资源依赖）** → 在该技能 / 词条下建**资源行**；逻辑就绪但资源占位 → 该**资源行**与**依赖它的技能/词条**标 `已开发但没资源`；资源正式挂上 → 再升 `已完成`。
- **禁止**：因同怪其它技能缺资源，而把无关的纯逻辑 Card / 基础属性 / Cheat 一并标成 `已开发但没资源`。
- **禁止**：技能未声明任何资源行时，仅因「手感还能再美化」就标 `已开发但没资源`；未声明的美化诉求应写入资源表或正文备注，并在 §5 备忘，而不是污染状态列。
- 词条找茬「需要独立特效吗？」若 AI裁定为否 → 该词条**无**资源行，落地后 `已完成`。

### 分项判定优先级

1. Canonical **硬冲突 / 自相矛盾** → `策划未明确`（STOP，唯一阻塞落地的设计状态）；写入专节「硬冲突」，**不要**放进 §5 AI裁定备忘冒充已收口
2. Canonical 有空隙 → Agent 将可执行结论**写入正文** + §5 备忘 → 项标 `待开发`（**可进阶段 B**）
3. Canonical 已写清（或仅差 TUNABLE 数值）、无代码 → `待开发`
4. 有代码，且该项（或其资源表）仍有未完成的**已声明必需资源** → `已开发但没资源`
5. 有代码，且无未完成的已声明必需资源 → `已完成`

**数值说明**：Canonical 将 Exact Numbers 标为 `TUNABLE / PLAYABLE` 时，**不**因此标 `策划未明确`，也**不**因此标 `已开发但没资源`；可落地首轮可玩数值并写入正文、备注 `TUNABLE`；若数值由 AI 首轮选定，同步记入 §5。

### 怪物级总状态

取各**技能槽**、**有交付物的词条/卡**、**已声明的关键资源行**的最差状态（`策划未明确` < `待开发` < `已开发但没资源` < `已完成`）。§5 AI裁定备忘**不**单独拉高总状态为 `策划未明确`。

- 纯文档字段（情绪文案等）与 `—` 行**不参与**最差聚合。
- 因此：小猫模型 / 巨口 VFX 仍缺时，怪物总状态可以是 `已开发但没资源`；但已落地的纯逻辑卡应仍为 `已完成`，不得被总状态反向改写。

---



## 阶段总览

```text
读取相关 Canonical（怪物 / 卡牌 / 规则 / 表现）
    │
    ▼
【阶段 A】按本 skill 生成/更新 细分开发需求.md
    │  （找茬→结论写入正文；§5 仅备忘供策划审阅；资源占位、状态）
    │  仅 Canonical 硬冲突保留「策划未明确」；其余空隙不阻塞
    ▼
【阶段 B】按细分需求.md**正文规格**工程落地
    │  （脚本 / Effect / Tag / Card / Prefab / Cheat；§5 不参与执行）
    ▼
生成/更新 《技能配置测试指南》并 Play Mode 验收
    │
    ▼
【阶段 C】用户说「提交xx怪物 / 提交所有怪物」
         commit&push 当前分支 → 合入 origin/main → push main
```

用户日常用法：

1. Canonical 有怪物/卡牌改动 → 对 Agent 说：「按 monster-dev-pipeline 阶段 A，更新某某怪物细分需求」
2. 阶段 A 完成后即可：「按阶段 B，落地某某怪物」（**不**等策划审阅 §5；仅存在硬冲突 `策划未明确` 时先停）
3. 只补资源 → 「按细分需求把某某**已声明**资源行从已开发但没资源改为已完成」（无资源依赖项不要标此状态）
4. 要入库 → 「提交傲慢」/「提交怠惰-机械之灵」/「提交所有怪物」（走阶段 C）
5. Owner 事后追认 / 改 Canonical → 刷新细分需求：更新正文，并把 §5 对应行改为 `Owner确认` / `Canonical`，再按需改工程

---



# 阶段 A — Canonical → 细分开发需求.md



## A.0 何时执行

- 新建怪物（Canonical Roster 新增或首次落地）
- 相关 Canonical 有任何改动（尤其 `02` 技能 / Roster、`Content/Card_System_Current_Truth_*`、`01` 三槽与生命周期、`03` 表现合同）
- 用户要求「按流程 skill 刷新细分需求」



## A.1 必读

1. `.vibe/rules.md`
2. 本 skill
3. `.vibe/doc/项目设计.md`
4. `.vibe/doc/Canonical/00_CANONICAL_INDEX.md`（确认 Authority Order 与本轮 Owner Decisions）
5. `.vibe/doc/Canonical/01_DESIGN_CANONICAL.md`（至少：输入与三槽、Possession 初始化、跨身生命周期、与当前怪相关的 Interaction）
6. `.vibe/doc/Canonical/02_CONTENT_CANONICAL.md`：
   - §2 基础 Monster Roster（该怪行）
   - §3 该怪三槽基础技能 + Enemy 行为基线
   - §4 Possession 初始化（若影响该怪）
   - §5–6 该怪 Card 注册表摘要
7. `.vibe/doc/Canonical/Content/Card_System_Current_Truth_v1.1.md` 中该怪全部逐卡字段（含已删除卡：确认不得补回）
8. `.vibe/doc/Canonical/03_PRESENTATION_CANONICAL.md` 中 Monster 视觉合同、Telegraph、与该怪相关的表现条款
9. 若已存在 `.vibe/doc/Modules/Monsters/<怪物>.md`，将其作为 Implementation Tracking **增量更新**，不要无故抹掉已填的资源名 / 工程路径 / 验收记录；但其中与 Canonical 冲突的**玩法文案必须按 Canonical 改正**，不得保留 CSV/旧文案为“当前需求”

若现有 Module / 工程实现的 Gameplay 或 Content 规格与 Canonical **硬冲突**，停止把旧规格当当前 Requirement，列出冲突双方并标 `策划未明确`，等待 Owner；不得自行选择旧版本，也不得回读 CSV 裁决。Canonical **仅有空隙、无硬冲突**时，走 A.3 `[AI裁定]`，不要当成硬冲突 STOP。


## A.1.1 Canonical 变更后的分项对账（强制）

Canonical 会改。阶段 A **不是**整表重标状态，也**不是**无脑保留旧状态。对每一项（技能槽 / 卡 / 资源行 / 工程标识）必须先做 **Delta 分类**，再套「分项判定优先级」。

### 对账输入（缺一不可）

1. **新 Canonical**：该怪当前正式规格（`02` + Card Truth + 相关 `01`/`03`）
2. **旧细分需求.md**：上一版 Implementation Tracking（含状态、工程路径、§5 AI裁定备忘）
3. **工程事实**（若仓库已有实现）：Ability / Prefab / CardLibrary / Effect 是否仍存在、行为是否仍匹配新规格（读代码 / 资产，不靠记忆）

### Delta 四类

| 类 | 含义 | 文档动作 | 状态如何确认 |
|---|---|---|---|
| **Unchanged** | 新 Canonical 与旧细分需求中的**行为规格**一致（允许 TUNABLE 数值不同） | 保留工程路径、资源名、验收记录；文案可按 Canonical 轻微润色 | **保留**原状态，除非工程事实已变（例如资源已挂上 → 可升 `已完成`；代码被删 → 降为 `待开发`） |
| **Changed** | 同一稳定键（见下）仍存在，但行为规则 / 目标 / 生命周期 / 卡机制等**语义变了** | 用 Canonical **覆盖**玩法文案；旧规格移入 Appendix（标「被 Canonical 取代」）；工程路径先保留作对照 | **不得沿用**旧的 `已完成` / `已开发但没资源`。重新判定：规格已清且代码**尚未按新规改** → `待开发`；代码已按新规改完 → 再按资源门禁判 `已完成` 或 `已开发但没资源`；新规空隙 → **写入正文** + §5 备忘后标 `待开发`；新规硬冲突 → `策划未明确` |
| **Added** | Canonical 有、旧细分需求无（新技能条款、新卡、新资源诉求） | **新建行** | 规格空隙 → **写入正文** + §5 备忘后标 `待开发`；规格清且无工程 → `待开发`；硬冲突 → `策划未明确`；禁止写成 `已完成` |
| **Removed** | 旧细分需求有、Canonical 已删或退役（含 Card Truth 人工删除） | **移出**当前 §2–§4；写入 Appendix Legacy，注明不得实现 / 须清理工程残留 | **不再占用**当前状态列，**不参与**怪物总状态聚合。若工程仍残留 → 检查清单增加「清理项」`待开发`，不是继续当 Requirement |

### 稳定键（用来判断是同一项还是新增/删除）

| 对象 | 稳定键（优先） | 备注 |
|---|---|---|
| 技能槽 | `Space` / `LMB` / `Q` / `Passive`（三槽合同） | 技能**显示名**可变；换槽或拆合技能视为 Changed/Added/Removed 组合，勿只改名糊弄 |
| 卡 / 词条 | Canonical `CardId`（如 `GL-A01`、`LU-TG01`） | 禁止用旧工程 `effectId` 当 Authority；映射关系可留在 Legacy Mapping 表 |
| 资源行 | `技能或卡稳定键` + `资源用途` | 用途文案微调仍算 Unchanged；用途本质变了算 Changed |
| 工程标识 | 英文 id / 正式 Prefab 路径 | 一般 Unchanged；换 prefab 名算 Changed |

### 状态确认禁令

- **禁止**因「整怪刷新」把所有行刷成同一状态。
- **禁止** Canonical 已 Changed 仍保留 `已完成`（除非已核对工程**符合新规**）。
- **禁止**把 Removed 项留在 §4 当前牌表「待开发」充数。
- **禁止**用旧 Module / 旧实现推断新项为 `已完成`。
- Unchanged 项若只改了 TUNABLE 数值建议值 → **不**因此降状态；在备注写新初值即可。

### 阶段 A 输出时必须附带的 Diff 摘要

刷新结束时，在该怪 md 顶部或 README 变更说明中用短表列出本轮：

```text
| 稳定键 | Delta | 旧状态 → 新状态 | 一句话原因 |
```

至少覆盖：Changed / Added / Removed；Unchanged 且状态有升降的也要写。无变更可写「本轮无 Delta」。

人类若对某条 Changed 的工程是否已对齐有争议 → **STOP** 该条，保持 `待开发` 或标冲突，等待确认后再升状态。


## A.2 解析 Canonical 规则（取代旧 CSV 宽表）

策划字段**只**从 Canonical 抽取。按目标怪（Sin / 中文名）组装一块「怪物内容包」：

| 字段 | Canonical 来源 |
|---|---|
| 显示名 / Sin / Action Grammar / 战斗岗位 / Possessed 快速价值 | `02` §2 Roster 行 |
| 三槽技能名 + 行为规格 | `02` §3 该怪 `Movement` / `Attack` / `Special` 条目 |
| Enemy AI 行为基线 | `02` §3 该怪「Enemy行为基线」 |
| 跨身 / Possession / 三槽合同 | `01` 相关章节 + `02` §4 |
| 词条 / 卡牌 Build | `02` Card 注册表 + `Content/Card_System_Current_Truth_v1.1.md` 逐卡 |
| 表现 / Telegraph / 资源诉求 | `03` Monster 视觉合同、Telegraph、VFX 最低语言；以及技能文案中已写明的前兆 / 危险区等 |

抽取约束：

- Roster 未列出的基础怪 → **不生成**；本 Demo Canonical 不新增第 8 只基础怪；
- 三槽缺任一槽正式规格 → **不得**用旧 CSV / 旧 Module 补全；Agent 写最小可玩基线**直接进正文行为规格**，并在 §5 备忘，该项标 `待开发`（可进 B）；仅当与其它 Canonical 条款硬冲突时才标 `策划未明确`；
- Card 以 Canonical 注册表与 Current Truth 为准；人工删除卡**不保留、不改造、不为覆盖率补回**；
- Exact Numbers 未给出 → 在正文填首轮可玩值并标 `TUNABLE`，可进阶段 B，**不要**为此阻塞或回读 CSV；若该首轮值由 AI 选定，记入 §5；
- 行为规则**硬冲突** → `策划未明确` + STOP；行为规则**缺失 / 歧义** → **写入正文可执行规格** + §5 备忘，项标 `待开发`，**允许进 B**（不得冒充 Canonical，不得回写 CSV）。

命名建议（工程英文 id，写入细分需求「工程标识」）：


| 中文      | 建议 id / prefab              |
| ------- | --------------------------- |
| 主角-灵魂体  | `soul` / 现有灵魂 prefab（以工程为准） |
| 傲慢-终刃绝影 | `pride` / `pride_new`       |
| 怠惰-机械之灵 | `sloth` / `sloth_new`       |
| 暴食-魔猫   | `gluttony` / `gluttony_new` |
| 嫉妒-激光异形 | `envy` / `envy_new`         |
| 愤怒-链狱冥兽 | `wrath` / `wrath_new`       |
| 贪婪-万手藏主 | `greed` / `greed_new`       |
| 色欲-灵念师  | `lust` / `lust_new`         |


旧 `*(full)` Prefab **只作参考，禁止直接改成正式交付**；新怪一律 `*_new`。

## A.3 「找茬」强制清单（每技能 / 每词条都要过）

Agent 必须主动对每一项提问，并**当场收口进正文**，不得把空答案甩给策划，也不得只在附录留问题。

### 找茬执行协议（强制）

对清单中每一问：

1. **Canonical 已唯一写清** → 直接写入行为规格；§5 不必重复（除非需要标注「已由 Canonical 关闭」）。
2. **Canonical 有空隙 / 多解但无硬冲突** →  
   - **立刻**给出默认可执行结论；  
   - **先写入**对应技能 / 卡 / 资源的**正文**（行为规格、数值、资源用途等），使正文可独立落地；  
   - **再**在 §5 AI裁定备忘追加一行：涉及项、结论摘要、Canonical / 世界观依据；  
   - 分项状态用 `待开发`（或后续工程状态），**不要**标 `策划未明确`。  
3. **Canonical 自相矛盾，或与既有 Canonical 条款硬冲突** → 标 `策划未明确`，在「硬冲突」专节写清冲突双方原文，**STOP** 该项，不得用 AI裁定强行选边。  
4. **禁止**：保留「开放问题」空表；禁止「等策划填写」作为阶段 A/B 完成条件；禁止只在 §5 写答案却不改正文。  
5. AI裁定**不是** Design Authority；事后 Owner 可改 Canonical 覆盖正文；Agent 不得把裁定静默写进 Canonical。

### AI裁定依据（强制参考顺序）

补全空隙时按此顺序取精神，后者不得推翻前者已写清的条款：

1. `01_DESIGN_CANONICAL`：三槽合同、Possession、跨身生命周期、敌我同源  
2. `02_CONTENT_CANONICAL`：该怪 Roster 岗位 / Action Grammar、三槽与 Enemy 基线、Card 注册表  
3. `03_PRESENTATION_CANONICAL`：Seven Sins 语法、Mythic/System 双线、Telegraph / VFX 最低可读性  
4. 同类已落地怪（如 `pride_new` / `sloth_new`）的工程惯例  
5. 仅当以上皆无唯一解时，选最小可玩、最少新机制的默认

至少覆盖：

### 通用

- [ ] 触发输入：左键 / Space / Q / 被动？按住还是点按？松手结算还是按下即放？（对照 `01` 三槽合同 + `02` 技能条文）
- [ ] 目标：最近 / 瞄准方向 / 最高血量 / 范围内全部 / 无目标是否可放？
- [ ] 数值：伤害、CD、耗血、时长、半径、段数 — 缺精确值在正文标 `TUNABLE` 并给首轮值；缺行为规则写入正文 + §5
- [ ] 与位移/飞行/变身叠加：能否打断？共享 CD？
- [ ] 附身 vs AI：行为是否一致？（对照 `02` Enemy 行为基线）召唤物跟谁？
- [ ] 死亡/离身/换身：被动与残留物按 `01` 跨身生命周期 + 该怪 `02` 条文；`02` 未写明则写入正文默认并 §5 备忘（参考同类怪先例）
- [ ] Tag / effectId 命名是否已定？未定则按 B.2 写入正文
- [ ] Telegraph：高危险动作是否满足 `03` 方向/范围/时机/落点要求？


### Movement

- [ ] 纯位移还是位移+伤害 / 派生物？
- [ ] 前摇 / 无敌 / 不可选中？（Canonical 写明「不基础无敌」则禁止擅自加）
- [ ] 落点判定、撞墙、撞敌？


### Attack

- [ ] 近战盒 / 弹道 / 持续射线 / 地面危险区？
- [ ] 蓄力曲线？最小/最大档？（Canonical 有蓄力语义则必须问清档位是否 TUNABLE；空隙写入正文）
- [ ] 命中特效挂自己还是挂敌人？


### Special / 召唤 / 残留物

- [ ] 召唤物寿命到期是「直接销毁」还是「先死亡表现再销毁」？
- [ ] 召唤物死亡：爆炸？回血？变残影？
- [ ] 主人死亡 / 换身后召唤物：立刻死 / 继续活 / 按自身寿命？（优先信 `02`；未写明则写入正文默认，参考同类先例）
- [ ] 场上数量上限？


### 词条（Card）

- [ ] 是改参数（`abilityParameters`）还是改行为分支（`IsUpgradeUnlocked`）？
- [ ] 与其它词条是否叠加？互斥？Stack Max 以 Card Truth 为准
- [ ] 需要独立特效吗？（否 → `NONE`/`REUSE`；是 → 资源行 + 用途；结论进正文）
- [ ] 已删除卡是否仍残留在工程 / 细分需求中？（有则列入清理项，不得当需求）


### 资源

- [ ] 仅当该项**确实需要**独立美术交付物时，才拆资源行（勿为空美化义务造行）
- [ ] 基础技能必须填写稳定 `ResourceSetId`；格式建议 `RS_MON_<SIN>_<SLOT>_<ABILITY>`
- [ ] 每个资源行必须有：`对应项`、`ResourceSetId`、`资源名称`、`类型`、`状态`、`当前资源事实 / 路径`、`资源用途`、`具体设计需求`
- [ ] `资源用途`回答“为什么需要”；`具体设计需求`至少写明触发时机、表现对象、生命周期 / 清理时机、必须传达的信息与禁止误读项
- [ ] 阶段 A 不得再留空资源名。若正式资产尚不存在，先填写稳定候选名并标`待开发`；命名应让AI / 程序可从名称判断所属怪物、槽位 / Card与用途
- [ ] Card资源关系必须明确写 `NEW` / `REUSE` / `NONE`：`NEW`需独立资源，`REUSE`必须指出复用对象，`NONE`必须说明纯逻辑且不得额外改变原表现
- [ ] 阶段 B 后：已声明资源仍占位 → **仅该资源行**（及直接依赖它的技能 / 词条）=`已开发但没资源`；无资源行的纯逻辑项不得套此状态
- [ ] 找茬“需要独立特效吗？”AI裁定答否 → Card行填写`NONE`或`REUSE`，词条逻辑落地后直接`已完成`



## A.4 输出文件模板（必须按此结构）

路径：`.vibe/doc/Modules/Monsters/<怪物中文名>.md`

```markdown
# <怪物中文名> — 细分开发需求

> 来源：`.vibe/doc/Canonical/`（`02_CONTENT_CANONICAL.md` 该怪章节 + 相关 `01`/`03` + `Content/Card_System_Current_Truth_v1.1.md`）  
> 生成/更新：按 `.vibe/shared-skills/monster-dev-pipeline/SKILL.md` 阶段 A  
> 怪物总状态：`<状态>`  
> 不作为来源：`项目全量表 - 怪物设计（新）.csv` 及旧 Modules 玩法文案

## 0. 工程标识

| 项 | 值 | 状态 |
|---|---|---|
| 英文 id | `pride` | |
| 正式 Prefab | `Assets/Prefabs/Monster/pride_new.prefab` | |
| 参考旧 Prefab | `pride(full)`（只读） | |
| Cheat 显示名 / 热键 | | |
| 模型资源 | | |

## 1. 基础属性

| 项 | Canonical / TUNABLE 值 | 工程字段 | 状态 | 备注 |
|---|---|---|---|---|
| 移速 | | MonsterActor / Enemy moveSpeed | | TUNABLE 可填首轮值 |
| 血量 / 玩家耐久相关 | | maxHealth 等 | | 对照 `01` Body 耐久合同 |
| Action Grammar / 岗位 / 快速价值 | | — | | 来自 `02` Roster |

## 2. 技能总表

| 槽位 | 技能名 | ResourceSetId | Ability 类（建议/已有） | abilityTags | CD | 耗血 | 伤害 | 状态 |
|---|---|---|---|---|---|---|---|---|
| Space / Movement | | `RS_MON_<SIN>_MV_<ABILITY>` | | Ability.Monster.<Sin>.<Skill> | TUNABLE | | | |
| LMB / Attack | | `RS_MON_<SIN>_AT_<ABILITY>` | | | | | | |
| Q / Special | | `RS_MON_<SIN>_SP_<ABILITY>` | | | | | | |
| Passive | | `NONE`或稳定资源集 | | | | | | |

## 3. 技能明细

### 3.x <槽位> — <技能名>

- **状态**：
- **Canonical 原文**（摘录 `02` 要点，勿抄 CSV）：
- **行为规格**（点按/蓄力/目标/段数/无敌等；**含 AI 已收口的默认**，读者不看 §5 也能落地）：
- **数值**（缺省标 TUNABLE 并给首轮值）：
- **Enemy 行为基线**（若适用）：
- **Ability 脚本**：
- **自身 Effect / 命中 Effect**：
- **Telegraph / 表现诉求**（对照 `03`）：
- **资源表**：

| 对应项 | ResourceSetId | 资源名称 | 类型 | 状态 | 当前资源事实 / 路径 | 资源用途 | 具体设计需求 |
|---|---|---|---|---|---|---|---|
| <槽位> | `RS_MON_<SIN>_<SLOT>_<ABILITY>` | `VFX_MON_<SIN>_<ABILITY>_<PURPOSE>` | VFX | 待开发 | 字段为空 / 占位路径 / 已挂路径 | 为什么需要该资源 | 触发于…；挂在…；持续至…；必须传达…；不得误读为… |

> **禁止**在技能下再挂「开放问题 / 待策划填写」列表。空隙结论写进上方行为规格与数值。

### 词条 / Card资源需求

| CardId | 名称 | 资源关系 | 资源名称 | 资源状态 | 资源用途 | 具体设计需求 |
|---|---|---|---|---|---|---|
| | | `NEW` / `REUSE` / `NONE` | 独立资源名 / 复用的ResourceSetId / `NONE` | | 为什么需要或为何不新增 | 触发、对象、生命周期、必须传达与禁止项 |

## 4. 卡牌 / Catalog 清单

| CardId / effectId | targetAbilityTags | 参数 | CardLibrary | upgrades 槽 | 资源关系 | 资源名称 | 状态 |
|---|---|---|---|---|---|---|---|

## 5. AI裁定备忘（供策划审阅；非开放问题）

> 下列结论**已经写入**上文正文；阶段 B **只执行正文**。  
> 本表仅供策划事后审阅或改 Canonical 覆盖，**不是**待填写问题清单。

| ID | 涉及项 | AI写入正文的结论摘要 | 主要依据 |
|---|---|---|---|
| A1 | Space 续飞 | 单次 1s 后强制落地，无续飞轴 | `02` 飞行条款；`01` 最小可玩 |

> 无 AI 补全时可写「本轮无 AI裁定（正文均来自 Canonical）」。  
> Owner 追认后把依据列改为 `Owner确认` / 回写后标 `Canonical`。

### 硬冲突（仅当存在；否则整节删除）

| ID | 冲突双方原文 | 影响项 | 状态 |
|---|---|---|---|
| H1 | … vs … | | `策划未明确` |

## 6. 工程落地检查（阶段 B 勾选）

- [ ] Ability 脚本
- [ ] Effect 资产 + GameplayTagCatalog
- [ ] Prefab 三槽 + hpCost
- [ ] CardLibrary + upgrades
- [ ] MonsterCheatCatalog / MonsterAIConfig
- [ ] 测试指南已生成
- [ ] Play Mode 验收通过
```



## A.5 阶段 A 完成标准

- Canonical Roster 中目标怪（或「所有怪物」范围内的每只）都有对应 md（或已声明跳过原因）
- 已按 **A.1.1** 完成 Delta 对账；Changed / Added / Removed 有 Diff 摘要
- Removed 项不在当前 §2–§4；只在 Appendix / 清理清单
- 行为规则**硬冲突** → `策划未明确` + 硬冲突专节 STOP；行为规则**空隙** → **正文已含可执行规格** + §5 备忘，项标 `待开发`（可进 B）；纯数值空缺 → 正文 `TUNABLE` 首轮值，不阻塞
- 有设计无工程 → `待开发`；Changed 且工程未跟新规 → 不得保留旧 `已完成`
- **无**「开放问题 / 待策划填写」空表；找茬每问均已收口进正文
- 21个基础技能（或当前目标怪三槽）均有稳定`ResourceSetId`；每个资源行都包含用途与具体设计需求，不得残留“待填”资源名
- 当前Card逐项标明`NEW / REUSE / NONE`；已删除Card不得进入当前资源表
- 更新 `.vibe/doc/Modules/Monsters/README.md` 总表状态
- **不写代码**
- **未**把 CSV / 旧 Module 玩法文案写回为当前需求
- **未**把 AI裁定静默写入 Canonical



## A.6 范例对照（用来校准「找茬」深度）

傲慢 / 怠惰已证明必须提前问清的典型坑：


| 坑        | 正确问法                | 正确收口（写入正文 + §5 备忘） |
| -------- | ------------------- | --------------------- |
| 落地爆炸特效挂谁 | 挂自己还是挂每个敌人？         | 怠惰：伤害 AoE，VFX 挂自己     |
| 地雷何时生成   | 起飞前还是落地后？           | 怠惰：起飞前、起飞点            |
| 散射何时触发   | 超时爆炸也散射吗？           | 怠惰：仅主弹命中敌人；碎片忽略首目标    |
| 召唤物寿命到   | 直接 Destroy 还是先「死亡」？ | 怠惰死亡炸：先脱离跟随再俯冲，炸完再销毁  |
| 主人死后召唤物  | 活？死？继续攻击？           | 优先信 `02`（木灵换身后按寿命继续）；未写明 → 正文默认按自身寿命继续，§5 备忘 |
| 旧词条过时    | 工程里还有但 Canonical 已删？ | 以 Canonical / Card Truth 为准；旧卡与旧实现保留为 Legacy Evidence，不得当需求 |


---



# 阶段 B — 细分需求.md → 工程落地

> Stage B 的既有 Unity Monster Engineering 步骤完整保留。进入本阶段前，设计输入必须已经通过 Stage A Authority Compatibility 检查（Canonical 为唯一策划真源）。



## B.0 前置门禁

1. 读 `.vibe/rules.md` + 本 skill + 目标怪物细分需求.md
2. 若仍有**硬冲突** `策划未明确`（Canonical 自相矛盾或与 Authority 冲突且无法选边）→ **停止并列出问题**，禁止用猜测覆盖 Canonical；禁止回读 CSV 填坑
3. 阶段 B **只执行正文**（§0–§4 行为规格 / 数值 / 资源 / 卡表）；§5 AI裁定备忘仅供追溯，不单独当执行清单
4. `TUNABLE` 数值可按正文首轮值落地，但不得假装对应分项已 `已完成`
5. 改 Prefab / 场景前 `git status`；不改 `Packages/manifest.json`、`ProjectSettings/`（除非用户明确放行）
6. 不改 Shared Original 旧怪 `*(full)`；只建/改 `*_new`



## B.1 标准落地顺序（严格按序）



### Step 1 — 对齐三槽拆分

把细分需求 §2/§3 映射为：


| 输入    | `EnemyAbility.type` | `MonsterActor` 列表              |
| ----- | ------------------- | ------------------------------ |
| 左键    | `BasicAttack`       | `basicAbilities` + `hpCost`    |
| Space | `Mobility`          | `mobilityAbilities` + `hpCost` |
| Q     | `Skill`             | `skillAbilities` + `hpCost`    |
| 常驻    | `Passive`           | 被动组件                           |


原则（来自傲慢/怠惰）：

1. **一技能一类**：独立玩法 → 独立 `EnemyAbility_`*，不往万能原型塞枚举
2. **卡牌只认 Tag**：`abilityTags` / `CardData.targetAbilityTags`
3. **伤害走** `DealDamageTo` / `SettleHit`，才能吃吸血与 `appliedEffectTags`
4. 有自定义 Mobility 时，不要再挂通用 `EnemyAbility_MobilityDash`
5. Prefab 用完整资产 `*_new`，避免深层 Variant 丢引用



### Step 2 — Ability 脚本

路径：`Assets/Scripts/Combat/Abilities/Monster/`

- 优先复用已有类；没有再新建  
- `Reset()` 或初始化里确保默认 `abilityTags`  
- 词条：`IsUpgradeUnlocked("Xxx.Yyy")` 或 `GetCardParameter("Key", default)`  
- 连续耗血技能走 `PayAbilityHpCost`  
- 身体/武器特效字段在 Ability 上（`vfxPrefab` / `weaponVfxPrefab`），命中特效在 Effect 上

参考类：


| 类                                                                            | 怪物    |
| ---------------------------------------------------------------------------- | ----- |
| `EnemyAbility_PrideChargeStrike` / `SwordQi` / `PrideBlinkChain`             | 傲慢    |
| `EnemyAbility_SlothLaunch` / `SlothChargeShot` / `SlothDrone` / `SummonBolt` | 怠惰    |
| `SummonActor`                                                                | 召唤物通用 |




### Step 3 — Effect 资产

`Create > Possession/Combat/Gameplay Effect` → `Assets/Combat/Effects/`


| 用途                | 配法                                                  |
| ----------------- | --------------------------------------------------- |
| 自身持续态（不可选中/飞行/变身） | `duration<=0` 或技能时长；`grantedTags`；`activeVfxPrefab` |
| 命中反馈              | 短 duration；`hitVfxPrefab` + `hitVfxDuration`        |


登记：`GameplayTagCatalog.declaredTags` + `effectDefinitions`；场景 `CardManager.gameplayTagCatalog` 必须指向它。

### Step 4 — Prefab

```text
xxx_new
├─ Enemy + CombatAbilityComponent + MonsterActor 列表
└─ abilities/
   ├─ basic_*
   ├─ mobility_*
   ├─ skill_*
   └─ passive_*（可选）
```

填写：`displayName`、`maxHealth`、`moveSpeed`、各 Ability 数值、`abilityTags`、`upgrades.effectId`、VFX 引用。  
召唤物另建 prefab（如 `sloth_drone`），挂自己的 Ability。

### Step 5 — 卡牌

`Assets/Configs/CardLibrary.asset`：

```text
effectId: Sin.CardName
targetAbilityTags: Ability.Monster.Sin.Skill
abilityParameters: [{ key: Foo, value: N }]
```

Ability `upgrades` 必须包含同名 `effectId`。  
卡牌内容以 Canonical Card Truth 为准，不得按已删除卡或旧 CSV 词条补回。

### Step 6 — 测试入口

- `MonsterCheatCatalog`：显示名、prefab、热键  
- 需要时 `MonsterAIConfig` 加条目  
- `CombatTest`：`MonsterPossessionCheat`；建议关 `testDoublePickOnStart` 以免挡热键



### Step 7 — 生成《技能配置测试指南》

路径：`.vibe/doc/Modules/Monsters/<怪物中文名>-技能配置测试指南.md`

至少包含：

1. 热键：生成木桩 / 附身本怪 / 开卡
2. 每技能：无目标 / 单目标 / 多目标
3. 每词条：解锁前后对比
4. 资源占位项单独列出「看的是占位还是正式」
5. 已知问题



### Step 8 — 回写细分需求状态

按「资源依赖门禁」分项回写，禁止整表刷成同一状态：

- 无资源依赖的项（id / Cheat / 数值 / 纯逻辑 Card 等）：代码就绪 → `已完成`
- 有已声明资源行：逻辑完成但资源占位 → 资源行与依赖该项的技能/词条 → `已开发但没资源`（占位名写清）
- 正式资源挂上 → 对应行升 `已完成`
- 怪物总状态按最差聚合规则重算（缺资源的技能可拉低总状态；不得回头改写无关纯逻辑行）
- 更新 Monsters `README.md` 总表



## B.2 Tag / 命名约定

```text
Ability.Monster.<Sin>.<Skill>
Effect.Combat.<Sin><HitName>
Effect.Defense.Untargetable / DamageImmune
State.Defense.Untargetable / Flying / DamageImmune
Card effectId: <Sin>.<CardName>   # 例 Pride.Pierce / Sloth.LandingMine
```

`<Sin>`：`Pride` `Sloth` `Gluttony` `Envy` `Wrath` `Greed` `Lust` `Soul`

## B.3 卡牌参数常用 key（可扩展，扩展时写进细分需求）


| key                 | 含义     |
| ------------------- | ------ |
| `ChargeDistance`    | 突进距离   |
| `BlinkCount`        | 穿梭次数   |
| `SummonCount`       | 召唤数量   |
| `TransformDuration` | 变身时长   |
| `FlightDuration`    | 飞行时长   |
| `ExecuteThreshold`  | 斩杀血量比例 |
| `BladeCount`        | 环绕数量   |




## B.4 阶段 B 完成标准

- 细分需求 §6 检查清单全勾（允许个别**已声明资源行**仍为「已开发但没资源」；纯逻辑项应为「已完成」）  
- 无硬冲突 `策划未明确` 被偷偷实现；正文中由 AI 补全的规格已落地，且仍可在 §5 备忘追溯  
- 测试指南可独立交给他人验收  
- 未改旧 `*(full)`，未动 Packages/ProjectSettings（除非获准）

---



# 阶段 C — Git 版本管理（提交怪物）

> 本阶段是用户**明确口述提交指令**时的授权流程。平时仍遵守 `.vibe/rules.md`：未说「提交…」则不 `commit` / `push`。  
> 用户说出下方触发语 = 授权完成本节全部步骤（含 push 当前分支与 push `main`）。

> **Authority scope：** Stage C 是 Owner-approved Monster Development Fast Path，不是整个 Repository 的默认 Git 流程。其现有触发语和固定操作步骤保持不变。
>
> 此 Fast Path 不适用于 Canonical、`.vibe/rules.md`、`AGENTS.md`、`Packages/`、`ProjectSettings/`、高风险 Shared Original、无法安全判断的 Unity YAML 冲突、跨系统大型架构修改或非 Monster 任务；这些内容继续服从项目通用工作流或明确的人类确认。
## C.0 触发语（语义匹配，不要求一字不差）

| 用户说 | 范围 |
|---|---|
| 「提交\<怪物\>」「把\<怪物\>提交了」「提交傲慢 / 怠惰 / pride / sloth_new …」 | **单怪**：该怪物全部相关改动 |
| 「提交所有怪物」「提交全部怪物」「把怪物相关都提交」 | **全怪**：所有怪物相关改动 |

「怪物名」可用中文全称、简称（傲慢/怠惰）、英文 id（`pride`/`sloth`）、或 prefab 名（`pride_new`）。解析不到唯一怪物 → **先问用户**，不要猜。

## C.1 固定操作顺序（不得跳步、不得 force）

```text
1. 在当前工作分支（通常 dev_*）识别并暂存「范围内」文件
2. commit（有改动才提交；信息写清怪物范围）
3. push 到当前分支的远端（origin/<当前分支>）
4. fetch origin main；把 origin/main 合进当前分支（merge，默认不用 rebase）
5. 冲突：AI 能安全解决则解决并继续；否则停下交给用户，解决前不 push main
6. 将当前分支合入本地 main（fast-forward 优先），再 push origin main
7. checkout 回用户原来的工作分支；回报 commit hash 与是否已上 main
```

硬性禁止：

- `git push --force` / `--force-with-lease`（除非用户当次另用更高优先级明文放行）
- `git reset --hard`、`git clean -fdx`
- `--no-verify` 跳过 hook
- 把无关文件塞进本次提交（见 C.2 / C.3）

## C.2 「单怪」相关文件怎么圈定

以该怪细分需求 `.vibe/doc/Modules/Monsters/<中文名>.md` 的「工程标识」+ 实际 diff 为准，**只 stage 与该怪有关的路径**。典型包括：

| 类别 | 示例（傲慢 / 怠惰） |
|---|---|
| 细分需求 / 测试指南 | `.vibe/doc/Modules/Monsters/傲慢-终刃绝影.md`、`…-技能配置测试指南.md`；必要时更新 `Modules/Monsters/README.md` 中该行状态 |
| Prefab | `Assets/Prefabs/Monster/pride_new.prefab`、`sloth_new.prefab`、`sloth_drone.prefab` 及对应 `.meta` |
| Ability / Actor 脚本 | `EnemyAbility_Pride*.cs`、`EnemyAbility_SwordQi.cs`、`EnemyAbility_Sloth*.cs`、`SummonActor.cs`（**仅当本次确为该怪改动**）及 `.meta` |
| Effect 资产 | `Assets/Combat/Effects/Effect_*Pride*`、`Effect_*Sloth*` 等 |
| 配置片段 | `CardLibrary.asset`、`GameplayTagCatalog.asset`、`MonsterCheatCatalog.asset`、`MonsterAIConfig.asset`、相关场景（如 `CombatTest.unity`）——**仅当 diff 内容属于该怪**；若同一文件混有多怪改动，优先只提交能干净分离的 hunk；无法分离则询问用户：本次扩大到多怪，或先拆开 |

不要纳入单怪提交（除非用户明确说带上）：

- `.DS_Store`、本地 Excel/CSV 策划源表（含 `项目全量表 - 怪物设计（新）.csv`，除非用户点名）
- `Packages/packages-lock.json`、`Packages/manifest.json`、`Packages/com.quaza.unitymcp/`
- `ProjectSettings/`、`Library/`、其它怪物的 prefab/脚本/文档
- 与怪物无关的重构 / 工具改动
- Canonical 文件（Stage C Fast Path **默认不提交** Canonical；Canonical 变更走独立 Owner 流程）

拿不准某文件是否属于该怪 → **列出路径问用户**，不要擅自扩大范围。

## C.3 「所有怪物」相关文件怎么圈定

Stage 所有与怪物流水线/实现相关的改动，包括但不限于：

- `.vibe/doc/Modules/Monsters/**`
- `.vibe/doc/项目设计.md`（若只改了怪物索引）
- `.vibe/shared-skills/monster-dev-pipeline/**`
- `Assets/Prefabs/Monster/**` 下本次改动的 `*_new` / 召唤物等
- `Assets/Scripts/Combat/Abilities/Monster/**`、`Assets/Scripts/Combat/Actors/**` 中与怪物相关的改动
- `Assets/Combat/Effects/**` 中怪物 Effect
- `Assets/Configs/CardLibrary.asset`、`MonsterCheatCatalog.asset`、`MonsterAIConfig.asset`、`Assets/GameplayTagCatalog.asset`、相关测试场景

仍排除：`.DS_Store`、未点名的策划 xlsx/csv（含怪物全量表）、`Packages/**`（含 lock / unitymcp）、`ProjectSettings/`、Canonical（除非用户当次明确要求）、明显非怪物改动。

## C.4 Commit message

- 单怪：`feat(<id>): …` 或 `docs(<id>): …` / `fix(<id>): …`，`<id>` 用英文 id（`pride`/`sloth`/…）
- 全怪：`feat(monsters): …` 或 `docs(monsters): …`
- 1–2 句，写清**为什么**；用 HEREDOC 提交
- 无改动可提交时：不建空 commit，直接进入 merge/push 检查并告知「工作区无该范围变更」

## C.5 合 main 与冲突

1. `git fetch origin main`
2. 在**当前功能分支**上：`git merge origin/main`（保留合并提交；与仓库既有习惯一致）
3. 冲突处理：
   - **可自行解决**：纯文档合并、明显一边新增、按细分需求/本 skill 能唯一判定的代码；解决后继续编译风险低的路径，再 commit merge
   - **必须交给用户**：Prefab/场景/asset YAML 双方都改且无法判断；Shared Original；`ProjectSettings`；逻辑语义冲突；任何需要 `--ours/--theirs` 盲选的 Unity YAML → **停下**，列出冲突文件与原因，等用户解决后再从步骤 6 续跑
4. 功能分支已含 main 且已 push 后：`checkout main` → `merge` 功能分支（优先 fast-forward）→ `push origin main` → `checkout` 回原分支

Push `main` 若被保护分支/权限拦截：把错误原文给用户，不要改用 force。

## C.6 阶段 C 完成标准

- 范围内文件已进 commit，且已 push 当前远端分支
- 当前分支已合并最新 `origin/main`（或已因冲突暂停并说明）
- 无冲突时：`origin/main` 已包含本次提交
- 工作区回到用户原分支；未提交的无关脏文件仍保持未 stage

---

# Agent 话术触发（给用户/其它 AI）


| 用户说                                 | 执行                     |
| ----------------------------------- | ---------------------- |
| 「按怪物流程 / monster-dev-pipeline 刷新需求」 | 仅阶段 A（只读 Canonical）     |
| 「根据 Canonical 生成/更新细分需求」           | 仅阶段 A                  |
| 「根据 CSV 生成细分需求」                     | **拒绝**：提示已改用 Canonical，改走上一行 |
| 「按细分需求落地某某怪」                        | 阶段 B                   |
| 「补某某特效资源并改状态」                       | 改 Prefab 引用 + 回写 md 状态 |
| 「生成/更新测试指南」                         | B.Step 7               |
| 「提交\<怪物\>」/「提交傲慢」等                   | **阶段 C（单怪）**           |
| 「提交所有怪物」/「提交全部怪物」                   | **阶段 C（全怪）**           |


一次请求若同时要 A+B：先 A 产出 md（空隙已写入正文 + §5 备忘），再直接进 B；**不**因策划未审阅 §5 而停。仅当存在硬冲突 `策划未明确` 时先停该项。  
「提交…」可与 A/B 分开说；若同一句话既落地又提交，先完成 B 再跑 C。
