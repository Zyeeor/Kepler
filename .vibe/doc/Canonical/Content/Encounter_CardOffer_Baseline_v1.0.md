# Possession — Encounter & Card Offer Baseline v1.0

**Date:** 2026-08-15<br>
**Status:** `BASELINE / TUNABLE / NOT PLAYABLE-VALIDATED`<br>
**Authority:** 低于`01_DESIGN_CANONICAL.md`与`02_CONTENT_CANONICAL.md`；若冲突，以Canonical与最新Owner Decision为准。

---

# 1. 目的

给当前8 Wave / 9次选卡结构一个**可直接实现的第一版算法**。

它不是最终平衡表。数值、波次强度、Elite频率、权重与Pity均可通过Playable修改。

当前Repository已有或正在制作的Chunk / WaveDef / MonsterSpawner可以作为底层执行器；本文件只定义上层Run Director应提供的节奏与选择规则，不强制代码架构。

---

# 2. Known Type / Spawn Pool

Run开始：

- Pride为固定Starting Carrier；
- Pride立即进入`Known Type Set`；
- Pride是否立刻作为Enemy可刷由Wave解锁表决定，不因Starting Carrier身份自动增加Spawn Weight；
- 其他Sin第一次进入合法Encounter后加入Known Type Set，并保持到本Run结束。

Monster-Type / Type Growth Card只能从Known Type Set对应池中进入Monster-Type位。

---

# 3. W1–W8首版解锁与压力结构

以下是初版，不是最终内容顺序。

| Wave | 新增可刷类型 | 目标Alive压力 | 组合目标 | Elite |
|---|---|---:|---|---|
| W1 | Pride + Gluttony | 3–4 | 只用1–2种清晰威胁，优先教Kill→Corpse→Possession | 无 |
| W2 | + Wrath | 4–5 | 开始混合近身 / 位移压力 | 无 |
| W3 | + Sloth | 5–6 | 首次近远程组合；给玩家学习离手输出 | Eligible，建议首次注入 |
| W4 | + Greed | 6–7 | 加入Guard / 资源型目标，开始要求目标优先级 | 可选 |
| W5 | + Lust | 7–8 | 加入位置牵引与空间关系 | 建议注入 |
| W6 | + Envy | 8–9 | 七罪全部进入池；加入持续锁定 / 延迟兑现压力 | 可选 |
| W7 | 全池 | 9–10 | 3角色组合、投资类型兑现、较高Elite概率 | 建议注入 |
| W8 | 全池 | 10–12 | 完整Build前最终普通Wave，强调组合而非纯数值膨胀 | 高概率但仍占Budget |

设计目标：

- 不是每Wave必须把Alive目标填满；它是Pressure目标范围；
- 每波至少提供合理的可附身Body供应；
- 新类型首登时避免同时叠太多新机制；
- 复杂度增长优先顺序：**类型组合 → 密度 → Elite / 高价值目标 → 少量数值修正**。

---

# 4. Pressure Budget初版

建议用相对Budget Index，而不是先锁绝对数值：

| Wave | Budget Index |
|---|---:|
| W1 | 1.00 |
| W2 | 1.20 |
| W3 | 1.45 |
| W4 | 1.70 |
| W5 | 2.00 |
| W6 | 2.35 |
| W7 | 2.70 |
| W8 | 3.10 |

每种Monster / Elite Profile由程序或Content配置`Threat Cost`。

Director在Budget内抽取合法组合；Elite必须消耗Budget，不作为免费额外怪。

统一HP / Damage / MoveSpeed倍率只能作为次级调节，不应成为主难度曲线。

---

# 5. Combination Templates

当前至少支持以下模板：

1. `FOCUS`：1个主要类型 + 少量同类，适合新类型首登；
2. `DUO`：2种岗位互补；
3. `TRIO`：3种岗位组合，W4以后逐步使用；
4. `ANCHOR + PRESSURE`：1个高价值 / 防守 / 远程核心 + 机动压力单位；
5. `ELITE + SUPPORT`：1个Elite + 少量普通支援，Elite成本计入Budget；
6. `PAYOFF`：主动提高玩家高Investment类型出现概率，让Reverse-BD有兑现机会。

不建立固定七罪克制矩阵。

---

# 6. Investment Weight / Soft Pity

每个普通Sin维护：

- `Investment_s`：本Run取得该Sin的Monster-Type或Type Growth Card数量；
- `Miss_s`：该Sin已经合法但连续多少个Wave未出现；
- `RecentShare_s`：最近2个Wave该Sin在生成中的占比。

初版权重：

```text
EffectiveWeight_s = BaseWeight_s
                  × InvestmentMultiplier_s
                  × PityMultiplier_s
                  × RepeatSuppression_s
```

建议初值：

- `BaseWeight = 1.0`；
- 每1 Investment：`+0.30`乘数增量，建议上限`+1.50`；
- Eligible后连续2个Wave未出现，从第3次抽取开始每Miss增加`+0.50` Pity，建议上限`+1.50`；
- 若某Sin最近2 Wave占比明显过高，`RepeatSuppression = 0.70–0.85`；
- 该Sin实际生成并达到最低有效数量后，`Miss_s`重置。

以上数字全部`TUNABLE`。

核心Acceptance：

> 玩家连续投资某Sin后，应在后续1–2个有效Wave窗口内明显感到“它更容易回来”，但不能100%锁死下一波。

---

# 7. Elite注入

初版：

- W1–W2不注入；
- W3开始Eligible；
- W3 / W5 / W7作为推荐节奏点，不是硬保证；
- W8可以高概率出现；
- 同一Wave默认不超过1个Elite，除非专项压力测试；
- Elite占用Pressure Budget；
- Elite不读取当前Run任何Card层；
- Elite死亡后作为高价值可Possess Body，仍只携带自己的Historical Build Snapshot。

具体Fake Profile另行Content化。

---

# 8. Final三段初版

Final继续用约5分钟作为**测试上限Baseline**，实际时长需Playtest。

建议按比例而不是绝对秒数组织：

- Phase F1（约前40%）：密度上升，维持已熟悉组合；
- Phase F2（约中35%）：更多3角色组合 / Elite / 高价值威胁；
- Phase F3（约后25%）：峰值压力，但最后短窗口降低新增生成，允许清场与胜利收束。

Final必须保留尸体供应与换身循环，不得退化成单纯躲避计时。

---

# 9. Card Offer — 当前9次

时点：

- W1–W7后各1次；
- W8后连续2次；
- 无Opening Card。

每次3选1：

## Slot A — Horizontal

合法池：

- Basic Universal；
- Global Slot。

## Slot B — Monster Type

合法池：

- Known Type Set对应的Monster-Type Card；
- 对应Type Growth。

按对应Sin Investment做轻度加权。

## Slot C — Flex

从剩余全部合法Card中抽取；排除本次已经出现的ID。

---

# 10. Offer过滤与Fallback

先过滤：

1. 已达Stack Max；
2. Standalone / Unique已取得；
3. 前置不满足；
4. 对当前合法对象完全零效果；
5. 同一次Offer已被另一个Slot选中的同Card ID。

Fallback：

- Horizontal空 → 从Flex合法池补；
- Monster-Type空 → 先从Known Type的其他合法Type Card补；仍空则Flex；
- Flex空 → 从任一未使用合法池补；
- 最终不足3个唯一合法ID时，可以少于3个，不生成无效/重复假选项。

---

# 11. Global软保底

不恢复历史`W2/W4/W6/W8硬保底质变`。

初版软保底：

- Global基础权重与Basic同池竞争；
- 连续2次Offer三张都没有Global后，Global候选权重开始提高；
- 每继续Miss一次进一步提高；
- 某次Offer出现Global后重置；
- 只提高“出现机会”，不强制玩家拿Global。

具体权重Playable调节。

---

# 12. Acceptance

首版算法至少通过：

- W1不因类型过多造成教学爆炸；
- W6前七罪可以全部进入合法生态；
- 投资类型有可感知重现；
- 不投资类型也不会永久消失；
- Elite不是免费额外密度；
- 任何Wave都有合理Corpse供应；
- 9次选卡能形成可辨识Build；
- 不因无效池生成空白 / 重复 / 零效果Offer；
- Global不会硬保底到每局高度模板化；
- Final仍持续要求Possession，而不是把核心循环关掉。
