# BorderBarrierGenerator 발광 결계 연결

- 상태: Ready
- 담당: Codex (메인 직접 작업)
- 브랜치: codex/integration
- 워크트리: C:/PersonalFiles/2026/Task/Unity/Graduation_Project/Graduation_Project_v01
- 기준: 기존 미커밋 변경을 보존한 현재 작업 트리

## 목표와 완료 조건

`BorderBarrierGenerator` 네 개를 보드 경계의 기준점으로 사용해, 모델의 발광 라인과 어울리는 얇은 결계를 연결한다. 결계는 낮은 발광 코어선과 희미한 외곽선으로 구성해 기물·타일을 덮지 않아야 한다. `BattleScene`에서 네 변이 닫힌 형태로 보이고 컴파일 오류와 새 런타임 예외가 없어야 한다.

## 허용 파일

후속 요청: 얇은 선에 반투명 에너지 장벽 면, 흐르는 격자와 파동을 추가한다. 카메라 쪽 면은 자동으로 약해져 보드 가독성을 유지한다.

- `Assets/Environment/CombatRuins/Shaders/BoardEnergyBarrier.shader` 및 `.meta`
- `Assets/Environment/CombatRuins/Materials/Board Barrier - Energy Surface.mat` 및 `.meta`

- `Assets/Scripts/Editor/StylizedBackground/CombatRuinsArt.cs`
- `Assets/Environment/CombatRuins/Materials/Board Barrier - Emission Core.mat`
- `Assets/Environment/CombatRuins/Materials/Board Barrier - Emission Glow.mat`
- `Assets/Scenes/Test/BattleScene.unity`
- 이 작업서 및 검증 기록

## 금지 및 경계

전투 규칙, 입력, 카메라, 보드 타일 배치와 기존 사용자 변경은 수정하지 않는다. 큰 불투명 벽면이나 보드 상부를 가리는 구조물은 추가하지 않는다.

## 검증

Unity 실행권을 확보한 상태에서 `BattleScene`을 대상으로 에디터 빌드/콘솔 확인과 Play Mode 화면 확인을 수행한다. 확인 후 반드시 Play Mode를 종료하고 실행권을 반납한다.

## 결과 제출

완료 결과와 미검증 항목은 최종 응답에 기록한다.

## 에너지 장벽 후속 검증 — 2026-10-02

- 네 변에 높이 2.4m의 반투명 에너지 면 추가. 격자·상승 파동·상단 소멸, 카메라 쪽 면의 발광량 12% 적용.
- 기존 생성기와 코어선 연결 유지. 에너지 면 Collider 0개, 그림자 및 깊이 쓰기 없음.
- Unity MCP로 대상 프로젝트 경로 확인 후 씬 생성·저장. 셰이더 컴파일 메시지 0건.
- BattleScene Play에서 정방향 및 반대 방향 렌더 비교. 결계 활성/비활성 비교에서 보드 가독성 확인. 기물 이동·공격 상호작용은 이번 렌더 검증에 포함하지 않음.
- CheckmateRPG.Tests: 179/179 통과, job `1efa4021ed5f4d80935b7014b2d1787d`.
- Play 종료 후 콘솔 오류 0건 확인.
- 캡처: `Captures/CombatRuins/energy-barrier-play.png`, `energy-barrier-reverse.png`, `energy-barrier-disabled.png`.

## 육각형·높이 후속 검증 — 2026-10-02

- 사용자 요청에 따라 정육각형 벌집 무늬로 변경하고 네 면 높이를 2.4m에서 4m로 증가. 월드 좌표 무늬를 사용해 높이에 따라 육각형이 늘어나지 않도록 유지.
- 카메라 쪽 면의 흐림, 상승 파동과 상단 소멸 유지. BattleScene 재생에서 정방향·반대 방향 캡처로 보드 가독성 확인.
- 네 면의 실제 높이 4m 확인. 컴파일 완료, 셰이더 오류 및 재생 중·종료 후 콘솔 오류 0건. 씬 저장 및 Play Mode 종료 확인.
- 이번 시각 효과 수정에서는 Edit Mode 테스트와 기물 이동·공격 상호작용을 재실행하지 않음. 위 179/179 기록은 이전 검증 결과임.
- 캡처: `Captures/CombatRuins/energy-barrier-hex-4m.png`, `energy-barrier-hex-4m-reverse.png`.
