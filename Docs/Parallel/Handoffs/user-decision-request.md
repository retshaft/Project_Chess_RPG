# User Decision Request — Outgame/Progression Design Items Consolidated (S0)

> **✅ ANSWERS RECORDED (user reply, 2026-09-11) — summary in Korean:**
> - **D1**: 첫 클리어 = 첫 클리어 보너스 + 반복 보상 / 이후 반복 보상만. 단, TP는 임시 제공품이므로 보상 체계 자체의 수정 필요 → R01a를 "데이터 주도 보상 체계 재작성"으로 확대
> - **D2**: 공명 효과를 구현하고, 사용자가 공명 옵션을 수정 가능한 구조로 만들 것(데이터 주도) → S2 범위 확정
> - **D3**: 킹 칙령 수용량은 워프레임 모드 시스템 유래 → **기본 30으로 확정, 적용 완료(2026-09-11)**
> - **D4**: 문서 가독성 문제로 미결정 → 한국어 재질문 필요 (스타터 덱 조합)
> - **D5**: 상태이상 관련으로 보이며 **기획서 컨셉 유지** → 프로덕션(DotEffectProcessor, 마법 피해) 유지 + 테스트를 프로덕션 기준 재작성 **완료(2026-09-10~11 검증: EditMode 110/110)**. 사용자가 상세 내용을 정확히 확인하지는 못했으므로, 읽기 쉬운 한국어 요약본으로 재확인 권장
> - **D6**: 적 킹 이동 데이터는 수정 불필요 → **24 유지 확정**
> - **D7**: 승급 AP 회복은 기획상 수정이 필요하므로 **보류** → 현행 동작 유지, 재설계는 별도 기획 배치로
> - **D8**: 스킬은 **도입해야 함** → 별도 기획 배치 필요(GDD는 레벨 7/10 특성 선택 방향만 제시, 수치 테이블 없음)

- Author: ZCode (GLM-5.3-Flash) / 2026-09-11 / Deliverable of roadmap batch S0 (see long-term-roadmap.md)
- Purpose: Consolidate the 8 open design decisions so a single reply unlocks the next batches (S1~S4, T-2)
- Reply format: answer with item numbers and options only (e.g., "D1-a, D2-b, D3: 5")
- Principle: each item cites its GDD (Master Design Doc) basis with file location. Items without a GDD basis are marked "no GDD basis found" — nothing was invented or pre-decided.
- Note: this document was regenerated in English because the Korean text generation in this session was producing corrupted glyphs. A Korean version can be regenerated in a fresh session on request.

---

## D1. Stage victory reward rule (first clear vs repeat)

- Background: every win currently grants full `stage.RewardTP` (default 50) + King Suit Level +1 + edict unlocks unconditionally (`BattleResultUI.cs:147-152`). There is no first/repeat distinction in code.
- GDD basis: **none found** — Part 4's "progress reward" refers to notation (chess-puzzle) stat progress, not stage-clear currency rules. "King gains resonance currency and TP per campaign clear" (Part 4, King resonance section) exists as a direction only, with no amounts.
- Options:
  - (a) Keep full rewards every win (current) — farming loop works immediately, zero extra implementation
  - (b) Full reward on first clear + reduced repeat reward (e.g., 30%) — requires a new reduction rate, and implements together with R01a's save-history field
- Impact: choosing (b) expands the R01a scope. Choosing (a) keeps R01a as duplicate-grant prevention only.
- Recommendation: (a) first; revisit (b) at release-balance time (numbers decided then).

## D2. Resonance stages 1·2·4·5 handling

- Background: GDD defines all 5 stages, but **only stage 3 (permanent AP discount: pawn -1, knight/bishop -2, rook -5 — Part 4, resonance stage 3) is implemented** (`DefaultActionCostPolicy`). Notation TP +2 per stage is implemented (Part 4 resonance definition, `ResonanceSystem.GetNotationTP`).
- GDD basis (Part 4 resonance section):
  - Stage 1: death respawn timer -10% → **부활 시스템 배제(영구 사망 원칙) 확정**. 역할군별 고유 실전 스탯 보너스로 개정 (공격형: ATK +10% / 수비형: MaxHP +10% / 지원형: 치유량 및 버프 계수 +10%)
  - Stage 2: battle-start initial SP +10 → **implementable** (UnitData MaxSP/InitSP fields exist)
  - Stage 4: status-resist chance +10% vs freeze/stun → **resist-chance system not implemented** (cannot wire)
  - Stage 5: final enhancement of the 2nd talent (passive) → **2nd-talent system not implemented** (cannot wire)
- Options:
  - (a) Keep "coming soon" display for unwired stages (current, honest display) — zero extra implementation
  - (b) Wire stage 2 only (initial SP +10) — small implementation (add InitSP at battle start)
  - (c) Implement resist/2nd-talent systems first (large, separate milestone work)
- Recommendation: (b) — the GDD specifies the number (+10) and it is the smallest change. (a) is also valid.

## D3. King suit base capacity absolute value

- Background: GDD Part 4 formula "max sync capacity = (suit base level-scaled capacity) + (play TP) + (absolute edict bonus)" — **the absolute value is undefined**. Current asset `Resources/SyncCapacity.asset` = 2 (code default). Measured in play: capacity = 2 + play TP 250 + absolute edict +12 = 264.
- Options: provide a number (e.g., 5, 10, 20) — if far below the base edict costs (6~7), early-game equipping becomes impossible (no practical impact for the current 250-TP save).
- Recommendation: a value that allows 1~2 edicts at PlayTP 0 (e.g., 5~10); "level-scaled" expansion can be wired to suit level later.

## D4. Starter deck composition

- Background: a new save auto-equips the king + the top 2 database-order units (`DeckManager.cs` starter block, comment says "temporary"). GDD has **no starter deck definition**.
- Options: (a) keep current (DB order dependent) (b) specify a fixed combination (e.g., PlayerKing + Elena + Kiara — assets that exist today)
- Recommendation: (b) — both "Kiara_UnitData" and "Kiara" assets exist, so a fixed starter list guarantees a deterministic new-player screen.

## D5. Poison damage rule — **GDD basis secured**

- Background: one Poison test is failing. Audit found: **the test exercises a processor that production never selects**, and that processor's floor calculation (100 × 0.02 × 2.5 = 4.9999 → 4) is the direct failure cause.
- GDD basis: **Part 2 poison entry: "inflicts continuous magic damage per tick"** — magic damage is explicit; there is no basis for true damage or floor rounding. → **production (DotEffectProcessor: magic, round, stack-multiplied) matches the GDD.**
- Options:
  - (a) **Keep production + rewrite the test** (recommended) — verify against DotEffectProcessor (magic, round, stack scaling) + clean up the dead branch in the unselected StatusEffectProcessor
  - (b) Change production to the test rule (true damage, floor) — no GDD basis found for treating poison as fixed damage (re-confirm needed)
- Recommendation: (a) — reaches EditMode 109/109 and cleans up dead code at the same time.

## D6. Enemy king movement AP

- Background: the task text said "enemy king move AP 20", but **current data is 24** (`Enemy_BlackKing.asset` MoveCostAP), matching GDD Part 3's king spec (24 AP). No document basis for 20 was found.
- Options: (a) keep 24 (recommended — GDD and data agree) (b) change to 20 (re-gate as an E01 balance experiment value only)
- Recommendation: (a)

## D7. Keep promotion full-AP-refill or change

- Background: D18 audit complete — GDD Part 3 promotion section "immediately act with AP restored to 100%" **has a GDD basis** (present in the doc since the first commit). Code fills the ally pool up to the cap for ally promotions only (no over-cap), with no cross-battle accumulation.
- 5 sub-decisions (D18 doc §9): (1) confirm refill target (2) whether enemy-team promotion refills the enemy pool (current: no) (3) allow Herald re-promotion refill loop (current: structurally possible) (4) include conscripts (current: yes) (5) formalize 3s wait + 70% damage reduction
- Options: (a) **keep all as-is** (recommended — consistent reading of the GDD sentence) (b) change per-sub-item (each then needs implementation/fix)
- Recommendation: (a)

## D8. Skill level data introduction

- Background: skills currently store only an "equip index"; there is no per-level stat table (and we do not display numbers that do not exist).
- GDD basis: Part 4 "skill traits" section — **level 7 / 10 thresholds where a skill trait (efficiency/power vs vanguard/destruction) is chosen** is mentioned (direction exists; no stat table).
- Options: (a) keep un-introduced (current — equip index only, no missing numbers displayed) (b) introduce the level 7/10 trait-choice system (needs a new design table, long-term)
- Recommendation: (a) first — the level system itself is unimplemented, so introduction belongs in a separate design batch.

---

## Reply form (copy-paste)

```
D1: (a/b)
D2: (a/b/c)
D3: value = __
D4: (a/b) — if (b), combination: king + __ + __
D5: (a/b)
D6: (a/b)
D7: (a/b)
D8: (a/b)
```

## Unlocked batches per answer

| Answer | Unlocks |
|---|---|
| D1 | S1 (R01a reward dedup) |
| D2 | S2 (resonance display cleanup) |
| D3 | capacity asset value swap (one line) |
| D4 | starter deck test pin |
| D5 | Poison test rewrite → EditMode 109/109 |
| D6 | E01 measurement baseline confirmed |
| D7 | promotion logic change only if changed |
| D8 | separate design batch only if introduced |

## Reference: technical defects that are NOT design decisions (need task sheets — unrelated to the above)

- F1: promotion does not clear runtime status effects (needs C02 task-sheet ownership)
- F2: BattleScene standalone run has no StageManager (warning + no enemy spawn, no crash)
- F3: OperatorHubUI rewrite self-review (code audit + Play interaction measurement done 2026-09-10)
