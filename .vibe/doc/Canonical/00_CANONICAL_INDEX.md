# 00_CANONICAL_INDEX — Repository-Ready v1.1

**Project:** Possession<br>
**Date:** 2026-08-18<br>
**Package Status:** `REPOSITORY-READY DESIGN CANONICAL v1.1 + OWNER DELTA 2026-08-20`<br>
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

负责七宗罪Roster、21个基础技能、65张Card注册表、Card作用域、Type Growth / Global Slot、固定起始傲慢Body、Encounter / Spawn、Elite、Terrain、Tutorial等Content基线。

## `03_PRESENTATION_CANONICAL.md`

`CANONICAL / CURRENT PRESENTATION BASELINE`

负责R03.1世界观中心整合、Mythic/System双线、User Distillation、A0–A4 Reveal、开场Carrier分配、First Clear、Seven Sins语法、UI/VFX/Audio/Telegraph与双线文本资产需求。

## `Content/Card_System_Current_Truth_v1.1.md`

`OWNER REVIEW PASSED / CURRENT CARD CONTENT TRUTH`

负责65张Card完整逐卡字段、明暗文案、机制、Stack、人工删除历史，以及当前Basic / Global / Type Growth规则。

## `Content/Encounter_CardOffer_Baseline_v1.0.md`

`BASELINE / TUNABLE / NOT PLAYABLE-VALIDATED`

负责当前首版Wave/Encounter节奏算法、投资权重、Soft Pity、Elite注入与9次Card Offer算法。用于程序第一版对接，不代表数值冻结。

## `Content/Dual_Line_Text_Requirements_v1.0.md`

`PRESENTATION PRODUCTION REQUIREMENT`

负责全项目需要Mythic/System双版本的文本资产清单、显示原则与A0–A4映射。

## `Content/Narrative_Voice_Delivery_Baseline_v1.0.md`

`CANONICAL PRESENTATION DELIVERY CONTRACT / EXACT COPY & AUDIO OPEN`

负责数据驱动旁白、Narrative Access / Trigger / Display解耦、双Voice通道、Card显示、Run Analytics、Profile持久化、调试与策划配置合同。

## `Content/Tutorial_Delivery_Baseline_v1.0.md`

`CANONICAL TUTORIAL DELIVERY CONTRACT / EXACT COPY & TIMING OPEN`

负责TUT-01–07与Monster微教学的运行时步骤、Encounter保障、动态按键、持久化、异常兜底、表现与验收合同。

## `04_FINAL_CLOSURE_AUDIT.md`

记录本轮最终Owner Decision、五轴Closure与入仓Gate；它是审计证据，不高于01–03的设计真源。

---

# 4. 当前完成状态

| 层 | 当前状态 | 判断 |
|---|---|---|
| Rules | `CLOSED` | 无核心规则阻塞 |
| Monster Core | `CARD-READY / CLOSED` | 七只Roster与三槽稳定 |
| Starting Carrier | `CONFIRMED` | 开场Soul被分配进傲慢Carrier；玩家首个可操作Body固定傲慢 |
| Card Content | `OWNER REVIEW PASSED / OWNER DELTA APPLIED` | 65张当前真源；傲慢8、怠惰7、暴食7、嫉妒6、愤怒6、贪婪8、色欲7张 |
| Card Timing | `CONFIRMED` | 删除Opening Card；W1–7后各1次，W8后连续2次，共9次 |
| Card Scope | `CONFIRMED` | Basic / Global作用普通Enemy + Possessed；Elite完全排除当前Run Card层 |
| World / Narrative | `R03.1 INTEGRATED` | 核心主题、Reveal、First Clear已收口；深层Ontology故意Open |
| Presentation Contracts | `DELIVERY CONTRACT CLOSED / COPY & AUDIO OPEN` | 双线、双Voice、可配置Access / Cue / Display与教学Delivery Contract已闭合；最终文案音频仍待生产 |
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
11. 色欲牌池由旧10张替换为7张：`LU-M03`, `LU-M05`, `LU-S05`, `LU-A03`, `LU-S06`, `LU-A04`, `LU-TG01`；`LU-TG01`保留Type Growth身份并改名为“欲潮不息”。
12. `LU-S05`确认为“同一次色欲Special中，被牵引目标相互碰撞时爆炸”，不重复解锁基础Special已有的牵引能力。
13. 色欲6张非Type Growth卡的精确伤害、半径、延迟、扩散时间和伤害隔离宽限为首轮`TUNABLE / PLAYABLE` Baseline。
14. `LU-TG01`提高色欲Body基础移速，并使Movement / Attack / Special三槽冷却统一减少30%；冷却缩减为Owner确认值，基础移速增幅留待Playable调校，且不缩短前摇、后摇或敌人最低预警。
15. 暴食三槽更新：小猫化为约50%体型、持续3秒、移速+100%；基础深渊巨口在脚下快照后0.5秒生成，`GL-A03`解锁远程诱饵落点；吞噬只命中最近Enemy、写入`Overfed`并复制其E槽Skill Ability，复制技能成功使用一次后恢复吞噬，换身清除相关状态。
16. 暴食牌池维持7张：`GL-A01`使`Overfed`后的下一巨口成对生成；`GL-A02`使Movement后的下一巨口范围+100%；两者可叠加，成对巨口均获得范围加成。`GL-S03`吞噬扇形内全部合法飞行攻击。
17. 暴食成为无Type Growth的Owner确认例外；当前Type Growth共2张，当前Card总数为65张。
18. 愤怒三槽更新：钩索将自身钩向前方；砸地燃烧每秒5点、持续3秒且可点燃黑油；暴怒锁链形成2秒龙卷风，每0.5秒结算一次。`WR-M01`, `WR-M02`, `WR-B01`, `WR-B02`, `WR-S01`, `WR-S03`为当前6张牌池；`WR-B01`低耐久按比例提高伤害、攻击范围并缩短Attack CD，每项最高50%。
19. 愤怒删除`WR-A01`, `WR-M03`, `WR-S02`, `WR-TG01`；`WR-M03`落点冲击并入`WR-M02`，`WR-S03 终末飓风`使龙卷风时长+2秒。Wrath为无Type Growth的Owner确认例外。
20. 贪婪三槽更新：基础黑油使玩家 / Possessed Greed加速，`GR-M01`扩大黑油长度与范围并使Enemy减速；魔手每秒生成、库存上限6、按0.2秒间隔释放；Guard吸收所有伤害，基础每100点转1手，`GR-S01`每60点转1手，`GR-S04`使Guard持续3秒并可主动结束。
21. 历史Owner Decision：贪婪曾收缩为6张，保留`GR-M01`, `GR-A01`, `GR-A02`, `GR-A03`, `GR-S01`, `GR-S04`；删除`GR-A05`, `GR-A06`, `GR-M02`, `GR-M03`, `GR-S02`, `GR-TG01`。Greed成为无Type Growth例外；当时Type Growth共4张、Card总数为68张。后续本轮Decision 23–24已恢复`GR-M02`并新增`GR-A07`。
22. Owner确认「怪物设计（新）」为最新设计输入：Soul不加普攻，`Q=Bullet Time`、`E=Special`不变；傲慢、怠惰、暴食、嫉妒、愤怒、贪婪与色欲三槽及卡牌按本轮Owner Delta更新。
23. 新增正式Card：`PR-A04 王命远征`、`EN-M01 镜痕巡猎`、`EN-A05 妒焰渐炽`、`GR-M02 圣路恩赐`、`GR-A07 迂回纳贡`；当前Card总数为73张。
24. 贪婪`GR-M02`由历史删除状态恢复为新定义的「圣路恩赐」，不沿用旧“黑油减速叠层”语义；愤怒表格“龙卷风时长+2秒”合并回现有`WR-S03 终末飓风`，不新增重复卡。
25. 废除叠层机制（2026-08-19）：全牌池`Stack Max`统一为1，卡牌唯一获取、不再支持重复叠加。原6张可叠层卡`SL-A02`（原Stack 3）、`SL-S01`、`EN-A04`、`EN-R04`、`EN-S01`、`GR-A01`（原Stack 2）的累计叠层效果合并进单层，`明线/暗线叠层后文本`字段恒置空保留追溯；合并后具体数值档位一律`TUNABLE / PLAYABLE`，开发期公开可调。
26. 删除怠惰4张卡（2026-08-20）：`SL-A01`、`SL-A02`、`SL-S02`、`SL-TG01`。怠惰从11张减为7张；`SL-TG01`删除后怠惰成为无Type Growth的Owner确认例外（与Gluttony、Wrath、Greed并列）。总牌池从73张减为69张，类型成长卡从4张减为3张。
27. 嫉妒卡牌裁剪与合并（2026-08-20）：删除 `EN-A04`、`EN-R04`、`EN-TG01`；保留 `EN-R01` 并将 `EN-R02` 的 Mark 写入效率合并进 `EN-R01`。嫉妒从10张减为6张；总牌池从69张减为65张；类型成长卡从3张减为2张。
28. 旁白成为当前Demo明暗线的核心低成本Delivery载体；Mythic众神声与System电子声是两个独立Presentation Channel，不自动定义为两个独立世界人格。
29. Narrative Access、Trigger Event与Display Mode正式解耦；具体Wave、Elite、Card、Possession或自定义事件映射全部可配置，不在Canonical写死。
30. Card与全项目双线文本通过可配置Display Profile读取Neutral / Mythic / System版本；Card ID、图标、Gameplay效果与中性机制摘要始终稳定。
31. 首个成功Run完成System / Certification / Version / Instance / User Distillation核心揭示；后续Run以Certified Lineage / Version Trials低频深化。
32. First Clear读取本Run原始统计；主 / 次倾向评分、行为句式和Model / Version / Instance格式配置化。Self-Declaration只表达玩家态度，不改变胜负、Build、评分、结局或能力。
33. Tutorial Delivery采用数据驱动Step：TUT-01 / 02为开场准备段，TUT-03–05嵌入Waves，TUT-06 / 07与Monster微教学延迟触发；关闭教学自动放行，Step完成跨Run持久化。
34. 最终Cue、音频、文案数量与具体触发节点不作为Canonical硬指标；程序必须提供Text / Audio / Cue / Access / Tutorial / Debug配置入口，供策划后续增删和Playable调校。

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
