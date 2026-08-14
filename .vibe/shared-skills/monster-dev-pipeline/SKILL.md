---
name: monster-dev-pipeline
description: >-
  Possession 七宗罪怪物流水线：①CSV→细分开发需求.md；②细分需求→工程落地；③用户说「提交xx怪物/所有怪物」时
  按怪物范围 commit&push 当前分支、合入 origin/main、再推 main。策划改表、落地、验收、提交时使用。
---

# 怪物开发流水线（monster-dev-pipeline）

本 skill 是后续怪物开发的**唯一流程入口**。根目录旧的 `怪物技能配置指南.md` 视为历史副产物，**不要依赖它**；测试指南应在落地阶段按本 skill 生成/更新。

权威输入：


| 输入          | 路径                                    |
| ----------- | ------------------------------------- |
| 项目规则        | `.vibe/rules.md`                      |
| 策划总表        | `项目全量表 - 怪物设计（新）.csv`                 |
| 模块总览        | `.vibe/doc/项目设计.md`                   |
| 怪物细分需求      | `.vibe/doc/Modules/Monsters/<中文名>.md` |
| 参考实现（已完成范例） | `pride_new`、`sloth_new`               |


输出：


| 输出          | 路径                                                  |
| ----------- | --------------------------------------------------- |
| 流程本文件       | `.vibe/shared-skills/monster-dev-pipeline/SKILL.md` |
| 每怪细分需求      | `.vibe/doc/Modules/Monsters/<中文名>.md`               |
| 每怪测试指南（落地后） | `.vibe/doc/Modules/Monsters/<中文名>-技能配置测试指南.md`      |


**禁止**：本 skill 触发的「生成细分需求」阶段不得改代码 / Prefab / `Packages/` / `ProjectSettings/`。  
**工程落地**阶段才允许改工程，且必须先读对应细分需求.md，遵守 `.vibe/rules.md`。

---



## 状态枚举（强制，全文统一）

每一项（怪物总览、技能、词条、资源行）必须使用且仅使用：


| 状态        | 含义                                                                |
| --------- | ----------------------------------------------------------------- |
| `策划未明确`   | CSV/策划文案为空、自相矛盾、或关键规则无法唯一解释；**不得实现**，只能写「待策划填写」问题                 |
| `待开发`     | 策划已写清，工程尚未做                                                       |
| `已开发但没资源` | 逻辑/Prefab 已可玩，但正式美术资源缺失；CSV「制作中」= 此状态。可暂用占位 VFX，必须在资源栏写清占位名与「待替换」 |
| `已完成`     | 逻辑 + 正式资源都齐，可验收通过                                                 |


判定优先级：

1. 技能详情 / 词条 / 关键数值任一关键字段空 → `策划未明确`
2. 有设计、无代码 → `待开发`
3. 有代码、资源列为空或写「制作中」或仍是占位 → `已开发但没资源`
4. 代码与正式资源都齐 → `已完成`

怪物级总状态 = 各槽位/词条/关键资源的**最差状态**（策划未明确 < 待开发 < 已开发但没资源 < 已完成，取最左）。

---



## 阶段总览

```text
策划改 CSV
    │
    ▼
【阶段 A】按本 skill 生成/更新 细分开发需求.md
    │  （找茬、留空、资源占位、状态）
    │  策划填空 → 状态从「策划未明确」推进
    ▼
【阶段 B】按细分需求.md 工程落地
    │  （脚本 / Effect / Tag / Card / Prefab / Cheat）
    ▼
生成/更新 《技能配置测试指南》并 Play Mode 验收
    │
    ▼
【阶段 C】用户说「提交xx怪物 / 提交所有怪物」
         commit&push 当前分支 → 合入 origin/main → push main
```

用户日常用法：

1. 策划改 CSV → 对 Agent 说：「按 monster-dev-pipeline 阶段 A，更新某某怪物细分需求」
2. 策划填完空 → 「按阶段 B，落地某某怪物」
3. 只补资源 → 「按细分需求把某某资源行从已开发但没资源改为已完成」
4. 要入库 → 「提交傲慢」/「提交怠惰-机械之灵」/「提交所有怪物」（走阶段 C）

---



# 阶段 A — 策划案 → 细分开发需求.md



## A.0 何时执行

- 新建怪物
- CSV 有任何改动
- 用户要求「按流程 skill 刷新细分需求」



## A.1 必读

1. `.vibe/rules.md`
2. 本 skill
3. 最新 `项目全量表 - 怪物设计（新）.csv`
4. 若已存在 `.vibe/doc/Modules/Monsters/<怪物>.md`，**增量更新**，不要无故抹掉已填的资源名 / 策划答复 / 工程路径



## A.2 解析 CSV 规则

CSV 是宽表：同一怪物跨多行。按「怪物名称」非空行开始一个怪物块，直到下一个非空怪物名。

每个怪物块必须抽出：


| 字段                  | 来源列                            |
| ------------------- | ------------------------------ |
| 显示名                 | 怪物名称(暂时)                       |
| 移速 / 血量             | 基础属性                           |
| 情绪 / 特点 / 体貌        | 对应列                            |
| 模型状态                | 角色模型初步状态                       |
| 三槽技能                | 技能列含 位移/普攻/技能（及 space/左键/Q 标注） |
| 技能详情 / CD / 耗血 / 伤害 | 同行及续行                          |
| 词条                  | 卡牌Build词条（可多行挂在同一技能下）          |
| 资源与资源状态             | 需要特效资源 + 其状态列                  |


空白怪物行（只有空的 位移/普攻/技能 骨架）→ **跳过，不生成文件**。

命名建议（工程英文 id，写入细分需求「工程标识」）：


| 中文      | 建议 id / prefab              |
| ------- | --------------------------- |
| 主角-灵魂体  | `soul` / 现有灵魂 prefab（以工程为准） |
| 傲慢-终刃绝影 | `pride` / `pride_new`       |
| 怠惰-机械之灵 | `sloth` / `sloth_new`       |
| 暴食-魔猫   | `gluttony` / `gluttony_new` |
| 嫉妒-激光异形 | `envy` / `envy_new`         |
| 愤怒-链狱冥兽 | `wrath` / `wrath_new`       |
| 贪婪-万手藏主 | `greed` / `greed_new`       |
| 色欲-灵念师  | `lust` / `lust_new`         |


旧 `*(full)` Prefab **只作参考，禁止直接改成正式交付**；新怪一律 `*_new`。

## A.3 「找茬」强制清单（每技能 / 每词条都要过）

Agent 必须主动对每一项提问；CSV 答不上来的写成 `策划未明确` 并留「待策划填写」空位。至少覆盖：

### 通用

- [ ] 触发输入：左键 / Space / Q / 被动？按住还是点按？松手结算还是按下即放？
- [ ] 目标：最近 / 瞄准方向 / 最高血量 / 范围内全部 / 无目标是否可放？
- [ ] 数值：伤害、CD、耗血、时长、半径、段数 — 缺一标未明确
- [ ] 与位移/飞行/变身叠加：能否打断？共享 CD？
- [ ] 附身 vs AI：行为是否一致？召唤物跟谁？
- [ ] 死亡/离身：被动「离开后持续 10 秒」是否对本技能生效？残留物怎么处理？
- [ ] Tag / effectId 命名是否已定？



### 位移

- [ ] 纯位移还是位移+伤害？
- [ ] 前摇 / 无敌 / 不可选中？
- [ ] 落点判定、撞墙、撞敌？



### 普攻

- [ ] 近战盒 / 弹道 / 持续射线？
- [ ] 蓄力曲线？最小/最大伤害？
- [ ] 命中特效挂自己还是挂敌人？



### Q / 召唤 / 残留物

- [ ] 召唤物寿命到期是「直接销毁」还是「先死亡表现再销毁」？
- [ ] 召唤物死亡：爆炸？回血？变残影？
- [ ] 主人死亡后召唤物：立刻死 / 继续活 / 叛变？
- [ ] 场上数量上限？



### 词条

- [ ] 是改参数（`abilityParameters`）还是改行为分支（`IsUpgradeUnlocked`）？
- [ ] 与其它词条是否叠加？互斥？
- [ ] 需要独立特效吗？



### 资源

- [ ] 每一句美术描述拆成独立资源行
- [ ] 每行必须有：`资源用途`、`资源名称（可空）`、`状态`
- [ ] CSV 写「制作中」→ 状态=`已开发但没资源`，资源名称可填占位或留空



## A.4 输出文件模板（必须按此结构）

路径：`.vibe/doc/Modules/Monsters/<怪物中文名>.md`

```markdown
# <怪物中文名> — 细分开发需求

> 来源：`项目全量表 - 怪物设计（新）.csv`  
> 生成/更新：按 `.vibe/shared-skills/monster-dev-pipeline/SKILL.md` 阶段 A  
> 怪物总状态：`<状态>`

## 0. 工程标识

| 项 | 值 | 状态 |
|---|---|---|
| 英文 id | `pride` | |
| 正式 Prefab | `Assets/Prefabs/Monster/pride_new.prefab` | |
| 参考旧 Prefab | `pride(full)`（只读） | |
| Cheat 显示名 / 热键 | | |
| 模型资源 | | |

## 1. 基础属性

| 项 | 策划值 | 工程字段 | 状态 | 备注 |
|---|---|---|---|---|
| 移速 | | MonsterActor / Enemy moveSpeed | | |
| 血量 | | maxHealth | | |
| 情绪 / 特点 / 体貌 | | — | | 美术向 |

## 2. 技能总表

| 槽位 | 技能名 | Ability 类（建议/已有） | abilityTags | CD | 耗血 | 伤害 | 状态 |
|---|---|---|---|---|---|---|---|
| Passive / 位移 | | | Ability.Monster.<Sin>.<Skill> | | | | |
| LMB / 普攻 | | | | | | | |
| Q / 技能 | | | | | | | |
| Passive | | | | | | | |

## 3. 技能明细

### 3.x <槽位> — <技能名>

- **状态**：
- **策划原文**：
- **行为规格**（点按/蓄力/目标/段数/无敌等）：
- **数值**：
- **Ability 脚本**：
- **自身 Effect / 命中 Effect**：
- **资源表**：

| 资源用途 | 资源名称 | 状态 | 备注 |
|---|---|---|---|
| | （待填） | | |

- **待策划填写**：
  1. …

### 词条

| 词条名 | effectId | 解锁行为 | 参数 key/value | 资源名称 | 状态 | 待策划填写 |
|---|---|---|---|---|---|---|
| | | | | （待填） | | |

## 4. 卡牌 / Catalog 清单

| effectId | targetAbilityTags | 参数 | CardLibrary | upgrades 槽 | 状态 |
|---|---|---|---|---|---|

## 5. 开放问题汇总（策划填空区）

| ID | 问题 | 答案（策划填） | 影响项 | 状态 |
|---|---|---|---|---|
| Q1 | | | | 策划未明确 |

## 6. 工程落地检查（阶段 B 勾选）

- [ ] Ability 脚本
- [ ] Effect 资产 + GameplayTagCatalog
- [ ] Prefab 三槽 + hpCost
- [ ] CardLibrary + upgrades
- [ ] MonsterCheatCatalog / MonsterAIConfig
- [ ] 测试指南已生成
- [ ] Play Mode 验收通过
```



## A.5 阶段 A 完成标准

- 每个 CSV 已命名怪物都有对应 md（或已声明跳过原因）
- 空技能/空词条 → `策划未明确` + 开放问题
- 有设计无工程 → `待开发`
- 更新 `.vibe/doc/Modules/Monsters/README.md` 总表状态
- **不写代码**



## A.6 范例对照（用来校准「找茬」深度）

傲慢 / 怠惰已证明必须提前问清的典型坑：


| 坑        | 正确问法                | 参考结论                  |
| -------- | ------------------- | --------------------- |
| 落地爆炸特效挂谁 | 挂自己还是挂每个敌人？         | 怠惰：伤害 AoE，VFX 挂自己     |
| 地雷何时生成   | 起飞前还是落地后？           | 怠惰：起飞前、起飞点            |
| 散射何时触发   | 超时爆炸也散射吗？           | 怠惰：仅主弹命中敌人；碎片忽略首目标    |
| 召唤物寿命到   | 直接 Destroy 还是先「死亡」？ | 怠惰死亡炸：先脱离跟随再俯冲，炸完再销毁  |
| 主人死后召唤物  | 活？死？继续攻击？           | 必须写进开放问题，禁止猜测         |
| 旧词条过时    | CSV 删了的词条是否废弃？      | 以最新 CSV 为准，旧实现要标「待对齐」 |


---



# 阶段 B — 细分需求.md → 工程落地



## B.0 前置门禁

1. 读 `.vibe/rules.md` + 本 skill + 目标怪物细分需求.md
2. 若仍有**阻塞性** `策划未明确`（无默认值就无法实现）→ **停止并列出问题**，禁止靠猜落地
3. 非阻塞未明确项可保留，但不得假装 `已完成`
4. 改 Prefab / 场景前 `git status`；不改 `Packages/manifest.json`、`ProjectSettings/`（除非用户明确放行）
5. 不改 Shared Original 旧怪 `*(full)`；只建/改 `*_new`



## B.1 标准落地顺序（严格按序）



### Step 1 — 对齐三槽拆分

把细分需求 §2/§3 映射为：


| 输入    | `EnemyAbility.type` | `MonsterActor` 列表              |
| ----- | ------------------- | ------------------------------ |
| 左键    | `BasicAttack`       | `basicAbilities` + `hpCost`    |
| Space | `Mobility`          | `mobilityAbilities` + `hpCost` |
| Q     | `Skill`             | `skillAbilities` + `hpCost`    |
| 常驻    | `Passive`           | 被动组件                           |


原则（来自傲慢/怠惰）：

1. **一技能一类**：独立玩法 → 独立 `EnemyAbility_`*，不往万能原型塞枚举
2. **卡牌只认 Tag**：`abilityTags` / `CardData.targetAbilityTags`
3. **伤害走** `DealDamageTo` / `SettleHit`，才能吃吸血与 `appliedEffectTags`
4. 有自定义 Mobility 时，不要再挂通用 `EnemyAbility_MobilityDash`
5. Prefab 用完整资产 `*_new`，避免深层 Variant 丢引用



### Step 2 — Ability 脚本

路径：`Assets/Scripts/Combat/Abilities/Monster/`

- 优先复用已有类；没有再新建  
- `Reset()` 或初始化里确保默认 `abilityTags`  
- 词条：`IsUpgradeUnlocked("Xxx.Yyy")` 或 `GetCardParameter("Key", default)`  
- 连续耗血技能走 `PayAbilityHpCost`  
- 身体/武器特效字段在 Ability 上（`vfxPrefab` / `weaponVfxPrefab`），命中特效在 Effect 上

参考类：


| 类                                                                            | 怪物    |
| ---------------------------------------------------------------------------- | ----- |
| `EnemyAbility_PrideChargeStrike` / `SwordQi` / `PrideBlinkChain`             | 傲慢    |
| `EnemyAbility_SlothLaunch` / `SlothChargeShot` / `SlothDrone` / `SummonBolt` | 怠惰    |
| `SummonActor`                                                                | 召唤物通用 |




### Step 3 — Effect 资产

`Create > Possession/Combat/Gameplay Effect` → `Assets/Combat/Effects/`


| 用途                | 配法                                                  |
| ----------------- | --------------------------------------------------- |
| 自身持续态（不可选中/飞行/变身） | `duration<=0` 或技能时长；`grantedTags`；`activeVfxPrefab` |
| 命中反馈              | 短 duration；`hitVfxPrefab` + `hitVfxDuration`        |


登记：`GameplayTagCatalog.declaredTags` + `effectDefinitions`；场景 `CardManager.gameplayTagCatalog` 必须指向它。

### Step 4 — Prefab

```text
xxx_new
├─ Enemy + CombatAbilityComponent + MonsterActor 列表
└─ abilities/
   ├─ basic_*
   ├─ mobility_*
   ├─ skill_*
   └─ passive_*（可选）
```

填写：`displayName`、`maxHealth`、`moveSpeed`、各 Ability 数值、`abilityTags`、`upgrades.effectId`、VFX 引用。  
召唤物另建 prefab（如 `sloth_drone`），挂自己的 Ability。

### Step 5 — 卡牌

`Assets/Configs/CardLibrary.asset`：

```text
effectId: Sin.CardName
targetAbilityTags: Ability.Monster.Sin.Skill
abilityParameters: [{ key: Foo, value: N }]
```

Ability `upgrades` 必须包含同名 `effectId`。

### Step 6 — 测试入口

- `MonsterCheatCatalog`：显示名、prefab、热键  
- 需要时 `MonsterAIConfig` 加条目  
- `CombatTest`：`MonsterPossessionCheat`；建议关 `testDoublePickOnStart` 以免挡热键



### Step 7 — 生成《技能配置测试指南》

路径：`.vibe/doc/Modules/Monsters/<怪物中文名>-技能配置测试指南.md`

至少包含：

1. 热键：生成木桩 / 附身本怪 / 开卡
2. 每技能：无目标 / 单目标 / 多目标
3. 每词条：解锁前后对比
4. 资源占位项单独列出「看的是占位还是正式」
5. 已知问题



### Step 8 — 回写细分需求状态

- 逻辑完成但资源仍空/制作中 → `已开发但没资源`，资源名称填占位 prefab  
- 正式资源挂上 → `已完成`  
- 更新 Monsters `README.md` 总表



## B.2 Tag / 命名约定

```text
Ability.Monster.<Sin>.<Skill>
Effect.Combat.<Sin><HitName>
Effect.Defense.Untargetable / DamageImmune
State.Defense.Untargetable / Flying / DamageImmune
Card effectId: <Sin>.<CardName>   # 例 Pride.Pierce / Sloth.LandingMine
```

`<Sin>`：`Pride` `Sloth` `Gluttony` `Envy` `Wrath` `Greed` `Lust` `Soul`

## B.3 卡牌参数常用 key（可扩展，扩展时写进细分需求）


| key                 | 含义     |
| ------------------- | ------ |
| `ChargeDistance`    | 突进距离   |
| `BlinkCount`        | 穿梭次数   |
| `SummonCount`       | 召唤数量   |
| `TransformDuration` | 变身时长   |
| `FlightDuration`    | 飞行时长   |
| `ExecuteThreshold`  | 斩杀血量比例 |
| `BladeCount`        | 环绕数量   |




## B.4 阶段 B 完成标准

- 细分需求 §6 检查清单全勾（资源允许仍为「已开发但没资源」）  
- 无阻塞性 `策划未明确` 被偷偷实现  
- 测试指南可独立交给他人验收  
- 未改旧 `*(full)`，未动 Packages/ProjectSettings（除非获准）

---



# 阶段 C — Git 版本管理（提交怪物）

> 本阶段是用户**明确口述提交指令**时的授权流程。平时仍遵守 `.vibe/rules.md`：未说「提交…」则不 `commit` / `push`。  
> 用户说出下方触发语 = 授权完成本节全部步骤（含 push 当前分支与 push `main`）。

## C.0 触发语（语义匹配，不要求一字不差）

| 用户说 | 范围 |
|---|---|
| 「提交\<怪物\>」「把\<怪物\>提交了」「提交傲慢 / 怠惰 / pride / sloth_new …」 | **单怪**：该怪物全部相关改动 |
| 「提交所有怪物」「提交全部怪物」「把怪物相关都提交」 | **全怪**：所有怪物相关改动 |

「怪物名」可用中文全称、简称（傲慢/怠惰）、英文 id（`pride`/`sloth`）、或 prefab 名（`pride_new`）。解析不到唯一怪物 → **先问用户**，不要猜。

## C.1 固定操作顺序（不得跳步、不得 force）

```text
1. 在当前工作分支（通常 dev_*）识别并暂存「范围内」文件
2. commit（有改动才提交；信息写清怪物范围）
3. push 到当前分支的远端（origin/<当前分支>）
4. fetch origin main；把 origin/main 合进当前分支（merge，默认不用 rebase）
5. 冲突：AI 能安全解决则解决并继续；否则停下交给用户，解决前不 push main
6. 将当前分支合入本地 main（fast-forward 优先），再 push origin main
7. checkout 回用户原来的工作分支；回报 commit hash 与是否已上 main
```

硬性禁止：

- `git push --force` / `--force-with-lease`（除非用户当次另用更高优先级明文放行）
- `git reset --hard`、`git clean -fdx`
- `--no-verify` 跳过 hook
- 把无关文件塞进本次提交（见 C.2 / C.3）

## C.2 「单怪」相关文件怎么圈定

以该怪细分需求 `.vibe/doc/Modules/Monsters/<中文名>.md` 的「工程标识」+ 实际 diff 为准，**只 stage 与该怪有关的路径**。典型包括：

| 类别 | 示例（傲慢 / 怠惰） |
|---|---|
| 细分需求 / 测试指南 | `.vibe/doc/Modules/Monsters/傲慢-终刃绝影.md`、`…-技能配置测试指南.md`；必要时更新 `Modules/Monsters/README.md` 中该行状态 |
| Prefab | `Assets/Prefabs/Monster/pride_new.prefab`、`sloth_new.prefab`、`sloth_drone.prefab` 及对应 `.meta` |
| Ability / Actor 脚本 | `EnemyAbility_Pride*.cs`、`EnemyAbility_SwordQi.cs`、`EnemyAbility_Sloth*.cs`、`SummonActor.cs`（**仅当本次确为该怪改动**）及 `.meta` |
| Effect 资产 | `Assets/Combat/Effects/Effect_*Pride*`、`Effect_*Sloth*` 等 |
| 配置片段 | `CardLibrary.asset`、`GameplayTagCatalog.asset`、`MonsterCheatCatalog.asset`、`MonsterAIConfig.asset`、相关场景（如 `CombatTest.unity`）——**仅当 diff 内容属于该怪**；若同一文件混有多怪改动，优先只提交能干净分离的 hunk；无法分离则询问用户：本次扩大到多怪，或先拆开 |

不要纳入单怪提交（除非用户明确说带上）：

- `.DS_Store`、本地 Excel/CSV 策划源表（除非用户点名）
- `Packages/packages-lock.json`、`Packages/manifest.json`、`Packages/com.quaza.unitymcp/`
- `ProjectSettings/`、`Library/`、其它怪物的 prefab/脚本/文档
- 与怪物无关的重构 / 工具改动

拿不准某文件是否属于该怪 → **列出路径问用户**，不要擅自扩大范围。

## C.3 「所有怪物」相关文件怎么圈定

Stage 所有与怪物流水线/实现相关的改动，包括但不限于：

- `.vibe/doc/Modules/Monsters/**`
- `.vibe/doc/项目设计.md`（若只改了怪物索引）
- `.vibe/shared-skills/monster-dev-pipeline/**`
- `Assets/Prefabs/Monster/**` 下本次改动的 `*_new` / 召唤物等
- `Assets/Scripts/Combat/Abilities/Monster/**`、`Assets/Scripts/Combat/Actors/**` 中与怪物相关的改动
- `Assets/Combat/Effects/**` 中怪物 Effect
- `Assets/Configs/CardLibrary.asset`、`MonsterCheatCatalog.asset`、`MonsterAIConfig.asset`、`Assets/GameplayTagCatalog.asset`、相关测试场景

仍排除：`.DS_Store`、未点名的策划 xlsx/csv、`Packages/**`（含 lock / unitymcp）、`ProjectSettings/`、明显非怪物改动。

## C.4 Commit message

- 单怪：`feat(<id>): …` 或 `docs(<id>): …` / `fix(<id>): …`，`<id>` 用英文 id（`pride`/`sloth`/…）
- 全怪：`feat(monsters): …` 或 `docs(monsters): …`
- 1–2 句，写清**为什么**；用 HEREDOC 提交
- 无改动可提交时：不建空 commit，直接进入 merge/push 检查并告知「工作区无该范围变更」

## C.5 合 main 与冲突

1. `git fetch origin main`
2. 在**当前功能分支**上：`git merge origin/main`（保留合并提交；与仓库既有习惯一致）
3. 冲突处理：
   - **可自行解决**：纯文档合并、明显一边新增、按细分需求/本 skill 能唯一判定的代码；解决后继续编译风险低的路径，再 commit merge
   - **必须交给用户**：Prefab/场景/asset YAML 双方都改且无法判断；Shared Original；`ProjectSettings`；逻辑语义冲突；任何需要 `--ours/--theirs` 盲选的 Unity YAML → **停下**，列出冲突文件与原因，等用户解决后再从步骤 6 续跑
4. 功能分支已含 main 且已 push 后：`checkout main` → `merge` 功能分支（优先 fast-forward）→ `push origin main` → `checkout` 回原分支

Push `main` 若被保护分支/权限拦截：把错误原文给用户，不要改用 force。

## C.6 阶段 C 完成标准

- 范围内文件已进 commit，且已 push 当前远端分支
- 当前分支已合并最新 `origin/main`（或已因冲突暂停并说明）
- 无冲突时：`origin/main` 已包含本次提交
- 工作区回到用户原分支；未提交的无关脏文件仍保持未 stage

---

# Agent 话术触发（给用户/其它 AI）


| 用户说                                 | 执行                     |
| ----------------------------------- | ---------------------- |
| 「按怪物流程 / monster-dev-pipeline 刷新需求」 | 仅阶段 A                  |
| 「根据 CSV 生成细分需求」                     | 仅阶段 A                  |
| 「按细分需求落地某某怪」                        | 阶段 B                   |
| 「补某某特效资源并改状态」                       | 改 Prefab 引用 + 回写 md 状态 |
| 「生成/更新测试指南」                         | B.Step 7               |
| 「提交\<怪物\>」/「提交傲慢」等                   | **阶段 C（单怪）**           |
| 「提交所有怪物」/「提交全部怪物」                   | **阶段 C（全怪）**           |


一次请求若同时要 A+B，先 A 产出 md 给用户看关键 `策划未明确`，用户确认后再 B。  
「提交…」可与 A/B 分开说；若同一句话既落地又提交，先完成 B 再跑 C。