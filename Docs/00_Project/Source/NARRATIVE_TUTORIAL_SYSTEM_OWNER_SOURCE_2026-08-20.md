# Narrative / Tutorial System Owner Source — 2026-08-20

> Status: OWNER-PROVIDED SOURCE / APPROVED FOR DESIGN INTAKE
>
> 本文件记录2026-08-20由Owner确认的叙事旁白、双线投放、音频/文字配置、First Clear数据与教学系统需求。它是Design Intake输入；正式设计以审核后更新的Canonical为准。

## 1. Owner目标

在不提前冻结最终旁白数量、音频数量、文案数量、具体触发节点和最终术语的前提下，先让程序开发一套可由策划自行配置、增删、替换和调试的基础系统。

目标系统必须支持：

- Mythic众神声与System电子声两个独立旁白表现通道；
- 数据驱动的旁白事件、触发条件、播放规则和多句组合；
- Narrative Access、触发事件与文本显示模式彼此解耦；
- Card与其他双线文本按可配置规则切换Mythic / System版本；
- 统一Text Key、Audio ID与术语配置，避免在代码和Prefab中写死最终文案或音频路径；
- 首次成功Run完成核心功能真相揭示，后续Run用低频Recognition继续深化；
- First Clear读取本Run统计，输出主倾向、次倾向和一句行为方式描述；
- 数据驱动、低成本、不暂停优先的教学系统；
- 旁白、教学、音频、文字、Profile存档与调试工具共享必要基础设施。

## 2. 已确认的叙事表现

### 2.1 双旁白通道

- Mythic通道使用众神 / 神谕式声音；
- System通道使用冷静、礼貌、程序化的电子声音；
- 两个声音是两个Presentation Channel，不自动定义为两个独立世界实体或人格；
- 两者不争论、不吐槽玩家、不形成善恶人格对话；
- 二者最终本体关系继续Open。

### 2.2 揭示节奏

- 首个成功Run必须让试玩玩家体验到System、认证、Version / Instance与User Distillation的核心功能真相；
- 后续Run允许通过prior certification、Certified Lineage与Version Trials继续增加Recognition；
- A0–A4的信息边界继续有效，但具体由Wave、Elite、Card、Possession或自定义事件中的哪一个推进，必须可配置，不写死；
- Elite一局可能出现3–4次，可作为递进旁白窗口，但不承担唯一不可替代的Reveal推进职责；
- Failure次数不推进Reveal，但可以按本Run已经达到的Access播放对应版本失败反馈。

### 2.3 Card与文本显示

- Card继续保留Mythic / System两套名称与包装描述；
- Card ID、图标、分类、Gameplay效果和中性机制摘要始终不变；
- Card何时切换版本、已获得Card是否同步切换、哪些界面跟随Access，全部配置化；
- 普通UI默认不并排展示两版；
- 最终术语通过统一Concept Key映射Neutral / Mythic / System文本，后续策划可替换。

## 3. 旁白系统配置要求

每个旁白事件需要支持：

- 稳定Cue ID；
- Trigger Event；
- 第N次事件与AND / OR条件；
- 当前Access条件与可选Access变更；
- Speaker Channel；
- Text Key；
- Audio ID；
- 单句或多句顺序组合；
- 组内间隔与中断规则；
- 延迟、优先级与重复范围；
- 每Run一次、全Profile一次或允许重复；
- 高压战斗时延后、放弃或强制播放；
- BGM Duck；
- 字幕模式：None / Optional / Forced；
- 可选Card / UI Display Mode变更。

播放调度最低要求：

- 同时只播放一条Voice；
- 主线Reveal高于普通氛围旁白；
- 普通旁白可在高压战斗中延后或放弃；
- 音频缺失时仍允许显示文本；
- Forced文本可以由Result UI、阶段标题或字幕承载，不要求全部使用常驻字幕；
- Voice使用独立通道，不占用普通战斗SFX池。

## 4. First Clear与Run数据

First Clear继续使用当前Canonical固定结构。Self-Declaration只表达玩家对“我是谁”的态度，不改变胜负、Build、评分、结局或能力。

程序先采集原始数据，评分规则配置化。最低字段：

- 每个Sin的有效Body控制时长；
- 每个Sin的Possession次数；
- 每个Sin的Movement / Attack / Special使用次数；
- 每个Sin的Card投资数量；
- Run时长、到达Wave、Final到达 / 完成；
- 总Possession、主动离身、Death Relay、Soul进入、Shrine恢复；
- 低耐久主动离身；
- Bullet Time使用次数与总时长；
- Elite击杀与Elite Possession；
- 使用过的不同Sin数量。

主倾向、次倾向和行为句式的权重、阈值、词库保持可配置。Model / Version / Instance使用模板生成，最终玩家可见格式后补。

## 5. 教学系统Owner结论

### 5.1 总体结构

- New Save使用嵌入式真实战斗教学；
- 不设独立Wave 0，不恢复Opening Card；
- `TUT-01 / 02`用于初始Pride取得控制后的短准备段；
- `TUT-03 / 04 / 05`嵌入正常Wave与真实战斗；
- `TUT-06 / 07`按真实Death Relay、Soul / Shrine条件延迟触发，不阻塞基础教学；
- `TUT-MONSTER-*`在首次Possess对应Monster时触发；
- Tutorial Controller贯穿整个Run，不只在`RunPhase.Tutorial`工作。

### 5.2 教学行为

每个Tutorial Step需要配置：

- Step ID；
- 开始条件；
- 完成条件；
- 是否阻塞；
- 提示Text Key；
- Input Action /动态图标；
- 可选Audio ID / Voice Cue；
- 提醒间隔与超时策略；
- 目标失效、死亡、读档和UI冲突兜底；
- 每Profile一次、每Run一次或可重看；
- 下一步。

教学系统必须支持：

- 关闭教学时隐藏表现并立即放行所有阻塞条件；
- 玩家先于提示完成动作时追溯判定完成；
- 完成处理幂等；
- 每Step完成后跨Run持久化；
- 未完成Step下次重新开始，不保存半步状态；
- TUT-06 / 07可跨多个Run等待；
- 首个教学Corpse保护与第二具合法Body供应；
- 按键显示读取实际Input Binding，不在文案中写死WASD / LMB等；
- Monster微教学先显示关键机制，三槽详情可展开；
- 轻量完成反馈；
- 最小出现率、完成率、耗时和跳过率记录；
- 强制Step、完成Step、重置记录和拒绝原因日志等调试能力。

## 6. 统一配置工作流

策划侧期望的工作顺序：

1. 在Text Catalog登记Text Key及Neutral / Mythic / System版本；
2. 在Audio Catalog登记Audio ID与Clip、类别、空间、循环、音量等信息；
3. 在Narrative Cue或Tutorial Step中引用Text Key / Audio ID；
4. 配置Trigger、条件、重复范围、优先级、Access变化和Display变化；
5. 通过统一Debug工具强制触发验证。

推荐配置资产：

- Text Catalog / Terminology Profile；
- Audio Catalog；
- BGM State Profile；
- Narrative Cue Library；
- Narrative Access Profile；
- Tutorial Definition；
- 统一Debug面板。

## 7. 明确不冻结

本轮不冻结：

- 最终旁白条数；
- 最终音频文件数量；
- 最终文案条数；
- A0–A4具体绑定Wave几、Elite第几次或哪一事件；
- Card具体从哪个节点切换System；
- 三条Self-Declaration最终文案；
- Functional Summary精确权重、阈值和句式；
- Model / Version / Instance最终显示格式；
- Shrine、Elite、God / System等最终玩家术语；
- BGM Duck、延迟、冷却等精确参数。

这些内容应由配置、Playable验证和后续Presentation生产收口，不应阻塞底层系统开发。