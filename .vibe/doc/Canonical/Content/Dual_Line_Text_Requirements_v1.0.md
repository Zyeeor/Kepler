# Possession — Dual-Line Text Requirements v1.0

**Date:** 2026-08-20<br>
**Status:** `PRESENTATION PRODUCTION REQUIREMENT / DELIVERY CONFIG UPDATED`<br>
**Source:** R03.1 + Card Language Contract + latest Owner Decision

---

# 1. 核心规则

需要双线的文本遵守：

> **一事两译，机制唯一。**

- Mythic：西方黑暗神话、神圣、王权、誓约、审判、命运、祭仪；
- System：冷静、礼貌、程序化、评估 / 认证 / Carrier / Sampling / Version语汇；
- 两条线都认真，不主动制造笑点；
- 默认同一时间只显示当前Narrative Access适合的一版，不做左右并排“翻译对照”；
- 双线文案不能改变Gameplay事实；
- 玩家可见文本通过稳定Text Key与Concept Key读取，不在Gameplay代码中写死最终词汇；
- Display Profile可配置Neutral / Mythic / System / Follow Access；
- Card ID、图标、Gameplay效果与中性机制摘要不随包装版本改变；
- Narrative Access、Cue Trigger和Display Mode彼此解耦。

---

# 2. 必须双线生产的文本资产

| 载体 | Mythic版本 | System版本 | 备注 |
|---|---|---|---|
| Card | 完整名称 + 描述 | 完整名称 + 描述 | 当前65张正式资产；另保留稳定中性机制摘要 |
| Narrative Voice / Narration | 众神 / 神谕式意义与目标语言 | 冷静、礼貌、程序化的功能语言 | 两个独立Presentation Channel；具体Cue可配置 |
| Seven Sins / Monster首次介绍 | 罪之形 / 法则 / 权柄 | Training Domain / Carrier Profile | 首次出现 / 首次Possess包装 |
| Monster Micro Tutorial叙事标题 | 神话化能力称谓 | 功能化能力摘要 | 操作按键本身仍中性 |
| Initial Carrier Assignment | 灵魂承形 / 王座试炼起点 | Candidate → Pride Carrier initialization | 开场CG / 动画 |
| Corpse / Possession关键叙事提示 | 承继 / 借形 | Transfer-Eligible / Transfer | 纯RMB提示仍中性 |
| Elite入场与身份 | 高阶冠冕 / 他者之形 | Historical Build Snapshot / capability package | 需要说明高威胁+高价值 |
| Shrine / Fallback | 神坛 / 赐形 / 引路 | Emergency Fallback Carrier | 最终世界名称仍可Open |
| Wave开始 / 结束标题 | 试炼阶段 | Evaluation Batch | 不必每波都长文 |
| Final开始 / Phase Change | 王座认证 / 最终试炼 | Certification Stress Test | A4系统线显著增强 |
| Run Fail | 试炼失败 / 未获承认 | Candidate Rejection | 不写Data Erasure |
| Victory / Result | 加冕 / 完整的表层承诺 | certification / result summary | 首通与非首通可不同 |
| First Clear Self-Declaration | 第一人称神话语义 | 可保持第一人称但被系统记录 | 三个预设选项，最终文案待生产 |
| Functional Summary | 王冠 / 倾向 / 行为称谓 | Capability / Decision Pattern Summary | 结构已定，精确算法/文案待定 |
| Model / Version / Instance | 可先以称号 / 认证过渡 | 明确Model/Version/Instance | 最终ID格式待定 |
| Subsequent Run | 再次试炼 / 谱系延续 | Certified Lineage / Version Trial | 首通后 |
| Meta / Unlock（若当前里程碑出现） | 新试炼许可 / 新冠冕资格 | Evaluation Scope Authorization | 不等于Soul永久技能成长 |
| 重要世界内标识 / Shrine文案 | 神圣制度语汇 | 系统登记 / 规范语汇 | 仅用于有叙事意义的标识 |

---

# 3. 不需要双写的内容

以下保持**单一中性机制语言**：

- WASD / LMB / RMB / Space / E / Q；
- 控制设置；
- 音量 / 画面 / 辅助功能；
- CD / Reload / Charge数字；
- HP / Durability数值；
- 确认 / 返回 / 重开等纯按钮；
- 技术错误提示；
- Debug / QA文本；
- 纯数值统计表。

原则：

> **世界如何解释 → 可双线；玩家怎么操作 → 清晰优先、保持中性。**

---

# 4. A0–A4显示策略

以下定义各Access允许的信息与建议节奏，不是硬编码触发表。具体由Wave、Elite、Card、Possession或自定义事件中的哪一个推进，由Narrative Access Profile配置。

## A0 — 开场分配前

- Mythic几乎完全主导；
- 只允许非常有限的System初始化迹象；
- 不直接泄露User Distillation。

## A1 — 初始Pride Carrier / 早期Combat

- Mythic主导；
- Transfer / Carrier / Initialization可以作为轻微系统语汇渗入；
- 第一次真正Gameplay控制已经是A1。

## A2 — 首个Post-Wave Card后

- Mythic仍是主要玩家语言；
- System版本开始在Card / Wave /评估类文本中有存在感；
- 可以出现sampling / domain weight / behavioral pattern一类词。

## A3 — 中局System显著进入

- 具体由Wave、Elite、Card、Possession或自定义事件中的哪一个推进，由Narrative Access Profile配置；
- Elite / 高价值转移是合法候选条件与递进旁白窗口，但不是唯一硬编码入口；
- 明暗开始明显并置，但默认仍不把同一条文本两版同时显示；
- System可以明确谈capability package / historical profile / awareness monitoring。

## A4 — Final / Certification

- System线显著加强；
- Mythic的“王座 / 完整 / 加冕”继续作为表层闭环；
- First Clear完成从Mythic closure到System confirmation的转换。

## Voice文字承载

每个Cue可配置：

- `None`：不显示文字；
- `Optional`：受玩家字幕设置控制；
- `Forced`：通过字幕、阶段标题或Result UI可靠落字。

普通氛围旁白不要求常驻字幕。A4、First Clear与关键System Confirmation必须有可靠文字承载，但不限定为传统底部字幕。

---

# 5. First Clear生产需求

必须保留以下结构：

1. Mythic closure / 王座；
2. “我是谁？”；
3. 三个第一人称Self-Declaration选项；
4. Functional Summary：1主倾向 + 1次倾向 + 1句行为风格；
5. Model / Version / Instance；
6. System Confirmation；
7. 黑屏 / 短停顿；
8. **“用户蒸馏完成。”**

当前需要后续生产而非重新设计世界观的内容：

- 三条Self-Declaration最终文案；
- Functional Summary称号 / 句式词库；
- 精确评分阈值；
- ID格式；
- Shrine最终名称；
- God/System最终玩家称谓。

---

# 6. Terminology约束

不得让以下概念在UI里无控制地出现大量同义词：

- Body / Carrier / Vessel / Shell / Host；
- Soul / Candidate；
- Corpse / Transfer-Eligible Body；
- Elite / Historical Build；
- Final / Certification。

允许Mythic和System各自有一套稳定术语，但同一条线内部必须一致。

---

# 7. Acceptance

- 玩家不看系统词典也能理解操作；
- 两线替换后Gameplay含义完全一致；
- 黑色幽默来自反差，而不是任何一边主动讲笑话；
- A0–A4能感到信息层逐步变化；
- 首通前不提前泄底；
- First Clear能明确完成“王座承诺 → 用户蒸馏完成”的主题翻转；
- 后续Run能自然承接Certified Lineage / Version Trials。
