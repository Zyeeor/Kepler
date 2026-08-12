# Branch Rules

Use a simple first-stage model:

```text
main
work/<TASK-ID>-description
exp/<TASK-ID>-description
```

## `main`

Keep `main` runnable or close to runnable. Do not use it for long-running ordinary development. Final merge authority belongs to the Owner or Integration Owner.

## `work/`

Use for production Tasks, for example `work/ENG-POSS-021-monster-ability`.

Before work starts:

1. Claim the GitHub Issue.
2. Confirm the Assignee / Executor.
3. Move Project status to `Doing`.
4. Create `work/<TASK-ID>-description` from the latest `main`.

As a default, one logically complete Task uses one primary work Branch and one PR. Do not create multiple Branches or PRs only because a coherent Task has several small file changes.

## `exp/`

Use for experiments and spikes, for example `exp/DES-POSS-005-bullet-time-test`. Before retaining experiment work in production, record the approved Design Decision and create a production Task.

Do not introduce a long-lived `develop` branch or complex Git Flow during this stage. Do not rewrite shared history or force-push protected branches.
