# Git LFS Recommendation

Date: 2026-08-11

## Current state

- Git LFS 3.5.1 is installed.
- No `.gitattributes` exists and no files are tracked by LFS.
- The largest current and historical blob is 32.01 MiB.
- No blob exceeds GitHub's 100 MiB hard limit.

## Recommended LFS candidates for future binary assets

```text
*.psd
*.psb
*.fbx
*.blend
*.wav
*.mp3
*.mp4
*.mov
```

Consider PNG, JPG, JPEG, TGA, EXR, OTF, and TTF only after measuring expected churn and repository growth. Do not blanket-track them without Owner review.

## Do not place in LFS by default

```text
*.unity
*.prefab
*.asset
*.meta
*.cs
*.asmdef
*.json
*.md
*.yaml
*.yml
```

These are normally diffable Unity or repository text files. The 32.01 MiB SDF `.asset` remains regular Git content because the extension is broadly used for text-serialized Unity assets.

## Deployment decision

No active LFS patterns were added automatically. The repository already contains 116 FBX files and one MP4 in normal Git; enabling broad patterns now would change clean-filter behavior for existing assets and could cause accidental normalization. Adopt rules in a dedicated, human-reviewed Task. Do not run `git lfs migrate import` without explicit Owner approval; no history migration is currently recommended.
