# ENG-POSS-001 Boss 配置测试指南

> 面向策划与测试。本文件汇总七重收束 Boss 的可调项、当前默认值和验收方法；它不是 Canonical 设计文档。所有数值以 Boss 预制体 `Boss_Sevenfold_Convergence_new` 的 Inspector 和下列脚本默认值为准。

## 使用方法

1. 打开 `Assets/Prefabs/Monster/Boss_Sevenfold_Convergence_new.prefab`，在根节点选择 `BossSevenfoldActor`。
2. 在 `Abilities` 子节点选择对应的 `EnemyAbility_*` 组件，调节技能本体数值。
3. `BossTeleportPlanner` 与 `BossSpatialDistortionController` 会在运行时自动补齐；其核心数值已由 Boss 根节点的 `Void Walk Landing` 或脚本默认值覆盖。若需要将特效细调做成预制体配置，应由程序先将组件显式挂入预制体。
4. 每轮只修改一个参数组；记录关卡、Boss 阶段、玩家附身怪物、距离和复现步骤。

## Boss 根节点（`BossSevenfoldActor`）

| 参数 | 默认值 | 作用与建议 |
| --- | ---: | --- |
| `baseBossMaxHealth` | 8000 | Boss 战初始生命。 |
| `normalDamageMultiplier` | 1.65 | Boss 继承的普通伤害倍率。 |
| `minimumEscortCount` / `escortRefreshSeconds` | 3 / 18 s | 最低护卫数量与补充检查间隔。 |
| `bossCombatScaleMultiplier` | 2 | Boss 本体、可缩放技能命中盒、投掷物和召唤物的倍率。激光例外，长度和范围不随此值放大。 |
| `teleportFarTriggerDistance` | 16 m | 玩家距离达到该值后进入远距追击判断。 |
| `teleportEmergencyDistance` | 24 m | 达到该值时立即优先虚空行走。 |
| `teleportFarTargetDelay` | 1.2 s | 远距但非紧急时的等待时间。 |
| `voidWalkInterval` | 5 s | 即使玩家不远，每隔该时间也会尝试虚空行走。 |
| `voidWalkMinPlayerDistance` | 5 m | 落点安全下限，避免 Boss 与玩家身体重叠。 |
| `voidWalkPreferredPlayerDistance` | 5 m | 落点期望距离；原默认 10 m，现已减半。 |
| `voidWalkMaxPlayerDistance` | 6 m | 落点最远距离。应不小于期望距离。 |

虚空行走落点在默认配置下为 **5–6 m**；这是“更近一半”与不贴身重叠之间的安全折中。改动三项落点距离后，务必保持 `min <= preferred <= max`。

## 虚空行走与外观（运行时组件）

`BossSpatialDistortionController` 控制黑洞、出入场、暗黑环绕与材质扭曲。当前脚本默认值如下：

| 参数 | 默认值 | 作用 |
| --- | ---: | --- |
| `idleAmplitude` / `idleFrequency` | 0.18 / 1.1 | 待机悬浮幅度与频率。 |
| `distortionStrength` | 0.25 | 身体材质的空间扭曲强度。 |
| `teleportOutDuration` | 0.75 s | 入黑洞前摇。 |
| `teleportInDuration` | 0.85 s | 出黑洞后摇。 |
| `hitboxRestoreDelay` | 0.60 s | 后摇期间恢复受击碰撞的时机；不得大于后摇总时长。 |
| `departureRiftLifetime` | 8 s | 起点黑洞的残留时间。 |

验收：Boss 消失前应完整进入黑洞、到达后从另一黑洞出现；前后摇期间不应瞬间普攻。传送完成后必须立即接 `EnemyAbility_SwordQi` 或 `EnemyAbility_GluttonyDevour`；吞噬仅在其实际有效距离内可选。

### 七具不朽尸身与开战提示

- 每具 Boss 保留尸体都会自动挂接运行时 `BossReserveCorpseVisualFx`。它使用 `BossSevenfoldDistortion` 的独立运行时材质，在尸身脚下生成三层罪印灵环；七种罪使用不同的色相、旋转速度和半径。它不会改动普通怪物 Prefab 或共享材质。
- Boss 变为可行动时，HUD 顶部会显示 5 秒提示：`boss战开启，使用场上的七具不朽尸身与之作战！`。`BossBattleAnnouncementUI` 优先使用项目注入的中文字体，缺失时使用系统中文字体兜底，避免出现乱码或方框；显示时长与淡出时长位于其 `displayDuration` / `fadeDuration`。
- `BossHealthBarUI` 会在 Boss 可行动期间常驻屏幕顶部，左侧固定显示 `Boss`，条高为 32 px（按主角现有细条的两倍规格），并实时绑定 `BossSevenfoldActor.currentHealth / maxHealth`；Boss 死亡后自动隐藏。
- 验收：七具尸体均可辨认出不同的灵环表现；脱离附身后灵环恢复；Boss 战结束或尸体回收后灵环必须消失。提示仅在该次 Boss 战开始时出现一次，并且中文不应显示为方块。

## 阶段与 AI

| 配置/规则 | 当前值 | 说明 |
| --- | --- | --- |
| 阶段阈值 | 70% / 35%（代码规则） | >70% 为 P1，70%–35% 为 P2，<=35% 为 P3。当前位于 `BossSevenfoldActor.CombatPhase`。 |
| 决策间隔 | 0.30 s | `BossCombatBrain.decisionInterval`。 |
| 施法距离 | 技能真实射程 | AI 只会选中距离、视线、状态均成立的技能；打不到时不应使用傲慢普攻等近战。 |
| 记忆抑制 | 最近 2 次 | 避免连续同技能/同技能族。 |
| 传送后吞噬优先率 | 45%（代码规则） | 吞噬和剑气均可用时的吞噬随机权重；否则选择可用的一个。 |

## 关键技能配置

以下参数都在对应 `EnemyAbility_*` 组件上；范围、投掷物尺寸、召唤物尺寸默认遵从 Boss 的 2 倍战斗缩放，激光除外。

每个技能还共用继承自 `EnemyAbility` 的 `cooldown`、`damage`、HP 消耗、命中反馈和激活特效等字段。除下表中明确为“代码固定”的规则外，优先在相应技能组件的 Inspector 调节，而不是修改 Boss 根节点。

| 技能 | 重点参数与默认值 | Boss 专属规则 / 验收 |
| --- | --- | --- |
| `EnemyAbility_PrideBlinkChain`（穿梭斩） | `searchRange` 8 m；`blinkCount` 4；`bossBlinkInterval` 0.55 s；`bossStrikeRadius` 1.25 m；`passThroughDistance` 1.2 m | Boss 冷却固定 15 s。锁定玩家当时位置后可见冲刺；玩家移动或释放 Mobility 都能躲掉并打断余段。 |
| `EnemyAbility_SwordQi`（剑气） | `maxRange` 12 m；`projectileSpeed` 15；`projectileDelay` 0.3 s；`projectileWidth` 1.5；`projectileHeight` 2；`blastRadius` 4 | 虚空行走后的优先追击之一；只有玩家在实际射程内才可释放。 |
| `EnemyAbility_GluttonyDevour`（吞噬） | `range` 1 m；`angle` 100°；`damageAmount` 40；`hitDelay` 0.5 s；`executeHealthFraction` 20% | 虚空行走后的优先追击之一；只在近身实际范围可释放。 |
| `EnemyAbility_SlothDrone`（无人机） | `droneLifetime` 4 s；`tossHeight` 4；`tossDuration` 0.35 s；`deathBlastDamage` 20；`deathBlastRadius` 3 | 开战立即召唤；Boss 冷却固定 15 s。上限按阶段 P1/P2/P3 为 1/2/3；场上未达上限才会补召。 |
| `EnemyAbility_LustRoundTrip`（迷情往返） | `segmentDamage` 20；`mistWidth` 1；`mistSpeed` 14；`mistRange` 8；`linkDuration` 6 | 以玩家方向扇形释放；P1/P2/P3 分别 1/2/3 发，三发总扇角当前为 ±24°（代码规则）。 |
| `EnemyAbility_SlothChargeShot`（蓄力炮） | `bossPatternCooldown` 6 s；`maxChargeTime` 2 s；`min/maxBlastRadius` 1.5 / 4；`min/maxDamage` 2 / 100；`projectileSpeed` 30；`maxRange` 15 | Boss 序列为 1.5 s 内每 0.3 s 短射，随后蓄力 1.5 s 发射满蓄力炮；序列中禁止其他技能。时序当前为代码规则。 |
| `EnemyAbility_EnvyLaser`（激光） | `maxRange` 15 m；`damagePerSecond` 2；`tickInterval` 0.25 s；`maxConnectDuration` 8 s | 激光长度和范围**不受 2 倍缩放**；蓄力炮序列期间停止发射。 |
| `EnemyAbility_GreedHands`（念力魔手） | `maxDaggers` 6；`regenInterval` 1 s；`detectRange` 8 m；`launchInterval` 0.3 s；`homingSpeed` 20 | 调节库存、索敌、弹道和命中伤害；AI 以 `detectRange` 作为实际选择上限。 |
| `EnemyAbility_GreedGuard`（大手 Guard） | `baseDuration` 1 s；`extendedDuration` 3 s；`absorbPerHand` 100 | 调节护盾时长和吸收转化效率。 |
| `EnemyAbility_WrathSlam`（双拳砸地） | `radius` 3 m；`firstHitDelay` 0.15 s；`burnRadius` 3 m；`burnDps` 5；`burnDuration` 3 s；`baseCooldown` 2 s | 调节近战命中、前摇和灼烧场。 |
| `EnemyAbility_WrathChainStorm`（暴怒锁链） | `baseDuration` 2 s；`tickInterval` 0.5 s；`pullRadius` 5 m；`tickDamage` 12；`pullStepMax` 1.2 | 调节拉拽范围、频率、伤害和不同重量目标的拉拽比例。 |
| `EnemyAbility_LustSoulPull`（诱引牵魂） | `pullWindow` 0.60 s；`pullMaxDistance` 6 m；`pullDamage` 25 | 需要有效锚点与链接目标；无合法目标不得进入冷却。 |
| `EnemyAbility_GluttonyAbyssMaw`（深渊巨口） | `maxAimDistance` 8 m；`warnDelay` 0.5 s；`blastRadius` 2.2 m；`damageAmount` 20；`secondBiteDelay` 0.5 s | 调节预警、落点距离、范围、伤害与连咬节奏。 |
| `EnemyAbility_EnvyThunderstorm`（雷暴兑现） | `baseThunderDamage` 10；`searchRadius` 30 m；`telegraphDuration` 0.6 s；`chainDelay` 0.08 s | 仅对带嫉妒印记的合法目标发动；调节结算伤害、搜索和预警。 |

`BossCombatBrain` 会按各技能组件的真实 `maxRange`、`radius`、`pullRadius` 或 `detectRange` 判定。调节伤害、冷却、命中范围或视觉资源时，直接在各自 `EnemyAbility_*` 组件中配置；不要以 Boss 缩放代替单个技能的平衡调整。

## 七具可附身尸体与进场检查

| 项目 | 期望结果 |
| --- | --- |
| 进场 | Boss 接管后，七具保留尸体应创建在玩家身旁，按相邻的 4+3 队列排布。 |
| 附身 | 附身期间不因被动衰减扣血；受到 Boss 的合法伤害才扣血。 |
| 脱离 | 脱离附身不消散，恢复为可再次附身的尸体。 |
| 视觉 | 进场时应能直接看到七具尸体；若没有，优先检查运行日志中的资源目录与生成失败信息。 |

## 最小测试矩阵

1. 站在 Boss 6 m、16 m、24 m 处各测试一次：确认 5 秒周期、远距压力和紧急追击均可触发，且落点始终在 5–6 m。
2. 传送后分别测试剑气和吞噬：剑气在中距离可用，吞噬超出实际范围时不得被选中。
3. 在穿梭斩冲刺中横移和释放 Mobility：均不应受到锁定命中，Mobility 后余段应停止。
4. 将 Boss 血量分别维持在 80%、50%、20%：确认无人机上限和迷情往返发数为 1/1、2/2、3/3。
5. 测试蓄力炮全序列：短射间隔约 0.3 s，蓄力期间无其他 Boss 技能，随后有满蓄力炮。
6. 修改 `bossCombatScaleMultiplier` 后复测剑气、无人机、蓄力炮和激光：前三者变大，激光射程与长度不变。
