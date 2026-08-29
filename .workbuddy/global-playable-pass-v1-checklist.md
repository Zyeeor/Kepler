# Global Playable Pass v1 — 实施 Checklist（断点续传）

> 任务唯一真源：`D:/开普勒/八月正式开发/调参/交接文档/第一版GPT给的调参文件.md`
> 优先级：Implementation MD > 仓库 Runtime/Scene/Prefab/Code 真值 > Canonical（仅背景）
> 正式战斗场景：`Assets/Scenes/EnemyAiTest.unity`（MainMenuController.battleSceneName）

## 关键真值定位（摸底结论）

| 系统 | 文件 | 关键字段 |
|---|---|---|
| 刷怪编排 | `Scripts/Levels/Waves/WaveManager.cs` | 连续刷怪 continuousSpawning、速率曲线、Elite schedule |
| 刷怪调度 | `Scripts/Levels/Spawning/RunSpawnDirector.cs` | ActiveCombatSeconds、Boss 420s、Minion |
| 刷怪基础 | `Scripts/Levels/MapStreaming/MonsterSpawner.cs` | 精英取点、尸体FIFO、配额 |
| 精英投放 | `Scripts/Systems/Elite/EliteBuildDirector.cs` | Elite奖励、HP/ATK系数 |
| 卡牌/宝石 | `Scripts/Levels/Rooms/CardManager.cs` | 开局gem、Elite奖励、Offer算法 |
| 选卡宝石 | `Scripts/Levels/Rooms/CardChoiceGemPickup.cs` | 拾取流程 |
| 附身 | `Scripts/Combat/Possession/PossessionManager.cs` | cooldown、minPossessTime、decay、自动BT |
| 子弹时间 | `Scripts/Combat/Possession/BulletTimeController.cs` | duration、timeScale |
| 灵魂 | `Scripts/Combat/Actors/SoulActor.cs` + `PlayerHealth.cs` | 移动、衰减 |
| 开场 | `Scripts/Systems/GameFlow/OpeningLandingSequence.cs` + `Scripts/Tutorial/TutorialController.cs` | 降落、开场载体 |
| AI参数 | `Assets/Configs/MonsterAIConfig.asset` | Gluttony/Wrath 等 |
| Soul参数 | `Assets/Prefabs/VFX/Prefabs/Player.prefab` | soulMaxHealth=1000、healthDecayPercent=0.02 |
| 精英schedule | `Scripts/Levels/Waves/ContinuousSpawnEntry.cs` | eliteSpawnTimeSeconds、HP/ATK系数 |

## 场景 EnemyAiTest.unity 真值（正式生效 = GameObject 396728508 上的 WaveManager &396728509）

- `normalSpawnRateByMinute`: 2key(0→0.3, 7→1.35) → **MD 改 8key**
- `continuousSpawnOrder`: 90/165/225/270/300/330/360，HP/ATK 全 2/2 → **MD 改 50/100/150/200/250/300/350 + 按Sin HP/ATK**
- `continuousSpawnMaxCountsByMinute`: 4,6,8,10,12,14,16 → **MD 改 5,6,8,10,11,12,14,14**
- `monsterHealthMultiplierByMinute`: 1→1.85，`monsterAttackMultiplierByMinute`: 1→1.5 → **保持不动（MD §3.3）**
- `nonBossDurationSeconds`: 420 → **保持（MD §15.1）**
- CardManager `openingGemCount: 2` → **MD §2.1 删开局2Gem**
- MonsterSpawner `eliteSpawnScreenDiameterFraction: 0.25` → **MD §4.5 改 0.6-0.7（约0.65）**
- `corpseDissipationThreshold: 5` → **保持（MD §7.1）**
- prefab override `possessionDecayPercent: 0.04` → **保持 4%（MD §7.1）**

## Phase 状态

### Phase 1：Opening / Card-Gem / Spawn / Elite（任务 #1）— ✅ 代码+场景完成，待 Unity 编译验证
- [x] §1.1 固定 Pride Corpse + Soul 落地亲手 Possess（TutorialController.OpeningCarrierRoutine 改造：不依赖 autoPossessOpeningCarrier，刷 Pride corpse 不自动附身）
- [x] §1.2 Pride 保护（复用 SpawnAsPermanentCorpse：窗口无限、不进 FIFO/Pool，+ 新增 IsOpeningCarrier 标记）
- [x] §1.3 Pre-Combat 门（RunSpawnDirector.CombatStarted + PossessionManager 首次 Possess Opening Carrier 触发 + PlayerHealth Soul 衰减门）
- [x] §2.1 删开局 2 Gem（场景 openingGemCount: 2→0）
- [x] §2.2 Starter Gem 30s（CardManager.starterGemTime=30 + SpawnStarterGem，复用 Gem Pickup/Attract）
- [x] §2.3 Elite 奖励按 Sin（ContinuousSpawnEntry.eliteRewardPickCount + WaveManager.GetEliteRewardPickCount + EliteBuildDirector 按 Sin 单/双选）
- [x] §2.4 不做10张补偿（确认无 Card Budget 补偿系统，无需改）
- [x] §2.5 Focus Assist 0.65（CardManager.focusAssistProbability + ResolveFocusAssistSin）
- [x] §2.6 第一次 Reverse-BD 提示（TutorialController.ShowPrompt 公开入口 + CardManager 首次选卡调用 + TutorialConfig 加 TUT-REVERSE-BD 队列提示）
- [x] §3.1 Spawn Rate 8key（场景 normalSpawnRateByMinute → 0.35/0.5/0.65/0.82/0.98/1.12/1.22/1.28）
- [x] §3.2 Normal Cap（场景 continuousSpawnMaxCountsByMinute → 5,6,8,10,11,12,14；MD 8值第8个14与第7个合并）
- [x] §3.3 HP 1.85 / ATK 1.5 保持（场景已是，无需改）
- [x] §4.1 Elite Schedule（场景 continuousSpawnOrder → 50/100/150/200/250/300/350）
- [x] §4.2 Elite HP/ATK 按 Sin（场景 → Pride1.5/1.15、Sloth1.75/1.2、其余2.0/1.25）
- [x] §4.3 Elite 并发不限（确认无 Pending Queue/Elite Debt，无需改）
- [x] §4.4 Elite 存在时 Normal Rate ×0.65（WaveManager.elitePresentNormalSpawnMultiplier=0.65 + HasActiveElite，只乘一次）
- [x] §4.5 Elite Spawn Position 中圈 0.6-0.7（MonsterSpawner.eliteSpawnScreenDiameterFraction 0.25→0.65，Range 0.1-0.5→0.1-0.8）

### Phase 2：Soul / Possession / BT / Elite Body（任务 #3）— ✅ 代码+Prefab完成，编译通过
- [x] §6 Soul MaxHP400（Player.prefab soulMaxHealth 1000→400）/ decay 3%（0.02→0.03）/ Slash damage 20（50→20）/ cooldown 1 保持 / range 3.5 保持 / moveSpeed 15 保持
- [x] §6 隐藏 soulTime HUD（确认：当前无 HUD 显示 soulTime，无需改）
- [x] §7.1 Body Decay 4% 保持（possessionDecayPercent 已是 0.04）
- [x] §7.2 Body→Body minPossessTime 1s（BeginPossessionFlight 换身路径加检查，bossBattleSwitchMode 豁免）
- [x] §7.3 F 主动离身无额外 CD（RequestRelease → startCooldown:false）
- [x] §7.4 Body 死亡锁 1.5s（bodyDeathPossessionLock=1.5，独立于主动离身）
- [x] §7.5 Commit 后 0.25s 免伤（postPossessDamageImmunityDuration=0.25 + BulletTimeController.ApplyDamageImmunityForDuration）
- [x] §8.1 移除 Commit 后自动 BT + E 手动（移除 TriggerBulletTime 调用；PlayerController 补 Skill3 采集 + MonsterActor.ExecuteButtons 补 Skill3→TriggerBulletTime）
- [x] §8.2 每 Body 1 Charge（bulletTimeChargesPerBody=1 + MonsterActor.bulletTimeChargesRemaining + TriggerBulletTime charge 检查）
- [x] §8.3 BT Duration 2s / timeScale 0.2 保持
- [x] §9 Elite Possessed Body 满HP（preserveEliteHealth=false）+ 不再免疫4%衰减（移除 IsElite return）

### Phase 3：Enemy AI / Collision / Telegraph / Summon（任务 #4）— ✅ 完成，编译通过
- [x] §5 Enemy Summon Cap 4（MonsterSpawner.enemySummonCap=4 + SummonActor 全局管理，只统计 AI Enemy 创建，回收最老）
- [x] §11.1 Gluttony aiMin 1.5→2.2 / Eagerness 0.58→0.50（MonsterAIConfig.asset）
- [x] §11.2 Wrath Chase 0→1 / Eagerness 0.50→0.56 / Mobility 0.24→0.32（MonsterAIConfig.asset）
- [x] §12 Envy/Lust 碰撞伤害 30→15（enemyStats 侧，possessedStats 保持 30）
- [x] §13.1 Pride ChargeStrike 矩形 Telegraph（override GetEnemyTelegraphGeometry 返回 Rect，长度=chargeDistance，宽度=hitRadius×2，方向=owner.forward）
- [x] §13.2 Gluttony Devour 扇形 Telegraph（shader 加 Sector/_SectorAngle + 枚举+geometry+MonsterAbilityTelegraph+override，角度 100°、范围 range×2）
- [x] §13.3 Envy Laser 动态 Lock + 有限 Tracking（enemyTrackingTurnSpeed=100°/s + GetEnemyTrackedAimPoint 平滑追踪 + 丢失目标结束 Cast + enemyCastLeadTime 0.8 + maxConnectDuration 1.0）
- [x] §13.3 动态 Lock 警示视觉（EnemyAbility 加 TelegraphBegin/Tick/End 钩子 + EnvyLaser RefreshLockLine 由弱到强锁定线，复用 beamPrefab，宽度 0.12→1 渐变）
- [x] §14 Hit Feedback 节流检查（结论：已有 _hitFeedbackFiredThisAttack 首击触发机制，无需改）

### Phase 4：Boss / 整合 / 验证（任务 #5）— ✅ 完成，编译通过
- [x] §15.1 420s Takeover 保持（bossCombatTime=420 已是）
- [x] §15.2 Boss HP 6500（DefaultBossMaxHealth 7777→6500 + prefab 8000→6500）
- [x] §15.3 Minion Initial 2 / Refresh 20s（minimumEscortCount 3→2 + escortRefreshSeconds 18→20）
- [x] §18 最低验证（编译验证：4 次 AssetDatabase.Refresh 全部无 CS error / 无 shader error；PlayMode 需用户在 Unity 里跑）
- [x] §19 Owner 回复（见本会话最终回复）

## 遗留项（需后续补）

（无——§2.6 与 §13.3 视觉已于本轮补齐。）

## Owner 实测 BUG 修复轮（2026-08-29）

Owner 实测报 5 个 bug，本轮修复 4 个 + 1 个待验证：

1. **开局多了非傲慢尸体** → 出生点神龛 `RageStatue.prefab`（`mode=RandomFromCatalog`）在玩家灵魂落地后立即刷随机躯体。修复：`PossessionBodyProvider.ProvideBody` 加 Pre-Combat 门（`!CombatStarted` 且非 Boss 模式时 return），开场固定 Pride corpse 只由 TutorialController 负责。
2. **E 无子弹时间** → 代码链路已完整核对（PlayerController.Tick 采集 Skill3 → Actor.Update 调 ExecuteButtons → MonsterActor.ExecuteButtons Skill3 分支 → PossessionManager.TriggerBulletTime → charge 检查 → BulletTimeController.Trigger），静态无断点。**已加 [BT-Debug] 诊断日志**（ExecuteButtons 按下帧 + TriggerBulletTime 两个拦截分支），Owner 下次 PlayMode 按 E 时 Console 会明确显示断点：①manager 为 NULL ②CurrentBody != this ③State 非 Possessing ④charge 用完 ⑤成功触发。
3. **首次刷宝石看不到** → Starter Gem 直接 SpawnCardOfferGem 无掉落动画，战斗中难察觉。修复：`SpawnStarterGem` 加 `StartDrop`（从上方 1.5m 掉落）。
4. **精英宝石不显示直接选卡** → 单颗精英宝石 `scatter=0` 落在玩家脚下立即被吸附。修复：`SpawnCardOfferGemScatter` 单颗也散开（保留弹射动画）。
5. **暴食扇形脚下多红圈** → shader Sector 分支 `edgeArc` 无角度限制，在 distS=0.84 处画了整圈圆环。修复：`MonsterTelegraphIndicator.shader` Sector 分支加 `arcMask = step(angAbsS, halfAngleS)` + 侧边 `step(distS, 0.84)`。
6. **主界面两次**（Owner 澄清：开的是 MainMenu scene，非本任务 bug，忽略）。

## 新增可配字段记录

| 字段 | 文件 | 默认值 | 说明 |
|---|---|---|---|
| CombatStarted（运行时门） | RunSpawnDirector.cs | false | Pre-Combat 门，首次 Possess Opening Carrier 置 true |
| starterGemTime | CardManager.cs | 30f | Starter Gem 生成时刻（战斗秒） |
| starterGemOffset | CardManager.cs | (0,0,1.5) | Starter Gem 相对玩家锚点前方偏移 |
| focusAssistProbability | CardManager.cs | 0.65f | Focus Assist 概率 |
| eliteRewardPickCount | ContinuousSpawnEntry.cs | 1 | 精英击杀选卡次数（1=单选，2=双选） |
| elitePresentNormalSpawnMultiplier | WaveManager.cs | 0.65f | Elite 在场时普通怪速率乘数 |
| eliteSpawnScreenDiameterFraction | MonsterSpawner.cs | 0.65f（原0.25） | Elite 刷点屏幕直径比例（Range 0.1-0.8） |
| IsOpeningCarrier（运行时标记） | MonsterActor.cs | false | 开场载体标记，供 Pre-Combat 门识别 |
| bodyDeathPossessionLock | PossessionManager.cs | 1.5f | Body 死亡后附身锁定（失败惩罚，独立于主动离身） |
| postPossessDamageImmunityDuration | PossessionManager.cs | 0.25f | Commit 后免伤时长 |
| bulletTimeChargesPerBody | BulletTimeController.cs | 1 | 每 Body BT Charge 次数 |
| bulletTimeChargesRemaining（运行时） | MonsterActor.cs | - | 当前 Body 剩余 BT Charge |
| enemySummonCap | MonsterSpawner.cs | 4 | AI Enemy 创建 Summon 全局上限 |
| enemyTrackingTurnSpeed | EnemyAbility_EnvyLaser.cs | 100°/s | Enemy 激光有限追踪转向速度 |
| lockLineMinWidthMultiplier | EnemyAbility_EnvyLaser.cs | 0.12f | 前摇 Lock 线最弱宽度（progress 0→1 渐变到 1） |
| lockLineMaterial | EnemyAbility_EnvyLaser.cs | null | Lock 线材质（空=沿用 beamMaterial） |
| DefaultBossMaxHealth | BossSevenfoldActor.cs | 6500 | Boss 最终有效 HP |

## 编译验证状态
- ✅ Unity MCP 连通，已执行 4 次 AssetDatabase.Refresh，**全部编译通过（无 CS error、无 shader error）**。
- 仅有历史遗留 warnings（CS0114/CS0252/CS0618/CS0219/CS0414 等，改动前已存在）+ Inspector 瞬态异常（外部编辑 YAML 后重载）。
- ⚠️ PlayMode 验证需用户在 Unity 编辑器里跑（MD §18 的运行时验收项），本会话仅做编译级验证。

---

# Global Playable Pass v1.1 — Feedback Patch（Owner 第一轮实机反馈，2026-08-29）

> 唯一真源：`D:/开普勒/八月正式开发/调参/Possession_Global_Playable_Pass_v1.1_Feedback_Patch.md`
> 范围：仅 4 个体验问题，其余全部沿用 v1，不重做、不顺手改其他项。

## 4 项改动结论

| # | 问题 | 根因 | 改动 |
|---|---|---|---|
| 1 | Greed Black Oil Enemy 减速过强 | `enemySlowMultiplier=0.5`（speedMultiplier 语义=保留50%移速），Enemy/Possessed 共用同一字段 | 拆出 `enemyOilPlayerSlowMultiplier=0.7`（Enemy 减速玩家身体），`enemySlowMultiplier=0.5` 保留给 Possessed Greed |
| 2 | 开局找不到怪 | 寻敌轨迹逻辑已完整正确，仅触发延迟 `noMonsterVisibleSeconds=5` 过长 | 场景 `EnemyAiTest.unity` guideMode:0（Monsters）`noMonsterVisibleSeconds` 5→1.25 |
| 3 | Elite Envy Laser 全图锁人 | `eliteVisualScaleMultiplier=2f` 经 `ScaleAbilityRadius` 把 15m 放大到 ~30m（叠加 EN-TG01 快照 ~38m） | 加 `enemyLaserTargetRangeCap=15`、`enemyLaserBreakRange=17`，前摇越界取消 + Beam 越界断束 |
| 4 | Gluttony Possessed 伤害过高 | `AbyssMaw.damage=100`、`Devour.damage=100`，Enemy/Possessed 共用 | 拆出 `possessedDamageOverride`（AbyssMaw 70 / Devour 60），Enemy 保持 100 |

## 修改文件清单

- `Assets/Scripts/Combat/Abilities/Monster/EnemyAbility_GreedBlackOil.cs` — 新增 `enemyOilPlayerSlowMultiplier=0.7f`，SpawnOil→Initialize 传参
- `Assets/Scripts/Combat/Abilities/Monster/GreedBlackOilZone.cs` — 新增 `enemyOilPlayerSlowMultiplier`，Initialize 加参+clamp，ApplyTo 按 `owner.isPossessed` 分流
- `Assets/Scripts/Combat/Abilities/Monster/EnemyAbility_GluttonyAbyssMaw.cs` — 新增 `possessedDamageOverride=70f` + `ResolveDamageAmount()`
- `Assets/Scripts/Combat/Abilities/Monster/EnemyAbility_GluttonyDevour.cs` — 新增 `possessedDamageOverride=60f` + `ResolveDamageAmount()`，ConsumeEnemy 改走 override
- `Assets/Scripts/Combat/Abilities/Monster/EnemyAbility_EnvyLaser.cs` — 新增 `enemyLaserTargetRangeCap=15f`/`enemyLaserBreakRange=17f`，GetEffectiveRange 封顶，`ShouldCancelEnemyTelegraph` override，`IsEnemyOutOfBreakRange` 断束
- `Assets/Scripts/Combat/Abilities/Core/EnemyAbility.cs` — 新增 `protected virtual bool ShouldCancelEnemyTelegraph()` 钩子 + TelegraphRoutine 每帧校验
- `Assets/Scenes/EnemyAiTest.unity` — guideMode:0 `noMonsterVisibleSeconds` 5→1.25

## 新增可配字段（v1.1）

| 字段 | 文件 | 默认值 | 说明 |
|---|---|---|---|
| enemyOilPlayerSlowMultiplier | EnemyAbility_GreedBlackOil.cs / GreedBlackOilZone.cs | 0.7f | Enemy Greed 减速玩家身体移速倍率（保留70%）；Possessed 仍用 enemySlowMultiplier=0.5 |
| possessedDamageOverride | EnemyAbility_GluttonyAbyssMaw.cs | 70f | Possessed Abyss Maw 伤害（Enemy 保持 damage=100） |
| possessedDamageOverride | EnemyAbility_GluttonyDevour.cs | 60f | Possessed Devour 伤害（Enemy 保持 damage=100） |
| enemyLaserTargetRangeCap | EnemyAbility_EnvyLaser.cs | 15f | Enemy/Elite 激光锁定距离安全上限（0=不封顶；非 Boss、非附身才生效） |
| enemyLaserBreakRange | EnemyAbility_EnvyLaser.cs | 17f | Beam 期间玩家超此距离断束（0=不断束；非 Boss、非附身才生效） |
| ShouldCancelEnemyTelegraph() | EnemyAbility.cs | virtual | 前摇每帧越界校验钩子（默认 false） |

## v1.1 编译验证状态
- ⚠️ 本会话 Unity Editor **未打开**，`assets-refresh`（AssetDatabase.Refresh）超时、Console 日志为空，**无法完成编译级验证**。
- 新字段均为 C# 默认值，prefab YAML 未改动（`greed_new`/`envy_new`/`gluttony_new` 仍保留原值）；Unity 打开后新字段会以默认值序列化生效。
- PlayMode 验收需用户在 Unity 里跑：①Black Oil 玩家被减速后约 70% 移速 ②开局 1.25s 无怪即显白色轨迹 ③Elite Envy 锁定 ≤15m、玩家跑出 ~17m 断束 ④Possessed Gluttony Devour 60 / Abyss Maw 70。

---

## v1.1 之后追加：Envy 视觉回退（同日 Owner 第二轮指令，2026-08-29 晚）

**Owner 拍板**：删掉前摇红色 Lock 预测线（`RefreshLockLine` 系列），激光视觉长度恢复到 v1 时代（即精英视觉拉伸仍 ×2）。v1.1 §3 的 15m 封顶 + 17m 断束数值**保留不动**。

### 改动
- `Assets/Scripts/Combat/Abilities/Monster/EnemyAbility_EnvyLaser.cs`：
  - 删 `lockLineMinWidthMultiplier` / `lockLineMaterial` 字段
  - 删 `_lockLineVfx` / `_lockLineBaseScaleZ` 私有字段
  - 删 `OnEnemyTelegraphBegin/Tick/End` 三个 override
  - 删 `RefreshLockLine()` / `ReleaseLockLine()`
  - 删 `StopLaser()` / `OnDisable()` 中的 `ReleaseLockLine()` 调用
  - SpawnBeamVfx 视觉长度逻辑回退：`scale.z *= dir.magnitude / authoredLength`（保留 `ScaleAbilityObject` ×2 放大）——精英瞄准 15m 时激光视觉 ≈ 30m；伤害锁定距离仍由 GetEffectiveRange 限制 ≤15m
- `Assets/Prefabs/Monster/envy_new.prefab`：删 `lockLineMinWidthMultiplier: 0.35` / `lockLineMaterial: {fileID: 0}` 两行

### 效果
- 视觉：精英 Envy 激光视觉 ≈ 30m（与 v1 同），不再有"射穿目标"的视觉违和（因为视觉长度本来就该长，锁定距离由 GetEffectiveRange 严格 ≤15m，不会真打到 30m 外的目标）。
- 数值：瞄准距离 15m、Beam 期间 17m 断束、前摇越界取消 Cast——全部 v1.1 §3 约束仍生效。
- Lock 线（红条）已无残留，全库 grep 无 `lockLine` / `RefreshLock` / `ReleaseLock` 引用。

## v1.1 之后追加：Envy AI 版"穿过玩家"修复（Owner 第三轮指令，2026-08-29 深夜）

**Owner 拍板**：普通怪 + 精英怪都不得"穿"过目标；激光穿透是 EN-A03（`IsPierceActive`）卡牌单独控制的功能，视觉层不得因体型缩放而穿透。只修 AI 版（`!owner.isPossessed`），玩家附身版（Possessed Player 用 Envy）保持不动。

### 真值定位
- "穿过"根因有两个独立来源：
  1. `GetEnemyTrackedAimPoint` 返回 `origin + dir × range`（固定 15m），不随玩家实际距离变化 → 玩家站 5m 时光柱画到 15m，穿过。
  2. `SpawnBeamVfx` 的 `scale.z *= dir.magnitude / authoredLength` 累乘，叠加 `ScaleAbilityObject`（Elite `CombatScaleMultiplier=2`）把 z 再放大 → 精英视觉 = 2× 终点距离。
- prefab 根 `蓝-激光` `localScale.z = 1`（实测），baseZ 基准取 `beamPrefab.transform.localScale.z`。

### 改动（`Assets/Scripts/Combat/Abilities/Monster/EnemyAbility_EnvyLaser.cs`，仅 AI 版）
- `GetEnemyTrackedAimPoint`：新增 `float playerDist = desired.magnitude;`，返回 `origin + _enemyBeamDirection.normalized * Mathf.Min(playerDist, range)` —— 终点缩到玩家水平距离（对齐玩家版 `GetAimPoint` 的实际距离语义）。
- `SpawnBeamVfx`：AI 版（`!owner.isPossessed`）用 `scale.z = beamPrefab.transform.localScale.z * (dir.magnitude / authoredLength)` 绝对值赋值；玩家版保持 `*= 累乘` 不变。

### 效果
- 普通 AI Envy：光柱终点 = 玩家距离，不再穿过。
- 精英 AI Envy：z 不再被 CombatScaleMultiplier ×2，光柱精确落在 beamEnd（玩家/命中点），不再穿。
- 软锁手感保留（`enemyTrackingTurnSpeed=100°/s` 平滑转向不变）；伤害距离兜底不变；玩家附身版不受影响。
- 穿透仍由 EN-A03 `IsPierceActive`（`ResolveBeamHits` 的 pierce 分支）单独控制。

### 编译验证
- Unity Editor 未打开，未做编译级验证。两处改动均为纯算术/条件分支，无新增字段/引用，C# 语法层安全。
