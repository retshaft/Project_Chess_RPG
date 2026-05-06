# Milestones (Project: Checkmate RPG)

> Repo: `retshaft/Project_Chess_RPG`
>
> Target: Unity 6000.3.10f1 (see `ProjectSettings/ProjectVersion.txt`).
>
> This document turns the **Master GDD (Part 1~4)** into an actionable milestone plan.

---

## Scope + current status (quick)

### Confirmed in repo (prototype foundation)
- 8x8 grid + occupancy tracking: `Assets/Scripts/Grid/GridSystem.cs`
- Unit orchestration + simple AI loop: `Assets/Scripts/Units/UnitBrain.cs`
- ScriptableObject-driven unit stats: `Assets/Scripts/Data/UnitData.cs`
- Scenes: `Assets/Scenes/SampleScene.unity`, `Assets/Scenes/Test/TestScene.unity`

### Major gaps vs GDD
- AP economy (tick regen, action costs, refunds)
- Chess class rules (Pawn/Knight/Bishop/Rook/Queen/King) + promotion
- Tile layers/effects (Swamp/Spikes/Sanctuary)
- Physics 2.0 (weight, knockback, grab, Splat)
- Elemental reactions + CC matrix
- Scoring/override AI
- Tactical UI (overlays, prediction ghost)
- Progression meta (Notation, Resonance, King Edicts)

---

## Definition of Done (global)
- **Playable**: a user can start a battle, issue actions, win/lose, and restart.
- **Deterministic systems**: AP, movement rules, status effects are data-driven and testable.
- **Debuggable**: key subsystems have logs/visual debug toggles.

---

## Milestone 0 — Project hygiene & playable test loop (1–3 days)
**Goal:** Anyone can open the project, press Play in a test scene, and understand what works.

### Deliverables
- A single “BattleTest” scene (or clearly documented existing test scene) with:
  - GridSystem instance
  - A few units spawned and registered in grid occupancy
  - Basic camera + input instructions
- Minimal developer documentation.

### Tasks
- [ ] Create/confirm a canonical test scene (recommend: `Assets/Scenes/Test/BattleTest.unity`).
- [ ] Add an in-project readme (Unity `Readme.asset` or `Docs/`) explaining:
  - Unity version
  - How to run the test scene
  - What is implemented / not implemented

### DoD
- Fresh clone → open Unity → load test scene → Play works without missing references.

---

## Milestone 1 — AP Economy + command loop (1–2 weeks)
**Goal:** The battle is driven by **AP economy** (GDD Part 1/3), not free actions.

### Deliverables
- AP manager (global) with tick-based regen.
- Move/Attack consume AP and are blocked when insufficient.
- Simple HUD shows AP value and error feedback.

### Tasks
- [ ] Implement `APManager` (or `BattleResourceSystem`):
  - Current AP / Max AP
  - Natural regen: **+4 AP/sec** (configurable)
  - Events: `OnAPChanged`, `OnInsufficientAP`
- [ ] Extend `UnitData` to include AP costs (initial stub):
  - `MoveAPCost`, `AttackAPCost` (later per-class/per-skill)
- [ ] Wire AP checks into:
  - `MovementComponent.MoveTo`
  - `CombatComponent.Attack`
- [ ] UX:
  - Central AP gauge text bar (temporary UI ok)
  - When action fails due to AP: message + AP flash

### DoD
- In test scene: AP regenerates; moving/attacking changes AP; AP 부족 시 행동 불가.

---

## Milestone 2 — Chess-like movement rules (classes) + battle setup (2–3 weeks)
**Goal:** Units behave like chess classes (at least the “core set”), and battles start with all units deployed.

### Deliverables
- Movement rule framework (`IMovementRule` / `MovementPattern`).
- Class tags in data (Pawn/Knight/Bishop/Rook/Queen/King).
- Initial deployment system (“All-Deployed Start”).

### Tasks
- [ ] Add to `UnitData`:
  - `PieceClass` enum (Pawn/Knight/Bishop/Rook/Queen/King)
  - Team/faction fields (`TeamId` or `IsPlayerTeam`)
- [ ] Create movement rule calculators:
  - Pawn (forward move + diagonal capture)
  - Knight (L jump)
  - Bishop (diagonal)
  - Rook (orthogonal)
  - Queen (bishop+rook)
  - King (1-step)
- [ ] Implement selection + valid-move querying:
  - `GetValidMoves(unit)` returns cells respecting occupancy.
- [ ] `BattleSpawner`:
  - Spawns a preset roster at fixed cells
  - Registers with `GridSystem.SetOccupant`

### DoD
- Each class’ movement is correct on 8x8.
- Battle starts with both sides deployed.

---

## Milestone 3 — Tile system (Swamp/Spikes/Sanctuary) + status framework (2–3 weeks)
**Goal:** The board has meaningful tile layers and a reusable status-effect system.

### Deliverables
- Tile data and tile effect processing.
- Status effects framework with durations, stacking rules.

### Tasks
- [ ] Tile layer architecture:
  - `TileData` (ScriptableObject): type, move cost multiplier, periodic effects
  - Grid stores tile types for each cell
  - Implement `GridSystem.GetMoveCostMultiplier` and `ApplyTileEffects`
- [ ] Implement required tiles (GDD Part 2):
  - Swamp: Move AP cost *2
  - Spikes: periodic damage (align to GDD; current stub uses 5% current HP)
  - Sanctuary: ally-only defense +15% and regen (values configurable)
- [ ] Status core:
  - Base `StatusEffect` (duration, refresh/stack policy)
  - `StatusEffectComponent` processes ticks

### DoD
- Moving onto/standing on tiles has visible mechanical effect.
- Status effects can be applied/expire reliably.

---

## Milestone 4 — Physics 2.0 (weight/knockback/grab/splat) + prediction ghost (3–4 weeks)
**Goal:** The signature “position control” gameplay works and is readable.

### Deliverables
- Weight-based knockback.
- Splat damage (soft ring-out) per GDD Part 2.
- UI prediction ghost for knockback results.

### Tasks
- [ ] Physics rules:
  - Weight grade (0–4) influences knockback distance/resist
  - Collision with unit/obstacle/border triggers Splat
- [ ] Implement “soft ring-out”:
  - Border treated as red-zone barrier
  - Knocked into barrier → stop + splat damage
- [ ] Prediction:
  - While aiming knockback: show end cell and splat indicator

### DoD
- A knockback skill can push units; barrier collision deals damage; player can preview outcome.

---

## Milestone 5 — Elemental synergy + CC matrix (3–5 weeks)
**Goal:** Elemental reactions and CC keywords behave consistently.

### Deliverables
- Element tags and reaction resolver.
- CC keywords implemented with shared rules.

### Tasks
- [ ] Add to unit/attack data:
  - Element type(s)
  - Element application strength/duration
- [ ] Implement reaction resolver (start small):
  - Fire+Fire = Burn (Res -20%)
  - Cold progression (Chill → Freeze)
  - Lightning+Cold = Superconduct (Def -40%)
- [ ] CC matrix (at least): Stun, Root, Silence, Disarm, Taunt, Stealth.

### DoD
- Reactions trigger from element application and affect stats/behavior.

---

## Milestone 6 — AI scoring + overrides (4–6 weeks)
**Goal:** Enemy AI approximates “best move” behavior (GDD Part 4).

### Deliverables
- Candidate action generation + scoring.
- Override triggers: Checkmate, Danger, Setup Kill, Taunt.

### Tasks
- [ ] Refactor decision loop:
  - Generate candidate moves/attacks/skills
  - Score using weights (KillValue, AP weight, king safety)
- [ ] Override layer that can bypass scoring.
- [ ] Performance guardrails (tick budget, caching reachable cells).

### DoD
- AI does not just chase nearest; it makes tactical choices and respects overrides.

---

## Milestone 7 — Progression meta (Notation/Resonance/King Edicts) (timeboxed, later)
**Goal:** Implement the meta systems once core combat is stable.

### Deliverables
- Notation puzzle prototype for 1 unit.
- Resonance levels affecting AP costs.
- King Edict deck UI prototype.

### DoD
- A player can progress a unit and see combat impact.

---

## Immediate next actions (recommended)
1. Finalize **Milestone 1** design decisions:
   - AP max value? AP regen constant or modifiers?
   - Are actions real-time (cooldowns) or turn slices?
2. Choose “vertical slice” class set for early prototype:
   - Recommend: Pawn / Knight / King (minimum tactical variety)

