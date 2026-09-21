# M6 안정화·회귀·로그 정리 계획 (GLM-04 산출)

- 작업: GLM-04 (Docs/Parallel/GLM_TASK_QUEUE.md "즉시 배정할 조사·설계 작업")
- 기준 커밋: 77160f4
- 성격: 문서만 작성. 코드 수정 없음. Unity 미실행.
- 이 문서의 역할: M6 착수 시(전체 Play Mode 검증 완료 후)의 회귀 세트·로그 정리 기준. 구현은 별도 배정(GLM-09 등).

## 1. 기존 자동 테스트 커버리지

| 테스트 파일(개수) | 검증 규칙 | 공백 |
|---|---|---|
| `ChessRuleRegressionTests.cs`(21+) | 6기물 이동 패턴 전수, 공격 도달, 중간 경로 차폐, 이동 쿨타임·행동 AP 비용 GDD 값 고정(`Testing/EditModeTests/ChessRuleRegressionTests.cs`) | 전투 시작/종료, AP 소진 거부 런타임 경로, 런타임 점유 충돌, 저장 |
| `PhysicsRuleRegressionTests.cs`(12) | 넉백 거리=힘−무게, 비틀거림 무게 −1, 보스 면역, Grab 축 판정, Splat 사유 보존 | 넉백 도착지에 기물이 있는 경우(빈 보드 가정) |
| `SimulationDivergenceTests.cs`(2) | 1000회 결정론 스트레스, 저널 리플레이 일치 | 실제 BattleManager/AP/입력을 통한 리플레이(순수 SimulationRuntime 레벨만) |
| `StatusEffectRegressionTests.cs`(6) | 스택 정책 4종, Burn 마법/Poison 고정 피해, Bleed/Wound 틱 피해 없음 | Stagger 계열(주석으로 EditMode 불가 명시), 상태 만료 경로, 지형 효과 |

## 2. 8영역 자동/수동 매핑과 공백

수동 기준: `Docs/DEV_PROCESS.md` §4(9항목).

| 영역 | 자동 | 수동 | 판정 |
|---|---|---|---|
| 전투 시작 | 없음 | §4 #3 | 공백 |
| 전투 종료 | 없음(승패 판정 `BattleManager.cs:900-940` 미테스트) | §4 #7 | 공백 |
| AP 소진 거부 | 없음(비용 상수만 고정) | §4 #6 | **공백(자동화 1순위)** |
| 이동 | 있음 | §4 #4(UI 측면만 수동) | 충실 |
| 점유 충돌 | 부분(이동 패턴 차폐만) | §4에 항목 없음 | 부분 공백 |
| 넉백 | 있음 | — | 충실(도착지 충돌만 공백) |
| 상태 | 있음 | — | 충실(Stagger 경로만 공백) |
| 저장 | 없음(SaveManager 테스트 0, §4에 항목 자체가 없음) | 없음 | **이중 공백** |
| 복귀 | 없음(씬 전환은 EditMode 불가) | §4 #8 | 수동 전담 |

## 3. 최소 회귀 세트 제안

**자동화 후보(EditMode로 가능한 구조인 것만 — 구현은 별도 작업):**
1. AP 소진 거부: `APManager`/`APFailFeedback`는 MonoBehaviour로 분리돼 있어 EditMode 검증 가능 — "잔액 부족 시 거부+이벤트" 고정.
2. 승패 판정: `EvaluateBattleState`의 킹 생존/전멸 분기는 순수 조건 로직 — 추출 가능하면 테스트화(구조 변경은 메인 승인 필요).
3. 런타임 점유 거부: `MovementComponent.cs:272`(목적지 점유 거부) — GridSystem 스텁 구성 시 가능.
4. 저장 직렬화: SaveData JSON 직렬화·역직렬화 왕복 테스트(SaveManager 로직 중 순수 부분).
5. 넉백 도착지 점유: `MovementMutationProcessor.ResolveMovementResolution`의 충돌 분기 — SimulationRuntime 구성으로 가능.

**수동 고정(자동화 불가·비효율):** §4 체크리스트 #3/#7/#8(씬 전환·스폰·복귀) + M5 감사의 10분 체크리스트(`Docs/Parallel/Tasks/m5-outgame-flow-audit.md` §5)를 통합해 "정식 전체 흐름 1회"를 시연 직전 실행.

**우선순위 원칙**: 자동화 1·2(AP 거부, 승패)가 M1 체크리스트의 남은 공백과 직결 — M6 착수 전 통합 검증에서 실패 재현 시 GLM-09의 첫 대상.

## 4. 로그 정리 분류

원칙: **제거보다 게이트화/기본값 변경이 정답**. 이미 게이트+기본 off인 로그는 진단 인프라로 유지한다.

### 4.1 반드시 남길 진단 로그
- `[Validation][Critical]` 런타임 불변성 감지(`ActionRuntimeController.cs:1042,1054`)
- 행동 거부 사유: `TryEnqueueMove/Attack/Reserve` 11건(`ActionRuntimeController.cs:376-420,1209-1242`), 이동 거부 9건(`MovementComponent.cs:122-291` — 점유/쿨타임 진단은 자동 테스트 공백 영역의 유일 관측점)
- 예외·폭주 방지선: `ActionScheduler.cs:124`, `EventBus.cs:43`, `ReactionSystem.cs:101,194,247`, `EffectSystem.cs:147,436`
- 초기화 오류: `UnitBrain.cs:160`, `MovementComponent.cs:80,87`, `BattleManager.cs:190,226,741`
- AP 부족 백업 경고(`APManager.cs:111` — 리스너 없을 때만 출력되는 설계)

### 4.2 기본값 false 변경 후보 (기본 true인 4종)
| 로그 | 위치 | 근거 |
|---|---|---|
| APDebugLogger `_logAPChanges/_logInsufficientAP` | `APDebugLogger.cs:10-11,64,72` | AP 자연회복까지 찍어 수 초마다 1줄 |
| APFailFeedback `_logFailures` | `APFailFeedback.cs:7,35` | 부족 시도마다 |
| BattleManager `_logQueuedInputCommands` | `BattleManager.cs:35,1283,1711` | 명령 큐잉마다 |

부착 스위치 `BattleManager._enableAPDebugLogger`(기본 true)를 false로 바꾸는 것만으로 1·2행이 함께 꺼진다 — 최소 침해 후보.

### 4.3 무게이트 노이즈원 (게이트화 선행 대상)
| 로그 | 위치 | 빈도 |
|---|---|---|
| 피해 적용 상세 | `Runtime/Processors/DamageMutationProcessor.cs:97` | **모든 피해마다(도트 포함) — 최대 노이즈원**. `BattleDiagnostics.EnableDamageDebug` 게이트로 흡수 권장 |
| `[Shatter]` | 같은 파일 :48 | 쇄빙마다(저빈도, 유지 가능) |
| `[CombatComponent]` 공격 거부/성공 | `Components/CombatComponent.cs:112,119,131,147,180` | 매 공격. 거부는 유지, 성공(:180)만 게이트화 권장 |
| SubclassComponent 스킬/패시브 | `Components/SubclassComponent.cs:89,120,138,148,156,188` | 캐스트/히트마다 |
| 입력 실패 안내를 Debug.Log로 출력 | `BattleManager.cs:1233,1239,1250,1254,1270` | 클릭마다 — UI 피드백 승격 또는 삭제 검토(사용자 피드백이라 유지도 가능, 메인 결정) |

### 4.4 유지(정보성·저빈도)
전투 시작/종료·승패(`BattleManager.cs:246,274,906-937`), 스폰/사망/승진(`UnitBrain.cs:181,318,343,368,425`), AI 설정(`AITeamCommander.cs:39`), F1~F3 시나리오 로그(이미 `BattleDiagnostics` 게이트), `[MovementDebug]`·`[DamageDebug]`·`[EventBus][Phase]`(게이트+기본 off).

## 5. 실행 순서 제안 (M6 착수 시)

1. (선행) 4.3의 DamageMutationProcessor 피해 로그 게이트화 — 자동·수동 회귀 실행 시 로그 노이즈가 원인 파악을 방해하므로 최우선. 단 이는 코드 변경이라 별도 작업 배정 필요.
2. 통합 검증 1회: §4 체크리스트 + M5 10분 체크리스트(위 "수동 고정").
3. 자동화 후보 5종 중 실패가 재현된 영역부터 GLM-09로 최소 수정(테스트 먼저, 통과시키는 최소 코드).
4. 로그 기본값 변경(4.2)은 기능 변경 아님 — 별도 소규모 작업으로 묶어 1커밋 권장.

## 6. 메인/사용자 결정 사항

- 4.3 입력 실패 안내의 행방(콘솔 유지 vs UI 피드백 승격 vs 삭제)
- 4.2 기본값 false 전환의 시점(시연 직전 일괄 vs 즉시)
- 자동화 후보 중 M6 이전에 선제 추가할 것(AP 거부·승패 판정 권장) — EditMode 테스트 추가는 코드 변경이므로 작업서 별도 발행 필요
