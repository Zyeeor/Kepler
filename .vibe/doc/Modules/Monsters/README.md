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
| 策划未明确 | 关键规则无法唯一解释；**不得实现** |
| 待开发 | Canonical 已写清，工程尚未做 |
| 已开发但没资源 | 逻辑已可玩，**且该项已声明必需美术资源**仍缺/占位（勿套到纯逻辑项） |
| 已完成 | 工程已落地；无资源依赖则代码就绪即完成 |
| — | 不适用 |

## 总表

| 怪物 | 英文 id | Prefab | 总状态 | Canonical 对齐 | 细分需求 |
|---|---|---|---|---|---|
| 主角-灵魂体 | soul | （现有灵魂） | 策划未明确 | **非**七宗罪 Roster；无战斗技能；开场进傲慢 Carrier | [主角-灵魂体.md](./主角-灵魂体.md) |
| 傲慢-终刃绝影 | pride | `pride_new` | 已开发但没资源 | 三槽逻辑已落地；突进 / 穿梭主VFX仍空或通用占位；7张`PR-*`资源需求已补齐 | [傲慢-终刃绝影.md](./傲慢-终刃绝影.md) |
| 怠惰-机械之灵 | sloth | `sloth_new` | 已开发但没资源 | 三槽已落地；正式VFX / 木灵模型仍缺；11张`SL-*`资源需求已补齐 | [怠惰-机械之灵.md](./怠惰-机械之灵.md) |
| 暴食-魔猫 | gluttony | `gluttony_new` | 待开发 | 已完成2026-08-19 Canonical Delta 对账；待落地三槽、7张`GL-*`、删除卡清理与复制E槽；`GL-S03`攻击分类本轮暂不处理 | [暴食-魔猫.md](./暴食-魔猫.md) |
| 嫉妒-激光异形 | envy | `envy_new`（待建） | 待开发 | Canonical三槽 + 8张`EN-*`资源需求已补齐；工程未做 | [嫉妒-激光异形.md](./嫉妒-激光异形.md) |
| 愤怒-链狱冥兽 | wrath | `wrath_new`（待建） | 待开发 | 已对齐2026-08-18三槽与6张`WR-*`，无Type Growth；资源需求已补齐 | [愤怒-链狱冥兽.md](./愤怒-链狱冥兽.md) |
| 贪婪-万手藏主 | greed | `greed_new`（待建） | 待开发 | 已对齐2026-08-18三槽与6张`GR-*`，无Type Growth；资源需求已补齐 | [贪婪-万手藏主.md](./贪婪-万手藏主.md) |
| 色欲-灵念师 | lust | `lust_new`（待建） | 待开发 | 基础Q=牵魂；7张Owner最终牌；三槽与Card资源需求已补齐 | [色欲-灵念师.md](./色欲-灵念师.md) |

总状态取该怪各技能 / 有交付物的词条 / 已声明关键资源的最差状态。纯逻辑已完成项不被总状态改写。Soul 不按三槽流水线计最差状态，保持非 Roster 追踪。

## 资源需求合同（2026-08-18）

- 21个基础技能均已分配稳定`ResourceSetId`。
- 每个独立资源行必须包含：资源名、类型、状态、当前资源事实 / 路径、用途、具体设计需求。
- Card资源关系统一使用`NEW / REUSE / NONE`；当前怪物专属 / Type Growth Card共57张。
- `已挂`只代表Prefab / Ability / Effect已有序列化引用，不等于正式资源完成；通用或占位引用仍按`已开发但没资源`追踪。
- 资源具体设计需求必须说明触发时机、表现对象、生命周期 / 清理时机、必须传达的信息和禁止误读项。

## 权威与合并要点（2026-08-18）

| 项 | 内容 |
|---|---|
| Authority | Gameplay / Content / Card：**Canonical only** |
| Modules 角色 | Implementation Tracking（路径、状态、检查清单） |
| CSV | Legacy Evidence only；不得回读裁决当前 Requirement |
| 当前怪物Card总量 | 57张：Pride 8 / Sloth 11 / Gluttony 7 / Envy 10 / Wrath 6 / Greed 8 / Lust 7 |
| Type Growth | 当前4张；Gluttony / Wrath / Greed无Type Growth |
| 色欲牌池 | Owner最终7张：`LU-M03`, `LU-M05`, `LU-S05`, `LU-A03`, `LU-S06`, `LU-A04`, `LU-TG01` |
| 已落地怪 | 傲慢 / 怠惰有基线工程；暴食有旧版工程但与当前Canonical存在Changed Delta；均仍有正式资源缺口 |
| 待开发怪 | 嫉妒 / 愤怒 / 贪婪 / 色欲 — 阶段B前以各文件 §3–§4 Canonical规格与资源需求为准 |

阶段 B 前：勿实现各文件 Appendix 中的 Legacy / 退役行。
