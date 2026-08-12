# Design Intake

Place new Owner-provided central design drafts in `Docs/00_Project/Source/`. Do not silently overwrite Canonical.

```text
New Central Design + Current Canonical
→ Decision Update Bundle
→ Human Review
→ Canonical Update
→ Open Decisions Update
→ Decision Log Update
→ Commit
```

A Decision Update Bundle must identify:

- Added
- Changed
- Removed
- Conflicts
- Open Decisions
- Files recommended for update

The Owner or corresponding Professional Owner must approve the bundle before an Agent updates Canonical. Protected source documents under `.vibe/doc/` remain read-only unless the Owner explicitly requests their modification.
