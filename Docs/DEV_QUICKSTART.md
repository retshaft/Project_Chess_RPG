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

## Running the formal game flow

**Canonical execution path:** `Assets/Scenes/OutgameScene.unity` → `Assets/Scenes/Test/BattleScene.unity`

> 2026-09-07 기준, 아웃게임 허브 씬은 루트 `Assets/Scenes/OutgameScene.unity`가 유일한 정식 씬이다.
> (구 `Test/OutgameScene.unity`는 삭제되었고, Build Settings도 루트 씬을 가리킨다.)

1. In the **Project** window, navigate to `Assets/Scenes/`.
2. Double-click `OutgameScene.unity` to open it.
3. Press **▶ Play**. (The game will transition to `BattleScene.unity` during play).

### What you should see

| Object | Expected behaviour |
|--------|--------------------|
| **Outgame Canvas** | Title / Main Lobby / Stage Select / Roster(덱 편성) / Operator Hub / Recruit Hub / Synchro Board / Archive 뷰 전환 |
| **GameManagers** | `SaveManager`, `StageManager`, `DeckManager` 등 아웃게임 런타임이 씬에 상주 |
| **Transition** | 덱 편성 후 출격 시 `Test/BattleScene.unity` 로드 |
| **Console** | No errors; 예외/Missing Script 없음 |

전투 흐름과 세부 검증 절차는 [MILESTONES.md](MILESTONES.md)와 [DEV_PROCESS.md](DEV_PROCESS.md)를 따른다.

---

## Project structure

```
Assets/
├── Scenes/
│   ├── OutgameScene.unity       ← Canonical START scene (아웃게임 허브)
│   ├── CombatSandbox.unity      – 전투 연출 샌드박스 (에디터 툴로 생성)
│   ├── StageSelectScene.unity   – 레거시 (정식 흐름은 OutgameScene 내 Stage Select 뷰 사용)
│   └── Test/
│       ├── BattleScene.unity    ← Canonical BATTLE scene
│       └── TestScene.unity      – Legacy minimal scene (camera + light only)
└── Docs/
    ├── MILESTONES.md            – 우선순위 실행 계획 + 마일스톤 M0~M6 (단일 기준 문서)
    ├── DEV_PROCESS.md           – AI·개발자 협업 사이클 + 검증 체크리스트
    ├── ACTION_TIMELINE.md       – Action speed tick formula + timing field definitions
    └── DEV_QUICKSTART.md        ← YOU ARE HERE
```

---

## 구현 상태

구현 현황·우선순위·남은 작업의 단일 기준은 [MILESTONES.md](MILESTONES.md)를 참조한다.
(구버전 "Milestone 0 baseline" 표는 폐기되었다.)

---

## Common issues

| Symptom | Fix |
|---------|-----|
| Console shows "Missing Script" | Most likely a stale scene reference after a rename/delete. Check which scene you opened and re-open the canonical `Assets/Scenes/OutgameScene.unity`. |
| Units spawn but stand still | Expected — the AI requires a valid target. Give them a few FixedUpdate ticks; units find the nearest enemy automatically. |
| Pink / magenta materials on units | URP shader not applied. Click **Edit → Rendering → Render Pipeline → Upgrade Project Materials** to URP. |
| Camera shows nothing | Ensure you start from `Assets/Scenes/OutgameScene.unity` and transition to `Test/BattleScene.unity`. Press **F** in Scene view to frame all objects. |
| 전투 종료 후 오래된 로비로 복귀 | Build Settings가 루트 `Assets/Scenes/OutgameScene.unity`를 가리키는지 확인한다 (9/7 이후 정상). |

---

## Adding new UnitData assets

1. Right-click in the **Project** window → **Create → CheckmateRPG → Unit Data**.
2. Fill in stats in the Inspector.
3. Drag the new asset onto a `UnitBrain._unitData` field, or pass it to `brain.Prepare(data, cell)` in a bootstrapper script.
