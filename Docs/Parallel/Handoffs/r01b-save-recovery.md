# r01b-save-recovery 인계 — 저장 원자화·백업·복구 (구현 완료, Unity 검증 대기)

> **⚠ 메인에게 요청 (2026-09-09 ~19:00 KST, ZCode — 원자적 AcquireUnity 9회 시도 모두 실패)**
> Unity 실행권이 메인 세션(16:40 KST 획득)에 계속 있어 아래 검증을 실행하지 못했습니다. 다음 중 하나만 해 주세요:
> ① **실행권 반납** 후 ZCode 세션 재호출 → ZCode가 즉시 컴파일 + EditMode 전체(신규 7건 + 유예 24건) 실행·수정·결과 갱신·반납까지 수행합니다.
> ② 또는 실행권 보유 상태에서 **메인이 절차 대실행**: Unity Test Runner(EditMode) 전체 실행 → `SaveManagerRecoveryTests` 7건과 `AttackGraceRegressionTests` 24건 결과를 본 문서 §7에 기록(실패 시 스택 포함). 기존 `StatusEffectRegressionTests.Processor_Poison...` 1건 실패는 별개 결함입니다.
> 이 요청은 사용자 명령(2026-09-09 "검증 및 루프")의 종결을 위해 필요한 최소 해제 단계입니다.

- 담당 / 브랜치 / 워크트리: ZCode / 작업서 없음 — **사용자 직접 명령(2026-09-09 "다음 작업 판단 후 개시, 검증 및 루프")으로 개시**. 메인 승인 전까지 본 문서는 제안+구현 상태다
- 실제 시작 커밋: 77160f4 (미커밋 변경 위 작성 — Git 규칙상 커밋 없음)
- 코드 커밋 목록: 없음
- 상태: **Review — 컴파일·EditMode 미실행(Unity 실행권이 메인 세션에 점유 중, 2026-09-09 16:40 KST 획득분 확인)**

## 0. 배경과 범위

`long-goal-reward-save-audit.md` §2의 R01b를 구현했다. 목표: 쓰기 도중 손상 방지(원자적 교체), 손상 시 백업 복구, 무음 리셋 금지(손상 파일 보존). R01a(보상 중복 방지)는 첫/반복 클리어 보상 규칙의 사용자 결정이 선행이라 제외했다. 클라우드 저장·부정 방지는 추가하지 않았고, 저장 파일명·JSON 형식은 기존과 동일하다(호환 유지).

**아웃게임 씬과의 경계:** 본 작업은 `SaveManager.cs`와 새 테스트 파일만 만진다. 씬·아웃게임 UI는 건드리지 않았다 — 메인의 아웃게임 개발과 파일 충돌이 없다.

## 1. 현재 동작(수정 전) vs 기대 동작(수정 후)

| 상황 | 수정 전 | 수정 후 |
|---|---|---|
| 정상 저장 | `File.WriteAllText` 직접 덮어쓰기(쓰기 중단 시 파일 파손) | 임시 파일에 온전히 기록 → `File.Replace`로 원자적 교체, 이전 본체는 `.bak`으로 자동 보존(Replace 불가 플랫폼은 복사+이동 폴백) |
| 본체 손상 후 로드 | catch → `new SaveData()`로 무음 교체 → 다음 저장이 손상 파일을 덮어씀(진행 영구 소실) | 손상 본체를 `.corrupt`로 보존 → `.bak`에서 복구(직전 저장) |
| 본체+백업 모두 손상 | (백업 자체가 없었음) | 둘 다 `.corrupt` 보존 → null 반환 → 신규 데이터 생성(사용자 진행은 소실되나 원본이 남아 수동 복구 가능) |
| 파일 없음 | 신규 생성+저장(기존과 동일) | 동일 |
| 최소 유효성 | 없음(형태만 맞으면 수용) | 파싱 결과가 null이거나 `KingProgression`이 없으면 손상으로 처리 |

## 2. 변경 파일

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/Progression/SaveManager.cs` | 재작성. 공용 API(`SaveGame`/`LoadGame`/`ResetSave`/`CurrentData`/`EnsureInstance`) 시그니처·동작 계약 불변 — 호출부 18곳(ResonanceSystem, DeckManager, RosterUI, SynchroBoardUI, MetaProgressionRuntime, BattleResultUI) 무수정. 코어 로직을 경로 주입 `internal static`(WriteSaveAtomic/LoadSaveWithRecovery)으로 분리해 테스트 가능 |
| `Assets/Scripts/Testing/EditModeTests/SaveManagerRecoveryTests.cs` + `.meta` | 신규 — 회귀 테스트 7건(meta는 수작성, guid `9d4e7a1c8f2b4c6d8e0a3b5c7d9e1f24` — 프로젝트 내 유일 확인) |

수정하지 않음: `SaveData.cs`(직렬화 호환 — 필드 무변경), 씬·프리팹·에셋·ProjectSettings, R01a 관련 파일 전부.

## 3. 작성한 테스트 (7건 — 임시 폴더 실제 파일 I/O, 싱글턴 미사용)

1. `WriteSaveAtomic_RoundTripsData_WithoutTempLeftover` — 저장→로드 동일성, 임시 파일 잔존 없음
2. `WriteSaveAtomic_SecondSave_PushesPreviousSaveToBackup` — 본체=최신, `.bak`=직전 저장
3. `WriteSaveAtomic_FirstSave_CreatesMainWithoutBackup` — 첫 저장엔 백업 없음
4. `Load_FallsBackToBackup_WhenMainCorrupt_AndPreservesCorruptMain` — 본체 손상 → `.corrupt` 보존 + 백업 복구
5. `Load_BothFilesUnusable_ReturnsNull_AndPreservesBoth` — 복구 불가 → null + 양쪽 보존
6. `Load_NoFiles_ReturnsNull_WithoutCorruptFiles` — 파일 없음 → null, .corrupt 오생성 없음
7. `Load_ValidMain_IgnoresBackup_AndKeepsItIntact` — 정상 본체 우선, 백업 무손상

## 4. 실행한 검증 / 실행하지 못한 검증

| 구분 | 내용 |
|---|---|
| 실행 | diff 자체 점검(2회 — 구현 후 + 마커 검사 보강 후), 테스트 7건 전수 수동 시뮬레이션(각 단계의 파일 상태 추적), C01 신규 테스트 13건 데스크 체크(기하·틱 계산 수동 시뮬레이션), 공용 API 호환 확인(호출부 18곳), meta GUID 유일성 확인 |
| 미실행 | **컴파일 / EditMode(신규 7건 + 기존 전체) / Play Mode** — Unity 실행권이 메인 세션에 점유 중이라 미실행. 미실행을 통과로 보고하지 않음 |

테스트의 외부 의존 가정은 구현 쪽에서 제거했다: `TryReadSave`가 저장 형식 마커(`"TacticalPoints"`)를 확인하므로, JsonUtility가 잘못된 JSON에서 예외를 던지지 않는 환경에서도 테스트 4·5의 손상 판정이 결정적으로 동작한다.

## 5. 남은 위험

1. `File.Replace` 폴백 경로(복사+삭제+이동)는 Replace 불가 환경에서만 도달 — 실측 미커버.
2. 백업은 "직전 저장" 1세대만 유지(2세대 롤백 불가) — 최소 요건 충족, 더 깊은 히스토리는 범위 밖.
3. 저장 실패 후 자동 재시도는 없음(다음 저장 시점에 자연 재시도) — 사용자 안내 문구는 R01a와 함께 결정.
4. 본 수정은 이전 C01 수정과 같은 미커밋 트리에 쌓여 있다 — 메인 통합 시 C01 인계서와 본 문서를 함께 보고 파일 단위로 스테이징할 것.

## 6. 메인 다음 할 일

1. **(상단 요청 블록 참조)** 실행권 반납 후 ZCode 대행, 또는 메인 직접 대실행 — 컴파일 확인 → EditMode 전체 실행(`SaveManagerRecoveryTests` 7건 특히 4·5번, `AttackGraceRegressionTests` 24건) → 결과를 아래 §7에 기록.
2. 승인 시 R01b를 통합에 포함하고, R01a는 보상 규칙 사용자 결정 후 별도 작업서로.
3. 사용자 최소 확인 절차: 승리→결과 저장 → `persistentDataPath/saveData.json` 옆에 `saveData.json.bak`이 생겼는지 → 본체를 임의로 망가뜨리고 재실행 → 백업에서 복구되고 `.corrupt` 파일이 남는지.

## 7. 검증 결과 (2026-09-10 ZCode — Unity 실행권 a1836e40 아래 EditMode 실행)

| 항목 | 결과 | 비고 |
|---|---|---|
| 컴파일 | **오류 0건** | 2026-09-10 확인 |
| SaveManagerRecoveryTests (7) | **7/7 통과** | 원자적 저장 라운드트립, 이전 저장→백업, 본체 손상→백업 복구+.corrupt 보존, 복구 불가→null, 파일 없음, 정상 본체 우선 — 전부 실측 통과 |
| EditMode 전체 (109) | 108 통과 / 1 실패 | 유일한 실패는 별개 결함(기존 Poison 테스트 — 본 작업과 무관) |
| Play Mode | 미실행 | §3의 사용자 확인 절차는 메인/사용자 몫 |

→ **R01b 검증 종결.** 상태를 Review → 통합 대기로 갱신한다. 2026-09-09 기록 "미실행"은 위 실행으로 해소됐다.

