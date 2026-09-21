# Integration Proposal — Uncommitted Changes Commit Batches (T-1)

- Author: ZCode (GLM-5.3-Flash) / 2026-09-11 / Deliverable of roadmap batch T-1 (see long-term-roadmap.md)
- Target: main (Codex) — committing is MAIN's job; this document only proposes batches and order
- Basis: `git status --porcelain` on 2026-09-11 — 107 uncommitted entries, working tree = codex/integration @ 77160f4
- Principle: file-level batching (git has no partial-file commits). Batches are ordered so each commit leaves the tree in a sensible state. Everything stays uncommitted until MAIN stages and commits.
- Note: this document is written in English because Korean text generation was corrupted in this session (see user-decision-request.md note).

---

## Batch order proposal (commit in this order)

### C-B1. Art/Environment set (main's own, BOARD: Verified)

- `Assets/Settings/PC_RPAsset.asset`
- `Assets/Scenes/CombatSandbox.unity`, `Assets/Scenes/Test/BattleScene.unity` (if water placement is included, coordinate with C-B5 note below)
- `Assets/Scenes/Test/Materials.unity` (+meta, new), `Assets/Scenes/Test/Water.meta` + `Water/` (new)
- `Assets/Resources/Materials/Water.meta` + `Water/`, `Assets/Resources/Materials/RuinTiles.mat`, `Assets/Resources/Shader/Water.meta` + `Water/`, `Assets/Resources/Textures/Water.meta` + `Water/`, `Assets/Environment.meta` + `Assets/Environment/`
- `Assets/Scripts/Editor/Water.meta` + `Water/`, `Assets/Scripts/Water.meta` + `Assets/Scripts/Water/`, `Assets/Scripts/Editor/StylizedBackground.meta` + `StylizedBackground/`
- `Assets/Resources/Prefabs/ShallowWaterPortable.prefab` (+meta)
- Docs evidence: `Docs/Water.md`, `WaterPreview.png`, `WaterReferenceBeach.png`, `WaterReferenceLagoon.png`, `StylizedBackground.md`, `StylizedBackgroundPreview.png`, `SunkenCourt.md`, `SunkenCourt*.png`, `MechanicalRuinsAssets.md`, `BattleWater.png`, `Chessboard*.png`
- Tasks/Handoffs pairs: shallow-water, battle-water-placement, mechanical-ruables-assets, stylized-background, sunken-court

### C-B2. Enemy unit data set (BOARD "preserve" decision target)

- `Assets/Data/Units/Enemies/Enemy_BlackKnight.asset`, `Enemy_BlackPawn.asset`
- `Assets/Resources/Units/Enemies/Enemy_Black{Bishop,King,Knight,Pawn,Queen,Rook}.asset`
- `Assets/Resources/Units/PlayerKing.asset` (+meta, new — added by main)

### C-B3. Outgame UX set (main's rewrite + internal edict worker fixes — see glm-outgame-long-goal.md §3)

- `Assets/Scripts/UI/SynchroBoardUI.cs`, `EdictSelectorModal.cs`, `DeckBuilderUI.cs`, `OperatorHubUI.cs`, `MainLobbyUI.cs`, `OutgameUIManager.cs`, `RosterUI.cs`, `UiFontProvider.cs`
- `Assets/Scripts/Progression/DeckManager.cs`, `MetaProgressionCalculator.cs`, `MetaProgressionRuntime.cs`, `PolarityCalculator.cs`
- `Assets/Resources/SyncCapacity.asset` (+meta, new)
- `Assets/Scripts/Testing/EditModeTests/OutgameRuleRegressionTests.cs` (+meta, new), `OutgameProgressionRegressionTests.cs` (+meta, new — **authored by main; partially overlaps OutgameRuleRegressionTests coverage: integrate-review recommended**)
- WARN: `CheckmateRPG.Tests.asmdef` must be committed **together with C-B4** (see C-B4 — OutgameRuleRegressionTests needs the TMP reference). Put the asmdef in whichever of C-B3/C-B4 comes first.

### C-B4. C01 attack-grace set (review-fix version — original tree is canonical)

- `Assets/Scripts/Core/AttackGraceRules.cs` (+meta), `ActionRuntimeController.cs`, `Actions/Resolvers/AttackActionResolver.cs`, `Actions/Resolvers/IBattleContext.cs`, `Prediction/PredictionBattleContext.cs`, `Prediction/PredictionMutationApplier.cs`, `Prediction/PredictionPipeline.cs`, `Runtime/IReadOnlyUnitRuntimeState.cs`, `Runtime/Processors/MovementMutationProcessor.cs`, `Runtime/UnitRuntimeState.cs`, `Simulation/SimulationRuntime.cs`
- `Assets/Scripts/Testing/EditModeTests/AttackGraceRegressionTests.cs` (+meta)
- `Assets/Scripts/Testing/EditModeTests/CheckmateRPG.Tests.asmdef` — include here (or with C-B3, whichever comes first)
- Editor: `RepresentativeComboSetupEditor.cs` (+meta) is M2 ownership — moved to C-B6

### C-B5. R01b save-recovery set (verified 7/7 on 2026-09-10)

- `Assets/Scripts/Progression/SaveManager.cs`
- `Assets/Scripts/Testing/EditModeTests/SaveManagerRecoveryTests.cs` (+meta)

### C-B6. M2 combo tool set (BOARD: Review — M2 close-out target)

- `Assets/Scripts/Editor/RepresentativeComboSetupEditor.cs` (+meta)
- `Assets/Scripts/Testing/EditModeTests/RepresentativeComboRegressionTests.cs` (+meta)

### C-B7. M3 status tests set (Antigravity deliverable — includes 1 known Poison failure, keep until D5 decision)

- `Assets/Scripts/Testing/EditModeTests/StatusEffectRegressionTests.cs` (+meta)

### C-B8. Parallel docs set

- `Docs/Parallel/` whole: BOARD.md, START_ZCODE.md, AUTOMATION.md, CONNECTION_STATUS.md, DEVELOPMENT_ORDERS.md, GLM_TASK_QUEUE.md, ZCODE_LONG_GOAL.md, `Tasks/` (9 files), `Handoffs/` (16 files incl. user-decision-request.md, long-term-roadmap.md, integration-proposal.md)
- `Tools/Parallel/agents.json`, `dispatch.py`, `test_dispatch.py`

## NOT recommended for commit (main decides)

| Path | Reason |
|---|---|
| `.codex-remote-attachments/`, `.zcode/` | local tool-cache directories — do not commit; add to `.gitignore` (recommended) |
| `Assets/_Recovery/0 (10).unity` (+meta) | temp recovery scene — main decides commit or delete |
| `compile.log`, `editmode-tests.log`, `editmode-results.xml` (project root) | 배치 검증 증빙 파일 — 보존하려면 Docs/ 로 이동하거나 gitignore 처리(메인 결정) |

## Cautions

1. Sequential commits in ONE working tree — no cross-batch file sharing EXCEPT `CheckmateRPG.Tests.asmdef` (C-B3/C-B4 boundary; put it in whichever commit comes first).
2. Recommended: after each batch commit, enter Unity once to confirm compile (especially C-B3 and C-B4).
3. This is a file-level proposal; running the commits and deciding the order is MAIN's job.
