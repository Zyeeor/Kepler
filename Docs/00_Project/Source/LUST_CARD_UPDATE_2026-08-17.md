# Lust Card Update — 2026-08-17

**Project:** Possession  
**Type:** Decision Update Bundle + Owner Decision Record  
**Status:** `OWNER APPROVED / CANONICAL SYNCED`  
**Decision date:** 2026-08-17  

## Source boundary

- 仅以Owner在2026-08-17对话中确认的第二张截图为目标输入。
- 第一张截图为误上传，不参与变更判断。
- “同时射出两发迷气”不属于本轮设计，也不作为旧牌池删除项。
- Owner后续确认保留色欲Type Growth卡，并将其重写为Body基础移速提高 + 三槽冷却减少30%。
- 本轮只同步设计真源，不修改Unity实现资产。

## Added

新增5个正式Card ID：

- `LU-M05` — 同欲之影：Anchor模仿本体普通攻击。
- `LU-S05` — 同欲相噬：同一次色欲Special中，被牵引目标相互碰撞时爆炸。
- `LU-A03` — 欲痕殉爆：Linked目标死亡时消费Link并爆炸。
- `LU-S06` — 无害之拥：被牵引目标在牵引期间不能伤害玩家当前Possessed Body。
- `LU-A04` — 色欲潮汐：普通攻击替换为以色欲为圆心向外扩散的环形迷气。

## Changed

- `LU-M03` — 背离之罚：明确为与Anchor成功换位后引爆原Anchor位置，首轮数值为30伤害、2.5米半径、0.15秒延迟。
- `LU-TG01`保留正式ID并改名为“欲潮不息”：提高色欲Body基础移速，Movement / Attack / Special三槽冷却均减少30%。
- 色欲正式牌池从10张改为7张。
- 全项目正式牌池从82张改为79张。
- Type Growth保持7张，每Sin 1张。
- 色欲7张全部`Stack Max = 1`。

## Removed

以下8张旧色欲卡正式退役：

- `LU-A01` — 誓约回路
- `LU-A02` — 群魂系带
- `LU-M01` — 不毁之契
- `LU-M02` — 双印圣约
- `LU-S01` — 远誓召回
- `LU-S02` — 不可抗拒的誓约
- `LU-S03` — 誓印神罚
- `LU-S04` — 契约见证者

历史已删除的`LU-M04`不复用；Anchor模仿卡使用新ID `LU-M05`。

## Conflicts resolved

### `LU-S05`与基础Special牵引重复

当前Canonical基础Special已经负责把Linked目标拉向Anchor，因此不再把第3张卡写成“解锁/重复牵引”。Owner确认改为：

> 被同一次色欲Special牵引的目标相互碰撞时，在碰撞位置产生爆炸。

首轮数值：25伤害、2米半径；每个目标每次Special最多参与一次碰撞爆炸，4个目标最多形成2次；不响应墙壁、Anchor、玩家或未被牵引目标的碰撞。

### `LU-A04`与往返攻击

`LU-A04`是Attack形态替换卡：获得后，基础去程/回程迷气替换为环形扩散迷气；未获得时基础Attack仍按Canonical执行。

### `LU-M05`与`LU-A04`组合

若同时持有，Anchor模仿当前普通攻击形态：在Anchor位置生成一圈50%伤害的环形迷气；模仿不再触发新的模仿事件。

### `LU-TG01`旧距离成长缺乏收益

旧“Movement距离 + Special牵引距离”与当前色欲基础技能收益重复：位移更远缺少明确价值，基础Special已可远距离牵引。Owner确认保留Type Growth身份与`LU-TG01` ID，改名为“欲潮不息”，效果改为提高色欲Body基础移速，并使Movement / Attack / Special三槽冷却均减少30%。该卡不缩短技能前摇、后摇或敌人最低预警时间。

## Exact-number status

以下均为Owner允许自动补齐的首轮`TUNABLE / PLAYABLE` Baseline，不视为冻结：

- `LU-M03`：30伤害；2.5米；0.15秒。
- `LU-M05`：50%伤害；0.10秒延迟。
- `LU-S05`：25伤害；2米；每目标单次Special最多参与一次。
- `LU-A03`：30伤害；2.5米；0.10秒。
- `LU-S06`：100%来源伤害隔离；结束后0.15秒宽限。
- `LU-A04`：0.5至4米；0.8秒扩散；0.8米环宽；20伤害；单目标单次最多命中一次。
- `LU-TG01`：三槽冷却减少30%为Owner确认值；Body基础移速增幅为首轮`TUNABLE / PLAYABLE`参数。

## Open Decisions

- 无阻塞Canonical的Open Decision。
- 上述可调数值需要后续Playable验证，不得自行改写已确认的触发关系、形态替换、Link消费、防递归边界或`LU-TG01`三槽冷却减少30%的Owner确认值。

## Canonical files updated

- `.vibe/doc/Canonical/00_CANONICAL_INDEX.md`
- `.vibe/doc/Canonical/01_DESIGN_CANONICAL.md`
- `.vibe/doc/Canonical/02_CONTENT_CANONICAL.md`
- `.vibe/doc/Canonical/04_FINAL_CLOSURE_AUDIT.md`（仅追加历史快照已被后续Delta取代的说明）
- `.vibe/doc/Canonical/Content/Card_System_Current_Truth_v1.1.md`
- `.vibe/doc/Canonical/Content/Dual_Line_Text_Requirements_v1.0.md`

## Implementation impact — not executed in this change

后续实现Task需要覆盖：

- `Assets/Configs/CardLibrary.asset`
- 正式Lust Prefab与Anchor / Link运行时状态
- Movement / Attack / Special Ability实现
- 牵引目标碰撞配对、每目标单次登记、防递归与伤害来源隔离
- `LU-M05`与`LU-A04`组合行为
- `LU-TG01` Body基础移速与三槽冷却缩减
- Play Mode验证与数值调校
