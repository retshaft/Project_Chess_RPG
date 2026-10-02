# 수중 유적 모듈 — 빠른 재사용 안내

2026-10-02. BattleScene의 `Combat Ruins - Submerged District`에 모듈 40개(렌더러 368개)를 추가했다. 기존 회랑 남서쪽 끝에서 출발해 남쪽·서쪽·동쪽·북쪽 외곽으로 확장한다. 수면 y=-10.2, 수중 바닥 y=-13.2/-16.2. 기존 오브젝트와 원본 프리팹은 보존했다.

## 다음 작업에서는 이 문서부터 읽기

프리팹 위치: `Assets/Prefabs/RuinsBackgrounds/Modules/`

| 모듈 | 용도와 접속점 (로컬 좌표) |
|---|---|
| Ruins_Terrace_8m | 8.8×8.8 광장, 원점이 바닥 중앙, 각 변 접속점 ±4.4m |
| Ruins_Gallery_12m | 난간·받침 포함 회랑, 폭 3.98m, z=-6~+6m, 원점이 바닥 중앙 |
| Ruins_DescendingStairs_6m | 계단 상단 (0,0,-3), 하단 (0,-3,3), yaw로 진행 방향 지정 |

위 세 이름과 위치·Y 회전만 지정하면 반복 배치를 설명할 수 있다. 기존 RuinsBackgrounds 원본을 중첩 프리팹으로 유지하므로 재질과 메시 연결이 살아 있다. 모듈 수정은 연결된 모든 인스턴스에 반영되며, 특정 장소만 수정하려면 씬 인스턴스에서 조정한다.

- 신규 배치 코드: `Assets/Scripts/Editor/StylizedBackground/CombatRuinsSubmerged.cs`.
- 기존 배치 함수 Walk/Plaza/Steps를 재사용한다. 런타임 코드와 폴링 추가 없음.
- 메뉴 `Tools > Checkmate > Background > Add Submerged Ruins Modules`는 신규 그룹이 없는 BattleScene에 한 번만 적용한다. 기존 그룹이 있으면 중복 생성을 거부한다. 기존 모듈 에셋은 덮어쓰지 않는다.
- 원본 배치 재생성 메뉴나 FinishJoints를 실행할 필요 없음.

## 검증

- Unity 6000.3.10f1 연결 프로젝트 경로 확인, 컴파일 오류 0.
- Edit Mode CheckmateRPG.Tests 179/179 통과 (job 945fca7e24354e568d77f3bb2c41a370).
- BattleScene Play 진입, 모듈 40개 유지 및 카메라 렌더 확인, 당시 오류/경고 0, Play 종료.
- 전체 전투 상호작용 및 성능 벤치마크는 미실행. 배경 검증용 카메라 캡처이며 UI 포함 캡처는 아님.
- 추가 그룹 Collider 0, MonoBehaviour 0. 전투판 가림 없음.
- 적용 직전 백업과 저장 씬의 기존 YAML 문서 비교: 기존 요소 삭제/변경 없음. SceneRoots 목록에 신규 그룹만 추가.
- 이미지: `Captures/CombatRuins/submerged-after.png`, `submerged-detail.png`, `submerged-top.png`, `submerged-play.png`.
- 백업: `Captures/CombatRuins/submerged-backup-20261002-070315.unity`.
