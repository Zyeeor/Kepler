# Possession — Narrative Voice Delivery Baseline v1.0

**Date:** 2026-08-20<br>
**Status:** `CANONICAL PRESENTATION DELIVERY CONTRACT / EXACT COPY & AUDIO OPEN`<br>
**Source:** `03_PRESENTATION_CANONICAL.md` + Owner Decision 2026-08-20

---

# 1. Purpose

本文件定义：

> 在最终旁白、音频、文案和触发节点尚未冻结时，程序仍必须提供什么策划配置能力，才能让Mythic / System双线、First Clear与后续Run Recognition低成本落地。

本文件冻结系统行为与配置边界，不冻结具体类名、ScriptableObject结构、Localization实现或内部代码架构。

---

# 2. Core Delivery Model

## 2.1 两个Voice Channel

当前正式Presentation Channel：

- `Mythic Voice`：众神 / 神谕式声音，承担意义、目标、试炼、王座与加冕语言；
- `System Voice`：冷静、礼貌、程序化的电子声音，承担Carrier、Sampling、Certification、Version、Instance与Distillation语言。

两个Channel：

- 不自动等于两个独立世界实体；
- 不自动拥有可对话的人格；
- 不争论、不吐槽、不评价玩家聪明与否；
- 可以先后描述同一Gameplay事实；
- 最终本体关系保持Open。

## 2.2 首通与后续Run

首个成功Run必须交付：

- Mythic意义层成立；
- System功能层逐步进入；
- Final认证；
- Model / Version / Instance；
- User Distillation核心功能真相。

后续Run采用：

> `Certified Lineage / Version Trials`

仅需低频Recognition承认prior certification，不重复第一次核心反转，也不冻结Soul与Original Human的最终关系。

---

# 3. Access / Trigger / Display解耦

系统必须把以下三层分开：

1. `Narrative Access`：当前Run允许出现的信息深度；
2. `Trigger Rule`：哪一个Gameplay事件满足何种条件；
3. `Display Mode`：当前载体显示Neutral、Mythic或System文本。

约束：

- Access每Run重置并单调上升；
- 删除或替换一条旁白不得导致Access永久无法推进；
- 旁白可以读取Access，也可以按配置请求推进Access，但不作为唯一硬编码入口；
- Card、阶段标题、Result与其他UI可以各自选择是否Follow Access；
- 具体Wave、Elite次数、Card次数、Possession次数与组合条件为Production配置，不是Frozen Canonical。

## 3.1 必须支持的Trigger语义

最低支持：

- New Run；
- Initial Carrier Assignment完成；
- Run Phase变化；
- Wave N开始 / 完成；
- 第N次Card Offer / Card确认；
- 第N次Possession / 主动离身 / Death Relay；
- Soul Enter / Shrine Recover；
- 第N只Elite Spawn / Fatal / Possession；
- Final Begin / Phase Change / Clear；
- Run Fail / First Clear / Subsequent Run；
- 自定义Gameplay Event；
- AND / OR条件组合。

## 3.2 必须支持的Trigger结果

- 播放一个Cue或Cue Sequence；
- 推进到指定Access；
- 修改指定载体Display Mode；
- 记录一次Run / Profile事实；
- 不产生任何Gameplay Buff、Damage或Build变化。

---

# 4. Text与Terminology

所有玩家可见叙事文本必须通过稳定Text Key读取，不在程序逻辑中写死最终中文。

需要支持：

- `Neutral`；
- `Mythic`；
- `System`。

同一个Concept Key可映射三种语言，例如：

```text
Concept: Elite
Neutral: 可配置
Mythic: 可配置
System: 可配置
```

最终术语保持Production Open，但系统必须允许策划只修改配置表而不修改代码或Prefab。

Card显示要求：

- Card ID、图标、分类和Gameplay效果始终稳定；
- Mythic / System名称与包装描述可按Display Profile切换；
- 中性机制摘要始终保持Gameplay可读；
- 普通界面默认不并排展示两版；
- 已获得Card是否同步切换由配置决定。

---

# 5. Audio Delivery

Audio资源通过稳定Audio ID引用。

最低类别：

- Voice；
- BGM；
- Gameplay SFX；
- UI。

Voice要求：

- 使用独立Voice Channel，不占用普通Gameplay SFX池；
- 支持Mythic / System两套独立音频资产；
- 支持BGM Duck；
- 支持单句和多句顺序组合；
- 支持组内不同Speaker、间隔和中断策略；
- Audio缺失时不得阻塞文本或流程。

BGM最低支持按场景或Run状态映射，并允许后续替换Clip而不改Gameplay代码。

本文件不冻结具体资产数量、命名、响度、音色或Duck参数。

---

# 6. Cue Data Contract

每个Narrative Cue最低需要表达：

- Cue ID；
- Trigger与条件；
- Access Requirement；
- 可选Access Result；
- Speaker Channel；
- Text Key；
- Audio ID；
- Subtitle Mode；
- Delay；
- Priority；
- Repeat Scope；
- Minimum Interval；
- Busy Policy；
- BGM Duck；
- 可选Display Mode Result。

`Repeat Scope`最低支持：

- 可重复；
- 每Run一次；
- 每Profile一次。

`Subtitle Mode`最低支持：

- `None`：不显示文字；
- `Optional`：受玩家字幕设置控制；
- `Forced`：核心信息必须通过字幕、阶段标题或Result UI落字。

字幕不是所有旁白的强制常驻表现。A4、First Clear和关键System Confirmation必须有可靠文字承载，但不要求使用传统底部字幕。

---

# 7. Scheduler Rules

最低调度合同：

- 同时只播放一条Voice；
- 主线Reveal高于Monster微教学和普通氛围旁白；
- 操作所需的关键Tutorial提示不得被旁白遮挡；
- 普通Cue遇到高压战斗可延后或放弃；
- 最多保留有限等待项，避免过期旁白连续补播；
- Pause时Voice与其文字表现同步暂停；
- Cue完成、取消和拒绝必须产生可调试原因；
- 同一个唯一Cue的完成处理必须幂等。

高压战斗最低可读条件由可配置Gameplay状态判断，至少可引用：

- 当前处于Final高压阶段；
- 当前Body低 / 临界耐久；
- 正在Possession Transfer；
- Card、Pause、Result或关键Tutorial UI打开；
- Elite刚进入的不可打扰窗口。

精确阈值与窗口为TUNABLE。

---

# 8. First Clear Runtime Data

First Clear继续使用`03_PRESENTATION_CANONICAL.md`固定八段结构。

Self-Declaration：

- 预留三个可配置选项；
- 选择不改变胜负、Build、评分、结局或能力；
- 可选记录`selectedDeclarationId`供后续Recognition使用；
- 最终文案保持Open。

程序需采集本Run原始数据，最低包括：

## 8.1 Per-Sin

- 有效Body控制时长；
- Possession次数；
- Movement / Attack / Special使用次数；
- Card投资数量；
- 可选击杀数。

## 8.2 Global

- Run总时长；
- 到达Wave；
- Final到达 / 完成；
- 总Possession；
- 主动离身；
- Death Relay；
- Soul Enter；
- Shrine Recover；
- 低耐久主动离身；
- Bullet Time次数与总时长；
- Elite Fatal与Elite Possession；
- 使用过的不同Sin数量。

要求：

- 程序保存原始值；
- 主 / 次倾向权重配置化；
- 行为方式描述阈值与词库配置化；
- 不进行医学、心理或人格诊断；
- Model / Version / Instance通过可替换模板生成；
- 最终ID格式保持Production Open。

---

# 9. Persistence Boundary

## Run-local

至少支持：

- 当前Narrative Access；
- 已播放的每Run Cue ID；
- Trigger计数；
- 当前Run Analytics；
- 需要恢复的等待 / 组合状态按技术方案最小保存。

## Profile

至少支持：

- First Clear完成状态；
- Certification / Version计数；
- 已播放的每Profile Cue ID；
- 可选Self-Declaration选择；
- Tutorial完成记录；
- 首次Possess过的Monster类型；
- 教学与字幕设置。

Restart只重置Run-local状态；重置教学不清除First Clear；清除全部进度才清除Profile。

---

# 10. Debug / Authoring Requirement

策划必须能够在不修改代码的情况下：

- 新增、删除和替换Cue；
- 修改Text Key与Audio ID；
- 修改触发事件与第N次条件；
- 修改Access推进规则；
- 修改Card / UI Display Mode；
- 强制设置A0–A4；
- 强制触发指定Cue / Sequence；
- 模拟Wave、Card、Elite、Possession等事件计数；
- 重置Run / Profile播放记录；
- 查看Cue未触发、延后、取消或拒绝的原因。

---

# 11. Acceptance

- 新增Cue不需要修改Gameplay代码；
- 修改Access推进节点不需要修改Gameplay代码；
- 修改Card显示切换不改变Card效果；
- 两套Voice可独立配置并按顺序组合；
- 高压战斗不会连续补播过期普通旁白；
- 关键First Clear信息有可靠文字承载；
- 读档不会错误重复每Run / 每Profile唯一Cue；
- Profile与Run状态清理边界正确；
- 最终台词、音频、术语和ID格式可后补，不阻塞系统Playable。

---

# 12. Production Open

- 最终Cue数量与具体触发配置；
- 最终旁白台词与音频；
- Access实际映射；
- Card实际切换节点；
- Self-Declaration文案；
- Functional Summary权重、阈值与词库；
- Model / Version / Instance最终格式；
- 最终玩家术语；
- 最终字幕、混音、音色与BGM Duck参数。