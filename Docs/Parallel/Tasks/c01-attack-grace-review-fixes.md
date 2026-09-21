# C01 리뷰 수정 — 이동 유예의 판정 일치와 강제 이동 차단

- 상태: Ready
- 담당: ZCode / GLM-5.3-Flash
- 브랜치: codex/zcode/c01-grace-review-fixes
- 워크트리: C:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01/.worktrees/zcode-c01-grace-review-fixes
- 코드 기준: aa0637a2de839ff9bdd6de3536b19b45bc6744a1
- 실행 기준: 위 코드 기준에 이 작업서만 커밋한 HEAD. 자동 호출 JSON에 실제 전체 SHA 기록.
- 메인 리뷰: 2026-09-09. 이전 결과는 Review 유지. 새 기능보다 아래 보완 우선.

## 목표

기존 유예 구현을 재사용해 실제 전투·예측의 공격 규칙을 일치시키고, 강제 이동으로 이전 정상 이동 기록이 잘못 재사용되지 않게 한다. 계획으로 끝내지 말고 허용 범위에서 구현·회귀 테스트 작성·인계까지 수행한다.

## 리뷰 지적과 명령

1. **예측 불일치:** PredictionBattleContext는 체비셰프 거리, RuntimeBattleContext는 CombatPatternRules를 사용한다. 공통 유예 함수만 같다고 판정이 같아지지 않는다. 실전에서 사용하는 기물 종류·팀 방향·사거리 규칙을 예측에도 제공하되 클론 위치를 사용하고 실전 상태를 수정하지 말라. 비숍의 비대각선, 룩의 대각선, 나이트와 폰 방향을 포함하여 정상 판정 및 유예 판정을 비교하라. 기존 헤드리스 호출자의 호환성도 유지하라.
2. **강제 이동 뒤 오래된 기록:** MovementMutationProcessor는 정상 이동만 기록하며 넉백 때 기존 기록을 무효화하지 않는다. 정상 이동 직후 넉백된 대상이 예전 칸으로 맞거나, 공격자가 강제 이동해도 미이동으로 처리될 가능성이 있다. 실전·예측 양쪽에서 실제 위치가 바뀐 강제 이동은 이전 유예를 무효화하라. 최소한의 기록/무효화 방식으로 처리하고 클론·스냅샷·리플레이 경로를 확인하라. 정상 이동→넉백, 공격자 넉백, 실패한 이동을 회귀로 구분하라.
3. **검증 공백:** 기존 11개는 주로 순수 함수와 기록 복사 테스트다. 실제 사전검증/리졸버와 예측 컨텍스트를 거치는 테스트를 추가하라. 취소·대상 사망·이전 칸의 다른 유닛·중복 피해 방지 및 동일 틱 이동/접수 순서를 조사하고 필요한 회귀를 작성하라. 단순히 콜백에 다른 규칙을 넣는 테스트만으로 실전·예측 일치를 주장하지 말라.
4. **접수 유효성:** TryEnqueueAttack 직접 호출도 접수 시 유효한 사거리였음을 보장하는지 확인하라. UI 선검사만으로 전체 API가 안전하다고 가정하지 말라. 재현되면 허용 범위에서 최소 수정하고, 범위 밖이면 재현과 필요한 파일을 보고하라.
5. **플레이 경로 확인:** BasicAttackAbility를 가진 플레이어가 능력 경로로 우회한다는 기존 인계 내용상, 이 수정이 실제 사용자 킹 추격 문제를 해결하는지 아직 불명확하다. 기존 유닛 데이터와 입력 경로를 읽어 실제 적용 대상/비대상을 명시하라. 능력 전체에 유예를 자동 확대하지 말라.
6. **인계 정확성:** 스킬은 사거리 재검사 없음이라는 설명과 '이동하면 회피됨'이라는 Play 절차는 모순이다. 실제 능력별 경로에 맞춰 새 인계서를 작성하라. 강제 이동을 정상 이동 재현 방법으로 사용하지 말라. 기존 67/68 결과를 전체 통과나 새 결과로 보고하지 말라.

## 허용 파일 (아래만 수정)

- Assets/Scripts/Core/AttackGraceRules.cs
- Assets/Scripts/Core/ActionRuntimeController.cs
- Assets/Scripts/Core/Actions/Resolvers/AttackActionResolver.cs
- Assets/Scripts/Core/Actions/Resolvers/IBattleContext.cs
- Assets/Scripts/Core/Prediction/PredictionBattleContext.cs
- Assets/Scripts/Core/Prediction/PredictionMutationApplier.cs
- Assets/Scripts/Core/Prediction/PredictionPipeline.cs
- Assets/Scripts/Core/Runtime/IReadOnlyUnitRuntimeState.cs
- Assets/Scripts/Core/Runtime/UnitRuntimeState.cs
- Assets/Scripts/Core/Runtime/Processors/MovementMutationProcessor.cs
- Assets/Scripts/Core/Simulation/SimulationRuntime.cs
- Assets/Scripts/Testing/EditModeTests/AttackGraceRegressionTests.cs
- Docs/Parallel/Handoffs/c01-attack-grace-review-fixes.md

기존 .meta는 유지한다. 신규 파일은 위 인계서 외 추가하지 않는다. 추가 소유권이 필요하면 해당 부분을 대기하고 최소 변경안을 보고한다.

## 금지 및 완료 조건

- 원본 및 다른 워크트리, 씬·프리팹·에셋·설정, AI, 승급 AP, Poison 규칙을 수정하지 않는다. 유예 시험값 5/10틱은 확정값으로 취급하거나 조정하지 않는다.
- 자동 호출 예외: Git 명령으로 변경·커밋·브랜치 전환하지 않으며 Unity와 서브 에이전트를 실행하지 않는다. 메인이 통합과 Unity 검증을 담당한다.
- 기존 AGENTS.md·WORKFLOW.md를 먼저 읽는다. 이번 작업서가 자동 호출의 Git/Unity 실행 범위를 정한다.
- 수정 후 diff 자체 점검 및 가능한 비Unity 검증을 수행한다. 컴파일·EditMode·Play Mode 미실행이면 그대로 보고한다. 검증 불가를 이유로 테스트 작성과 코드 자체 점검을 생략하지 않는다.
- 인계서에 각 지적의 해결/미해결, 변경 파일, 재현 입력과 기대 결과, 수행한 검증, 남은 한계를 기록한다. 실행 결과는 Review이며 메인 검증 후에만 Verified다.

