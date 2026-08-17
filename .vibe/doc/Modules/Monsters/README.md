# Monsters 模块索引

> **Authority status: IMPLEMENTATION TRACKING / HISTORICAL REFERENCE**  
> Design Authority：`.vibe/doc/Canonical/**`（冲突时 Canonical 胜）。  
> 本目录为 monster-dev-pipeline 阶段 A 产出的工程追踪文档，不是独立 Design Truth。  
> 2026-08-17：需求合并刷新；CSV 与 Canonical 冲突项已按 Canonical 重写，旧 CSV 规格进各文件 Appendix。

> 细分需求由 `monster-dev-pipeline` 阶段 A 生成；工程落地走阶段 B。  
> 结构化工程输入：`项目全量表 - 怪物设计（新）.csv`  
> Card 真源：`Canonical/Content/Card_System_Current_Truth_v1.1.md`

## 状态说明

| 状态 | 含义 |
|---|---|
| 策划未明确 | 关键规则无法唯一解释；**不得实现** |
| 待开发 | Canonical 已写清，工程尚未做 |
| 已开发但没资源 | 可玩，正式 VFX/模型仍缺（含 CSV「制作中」） |
| 已完成 | 逻辑 + 正式资源齐 |

## 总表

| 怪物 | 英文 id | Prefab | 总状态 | Canonical 对齐 | 细分需求 |
|---|---|---|---|---|---|
| 主角-灵魂体 | soul | （现有灵魂） | 策划未明确 | Soul **无**战斗技能；开场进傲慢 | [主角-灵魂体.md](./主角-灵魂体.md) |
| 傲慢-终刃绝影 | pride | `pride_new` | 已开发但没资源 | 三槽已对齐；Card 待迁 `PR-*` | [傲慢-终刃绝影.md](./傲慢-终刃绝影.md) |
| 怠惰-机械之灵 | sloth | `sloth_new` | 已开发但没资源 | 三槽已对齐；Card 待迁 `SL-*` | [怠惰-机械之灵.md](./怠惰-机械之灵.md) |
| 暴食-魔猫 | gluttony | `gluttony_new`（待建） | 待开发 | 三槽+`GL-*` 已合并 | [暴食-魔猫.md](./暴食-魔猫.md) |
| 嫉妒-激光异形 | envy | `envy_new`（待建） | 待开发 | 已按 Canonical 重写（弃最高血/旧标记） | [嫉妒-激光异形.md](./嫉妒-激光异形.md) |
| 愤怒-链狱冥兽 | wrath | `wrath_new`（待建） | 待开发 | 已按 Canonical 重写（路径伤降为 Card） | [愤怒-链狱冥兽.md](./愤怒-链狱冥兽.md) |
| 贪婪-万手藏主 | greed | `greed_new`（待建） | 待开发 | 已按 Canonical 重写（库存+正面 Guard） | [贪婪-万手藏主.md](./贪婪-万手藏主.md) |
| 色欲-灵念师 | lust | `lust_new`（待建） | 待开发 | 已按 Canonical 重写（Q=牵魂；色灵=`LU-S04`） | [色欲-灵念师.md](./色欲-灵念师.md) |

总状态取该怪各技能 / 词条 / 关键资源的最差状态。

## 合并决议摘要（2026-08-17）

| 决议 | 内容 |
|---|---|
| Authority | Gameplay / Content / Card：Canonical 优先 |
| CSV 保留 | 移速、血量、CD/耗血等 TUNABLE 初值；模型与 VFX「制作中」状态；外观描述 |
| 硬冲突已吸收 | 嫉妒目标与 Record；色欲基础 Q；贪婪 Guard/魔手；愤怒钩索路径伤；Soul 气波 |
| 已落地怪 | 傲慢 / 怠惰保留工程路径与旧 effectId，另附 Canonical Card 对齐表与缺口 |

阶段 B 前：待开发怪以各文件 §3–§4 Canonical 规格为准；勿再实现 Appendix 中的 Legacy 行。
