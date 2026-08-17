# 01_DESIGN_CANONICAL — v1.1

**Project:** Possession<br>
**Date:** 2026-08-15<br>
**Status:** `CANONICAL / RULES CLOSED`<br>
**Purpose:** 当前权威 Gameplay Behavior Contract。<br>
**Not included:** 代码架构、迁移、资产复用判断、最终数值平衡。

---

# 1. 产品定义

《Possession》当前是：

- PC；
- 单机；
- 实时动作 Roguelike；
- 买断制方向；
- 单局目标约 10–15 分钟；
- 当前以 GameJam / Demo 可交付为里程碑。

最高优先体验：

> **Combat + 高频 Possession / 换身**

次级体验：

> **Reverse-BD / Roguelike Build**

当前核心阶段不依赖大型局外成长、真实异步在线生态、UGC/AIGC或复杂长期经济。

新玩家 / 评审应在约30秒内理解：

- 怎么移动与攻击；
- 敌人死后可成为Body；
- 当前Body不是永久角色；
- 换身是核心而不是偶发技能。

---

# 2. 最高设计法则

## 2.1 高频换身

核心循环：

```text
战斗
→ 击杀Enemy
→ Corpse
→ 选择并Possess
→ 快速兑现三槽价值
→ 主动离身或Body Fatal
→ 接入下一具Body
→ Skill Relay
```

禁止核心系统把玩家推向：

- 长期固定一具Body；
- 换身损失巨大；
- 新Body需要长时间预热；
- 囤积大量舍不得放弃的Body-bound资源；
- Possession退化为偶尔使用的工具技能。

## 2.2 低容错，不做伪难度

允许高压力。

不允许把难度主要建立在：

- 无前兆瞬杀；
- 不可理解的随机性；
- 长控制链；
- 玩家无法干预的失败；
- 专门惩罚换身。

主要难度来源：

> 决策、操作、Body耐久压力、战场压力、目标选择、换身时机。

## 2.3 新机制准入

新机制进入当前Demo前必须回答：

1. 是否强化 Combat / Possession核心乐趣；
2. 是否强化高频换身或Reverse-BD；
3. 是否适合短局Demo；
4. 是否值得其认知、实现、表现和测试成本。

若答案不成立：

> 删除或降级，不为“系统完整”保留。

---

# 3. 玩家主体与Body

玩家控制主体与当前Body分离。

Body是：

- 临时战斗载体；
- 可消耗；
- Movement / Attack / Special的承载者；
- Monster-Type Build的载体；
- Skill Relay节点。

成功Possession后：

> **新Body以满玩家耐久进入，三槽回到该Body的初始可用状态。**

不继承：

- Enemy死前Cooldown；
- Enemy死前Body-bound资源；
- 之前同类Body的Runtime Cooldown；
- 上一具Body的Body-bound状态。

Elite的历史Build身份属于内容层例外，但Runtime仍按上述初始化。

---

# 4. 输入与三槽

当前键鼠基线：

- WASD — 移动；
- LMB — Attack；
- Space — Movement；
- E — Special；
- RMB — Possession / Active Leave；
- Q — Bullet Time。

三槽合同：

1. Movement
2. Attack
3. Special

Movement不必是通用闪避，可以是突进、跳跃、钩索、换位等Body专属移动。

输入优先级：

> Possession / Bullet Time<br>
> ＞ Attack / Movement / Special<br>
> ＞ 基础移动

所有输入仍需通过当前State、前置和Cancel合法性。

---

# 5. Enemy / Possessed同源

每只普通Monster同时是：

- Enemy；
- Future Corpse；
- Playable Body；
- Build Carrier；
- Skill Relay Node。

核心：

> **看见什么，就夺取什么。**

默认同源：

- 主题；
- 核心机制；
- Gameplay Payload；
- 三槽归属；
- Card关系；
- 核心视觉语言；
- 高阶机制扩展关系。

允许不同：

- 起手 / 后摇；
- CD / Reload；
- 瞄准；
- AI使用条件；
- 输入响应；
- 玩家操作便利参数。

不能通过“Enemy是一套、玩家抢来变另一套”解决公平性。

---

# 6. 动作、取消与输入缓冲

Attack / Special等动作至少有：

- Startup；
- Active；
- Recovery；
- Reload / CD。

基础移动在Reload期间可继续。

Movement可在明确合法窗口取消其他动作表现。

当前基线：

> 若Movement合法取消一个动作，该动作的Reload从0重新计时。

精确Cancel Window按Ability可调。

Possession传输 / 重构期间支持短预输入：

- 移动；
- Attack；
- Movement；
- Special。

恢复控制后可立即执行合法缓存输入。

---

# 7. Body耐久、Fatal与Soul

当前玩家Body耐久来自同一池：

- 受到Damage；
- 自然耐久衰减。

自然衰减仅作用当前玩家Body。

非战斗过渡状态冻结自然衰减。

精确衰减：

> `TUNABLE / PLAYABLE VALIDATION`

Body Fatal：

- 有合法Corpse：进入短Death Relay窗口；
- 无合法Corpse：进入Soul。

Soul：

- 可移动；
- 可Possess；
- 可受伤；
- 可死亡；
- 不拥有普通攻击；
- 不拥有战斗技能树；
- 不是第二套战斗Build。

Soul Death：

> **Run Fail**

---

# 8. Corpse与Possession

Enemy Fatal后：

> **立即成为合法Possession目标。**

不等待完整死亡动画。

Possession可打断Death Animation。

目标选择优先：

1. 鼠标直接指向的合法Corpse；
2. 无直接目标时，使用可调小范围内最近合法Corpse；
3. 滚轮切换候选。

RMB提交后：

> 目标冻结。

可见合法Corpse可跨静态地形Possess。

旧规则“穿墙可见但不可选”：

> `DEPRECATED`

当前没有Blocked Corpse状态。

Corpse碰撞基线：

- 阻挡Body；
- 阻挡Enemy移动 / 寻路；
- 不阻挡飞行攻击；
- 不阻挡Soul；
- 不阻挡Possession传输。

需Playable验证Corpse Wall是否形成永久安全区或Hard Lock。

---

# 9. Corpse清理与主动离身

Combat中Corpse可用：

- 数量上限；
- Lifetime；
- 离屏加速；
- 距离加速；

进行系统清理。

Wave结束：

> Corpse Window后清理未选择尸体。

Elite Corpse可有短保护窗口。

主动离身：

> 当前Body立即崩解 / 消失，不留下可重复附身Corpse。

防止：

```text
Body A
→ Leave
→ Repossess A
→ 刷新技能
```

主动离身可进入Soul。

---

# 10. Shrine Fallback

Soul没有合法Corpse时：

> Shrine Fallback启动。

其功能合同：

- 提供方向指引；
- 生成 / 提供一个合法普通Fallback Body或Corpse；
- Soul到达后自动Possess恢复循环。

Fallback具体池属于Content。

当前允许：

> 主动离身 → Soul → 无Corpse → Shrine

作为高风险Reset Route。

是否可被滥用：

> `HIGH-PRIORITY PLAYABLE VALIDATION`

---

# 11. Damage与控制

最低Damage Pipeline：

```text
Hit Valid
→ Invulnerability Check
→ Damage Modify
→ Durability Change
→ Fatal Check
→ Reaction / Feedback
```

普通命中：

- 不产生通用Gameplay Hit Stun；
- 不自动取消Enemy当前动作。

Knockback可以位移。

除Ability明确例外外：

> Knockback不等于动作打断。

当前避免大量：

- Stun；
- Freeze；
- Silence；
- Chain Control。

普通Enemy攻击默认不伤害其他Enemy。

普通攻击默认不摧毁Corpse。

环境Hazard可在明确定义时同时作用玩家与Enemy。

---

# 12. 基础Interaction边界

玩家侧原则：

> **隐式规则，显式结果。**

内部可有飞行攻击、重量、来源等分类，但玩家不应学习大型Tag表。

基础状态：

> 飞行攻击彼此默认穿过，不自动发生Projectile Clash。

基础Interaction保留：

### Greed正面Guard

- 正面合法飞行攻击；
- 正面激光；

不基础处理：

- Ground AoE；
- Field；
- Pull；
- Burning；
- 侧后方攻击。

### Oil + Ignite

明确火源可点燃Greed Oil。

基础：

- 不无限扩散；
- 不无限刷新。

### Pull × Body Weight

重量只分：

- 轻；
- 中；
- 重。

不同重量产生不同位移。

不使用玩家可见百分比抗性表。

### Beam

基础激光：

- 不清普通飞行攻击；
- 不与飞行攻击对冲；
- 可被明确Guard截断。

Cut / Crush / Reflection / Devour / Clear等：

> 只有明确Card、高阶质变或特定Ability赋予时才存在。

---

# 13. 跨Body生命周期

原则：

> **已经脱手、具有独立生命周期的战斗结果可以跨身继续；依赖旧Body Runtime的资源与状态不跨身。**

典型继续：

- 已发射飞行攻击；
- 已生成AoE；
- Sloth Drone；
- Greed Oil / Burning Oil；
- 已发射Greed魔手；
- 已生成Gluttony巨口；
- Pride已生成剑气；
- 其他明确独立派生。

典型清除：

- Envy激光持续引导；
- Envy Record；
- Gluttony Overfed；
- Greed未释放库存；
- Greed Guard；
- Wrath旋转 / Body Aura；
- Lust Anchor / Link；
- 其他Body-bound状态。

---

# 14. Bullet Time

输入：

> Q

模型：

> **以玩家为中心的局部时间场。**

按Gameplay Origin决定时间域。

Player-Origin Chain不减速：

- 玩家移动；
- 当前Body动作；
- Reload；
- 玩家来源飞行攻击；
- 玩家Gameplay VFX；
- 已脱手Player-Origin输出即使换身后仍保持Player-Origin时间域。

Non-Player-Origin在场内减速：

- Enemy；
- Enemy Ability；
- Enemy Projectile；
- 非玩家世界过程。

Bullet Time期间：

> **Current Body自然耐久衰减加速。**

当前基线：

- 按Q启动；
- 固定持续；
- 结束后可配置CD；
- CD可后续调至0。

可调：

- 持续；
- 半径；
- 减速率；
- CD；
- 额外衰减倍率。

Bullet Time不改变Interaction分类，不额外放大Pull单次强度。

---

# 15. Run / Wave / Final

当前Run：

- 8个普通Wave；
- 普通阶段目标约10分钟；
- Final Pressure当前测试基线约5分钟；
- 总局长目标仍约10–15分钟，因此Final时长属于高优先级Playable验证，不冻结为最终值。

## 15.1 起始状态

叙事起点是Soul / Candidate无稳定Carrier，但玩家**不先进入可操作Soul阶段**。

New Save开场：

```text
Opening Cinematic / CG
→ Candidate / Soul处于无稳定Carrier状态
→ System分配第一具Transfer-Eligible Carrier
→ 初始Possession / Assignment完成
→ 玩家获得傲慢Body控制
→ 嵌入式基础教学
→ Wave 1
```

第一具可操作Body：

> **固定为 Pride / 傲慢·终刃绝影。**

不随机、不在开局提供Body选择。

普通后续Run当前也沿用固定傲慢起始Body，直到Owner明确改变。

## 15.2 Card时点

`Opening Card`：

> **DEPRECATED / REMOVED**

它不是Wave 0奖励，也不再作为Run开始前的独立构筑节点。

当前单Run Card选择：

- Wave 1–7后：各1次；
- Wave 8后：连续2次；
- **合计9次。**

如果Playable证明Build成型过慢，可以另选更合理节点增加一次Card，但不得为了恢复历史“10次”而自动恢复Opening Card。

## 15.3 Wave Transition

```text
Last Enemy
→ Wave Clear
→ Corpse Window
→ Clear Unselected Corpses
→ Card Selection
→ Next Wave
```

Wave 8：

```text
Wave 8 Clear
→ Corpse Window
→ Card Selection #8
→ Card Selection #9
→ Final
```

Corpse Window基线：

- 约2–3秒；
- 可移动；
- 可Possess；
- 自然衰减冻结。

Card Selection暂停世界时间。

Game State：

```text
Opening Cinematic / Initial Carrier Assignment
→ Tutorial in Pride Body
→ Wave 1
→ Wave Clear
→ Corpse Window
→ Card
→ Next Wave
→ ...
→ Wave 8
→ Card ×2
→ Final
→ Final Clear
→ Result
```

Combat可进入：Death / Soul / Fail。Pause是Overlay。

---

# 16. Reverse-BD

定义性承诺：

> **Future Enemy Threat ↔ Future Body Value**

Monster-targeted Build会让未来同类普通Enemy更危险，同时玩家之后Possess该类时获得同源强化。

Enemy/Possessed不允许因Reverse-BD变成两个不同系统。

投资后的Monster需要在后续遭遇中有足够回报机会。

---

# 17. Card系统合同

当前Card详细内容属于`02_CONTENT_CANONICAL`与Card详细真源。

设计层当前分：

1. **基础通用Card**：简单、单轴、非直接Damage / HP为主；
2. **Global Slot质变**：单局级常驻槽位规则改变；
3. **Monster-Type Card**：某Sin / Monster专属显式强化、联动和质变；
4. **Type Growth Card**：每Sin一张的显式多维基础成长，属于Monster-Type体系。

当前没有稀有度系统。

## 17.1 当前Run Card作用域

对普通Monster：

- Basic Universal：作用于本Run所有普通Enemy与普通Possessed Body；
- Global Slot：作用于本Run所有普通Enemy与普通Possessed Body；
- Monster-Type：作用于对应类型的普通Enemy与对应Possessed Body；
- Type Growth：作用于对应Sin的普通Enemy与对应Possessed Body。

Elite是明确例外：

> **Elite不读取当前Run Basic / Global / Monster-Type / Type Growth。**

Elite被玩家Possess后也保持这一例外，只保留自己的External Historical Build Snapshot；正常满耐久 / 三槽初始化仍照常执行。

## 17.2 Type Growth替代旧隐性属性成长

旧规则：

> “每拿一张某Sin普通卡，后台自动偷偷增加该Sin基础属性”

已取消。

当前：

- 普通Monster-Type Card取得后：对应Sin `Investment +1`，用于遭遇倾向 / Spawn Weight；不自动增加基础属性；
- Type Growth Card取得后：对应Sin `Investment +1`，并显式让该Sin多维基础形态成长。

Type Growth：

- 每Sin 1张；色欲保留`LU-TG01`，当前Type Growth共7张；
- Stack Max = 1；
- 约2个直观非伤害基础维度；
- 不新增复杂资源；
- 不隐藏加HP / Damage；
- 单轴强度原则上低于对应基础通用Card；`LU-TG01`三槽冷却减少30%是Owner明确确认的当前例外轴，基础移速增幅仍须低于`UB-B01`；
- `LU-TG01`不缩短技能前摇、后摇或敌人最低预警时间；
- Enemy与Possessed同源。

## 17.3 Global Slot质变

定义：

> **Run级常驻规则改变。**

原则：

- 单张尽量一句话能懂；
- 可做简单槽位联动、动作衔接、追加执行、明确Interaction；
- 不做技能刷新闭环；
- 不形成 `A → B → C → A` 自动回流；
- 自动追加执行属于原施放派生，不生成新的槽位使用事件；
- 同一待触发状态不叠层，重复前置只刷新状态；
- 终局按常见取得3–4张Global组合压力测试；
- 对当前Build完全零合法作用的Card不得作为纯陷阱Offer。

## 17.4 Offer

当前没有Opening Offer。

每个Post-Wave Offer：

> **3选1：横向位 + Monster-Type位 + Flex位。**

- 横向位：Basic Universal / Global Slot合法池；
- Monster-Type位：Known Type Set中的合法Monster-Type / Type Growth；
- Flex位：全体剩余合法池；
- 同一Offer禁止重复Card ID；
- 满Stack / 唯一Card已取得后移出合法池；
- 完全零效果Card过滤；
- 空池按`Content/Encounter_CardOffer_Baseline_v1.0.md`执行Fallback；
- Global使用软保底，不恢复历史W2/W4/W6/W8硬保底。

Known Type Set包含：

- 当前Run起始Carrier类型（Pride从Run开始即Known）；
- 已正式进入本Run Encounter的其他Monster类型。

精确权重与Soft Pity为`BASELINE / TUNABLE`，见Content算法文件。

---

# 18. Spawn Pool × Build Payoff

Monster解锁、Spawn Pool、Card资格、Reverse-BD必须联动。

系统应支持：

- Wave门槛；
- 每类Monster权重；
- 投资Monster重现倾向；
- Soft Pity；
- 轻量重复抑制。

重要修正：

> Monster-Type投资仍可提高未来遭遇倾向，但**不会再隐式增加该Sin基础属性**。

精确倍率与Pity：

> `TUNABLE / PLAYABLE`

---

# 19. Encounter与Spawn

当前范式：

> **Wave Pressure Budget + Legal Spawn Pool + Combination Constraints / Templates + Weighted Random**

规则：

- Elite消耗压力预算，不作为免费额外密度；
- 高压组合仍需产生合理Corpse机会；
- Final按阶段增加密度、组合和高价值威胁；
- Spawn发生在玩家可见战斗区外；
- 只在合法已生成地形；
- 必须有足够激活可读性；
- 默认不做可见范围突然传送到脸上。

精确Budget、Alive Cap、解锁波次、权重：

> `CONTENT BASELINE / TUNABLE`

---

# 20. Infinite Dynamic Terrain

当前空间基线：

> **连续、以玩家为中心动态生成的世界。**

旧Room进度：

> `DEPRECATED AS DESIGN BASELINE`

方向：

- 模块化地形；
- 围绕玩家移动扩展；
- 已生成地形默认保留；
- 持续承载Wave战斗。

当前Wave Enemy不会因玩家跑远而被“自动完成 / 删除”。

离屏追击加速或合法重定位可作为实现支持，但：

> 不允许玩家可见范围突然Teleport。

技术拓扑属于Technical Canonical。

---

# 21. Terrain Gameplay Modules

目标模块：

- Neutral Floor；
- Collision Obstacle；
- Decorative Geometry；
- Speed Zone；
- Slow Zone；
- Lava；
- Spike / Periodic Hazard；
- Spawn-safe区域 / Spawn legality。

Hazard必须可读，不做无预警环境瞬杀。

环境默认不摧毁Corpse。

---

# 22. Monster / Enemy AI合同

每只Monster内容必须能定义：

```text
Acquire Target
→ Preferred Position / Range
→ Ability Selection
→ Execute
→ Recover / Reposition
```

当前实现可以更简单，但目标内容不能依赖“未来复杂AI才成立”。

FSM / BT / Nav架构属于Technical Open。

每个Ability内容至少定义：

- Slot；
- Target；
- Range；
- Startup；
- Active；
- Recovery；
- Reload / CD；
- Charge / Resource（如有）；
- Hit Type；
- 飞行攻击 / AoE规则；
- Enemy Use；
- Possessed Input / Aim；
- Cancel；
- Telegraph；
- Card Hook。

---

# 23. Elite与历史Build Snapshot

当前Elite核心定义：

> **Elite = Base Monster + External Historical Build Snapshot**

它代表：

> 其他玩家历史通关 / 高投资Monster Build侵入当前Run。

当前Demo不依赖真实网络，使用Preset / Fake Historical Build Profiles模拟。

Elite：

- 不叠当前Run Monster-Type Card；
- 不叠当前Run Type Growth；
- 不读取当前Run Global Slot；
- 不读取当前Run Basic Universal；
- 不因当前Run投资自动改变自身Historical Build；
- 被击杀并Possess后，继续保持自身Historical Build强化身份，并继续排除当前Run四类Card层；
- Possession后仍以满玩家耐久、三槽初始可用状态开始；
- Runtime Cooldown / Body-bound Resource不继承死前状态。

具体Preset Historical Profile清单与权重属于后续Content Baseline，不影响本核心合同。

---

# 24. Mutation / Qualitative术语边界

当前“Mutation”不自动表示：

> 一类独立随机变异怪。

在当前Canonical中优先理解为：

> **高阶机制质变 / Upgrade Hook / Historical Build中的质变能力。**

除非Owner后续单独确认，不创建“随机Mutation Monster”单位体系。

---

# 25. Boss边界

Boss不是当前核心必需内容。

Boss可：

- 不可Possess；
- 召唤可Possess的小怪。

Boss不得长时间把玩家移出换身循环。

当前：

> `DEFERRED / MILESTONE-DEPENDENT`

Final不等于Boss战。

---

# 26. Tutorial

New Save使用嵌入式真实战斗教学。

开场通过动画 / CG完成“无稳定Carrier的Candidate / Soul → 被分配进傲慢Carrier”的初始Possession。玩家获得控制时已经在傲慢Body内。

不设置玩家侧独立Wave 0，也不设置Wave 0选卡或Opening Card。

关键教学：

- 移动 / 瞄准；
- 三槽；
- Kill → Corpse；
- Possession；
- 主动换身；
- 第一次真实Death Relay；
- 第一次自然进入Soul / Shrine。

首次Possess每种Monster：

> 一次性、不暂停，只解释该Body独特机制。

教学完成状态持久化。

支持：

- 重看教学；
- 教学提示开关。

---

# 27. HUD与Card信息合同

当前Body：

> 必须持续可读耐久 / 生命周期压力。

Ability HUD至少能表达：

- Ready；
- Reload / CD；
- Charge；
- Unavailable / Lock。

普通Enemy：

> 不要求常驻HP Bar。

Elite：

> 不要求常驻HP Bar；危险与高价值通过表现表达。

Corpse：

- Available — 弱轮廓；
- Current Target — 强轮廓；
- Decaying / Expiring — 额外变化；
- 无Blocked状态。

Card必须让玩家理解：

> 强化某Monster / Ability，同时提高未来Enemy威胁和未来Possessed Body价值。

Card明线 / 暗线具体呈现属于Presentation Canonical。

---

# 28. Final / Result / Review

Final Pressure位于普通Wave Build完成之后。

目的：

> 让本局完成的Build获得足够兑现时间。

当前时长：

> `~5 min BASELINE / PLAYABLE`

需重点验证：

> 收益感是否大于疲劳，并仍符合总局长。

Final结束：

> Final Clear清除剩余最终Encounter后进入胜利。

Result最低：

- Win / Fail；
- Restart；
- Return / Lobby。

Restart直接新开一局。

Review / Demo Anti-block与正式结果严格分离。

Review跳段：

- 必须清晰标注；
- 不改正式失败；
- 不计正式成绩 / 进度；
- 可使用Preset Build / Stage Snapshot。

---

# 29. Save / Settings

当前Demo不要求Mid-run Suspend / Resume。

至少持久化：

- Tutorial状态；
- Settings；
- 必要非Run状态；
- R03.1允许的最小Narrative Recognition（若实现）。

当前一个Standard Difficulty基线。

额外Difficulty / Assist：

> Future，除非后续明确提升优先级。

---

# 30. Playable Validation — 不阻塞Canonical

以下不是Frozen数值真相：

- Natural Decay；
- Bullet Time持续 / 半径 / Slow / CD / Decay Multiplier；
- Body Cost；
- Damage / HP；
- Reload / CD；
- Corpse Cap / Lifetime；
- Pull位移；
- Final精确时长；
- Spawn Budget / Weight / Pity；
- Global Reflection可反射对象与是否需要限制；
- 3–4张Global组合强度；
- 各Type Growth精确增幅；
- 各Monster具体表现参数；

- 主动离身→Shrine是否可滥用。

这些应进入Playable与后续技术实现验证，而不是重新打开Rules Layer。

---

# 31. Core Acceptance Scenarios

## A. Skill Relay

玩家：

> 使用Body A的强技能 → 主动换身 / Death Relay → Body B立即可用 → 延续脱手结果与新Body三槽

不得因旧Body资源、CD或长期预热卡死。

## B. Reverse-BD

玩家强化某Monster类型后：

- 后续同类普通Enemy明确更危险 / 更有变化；
- 击杀并Possess后能获得同源强化价值；
- 投资类型有足够重现机会。

## C. Death Relay

Body Fatal且存在合法Corpse：

> 玩家能快速接入新Body，而不是被长Death Animation阻塞。

## D. Soul Fallback

无Corpse进入Soul：

> 玩家理解脆弱状态、合法Possession路径和Shrine方向。

Soul死亡：

> Fail。

## E. Card Safety

Global额外执行不得自递归触发新的Slot Event。

零效果Card不得进入Offer。

Type Growth不得通过隐藏Damage / HP破坏规则。

## F. Final Payoff

Final明显区别普通Wave，Build获得兑现空间，且仍保留换身循环。

---

# 32. Design / Technical Boundary

本文件定义：

> **游戏必须表现出的行为。**

不规定：

- ECS / OOP；
- FSM / BT具体实现；
- Source Tracking架构；
- Pooling架构；
- Save数据结构；
- Terrain生成算法；
- Ability内部类层次；
- 网络后端；
- Migration方案。

这些进入Technical Canonical / ADR。

Repository当前代码与本文件不一致时：

> 记录为Implementation Gap，不能反向改写本文件。
