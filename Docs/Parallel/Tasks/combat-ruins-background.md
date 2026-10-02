# Combat ruins environment

- Status: Ready (2026-10-01: user requested implementation and application)
- Owner: Codex main / codex/integration / original workspace
- Base: ef130428defd3076024644c5addc0b9bd4d8dcf5
- Target: Assets/Scenes/Test/BattleScene.unity (existing project mapping for CombatScene; scene placement intentionally not re-run after the user's cleanup).
- Allowed: target scene; Assets/Environment/CombatRuins/** and metas; Assets/Scripts/Editor/StylizedBackground/CombatRuins*.cs and metas; Assets/Settings/PC_Renderer.asset; Ruin/Textures importer settings with preserved GUIDs; Tools/Art/**; Docs/CombatRuins.md; Captures/CombatRuins/**; this task and BOARD entry.
- Preserve: gameplay, UI and camera director work; existing water location; original FBX assets/importers; all other scenes.
- Scope: use user-authored `Assets/Resources/Models/Ruin/Textures/**` PBR sets in URP Shader Graph surface materials; provide URP Decal Shader Graphs for moss/waterline projection; import a CC0 ivy glTF asset and expose explicit placement commands. Do not restore the removed layout automatically.
- Validation: run Unity import/compiler console, create materials from the editor menu, inspect Decal Renderer Feature or mesh-overlay fallback, then Play Mode and errors. Unity is currently unavailable in this environment, so those checks remain pending. No paid services or exports.
