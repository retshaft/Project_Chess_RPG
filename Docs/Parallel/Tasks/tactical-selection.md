# 기물 선택 중심 조작
- 상태: Ready
- 담당: Codex 메인 / 원본 codex/integration / 현재 작업 내용 보존.
- 허용: Core/BattleManager.cs, Core/BattleCameraDirector.cs, UI/BattleHudView.cs, Testing/EditModeTests/CameraUIRegressionTests.cs 및 메타, Docs/Parallel, Docs/TACTICAL_SELECTION.md.
- 범위: 선택 확대·감속·스테이터스와 일체형 명령창, 명령 성공/취소/사망/전투 종료 시 원상 복구. 기존 행동 처리 재사용.
- 금지: 씬/배경/물/적 데이터/경제 수정, 다른 작업 변경 폐기.
- 검증: Unity 컴파일·회귀·정식 경로 Play. 질문 목록은 별도 문서에 기록.
