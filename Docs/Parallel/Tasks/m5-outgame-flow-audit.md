# M5 아웃게임 흐름 감사 (GLM-03 산출)

- 작업: GLM-03 (Docs/Parallel/GLM_TASK_QUEUE.md "즉시 배정할 조사·설계 작업")
- 기준 커밋: 77160f4
- 성격: 문서만 작성. 씬·코드 수정 없음. Unity 미실행.
- 이 문서의 역할: M5 "전투 수치 반영 검증 전환"의 확인 지점 확정 + GLM-08(연결 검증 보조)의 결함 후보 목록.

## 1. 정식 흐름과 담당 클래스

```
Title ──TitleUI.OnStartClicked──▶ MainLobby ──stageSelectButton──▶ StageSelect
(UI/TitleUI.cs:22)              (UI/MainLobbyUI.cs:33)
   StageSelect: 스테이지 클릭 → 브리핑 → OnConfirmSortieClicked
     ① StageManager.SetCurrentStage (UI/StageSelectUI.cs:162)
     ② OutgameUIManager.ChangeView(DeckBuilder) (:163)
DeckBuilder: 슬롯 편성(DeckManager.SetDeckSlot, Progression/DeckManager.cs:155-186, 즉시 저장)
   START BATTLE → SceneManager.LoadScene("BattleScene") (UI/DeckBuilderUI.cs:76-80)
BattleScene: BattleManager.BootstrapBattle (Core/BattleManager.cs:163-193)
   ├─ SpawnRedTeam: StageManager.CurrentStage.EnemySpawns (:195-228)
   └─ 배치 패널: DeckManager.GetModifiedDeck() (:1452) → 드래그 배치 → START BATTLE (:237-275)
결과: EvaluateBattleState → ShowGameOverPanel (:888-969) → BattleResultUI.ShowResult
복귀: BattleResultUI "Back to Map" → LoadScene("OutgameScene") (UI/BattleResultUI.cs:189)
```

- 아웃게임 씬은 `Editor/OutgameSceneBuilder.cs:16-272`가 구성(매니저 생성 :29-40, 9개 뷰 조립 :62-264). 단일 씬 뷰 라우터 방식(`UI/OutgameUIManager.cs:104-121`).
- SaveManager/StageManager/DeckManager는 `DontDestroyOnLoad`(각 파일 Awake) → BattleScene 복귀 후에도 유지. **OutgameScene 경유가 전제** — BattleScene 직접 Play 시 StageManager 부재로 적 미스폰(`BattleManager.cs:226`).

## 2. 실제 전투 수치에 연결된 항목 (연결됨)

| 항목 | 경로 |
|---|---|
| 덱 편성 | `DeckManager.GetModifiedDeck`(Progression/DeckManager.cs:199-207) → 배치 패널(`BattleManager.cs:1452`) → 스폰. 슬롯 0은 킹 강제(`DeckManager.cs:160-168`) |
| 메타 스탯 합산 | `MetaProgressionCalculator.CreateModifiedUnitData`(Progression/MetaProgressionCalculator.cs:15-34) — 기보+공명 프로필+칙령 보너스를 `(base+flat)×(1+percent)`로 합산(:110-118) |
| 칙령(킹) 스탯 | `DeckManager.cs:220-236` → `MetaProgressionRuntime.BuildKingLoadout`(Progression/MetaProgressionRuntime.cs:187-233, SyncCapacity 초과 시 칙령 탈락 :221-228) → `MetaProgressionCalculator.cs:60-91` |
| 칙령 액티브 스킬 | `DeckManager.GetEquippedActiveEdict`(DeckManager.cs:259-283) → `UI/KingSynchroUI.cs:58-78` → 발동 시 `APManager.AddAP`(:122-125), 아군 상태 부여(:146-163) |
| **공명 3단계 AP 할인** | `ResonanceSystem.GetResonanceStage`→`GetAPDiscount`(Progression/ResonanceSystem.cs:17-70) → **`Core/Actions/DefaultActionCostPolicy.cs:57,69,90`** — 공명이 실전에 닿는 유일한 직접 경로 |
| 기보(Notation) | `OperatorHubUI`(UI/OperatorHubUI.cs:269,315) → `MetaProgressionRuntime.cs:28-118` → `DeckManager.cs:240-249` → 스탯 반영(`MetaProgressionCalculator.cs:36-49`) |
| 공명 스테이지 육성 | `RosterUI.OnEnhanceResonanceClicked`(UI/RosterUI.cs:218-244) → ResonanceStage 저장 → 위 AP 할인 경로. 공명 스테이지는 보너스 TP에도 기여(`ResonanceSystem.cs:159-164`) |

## 3. UI·데이터만 있고 전투에 무반영인 항목 (연결 안 됨)

| 항목 | 근거 |
|---|---|
| 공명 1·2·4·5단계 효과군(공격/체력 배율, 쿨감, 초기 SP, 방어, 관통, 상태 저항 등) | `ResonanceSystem.cs:75-157` — 소비처가 `Testing/MasterGDDPart4Tester.cs:82-93`뿐. 공명은 3단계 AP 할인만 실전 유효 |
| PlayTP(킹 플레이 TP) | 적립만(`BattleResultUI.cs:148`), 소비처 없음 |
| King SuitLevel | 승리당 무조건 +1(`BattleResultUI.cs:149`)·표시(`MainLobbyUI.cs:89`)뿐, SyncCapacity와 무관(`Data/SyncCapacityData.cs:8` 고정값) |
| UnlockedUnits(보유 기물 게이팅) | 기록만 되고(`ResonanceSystem.cs:176-179`) 편성 게이팅 미사용 — 미획득 기물도 편성 가능(`UI/DeckBuilderUI.cs:126-137`) |
| SlotPolarities | `Progression/SaveData.cs:22` 필드만 존재, 사용처 0건 |
| SelectedSkillIndex | 저장 누락(`MetaProgressionRuntime.cs:252-265`의 SetSelectedSkillIndex가 SaveGame 미호출) + 전투의 `UnitData.GetSkillLevel`과 연결 코드 없음(`DefaultActionCostPolicy.cs:28`) |
| NotationPuzzleScreen | 뷰 라우터(`OutgameUIManager.cs:7-20`)·빌더 어디에도 미등록 고아 뷰 |
| 적 EnemyResonanceStage | `BattleManager.cs:208`에서 ResonanceRole만 세팅, 스테이지 수치는 소비 없음 |

## 4. 저장 지속성

- 파일: `Application.persistentDataPath/saveData.json`(`SaveManager.cs:13`), Awake 로드 + `DontDestroyOnLoad`.
- 저장 항목(`SaveData.cs:26-46`): TP, UnlockedUnits, CurrentDeck(8슬롯), UnitProgressions(공명/기보/스킬 인덱스), KingProgression(SuitLevel/PlayTP/칙령/SlotPolarities).
- 저장 시점: 세이브 생성, 덱 변경(`DeckManager.cs:152`), 칙령 장착(`SynchroBoardUI.cs:113-127`), 기보 해금(`MetaProgressionRuntime.cs:129-149`), 공명 돌파(`RosterUI.cs:240`), **전투 승리**(`BattleResultUI.cs:143-153`: TP+보상+SuitLevel++). **패배 시 무저장**(사양).
- 스테이지 선택 상태는 런타임 전용(`StageManager.cs:9`) — 저장 안 됨.

## 5. 10분 확인 체크리스트 (사용자 실행용 — §4 체크리스트의 M5 보조판)

1. [0:00] OutgameScene Play → 콘솔에 세이브 로드/신규 생성 로그 확인.
2. [0:30] 로비 좌상단 King Lv/AP가 세이브값과 일치.
3. [1:00] 스테이지 목록 채워짐(Resources/Stages 비어 있으면 StageData 에셋 부재).
4. [2:00] SORTIE → 덱 편성 → 편성 즉시 저장 로그.
5. [2:30] 칙령/기보 변경 전후 덱빌더 전투력 텍스트 변화(`DeckBuilderUI.cs:152-167`) — 메타 스탯 반영 간접 확인.
6. [3:00] SynchroBoard 칙령 카드 모달 동작 확인(안 뜨면 §6-1 결함).
7. [4:00] START BATTLE → 배치 패널에 덱 기물 노출, 적 스폰 수 = 스테이지 정의.
8. [5:30] **공명 3단계 기물 이동 시 실제 차감 AP가 할인 반영값인지** 상태바/AP 패널로 확인 — 공명 실전 반영의 직접 증거.
9. [6:30] 액티브 칙령 장착 시 HUD 버튼 노출·발동(AP 증가 + 로그).
10. [8:00] 승리 → 결과 UI 리워드 텍스트 + 저장 로그 → Back to Map.
11. [9:00] 복귀 후 시작 뷰 확인(현 사양은 Title로 리셋 — §6-2).
12. [9:30] 패배 시 저장 로그·TP 변동 없음 확인(사양).

## 6. 연결 결함 후보 (GLM-08 배정 심사 대상 — 우선순위 순)

1. **SynchroBoardUI 필드 미주입**: `OutgameSceneBuilder.cs:143-147`은 backToLobbyButton만 주입 — selectorModal/슬롯 버튼/capacityMeterText가 null이면 칙령 장착이 조용히 무동작(`SynchroBoardUI.cs:59-65` null 가드). 칙령이 M5 시연 핵심이라 최우선 확인.
2. **복귀가 로비가 아닌 Title로 리셋**: `OutgameUIManager.Start`가 항상 ChangeView(Title)(`OutgameUIManager.cs:93-102`) — "Back to Map" 라벨과 불일치.
3. **UnitSkillRuntime 스킬 선택 미저장·미반영**(§3 표) — 저장 유실 + 전투 무반영 이중 결함.
4. **스테이지 미선택 상태 전투 진입 가능**: `DeckBuilderUI.cs:76-80`이 CurrentStage 미검증 → 적 0스폰 전투.
5. **DefaultResonanceProfile/DefaultSyncCapacity 자동 바인딩 부재**: `DeckManager.cs:18-21` Inspector 수동 할당 의존 — 누락 시 칙령 전체 탈락(경고 로그만).
6. **GetModifiedUnitData가 호출마다 ScriptableObject 생성 후 미파괴**(`DeckManager.cs:252`, `MetaProgressionCalculator.cs:20`) — 덱빌더 리프레시마다 누적, 메모리 누수 후보.
7. UnlockedUnits 게이팅 부재(§3 표).
8. BattleResultUI를 매 전투 신규 GameObject로 생성(`BattleManager.cs:960-969`) — 사소.
9. NotationPuzzleScreen 고아 뷰, SlotPolarities 데드 필드 — 정리 대상(M6 연계).

## 7. M5 판정에 필요한 사용자 결정 사항

- 복귀 목적지를 Title로 할지 로비로 할지(§6-2) — 라벨 수정 vs 동작 수정.
- 공명 미연결 효과군을 시연 전에 끄는가(UI 문구 정리) vs 최소 1개라도 연결하는가(GLM-08 범위) — MILESTONES M5 "최소 기능만 선택해 반영" 원칙상 사용자 결정.
- 패배 무저장 사양 유지 여부.
