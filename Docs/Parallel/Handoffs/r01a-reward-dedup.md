# r01a-reward-dedup 인계 — 보상 지급 단일 경로 + 첫 클리어/반복 구조 (구현·EditMode/Play 실측 완료)

- 담당 / 브랜치 / 워크트리: ZCode / codex/integration (원본 워크스페이스, 미커밋 — Git 쓰기 없음) / 로드맵 배치 S1
- 실제 시작 커밋: 77160f4 (미커밋 변경 위 작성 — 메인의 아웃게임·아트 변경 보존)
- 코드 커밋 목록: 없음 (원본 워크스페이스 미커밋 상태)
- 상태: **완료 — EditMode 116/116 + Play 핵심 경로 실측 + 배치 모드 전체 118/118 재검증(2026-09-12)까지 완료. 게임 내 실제 승리 풀 플레이만 메인 확인 권장**

## 1.1 검증 이력 보강 (2026-09-12)

- 2026-09-11 밤 에디터가 종료되어 S2(공명 2단계 초기 SP) 구현분의 실행 검증이 미뤄졌으나, 2026-09-12 새벽 실행권 재획득(lease 없음 → f31e5bb2) 후 **validate_build.bat 배치 컴파일 CLEAN(0 errors)** + **배치 모드 EditMode 전체 실행으로 재검증: 118/118 통과, 0 실패, 컴파일 오류 0**(증빙: 프로젝트 루트 editmode-results.xml, editmode-tests.log — 커밋 여부는 메인 결정).
- 118 = 기존 110 + BattleRewardServiceTests 6 + StatusEffect 재작성분 순증 +1 + 공명 2단계 InitSP 테스트 2건(OutgameRuleRegressionTests에 추가) 등 S1·S2 전체 반영.
- R01a Play 실측(§4)은 2026-09-11 완료 — 재실행 불필요.

## 1. 작업 요약

사용자 결정 D1(2026-09-11)을 반영한 R01a 구현. 스테이지 클리어 보상의 단일 지급 경로(BattleRewardService)를 만들어 첫 클리어/반복 클리어를 구분 기록하고, 같은 결과 화면 재호출로 보상이 두 번 지급되는 구조적 위험을 제거했다.

- **첫 클리어**: 반복 보상 + 첫 클리어 보너스(`StageData.FirstClearBonusTP`, 신규 필드 — 기본 0, **수치는 기획 결정 필요**)를 지급하고 `ClearedStages`에 스테이지 키를 기록
- **반복 클리어**: 반복 보상만 지급(첫 클리어 보너스 없음 — "이후에는 반복 보상만 수령 가능" 충족)
- **TP 임시 제공품 대응**: 지급 값은 호출자가 전달(StageData.RewardTP 등) — 화폐 교체 시 서비스 시그니처 유지

## 2. 수정 파일

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Progression/BattleRewardService.cs` | **신규** — 지급 단일 경로(순수 정적, SaveManager 싱글턴 미의존 → EditMode 테스트 가능) |
| `Assets/Scripts/Progression/SaveData.cs` | `ClearedStages` 리스트 추가(가산 — 구형 저장 파일 역직렬화 호환) |
| `Assets/Scripts/Data/StageData.cs` | `FirstClearBonusTP`(기본 0) 추가(가산) |
| `Assets/Scripts/UI/BattleResultUI.cs` | 승리 경로를 서비스 호출로 전환 + `_rewardGranted` 인스턴스 가드(같은 결과 화면 재호출 시 재지급 차단) + 첫/반복 표시 |
| `Assets/Scripts/Testing/EditModeTests/BattleRewardServiceTests.cs`(+meta guid `b2d7f4a9e6c148b09d3a7c5e1f8b6d42`) | 신규 — 회귀 6건 |

## 3. 검증 결과 (2026-09-11, 실행권 8e758068 아래)

| 항목 | 결과 |
|---|---|
| 컴파일 | **오류 0건** (중간 2건 CS0246/CS0103 — 테스트 코드 오류로 즉시 수정) |
| EditMode 전체 | **116/116 통과** (기존 110 + 신규 6) |
| 신규 6건 | 첫 클리어 지급+기록 / 반복 클리어 반복 지급·기록 무중복 / 다중 스테이지 독립 추적 / 무효 입력 거부 / 음수 보상 0 클램프 / JSON 라운드트립 — 전부 통과 |
| Play Mode | 핵심 경로 실측 완료(아래 §4): 첫 클리어 지급(첫 클리어 기록 생성) → 반복 클리어(보너스 없이 반복 지급, 기록 무중복) → 복귀 후 저장 유지 |

## 4. Play Mode 실측 (실행권 8e758068, OutgameScene → 출격 → 결과 → 복귀)

1. OutgameScene Play 진입: 뷰 10개, 콘솔 예외 없음
2. 스테이지 지정("Frontier Recon (정찰 훈련)", RewardTP 50, FirstClearBonusTP 0 — 기본값)
3. 덱 빌더 → START BATTLE(실제 버튼 경로) → **BattleScene 진입 확인**
4. 결과 경로 실측(실제 ShowResult(true) 지급 경로): **첫 클리어** — TP 300→350, `ClearedStages=["OP-01"]` 신규 기록, firstClear=true
5. 같은 스테이지 재승리(새 결과 인스턴스) → **반복 클리어** — TP 350→400, ClearedStages 변동 없음(중복 기록 없음), firstClear=false
6. `saveData.json` 파일 실측: TP 400 + OP-01 키 기록 확인
7. OutgameScene 복귀 → 저장 유지 확인(TP 400, KingLv, 칙령 장착 상태)
8. Play 종료 후 콘솔 에러/경고 **0건**

실험 조건 비고: StageData.FirstClearBonusTP가 기본 0이므로 이번 실측에서 첫/반복 TP 액수는 동일했다(구조만 검증). 기획이 보너스값을 정하면 에셋 수치만 바꾸면 된다.

## 5. 미실행 검증

- 게임 내 실제 전투를 끝까지 승리하는 풀 플레이(本 검증은 ShowResult 직접 호출로 지급·표시·저장 경로를 실측 — BattleManager 승리 판정 이후의 경로와 동일)
- OperatorHub 성장 탭 상호작용(메인 소유 화면)
- 캠페인 테스터 반복 호출 시나리오(디버그 도구 — 반복 지급은 구조상 의도됨)

## 6. 발견했지만 수정하지 않은 결함

- 복귀 시 활성 뷰가 Title로 진입(m5 감사 인계 — 메인 결정 사항)
- CampaignCombatTester.SimulateStageVictory 반복 호출 = 반복 보상 지급(디버그 도구 특성 — 문서화만)
- StatusEffectProcessor의 미선택 Burn/Ignite/Poison 분기(죽은 코드) — 프로덕션 파일 소유자 결정 필요

## 7. 기획 결정이 필요한 항목 (D1 관련)

- 첫 클리어 보너스 TP 수치(현: 0 — FirstClearBonusTP 에셋/인스펙터에서 설정)
- 반복 보상 감소율 도입 여부(현: 반복 = 전액)
- 장기적으로 TP 외 화폐 전환 시 서비스 파라미터만 교체하면 됨

## 8. 다른 AI와 충돌 가능한 파일

- `BattleResultUI.cs`: 메인 소유(아웃게임 작업서) — 본 변경은 사용자 결정 D1·플랜 승인에 근거. 메인 검토 요망
- `SaveData.cs`·`StageData.cs`: 가산 필드만 추가(직렬화 호환)
- 그 외 파일은 outgame-ux-main 작업서의 내부 edict 작업자 소유 범위와 동일

## 9. 메인 다음 확인 순서

1. 컴파일 → EditMode 116/116 재확인
2. OutgameScene Play: 실제 전투 승리까지 진행해 결과 화면에서 "첫 클리어" 표시와 ClearedStages 기록 확인 → 재도전 시 "반복 클리어" 표시 확인
3. D2(공명)·D8(스킬) 등 잔여 기획 결정은 [user-decision-request.md](user-decision-request.md) 참조

## 10. 남은 작업 우선순위

1. 메인: 미커밋 통합(integration-proposal.md 배치 참조) + OperatorHub 자체 검토
2. 사용자: D4 스타터 덱 재질문(한국어), 기획 수치(첫 클리어 보너스, 수용량 확정값)
3. 이후 배치: S2 성장 연결 완성 → S3 M5 완판 → S4 M3(스펙 승인 후)
