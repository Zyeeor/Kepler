# Legacy Audit

This deployment creates the audit mechanism only; it does not audit or retire current Demo content.

Audit inputs:

- **Design Truth:** `.vibe/doc/Canonical/00_CANONICAL_INDEX.md` and the task-relevant Canonical documents.
- **Open / Deferred constraints:** `Docs/02_Open_Decisions/`.
- **Repository Fact:** the latest approved Inventory Freeze plus a required Micro Delta Inventory when Repository Fact changed after that freeze.
- **Boundary:** Repository Fact ≠ Design Truth. Modules, CSV files, and old Demo behavior are audit evidence, not current Authority.

Use these classifications:

- `KEEP`: may continue unchanged.
- `REFACTOR`: core value remains but implementation needs change.
- `SALVAGE`: retain selected code, components, assets, or algorithms only.
- `RETIRE`: the approved new design no longer needs it.
- `UNKNOWN`: insufficient evidence; human decision required.

Any Agent may perform a future audit if it can read the repository, current Canonical, Open Decisions, and the standard template. Professional Owner confirmation is mandatory before retiring core gameplay architecture, Scenes, Shared Prefabs, Shared Materials, public APIs, save systems, core state machines, ProjectSettings, or Packages.
