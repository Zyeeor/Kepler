# GitHub Manual Setup Checklist

Repository: `Zyeeor/Kepler`

Observed on 2026-08-11:

- Visibility: public
- Default branch: `main`
- Connected account: pull, triage, and push permissions available
- Connected account: admin and maintain permissions unavailable
- `.github/` workflows: none in the audited commit
- GitHub CLI: not installed locally

An administrator should review and record:

- [ ] Protect `main` against force pushes and deletion.
- [ ] Require pull requests before merging to `main`.
- [ ] Decide required approval count and code-owner policy.
- [ ] Decide whether branch freshness or linear history is required.
- [ ] Confirm GitHub Issues are enabled and define their use.
- [ ] Decide whether GitHub Projects is needed for team planning.
- [ ] Decide whether GitHub Actions checks are needed before merge.
- [ ] Review collaborator roles using least privilege.
- [ ] Confirm repository visibility is intentional.
- [ ] Review Git LFS storage and bandwidth before activating broad patterns.
- [ ] Confirm baseline tag `legacy-demo-baseline-20260811` is protected from accidental deletion by team policy.

This deployment does not modify repository permissions, visibility, collaborators, protection rules, or Actions settings.
