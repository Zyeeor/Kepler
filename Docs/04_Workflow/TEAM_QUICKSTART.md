# 《Possession》团队开发流程｜5–10分钟快速上手

> 文件定位：新人第一次开工前必看。
> 完整说明：`Docs/04_Workflow/TEAM_WORKFLOW_GUIDE.md`。
> 如果本文与仓库规则冲突，以 `.vibe/rules.md`、`AGENTS.md`、`Tasks/README.md` 和对应 Workflow 为准。
> 包含本文的变更 Merge 到 `main` 前仅供 Review，不代表流程已经正式生效。

## 1. 只记住这一条主流程

```text
Possession Development 看板
→ 领取 Ready Issue，并设为 Doing
→ Pull 最新 main
→ 创建 Task Branch
→ 人 + WorkBuddy 开发
→ 自检
→ Commit
→ Push
→ Pull Request
→ Review
→ Integration Owner Merge
→ Done
```

日常正式开发不要直接在 `main` 上进行。

## 2. Task 在哪里？

```text
GitHub
→ Zyeeor/Kepler
→ Possession Development
→ 找到自己的 Issue
```

GitHub Issue 是所有正式 Task 的人类主入口，用于查看 Goal、Assignee、Priority、Acceptance Criteria、讨论和状态。

Project 状态固定为：

```text
Backlog → Ready → Doing → Review → Done
```

- **Backlog**：已记录，但还不能开工。
- **Ready**：信息和依赖已满足，可以领取。
- **Doing**：已有 Executor，正在执行。
- **Review**：开发完成，等待专业 Review 或集成检查。
- **Done**：完成验收。

Priority 正式采用：

- **P0**：当前 Build Goal 必须完成 / 阻塞关键路径
- **P1**：本阶段应该完成
- **P2**：有时间完成，必要时可砍
- **P3**：Backlog / 暂不排期

Priority 由排期和关键路径决定，不按个人着急程度设置。

## 3. Issue-only 还是需要 Markdown？

```text
简单、低风险 Task = Issue-only
复杂、高风险 Task = Issue + Tasks/*.md Detailed Contract
```

通常可 Issue-only：简单 Bug、调参、小 UI、小 VFX、文案、简单配置，以及少量文件且验收明确的低风险修改。

以下情况必须有 Detailed Contract：Core Gameplay、跨模块或跨角色、Hard Dependency、Canonical、Open Decision、Shared Original、Scene、复杂 Prefab、ProjectSettings、Packages、核心状态机、公共接口或其他高风险 Unity 内容。

Issue 会链接类似路径：

```text
Tasks/Active/ENG-POSS-021.md
```

没有 Markdown 不代表 Task 非法；但 Issue 信息不足时必须先补充，不允许人或 Agent 自行猜规则。

## 4. 正式开始一个 Task

### 在 GitHub Project

1. 只领取 `Ready` Issue。
2. 确认 Task ID、Goal、Professional Owner、Acceptance Criteria、Dependency 和 Priority。
3. 将 Assignee / Executor 设为自己。
4. 把状态改为 `Doing`。

### 在 GitHub Desktop

```text
Current Branch → main
Fetch origin
Pull origin
Current Branch → New Branch
work/<TASK-ID>-description
```

例如：

```text
work/ENG-POSS-021-monster-ability
work/ART-POSS-008-possession-vfx
```

实验使用：

```text
exp/<TASK-ID>-description
```

原则：

> 一个逻辑完整 Task = 一个主要 Branch = 一个 PR。

切换 Branch 会改变本地 Unity 工程所显示的文件状态。切换前先确认当前修改已经 Commit，或已被 Git 安全保留。

## 5. 怎么让 WorkBuddy / Codex 开始？

统一输入：

```text
开始 <TASK-ID>
```

Agent 应依次读取：

1. `AGENTS.md`
2. `.vibe/rules.md`
3. GitHub Issue
4. Issue 链接的 Detailed Contract（如有）
5. 相关 Canonical、Open Decisions 和当前实现

修改前，Agent 必须先报告：

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

如果 Agent 无法读取 Issue，把 Issue 链接或正文交给它。信息不足时停止并补充，不允许 Agent 自行设计缺失规则。

## 6. 做完后怎么办？

先完成 Completion Check：

- Acceptance Criteria 是否满足；
- 是否超出 Task Scope；
- Git Diff 是否只有本 Task 内容；
- 是否新增 Console Error、Missing Script 或 Missing Reference；
- 是否误改 Shared Original；
- 是否改动 Scene、ProjectSettings、Packages 或异常大文件；
- 要求的测试是否完成。

然后：

```text
Commit → Push origin → Create Pull Request
Doing → Review
```

- **Commit**：本地存档点。
- **Push**：把 Branch 和 Commit 同步到 GitHub，不等于 Merge。
- **PR**：请求 Review 并考虑合入 `main`。

如果由 Agent 代为 Commit / Push，必须在当前会话明确授权。Agent 不得自行 Merge；最终 Merge 由人执行。

## 7. 谁负责 Review 和 Merge？

- **Executor / Assignee**：实际执行 Task。
- **Professional Owner**：负责专业正确性和专业 Review。
- **Integration Owner**：负责集成风险、Merge 顺序和最终 Merge。

Executor 可以跨岗，但不会因此获得其他专业的最终决定权。普通成员不要自行 Merge 自己的 PR。

GitHub Branch Protection、Required Review、Actions 等管理员强制设置即使尚未全部开启，团队规则仍然有效。

## 8. Unity 与设计红线

- 不直接在 `main` 上长期开发正式 Task。
- Shared Prefab、Material、ScriptableObject 等修改前先确认影响范围。
- Scene、复杂 Prefab、ProjectSettings、Packages 属于高风险内容。
- Unity YAML 冲突不得自动选择 `ours` 或 `theirs`。
- 不执行 `git reset --hard`、`git clean -fdx` 或 force push。
- Task 不得擅自覆盖 Canonical。
- 旧 Demo 实现不能自动当作新版本正式设计。
- LFS 未正式启用前，不自行迁移历史或扩大规则。

## 9. 各岗位第一次只注意什么？

- **ENG**：公共接口、状态机、性能、Unity 引用和高风险配置。
- **ART**：Shared Asset、Prefab / Material、导入设置、`.meta` 和引用关系。
- **DES**：Canonical、Open Decision、数值目标和设计冲突。

遇到 Conflict、LFS、Merge 异常或 Shared Original 风险，停止扩大修改并找 Professional Owner / Integration Owner 协助。

## 10. 不知道下一步时

可以对 Agent 说：

```text
读取 AGENTS.md 和团队开发规范。
我正在处理 <TASK-ID>。
告诉我下一步应该做什么，不要直接修改文件。
```

或：

```text
判断这个 Task 是否需要 Markdown Contract，
给出仓库规则依据，不要猜测。
```

需要完整岗位流程、GitHub Desktop 操作、Review、Shared Original、Design Intake、Legacy Audit 或 LFS 说明时，继续阅读：

```text
Docs/04_Workflow/TEAM_WORKFLOW_GUIDE.md
```
