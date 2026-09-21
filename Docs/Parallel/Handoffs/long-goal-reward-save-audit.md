# long-goal-reward-save-audit — 보상·저장 시스템 조사 (읽기 전용, 코드 변경 없음)

- 담당 / 브랜치 / 워크트리: ZCode / 작업서 없음(조사 전용) / 원본 워크스페이스
- 실제 시작 커밋: 77160f4 (+미커밋 C01 변경 존재, 본 조사와 무관)
- 코드 커밋 목록: 없음 — 본 문서만 산출
- 상태: **Review — 조사 완료. R01a/R01b 후속 구현은 별도 작업서 필요**

## 0. 추적한 흐름

결과 표시 → 보상 계산 → 재화·유닛·해금 반영 → 저장 → 전투 복귀

```
BattleManager 승패 판정(킹 생존/전멸, _battleEnded 가드)
  → ShowGameOverPanel → (승리 시 CheckmateCinematicUI 콜백) → BattleResultUI.ShowResult(true)
      → TP 계산(stage.RewardTP, 0이면 50 폴백)
      → saveData.TacticalPoints += TP, KingProgression.PlayTP += TP, SuitLevel++   (무조건)
      → ResonanceSystem.AddUnitResonance(reward.name)  (ClearRewards 각 항목 — 메모리만 변경)
      → SaveManager.SaveGame()  (saveData.json 전체 덮어쓰기)
  → Restart 버튼 = BattleScene 재로드 / Back to Map = OutgameScene 로드
```

확인한 경로: `Assets/Scripts/UI/BattleResultUI.cs:113-172`, `Assets/Scripts/Core/BattleManager.cs:898-975`(승패 판정·가드), `Assets/Scripts/Progression/SaveManager.cs`(전체), `Assets/Scripts/Progression/ResonanceSystem.cs:170-203`, `Assets/Scripts/Testing/CampaignCombatTester.cs:116-135`.

## 1. 조사 항목별 결과 (목표의 8개 사례)

| # | 사례 | 현재 동작 | 위험도 |
|---|---|---|---|
| 1 | 결과 화면을 여러 번 열 때 중복 보상 | `ShowResult` 자체에는 지급 가드가 **없다**. 재호출 시 TP·SuitLevel++·해금이 재지급된다. 정상 전투 흐름에서는 1회만 호출되지만 API 계약상 위험 | 중(구조) |
| 2 | 동일 결과 콜백 중복 호출 | 정상 흐름은 안전 — 승패 확인 진입부가 `_battleEnded \|\| phase==Ended`로 조기 반환(`BattleManager.cs:143`), 각 분기에서 `_battleEnded=true` 선행(902/918/927/936행). 단 (a) 승리 콜백이 시네마틱 콜백 안에서 지연 실행되어 `PlayCheckmate`의 재호출·씬 언로드 타이밍에 대한 검증은 없고, (b) **`CampaignCombatTester.SimulateStageVictory`는 가드 없이 `ShowResult(true)`를 직접 호출하는 도구**라 반복 실행 시 중복 지급된다(테스트 범위) | 중 |
| 3 | 보상 지급 도중 저장 실패 | `AddUnitResonance`는 `CurrentData`(메모리)만 수정하고 저장하지 않는다. `SaveGame` 실패 시 로그만 남고 파일은 이전 상태 유지 → 세션 중에는 화면과 메모리가 일치하지만, **재시작하면 결과 화면에서 본 TP·해금이 사라진다**(사용자 입장 "지급됐는데 증발"). 실패 후 재시도 경로 없음 | 중 |
| 4 | 앱 종료 후 재실행 | `SaveManager`가 `persistentDataPath/saveData.json`을 `Awake`에서 재로드(DontDestroyOnLoad)한다. 결과 시점 저장이므로 전투 중 종료분은 미저장(의도 단순 구조). 재실행 시 결과 UI가 자동으로 다시 열리지 않아 1번·4번이 겹칠 위험은 없음 | 저 |
| 5 | 저장 파일 손상 | `LoadGame`의 catch가 **`CurrentData = new SaveData()`로 무조건 교체**한다(`SaveManager.cs:59-63`). 이후 어느 시스템이든 저장을 호출하면 손상 파일을 신규 데이터로 **덮어써 진행이 영구 소실**된다. 또한 `JsonUtility.FromJson`은 일부 형태의 잘못된 JSON에서 예외 대신 기본값 객체를 돌려줄 수 있어 예외가 없는데도 데이터가 비는 무음 손상 가능성이 있다 | **고** |
| 6 | 백업 파일 복구 | 백업·임시 파일·버전 관리 **전무**. `SaveGame`은 `File.WriteAllText` 직접 덮어쓰기라 쓰기 도중 크래시 시에도 파일이 잘린다(비원자적). 복구 경로 없음 | **고** |
| 7 | 첫 클리어 vs 반복 클리어 보상 | 구분 없음 — 매 승리마다 `stage.RewardTP`(기본 50) 전액 + `SuitLevel++` + `ClearRewards` 해금. 첫 클리어 플래그·반복 감소율 개념이 코드에 없다. 반복 도전 파밍 자체는 명일방주식 반복 도전 구조(유지 지시)와 부합하나 **보상 규모·감소 여부는 기획 미확정(사용자 결정 사항)** | 정보 |
| 8 | 정상 재도전 보상이 차단되는지 | 차단 없음 — 재도전 승리 시마다 전액 지급(7번과 동일 사실의 반대면). 차단 위험은 현재 없음 | 저 |

## 2. 후속 작업 분리 (구현은 별도 작업서로)

### R01a — 보상 중복 방지 (보상 지급의 단일화)

- **목표:** 동일한 전투 결과가 두 번 지급되지 않고, 정상 재도전 보상은 계속 지급된다. "메모리 bool만"으로 끝내지 않는다(재실행 시에도 성립해야).
- **수정 파일(예상):** `Assets/Scripts/UI/BattleResultUI.cs`(지급 로직을 Progression 쪽으로 이동·가드), `Assets/Scripts/Progression/SaveData.cs`(클리어 이력 필드 추가 — 직렬화 호환: 새 필드 추가만, 기존 필드명 불변), 신규 순수 지급 클래스 1개(작업서가 소유 결정). 테스트: `Assets/Scripts/Testing/EditModeTests/`(새 파일 — 지급 중복·재도전 시나리오).
- **설계 요건:** 지급 함수는 (스테이지 ID + 전투 결과 식별자)를 받아 저장 데이터에 기록을 남긴 뒤 지급한다. UI는 표시만 담당. 시네마틱 콜백·디버그 도구(`SimulateStageVictory`)가 같은 함수를 지나도록 한다.
- **선행 조건:** (1) 첫 클리어/반복 클리어 보상 규칙·수치의 **사용자 결정**(현재 전액 지급을 유지할지), (2) `BattleResultUI`가 프로그래밍 생성 UI라는 점을 감안한 지급-표시 분리 범위의 메인 승인.
- **검증 계획:** EditMode로 "동일 결과 2회 지급 시도 → 1회만 반영", "다른 전투(재도전) → 정상 지급", "지급 후 저장 라운드트립 → 재실행 시 중복 없음". Play Mode는 §3 절차.

### R01b — 저장 복구 (원자적 쓰기 + 백업)

- **목표:** 쓰기 도중 손상이 생기지 않고, 손상 시 최신 백업에서 복구하며, 읽기 실패를 이유로 무음 리셋하지 않는다.
- **수정 파일(예상):** `Assets/Scripts/Progression/SaveManager.cs` 단일 파일. 테스트: `Assets/Scripts/Testing/EditModeTests/SaveManagerRecoveryTests.cs`(신규).
- **설계 요건:** (a) 저장 = 임시 파일 쓰기 → 기존 파일을 `.bak`으로 이동 → 임시 파일을 본체로 이동(원자적 교체), (b) 로드 실패 시 본체 → `.bak` 순으로 시도, (c) 둘 다 실패해야 신규 데이터이며 이때도 **손상 파일을 `.corrupt`로 보존**(덮어쓰지 않음), (d) 로드된 데이터의 최소 유효성 검사(버전 필드 등 — `SaveData`에 버전 필드가 있는지 확인 필요). 기존 JSON 형식 호환 유지(JsonUtility 유지).
- **선행 조건:** 저장 경로가 `Application.persistentDataPath` 하드코딩이라 EditMode 테스트가 어렵다 → `SavePath`를 주입 가능하게 하는 최소 리팩터(동일 파일 내)를 작업서가 허용해야 한다.
- **검증 계획:** EditMode로 "정상 라운드트립", "본체 손상 → 백업 복구", "둘 다 손상 → 신규 생성 + .corrupt 보존", "저장 중단(쓰기 예외) → 기존 파일 불변". Play는 앱 강제 종료 재실행 시나리오.

## 3. 사용자가 직접 확인할 최소 절차 (구현 후)

1. 승리 → 결과 화면 확인(TP/해금 표시) → Restart로 재도전 → 승리 → 재화가 재도전 규칙대로 증가하는지.
2. 결과 화면 직후 프로세스 강제 종료 → 재실행 → TP·해금이 결과 화면과 일치하는지.
3. (R01b 구현 후) saveData.json을 임의로 망가뜨린 뒤 재실행 → 백업에서 복구되는지, 콘솔에 무음 리셋이 없는지.

## 4. 검증 근거

| 항목 | 결과 | 비고 |
|---|---|---|
| 코드 추적 | 완료 | 위 §0 경로 전수 확인(지급·저장·가드·호출자) |
| 컴파일/테스트/Play | 해당 없음/미실행 | 코드 변경 0건(조사 전용). Play 절차는 §3 |

## 5. 남은 위험

- `PlayCheckmate` 콜백의 생명주기(씬 전환 중 콜백 유실/재호출)는 Play 실측이 필요해 미확정으로 남긴다.
- `SaveData`에 버전 필드 존재 여부는 R01b 작업서 작성 시 확인 항목이다.
- 반복 클리어 보상 "규칙"은 코드에 존재하지 않으므로(전액 지급), 규칙 확정 전에 R01a를 구현하면 현재 값을 기본값으로 고정하는 실수가 나오기 쉽다 — 사용자 결정이 선행이다.
