# Minimum Agent Pipeline Deployment Report

Date: 2026-08-11

## A. Completed

- Audited the local Git repository, remote, Unity project structure, workflow files, large objects, and LFS state.
- Created and remotely verified `legacy-demo-baseline-20260811`.
- Created and remotely verified `backup/legacy-demo-20260811`.
- Confirmed the baseline tag, backup branch, local `main`, and remote `main` point to `f4f1ddb058923de811f4d25fad278231ecba7bb4` at deployment start.
- Preserved the existing `.vibe/rules.md`-based `AGENTS.md` and appended Possession workflow, authority, scope, ownership, Shared Original, and completion rules.
- Created the standard Docs, Tasks, and Templates structure.
- Added audit, LFS recommendation, branch rules, GitHub checklist, WorkBuddy onboarding, Agent start prompt, Design Intake, Legacy Audit, PM, review, handoff, and integration materials.
- Created deployment branch `work/INFRA-agent-pipeline-v1`.
- Verified Unity settings are Force Text and Visible Meta Files without editing ProjectSettings.
- Opened and exited the project successfully in Unity `2022.3.62f3c1` BatchMode.
- Verified no gameplay, Scene, Prefab, Material, Package, or ProjectSettings file is included in the deployment diff.

## B. Not completed by design

- No central design draft, formal gameplay Canonical, final Open Decisions set, Legacy Audit, migration plan, final PM, or production gameplay Task was generated.
- No GitHub administrative setting was changed.
- No active `.gitattributes` LFS rules or LFS history migration was applied.
- No merge to `main` was performed.

## C. Owner manual actions

- Review `GITHUB_MANUAL_SETUP_CHECKLIST.md` with a repository administrator.
- Review the deployment branch and use `DEPLOYMENT_PR_SUMMARY.md` when opening a PR.
- Decide in a separate Task whether future binary assets should use Git LFS.
- Have the appropriate Professional Owners review workflow terminology and templates before team-wide rollout.

## D. Risks and observations

- The repository is public; confirm this is intentional.
- The connected account can push but does not have admin or maintain permission, so branch protection and collaborator settings were not verifiable or editable through this deployment.
- Existing large binary assets are stored in normal Git. The maximum current and historical blob is 32.01 MiB; no blob exceeds GitHub's 100 MiB limit.
- Enabling broad FBX or MP4 LFS patterns now could affect already tracked assets. A separate human-reviewed adoption Task is safer.
- Unity compiled successfully. BatchMode logged recoverable license-handshake retries, expected Unity MCP connection warnings because its service was not running, and an existing Cinemachine sample asmref warning. No compiler error, unhandled exception, or fatal error was found.

## E. Git status

```text
Current Branch: work/INFRA-agent-pipeline-v1
Deployment Base Commit: f4f1ddb058923de811f4d25fad278231ecba7bb4
origin: https://github.com/Zyeeor/Kepler.git
Baseline Tag: legacy-demo-baseline-20260811 -> f4f1ddb058923de811f4d25fad278231ecba7bb4
Backup Branch: backup/legacy-demo-20260811 -> f4f1ddb058923de811f4d25fad278231ecba7bb4
Git LFS: 3.5.1 installed; no tracked files; no active rules
Deployment Diff: AGENTS.md addendum plus Docs/, Tasks/, and Templates/ only
```

The deployment commit containing this report uses message `chore: bootstrap minimum agent development pipeline`. Its exact SHA is the remote head of `work/INFRA-agent-pipeline-v1` after push.

## F. Minimum Agent Pipeline conclusion

`PASS`

The repository now defines where source inputs, Canonical, Open Decisions, Tasks, workflow rules, audits, PM inputs, and Agent prompts belong. Team members and supported Agents can follow a consistent Task-to-review path, while the old Demo has a remotely verified recovery baseline. Remaining GitHub administration and LFS adoption are explicit human decisions rather than deployment failures.

## G. Post-deployment Task workflow update

On 2026-08-11, the Owner confirmed that the `Possession Development` GitHub Project was created under `Zyeeor`, uses `Zyeeor/Kepler` as its default repository, and exposes `Backlog`, `Ready`, `Doing`, `Review`, and `Done` statuses. The connected collaborator can open and edit the Project.

The repository workflow documentation was subsequently aligned to a GitHub Issue + Markdown Contract hybrid mode:

- Every Task uses a GitHub Issue as the human entry point.
- Low-risk, well-specified work may remain Issue-only.
- Complex or high-risk work additionally requires a linked Detailed Contract under `Tasks/`.
- GitHub Project status controls daily tracking; Contract directories map to the relevant lifecycle stages when a Contract exists.

This section records a later workflow clarification and does not rewrite the original deployment facts. Branch protection, required PR rules, collaborator administration, LFS adoption, Canonical content, and Unity business content remain unchanged.
## H. Team-Facing Summary / 普通成员需要知道什么

本次部署为《Possession》增加了最小 Agent 开发流水线和团队协作规则。

普通成员日常只需要理解：

1. `Possession Development` 是团队任务总看板；
2. GitHub Issue 是所有正式 Task 的人类主入口；
3. 简单、低风险 Task 可以 Issue-only；
4. 复杂或高风险 Task 使用 Issue + `Tasks/*.md` Detailed Contract；
5. 正式 Task 从最新 `main` 创建独立 Branch；
6. Branch 命名以 `work/<TASK-ID>-description` 为主；
7. 一个逻辑完整 Task 原则上对应一个主要 Branch 和一个 PR；
8. 完成后通过 PR 进入 Review，不直接修改或自行 Merge `main`；
9. Professional Owner 负责专业 Review；
10. Owner / Integration Owner 负责最终 Merge；
11. WorkBuddy、Codex 等 Agent 统一从 `AGENTS.md` 进入项目规则；
12. 团队使用指南为 `Docs/04_Workflow/TEAM_QUICKSTART.md` 和 `Docs/04_Workflow/TEAM_WORKFLOW_GUIDE.md`。

Project 状态统一为：

```text
Backlog -> Ready -> Doing -> Review -> Done
```

Priority 已由 Owner 于 2026-08-12 确认为保存成功的 Single select 字段，正式选项为 P0 / P1 / P2 / P3。

需要注意：

- 团队规范和 GitHub 管理员级强制设置是两回事；Branch Protection、Required Review、Actions 等只有在管理员实际启用后才能写成 GitHub 已强制；
- Git LFS 尚未正式启用广泛规则，也没有执行历史迁移；
- Legacy Audit 机制已准备，不代表旧 Demo 审计已经完成；
- Canonical 不能由 Agent 从旧 Demo 自行推断；
- 本摘要所在文档变更当前仍位于 `work/INFRA-agent-pipeline-v1`，等待 Owner Review，尚未 Merge 到 `main`。
