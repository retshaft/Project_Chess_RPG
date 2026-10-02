# UI 레이어와 Cinemachine 조작
- 상태: Review
- 담당: Codex 메인, 원본 codex/integration
- 기준: ef13042. 기존 폰트 변경 및 COMBAT_BACKDROP_AND_CAMERA_PLAN.md 보존.
- 허용: Core/BattleCameraDirector.cs, Core/BattleManager.cs, Input/PlayerInputController.cs, UI/UILayers.cs, UI/BattleHudView.cs, UI/CheckmateCinematicUI.cs, UI/SkillCutInUI.cs, UI/PlayerAPBar.cs, UI/BattleResultUI.cs, UI/KingSynchroUI.cs, UI/EconomyDialog.cs, UI/DeckBuilderUI.cs, UI/StageSelectUI.cs, UI/EdictSelectorModal.cs, Testing/EditModeTests/CameraUIRegressionTests.cs 및 meta; Docs/Parallel 및 질문 기록; Captures/CameraUI.
- 범위: 카메라 팬/회전/줌/복귀/포커스, Cinemachine 전환, 짧은 시작 연출과 입력 차단, UI 정렬 및 중복 캔버스/팝업 관통 수정.
- 금지: 카메라 계획서의 배경 아트 작업, 수치 규칙 변경, 기존 작업/사용자 저장 되돌리기.
- 검증: Unity 컴파일/회귀/공식 씬 Play, 테스트 종료 및 실행권 반환.
- 어셈블리 연결: Assets/Scripts/CheckmateRPG.asmdef에 이미 설치된 Unity.Cinemachine 참조 추가 허용.
- 배치 드래그 고스트의 텍스트 입력 가로채기 수정: Testing/UI/DraggableDeploymentButton.cs 허용.
