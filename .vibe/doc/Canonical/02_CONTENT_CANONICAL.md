# 02_CONTENT_CANONICAL — v1.1

**Project:** Possession<br>
**Date:** 2026-08-17<br>
**Status:** `CANONICAL / CURRENT CONTENT BASELINE`<br>
**Primary sources integrated:** Seven Sins Monster Production Baseline v1.0；Card System v1.1 Owner Review Passed；R03.1；Design Canonical v1.0；2026-08-17 Owner-approved Lust Card Update。

---

# 1. Content Truth Boundary

本文件定义当前目标Content。

优先级：

1. 最新Owner明确人工决策；
2. 本文件；
3. `Content/Card_System_Current_Truth_v1.1.md`逐卡详细字段；
4. Seven Sins Monster Production Baseline v1.0；
5. Repository Fact只作实现证据；
6. Legacy / old Demo / early proposal只作追溯。

当前仓库中的旧5怪、旧8张Card、旧Room、旧Soul攻击、新Pride候选等：

> **不能自动升级为当前目标Content。**

---

# 2. 当前基础Monster Roster

当前基础Roster固定为七宗罪七只普通可Possess Monster。

| Sin | Monster | Action Grammar | 战斗岗位 | Possessed快速价值 |
|---|---|---|---|---|
| Pride | 傲慢·终刃绝影 | 宣称 / 压制 | 高机动刺客 | 快速突进、剑气、多段穿梭后切身 |
| Sloth | 怠惰·机械之灵 | 委托 / 延迟 | 重炮 / 短寿命召唤 | 留Drone与炮弹后切身 |
| Gluttony | 暴食·魔猫 | 吞并 / 消耗 | 捕食 / 短周期爆发 | 小猫接近→吞噬→过饱巨口 |
| Envy | 嫉妒·激光异形 | 映照 / 夺取差异 | 持续猎杀 / 延迟兑现 | 激光写Mark→雷暴兑现 |
| Wrath | 愤怒·链狱冥兽 | 冲突 / 反击 | 低耐久狂战 / 聚怪 | 钩入→砸地→Pull→Relay |
| Greed | 贪婪·万手藏主 | 占有 / 累积 | 囤积 / 防守反击 | 初始手→Guard转化→倾泻 |
| Lust | 色欲·灵念师 | 牵引 / 绑定 | Anchor / Link位置控制 | Anchor→Link→Pull→切AoE Body |

当前不新增第8只基础Monster作为本Demo Canonical依赖。

---

# 3. 21个基础技能

## Pride｜傲慢·终刃绝影

**Movement — 一刀斩突进**
- 高速向输入方向突进，并以刀锋前端斩击；
- 基础不清弹；Enemy必须有方向前兆；
- 当前Playable Baseline：`Reload 2–5 / hp cost 5–25 / damage 20`。

**Attack — 剑气**
- 短程实体飞行斩击；玩家自由瞄准；Enemy有挥刀前摇；
- 基础不与飞行攻击对冲；可被Greed Guard吸收；
- 当前Playable Baseline：`Reload 1 / hp cost 5 / damage 20`。

**Special — 穿梭斩**
- 一次合法施放进行4段高速穿梭，每段0.25秒；
- 无合法目标不启动；目标少时可重复命中；穿梭瞬间可不可选中；结束后有恢复窗口；
- 当前Playable Baseline：`Reload 1.5 / hp cost 20 / damage 10 per blink`；
- 输入仍为`E`，`Q`保持Bullet Time。

Enemy行为基线：

> 快速接近 / 改角度 → 中距离剑气 → 合法目标存在时多段穿梭 → 恢复 / 重定位。

---

## Sloth｜怠惰·机械之灵

**Movement — 弹射起跳**
- 朝地面开炮后弹射起跳；有明显起跳前摇；
- 腾空主要是表现，不获得地面攻击免疫；
- 当前Playable Baseline：`Reload 1 / hp cost 20 / damage 0`。

**Attack — 蓄力爆炸炮**
- 按住蓄力；蓄力提高威胁、范围与伤害档；
- 释放有明显后坐动画 / 反馈，但不产生Gameplay位移；
- “重型”本身不自动压弹；
- 当前Playable Baseline：`Reload 0 / hp cost 5 / damage 2–100`。

**Special — 木灵**
- 向天空投出短寿命自动攻击木灵；木灵跟随玩家、持续追击并攻击最近Enemy直到死亡；
- 可被击毁；不可Possess；不产Corpse；换身后按自身寿命继续；达上限后新木灵替换最旧同源木灵；
- 当前Playable Baseline：`lifetime 4s / Reload 1 / hp cost 50 / damage 5 per attack / attack range 30`。

Enemy行为基线：

> 保持中远距离 → 合法时蓄力炮 → 木灵低于上限时部署 → 太近 / 站位不利时弹射重定位。

---

## Gluttony｜暴食·魔猫

**Movement — 小猫化**
- 变为约50%体型的小猫，持续3秒；移动速度提高100%，重量变轻；不改变受击层；
- 再次使用Movement切换回魔猫形态；使用Special也会恢复魔猫形态；普通Attack默认解除小猫化，取得`GL-M01`后不解除；
- 当前Playable Baseline：`Reload 0 / hp cost 100 / damage 0`。

**Attack — 深渊巨口**
- 基础在施放者脚下生成深渊巨口；施放时快照，0.5秒后生成，不持续追踪；
- `GL-A03`使施放者将诱饵抛向远处，在远端位置生成巨口；
- Enemy与玩家版本均必须显示清晰地面危险区；
- 当前Playable Baseline：`Reload 2 / hp cost 10 / damage 20`。

**Special — 吞噬 / 复制Skill Ability**
- 魔猫形态下向前方约1米半径扇形作出巨大咬合，只命中最近的合法Enemy；
- 命中后获得`Overfed`，下一次基础吞噬变为二次咬合并消费；
- 复制该最近Enemy的`E / Skill Ability`，临时替换暴食E槽；成功使用一次后失效并恢复吞噬；
- 换身清除`Overfed`和未使用的复制Skill Ability；
- 基础不吞飞行攻击；`GL-S03`是吞飞行攻击并获得`Overfed`的例外；
- 当前Playable Baseline：`Reload 1 / hp cost 50 / damage 40`。

Enemy行为基线：

> 小猫态追击 / 重定位 → 在脚下放巨口 → 近距离尝试吞噬 → 获得`Overfed`后优先二次咬合；Enemy不使用玩家专属的临时复制E槽，但保留吞噬、过饱与二次咬合的核心Payload与表现语义。

---

## Envy｜嫉妒·激光异形

**Movement — 飞行加速**
- 飞起并获得+200%移动速度，持续1秒；可在维持激光时调角度 / 距离；不建立真实空中层，不基础无敌 / 清弹；
- 当前Playable Baseline：`Reload 0 / hp cost 20 / damage 0`。

**Attack — 激光 + Mark**
- 玩家可直接鼠标瞄准；Enemy锁当前玩家，不使用“最高HP目标”；
- 激光命中目标时向该目标写入Mark；每个Mark储存目标受到伤害的20%；
- 激光可被Greed Guard截断；被截断伤害不写入Mark；
- 当前Playable Baseline：`Reload 0 / hp cost 10 per second / damage 2 per second`。

**Special — 雷暴兑现**
- 将锁链雷电传导至所有带Mark的合法Enemy；每个目标结算基础雷暴+自身Mark储存伤害，并消费自身Mark；
- 没有合法带Mark目标时不启动、不收费、不进Reload；Enemy使用前必须预警；
- 当前Playable Baseline：`Reload 1 / hp cost 50 / damage 10 + stored Mark damage per target`。

Enemy行为基线：

> 保持中距离视线 → 激光持续压迫 / 追踪并写入Mark → 合法时雷暴传导兑现 → 飞行调整角度。

---

## Wrath｜愤怒·链狱冥兽

**Movement — 钩索位移**
- 将自己钩向前方合法地面 / 位移点；
- 无合法点不启动；
- 钩头不是Attack Projectile；
- 基础只负责位移；`WR-M01`赋予路径伤害，`WR-M02`提高合法抓取距离并使抵达落点后冲击周围地面。

**Attack — 双拳砸地 + Burning**
- 双拳砸地，对范围内敌人造成基础范围伤害；
- 命中区域生成燃烧，燃烧每秒造成5点伤害、持续3秒；
- 若砸地区域存在Greed普通黑油，则点燃该黑油；
- 满耐久时本身就必须好用。

**Special — 暴怒锁链 / 龙卷风**
- 用触角将范围内敌人吸入中心，形成持续2秒的龙卷风式牵引；
- 每0.5秒结算一次伤害；
- 轻 / 中 / 重产生不同牵引位移；
- 不Stun；
- 被Enemy Pull的玩家仍保留移动与Space输入；
- `WR-S01`使龙卷风期间获得+300%移动速度；`WR-S03`使龙卷风时长延长2秒。

Enemy行为基线：

> 钩索逼近 / 改角度 → 砸地建立燃烧区并点燃合法黑油 → 合法距离内开启锁链龙卷风Pull → 恢复 / 再接近。

---

## Greed｜贪婪·万手藏主

**Movement — 铺黑油**
- 向前方释放短寿命黑油；站在自身普通黑油上的玩家 / Possessed Greed获得加速；
- 明确火源可点燃黑油；`GR-M01`扩大黑油路径长度与覆盖范围，并使Enemy踏入黑油时减速；
- 当前Playable Baseline：`Reload 0 / hp cost 10 / damage 0`。

**Attack — 念力魔手**
- 每秒生成1只魔手，魔手环绕本体，基础库存上限6；
- LMB以0.2秒间隔依次释放所有库存魔手；一次释放总计消耗20HP；无合法目标的魔手消失；
- Possession初始化获得少量魔手；0库存时Attack不启动、不收费；已发射魔手跨Body继续；
- `GR-A01`提高库存与单次释放上限；`GR-A02`和`GR-A03`分别提供击杀后的再索敌与新增魔手派生。

**Special — 大手Guard / 吸收转化**
- 大手保护身体，吸收所有伤害；基础持续1秒，期间不能移动，也不能倾泻魔手；
- 每100点实际吸收伤害转化1只魔手；空开也视为正常释放并收费 / 进入Reload；
- `GR-S01`使每60点实际吸收伤害转化1只魔手；`GR-S04`使Guard持续3秒，可再次使用Special提前结束并立即进入Reload。

Enemy行为基线：

> 铺油建立玩家机动路径 → 累积并释放魔手 → 需要防护时开启Guard并将吸收结果转化后反压。

---

## Lust｜色欲·灵念师

**Movement — 魅影换位 / Anchor**
- 位移；
- 起点留Anchor；
- 再次使用可换位；
- 基础只维护一个主Anchor；
- 新Anchor替换旧Anchor；
- Anchor不可攻击 / 不被Pull；
- 有寿命；
- 换身清除。

**Attack — 迷情往返**
- 雾状攻击去程 / 回程；
- 两段可分别命中；
- 命中施加Link；
- Link不无限叠层；
- 基础不对冲普通飞行攻击。

**Special — 诱引 / 牵魂**
前置：
- 有有效Anchor；
- 至少一个合法Linked目标。

前置失败：
- 不启动；
- 不收费；
- 不进Reload。

成功：
- 将Linked目标拉向Anchor；
- 造成伤害；
- 重量影响位移；
- 不Stun；
- 消费Anchor和相关Link。

Enemy行为基线：

> 留Anchor → 往返攻击建立Link → Anchor与Link均合法时Pull → 重新建立位置关系。

---

# 4. Possession初始化与生命周期

成功Possession：

> **满玩家耐久 + 三槽初始可用 + 不继承Enemy死前Cooldown。**

Monster额外初始化：

- Gluttony：Overfed = 0，未持有复制Skill Ability；
- Envy：Mark清空；换身清除已写入目标身上的Mark；
- Greed：少量初始魔手，数量Tunable；
- Lust：无Anchor / Link；
- Wrath：满耐久，不继承Enemy死前低耐久运行状态。

独立输出跨Body继续，Body-bound状态清除，具体遵循`01_DESIGN_CANONICAL`。

---

# 5. 当前Card正式内容

当前牌池：

> **73张**

| 分类 | 数量 |
|---|---:|
| 基础通用 | 9 |
| Global Slot质变 | 7 |
| Pride | 8 |
| Sloth | 11 |
| Gluttony | 7 |
| Envy | 10 |
| Wrath | 6 |
| Greed | 8 |
| Lust | 7 |

其中：

- 七罪Monster-Type + Type Growth：57张；
- Type Growth：4张；Gluttony、Wrath与Greed是当前无Type Growth的Owner确认例外；
- 当前无稀有度系统。

详细逐卡真源：

> `Content/Card_System_Current_Truth_v1.1.md`

本文件下列Registry用于快速检索；名称、描述、机制简述、叠层文本等完整字段以详细Card真源为准。

---

# 6. Card Registry

## 基础通用｜9张
| Card ID | 明线名称 | 机制类别 | Stack Max |
|---|---|---|---:|
| `UB-A01` | 远击神谕 | Attack合法作用距离 | 1 |
| `UB-A02` | 扩张圣域 | Attack外显覆盖尺寸 | 1 |
| `UB-A03` | 先制祝祷 | Attack Active / 延伸速度 | 1 |
| `UB-B01` | 朝圣者之步 | Body基础移速 | 1 |
| `UB-M01` | 远征恩典 | Movement位移 / 作用距离 | 1 |
| `UB-M02` | 疾行福音 | Movement Active推进速度 | 1 |
| `UB-S01` | 远域祷文 | Special合法作用距离 | 1 |
| `UB-S02` | 宏仪圣印 | Special外显效果规模 | 1 |
| `UB-S03` | 不息祷文 | Special合法持续参数 | 1 |

## 全局槽位质变｜7张
| Card ID | 明线名称 | 机制类别 | Stack Max |
|---|---|---|---:|
| `GX-MA01` | 征途裁决 | 位移→普攻联动；Stack 1 | 1 |
| `GX-AS01` | 血证大仪 | 普攻→特殊联动；Stack 1 | 1 |
| `GX-A01N` | 双重宣判 | 普攻复执行质变；Stack 1 | 1 |
| `GX-MA02N` | 无隙追猎 | 位移→普攻动作衔接；Stack 1 | 1 |
| `GX-AS02N` | 裁决续礼 | 普攻→特殊动作衔接；Stack 1 | 1 |
| `GX-MR01` | 命运折返 | 位移弹反质变；Stack 1 | 1 |
| `GX-AR01` | 审判回返 | 普攻弹反质变；Stack 1 | 1 |

## 傲慢｜8张
| Card ID | 明线名称 | 机制类别 | Stack Max |
|---|---|---|---:|
| `PR-A01` | 王冠军势 | Attack三道散射；Stack 1 | 1 |
| `PR-A02` | 十字圣裁 | Attack十字剑气；Stack 1 | 1 |
| `PR-A03` | 异端噤声 | Attack高阶Interaction；Stack 1 | 1 |
| `PR-A04` | 王命远征 | Attack合法作用距离强化；Stack 1 | 1 |
| `PR-M01` | 王座之步 | Movement距离+伤害强化；Stack 1 | 1 |
| `PR-S01` | 王权巡猎 | Special数量质变；Stack 1 | 1 |
| `PR-X01` | 征服者之径 | Special路径剑气；Stack 1 | 1 |
| `PR-TG01` | 王权疾令 | 类型成长；Stack 1 | 1 |

## 怠惰｜11张
| Card ID | 明线名称 | 机制类别 | Stack Max |
|---|---|---|---:|
| `SL-A01` | 怠惰者的赦免 | Attack专属强化；Stack 1 | 1 |
| `SL-A02` | 巨像之息 | Attack叠层；Stack 3 | 3 |
| `SL-A03` | 圣骸分裂 | Attack形态质变；Stack 1 | 1 |
| `SL-A04` | 众仆齐鸣 | Attack形态质变；Stack 1 | 1 |
| `SL-A05` | 巨像践踏 | Attack高阶Interaction；Stack 1 | 1 |
| `SL-M01` | 遗下的守望者 | Movement派生质变；Stack 1 | 1 |
| `SL-M02` | 迟来的地鸣 | Movement派生质变；Stack 1 | 1 |
| `SL-S01` | 沉眠侍从 | Special叠层；Stack 2 | 2 |
| `SL-S02` | 遗命不绝 | Special终结协议质变；Stack 1 | 1 |
| `SL-S03` | 侍从圣武 | Special专属成长；Stack 1 | 1 |
| `SL-TG01` | 沉眠遗命 | 类型成长；Stack 1 | 1 |

## 暴食｜7张
| Card ID | 明线名称 | 机制类别 | Stack Max |
|---|---|---|---:|
| `GL-M01` | 饥神猎步 | Movement基础强化 + Attack形态例外；Stack 1 | 1 |
| `GL-A01` | 群口圣宴 | Overfed→Attack成对巨口；Stack 1 | 1 |
| `GL-A02` | 猎步圣餐 | Movement→Attack巨口范围+100%；Stack 1 | 1 |
| `GL-A03` | 远方圣餐 | Attack基础强化；Stack 1 | 1 |
| `GL-S01` | 鲜血圣餐 | Special首次吞噬耐久回复；Stack 1 | 1 |
| `GL-S02` | 最后一餐 | Special条件处决；Stack 1 | 1 |
| `GL-S03` | 万物皆食 | Special高阶Interaction；Stack 1 | 1 |

## 嫉妒｜10张
| Card ID | 明线名称 | 机制类别 | Stack Max |
|---|---|---|---:|
| `EN-A01` | 万眼同视 | Attack周期四目标；Stack 1 | 1 |
| `EN-A03` | 穿镜圣光 | Attack高阶形态质变；Stack 1 | 1 |
| `EN-A04` | 妒神凝视 | Attack叠层；Stack 2 | 2 |
| `EN-A05` | 妒焰渐炽 | Attack持续伤害成长；Stack 1 | 1 |
| `EN-M01` | 镜痕巡猎 | Movement路径印记；Stack 1 | 1 |
| `EN-R01` | 无底之镜 | Mark储存成长；Stack 1 | 1 |
| `EN-R02` | 伤痕告解 | Resource成长；Stack 1 | 1 |
| `EN-R04` | 凝视未终 | Resource叠层；Stack 2 | 2 |
| `EN-S01` | 雷霆作证 | Special派生质变；Stack 2 | 2 |
| `EN-TG01` | 万妒远证 | 类型成长；Stack 1 | 1 |

## 愤怒｜6张
| Card ID | 明线名称 | 机制类别 | Stack Max |
|---|---|---|---:|
| `WR-M01` | 焚途誓约 | Movement路径伤害；Stack 1 | 1 |
| `WR-M02` | 末日锁链 | Movement距离 + 落点冲击；Stack 1 | 1 |
| `WR-B01` | 殉身加冕 | 低耐久伤害 / 攻击范围 / Attack CD缩短；Stack 1 | 1 |
| `WR-B02` | 以身为薪 | Body Trait燃烧光环；Stack 1 | 1 |
| `WR-S01` | 风暴锁链 | Special龙卷风移速强化；Stack 1 | 1 |
| `WR-S03` | 终末飓风 | Special龙卷风时长强化；Stack 1 | 1 |

## 贪婪｜8张
| Card ID | 明线名称 | 机制类别 | Stack Max |
|---|---|---|---:|
| `GR-M01` | 黑油圣路 | Movement黑油范围 / 长度 + 敌人减速；Stack 1 | 1 |
| `GR-M02` | 圣路恩赐 | Movement区域地形伤害防护；Stack 1 | 1 |
| `GR-A01` | 万手圣库 | Attack Resource上限；Stack 2 | 2 |
| `GR-A02` | 未收之贡 | Attack击杀后再索敌；Stack 1 | 1 |
| `GR-A03` | 亡者遗产 | Attack击杀后新增魔手；Stack 1 | 1 |
| `GR-A07` | 迂回纳贡 | Attack侧翼路径形态质变；Stack 1 | 1 |
| `GR-S01` | 圣库纳贡 | Special受击资源转化；Stack 1 | 1 |
| `GR-S04` | 贪神庇护 | Special Guard持续强化；Stack 1 | 1 |

## 色欲｜7张
| Card ID | 明线名称 | 机制类别 | Stack Max |
|---|---|---|---:|
| `LU-M03` | 背离之罚 | Movement换位爆炸；Stack 1 | 1 |
| `LU-M05` | 同欲之影 | Movement→Attack模仿联动；Stack 1 | 1 |
| `LU-S05` | 同欲相噬 | Special牵引碰撞爆炸；Stack 1 | 1 |
| `LU-A03` | 欲痕殉爆 | Attack+Link死亡爆炸；Stack 1 | 1 |
| `LU-S06` | 无害之拥 | Special牵引期间伤害屏蔽；Stack 1 | 1 |
| `LU-A04` | 色欲潮汐 | Attack环形扩散质变；Stack 1 | 1 |
| `LU-TG01` | 欲潮不息 | Type Growth：Body移速 + 三槽冷却减少30%；Stack 1 | 1 |


# 7. Card内容规则

## 7.1 基础通用

9张单轴基础成长，全部`Stack Max = 1`。

主要轴：

- Body基础移动速度；
- Movement距离；
- Movement推进速度；
- Attack距离；
- Attack覆盖尺寸；
- Attack展开 / 延伸速度；
- Special距离；
- Special规模；
- Special持续参数。

作用域：

> **本Run所有普通Enemy + 所有普通Possessed Body。**

Elite不读取。

## 7.2 Type Growth

当前采用显式方案：

- 旧“拿普通Sin卡后后台自动加基础属性”取消；
- 当前共4张；除Gluttony、Wrath与Greed外，每个有Type Growth的Sin恰好1张；Gluttony、Wrath与Greed是Owner确认的无Type Growth例外；
- Stack Max = 1；
- 一次取得即完整生效；
- 约2个直观非伤害维度；
- 单轴原则上低于对应基础通用Card；`LU-TG01`三槽冷却减少30%为Owner确认的当前例外轴，Body基础移速增幅仍低于`UB-B01`；
- `LU-TG01`不缩短技能前摇、后摇或敌人最低预警时间；
- 对应Sin普通Enemy与Possessed同源生效；
- 取得后对应Sin `Investment +1`。

当前ID：`PR-TG01`, `SL-TG01`, `EN-TG01`, `LU-TG01`。

## 7.3 Global Slot

当前7张：

- `GX-MA01`：位移 → 下一Attack范围 / 规模增幅；
- `GX-AS01`：Attack → 下一Special范围 / 规模增幅；
- `GX-A01N`：Attack额外执行一次；
- `GX-MA02N`：Movement有效段后允许Attack取消剩余后摇；
- `GX-AS02N`：Attack有效段后允许Special取消剩余后摇；
- `GX-MR01`：Movement有效阶段反射飞行攻击；
- `GX-AR01`：Attack有效区域反射飞行攻击。

作用域：

> **本Run所有普通Enemy + 所有普通Possessed Body。**

Elite不读取。

额外执行不生成新的Slot Use Event，不触发指数连锁。

反射单次数量上限和合法对象继续为`PLAYABLE / TUNABLE`。

## 7.4 人工删除

Owner最新Excel / Card v1.1删除线为正式删除。

旧Global 6张全部删除：`GX-M01`, `GX-M02`, `GX-A01`, `GX-A02`, `GX-S01`, `GX-S02`。

普通Monster Card正式删除且不补：`PR-S02`, `SL-A06`, `GL-M02`, `GL-X01`, `GL-TG01`, `EN-A02`, `EN-R03`, `EN-X01`, `WR-A01`, `WR-M03`, `WR-S02`, `WR-TG01`, `GR-A04`, `GR-A05`, `GR-A06`, `GR-M03`, `GR-M04`, `GR-S02`, `GR-S03`, `GR-TG01`, `LU-M04`。`GR-M02`已按2026-08-18 Owner Delta以“圣路恩赐”新定义恢复，不沿用历史黑油减速叠层语义。

不得为了恢复历史84张或旧Coverage重新补回。

---

# 8. Card Offer / Legal Pool

确认：

- 当前单Run **9次选卡**；
- Wave 1–7后各1次；
- Wave 8后连续2次；
- `Opening Card = DEPRECATED / REMOVED`；
- 每次3选1；
- 达Stack Max / 唯一Card后退出池；
- 当前Build完全零作用的Card不应出现；
- 同一Offer禁止重复Card ID。

Offer结构：

> **横向位 + Monster-Type位 + Flex位。**

Known Type Set：

- Pride由于是起始Carrier，从Run开始即Known；
- 其他Monster在正式进入本Run Encounter后加入Known Type Set；
- 不要求先击杀或Possess。

Monster-Type / Type Growth取得：

> 对应Sin `Investment +1`。

Investment用于未来普通Enemy的Spawn Weight / Soft Pity，不隐式加基础属性。

Global质变不使用历史W2/W4/W6/W8硬保底，当前采用软保底。

Fallback、精确权重与Pity算法见：

> `Content/Encounter_CardOffer_Baseline_v1.0.md`

状态：`BASELINE / TUNABLE / PLAYABLE`。

---

# 9. Elite Content Contract

当前Elite：

> **Base Monster + External Historical Build Snapshot**

当前Demo用Preset / Fake Historical Build Profile模拟“其他玩家历史Build”。

Elite在Enemy状态和被玩家Possess后都：

- 不读取当前Run Basic Universal；
- 不读取当前Run Global Slot；
- 不读取当前Run Monster-Type；
- 不读取当前Run Type Growth；
- 保留自身Historical Build强化；
- Runtime按正常Possession初始化：满玩家耐久 + 三槽初始可用，不继承死前Cooldown / Body-bound Runtime资源。

当前不建立独立“随机Mutation Monster”单位类。

具体Preset Profile清单与注入权重仍是后续Content Baseline / Playable项。

---

# 10. Encounter / Spawn Content Baseline

当前生成模型：

> **Run Director → Pressure Budget + Legal Spawn Pool + Combination Templates / Constraints + Weighted Random → Runtime Spawner / WaveDef。**

Repository当前可以有自己的Chunk / WaveDef / Spawner底层实现；Canonical只规定上层Run节奏目标，不强制代码架构。

首版可执行算法、W1–W8解锁节奏、Alive目标、Investment Weight、Soft Pity、Elite注入和Final三段逻辑见：

> `Content/Encounter_CardOffer_Baseline_v1.0.md`

该文件状态：

> `BASELINE / TUNABLE / NOT PLAYABLE-VALIDATED`

基本约束：

- Monster逐步进入合法Spawn Pool；
- 已解锁普通Monster继续可出现；
- 投资类型增加重现倾向；
- 重复Miss触发Soft Pity；
- 某一类型过度主导时可轻量抑制；
- Elite消耗Pressure Budget；
- 高压组合仍维持合理Corpse供给；
- Final按阶段提高密度、组合和高价值威胁；
- 不主要靠统一HP / Damage倍率制造难度。

---

# 11. Terrain Content

Gameplay模块：

- Neutral Floor；
- Collision Obstacle；
- Decorative Geometry；
- Speed Zone；
- Slow Zone；
- Lava；
- Spike / Periodic Hazard；
- Spawn-safe区域；
- Spawn legality。

Repository存在大量环境资产，仅作为Production Evidence。

具体Mesh / Material / Art Asset是否采用：

> 不在Content Canonical自动决定。

---

# 12. Tutorial Content

当前最低事件：

- `TUT-01`：基础移动 / Aim；
- `TUT-02`：三槽；
- `TUT-03`：首次Kill → Corpse；
- `TUT-04`：Possession；
- `TUT-05`：主动换身；
- `TUT-06`：首次真实Death Relay；
- `TUT-07`：首次真实Soul / Shrine；
- `TUT-MONSTER-*`：首次Possess某Monster，只说明独特机制。

不暂停的微教学优先。

---

# 13. Result / Review Content

最低正式Result：

- Victory / Fail；
- Restart；
- Return / Lobby。

强烈建议保留的验证数据：

- Run时长；
- Failure Stage / Reason；
- 换身次数；
- Body使用分布；
- Card选择；
- 投资Monster后续出现；
- Elite击杀 / Possession；
- Final到达 / 完成。

Review Jump与正式Run数据必须隔离。

---

# 14. Starting State / First Carrier

当前首局叙事起点仍是：

> Candidate / Soul无稳定Carrier，需要借Body继续存在。

但玩家不先进入可操作Soul阶段。

开场Content：

```text
Opening Cinematic / CG
→ Candidate / Soul处于无稳定Carrier状态
→ 第一具Transfer-Eligible尸体 / Carrier被分配
→ 初始Possession完成
→ 玩家获得傲慢·终刃绝影控制
→ 嵌入式基础教学
→ Wave 1
```

第一具可控Body：

> **Pride / 傲慢·终刃绝影 — CONFIRMED**

当前Demo所有Run沿用固定傲慢开局，不随机、不提供起始Body选择。

这个初始Body从Run开始即进入Known Type Set，但由于Opening Card已删除，不需要任何开局Offer特殊规则。

---

# 15. Content Playable / Tunable

当前不冻结：

- Monster HP / Damage；
- Body Cost；
- CD / Reload（`LU-TG01`三槽冷却减少30%除外，该值为Owner当前确认）；
- 各种持续 / 距离 / Pull量；
- Envy Mark写入比例与储存上限；
- Greed初始魔手；
- Sloth Drone上限；
- 实例预算；
- Type Growth具体增幅（`LU-TG01`的Body基础移速增幅仍待调校）；
- Global Reflection资格 / 安全限制；
- Global 3–4张组合强度；
- Encounter精确数值；
- Final精确压力曲线。

这些进入Playable Validation。

---

# 16. Repository Content Boundary

Repository现状由单独的最新资产 / 工程盘点维护。

原则：

- Repository Fact只描述“当前已经做了什么”；
- 新实现、Prefab、AI、Spawner、地图或旧模块文档不自动成为设计真源；
- 本文件不绑定旧Snapshot B；
- 导入前重新做一次资产盘点即可更新Implementation Gap，不需要因此重开中心设计。

后续Design Intake / Legacy Audit负责判断实现Gap、复用与迁移。

---
