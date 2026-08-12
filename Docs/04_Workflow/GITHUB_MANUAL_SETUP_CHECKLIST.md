# GitHub Manual Setup Checklist

Repository: `Zyeeor/Kepler`

Observed on 2026-08-11:

- Visibility: public
- Default branch: `main`
- Connected account: pull, triage, and push permissions available
- Connected account: admin and maintain permissions unavailable
- `.github/` workflows: none in the audited commit
- GitHub CLI: not installed locally

Owner-confirmed Project state on 2026-08-11:

- [x] GitHub Issues are the human entry point for all formal Tasks.
- [x] GitHub Project `Possession Development` exists under Owner `Zyeeor`.
- [x] Default repository is `Zyeeor/Kepler`.
- [x] Status values are `Backlog`, `Ready`, `Doing`, `Review`, and `Done`.
- [x] Collaborator `RangoSong-13` has Write access to the Project and can open and edit it.
- [x] `Priority` is a saved Single select field with the Owner-confirmed options:
  - `P0`: 当前 Build Goal 必须完成 / 阻塞关键路径
  - `P1`: 本阶段应该完成
  - `P2`: 有时间完成，必要时可砍
  - `P3`: Backlog / 暂不排期

An administrator should still review and record:

- [ ] Protect `main` against force pushes and deletion.
- [ ] Require pull requests before merging to `main`.
- [ ] Decide required approval count and code-owner policy.
- [ ] Decide whether branch freshness or linear history is required.
- [ ] Decide whether GitHub Actions checks are needed before merge.
- [ ] Review collaborator roles using least privilege.
- [ ] Confirm repository visibility is intentional.
- [ ] Review Git LFS storage and bandwidth before activating broad patterns.
- [ ] Confirm baseline tag `legacy-demo-baseline-20260811` is protected from accidental deletion by team policy.

This workflow update records Owner-confirmed Project state only. It does not modify repository permissions, visibility, collaborators, protection rules, or Actions settings.
