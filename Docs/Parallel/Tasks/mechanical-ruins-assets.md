# Mechanical ruins surface assets

- Status: Review — asset files prepared; Unity import/render verification pending because another operation holds the Unity lease.
- Owner: Codex main, original workspace, codex/integration
- Base: 77160f46d11c6780484164d3167f5a7140d3135d
- Allowed: Assets/Environment/MechanicalRuins/ and meta; Docs/MechanicalRuinsAssets.md; this task.
- Preserve: all scenes, water placement/settings, existing materials, gameplay and other work.
- Deliver: generated floor texture, transparent moss decal and grass cutout, Unity materials and placement prefabs, usage guide and visual/import validation.
- Delivered: 3 PNG textures, 3 URP Lit materials, floor/moss/grass prefabs, generation prompts and usage guide. Parent folder metas included as required for stable Unity asset paths.
- Verified: image visual inspection; floor RGB and vegetation RGBA; 1254x1254; transparent corner alpha 0; prefab internal references and all material/texture GUID links.
- Unexecuted: Unity import/render, Play Mode and battle tests. No C# or shader source changed, no scene or water edit, no lease taken/released by this task.
