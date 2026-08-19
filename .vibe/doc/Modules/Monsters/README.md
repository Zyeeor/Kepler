# Monsters 模块索引

> **Authority status: IMPLEMENTATION TRACKING**  
> **Design Authority**：仅 `.vibe/doc/Canonical/**`（冲突时 Canonical 胜）。  
> 本目录为 `monster-dev-pipeline` 阶段 A 产出的工程追踪（Prefab / Ability / 资源 / 状态），**不是**独立 Design Truth。  
> 旧 `项目全量表 - 怪物设计（新）.csv` 与历史 Modules 玩法文案 = **Legacy Evidence only**，不得作策划 / 工程规格输入。

> 细分需求由 `monster-dev-pipeline` 阶段 A 生成；工程落地走阶段 B。  
> Card 真源：`Canonical/Content/Card_System_Current_Truth_v1.1.md`  
> 内容主源：`Canonical/02_CONTENT_CANONICAL.md`

## 状态说明

| 状态 | 含义 |
|---|---|
| 策划未明确 | **仅** Canonical 硬冲突 / 自相矛盾；**不得实现**（空隙须由 AI 写入正文，不得用此状态） |
| 待开发 | Canonical 已写清，或空隙已写入正文可执行规格；工程尚未做 |
| 已开发但没资源 | 逻辑已可玩，**且该项已声明必需美术资源**仍缺/占位（勿套到纯逻辑项） |
| 已完成 | 工程已落地；无资源依赖则代码就绪即完成 |
| — | 不适用 |

> **§5 合同（2026-08-19）**：细分需求**不再保留「开放问题」台账**。AI 补全的结论写入正文；§5 仅为「AI裁定备忘」供策划审阅。阶段 B 只读正文。

## 总表

| 怪物 | 英文 id | Prefab | 总状态 | Canonical 对齐 | 细分需求 |
|---|---|---|---|---|---|
| 主角-灵魂体 | soul | （现有灵魂；路径见该文件） | 非 Roster 追踪 | **非**七宗罪 Roster；无战斗技能；开场进傲慢 Carrier；路径/手感已 AI 收口进正文 | [主角-灵魂体.md](./主角-灵魂体.md) |
| 傲慢-终刃绝影 | pride | `pride_new` | 已开发但没资源 | 三槽逻辑已落地；8张`PR-*`（含`PR-A04`）；§5=AI裁定备忘 | [傲慢-终刃绝影.md](./傲慢-终刃绝影.md) |
| 怠惰-机械之灵 | sloth | `sloth_new` | 已开发但没资源 | 三槽已落地；11张`SL-*`；§5=AI裁定备忘 | [怠惰-机械之灵.md](./怠惰-机械之灵.md) |
| 暴食-魔猫 | gluttony | `gluttony_new` | 待开发 | 已完成 Canonical Delta 对账；待落地三槽、7张`GL-*`、删除卡清理与复制E槽；`GL-S03`攻击分类本轮暂不处理；§5=AI裁定备忘 | [暴食-魔猫.md](./暴食-魔猫.md) |
| 嫉妒-激光异形 | envy | `envy_new`（待建） | 待开发 | 目标**Mark**；10张`EN-*`；§5=AI裁定备忘 | [嫉妒-激光异形.md](./嫉妒-激光异形.md) |
| 愤怒-链狱冥兽 | wrath | `wrath_new`（待建） | 待开发 | 6张`WR-*`；§5=AI裁定备忘 | [愤怒-链狱冥兽.md](./愤怒-链狱冥兽.md) |
| 贪婪-万手藏主 | greed | `greed_new`（待建） | 待开发 | 黑油加速+Guard全伤害；8张`GR-*`；§5=AI裁定备忘 | [贪婪-万手藏主.md](./贪婪-万手藏主.md) |
| 色欲-灵念师 | lust | `lust_new`（待建） | 待开发 | 7张最终牌；互撞/隔离等已写入正文；§5=AI裁定备忘 | [色欲-灵念师.md](./色欲-灵念师.md) |

总状态取该怪各技能 / 有交付物的词条 / 已声明关键资源的最差状态。纯逻辑已完成项不被总状态改写。Soul 不按三槽流水线计最差状态，保持非 Roster 追踪。

## 本轮阶段 A Diff 摘要（2026-08-19）

| 怪物 | 稳定键 | Delta | 旧 → 新 | 原因 |
|---|---|---|---|---|
| **全怪** | §5 文档合同 | Changed | 开放问题台账 → 正文规格 + AI裁定备忘 | skill 强制：空隙收口进正文；§5 仅供策划审阅 |
| 傲慢 | `PR-A04` 等 | Added / 收口 | 见该文件 §0 / §5 A1–A5 | Owner Delta + 蓄力/被动/散射收口 |
| 怠惰 | Scatter / 木灵等 | Changed | 误标策划未明确 → 正文 | Card Truth 选边；§5 A1–A9 |
| 暴食 | 三槽 / `GL-*` | Changed | 工程 Delta 对账 + §5 备忘 | 合并 main 对账与 skill 合同；`GL-A02`=范围×2 |
| 嫉妒 | Mark + `EN-M01`/`EN-A05` | Changed / Added | Record→Mark；8→10 张 | Owner Delta + skill 合同 |
| 愤怒 | 三槽 / 模型名 | 收口 | §5 A1–A6 | 无硬冲突 |
| 贪婪 | 黑油/Guard + `GR-M02`/`GR-A07` | Changed / Added | 6→8 张；§5 备忘 | Owner Delta + skill 合同 |
| 色欲 | `LU-S05`/`S06`/`M05×A04` 等 | Changed | 误标策划未明确 → 正文可执行规格 | §5 A1–A10 |
| 灵魂体 | Prefab/手感路径 | Changed | 误标策划未明确 → 正文候选值 | 非 Roster；§5 A1–A5 |

## 资源需求合同（2026-08-18 / 19）

- 21个基础技能均已分配稳定`ResourceSetId`。
- 每个独立资源行必须包含：资源名、类型、状态、当前资源事实 / 路径、用途、具体设计需求。
- Card资源关系统一使用`NEW / REUSE / NONE`；当前怪物专属 / Type Growth Card共**57**张。
- `已挂`只代表Prefab / Ability / Effect已有序列化引用，不等于正式资源完成；通用或占位引用仍按`已开发但没资源`追踪。
- 资源具体设计需求必须说明触发时机、表现对象、生命周期 / 清理时机、必须传达的信息和禁止误读项。

## 权威与合并要点（2026-08-19）

| 项 | 内容 |
|---|---|
| Authority | Gameplay / Content / Card：**Canonical only** |
| Modules 角色 | Implementation Tracking（路径、状态、检查清单） |
| CSV | Legacy Evidence only；不得回读裁决当前 Requirement |
| 当前怪物Card总量 | **57**张：Pride **8** / Sloth 11 / Gluttony 7 / Envy **10** / Wrath 6 / Greed **8** / Lust 7 |
| 全项目 Card | **73**张 = 基础通用9 + Global Slot7 + 上表57 |
| Type Growth | 当前4张；Gluttony / Wrath / Greed无Type Growth |
| 色欲牌池 | Owner最终7张：`LU-M03`, `LU-M05`, `LU-S05`, `LU-A03`, `LU-S06`, `LU-A04`, `LU-TG01` |
| 已落地怪 | 傲慢 / 怠惰有基线工程；暴食有旧版工程但与当前Canonical存在Changed Delta；均仍有正式资源缺口 |
| 待开发怪 | 嫉妒 / 愤怒 / 贪婪 / 色欲 — 阶段B前以各文件 Canonical规格与资源需求为准 |

阶段 B 前：勿实现各文件 Appendix 中的 Legacy / 退役行。
