# Possession — Tutorial Delivery Baseline v1.0

**Date:** 2026-08-20<br>
**Status:** `CANONICAL TUTORIAL DELIVERY CONTRACT / EXACT COPY & TIMING OPEN`<br>
**Source:** `01_DESIGN_CANONICAL.md` §26 + `02_CONTENT_CANONICAL.md` §12 + Owner Decision 2026-08-20

---

# 1. Purpose

本文件将现有Tutorial内容清单补成可开发、可配置和可验证的Delivery Contract。

目标：

> 用最少的独立场景、演出、锁输入和额外资产，让玩家在真实战斗中理解移动、三槽、Corpse、Possession、高频换身、Death Relay、Soul / Shrine与各Monster独特机制。

本文件冻结教学行为，不冻结最终提示文案、图标、精确时间或程序内部类名。

---

# 2. Core Principles

- New Save使用嵌入式真实战斗教学；
- 不设置独立Wave 0；
- 不恢复Opening Card；
- 不创建只为教学存在的第二套战斗规则；
- 不暂停的微教学优先；
- 默认不锁输入；
- 不为教学强制制造一次玩家死亡；
- 教学不应永久阻塞Run；
- 提示只解释当前最需要理解的行为；
- 按键显示读取实际Input Binding；
- 教学完成状态跨Run持久化；
- 支持关闭与重看。

---

# 3. Tutorial与Run Phase关系

## Opening / Tutorial准备段

初始Pride Carrier分配完成后：

1. `TUT-01`：基础移动 / Aim；
2. `TUT-02`：三槽认知与至少一次合法尝试；
3. 进入正常Wave流程。

该准备段可提供短暂无敌人窗口，但不创建独立Wave编号。

## Waves内教学

- `TUT-03`：Kill → Corpse；
- `TUT-04`：Possession；
- `TUT-05`：主动换身。

这些步骤使用正式Spawner、Enemy、Corpse与Possession规则。

## 延迟教学

- `TUT-06`：首次真实Death Relay；
- `TUT-07`：首次真实Soul / Shrine；
- `TUT-MONSTER-*`：首次Possess某Monster。

延迟教学可以在任意后续Run阶段发生。Tutorial Controller必须贯穿整个Run，不得在离开`RunPhase.Tutorial`后停止工作。

---

# 4. Step Data Contract

每个Tutorial Step最低需要表达：

- Step ID；
- Start Condition；
- Completion Condition；
- Blocking Mode；
- Text Key；
- Input Action / Dynamic Icon；
- 可选Audio ID；
- 可选Narrative Cue；
- Repeat / Reminder Interval；
- Timeout Policy；
- Invalid Target Policy；
- Death / Scene / Save Recovery；
- Persistence Scope；
- Next Step；
- 可选Encounter Requirement。

`Blocking Mode`最低支持：

- Non-blocking；
- 只阻塞下一教学步骤；
- 短暂阻塞Wave启动。

关闭教学时：

- 不显示教学UI；
- 不播放教学音频；
- 所有阻塞条件立即放行；
- 不得出现隐藏UI但流程仍等待Step完成的状态。

---

# 5. Step Baseline

| Step | Start | Completion | Blocking | Notes |
|---|---|---|---|---|
| `TUT-01` | 初始Pride取得控制 | 有效移动且Aim方向发生变化 | 可短暂阻塞Wave | 超时后仍放行，不永久阻塞 |
| `TUT-02` | `TUT-01`完成或放行 | Movement / Attack / Special均发生至少一次合法尝试 | 可短暂阻塞Wave | 不强制全部命中；已提前使用可追溯 |
| `TUT-03` | 正常Wave中出现首个教学目标 | Enemy Fatal并产生合法Corpse | Non-blocking | 使用正式Enemy与Damage规则 |
| `TUT-04` | 首个合法Corpse出现 | Possession完成 | 只阻塞教学链 | 首个教学Corpse需保护，避免提示期间被清理 |
| `TUT-05` | 已完成首次Possession且存在下一合法Body机会 | 主动Leave或主动转移至另一Body | Non-blocking | 无第二Body时延后，不强制生成假机会 |
| `TUT-06` | 首次Body Fatal且存在合法Corpse | Death Relay成功 | Non-blocking / Delayed | 不强制制造死亡 |
| `TUT-07` | 首次Body Fatal且无合法Corpse | 进入Soul并通过Shrine恢复Body | Non-blocking / Delayed | 可跨多个Run等待 |
| `TUT-MONSTER-*` | 首次Possess对应Monster | 核心机制提示完成显示 | Non-blocking | 每Profile一次，可重看 |

精确时间、次数与条件阈值为TUNABLE，但不得破坏表中行为语义。

---

# 6. Retrospective Completion / Idempotency

系统必须保存本Run关键Gameplay Facts。

当Step开始时：

1. 先检查玩家是否已经完成对应行为；
2. 若已完成，直接幂等结算；
3. 若未完成，再监听后续事件。

要求：

- 玩家不因操作过快被要求重复同一动作；
- 重复事件不重复发放完成反馈；
- 读档、场景恢复或重复订阅不产生重复完成；
- Completion处理必须幂等。

---

# 7. Encounter / Corpse Guarantees

为保证教学可完成，Gameplay系统必须提供最低保障：

- 首个教学Enemy应是合法普通Enemy，威胁受控；
- 首个教学Corpse在`TUT-04`完成、教学关闭或保障失效前不被普通Corpse清理移除；
- 教学Corpse仍服从正式Possession语义，不创建特殊附身规则；
- `TUT-05`只在存在第二具合法Body机会时提示；
- 目标被异常回收、离场或失效时，Step重新等待下一个合法目标；
- 不因玩家跑远让教学永久Hard Lock；
- TUT-06 / 07不通过脚本强制杀死玩家。

这些保障不得创建永久安全区或可滥用资源刷新。

---

# 8. Presentation

## 8.1 Tutorial Prompt

最低提示组件：

- 标题；
- 一句中性操作说明；
- 动态Input Action图标 / 当前绑定键；
- 可选轻量进度；
- 完成反馈；
- 自动收起。

默认不需要：

- 全屏弹窗；
- 角色立绘；
- 复杂遮罩；
- 独立教学场景；
- 每一步语音；
- 长篇说明。

## 8.2 Monster Micro Tutorial

首次Possess某Monster时，自动提示优先只显示：

- Monster名称；
- 一句核心玩法；
- 最关键状态 / 资源。

三槽完整说明可展开或在暂停 / 查看入口读取。默认不暂停。

## 8.3 Audio

教学以文字、动态图标和轻量提示音为主。Voice Cue为可选包装：

- 教学Voice不承担按键说明；
- 具体按键始终由Neutral Tutorial Prompt表达；
- 教学音频缺失不阻塞Step；
- 不同时播放教学Voice与其他旁白Voice。

---

# 9. UI / Voice Priority

建议统一优先级：

```text
Pause / System Confirm
> Card Selection
> Critical Tutorial Action
> Main Reveal Voice
> Monster Micro Tutorial
> Ambient Voice
```

行为要求：

- Card / Pause打开时教学可暂时隐藏，关闭后恢复；
- Critical Tutorial文本不得被Voice字幕覆盖；
- Main Reveal可以延后到关键操作教学完成；
- Monster Micro Tutorial等待玩家恢复控制后显示；
- 普通氛围旁白可放弃；
- 同时只允许一个Voice Channel输出。

---

# 10. Timeout / Failure / Recovery

每个Step必须配置：

- 提醒间隔；
- 超时后继续等待、放行或延后；
- 目标失效后的重新获取；
- 玩家死亡后的继续、延后或重启；
- UI冲突后的恢复；
- 退出 / 读档后的恢复。

默认原则：

- 除开场短准备段外，教学不永久阻塞Run；
- 开场准备段超时后放行Wave，教学可在Waves中继续；
- 未完成Step不保存半步状态，下次从该Step重新开始；
- 已完成Step立即写入Profile；
- TUT-06 / 07允许跨多个Run等待。

---

# 11. Settings / Replay / Persistence

Profile最低保存：

- 每个Tutorial Step完成状态；
- 首次Possess过的Monster类型；
- 教学提示开关；
- 可选教学重看状态。

最低入口：

- Settings中的教学提示开关；
- 重看 / 重新显示基础操作入口；
- 重看不覆盖正式完成记录；
- 重置教学只清教学记录，不清First Clear或其他Profile进度。

---

# 12. Minimal Telemetry

最低记录：

- Step是否出现；
- Step是否完成；
- 出现到完成耗时；
- 是否关闭 / 跳过；
- 首次Possession耗时；
- 各Step未完成 / 流失情况。

该数据只用于验证教学有效性，不自动生成玩家心理结论。

---

# 13. Debug / Authoring Requirement

策划 / QA必须能够：

- 强制开始指定Step；
- 强制完成指定Step；
- 跳转下一Step；
- 重置Run / Profile教学记录；
- 模拟首次Corpse、Possession、Death Relay、Soul / Shrine与Monster Possession；
- 查看Step未开始 / 未完成原因；
- 查看当前目标与保障状态；
- 切换教学提示开关；
- 测试实际Input Binding显示。

---

# 14. Acceptance

- TUT-01 / 02完成或超时后正常进入Wave；
- 玩家提前操作不会被要求重复；
- 首个教学Corpse不会在提示期间意外清理；
- 没有第二具Body时TUT-05不会给出无效指令；
- TUT-06 / 07不阻塞基础Run；
- 教学关闭后流程立即放行；
- 改键后提示显示实际绑定；
- Card / Pause / Voice冲突后提示能正确恢复或延后；
- 已完成Step跨Run保持，未完成Step可重试；
- 新增Tutorial Step不需要修改核心Gameplay代码；
- 最终文案、音频和精确时间可后补。

---

# 15. Production Open

- 最终教学提示文案；
- 最终图标与视觉样式；
- 提醒间隔与超时数值；
- 首个教学Enemy配置；
- 教学Corpse保护时长；
- Monster微教学最终内容；
- 是否为部分Step增加Voice；
- 最终Telemetry输出与分析方式。