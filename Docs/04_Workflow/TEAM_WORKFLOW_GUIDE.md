# 《Possession》团队开发工作流使用指南

> 文件定位：团队正式使用指南。
> 读者：策划、客户端、美术、Owner、Integration Owner，以及WorkBuddy / Codex等Agent。
> 语言：简体中文为主；Git / GitHub / Unity术语、Task ID、字段名保留英文。
> 本文件负责解释“怎么使用”，不取代底层规则真源。

---

# 0. 规则真源与优先级

## 0.1 Agent行为规则

```text
.vibe/rules.md
>
AGENTS.md
>
对应Workflow / Task / Template
>
Agent自行推断
```

`.vibe/rules.md` 是Agent行为最高约束；`AGENTS.md` 是项目级Agent入口和导航。

## 0.2 设计事实优先级

```text
Approved Canonical
>
Open Decisions 对当前选择范围的约束
>
Current Task
>
Existing implementation
>
Agent inference
```

注意：

- Open Decision表示未决问题，不是已批准规则；
- Task不得擅自覆盖Canonical；
- Workflow是操作规范，不属于玩法设计事实层级；
- 旧Demo实现不能自动被当成新版本正式设计。

---

# 1. 这套工作流解决什么问题？

《Possession》当前采用多人 + AI Agent协作。

这套流程主要解决：

- 任务靠口头传递导致信息丢失；
- 每个人的AI拿到不同上下文；
- 多人直接修改同一正式版本；
- Unity Shared Asset误改；
- 做完后没有专业Review；
- 旧Demo实现被误认为新设计；
- Owner无法快速知道当前进度和风险。

第一阶段遵循：

> Minimum Agent Pipeline。

如果流程本身明显比实际工作更麻烦，应继续简化，而不是堆更多制度。

---

# 2. 核心结构

```text
GitHub Project
= 团队任务总看板

GitHub Issue
= 所有正式Task的人类主入口

Tasks/*.md
= 复杂/高风险Task的详细执行合同

Branch
= Task自己的施工区

Pull Request
= 请求把成果合入main

Professional Owner
= 专业正确性负责人

Integration Owner
= 最终集成与Merge负责人
```

---

# 3. GitHub Project

项目：

```text
Possession Development
```

关联仓库：

```text
Zyeeor/Kepler
```

状态：

```text
Backlog
Ready
Doing
Review
Done
```

Priority正式采用：

```text
P0 = 当前Build Goal必须完成 / 阻塞关键路径
P1 = 本阶段应该完成
P2 = 有时间完成，必要时可砍
P3 = Backlog / 暂不排期
```

---

# 4. Task状态

## Backlog

已经记录，但尚未满足开工条件。

## Ready

满足开工条件，可以领取。进入Ready前至少应知道：Task ID、Goal、Professional Owner、Acceptance Criteria、关键Dependency，以及是否需要Markdown Contract。

## Doing

已有Executor并正在执行。

## Review

Executor认为实现完成，已经提交PR，等待Professional Review或Integration检查。

## Done

完成验收并正式结束。

---

# 5. GitHub Issue与Tasks/*.md的分工

## 5.1 GitHub Issue

所有正式Task的人类主入口，主要承担：

- Task ID；
- Title；
- Goal；
- Assignee；
- Professional Owner；
- Priority；
- Project Status；
- Acceptance Criteria；
- Dependency；
- 讨论；
- PR关联；
- 日常进度查看。

## 5.2 Issue-only Task

以下任务通常可以只使用Issue：

- 低风险；
- 单文件或极少文件；
- 验收简单；
- 简单Bug；
- 调参；
- 小UI；
- 小VFX；
- 文案；
- 简单配置；
- 不涉及核心架构、Canonical、Open Decision、Shared Original、Scene、ProjectSettings、Packages或复杂跨角色依赖。

Issue信息不足时，不能因为是“小Task”就允许AI猜需求。

## 5.3 Issue + Markdown Contract

以下任务应使用：

```text
Issue + Tasks/*.md
```

包括但不限于：Core Gameplay、跨多个模块、跨角色、Hard Dependency、Canonical、Open Decision、Shared Original、Scene、复杂Prefab、ProjectSettings、Packages、核心状态机、公共接口、高风险Unity内容、需要详细DoR / DoD / Test Plan、AI只看Issue容易产生歧义。

Issue应链接对应Contract。

---

# 6. Markdown Contract目录

```text
Ready / Doing → Tasks/Active/
Review        → Tasks/Review/
Done          → Tasks/Done/
取消/替代     → Tasks/Archive/
```

Backlog原则上不需要提前建立正式Contract。如提前存在草案，不得让Agent因为看到文件就误认为已经Ready。

---

# 7. Professional Owner与Executor

## Professional Owner

对专业正确性负责。

例如：DES Task由对应设计Owner负责，ENG Task由对应客户端Owner负责，ART Task由对应美术Owner负责。

职责包括：判断方案是否专业可接受、判断高风险变更、Review成果、对专业范围内的最终结论负责。

## Executor

实际执行Task的人，可以和Professional Owner是同一个人，也可以不同。

例如策划可以帮助执行简单ENG Task：

```text
Executor = 策划
Professional Owner = 客户端
```

策划可以执行，但程序仍负责最终技术Review。

---

# 8. Branch规则

底层Branch规则以：

```text
Docs/04_Workflow/BRANCH_RULES.md
```

为准。

常用命名：

```text
main
work/<TASK-ID>-description
exp/<TASK-ID>-description
```

不使用长期 `develop` 分支。

---

# 9. 正式Task什么时候建Branch？

```text
Task Ready
↓
领取Issue
↓
明确Assignee / Executor
↓
Doing
↓
main Pull最新
↓
创建Task Branch
↓
开始执行
```

正式Task不应长期直接在main开发。

---

# 10. 一个Task和Branch / PR的关系

原则：

> 一个逻辑完整Task = 一个主要Branch = 一个PR。

一个Task可以同时修改Script、ScriptableObject、Prefab、Test Scene和配置，不需要因为文件多就拆成很多Branch。

---

# 11. Branch在Unity中的实际表现

切换Branch后，Git会把本地工作目录切换到该Branch对应的文件状态，因此Unity会真实受到影响。

例如：

```text
main = 正式版本
work/ENG-POSS-021 = 怪物Dash半成品
work/DES-POSS-031 = Wave配置调整
```

切到 `work/ENG-POSS-021` 时，Unity运行的就是包含该Branch修改的版本。切到另一个Branch后，尚未Merge到main的修改通常不会出现在当前工程状态。

---

# 12. 切Branch前必须注意什么？

建议：

1. 查看GitHub Desktop Changes；
2. 确认当前修改属于哪个Task；
3. 需要保留时Commit；
4. 半成品可做WIP Commit；
5. 再切Branch。

示例：

```text
WIP: ENG-POSS-021 monster dash in progress
```

---

# 13. GitHub Desktop最基础操作

普通成员第一阶段只需要会：

```text
Pull
Branch
Commit
Push
Create Pull Request
回main再Pull
```

常用顺序：

```text
Current Branch → main
Fetch origin
Pull origin

Current Branch → New Branch
work/<TASK-ID>-description

开发

Changes
→ 检查文件
→ Summary
→ Commit

Push origin

Preview Pull Request / Create Pull Request
```

---

# 14. 开始Task时Agent应该做什么？

成员通常输入：

```text
开始 <TASK-ID>
```

Agent不应立刻修改文件，应先读取：

1. `AGENTS.md`
2. `.vibe/rules.md`
3. GitHub Issue
4. 如果有Detailed Contract，则读取对应 `Tasks/*.md`
5. Parent Feature（如存在）
6. 相关Canonical
7. Open Decisions
8. 当前实现
9. 对应Workflow / Template

然后先报告：

```text
Goal
Scope
Out of Scope
Files
Plan
Test Plan
Risks
Requirement Conflicts
Shared Asset Risk
```

---

# 15. Issue-only时Agent怎么办？

没有Markdown并不代表Task非法。

```text
找到Issue
↓
确认这是Issue-only Task
↓
检查Issue是否足够执行
↓
足够 → 正常执行
不足 → 停止并要求补充
```

禁止因为找不到 `Tasks/*.md` 就拒绝Issue-only Task，也禁止因为信息不足就自己补玩法规则。

---

# 16. WorkBuddy如何接入？

团队默认Agent客户端为WorkBuddy；Owner可额外使用Codex。

建议WorkBuddy Project Instruction至少明确：

```text
开始任何Possession项目任务前：
1. 先读取仓库根目录AGENTS.md；
2. 按AGENTS.md导航读取对应Workflow和Task规则；
3. 在修改前先报告Scope、Files、Plan、Test和Risks；
4. 不自行覆盖Canonical；
5. 遇到不明确需求先询问，不自行猜测。
```

实际使用前应做一次真实验收，确认WorkBuddy确实按规则读取。

如果WorkBuddy无法直接访问GitHub Issue，应提供Issue链接或Issue正文，AI不能自行猜Task。

---

# 17. Commit规则

Commit是可恢复的工作节点。

建议：

- 一个逻辑完整的小阶段一个Commit；
- 不需要每改一行就Commit；
- 也不建议整个Task做几天后只留一个巨大Commit；
- 切Branch前的半成品允许WIP Commit。

Commit前检查：只包含本Task文件，没有误选临时文件、未知大文件或无关改动。

---

# 18. Push

Push把本地Branch同步到GitHub。

Push不是Merge。Push后别人可以Review，但仍不会自动进入main。

---

# 19. Pull Request

PR表示：

> “这张Task已经达到提交Review条件，请检查并决定是否进入main。”

PR建议至少说明：

```text
Goal
Changes
Files
Test
Risks
Known Issues
Related Task
```

Task状态：

```text
Doing → Review
```

---

# 20. Completion Check

开PR前让Agent做一次自检，至少检查：Acceptance Criteria、Scope、Git Diff、Console Error、Missing Reference、Shared Original、Scene、ProjectSettings、Packages、大文件、测试结果、已知问题。

AI自检不能替代人类Review。

---

# 21. Professional Review

Review重点不是“有没有写出来”，而是是否以专业上可接受的方式完成Task。

### ENG

关注架构、公共接口、Unity引用、性能、状态机等。

### ART

关注视觉目标、Shared Asset、导入设置等。

### DES

关注Canonical、规则、数值目标和新的设计冲突。

---

# 22. Integration Owner

Integration Owner负责：查看PR状态、检查集成风险、必要时本地Checkout验证、判断Merge顺序、解决或协调Conflict、最终Merge、确保main健康。

第一阶段：

> 普通成员不自行Merge自己的PR。

Agent只有获得当前会话明确授权时才能Commit / Push；最终Merge由人执行。

---

# 23. GitHub强制设置 vs 团队政策

团队政策包括“不直接开发main”“普通成员不自行Merge”“必须Review”“Integration Owner负责最终Merge”。

GitHub管理员级强制设置包括Branch Protection、Required Pull Request、Required Reviews、Code Owners、Required Checks、GitHub Actions。

如果管理员设置还没启用，团队政策仍然有效。

---

# 24. 美术工作流

```text
领ART Issue
→ Pull main
→ 创建ART Branch
→ WorkBuddy读Task和规则
→ 检查文件/Shared风险
→ 制作
→ Agent检查Git变化
→ Commit
→ Push
→ PR
→ 专业Review
→ Integration Merge
```

美术第一阶段不需要学高级Git。特别注意Shared Prefab、Shared Material、Animator、VFX、Source Asset、`.meta` 和引用关系。

---

# 25. 客户端工作流

```text
领ENG Issue
→ 看Contract
→ Pull main
→ Branch
→ Agent预检查
→ 实现
→ Unity验证
→ 自检
→ Commit
→ Push
→ PR
→ Code/Architecture Review
→ Integration
```

高风险内容包括核心状态机、Save、Resource架构、公共API、Scene、ProjectSettings、Packages、Shared Prefab、Unity YAML Conflict。

---

# 26. 策划工作流

普通DES Task：

```text
Issue
→ Branch
→ WorkBuddy
→ 修改
→ Review
→ PR
```

正式设计变更不应直接把Task描述当成新的Canonical，应按：

```text
Design Source
→ Design Intake
→ Decision Update Bundle
→ Owner / Professional Owner确认
→ Canonical / Open Decisions / Decision Log更新
```

具体以 `Docs/04_Workflow/DESIGN_INTAKE.md` 为准。

---

# 27. 跨岗协作

小团队允许跨岗执行，但必须区分：

```text
Professional Owner ≠ Executor
```

策划可以帮助执行参数、配置、ScriptableObject、简单UI逻辑、测试场景、简单组件、数据接线等。

不适合无边界跨岗接手核心状态机、Save、网络、公共API、核心战斗框架、ProjectSettings、Packages等高风险任务。

---

# 28. Shared Original

Unity里的Shared Original属于重点风险。

局部修改前要判断：引用范围、是否全局改、是否应该做Variant、是否应该Independent Copy、是否需要Lock / Ownership协调。

具体规则以仓库现有Shared Original规范为准。

---

# 29. 常见情况

## 29.1 在main上误改了

停止继续扩大修改，不要乱Reset / Clean。先让WorkBuddy / 客户端检查 `git status` 和Diff，再判断如何安全迁移到Task Branch。

## 29.2 忘记Pull就开Branch

检查当前Branch、本地修改、main与origin/main差异。不要强行覆盖，由Agent或客户端判断后续处理。

## 29.3 Task做到一半要去做另一个Task

```text
当前Task
→ Commit / WIP Commit
→ 切main
→ Pull
→ 创建另一个Task Branch
```

原Branch内容不会因为切走就丢失。

## 29.4 两个人要改同一个Prefab

先协调Ownership，尽量不要同时直接修改同一个高风险Shared Prefab。必要时拆Variant、独立组件、独立Prefab或明确Integration顺序。

## 29.5 PR Review发现问题

通常继续在原Task Branch修：

```text
修改 → Commit → Push
```

PR会自动更新。

## 29.6 Merge后发现Bug

创建新的Bug Issue，不要偷偷在main上直接修。

---

# 30. LFS当前注意事项

当前不应假设Git LFS已经正式启用或完成历史迁移。

遇到超大PSD、FBX、大型音频或其他大型二进制资产时，先按仓库当前LFS建议和客户端安排处理，不要自行执行历史LFS迁移。

---

# 31. Canonical当前注意事项

Canonical v1.1 已正式导入并作为当前 Design Truth。

旧 Demo、CSV 与 Modules 是 Historical / Implementation Reference，不得覆盖 Canonical。Agent 不得从旧实现反推并静默创建新的正式玩法 Canonical。

---

# 32. Legacy Audit

Legacy Audit机制和模板存在，不代表旧Demo审计已经完成。

当前后续顺序：

```text
Authority Sync
→ Review / Merge
→ Micro Delta Inventory（如 Repository Fact 有变化）
→ Legacy Audit
→ Keep / Refactor / Salvage / Retire / Unknown
→ 人工确认
→ Migration
→ 正式PM
```

---

# 33. 一张完整流程图

```text
            Owner / System Owner / PM
                      ↓
                创建 GitHub Issue
                      ↓
           Possession Development
                      ↓
        Backlog → Ready → Doing
                      ↓
                Executor领取
                      ↓
                 main Pull
                      ↓
              Task Branch
                      ↓
         WorkBuddy读取规则/Task
                      ↓
           报告Scope/Plan/Risks
                      ↓
               人 + AI开发
                      ↓
             Completion Check
                      ↓
              Commit + Push
                      ↓
                     PR
                      ↓
                   Review
                      ↓
             Professional Owner
                      ↓
              Integration Owner
                      ↓
                    Merge
                      ↓
                    main
                      ↓
                    Done
```

---

# 34. 团队成员实际只需要先学什么？

第一周只需要掌握：

```text
Project
Issue
Pull
Branch
WorkBuddy
Commit
Push
PR
Review
Merge
```

复杂Git问题交给客户端 / Integration Owner + Agent协助。

---

# 35. AI不知道规则怎么办？

统一问：

```text
先读取AGENTS.md和对应Workflow。
不要修改文件。
告诉我当前Task应该按什么流程执行，并给出依据。
```

如果AI回答和仓库规则冲突，以仓库正式规则优先。

---

# 36. 本指南与底层文件的关系

本指南负责“人怎么用”。

底层真源包括：

```text
.vibe/rules.md
AGENTS.md
Tasks/README.md
Templates/
Docs/04_Workflow/BRANCH_RULES.md
Docs/04_Workflow/DESIGN_INTAKE.md
Docs/04_Workflow/WORKBUDDY_ONBOARDING.md
Docs/04_Workflow/AGENT_TASK_START_PROMPT.md
Templates/INTEGRATION_CHECKLIST.md
```

修改底层规则时，应先修改对应真源，再同步本指南。不要只改本指南来改变正式规则。
