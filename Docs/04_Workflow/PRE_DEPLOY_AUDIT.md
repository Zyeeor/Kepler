# Pre-Deployment Audit

Date: 2026-08-11

## Git

- Repository root: `G:\UnityProjects\Kepler`
- Current baseline branch: `main`
- Baseline commit: `f4f1ddb058923de811f4d25fad278231ecba7bb4`
- Upstream at audit: `origin/main`
- Ahead / behind at audit: `0 / 0`
- Origin: `https://github.com/Zyeeor/Kepler.git`
- Git identity: configured locally
- Git LFS: `3.5.1`
- Existing LFS-tracked files: none
- Existing tags before deployment: none
- Existing baseline branch before deployment: none

One RenderTexture initially appeared modified, but `git diff` was empty and its filtered and raw blob hashes exactly matched the index blob. Refreshing Git index metadata restored a clean status without changing the asset.

## Unity

- Unity version: `2022.3.62f3c1`
- Required roots present: `Assets/`, `Packages/`, `ProjectSettings/`
- Asset serialization: Force Text (`m_SerializationMode: 2`)
- Version control mode: Visible Meta Files
- Tracked files: 2,213
- Tracked `.meta` files: 1,081
- Tracked generated-directory files: none in Library, Temp, Logs, obj, Build, Builds, UserSettings, or MemoryCaptures

## Large files and source art

- Current and historical maximum blob: 32.01 MiB
- Files over GitHub's 100 MiB per-file limit: none found
- Source-art counts: 163 PNG, 116 FBX, 1 EXR, 1 MP4
- Largest files include a 32.01 MiB TextMesh Pro SDF asset, 18.77 MiB font files, and a 17.83 MiB MP4

## Existing workflow

- Existing `AGENTS.md`: yes; contains mandatory `.vibe/rules.md` integration
- Existing `.gitignore`: yes; suitable Unity baseline
- Existing `.gitattributes`: no
- Existing `.github/`: no
- Existing `Docs/`, `Tasks/`, `Templates/`: no
- GitHub CLI: not installed
- Connected GitHub permission: push available; admin and maintain unavailable

## Audit conclusion

The repository is healthy enough for a minimum Agent pipeline deployment. No gameplay, Scene, Prefab, Material, Package, or ProjectSettings modification is required. GitHub administrative configuration and active LFS rules require separate human review.
