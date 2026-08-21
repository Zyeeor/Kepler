# Narrative / Tutorial System Decision Update Bundle — 2026-08-20

> Status: OWNER REVIEWED / APPROVED FOR CANONICAL UPDATE
>
> Source: `NARRATIVE_TUTORIAL_SYSTEM_OWNER_SOURCE_2026-08-20.md`

## Added

- 数据驱动Narrative Cue系统的正式Presentation行为合同；
- Mythic众神声与System电子声两个独立表现通道；
- Narrative Access、Trigger Event与Display Mode解耦；
- Card与全项目双线文本的可配置显示规则；
- 统一Text Key、Audio ID与Terminology映射要求；
- Voice独立通道、播放调度、优先级、繁忙策略、BGM Duck与字幕模式；
- First Clear最低Run Analytics采集与可配置评分合同；
- Run状态与Profile长期状态的持久化边界；
- 教学Step数据合同、开场/波次/延迟教学分层、Encounter保障与异常兜底；
- 旁白与教学共享Trigger / Text / Audio / Save / Debug基础设施，但保持独立Controller职责；
- `Narrative_Voice_Delivery_Baseline_v1.0.md`；
- `Tutorial_Delivery_Baseline_v1.0.md`。

## Changed

- A0–A4继续定义信息权限，但具体推进条件从固定事件语义调整为可配置映射；
- A3不再要求由唯一一只Elite或唯一高价值转移事件硬编码推进；Elite仍是合法的A3候选条件与递进旁白窗口；
- Card双线显示从抽象“由Access决定”细化为可配置Display Profile；
- First Clear的Functional Summary明确需要程序采集Run原始数据，但权重与文案保持可配置；
- R03.1最小Narrative Recognition由“若实现”提升为首通后最低Profile需求；
- Tutorial从内容事件清单补充为可执行的Delivery Contract；
- `RunPhase.Tutorial`仅覆盖开场准备语义，后续微教学可在Waves及其他Run阶段继续。

## Removed

- 不删除既有Gameplay规则、Card内容或First Clear结构；
- 不恢复Wave 0或Opening Card；
- 不要求字幕常驻；
- 不要求旁白条数、音频数量或文案数量成为Canonical硬指标。

## Conflicts

### A3固定事件语义

旧Presentation写法：

> Elite / high-value transfer → A3

新Owner决定：

> A3仍表示中局System显著进入的信息权限；其推进条件由Narrative Access Profile配置，可绑定Wave、Elite、Card、Possession或自定义事件。Elite / high-value transfer仍是默认候选与叙事窗口，但不作为底层硬编码的唯一条件。

处理：更新`03_PRESENTATION_CANONICAL.md`和`Dual_Line_Text_Requirements_v1.0.md`，保留信息权限，解除固定触发耦合。

### Mid-run Suspend / Resume

`01_DESIGN_CANONICAL.md`仍写当前Demo不要求Mid-run Suspend / Resume，但Repository已存在波间Run Save实现。本轮只定义叙事与教学状态在系统支持恢复时应保持一致，不将现有实现反向升级为新Frozen设计目标。

## Open Decisions

继续Open / Production Closure：

- 最终旁白台词与总量；
- 最终音频资产与总量；
- A0–A4具体触发配置；
- Card实际切换节点；
- Self-Declaration最终文案；
- Functional Summary精确权重、阈值与句式；
- Model / Version / Instance最终格式；
- 最终玩家术语；
- 最终字幕默认设置、音色、混音和BGM Duck参数；
- 教学最终提示文案、图标和精确超时参数。

## Files recommended for update

- `.vibe/doc/Canonical/00_CANONICAL_INDEX.md`
- `.vibe/doc/Canonical/01_DESIGN_CANONICAL.md`
- `.vibe/doc/Canonical/02_CONTENT_CANONICAL.md`
- `.vibe/doc/Canonical/03_PRESENTATION_CANONICAL.md`
- `.vibe/doc/Canonical/Content/Dual_Line_Text_Requirements_v1.0.md`
- `.vibe/doc/Canonical/Content/Narrative_Voice_Delivery_Baseline_v1.0.md`（新增）
- `.vibe/doc/Canonical/Content/Tutorial_Delivery_Baseline_v1.0.md`（新增）
- `.vibe/doc/项目设计.md`
- `Docs/02_Open_Decisions/Canonical_v1.1_Open_Decisions.md`
- `Docs/03_Decision_Log/DEC-CANON-20260820-005.md`（新增）

## Implementation handoff boundary

Canonical冻结系统必须表现出的行为和策划配置能力，不冻结具体类名与内部架构。程序Task应据此提供：

- 可配置Text / Terminology；
- 可配置Audio / BGM；
- Narrative Cue / Access / Display；
- Tutorial Step / Controller；
- Run Analytics与Profile持久化；
- 统一Debug工具。

具体ScriptableObject、Localization Table、事件总线、序列化格式和类层次进入程序Task Contract / Technical ADR。