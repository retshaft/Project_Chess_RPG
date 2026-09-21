# c01-attack-grace-review-fixes 인계 — 유예 판정 일치와 강제 이동 차단 (구현 완료, Unity 검증 대기)

- 담당 / 브랜치 / 워크트리: ZCode / 작업서 명시 브랜치 `codex/zcode-c01-grace-review-fixes` / **이번 실행은 Git 금지 규칙에 따라 커밋·브랜치 전환 없이 원본 워크스페이스 미커밋 변경으로 존재** (기준 커밋 77160f4 위에 이전 C01 구현 + 본 수정 누적)
- 실제 시작 커밋: aa0637a2de839ff9bdd6de3536b19b45bc6744a1 (작업서 코드 기준 — 원본 워크스페이스의 이전 C01 변경과 동일 내용이 브랜치에 커밋돼 있음)
- 코드 커밋 목록: 없음 (작업서 §금지 — Git 명령 미사용)
- 상태: **Review — 컴파일·EditMode·Play Mode 미실행(작업서 규칙상 Unity/Git 미사용), 정적 diff 자체 점검만 수행**

## 0. 요약

기존 유예 구현을 재사용해 리뷰 지적 6건 중 4건을 코드로 해결하고(지적 1·2·3·4), 나머지 2건(지적 5·6)은 데이터·경로 확인으로 답을 확정해 문서로 정리했다. 유예 시험값(5/10틱)은 변경하지 않았다.

## 1. 리뷰 지적별 해결 상태

| # | 지적 | 결과 | 핵심 내용 |
|---|---|---|---|
| 1 | 예측 불일치(체비셰프 vs 기물 규칙) | **해결 (코드)** | 예측이 실전과 같은 기물 규칙으로 판정 |
| 2 | 강제 이동 뒤 오래된 기록 | **해결 (코드)** | 위치 변경 시 기록 무효화(실전·예측 공통) |
| 3 | 검증 공백 | **해결 (테스트 13건 추가)** | 컨텍스트·리졸버·파이프라인 경유 판정 테스트 |
| 4 | 접수 유효성 | **해결 (코드)** | TryEnqueueAttack 접수 시점 사거리 검사 추가 |
| 5 | 플레이 경로 확인 | **해결 (조사)** | 현재 전 유닛이 레거시 경로 → 유예가 실제 문제에 적용됨(§4) |
| 6 | 인계 정확성 | **해결 (본 문서)** | 스킬 경로 모순 정정, 67/68 재보고 안 함(§6) |

### 지적 1 — 예측 사거리 규칙 통일

**현재 동작(수정 전):** `PredictionBattleContext`는 기물 종류·팀 방향 무관 최대축(체비셰프) 거리로 판정. 실전 `RuntimeBattleContext`는 `CombatPatternRules.IsAttackReachable`(기물 규칙+팀 방향+경로 점유 샘플러). 유예 재검사까지 체비셰프라 비숍 대각선·룩 직선·나이트 L자·폰 방향이 실전과 예측에서 달라졌다.

**기대 동작(수정 후):** 두 컨텍스트가 **같은 공유 판정기 생성 함수** `AttackGraceRules.BuildPieceReachJudge(pieceType, isEnemy, range, isBlockedCell)`를 쓴다. 실전은 실전 점유(`SimulationRuntime`), 예측은 클론 점유(`PredictionContext.ClonedRuntime.GetUnitsAtPosition`)를 샘플러로 넘겨 클론 위치만 본다. 기물·팀 룩업이 없는 헤드리스 호출자를 위해 기존 체비셰프 폴백을 유지한다(호환성 — 기존 호출자는 `ActionRuntimeController` 1곳뿐이며 이곳은 룩업을 전달하도록 갱신).

**확인한 경로:** `Core/AttackGraceRules.cs`(신규 판정기 생성 함수), `Core/Prediction/PredictionBattleContext.cs`(룩업 2개 추가, `IsWithinAttackRange` 교체), `Core/Prediction/PredictionPipeline.cs`(생성자 선택 매개변수 2개 추가·전달), `Core/ActionRuntimeController.cs`(파이프라인 생성 시 `ResolveAttackPieceType`/`ResolveAttackIsEnemy` 전달, `RuntimeBattleContext.TryBuildAttackJudge`가 공유 함수 사용). 기물 규칙 원본: `Core/CombatPatternRules.cs:9-71`(나이트 L자 25행, 룩 직선 27-30행, 비숍 대각선 32-35행, 킹 42-43행, 폰 팀 방향 45-49행), 경로 샘플러는 중간 칸만 평가(`IsClearPath` 55-71행 — 출발/도착 셀 제외, 실전·예측 동일).

### 지적 2 — 강제 이동 뒤 오래된 기록 무효화

**현재 동작(수정 전):** 정상 이동 기록(`LastNormalMoveFromCell/Tick`)은 넉백 등 강제 이동으로 위치가 바뀌어도 남았다. 정상 이동 직후 넉백된 대상이 "직전 정상 이동 셀" 기준으로 유예 명중될 수 있었다.

**기대 동작(수정 후):** `UnitRuntimeState.SetPosition`에서 **위치가 실제로 변경될 때** 기록을 무효화한다(-1 리셋). 정상 이동은 적용 프로세서가 `SetPosition` 직후 다시 기록하므로(실전 `MovementMutationProcessor.cs:54→57`, 예측 `PredictionMutationApplier.cs:122→127` — 기존 순서 그대로) 정상 이동 유예는 유지된다. 넉백은 컴포넌트를 움직이고 런타임 위치는 동기화로 반영되므로(`ActionRuntimeController.SyncRuntimeState:597`, 틱 처리 시작 `:678`·예측 클론 직전 `:1042`의 `SyncAllRuntimeStates`), 강제 이동이 반영되는 순간 기록이 지워져 해석 전에 무효화가 닿는다. 같은 위치 재설정(동기화 중복)은 기록을 유지한다. 클론·스냅샷·리플레이: 기록 필드는 기존 복사 생성자·`CreateSnapshot`·ReadOnlyView에 이미 포함돼 있고(이전 구현) 무효화도 같은 상태 클래스 안에서 일어나므로 별도 경로 작업 없이 함께 적용된다.

**한계(보고 의무):** 실전 넉백은 틱 정보 없이 동기화로만 런타임에 반영되므로(`PhysicsMutationProcessor`는 런타임 상태가 아니라 컴포넌트를 이동시킴 — 허용 파일 밖), "공격자가 접수 후 강제 이동했다"는 사실 자체를 틱으로 감지하지는 못한다. 무효화로 공격자의 오래된 기록은 지워지고, 유예 재검사는 항상 공격자의 **현재 위치**에서 기물 규칙으로 다시 판정되므로 기하학적 안전성은 유지된다. 틱 기반 강제 이동 감지가 필요하면 `PhysicsMutationProcessor` 소유권이 필요한 후속 작업이다.

**확인한 경로:** `Core/Runtime/UnitRuntimeState.cs`(SetPosition 무효화), `SetUnitPosition` 전체 호출부 4곳(`ActionRuntimeController.cs:597` 동기화, `MovementMutationProcessor.cs:54`, `PredictionMutationApplier.cs:122`, `SimulationDivergenceTests.cs:500` — 마지막은 기록을 쓰지 않는 테스트 헬퍼라 영향 없음).

### 지적 3 — 검증 공백 (추가 테스트 13건)

기존 11건(순수 함수·기록 복사)에 더해, 실제 컨텍스트·리졸버·파이프라인을 거치는 테스트를 `AttackGraceRegressionTests.cs`에 추가했다. 콜백 교체식 테스트가 아니라 **생산 `PredictionBattleContext`(기물 룩업 주입)**·**생산 `AttackActionResolver`**·**생산 `PredictionPipeline.Execute`**를 호출한다.

| 테스트 | 검증 내용 |
|---|---|
| `Prediction_Bishop_NonDiagonalDenied_LikeRealRules_NormalAndGrace` | 비숍 비대각선 정상 판정 거부 + 이전 셀도 비대각선이면 유예 거부 + 대각선 이전 셀은 허용. `RealRulesReach`로 실전 규칙과 동일 입력 동등성 비교 |
| `Prediction_Rook_DiagonalDeniedStraightAllowed_PathBlockedLikeRealRules` | 룩 대각선 거부(체비셰프였으면 허용), 직선 허용, 경로 차단 시 클론 점유 샘플러로 거부 — 실전 규칙 동등성 동봉 |
| `Prediction_Knight_LShapeOnly_BeyondChebyshevRange` | 사거리 1 나이트가 L자(체비셰프 2)를 맞히고 대각선 인접을 못 맞힘 |
| `Prediction_Pawn_ForwardDiagonalOnly_RespectsTeamDirection` | 아군 폰 전방 대각선만, 적 폰은 반대 방향 — 양 팀 |
| `Prediction_GraceRecheck_RookUsesPieceRulesNotChebyshev` | 유예 재검사가 기물 규칙+점유 샘플러를 따름(재검사 경로 차단 시 거부) |
| `Prediction_LegacyChebyshevFallback_KeptForHeadlessCallers` | 룩업 없는 호출자의 기존 동작 유지 |
| `Grace_Denied_WhenTargetMoveTickEqualsQueuedTick` | 동일 틱 접수·이동 순서: 이동틱==접수틱 거부, +1틱 허용 |
| `ForcedMoveAfterNormalMove_InvalidatesGraceRecord_RealAndPrediction` | 위치 변경 시 기록 무효화, 동일 위치 재설정은 유지, 무효화 후 유예 거부, 정상 이동 재기록 복구 |
| `Resolver_ThroughGraceContext_HitsOriginalTargetOnly_WithUnitOnOldCell` | 생산 리졸버: 이전 칸에 다른 유닛이 있어도 피해 뮤테이션은 정확히 1건·원래 대상 ID만 |
| `Resolver_DeadTarget_FailsWithoutDamageMutations` | 대상 사망 시 해석 실패·피해 없음 |
| `Pipeline_OutOfRangeUnderPieceRules_CancelsLikeRealPreValidation` | 파이프라인: 기물 규칙 기준 OutOfRange 취소(인터럽트 1건·피해 0) |
| `Pipeline_InRangeStraightLine_ProducesSingleDamage` | 파이프라인: 사거리 내 명중 — 피해 1건·취소 0 |
| `Pipeline_GraceHit_MatchesRealRuleFromTargetMoveRecord` | 파이프라인: 대상 이동 기록 기반 유예 명중이 예측에서 동일 계산 |

취소·인터럽트·대상 교체의 "대상 ID 유지"는 리졸버가 피해를 항상 `action.TargetId`로 만든다는 구조(`AttackActionResolver.cs:48-61` — 변경 없음) + 위 테스트로 고정했다.

### 지적 4 — 접수 유효성

**확인:** `TryEnqueueAttack`(`ActionRuntimeController.cs:413-438`)은 생존·CanAttack·대상 ID 검사만 하고 사거리 검사가 없었다. UI 선검사(`BattleManager.cs:1237` → `CombatComponent.IsInRange` → `CombatPatternRules`, 점유 포함)와 AI 입찰 선검사(`TacticalAIEvaluator.cs:63` → `UnitBrain.IsAttackRange:979-985` → `CombatPatternRules`, 점유 샘플러 없음)를 우회한 직접 호출이 사거리 밖 공격을 예약할 수 있었다(해석 시 OutOfRange 취소로 피해는 없었으나 예약·취소 비용이 낭비됐다).

**최소 수정(허용 파일 내):** 접수 시 대상 런타임 상태를 동기화한 뒤 `_battleContext.IsTargetInAttackRange`(실전 기물 규칙·점유 포함)로 검증해 실패 시 경고 로그와 함께 예약을 거부한다. AI 선검사는 점유 샘플러가 없어 경로가 막힌 대상을 입찰할 수 있는데, 이제 접수에서 조기 거부된다(기존에도 해석 취소로 피해는 없었음 — 거부 시점이 앞당겨지고 예약 낭비가 제거된 것). 재현 입력: `TryEnqueueAttack(unit, outOfRangeTarget)` 직접 호출 → 수정 전: 예약 성공 후 해석 취소 / 수정 후: 즉시 false + `[TryEnqueueAttack] Admission range check failed` 로그. 자동 테스트는 UnitBrain·컴포넌트 의존으로 미작성(§5 한계).

### 지적 5 — 플레이 경로 (킹 추격 문제에 적용되는가)

**결론: 현재 데이터에서는 적용된다.** 근거:

- 분기 조건은 `UnitBrain.QueueAttackAction`의 `UnitData.BasicAttackAbility != null`(`UnitBrain.cs:261-272`)이다.
- **모든 유닛 에셋의 `BasicAttackAbility`가 null(`{fileID: 0}`)이다** — `Resources/Units/Kiara.asset:39`, `Elena.asset`, `Vesta.asset` 및 `Resources/Units/Enemies/*.asset` 전수 grep 확인(2026-09-09). 기존 인계서의 "플레이어 Kiara·Elena·Vesta는 능력 경로" 설명은 현재 에셋과 **불일치**한다(과거 유효했거나 다른 필드와 혼동 — `BasicAttackAbilityData`도 전부 null).
- 따라서 현재 **모든 기본 공격(플레이어·적 포함)은 레거시 `AttackAction` 경로**이고, "적 킹이 이동하면 플레이어가 공격 각을 잡기 어렵다" 문제의 명중 판정은 이번 유예 수정의 적용 대상이다. 스킬(`TryEnqueueAbility` — `BattleManager.cs:537,663,1705`, M2 에디터 도구)은 능력 경로로 사거리 재검사가 아예 없어(`AbilityActionResolver` 내 `AttackRange|IsAttackReachable` 0건) 유예와 무관하며, 기존 명세대로 자동 확대하지 않았다.

### 지적 6 — 인계 정확성

- 기존 인계서의 모순("스킬은 사거리 재검사 없음" vs Play 절차 5번 "능력 경로는 대상 이탈 시 여전히 회피됨")의 원인: 능력 경로에 사거리 재검사가 없으면 대상이 이동해도 **회피되지 않는다**가 옳다. 본 문서가 정정한다. 기존 절차의 "스킬은 회피됨" 항목은 폐기.
- 기존 67/68 결과는 이전 실행의 기록일 뿐이며 본 수정 후의 결과가 아니다. 본 수정으로 테스트가 24건(기존 11 + 신규 13)이 됐고 **Unity 실행 전까지 통과 여부는 미확정**이다. Poison 1건 실패는 본 작업 소유가 아니며 여전히 별도 처분 대상이다.

## 2. 변경 파일 (허용 목록 내 6개 + 테스트 1개)

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Core/AttackGraceRules.cs` | 공유 판정기 생성 함수 `BuildPieceReachJudge` 추가 |
| `Assets/Scripts/Core/Prediction/PredictionBattleContext.cs` | 기물·팀 룩업 2개 추가, 범위 판정을 공유 판정기로 교체(체비셰프 폴백 유지) |
| `Assets/Scripts/Core/Prediction/PredictionPipeline.cs` | 생성자 선택 매개변수 2개 추가·컨텍스트 전달 |
| `Assets/Scripts/Core/ActionRuntimeController.cs` | 파이프라인에 룩업 전달, `TryBuildAttackJudge`가 공유 함수 사용, `TryEnqueueAttack` 접수 사거리 검사 |
| `Assets/Scripts/Core/Runtime/UnitRuntimeState.cs` | `SetPosition` 위치 변경 시 정상 이동 기록 무효화 |
| `Assets/Scripts/Testing/EditModeTests/AttackGraceRegressionTests.cs` | 테스트 13건 추가(총 24건) |
| `Docs/Parallel/Handoffs/c01-attack-grace-review-fixes.md` | 본 문서 (신규 — 허용됨) |

수정하지 않음: `IBattleContext.cs`·`AttackActionResolver.cs`·`IReadOnlyUnitRuntimeState.cs`·`MovementMutationProcessor.cs`·`PredictionMutationApplier.cs`·`SimulationRuntime.cs`(이번 지적에서는 변경 불필요 — 기존 구현 재사용), 씬·프리팹·에셋·설정·AI·Poison 규칙 전부.

## 3. 정상 이동을 이용한 Play Mode 재현 절차 (넉백 미사용)

사전: 컴파일 0건 확인, `_meleeAttackGraceTicks=5` 유지. 콘솔 필터 `OutOfRange`.

1. 정식 경로로 전투 진입. 근접 아군 1기와 이동 쿨타임이 돌아온 적 1기를 1칸 거리에 배치한다.
2. 아군이 적을 Attack 접수(일반 공격)한다 — 이 상태의 기대: 사거리 내라 즉시 해석으로 명중(기준선).
3. 유예 재현은 "접수 이후 대상의 자발적 정상 이동"이 필요하다. 적 AI가 위협을 느끼고 이동하도록 유도한 뒤(아군 2기를 접근시켜 퇴로 판단을 만든다), 이동 쿨타임 직후에 공격을 접수해 해석 지연(1.4초=14틱) 안에 적이 이동하는 타이밍을 만든다. 기대: 이동 직후 0.5초(근접 5틱) 안의 해석은 이전 칸 기준으로 명중하고, 그 밖의 해석은 현재 위치 기준 거부(취소). 콘솔에 OutOfRange 취소가 수정 전보다 줄었는지 비교한다.
4. 대조(유예 비활성): `_meleeAttackGraceTicks=0`으로 두고 3을 반복 → 이동하면 무조건 OutOfRange 취소(기존 동작 복원). 확인 후 시험값 5로 복원.
5. 스킬: 같은 상황에서 스킬로 이동 중인 대상 공격 → 사거리 재검사가 없으므로 이동 자체로는 회피되지 않는다(회피 로그가 없어야 정상).
6. 종료 후 Play Mode 종료 + 콘솔 예외 0 확인.

주의: 적이 "이동 없이" 가만히 있으면 유예는 발동하지 않는다(기존 판정). 유예는 확률적 타이밍 문제라 확정 재현은 EditMode 테스트(§1 지적 3)가 담당하고 Play 절차는 체감·로그 검증용이다.

## 4. 검증 근거

| 항목 | 결과 | 비고 |
|---|---|---|
| diff 자체 점검 | 수행 | 전체 diff 재독, 네임스페이스·시그니처·틱 계산(해석 19틱=시작 5+Normal 14, 기록 15, 창 4≤5) 대조 |
| 컴파일 | **미실행** | 작업서 규칙상 Unity 미실행. Unity 진입 시 자동 컴파일로 확인 필요 |
| Edit Mode 테스트 | **미실행** | 신규 포함 24건 — Test Runner 실행 필요. 미통과를 통과로 보고하지 않음 |
| Play Mode | **미실행** | §3 절차로 메인 검증 필요 |
| 이전 67/68 기록 | 참고만 | 본 수정 이전 상태의 결과 — 현재 상태의 근거로 사용 금지 |

## 5. 남은 위험·한계

1. 컴파일·테스트 미실행 — 신규 13건 중 실패가 나오면 기물 규칙 기대값(특히 폰 방향·나이트)과 점유 시딩 부분이 1차 확인 지점이다.
2. 실전 경로에서 넉백 위치가 런타임에 반영되는 시점은 동기화 의존이다(§1 지적 2 한계). 틱 기반 무효화가 필요하면 `PhysicsMutationProcessor` 소유권 후속 작업.
3. 접수 거부 로그가 반복될 수 있다(AI의 점유 무시 입찰이 막힌 경로면 매 평가 주기 거부). 로그 노이즈가 보이면 M6에서 정리.
4. 예측 클론은 KnockbackMutation을 적용하지 않는다(기존 동작 — 예측 대상 행동이 이동·공격뿐이라 현재 무영향). 능력 예측을 확대하면 함께 보완 필요.
5. 유예 시험값 5/10틱은 여전히 미확정 시험값이다.

## 6. 메인 다음 할 일

1. 통합 브랜치에서 컴파일 → EditMode 24건 실행(실패 시 §5-1 확인 지점).
2. §3 Play 절차로 실전 체감·로그 검증(근접 5틱 기준 "허무한 실패 감소 vs 양여진 피해" 질문).
3. `BasicAttackAbility`가 실제로 설정되는 시점(능력 경로 전환)이 오면 지적 5의 결론을 재확인 — 그때는 능력 경로에 사거리 재검사·유예 확대를 별도 작업서로 결정해야 한다.
4. Poison 테스트 1건은 별도 처분(본 작업과 무관).
