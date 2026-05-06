# Checkmate RPG — Developer Quick-Start

## Requirements

| Tool | Version |
|------|---------|
| **Unity** | **6000.3.10f1** (exact version — use Unity Hub) |
| Rendering | Universal Render Pipeline (URP) — already configured |
| Platform | PC (Windows/macOS/Linux) |

---

## First-time setup

1. **Clone** the repository.
2. Open **Unity Hub → Open Project** and point it at the repo root.  
   Unity will import assets and regenerate `.meta` files on the first open (expect 1–2 minutes).
3. You can ignore "Enter Safe Mode" warnings — just click **Ignore** and continue.

---

## Running the canonical test scene

**Canonical scene:** `Assets/Scenes/Test/BattleTest.unity`

1. In the **Project** window, navigate to `Assets/Scenes/Test/`.
2. Double-click `BattleTest.unity` to open it.
3. Press **▶ Play**.

### What you should see

| Object | Expected behaviour |
|--------|--------------------|
| **GridSystem** (created at runtime) | Singleton available; 8 × 8 occupancy grid active |
| **Player units** (blue, cells 1-0, 3-0, 5-0) | Spawned, registered in grid, auto-chase nearest enemy |
| **Enemy units** (red, cells 2-7, 4-7, 6-7) | Same as above, targeting player units |
| **Camera** | Positioned above the grid, angled 60 ° down to show all 8 columns |
| **Console** | No errors; bootstrapper logs six "Spawned … at (x, y)" messages |

The `BattleTestBootstrapper` MonoBehaviour creates everything at runtime from scratch — no
prefabs or ScriptableObject assets need to be configured by hand.

---

## Project structure

```
Assets/
├── Scenes/
│   ├── SampleScene.unity        – Unity default scene (ignore for now)
│   └── Test/
│       ├── BattleTest.unity     ← CANONICAL test scene
│       └── TestScene.unity      – Legacy minimal scene (camera + light only)
├── Scripts/
│   ├── Core/                    – Interfaces (IDamageable, IMovable, IAttackable, …)
│   ├── Data/
│   │   └── UnitData.cs          – ScriptableObject: design-time stats per unit archetype
│   ├── Grid/
│   │   └── GridSystem.cs        – 8×8 grid, world↔cell conversion, occupancy tracking
│   ├── Components/
│   │   ├── HealthComponent.cs   – HP, damage, healing, death event
│   │   ├── MovementComponent.cs – Grid-based move with lerp animation, knockback, grab
│   │   ├── CombatComponent.cs   – Cooldown attack, range check, damage dispatch
│   │   └── StatusEffectComponent.cs – DoT, CC, elemental auras (framework ready)
│   ├── Units/
│   │   └── UnitBrain.cs         – Orchestrator: wires components, runs AI decision loop
│   └── Testing/
│       └── BattleTestBootstrapper.cs – Runtime scene builder for BattleTest.unity
└── Docs/
    ├── MILESTONES.md            – Full milestone plan (M0 → M7)
    └── DEV_QUICKSTART.md        ← YOU ARE HERE
```

---

## What is implemented (Milestone 0 baseline)

| System | Status | Notes |
|--------|--------|-------|
| 8×8 Grid (`GridSystem`) | ✅ Complete | Singleton, occupancy, world↔grid conversion, tile-effect stubs |
| Unit stats (`UnitData`) | ✅ Complete | ScriptableObject; all fields data-driven |
| Health / damage | ✅ Complete | Physical, magical, true damage; defence/resistance; death event |
| Movement | ✅ Complete | Grid-based lerp, range check, knockback, grab |
| Combat | ✅ Complete | Cooldown attack, Chebyshev range, damage dispatch |
| Status effects | ✅ Framework | Durations, DoT ticks, CC flags, elemental aura logic wired |
| AI decision loop | ✅ Basic | Chase-nearest + attack-if-in-range; runs in `FixedUpdate` |
| Test scene setup | ✅ Complete | `BattleTestBootstrapper` spawns 6 units with no manual asset setup |

## What is NOT yet implemented

| System | Milestone |
|--------|-----------|
| AP economy (regen, action costs) | M1 |
| Chess class movement rules (Pawn, Knight, …) | M2 |
| Battle setup / deployment phase | M2 |
| Tile types with real effects (Swamp, Spikes, Sanctuary) | M3 |
| Physics 2.0 (weight grades, splat damage, ring-out) | M4 |
| Elemental reactions & CC matrix | M5 |
| Scoring AI & override system | M6 |
| Progression meta (Notation, Resonance, King Edicts) | M7 |
| Any UI (HUD, overlays, prediction ghost) | M1–M4 |

See `Docs/MILESTONES.md` for the full breakdown and DoD per milestone.

---

## Common issues

| Symptom | Fix |
|---------|-----|
| Console shows "Missing Script" on the Bootstrapper | Verify `Assets/Scripts/Testing/BattleTestBootstrapper.cs.meta` is present in the repo; the GUID must match the scene reference (`7e3f9a1b2c4d5e6f0a1b2c3d4e5f6a7b`). |
| Units spawn but stand still | Expected — the AI requires a valid target. Give them a few FixedUpdate ticks; units find the nearest enemy automatically. |
| Pink / magenta materials on units | URP shader not applied. Click **Edit → Rendering → Render Pipeline → Upgrade Project Materials** to URP. |
| Camera shows nothing | Ensure the scene is `BattleTest.unity`, not `TestScene.unity`. Press **F** in Scene view to frame all objects. |

---

## Adding new UnitData assets

1. Right-click in the **Project** window → **Create → CheckmateRPG → Unit Data**.
2. Fill in stats in the Inspector.
3. Drag the new asset onto a `UnitBrain._unitData` field, or pass it to `brain.Prepare(data, cell)` in a bootstrapper script.
