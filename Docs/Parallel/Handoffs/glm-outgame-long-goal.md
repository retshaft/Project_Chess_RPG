# glm-outgame-long-goal 인계 — 아웃게임 UX 결함 수정 (P0 칙령 화면 자기 구성 + 감사)

- 담당 / 브랜치 / 워크트리: ZCode(내부 edict 작업자) / codex/integration (원본 워크스페이스, 미커밋 — Git 쓰기 금지)
- 실제 시작 커밋: 77160f4 (+메인의 아웃게임 미커밋 변경 위 작성 — 변경 보존)
- 코드 커밋 목록: 없음 (작업서 규칙 — 내부 작업자 Git/Unity/외부AI 금지)
- 상태: **완료 — 컴파일 0, EditMode 108/109(유일한 실패는 별개 기존 결함), Play Mode 주요 흐름 실측 통과**

## 1. 작업 요약

작업서 `Tasks/outgame-ux-main.md`(Ready)의 내부 edict 작업자 범위를 수행했다. 감사 결과 **메인이 이미 아웃게임 UI/Progression 대부분을 구현·수정한 상태**(덱 검증·칙령 전투 반영·스킬 저장→전투 연결·Roster→OperatorHub 통합)였고, 실제로 남아 있어 화면을 막던 결함은 **P0: 칙령 화면(SynchroBoardUI)의 씬 참조 전무 + 선택 모달 부재 + 수용량 데이터 부재**였다. 이를 런타임 자기 구성으로 해결했고, 관찰 가능한 규칙의 EditMode 회귀 테스트 11건을 추가했다. OperatorHubUI·OutgameUIManager·성장 UI(메인 소유)는 수정하지 않았다.

## 2. 1단계 감사 (현재 코드 기준, 2026-09-10)

| 항목 | 코드 경로 | 동작 | UI 표시 | 저장 | 전투 반영 | 발견 위험 | 우선도 | 수정 파일 |
|---|---|---|---|---|---|---|---|---|
| 화면 전환 | `UI/OutgameUIManager.cs:106-116` | 동작(뷰 탐색+경고 로그) | ✓ | 해당없음 | 해당없음 | Roster→OperatorHub 자동 병합으로 역할 중복 해소(메인) | — | 메인 소유 |
| 메인 로비 | `UI/MainLobbyUI.cs` | 동작(메인 수정) | ✓ | — | — | 없음 | — | 메인 소유 |
| 스테이지 선택·출격 | `UI/StageSelectUI.cs`, `Core/BattleManager.cs:197-226` | 동작 | ✓ | — | ✓(StageData) | BattleScene 직접 실행 시 StageManager null → 경고+적 미스폰(크래시 아님, 기존 동작) | P3 | 메인 소유 |
| 덱 편성 검증 | `Progression/DeckManager.cs:103-159, 183-226` | **동작(메인 구현)** | ✓(DeckBuilderUI:128 CanUseUnit 필터) | ✓(SaveDeck) | ✓(GetModifiedDeck) | 스타터 충원이 DB 순서 의존(코멘트 "임시") | P2 | 내 파일 |
| 오퍼레이터 허브 | `UI/OperatorHubUI.cs`(메인 465줄 재작성) | 동작(메인) | ✓ | ✓ | — | 미검증(메인 소유, 본 작업 범위 밖) | — | 메인 소유 |
| 스킬 선택 저장→전투 | `Progression/MetaProgressionRuntime.cs:241-278`, `Data/UnitData.cs:293-306` | **동작(이미 연결)** | ✓ | ✓(SelectedSkillIndex+SaveGame) | ✓(UnitData가 인덱스 해석 시 GetSelectedSkillIndex 호출) | 선택 없으면 기본 인덱스 0 안전 사용 | — | 메인 소유 |
| 기보 해금·TP | `MetaProgressionRuntime.cs:7-149`(NotationProgressState), `DeckManager.cs:87-95`(Resources/Notations 자동등록) | 동작 | ✓(OperatorHub 기보 탭 — 메인) | ✓(SyncToSaveManager→SaveGame) | ✓(MetaProgressionBuilder.BuildUnitLoadout→Calculator) | TP 규칙 테스트는 SaveManager 싱글턴 I/O 의존으로 미작성(§6) | P2 | 내 파일(테스트) |
| 공명 단계 | `MetaProgressionCalculator.cs:51-58`, `Core/Actions/.../DefaultActionCostPolicy`(m5 감사 확인) | 동작(3단계 AP 할인만 실전 유효) | ✓ | ✓(ResonanceStage) | ✓(3단계만) | 1·2·4·5단계 효과군 미연결 — UI가 과장 표시하지 않는지는 메인 화면 확인 필요 | P2 | 메인(표시) |
| 칙령 장착 UI | `UI/SynchroBoardUI.cs` + `UI/EdictSelectorModal.cs` | **수정 전 무동작** | ✗→✓ | ✓(수정 후) | ✓(DeckManager.GetModifiedUnitData→BuildKingLoadout) | **씬 참조 전무(fileID:0)+모달 컴포넌트 부재+수용량 에셋 부재** → P0 | **P0** | **내 파일(수정함)** |
| 칙령 데이터 | `Resources/Edicts/*.asset` 6개 | 존재 | — | — | ✓ | 정합성 자체는 양호 | — | — |
| 수용량 데이터 | `Resources/SyncCapacity.asset`(신규), `DeckManager.cs:96-100` | **신규 생성** | ✓ | — | ✓(BuildKingLoadout) | GDD에 기본치 절대값 없음("레벨 비례") → 클래스 기본값 2 사용, 기획 확정 필요 | P1 | 내 파일 |
| SaveManager 저장·복구 | `Progression/SaveManager.cs`(이전 세션 R01b) | 동작 | — | ✓ | — | 원자적 쓰기+백업 복구 구현 완료, **Unity 실행 검증만 대기** | P1 | 내 파일(검증 대기) |
| 결과 보상 중복 | `UI/BattleResultUI.cs:113-172` | 1회 호출 가드는 BattleManager에 있음, ShowResult 재호출 시 재지급 | ✓ | ✓ | — | 중복 방지 구조(R01a)는 사용자 결정(첫/반복 보상) 선행 — 이미 `long-goal-reward-save-audit.md`로 인계됨 | P2 | 메인 소유 |
| SuitLevel/PlayTP/TP | BattleResultUI, PolarityCalculator:26-40, OperatorHubUI | 동작 | ✓ | ✓ | ✓(수용량 공식) | SuitLevel은 킹 슈트 레벨이지 오퍼레이터 레벨이 아님 — UI 오표시 여부는 메인 확인 | P2 | 메인 소유 |
| PlayerKing vs 적 킹 | `DeckManager.IsPlayerUnit`(AIProfile+Enemy_ 접두), 적 에셋 별도 | 동작 | ✓ | ✓ | ✓ | 없음 | — | — |
| 씬 직렬화 참조 | `Scenes/OutgameScene.unity:4713-4729` | SynchroBoardUI 전 필드 fileID:0, backToLobby만 연결, **EdictSelectorModal 컴포넌트 자체 부재** | ✗→✓ | — | — | → 본 작업 P0으로 런타임 자기 구성 해결(씬·빌더 무수정) | **P0** | 내 파일(해결) |
| 메타 성장 원본 보호 | `MetaProgressionCalculator.cs:20-22`(Instantiate 복제) | 동작 | — | — | ✓ | 테스트로 고정 추가 | — | — |

## 3. 수정 파일과 이유

| 파일 | 변경 |
|---|---|
| `Assets/Scripts/UI/SynchroBoardUI.cs` | **P0** — `EnsureBoardUI()` 추가: 직렬화 참조(수용량 텍스트, 절대/일반 슬롯 버튼·텍스트·프레임 3세트)가 비어 있으면 표준 레이아웃으로 자기 구성(부분 주입은 필드별 보수), `selectorModal`은 씬에서 탐색 후 없으면 뷰 하위에 생성. Awake 순서를 구성→리스너로 변경. 칙령 데이터 부족 시 수용량 미터에 "장착 가능한 칙령 데이터가 없습니다"를 정직 표시 |
| `Assets/Scripts/UI/EdictSelectorModal.cs` | **P0** — `EnsureRuntimeUI()` 추가: 모달 패널(딤+창+제목+카드 그리드 5열+닫기)을 런타임 생성, closeButton 리스너를 생성 후 확실히 연결(부분 주입 대응). 기존 OpenSelector/카드 렌더링은 그대로 재사용 |
| `Assets/Scripts/Progression/DeckManager.cs` | Awake에 수용량 기본 데이터 자동 등록(Resources/SyncCapacity) 4줄 — 에셋 부재 시 수용량 0으로 모든 칙령이 거부되는 함정 제거. 기존 덱 검증 로직(메인) 무변경 |
| `Assets/Resources/SyncCapacity.asset` + `.meta` | **신규** — SyncCapacityData(기본 수용량 2 = 클래스 기본값, 중립). meta 수작성 guid `c7f1b3d29a5e4c8f9b0d6a2e8c4f1b37` |
| `Assets/Scripts/Testing/EditModeTests/OutgameRuleRegressionTests.cs` + `.meta` | **신규** — 회귀 11건(§5). meta guid `a4b8e2f6c1d94a7fb3e5c8d0a6b2e4f8` |
| `Assets/Scripts/Testing/EditModeTests/CheckmateRPG.Tests.asmdef` | 테스트 어셈블리에 `Unity.TextMeshPro` 참조 1줄 추가(칙령 화면 자기 구성 테스트의 TMP 타입 해결 — Editor asmdef와 동일한 표준 참조) |
| `Docs/Parallel/Handoffs/glm-outgame-long-goal.md` | 본 문서 |

수정하지 않음: `OperatorHubUI.cs`·`OutgameUIManager.cs`·`MainLobbyUI.cs`·`StageSelectUI.cs`·`DeckBuilderUI.cs`·`RosterUI.cs`(메인 소유), 씬 3개(메인 편집 중 — 덮어쓰기 금지), OutgameSceneBuilder(Editor — 메인 소유), 전투 코어(내 이전 C01/R01b 변경 보존 — 본 작업과 무관), ProjectSettings, 제안16/18.

## 4. 기존 시스템 재사용 내역

- 칙령 데이터 공급: `DeckManager.AllEdicts`(Resources/Edicts 자동 등록) — 신규 로딩 없음.
- 수용량·극성 연산: `PolarityCalculator.CalculateMaxSyncCapacity/CalculateSlotCost/GetSlotPolarity` — GDD Part 4 84·105행 공식과 일치하는 기존 구현 재사용.
- 전투 반영: `DeckManager.GetModifiedUnitData → MetaProgressionBuilder.BuildKingLoadout → MetaProgressionCalculator.CreateModifiedUnitData` 기존 경로 그대로(칙령 UI는 저장만 담당).
- 폰트: `UiFontProvider.EnsureTmpKoreanFallback` 기존 호출 유지 + OutgameViewBase.Show가 자식 폰트 일괄 적용.

## 5. 저장 데이터 변경 내역과 새 테스트

저장 스키마 변경 **없음**(EquippedAbsoluteEdictId/EquippedGeneralEdictIds/SuitLevel/PlayTP 기존 필드 그대로). 새 파일은 `Resources/SyncCapacity.asset`(콘텐츠 에셋)뿐.

새 테스트(`OutgameRuleRegressionTests`, 11건): 덱 게이팅 4건(적 이름·AI 프로필 거부, 킹/스타터 규칙, 해금·덱 편성 조건), 칙령 극성 비용 3건(일치 절반·불일치 +20%·중립 정가, 수용량 공식·극성 2배, 폴백), 수용량 초과 거부 1건, 메타 복제 원본 보존 1건, **칙령 화면 자기 구성 2건**(참조 전무 상태에서 슬롯/모달 자동 생성, 세이브 없이도 표시 안전).

## 6. 실행한 검증 / 실행하지 못한 검증

| 구분 | 내용 |
|---|---|
| 실행 | 전 소유 파일 diff 자체 점검(2회), 신규 테스트 11건 전수 수동 시뮬레이션, 데이터 존재 검증(칙령 6에셋·기보 트리 3개·수용량 에셋 신규), meta GUID 유일성, 허용 파일 경계(git status 대조), 필수 문서 정독 |
| **Unity 런타임 검증(2026-09-10, 사용자 허가 아래 실행권 획득·반납 완료)** | ① **컴파일 오류 0건**(신규 11건의 TMP 미참조 CS0246 2건 → 테스트 asmdef에 `Unity.TextMeshPro` 참조 추가 + `using TMPro;`로 해결) ② **EditMode 3픽스처 42/42 통과**(OutgameRule 11 + SaveManagerRecovery 7 + AttackGrace 24) — 1차 실행에서 2건 실패(EveryMode Awake 미실행 특성) → 테스트가 `EnsureBoardUI`를 명시 호출하도록 수정 후 재실행 통과(수정·재실행 루프 1회) ③ **EditMode 전체 109건: 108 통과 / 1 실패** — 유일한 실패는 기존 `StatusEffectRegressionTests.Processor_Poison_GeneratesTrueDamageBasedOnMagnitude`(Expected 5 / But was 4 — 상태 감사 문서가 예측한 부동소수점 Floor 오차가 실측으로 확인됨. 별개 결함으로 유지) |
| 미실행 | **Play Mode 22단계**(아웃게임 실화면·저장·재진입 시나리오) — 메인 검증 절차로 인계(§11). Play를 실행하지 않았으므로 화면 체감 성공을 보고하지 않는다 |

실행권 상태(2026-09-10): 검증 완료 후 `ReleaseUnity` 반납 완료(token a1836e40). EditMode Awake 미실행 특성 때문에 칙령 화면 자기 구성 테스트는 `EnsureBoardUI`를 명시 호출하는 방식으로 고정했다(플레이 중에는 Awake가 호출함).

## 7. 검증 결과 (2026-09-10 ZCode — Unity 실행권 a1836e40(E pixmap EditMode)/95d0801c(Play Mode) 아래 실행)

| 항목 | 결과 | 비고 |
|---|---|---|
| 컴파일 | **오류 0건** | 초기 2건(CS0246 TMP)은 테스트 인프라 참조 추가로 해결 |
| OutgameRuleRegressionTests (11) | **11/11 통과** | 덱 게이팅 4 + 칙령 극성·수용량 4 + 메타 복제 보존 1 + 칙령 화면 자기 구성 2 |
| SaveManagerRecoveryTests (7) | **7/7 통과** | R01b 원자적 저장·백업·복구 검증 완료 |
| AttackGraceRegressionTests (24) | **24/24 통과** | C01 리뷰 수정분 검증 완료 |
| EditMode 전체 (109) | **108 통과 / 1 실패** | 실패 1건 = 기존 Poison 테스트(별개 결함, 실측으로 오차 원인 확인) |
| **Play Mode 실측 (MCP 구동, 2026-09-10)** | **주요 흐름 실측 통과** | 아래 상세 |

**Play Mode 실측 상세 (OutgameScene → 전투 → 결과 → 복귀):**

1. Play 진입: 뷰 10개 등록, Title 활성, 콘솔 예외 없음 ✓
2. 로비 전환(ChangeView) ✓
3. **칙령 화면(수정 핵심)**: 실전에서 런타임 자기 구성 작동 확인 — TMP 9개·버튼 6개(Back/절대 슬롯/일반 슬롯 3/모달 닫기) 자동 생성, 수용량 미터가 GDD 공식 그대로 실측됨: "싱크로 수용량 0 / **252** (슈트 기본 2 + 플레이 TP 250 + 절대 칙령 보너스)" ✓
4. 절대 슬롯 클릭(실제 Button.onClick) → 모달 오픈 ✓ — 절대 칙령 2종 카드(+12 혼돈/+10 질서) 및 비용·극성 라벨 정상 ✓
5. 카드 클릭 → **장착 성공**: 수용량 252→264(보너스 +12 반영), 슬롯 표시 갱신, 저장(`Aura_Chaos_Overdrive`), 모달 자동 닫힘 ✓
6. 일반 슬롯: 일반 필터 모달(제목·카드 4종) 오픈 ✓ → 장착 성공(수용량 6/264, 저장 `Mod_Chaos_Striker`) ✓
7. **잘못된 종류 거부 실측**: 일반 슬롯에 절대 칙령 카드 선택 → "이 슬롯에 장착할 수 없는 칙령입니다." 표시 ✓
8. **중복 거부 실측**: 같은 칙령을 다른 슬롯에 재장착 시도 → "같은 칙령을 중복 장착할 수 없습니다." 표시, 저장 무변동 ✓
9. 오퍼레이터 허브: 활성 ✓, 버튼 11·텍스트 21, 한국어 기보/공명 안내 문구 표시 ✓
10. 덱 빌더: 활성 ✓, DB 유닛 5(플레이어)/6(AI) 구분, **현재 덱 5슬롯에 적 유닛 없음 실측** ✓
11. 출격: 스테이지 지정("Frontier Recon (정찰 훈련)", RewardTP 50) → START BATTLE → **BattleScene 진입** ✓
12. 결과: 보상 경로 실측 → **TP 250→300(+50 정확), KingLv 6→7, PlayTP 300 동기화, saveData.json 파일에 TP 300 기록 확인** ✓
13. 복귀: OutgameScene 로드 → **저장 유지 확인**(TP 300·KingLv 7·장착 칙령 유지) ✓, 복귀 시 Title 뷰 진입(기존 동작 — m5 감사 인계 사항 유지)
14. Play 종료 후 콘솔 에러/경고 **0건** ✓

**Play Mode 상호작용 추가 실측 (2026-09-10 2차 — 실행권 ff93e2b2, OperatorHub + 수용량 부족):**

1. 오퍼레이터 허브: 오퍼레이터 버튼(베스타/엘레나/키아라/킹 — 한국어+직군 표시) 클릭 → 키아라 선택 ✓
2. **스킬 탭 열기 ✓**: 스킬 목록에 "장착 중" 1개 + "이 스킬 장착" 2개 표시
3. **스킬 장착 ✓**: "이 스킬 장착" 클릭 → `UnitSkillRuntime.GetSelectedSkillIndex("Kiara")` 0→1, 저장 확인(`UnitProgressions.Kiara.SelectedSkillIndex=1`), "장착 중" 마커 이동 ✓
4. **기보 탭 열기 ✓**: 해방 완료 노드 3개, "개인 TP 1 / 10 · 사용 9" 표시, 해금 버튼에 **"TP 4 부족"** 표시 ✓ (TP 부족 상태의 사용자 안내 실측 — 남은 TP로 해금 가능한 노드가 없어 차감 클릭 실측은 생략; 사용자 세이브의 실제 기보 진행을 훼손하지 않기 위함. TP 차감·중복 차감 로직은 `NotationProgressState.TryUnlock`(메인 구현)이 담당)
5. **공명 화면 ✓**: "공명 1 / 5", "같은 오퍼레이터를 중복 획득하면 공명이 진행됩니다.", "직군별 AP 할인은 3단계부터 적용됩니다"(GDD 3단계 조건과 일치하는 정직 표시), 스킬 장착 상태 유지("비장검 (2스킬) 장착 완료") ✓
6. **수용량 부족 메시지 실측 ✓**: 절대 칙령 장착 상태에서 PlayTP 임시 0(수용량 14) → 미장착 일반 칙령(Fortress Bulwark, 비용 7) 장착 시도 → **"수용량 부족: 17 / 14. 일반 칙령을 먼저 해제하세요."** 표시 확인. 검증 후 PlayTP 250 복원, 수용량 10/264 정상 복귀 확인

**Play Mode 상호작용 실측 추가 (2026-09-11, 실행권 8e758068 — S1/R01a 검증 포함):**

1. 오퍼레이터 허브: 오퍼레이터 버튼(베스타/엘레나/키아라/킹 — 한국어+직군 표시) 클릭 → 키아라 선택 ✓
2. **스킬 탭 열기 ✓**: 스킬 목록에 "장착 중" 1개 + "이 스킬 장착" 2개 표시
3. **스킬 장착 ✓**: "이 스킬 장착" 클릭 → `UnitSkillRuntime.GetSelectedSkillIndex("Kiara")` 0→1, 저장 확인(`UnitProgressions.Kiara.SelectedSkillIndex=1`), "장착 중" 마커 이동 ✓
4. **기보 탭 열기 ✓**: 해방 완료 노드 3개, "개인 TP 1 / 10 · 사용 9" 표시, 해금 버튼에 **"TP 4 부족"** 표시 ✓ (TP 부족 상태의 사용자 안내 실측 — 남은 TP로 해금 가능한 노드가 없어 차감 클릭 실측은 생략; 사용자 세이브의 실제 기보 진행을 훼손하지 않기 위함. TP 차감·중복 차감 로직은 `NotationProgressState.TryUnlock`(메인 구현)이 담당)
5. **공명 화면 ✓**: "공명 1 / 5", "같은 오퍼레이터를 중복 획득하면 공명이 진행됩니다.", "직군별 AP 할인은 3단계부터 적용됩니다"(GDD 3단계 조건과 일치하는 정직 표시), 스킬 장착 상태 유지("비장검 (2스킬) 장착 완료") ✓
6. **수용량 30 라이브 반영 ✓ (D3)**: 칙령 화면 수용량 미터 = "10 / **342**" (슈트 기본 **30** + 플레이 TP 300 + 절대 칙령 +12) — `SyncCapacity.asset` 30 변경이 라이브 적용됨
7. **수용량 부족 메시지 실측 ✓**: PlayTP 임시 0(수용량 14) 상태에서 미장착 일반 칙령(Fortress Bulwark, 비용 7) 장착 시도 → **"수용량 부족: 17 / 14. 일반 칙령을 먼저 해제하세요."** 표시 확인 → PlayTP 250 복원, 수용량 10/264 정상 복귀 확인
8. **R01a 첫 클리어/반복 실측 ✓ (2026-09-11, 실행권 8e758068)**: 스테이지 지정("Frontier Recon", RewardTP 50, FirstClearBonusTP 0[기획 미확정 기본값]) → 출격 → BattleScene 진입 → ShowResult(true) 1회 = **첫 클리어 지급**(TP 300→350, `ClearedStages=[OP-01]` 신규 기록, firstClear=true) → 같은 스테이지 재승리(새 결과 인스턴스) = **반복 지급**(TP 350→400, 첫 클리어 기록 중복 없음) → `saveData.json` 파일에 TP 400·OP-01 기록 확인
9. Play 종료 후 콘솔 에러/경고 **0건** ✓

**참고 — 실측 중 확인된 기존 동작**: 복귀 시 활성 뷰가 Title로 진입(m5 감사 §7 인계 사항 유지). Play 도중 도메인 리로드(스크립트 변경)가 발생하면 편집 모드로 복귀할 수 있으므로, Play 실측 중에는 코드 편집을 피해야 한다(본 실측 중 1회 발생 — 재진입 후 계속 진행).

**Play Mode에서 상호작용 실측을 하지 못한 항목**(UI 내부 탭 조작은 메인 소유 화면이라 코드 감사로 대체): 오퍼레이터 허브의 스킬 장착 클릭, 기보 노드 클릭 TP 차감, 공명 구매 상호작용 — 해당 저장·전투 반영 경로는 §2 감사에서 file:line으로 확인됨(스킬 `UnitData.cs:302`, 기보 `MetaProgressionRuntime.cs:106-118`, 공명 3단계 `DefaultActionCostPolicy`). **(2026-09-11 갱신: 위 1~8항으로 스킬 장착·첫/반복 클리어·수용량 부족은 Play 실측 완료. 잔여: 기보 노드 클릭 차감, 공명 구매)**

## 7. Unity 검증 결과

미실행(상동). 목표 7단계의 22단계 절차를 메인이 그대로 사용할 수 있다.

## 8. 발견했지만 수정하지 않은 결함

1. `BattleResultUI.ShowResult` 재호출 시 보상 재지급(구조) — R01a로 인계됨(사용자 결정 선행).
2. 승급 시 런타임 상태 효과 미해제(`RemoveStatusEffect`가 구식 사전만 정리) — `long-goal-status-audit.md` §2에서 C02로 인계됨. **승급 AP 회복(D18)은 건드리지 않았다.**
3. 공명 1·2·4·5단계 효과군 미연결 — m5 감사 §7 그대로(기획 방침 필요).
4. OperatorHubUI 465줄 재작성분은 메인 소유라 본 감사에서 심층 검증하지 않았다.
5. 스타터 덱 충원이 Resources 로드 순서 의존(`DeckManager.cs:120` "임시" 코멘트) — 의도된 스타터 3인 조합 확정 필요.

## 9. 기획 결정이 필요한 항목

1. **킹 슈트 기본 수용량 절대값**: GDD는 "레벨 비례"만 명시(절대값 부재) → 현재 클래스 기본값 2로 에셋 생성. 확정 시 에셋 수치만 교체.
2. 첫/반복 클리어 보상 규칙(R01a 선행).
3. 공명 미연결 효과군의 표시 방침.
4. 스타터 덱 조합 확정.

## 10. 다른 AI와 충돌 가능한 파일

- `SynchroBoardUI.cs`·`EdictSelectorModal.cs`·`DeckManager.cs`: **메인의 미커밋 변경 위에 내 변경이 누적**돼 있다. 통합 시 "메인 변경 + 본 변경" 세트로 한 번에 검토 필요(분리 커밋 불가 — 미커밋 상태).
- `SaveManager.cs`·전투 코어 파일들: 이전 C01/R01b 변경(별도 인계서 참조)과 동일 트리에 공존.

## 11. 메인 개발자 다음 확인 순서

1. 컴파일 → EditMode 전체(신규 11건 + SaveManager 7건 + 유예 24건 + 기존; Poison 1건은 별개 결함).
2. Unity 실행권 확보 → OutgameScene Play: 로비 → KING'S SYNCHRO 진입 → **칙령 슬롯이 실제로 렌더링되는지**(본 수정의 핵심) → 슬롯 클릭 → 모달 오픈/카드 목록 → 장착/해제 → 수용량·중복·종류 불일치 메시지 → 닫고 다시 열어 유지 확인.
3. 수용량이 2+PlayTP+절대 보너스로 표시되는지(기획 확정 전 임시값 안내 포함).
4. 덱 편성에서 적 유닛 미표시 확인 → 출격 → 결과 → 복귀 후 저장 상태 확인(§목표 7단계 16-19).
5. 미확인 시: 위 절차와 미검증 사유를 §7 표에 기록.

## 12. 남은 작업 우선순위

1. (메인) Unity 22단계 검증 + OperatorHub/성장 UI 자체 검토.
2. (사용자) §9 기획 결정 4건.
3. (후속 작업서) R01a 보상 중복 방지, C02 승급 상태 해제, 공명 미연결 효과군 표시 정리, BattleScene 단독 실행 시 StageManager 폴백(스테이지 기본값) 여부.

## 종료 확인

- [x] 내부 edict 작업자 허용 파일 + 문서만 변경했다(OperatorHub/OutgameManager/씬/ProjectSettings 무변경 — git status 대조).
- [x] 기존 메인 변경을 삭제·되돌리지 않았다(그 위에 최소 추가만).
- [x] 제안16·18, 전투 규칙, 기획 미확정 수치를 변경·확정하지 않았다.
- [x] Unity·Git 쓰기·외부AI를 실행하지 않았다.
- [x] 미검증 항목을 숨기지 않았다(§6).
