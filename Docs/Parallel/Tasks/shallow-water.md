# Shallow water

- Owner: Codex (main, sequential implementation)
- Status: Verified — portable cross-scene water fix (2026-09-10)
- Workspace: C:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01
- Branch: codex/integration
- Base: 77160f46d11c6780484164d3167f5a7140d3135d
- Scope: Assets/Resources/Shader/Water/, Assets/Resources/Materials/Water/, Assets/Resources/Textures/Water/, Assets/Scenes/Test/Water/, Assets/Scripts/Editor/Water/, Assets/Settings/PC_RPAsset.asset (Opaque Downsampling only), Docs/Water.md, Docs/WaterPreview.png and this task.
- Preserve: all pre-existing changes, Materials scene unsaved edits, battle systems, existing WaterShader/WaterMats and texture GUIDs.
- Acceptance: URP 17.3 HLSL shallow water; generated presets and demonstration scene; shader compilation, actual rendered viewpoints, Play Mode and console verification; documented limits.
- Follow-up: four-direction waves, macro-normal refraction, reference presets, and portable cross-scene camera/depth support. Preserve flat Pool preset and unrelated edits.
- Follow-up result: compile/Play/render verified, SRP Batcher code 0; portable prefab and surface-tint fallback added. Computer-use capture failed twice with 0x80004002; used Unity MCP camera captures. Added Docs/WaterReferenceBeach.png and Docs/WaterReferenceLagoon.png to output scope.
- Renderer settings: PC Opaque Downsampling 2x Bilinear -> None for detailed transmission; camera Depth/Opaque overrides for demo. No package or renderer feature changes.
