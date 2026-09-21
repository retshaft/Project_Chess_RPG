# attack-move-grace 인계 — 이동 직후 공격 판정 유예 (구현 완료, Play Mode 검증 대기)

- 담당 / 브랜치 / 워크트리: ZCode / `codex/zcode/c01-attack-grace` / `.worktrees/zcode-c01-attack-grace` (2026-09-09 사용자 지시로 브랜치 반출 — §8. 원본 워크스페이스에도 동일 미커밋 사본 유지)
- 실제 시작 커밋: 77160f46d11c6780484164d3167f5a7140d3135d
- 코드 커밋 목록: 브랜치에 feat 커밋 1건(§3의 14개 파일) + docs 커밋 1건(D18 보고서). SHA는 최종 응답 참조
- 상태: **Review — Play Mode 검증 대기** (컴파일·EditMode는 통과)

---

## 0. 요약

대상이 공격 해석 전에 이동해 사거리 밖이 되어 무효화되던 문제를 완화했다. **이동 전에 유효하게 시작한 공격만**, 대상의 "정상 이동 직전 위치" 기준으로 유예 틱 안에서 재판정한다. 근접(`UnitData.CombatStyle.Melee`) 5틱 / 원거리(`Range`) 10틱(시험값, 인스펙터 조정, 0이면 기존 동작). 사거리 판정만 완화하며 피해 대상·피해량·기존 기물 사거리 규칙은 변경하지 않는다. 실전·예측이 같은 규칙 함수(`Core/AttackGraceRules`)를 사용한다.

## 1. 조사 결과 (요구사항 §2 — 구현 전 기록, file:line 근거)

이하 경로는 `Assets/Scripts/` 기준.

### 1.1 근접/원거리 공격 타입의 실제 필드와 사용 위치

- **유일한 기존 구분 필드**: `Data/UnitData.cs:37-40` `enum CombatStyle { Melee, Range }`, 필드 `UnitData.cs:103` `public CombatStyle Style = CombatStyle.Melee;`
- 구현 전 소비처 0건(데이터 전용) → 본 작업에서 이 필드를 유예 틱 조회 키로 처음 사용. 새 타입 생성·이름/사거리 추측 없음.
- 투사체 시스템 전무(`Projectile`/투사체 관련 코드 0건) → 요구사항 §4 후기 적용: "투사체가 없는 원거리 공격은 기존 타격 확정 시점 기준".

### 1.2 공격 접수 → 시전 → 타격 판정 → 피해 적용 순서

1. **접수**: 입력(`BattleManager.cs:1237` 사거리 선검사) 또는 AI 입찰(`TacticalAIEvaluator.cs:63,107`) → `UnitBrain.QueueAttackAction`(`UnitBrain.cs:246-283`) → `ActionRuntimeController.TryEnqueueAttack`(`ActionRuntimeController.cs:406-431`, 생존·CanAttack 검사) → 비용 예약 → 스케줄. `startTick = 접수+1`.
2. **시전 대기**: SpeedTier별 해석 대기(`ActionCommands.cs:14-29`, Normal 14틱) — 이 창의 대상 이동이 무효화 원인.
3. **타격 판정(해석 틱)**: ①`ValidateAttackActionPreResolve`(`ActionRuntimeController.cs:957` 부근) 사거리 실패 시 `OutOfRange` 취소 ← **주 무효화 지점** ②`AttackActionResolver.cs:27` 재검사.
4. **피해**: `DamageMutation`이 항상 원래 `action.TargetId`로 적용(`AttackActionResolver.cs:47-60`) — 대상의 현재 위치와 무관.

### 1.3 이동 시작 / 논리 좌표 / 시각 이동 완료 — 차이 존재(보고 완료)

| 시점 | 실제 | 근거 |
|---|---|---|
| 이동 시작 이벤트 | 뮤테이션 적용 직후 동기 | `MovementComponent.cs:161-168` |
| **논리 좌표 확정** | **뮤테이션 커밋 틱에 즉시**(점유 동시 갱신) | `MovementComponent.cs:172-174`, `MovementMutationProcessor.cs:36,54` |
| 시각 이동 완료 | lerp `duration` 후, `MoveCompletedEvent`도 이때 | `MovementComponent.cs:186,199-215` |

**적용 기준**: 유예는 **논리 좌표 확정 틱(이동 뮤테이션 `Context.Tick`)**부터 계산. 시각 완료는 프레임 의존적이라 틱 관리 불가. 기존 Move 이벤트 페이로드에는 틱이 없어 기록은 뮤테이션 적용 경로에 둠(요구사항 §6 "기존 이동 기록 재사용").

### 1.4 기본 공격 vs 스킬 경로 차이

- `BasicAttackAbility != null`(플레이어 Kiara/Elena/Vesta)은 **능력 파이프라인** — 해석 시 사거리 재검사가 아예 없어(`AbilityActionResolver`/`AbilityExecutionPipeline` 0건) 무효화 경로 부재 → 유예 대상 아님(스킬 기존 명세 유지와 일치).
- 적 전체+BasicAttackAbility 없는 유닛은 **레거시 `AttackActionCommand`** — 무효화가 발생하는 유일 경로.
- `CombatComponent.Attack` 직접 피해 경로는 호출부 0건의 사망 코드 — 무변경.

### 1.5 실전·예측·리플레이의 거리·타깃 검사 위치

- 실전: `ValidateAttackActionPreResolve` → `RuntimeBattleContext.IsTargetInAttackRange`(`ActionRuntimeController.cs` — `SimulationRuntime` 좌표 + `CombatPatternRules.IsAttackReachable`) / 리졸버 `AttackActionResolver.cs:27`.
- 예측: `PredictionPipeline.ValidateAttackPreResolve`(`PredictionPipeline.cs`) → `PredictionBattleContext`(클론 좌표, 체비셰프 거리 — 실전과 이미 다른 기존 특성).
- 판정 원시 규칙: `CombatPatternRules`는 순수 함수 — 유예 재검사가 그대로 재사용(규칙 이중화 없음).
- 예측 클론 뮤테이션 적용은 `PredictionMutationApplier`가 직접 수행(`MovementMutationProcessor` 미경유) → 이동 기록 갱신은 양쪽에 동일 조건으로 반영(§2.2). `CreateSnapshot`은 깊은 복사 → 기록 필드는 클론·스냅샷 자동 포함.

## 2. 구현 내용

### 2.1 공유 판정 규칙 — `Core/AttackGraceRules.cs` [신규]

- `IsHitAllowed(...)`: ①현재 위치 기존 판정 우선 → ②유예 게이트(`IsGraceWindowOpen`: graceTicks>0, 기록 존재, `attackQueuedTick < 이동틱 <= currentTick`, `currentTick - 이동틱 <= graceTicks`, 공격자 접수 후 미이동) → ③**같은 기물 사거리 규칙 함수**로 대상 직전 셀 재검사(1회 이동만 소급).
- 생존·취소·인터럽트·AttackLocked는 기존 사전검증·리졸버 게이트가 그대로 담당(유예는 사거리만 완화).

### 2.2 최소 이동 기록 (기존 경로 재사용, 점유 무관)

- `UnitRuntimeState`: `_lastNormalMoveFromCell`/`_lastNormalMoveTick(-1=없음)` 필드 + 복사 생성자·`SeedBaseline` 반영 + `RecordNormalMove`(기존 Position 소유권 검증 재사용 — 새 소유권 키 없음). `IReadOnlyUnitRuntimeState`·`SimulationRuntime.ReadOnlyUnitRuntimeStateView`에도 노출.
- 갱신 조건: `MutationReason == nameof(MovementMutation)`일 때만 — 넉백("Knockback")·Grab(경유 KnockbackMutation)·순간이동(코드상 부재) 자동 제외. 실전: `MovementMutationProcessor`(moved 후). 예측: `PredictionMutationApplier.ApplyMovement`(동일 조건). 점유 사전은 건드리지 않음.
- 유닛당 1슬롯 덮어쓰기 → 여러 이동 전 소급이 구조적으로 불가. 전투 종료 시 런타임 폐기로 잔존 없음.

### 2.3 적용 지점(2곳)과 설정

- 실전 사전검증: `ValidateAttackActionPreResolve`가 `IsTargetInAttackRangeWithGrace(queued=attack.QueuedTick, current=attack.ResolveTick)`으로 교체 — 리졸버와 항상 동일 판정·동일 틱 기준.
- 리졸버: `AttackActionResolver.cs` 사거리 검사 1줄을 동일 메서드 호출로 교체.
- 예측: `PredictionPipeline`(lookup 전달, 사전검증 교체) + `PredictionBattleContext`(구현, 기존 체비셰프 규칙 유지).
- 설정: `ActionRuntimeController` SerializeField `_meleeAttackGraceTicks`(시험값 5) / `_rangedAttackGraceTicks`(시험값 10) — 인스펙터 조정, **0이면 게이트 자체를 건너뛰어 기존 동작과 완전 동일**. `CombatStyle`로 근접/원거리 틱 선택.

## 3. 변경 파일 (실제 구현 분)

| # | 파일 | 구분 |
|---|---|---|
| 1 | `Assets/Scripts/Core/AttackGraceRules.cs` | **신규** |
| 2 | `Assets/Scripts/Core/AttackGraceRules.cs.meta` | **신규** (guid `c4e9b1a72d8f4e5c9a6d0b3f8e2c7a41`, 사전 중복 확인) |
| 3 | `Assets/Scripts/Core/Runtime/UnitRuntimeState.cs` | 수정(기록 필드·복사 생성자·setter) |
| 4 | `Assets/Scripts/Core/Runtime/IReadOnlyUnitRuntimeState.cs` | 수정(읽기 2개) |
| 5 | `Assets/Scripts/Core/Simulation/SimulationRuntime.cs` | 수정(`RecordNormalMove` + 내부 View 클래스 필드) |
| 6 | `Assets/Scripts/Core/Runtime/Processors/MovementMutationProcessor.cs` | 수정(정상 이동 기록) |
| 7 | `Assets/Scripts/Core/Prediction/PredictionMutationApplier.cs` | 수정(클론 기록) |
| 8 | `Assets/Scripts/Core/Actions/Resolvers/IBattleContext.cs` | 수정(스냅샷 필드 2개 + `IsTargetInAttackRangeWithGrace`) |
| 9 | `Assets/Scripts/Core/ActionRuntimeController.cs` | 수정(설정 2개, `ResolveAttackGraceTicks`, 사전검증 교체, `RuntimeBattleContext` 구현·스냅샷 채움, 예측 룩업 전달) |
| 10 | `Assets/Scripts/Core/Actions/Resolvers/AttackActionResolver.cs` | 수정(사거리 검사 1줄 교체) |
| 11 | `Assets/Scripts/Core/Prediction/PredictionPipeline.cs` | 수정(룩업 전달, 사전검증 교체) |
| 12 | `Assets/Scripts/Core/Prediction/PredictionBattleContext.cs` | 수정(룩업 주입, 스냅샷 채움, 유예 구현) |
| 13 | `Assets/Scripts/Testing/EditModeTests/AttackGraceRegressionTests.cs` | **신규** (11개 테스트) |
| 14 | `Assets/Scripts/Testing/EditModeTests/AttackGraceRegressionTests.cs.meta` | **신규** (guid `e2b7d4f09c8a431db6e5a2c8f4d1b639`, 사전 중복 확인) |

**수정하지 않음**: `BattleManager.cs`, AI 전부, `MovementComponent.cs`(이동 로직·쿨타임), `CombatComponent.cs`, `ActionCommands.cs`(틱 공식), 능력·스킬 경로, 피해량 관련 전부, 씬·프리팹, ProjectSettings.

## 4. 시험값 (미확정 — 기획 확정값 아님)

| 구분 | 시험값 | 비고 |
|---|---|---|
| 근접 유예 | 5틱 (0.5초) | 이동 1회 분만 유효 — 과잉 추격 방지 최소값 |
| 원거리 유예 | 10틱 (1.0초) | 이격 거리만큼 실제 접근 시간 가정 |

틱 간격 100ms. 사용자 플레이테스트 질문("허무한 실패 감소 vs 억울한 피해 과다")으로 재조정 대상. 0 설정 시 기존 동작 복원(테스트로 고정).

## 5. 검증 근거

| 항목 | 결과 | 비고 |
|---|---|---|
| 컴파일 | **통과 (0 오류)** | 스테이징 복사본 배치 컴파일(스크립트+csc.rsp+NCalc 동일 구성). 원본 프로젝트는 에디터가 열려 있어 배치 진입 불가 → 에디터 포커스 시 재컴파일로 동일 확인 가능 |
| Edit Mode 테스트 | **67/68 통과** | 신규 `AttackGraceRegressionTests` **11/11 전부 통과**(유예 허용·만료 거부·접수 이전 이동 거부·0 비활성·정상 판정 우선·공격자 이동 배제·기록 없음·미래 틱 거부·기물 규칙 재사용·점유 불변+클론 전파·1회 소급). 결과 XML: 스테이징 배치 실행(2026-09-08) |
| Play Mode | **미실행** | Unity MCP 브리지 세션 불능("Session not found") + 에디터 점유로 배치 Play 불가. §6 절차로 메인/사용자 검증 필요 |

- **기존 테스트 1건 실패(사전 존재, 본 작업과 무관)**: `StatusEffectRegressionTests.Processor_Poison_GeneratesTrueDamageBasedOnMagnitude` — 본 변경 파일과 접점 0건(grep 확인). 이 스위트는 메인 Unity 검증 전 상태로 인계된 첫 실행이며, M3 감사(`Docs/Parallel/Tasks/m3-status-package-spec.md` §5-2)가 예측한 "테스트 규칙 vs 프로덕션 DOT 불일치"가 그대로 드러난 것. 처리는 메인 결정.
- 검증 중 발견·수정한 자체 컴파일 오류 3건(최종 상태 반영): 람다의 out 매개변수 캡처(CS1628), `ReadOnlyUnitRuntimeStateView` 인터페이스 구현 누락, 복사 생성자의 읽기 전용 속성 할당(CS0200).
- Unity 실행권 획득·반납 완료(token `cc53d9ba...`, 반납 확인). 검증용 스테이징 디렉터리는 삭제 완료(원본 프로젝트 외부에 생성).

## 6. 통합 후 Play Mode 검증 절차 (메인/사용자)

1. 프로젝트 열기(컴파일 0건 확인) → 정식 경로 OutgameScene → 덱 편성 → 출격 → START BATTLE.
2. **케이스 1**: 아군 선택 → Attack 모드로 인접 적 클릭(접수) → 즉시 그 적을 1칸 밀어내는 이동 유도 → 유예 안(0.5~1.0초) 해석 시 피해가 들어가는지. 콘솔에 OutOfRange 취소 로그가 사라졌는지 확인.
3. **케이스 2**: 같은 상황에서 유예보다 늦게 해석 → 현재 위치 기준 거부(기존 동작).
4. **케이스 3**: 적이 이동해 먼 곳에서 새 공격 시도 → 사거리 밖 거부(과거 위치 공격 불가).
5. **케이스 4**: 원거리(`CombatStyle.Range`) 유닛으로 2칸 거리 공격 → 대상 이동 시 유예 10틱 안 명중.
6. **케이스 5**: 스킬/범위 공격(능력 경로)은 대상 이탈 시 여전히 회피됨 확인(미적용).
7. **케이스 6**: 유닛이 이전 타일로 들어와도 피해는 원래 대상에게만 1회.
8. **케이스 7**: 연속 이동·공격 취소·대상 사망 상황에서 오래된 기록·취소 공격으로 피해 없음.
9. **케이스 8**: `_melee/_rangedAttackGraceTicks = 0` 설정 → 기존 동작 복원 확인 후 시험값 복원.
10. **케이스 9**: 같은 입력 반복 시 예측 오버레이와 실측 결과 일치.
11. 종료 후 Play Mode 종료 + 콘솔 런타임 예외 0 확인.
12. 핵심 질문: "공격을 제대로 시작했는데 이동 때문에 허무하게 실패하는 상황이 줄었는가? 이미 충분히 피했는데 맞았다는 느낌이 과하지 않은가?" → 답이 후자면 시험값(5/10틱) 축소.

## 7. 메인 확인 사항

- 충돌 가능 파일: §3의 공용 파일 10건 — 본 작업을 위해 사용자가 단독 소유권을 승인했다(2026-09-08). 통합 전 다른 워크트리의 동일 파일 변경 여부 확인 권장.
- 사전 존재 실패 1건(§5 Poison)은 본 작업 소유가 아니므로 별도 처리 바람.
- 시험값 5/10틱은 기획 미확정 — 플레이테스트 후 사용자 확정 필요.

## 8. 브랜치 반출 기록 (2026-09-09)

- **사용자 지시**(2026-09-09): 메인 개발자가 작업 중에 명령 하달이 불가하므로, 마일스톤 완성도를 계속 올리고 작업을 자신의 브랜치에 반출해 추후 검토를 받을 것.
- 반출 방식: `Team.ps1 -Action New -Agent zcode -Task c01-attack-grace -Base HEAD(=77160f4)` — 원본 체크아웃(`codex/integration`, 메인 작업 중)의 브랜치·파일은 변경하지 않았다.
- 커밋 대상: §3의 14개 파일 + `Docs/Parallel/Handoffs/D18-promotion-ap-audit.md`(docs 커밋 분리). 원본 워크스페이스의 미커밋 사본은 그대로 유지 — 메인 에디터가 로드한 프로젝트의 현재 상태가 우선이다.
- 미완료 항목: Play Mode 검증(§6). 2026-09-09 기준 Unity 실행권이 메인 세션에 점유 중이라 ZCode가 실행하지 않았고, 실행권을 강제 회수하지 않았다. 메인 통합 시 §6 절차 또는 메인 실행권 아래 재검증이 필요하다.
- 참고: §5의 EditMode 67/68 결과는 2026-09-08 스테이징 배치 기준이다. 이후 원본 트리의 변경(PC_RPAsset, Water 셰이더·머티리얼 등 시각 리소스)은 본 작업 파일과 접점이 없다.

## 종료 확인

- [x] 승인된 파일 목록(소유권 범위)만 변경했고 관련 .meta를 포함했다.
- [x] 다른 워크트리, 공유 설정, main을 수정하지 않았다.
- [x] Play Mode 종료 및 Unity 실행권 반납을 확인했다(실행권 획득·반납 완료; Play Mode 자체는 브리지 불능으로 미실행).
- [x] 미커밋 변경과 미검증 항목을 숨기지 않았다.
