# Stylized background

- Status: Verified — actual chessboard material applied
- Owner: Codex (main, sequential implementation)
- Workspace: C:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01
- Branch: codex/integration
- Base: 77160f46d11c6780484164d3167f5a7140d3135d
- Scope: Assets/Environment/StylizedBackground/ and .meta; Assets/Scripts/Editor/StylizedBackground/ and .meta; Docs/StylizedBackground.md; Docs/StylizedBackgroundPreview.png; this task; BOARD.md task entry.
- Goal: URP background material with soft stylized lighting, cool shadows, texture detail, restrained specular and adjustable distance haze; reusable material presets and an isolated preview prefab.
- Preserve: existing scene edits, water resources, mechanical ruins originals, gameplay and project settings.
- Verification: Unity shader/C# compile, rendered preview, temporary prefab in active BattleScene Play Mode, console inspection, exit Play Mode and remove only temporary preview objects. No battle-rule changes.
- Result: 7 materials, reusable prefab, isolated preview scene and ACES profile; shader messages 0, SRP Batcher code 0; BattleScene and preview Play rendering verified including runtime haze/cutout changes. Play exited and BattleScene restored.
- Existing tests: 109 completed, 1 reported Poison failure (expected 5, actual 4); no gameplay/test changes.
- Environment: Windows capture failed 0x80004002; Unity MCP camera captures used. Test Runner saved pre-existing dirty BattleScene edits before testing.
- Delivery: Docs/StylizedBackground.md and Docs/StylizedBackgroundPreview.png. No commit/merge.
- Follow-up scope: existing scope plus Assets/Resources/Materials/RuinTiles.mat, Docs/ChessboardBefore.png, Docs/ChessboardAfter.png. Preserve its GUID and source texture references; keep a source material backup in the dedicated background folder. No board mesh/layout or scene edits.
- Follow-up acceptance: board's original rock/metal atlas, normal and packed mask remain usable; light/dark cells and units remain readable in actual BattleScene camera; compile and Play Mode console verification.
- Follow-up result: RuinTiles shared material updated with original GUID and texture references preserved, packed mask and masked metal tint added, source material backed up. Actual 64 board renderers, units and movement overlay rendered in BattleScene Play; shader/console errors and warnings 0, SRP Batcher 0, Play exited. No scene/gameplay changes. Docs/ChessboardPlay.png also included as validation evidence.
