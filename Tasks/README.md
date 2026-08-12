# Tasks

Possession uses a **GitHub Issue + Markdown Contract hybrid mode**.

## Source of Task work

- A GitHub Issue is the human entry point for every Task. Use it for discovery, assignment, discussion, priority, Project status, and daily tracking in the `Possession Development` GitHub Project.
- `Tasks/*.md` is a Detailed Contract for complex Tasks. It provides precise execution context to humans, WorkBuddy, Codex, and other approved Agents.
- Not every Task needs a Markdown Contract. The absence of `Tasks/*.md` is not a blocker for a valid Issue-only Task.

Every Issue should include at least:

- Task ID and Title
- Goal
- Professional Owner
- Executor / Assignee
- Priority
- Acceptance Criteria
- Necessary Dependencies / References

## Issue-only Task

Use only a GitHub Issue when work is low risk, limited to one or very few files, easy to accept, and does not involve core architecture, Canonical, Open Decisions, Shared Originals, high-risk Unity content, or complex cross-role dependencies. Typical examples include a simple bug, tuning, small UI or VFX adjustment, copy, or simple configuration.

If the Issue is sufficient, an Agent must not refuse the Task because no Markdown file exists. If the Issue lacks rules, scope, dependencies, or acceptance criteria required for safe execution, stop and ask for the Issue to be completed instead of guessing.

## Issue + Markdown Contract Task

A GitHub Issue and a linked `Tasks/*.md` Detailed Contract are both required for:

- Core Gameplay or a core state machine
- Work spanning multiple files, modules, or professional roles
- Hard Dependencies
- Canonical, Open Decisions, or Shared Originals
- Scenes, complex Prefabs, ProjectSettings, or Packages
- Public interfaces or other high-risk Unity content
- Work requiring detailed Definition of Ready, Definition of Done, or Test Plan
- Any Task where an Issue alone would leave material execution ambiguity

The Issue must link the repository path of its Detailed Contract, for example `Tasks/Active/ENG-POSS-021.md`. The Contract should link back to the Issue.

## Status and directory mapping

GitHub Project status is authoritative for daily tracking:

```text
Backlog -> Ready -> Doing -> Review -> Done
```

- **Backlog**: recorded but not ready to start. A formal Contract is normally not required; an early Contract remains a draft and must not make an Agent treat the Task as Ready.
- **Ready / Doing**: Detailed Contracts, when required, live in `Tasks/Active/`.
- **Review**: Detailed Contracts, when required, live in `Tasks/Review/`.
- **Done**: Detailed Contracts, when required, live in `Tasks/Done/`.
- **Cancelled / Superseded / Historical**: Detailed Contracts, when retained, live in `Tasks/Archive/`.

Issue-only Tasks remain in GitHub and do not need placeholder Markdown files in these directories.

## Task, Branch, and PR

When a production Task starts, claim the Issue, confirm the Assignee / Executor, move it to `Doing`, and create `work/<TASK-ID>-description` from the latest `main`. Experiments use `exp/<TASK-ID>-description`.

As a default:

```text
one logically complete Task = one primary work Branch = one PR
```

Do not split a coherent Task into multiple Branches or PRs only because its file changes are small. Use the Small template for an Issue-only Task; use the Normal or Core template when a Detailed Contract is required.
