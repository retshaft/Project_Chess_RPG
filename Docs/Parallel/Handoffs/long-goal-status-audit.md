# long-goal-status-audit — 상태 효과 정합성 조사 (읽기 전용, 코드·테스트 변경 없음)

- 담당 / 브랜치 / 워크트리: ZCode / 작업서 없음(조사 전용) / 원본 워크스페이스
- 실제 시작 커밋: 77160f4 (+미커밋 C01 변경 — 본 조사와 무관)
- 코드 커밋 목록: 없음 — 본 문서만 산출
- 상태: **Review — 정적 조사 완료. Play 실측 항목은 미검증으로 표기**

## 0. 확인한 코드·문서 경로

`Assets/Scripts/Core/Effects/`(EffectSystem.cs, Processors/DotEffectProcessor.cs, Processors/StatusEffectProcessor.cs), `Assets/Scripts/Core/ActionRuntimeController.cs:736-752`(프로세서 등록), `Assets/Scripts/Components/StatusEffectComponent.cs:75-190`, `Assets/Scripts/Units/UnitBrain.cs:406-412`(승급 상태 제거), `Assets/Scripts/Testing/EditModeTests/StatusEffectRegressionTests.cs:131-204`, GDD Part 3 §2.2(34행). 테스트 실행 기록 출처: `Docs/Parallel/Handoffs/attack-move-grace.md` §5(스테이징 배치, 2026-09-08).

## 1. Poison 테스트 실패 원인 — **테스트 쪽 결함이 근거 있음 (구현 규칙 변경 근거는 없음)**

실패 테스트: `Processor_Poison_GeneratesTrueDamageBasedOnMagnitude`(기대: MaxHP 100 × 0.02 × 2.5 = 5, TrueDamage).

원인 1 — **부동소수점 Floor 오차(테스트가 실패하는 직접 원인)**: `StatusEffectProcessor.OnTick`은 `Mathf.FloorToInt(MaxHealth × 0.02f × Magnitude)`로 계산한다(`StatusEffectProcessor.cs:58`). float로 0.02f×2.5f×100f = 4.9999… → `FloorToInt = 4` → 기대 5와 불일치. 같은 식을 `RoundToInt`로 하면 5가 된다. 즉 이 테스트는 **프로세서의 반올림 방식에 의존하는 경계값 테스트**라 어느 쪽이든 취약하다.

원인 2 — **테스트가 프로덕션에서 선택되지 않는 프로세서를 검증(구조적 불일치)**: 프로덕션은 `EffectSystem`에 Terrain → **DotEffectProcessor**(Burn 0.02/Ignite 0.03/Poison 0.02 사전) → Hot → **StatusEffectProcessor** → ElementalAura 순으로 등록하고(`ActionRuntimeController.cs:739-750`), 틱 처리기는 **첫 번째로 CanProcess를 통과한 프로세서만** 실행한다(`EffectSystem.cs:391-400` — `return processor`). 따라서:

| 구분 | 프로덕션(실제로 도달) | 테스트 대상 StatusEffectProcessor |
|---|---|---|
| Poison 도트 | DotEffectProcessor: `RoundToInt(MaxHP×0.02×max(1,스택)×Magnitude)`, `DamageType.Magical`, **IsTrueDamage=false**(`EffectMutationFactory` 기본값), 스택 스케일 적용 | `FloorToInt(MaxHP×0.02×Magnitude)`, Magical, **IsTrueDamage=true**, 스택 스케일 없음 |
| Burn/Ignite | DotEffectProcessor | 등록돼 있으나 항상 Dot이 먼저 선택되어 **도달 불가** |
| Bleed/Wound | 어느 쪽도 틱 피해 없음(StatusEffectProcessor가 Empty 반환 — 테스트 187-202는 이 덕분에 통과) | 도달 가능하나 항상 Empty |

**판정:** 테스트가 어설션하는 "Poison=TrueDamage·Floor·무스택 스케일" 규칙은 현재 프로덕션 규칙이 아니다. 프로덕션 규칙이 GDD에 부합하는지(TrueDamage 여부·스택 스케일·반올림)는 **GDD 근거 확인이 먼저**인 기획 미확정 사항이다. 근거 없이 테스트나 구현 중 한쪽을 고치는 것은 금지(목표 지시)에 따라 수행하지 않았다.

## 2. 상태 수명주기 항목별 결과

| 항목 | 결과 | 근거 |
|---|---|---|
| 적용/스택 증가/갱신/교체 | 순수 규칙은 EffectRuntimeState 스택 정책으로 구현, 테스트 `EffectState_StackCount_IsClampedByMaxStackCap` 등이 커버(스테이징 실행에서 통과) | StatusEffectRegressionTests 1-130행 |
| 만료 | EffectExpirationQueue/EffectTimingPipeline이 담당. 정적 확인까지만 — **같은 틱 안에서 갱신과 만료의 정확한 순서는 미검증 가설**(해당 파이프라인 정밀 추적 미실시) | Core/Effects/ |
| **승급 시 상태 제거** | **결함 후보(구조적 근거, Play 미검증)** — 승급 루틴이 `StatusEffects.RemoveStatusEffect(type)`을 전 유형 호출하지(`UnitBrain.cs:406-412`) 이 메서드는 **구식 로컬 사전 `_activeEffects`만 정리**한다(`StatusEffectComponent.cs:75-81`). 반면 `HasStatus`는 이미 **EffectSystem 런타임**을 읽도록 이전됐다(`StatusEffectComponent.cs:169-177` — `SimulationRuntime.TryGetEffect`). 즉 승급 후에도 런타임 효과(도트·행동 제한의 근거)가 유지되어 GDD "모든 디버프가 해제"(Part 3 34행)와 불일치한다. 런타임 효과를 외부에서 제거하는 API는 발견되지 않았다(유일한 단서는 `UnitStatModifierComponent.cs:225`의 TODO 주석) | 두 저장소의 읽기/쓰기 경로 대조 |
| Runtime vs 컴포넌트 일치 | **이중 저장소 상태** — 컴포넌트 `_activeEffects`(구식, 일부 잔존 경로)와 `SimulationRuntime` 효과(정식). 읽기(HasStatus/CanMove/CanAttack)는 런타임 기반이지만 쓰기(제거)는 컴포넌트 기반이라 불대칭. 행동 제한 플래그는 `SyncRuntimeState`가 컴포넌트 CanMove/CanAttack을 런타임 플래그로 복사(`ActionRuntimeController.cs:588-593`) — 컴포넌트가 런타임을 읽으므로 순환은 닫혀 있으나 제거 누락이 그대로 반영됨 | 위 경로 |
| UI 표시 vs 행동 제한 | 둘 다 런타임 기반 HasStatus/CanMove로 수렴 — 정적으로는 일치. 실측(UI가 상태를 남기거나 늦게 지우는지)은 미검증 | UnitStatusBar/StatusEffectComponent 경로 |
| 사망 후 DOT | **코드상 안전** — DotEffectProcessor가 대상 사망 시 Empty 반환(`DotEffectProcessor.cs:36`), StatusEffectProcessor도 `!unit.IsDead` 게이트(`:44`). Play 실측은 미검증 | 프로세서 게이트 |
| 같은 틱 갱신과 만료 | 미검증 가설(§만료 행). EffectTimingOrderComparer가 순서 규칙을 갖는 것까지 확인 — 규칙 내용과 실측은 후속 확인 필요 | Core/Effects/EffectTiming* |
| C02 경계(승급 상태 제거 vs 18번 AP) | 승급 AP 회복(`UnitBrain.cs:414-422`)은 상태 제거(`:406-412`)와 별개 블록 — 상태 정합성 작업이 18번 AP 규칙으로 번질 경로 없음(D18은 조사 전용 유지) | UnitBrain 승급 루틴 |

## 3. 확인된 결함 / 문서 충돌 / 미검증 가설 구분

**확인된 결함(정적 근거, 수정은 별도 작업서+소유권 필요):**
1. 승급 시 런타임 효과 미해제(§2 표) — 수정은 `SimulationRuntime`/`EffectSystem`에 효과 제거 API 추가 + 승급 루틴 연결이 필요(공용 파일이라 C02 작업서 소유권 요구).
2. StatusEffectProcessor의 Burn/Ignite/Poison 분기는 프로덕션에서 도달 불가한 죽은 코드(등록 순서상 DotEffectProcessor가 항상 선점). 삭제·통합은 코드 소유자 결정.

**문서 충돌:**
3. 이전 인계서(`attack-move-grace.md` §1.4)의 "플레이어 기본 공격은 능력 파이프라인" 서술은 현재 에셋과 불일치(전 유닛 `BasicAttackAbility: {fileID: 0}`) — C01 인계서(`c01-attack-grace-review-fixes.md` §1 지적 5)에서 정정 완료.

**미검증 가설(Play 실측 필요):**
4. 같은 틱 갱신/만료 순서, 승급 후 도트가 실제로 계속 들어가는지, UI 아이콘·행동 제한 실측 일치.

## 4. 검증 근거

| 항목 | 결과 | 비고 |
|---|---|---|
| 코드 정적 추적 | 완료 | §0 경로 전수 |
| 컴파일/테스트 실행 | 미실행 | 조사 전용(작업서·허용 파일 없음 — 코드·테스트 수정 안 함) |
| Play Mode | 미실행 | 규칙상 Unity 미사용 |

## 5. 메인이 다음에 할 일

1. C02 작업서에 "승급 시 런타임 효과 해제"를 결함으로 포함하고 소유권 배정(효과 제거 API 추가 위치 포함).
2. Poison 규칙(TrueDamage 여부·반올림·스택 스케일)의 GDD 근거를 확인하고 사용자 결정을 받은 뒤, 테스트를 프로덕션 프로세서(DotEffectProcessor) 대상으로 재작성하거나 구현 규칙을 확정 — 순서는 반드시 근거 확인이 먼저.
3. StatusEffectProcessor 죽은 분기와 `_activeEffects` 잔존 저장소의 정리 여부를 C02/M6에서 결정.
