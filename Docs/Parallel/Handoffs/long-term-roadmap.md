# Long-Term Roadmap — Growth/Collection UX Track First (baseline 2026-09-11)

- Author: ZCode (GLM-5.3-Flash) / 2026-09-11 / user directive "여기에 더 장기 픬란" (add a longer-term plan here)
- Nature: **planning document** — each batch is issued by the user as a separate long-goal (/goal). At issue time, a Ready task sheet is prepared and then executed.
- Priority track: **Growth/Collection UX** (user-selected) — R01a rewards → resonance/notation growth wiring → M5 close-out → M3
- Location note: filed under Handoffs for consistency, but this is a roadmap; main may promote it to `Docs/Parallel/ROADMAP.md`
- References: [glm-outgame-long-goal.md](glm-outgame-long-goal.md) · [long-goal-reward-save-audit.md](long-goal-reward-save-audit.md) · [long-goal-status-audit.md](long-goal-status-audit.md) · [long-goal-king-chase-test-plan.md](long-goal-king-chase-test-plan.md) · [user-decision-request.md](user-decision-request.md) · [integration-proposal.md](integration-proposal.md) · [../MILESTONES.md](../../MILESTONES.md) · [DEVELOPMENT_ORDERS.md](DEVELOPMENT_ORDERS.md)
- Language note: written in English because Korean text generation was corrupted in this session (see user-decision-request.md note). Korean chat summaries remain available.

---

## 1. Baseline snapshot (2026-09-11)

### 1.1 Verified (do NOT re-verify — EditMode 108/109 + Play measurement on 2026-09-10)

| Verified item | Evidence |
|---|---|
| Edict screen self-configuration (scene refs all empty → slots/modal/meter auto-built) — Play-measured | glm-outgame-long-goal §7 |
| Edict equip/save + all three rejection messages (wrong kind / duplicate / capacity 17 vs 14) — Play-measured | glm-outgame-long-goal §7 |
| Full outgame flow: lobby → operator hub → skill equip (index 0→1 saved) → notation TP-shortage display → resonance display → deck (no enemy units) → stage set → BattleScene entry → result rewards (TP 250→300, KingLv 6→7, saveData.json file check) → return with persisted state | glm-outgame-long-goal §7 |
| C01 attack-grace review fixes (prediction piece rules, forced-move invalidation, admission check) — 24/24 | c01-attack-grace-review-fixes + EditMode run |
| R01b save atomic-write/backup/recovery — 7/7 | r01b-save-recovery §7 |
| Enemy unit gating rules (IsPlayerUnit/IsAvailableToPlayer) — 4 tests | OutgameRuleRegressionTests |
| D18 promotion-AP audit / status-consistency audit / reward-save audit / king-chase test plan | each handoff (audit/plan complete) |

### 1.2 Main's work (track only — not touched by this roadmap)

- Art 5 task sheets (sunken court, shallow water, water placement, stylized background, mechanical ruins assets) — BOARD: **Verified**. Includes scene edits (OutgameScene/BattleScene/CombatSandbox) and Water/StylizedBackground/Material assets — all uncommitted (see T-1).
- OperatorHubUI 465-line rewrite (skill/notation/resonance tabs, Korean guidance) — **Play interaction measured 2026-09-10**; main's own code review remains.
- **107 uncommitted entries** — see T-1 integration proposal.

### 1.3 Open items (what this roadmap organates)

| # | Item | Type | Gates |
|---|---|---|---|
| D1 | first/repeat clear reward rule undefined | user decision | R01a (S1) |
| D2 | resonance stages 1·2·4·5 unwired (only stage-3 AP discount live) | user decision (display policy / implement) | S2, M5 close-out |
| D3 | king suit base capacity absolute value (current: 2 = code default) | user decision (a number) | capacity asset value swap |
| D4 | starter deck combo (DB order dependent, "temporary") | user decision (a list) | starter deck test pin |
| D5 | Poison rule basis (test vs production mismatch; GDD Part 2 supports production: magic damage per tick) | user decision (confirm basis) | Poison test rewrite → 109/109 |
| D6 | enemy king move AP (goal text said 20; data and GDD say 24) | user decision | E01 measurement baseline |
| D7 | promotion full-AP-refill keep/change (D18 audit; GDD basis exists for keeping) | user decision (+5 sub-items) | promotion logic change only if changed |
| D8 | skill level data introduction (GDD mentions level 7/10 trait choice; no stat table) | user decision (introduce or not) | skill UI number display cleanup |
| F1 | promotion does not clear runtime status effects (structural defect) | task sheet + ownership (C02) | promotion status-clear implementation |
| F2 | BattleScene standalone run: no StageManager (warning + no enemy spawn, no crash) | task sheet (fallback stage decision) | standalone-run fallback |
| F3 | BattleResultUI.ShowResult re-call re-grants rewards (normal flow guarded once; structure risk) | same batch as D1 (R01a) | = D1 |

---

## 2. Dependency map — decision gates → locked work

```
D1 (reward rule)  ──▶ R01a reward dedup (S1) ──▶ M5 close-out
D2 (resonance policy) ─▶ resonance display cleanup (S2) ──▶ M5 close-out
D3 (capacity value) ─▶ SyncCapacity.asset value swap (one line)
D4 (starter deck) ──▶ starter deck test pin
D5 (poison basis) ─▶ Poison test rewrite or production fix ──▶ EditMode 109/109
D6 (king AP) ─────▶ enemy king data value ──▶ E01 measurement baseline
D7 (promotion AP) ─▶ (only if changed) promotion logic ──▶ battle balance
D8 (skill levels) ─▶ skill UI display cleanup ──▶ operator growth UX
F1 (promotion status clear, structural) ─▶ C02 task sheet (ownership) ─▶ implementation
F2 (standalone battle fallback) ─▶ small task sheet
```

---

## 3. Track S — Growth/Collection UX (priority)

### S0. Consolidated user decision request — **DONE + ANSWERED (2026-09-11)**

- Deliverable: [user-decision-request.md](user-decision-request.md) — D1~D8 with options, GDD citations, impacts, reply form
- Status: **user answered all 8** (recorded at the top of that document). Unlocked: R01a expanded to data-driven reward rework (D1), resonance implementation with modifiable options (D2), capacity 30 applied (D3), production-basis poison test rewrite done — **EditMode 110/110 (2026-09-11)** (D5), king AP 24 kept (D6), promotion refill on hold — keep current behavior (D7), skill system introduction deferred to its own design batch (D8), starter deck re-ask pending (D4 — original request was unreadable due to corrupted text).

### S1. R01a reward dedup (gate: D1)

- Goal: same battle result never pays twice; a legitimate re-challenge still pays. NOT a memory-bool (must survive app restart).
- Design (from reward-save audit): extract the grant logic from BattleResultUI into a new pure Progression class; record (stage ID + result identifier) in SaveData before granting; cinematic callback and debug tools go through the same function.
- Owner: implementation ZCode (task sheet: BattleResultUI.cs + SaveData.cs [additive fields only] + new pure class + tests — **needs main approval first**; gated on D1 for the reward rule itself)
- Verify: EditMode "same result twice → granted once", "re-challenge → granted", "save round-trip" / Play: result → restart → lobby, currencies match
- Deliverable: handoff r01a-reward-dedup.md

### S2. Growth wiring completion (gate: D2 partial, D8) — **DONE 2026-09-12 (implementation + batch EditMode 118/118 verified)**

- Status (2026-09-12): **stage-2 initial SP +10 wired and batch-verified**. MetaProgressionCalculator.CreateModifiedUnitData applies the GDD Part 4 stage-2 number "+10 초기 SP" to the clone's InitSP when `loadout.ResonanceStage >= 2` (SPComponent clamps to MaxSP at battle start). Regression tests added (OutgameRuleRegressionTests: stage 2 adds +10 on clone only, stage 1 adds nothing). **Batch-mode EditMode run AFTER these changes: 118/118 passed, 0 failures, compile errors 0** (validate_build.bat CLEAN + `Unity -batchmode -runTests -testPlatform EditMode`, evidence: editmode-results.xml / editmode-tests.log in project root — main decides commit). Play SP-delta measurement remains optional (the modified-data path is unit-tested; battle-side clamp is SPComponent's existing behavior).
- Data-driven note (D2 "옵션 수정 가능"): ResonanceProfileData.StageBonuses is already designer-editable data. Stage 1 is determined as role-based stat enhancements (Offensive: ATK +10%, Defensive: MaxHP +10%, Support: SupportPower +10% [healing/buff multiplier]), strictly excluding any revival/respawn mechanics (perma-death rule). Stages 4/5 (status resist, 2nd talent) require systems that do not exist — documented as separate design/implementation batches, not silently wired.
- Goal: make "display = actual effect" trustworthy for growth systems.
- Remaining contents: (a) resonance unwired stages 1/4/5 — per D2 decision (b) notation node unlock → TP deduction → re-entry persistence, Play interaction measurement (operator hub screen — main's, coordinate) (c) grown stats reach battle — deploy measurement (GetModifiedDeck path verified in code; InitSP path now wired and tested)
- Owner: display changes are OperatorHubUI (main's) → task sheet needed; measurements are ZCode (Unity permission)
- Verify: EditMode full suite (**achieved 118/118**) + Play (deploy at resonance stage 2 → battle-start SP = base +10; unlock 1 notation node → TP delta), console exceptions 0

### S3. M5 close-out (gate: D1·D2 handled)

- Goal: the target flow (title → lobby → … → result → return) works as a 10-minute demo.
- Contents: 10-minute demo scenario document (per-minute expected screens), remaining UX cleanup (empty lists show guidance, failure reasons visible), "Back to Map lands on Title" destination decision (additional user decision — m5 audit §7)
- Owner: scenario document ZCode / UX fixes per screen owner (main + internal worker)
- Verify: re-run the 22-step Play procedure + scenario document pass

### S4. M3 status combos (gate: m3-status-package-spec main+user approval — **waiting**)

- Goal: build the recommended 2~3 combos (e.g., Chill/Freeze + shatter) as data + tests + UI (GLM-05)
- Owner: ZCode implementation (GLM-05 scope) — **must not start before approval**
- Verify: per-combo EditMode + Play (apply/clear/UI display consistency)

---

## 4. Parallel tracks

### T-1. Integration support (main's commit work; ZCode can write the proposal doc) — **DONE (2026-09-11)**

- Deliverable: [integration-proposal.md](integration-proposal.md) — 107 uncommitted entries grouped into C-B1~C-B8 commit batches (art set, enemy data set, outgame UX set, C01 grace set, R01b set, M2 set, M3 tests set, parallel docs set), commit-order cautions (asmdef boundary), and do-not-commit list (.codex-remote-attachments/, .zcode/, _Recovery scene → main decides).

### T-2. Battle content (gates: D5 decision, task sheets)

1. Poison disposition (decision D5) → test rewrite or production fix → **EditMode 109/109**
2. C02: promotion runtime status clear (F1) + status-consistency fixes — needs C02 task sheet ownership
3. E01 king-chase measurement: run [long-goal-king-chase-test-plan.md](long-goal-king-chase-test-plan.md) in Play (after D6 confirmed)
4. M4: representative encounter (StageData-based — see m4 audit)

### T-3. Art (main Verified — track only)

Sunken court, water, background 5 sheets. This roadmap does not touch them. One recommendation: after art lands in scenes, run one Play re-check that visual resources did not break UI references or references in scenes.

---

## 5. Batch standard format (issu template)

| Field | Content |
|---|---|
| Batch ID | S0~S4 / T-1~T-3 |
| Goal | one sentence |
| Preconditions | user decision / main approval / prior batch |
| Owner | implementation + verification subject (with file ownership) |
| Expected files | whitelist |
| Verify | new EditMode tests + Play procedure |
| Deliverable | handoff (results, unverified items, risks) |

Issue method: user picks a batch and says "S1 실행해 줘" style → ZCode prepares the task sheet (waits where main approval is required) → executes → hands off.

---

## 6. User decision checklist (answer all at once)

See [user-decision-request.md](user-decision-request.md) §D1~D8 with the copy-paste reply form. Summary:

| # | Question | Current default |
|---|---|---|
| D1 | reward rule: (a) full every win (b) first full + repeat reduced | full every win |
| D2 | resonance unwired stages: (a) honest display (b) wire stage 2 only (c) full systems | honest display |
| D3 | king base capacity value | 2 (code default) |
| D4 | starter deck: (a) DB-order 2 units (b) fixed list | DB-order 2 units |
| D5 | poison rule: (a) keep production + rewrite test (b) fix production to test rule | production (per GDD Part 2) |
| D6 | enemy king move AP: 24 or 20 | 24 |
| D7 | promotion AP refill: keep or change | keep |
| D8 | skill level data: not introduced vs level 7/10 system | not introduced |

---

## 7. Risks

1. **107 uncommitted entries accumulate** — integration delay increases cross-set conflict risk (run T-1 proposal early).
2. **Gate stagnation** — if D1/D2/D5/D7 stay undecided, late Track S and all of T-2 stall. Resolve via the S0 request document in one pass.
3. **Dual state** — C01 fixes exist only in the original tree; branch aa0637a is older. Integration must standardize on the original tree.
4. **Poison and promotion defects left alone** — mild symptoms now (1 test, buffs persist after promotion) but they will contaminate M3/M4 verification if ignored.

---

## 8. Recommended execution order (summary)

**S0 (done) → user answers → S1 (R01a) → S2 (growth wiring) → S3 (M5 close-out) → S4 (M3)**, with T-1 proposal doc (done) handed to main at any time and T-2 unlocked after D5/D6. Each issued batch follows: task sheet (main approval where needed) → implementation → EditMode → Play measurement → handoff.
