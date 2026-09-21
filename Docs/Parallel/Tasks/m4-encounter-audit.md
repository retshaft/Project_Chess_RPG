# M4 대표 인카운터 구조 감사 (GLM-02 산출)

- 작업: GLM-02 (Docs/Parallel/GLM_TASK_QUEUE.md "즉시 배정할 조사·설계 작업")
- 기준 커밋: 77160f4
- 성격: 문서만 작성. 코드 수정 없음. Unity 미실행.
- 이 문서의 역할: GLM-07(M4 인카운터 에디터 셋업)의 설계 근거. 배정 조건: M2 Play Mode 통과 + 본 감사 검토 후.

## 0. 핵심 결론

1. 적 수·배치·성향·AP 템포·승리 조건(2종)은 **StageData만으로 완결 구성 가능**하다 — 코어 수정 불필요.
2. 불가능한 것: 행동 순서 강제, 틱당 집행 수 제어(런타임 AddComponent라 StageData 밖), 3종째 목표, 플레이어 측 고정 배치.
3. 라이브 AI는 예측 시나리오(`AIPredictionAdapter.ExploreScenarios`)를 **소비하지 않는다** — 유닛 단위 예측 의사결정(`UnitBrain.EvaluateDecision`, `UnitBrain.cs:430-460`)은 호출부가 없는 사망 코드. "예측-실측 불일치 디버그"(MILESTONES M4)는 AI 결정 예측이 아니라 결정론 도구로 접근해야 한다.
4. GDD Part 4에 대표 인카운터 규격(유닛 수·배치·맵)이 없다 → 기존 `StageDesignerStudio` 캠페인 4스테이지가 사실상 유일한 후보 재료.

## 1. AI 의사결정 구조 현황

- 틱 = 100ms(`Core/ActionCommands.cs:14`). `AITeamCommander`가 5틱마다 평가(`Units/AITeamCommander.cs:13,119-133`), 틱당 입찰 1개만 집행(`:15,176-191`) → 적 행동이 순차 분산됨(9/7 수정 사항).
- 유닛 평가: `UnitBrain.GetBestActionBid` → `Units/TacticalAIEvaluator.cs:31-140`. 킬 밸류(King 1000/Queen 50/…/Pawn 10, `:16-29`), 체크메이트 +100000(`:187-193`), 약한 적 +50×가중치(`:201-205`), Setup Kill(낙사·벽 충돌 예측) +300×가중치(`:212-228`), AP 보존(Tactician ×2.5, `:303-323`), 킹 위험 대피 +50000/호위 +500×가중치(`:273-301`), 도발 강제(`:169-176`), 이동 쿨타임 차단(`:83-85`).
- 적 AP: `ConfigureFromStage`로 InitialEnemyAP/EnemyAPRegen/MaxEnemyAP 적용(`AITeamCommander.cs:33-40`), 회복은 `Update()`의 `Time.deltaTime` 기반(`:93-105`).
- M4 완료 기준(위험 회피·공격 우선·포지셔닝 중 2개 이상)에 대응하는 구현은 이미 존재: 킹 위험 회피(`:273-301`), 약한 적 우선(`:201-205`), 중앙 전개/킹 호위(`:258-301`).

## 2. 공개 API로 가능 vs 코어 수정 필요

| 항목 | 가능 여부 | 근거 |
|---|---|---|
| 적 유닛 수/배치 | StageData만 가능 | `Data/StageData.cs:46` EnemySpawns+GridPosition → `BattleManager.SpawnRedTeam`(`BattleManager.cs:203-222`) |
| 스폰별 성향 오버라이드 | StageData만 가능 | `StageData.cs:14-16` → 프로필 클론 후 BehaviorType 교체(`BattleManager.cs:208-216`) |
| 적 AP 템포 | StageData만 가능 | `StageData.cs:42-44` → `AITeamCommander.cs:33-40` |
| 승리 조건 | 부분 가능 | `ObjectiveType { Annihilation, KingDefeat }` 2종뿐(`StageData.cs:21-25`) |
| AI 가중치 상세 | 가능(에셋) | `UnitData.AIProfile`에 `EnemyAIProfile` SO 할당(`Data/UnitData.cs:56`) — 단 스폰 오버라이드는 BehaviorType 1개뿐 |
| 플레이어 고정 배치 | **불가(간접만)** | 덱 있으면 배치 페이즈, 없으면 1행 자동 충원(`BattleManager.cs:177-187, 260-272`) — 샌드박스식 씬 구성(`CombatSandboxSetupEditor.cs:36-46`의 `_spawnOnAwake=false`)으로만 대체 |
| 행동 순서 강제 | **불가** | 실행 순서 = 입찰 점수 정렬(`AITeamCommander.cs:174`) 결과 |
| 틱당 집행 수/평가 주기 지정 | **불가(에디터만)** | 커맨더를 BattleManager가 런타임 AddComponent(`BattleManager.cs:168`) — SerializeField(`AITeamCommander.cs:13-15`)는 StageData로 못 건드림 |
| 목표 추가(생존 n턴·호위·웨이브) | **불가** | ObjectiveType 2종 제한 — 코어 수정 필요 |

## 3. 대표 인카운터 제안 (코어 수정 0건)

**방식**: `StageDesignerStudio`가 이미 생성한 캠페인 스테이지 중 1개를 대표로 지정하거나, 동일 도구로 신규 1개를 베이크한다(`StageDesignerStudio.cs:80-100`). 신규 수치를 만들지 말고 기존 UnitData/StageData 값만 조합한다.

- **구성 예시(구조 제안 — 구성원 수는 기존 스테이지 자산 값 그대로)**: 킹 1 + 성향 3종(Berserker/Tactician/Defensive)이 각각 최소 1개씩 섞인 적 편성. 세 성향의 차이(즉흥 돌파 vs AP 보존 vs 이탈 기동)가 한 전투에서 함께 보인다(M4 완료 기준 "최소 두 가지 전술 행동"을 관측하기 좋은 최소 구성).
- **행동 순서**: 강제 불가(§2) — 대신 기대 시퀀스를 "관측 시나리오"로 문서화한다: ①킹 위협 시 대피/호위 이동 우선 ②체크메이트 가능 시 즉시 집행 ③이동 쿨타임 중 공격 입찰 전환. 검증은 순서 강제가 아니라 "이 중 2개 이상이 매 실행 관측되는지"로 한다.
- **승리 조건**: 기존 2종(KingDefeat 권장 — `_preferKingDefeatVictory` 기본값과 일치, `BattleManager.cs:36`).
- **플레이어 측**: 정식 배치 흐름(덱 편성→드래그 배치) 사용. 고정 배치가 필요한 특수 촬영용으로는 샌드박스 경로를 별도로 기록.
- **재현 조건**: 같은 덱·같은 스테이지·같은 시드로 반복 실행해 "동일한 적 첫 행동 시퀀스"가 나오는지를 재현 기준으로 삼는다(다음 절의 비결정 요인 정리 전제).

사용자/메인 결정 사항: ①캠페인 기존 스테이지 재사용 vs 신규 1스테이지 베이크 ②대표 스테이지의 성향 조합 ③목표(2종 중 택1).

## 4. 랜덤성 원천과 제거 방법

| 요인 | 상태 | 제거/우회 방법 |
|---|---|---|
| 시드 | 이미 고정(1001) | `SeededRandomProvider.cs:7,12`, `ActionRuntimeController.cs:33,186`. 인스펙터 `_simulationSeed`로 고정 유지만 확인 |
| AI 스코어 난수 | 없음(전수 평가) | `TacticalAIEvaluator`에 `UnityEngine.Random` 미사용, `Determinism/RandomUsageValidator.cs:61,77-80`가 틱마다 감사 |
| 적 AP 회복 | **프레임 레이트 의존** | `AITeamCommander.cs:103` `Time.deltaTime` 기반 → 완전 재현이 필요하면 프레임 레이트 고정(에디터 AnimationMode/고정 FPS) 또는 틱 기반 전환(코어 수정 — 권장하지 않음). 감사 도구: `DeterministicAuditLog.RecordFrameTimingDependency`(`Determinism/DeterministicAuditLog.cs:134`) |
| 동점 입찰 정렬 | **불안정 정렬** | `List.Sort`(`AITeamCommander.cs:174`) — 동점 시 실행 순서 뒤바뀜 가능. 우회: 점수가 겹치지 않는 편성으로 구성하거나 유닛 등록 순서 고정. 근본 수정은 비교자 보강(코어 수정 — GLM-07 범위 밖, 메인 결정) |
| Guid 소비 순서 | 결정론적이나 경로 의존 | `SeededRandomProvider.Shared.NextGuid`(`SeededRandomProvider.cs:65-78`) — 실행 경로가 같으면 동일. 같은 입력 시퀀스 유지가 전제 |

## 5. 예측-실측 확인 지점

- **주의**: 라이브 AI는 예측을 소비하지 않음(§0-3). `AIPredictionAdapter` 소비처는 사망 코드 내부(`UnitBrain.cs:537-559`).
- 사용 가능한 관측 도구 (모두 기존 자산):
  - `Core/Debug/RuntimeDebugOverlay.cs:34-75` — 틱, 액션 큐, 위치 예약 win/lost, 틱당 뮤테이션 수
  - `Core/Debug/DivergenceDebugPanel.cs:49-89` — 스냅샷/액션 순서/뮤테이션 시퀀스 불일치 이벤트
  - `Core/Debug/SimulationDebuggerWindow.cs:186-210` — 최신 mismatch와 검출 카운트
  - `MutationJournal`/`ActionJournal`(틱마다 기록, `ActionRuntimeController.cs:654-709`) — 실행 추적
  - EditMode: `SimulationDivergenceTests` 1000회 결정론 스트레스 — "예측-실측"의 자동화 등가물
- GLM-07에서 제공할 로그: 대표 인카운터 셋업 도구가 "적 첫 행동 시퀀스"를 저널에서 요약 출력하는 수준을 권장(코어 수정 없이 저널 읽기만).

## 6. GDD Part 4 대조

| 항목 | 판정 |
|---|---|
| 킬 밸류(King 1000/Queen 50/Rook 30/Bishop 25/Knight 20/Pawn 10) | 일치 (`TacticalAIEvaluator.cs:19-27`) |
| 성향 3종(Berserker/Tactician/Defensive) 동작 | 일치 (`:303-323`, `:233-256`) |
| 절대 조건 4종(체크메이트·위협회피·Setup Kill·도발) | 구현 (`:187-193`, `:273-301`, `:212-228`, `:169-176`) |
| "매 틱마다 모든 시나리오 시뮬레이션" | **어긋남** — 평가 5틱+틱당 1집행이고 시나리오 탐색은 사망 코드 |
| 위협회피 "폰 방어벽 구축" | **부분** — 킹 대피/호위 보너스까지만, 명시적 벽 기동 없음 |
| 성향 Aggressive/Assassin | **코드가 GDD 없이 2종 추가 보유**(`Data/EnemyAIProfile.cs:5-12`) |
| 대표 인카운터 규격 | **GDD 미정의** |
